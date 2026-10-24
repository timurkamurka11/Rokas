"""Blender-only, trusted GLB->native skinned FBX recovery.
Usage: blender -b -noaudio --python reexport_argon_master_glb.py -- source.glb output_dir

Never execute untrusted user-packaged build_master_blend.py or copy its code.
This script imports the actual original GLB binary and reexports authentic
Blender FBX 7.4 with real armature deform links. The original Argon FBX generated
outside Blender imports into Unity with zero SkinnedMeshRenderers.
"""
import bpy
import pathlib
import sys
import json

args = sys.argv[sys.argv.index("--") + 1:]
if len(args) != 2:
    raise RuntimeError("Usage: -- original-master.glb Unity-Models-output-folder")
glb = pathlib.Path(args[0]).resolve()
dst = pathlib.Path(args[1]).resolve()
dst.mkdir(parents=True, exist_ok=True)
if not glb.is_file():
    raise FileNotFoundError(str(glb))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(glb))

ARM_PARTS = {
    "Keiko_Hand_Skin_Mesh",
    "Keiko_Fingernails_Mesh",
    "Keiko_Homewear_Sleeve_Mesh",
}
WATCH_PARTS = {
    "Watch_TitaniumCase", "Watch_Strap_Upper", "Watch_Strap_Lower",
    "Watch_OLED_Display", "Watch_SapphireGlass",
    "Watch_DigitalCrown_Rotary", "Watch_SideConfirmButton",
}
ARM_JOINTS = [
    "Arm_ForearmRoot", "Arm_ForearmTwist", "Arm_Wrist", "Hand_Palm",
    "Thumb_CMC", "Thumb_MCP", "Thumb_IP", "Thumb_Tip",
    "Index_MCP", "Index_PIP", "Index_DIP", "Index_Tip",
    "Middle_MCP", "Middle_PIP", "Middle_DIP", "Middle_Tip",
    "Ring_MCP", "Ring_PIP", "Ring_DIP", "Ring_Tip",
    "Little_MCP", "Little_PIP", "Little_DIP", "Little_Tip",
    "Wrist_WatchAttachment",
]

meshes = {o.name: o for o in bpy.data.objects if o.type == 'MESH'}
for name in ARM_PARTS | WATCH_PARTS:
    if name not in meshes:
        raise RuntimeError(f"Master GLB does not contain physical mesh {name}: {sorted(meshes)}")
armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
if len(armatures) != 1:
    raise RuntimeError(f"Expected one actual skinned GLB armature; got {len(armatures)}")
armature = armatures[0]
actual_bones = {bone.name for bone in armature.data.bones}
missing = set(ARM_JOINTS) - actual_bones
if missing:
    raise RuntimeError(f"Missing genuine GLB rig bones: {sorted(missing)}")
for name in ARM_PARTS:
    ob = meshes[name]
    has_modifier = any(m.type == 'ARMATURE' and m.object == armature for m in ob.modifiers)
    if not has_modifier or len(ob.vertex_groups) == 0:
        raise RuntimeError(f"{name} is not truly deformable by the imported GLB skeleton")

actions = sorted(a.name for a in bpy.data.actions)
print("[ROKAS ARGON] Genuine Blender imported animation actions:", actions, flush=True)
if len(actions) < 8:
    raise RuntimeError(f"Expected at least 8 real master GLB actions; got {len(actions)}")
bpy.context.scene.render.fps = 30

def select(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]

# Native FBX exporter writes Deformer::Skin, Cluster and BindPose references
# recognized as SkinnedMeshRenderer by actual Unity ModelImporter.
arm_file = dst / "ROKAS_Keiko_FPS_Arm_Rigged.fbx"
select([armature] + [meshes[n] for n in sorted(ARM_PARTS)])
bpy.ops.export_scene.fbx(
    filepath=str(arm_file), use_selection=True, use_visible=False,
    object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
    use_mesh_modifiers=True, add_leaf_bones=False,
    apply_unit_scale=True, global_scale=1.0,
    bake_anim=True, bake_anim_use_all_bones=True,
    bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
    path_mode="AUTO", embed_textures=False,
)
watch_file = dst / "ROKAS_Smartwatch_HighDetail.fbx"
select([meshes[n] for n in sorted(WATCH_PARTS)])
bpy.ops.export_scene.fbx(
    filepath=str(watch_file), use_selection=True,
    object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
    use_mesh_modifiers=True, apply_unit_scale=True, global_scale=1.0,
    bake_anim=False, path_mode="AUTO", embed_textures=False,
)
for p in (arm_file, watch_file):
    if not p.exists() or p.stat().st_size < 100000:
        raise RuntimeError(f"Native Blender FBX export is empty/invalid: {p}")
print("[ROKAS ARGON] Native Blender FBX conversion succeeded:",
      json.dumps({p.name: p.stat().st_size for p in (arm_file, watch_file)}), flush=True)
