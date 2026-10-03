import bpy, math, json
from pathlib import Path
from mathutils import Vector
ROOT=Path('D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY')
BASE=ROOT/'Docs/Verification/ReactiveTurnsFinalVfx/AnimationRepair'
SOURCE=ROOT/'Assets/Rokas/Art/CombatActors/Keiko'
WEAPON=ROOT/'Assets/Rokas/Art/CombatActors/Weapons/KeikoThrowingDagger'
TEX=ROOT/'Assets/Rokas/Art/CombatActors/Textures/Keiko/anime_character_3d_model_basecolor.JPEG'
REPORT=json.loads((BASE/'throw_authoring_report.json').read_text())
def clear():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def material(name,color,texture=None):
    m=bpy.data.materials.new(name);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=color;bs.inputs['Roughness'].default_value=.5
    if texture:
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(texture))
        m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    return m
checks={}
clear();bpy.ops.import_scene.fbx(filepath=str(SOURCE/'Two handed battle idle.fbx'),automatic_bone_orientation=False)
source_arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
source_ready={p.name:p.matrix_basis.copy() for p in source_arm.pose.bones}
prep_end=None
for name in ['Throw preparation','Throw attack corrected']:
    clear();bpy.ops.import_scene.fbx(filepath=str(SOURCE/(name+'.fbx')),automatic_bone_orientation=False)
    arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');take=REPORT['takes'][name]
    binderror=max(abs(float(arm.data.bones[b].matrix_local[r][c])-take['bind'][b][r][c]) for b in take['bind'] for r in range(4) for c in range(4))
    release=take['releaseFrame'];frame=(release if release is not None else take['lastFrame'])+1
    bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
    hand=arm.pose.bones['mixamorig:RightHand'];direction=hand.matrix.to_quaternion()@Vector((0,0,1))
    checks[name]={'bones':len(arm.data.bones),'maxBindRestMatrixError':binderror,
        'actionFrameRange':list(arm.animation_data.action.frame_range),
        'duration':(arm.animation_data.action.frame_range[1]-arm.animation_data.action.frame_range[0])/120,
        'checkedFrame':frame,'checkedHand':list(hand.head),'checkedBladeDirection':list(direction),
        'alignmentToStageEnemyAtRelease':direction.dot(Vector(REPORT['releaseDirectionInRigCoordinates']))}
    assert len(arm.data.bones)==33 and binderror < .0001
    if release is not None:assert checks[name]['alignmentToStageEnemyAtRelease']>.999
    bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
    comparison=source_ready if release is None else prep_end
    pose_error=max(abs(float(arm.pose.bones[b].matrix_basis[r][c])-float(comparison[b][r][c])) for b in comparison for r in range(4) for c in range(4))
    checks[name]['startPoseMatrixError']=pose_error
    assert pose_error<.0001
    bpy.context.scene.frame_set(take['lastFrame']+1);bpy.context.view_layer.update()
    if release is None:prep_end={p.name:p.matrix_basis.copy() for p in arm.pose.bones}
    else:
        recovery_error=max(abs(float(arm.pose.bones[b].matrix_basis[r][c])-float(source_ready[b][r][c])) for b in source_ready for r in range(4) for c in range(4))
        checks[name]['endRecoveryPoseMatrixError']=recovery_error;assert recovery_error<.0001
        before=Vector(take['diagnostics'][release-1]['grip']);after=Vector(take['diagnostics'][release+1]['grip'])
        velocity=(after-before)*60
        checks[name]['releaseHandSpeed']=velocity.length
        checks[name]['releaseForwardSpeed']=velocity.dot(Vector(REPORT['releaseDirectionInRigCoordinates']))
        assert checks[name]['releaseForwardSpeed']>1
clear();bpy.ops.import_scene.fbx(filepath=str(WEAPON/'KeikoThrowingDagger.fbx'))
ob=next(o for o in bpy.context.scene.objects if o.type=='MESH')
points=[ob.matrix_world@v.co for v in ob.data.vertices]
checks['dagger']={'objectMatrix':[list(r) for r in ob.matrix_world],
    'vertexRanges':[[min(v[i] for v in points),max(v[i] for v in points)] for i in range(3)],
    'materials':[m.name for m in ob.data.materials],'triangles':len(ob.data.polygons)}
(BASE/'throw_roundtrip_report.json').write_text(json.dumps(checks,indent=2))
print('ROUNDTRIP',json.dumps(checks,indent=2))

def lighting(scene,focus,scale=1):
    scene.world.color=(.035,.043,.058)
    for pos,power,size,color in [((1,-2,3),350,3,(.72,.82,1)),((-2,-.4,2),220,2,(1,.62,.32)),((0,2,2.5),550,2,(.25,.62,1))]:
        bpy.ops.object.light_add(type='AREA',location=tuple(Vector(pos)*scale));o=bpy.context.object
        o.data.energy=power*scale*scale;o.data.color=color;o.data.shape='DISK';o.data.size=size*scale
        o.rotation_euler=(focus-o.location).to_track_quat('-Z','Y').to_euler()
def camera(scene,pos,target,ortho):
    bpy.ops.object.camera_add(location=pos);cam=bpy.context.object
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=ortho;scene.camera=cam
def settings(scene,file):
    scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.filepath=str(BASE/file)
    scene.view_settings.view_transform='AgX';scene.render.film_transparent=False
def render(name,frame,view):
    bpy.ops.wm.open_mainfile(filepath=str(BASE/(name+'.blend')));scene=bpy.context.scene;scene.frame_set(frame)
    for ob in scene.objects:
        if ob.type=='MESH' and ob.name != 'KeikoThrowingDagger':
            ob.data.materials.clear();ob.data.materials.append(material('Keiko basecolor',(1,1,1,1),TEX))
    bpy.ops.mesh.primitive_plane_add(size=100,location=(0,0,-.008))
    bpy.context.object.data.materials.append(material('Stage',(0.025,.030,.045,1)))
    lighting(scene,Vector((0,-.05,.6)))
    pos=(1.7,-2.5,1.15) if view=='threequarter' else (2.8,-.25,.95)
    camera(scene,pos,Vector((0,-.03,.52)),1.32)
    settings(scene,name.replace(' ','_')+'_'+str(frame)+'_'+view+'.png');bpy.ops.render.render(write_still=True)
for name,frames in [('Throw preparation',[0,48]),('Throw attack corrected',[0,19,40,51,68,100,132])]:
    for frame in frames:render(name,frame,'threequarter')
render('Throw attack corrected',51,'side')
bpy.ops.wm.open_mainfile(filepath=str(BASE/'KeikoThrowingDagger.blend'));scene=bpy.context.scene
lighting(scene,Vector((0,0,.062)),.22)
camera(scene,(.24,-.4,.23),Vector((0,0,.062)),.245)
settings(scene,'KeikoThrowingDagger_detail.png');bpy.ops.render.render(write_still=True)
print('DONE THROW RENDER REVIEW')
