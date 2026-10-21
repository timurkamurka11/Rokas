"""ROKAS Blender conversion for the exact 23-joint animated user GLB.

Blender 5.x can expand the ONE glTF Mesh into more than one Blender Mesh
object. Keep all original skinned pieces; never reject by raw object count
or include an unrelated helper object in the game.
"""
import bpy
import os
import sys
import traceback


def main():
    args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(args)!=3:
        raise RuntimeError("Usage: INPUT.glb OUTPUT.fbx OUTPUT.blend")
    source,target,blend = [os.path.abspath(x) for x in args]
    if not os.path.isfile(source):
        raise RuntimeError("Exact user GLB source not found: "+source)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=source)
    meshes=[x for x in bpy.data.objects if x.type=="MESH"]
    rigs=[x for x in bpy.data.objects if x.type=="ARMATURE"]
    if len(rigs)!=1 or len(rigs[0].data.bones)<20:
        raise RuntimeError("Expected one authentic 23-joint GLB rig")
    rig=rigs[0]
    bone_names={b.name for b in rig.data.bones}
    skinned=[]
    for mesh in meshes:
        group_count=sum(g.name in bone_names for g in mesh.vertex_groups)
        bound=any(m.type=="ARMATURE" and m.object==rig for m in mesh.modifiers)
        print("[ROKAS] Imported Blender mesh %s vertices=%d armature=%s boneGroups=%d UV=%d" %
              (mesh.name,len(mesh.data.vertices),bound,group_count,len(mesh.data.uv_layers)),flush=True)
        if bound and group_count>0 and len(mesh.data.vertices)>0:
            skinned.append(mesh)
        elif mesh.parent==rig and group_count>0 and len(mesh.data.vertices)>0:
            modifier=mesh.modifiers.new("ROKAS_SourceArmature","ARMATURE")
            modifier.object=rig
            skinned.append(mesh)
    if not skinned:
        raise RuntimeError("GLB contains no valid original rigged skin")
    if not all(mesh.data.uv_layers for mesh in skinned):
        raise RuntimeError("Original skinned GLB part lost UV texture coordinates")
    count=sum(len(mesh.data.vertices) for mesh in skinned)
    if count<40000 or count>125000:
        raise RuntimeError("Unexpected source skin vertex total: "+str(count))
    if len(skinned)>1:
        bpy.ops.object.select_all(action="DESELECT")
        skinned.sort(key=lambda m:len(m.data.vertices),reverse=True)
        for mesh in skinned:mesh.select_set(True)
        bpy.context.view_layer.objects.active=skinned[0]
        bpy.ops.object.join()  # preserves source material slots/UV/bone groups
        skin=bpy.context.view_layer.objects.active
    else:
        skin=skinned[0]
    if len(skin.data.vertices)<40000 or not skin.data.uv_layers:
        raise RuntimeError("Original GLB skin/UV disappeared during Blender import")
    if not any(m.type=="ARMATURE" and m.object==rig for m in skin.modifiers):
        raise RuntimeError("Original rig binding disappeared during joining")
    if len({g.name for g in skin.vertex_groups} & bone_names)<20:
        raise RuntimeError("Original 23-joint skin weights were not preserved")
    for name in ("root","forearm","wrist","palm","index_proximal",
                 "middle_proximal","ring_proximal","little_proximal","thumb_proximal"):
        if name not in rig.data.bones:
            raise RuntimeError("Missing original animated bone "+name)
    if not bpy.data.actions:
        raise RuntimeError("Original GLB finger animation is missing")
    for image in bpy.data.images:
        if image.source=="FILE" and not image.packed_file:
            try:image.pack()
            except Exception as e:print("[ROKAS] Texture warning: "+str(e),flush=True)
    os.makedirs(os.path.dirname(target),exist_ok=True)
    os.makedirs(os.path.dirname(blend),exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend,compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    skin.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=target,use_selection=True,
        object_types={"ARMATURE","MESH"},apply_unit_scale=True,
        axis_forward="-Z",axis_up="Y",add_leaf_bones=False,
        bake_anim=True,bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=True,bake_anim_use_all_actions=True,
        bake_anim_step=1.0,bake_anim_simplify_factor=0.0,
        path_mode="AUTO",use_mesh_modifiers=False)
    if not os.path.isfile(target) or os.path.getsize(target)<1500000:
        raise RuntimeError("Unity FBX was not exported")
    print("[ROKAS NEW USER HAND FBX PASS] originalGLBMeshes=%d skinnedParts=%d verts=%d bones=%d animations=%d" %
          (len(meshes),len(skinned),len(skin.data.vertices),len(rig.data.bones),len(bpy.data.actions)),flush=True)


if __name__=="__main__":
    try:main()
    except Exception as exc:
        print("[ROKAS USER GLB CONVERSION FAILED] "+repr(exc),flush=True)
        traceback.print_exc()
        sys.exit(19)
