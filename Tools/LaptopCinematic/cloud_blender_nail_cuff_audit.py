#!/usr/bin/env python3
"""Non-release material-ID renders for pixel-accurate anatomical QA.
Opens existing Blender source, does not touch the original artwork or final PNG.
Outputs nail-only and cloth-only masks so an invisible/occluded nail is diagnosed.
"""
import argparse,os,sys,json
import bpy

def args():
    ar=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    p=argparse.ArgumentParser()
    p.add_argument("--input",required=True)
    p.add_argument("--output",required=True)
    p.add_argument("--frame",type=int,default=32)
    return p.parse_args(ar)

def audit():
    a=args()
    os.makedirs(a.output,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a.input))
    scene=bpy.context.scene
    scene.frame_set(a.frame)
    scene.render.resolution_x=960
    scene.render.resolution_y=540
    scene.render.resolution_percentage=100
    scene.render.engine="CYCLES"
    scene.cycles.samples=4
    scene.render.film_transparent=True
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    meshes=[o for o in scene.objects if o.type=="MESH"]
    nails=[o for o in meshes if o.name.startswith("ROKAS_AnatomicalNail_")]
    sleeves=[o for o in meshes if o.name.startswith(("RIGHT_FreeHomeSleeve","RIGHT_SleeveSoftCuff"))]
    hands=[o for o in meshes if "anatomical body" in o.name and len(o.data.vertices)>1000]
    if len(nails)!=5 or len(sleeves)!=2 or len(hands)!=1:
        raise RuntimeError("Unexpected geometry for right-hand material audit: "+str((len(nails),len(sleeves),len(hands))))
    stored={o.name:o.hide_render for o in meshes}
    mat=bpy.data.materials.new("TEMP_VisibleMaterialID")
    mat.use_nodes=True
    nt=mat.node_tree
    nt.nodes.clear()
    out=nt.nodes.new("ShaderNodeOutputMaterial")
    emission=nt.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value=(.0,1.,.0,1.)
    emission.inputs["Strength"].default_value=1
    nt.links.new(emission.outputs[0],out.inputs["Surface"])
    saved=[]
    for kind,visible in [("NAIL_ONLY",nails),("CUFF_ONLY",sleeves),("HAND_ONLY",hands)]:
        for o in meshes:o.hide_render=o not in visible
        previous=[]
        for o in visible:
            previous.append((o,list(o.data.materials)))
            o.data.materials.clear()
            o.data.materials.append(mat)
        scene.render.filepath=os.path.join(a.output,kind+"_frame%02d.png"%a.frame)
        bpy.ops.render.render(write_still=True)
        if os.stat(scene.render.filepath).st_size<256:raise RuntimeError("Empty mask "+kind)
        for o,materials in previous:
            o.data.materials.clear()
            for mm in materials:o.data.materials.append(mm)
    for o in meshes:o.hide_render=stored[o.name]
    outreport={"frame":a.frame,"nails":[o.name for o in nails],"sleeves":[o.name for o in sleeves],"hand":hands[0].name}
    with open(os.path.join(a.output,"nail_cuff_audit.json"),"w") as f:json.dump(outreport,f,indent=2)
    print("ROKAS_NAIL_CUFF_MATERIAL_AUDIT_PASS",outreport)

if __name__=="__main__":
    try:audit()
    except Exception:
        import traceback;traceback.print_exc();sys.exit(1)
