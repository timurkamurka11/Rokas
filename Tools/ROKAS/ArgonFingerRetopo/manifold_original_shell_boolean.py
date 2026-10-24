#!/usr/bin/env python3
"""System-Python Manifold CSG of Blender-extracted REAL Argon shell triangles.
No voxel remeshing or generated primitive; data are six genuine FBX islands.
Never overwrites FBX source or the game V10. Returns only QA candidate arrays.
"""
import sys,json,pathlib
import numpy as np
import manifold3d as mfd

def fail(msg):raise RuntimeError("ROKAS MANIFOLD SOURCE CSG: "+msg)
def main():
    if len(sys.argv)!=2:fail("Expected per-shell work directory")
    out=pathlib.Path(sys.argv[1]).resolve()
    report={"inputs":[],"unions":[],"original_source_only":True}
    shells=[]
    for k in range(6):
        path=out/("source_closed_shell_%02d.npz"%k)
        if not path.is_file():fail("Missing authentic shell "+str(k))
        data=np.load(path)
        vertices=np.asarray(data["vertices"],dtype=np.float32)
        triangles=np.asarray(data["triangles"],dtype=np.uint32)
        if len(vertices)<300 or len(triangles)<300:fail("Non-original tiny mesh detected")
        mesh=mfd.Mesh(vertices,triangles)
        part=mfd.Manifold(mesh)
        if part.is_empty():fail("Original sealed source island not recognized as a solid: "+str(k))
        report["inputs"].append({
            "island":k,"vertices":len(vertices),"triangles":len(triangles),
            "status":str(part.status()),"volume":float(part.volume())})
        shells.append(part)
    result=shells[0]
    for i,part in enumerate(shells[1:],1):
        result=result+part
        if result.is_empty():fail("Source CSG unexpectedly produced empty shell "+str(i))
        mesh=result.to_mesh()
        report["unions"].append({
            "source_digit":i,"vertices":len(mesh.vert_properties),
            "triangles":len(mesh.tri_verts),
            "status":str(result.status()),"volume":float(result.volume())})
        if len(mesh.vert_properties)>250000:fail("CSG vertex explosion")
    final=result.to_mesh()
    np.savez_compressed(out/"authentic_union_combined.npz",
                        vertices=np.asarray(final.vert_properties[:,:3],dtype=np.float32),
                        triangles=np.asarray(final.tri_verts,dtype=np.int32))
    (out/"manifold_union_report.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    print("ROKAS MANIFOLD ORIGINAL EXACT CSG",json.dumps(report),flush=True)
if __name__=="__main__":main()
