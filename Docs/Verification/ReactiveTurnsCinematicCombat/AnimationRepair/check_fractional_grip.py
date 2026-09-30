import bpy,os
from pathlib import Path
from mathutils import Vector
base=Path(r'D:/Rokas/reactiveturns-b2-staging/cinematic')
folder=Path(r'D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY/Assets/Rokas/Art/CombatActors/Keiko')
if os.environ.get('ROKAS_ANIMATION_PREVIEW'):folder=base/'preview_sources'
for name in ['Normal attack corrected','Hard jump attack corrected','Two handed battle idle','Enter battle corrected']:
    for mode in ['blend','fbx']:
        if mode=='blend':
            bpy.ops.wm.open_mainfile(filepath=str(base/(name+'.blend')))
            start=0
        else:
            bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
            bpy.ops.import_scene.fbx(filepath=str(folder/(name+'.fbx')),automatic_bone_orientation=False)
            start=1
        arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
        end=arm.animation_data.action.frame_range[1]
        for i in range(9):
            f=start+(end-start)*i/8
            bpy.context.scene.frame_set(int(f),subframe=f-int(f))
            r=arm.pose.bones['mixamorig:RightHand'];l=arm.pose.bones['mixamorig:LeftHand']
            target=r.matrix@Vector((0,.033,-.055));actual=l.matrix@Vector((0,.033,0))
            print('FRACTION',name,mode,i,f,(actual-target).length)
