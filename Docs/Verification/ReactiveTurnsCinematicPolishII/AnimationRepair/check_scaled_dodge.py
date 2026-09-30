import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
base=Path('D:/Rokas/reactiveturns-b2-staging/polish-ii-animation')
root=Path('D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY')
def load(path):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 bpy.ops.import_scene.fbx(filepath=str(path),automatic_bone_orientation=False)
 return next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
original=load(root/'Assets/Rokas/Art/CombatActors/Keiko/Keiko@Idle.fbx')
rest={b.name:b.matrix_local.copy() for b in original.data.bones}
arm=load(base/'scaled-dodge-staging/Two handed dodge backstep.fbx')
assert set(rest)==set(arm.data.bones.keys())
binderror=max(abs(b.matrix_local[i][j]-rest[b.name][i][j]) for b in arm.data.bones for i in range(4) for j in range(4))
grip=[]
for f in range(1,98):
 bpy.context.scene.frame_set(f)
 right=arm.pose.bones['mixamorig:RightHand'];left=arm.pose.bones['mixamorig:LeftHand']
 grip.append(((right.matrix@Vector((0,.033,-.055)))-(left.matrix@Vector((0,.033,0)))).length)
report={'boneCount':len(rest),'maxBindRestMatrixError':binderror,'maxExportedGripError':max(grip)}
assert binderror<.000013 and max(grip)<.0000015
bpy.ops.wm.open_mainfile(filepath=str(base/'Two handed dodge backstep.blend'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
metrics=json.loads((base/'animation_bake_report.json').read_text())['dodgeRecoveryRig']
rotation=Matrix.Rotation(math.radians(metrics['forwardYaw']),3,'Y');scale=metrics['scaleRatio']
def smooth(t):
 t=max(0,min(1,t));return t*t*(3-2*t)
def foot(side,time):
 bpy.context.scene.frame_set(round(time*120))
 shift=-metrics['backstepWorldDistance']*(1-smooth((time-.38)/.42))
 return rotation@(arm.pose.bones['mixamorig:'+side+'Foot'].head*scale)+Vector((shift,0,0))
errors={}
for side,start,end in [('Right',.38,.575),('Left',.60,.80)]:
 baseline=foot(side,start)
 errors[side]=max((foot(side,t/120)-baseline).length for t in range(round(start*120),round(end*120)+1))
report['plantedFootWorldDrift']=errors
report['rigMetrics']=metrics
(base/'scaled_dodge_validation.json').write_text(json.dumps(report,indent=2))
print('SCALED DODGE',json.dumps(report))
assert max(errors.values())<.025
