#!/usr/bin/env python3
"""ROKAS: data-grounded original-FBX finger seam atlas (NO mesh mutation).

Reads the authentic 25-bone skinned hand, measures *existing* boundary
edges and nearest palm/finger contacts in rest-pose world coordinates.
This is the input to a real local retopology patch (not voxel destruction).
The source FBX, vertex positions, bone weights and face order remain intact.
"""
import sys
import json
import pathlib
from collections import defaultdict
import bpy
import bmesh
from mathutils import Vector

def main():
    argv=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(argv)!=2:
        raise RuntimeError("Usage: blender -b -P map_original_seams.py -- SOURCE.fbx OUT")
    source=pathlib.Path(argv[0]).resolve()
    out=pathlib.Path(argv[1]).resolve()
    out.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source),use_anim=False)
    meshes=[o for o in bpy.data.objects if o.type=='MESH' and
            'Keiko_Hand_Skin_Mesh' in o.name]
    if len(meshes)!=1:raise RuntimeError("Authentic skin not uniquely found")
    o=meshes[0]
    arm=o.find_armature()
    if arm is None or len(arm.data.bones)<25:
        raise RuntimeError("Original 25-bone rig is required")
    bm=bmesh.new()
    bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()

    # True connectivity from the original triangles, NOT naive co-located
    # point collapse. Five fingers are distinct islands and must be named.
    seen=set()
    islands=[]
    for v in bm.verts:
        if v.index in seen:continue
        stack=[v];group=[]
        while stack:
            x=stack.pop()
            if x.index in seen:continue
            seen.add(x.index);group.append(x.index)
            stack.extend(other for edge in x.link_edges for other in edge.verts
                         if other.index not in seen)
        islands.append(group)
    islands.sort(key=len,reverse=True)
    if len(islands)!=6 or len(islands[0])<5000:
        raise RuntimeError("Expected original palm + 5 independent finger islands")
    membership={}
    for k,group in enumerate(islands):
        for index in group:membership[index]=k
    raw={k:[] for k in range(6)}
    for e in bm.edges:
        if e.is_boundary:
            a,b=[v.index for v in e.verts]
            if membership[a]!=membership[b]:
                raise RuntimeError("A source boundary crosses independent islands")
            raw[membership[a]].append((a,b))
    # Boundary topology must be a CLOSED ring per component. Preserve every
    # index and actual original world-space coordinate for patch generation.
    loops={}
    mat=o.matrix_world
    pos={v.index:mat@v.co for v in bm.verts}
    for k,edges in raw.items():
        adj=defaultdict(list)
        for a,b in edges:
            adj[a].append(b);adj[b].append(a)
        if len(adj)<25 or any(len(neigh)!=2 for neigh in adj.values()):
            raise RuntimeError("Non-loop/open junction in original boundary "+str(k))
        first=min(adj.keys());prev=None;current=first;sequence=[]
        for _ in range(len(adj)+1):
            sequence.append(current)
            nxt=next(n for n in adj[current] if n!=prev)
            if nxt==first:break
            prev,current=current,nxt
        if len(sequence)!=len(adj):
            raise RuntimeError("More than one boundary loop in source island "+str(k))
        loops[k]=sequence

    palm=loops[0]
    ext=max((max(pos[v][axis] for v in range(len(bm.verts)))-
             min(pos[v][axis] for v in range(len(bm.verts))))
            for axis in range(3))
    report={"mesh":o.name,"original_bones":len(arm.data.bones),
            "original_vertices":len(bm.verts),
            "original_triangles":len(bm.faces),
            "connected_components":len(islands),
            "world_extent":ext,
            "components":[]}
    for i,ring in loops.items():
        # Finger contact is a *section* of a long open ring, not a small
        # circular root cap. Choose nearest ORIGINAL palm boundary points.
        pairs=[]
        for v in ring:
            p=pos[v]
            q=min(palm,key=lambda u:(pos[u]-p).length_squared)
            dist=(pos[q]-p).length
            pairs.append({"finger_vertex":v,"palm_vertex":q,
                          "distance":round(dist,10)})
        distances=sorted(x["distance"] for x in pairs)
        proximity=ext*.025
        contacts=[x for x in pairs if x["distance"]<=proximity]
        report["components"].append({
            "id":i,"vertices":len(islands[i]),
            "boundary_vertices":len(ring),
            "boundary_loop":ring,
            "nearest_palm_dist_min":distances[0],
            "nearest_palm_dist_median":distances[len(distances)//2],
            "root_close_pairs":contacts if i>0 else [],
            "root_close_count":len(contacts) if i>0 else 0,
            "root_world_positions":[[round(c,9) for c in pos[v]] for v in ring]
        })
    bm.free()
    path=out/"authentic_finger_palm_contact_atlas.json"
    path.write_text(json.dumps(report,indent=2,ensure_ascii=False),
                    encoding="utf-8")
    print("ROKAS SOURCE SEAM MAP ONLY: no FBX edits")
    for x in report["components"]:
        print("island",x["id"],"verts",x["vertices"],
              "boundary",x["boundary_vertices"],
              "root close contacts",x["root_close_count"],
              "min dist",x["nearest_palm_dist_min"])
    print("Saved",path)

if __name__=="__main__":
    main()
