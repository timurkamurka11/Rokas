#!/usr/bin/env python3
"""ROKAS production art v1: 1 skinned right hand + dark loose cloth sleeve.
Uses licensed CC0 anatomical hand GLB. Generates transparent RGBA overlays.
Contact 3.10s in the Unity sequence = (3.10 - 1.8) * 24 ~= frame 32.
Not a substitute for human art/game-view approval.
"""
import argparse, json, math, os, sys, traceback
import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

W, H, FPS, TOTAL = 1672, 941, 24, 49
POWER_X, POWER_Y = 1040., 708.
FRAME_LENGTH = .84 # camera ortho height in Blender meters
CONTACT_INDEX = round((3.10 - 1.8) * FPS)

def args():
    argv=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    p=argparse.ArgumentParser()
    p.add_argument("--input",required=True)
    p.add_argument("--output",required=True)
    p.add_argument("--frames",type=int,default=TOTAL)
    p.add_argument("--width",type=int,default=W)
    p.add_argument("--height",type=int,default=H)
    p.add_argument("--samples",type=int,default=10)
    p.add_argument("--only",default="")
    return p.parse_args(argv)

def camera_xy(px,py):
    # Exact inverse of the currently configured Blender orthographic viewport.
    # Do not guess whether ortho_scale denotes horizontal or vertical size.
    sc=bpy.context.scene
    corners=sc.camera.data.view_frame(scene=sc)
    xmin=min(v.x for v in corners);xmax=max(v.x for v in corners)
    ymin=min(v.y for v in corners);ymax=max(v.y for v in corners)
    return Vector((xmin+(px/W)*(xmax-xmin),ymax-(py/H)*(ymax-ymin),0.))

def smooth(v):
    v=max(0.,min(1.,v))
    return v*v*(3-2*v)

def mat(name,col,rough=.7,subsurface=0.,cloth=False):
    m=bpy.data.materials.new(name)
    m.diffuse_color=(*col,1)
    m.use_nodes=True
    nt=m.node_tree
    bsdf=next(n for n in nt.nodes if n.type=="BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value=(*col,1)
    bsdf.inputs["Roughness"].default_value=rough
    if "Subsurface Weight" in bsdf.inputs: bsdf.inputs["Subsurface Weight"].default_value=subsurface
    if "Subsurface Radius" in bsdf.inputs:bsdf.inputs["Subsurface Radius"].default_value=(1.0,.45,.24)
    if cloth:
        noise=nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value=135.
        noise.inputs["Detail"].default_value=2.
        bump=nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value=.052
        bump.inputs["Distance"].default_value=.0015
        nt.links.new(noise.outputs["Fac"],bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"],bsdf.inputs["Normal"])
    else:
        noise=nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value=185.
        noise.inputs["Detail"].default_value=2.
        bump=nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value=.018
        bump.inputs["Distance"].default_value=.00009
        nt.links.new(noise.outputs["Fac"],bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"],bsdf.inputs["Normal"])
    return m

def sleeve_geometry(mesh, wrist, cuff=False):
    # Four tapered cloth rings: skin cuff hugs the wrist; forearm fabric loosens
    # toward off-screen elbow. Add independent folds with low-frequency harmonics.
    axis=Vector((.56,-.83,0)).normalized()
    across=Vector((axis.y,-axis.x,0)).normalized()
    # Sleeve enters skin only ~10mm at the wrist, never buries the palm; no floating seam.
    # The back of the sleeve broadens toward the offscreen elbow.
    rings=([(-.012,.028),(-.010,.029),(-.007,.030),(-.003,.033),(0.,.035),
            (.027,.043),(.059,.050),(.102,.058),(.157,.064),(.214,.073),
            (.280,.080),(.352,.088),(.432,.096)]
           if not cuff else [(-.014,.027),(-.010,.0275),(-.006,.029),
                              (-.002,.031),(.004,.034),(.010,.036),(.022,.039),
                              (.031,.040)])
    N=64; verts=[]; faces=[]
    for ri,(distance,radius) in enumerate(rings):
        c=wrist + axis*distance
        for j in range(N):
            theta=2*math.pi*j/N
            # Organic soft fabric folds, different radii down the forearm.
            # The extended outer ring continues out of frame rather than
            # terminating as a visible square cylinder cap.
            fold=1+.027*math.sin(theta*5+ri*.45)+.019*math.cos(theta*9-ri*.35)
            if cuff:fold+=.008*math.cos(theta*32)
            d=radius*fold
            axial_warp=0.004*math.sin(theta*6+ri*.37)*(0.3 if cuff else 1)
            verts.append((c+axis*axial_warp+across*math.cos(theta)*d+
                          Vector((0,0,1))*math.sin(theta)*d*.67).to_tuple())
    for ri in range(len(rings)-1):
        for j in range(N):
            a=ri*N+j;b=ri*N+(j+1)%N
            faces.append((a,b,b+N,a+N))
    # Back of fabric is outside the camera crop; retain only the under-wrist
    # cap. Capping the distant end makes the obvious flat square plug.
    faces.append(tuple(reversed(range(N))))
    mesh.clear_geometry()
    mesh.from_pydata(verts,[],faces);mesh.update()
    for p in mesh.polygons:p.use_smooth=True

def apply_subtle_finger_slimming(hand, rig):
    """Single 6% radial edit per skinned vertex; no cumulative joint shrink."""
    mesh=hand.data
    inv=hand.matrix_world.inverted()
    influences={}
    for digit in ("index","middle","ring","pinky","thumb"):
        for segment in ("01","02","03"):
            name=f"{digit}_{segment}_r"
            group=hand.vertex_groups.get(name)
            bone=rig.data.bones.get(name)
            if group is None or bone is None: continue
            root=inv @ (rig.matrix_world @ bone.head_local)
            axis=(inv @ (rig.matrix_world @ bone.tail_local))-root
            if axis.length<1e-6:continue
            axis.normalize()
            for vertex in mesh.vertices:
                try: weight=group.weight(vertex.index)
                except RuntimeError:continue
                if weight>.25 and (vertex.index not in influences or weight>influences[vertex.index][0]):
                    influences[vertex.index]=(weight,root.copy(),axis.copy())
    for vertex in mesh.vertices:
        entry=influences.get(vertex.index)
        if entry is None:continue
        weight,root,axis=entry
        radial=vertex.co-root-axis*(vertex.co-root).dot(axis)
        vertex.co-=radial*(.062*min(1.,weight))
    mesh.update()
    print("ROKAS_SLIM_FINGER_UNIQUE_VERTICES",len(influences),flush=True)


def create_nail_objects():
    """Pale satin keratin with subtle curved edges, not chunky sphere caps."""
    matte=bpy.data.materials.new("Nail_Natural_Keratin_Subtle")
    matte.diffuse_color=(.63,.48,.43,1.)
    matte.use_nodes=True
    bsdf=next(node for node in matte.node_tree.nodes if node.type=="BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value=(.63,.48,.43,1.)
    bsdf.inputs["Roughness"].default_value=.49
    if "Coat Weight" in bsdf.inputs: bsdf.inputs["Coat Weight"].default_value=.12
    for digit in ("index","middle","ring","pinky","thumb"):
        verts=[]; faces=[]
        # Dorsal oval shield, 11 x 7 vertex lattice, slight convexity
        for yi in range(11):
            yy=-1.+2*yi/10
            for xi in range(7):
                xx=-1.+2*xi/6
                outline=max(0.,1.-(xx**2*.84+yy**2))
                # Nail plate sits flush at the edges: only center gently domed.
                width=.0058*max(.14,math.sqrt(max(0.,1.-yy*yy)))
                verts.append((xx*width, yy*.0081, .00065*outline))
        for yi in range(10):
            for xi in range(6):
                a=yi*7+xi
                faces.append((a,a+1,a+8,a+7))
        mesh=bpy.data.meshes.new(f"Nail_{digit}_SmoothSurface")
        mesh.from_pydata(verts,[],faces);mesh.update()
        obj=bpy.data.objects.new(f"ROKAS_AnatomicalNail_{digit}",mesh)
        bpy.context.collection.objects.link(obj)
        mesh.materials.append(matte)
        for poly in mesh.polygons:poly.use_smooth=True


def update_nail_positions(hand, rig, frame):
    """Position keratin onto real deforming distal skin, not offset GLB bones."""
    dg=bpy.context.evaluated_depsgraph_get()
    eval_hand=hand.evaluated_get(dg)
    evaluated=eval_hand.to_mesh()
    report={}
    try:
        mw=eval_hand.matrix_world
        wrist=rig.matrix_world @ rig.pose.bones["hand_r"].head
        for digit in ("index","middle","ring","pinky","thumb"):
            group=hand.vertex_groups.get(f"{digit}_03_r")
            obj=bpy.data.objects.get(f"ROKAS_AnatomicalNail_{digit}")
            if group is None or obj is None:raise RuntimeError("No distal nail/weight group: "+digit)
            vertices=[]
            for vertex in hand.data.vertices:
                try:weight=group.weight(vertex.index)
                except RuntimeError:continue
                if weight>.34 and vertex.index<len(evaluated.vertices):
                    vertices.append((mw @ evaluated.vertices[vertex.index].co,weight))
            if len(vertices)<8:
                raise RuntimeError(f"Not enough skinned fingertip surface vertices for {digit}: {len(vertices)}")
            # Distal-most 15% of the actual vertex cloud, not nominal GLB
            # bone.tail, which can be 80-95px away from the rendered surface.
            def dist2(v):
                dx=v.x-wrist.x;dy=v.y-wrist.y
                return dx*dx+dy*dy
            vertices.sort(key=lambda p:dist2(p[0]),reverse=True)
            tips=[v for v,w in vertices[:max(8,len(vertices)//7)]]
            tip=sum(tips,Vector())/len(tips)
            direction=Vector((tip.x-wrist.x,tip.y-wrist.y))
            if direction.length<1e-5:raise RuntimeError("No digit axis for "+digit)
            direction.normalize()
            # Nail center is behind the fingertip, on the keratin plate,
            # instead of over the swollen tip pad or dorsal knuckle.
            cx=tip.x-direction.x*.0085
            cy=tip.y-direction.y*.0085
            near=[v for v,w in vertices if
                  (v.x-cx)**2+(v.y-cy)**2 <= .013**2]
            if len(near)<4:
                near=[v for v,w in vertices if
                      (v.x-cx)**2+(v.y-cy)**2 <= .018**2]
            if len(near)<4:raise RuntimeError("No nail bed found for "+digit)
            # Nail front plate must be on the camera-facing +Z surface.
            surface=max(v.z for v in near)
            obj.hide_render=False
            obj.location=(cx,cy,surface+.00038)
            obj.rotation_euler=(0.,0.,math.atan2(direction.y,direction.x)-math.pi*.5)
            obj.scale=(.85 if digit=="pinky" else 1.02 if digit=="thumb" else .95, .88, 1.)
            obj.keyframe_insert(data_path="location",frame=frame)
            obj.keyframe_insert(data_path="rotation_euler",frame=frame)
            obj.keyframe_insert(data_path="scale",frame=frame)
            report[digit]={"tip_distance_mm":round((tip-wrist).length*1000,2),
                           "nail_to_tip_mm":round(math.hypot(tip.x-cx,tip.y-cy)*1000,2),
                           "nail_skin_z_gap_mm":.38,
                           "nail_bed_vertices":len(near)}
    finally:
        eval_hand.to_mesh_clear()
    return report


def add_area(name,loc,watts,rgb,size):
    ld=bpy.data.lights.new(name,"AREA");o=bpy.data.objects.new(name,ld);bpy.context.collection.objects.link(o)
    o.location=Vector(loc);ld.energy=watts;ld.color=rgb;ld.shape="DISK";ld.size=size
    o.rotation_euler=(Vector((0,0,0))-o.location).to_track_quat("-Z","Y").to_euler()

def create_scene(a):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(a.input))
    rig=next((o for o in bpy.data.objects if o.type=="ARMATURE" and "index_03_r" in o.pose.bones),None)
    if not rig:raise ValueError("CC0 hand armature with index_03_r not found")
    meshes=[o for o in bpy.data.objects if o.type=="MESH"]
    hand=next((o for o in meshes if len(o.data.vertices)>1000),None)
    if not hand:raise ValueError("Expected skinned high quality right-hand mesh")
    for o in meshes:
        if o != hand:o.hide_render=True
    skin=mat("Skin_Cinematic_NaturalWarm",(.55,.365,.305),.72,.085)
    # Natural skin color breakup: low-frequency blotches and subtle joint redness.
    nodes=skin.node_tree.nodes
    links=skin.node_tree.links
    skin_bsdf=next(n for n in nodes if n.type=="BSDF_PRINCIPLED")
    pores=nodes.new("ShaderNodeTexNoise")
    pores.inputs["Scale"].default_value=36.
    pores.inputs["Detail"].default_value=3.
    palette=nodes.new("ShaderNodeValToRGB")
    palette.color_ramp.elements[0].position=.23
    palette.color_ramp.elements[0].color=(.39,.235,.19,1)
    palette.color_ramp.elements[1].position=.77
    palette.color_ramp.elements[1].color=(.58,.38,.31,1)
    links.new(pores.outputs["Fac"],palette.inputs["Fac"])
    links.new(palette.outputs["Color"],skin_bsdf.inputs["Base Color"])
    hand.data.materials.clear();hand.data.materials.append(skin)
    apply_subtle_finger_slimming(hand,rig)
    create_nail_objects()
    # Force mildly animated real finger joints, no frame-to-frame morph cards.
    for bone in rig.pose.bones:
        bone.rotation_mode="XYZ"
    fabric=mat("Graphite_HomeSweatshirt_Cotton",(.018,.022,.027),.94,0,True)
    sleeve_mesh=bpy.data.meshes.new("LooseSleeve_Creased_Tube")
    sleeve_obj=bpy.data.objects.new("RIGHT_FreeHomeSleeve",sleeve_mesh)
    bpy.context.collection.objects.link(sleeve_obj)
    sleeve_mesh.materials.append(fabric)
    cuff_mesh=bpy.data.meshes.new("RibbedCuff")
    cuff=bpy.data.objects.new("RIGHT_SleeveSoftCuff",cuff_mesh)
    bpy.context.collection.objects.link(cuff)
    cuff_mat=mat("Cuff_Graphite",(.013,.016,.020),.95,0,True)
    cuff_mesh.materials.append(cuff_mat)
    # Local sleeve/cuff meshes are now static geometry following keyed object transforms.
    # This preserves a genuinely editable .blend instead of only a single-pose snapshot.
    sleeve_geometry(sleeve_mesh, Vector((0,0,0)))
    cuff_mesh.clear_geometry()
    sleeve_geometry(cuff_mesh, Vector((0,0,0)), cuff=True)
    sleeve_mesh.materials.clear(); sleeve_mesh.materials.append(fabric)
    cuff_mesh.materials.clear(); cuff_mesh.materials.append(cuff_mat)
    camera_data=bpy.data.cameras.new("POV_1672x941_PixelAligned")
    cam=bpy.data.objects.new("POV_1672x941_PixelAligned",camera_data)
    bpy.context.collection.objects.link(cam)
    cam.location=(0,0,3.0)
    cam.rotation_euler=(0,0,0)
    # Camera local -Z views XY plane, camera up is global +Y.
    camera_data.type="ORTHO";camera_data.ortho_scale=FRAME_LENGTH
    bpy.context.scene.camera=cam
    add_area("Window_Amber_Key",(-.45,.65,1.35),65,(1.,.66,.40),.75)
    add_area("Monitor_Warm_Fill",(.65,.15,1.1),40,(1.,.82,.68),.65)
    add_area("Ambient_Cool_Edge",(0,-.55,1.0),25,(.59,.67,.80),.45)
    scene=bpy.context.scene
    scene.render.engine="CYCLES"
    scene.cycles.samples=max(2,a.samples)
    scene.render.resolution_x=a.width;scene.render.resolution_y=a.height
    scene.render.resolution_percentage=100
    scene.render.film_transparent=True
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    scene.render.image_settings.color_depth="8"
    scene.view_settings.view_transform="AgX"
    scene.view_settings.exposure=-1.75
    scene.render.fps=FPS
    scene.render.filepath=os.path.join(a.output,"HandsRight_0000.png")
    scene.render.image_settings.compression=35
    scene.world=bpy.data.worlds.new("ROKAS_WarmNightEnvironment")
    scene.world.color=(.055,.045,.038)
    scene.camera.data.lens=45
    return rig,hand,sleeve_mesh,cuff_mesh,scene


# Real skinned contact is measured from the evaluated distal skin, never from
# the glTF bone tail (which is offset from the visible fingertip).
def index_skin_tip_px(hand,rig):
    dg=bpy.context.evaluated_depsgraph_get()
    evaluated_obj=hand.evaluated_get(dg)
    evaluated_mesh=evaluated_obj.to_mesh()
    try:
        grp=hand.vertex_groups.get("index_03_r")
        if grp is None:raise RuntimeError("Skinned index distal vertex group missing")
        wrist=rig.matrix_world @ rig.pose.bones["hand_r"].head
        cloud=[]
        for vertex in hand.data.vertices:
            w=next((membership.weight for membership in vertex.groups
                    if membership.group==grp.index),0.)
            if w >= .45 and vertex.index<len(evaluated_mesh.vertices):
                v=evaluated_obj.matrix_world @ evaluated_mesh.vertices[vertex.index].co
                cloud.append((v,(v.x-wrist.x)**2+(v.y-wrist.y)**2))
        if len(cloud)<12:raise RuntimeError("Index distal surface does not contain enough weighted vertices")
        cloud.sort(key=lambda x:x[1],reverse=True)
        # Extremal skin surface, not the displaced bone reference.
        tip=sum((p for p,d in cloud[:max(8,len(cloud)//9)]),Vector())/max(8,len(cloud)//9)
        uv=world_to_camera_view(bpy.context.scene,bpy.context.scene.camera,tip)
        return (uv.x*W,(1.-uv.y)*H)
    finally:
        evaluated_obj.to_mesh_clear()

def articulate_index(rig,reach,curl,yaw,press_amount):
    # MCP/PIP/DIP have independently keyed X rotations; the knuckle points
    # the pad downward while PIP/DIP shorten the silhouette naturally.
    for joint,mult,base in (("01",.34,.08),("02",.64,.12),("03",.42,.075)):
        pb=rig.pose.bones["index_"+joint+"_r"]
        pb.rotation_euler=(mult*curl,0.,base*reach+yaw*(1. if joint=="01" else .32)+
                           (.028 if joint=="03" else 0.)*press_amount)

def fit_index_pad_to_power(rig,hand,reach,frame,press_amount):
    # Geometry-space 2D calibration of the actual pad to the approved
    # photographed button. The fingertip articulates independently of the
    # whole hand; at most a short (<=24px) anatomical wrist approach follows.
    target=(POWER_X,POWER_Y+2.2*press_amount)
    curl_floor=.15+.34*press_amount
    candidates=[]
    for curl in (curl_floor,curl_floor+.07,curl_floor+.14,curl_floor+.21,curl_floor+.28):
        for yaw in (-.35,-.20,-.08,0.,.08,.20,.35):
            articulate_index(rig,reach,curl,yaw,press_amount)
            bpy.context.view_layer.update()
            px=index_skin_tip_px(hand,rig)
            d=math.hypot(px[0]-target[0],px[1]-target[1])
            candidates.append((d,curl,yaw,px))
    candidates.sort(key=lambda x:x[0])
    print("ROKAS_FINGER_GRID",frame,
          [(round(x[0],1),x[1],x[2],tuple(round(v,1) for v in x[3])) for x in candidates[:6]],
          flush=True)
    best=candidates[0]
    for step in (.065,.025):
        tests=[]
        for curl in (max(curl_floor,best[1]-step),best[1],min(.95,best[1]+step)):
            for yaw in (max(-.45,best[2]-step),best[2],min(.45,best[2]+step)):
                articulate_index(rig,reach,curl,yaw,press_amount)
                bpy.context.view_layer.update()
                px=index_skin_tip_px(hand,rig)
                tests.append((math.hypot(px[0]-target[0],px[1]-target[1]),curl,yaw,px))
        best=min(tests,key=lambda x:x[0])
    articulate_index(rig,reach,best[1],best[2],press_amount)
    bpy.context.view_layer.update()
    pad=index_skin_tip_px(hand,rig)
    dx=target[0]-pad[0];dy=target[1]-pad[1]
    shift=math.hypot(dx,dy)
    # If the finger is too far away, the anatomy is still wrong. Do not
    # "fix" it by pulling the entire mesh across the image.
    if shift>24.:
        # Keep the failed 3D pose for diagnosis; never shift the rig beyond
        # the natural anatomical bound. Final run still FAILS after rendering.
        print("ROKAS_POWER_OUT_OF_REACH",frame,round(shift,2),flush=True)
    else:
        delta_world=camera_xy(target[0],target[1])-camera_xy(pad[0],pad[1])
        rig.location.x += delta_world.x
        rig.location.y += delta_world.y
        bpy.context.view_layer.update()
    measured=index_skin_tip_px(hand,rig)
    error=math.hypot(measured[0]-target[0],measured[1]-target[1])
    print("ROKAS_SKIN_CONTACT_SOLVER",frame,"skin",measured,"target",target,
          "error_px",round(error,2),"curl",best[1],"yaw",best[2],
          "wrist_shift_px",round(shift,2),flush=True)
    if error>4.:
        print("ROKAS_SKIN_CONTACT_QA_FAIL",frame,round(error,2),flush=True)
    return measured,error

def pose_for_frame(rig,hand,sleeve_mesh,cuff_mesh,scene,index,total):
    t=index/FPS
    # First entry at bottom-right, confident anatomical motion toward upper-right
    # keyboard; last pose retracts without re-entering.
    reach=smooth(t/1.30)
    withdraw=smooth((t-1.40)/.55)
    press=smooth((t-1.27)/.06)*(1-smooth((t-1.40)/.07))
    # Camera-calibrated armature reach; index curl is solved separately in deformed skin space.
    tip_x=1415.+(POWER_X+41.-1415.)*reach + 135.*withdraw
    # Skinned fingertip and bone tail differ; apply the measured 3D reach, not sprite offsets.
    # Calibrated against actual alpha pixels in the user-approved 1672x941 POV.
    # Finger approaches from above the illuminated Power key, then depresses
    # through the last 5px instead of translating sideways over the key.
    tip_y=1080.+(POWER_Y-16.-1080.)*reach + 175.*withdraw + 5.*press
    # Effortless low-frequency breathing; suppress during physical contact.
    tip_x+=2.2*math.sin(3.5*t)*(1-press)*smooth(t/.4)
    tip_y+=1.3*math.sin(3.2*t+1.2)*(1-press)*smooth(t/.4)
    wrist_x=tip_x+98.
    wrist_y=tip_y+82.
    # Wrist tucking rotates the actual glTF armature and leaves the finger mesh skinned.
    base=rig.pose.bones["hand_r"]
    direction=rig.data.bones["index_03_r"].tail_local-rig.data.bones["hand_r"].head_local
    desired=camera_xy(tip_x,tip_y)-camera_xy(wrist_x,wrist_y)
    rot=math.atan2(desired.y,desired.x)-math.atan2(direction.y,direction.x)
    # Wrist rolls gently down into the key; the visual contact is still
    # calibrated from the visible index-finger surface.
    rig.rotation_mode="XYZ";rig.rotation_euler=(.025+.07*reach, -.035*reach, rot)
    length=max(.001,Vector((direction.x,direction.y)).length)
    rig.scale=Vector((desired.length/length,)*3)
    # The mesh may have its own parent transform; Blender's skin modifier resolves its rig.
    for bone in rig.pose.bones:
        if bone.name.startswith("index_"):
            # Articulated separately after the palm is placed. The bone tail
            # must NOT determine the wrist translation after finger flexion.
            joint=bone.name.split("_")[1]
            bone.rotation_euler=(0.,0.,{"01":.08,"02":.12,"03":.075}.get(joint,.04)*reach)
        elif bone.name.startswith(("middle_","ring_","pinky_")):
            digit=bone.name.split("_")[0]
            joint=bone.name.split("_")[1]
            # Individually curled, grouped fingers: no splayed starfish pose.
            curls={"middle":(.38,.32,.19),"ring":(.48,.39,.26),
                   "pinky":(.56,.46,.32)}
            flex=curls[digit][{"01":0,"02":1,"03":2}.get(joint,0)]
            # X is the actual out-of-plane PIP/DIP bend. Z alone was
            # spreading straight fingers across the approved laptop POV.
            bone.rotation_euler=(flex*(.20+.10*reach),
                                 (.025 if digit=="pinky" else -.018 if digit=="ring" else 0.)*reach,
                                 (.025 if digit=="pinky" else -.018 if digit=="ring" else .008)*reach)
        elif bone.name.startswith("thumb_"):
            joint=bone.name.split("_")[1]
            bone.rotation_euler=(.025*reach, -.06*reach,
                                 {"01":.28,"02":.20,"03":.11}.get(joint,.04)*(0.65+.35*reach))
        else:
            bone.rotation_euler=(0,0,0)
    rig.location=(0,0,-.0035*press)
    bpy.context.view_layer.update()
    tip_now=rig.matrix_world @ rig.pose.bones["index_03_r"].tail
    wanted=camera_xy(tip_x,tip_y)
    rig.location.x += wanted.x-tip_now.x
    rig.location.y += wanted.y-tip_now.y
    bpy.context.view_layer.update()
    # From this point onward keep the wrist fixed. All reach/contact
    # correction happens through the three index phalanges.
    approach=smooth((index-17.)/13.)
    retract=smooth((index-34.)/10.)
    curl=(.07+.11*approach)*(1.-retract)
    articulate_index(rig,reach,curl,0.,0.)
    bpy.context.view_layer.update()
    contact_error=None
    if 30<=index<=34:
        # CONTACT 30, PRESS 31-33, RELEASE 34. No PNG positioning tricks.
        pad,contact_error=fit_index_pad_to_power(rig,hand,reach,index,{30:0.,31:.50,32:1.,33:.50,34:0.}[index])
    measured_pad=index_skin_tip_px(hand,rig)
    wrist=rig.matrix_world @ base.head
    nail_report=update_nail_positions(hand,rig,index+1)
    # Locally modeled cloth follows the wrist with real keyframed transforms.
    sleeve_obj=bpy.data.objects["RIGHT_FreeHomeSleeve"]
    cuff_obj=bpy.data.objects["RIGHT_SleeveSoftCuff"]
    # Important: wrist is the actual posed rig coordinate. Fixed Z=1.10
    # made the cuff float almost a metre in front of the original skinned mesh.
    sleeve_obj.location=(wrist.x, wrist.y, wrist.z-.009)
    cuff_obj.location=(wrist.x, wrist.y, wrist.z+.003)
    cuff_obj.scale=(.99,.99,.99)
    # Keyframe every control: the saved .blend contains 49 editable animation keys,
    # not merely a snapshot of the final pose.
    k=index+1
    rig.keyframe_insert(data_path="location",frame=k)
    rig.keyframe_insert(data_path="rotation_euler",frame=k)
    rig.keyframe_insert(data_path="scale",frame=k)
    for bone in rig.pose.bones:
        bone.keyframe_insert(data_path="rotation_euler",frame=k)
    sleeve_obj.keyframe_insert(data_path="location",frame=k)
    cuff_obj.keyframe_insert(data_path="location",frame=k)
    # Distance and overlap QA on every render frame, not just the press pose.
    seam_distance=(Vector(cuff_obj.location)-wrist).length
    if seam_distance>.025:
        raise RuntimeError(f"Wrist seam detached at frame {index}: {seam_distance:.5f} m")
    nails=[bpy.data.objects.get(f"ROKAS_AnatomicalNail_{d}")
           for d in ("index","middle","ring","pinky","thumb")]
    if any(n is None for n in nails):
        raise RuntimeError("Anatomical nail mesh absent in Blender frame")
    # Non-cuff sleeve and cuff overlap slightly, forming the fabric seam.
    def world_pixel(point):
        uv=world_to_camera_view(scene,scene.camera,point)
        return [round(uv.x*W,2),round((1.-uv.y)*H,2)]
    after_tip=rig.matrix_world @ rig.pose.bones["index_03_r"].tail
    return {"index":index,"tip_px":[tip_x,tip_y],
            "actual_bone_tip_px":world_pixel(after_tip),
            "actual_skin_tip_px":[round(measured_pad[0],2),round(measured_pad[1],2)],
            "skin_tip_power_distance_px":round(math.hypot(measured_pad[0]-POWER_X,measured_pad[1]-POWER_Y),2),
            "contact_skin_error_px":round(contact_error,2) if contact_error is not None else None,
            "actual_bone_wrist_px":world_pixel(wrist),
            "skeleton_reference_delta_px":math.hypot(tip_x-POWER_X,tip_y-POWER_Y),
            "cuff_wrist_distance_mm":round(seam_distance*1000,2),
            "nail_objects_present":len(nails),
            "nail_skin_metrics":nail_report,
            "press":round(press,3),
            "wrist_px":[wrist_x,wrist_y]}

def main():
    a=args();os.makedirs(a.output,exist_ok=True)
    rig,hand,sleeve,cuff,scene=create_scene(a)
    ids=[int(x) for x in a.only.split(",") if x.strip()] if a.only else list(range(a.frames))
    poses=[]
    for idx in ids:
        scene.frame_set(idx+1)
        record=pose_for_frame(rig,hand,sleeve,cuff,scene,idx,a.frames)
        scene.render.filepath=os.path.join(a.output,"HandsRight_%04d.png"%idx)
        bpy.ops.render.render(write_still=True)
        if not os.path.getsize(scene.render.filepath)>250:
            raise RuntimeError("Rendered PNG missing: "+scene.render.filepath)
        poses.append(record)
        print("ROKAS_FRAME_COMPLETE",idx,record,flush=True)
    scene.frame_start=1
    scene.frame_end=a.frames
    scene.frame_set(CONTACT_INDEX+1)
    assert rig.animation_data and rig.animation_data.action, "Rig animation keyframes not saved"
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(a.output,"ROKAS_RightHand_Editable.blend"))
    report={"renderer":"Blender "+bpy.app.version_string,"mesh_vertices":len(hand.data.vertices),
        "armature_bones":len(rig.pose.bones),"handedness":"right","frames":a.frames,
        "fps":FPS,"pixel_canvas":[W,H],"contact_index":CONTACT_INDEX,
        "target_power":[POWER_X,POWER_Y],"pose_reports":poses,
        "note":"V4 ART QA PENDING: skinned contact rig solve is a geometry gate, not a visual approval"}
    with open(os.path.join(a.output,"production_report.json"),"w",encoding="utf-8") as f:json.dump(report,f,indent=2)
    failed=[(p["index"],p["contact_skin_error_px"]) for p in poses
            if p["contact_skin_error_px"] is not None and p["contact_skin_error_px"]>4.]
    if failed:
        raise RuntimeError("V4 CONTACT QA FAIL (renders retained for diagnosis): "+str(failed))
    print("ROKAS_PRODUCTION_RENDER_PASS",len(poses),flush=True)
if __name__=="__main__":
    try:main()
    except Exception:traceback.print_exc();sys.exit(1)
