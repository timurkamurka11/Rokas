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
    palette.color_ramp.elements[0].color=(.42,.215,.17,1)
    palette.color_ramp.elements[1].position=.77
    palette.color_ramp.elements[1].color=(.71,.45,.37,1)
    links.new(pores.outputs["Fac"],palette.inputs["Fac"])
    links.new(palette.outputs["Color"],skin_bsdf.inputs["Base Color"])
    hand.data.materials.clear();hand.data.materials.append(skin)
    # Force mildly animated real finger joints, no frame-to-frame morph cards.
    for bone in rig.pose.bones:
        bone.rotation_mode="XYZ"
    fabric=mat("Graphite_HomeSweatshirt_Cotton",(.062,.071,.080),.86,0,True)
    sleeve_mesh=bpy.data.meshes.new("LooseSleeve_Creased_Tube")
    sleeve_obj=bpy.data.objects.new("RIGHT_FreeHomeSleeve",sleeve_mesh)
    bpy.context.collection.objects.link(sleeve_obj)
    sleeve_mesh.materials.append(fabric)
    cuff_mesh=bpy.data.meshes.new("RibbedCuff")
    cuff=bpy.data.objects.new("RIGHT_SleeveSoftCuff",cuff_mesh)
    bpy.context.collection.objects.link(cuff)
    cuff_mat=mat("Cuff_Graphite",(.045,.048,.057),.90,0,True)
    cuff_mesh.materials.append(cuff_mat)
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
    tip_x=1415.+(POWER_X-40.-1415.)*reach + 135.*withdraw
    tip_y=1080.+(POWER_Y-35.-1080.)*reach + 175.*withdraw + 3.*press
    # Effortless low-frequency breathing; suppress during physical contact.
    tip_x+=2.2*math.sin(3.5*t)*(1-press)*smooth(t/.4)
    tip_y+=1.3*math.sin(3.2*t+1.2)*(1-press)*smooth(t/.4)
    wrist_x=tip_x+140.
    wrist_y=tip_y+95.
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
    # Cloth geometry has real tube cross section, nonuniform diameter and folds.
    sleeve_geometry(sleeve_mesh,wrist + Vector((0,0,.45-wrist.z)))
    # Narrow rib-knit cuff at hand–sleeve junction, not a flat straight sleeve cap.
    # Make only first two rings from the same tapered profile for smoother join.
    sleeve_geometry(cuff_mesh,wrist + Vector((0,0,.46-wrist.z)), cuff=True)
    # Mesh.clear_geometry() may drop slots in Blender 4.5; rebind the actual
    # dark sweatshirt and dark ribbed cuff every frame, never grey default.
    sleeve_mesh.materials.clear()
    sleeve_mesh.materials.append(bpy.data.materials["Graphite_HomeSweatshirt_Cotton"])
    cuff_mesh.materials.clear()
    cuff_mesh.materials.append(bpy.data.materials["Cuff_Graphite"])
    cf_obj=bpy.data.objects["RIGHT_SleeveSoftCuff"]
    cf_obj.scale=(.98,.98,.98)
    # Non-cuff sleeve and cuff overlap slightly, which creates a fabric seam.
    def world_pixel(point):
        return [round((point.x/(FRAME_LENGTH*W/H)+.5)*W,2), round((.5-point.y/FRAME_LENGTH)*H,2)]
    after_tip=rig.matrix_world @ rig.pose.bones["index_03_r"].tail
    return {"index":index,"tip_px":[tip_x,tip_y],
            "actual_bone_tip_px":world_pixel(after_tip),
            "actual_bone_wrist_px":world_pixel(wrist),
            "contact_delta_px":math.hypot(tip_x-POWER_X,tip_y-POWER_Y),
            "press":round(press,3),
            "wrist_px":[wrist_x,wrist_y]}

def main():
    a=args();os.makedirs(a.output,exist_ok=True)
    rig,hand,sleeve,cuff,scene=create_scene(a)
    ids=[int(x) for x in a.only.split(",") if x.strip()] if a.only else list(range(a.frames))
    poses=[]
    for idx in ids:
        record=pose_for_frame(rig,hand,sleeve,cuff,scene,idx,a.frames)
        scene.frame_set(idx+1)
        if idx==CONTACT_INDEX:
            bpy.ops.wm.save_as_mainfile(filepath=os.path.join(a.output,"ROKAS_RightHand_Editable.blend"))
        scene.render.filepath=os.path.join(a.output,"HandsRight_%04d.png"%idx)
        bpy.ops.render.render(write_still=True)
        if not os.path.getsize(scene.render.filepath)>250:
            raise RuntimeError("Rendered PNG missing: "+scene.render.filepath)
        poses.append(record)
        print("ROKAS_FRAME_COMPLETE",idx,record,flush=True)
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
