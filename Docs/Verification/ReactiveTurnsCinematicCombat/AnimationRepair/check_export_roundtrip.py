import bpy, json
from pathlib import Path
from mathutils import Vector
folder=Path(r'D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY/Assets/Rokas/Art/CombatActors/Keiko')
def load(file):
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(folder/file),automatic_bone_orientation=False)
    return next(x for x in bpy.context.scene.objects if x.type=='ARMATURE')
original=load('Normal attack.fbx')
rest={b.name:b.matrix_local.copy() for b in original.data.bones}
for file in ['Normal attack corrected.fbx','Hard jump attack corrected.fbx','Two handed battle idle.fbx','Enter battle corrected.fbx']:
    arm=load(file)
    errors={b.name:max(abs(b.matrix_local[i][j]-rest[b.name][i][j]) for i in range(4) for j in range(4)) for b in arm.data.bones}
    print('ROUNDTRIP',file,'BONES',len(arm.data.bones),'MAX REST ERROR',max(errors.values()),'ACTION RANGE',list(arm.animation_data.action.frame_range))
    print('HAND REST',[[round(v,6) for v in row] for row in arm.data.bones['mixamorig:RightHand'].matrix_local])
    for f in [1,19,34]:
        bpy.context.scene.frame_set(f)
        r=arm.pose.bones['mixamorig:RightHand']; l=arm.pose.bones['mixamorig:LeftHand']
        rg=r.matrix @ Vector((0,.033,0)); target=rg-r.matrix.to_3x3()@Vector((0,0,.055)); actual=l.matrix@Vector((0,.033,0))
        print('FRAME',f,'HAND ERROR',(actual-target).length,'RIGHT HEAD',list(r.head))
