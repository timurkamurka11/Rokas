"""
ROKAS / original Argon anatomical source audit (Blender 3.6+ / 4.x).
READ-ONLY: never overwrites FBX, meshes, UVs, weights, animation or scene.

Run from the existing project root:
blender -b -noaudio --python Assets/Rokas/BedSleep/ArgonIntegration/Tools/audit_argon_hand_anatomy.py -- \
 Assets/Rokas/BedSleep/ArgonSource/Models/ROKAS_Keiko_FPS_Arm_Rigged.fbx \
 Artifacts/ArgonAnatomyAudit

Report is REQUIRED before destructive finger / palm topology reconstruction.
It does not claim to have welded six disconnected skin components.
"""
import bpy
import json
import math
import pathlib
import statistics
import sys
from collections import defaultdict
from mathutils import Vector
from mathutils.kdtree import KDTree

BONE_NAMES = ["Thumb", "Index", "Middle", "Ring", "Little"]
SKIN = "Keiko_Hand_Skin_Mesh"

def fail(reason):
    raise RuntimeError("[ROKAS ANATOMY AUDIT] " + reason)

def connected_components(mesh):
    n = len(mesh.vertices)
    adjacency = [[] for _ in range(n)]
    for poly in mesh.polygons:
        verts = list(poly.vertices)
        for a,b in zip(verts, verts[1:]+verts[:1]):
            adjacency[a].append(b)
            adjacency[b].append(a)
    marked = bytearray(n)
    out = []
    for seed in range(n):
        if marked[seed]:
            continue
        stack = [seed]
        marked[seed] = 1
        result = []
        while stack:
            k = stack.pop()
            result.append(k)
            for nxt in adjacency[k]:
                if not marked[nxt]:
                    marked[nxt] = 1
                    stack.append(nxt)
        out.append(result)
    return sorted(out, key=len, reverse=True)

def boundary_edges(mesh):
    uses = defaultdict(int)
    for face in mesh.polygons:
        verts = list(face.vertices)
        for a,b in zip(verts, verts[1:]+verts[:1]):
            uses[(min(a,b),max(a,b))] += 1
    return [edge for edge, count in uses.items() if count==1], sum(c>2 for c in uses.values())

def vector(v):
    return [round(float(x),7) for x in v]

def inspect_boundary_loops(skin, mesh, boundary, piece_map, arm):
    # Each ring is a connected subgraph of all edges used by exactly
    # one polygon. Closed loops with degree-two vertices are stitchable
    # only after identifying their anatomical finger/palm assignment.
    graph=defaultdict(set)
    for a,b in boundary:
        graph[a].add(b)
        graph[b].add(a)
    bones=["Arm_Wrist","Thumb_MCP","Index_MCP","Middle_MCP",
           "Ring_MCP","Little_MCP"]
    centers={n:arm.matrix_world@arm.pose.bones[n].head for n in bones
             if n in arm.pose.bones}
    seen=set()
    loops=[]
    for first in graph:
        if first in seen:
            continue
        stack=[first]
        seen.add(first)
        group=[]
        while stack:
            node=stack.pop()
            group.append(node)
            for nxt in graph[node]:
                if nxt not in seen:
                    seen.add(nxt)
                    stack.append(nxt)
        world=[skin.matrix_world@mesh.vertices[n].co for n in group]
        centroid=sum(world,Vector())/len(world)
        nearest=sorted((float((centroid-v).length),name)
                       for name,v in centers.items())
        loops.append({
            "id":len(loops),
            "skin_component":piece_map[group[0]],
            "vertex_count":len(group),
            "closed_degree_two":all(len(graph[n])==2 for n in group),
            "center_world":vector(centroid),
            "closest_joint":nearest[0][1],
            "joint_distance_m":nearest[0][0],
            "perimeter_m":sum(float((skin.matrix_world@
                mesh.vertices[a].co-skin.matrix_world@
                mesh.vertices[b].co).length)
                for a,b in boundary if a in group and b in group)
        })
    return sorted(loops,key=lambda d:(d["skin_component"],d["closest_joint"]))

def main():
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(args)!=2:
        fail("Usage: -- source-Argon-arm.fbx output-report-folder")
    source=pathlib.Path(args[0]).expanduser().resolve()
    target=pathlib.Path(args[1]).expanduser().resolve()
    if not source.is_file() or source.suffix.lower()!=".fbx":
        fail("Source must be the REAL Argon source FBX: "+str(source))
    target.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True)
    skin=next((o for o in bpy.data.objects
               if o.type=="MESH" and o.name==SKIN), None)
    if skin is None:
        fail("Authentic Keiko_Hand_Skin_Mesh missing")
    arm=next((mod.object for mod in skin.modifiers
              if mod.type=="ARMATURE" and mod.object is not None),None)
    if arm is None or arm.type!="ARMATURE":
        fail("Skin has no valid deform armature")
    missing=[x for name in BONE_NAMES for x in
             ((name+"_MCP",name+"_PIP",name+"_DIP")
              if name!="Thumb" else ("Thumb_CMC","Thumb_MCP","Thumb_IP"))
             if x not in arm.pose.bones]
    if missing:
        fail("Missing deform bones: "+", ".join(missing))
    mesh=skin.data
    pieces=connected_components(mesh)
    edge_list,nonmanifold=boundary_edges(mesh)
    bounds_by_piece={}
    index_to_piece={}
    for i,ids in enumerate(pieces):
        for v in ids:
            index_to_piece[v]=i
        world=[skin.matrix_world @ mesh.vertices[v].co for v in ids]
        mn=[min(v[j] for v in world) for j in range(3)]
        mx=[max(v[j] for v in world) for j in range(3)]
        bounds_by_piece[i]={"vertices":len(ids),
            "min":vector(mn),"max":vector(mx)}
    loop_inventory=inspect_boundary_loops(skin,mesh,edge_list,index_to_piece,arm)
    boundary_by_piece=defaultdict(int)
    for a,b in edge_list:
        if index_to_piece[a]==index_to_piece[b]:
            boundary_by_piece[index_to_piece[a]]+=1
    # The source is documented as one palm island and five finger islands.
    # Never guess by position alone: cross-check each component's centroid
    # with anatomically named bone segments and output the assignment.
    palm_id=0
    palm_ids=pieces[palm_id]
    palm_tree=KDTree(len(palm_ids))
    for i,v in enumerate(palm_ids):
        palm_tree.insert(skin.matrix_world @ mesh.vertices[v].co,i)
    palm_tree.balance()
    finger_info=[]
    assigned={}
    for name in BONE_NAMES:
        proximal=arm.matrix_world @ arm.pose.bones[
            name+"_MCP" if name!="Thumb" else "Thumb_MCP"].head
        distal_name=name+"_DIP" if name!="Thumb" else "Thumb_IP"
        distal=arm.matrix_world @ arm.pose.bones[distal_name].tail
        axis=distal-proximal
        axis_len=axis.length
        if axis_len<.001:
            fail(name+": degenerate finger joint locations")
        axis.normalize()
        candidates=[]
        for piece_id,ids in enumerate(pieces):
            if piece_id==palm_id:
                continue
            # Select vertices near the proximal bone to identify the
            # TRUE finger island, independent of corrupted skin weights.
            near=[]
            for vid in ids:
                w=skin.matrix_world @ mesh.vertices[vid].co
                along=(w-proximal).dot(axis)
                off=(w-proximal-axis*along).length
                if -.2*axis_len<=along<=.4*axis_len:
                    near.append((off,vid))
            if near:
                near.sort()
                candidates.append((statistics.median(a for a,_ in near[:min(80,len(near))]),
                                   piece_id,near))
        if not candidates:
            fail(name+": no proximal surface candidates")
        candidates.sort()
        _,piece_id,near=candidates[0]
        if piece_id in assigned.values():
            fail("Two fingers assigned to one disconnected skin island; manual review required")
        assigned[name]=piece_id
        # A small proximal sample estimates palm-gap WITHOUT changing UV.
        closest=[]
        for _,vid in near[:min(120,len(near))]:
            w=skin.matrix_world @ mesh.vertices[vid].co
            _,_,dist=palm_tree.find(w)
            closest.append(float(dist))
        vertices=pieces[piece_id]
        influence=defaultdict(float)
        # Vertex groups follow the actual imported names, unlike prior
        # malformed positional remapping from the GLB.
        for vid in vertices:
            for weight in mesh.vertices[vid].groups:
                influence[skin.vertex_groups[weight.group].name]+=weight.weight
        finger_info.append({
            "finger":name, "component":piece_id,
            "vertices":len(vertices), "boundary_edges":
                boundary_by_piece.get(piece_id,0),
            "has_open_boundary":bool(boundary_by_piece.get(piece_id,0)),
            "proximal_to_palm_min_m":min(closest),
            "proximal_to_palm_median_m":statistics.median(closest),
            "dominant_weights":sorted(influence.items(),
                                     key=lambda v:-v[1])[:8],
            "mcp_world":vector(proximal),
            "dip_or_ip_world":vector(distal)
        })
    if len(pieces)!=6 or len(set(assigned.values()))!=5:
        fail("Unexpected island count: STOP before any topology surgery")
    face_loops=sum(1 for x in finger_info if x["has_open_boundary"])
    blockers=[]
    if boundary_by_piece.get(palm_id,0)==0:
        blockers.append("Palm is closed/capped: select proximal region and remove caps before any edge bridge")
    if face_loops!=5:
        blockers.append("At least one finger shell has no open edge boundary: do not auto-bridge watertight shells")
    if nonmanifold:
        blockers.append(f"{nonmanifold} non-manifold edges: resolve before joining the topology")
    report={
        "project":"ROKAS",
        "source":str(source),
        "mesh":skin.name,
        "vertex_count":len(mesh.vertices),
        "triangles":sum(len(p.vertices)-2 for p in mesh.polygons),
        "skin_uv_sets":len(mesh.uv_layers),
        "vertex_groups":len(skin.vertex_groups),
        "blend_shapes":len(mesh.shape_keys.key_blocks) if mesh.shape_keys else 0,
        "components":len(pieces),
        "component_bounds":bounds_by_piece,
        "total_open_boundary_edges":len(edge_list),
        "boundary_loop_inventory":loop_inventory,
        "nonmanifold_edges":nonmanifold,
        "palm_open_boundary_edges":boundary_by_piece.get(palm_id,0),
        "fingers":finger_info,
        "geometry_reconstruction_is_proven":False,
        "safe_to_apply_automatic_stitch":False,
        "blocking_conditions":blockers or [
            "Proximity alone cannot validate seam edge flow, webbing, UV or skinned deformation; inspect authoring views"
        ],
        "needed_next_pass":[
            "Capture index/pinky base at 0/25/50/75/100 percent flex",
            "Construct continuous index and little proximal palm bridge with controlled loops",
            "Transfer/smooth skin weights, keep original albedo/UV texel density",
            "Add corrective shapes for bent MCP/PIP and closed fist",
            "Bake/export to test candidate FBX, validate five fingers at 60 FPS target",
            "Only then replace the production candidate with visual evidence"
        ]
    }
    out=target/"ROKAS_Argon_Anatomy_DefectMap.json"
    out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    print("[ROKAS ANATOMY AUDIT] " + str(out),flush=True)
    print("[ROKAS ANATOMY AUDIT] "+
          f"{len(mesh.vertices)} vertices, {len(pieces)} shells; " +
          f"palm boundary edges={boundary_by_piece.get(palm_id,0)}",flush=True)
    for f in finger_info:
        print("[ROKAS ANATOMY AUDIT] " + f["finger"] +
              " palm gap min="+format(f["proximal_to_palm_min_m"],".6f")+
              "m; open edges="+str(f["boundary_edges"]),flush=True)
    for loop in loop_inventory:
        print("[ROKAS RIM INVENTORY] component="+str(loop["skin_component"])+
              " ring="+str(loop["id"])+" n="+str(loop["vertex_count"])+
              " closed="+str(loop["closed_degree_two"])+
              " nearest="+loop["closest_joint"]+
              " distance="+format(loop["joint_distance_m"],".5f"),
              flush=True)
    print("[ROKAS ANATOMY AUDIT] No geometry modified. Source FBX unchanged.",flush=True)

if __name__=="__main__":
    main()
