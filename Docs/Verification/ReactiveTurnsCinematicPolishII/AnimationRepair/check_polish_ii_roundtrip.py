import bpy,json
from pathlib import Path
from mathutils import Vector
P=Path('D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY/Assets/Rokas/Art/CombatActors')
def load(path):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 bpy.ops.import_scene.fbx(filepath=str(path),automatic_bone_orientation=False)
 return next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
reports={}
for group,original,names in [('Keiko','Keiko@Idle.fbx',['Two handed sword block','Two handed dodge backstep','Normal preparation','Heavy preparation','Two handed approach','Two handed return','Enter battle corrected','Enter battle settle']),('Yokai','Still stance.fbx',['Claw attack corrected','Claw heavy corrected','Claw locomotion corrected'])]:
 original=load(P/group/original);rest={b.name:b.matrix_local.copy() for b in original.data.bones}
 for name in names:
  arm=load(P/group/(name+'.fbx'))
  assert set(rest)==set(arm.data.bones.keys()),name
  errors={b.name:max(abs(b.matrix_local[i][j]-rest[b.name][i][j]) for i in range(4) for j in range(4)) for b in arm.data.bones}
  grip=[]
  if group=='Keiko':
   for f in range(int(arm.animation_data.action.frame_range[0]),int(arm.animation_data.action.frame_range[1])+1):
    bpy.context.scene.frame_set(f)
    right=arm.pose.bones['mixamorig:RightHand'];left=arm.pose.bones['mixamorig:LeftHand']
    target=right.matrix@Vector((0,.033,-.055));actual=left.matrix@Vector((0,.033,0));grip.append((target-actual).length)
  reports[name]={'maxBindRestMatrixError':max(errors.values()),'boneCount':len(rest),'maxExportedGripError':max(grip) if grip else None,'actionFrameRange':list(arm.animation_data.action.frame_range),'armatureName':arm.name}
  print(name,reports[name])
  assert max(errors.values())<.001,name
  assert not grip or max(grip)<.001,name
Path('D:/Rokas/reactiveturns-b2-staging/polish-ii-animation/roundtrip_report.json').write_text(json.dumps(reports,indent=2))
