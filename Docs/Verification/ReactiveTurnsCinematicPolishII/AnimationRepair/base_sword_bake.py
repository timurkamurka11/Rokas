"""Bake the existing 33-bone Keiko sources into two-handed sword takes.

The original FBX inputs are read-only. Only animation source FBX outputs are
written into the authoritative project's CombatActors/Keiko folder.
"""
import bpy, json, math, hashlib, shutil, os
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

ROOT=Path(r'D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY')
FOLDER=ROOT/'Assets/Rokas/Art/CombatActors/Keiko'
EVIDENCE=Path(r'D:/Rokas/reactiveturns-b2-staging/cinematic')
EVIDENCE.mkdir(parents=True, exist_ok=True)
REPORT={"blender":bpy.app.version_string,"sources":{},"takes":{}}
FPS=120
OUTPUT=EVIDENCE/'preview_sources' if os.environ.get('ROKAS_ANIMATION_PREVIEW') else FOLDER
OUTPUT.mkdir(parents=True,exist_ok=True)

def clear():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)

def load(path):
    clear()
    REPORT['sources'][str(path)]={"sha256":hashlib.sha256(path.read_bytes()).hexdigest()}
    bpy.ops.import_scene.fbx(filepath=str(path), automatic_bone_orientation=False)
    arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    assert len(arm.data.bones)==33
    return arm

def vec(values): return Vector(values)

def lerp_keys(keys, t):
    if t<=keys[0][0]: return vec(keys[0][1]),vec(keys[0][2])
    for a,b in zip(keys,keys[1:]):
        if t<=b[0]:
            w=(t-a[0])/(b[0]-a[0]); w=w*w*(3-2*w)
            return vec(a[1]).lerp(vec(b[1]),w),vec(a[2]).lerp(vec(b[2]),w).normalized()
    return vec(keys[-1][1]),vec(keys[-1][2])

READY=(-.025,.708,.115)
READY_BLADE=(0,.76,.65)
NORMAL=[
    (0,READY,READY_BLADE),
    (.16,(-.038,.750,.087),(-.18,.83,.53)),
    (.36,(-.028,.821,.036),(-.23,.91,-.34)),
    (.44,(-.022,.840,.026),(-.15,.91,-.39)),
    (.60,(-.025,.708,.150),(0,.08,1)),
    (.68,(-.015,.665,.150),(.24,-.35,.905)),
    (.86,(-.014,.660,.105),(.31,-.54,.78)),
    (1.10,READY,READY_BLADE),
]
HEAVY=[
    (0,READY,READY_BLADE),
    (.23,(-.021,.763,.080),(0,.90,.43)),
    (.50,(-.010,.836,.022),(-.12,.975,-.18)),
    (.68,(-.010,.864,.006),(-.08,.91,-.40)),
    (.86,(-.018,.692,.159),(.08,-.15,.985)),
    (.95,(-.018,.635,.125),(.18,-.62,.765)),
    (1.12,(-.012,.644,.116),(.15,-.64,.754)),
    (1.43,READY,READY_BLADE),
]

def update(): bpy.context.view_layer.update()

def align_axis(pb, desired, plane_normal):
    old=pb.matrix.copy()
    y=desired.normalized()
    z=(plane_normal-y*plane_normal.dot(y)).normalized()
    x=y.cross(z).normalized()
    rotation=Matrix((x,y,z)).transposed().to_quaternion()
    pb.matrix=Matrix.LocRotScale(old.translation, rotation, old.to_scale())
    update()

def solve_arm(arm, side, wrist):
    upper=arm.pose.bones['mixamorig:'+side+'Arm']
    fore=arm.pose.bones['mixamorig:'+side+'ForeArm']
    hand=arm.pose.bones['mixamorig:'+side+'Hand']
    start=upper.head.copy()
    l1=(fore.head-start).length; l2=(hand.head-fore.head).length
    d=wrist-start; distance=max(abs(l1-l2)+.0002,min(d.length,l1+l2-.0002))
    axis=d.normalized()
    shoulders=(arm.pose.bones['mixamorig:RightArm'].head+arm.pose.bones['mixamorig:LeftArm'].head)*.5
    elbow_pole=shoulders+vec((-.32 if side=='Right' else .32,-.15,-.05))
    pole=elbow_pole-start
    pole=(pole-axis*pole.dot(axis)).normalized()
    along=(l1*l1-l2*l2+distance*distance)/(2*distance)
    height=math.sqrt(max(0,l1*l1-along*along))
    elbow=start+axis*along+pole*height
    actual_wrist=start+axis*distance
    plane_normal=axis.cross(pole).normalized()
    align_axis(upper,elbow-start,plane_normal)
    align_axis(fore,actual_wrist-fore.head,plane_normal)
    return hand

def hand_rotation(blade, side):
    z=blade.normalized()
    y=vec((1 if side=='Right' else -1,0,0))
    y=(y-z*y.dot(z)).normalized()
    x=y.cross(z).normalized()
    return Matrix((x,y,z)).transposed().to_quaternion(), y

def apply_grip(arm, wrist, blade):
    blade.normalize()
    shoulder_center=(arm.pose.bones['mixamorig:RightArm'].head+arm.pose.bones['mixamorig:LeftArm'].head)*.5
    wrist=wrist+shoulder_center-vec((0,.770963,-.015))
    q,right_fingers=hand_rotation(blade,'Right')
    left_q,left_fingers=hand_rotation(blade,'Left')
    left_offset=right_fingers*.033-blade*.055-left_fingers*.033
    # Project the shared shaft into the overlap of both arm reach volumes.
    # This keeps the jump/torso lean without pulling either palm off the handle.
    for iteration in range(12):
        for side,offset in [('Right',vec((0,0,0))),('Left',left_offset)]:
            upper=arm.pose.bones['mixamorig:'+side+'Arm']; fore=arm.pose.bones['mixamorig:'+side+'ForeArm']; hand=arm.pose.bones['mixamorig:'+side+'Hand']
            l1=(fore.head-upper.head).length; l2=(hand.head-fore.head).length
            center=upper.head-offset
            delta=wrist-center
            distance=max(abs(l1-l2)+.0003,min(delta.length,l1+l2-.0003))
            wrist=center+delta.normalized()*distance
    right=solve_arm(arm,'Right',wrist)
    right.matrix=Matrix.LocRotScale(right.matrix.translation,q,vec((1,1,1)))
    update()
    # Both palms meet the same shaft: right is nearest the guard, left is
    # 5.5 cm behind it. The remaining runtime constraint corrects crossfades.
    grip=right.head+right_fingers*.033
    left_wrist=grip-blade*.055-left_fingers*.033
    left=solve_arm(arm,'Left',left_wrist)
    left.matrix=Matrix.LocRotScale(left.matrix.translation,left_q,vec((1,1,1)))
    update()
    for side in ['Right','Left']:
        for n in range(1,4):
            p=arm.pose.bones['mixamorig:'+side+'HandIndex'+str(n)]
            p.rotation_mode='QUATERNION'
            p.rotation_quaternion=p.rotation_quaternion @ Quaternion(vec((1,0,0)),math.radians(35 if n!=2 else 46))
    update()
    return (left.head+left_fingers*.033-(grip-blade*.055)).length

def stabilize_heavy_body(arm,time,n):
    # Keep the authored lift, knee preparation, landing and pitch/roll. The
    # original source pirouettes repeatedly; a sword strike needs a bounded
    # torso turn that faces the opponent at the downward contact.
    keys=[(0,-35),(.23,-50),(.68,-30),(.86,0),(.95,10),(1.43,-43)]
    yaw=keys[-1][1]
    for a,b in zip(keys,keys[1:]):
        if time<=b[0]:
            w=max(0,min(1,(time-a[0])/(b[0]-a[0])));w=w*w*(3-2*w)
            yaw=a[1]+(b[1]-a[1])*w;break
    hip=arm.pose.bones['mixamorig:Hips'];m=hip.matrix.copy()
    euler=m.to_quaternion().to_euler('YXZ');euler.y=math.radians(yaw)
    hip.matrix=Matrix.LocRotScale(m.translation,euler.to_quaternion(),m.to_scale());update()
    # Neck/head in the source counter-rotate its full spins. Keep their facing
    # direction on the opponent after the torso spin has been removed.
    for name in ['mixamorig:Neck','mixamorig:Head']:
        p=arm.pose.bones[name];m=p.matrix.copy()
        p.matrix=Matrix.LocRotScale(m.translation,FOCUS_ROTATIONS[name],m.to_scale());update()

def bake(source,name,frames,source_range,pose,contact=None,loop=False,body=None):
    frames=(frames-1)*4+1
    arm=load(source)
    original=arm.animation_data.action
    bpy.context.scene.frame_set(int(source_range[0]),subframe=source_range[0]-int(source_range[0]))
    original_basis={p.name:p.matrix_basis.copy() for p in arm.pose.bones}
    bpy.context.scene.render.fps=FPS
    bpy.context.scene.frame_start=0; bpy.context.scene.frame_end=frames-1
    samples=[]; grip_errors=[]; tips=[]
    for f in range(1,frames+1):
        time=(f-1)/FPS
        source_frame=source_range[0]+(source_range[1]-source_range[0])*(f-1)/(frames-1)
        for p in arm.pose.bones:p.matrix_basis=original_basis[p.name]
        bpy.context.scene.frame_set(int(source_frame),subframe=source_frame-int(source_frame))
        if body:body(arm,time,(f-1)/(frames-1))
        wrist,blade=pose(time, (f-1)/(frames-1))
        grip_errors.append(apply_grip(arm,wrist,blade))
        hand=arm.pose.bones['mixamorig:RightHand']
        tips.append(list(hand.head+blade*.63))
        samples.append({p.name:p.matrix_basis.copy() for p in arm.pose.bones})
    action=bpy.data.actions.new('Keiko_'+name)
    arm.animation_data.action=action
    previous_quaternions={}
    for f,values in enumerate(samples):
        for p in arm.pose.bones:
            loc,rotation,scale=values[p.name].decompose()
            if p.name in previous_quaternions and rotation.dot(previous_quaternions[p.name])<0:
                rotation.negate()
            previous_quaternions[p.name]=rotation.copy()
            p.rotation_mode='QUATERNION'
            p.location=loc; p.rotation_quaternion=rotation; p.scale=scale
            p.keyframe_insert('location',frame=f,group=p.name)
            p.keyframe_insert('rotation_quaternion',frame=f,group=p.name)
            p.keyframe_insert('scale',frame=f,group=p.name)
    # Dense samples have exact phase timing; prevent Bezier overshoot at the grip.
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                channelbag=strip.channelbag(slot)
                if channelbag:
                    for curve in channelbag.fcurves:
                        for key in curve.keyframe_points: key.interpolation='LINEAR'
    bpy.context.scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True); bpy.context.view_layer.objects.active=arm
    # FBX needs the existing skinned mesh to write the original bind pose.
    # The library extracts only AnimationClip copies; this source model is
    # never instantiated or referenced as a second runtime Keiko.
    for ob in bpy.context.scene.objects:
        if ob.type=='MESH': ob.select_set(True)
    output=OUTPUT/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True,object_types={'ARMATURE','MESH'},
        add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,
        bake_anim_step=1,bake_anim_simplify_factor=0,
        primary_bone_axis='Y',secondary_bone_axis='X',axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        use_armature_deform_only=False,use_mesh_modifiers=False)
    velocities=[(vec(b)-vec(a)).length*FPS for a,b in zip(tips,tips[1:])]
    REPORT['takes'][name]={"frames":frames,"fps":FPS,"duration":(frames-1)/FPS,"contactSeconds":contact,
        "maxGripErrorMeters":max(grip_errors),"bladeTipPeakSpeedMetersPerSecond":max(velocities),
        "bladeTips":tips,"sha256":hashlib.sha256(output.read_bytes()).hexdigest(),"bones":list(arm.data.bones.keys())}
    bpy.ops.wm.save_as_mainfile(filepath=str(EVIDENCE/(name+'.blend')))
    print('BAKED',name,REPORT['takes'][name]['duration'],'GRIP ERROR',max(grip_errors),'TIP PEAK',max(velocities))

idle_source=FOLDER/'Keiko@Idle.fbx'
reference=load(idle_source);bpy.context.scene.frame_set(1)
FOCUS_ROTATIONS={name:reference.pose.bones[name].matrix.to_quaternion().copy() for name in ['mixamorig:Neck','mixamorig:Head']}
bake(idle_source,'Two handed battle idle',61,(1,1),lambda t,n:(vec(READY)+vec((0,math.sin(t*math.pi)*.002,0)),vec(READY_BLADE)),loop=True)
bake(idle_source,'Normal preparation',9,(1,1),lambda t,n:lerp_keys([(0,READY,READY_BLADE),(.267,(-.035,.743,.094),(-.16,.86,.485))],t))
bake(idle_source,'Heavy preparation',13,(1,1),lambda t,n:lerp_keys([(0,READY,READY_BLADE),(.4,(-.020,.755,.088),(0,.89,.45))],t))
bake(FOLDER/'Normal attack.fbx','Normal attack corrected',34,(1,39),lambda t,n:lerp_keys(NORMAL,t),contact=.60)
bake(FOLDER/'Hard jump attack.fbx','Hard jump attack corrected',44,(1,58),lambda t,n:lerp_keys(HEAVY,t),contact=.86,body=stabilize_heavy_body)
bake(FOLDER/'Run to hit enemies.fbx','Two handed approach',25,(1,25),lambda t,n:(vec(READY)+vec((0,math.sin(t*math.pi*5)*.006,0)),vec(READY_BLADE)))
bake(FOLDER/'Run Back after hit.fbx','Two handed return',25,(1,25),lambda t,n:(vec(READY)+vec((0,math.sin(t*math.pi*5)*.006,0)),vec(READY_BLADE)))
entrance=Path(r'D:/3DMODELS/Keiko sword attack/Enter the batle.fbx')
if OUTPUT==FOLDER: shutil.copy2(entrance,FOLDER/'Enter the batle.fbx')
bake(entrance,'Enter battle corrected',37,(1,19),lambda t,n:lerp_keys([(0,(-.02,.668,.093),(0,.55,.835)),(.75,(-.027,.721,.107),(0,.8,.6)),(1.2,READY,READY_BLADE)],t))
(EVIDENCE/'animation_bake_report.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
