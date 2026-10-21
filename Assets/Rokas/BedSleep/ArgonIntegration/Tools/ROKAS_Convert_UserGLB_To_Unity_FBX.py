"""Convert the exact animated hand GLB supplied by the user to Unity Generic FBX.
Blender -b --python SCRIPT -- INPUT.glb OUTPUT.fbx OUTPUT.blend
Keep the user's one-mesh skin, texture/UV and original 23-joint animation.
"""
import bpy
import sys
import os
import traceback

def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(args) != 3:
        raise RuntimeError("Usage: INPUT.glb OUTPUT.fbx OUTPUT.blend")
    source, target, blend = [os.path.abspath(p) for p in args]
    if not os.path.isfile(source):
        raise RuntimeError("Exact user GLB missing: " + source)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=source)
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    rigs = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if len(meshes) != 1 or len(rigs) != 1:
        raise RuntimeError("Expected 1 user skin mesh and 1 rig")
    mesh, rig = meshes[0], rigs[0]
    if len(mesh.data.vertices) < 40000 or len(rig.data.bones) < 20:
        raise RuntimeError("User 23-joint model not loaded")
    if not mesh.data.uv_layers:
        raise RuntimeError("User GLB UV lost")
    if not any(m.type == "ARMATURE" and m.object == rig for m in mesh.modifiers):
        raise RuntimeError("Native GLB skin modifier lost")
    for name in ("root","forearm","wrist","palm","index_proximal",
                 "middle_proximal","ring_proximal","little_proximal","thumb_proximal"):
        if name not in rig.data.bones:
            raise RuntimeError("Missing authentic user rig joint " + name)
    if not bpy.data.actions:
        raise RuntimeError("User GLB animation is missing")
    for image in bpy.data.images:
        if image.source == "FILE" and not image.packed_file:
            try: image.pack()
            except Exception as exc: print("[ROKAS] pack warning", image.name, exc)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    os.makedirs(os.path.dirname(blend), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend,compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=target,use_selection=True,
        object_types={"ARMATURE","MESH"},apply_unit_scale=True,
        axis_forward="-Z",axis_up="Y",add_leaf_bones=False,
        use_anim=True,bake_anim=True,bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=True,bake_anim_use_all_actions=True,
        bake_anim_step=1.,bake_anim_simplify_factor=0.,
        path_mode="AUTO",use_mesh_modifiers=False)
    if os.path.getsize(target) < 1500000:
        raise RuntimeError("Unity FBX export implausibly small")
    print("[ROKAS USER GLB CONVERSION PASS] skin_verts=%d bones=%d actions=%d" %
          (len(mesh.data.vertices),len(rig.data.bones),len(bpy.data.actions)))

if __name__ == "__main__":
    try: main()
    except Exception as exc:
        print("[ROKAS USER GLB CONVERSION FAILED]",repr(exc))
        traceback.print_exc()
        sys.exit(19)
