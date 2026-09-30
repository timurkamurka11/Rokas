import bpy,json
from pathlib import Path
from mathutils import Vector
R=Path('D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY/Assets/Rokas/Art/CombatActors')
rows={}
for group,name,height in [('Keiko','Keiko@Idle.fbx',2),('Yokai','Still stance.fbx',2.7)]:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 bpy.ops.import_scene.fbx(filepath=str(R/group/name),automatic_bone_orientation=False)
 bpy.context.scene.frame_set(1)
 deps=bpy.context.evaluated_depsgraph_get()
 verts=[o.matrix_world@v.co for source in bpy.context.scene.objects if source.type=='MESH' for o in [source.evaluated_get(deps)] for v in o.to_mesh().vertices]
 sourceheight=max(v.z for v in verts)-min(v.z for v in verts)
 scale=height/sourceheight
 rows[group]={'sourceStandingHeight':sourceheight,'requestedRuntimeHeight':height,'scaleRatio':scale}
 print(group,rows[group])
inspection=json.loads(Path('D:/Rokas/reactiveturns-b2-staging/cinematic/polish_ii_source_inspection.json').read_text())
for relative in ['Keiko/Run to hit enemies.fbx','Keiko/Run Back after hit.fbx','Keiko/Enter the batle.fbx','Yokai/Walking to attack.fbx']:
 group=relative.split('/')[0]
 samples=inspection[relative]['samples'];start=samples[0]['bones']['mixamorig:Hips'];end=samples[-1]['bones']['mixamorig:Hips']
 stride=abs(end[2]-start[2])*rows[group]['scaleRatio']
 print('RUNTIME STRIDE',relative,stride)
 rows[relative]={'runtimeStride':stride,'sourceStride':abs(end[2]-start[2])}
Path('D:/Rokas/reactiveturns-b2-staging/polish-ii-animation/stride_report.json').write_text(json.dumps(rows,indent=2))
