"""Verify bone animation AND live corrective shape keys survive an FBX round trip.

In Blender 3.x multi-action exports can silently zero all morph curves. V17
therefore ships 3 optional paired, single-action FBXs. This importer verifies
they work before the PR candidate can be published. No live Unity edits.
Usage: blender -b -noaudio --python-exit-code 2 --python <this> --
          <single-action FBX> <V17_Grip|V17_Index|V17_Living> <output_dir>
"""
import bpy,json,pathlib,sys
from mathutils import Vector

def require(condition,msg):
    if not condition:raise RuntimeError("[ROKAS V17 PAIRED FBX] "+msg)

def bones_action_score(action,begin,peak):
    delta=0.
    for fc in action.fcurves:
        if "Index_" in fc.data_path and "rotation_quaternion" in fc.data_path:
            delta+=abs(fc.evaluate(peak)-fc.evaluate(begin))
    return delta

def shape_action_score(action,begin,peak):
    scores={}
    for fc in action.fcurves:
        if "Index_MCP_Corrective" in fc.data_path:
            scores[fc.data_path]=abs(fc.evaluate(peak)-fc.evaluate(begin))
    return max(scores.values(),default=0.)

def mesh_positions(scene,skin):
    depsgraph=bpy.context.evaluated_depsgraph_get()
    eval_obj=skin.evaluated_get(depsgraph)
    evaluated=eval_obj.to_mesh(preserve_all_data_layers=False,depsgraph=depsgraph)
    require(evaluated is not None,"Skin evaluated mesh missing")
    result=[skin.matrix_world@v.co for v in evaluated.vertices]
    eval_obj.to_mesh_clear()
    return result

def main():
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    require(len(args)==3,"Pass exported FBX, clip name, output folder")
    source=pathlib.Path(args[0]).resolve()
    label=args[1]
    out=pathlib.Path(args[2]).resolve()
    require(label in ("V17_Grip","V17_Index","V17_Living"),
            "Unknown supported clip")
    require(source.exists(),"FBX missing: "+str(source))
    out.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source),use_anim=True)
    skin=next((o for o in bpy.data.objects
               if o.type=="MESH" and o.name=="Keiko_Hand_Skin_Mesh"),None)
    require(skin is not None,"Paired FBX mesh name changed")
    arm=next((m.object for m in skin.modifiers if m.type=="ARMATURE"),None)
    require(arm is not None,"Paired FBX lost original armature")
    require(skin.data.shape_keys is not None,"Paired FBX lost corrective shapes")
    begin=1
    peak={"V17_Grip":51,"V17_Index":43,"V17_Living":15}[label]
    actions=list(bpy.data.actions)
    bone_candidates=[a for a in actions if any(
        fc.data_path.startswith("pose.bones[") for fc in a.fcurves)]
    morph_candidates=[a for a in actions if any(
        fc.data_path.startswith("key_blocks[") for fc in a.fcurves)]
    require(bone_candidates and morph_candidates,
            "FBX lost either armature or corrective animation channels")
    bones=max(bone_candidates,key=lambda a:bones_action_score(a,begin,peak))
    morph=max(morph_candidates,key=lambda a:shape_action_score(a,begin,peak))
    bone_value=bones_action_score(bones,begin,peak)
    shape_curve_value=shape_action_score(morph,begin,peak)
    print("[ROKAS V17 PAIRED] chosen",label,
          "armatureAction",bones.name,"correctiveAction",morph.name,
          "boneCurveDelta",bone_value,"shapeCurveDelta",shape_curve_value,
          flush=True)
    require(bone_value>.03,
            "Exported clip has no meaningful index finger bone rotation")
    require(shape_curve_value>.01 if label=="V17_Living" else shape_curve_value>.15,
            "Corrective animation remained ZERO after single-action FBX export")
    arm.animation_data_create().action=bones
    skin.data.shape_keys.animation_data_create().action=morph
    scene=bpy.context.scene
    scene.frame_set(begin)
    bpy.context.view_layer.update()
    rest=mesh_positions(scene,skin)
    rest_shape=skin.data.shape_keys.key_blocks["Index_MCP_Corrective"].value
    scene.frame_set(peak)
    bpy.context.view_layer.update()
    bent=mesh_positions(scene,skin)
    flex_shape=skin.data.shape_keys.key_blocks["Index_MCP_Corrective"].value
    require(len(rest)==len(bent),"Vertex topology changed in animation")
    require(flex_shape-rest_shape>.005 if label=="V17_Living"
            else flex_shape-rest_shape>.10,
            "Corrective key values do not update with animation")
    groups={g.index:g.name for g in skin.vertex_groups}
    owner=[]
    for vert in skin.data.vertices:
        own_weight=sum(vg.weight for vg in vert.groups
                       if groups.get(vg.group,"").startswith("Index_"))
        if own_weight>.60:
            owner.append(vert.index)
    require(len(owner)>200,"Skin has too few index vertices for QA")
    delta=[(bent[i]-rest[i]).length for i in owner]
    require(max(delta)>.001,
            "Skinned index finger is static in paired FBX")
    report={
        "clip":label,"source":source.name,
        "bone_action":bones.name,"corrective_action":morph.name,
        "bone_curve_delta":bone_value,
        "shape_curve_delta":shape_curve_value,
        "corrective_weight_at_start":rest_shape,
        "corrective_weight_at_peak":flex_shape,
        "index_vertices":len(owner),
        "index_skin_max_displacement_m":max(delta),
        "index_skin_mean_displacement_m":sum(delta)/len(delta),
        "result":"PASS: armature and shape corrections animate inside exported FBX"
    }
    dst=out/("ROKAS_"+label+"_PairedClipQA.json")
    dst.write_text(json.dumps(report,indent=2,ensure_ascii=False),
                   encoding="utf-8")
    print("[ROKAS V17 PAIRED PASS] "+json.dumps(report),flush=True)

if __name__=="__main__":main()
