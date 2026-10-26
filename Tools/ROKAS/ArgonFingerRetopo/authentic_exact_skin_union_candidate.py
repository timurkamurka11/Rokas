#!/usr/bin/env python3
"""ROKAS exact ORIGINAL palm + 5 original finger skins union — EXPERIMENT ONLY.

Unlike destructive voxel remesh, this uses Blender Exact Mesh Boolean on
closed COPY shells of the authentic 25-bone FBX source. The original Argon
meshes, materials, textures, and production V10 are NEVER overwritten.
Bail if actual topology, UV/weights, bone hierarchy or silhouette fail.
Never accept mere 1-island technical PASS as final anatomical approval.
"""
import sys,json,math,pathlib
import bpy,bmesh
from mathutils import Vector

def stop(msg):raise RuntimeError("ROKAS AUTHENTIC EXACT UNION SAFETY: "+msg)
def active_only(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
def bounds(obj):
    pts=[obj.matrix_world@Vector(x) for x in obj.bound_box]
    return [(min(p[i] for p in pts),max(p[i] for p in pts)) for i in range(3)]
def topology(obj):
    bm=bmesh.new();bm.from_mesh(obj.data)
    visited=set();isles=0
    for v in bm.verts:
        if v in visited:continue
        isles+=1;stack=[v]
        while stack:
            x=stack.pop()
            if x in visited:continue
            visited.add(x)
            stack.extend(w for e in x.link_edges for w in e.verts if w not in visited)
    result={"vertices":len(bm.verts),"faces":len(bm.faces),
        "islands":isles,"boundary":sum(e.is_boundary for e in bm.edges),
        "nonmanifold_high_degree":sum(len(e.link_faces)>2 for e in bm.edges),
        "zero_area_faces":sum(f.calc_area()<1e-12 for f in bm.faces)}
    bm.free()
    return result
def seal(obj,epsilon):
    bm=bmesh.new();bm.from_mesh(obj.data)
    boundary=[e for e in bm.edges if e.is_boundary]
    if not boundary:stop("Original detached shell missing a real open ring")
    before=len(boundary)
    # Source 31-vertex tip pole has coincident zero-area triangles.
    # Remove ONLY degeneracies, then cap existing genuine boundary loops.
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=epsilon*.5)
    boundary=[e for e in bm.edges if e.is_boundary]
    added=bmesh.ops.holes_fill(bm,edges=boundary,sides=0)
    if not added.get("faces"):stop("Actual original boundary cap operation failed")
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    result=topology(obj)
    if result["boundary"] or result["nonmanifold_high_degree"] or result["islands"]!=1:
        stop("Original source cannot safely close for EXACT boolean "+str(result))
    result["original_open_boundary"]=before
    result["temporary_source_hole_caps"]=len(added["faces"])
    return result
def main():
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(args)!=2:stop("original FBX / QA output directory required")
    source=pathlib.Path(args[0]);out=pathlib.Path(args[1]);out.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source),use_anim=False)
    skin=[o for o in bpy.data.objects if o.type=="MESH" and "Keiko_Hand_Skin_Mesh" in o.name]
    if len(skin)!=1:stop("Original authentic hand skin not unique")
    original=skin[0];armature=original.find_armature()
    if armature is None or len(armature.data.bones)!=25:stop("True source 25-bone rig required")
    bbox_orig=bounds(original)
    diameter=max(hi-lo for lo,hi in bbox_orig)
    first=topology(original)
    if first["islands"]!=6 or first["boundary"]!=1028 or first["vertices"]!=12240:
        stop("Source authentic shape differs from evaluated atlas "+str(first))
    clone=original.copy();clone.data=original.data.copy()
    clone.name="ROKAS_ExactBooleanGenuineSixIslands_CANDIDATE_ONLY"
    bpy.context.collection.objects.link(clone)
    clone.matrix_world=original.matrix_world.copy()
    for mod in list(clone.modifiers):
        if mod.type=="ARMATURE":clone.modifiers.remove(mod)
    eps=diameter*1e-6
    # Weld ONLY coordinates identical inside <0.5 micron-scale world span.
    bm=bmesh.new();bm.from_mesh(clone.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=eps)
    bm.to_mesh(clone.data);bm.free();clone.data.update()
    afterweld=topology(clone)
    if afterweld["islands"]!=6 or afterweld["boundary"]>300 or afterweld["nonmanifold_high_degree"]:
        stop("Source exact UV-pole seam repair distorted six components "+str(afterweld))
    # Divide ORIGINAL repaired surface into its genuine source islands,
    # NEVER fabricate 5 replacement cylinder fingers.
    active_only(clone)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    shells=[o for o in bpy.context.selected_objects
            if o.type=="MESH" and "Keiko_Hand_Skin_Mesh" not in o.name]
    if len(shells)!=6:
        # Blender may retain original object name on one separated part.
        shells=[o for o in bpy.context.selected_objects if o.type=="MESH" and o!=original]
    if len(shells)!=6:stop("Six actual loose shell objects not found: "+str([x.name for x in shells]))
    shells.sort(key=lambda o:len(o.data.vertices),reverse=True)
    palm=shells[0];digits=shells[1:]
    if len(palm.data.vertices)<4800:stop("Palm shell destroyed")
    per_shell=[]
    for obj in shells:
        per_shell.append({"name":obj.name,**seal(obj,eps)})
    # Keep original 2048 source skin material on all shells.
    source_materials=list(original.data.materials)
    for obj in shells:
        obj.data.materials.clear()
        for source_mat in source_materials:obj.data.materials.append(source_mat)
    report={"source_bones":len(armature.data.bones),
       "source":first,"exact_source_weld":afterweld,"pre_union_source_shells":per_shell,
       "union_operations":[],"production_V10_changed":False,"visual_acceptance":False}
    path=out/"exact_original_skin_union_report.json"
    path.write_text(json.dumps(report,indent=2),encoding="utf-8")
    # Apply exact Boolean UNION one original finger at a time. Temporary
    # caps only define solid manifold operands; the union removes overlap
    # and should naturally carve actual finger sockets into the palm.
    for k,digit in enumerate(digits):
        active_only(palm)
        mod=palm.modifiers.new("ROKAS_AUTHENTIC_BooleanUnion_Digit"+str(k),"BOOLEAN")
        mod.operation='UNION';mod.solver='EXACT';mod.object=digit
        try:
            bpy.ops.object.modifier_apply(modifier=mod.name)
        except Exception as exc:
            stop("Blender exact original union failed on source digit "+
                 str(k)+": "+repr(exc))
        report["union_operations"].append({"digit":k,"state":topology(palm)})
        path.write_text(json.dumps(report,indent=2),encoding="utf-8")
        bpy.data.objects.remove(digit,do_unlink=True)
        interim=topology(palm)
        if interim["vertices"]>100000 or interim["nonmanifold_high_degree"]:
            stop("Boolean union created exploding/invalid geometry "+str(interim))
    finished=topology(palm)
    later=bounds(palm)
    drift=max(abs(bbox_orig[i][j]-later[i][j])
        for i in range(3) for j in range(2))/diameter
    report["candidate"]=finished
    report["relative_world_bounds_drift"]=drift
    report["candidate_is_not_production_approved"]=True
    path.write_text(json.dumps(report,indent=2),encoding="utf-8")
    print("ROKAS ACTUAL EXACT MESH BOOLEAN",json.dumps(report),flush=True)
    if finished["islands"]!=1 or finished["boundary"]!=0:
        stop("Exact union did NOT produce genuine one piece watertight glove")
    if finished["nonmanifold_high_degree"] or finished["zero_area_faces"]>0:
        stop("Exact original mesh union left degenerate or nonmanifold faces")
    if drift>.025:stop("Original source silhouette drift unsafe "+str(drift))
    # Source-faithful original skin UV/weights via nearest source POLYGON
    # interpolation on authentic original unmodified FBX, not new colors.
    active_only(palm)
    for name in ("ROKAS_Original25BoneWeightTransfer","ROKAS_OriginalSourceUVTransfer"):
        if palm.modifiers.get(name):palm.modifiers.remove(palm.modifiers[name])
    weight=palm.modifiers.new("ROKAS_Original25BoneWeightTransfer","DATA_TRANSFER")
    weight.object=original;weight.use_vert_data=True
    weight.data_types_verts={'VGROUP_WEIGHTS'}
    weight.vert_mapping='POLYINTERP_NEAREST'
    weight.layers_vgroup_select_src='ALL'
    weight.layers_vgroup_select_dst='NAME'
    bpy.ops.object.modifier_apply(modifier=weight.name)
    if not palm.data.uv_layers:palm.data.uv_layers.new(name="UVMap")
    uv=palm.modifiers.new("ROKAS_OriginalSourceUVTransfer","DATA_TRANSFER")
    uv.object=original;uv.use_loop_data=True;uv.data_types_loops={'UV'}
    uv.loop_mapping='POLYINTERP_NEAREST'
    bpy.ops.object.modifier_apply(modifier=uv.name)
    for p in palm.data.polygons:p.use_smooth=True
    if not palm.vertex_groups:stop("UV transfer lost authentic finger bone weights")
    weighted=sum(bool(v.groups) and sum(g.weight for g in v.groups)>.50
                 for v in palm.data.vertices)
    report["skin_weight_coverage"]=weighted/max(len(palm.data.vertices),1)
    path.write_text(json.dumps(report,indent=2),encoding="utf-8")
    if report["skin_weight_coverage"]<.98:
        stop("Source transfer lost real 25-bone skin influences")
    arm=palm.modifiers.new("ROKAS_25BONE_AUTHENTIC_ARMATURE","ARMATURE")
    arm.object=armature;arm.use_vertex_groups=True;arm.use_bone_envelopes=False
    active_only(palm);armature.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(out/"ROKAS_ExactSourceUnionRig_QA_ONLY.fbx"),
        use_selection=True,object_types={'MESH','ARMATURE'},
        use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False)
    print("ROKAS: pass GEOMETRY ONLY; actual Unity visual review still mandatory")
if __name__=="__main__":main()
