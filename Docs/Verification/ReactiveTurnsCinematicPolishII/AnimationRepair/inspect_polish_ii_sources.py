import bpy,json,math
from pathlib import Path
from mathutils import Vector
R=Path('D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY/Assets/Rokas/Art/CombatActors')
rows={}
for relative in ['Keiko/Run to hit enemies.fbx','Keiko/Run Back after hit.fbx','Keiko/Enter the batle.fbx','Yokai/Walking to attack.fbx','Yokai/Attack.fbx','Yokai/Jump attack.fbx']:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 bpy.ops.import_scene.fbx(filepath=str(R/relative),automatic_bone_orientation=False)
 arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 a=arm.animation_data.action
 start,end=a.frame_range
 samples=[]
 names=['mixamorig:Hips','mixamorig:RightFoot','mixamorig:LeftFoot','mixamorig:RightHand','mixamorig:LeftHand']
 for f in range(int(start),int(end)+1):
  bpy.context.scene.frame_set(f)
  samples.append({'frame':f,'bones':{n:[round(v,5) for v in arm.pose.bones[n].head] for n in names if n in arm.pose.bones}})
 rows[relative]={'bones':list(arm.data.bones.keys()),'frameRange':[start,end],'fps':bpy.context.scene.render.fps,'samples':samples}
 print(relative,'range',start,end,'fps',bpy.context.scene.render.fps,'bones',len(arm.data.bones))
 print('SAMPLES',samples[::max(1,len(samples)//6)])
Path('D:/Rokas/reactiveturns-b2-staging/cinematic/polish_ii_source_inspection.json').write_text(json.dumps(rows,indent=2))
