from pathlib import Path
source=Path('D:/Rokas/reactiveturns-b2-staging/cinematic/render_authored_poses.py').read_text(encoding='utf-8')
exec(source.split('def render(name,frame,view):')[0])
BASE=Path('D:/Rokas/reactiveturns-b2-staging/polish-ii-animation')
TEX=PROJECT/'Assets/Rokas/Art/CombatActors/Textures/Yokai/fantasy+creature+3d+model_basecolor.jpg'
for name,frames in [('Claw attack corrected',[42,66,86]),('Claw heavy corrected',[65,96,119])]:
 for frame in frames:
  bpy.ops.wm.open_mainfile(filepath=str(BASE/(name+'.blend')))
  scene=bpy.context.scene;scene.frame_set(frame)
  arm=next(o for o in scene.objects if o.type=='ARMATURE')
  hip=arm.pose.bones['mixamorig:Hips'];m=hip.matrix.copy();m.translation.x=0;m.translation.z=0;hip.matrix=m;bpy.context.view_layer.update()
  for ob in [o for o in scene.objects if o.type=='MESH']:
   ob.data.materials.clear();ob.data.materials.append(material('Yokai basecolor',TEX))
  bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,0));bpy.context.object.data.materials.append(material('Floor',color=(.045,.054,.068,1)))
  scene.world.color=(.12,.12,.12)
  for pos,power,size in [((1,-2,3),250,3),((-2,-.4,2),160,2),((0,2,2.5),300,2)]:
   bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.5))-o.location).to_track_quat('-Z','Y').to_euler()
  bpy.ops.object.camera_add(location=(2,-3,1.4));cam=bpy.context.object;cam.rotation_euler=(Vector((0,-.15,.64))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.7;scene.camera=cam
  scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
  scene.render.image_settings.file_format='PNG';scene.render.filepath=str(BASE/(name.replace(' ','_')+'_'+str(frame)+'_threequarter.png'))
  scene.view_settings.view_transform='AgX';scene.render.film_transparent=False
  bpy.ops.render.render(write_still=True)
