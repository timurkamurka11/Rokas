"""Blender 5.2: original Keiko rig, held Throw stance and a designed dagger.

Run with Blender --background --factory-startup --python this_file.py.
Writes only the new Throw takes, dagger source and this pass's evidence.
"""
import bpy, math, json, hashlib
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

ROOT = Path('D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY')
ACTORS = ROOT / 'Assets/Rokas/Art/CombatActors'
OUT = ACTORS / 'Keiko'
WEAPON = ACTORS / 'Weapons/KeikoThrowingDagger'
EVIDENCE = ROOT / 'Docs/Verification/ReactiveTurnsFinalVfx/AnimationRepair'
for folder in [WEAPON, EVIDENCE]: folder.mkdir(parents=True, exist_ok=True)
SOURCE = OUT / 'Two handed battle idle.fbx'
FPS = 120
RELEASE_FRAME = 51
RELEASE = RELEASE_FRAME / FPS
FORWARD = Vector((math.cos(math.radians(65)), 0, math.sin(math.radians(65))))
REPORT = {'blender': bpy.app.version_string, 'source': str(SOURCE),
          'sourceSha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
          'rigCoordinates': 'X lateral, Y up, Z forward; source armature rotated X +90 degrees in Blender world',
          'runtimeForwardYaw': 65, 'releaseDirectionInRigCoordinates': list(FORWARD),
          'rootMotion': 'Horizontal Hips position constant; no object translation; all movement owned by arena',
          'takes': {}}

def update(): bpy.context.view_layer.update()
def clear():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def smooth(t):
    t=max(0,min(1,t)); return t*t*(3-2*t)
def scalar(keys,t):
    if t<=keys[0][0]: return keys[0][1]
    for a,b in zip(keys,keys[1:]):
        if t<=b[0]: return a[1]+(b[1]-a[1])*smooth((t-a[0])/(b[0]-a[0]))
    return keys[-1][1]
def lerp(keys,t):
    if t<=keys[0][0]: return Vector(keys[0][1])
    for a,b in zip(keys,keys[1:]):
        if t<=b[0]: return Vector(a[1]).lerp(Vector(b[1]),smooth((t-a[0])/(b[0]-a[0])))
    return Vector(keys[-1][1])
def material(name,color,metallic=0,roughness=.45,emission=0):
    m=bpy.data.materials.new(name);m.diffuse_color=color;m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value=color
    bs.inputs['Metallic'].default_value=metallic;bs.inputs['Roughness'].default_value=roughness
    if emission:
        bs.inputs['Emission Color'].default_value=color;bs.inputs['Emission Strength'].default_value=emission
    return m
def mesh(name,vertices,faces,materials,face_materials=None):
    data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
    ob=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(ob)
    for m in materials: data.materials.append(m)
    if face_materials:
        for p,i in zip(data.polygons,face_materials): p.material_index=i
    return ob
def cylinder(name,radius,depth,z,mat,vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=(0,0,z))
    ob=bpy.context.object;ob.name=name;ob.data.materials.append(mat)
    bevel=ob.modifiers.new('Small forged edge bevel','BEVEL');bevel.width=.0007;bevel.segments=2
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=bevel.name)
    return ob
def dagger():
    """Faceted leaf blade, steel edges, swept brass guard and wrapped grip."""
    steel=material('Dagger_ObsidianSteel',(.055,.075,.095,1),.85,.28)
    edge=material('Dagger_HonedSteel',(.32,.40,.46,1),.95,.22)
    bronze=material('Dagger_AgedBronze',(.26,.13,.05,1),.8,.37)
    leather=material('Dagger_CharcoalWrap',(.025,.022,.03,1),0,.8)
    rune=material('Dagger_FrostRune',(.035,.34,.48,1),.3,.3,.55)
    parts=[]
    # Six-point diamond section provides a visible spine and actual thin edges.
    rings=[(.040,.009,.0025),(.049,.017,.0032),(.068,.0185,.004),
           (.105,.014,.0036),(.140,.0075,.0025),(.167,.00005,.00005)]
    verts=[]
    for z,w,h in rings:
        verts.extend([(-w,0,z),(-w*.56,-h*.7,z),(0,-h,z),(w,0,z),(w*.56,h*.7,z),(0,h,z)])
    faces=[];mi=[]
    for j in range(len(rings)-1):
        for k in range(6):
            faces.append((j*6+k,j*6+(k+1)%6,(j+1)*6+(k+1)%6,(j+1)*6+k))
            mi.append(1 if k in [0,2,3,5] else 0)
    faces.extend([tuple(range(5,-1,-1)),tuple((len(rings)-1)*6+i for i in range(6))]);mi.extend([0,1])
    parts.append(mesh('ForgedLeafBlade',verts,faces,[steel,edge],mi))
    # A compact swept guard, custom polygon silhouette instead of a box.
    outline=[(-.029,.032),(-.027,.041),(-.017,.043),(-.010,.039),(.010,.039),
             (.017,.043),(.027,.041),(.029,.032),(.020,.034),(.012,.035),
             (-.012,.035),(-.020,.034)]
    vv=[(x,y,z) for y in [-.003,.003] for x,z in outline];n=len(outline)
    ff=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    ff.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    guard=mesh('SweptRavenGuard',vv,ff,[bronze]);parts.append(guard)
    parts.append(cylinder('GripCore',.0085,.064,0,leather))
    for z in [-.031,.028,.036]: parts.append(cylinder('ForgedGripCollar',.010,.003,z,bronze))
    # Continuous leather winding is actual geometry with a visible seam.
    v=[];f=[];steps=180
    for i in range(steps+1):
        a=i/steps*math.pi*2*9;z=-.027+i/steps*.053
        for r,dz in [(.0089,-.00075),(.0089,.00075),(.00965,.00075),(.00965,-.00075)]:
            v.append((math.cos(a)*r,math.sin(a)*r,z+dz))
    for i in range(steps):
        for k in range(4): f.append((i*4+k,i*4+(k+1)%4,(i+1)*4+(k+1)%4,(i+1)*4+k))
    parts.append(mesh('SpiralLeatherBinding',v,f,[leather]))
    parts.append(cylinder('Pommel',.0115,.008,-.037,bronze))
    # A restrained rune strip inset in the ridge on both blade faces.
    for side in [-1,1]:
        vv=[];ff=[]
        for z in [.072,.083,.094,.105]:
            width=.0012;y=side*(.0042-(z-.072)*.016)
            s=len(vv);vv.extend([(-width,y,z-.002),(0,y+side*.0002,z),
                               (width,y,z-.002),(0,y+side*.0002,z+.004)])
            ff.append((s,s+1,s+2,s+3))
        parts.append(mesh('FrostRuneInlay',vv,ff,[rune]))
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts: ob.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
    ob=bpy.context.object;ob.name='KeikoThrowingDagger';ob.data.name='KeikoThrowingDaggerMesh'
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    tri=ob.modifiers.new('Game ready triangulation','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
    return ob

def export_dagger():
    clear();ob=dagger()
    bpy.ops.export_scene.fbx(filepath=str(WEAPON/'KeikoThrowingDagger.fbx'),use_selection=True,
        object_types={'MESH'},bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True)
    REPORT['dagger']={'vertices':len(ob.data.vertices),'triangles':len(ob.data.polygons),
        'materials':[m.name for m in ob.data.materials], 'sourcePivot':[0,0,0],
        'sourceBladeAxis':'+Z','sourceFlatNormal':'+Y','sourceTip':[0,0,.167],
        'sourceLength':.208,'sourceGrip':[-.028,.028],
        'export':str(WEAPON/'KeikoThrowingDagger.fbx'),
        'sha256':hashlib.sha256((WEAPON/'KeikoThrowingDagger.fbx').read_bytes()).hexdigest(),
        'unityWrapperContract':'Place grip at origin and rotate imported blade direction onto prefab +Z; source Blender world +Z is exported FBX +Y with bake_space_transform.'}
    bpy.ops.wm.save_as_mainfile(filepath=str(EVIDENCE/'KeikoThrowingDagger.blend'))

def align_axis(pb,desired,plane_normal):
    old=pb.matrix.copy();y=desired.normalized()
    z=(plane_normal-y*plane_normal.dot(y)).normalized();x=y.cross(z).normalized()
    pb.matrix=Matrix.LocRotScale(old.translation,Matrix((x,y,z)).transposed().to_quaternion(),old.to_scale());update()
def solve_arm(arm,side,wrist):
    upper=arm.pose.bones['mixamorig:'+side+'Arm'];fore=arm.pose.bones['mixamorig:'+side+'ForeArm']
    hand=arm.pose.bones['mixamorig:'+side+'Hand'];a=upper.head.copy()
    l1=(fore.head-a).length;l2=(hand.head-fore.head).length;delta=wrist-a
    distance=max(abs(l1-l2)+.0002,min(delta.length,l1+l2-.0002));axis=delta.normalized()
    pole=Vector((-.24 if side=='Right' else .24,-.065,-.025))
    pole=(pole-axis*pole.dot(axis)).normalized()
    along=(l1*l1-l2*l2+distance*distance)/(2*distance);height=math.sqrt(max(0,l1*l1-along*along))
    elbow=a+axis*along+pole*height;plane=axis.cross(pole).normalized()
    align_axis(upper,elbow-a,plane);align_axis(fore,a+axis*distance-fore.head,plane)
    return hand
def right_rotation(blade):
    z=blade.normalized();y=Vector((1,0,0));y=(y-z*y.dot(z)).normalized();x=y.cross(z).normalized()
    return Matrix((x,y,z)).transposed().to_quaternion()
def left_rotation():
    y=Vector((.25,-.8,.3)).normalized();z=Vector((0,0,1));z=(z-y*z.dot(y)).normalized();x=y.cross(z).normalized()
    return Matrix((x,y,z)).transposed().to_quaternion()

def procedural_pose(arm,base,t,prep):
    # Preparation reaches its distinctive loaded pose, then the runtime holds
    # the last sample without a time limit. Attack starts at that same pose.
    phase=smooth(t/.35) if prep else 1
    yaw=(-22*phase if prep else scalar([(0,-22),(.16,-35),(.27,-33),(.425,12),(.57,20),(.80,4),(1.10,0)],t))
    pitch=(4*phase if prep else scalar([(0,4),(.16,7),(.27,7),(.425,-8),(.58,-10),(.8,-2),(1.1,0)],t))
    roll=(-3*phase if prep else scalar([(0,-3),(.22,-5),(.425,2),(.60,5),(.8,1),(1.1,0)],t))
    pb=arm.pose.bones['mixamorig:Spine'];m=pb.matrix.copy()
    rotation=Quaternion(Vector((0,1,0)),math.radians(yaw)) @ Quaternion(Vector((1,0,0)),math.radians(pitch)) @ Quaternion(Vector((0,0,1)),math.radians(roll))
    pb.matrix=Matrix.LocRotScale(m.translation,rotation@m.to_quaternion(),m.to_scale());update()
    # Opponent focus survives the throwing torso rotation.
    for name in ['mixamorig:Neck','mixamorig:Head']:
        p=arm.pose.bones[name];m=p.matrix.copy();p.matrix=Matrix.LocRotScale(m.translation,FOCUS[name],m.to_scale());update()
    right_shoulder=arm.pose.bones['mixamorig:RightArm'].head.copy()
    left_shoulder=arm.pose.bones['mixamorig:LeftArm'].head.copy()
    if prep:
        target=right_shoulder+Vector((-.11,.12,-.055));right_wrist=BASE_HAND['Right'].lerp(target,phase)
        left_wrist=BASE_HAND['Left'].lerp(left_shoulder+Vector((.035,-.13,.04)),phase)
        blade=BASE_BLADE.lerp(Vector((-.22,.93,.295)),phase).normalized()
    else:
        offset=lerp([(0,(-.11,.12,-.055)),(.16,(-.11,.09,-.105)),(.27,(-.09,.095,-.105)),
                     (.34,(-.025,.068,-.025)),(.47,tuple(FORWARD*.186+Vector((0,-.009,0)))),
                     (.57,tuple(FORWARD*.154+Vector((.005,-.055,0)))),(.78,(-.025,-.07,.13)),(1.1,(-.04,-.08,.09))],t)
        right_wrist=right_shoulder+offset
        left_wrist=left_shoulder+lerp([(0,(.035,-.13,.04)),(.24,(.05,-.105,.02)),
                      (.425,(.065,-.085,-.045)),(.60,(.055,-.095,-.06)),(.82,(.04,-.13,.04)),(1.1,(.025,-.13,.04))],t)
        blade=lerp([(0,(-.22,.93,.295)),(.24,(-.20,.94,.278)),(.34,tuple((FORWARD+Vector((0,.25,0))).normalized())),
                    (.425,tuple(FORWARD)),(.57,tuple((FORWARD+Vector((0,-.35,0))).normalized())),(.80,(0,.5,.866)),(1.1,tuple(BASE_BLADE))],t).normalized()
    right=solve_arm(arm,'Right',right_wrist)
    right.matrix=Matrix.LocRotScale(right.matrix.translation,right_rotation(blade),Vector((1,1,1)));update()
    left=solve_arm(arm,'Left',left_wrist)
    left.matrix=Matrix.LocRotScale(left.matrix.translation,left_rotation(),Vector((1,1,1)));update()
    for side in ['Right','Left']:
        openness=(.80 if side=='Left' else (0 if prep else smooth((t-RELEASE)/.085)))
        for i in range(1,4):
            p=arm.pose.bones['mixamorig:'+side+'HandIndex'+str(i)]
            p.rotation_mode='QUATERNION';p.rotation_quaternion=p.rotation_quaternion@Quaternion(Vector((1,0,0)),math.radians(-(46 if i==2 else 35)*openness))
    update()
    if prep and phase < 1:
        for p in arm.pose.bones:
            l,q,s=p.matrix_basis.decompose();bl,bq,bs=base[p.name].decompose()
            p.matrix_basis=Matrix.LocRotScale(bl.lerp(l,phase),bq.slerp(q,phase),bs.lerp(s,phase))
        update()
    if not prep:
        # Return every bone to the exact original ready pose. The sword is
        # restored by presentation after the completed throw recovery.
        recovery=smooth((t-.78)/.32)
        if recovery:
            for p in arm.pose.bones:
                l,q,s=p.matrix_basis.decompose();bl,bq,bs=base[p.name].decompose()
                p.matrix_basis=Matrix.LocRotScale(l.lerp(bl,recovery),q.slerp(bq,recovery),s.lerp(bs,recovery))
            update()

def bake(name,duration,prep):
    global BASE_HAND,BASE_BLADE,FOCUS
    clear();bpy.ops.import_scene.fbx(filepath=str(SOURCE),automatic_bone_orientation=False)
    arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');assert len(arm.data.bones)==33
    bpy.context.scene.frame_set(1);update()
    base={p.name:p.matrix_basis.copy() for p in arm.pose.bones}
    bind={b.name:[list(r) for r in b.matrix_local] for b in arm.data.bones}
    BASE_HAND={side:arm.pose.bones['mixamorig:'+side+'Hand'].head.copy() for side in ['Right','Left']}
    BASE_BLADE=arm.pose.bones['mixamorig:RightHand'].matrix.to_quaternion()@Vector((0,0,1))
    FOCUS={n:arm.pose.bones[n].matrix.to_quaternion().copy() for n in ['mixamorig:Neck','mixamorig:Head']}
    arm.animation_data_clear()
    count=round(duration*FPS)+1;samples=[];diagnostics=[];prop_matrices=[]
    for frame in range(count):
        for p in arm.pose.bones:p.matrix_basis=base[p.name]
        update();t=frame/FPS;procedural_pose(arm,base,t,prep)
        samples.append({p.name:p.matrix_basis.copy() for p in arm.pose.bones})
        hand=arm.pose.bones['mixamorig:RightHand'];socket=hand.matrix@Matrix.Translation(Vector((0,.033,0)))
        prop_matrices.append(arm.matrix_world@socket)
        direction=hand.matrix.to_quaternion()@Vector((0,0,1))
        diagnostics.append({'frame':frame,'seconds':t,'rightHand':list(hand.head),
            'leftHand':list(arm.pose.bones['mixamorig:LeftHand'].head),'grip':list(socket.translation),
            'releaseDirection':list(direction),'directionAlignmentToEnemy':direction.dot(FORWARD),
            'hips':list(arm.pose.bones['mixamorig:Hips'].head)})
    action=bpy.data.actions.new('Keiko_'+name);arm.animation_data_create();arm.animation_data.action=action
    previous={}
    for frame,values in enumerate(samples):
        for p in arm.pose.bones:
            loc,q,scale=values[p.name].decompose()
            if p.name in previous and q.dot(previous[p.name])<0:q.negate()
            previous[p.name]=q.copy();p.rotation_mode='QUATERNION';p.location=loc;p.rotation_quaternion=q;p.scale=scale
            for prop in ['location','rotation_quaternion','scale']:p.keyframe_insert(prop,frame=frame,group=p.name)
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag=strip.channelbag(slot)
                if bag:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:key.interpolation='LINEAR'
    scene=bpy.context.scene;scene.render.fps=FPS;scene.frame_start=0;scene.frame_end=count-1;scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm
    for ob in scene.objects:
        if ob.type=='MESH':ob.select_set(True)
    output=OUT/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True,object_types={'ARMATURE','MESH'},
        add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,bake_anim_step=1,bake_anim_simplify_factor=0,
        primary_bone_axis='Y',secondary_bone_axis='X',axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_armature_deform_only=False,use_mesh_modifiers=False)
    # Prop present only in authoring scene; animation FBX contains the original
    # actor/rig only, so runtime keeps its own authoritative detach event.
    prop=dagger()
    for f,m in enumerate(prop_matrices):
        prop.matrix_world=m
        for attr in ['location','rotation_euler','scale']:prop.keyframe_insert(attr,frame=f)
    if not prep:
        prop.hide_render=False;prop.keyframe_insert('hide_render',frame=RELEASE_FRAME)
        prop.hide_render=True;prop.keyframe_insert('hide_render',frame=RELEASE_FRAME+1)
    scene.frame_set(count-1 if prep else RELEASE_FRAME)
    bpy.ops.wm.save_as_mainfile(filepath=str(EVIDENCE/(name+'.blend')))
    velocities=[(Vector(b['grip'])-Vector(a['grip'])).length*FPS for a,b in zip(diagnostics,diagnostics[1:])]
    report={'frames':count,'lastFrame':count-1,'fps':FPS,'duration':duration,'bones':list(arm.data.bones.keys()),
        'sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'bind':bind,
        'horizontalHipTravel':max((Vector(d['hips'])-Vector(diagnostics[0]['hips'])).length for d in diagnostics),
        'peakHandSpeed':max(velocities),'releaseFrame':None if prep else RELEASE_FRAME,
        'releaseSeconds':None if prep else RELEASE,'heldPreview':'Clamp final preparation sample indefinitely' if prep else None,
        'diagnostics':diagnostics}
    REPORT['takes'][name]=report
    print('BAKED',name,duration,'RELEASE',report['releaseSeconds'],'HAND SPEED',report['peakHandSpeed'])

export_dagger()
bake('Throw preparation',.4,True)
bake('Throw attack corrected',1.1,False)
(EVIDENCE/'throw_authoring_report.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
print('DONE THROW AUTHORING',str(EVIDENCE))
