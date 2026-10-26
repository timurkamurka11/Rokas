"""
Blender read-only authoring topology visualization for ROKAS.
Exports true projected vertex/triangle SVG wireframes, independently colored
connected skin islands and highlighted exposed edges. NO mesh replacement.
Run after audit_argon_hand_anatomy.py using the SAME original FBX.
"""
import bpy
import html
import pathlib
import sys
from collections import defaultdict
from mathutils import Vector

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from audit_argon_hand_anatomy import connected_components, boundary_edges

WIDTH, HEIGHT=1480,860
COLORS=["#b98572","#c9a083","#f06e82","#a8b6dd","#a4bea3","#44c7c7"]

def frame_view(mesh, skin, pieces, exposed, arm, outdir, name, region, look_from_above=True):
    xmin,xmax,ymin,ymax=region
    margin=54
    scale=min((WIDTH-2*margin)/(xmax-xmin),
              (HEIGHT-2*margin)/(ymax-ymin))
    shiftx=(WIDTH-(xmax-xmin)*scale)/2
    shifty=(HEIGHT-(ymax-ymin)*scale)/2
    def project(v):
        return (shiftx+(v.x-xmin)*scale,
                shifty+(ymax-v.y)*scale)
    coords=[skin.matrix_world@v.co for v in mesh.vertices]
    which=[-1]*len(coords)
    for idx,component in enumerate(pieces):
        for v in component:
            which[v]=idx
    triangles=[]
    for poly in mesh.polygons:
        pts=[coords[v] for v in poly.vertices]
        if not any(xmin<=p.x<=xmax and ymin<=p.y<=ymax for p in pts):
            continue
        comp=which[poly.vertices[0]]
        depth=sum(p.z for p in pts)/len(pts)
        positions=" ".join(f"{x:.1f},{y:.1f}" for x,y in map(project,pts))
        triangles.append((depth,comp,positions))
    triangles.sort(key=lambda v:v[0],reverse=not look_from_above)
    out=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" viewBox="0 0 {WIDTH} {HEIGHT}">',
         '<rect width="100%" height="100%" fill="#121a27"/>',
         '<text x="42" y="46" font-family="Arial" font-size="30" font-weight="bold" fill="#f6f6f6">'+
         html.escape(name)+" (authentic Blender FBX)</text>",
         '<text x="42" y="80" font-family="Arial" font-size="17" fill="#afc4d2">Index and little finger: source mesh topology / NOT a repaired model</text>',
         '<g stroke="#233543" stroke-width=".33" stroke-linejoin="round">']
    for depth,comp,poly in triangles:
        color=COLORS[comp%len(COLORS)]
        out.append(f'<polygon points="{poly}" fill="{color}" fill-opacity=".66"/>')
    out.append('</g>')
    out.append('<g fill="none" stroke-linecap="round">')
    for a,b in exposed:
        aa=coords[a]; bb=coords[b]
        if not((xmin<=aa.x<=xmax and ymin<=aa.y<=ymax) or
               (xmin<=bb.x<=xmax and ymin<=bb.y<=ymax)):
            continue
        x1,y1=project(aa); x2,y2=project(bb)
        color="#ff4d7d" if which[a]==0 else "#27e5ff"
        out.append(f'<path d="M {x1:.1f} {y1:.1f} L {x2:.1f} {y2:.1f}" stroke="{color}" stroke-width="2.6"/>')
    out.append('</g>')
    # Draw anatomical joint anchors from the *real imported* skeleton.
    for bone in ("Arm_Wrist","Index_MCP","Index_PIP","Index_DIP",
                 "Little_MCP","Little_PIP","Little_DIP"):
        pb=arm.pose.bones.get(bone)
        if pb is None:continue
        pos=arm.matrix_world@pb.head
        if not(xmin<=pos.x<=xmax and ymin<=pos.y<=ymax):continue
        x,y=project(pos)
        out.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="5.2" fill="#fff58f" stroke="#222" stroke-width="1.6"/>')
        out.append(f'<text x="{x+9:.1f}" y="{y-9:.1f}" font-size="15" font-family="Arial" fill="#fff2c2">'+html.escape(bone)+"</text>")
    out.append('<rect x="34" y="792" width="1020" height="48" rx="12" fill="#263343" opacity=".96"/>')
    out.append('<text x="52" y="824" font-family="Arial" font-size="17" fill="#f2bac7">Pink edges = exposed palm boundary</text>')
    out.append('<text x="478" y="824" font-family="Arial" font-size="17" fill="#99f5fb">Cyan edges = exposed detached finger boundary</text>')
    out.append("</svg>")
    output=outdir/(name.lower().replace(" ","_")+".svg")
    output.write_text("\n".join(out),encoding="utf-8")
    print("[ROKAS ANATOMY WIREFRAME] "+str(output)+" triangles="+str(len(triangles)),flush=True)

def main():
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(args)!=2:
        raise RuntimeError("Usage: -- originalArm.fbx outputDir")
    source=pathlib.Path(args[0]).resolve()
    out=pathlib.Path(args[1]).resolve()
    out.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source),use_anim=True)
    skin=next((o for o in bpy.data.objects
               if o.type=="MESH" and o.name=="Keiko_Hand_Skin_Mesh"),None)
    if skin is None: raise RuntimeError("Authentic skin missing")
    arm=next((m.object for m in skin.modifiers if m.type=="ARMATURE"),None)
    if arm is None:raise RuntimeError("Authentic armature missing")
    mesh=skin.data
    pieces=connected_components(mesh)
    if len(pieces)!=6:raise RuntimeError("Unexpected hand component topology")
    exposed,_=boundary_edges(mesh)
    frame_view(mesh,skin,pieces,exposed,arm,out,"Full hand topology",
               (-.280,.150,-.078,.060))
    frame_view(mesh,skin,pieces,exposed,arm,out,"Index base wireframe",
               (.005,.145,-.058,.006))
    frame_view(mesh,skin,pieces,exposed,arm,out,"Little base wireframe",
               (.005,.130,.002,.053))
    print("[ROKAS ANATOMY WIREFRAME] Original FBX still unchanged",flush=True)

if __name__=="__main__":
    main()
