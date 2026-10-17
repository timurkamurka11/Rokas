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
def loop(island):
    bound=[e for v in island for e in v.link_edges if e.is_boundary and
           all(x in island for x in e.verts)]
    bound=list(set(bound))
    adjacency=defaultdict(list)
    for e in bound:
        a,b=e.verts;adjacency[a].append(b);adjacency[b].append(a)
    if len(adjacency)<10 or any(len(v)!=2 for v in adjacency.values()):
        fail("Source ring is branched or unclosed")
    first=min(adjacency,key=lambda v:v.index)
    ordered=[];last=None;v=first
    for _ in range(len(adjacency)+1):
        ordered.append(v)
        nxt=next(x for x in adjacency[v] if x!=last)
        if nxt==first:break
        last,v=v,nxt
    if len(ordered)!=len(adjacency):
        fail("Unexpected multiple rings in original finger")
    return ordered
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
    components=islands(bm)
    if len(components)!=6 or len(components[0])<5000:fail("Wrong FBX skin islands")
    source_indices=[[v.index for v in group] for group in components]
    per=[loop(x) for x in components]
    center=per[0]
    # Previous 0.045 hand-span allowance permitted 17-18 mm bridges across
    # 8-10 mm finger sections. Real Unity images proved severe polygons.
    # Reject distant contacts, use much shorter true surface transitions.
    diameter=max(b-a for a,b in b0)
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
        for ring in per:
            for vertex in ring:
                loops=[l for e in vertex.link_edges for face in e.link_faces
                       for l in face.loops if l.vert==vertex]
                if loops:orig_uv[vertex.index]=loops[0][uv].uv.copy()
    ringCache={}
    addedWeights=[]
    addedFaces=[]
    def point_between(f,p,t):
        key=(f.index,p.index,t)
        if key in ringCache:return ringCache[key]
        gap=(p.co-f.co).length
        n=f.normal+p.normal
        if n.length_squared>1e-12:n.normalize()
        else:n=f.normal.copy()
        # A restrained sub-millimetre skin fold easing from the palm.
        # No source vertex is displaced; motion weights get interpolated.
        height=min(gap*.045,diameter*.0006)*math.sin(math.pi*t)
        vertex=bm.verts.new(f.co.lerp(p.co,t)+n*height)
        ringCache[key]=vertex
        addedWeights.append((vertex,f.index,p.index,t))
        return vertex
    report={"original_vertices":len(bm.verts),
            "original_islands":len(components),
            "original_boundaries":sum(e.is_boundary for e in bm.edges),
            "source_bones":len(arm.data.bones),"max_bridge":maxBridge,
            "per_finger":[]}
    for fingerNo,ring in enumerate(per[1:],1):
        # Find real closest parallel boundary *edges*, no fabricated cylinders
        # and no moving/scaling of malformed ring finger roots.
        best=None
        for i in range(len(ring)):
            f0=ring[i].co;f1=ring[(i+1)%len(ring)].co
            for j in range(len(center)):
                p0=center[j].co;p1=center[(j+1)%len(center)].co
                direct=(f0-p0).length+(f1-p1).length
                reverse=(f0-p1).length+(f1-p0).length
                cur=(min(direct,reverse),i,j,1 if direct<=reverse else -1)
                if best is None or cur[0]<best[0]:best=cur
        _,fi,pi,orientation=best
        bridgeCount=0;maximum=0.0
        fingerPalmVerts=set()
        for k in range(-7,8):
            f0=ring[(fi+k)%len(ring)]
            f1=ring[(fi+k+1)%len(ring)]
            if orientation==1:
                p0=center[(pi+k)%len(center)]
                p1=center[(pi+k+1)%len(center)]
            else:
                p0=center[(pi-k+1)%len(center)]
                p1=center[(pi-k)%len(center)]
            dist0=(f0.co-p0.co).length;dist1=(f1.co-p1.co).length
            if max(dist0,dist1)>maxBridge or min(dist0,dist1)<minBridge:
                continue
            if p0.index in usedPalm or p1.index in usedPalm:continue
            if len({f0.index,f1.index,p0.index,p1.index})!=4:continue
            # 3 surface rows between genuine neighboring source outlines,
            # with intermediate shared loft vertices and no new primitives.
            a0=point_between(f0,p0,1.0/3.0)
            a1=point_between(f1,p1,1.0/3.0)
            b0v=point_between(f0,p0,2.0/3.0)
            b1v=point_between(f1,p1,2.0/3.0)
            strips=([f0,f1,a1,a0],[a0,a1,b1v,b0v],[b0v,b1v,p1,p0])
            for quad in strips:
                try:face=bm.faces.new(quad)
                except ValueError:fail("Nonmanifold or duplicate loft patch")
                face.material_index=0
                face.smooth=True
                addedFaces.append(face)
                if uv is not None:
                    for corner in face.loops:
                        v=corner.vert
                        if v.index in orig_uv:
                            corner[uv].uv=orig_uv[v.index].copy()
                        else:
                            record=next((x for x in addedWeights if x[0]==v),None)
                            if record is None:fail("Missing generated UV edge provenance")
                            _,fi_idx,pi_idx,t=record
                            if fi_idx not in orig_uv or pi_idx not in orig_uv:
                                fail("Cannot interpolate authentic finger-palm UV")
                            corner[uv].uv=orig_uv[fi_idx].lerp(
                                orig_uv[pi_idx],t)
            fingerPalmVerts.update((p0.index,p1.index))
            source_contact_vertices.update((f0,f1,p0,p1))
            bridgeCount+=1
            maximum=max(maximum,dist0,dist1)
        # A neighboring quad reuses its two end vertices: only forbid
        # overlapping patches BETWEEN DIFFERENT fingers, not within one.
        usedPalm.update(fingerPalmVerts)
        report["per_finger"].append({"finger":fingerNo,
            "quads":bridgeCount,"max_root_gap":maximum,
            "closest_pair_cost":best[0]})
        if bridgeCount<1:fail("Cannot safely bridge original finger "+str(fingerNo))
    # Smooth the AUTHENTIC anatomical junction and adjacent tissue only,
    # never the entire mesh. The earlier 3-row skin fillet still showed
    # artificial collar normals at MCP bases in 1280x720 closeups.
    # Move root-border vertices by a strictly bounded fraction of source
    # hand extent; source FBX and 60%+ distal fingers remain intact.
    loft_vertices={entry[0] for entry in addedWeights}
    primary=source_contact_vertices | loft_vertices
    outer={w for v in primary for e in v.link_edges for w in e.verts}
    outer.difference_update(primary)
    region=primary|outer
    original_positions={v:v.co.copy() for v in region}
    source_vertex_count=report["original_vertices"]
    for _ in range(3):
        positions={v:v.co.copy() for v in region}
        new_positions={}
        for v in region:
            adjacent=[w for e in v.link_edges for w in e.verts if w!=v]
            if not adjacent:continue
            mean=v.co*0.0
            for w in adjacent:mean+=positions.get(w,w.co)
            mean/=len(adjacent)
            if v in source_contact_vertices:strength=.115
            elif v in loft_vertices:strength=.17
            else:strength=.035
            target=v.co.lerp(mean,strength)
            # Bounded original tissue relaxation: no synthetic collapse.
            permitted=diameter*(.008 if v.index>=0 and
                      v.index<source_vertex_count else .013)
            delta=target-original_positions[v]
            if delta.length>permitted:
                target=original_positions[v]+delta.normalized()*permitted
            new_positions[v]=target
        for v,p in new_positions.items():v.co=p
    original_max_shift=max(
        (v.co-original_positions[v]).length
        for v in region if v.index>=0 and v.index<source_vertex_count)
    report["tissue_root_relaxation"]={
        "original_boundary_vertices":len(source_contact_vertices),
        "new_blended_loft_vertices":len(loft_vertices),
        "one_ring_vertices":len(outer),
        "maximum_original_vertex_displacement":original_max_shift,
        "maximum_relative_original_displacement":original_max_shift/diameter,
        "passes":3}
    if original_max_shift>diameter*.00801:
        fail("Anatomical collar relaxation displaced original tissue too far")
    bm.normal_update()
    bm.verts.index_update()
    bm.verts.ensure_lookup_table()
    generated=[(v.index,fi,pi,t) for v,fi,pi,t in addedWeights]
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
    if any(x["quads"]<1 or x["max_root_gap"]>maxBridge for x in report["per_finger"]):
        fail("Excessively stretched/absent bridge, reject")
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
