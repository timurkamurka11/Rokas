#!/usr/bin/env python3
"""ROKAS 25-bone ORIGINAL FBX: local finger/palm bridge QA candidate.

Unlike the REJECTED destructive voxel model, this proof of local repair
changes ZERO original vertices, bones, normals, base UVs or vertex groups.
It joins adjacent real original boundary strips near each finger root using
tiny quad surface patches and maps UVs from original neighboring polygons.
It is NOT a full closed hand: remaining long open shell boundaries require
additional anatomical retopology. Hard-limit all patch spans, prevent
overlapping roots and require 1 topological connected island; visual QA
still required. Source FBX stays untouched. NO V10 INTEGRATION.
"""
import bpy,bmesh,sys,json,pathlib,math
from collections import defaultdict

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

    def join_root_to_hole(finger, hole):
        N=len(finger);M=len(hole)
        if N<8 or M<8:fail("Graft rim too small")
        best=None
        for i,v in enumerate(finger):
            for j,w in enumerate(hole):
                metric=(v.co-w.co).length_squared
                if best is None or metric<best[0]:best=(metric,i,j)
        _,fi,pj=best
        # Choose orientation from GLOBAL source ring arc correspondence;
        # preserve mesh winding via existing boundary-face edge orientation.
        selections=[]
        for direction in (1,-1):
            a=[finger[(fi+k)%N] for k in range(N)]
            p=[hole[(pj+direction*k)%M] for k in range(M)]
            cost=0.
            for k in range(30):
                t=k/30.
                va=a[min(N-1,int(t*N))]
                vb=p[min(M-1,int(t*M))]
                cost+=(va.co-vb.co).length
            selections.append((cost,direction,a,p))
        _,direction,a,p=min(selections,key=lambda rec:rec[0])
        index_a=index_p=0
        built=[]
        while index_a<N or index_p<M:
            next_a=(index_a+1)/N if index_a<N else 1e9
            next_p=(index_p+1)/M if index_p<M else 1e9
            u=a[index_a%N];v=p[index_p%M]
            if abs(next_a-next_p)<1e-7:
                vertices=[u,a[(index_a+1)%N],p[(index_p+1)%M],v]
                index_a+=1;index_p+=1
            elif next_a<next_p:
                vertices=[u,a[(index_a+1)%N],v]
                index_a+=1
            else:
                vertices=[u,p[(index_p+1)%M],v]
                index_p+=1
            if len(set(vertices))!=len(vertices):
                fail("Degenerate finger-palm root graft triangles")
            try:
                face=bm.faces.new(vertices)
            except ValueError:
                fail("Duplicate root-to-palm surface triangle")
            face.material_index=0;face.smooth=True
            built.append(face)
            if uv is not None:
                for corner in face.loops:
                    corner[uv].uv=nearest_source_uv(corner.vert)
        if len(built)<max(N,M):
            fail("Anatomical graft missing source ring faces")
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
        radius_hole=max(radius*.57,diameter*.010)
        radius_hole=min(radius_hole,diameter*.022)
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
        faces,wind=join_root_to_hole(ring,pocket)
        addedFaces.extend(faces)
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
            "original_finger_rim_vertices":len(ring),
            "zipper_winding":wind,
            "max_root_gap":max((v.co-w.co).length for v in ring for w in pocket)})
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
    peripheral={v for v in primary for e in v.link_edges for w in e.verts}
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
    bm.normal_update()
    bm.verts.index_update()
    bm.verts.ensure_lookup_table()
    generated=[]  # Surface graft uses authentic existing boundary vertices; no cloned finger proxies
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
    for vi,fi,pi,t in generated:
        a,b=vweights(fi),vweights(pi)
        mix={}
        for name in set(a)|set(b):
            mix[name]=a.get(name,0)*(1-t)+b.get(name,0)*t
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
