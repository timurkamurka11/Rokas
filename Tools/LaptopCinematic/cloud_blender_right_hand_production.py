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
    return Vector(((px/W-.5)*FRAME_LENGTH*W/H, (.5-py/H)*FRAME_LENGTH,0))

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
        bump.inputs["Strength"].default_value=.085
        bump.inputs["Distance"].default_value=.003
        nt.links.new(noise.outputs["Fac"],bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"],bsdf.inputs["Normal"])
    else:
        noise=nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value=185.
        noise.inputs["Detail"].default_value=2.
        bump=nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value=.035
        bump.inputs["Distance"].default_value=.00025
        nt.links.new(noise.outputs["Fac"],bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"],bsdf.inputs["Normal"])
    return m

def sleeve_geometry(mesh, wrist, cuff=False):
    # Four tapered cloth rings: skin cuff hugs the wrist; forearm fabric loosens
    # toward off-screen elbow. Add independent folds with low-frequency harmonics.
    axis=Vector((.56,-.83,0)).normalized()
    across=Vector((axis.y,-axis.x,0)).normalized()
    rings=([(0.,.029),(.048,.036),(.125,.057),(.23,.078),(.36,.094)]
           if not cuff else [(0.,.027),(.018,.028),(.038,.035)])
    N=32; verts=[]; faces=[]
    for ri,(distance,radius) in enumerate(rings):
        c=wrist + axis*distance
        for j in range(N):
            theta=2*math.pi*j/N
            fold=1+.052*math.sin(theta*7+ri*.8)+.026*math.cos(theta*11-ri*.7)
            d=radius*fold
            verts.append((c+across*math.cos(theta)*d+Vector((0,0,1))*math.sin(theta)*d*.63).to_tuple())
    for ri in range(len(rings)-1):
        for j in range(N):
            a=ri*N+j;b=ri*N+(j+1)%N
            faces.append((a,b,b+N,a+N))
    faces += [tuple(reversed(range(N))),tuple((len(rings)-1)*N+j for j in range(N))]
    mesh.clear_geometry()
    mesh.from_pydata(verts,[],faces);mesh.update()
    for p in mesh.polygons:p.use_smooth=True

def apply_subtle_finger_slimming(hand, rig):
    """Trim swollen-looking fingers 7%, preserving skin weights and joint lengths."""
    mesh=hand.data
    inv=hand.matrix_world.inverted()
    changed=0
    for digit in ("index","middle","ring","pinky","thumb"):
        for segment in ("01","02","03"):
            name=f"{digit}_{segment}_r"
            group=hand.vertex_groups.get(name)
            bone=rig.data.bones.get(name)
            if not group or not bone: continue
            root=inv @ (rig.matrix_world @ bone.head_local)
            end=inv @ (rig.matrix_world @ bone.tail_local)
            axis=end-root
            if axis.length < 1e-6: continue
            axis.normalize()
            for vertex in mesh.vertices:
                try: weight=group.weight(vertex.index)
                except RuntimeError: continue
                if weight < .2: continue
                parallel=axis * (vertex.co-root).dot(axis)
                radial=vertex.co-root-parallel
                vertex.co -= radial * .075 * min(1.,weight)
                changed+=1
    mesh.update()
    print("ROKAS_SLIM_FINGER_VERTICES",changed,flush=True)


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
                verts.append((xx*.007, yy*.010, .0019*outline))
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
    """Use actual posed skinned vertices, not potentially offset GLB bone tails."""
    dg=bpy.context.evaluated_depsgraph_get()
    eval_hand=hand.evaluated_get(dg)
    evaluated=eval_hand.to_mesh()
    try:
        mw=eval_hand.matrix_world
        for digit in ("index","middle","ring","pinky","thumb"):
            name=f"{digit}_03_r"
            group=hand.vertex_groups.get(name)
            bone=rig.pose.bones.get(name)
            obj=bpy.data.objects.get(f"ROKAS_AnatomicalNail_{digit}")
            if not group or not bone or not obj:continue
            source=[]
            for vertex in hand.data.vertices:
                try: w=group.weight(vertex.index)
                except RuntimeError:continue
                if w>.42 and vertex.index < len(evaluated.vertices):
                    source.append(mw @ evaluated.vertices[vertex.index].co)
            if len(source)<4:
                obj.hide_render=True
                continue
            obj.hide_render=False
            # Mesh vertices in this GLB are authoritative for distal anatomy.
            source.sort(key=lambda v:v.z,reverse=True)
            top=source[:max(5,len(source)//7)]
            avg=sum(top,Vector())/len(top)
            bone_head=rig.matrix_world @ bone.head
            bone_tail=rig.matrix_world @ bone.tail
            direction=bone_tail-bone_head
            obj.location=(avg.x,avg.y,max(v.z for v in top)+.001)
            obj.rotation_euler=(0.,0.,math.atan2(direction.y,direction.x)-math.pi*.5)
            width=.82 if digit=="pinky" else 1.05 if digit=="thumb" else 1.
            obj.scale=(width,1.,1.)
            obj.keyframe_insert(data_path="location",frame=frame)
            obj.keyframe_insert(data_path="rotation_euler",frame=frame)
            obj.keyframe_insert(data_path="scale",frame=frame)
    finally:
        eval_hand.to_mesh_clear()


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
    skin=mat("Skin_Cinematic_NaturalWarm",(.57,.355,.286),.66,.125)
    # Natural skin color breakup: low-frequency blotches and subtle joint redness.
    nodes=skin.node_tree.nodes
    links=skin.node_tree.links
    skin_bsdf=next(n for n in nodes if n.type=="BSDF_PRINCIPLED")
    pores=nodes.new("ShaderNodeTexNoise")
    pores.inputs["Scale"].default_value=36.
    pores.inputs["Detail"].default_value=3.
    palette=nodes.new("ShaderNodeValToRGB")
    palette.color_ramp.elements[0].position=.23
    palette.color_ramp.elements[0].color=(.29,.15,.12,1)
    palette.color_ramp.elements[1].position=.77
    palette.color_ramp.elements[1].color=(.55,.305,.235,1)
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

def pose_for_frame(rig,hand,sleeve_mesh,cuff_mesh,scene,index,total):
    t=index/FPS
    # First entry at bottom-right, confident anatomical motion toward upper-right
    # keyboard; last pose retracts without re-entering.
    reach=smooth(t/1.30)
    withdraw=smooth((t-1.40)/.55)
    press=smooth((t-1.27)/.06)*(1-smooth((t-1.40)/.07))
    # Visible skinned finger apex is ~95px to the right of the index bone reference.
    tip_x=1415.+(POWER_X-87.-1415.)*reach + 135.*withdraw
    # The glTF index-bone tail differs from the visible skinned fingertip by about +84px vertically.
    # Calibrated against actual alpha pixels in the user-approved 1672x941 POV.
    tip_y=1080.+(POWER_Y-62.-1080.)*reach + 175.*withdraw + 3.*press
    # Effortless low-frequency breathing; suppress during physical contact.
    tip_x+=2.2*math.sin(3.5*t)*(1-press)*smooth(t/.4)
    tip_y+=1.3*math.sin(3.2*t+1.2)*(1-press)*smooth(t/.4)
    wrist_x=tip_x+100.
    wrist_y=tip_y+68.
    # Wrist tucking rotates the actual glTF armature and leaves the finger mesh skinned.
    base=rig.pose.bones["hand_r"]
    direction=rig.data.bones["index_03_r"].tail_local-rig.data.bones["hand_r"].head_local
    desired=camera_xy(tip_x,tip_y)-camera_xy(wrist_x,wrist_y)
    rot=math.atan2(desired.y,desired.x)-math.atan2(direction.y,direction.x)
    rig.rotation_mode="XYZ";rig.rotation_euler=(0.03,0.0,rot)
    length=max(.001,Vector((direction.x,direction.y)).length)
    rig.scale=Vector((desired.length/length,)*3)
    # The mesh may have its own parent transform; Blender's skin modifier resolves its rig.
    for bone in rig.pose.bones:
        if bone.name.startswith("index_"):
            bone.rotation_euler=(0,0,(-.11 if "_01_" in bone.name else -.22)*press)
        elif bone.name.startswith(("middle_","ring_","pinky_")):
            bone.rotation_euler=(.46*reach,0, .50 + .30*reach)
        elif bone.name.startswith("thumb_"):
            bone.rotation_euler=(0,0,.055)
        else:
            bone.rotation_euler=(0,0,0)
    rig.location=(0,0,0)
    bpy.context.view_layer.update()
    tip_now=rig.matrix_world @ rig.pose.bones["index_03_r"].tail
    wanted=camera_xy(tip_x,tip_y)
    rig.location.x += wanted.x-tip_now.x
    rig.location.y += wanted.y-tip_now.y
    bpy.context.view_layer.update()
    wrist=rig.matrix_world @ base.head
    update_nail_positions(hand,rig,index+1)
    # Locally modeled cloth follows the wrist with real keyframed transforms.
    sleeve_obj=bpy.data.objects["RIGHT_FreeHomeSleeve"]
    cuff_obj=bpy.data.objects["RIGHT_SleeveSoftCuff"]
    sleeve_obj.location=(wrist.x, wrist.y, 1.10)
    cuff_obj.location=(wrist.x, wrist.y, 1.11)
    cuff_obj.scale=(.98,.98,.98)
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
    # Non-cuff sleeve and cuff overlap slightly, forming the fabric seam.
    def world_pixel(point):
        return [round((point.x/(FRAME_LENGTH*W/H)+.5)*W,2), round((.5-point.y/FRAME_LENGTH)*H,2)]
    after_tip=rig.matrix_world @ rig.pose.bones["index_03_r"].tail
    return {"index":index,"tip_px":[tip_x,tip_y],
            "actual_bone_tip_px":world_pixel(after_tip),
            "actual_bone_wrist_px":world_pixel(wrist),
            "skeleton_reference_delta_px":math.hypot(tip_x-POWER_X,tip_y-POWER_Y),
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
        "note":"PRODUCTION PASS 1 - must be visually approved in real Unity"}
    with open(os.path.join(a.output,"production_report.json"),"w",encoding="utf-8") as f:json.dump(report,f,indent=2)
    print("ROKAS_PRODUCTION_RENDER_PASS",len(poses),flush=True)
if __name__=="__main__":
    try:main()
    except Exception:traceback.print_exc();sys.exit(1)
