"""Read the ACTUAL exported V17 FBX (not the in-memory Blender candidate).

Generate proof from projected real skinned triangles. Unlike a Cycles render,
this has no dependence on a video device, shader, lights or world rendering.

Proves that animations survive export and that vertices actually move.
Does NOT assert beautiful anatomy: real closeup visual QA remains mandatory.

blender -b -noaudio --python-exit-code 2 --python verify_argon_v17_fbx_pose.py
        -- candidate.fbx proof-folder
"""
import bpy, json, math, pathlib, sys
from collections import defaultdict
from mathutils import Vector

FINGERS=("Thumb","Index","Middle","Ring","Little")
NEEDED=("V17_Grip",
        "V17_Index",
        "V17_Living")

def check(ok,reason):
    if not ok:raise RuntimeError("[ROKAS V17 FBX ROUNDTRIP] "+reason)

def snapshot(scene,skin,frame,action,shape_action=None):
    arm=next((m.object for m in skin.modifiers if m.type=="ARMATURE"),None)
    check(arm is not None,"Skin lost Armature modifier during FBX export")
    arm.animation_data_create()
    arm.animation_data.action=action
    if shape_action is not None:
        require_keys=skin.data.shape_keys
        check(require_keys is not None,"FBX skin lost its corrective shapes")
        require_keys.animation_data_create().action=shape_action
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    print("[ROKAS V17 RIG DEBUG] frame",frame,
          "action",getattr(arm.animation_data.action,"name","NONE"),
          "rotations",{
             n:tuple(round(float(x),5) for x in arm.pose.bones[n].rotation_quaternion)
             for n in ("Index_MCP","Index_PIP","Index_DIP","Little_PIP")
             if n in arm.pose.bones
          },
          "modifier_flags",[(m.name,m.show_viewport,m.show_render)
                            for m in skin.modifiers if m.type=="ARMATURE"],
          flush=True)
    dep=bpy.context.evaluated_depsgraph_get()
    evaluated=skin.evaluated_get(dep)
    mesh=evaluated.to_mesh(preserve_all_data_layers=False,depsgraph=dep)
    check(mesh is not None,"Cannot obtain evaluated skinned candidate mesh")
    try:
        verts=[skin.matrix_world@v.co for v in mesh.vertices]
        tris=[tuple(t.vertices) for t in mesh.loop_triangles]
        if not tris:
            mesh.calc_loop_triangles()
            tris=[tuple(t.vertices) for t in mesh.loop_triangles]
    finally:
        evaluated.to_mesh_clear()
    return verts,tris

def draw_svg(vertices,triangles,dst,title,view=None,highlights=None):
    # Orthographic real-world XY projection of surface triangles.
    # Actual depth is used for painter ordering; this deliberately avoids
    # any GPU-render path which previously generated a fully BLACK image.
    W,H=1400,850
    allx=[p.x for p in vertices];ally=[p.y for p in vertices]
    if view is None:
        xmin,xmax=min(allx),max(allx)
        ymin,ymax=min(ally),max(ally)
        dx=xmax-xmin; dy=ymax-ymin
        xmin-=dx*.04;xmax+=dx*.04
        ymin-=dy*.22;ymax+=dy*.22
    else:
        xmin,xmax,ymin,ymax=view
    dx=xmax-xmin;dy=ymax-ymin
    check(dx>.0005 and dy>.0005,"Geometry projection bounds collapsed")
    margin=60
    scale=min((W-margin*2)/dx,(H-margin*2)/dy)
    offs_x=(W-dx*scale)/2;offs_y=(H-dy*scale)/2
    def xy(v):
        return (offs_x+(v.x-xmin)*scale,
                offs_y+(ymax-v.y)*scale)
    faces=[]
    for tri in triangles:
        v0,v1,v2=(vertices[i] for i in tri)
        if max(v0.x,v1.x,v2.x)<xmin or min(v0.x,v1.x,v2.x)>xmax:
            continue
        if max(v0.y,v1.y,v2.y)<ymin or min(v0.y,v1.y,v2.y)>ymax:
            continue
        n=(v1-v0).cross(v2-v0)
        if n.length_squared<1e-14:continue
        n.normalize()
        # Lambertian visible shading with fixed virtual light.
        shading=max(.13,min(.98,.26+.58*max(0,n.z)+.19*max(0,n.y)))
        rgb=(int(225*shading),int(163*shading),int(139*shading))
        depth=(v0.z+v1.z+v2.z)/3
        pts=" ".join("%.2f,%.2f"%xy(p) for p in (v0,v1,v2))
        faces.append((depth,pts,rgb))
    check(len(faces)>500,"Fewer than 500 projected triangles: FBX model missing/empty")
    faces.sort(key=lambda p:p[0])
    parts=[
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
        '<rect width="100%" height="100%" fill="#0c111b"/>',
        '<g stroke="none">']
    for _,pts,(r,g,b) in faces:
        parts.append(f'<polygon points="{pts}" fill="rgb({r},{g},{b})"/>')
    parts.append("</g>")
    if highlights:
        for name,p in highlights.items():
            if xmin<p.x<xmax and ymin<p.y<ymax:
                x,y=xy(p)
                parts.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="4" fill="#77e7ff"/>')
                parts.append(f'<text x="{x+8:.1f}" y="{y-6:.1f}" font-family="sans-serif" font-size="14" fill="#b5edff">{name}</text>')
    parts.extend([
        '<rect x="20" y="16" width="1360" height="74" rx="10" fill="#152539" opacity=".93"/>',
        f'<text x="42" y="51" font-family="sans-serif" font-size="27" font-weight="bold" fill="white">{title}</text>',
        '<text x="42" y="73" font-family="sans-serif" font-size="16" fill="#a1bfd4">Authentic V17 FBX re-import — actual evaluated skinned mesh; not an artist preview</text>',
        '</svg>'
    ])
    dst.write_text("\n".join(parts),encoding="utf-8")
    return len(faces)

def get_bones(arm):
    return {p.name:arm.matrix_world@p.head for p in arm.pose.bones}

def main():
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    check(len(args)==2,"Usage: -- exported-v17.fbx proof-output-directory")
    src,out=pathlib.Path(args[0]).resolve(),pathlib.Path(args[1]).resolve()
    check(src.is_file(),"Candidate FBX was not exported")
    out.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(src),use_anim=True)
    skin=next((o for o in bpy.data.objects
               if o.type=="MESH" and o.name=="Keiko_Hand_Skin_Mesh"),None)
    check(skin is not None,"Re-import changed expected skin name")
    arm=next((m.object for m in skin.modifiers if m.type=="ARMATURE"),None)
    check(arm is not None,"Re-import lost skeleton")
    available=[a.name for a in bpy.data.actions]
    # Blender importer can create BOTH skeletal pose and corrective
    # shape-key actions containing identical "V17_Grip" names.
    # Only a genuine POSE action can drive armature bones.
    skeletal=[a for a in bpy.data.actions
              if any(fc.data_path.startswith('pose.bones[') for fc in a.fcurves)]
    print("[ROKAS V17 RIG DEBUG] available_skeleton_actions",
          [a.name for a in skeletal],flush=True)
    missing=[p for p in NEEDED if not any(p in a.name for a in skeletal)]
    check(not missing,"Imported FBX missing SKELETAL animation actions: "+
          str(missing)+" available="+repr([a.name for a in skeletal]))
    actions={name:next(a for a in skeletal if name in a.name)
             for name in NEEDED}
    for name,ac in actions.items():
        print("[ROKAS V17 RIG DEBUG] imported",name,
              "fcurves",len(ac.fcurves),
              "samples",[(fc.data_path,fc.array_index,len(fc.keyframe_points))
                         for fc in list(ac.fcurves)[:5]],flush=True)
    shape_actions=[a for a in bpy.data.actions if
        any(fc.data_path.startswith('key_blocks[') for fc in a.fcurves)]
    print("[ROKAS V17 CORRECTIVE DEBUG] imported",
          [(a.name,len(a.fcurves),
            [(c.data_path,len(c.keyframe_points))
             for c in a.fcurves[:2]]) for a in shape_actions[-10:]],flush=True)
    corrective_actions={}
    for name in NEEDED:
        # Blender FBX names the shape-key action after the source *skeletal*
        # clip, NOT necessarily after its standalone *_Corrective data-block.
        matches=[a for a in shape_actions if name in a.name]
        check(matches,"Exported V17 missing animated corrective shape action: "+name)
        correction=matches[0]
        check(len(correction.fcurves)>=8,
              "Incomplete animated knuckle correction: "+correction.name)
        corrective_actions[name]=correction

    action=actions[NEEDED[0]]
    scene=bpy.context.scene
    corrected=corrective_actions[NEEDED[0]]
    neutral,tris=snapshot(scene,skin,1,action,corrected)
    half,_=snapshot(scene,skin,30,action,corrected)
    closed,_=snapshot(scene,skin,51,action,corrected)
    shape_at_grip={name:float(skin.data.shape_keys.key_blocks[name].value)
                   for name in ("Index_MCP_Corrective","Little_MCP_Corrective",
                                "Fist_Grip_Corrective")}
    released,_=snapshot(scene,skin,96,action,corrected)
    check(all(v>.15 for v in shape_at_grip.values()),
          "Corrective keys are present but are not animated in the grip: "+
          repr(shape_at_grip))
    check(len(neutral)==len(half)==len(closed)==len(released),
          "Skin topology changed between animation frames")
    bones=get_bones(arm)
    names=[n for n in bones if any(n.startswith(f+"_") for f in FINGERS)]
    # Use strong own-finger weights so adjacent palm motion cannot masquerade
    # as finger movement. Report per-digit magnitude and moved vertex count.
    weight_indices={g.index:g.name for g in skin.vertex_groups}
    owned={f:[] for f in FINGERS}
    for v in skin.data.vertices:
        candidates=[]
        for w in v.groups:
            name=weight_indices.get(w.group,"")
            for finger in FINGERS:
                if name.startswith(finger+"_"):
                    candidates.append((w.weight,finger))
        if candidates:
            strength,fn=max(candidates)
            if strength>.55:owned[fn].append(v.index)
    deltas={}
    for fn,indices in owned.items():
        check(len(indices)>30,"No meaningful skinned vertices belong to "+fn)
        displacement=[(closed[i]-neutral[i]).length for i in indices]
        deltas[fn]={
            "owned_vertices":len(indices),
            "max_m":round(max(displacement),6),
            "mean_m":round(sum(displacement)/len(displacement),6),
            "moved_gt_1mm":sum(v>.001 for v in displacement),
            "moved_gt_3mm":sum(v>.003 for v in displacement)
        }
    # Also prove V17_Index is genuinely independent: secondary fingers
    # should not bend when only the index gesture plays.
    index_action=actions["V17_Index"]
    index_corrective=corrective_actions["V17_Index"]
    index_rest,_=snapshot(scene,skin,1,index_action,index_corrective)
    index_flex,_=snapshot(scene,skin,43,index_action,index_corrective)
    isolated={}
    for fn,indices in owned.items():
        changes=[(index_flex[i]-index_rest[i]).length for i in indices]
        isolated[fn]={"max_m":round(max(changes),6),
                      "mean_m":round(sum(changes)/len(changes),6)}
    check(isolated["Index"]["max_m"]>.003,
          "Independent index clip does not animate index skin")
    check(all(isolated[n]["mean_m"]<.002
              for n in ("Thumb","Middle","Ring","Little")),
          "Index-only clip inappropriately animates other fingers: "+
          repr(isolated))
    # Side note: the neutral and fist pose may be impacted by authored
    # hinge orientation. Fail if nothing physically moves; do not infer
    # visually acceptable fists from this metric.
    check(all(v["max_m"]>.0015 for v in deltas.values()),
          "One or more fingers remain functionally rigid: "+repr(deltas))
    key_names=(list(skin.data.shape_keys.key_blocks.keys())
               if skin.data.shape_keys else [])
    check(len(key_names)>=6,"Most corrective shapes lost in FBX round-trip: "+
          repr(key_names))
    files={}
    files["neutral"]=draw_svg(neutral,tris,out/"V17_FBX_Neutral.svg",
                              "V17 re-import: neutral hand",highlights=bones)
    files["half_fist"]=draw_svg(half,tris,out/"V17_FBX_Half_Grip.svg",
                              "V17 re-import: half grip (frame 30)",highlights=bones)
    files["full_fist"]=draw_svg(closed,tris,out/"V17_FBX_Full_Grip.svg",
                              "V17 re-import: full grip (frame 51)",highlights=bones)
    files["release"]=draw_svg(released,tris,out/"V17_FBX_Release.svg",
                              "V17 re-import: released hand (frame 96)",highlights=bones)
    files["index_flex"]=draw_svg(index_flex,tris,
                              out/"V17_FBX_IndexOnly.svg",
                              "V17 re-import: INDEX finger only",highlights=bones)
    # Side profile is essential: a top-down "fist" can look like short
    # fingers even when the joints are correctly folding into palm depth.
    # Reproject genuine deformed mesh as XZ without authoring fake poses.
    xz=lambda vertices:[Vector((v.x,v.z,v.y)) for v in vertices]
    bone_xz={name:Vector((p.x,p.z,p.y)) for name,p in bones.items()}
    files["profile_neutral"]=draw_svg(
        xz(neutral),tris,out/"V17_FBX_Profile_Neutral.svg",
        "V17 re-import: side profile / open hand",highlights=bone_xz)
    files["profile_fist"]=draw_svg(
        xz(closed),tris,out/"V17_FBX_Profile_FullGrip.svg",
        "V17 re-import: side profile / fist",highlights=bone_xz)
    for target in ("Index_MCP","Little_MCP"):
        joint=bones.get(target)
        if joint is None:continue
        radius=.055
        view=(joint.x-radius,joint.x+radius,joint.y-.033,joint.y+.033)
        files[target+"_neutral"]=draw_svg(neutral,tris,
            out/("V17_"+target+"_Neutral.svg"),
            "V17 "+target+" — seam closeup / neutral",
            view=view,highlights=bones)
        files[target+"_grip"]=draw_svg(closed,tris,
            out/("V17_"+target+"_Grip.svg"),
            "V17 "+target+" — seam closeup / grip",
            view=view,highlights=bones)
    report={
        "fbx":src.name,
        "skin_vertices":len(skin.data.vertices),
        "reimport_actions":available,
        "corrective_shape_names":key_names,
        "real_finger_skin_motion":deltas,
        "index_only_motion":isolated,
        "corrective_shape_values_at_grip":shape_at_grip,
        "imported_corrective_actions":[a.name for a in corrective_actions.values()],
        "projected_face_counts":files,
        "result":"FBX round-trip geometry/rig motion proof. NOT an in-game Unity QA."
    }
    (out/"ROKAS_V17_FBX_Roundtrip_Motion.json").write_text(
        json.dumps(report,indent=2,ensure_ascii=False),encoding="utf-8")
    print("[ROKAS V17 FBX ROUNDTRIP] "+json.dumps(report),flush=True)

if __name__=="__main__":main()
