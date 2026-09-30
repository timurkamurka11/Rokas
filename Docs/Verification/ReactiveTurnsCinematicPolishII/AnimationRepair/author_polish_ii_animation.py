"""Polish II: use the original rigs and author readable defence plus full loops."""
import bpy, json, math, hashlib, re
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

previous=Path('D:/Rokas/reactiveturns-b2-staging/cinematic/repair_sword_animations.py')
exec(previous.read_text(encoding='utf-8').split('idle_source=FOLDER/')[0])
EVIDENCE=Path('D:/Rokas/reactiveturns-b2-staging/polish-ii-animation')
EVIDENCE.mkdir(exist_ok=True)
REPORT={'blender':bpy.app.version_string,'sources':{},'takes':{}}
idle_source=FOLDER/'Keiko@Idle.fbx'
DODGE_RIG_METRICS=None

def smooth(t):
 t=max(0,min(1,t));return t*t*(3-2*t)

def body_guard(arm,t,n):
 hip=arm.pose.bones['mixamorig:Hips']
 recoil=math.sin(max(0,min(1,(t-.34)/.18))*math.pi)*.025 if .34<t<.52 else 0
 m=hip.matrix.copy();m.translation+=vec((0,-recoil*.35,-recoil))
 hip.matrix=m;update()

def body_dodge(arm,t,n):
 global DODGE_RIG_METRICS
 if DODGE_RIG_METRICS is None:
  # Derive this current actor's stage stature/yaw and displacement contract;
  # do not carry the previous 2 m rig's hardcoded counter-step scale forward.
  arena=(ROOT/'Assets/Rokas/Scripts/Presentation/ReactiveCombatArena.cs').read_text()
  visual=(ROOT/'Assets/Rokas/Scripts/Presentation/ReactiveCombatActorVisual.cs').read_text()
  library=(ROOT/'Assets/Rokas/Resources/Combat/ReactiveCombatActorLibrary.asset').read_text().split('  mina:')[0]
  height=float(re.search(r'hunter\.SetStandingHeight\(([0-9.]+)f\)',arena).group(1))
  distance=float(re.search(r'return ([0-9.]+)f \* Mathf\.SmoothStep',visual).group(1))
  yaw=float(re.search(r'forwardYaw: ([0-9.]+)',library).group(1))
  deps=bpy.context.evaluated_depsgraph_get();vertices=[]
  for obj in bpy.context.scene.objects:
   if obj.type!='MESH':continue
   evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh()
   vertices.extend(evaluated.matrix_world@v.co for v in mesh.vertices)
   evaluated.to_mesh_clear()
  sourceheight=max(v.z for v in vertices)-min(v.z for v in vertices)
  manifest=Path('C:/Users/tim/AppData/Local/Temp/rokas-polish-ii-combat-visuals/capture-manifest.json')
  snapshot=EVIDENCE/'runtime_rig_scale.json'
  if manifest.exists():
   capture=json.loads(manifest.read_text())['captures'][0]
   record={'hunterWorldScale':capture['hunterWorldScale'],'runtimeScaleManifestSha256':hashlib.sha256(manifest.read_bytes()).hexdigest(),'runtimeScaleCapture':capture['image'],'requestedStageHeight':height,'forwardYaw':yaw}
   snapshot.write_text(json.dumps(record,indent=2))
  else:
   record=json.loads(snapshot.read_text())
   assert record['requestedStageHeight']==height and record['forwardYaw']==yaw,'Capture fresh Unity rig scale after changing the current actor target.'
  scale=record['hunterWorldScale']
  assert max(scale.values())-min(scale.values())<.00001,'Current Dodge bake expects the existing uniform sword rig.'
  DODGE_RIG_METRICS={'requestedStageHeight':height,'sourceStandingHeight':sourceheight,'scaleRatio':scale['z'],'forwardYaw':yaw,'backstepWorldDistance':distance,'runtimeScaleSource':'Unity PlayMode capture manifest (snapshot preserved)','runtimeScaleManifestSha256':record['runtimeScaleManifestSha256'],'runtimeScaleCapture':record['runtimeScaleCapture']}
  REPORT['dodgeRecoveryRig']=DODGE_RIG_METRICS
 # Root translation remains owned by presentation. Key real knees, pelvis and
 # feet into a crouch/push, a short hop, planted landing and return stance.
 originalfeet={side:(arm.pose.bones['mixamorig:'+side+'Foot'].head.copy(),arm.pose.bones['mixamorig:'+side+'Foot'].matrix.to_quaternion().copy()) for side in ['Right','Left']}
 hop=math.sin(max(0,min(1,(t-.08)/.25))*math.pi)*.045 if .08<t<.33 else 0
 crouch=(1-smooth(t/.08))*.025 if t<.08 else (math.sin(max(0,min(1,(t-.30)/.16))*math.pi)*.018 if t<.46 else 0)
 if .38<=t<=.8:crouch=max(crouch,math.sin(smooth((t-.38)/.42)*math.pi)*.018)
 hip=arm.pose.bones['mixamorig:Hips'];m=hip.matrix.copy()
 m.translation+=vec((0,hop-crouch,0));hip.matrix=m;update()
 for side,phase in [('Right',0),('Left',.045)]:
  leg=arm.pose.bones['mixamorig:'+side+'UpLeg']
  lower=arm.pose.bones['mixamorig:'+side+'Leg']
  feet=arm.pose.bones['mixamorig:'+side+'Foot']
  lift=math.sin(max(0,min(1,(t-.05-phase)/.30))*math.pi)
  for pb,angle in [(leg,-lift*18),(lower,lift*30),(feet,-lift*12)]:
   pb.rotation_mode='QUATERNION';pb.rotation_quaternion=pb.rotation_quaternion@Quaternion(vec((1,0,0)),math.radians(angle))
 update()
 if t>=.38:
  # The short return contains two planted steps. Counter the arena-model
  # displacement in each stance leg, rather than sliding both static feet.
  metrics=DODGE_RIG_METRICS;yaw=math.radians(metrics['forwardYaw'])
  # Inverse yaw maps the arena's lateral backstep into BOTH rig X and Z.
  # A Z-only counter-step also pushed planted feet sideways in world depth.
  back=vec((-math.cos(yaw),0,-math.sin(yaw)))*(metrics['backstepWorldDistance']/metrics['scaleRatio'])
  shift=back*(1-smooth((t-.38)/.42))
  for side,start,end in [('Left',.40,.60),('Right',.58,.80)]:
   upper=arm.pose.bones['mixamorig:'+side+'UpLeg'];lower=arm.pose.bones['mixamorig:'+side+'Leg'];foot=arm.pose.bones['mixamorig:'+side+'Foot']
   base,foot_q=originalfeet[side]
   phase=max(0,min(1,(t-start)/(end-start)))
   groundshift=back*(1-smooth(phase))
   target=base+groundshift-shift+vec((0,math.sin(phase*math.pi)*.045,0))
   a=upper.head.copy();l1=(lower.head-a).length;l2=(foot.head-lower.head).length
   delta=target-a;distance=max(abs(l1-l2)+.0002,min(delta.length,l1+l2-.0002));axis=delta.normalized()
   pole=vec((0,-.15,.6));pole=(pole-axis*pole.dot(axis)).normalized()
   along=(l1*l1-l2*l2+distance*distance)/(2*distance);height=math.sqrt(max(0,l1*l1-along*along))
   knee=a+axis*along+pole*height;plane=axis.cross(pole).normalized()
   align_axis(upper,knee-a,plane);align_axis(lower,a+axis*distance-lower.head,plane)
   foot.matrix=Matrix.LocRotScale(foot.matrix.translation,foot_q,vec((1,1,1)));update()

# Hanging guard keeps the two-handed hilt high while the blade protects the
# lower body where the current smaller Yokai's descending claws actually hit.
# Raising the blade above the head protected empty space in the Unity capture.
GUARD=[(0,READY,READY_BLADE),(.18,(-.020,.748,.090),(-.25,-.95,.18)),(.34,(-.020,.748,.090),(-.25,-.95,.18)),(.43,(-.028,.737,.078),(-.28,-.94,.20)),(.56,(-.020,.748,.090),(-.25,-.95,.18)),(.8,READY,READY_BLADE)]
DODGE=[(0,READY,READY_BLADE),(.08,(-.030,.679,.093),(0,.84,.54)),(.24,(-.030,.742,.063),(-.1,.91,.40)),(.38,(-.030,.725,.068),(-.1,.91,.40)),(.58,(-.027,.711,.094),(0,.80,.60)),(.82,READY,READY_BLADE)]
bake(idle_source,'Two handed sword block',25,(1,1),lambda t,n:lerp_keys(GUARD,t),contact=.34,body=body_guard)
# 25-frame input is .8 sec after dense bake; use .8 sec end, contact at .24.
DODGE[-1]=(.8,READY,READY_BLADE)
bake(idle_source,'Two handed dodge backstep',25,(1,1),lambda t,n:lerp_keys(DODGE,t),contact=.24,body=body_dodge)

# Distinct, easily read final silhouettes: compact shoulder guard for Normal,
# raised hilt and blade behind the head for Heavy, both with shared shaft grip.
bake(idle_source,'Normal preparation',10,(1,1),lambda t,n:lerp_keys([(0,READY,READY_BLADE),(.30,(-.045,.774,.080),(-.23,.85,.47))],t))
bake(idle_source,'Heavy preparation',15,(1,1),lambda t,n:lerp_keys([(0,READY,READY_BLADE),(.467,(-.018,.845,.023),(-.08,.95,-.30))],t))

# The source contains a full 27-interval approach cycle. The previous 24-
# interval extraction truncated it, causing visible foot resets every loop.
bake(FOLDER/'Run to hit enemies.fbx','Two handed approach',28,(1,28),lambda t,n:(vec(READY),vec(READY_BLADE)))
bake(FOLDER/'Run Back after hit.fbx','Two handed return',24,(1,24),lambda t,n:(vec(READY),vec(READY_BLADE)))

# EnterBattle is a WALK CYCLE. It must run while travelling, never be replayed
# as a stationary post-entry take. A new settle take plants the feet instead.
bake(FOLDER/'Enter the batle.fbx','Enter battle corrected',19,(1,19),lambda t,n:(vec((-.020,.683,.091)),vec((0,.65,.76))))
bake(idle_source,'Enter battle settle',15,(1,1),lambda t,n:lerp_keys([(0,(-.020,.683,.091),(0,.65,.76)),(.467,READY,READY_BLADE)],t))

def bake_yokai(source,name,duration,source_keys,contact=None):
 global FOLDER,OUTPUT
 clear();REPORT['sources'][str(source)]={'sha256':hashlib.sha256(source.read_bytes()).hexdigest()}
 bpy.ops.import_scene.fbx(filepath=str(source),automatic_bone_orientation=False)
 arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 assert len(arm.data.bones)==65
 original=arm.animation_data.action
 count=round(duration*FPS)+1
 samples=[];claws=[]
 bpy.context.scene.render.fps=FPS;bpy.context.scene.frame_start=0;bpy.context.scene.frame_end=count-1
 for f in range(count):
  t=f/FPS
  source_frame=source_keys[-1][1]
  for a,b in zip(source_keys,source_keys[1:]):
   if t<=b[0]:
    w=max(0,min(1,(t-a[0])/(b[0]-a[0])))
    source_frame=a[1]+(b[1]-a[1])*w;break
  bpy.context.scene.frame_set(int(source_frame),subframe=source_frame-int(source_frame))
  samples.append({p.name:p.matrix_basis.copy() for p in arm.pose.bones})
  claws.append({side:list(arm.pose.bones['mixamorig:'+side+'Hand'].head) for side in ['Right','Left']})
 action=bpy.data.actions.new(name);arm.animation_data.action=action
 previous_q={}
 for f,values in enumerate(samples):
  for p in arm.pose.bones:
   loc,q,scale=values[p.name].decompose()
   if p.name in previous_q and q.dot(previous_q[p.name])<0:q.negate()
   previous_q[p.name]=q.copy();p.rotation_mode='QUATERNION';p.location=loc;p.rotation_quaternion=q;p.scale=scale
   for property in ['location','rotation_quaternion','scale']:p.keyframe_insert(property,frame=f,group=p.name)
 for layer in action.layers:
  for strip in layer.strips:
   for slot in action.slots:
    bag=strip.channelbag(slot)
    if bag:
     for curve in bag.fcurves:
      for key in curve.keyframe_points:key.interpolation='LINEAR'
 bpy.context.scene.frame_set(0);bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm
 for ob in bpy.context.scene.objects:
  if ob.type=='MESH':ob.select_set(True)
 output=ROOT/'Assets/Rokas/Art/CombatActors/Yokai'/(name+'.fbx')
 bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_step=1,bake_anim_simplify_factor=0,primary_bone_axis='Y',secondary_bone_axis='X',axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_armature_deform_only=False,use_mesh_modifiers=False)
 bpy.ops.wm.save_as_mainfile(filepath=str(EVIDENCE/(name+'.blend')))
 REPORT['takes'][name]={'frames':count,'fps':FPS,'duration':duration,'contactSeconds':contact,'sourceFrameKeys':source_keys,'claws':claws,'bones':list(arm.data.bones.keys()),'sha256':hashlib.sha256(output.read_bytes()).hexdigest()}
 print('BAKED YOKAI',name,duration,contact)

Y=ROOT/'Assets/Rokas/Art/CombatActors/Yokai'
# Retain the existing claw attack, retime only its authored phases. Contact
# frame 35 is the forward descending claw at chest height. Frame 27 has the
# claw behind the shoulder and must never dispatch body/guard contact.
bake_yokai(Y/'Attack.fbx','Claw attack corrected',1.2,[(0,1),(.35,31),(.55,35),(.72,39),(1.2,68)],contact=.55)
bake_yokai(Y/'Jump attack.fbx','Claw heavy corrected',1.5,[(0,1),(.54,25),(.80,31),(.99,39),(1.5,75)],contact=.80)
bake_yokai(Y/'Walking to attack.fbx','Claw locomotion corrected',14/30,[(0,1),(14/30,15)])
REPORT['locomotion']={'KeikoApproachSourceStride':1.63,'KeikoReturnSourceStride':.90,'KeikoEntrySourceStride':1.46354,'YokaiSourceStride':.68552,'rootHorizontalTranslation':'removed only from extracted copies; arena owns horizontal roots','entrySettleSeconds':14/30}
(EVIDENCE/'animation_bake_report.json').write_text(json.dumps(REPORT,indent=2))
