#!/usr/bin/env python3
"""Read-only true-source FBX finger surface topology and geometric pairing atlas.

Inspects every original boundary edge, long-axis cross section, and actual
palm-contact distances. DOES NOT generate fake caps/bridges or mutate source.
Used to design a bounded realistic skin re-topology rather than inflate QA.
"""
import bpy, bmesh, json, pathlib, sys, math
import numpy as np
from collections import defaultdict

def must(ok,why):
    if not ok: raise RuntimeError("ROKAS SURFACE ANALYSIS: "+why)

def components_of(bm):
    seen=set(); out=[]
    for v in bm.verts:
        if v in seen:continue
        todo=[v]; group=set()
        while todo:
            w=todo.pop()
            if w in seen:continue
            seen.add(w);group.add(w)
            todo.extend(x for e in w.link_edges for x in e.verts if x not in seen)
        out.append(group)
    return sorted(out,key=len,reverse=True)

def boundary_ring(group):
    edges={e for v in group for e in v.link_edges if e.is_boundary}
    adj=defaultdict(list)
    for e in edges:
        a,b=e.verts;adj[a].append(b);adj[b].append(a)
    must(len(adj)>20 and all(len(n)==2 for n in adj.values()),
         "source component boundary must be exactly one closed loop")
    start=min(adj,key=lambda v:v.index); ring=[];previous=None;current=start
    for _ in range(len(adj)+2):
        ring.append(current)
        nxt=next(x for x in adj[current] if x!=previous)
        if nxt==start:break
        previous,current=current,nxt
    must(len(ring)==len(adj),"more than one ring")
    return ring

def quantile(a,q):
    return float(np.quantile(np.asarray(a),q)) if a else 0.

def main():
    argv=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    must(len(argv)==2,"SOURCE.fbx OUTPUT_DIRECTORY")
    out=pathlib.Path(argv[1]).resolve();out.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(pathlib.Path(argv[0]).resolve()),use_anim=False)
    src=[o for o in bpy.data.objects if o.type=="MESH" and
         "Keiko_Hand_Skin_Mesh" in o.name]
    must(len(src)==1,"original hand mesh not uniquely found")
    src=src[0]
    rig=src.find_armature();must(rig is not None and len(rig.data.bones)>=25,
                               "authentic 25 bones not found")
    bm=bmesh.new();bm.from_mesh(src.data)
    bm.verts.ensure_lookup_table();bm.verts.index_update()
    comps=components_of(bm);must(len(comps)==6,"expect palm + exactly five fingers")
    rings=[boundary_ring(g) for g in comps]
    xyz=np.array([v.co[:] for v in bm.verts],dtype=np.float64)
    dim=xyz.max(axis=0)-xyz.min(axis=0)
    extent=float(max(dim));must(extent>0,"zero size")
    palm_xyz=np.array([v.co[:] for v in rings[0]])
    report={"source":"original FBX only","original_bones":len(rig.data.bones),
            "source_vertices":len(bm.verts),"source_faces":len(bm.faces),
            "hand_extent":extent,"boundary_edges":sum(e.is_boundary for e in bm.edges),
            "islands":[]}
    for index,(part,ring) in enumerate(zip(comps,rings)):
        ids=[v.index for v in ring]
        arr=np.array([v.co[:] for v in ring],dtype=np.float64)
        interior=np.array([v.co[:] for v in part])
        center=interior.mean(axis=0)
        centered=interior-center
        evals,evecs=np.linalg.eigh(centered.T@centered)
        longitudinal=evecs[:,np.argmax(evals)]
        projections=(arr-center)@longitudinal
        span=float(np.ptp(projections))
        # Measure separation between edges that are distant ALONG the ring,
        # but close in 3D, to identify split open tubular underside rails.
        pairs=[]
        n=len(ring)
        for i in range(n):
            other=[]
            for j in range(n):
                cyc=min(abs(i-j),n-abs(i-j))
                if cyc<max(6,n//12): continue
                separation=float(np.linalg.norm(arr[i]-arr[j]))
                other.append((separation,j))
            if other:
                d,j=min(other)
                pairs.append((i,j,d,abs(float(projections[i]-projections[j]))))
        palm_d=[]
        for a in arr:
            palm_d.append(float(np.min(np.linalg.norm(palm_xyz-a,axis=1))))
        hist=[]
        for k in range(10):
            lo=np.min(projections)+span*k/10
            hi=np.min(projections)+span*(k+1)/10
            idx=np.flatnonzero((projections>=lo)&(projections<(hi if k<9 else hi+1e-9)))
            samples=arr[idx]
            cross=0.
            if len(samples)>1:
                cross=float(np.max(np.linalg.norm(samples[:,None,:]-samples[None,:,:],axis=2)))
            hist.append({"bin":k,"count":int(len(idx)),
                         "diameter_over_hand_extent":round(cross/extent,5),
                         "palm_nearest_median_over_extent":
                             round(float(np.median(np.asarray(palm_d)[idx]))/extent,5)
                             if len(idx) else None})
        near=[(i,v.index,palm_d[i]) for i,v in enumerate(ring)
              if palm_d[i]<extent*.04]
        result={"component":index,"vertices":len(part),"boundary_length":n,
                "centroid":np.round(center,6).tolist(),
                "axis":np.round(longitudinal,5).tolist(),
                "longitudinal_span_over_extent":round(span/extent,6),
                "boundary_xyz_bounds":[np.round(arr.min(axis=0),6).tolist(),
                                       np.round(arr.max(axis=0),6).tolist()],
                "near_palm_count_under_4pct":len(near),
                "near_palm_ring_indices":[int(x[0]) for x in near],
                "near_palm_min_over_extent":round(min(palm_d)/extent,7),
                "near_palm_median_over_extent":round(quantile(palm_d,.5)/extent,7),
                "nonlocal_rail_dist_over_extent":[round(quantile(
                    [x[2] for x in pairs],q)/extent,6)
                    for q in [.05,.25,.50,.75,.95]],
                "nonlocal_rail_longaxis_distance_over_extent":
                    round(quantile([x[3] for x in pairs],.5)/extent,6),
                "section_bins":hist,
                "ring_source_indices":ids,
                "ring_vertices_co":np.round(arr,7).tolist(),
                "nearest_nonlocal_pair_indices":[[int(a),int(b),round(d/extent,6)]
                     for a,b,d,_ in pairs]}
        report["islands"].append(result)
        print("ROKAS FINGER DIAG",index,"source",len(part),"boundary",n,
              "near palm",len(near),"axis-span",round(span/extent,3),
              "self-rail-q05",result["nonlocal_rail_dist_over_extent"][0],
              flush=True)
    (out/"finger_boundary_full_surface_atlas.json").write_text(
        json.dumps(report,indent=2),encoding="utf-8")
    bm.free()
    print("ROKAS original finger surface atlas PASS / READ-ONLY",flush=True)

if __name__=="__main__":
    main()
