#!/usr/bin/env python3
"""ROKAS source-faithful LONGITUDINAL FINGER SURFACE seam rehabilitation.

Distinct from MCP bridge: two lips of each finger's open *longitudinal*
boundary are at EXACTLY identical positions. This surgery may join only
coordinate-identical boundary vertices IN THE SAME original island. Never
voxel-remesh, invent cylinders, rescale, change materials, or overwrite the
25-bone source. Tip pole merging is gated separately; exclude it if it creates
non-manifold or collapsed polygons. QA ONLY, no production V10 usage.
"""
import bpy,bmesh,sys,json,math,pathlib
from collections import defaultdict,Counter

def reject(why): raise RuntimeError("ROKAS SOURCE SURFACE SAFETY: "+why)

def connected(bm):
    seen=set(); groups=[]
    for v in bm.verts:
        if v in seen: continue
        stack=[v];group=set()
        while stack:
            p=stack.pop()
            if p in seen:continue
            seen.add(p);group.add(p)
            stack.extend(other for e in p.link_edges for other in e.verts
                         if other not in seen)
        groups.append(group)
    return sorted(groups,key=len,reverse=True)

def border_stats(bm):
    return {
        "vertices":len(bm.verts),
        "faces":len(bm.faces),
        "boundary_edges":sum(e.is_boundary for e in bm.edges),
        "nonmanifold_high_degree_edges":sum(len(e.link_faces)>2 for e in bm.edges),
        "loose_edges":sum(len(e.link_faces)==0 for e in bm.edges),
        "degenerate_faces":sum(f.calc_area()<1e-13 for f in bm.faces),
        "connected_islands":len(connected(bm))
    }

def ring(island):
    adj=defaultdict(list)
    for v in island:
        for e in v.link_edges:
            if e.is_boundary:
                a,b=e.verts
                if a in island and b in island:
                    if b not in adj[a]:adj[a].append(b)
                    if a not in adj[b]:adj[b].append(a)
    if not adj or any(len(n)!=2 for n in adj.values()):
        reject("Original boundary is not a simple 2-neighbor cycle")
    start=min(adj,key=lambda v:v.index)
    chain=[];prev=None;current=start
    for _ in range(len(adj)+1):
        chain.append(current)
        successor=next(n for n in adj[current] if n!=prev)
        if successor==start:break
        prev,current=current,successor
    if len(chain)!=len(adj):reject("Original sheet had multiple boundary cycles")
    return chain

def main():
    a=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(a)!=2:reject("Usage: blender -b -P source_surface_seam_weld.py -- SOURCE.fbx OUTDIR")
    src=pathlib.Path(a[0]);out=pathlib.Path(a[1]);out.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action="SELECT");bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(src),use_anim=False)
    skins=[o for o in bpy.data.objects if o.type=="MESH" and
           "Keiko_Hand_Skin_Mesh" in o.name]
    if len(skins)!=1:reject("Exact original named hand skin required")
    original=skins[0];arm=original.find_armature()
    if arm is None or len(arm.data.bones)!=25:reject("Wrong original source rig")
    clone=original.copy();clone.data=original.data.copy()
    clone.name="ROKAS_Argon_LongitudinalSeam_Skinned_QA_ONLY"
    bpy.context.collection.objects.link(clone)
    clone.matrix_world=original.matrix_world.copy()
    for mod in list(clone.modifiers):
        if mod.type=="ARMATURE":clone.modifiers.remove(mod)
    bm=bmesh.new();bm.from_mesh(clone.data)
    bm.verts.ensure_lookup_table();bm.verts.index_update()
    groups=connected(bm)
    before=border_stats(bm)
    if len(groups)!=6 or before["boundary_edges"]!=1028 or len(bm.verts)!=12240:
        reject("Authentic six-island source topology fingerprint changed "+str(before))
    span=max(max(v.co[i] for v in bm.verts)-min(v.co[i] for v in bm.verts)
             for i in range(3))
    tol=max(span*1e-7,1e-9)
    original_area=sum(f.calc_area() for f in bm.faces)
    total_welded=0;island_reports=[]
    # A per-component vertex map means two different nearby FINGERS can
    # NEVER be accidentally welded, regardless of their spatial distance.
    for island_id,part in enumerate(groups):
        outline=ring(part)
        expected=288 if island_id==0 else 148
        if len(outline)!=expected:reject("Unexpected original contour length")
        coord_clusters=defaultdict(list)
        for vertex in outline:
            key=tuple(round(float(vertex.co[k])/tol) for k in range(3))
            coord_clusters[key].append(vertex)
        distribution=Counter(len(c) for c in coord_clusters.values())
        if island_id==0:
            if distribution[2]<75:reject("Expected duplicated palm sheet lip")
        else:
            if distribution[2]<40 or distribution[31]!=1:
                reject("Expected 44 exact long finger lip pairs and 31-vertex pole")
        targetmap={}
        paired=0;tip_pole=0
        for cluster in coord_clusters.values():
            if len(cluster)==2:
                x,y=sorted(cluster,key=lambda v:v.index)
                if (x.co-y.co).length>tol:reject("False lip pair proximity")
                targetmap[y]=x;paired+=1
            elif len(cluster)>2:
                # Collapsed tip is a *source defect*: do not merge all 31
                # source tip vertices blindly. It may destroy actual local
                # fan topology; capture for next guarded repair instead.
                tip_pole+=len(cluster)
        island_reports.append({"island":island_id,
            "boundary_before":len(outline),
            "exact_lip_pairs":paired,
            "unresolved_tip_pole_source_vertices":tip_pole,
            "cluster_distribution":dict(distribution)})
        if not targetmap:reject("No actual original duplicates found")
        bmesh.ops.weld_verts(bm,targetmap=targetmap)
        total_welded+=paired
    bm.verts.index_update();bm.verts.ensure_lookup_table()
    bm.normal_update()
    after=border_stats(bm)
    final_area=sum(f.calc_area() for f in bm.faces)
    report={"source":str(src.name),"original_25_bones":len(arm.data.bones),
      "before":before,"after":after,"per_island":island_reports,
      "source_exact_sheet_lips_welded":total_welded,
      "surface_area_change_fraction":abs(final_area-original_area)/max(1e-12,original_area),
      "source_clipped_fingertip_poles_remain_open":True,
      "v10_changed":False,"accepted_into_game":False}
    (out/"authentic_longitudinal_seam_report.json").write_text(
       json.dumps(report,indent=2),encoding="utf-8")
    print("ROKAS AUTHENTIC LONGITUDINAL SURFACE",json.dumps(report),flush=True)
    if after["connected_islands"]!=6:
        reject("Finger-to-palm merge is a SEPARATE stage; should remain 6")
    if after["boundary_edges"]>=before["boundary_edges"]*.80:
        reject("Real long finger slit not reduced")
    if after["nonmanifold_high_degree_edges"]>before["nonmanifold_high_degree_edges"]:
        reject("Seam weld created non-manifold high degree edges")
    if after["loose_edges"]>before["loose_edges"]:
        reject("Weld created loose geometry")
    if report["surface_area_change_fraction"]>1e-6:
        reject("Original anatomical surface area changed")
    # This diagnostic stage intentionally leaves root circumferences OPEN.
    bm.to_mesh(clone.data);bm.free();clone.data.update()
    skin=clone.modifiers.new("ROKAS_Genuine_25_Bone_Skin","ARMATURE")
    skin.object=arm;skin.use_vertex_groups=True;skin.use_bone_envelopes=False
    bpy.ops.object.select_all(action="DESELECT")
    clone.select_set(True);arm.select_set(True)
    bpy.context.view_layer.objects.active=clone
    bpy.ops.export_scene.fbx(filepath=str(out/"ROKAS_AuthenticSeamWeld_QA_ONLY.fbx"),
        use_selection=True,object_types={"ARMATURE","MESH"},
        use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False)
    print("ROKAS: genuine sheet lips joined, TIP/ROOT HOLES STILL REQUIRE FIX; do not merge")
if __name__=="__main__":main()
