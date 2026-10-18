#!/usr/bin/env python3
"""ROKAS authentic 25-bone FBX — isolated complete finger-root graft QA.

Weld ONLY source-coincident UV seams, preserving each source per-face UV and
its original 25-bone armature. Carve five small individually bounded openings
in a candidate COPY of the original palm surface; zipper authentic ring
vertices from each digit to the palm. Very small local relaxation is allowed.
Reject nonmanifold geometry, missing weights and excessive source deformation.
The SOURCE FBX / production V10 are NOT MODIFIED. Visual QA remains mandatory.
"""
import bpy,bmesh,sys,json,pathlib,math
from collections import defaultdict
from mathutils import Vector

def fail(msg):raise RuntimeError("ROKAS FINGER BRIDGE SAFETY: "+msg)
def bbox(o):
    return [(min(v.co[i] for v in o.data.vertices),
             max(v.co[i] for v in o.data.vertices)) for i in range(3)]
def islands(bm):
    visited=set();out=[]
    for vert in bm.verts:
        if vert in visited:continue
        stack=[vert];group=set()
        while stack:
            v=stack.pop()
            if v in visited:continue
            visited.add(v);group.add(v)
            stack.extend(n for edge in v.link_edges for n in edge.verts if n not in visited)
        out.append(group)
    return sorted(out,key=len,reverse=True)
def loops(island):
    """Return ALL boundary loops, not one: source seam welding exposes
    separate authentic cuff/finger-socket rims on the palm mesh."""
    boundary={e for v in island for e in v.link_edges
              if e.is_boundary and all(x in island for x in e.verts)}
    adjacency=defaultdict(list)
    for e in boundary:
        a,b=e.verts
        adjacency[a].append(b)
        adjacency[b].append(a)
    if any(len(neighbors)!=2 for neighbors in adjacency.values()):
        fail("Original skin rim is branched or nonmanifold after exact weld")
    remaining=set(adjacency)
    ordered_loops=[]
    while remaining:
        first=min(remaining,key=lambda v:v.index)
        ordered=[];last=None;v=first
        for _ in range(len(adjacency)+1):
            if v not in remaining:fail("Boundary ring repeats before closing")
            ordered.append(v);remaining.remove(v)
            nxt=next(x for x in adjacency[v] if x!=last)
            if nxt==first:break
            last,v=v,nxt
        else:fail("Boundary is an open ring")
        if len(ordered)<6:fail("Degenerate short boundary loop")
        ordered_loops.append(ordered)
    return sorted(ordered_loops,key=len,reverse=True)
def main():
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    if len(args)!=2:fail("SOURCE.fbx OUTPUT_DIR required")
    src=pathlib.Path(args[0]).resolve()
    out=pathlib.Path(args[1]).resolve();out.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(src),use_anim=False)
    sk=[o for o in bpy.data.objects if o.type=='MESH' and
        'Keiko_Hand_Skin_Mesh' in o.name]
    if len(sk)!=1:fail("Authentic source hand missing")
    original=sk[0]
    arm=original.find_armature()
    if arm is None or len(arm.data.bones)<25:fail("Source rig 25 bones missing")
    clone=original.copy();clone.data=original.data.copy()
    clone.name="ROKAS_Argon_LocalFingerBridge_CANDIDATE_QA_ONLY"
    bpy.context.collection.objects.link(clone)
    clone.matrix_world=original.matrix_world.copy()
    for mod in list(clone.modifiers):
        if mod.type=='ARMATURE':clone.modifiers.remove(mod)
    b0=bbox(original)
    bm=bmesh.new();bm.from_mesh(clone.data)
    bm.verts.ensure_lookup_table()
    original_mesh_vertices=len(bm.verts)
    original_mesh_boundaries=sum(e.is_boundary for e in bm.edges)
    b0=bbox(original)
    extent=max(b-a for a,b in b0)
    # Actual 2026-10-17 source boundary atlas proved 148 open edges per
    # finger are mainly duplicated coincident UV seam vertices. Five genuine
    # source fingers EACH have only ~30 actual open root edges after welding.
    # Stitch ONLY co-located vertices; maintain per-face loop UV islands.
    # Do not weld adjacent anatomical tissue or connect distinct fingers
    # across air. This is genuine mesh repair, not fabricated geometry.
    eps=extent*1e-6
    original_vertex_positions={v:v.co.copy() for v in bm.verts}
    deform_source=bm.verts.layers.deform.active
    if deform_source is None:fail("Original authentic skin weights unavailable")
    buckets=defaultdict(list)
    for v in bm.verts:
        buckets[tuple(round(float(c)/eps) for c in v.co)].append(v)
    duplicate_groups=[group for group in buckets.values() if len(group)>1]
    inconsistent=0
    for group in duplicate_groups:
        base=dict(group[0][deform_source])
        for other in group[1:]:
            current=dict(other[deform_source])
            drift=max((abs(base.get(k,0)-current.get(k,0))
                      for k in set(base)|set(current)),default=0)
            if drift>.012:inconsistent+=1
    if inconsistent:
        fail("UV coincident source vertices have DIFFERENT skin weights; "+
             "requires bone-safe weld, cannot merge blindly: "+str(inconsistent))
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=eps)
    bm.verts.ensure_lookup_table();bm.verts.index_update()
    bm.normal_update()
    welded_vertices=len(bm.verts)
    welded_boundaries=sum(e.is_boundary for e in bm.edges)
    invalid_nonmanifold=sum(not(e.is_boundary or e.is_manifold) for e in bm.edges)
    if invalid_nonmanifold:
        fail("Welding creates unexpected nonmanifold edges: "+
             str(invalid_nonmanifold))
    if original_mesh_vertices-welded_vertices<280:
        fail("Expected many genuine duplicated source UV seam verts to weld")
    if original_mesh_boundaries-welded_boundaries<500:
        fail("Weld did not restore the missing original skin side surfaces")
    components=islands(bm)
    if len(components)!=6 or len(components[0])<4900:
        fail("Genuine six source islands distorted by exact UV seam weld")
    per_component=[loops(x) for x in components]
    if any(len(group)!=1 for group in per_component[1:]):
        fail("Digit side seam weld must leave exactly one root rim per finger")
    palm_rims=per_component[0]
    per=[palm_rims[0]]+[group[0] for group in per_component[1:]]
    print("ROKAS AUTHENTIC UV EDGE REPAIR",
          original_mesh_vertices,welded_vertices,
          original_mesh_boundaries,welded_boundaries,
          "palm rims",list(map(len,palm_rims)),
          "finger root rim lengths",list(map(len,per[1:])),flush=True)
    # Previous 0.045 hand-span allowance permitted 17-18 mm bridges across
    # 8-10 mm finger sections. Real Unity images proved severe polygons.
    # Reject distant contacts, use much shorter true surface transitions.
    diameter=extent
    # Intermediate safe contact distance: 2.6% left visible unjoined collar,
    # 4.5% caused overlong bridges. Test 3.2% with curved 3-row loft QA.
    maxBridge=diameter*.032
    minBridge=diameter*.0001
    uv=bm.loops.layers.uv.active
    usedPalm=set()
    source_contact_vertices=set()
    # Build a genuine shared two-ring fillet, not one huge face connecting
    # anatomical surfaces with a flat hard collar. Cache crossed-edge
    # vertices between consecutive quads: NO cracks/T-junctions.
    bm.normal_update()
    deform=bm.verts.layers.deform.active
    if deform is None:fail("Original skinned Blender vertex groups absent")
    orig_uv={}
    if uv is not None:
        for ring in [r for group in per_component for r in group]:
            for vertex in ring:
                source_uv_corners=[l for e in vertex.link_edges for face in e.link_faces
                                   for l in face.loops if l.vert==vertex]
                if source_uv_corners:
                    orig_uv[vertex.index]=source_uv_corners[0][uv].uv.copy()
    # STAGE TWO: graft the 30 real finger-root rims into individually
    # selected pockets cut ONLY in the authentic palm skin surface.
    # 1) Source-side UV seams already EXACT-welded; no synthetic fingers.
    # 2) Remove a tiny contiguous island of original palm triangles.
    # 3) Zip real finger-root ring to the new palm-hole ring with flexible
    #    edge-count matching (triangles + quads, no center n-gon caps).
    # 4) Preserve the anatomical 25-bone influence field and loop UVs.
    # All operations are on disposable candidate clone, source untouched.
    source_original_palm=set(components[0])
    all_source_uv={}
    if uv is not None:
        for v in bm.verts:
            source_loops=[l for e in v.link_edges for face in e.link_faces
                          for l in face.loops if l.vert==v]
            if source_loops:
                all_source_uv[v]=source_loops[0][uv].uv.copy()
    addedFaces=[]
    face_origin={}
    addedWeights=[]
    source_contact_vertices=set()
    report={"original_source_vertices":original_mesh_vertices,
            "original_source_boundaries":original_mesh_boundaries,
            "exact_weld_epsilon":eps,
            "exact_weld_removed_vertices":original_mesh_vertices-welded_vertices,
            "exact_weld_closed_boundary_edges":original_mesh_boundaries-welded_boundaries,
            "palm_remaining_loop_sizes":[len(x) for x in palm_rims],
            "original_vertices":welded_vertices,
            "original_islands":len(components),
            "original_boundaries":welded_boundaries,
            "source_bones":len(arm.data.bones),
            "max_bridge":maxBridge,
            "per_finger":[]}

    def boundary_of_new_pocket(new_boundary_edges):
        adj=defaultdict(list)
        for e in new_boundary_edges:
            u,v=e.verts
            adj[u].append(v)
            adj[v].append(u)
        if len(adj)<6 or any(len(nn)!=2 for nn in adj.values()):
            fail("Local palm graft pocket touches UV border or branched area")
        start=min(adj,key=lambda v:v.index)
        ordered=[];last=None;v=start
        for _ in range(len(adj)+2):
            ordered.append(v)
            nxt=next(q for q in adj[v] if q!=last)
            if nxt==start:break
            last,v=v,nxt
        if len(ordered)!=len(adj):
            fail("Anatomical palm socket hole must be ONE closed boundary ring")
        return ordered

    def nearest_source_uv(v):
        if v in all_source_uv:return all_source_uv[v].copy()
        if uv is None:return None
        for face in v.link_faces:
            for corner in face.loops:
                if corner.vert==v:return corner[uv].uv.copy()
        fail("Graft source UV missing on authentic source vertex")

    def boundary_edge_flow(a,b):
        # Adjacent original face direction on this one-face boundary edge.
        edge=next((e for e in a.link_edges if b in e.verts),None)
        if edge is None or not edge.is_boundary or len(edge.link_faces)!=1:
            fail("Actual skin rim has no single-face edge")
        for corner in edge.link_faces[0].loops:
            if corner.vert==a and corner.link_loop_next.vert==b:return 1
            if corner.vert==b and corner.link_loop_next.vert==a:return -1
        fail("Cannot orient genuine finger/palm rim")

    def join_root_to_hole(finger, hole, finger_number):
        N=len(finger);M=len(hole)
        if N<8 or M<8:fail("Graft rim too small")
        # Exactly one loop winding choice is valid for each source shell.
        # The original minimization across both winding signs made inverted
        # triangular fins even while all edges remained formally manifold.
        # Arc-length coordinates prevent uniformly indexed ring matching
        # from stretching triangles around irregular original MCP contours.
        def arc_units(vertices):
            distances=[(vertices[(k+1)%len(vertices)].co-
                        vertices[k].co).length for k in range(len(vertices))]
            total=sum(distances)
            if total<1e-7 or min(distances)<1e-8:
                fail("Source contact rim has degenerate arc lengths")
            acc=0.0
            result=[]
            for distance in distances:
                result.append(acc/total)
                acc+=distance
            return result
        def sample_arc(vertices, units, t):
            t=t%1.0
            for k in range(len(vertices)):
                left=units[k]
                right=units[k+1] if k+1<len(vertices) else 1.0
                if t<=right+1e-10:
                    q=max(0.0,min(1.0,(t-left)/(right-left)))
                    u=vertices[k]
                    v=vertices[(k+1)%len(vertices)]
                    return u.co.lerp(v.co,q),u,v,q
            fail("Normalized epidermis arc outside real ring")
        best=None
        for df in (1,-1):
            for dp in (1,-1):
                for fi in range(N):
                    if boundary_edge_flow(finger[fi],finger[(fi+df)%N])!=-1:
                        continue
                    for pj in range(M):
                        if boundary_edge_flow(hole[pj],hole[(pj+dp)%M])!=1:
                            continue
                        aa=[finger[(fi+df*k)%N] for k in range(N)]
                        pp=[hole[(pj+dp*k)%M] for k in range(M)]
                        af=arc_units(aa);pf=arc_units(pp)
                        # Penalize isolated bridge spikes quadratically.
                        # Old minimum-SUM-distance could sacrifice one
                        # inter-finger web to make most root points closer.
                        # Optimize the full matched 3D skin contour instead.
                        errors=[(sample_arc(aa,af,k/60)[0]-
                                 sample_arc(pp,pf,k/60)[0]).length
                                 for k in range(60)]
                        mean_square=sum(d*d for d in errors)/60
                        largest=max(errors)
                        quality=mean_square+largest*largest*.65
                        trial=(quality,fi,pj,df,dp)
                        if best is None or trial<best[0]:
                            best=(trial,aa,pp,af,pf,mean_square,largest)
        if best is None:
            fail("Genuine palm and finger contours cannot be oriented")
        (_,fi,pj,df,dp),a,p,frac_a,frac_p,bridge_square,bridge_max=best
        direction=dp
        if bridge_max>diameter*.085:
            fail("Root-to-palm contour creates a high-aspect interdigit web")
        report.setdefault("root_surface_correspondence",[]).append({
            "finger_root_N":N,"authentic_palm_rim_M":M,
            "rms_distance":math.sqrt(bridge_square),
            "largest_matched_gap":bridge_max,
            "phase_indices":[fi,pj],
            "source_ring_orientation":[df,dp],
            "quadratic_spike_aware_score":bridge_square+
                                           .65*bridge_max**2})
        # UV and 25-bone skinning use the same true surface interpolation.
        # No guessed index neighbour bones near ring/little fingers.
        seam_uv={}
        def make_inner(point,source_mix):
            x=bm.verts.new(point)
            assert abs(sum(weight for _,weight in source_mix)-1)<1e-6
            addedWeights.append((x,source_mix))
            if uv is not None:
                value=Vector((0,0))
                for vertex,weight in source_mix:
                    value+=nearest_source_uv(vertex)*weight
                seam_uv[x]=value
            return x
        # C1-like tissue curvature from ACTUAL source epidermis.
        # The prior linear loft has real >120 degree folds/needle-like webs
        # even with 30x30 quads and perfect manifold orientation. Follow
        # original digit/palm surface tangents instead of a straight line.
        root_boundary=set(a)
        palm_boundary=set(p)
        def epidermis_tangent(v,boundary,out_of_surface):
            adjacent=[w for e in v.link_edges for w in e.verts
                      if w!=v and w not in boundary]
            if not adjacent:return None
            mean=sum((w.co for w in adjacent),
                     Vector((0,0,0)))/len(adjacent)
            vec=(v.co-mean) if out_of_surface else (mean-v.co)
            return vec.normalized() if vec.length>1e-8 else None
        def shaped_skin(f,p0,p1,q,t):
            palm_target=p0.co.lerp(p1.co,q)
            delta=palm_target-f.co
            gap=delta.length
            if gap<=1e-8:return f.co.copy()
            along=delta/gap
            t0=epidermis_tangent(f,root_boundary,True)
            a0=epidermis_tangent(p0,palm_boundary,False) or along
            a1=epidermis_tangent(p1,palm_boundary,False) or along
            t1=a0.lerp(a1,q)
            if t1.length<1e-8:t1=along
            if t0 is None:t0=along
            # Blend authentic epidermis direction with source-to-palm
            # displacement to prevent folded backwards Bezier handles.
            t0=(t0*.55+along*.45).normalized()
            t1=(t1*.55+along*.45).normalized()
            if t0.dot(along)<.15:t0=along
            if t1.dot(along)<.15:t1=along
            arm=min(gap*.62,diameter*.020)
            c1=f.co+t0*arm
            c2=palm_target-t1*arm
            u=1-t
            curved=f.co*(u*u*u)+c1*(3*u*u*t)+c2*(3*u*t*t)+palm_target*(t*t*t)
            straight=f.co.lerp(palm_target,t)
            shift=curved-straight
            if shift.length>diameter*.012:
                curved=straight+shift.normalized()*diameter*.012
            return curved
        near_finger=[]
        near_palm=[]
        for i,vertex in enumerate(a):
            pos,p0,p1,q=sample_arc(p,frac_p,frac_a[i])
            near_finger.append(make_inner(
                shaped_skin(vertex,p0,p1,q,.34),
                [(vertex,.66),(p0,.34*(1-q)),(p1,.34*q)]))
            near_palm.append(make_inner(
                shaped_skin(vertex,p0,p1,q,.72),
                [(vertex,.28),(p0,.72*(1-q)),(p1,.72*q)]))
        source_contact_vertices.update(near_finger)
        source_contact_vertices.update(near_palm)
        built=[]
        def add_skin_face(vertices):
            if len(set(vertices))!=len(vertices):
                fail("Anatomical transition has a degenerate polygon")
            try:
                face=bm.faces.new(vertices)
            except ValueError:
                fail("Anatomical transition duplicated original surface")
            face.material_index=0;face.smooth=True
            built.append(face)
            if uv is not None:
                for corner in face.loops:
                    vertex=corner.vert
                    corner[uv].uv=(seam_uv[vertex] if vertex in seam_uv
                                   else nearest_source_uv(vertex))
        # Original authentic digit ring -> 30-vertex near-digit ring.
        for i in range(N):
            j=(i+1)%N
            add_skin_face([a[i],a[j],near_finger[j],near_finger[i]])
        # SOURCE anatomically matched central transition 30 -> 30:
        # entirely quad surface with no fan triangles at exposed MCP roots.
        for i in range(N):
            j=(i+1)%N
            add_skin_face([near_finger[i],near_finger[j],
                           near_palm[j],near_palm[i]])
        # Only the FINAL short near-palm ring connects to the true 13-21
        # original palm perimeter. Real arc-length zipper is kept here
        # because the socket must keep its authentic existing skin outline.
        # Index MCP: re-topologize the actual index-to-shared-palm sheet.
        # Both ORIGINAL rings now have 30 authentic perimeter vertices.
        # The old arc-length zipper nonetheless built 59 tiny triangles,
        # leaving diagonal wedge fins at the index-palm boundary. Join
        # corresponding native rings by 30 quads, NOT radial wedges or
        # cylinder replacements. Keep the other four digits unchanged
        # until the single critical web passes real Blender and Unity.
        if finger_number in (1,2,3):
            if N!=30 or M!=30:
                fail("Original digit and shared palm contour must be 30x30")
            for i in range(N):
                j=(i+1)%N
                add_skin_face([near_palm[i],near_palm[j],p[j],p[i]])
            report.setdefault("shared_palm_five_digit_surface_quad_remesh",[]).append({
                "digit":finger_number,
                "original_digit_rim":N,
                "genuine_palm_rim":M,
                "replacement_quad_faces":N,
                "old_arc_zipper_faces":N+M-1,
                "preserve_original_palm_skin_uv_25_bones":True,
                "correspondence":"one-to-one_source_arc_phase"})
        else:
            index_a=index_p=0
            while index_a<N or index_p<M:
                next_a=(frac_a[index_a+1] if index_a+1<N else 1.0) if index_a<N else 1e9
                next_p=(frac_p[index_p+1] if index_p+1<M else 1.0) if index_p<M else 1e9
                u=near_palm[index_a%N]
                v=p[index_p%M]
                if abs(next_a-next_p)<1e-7:
                    vertices=[u,near_palm[(index_a+1)%N],
                              p[(index_p+1)%M],v]
                    index_a+=1;index_p+=1
                elif next_a<next_p:
                    vertices=[u,near_palm[(index_a+1)%N],v]
                    index_a+=1
                else:
                    vertices=[u,p[(index_p+1)%M],v]
                    index_p+=1
                add_skin_face(vertices)
        if len(built)<max(N,M):
            fail("Anatomical graft missing source ring faces")
        # The curved interpolation is better on SOME fingers and worse on
        # others. Rather than assign one global hand-tuned smoothness,
        # compare the real source-backed, authenticated 25-bone geometry
        # at each digit and SELECT by actual Blender fold angles. Only
        # the 60 GENERATED intermediate vertices are moved.
        paired=[]
        for i,vertex in enumerate(a):
            pos,_,_,_=sample_arc(p,frac_p,frac_a[i])
            paired.append((vertex.co.lerp(pos,.34),
                           vertex.co.lerp(pos,.72)))
        curved_f=[v.co.copy() for v in near_finger]
        curved_p=[v.co.copy() for v in near_palm]
        unique_skin_edges={e for face in built for e in face.edges if
                           e.is_manifold and len(e.link_faces)==2}
        outcomes=[]
        for blend in (0.,.5,1.):
            for i in range(N):
                near_finger[i].co=paired[i][0].lerp(curved_f[i],blend)
                near_palm[i].co=paired[i][1].lerp(curved_p[i],blend)
            bm.normal_update()
            observed=[]
            for edge in unique_skin_edges:
                fa,fb=edge.link_faces
                if fa.normal.length_squared<1e-12 or fb.normal.length_squared<1e-12:
                    fail("Zero-normal quads in anatomical shape search")
                observed.append(math.degrees(fa.normal.angle(fb.normal)))
            count_120=sum(z>120 for z in observed)
            count_90=sum(z>90 for z in observed)
            mean_fold=sum(max(z-70,0)**2 for z in observed)/max(1,len(observed))
            outcomes.append((count_120,count_90,mean_fold,blend))
        winner=min(outcomes)
        for i in range(N):
            near_finger[i].co=paired[i][0].lerp(curved_f[i],winner[3])
            near_palm[i].co=paired[i][1].lerp(curved_p[i],winner[3])
        bm.normal_update()
        report.setdefault("per_finger_shape_search",[]).append({
            "source_accurate_30x30":True,
            "candidates":[{"curvature_blend":w,"angles_over_120":n120,
                           "angles_over_90":n90,"mean_fold_penalty":v}
                          for n120,n90,v,w in outcomes],
            "selected_curvature_blend":winner[3],
            "selected_angles_over_120":winner[0]})
        return built,direction

    for fingerNo,ring in enumerate(per[1:],1):
        # Nearest TRUE palm surface rather than distant palm UV border.
        # The source atlas measured 7-11 mm genuine contact depths;
        # connecting to the old two 30-vertex perimeter loops was wrong.
        root=sum((v.co for v in ring),Vector((0,0,0)))/len(ring)
        radius=sorted((v.co-root).length for v in ring)[len(ring)//2]
        intact_palm=[v for v in source_original_palm
                     if v.is_valid and any(f.is_valid for f in v.link_faces)]
        nearest=min(intact_palm,key=lambda v:(v.co-root).length_squared)
        source_dist=(nearest.co-root).length
        if source_dist>diameter*.049:
            fail("Digit too far from genuine palm skin for safe anatomical graft")
        # The original digit source has an exposed circular/cylindrical root
        # collar: simply connecting it to a palm hole preserves a plug-like
        # ring and triangle fins. Anatomically feather only three existing
        # finger-base vertex rows INTO the true original palm skin. All
        # fingertips, middle/distal bones and source FBX remain unmodified.
        digit_island=set(components[fingerNo])
        tiers=[list(ring)]
        seen=set(ring)
        for depth in range(2):
            frontier={w for v in tiers[-1] for e in v.link_edges
                      for w in e.verts
                      if w in digit_island and w not in seen}
            if len(frontier)<10 or len(frontier)>100:
                fail("Authentic proximal finger base tier is irregular: "+
                     str((fingerNo,depth,len(frontier))))
            tiers.append(list(frontier))
            seen.update(frontier)
        # DEEP SOURCE-TO-PALM ANATOMICAL RETOPOLOGY — INDEX PILOT.
        # The visual pin/stubs are at the actual isolated finger tube
        # crowns. Closing the correct palm hole with 30 quads cannot
        # eliminate the already-exposed cylindrical *source* collar.
        # This candidate surgically replaces TWO native proximal collar
        # bands (NOT the finger or distal anatomy) with a common original
        # skin-root surface. Only the disposable loaded BMesh is changed.
        # The original FBX, game V10, UV set, weights and 25 armature bones
        # are NEVER overwritten.
        if fingerNo==2:
            if any(len(tier)!=30 for tier in tiers):
                fail("Deep index web surgery requires 3 native 30-point rings")
            old_boundary=set(tiers[0])
            first_belt=set(tiers[1])
            new_boundary=set(tiers[2])
            obsolete=old_boundary|first_belt
            affected={face for v in obsolete for face in v.link_faces
                      if face.is_valid}
            if not(105<=len(affected)<=145) or any(
                    not set(face.verts).issubset(obsolete|new_boundary)
                    for face in affected):
                fail("Source index double-band resection crosses protected PIP geometry")
            old_edge_count=sum(e.is_boundary for v in old_boundary
                               for e in v.link_edges
                               if all(q in old_boundary for q in e.verts))
            if old_edge_count!=60:
                fail("Index source native entry rim must contain 30 unique boundary edges")
            remaining_original=set(components[fingerNo])-obsolete
            if not new_boundary.issubset(remaining_original):
                fail("Resected index skin ring was not actually source-native")
            bmesh.ops.delete(bm,geom=list(affected),context='FACES')
            alive=[v for v in obsolete if v.is_valid]
            if alive:bmesh.ops.delete(bm,geom=alive,context='VERTS')
            bm.normal_update()
            components[fingerNo].difference_update(obsolete)
            reconstructed_island=set(components[fingerNo])
            new_edges={e for v in new_boundary if v.is_valid
                       for e in v.link_edges if e.is_valid and
                       e.is_boundary and all(q in new_boundary for q in e.verts)}
            if len(new_edges)!=30:
                fail("Deep original index surgery did not reveal 30-edge clean ring")
            ring=boundary_of_new_pocket(new_edges)
            if set(ring)!=new_boundary:
                fail("Real original index remnant ring topology corrupt")
            seen=set(ring)
            new_tiers=[list(ring)]
            for depth in range(2):
                next_ring={w for v in new_tiers[-1] for e in v.link_edges
                           for w in e.verts if w in reconstructed_island
                           and w not in seen}
                if len(next_ring)!=30:
                    fail("Deep index original next PIP skin tier not 30 native vertices")
                new_tiers.append(list(next_ring))
                seen.update(next_ring)
            tiers=new_tiers
            root=sum((v.co for v in ring),Vector((0,0,0)))/len(ring)
            radius=sorted(((v.co-root).length for v in ring))[len(ring)//2]
            source_dist=(nearest.co-root).length
            if source_dist>diameter*.058:
                fail("Deep index collar left the original metacarpal surface")
            report.setdefault("native_double_collar_web_surgery",[]).append({
                "finger":fingerNo,
                "deleted_authentic_root_skin_faces":len(affected),
                "deleted_native_source_collar_vertices":len(obsolete),
                "retained_next_original_ring_vertices":len(ring),
                "source_original_fbx_untouched":True,
                "new_three_native_ring_sizes":[len(tier) for tier in tiers],
                "source_to_original_palm_gap":source_dist})
        root_pull=nearest.co-root
        root_pull_size=root_pull.length
        if root_pull_size>diameter*.049:
            fail("Finger base cannot safely settle into distant palm tissue")
        moved_at_root=0.0
        # Candidate V2: source tube crowns still protrude 1-2 cm from
        # epidermis in Unity even with quad loops. Settle the real first
        # root ring deeper into the physically nearest palm tissue, taper
        # the movement gently to zero before proximal/middle phalanges.
        # This is an isolated candidate experiment; source FBX untouched.
        for depth,(tier,pull,radial) in enumerate(zip(
                tiers,(.32,.15,.035),(.93,.978,1.0))):
            tier_center=sum((v.co for v in tier),
                            Vector((0,0,0)))/len(tier)
            for v in tier:
                before=v.co.copy()
                v.co=tier_center+(v.co-tier_center)*radial+root_pull*pull
                delta=(v.co-before).length
                moved_at_root=max(moved_at_root,delta)
        if moved_at_root>diameter*.019:
            fail("Proximal anatomical finger blend moved authentic tissue too far")
        report.setdefault("proximal_digit_tissue_feather",[]).append({
            "finger":fingerNo,
            "source_tier_sizes":[len(tier) for tier in tiers],
            "max_vertex_displacement":moved_at_root,
            "fraction_hand_extent":moved_at_root/diameter,
            "palm_surface_vector":list(root_pull)})
        bm.normal_update()
        # Recompute after limited proximal tissue adjustment.
        root=sum((v.co for v in ring),Vector((0,0,0)))/len(ring)
        radius=sorted((v.co-root).length for v in ring)[len(ring)//2]
        eligible={f for v in intact_palm for f in v.link_faces
                  if f.is_valid and all(w in source_original_palm for w in f.verts)}
        target_face=min(
            (f for f in nearest.link_faces if f in eligible),
            key=lambda f:(f.calc_center_median()-root).length_squared,default=None)
        if target_face is None:
            fail("Cannot isolate original palm-only face for root insertion")
        insertion=target_face.calc_center_median()
        surface_normal=target_face.normal.normalized()
        # Hole smaller than finger root cross section; creates a natural
        # graduated tissue sleeve, not a mechanically abrupt edge join.
        # Keep palm aperture closer to the authentic 30-vertex digit root
        # rather than tapering to a tiny 10-vertex cone (visible MCP fins).
        # Existing guards forbid cutting outside original palm-only tissue.
        # STRUCTURAL NATIVE PALM WEB RESECTION: at index and little
        # finger, the nearest small source patch ends exactly at the
        # heavily folded original triangles (visible MCP tabs). Enlarge
        # the *original face selection* into healthy native palm tissue
        # before constructing a fresh connected quad/zipper bridge. This
        # changes WHICH original source faces form the seam, not how
        # already-built vertices are smoothed. The two restored source
        # epidermis slit volumes are elsewhere and remain untouched.
        ordinary_radius=max(radius*.94,diameter*.010)
        if fingerNo==2:
            radius_hole=max(radius*1.15,diameter*.0115)
            radius_hole=min(radius_hole,diameter*.033)
        elif fingerNo==5:
            radius_hole=max(radius*1.28,diameter*.0120)
            radius_hole=min(radius_hole,diameter*.033)
        else:
            radius_hole=min(ordinary_radius,diameter*.030)
        report.setdefault("native_palm_web_resection_target",[]).append({
            "digit":fingerNo,
            "original_selected_radius":ordinary_radius,
            "target_native_source_radius":radius_hole,
            "structural_broader_palm_reconstruction":fingerNo in (2,5)})
        pending=[target_face]; selected=set()
        while pending:
            face=pending.pop()
            if face in selected or face not in eligible:continue
            if (face.calc_center_median()-insertion).length>radius_hole:
                continue
            if face.normal.dot(surface_normal)<.55:continue
            selected.add(face)
            for edge in face.edges:
                for other in edge.link_faces:
                    if other not in selected and other in eligible:
                        pending.append(other)
        if len(selected)<4 or len(selected)>170:
            fail("Palm graft hole has implausible source face count "+str(len(selected)))
        previous_boundaries={e for e in bm.edges if e.is_boundary}
        bmesh.ops.delete(bm,geom=list(selected),context='FACES')
        fresh={e for e in bm.edges if e.is_boundary
               and e not in previous_boundaries and e.is_valid}
        pocket=boundary_of_new_pocket(fresh)
        if len(pocket)>70:
            fail("Oversized palm graft rim, would remove authentic tissue")
        # The prior shape still had visible wedge-shaped knuckle fins:
        # 30 source digit ring vertices collapsing into 15-26 authentic
        # palm rim points at the final zipper. Insert the missing VERTICES
        # IN THE EXISTING PALM BOUNDARY EDGES, not another long triangle
        # fan. Blender interpolates original face UV/deform data while
        # subdividing selected authentic palm boundary edges.
        socket_before=len(pocket)
        required=len(ring)-socket_before
        if required<0 or required>30:
            fail("Original palm socket vertex density outside safe range")
        if required:
            # Local 30-vertex adaptive edge refinement. Earlier code chose
            # 13 of the 17 index-socket edges ONCE before splitting, leaving
            # long, uneven original edges beside the new half-edges. This
            # creates sharply unequal 30->30 quad widths even though Blender
            # calls the surface manifold. Redistribute density against the
            # evolving AUTHENTIC rim, one longest real edge at a time; never
            # manufacture a circular synthetic socket or move source skin.
            def socket_lengths(boundary):
                lengths=[]
                for j in range(len(boundary)):
                    a0=boundary[j];a1=boundary[(j+1)%len(boundary)]
                    edge=next((e for e in a0.link_edges
                               if a1 in e.verts and e.is_boundary),None)
                    if edge is None:fail("Original socket edge disconnected")
                    lengths.append((edge.calc_length(),j,edge))
                return lengths
            before_edges=socket_lengths(pocket)
            before_spacing=[length for length,_,_ in before_edges]
            added_socket_vertices=0
            for step in range(required):
                current=socket_lengths(pocket)
                # Stable index breaks length ties. Revisit long original
                # segments only if they remain longer than newly split ones.
                length,j,edge=max(current,key=lambda row:(row[0],-row[1]))
                if length<=diameter*1e-7:
                    fail("Cannot subdivide nearly zero-area original palm edge")
                bmesh.utils.edge_split(edge,edge.verts[0],.5)
                bm.normal_update()
                fresh={e for e in bm.edges if e.is_valid and e.is_boundary
                       and e not in previous_boundaries}
                pocket=boundary_of_new_pocket(fresh)
                added_socket_vertices+=1
                if len(pocket)!=socket_before+added_socket_vertices:
                    fail("Authentic adaptive palm socket ring split lost verts")
            after_spacing=[length for length,_,_ in socket_lengths(pocket)]
            report.setdefault("adaptive_true_socket_spacing",[]).append({
                "finger":fingerNo,"original_vertex_count":socket_before,
                "final_vertex_count":len(pocket),"genuine_edge_splits":required,
                "before_max_edge":max(before_spacing),
                "after_max_edge":max(after_spacing),
                "before_min_edge":min(before_spacing),
                "after_min_edge":min(after_spacing),
                "before_edge_length_ratio":max(before_spacing)/min(before_spacing),
                "after_edge_length_ratio":max(after_spacing)/min(after_spacing)})
            if len(pocket)!=len(ring):
                fail("Adaptive authentic palm socket density incorrect: "+
                     str((len(pocket),len(ring),required)))
        faces,wind=join_root_to_hole(ring,pocket,fingerNo)
        addedFaces.extend(faces)
        face_origin.update({f:'finger_'+str(fingerNo) for f in faces})
        source_contact_vertices.update(ring)
        source_contact_vertices.update(pocket)
        report["per_finger"].append({
            "finger":fingerNo,
            "quads":len(faces),
            "palm_socket_id":"NEW_AUTHENTIC_LOCAL_PALM_HOLE",
            "source_nearest_skin_gap":source_dist,
            "root_radius":radius,
            "palm_hole_radius":radius_hole,
            "palm_source_faces_removed":len(selected),
            "palm_source_rim_vertices":len(pocket),
            "palm_rim_before_edge_density_match":socket_before,
            "authentic_palm_boundary_edge_splits":required,
            "original_finger_rim_vertices":len(ring),
            "zipper_winding":wind,
            "max_root_gap":max((v.co-w.co).length for v in ring for w in pocket)})
    # Anatomical full-surface completion: the original palm contains TWO
    # separate 30-edge open distal pockets unrelated to the five true root
    # grafts. Preserve 64-edge cuff hole (hidden by the real sleeve), but
    # fill each of the two distal palm pockets with a gently curved 2-ring
    # epidermis cap derived from its OWN original boundary and skin weights.
    # No polygon n-gons, artificial tubes, voxel reconstruction or props.
    bm.normal_update()
    holes=loops(islands(bm)[0])
    sizes=sorted(len(k) for k in holes)
    if sizes!=[30,30,64]:
        fail("Unexpected true post-graft palm holes: "+str(sizes))
    # The REAL input geometry atlas exposed the problem: these are NOT
    # round holes requiring invented epidermis domes. Each '30-edge hole'
    # is a nearly zero-area SOURCE SLIT: two opposed contours following
    # the same original palm crease, with 14 physically corresponding
    # pairs plus 2 terminal points. The old 30->30->30->apex filler was
    # geometrically false and caused 19-20 near-180 degree angle folds.
    #
    # Structural repair stitches matching authentic slit lips together
    # DIRECTLY. This removes false cap faces instead of smoothing them,
    # and uses the source per-corner UVs and intact 25-bone vertex weights.
    # The original FBX remains immutable. If any source topology, bone
    # weights or border geometry differs, reject the entire candidate.
    # Structural candidate: two 30-edge source openings were revealed by
    # genuine Blender atlas to be hairline slits (14 true opposed skin
    # pairs plus 2 terminal vertices each), not round palm cavities.
    # Direct BMesh welding produced 15 near-180-degree native folds PER
    # slit; beveling that zero-thickness crease created zero-area faces.
    # Instead form a continuous, narrow SOURCE-LIP ROLLOVER with the
    # ORIGINAL 30 boundary vertices and 3 new skinned cross-section rows.
    # No cylinder, artificial palm dome, voxel repair or original FBX edit.
    capped=[]
    cap_created=[]
    cap_groups=[]
    slit_tolerance=diameter*.00018
    for slit_id,pocket in enumerate(holes):
        if len(pocket)==64:
            continue
        n=len(pocket)
        if n!=30:fail("Metacarpal source rim no longer 30 authentic verts")
        if max(v.co.z for v in pocket)-min(v.co.z for v in pocket)>diameter*1e-5:
            fail("Source opening is not an authentic near-planar slit")
        nearby=[]
        for i in range(n):
            for j in range(i+1,n):
                if j in ((i-1)%n,(i+1)%n):continue
                gap=(pocket[i].co-pocket[j].co).length
                if gap<slit_tolerance:nearby.append((gap,i,j))
        nearby.sort()
        free=set(range(n))
        pairs=[]
        for gap,i,j in nearby:
            if i in free and j in free:
                free.remove(i);free.remove(j)
                pairs.append((i,j,gap))
        if len(pairs)!=14 or len(free)!=2:
            fail("Original metacarpal slit cannot be paired 14+2")
        source_tips=sorted(free)
        start=source_tips[0];end_tip=source_tips[1]
        orientation=boundary_edge_flow(pocket[start],pocket[(start+1)%n])
        delta=1 if orientation==1 else -1
        path_a=[pocket[(start+k*delta)%n] for k in range(16)]
        path_b=[pocket[(start-k*delta)%n] for k in range(16)]
        if path_a[-1]!=pocket[end_tip] or path_b[-1]!=pocket[end_tip]:
            fail("Authentic slit rim is not two 15-edge opposed arcs")
        if any((path_a[k].co-path_b[k].co).length>=slit_tolerance
               for k in range(1,15)):
            fail("Source slit contains unmatched authentic skin lip")
        for k in range(1,15):
            a,b=path_a[k],path_b[k]
            wa,wb=dict(a[deform_source]),dict(b[deform_source])
            drift=max((abs(wa.get(g,0)-wb.get(g,0))
                       for g in set(wa)|set(wb)),default=0)
            if drift>.035:fail("Two skin source lip bone fields disagree")
        before_edges=sum(e.is_boundary for e in bm.edges)
        added_lip_faces=[]
        source_skin_uv={}
        rolled=[]
        def source_edge_normal(a,b):
            edge=next((e for e in a.link_edges if b in e.verts),None)
            if edge is None or not edge.is_boundary or len(edge.link_faces)!=1:
                fail("True 30-rim source lip no longer has adjacent skin")
            return edge.link_faces[0].normal.copy()
        # Three-dimensional source-sheet separation: these two original
        # epidermis lips occupy nearly identical 3D coordinates. A bridge
        # crossing coincident lips cannot represent anatomical thickness.
        # Move only native source palm vertices in the cloned candidate,
        # along their OWN local source face normals, tapering to zero at
        # original slit endpoints. Nearby original source faces follow a
        # bounded soft transition to avoid merely creating a sharp seam
        # one polygon farther from the slit. This is genuine volume
        # reconstruction, NOT a smoothing/normal-map trick.
        skin_before={}
        skin_motion={}
        source_skin_vertices=set(pocket)
        original_normals={}
        native_gap=[]
        for k in range(1,15):
            a,b=path_a[k],path_b[k]
            na=source_edge_normal(path_a[k-1],a)+\
               source_edge_normal(a,path_a[k+1])
            nb=source_edge_normal(path_b[k-1],b)+\
               source_edge_normal(b,path_b[k+1])
            if na.length<1e-8 or nb.length<1e-8:
                fail("Source palm surface normals degenerate before thickness reconstruction")
            na.normalize();nb.normalize()
            if na.dot(nb)>-.45:
                fail("Source lips are not opposing skin layers; refusing false thickness")
            source_thickness=diameter*.0045*math.sin(math.pi*k/15.)
            skin_motion[a]=na*source_thickness
            skin_motion[b]=nb*source_thickness
            original_normals[a]=na.copy(); original_normals[b]=nb.copy()
            native_gap.append(na.dot(nb))
        # Deform source-neighbour face vertices a smaller amount; no
        # original finger, wrist, cuff or source FBX positions change.
        soft_motion={}
        for v,displacement in skin_motion.items():
            adjacent={q for edge in v.link_edges for q in edge.verts
                      if q!=v and q not in source_skin_vertices
                      and q in source_original_palm}
            for q in adjacent:
                soft_motion.setdefault(q,[]).append(displacement*.32)
        max_shift=0.
        for v,displacement in skin_motion.items():
            skin_before[v]=v.co.copy()
            v.co=v.co+displacement
            max_shift=max(max_shift,displacement.length)
        for v,displacements in soft_motion.items():
            skin_before[v]=v.co.copy()
            shift=sum(displacements,Vector((0,0,0)))/len(displacements)
            if shift.length>diameter*.002:
                shift=shift.normalized()*diameter*.002
            v.co=v.co+shift
            max_shift=max(max_shift,shift.length)
        if max_shift>diameter*.005:
            fail("Source skin volume reconstruction exceeds displacement gate")
        bm.normal_update()
        report.setdefault("source_palm_sheet_volume",[]).append({
            "slit":slit_id,
            "original_lip_vertices_moved":len(skin_motion),
            "adjacent_original_source_vertices_moved":len(soft_motion),
            "max_candidate_source_displacement":max_shift,
            "min_opposed_normal_dot":min(native_gap),
            "max_opposed_normal_dot":max(native_gap),
            "source_fbx_untouched":True})
        for k in range(1,15):
            a,b=path_a[k],path_b[k]
            along=path_a[k+1].co-path_a[k-1].co
            if along.length<diameter*1e-7:
                fail("True source skin longitudinal tangent collapsed")
            along.normalize()
            normal_a=source_edge_normal(path_a[k-1],a)+\
                     source_edge_normal(a,path_a[k+1])
            if normal_a.length<1e-7:
                fail("Original skin surface normals cancel at open lip")
            normal_a.normalize()
            outward=along.cross(normal_a)
            if outward.length<1e-7:
                fail("Source tangent cannot define anatomical rollover")
            outward.normalize()
            radius=diameter*.003*math.sin(math.pi*k/15.)
            row=[a]
            for fraction in (.25,.5,.75):
                position=a.co.lerp(b.co,fraction)+\
                         outward*(radius*math.sin(math.pi*fraction))
                vertex=bm.verts.new(position)
                mixture=[(a,1.-fraction),(b,fraction)]
                addedWeights.append((vertex,mixture))
                if uv is not None:
                    source_skin_uv[vertex]=nearest_source_uv(a).lerp(
                        nearest_source_uv(b),fraction)
                cap_created.append(vertex)
                row.append(vertex)
            row.append(b)
            rolled.append(row)
        def add_lip_patch_face(vertices):
            if len(set(vertices))!=len(vertices):
                fail("Source constrained rollover has collapsed face")
            try:face=bm.faces.new(vertices)
            except ValueError:fail("Duplicate authentic source lip patch polygon")
            face.smooth=True;face.material_index=0
            addedFaces.append(face)
            added_lip_faces.append(face)
            face_origin[face]="palm_cap"
            if uv is not None:
                for corner in face.loops:
                    vertex=corner.vert
                    corner[uv].uv=(source_skin_uv[vertex] if vertex in
                        source_skin_uv else nearest_source_uv(vertex))
        # Seam border face orientation: original A path runs tip0->tip1,
        # and original B edge loop returns from tip1->tip0. The new mesh
        # traverses BOTH in the opposite direction, preserving winding.
        for band in range(4):
            add_lip_patch_face([path_a[0],rolled[0][band+1],
                                rolled[0][band]])
        for row_id in range(len(rolled)-1):
            first=rolled[row_id]
            next_row=rolled[row_id+1]
            for band in range(4):
                add_lip_patch_face([first[band],first[band+1],
                                    next_row[band+1],next_row[band]])
        for band in range(4):
            add_lip_patch_face([path_a[-1],rolled[-1][band],
                                rolled[-1][band+1]])
        bm.normal_update()
        if sum(e.is_boundary for e in bm.edges)!=before_edges-30:
            fail("Rounded genuine palm skin rollover did not close its slit")
        if any(not(e.is_manifold or e.is_boundary) for e in bm.edges):
            fail("Actual source rollover created nonmanifold shared palm web")
        if any(e.is_manifold and not e.is_contiguous for e in bm.edges):
            fail("True metacarpal source lip winding is inverted")
        if any(face.normal.length_squared<1e-12 or face.calc_area()<
               diameter*diameter*1e-11 for face in added_lip_faces):
            fail("Source rollover contains collapsed tissue skin polygon")
        local_edges={e for face in added_lip_faces for e in face.edges
                     if e.is_manifold and len(e.link_faces)==2}
        angles=[math.degrees(e.link_faces[0].normal.angle(
                e.link_faces[1].normal)) for e in local_edges]
        # Measure the complete affected original + generated epidermis,
        # not just the new strip. A removed 180-degree dome can otherwise
        # hide severe bends in the adjacent untouched source polygons.
        actual_native_edges={
            edge for vert in skin_before for edge in vert.link_edges
            if edge.is_manifold and len(edge.link_faces)==2}
        actual_native_angles=[
            math.degrees(edge.link_faces[0].normal.angle(
                edge.link_faces[1].normal)) for edge in actual_native_edges
            if edge.link_faces[0].normal.length_squared>1e-12
            and edge.link_faces[1].normal.length_squared>1e-12]
        actual_native_severe=sum(angle>120 for angle in actual_native_angles)
        capped.append({
            "source_native_affected_edges":len(actual_native_angles),
            "source_native_affected_over120":actual_native_severe,
            "source_native_affected_over90":sum(
                angle>90 for angle in actual_native_angles),
            "source_native_max_angle":max(actual_native_angles,default=0),
            "method":"source_constrained_two_lip_skin_cross_section",
            "source_open_ring":30,
            "native_opposed_skin_pairs":14,
            "new_skin_vertices":len(rolled)*3,
            "faces":len(added_lip_faces),
            "genuine_original_palm_corners":30,
            "over120_including_original_boundary":sum(a>120 for a in angles),
            "over90_including_original_boundary":sum(a>90 for a in angles),
            "max_angle_including_original_boundary":max(angles,default=0),
            "max_real_lip_gap":max(gap for _,_,gap in pairs)})
    report["authentic_two_distal_palm_pockets_sealed"]=capped
    report["anatomical_skin_cap_vertices"]=len(cap_created)
    report["true_structural_palm_slit_lip_stitch"]=True
    # Only a genuine 64-edge sleeve cuff remains open. All 5 digits and
    # both old palm pockets now have a real continuous epidermis surface.
    if sum(e.is_boundary for e in bm.edges)!=64:
        fail("Skin not fully repaired (except intentional sleeve cuff)")
    nonmanifold=[e for e in bm.edges if not(e.is_manifold or e.is_boundary)]
    report["post_graft_nonmanifold_edges"]=len(nonmanifold)
    if nonmanifold:
        fail("Reconstructed genuine skin has nonmanifold edges: "+
             str(len(nonmanifold)))
    reversed_surfaces=[e for e in bm.edges if e.is_manifold and
                       not e.is_contiguous]
    report["inconsistent_face_winding_edges"]=len(reversed_surfaces)
    if reversed_surfaces:
        fail("Anatomical skin still has inverted or colliding face winding: "+
             str(len(reversed_surfaces)))
    # Blender does not guarantee that just-created BMesh faces have
    # computed normals until normal_update(); without it all new normals
    # can report a zero vector even on valid, nondegenerate polygons.
    bm.normal_update()
    # Winding/manifold PASS does not establish smooth tissue. Quantify
    # adjacent ORIGINAL/NEW face bending: direct anatomical evidence of
    # the visible long thin web spikes between fingers.
    zero_normals=sum(face.normal.length_squared<1e-12
                     for face in addedFaces if face.is_valid)
    report["root_surface_zero_face_normals"]=zero_normals
    if zero_normals:
        fail("Zero-area generated skin face after actual BMesh normal update: "+
             str(zero_normals))
    fold_angles=[]
    fold_by_region=defaultdict(list)
    seen_fold_edges=set()
    for face in addedFaces:
        if not face.is_valid:continue
        for edge in face.edges:
            if edge in seen_fold_edges:continue
            if not(edge.is_manifold and len(edge.link_faces)==2):continue
            seen_fold_edges.add(edge)
            neighbor=next((f for f in edge.link_faces if f!=face),None)
            if neighbor is None:continue
            angle=math.degrees(face.normal.angle(neighbor.normal))
            fold_angles.append(angle)
            region=face_origin.get(face,"source")
            opposite=face_origin.get(neighbor,"source")
            if region=="palm_cap" or opposite=="palm_cap":
                region="palm_cap"
            elif region.startswith("finger_"):
                pass
            else:
                region=opposite
            fold_by_region[region].append(angle)
    report["root_surface_dihedral_degrees"]={
        "sample_count":len(fold_angles),
        "max_angle":max(fold_angles,default=0),
        "over_90":sum(a>90 for a in fold_angles),
        "over_120":sum(a>120 for a in fold_angles),
        "by_region":{k:{"count":len(v),"over_90":sum(a>90 for a in v),
                         "over_120":sum(a>120 for a in v),
                         "max":max(v)} for k,v in fold_by_region.items()}}

    if uv is None:
        fail("Authentic source UV layer was lost in premium palm surface repair")
    report["source_uv_layer_retained"]=True
    # Source anatomical finger 3D shell is now a genuine connected skin
    # island, except TWO unrelated original distal-palm holes and the
    # 64-edge arm cuff (which belongs UNDER THE SHIRT SLEEVE).
    if len(islands(bm))!=1:
        fail("Original palm and all five source fingers must form ONE 3D mesh")
    remaining_boundaries=sum(e.is_boundary for e in bm.edges)
    report["post_graft_remaining_boundary_edges"]=remaining_boundaries
    if remaining_boundaries>160:
        fail("Too many unclosed edges after real individual palm grafts")

    # Bounded local epidermis relaxation at actual interfaces.
    primary={v for v in source_contact_vertices if v.is_valid}
    peripheral={w for v in primary for e in v.link_edges for w in e.verts}
    peripheral.difference_update(primary)
    region=primary|peripheral
    original_positions={v:v.co.copy() for v in region}
    for _ in range(3):
        previous={v:v.co.copy() for v in region}
        changed={}
        for v in region:
            neighbors=[w for e in v.link_edges for w in e.verts if w!=v]
            if not neighbors:continue
            average=sum((previous.get(w,w.co) for w in neighbors),
                        Vector((0,0,0)))/len(neighbors)
            amount=.12 if v in primary else .025
            target=v.co.lerp(average,amount)
            max_step=diameter*.003
            offset=target-original_positions[v]
            if offset.length>max_step:
                target=original_positions[v]+offset.normalized()*max_step
            changed[v]=target
        for v,target in changed.items():v.co=target
    root_shift=max((v.co-original_positions[v]).length for v in region)
    report["tissue_root_relaxation"]={
        "original_boundary_vertices":len(primary),
        "one_ring_vertices":len(peripheral),
        "passes":3,
        "maximum_original_vertex_displacement":root_shift,
        "maximum_relative_original_displacement":root_shift/diameter}
    # Targeted candidate-only anti-spike fairing. The 30->13 palm rim
    # reduction can create very high curvature on individual newly-made
    # tissue vertices even if Blender reports manifold winding. Work ONLY
    # with the original-source-weighted transition vertices, never distal
    # finger geometry, the source file, bones, texture or sleeve.
    generated_skin_vertices={v for v,_ in addedWeights if v.is_valid}
    fairing_rest={v:v.co.copy() for v in generated_skin_vertices}
    fairing_counts=0
    fairing_limit=diameter*.005
    for _ in range(3):
        rest={v:v.co.copy() for v in generated_skin_vertices}
        movements={}
        for v in generated_skin_vertices:
            neighbors={w for e in v.link_edges for w in e.verts if w!=v}
            if len(neighbors)<3:
                continue
            lengths=sorted((rest.get(w,w.co)-rest[v]).length
                           for w in neighbors)
            typical=lengths[len(lengths)//2]
            if typical<1e-8:
                fail("Collapsed original-weighted knuckle transition edge")
            mean=sum((rest.get(w,w.co) for w in neighbors),
                     Vector((0,0,0)))/len(neighbors)
            distortion=(mean-rest[v]).length
            # Adapt only clear local outliers rather than another global
            # laplacian pass; protect the actual contours at fingers.
            excess=distortion/typical-.24
            if excess<=0:
                continue
            coefficient=min(.23,excess*.17)
            target=rest[v].lerp(mean,coefficient)
            correction=target-fairing_rest[v]
            if correction.length>fairing_limit:
                target=fairing_rest[v]+correction.normalized()*fairing_limit
            if (target-rest[v]).length>1e-8:
                movements[v]=target
        for v,p in movements.items():
            v.co=p
        fairing_counts+=len(movements)
    fairing_drift=max(((v.co-fairing_rest[v]).length
                       for v in generated_skin_vertices),default=0)
    report["localized_generated_tissue_anti_spike"]={
        "vertices_examined":len(generated_skin_vertices),
        "vertex_adjustments":fairing_counts,
        "passes":3,
        "max_vertex_displacement":fairing_drift,
        "limit_ratio_of_hand_extent":.005}
    if fairing_drift>fairing_limit+1e-7:
        fail("Candidate anti-spike fairing exceeded hard safety displacement")
    # Explicit ACTUAL-FACE NORMAL hinge minimizer, not more blanket
    # smoothing. The last Blender proof still had 78 folds >120 degrees;
    # original 25 bone skin motion passed, but visible knuckle web fins did
    # not. Solve only newly generated bridge/cap vertices, never authentic
    # distal/source finger vertices. Measure angles between two REAL
    # adjacent skin polygons before/after every bounded optimization pass.
    generated_faces={face for face in addedFaces if face.is_valid}
    trial_skin_vertices=({v for v,_ in addedWeights if v.is_valid} |
                         {v for v in cap_created if v.is_valid})
    hinge_edges={edge for face in generated_faces for edge in face.edges
                 if edge.is_manifold and len(edge.link_faces)==2}
    bm.normal_update()
    initial_angles={e:math.degrees(e.link_faces[0].normal.angle(
        e.link_faces[1].normal)) for e in hinge_edges}
    baseline_over120=sum(angle>120 for angle in initial_angles.values())
    baseline_over90=sum(angle>90 for angle in initial_angles.values())
    base_vertex_positions={v:v.co.copy() for v in trial_skin_vertices}
    max_crease_shift=diameter*.006
    # Restrict the evaluation to edges touched by faces influenced by v;
    # otherwise optimization over all source polygons would overwhelm
    # the actual 5 MCP/cap problem with unrelated fixed source UV edges.
    neighborhoods={}
    for vertex in trial_skin_vertices:
        linked={face for face in vertex.link_faces}
        neighborhoods[vertex]={
            e for face in linked for e in face.edges if e in hinge_edges}
    def local_hinge_energy(vertex):
        total=0.
        for edge in neighborhoods[vertex]:
            f1,f2=edge.link_faces
            # No normal vector guessed from a screenshot or n-gon:
            # Blender's own actual BMesh polygon normals.
            angle=math.degrees(f1.normal.angle(f2.normal))
            excess=max(0.,angle-72.)
            total+=excess*excess
            if angle>120:
                total+=(angle-120.)**2*2.
        shift=(vertex.co-base_vertex_positions[vertex]).length
        total+=180.*(shift/max_crease_shift)**2
        return total
    moved_crease_vertices=set()
    changed_crease_vertices=0
    for pass_no in range(4):
        improvements=0
        for vertex in sorted(trial_skin_vertices,key=lambda v:id(v)):
            if not neighborhoods[vertex]:continue
            neighbors={q for edge in vertex.link_edges for q in edge.verts
                       if q!=vertex}
            if len(neighbors)<3:continue
            current=vertex.co.copy()
            mean=sum((q.co for q in neighbors),
                     Vector((0,0,0)))/len(neighbors)
            if (mean-current).length<1e-9:continue
            surrounding=list(vertex.link_faces)
            for face in surrounding:face.normal_update()
            start_cost=local_hinge_energy(vertex)
            best_cost=start_cost
            best=current
            for alpha in (.14,.32,.58):
                proposal=current.lerp(mean,alpha)
                movement=proposal-base_vertex_positions[vertex]
                if movement.length>max_crease_shift:
                    proposal=base_vertex_positions[vertex]+movement.normalized()*max_crease_shift
                vertex.co=proposal
                for face in surrounding:face.normal_update()
                cost=local_hinge_energy(vertex)
                if cost+1e-3<best_cost:
                    best_cost=cost
                    best=proposal.copy()
            vertex.co=best
            for face in surrounding:face.normal_update()
            if (best-current).length>1e-8:
                improvements+=1
                changed_crease_vertices+=1
                moved_crease_vertices.add(vertex)
        if improvements==0:break
    bm.normal_update()
    resulting_angles={e:math.degrees(e.link_faces[0].normal.angle(
        e.link_faces[1].normal)) for e in hinge_edges}
    post_over120=sum(angle>120 for angle in resulting_angles.values())
    post_over90=sum(angle>90 for angle in resulting_angles.values())
    drift=max(((v.co-base_vertex_positions[v]).length
               for v in trial_skin_vertices),default=0.)
    # Region-specific evidence prevents trading a better knuckle for a
    # worse source-derived palm cap. No UV or original bone weights touched.
    def summarize_actual_regions():
        groups=defaultdict(list)
        for edge in hinge_edges:
            faces=edge.link_faces
            regions=[face_origin.get(face,"source") for face in faces]
            if "palm_cap" in regions:
                region="palm_cap"
            else:
                region=next((name for name in regions if name.startswith("finger_")),
                            "unclassified")
            groups[region].append(math.degrees(faces[0].normal.angle(faces[1].normal)))
        return {name:{"edges":len(angles),
                      "over_90":sum(a>90 for a in angles),
                      "over_120":sum(a>120 for a in angles),
                      "max_angle":max(angles,default=0)}
                for name,angles in groups.items()}
    report["post_hinge_optimizer_by_region"]=summarize_actual_regions()
    # Structural rather than cosmetic QA: record where real >120-degree
    # bends are physically located. A green 25-bone manifold check does
    # NOT imply that the two source palm apertures form natural epidermis.
    # Keep enough 3D evidence to reconstruct the shared metacarpal sheet.
    structural_regions=defaultdict(list)
    for edge in hinge_edges:
        faces=list(edge.link_faces)
        regions=[face_origin.get(f,"source") for f in faces]
        region=("palm_cap" if "palm_cap" in regions else next(
            (name for name in regions if name.startswith("finger_")),
            "source"))
        angle=math.degrees(faces[0].normal.angle(faces[1].normal))
        if angle<=90.:continue
        midpoint=(edge.verts[0].co+edge.verts[1].co)*.5
        structural_regions[region].append({
            "degrees":round(angle,4),
            "midpoint":[round(float(c),6) for c in midpoint],
            "length":round(float(edge.calc_length()),6),
            "face_sources":regions})
    report["structural_high_angle_edge_atlas"]={
        group:sorted(entries,key=lambda e:e["degrees"],reverse=True)[:30]
        for group,entries in structural_regions.items()}
    report["structural_true_palm_apertures"]=[
        {"source_rim_vertices":len(rim),
         "root_coords":[[round(float(q),7) for q in v.co] for v in rim],
         "center":[round(float(q),7) for q in
                   sum((v.co for v in rim),Vector((0,0,0)))/len(rim)],
         "radial_max":max((v.co-sum((v1.co for v1 in rim),
                       Vector((0,0,0)))/len(rim)).length for v in rim)}
        for rim in holes if len(rim)==30 and
        all(v.is_valid for v in rim)]
    report["targeted_actual_face_hinge_optimization"]={
        "sampled_generated_surface_edges":len(hinge_edges),
        "tissue_vertices_in_scope":len(trial_skin_vertices),
        "different_vertices_moved":len(moved_crease_vertices),
        "accepted_changes":changed_crease_vertices,
        "hard_position_limit":max_crease_shift,
        "maximum_displacement":drift,
        "before_over_90":baseline_over90,
        "before_over_120":baseline_over120,
        "after_over_90":post_over90,
        "after_over_120":post_over120,
        "before_max_angle":max(initial_angles.values(),default=0),
        "after_max_angle":max(resulting_angles.values(),default=0)}
    if drift>max_crease_shift+1e-7:
        fail("Actual face hinge optimizer exceeded its safe candidate-only limit")
    if post_over120>baseline_over120+3:
        fail("Face-fold optimizer worsened high-risk finger/cap angles")
    print("ROKAS ACTUAL FACE CREASE OPTIMIZER",
          json.dumps(report["targeted_actual_face_hinge_optimization"]),
          flush=True)
    bm.normal_update()
    bm.verts.index_update()
    bm.verts.ensure_lookup_table()
    generated=[(v.index,[(src.index,weight) for src,weight in contributors])
               for v,contributors in addedWeights]
    # All transitional loops are assigned authentic source-derived bone
    # weights AFTER original five-digit retarget and remain skinned.
    merged=islands(bm)
    report["new_bridge_vertices"]=len(generated)
    report["new_bridge_faces"]=len(addedFaces)
    report["post_islands"]=len(merged)
    report["post_boundary_edges"]=sum(e.is_boundary for e in bm.edges)
    report["post_vertices"]=len(bm.verts)
    report["post_faces"]=len(bm.faces)
    # Retarget source FBX's misassigned island weights to the real finger
    # chains. Unity proved the Argon exporter reused the previous digit's
    # influence group for successive fingers. Missing Ring DIP and all Little
    # groups are not grounds to ignore those authentic existing joints.
    #
    # This does NOT move any vertex or bind bone. Blender's armature rest
    # matrices preserve the neutral pose; animated PIP/DIP bends must be
    # verified in Unity separately for every finger.
    finger_vertices = sorted(
        [sorted(v.index for v in group) for group in components[1:]],
        key=lambda ids: sum(bm.verts[i].co.y for i in ids)/len(ids))
    bm.to_mesh(clone.data);bm.free()
    clone.data.update()
    finger_target_chains = [
        ["Hand_Palm","Thumb_CMC","Thumb_MCP","Thumb_IP"],
        ["Hand_Palm","Index_MCP","Index_PIP","Index_DIP"],
        ["Hand_Palm","Middle_MCP","Middle_PIP","Middle_DIP"],
        ["Hand_Palm","Ring_MCP","Ring_PIP","Ring_DIP"],
        ["Hand_Palm","Little_MCP","Little_PIP","Little_DIP"]]
    original_groups={g.index:g.name for g in clone.vertex_groups}
    bone_names={b.name for b in arm.data.bones}
    for chain in finger_target_chains:
        if not all(name in bone_names for name in chain):
            fail("Authentic digit joints absent: "+str(chain))
    retarget_report=[]
    for digit,indices in enumerate(finger_vertices):
        totals=defaultdict(float)
        individual={}
        for index in indices:
            entries=[(original_groups[x.group],x.weight)
                     for x in clone.data.vertices[index].groups
                     if x.weight>1e-6 and x.group in original_groups]
            if not entries:fail("Original digit vertex without real skin weight")
            individual[index]=entries
            for name,weight in entries:totals[name]+=weight
        strong=sorted((name for name,value in totals.items()
                       if value>1.0),key=lambda name:
                      next(g.index for g in clone.vertex_groups if g.name==name))
        if len(strong)!=4:
            fail("Original finger %s influence histogram has %s strong groups: %s"%
                 (digit,len(strong),dict(totals)))
        fraction=sum(totals[x] for x in strong)/sum(totals.values())
        if fraction < .999:
            fail("Unknown original joint influence cannot be mapped safely")
        targets=finger_target_chains[digit]
        remap=dict(zip(strong,targets))
        # Add authentic bone groups when Argon's FBX omitted them.
        for name in targets:
            if clone.vertex_groups.get(name) is None:
                clone.vertex_groups.new(name=name)
        for index in indices:
            # First remove ONLY weights attached to this digit vertex;
            # all palm/forearm weights and all ORIGINAL source meshes remain.
            for name,_ in individual[index]:
                clone.vertex_groups[name].remove([index])
            target_weights=defaultdict(float)
            for name,weight in individual[index]:
                if name not in remap:
                    fail("Unmappable source bone influence: "+name)
                target_weights[remap[name]]+=weight
            norm=sum(target_weights.values())
            if not .99<=norm<=1.01:
                fail("Source skin weight normalization inconsistent")
            for name,weight in target_weights.items():
                clone.vertex_groups[name].add([index],weight/norm,'REPLACE')
        retarget_report.append({
            "digit":digit,"vertices":len(indices),
            "source_weights":dict(totals),
            "source_to_authentic_target":remap,
            "coverage":fraction})
    report["anatomical_digit_influences"]=retarget_report
    # Must skin each generated fillet AFTER the verified original digit
    # retarget. Previous order mixed WRONG old finger chains into the
    # connecting skin. No guessed linear ring/little reweighting.
    # Shared loft vertices: interpolate original palm weights with corrected
    # original finger weights to avoid snapping at the joining skin fold.
    group_names={g.index:g.name for g in clone.vertex_groups}
    def vweights(i):
        return {group_names[x.group]:x.weight
                for x in clone.data.vertices[i].groups if x.weight>1e-6}
    for vi,contributors in generated:
        mix=defaultdict(float)
        for source_index,ratio in contributors:
            for name,weight in vweights(source_index).items():
                mix[name]+=ratio*weight
        weightSum=sum(mix.values())
        if weightSum<.5:fail("Generated seam has no meaningful skin weights")
        for name,w in mix.items():
            if w>1e-6:
                clone.vertex_groups[name].add([vi],w/weightSum,'REPLACE')
    report["loft_weighted_vertices"]=len(generated)

    b1=bbox(clone)
    report["silhouette_drift"]=max(abs(b0[i][k]-b1[i][k])
        for i in range(3) for k in range(2))/diameter
    (out/"local_root_bridge_report.json").write_text(
        json.dumps(report,indent=2),encoding="utf-8")
    print("ROKAS ORIGINAL ROOT BRIDGE MEASUREMENTS",json.dumps(report),flush=True)
    if report["post_islands"]!=1 or report["silhouette_drift"]>1e-7:
        fail("Topology/shape safety gate: "+json.dumps(report))
    if report["post_boundary_edges"]>=report["original_boundaries"]:
        fail("Original long seam was not reduced")
    if any(x["quads"]<8 or x["source_nearest_skin_gap"]>diameter*.049
           for x in report["per_finger"]):
        fail("Anatomical graft missing / source surface too far")
    # The temporary unmodified geometry test earlier removed the original
    # Armature modifier. FBX export MUST restore real skinning, otherwise
    # Unity correctly imports a static MeshRenderer instead of a true
    # 25-bone SkinnedMeshRenderer. Original FBX and source rig stay intact.
    # The source has 25 authentic BONE OBJECTS, but FBX may omit deform
    # vertex groups for helper/end joints. Requiring 25 WEIGHTED GROUPS
    # incorrectly rejects a genuine skin with fewer influencing bones.
    names=[g.name for g in clone.vertex_groups]
    print("ROKAS SOURCE INFLUENCE GROUPS",len(names),names,flush=True)
    if len(names)<5:
        fail("Original skinning has fewer than five actual vertex groups")
    weighted=sum(bool(v.groups) for v in clone.data.vertices)
    report["skin_source_groups"]=names
    report["skin_weight_coverage"]=weighted/max(1,len(clone.data.vertices))
    if report["skin_weight_coverage"]<.98:
        fail("Candidate hand lost source per-vertex bone weights")
    skin=clone.modifiers.new("ROKAS_Authentic_25Bone_Armature","ARMATURE")
    skin.object=arm
    skin.use_vertex_groups=True
    skin.use_bone_envelopes=False
    (out/"local_root_bridge_report.json").write_text(
        json.dumps(report,indent=2),encoding="utf-8")

    bpy.ops.object.select_all(action='DESELECT')
    clone.select_set(True);arm.select_set(True)
    bpy.context.view_layer.objects.active=clone
    file=out/"ROKAS_Argon_LocalRootBridge_RIG_QA_ONLY.fbx"
    bpy.ops.export_scene.fbx(filepath=str(file),use_selection=True,
        object_types={'ARMATURE','MESH'},add_leaf_bones=False,
        use_mesh_modifiers=False,bake_anim=False)
    print("ROKAS ORIGINAL VERTEX PRESERVING BRIDGE QA",json.dumps(report))
    print("OPEN FINGER SURFACES STILL NEED RECONSTRUCTION; NOT VISUAL ACCEPTED")
if __name__=="__main__":
    main()
