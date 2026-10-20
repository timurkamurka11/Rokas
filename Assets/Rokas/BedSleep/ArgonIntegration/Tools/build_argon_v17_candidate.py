"""Build an ACTUAL V17 hand FBX from authentic source in Blender headless.

This is not an audit and does not touch working V16. Output is a new FBX
candidate and proof images, with geometry/rig/shape keys and real motion.

IMPORTANT: candidate must pass geometry AND human visual inspection before
the active Unity prefab can be switched. No automatically claimed AAA pass.

Usage:
blender -b -noaudio --python-exit-code 2 --python thisfile.py --
    Assets/Rokas/BedSleep/ArgonSource/Models/ROKAS_Keiko_FPS_Arm_Rigged.fbx output/
"""
import bpy, bmesh, json, math, pathlib, sys, traceback
from collections import defaultdict
from mathutils import Vector, Quaternion
from mathutils.bvhtree import BVHTree

NAMES=("Thumb","Index","Middle","Ring","Little")
SOURCE_SKIN="Keiko_Hand_Skin_Mesh"
SKIN_GROUPS=("Hand_Palm","Arm_Wrist","Arm_ForearmRoot","Arm_ForearmTwist")
def log(s): print("[ROKAS V17 BUILD] "+str(s),flush=True)
def require(x,s):
    if not x: raise RuntimeError(s)

def groups(mesh):
    parents=list(range(len(mesh.vertices)))
    def root(i):
        while parents[i]!=i:
            parents[i]=parents[parents[i]]
            i=parents[i]
        return i
    for poly in mesh.polygons:
        ids=poly.vertices
        for j in range(1,len(ids)):
            a=root(ids[0]);b=root(ids[j])
            if a!=b: parents[a]=b
    return len(set(root(i) for i in range(len(parents))))

def boundary_and_bad(mesh):
    use=defaultdict(int)
    for poly in mesh.polygons:
        verts=list(poly.vertices)
        for a,b in zip(verts,verts[1:]+verts[:1]):
            use[tuple(sorted((a,b)))] += 1
    return sum(v==1 for v in use.values()),sum(v>2 for v in use.values())

def fill_open_back(obj):
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    boundary=[e for e in bm.edges if e.is_boundary]
    require(boundary,"Missing opened back for "+obj.name)
    result=bmesh.ops.holes_fill(bm,edges=boundary,sides=0)
    require(len(result["faces"])>0,"Cannot cap authentic source "+obj.name)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return len(result["faces"])

def blender_split_skin(original):
    bpy.ops.object.select_all(action='DESELECT')
    original.select_set(True)
    bpy.context.view_layer.objects.active=original
    duplicate=original.copy()
    duplicate.data=original.data.copy()
    bpy.context.collection.objects.link(duplicate)
    bpy.ops.object.select_all(action='DESELECT')
    duplicate.select_set(True)
    bpy.context.view_layer.objects.active=duplicate
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='LOOSE')
    bpy.ops.object.mode_set(mode='OBJECT')
    pieces=[o for o in bpy.context.selected_objects if o.type=='MESH']
    require(len(pieces)==6,"source must yield 6 authentic pieces; got "+str(len(pieces)))
    pieces.sort(key=lambda o:len(o.data.vertices),reverse=True)
    return pieces

def apply_boolean(palm,finger):
    # Exact solver calculates true surface union, unlike superficial
    # runtime socket offsets. Original skins are retained as fallback.
    mod=palm.modifiers.new("ROKAS_Union_"+finger.name,"BOOLEAN")
    mod.operation='UNION'
    mod.solver='EXACT'
    mod.object=finger
    bpy.ops.object.select_all(action="DESELECT")
    palm.select_set(True)
    bpy.context.view_layer.objects.active=palm
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return palm

def seat_finger_roots_inside_palm(pieces,arm):
    """Overlap separate authentic finger root caps before watertight union."""
    roots={}
    for name in NAMES:
        mcp=arm.matrix_world@arm.pose.bones[name+"_MCP"].head
        pip_name=name+"_IP" if name=="Thumb" else name+"_PIP"
        pip=arm.matrix_world@arm.pose.bones[pip_name].head
        v=pip-mcp
        require(v.length>.0005,"Unsafe MCP-to-PIP axis "+name)
        roots[name]=(mcp,v.normalized())
    used=set()
    metrics={}
    for piece in pieces[1:]:
        world=[piece.matrix_world@v.co for v in piece.data.vertices]
        center=sum(world,Vector())/len(world)
        choices=sorted(((center-o).length,n) for n,(o,d) in roots.items()
                       if n not in used)
        require(bool(choices),"Missing finger candidate")
        distance,name=choices[0]
        used.add(name)
        require(distance<.15,"Source finger does not match MCP "+name)
        origin,axis=roots[name]
        positions=[(p-origin).dot(axis) for p in world]
        low,high=min(positions),max(positions)
        require(high-low>.008,"Degenerate finger socket "+name)
        band=min(.013,(high-low)*.24)
        insertion=.0055 if name in ("Index","Little") else .004
        inverse=piece.matrix_world.inverted()
        changed=0
        for vert,p,t in zip(piece.data.vertices,world,positions):
            u=max(0.,min(1.,(t-low)/max(1e-8,band)))
            blend=1.-u*u*(3.-2.*u)
            if blend<1e-5:continue
            vert.co=inverse@(p-axis*(insertion*blend))
            changed+=1
        piece.data.update()
        require(changed>=12,"Finger-root seat made no manifold overlap "+name)
        metrics[name]=dict(affected_vertices=changed,insertion_m=insertion)
    require(used==set(NAMES),"Unmatched source finger")
    return metrics


def clean_decimation_microdefects(obj):
    """Repair only tiny topology accidents introduced by Blender Decimate.

    The voxel source is already 1 closed component. The decimator created
    precisely two open edges and one over-connected edge in V19; never
    broadly fill authentic finger gaps. Reject anything more extensive.
    """
    before_open,before_bad=boundary_and_bad(obj.data)
    if before_open==0 and before_bad==0:return
    require(before_open<=12 and before_bad<=6,
            "V19 decimator damaged more than a tiny local region")
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    try:
        bm.verts.ensure_lookup_table()
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=0.000001)
        bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=0.000001)
        bm.edges.ensure_lookup_table()
        # A single triple-face edge is usually a collapsed microtriangle.
        # Cull the smallest area face, not an arbitrary neighboring surface.
        extra=set()
        for edge in bm.edges:
            if len(edge.link_faces)>2:
                faces=sorted(edge.link_faces,key=lambda face:face.calc_area())
                extra.update(faces[:len(faces)-2])
        require(len(extra)<=6,"V19 decimate nonmanifold not locally repairable")
        if extra:
            bmesh.ops.delete(bm,geom=list(extra),context='FACES_ONLY')
        bm.edges.ensure_lookup_table()
        holes=[e for e in bm.edges if e.is_boundary]
        if holes:
            require(len(holes)<=20,"V19 hole repair exceeds safe finger collar")
            bmesh.ops.holes_fill(bm,edges=holes,sides=0)
        # FACES_ONLY can leave isolated vertices/wire edges. The previous
        # topology counter treats those as an extra disconnected component,
        # despite the skinned surface itself being closed.
        orphans=[v for v in bm.verts if not v.link_faces]
        require(len(orphans)<=12,
                "V19 decimator left a real detached mesh island, not just orphans")
        if orphans:
            bmesh.ops.delete(bm,geom=orphans,context='VERTS')
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(obj.data)
        obj.data.update()
    finally:
        bm.free()


def rebuild_watertight_surface(candidate, original):
    """Convert overlapped Boolean seam into continuous watertight 3D surface.
    Remesh is a structural intermediate; preserve actual UV by projecting to
    the authentic PBR source triangles, then regenerate anatomical weights.
    """
    bpy.ops.object.select_all(action="DESELECT")
    candidate.select_set(True)
    bpy.context.view_layer.objects.active=candidate
    candidate.data.remesh_voxel_size=.00135
    candidate.data.remesh_voxel_adaptivity=.0
    log("Begin V17 controlled watertight voxel reconstruction at 1.35mm")
    bpy.ops.object.voxel_remesh()
    require(len(candidate.data.vertices)>10000, "Voxel reconstruction too coarse")
    # Correct stair stepping without destroying finger volume.
    smoothing=candidate.modifiers.new("AnatomicalSurfaceLaplacian","SMOOTH")
    smoothing.factor=.45
    smoothing.iterations=3
    bpy.ops.object.modifier_apply(modifier=smoothing.name)
    # A Boolean/voxel repair with deeply overlapped MCP caps changes the
    # curvature distribution. Fixed .46 decimation may now open collar loops.
    # Try several surface-preserving reductions; never accept an open seam.
    pre_open,pre_bad=boundary_and_bad(candidate.data)
    log("V19 voxel pre-decimate: verts="+str(len(candidate.data.vertices))+
        " components="+str(groups(candidate.data))+
        " open="+str(pre_open)+" nonmanifold="+str(pre_bad))
    require(pre_open==0 and pre_bad==0 and groups(candidate.data)==1,
            "V19 source overlap produced invalid pre-decimation topology")
    remeshed=candidate.data.copy()
    accepted=False
    chosen_ratio=None
    for ratio in (.46,.58,.70,.82):
        candidate.data=remeshed.copy()
        decimate=candidate.modifiers.new("ROKAS_V19_SurfaceSafeDecimate","DECIMATE")
        decimate.ratio=ratio
        bpy.ops.object.modifier_apply(modifier=decimate.name)
        clean_decimation_microdefects(candidate)
        post_open,post_bad=boundary_and_bad(candidate.data)
        post_parts=groups(candidate.data)
        log("V19 decimation ratio "+str(ratio)+" verts="+
            str(len(candidate.data.vertices))+" components="+str(post_parts)+
            " open="+str(post_open)+" nonmanifold="+str(post_bad))
        if post_open==0 and post_bad==0 and post_parts==1:
            chosen_ratio=ratio
            accepted=True
            break
    require(accepted, "V19 all manifold-preserving decimation ratios failed")
    require(len(candidate.data.vertices)<=40000,
            "V19 manifold surface is too expensive for Unity: "+
            str(len(candidate.data.vertices)))
    log("V19 preserved continuous skin with decimation="+str(chosen_ratio))
    # Project UV onto authentic original triangles with explicit barycentric
    # mapping. Blender 3.0 DataTransfer does NOT create a new target UV
    # channel automatically on a remeshed surface.
    if not original.data.uv_layers:
        raise RuntimeError("Original Argon source has no UV channel")
    from mathutils.geometry import barycentric_transform
    source=original.data
    uv_in=source.uv_layers.active.data
    out_uv=candidate.data.uv_layers.new(name="ROKAS_V17_AuthenticSkinUV")
    raw_points=[v.co.copy() for v in source.vertices]
    triangles=[list(p.vertices) for p in source.polygons]
    tree=BVHTree.FromPolygons(raw_points,triangles,all_triangles=True)
    for poly in candidate.data.polygons:
        for li in poly.loop_indices:
            pt=candidate.data.vertices[candidate.data.loops[li].vertex_index].co
            closest,normal,fi,distance=tree.find_nearest(pt)
            if fi is None:raise RuntimeError("UV projection missing a source triangle")
            face=source.polygons[fi]
            require(len(face.vertices)==3,"Expected authentic triangulated skin")
            a,b,c=(raw_points[i] for i in face.vertices)
            uvtri=[Vector((uv_in[lp].uv.x,uv_in[lp].uv.y,0))
                   for lp in face.loop_indices]
            mapped=barycentric_transform(closest,a,b,c,*uvtri)
            out_uv.data[li].uv=(mapped.x,mapped.y)
    require(len(candidate.data.uv_layers)>0 and
            len(candidate.data.uv_layers.active.data)==len(candidate.data.loops),
            "Barycentric UV transfer failed")
    for polygon in candidate.data.polygons:polygon.use_smooth=True
    candidate.data.update()
    return dict(voxel_meters=.00135,uv_layers=len(candidate.data.uv_layers),
                vertices=len(candidate.data.vertices))

def locally_relax_mcp_collars(candidate,arm):
    """Targeted geometry polish on actual ONE-PIECE skin near finger roots.

    Keep all geometry/UV/weights and topology, but smooth high-frequency
    boolean-remesh rim transitions. Soft falloff prevents flattening the
    whole palm, thinning tips or pulling the OLED-facing wrist.
    """
    mesh=candidate.data
    xyz=[v.co.copy() for v in mesh.vertices]
    adjacency=[set() for _ in xyz]
    for e in mesh.edges:
        a,b=e.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)
    to_local=candidate.matrix_world.inverted()
    locations={}
    radii={"Thumb":.025,"Index":.028,"Middle":.022,
           "Ring":.021,"Little":.026}
    for f in NAMES:
        mcp=f+"_MCP"
        require(mcp in arm.pose.bones,"Missing finger MCP during collar polish")
        locations[f]=to_local@(arm.matrix_world@arm.pose.bones[mcp].head)
    influence=[]
    for v in xyz:
        falloffs=[]
        for f,center in locations.items():
            d=(v-center).length
            r=radii[f]
            if d<r:
                t=1.-d/r
                falloffs.append((t*t*(3.-2.*t))*.92)
        influence.append(max(falloffs,default=0.))
    original=[p.copy() for p in xyz]
    # Bounded Taubin-style geometry relaxation. No new polygons, no
    # renormalization artifacts, no uniform shrink of the hand volume.
    def pass_smoothing(positions,factor):
        new=[p.copy() for p in positions]
        for i,fall in enumerate(influence):
            if fall<1e-6 or not adjacency[i]:continue
            avg=sum((positions[j] for j in adjacency[i]),Vector())/len(adjacency[i])
            new[i]=positions[i]+(avg-positions[i])*factor*fall
        return new
    # The previous seven weak passes moved only ~1mm and left the
    # visibly cylindrical MCP socket rings in real Unity Game View.
    # Work on the original continuous mesh; never split finger islands.
    for _ in range(18):
        xyz=pass_smoothing(xyz,.50)
        xyz=pass_smoothing(xyz,-.48)
    maximum=0.
    displaced=0
    for i,p in enumerate(xyz):
        delta=p-original[i]
        d=delta.length
        if d>.0040:
            # Limit anatomically localized collar blending to 4mm; no
            # arbitrary global sleeve, nails or distal digit reshaping.
            delta*=.0040/d
            d=.0040
        if d>.00010:displaced+=1
        maximum=max(maximum,d)
        mesh.vertices[i].co=original[i]+delta
    mesh.update()
    log("Anatomical MCP collar relaxation: displaced="+str(displaced)+
        " max_mm="+str(round(maximum*1000,3)))
    require(displaced>25,"No real junction correction generated")
    return {"affected_vertices":displaced,"max_displacement_m":maximum,
            "total_vertices":len(mesh.vertices)}

def source_forearm_influences(original,arm):
    """Transfer authentic forearm skinning, NOT the disconnected finger weights.

    V19 rewrote every proximal forearm vertex as Hand_Palm / Arm_Wrist,
    ignoring Arm_ForearmTwist / Arm_ForearmRoot. That cannot track the native
    sleeve when the game rotates the wrist, and exposes a dorsal strip.
    The original Blender FBX forearm weighting is the reference for this area.
    """
    from mathutils.kdtree import KDTree
    tree=KDTree(len(original.data.vertices))
    names={g.index:g.name for g in original.vertex_groups}
    for vertex in original.data.vertices:
        tree.insert(original.matrix_world@vertex.co,vertex.index)
    tree.balance()
    allowed={"Arm_Wrist","Arm_ForearmTwist","Arm_ForearmRoot","Hand_Palm"}
    def weights(world):
        near,index,d=tree.find(world)
        vertex=original.data.vertices[index]
        data={names[g.group]:g.weight for g in vertex.groups
              if names.get(g.group) in allowed and g.weight>1e-5}
        total=sum(data.values())
        return {n:v/total for n,v in data.items()} if total>1e-5 else {}
    return weights


def sleeve_radius_audit(original,candidate,arm):
    """Measure forearm/cuff relation in authentic source world coordinates."""
    sleeve=next((o for o in bpy.data.objects
        if o.type=="MESH" and o.name=="Keiko_Homewear_Sleeve_Mesh"),None)
    if sleeve is None:
        raise RuntimeError("V20 source sleeve unavailable for fit")
    wrist=arm.matrix_world@arm.pose.bones["Arm_Wrist"].head
    upper=arm.matrix_world@arm.pose.bones["Arm_ForearmRoot"].head
    axis=upper-wrist
    require(axis.length>.01,"V20 degenerately short forearm axis")
    axis.normalize()
    def samples(obj):
        vals=[]
        for v in obj.data.vertices:
            p=obj.matrix_world@v.co
            along=(p-wrist).dot(axis)
            radius=((p-wrist)-axis*along).length
            vals.append((along,radius))
        return vals
    a,b=samples(sleeve),samples(candidate)
    result={"sleeve_vertex_count":len(a),"skin_vertex_count":len(b),
            "sleeve_axial_min_m":round(min(x for x,r in a),5),
            "sleeve_axial_max_m":round(max(x for x,r in a),5),
            "skin_axial_min_m":round(min(x for x,r in b),5),
            "skin_axial_max_m":round(max(x for x,r in b),5)}
    for s in (0.,.015,.035,.055,.075,.105):
        def section(samples):
            near=[r for t,r in samples if abs(t-s)<.007]
            if not near:return None
            near.sort()
            return {"min":round(near[0],5),"median":round(near[len(near)//2],5),
                    "max":round(near[-1],5),"count":len(near)}
        result["at_"+str(int(s*1000))+"mm"]={"sleeve":section(a),"skin":section(b)}
    log("V20 ORIGINAL SLEEVE AND CANDIDATE SKIN RADIAL AUDIT "+json.dumps(result))
    return result


def fit_native_skin_under_native_sleeve(candidate,arm):
    """Make the FBX forearm geometry sit UNDER its own authentic cuff.

    This is a SOURCE vertex correction, not runtime clipping. The final
    FBX keeps one continuous closed hand surface, with native sleeve and
    nails driven by that exact same armature in Unity.
    """
    sleeve=next((o for o in bpy.data.objects if
        o.type=="MESH" and o.name=="Keiko_Homewear_Sleeve_Mesh"),None)
    require(sleeve is not None, "V20 cannot find authored grey sleeve")
    wrist=arm.matrix_world@arm.pose.bones["Arm_Wrist"].head
    upper=arm.matrix_world@arm.pose.bones["Arm_ForearmRoot"].head
    axis=upper-wrist
    require(axis.length>.01,"V20 missing reliable forearm axis")
    axis.normalize()
    sleeve_along=[((sleeve.matrix_world@v.co)-wrist).dot(axis)
                  for v in sleeve.data.vertices]
    lip=min(sleeve_along)
    require(.045<lip<.13,
            "V20 source cuff lip is unexpectedly positioned: "+str(lip))
    local_inv=candidate.matrix_world.inverted()
    start=lip-.037
    full=lip+.052
    changed=0;max_move=0.
    for vertex in candidate.data.vertices:
        world=candidate.matrix_world@vertex.co
        diff=world-wrist
        longitudinal=diff.dot(axis)
        if longitudinal<=start:continue
        radial=diff-axis*longitudinal
        # Taper from 0 to 8.5% radial tuck-in over the cuff boundary.
        u=min(1.,max(0.,(longitudinal-start)/(full-start)))
        w=u*u*(3.-2.*u)
        inset=radial*.085*w
        vertex.co=local_inv@(world-inset)
        moved=inset.length
        if moved>1e-5:changed+=1
        max_move=max(max_move,moved)
    candidate.data.update()
    require(changed>500 and .001<max_move<.006,
            "V20 cuff surface fit did not produce safe inward overlap")
    result=dict(native_sleeve_lip_from_wrist_m=lip,
                tuck_in_fraction=.085,affected_vertices=changed,
                max_inset_m=round(max_move,6),
                runtime_triangle_mask=False)
    log("V20 AUTHORED continuous forearm/sleeve fit "+json.dumps(result))
    return result


def select_influences(candidate,arm,original):
    # Create finger-local weights, never drive little finger by Ring_PIP.
    vertices=candidate.data.vertices
    original_groups={g.name:g for g in candidate.vertex_groups}
    for name in list(original_groups):
        g=original_groups[name]
        g.remove(list(range(len(vertices))))
    for name in [b.name for b in arm.data.bones]:
        if name not in candidate.vertex_groups:
            candidate.vertex_groups.new(name=name)
    bones=arm.pose.bones
    local_to_world=candidate.matrix_world
    anchors={}
    for f in NAMES:
        names=([f+"_CMC",f+"_MCP",f+"_IP",f+"_Tip"] if f=="Thumb" else
               [f+"_MCP",f+"_PIP",f+"_DIP",f+"_Tip"])
        require(all(n in bones for n in names),"missing "+f+" bones")
        anchors[f]=[(n,arm.matrix_world@bones[n].head) for n in names]
    pale=arm.matrix_world@bones["Hand_Palm"].head
    wrist=arm.matrix_world@bones["Arm_Wrist"].head
    count=defaultdict(int)
    source_weights=source_forearm_influences(original,arm)
    upper=arm.matrix_world@bones["Arm_ForearmRoot"].head
    forearm_axis=upper-wrist
    require(forearm_axis.length>.01, "V20 invalid native forearm axis")
    forearm_axis.normalize()
    for v in vertices:
        xyz=local_to_world@v.co
        matches=[]
        for f, chain in anchors.items():
            # Distance to centreline AND longitudinal projection,
            # penalise extension back into palm behind MCP.
            axis=(chain[-1][1]-chain[1 if f=="Thumb" else 0][1])
            start=chain[1 if f=="Thumb" else 0][1]
            length=axis.length
            if length<1.e-5:continue
            direction=axis.normalized()
            t=(xyz-start).dot(direction)/length
            closest=min(
                (max(0,min(1,(xyz-a[1]).dot(b[1]-a[1])/
                             max((b[1]-a[1]).length_squared,1.e-9)))*
                 (b[1]-a[1])+a[1]-xyz).length
                for a,b in zip(chain,chain[1:]))
            penalty=(max(0,-t)*.055 + max(0,t-1.15)*.055)
            matches.append((closest+penalty,f,t,closest))
        matches.sort()
        gweights={}
        if matches:
            rank,fn,t,dist=matches[0]
            maxradius=.018 if fn=="Thumb" else .014
            start=anchors[fn][1 if fn=="Thumb" else 0][1]
            # Avoid treating nearby palm surface as another digit.
            if -0.055<=t<=1.18 and dist<maxradius:
                chain=anchors[fn]
                # smooth, physiologically reasonable joint blending,
                # using nearest joint stations and always retaining
                # palm influence near the digit root.
                spots=[(xyz-p).length for _,p in chain]
                # Distal-most vertices should bind to their own last bone.
                close=min(range(len(spots)),key=lambda i:spots[i])
                if close==0:
                    one,two=0,1
                elif close==len(chain)-1:
                    one,two=len(chain)-2,len(chain)-1
                else:
                    one,two=close-1,close
                    if spots[close+1]<spots[close-1]:
                        one,two=close,close+1
                p0,p1=chain[one][1],chain[two][1]
                frac=max(0.,min(1.,(xyz-p0).dot(p1-p0)/max((p1-p0).length_squared,1e-8)))
                # Continuous palm-to-MCP influence across first ~35% of finger.
                # The previous 100% MCP root caused visible cylindrical tearing.
                near=max(0.,min(1.,(t+.08)/.43))
                near=near*near*(3.-2.*near)
                palm_factor=.72*(1.-near)
                gweights[chain[one][0]]=(1.-palm_factor)*(1.-frac)
                gweights[chain[two][0]]=(1.-palm_factor)*frac
                if palm_factor>1e-6:
                    gweights["Hand_Palm"]=palm_factor
                count[fn]+=1
        if not gweights:
            # Non-digit regions belong to the HAND or real FOREARM rig,
            # not a made-up Hand_Palm / Arm_Wrist blend along the whole arm.
            gweights={"Hand_Palm":1.}
            count["Palm"]+=1
        proximal=(xyz-wrist).dot(forearm_axis)
        if proximal>-.004:
            authored=source_weights(xyz)
            if authored:
                # Smooth, stable 4 cm source->new skin weights transition.
                factor=max(0.,min(1.,(proximal+.004)/.04))
                factor=factor*factor*(3.-2.*factor)
                merged=defaultdict(float)
                for name,value in gweights.items():merged[name]+=(1.-factor)*value
                for name,value in authored.items():merged[name]+=factor*value
                gweights=dict(merged)
                if factor>.98:count["SourceForearm"]+=1
        tot=sum(gweights.values())
        for bone,weight in gweights.items():
            if weight/tot>1e-5:
                candidate.vertex_groups[bone].add([v.index],weight/tot,'REPLACE')
    return dict(count)

def build_correctives(candidate,arm):
    data=candidate.data
    require(data.shape_keys is None,"Unwanted copied corrective forms")
    candidate.shape_key_add(name="Basis",from_mix=False)
    rest=[v.co.copy() for v in data.vertices]
    inv=candidate.matrix_world.inverted()
    made={}
    targets=[
        ("Index_MCP_Corrective","Index_MCP",.00050,.011),
        ("Index_PIP_Corrective","Index_PIP",.00032,.008),
        ("Index_DIP_Corrective","Index_DIP",.00020,.006),
        ("Little_MCP_Corrective","Little_MCP",.00038,.009),
        ("Little_PIP_Corrective","Little_PIP",.00024,.007),
        ("Thumb_Opposition_Corrective","Thumb_MCP",.00038,.009),
        ("Knuckle_Compression_Corrective","Middle_MCP",.00028,.012),
        ("Fist_Grip_Corrective","Hand_Palm",.00020,.028)
    ]
    for keyname,bone,amt,radius in targets:
        center=inv@(arm.matrix_world@arm.pose.bones[bone].head)
        shape=candidate.shape_key_add(name=keyname,from_mix=False)
        n=0
        for i,pos in enumerate(rest):
            d=(pos-center).length
            if d>radius:continue
            fall=(1.-d/radius)**2
            # Physically localized knuckle volume, NOT a blank shape key.
            # Shape layer remains zero until Unity confirms intended driver.
            shape.data[i].co=pos+Vector((0,0,amt*fall))
            n+=1
        if n<3:
            candidate.shape_key_remove(shape)
            continue
        made[keyname]=n
    return made

def animator_actions(arm,candidate):
    # Shape keys without authored curves stay at zero: they were present in
    # V17 but did NOT actually correct bent joints. Build synchronized
    # deform curves for each action, exported with the real skinned FBX.
    shape_data=candidate.data.shape_keys
    require(shape_data is not None,"No authored corrective shapes to animate")
    correctives={name:shape_data.key_blocks.get(name) for name in (
        "Index_MCP_Corrective","Index_PIP_Corrective",
        "Index_DIP_Corrective","Little_MCP_Corrective",
        "Little_PIP_Corrective","Thumb_Opposition_Corrective",
        "Knuckle_Compression_Corrective","Fist_Grip_Corrective")}
    require(all(x is not None for x in correctives.values()),
            "Corrective key names missing from reconstruction")
    bone_names={}
    for name in NAMES:
        bone_names[name]=[name+x for x in (
             ("_CMC","_MCP","_IP") if name=="Thumb"
             else ("_MCP","_PIP","_DIP"))]
    real=arm.pose.bones
    for bone in real:
        bone.rotation_mode='QUATERNION'
    # Full authorable pose library is distinct from constant sine sway.
    def key_pose(action_name,timeline):
        action=bpy.data.actions.new(action_name)
        arm.animation_data_create().action=action
        shape_action=bpy.data.actions.new(action_name+"_Corrective")
        shape_data.animation_data_create().action=shape_action
        for frame,extent in timeline:
            for finger, names in bone_names.items():
                # Index QA clip MUST NOT animate the other four fingers.
                amplitude=extent if action_name!="V17_Index" or finger=="Index" else 0
                stagger={"Thumb":.78,"Index":1.0,"Middle":1.03,
                         "Ring":.97,"Little":.88}[finger]
                for j,n in enumerate(names):
                    bone=real[n]
                    # Convert anatomically planned global hinge axis into
                    # original bone local rotation frame.
                    axis=bone.bone.matrix_local.to_quaternion().inverted()@Vector((0,1,0))
                    angle=math.radians(amplitude*stagger*(.50 if j==0 else
                         .76 if j==1 else .48))
                    if finger=="Thumb":
                        axis=bone.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,1))
                        angle*=.60
                    bone.rotation_quaternion=Quaternion(axis.normalized(),angle)
                    bone.keyframe_insert(data_path="rotation_quaternion",
                                         frame=frame,group=finger)
            # Each corrective is synchronized with joint flexion.
            # Finger-specific keys only respond to their own gesture;
            # V17_Index must not bulge pinky/thumb/whole palm.
            grip=max(0.,min(1.,extent/95.))
            only_index=action_name=="V17_Index"
            curve_scale={
                "Index_MCP_Corrective":.58,
                "Index_PIP_Corrective":.52,
                "Index_DIP_Corrective":.36,
                "Little_MCP_Corrective":0. if only_index else .53,
                "Little_PIP_Corrective":0. if only_index else .40,
                "Thumb_Opposition_Corrective":0. if only_index else .38,
                "Knuckle_Compression_Corrective":0. if only_index else .47,
                "Fist_Grip_Corrective":0. if only_index else .43}
            for name,key in correctives.items():
                key.value=grip*curve_scale[name]
                key.keyframe_insert(data_path="value",frame=frame,
                                    group="ROKAS_FlexCorrectives")
        for curve in action.fcurves:
            for key in curve.keyframe_points:
                key.interpolation="BEZIER"
                key.handle_left_type="AUTO_CLAMPED"
                key.handle_right_type="AUTO_CLAMPED"
        for curve in shape_action.fcurves:
            for key in curve.keyframe_points:
                key.interpolation="BEZIER"
                key.handle_left_type="AUTO_CLAMPED"
                key.handle_right_type="AUTO_CLAMPED"
        action.use_fake_user=True
        shape_action.use_fake_user=True
        return action
    actions=[
        key_pose("V17_Grip",
            [(1,0),(9,5),(18,26),(30,65),(46,91),(56,92),
             (70,56),(83,14),(96,0)]),
        key_pose("V17_Index",
            [(1,0),(12,0),(26,36),(43,57),(64,0)]),
        key_pose("V17_Living",
            [(1,4),(15,10),(32,4),(51,0),(75,5),(95,1)])
    ]
    for bone in real:
        bone.rotation_quaternion=Quaternion((1,0,0,0))
    arm.animation_data.action=None
    shape_data.animation_data.action=None
    for key in correctives.values():key.value=0.
    return [a.name for a in actions]

def save_proof(scene,obj,out,cam_name):
    # Geometry renders use side and dorsal views for quick QA.
    camera_data=bpy.data.cameras.new(cam_name)
    camera=bpy.data.objects.new(cam_name,camera_data)
    scene.collection.objects.link(camera)
    # Frame ACTUAL object world-space bounds rather than assuming the GLB
    # mesh and exported armature share identity transforms.
    points=[obj.matrix_world@Vector(v) for v in obj.bound_box]
    target=sum(points,Vector())/len(points)
    xyz=[max(p[i] for p in points)-min(p[i] for p in points)
         for i in range(3)]
    camera.location=target+Vector((0,-.48,.34))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO'
    camera.data.ortho_scale=max(.36,xyz[0]*1.5,xyz[1]*2.2,xyz[2]*1.7)
    camera.data.lens=50
    log("Proof camera object bounds="+str([round(i,4) for i in xyz])+
        " worldCenter="+str(tuple(round(i,4) for i in target)))
    scene.camera=camera
    scene.render.engine="BLENDER_EEVEE"
    scene.render.resolution_x=1280
    scene.render.resolution_y=720
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.render.film_transparent=False
    if scene.world is None: scene.world=bpy.data.worlds.new("ROKAS_V17_GeometryProofWorld")
    scene.world.color=(.07,.08,.10)
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value=(.08,.10,.14,1)
        bg.inputs["Strength"].default_value=.85
    # Use a neutral proof-only skin material override, NOT the exported
    # textured production material; its job is proving 3D silhouette.
    qa_mat=bpy.data.materials.new("ROKAS_V17_PROOF_ONLY_UNEXPORTED")
    qa_mat.use_nodes=True
    bsdf=qa_mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value=(.83,.49,.40,1)
        bsdf.inputs["Roughness"].default_value=.62
    scene.view_layers[0].material_override=qa_mat
    # Workbench requires a display on Blender 3.0 Ubuntu runner.
    # Cycles CPU is headless and produces an actual shaded skin image.
    scene.render.engine="CYCLES"
    scene.cycles.device="CPU"
    scene.cycles.samples=12
    light_data=bpy.data.lights.new("ROKAS_V17_QA_Softbox","AREA")
    light_data.energy=650
    light_data.shape="DISK"
    light_data.size=.75
    light=bpy.data.objects.new("ROKAS_V17_QA_Softbox",light_data)
    scene.collection.objects.link(light)
    light.location=(.08,-.18,.30)
    light.rotation_euler=(Vector((0,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
    obj.hide_render=False
    scene.render.filepath=str(out/"ROKAS_V17_Candidate_3D.png")
    bpy.ops.render.render(write_still=True)
    scene.view_layers[0].material_override=None

def main():
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    require(len(args)==2,"Usage: -- existing-fbx output-folder")
    src,out=pathlib.Path(args[0]).resolve(),pathlib.Path(args[1]).resolve()
    require(src.is_file(),"Missing source "+str(src))
    out.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(src),use_anim=True)
    src_skin=next((o for o in bpy.data.objects
                   if o.type=='MESH' and o.name==SOURCE_SKIN),None)
    arm=next((m.object for m in src_skin.modifiers if m.type=="ARMATURE"),None) if src_skin else None
    require(src_skin is not None and arm is not None,"Source hand/rig missing")
    before_v=len(src_skin.data.vertices)
    before_components=groups(src_skin.data)
    require(before_components==6,"Expected six separate source islands")
    pieces=blender_split_skin(src_skin)
    caps=[fill_open_back(p) for p in pieces]
    # V19 'EXACT' Boolean fell back to fast solver on Ubuntu Blender 3.0
    # without GMP and produced 634 nonmanifold edges BEFORE voxel repair.
    # This leaves an ugly cylinder-socket imprint even when the final
    # manifold check passes. V21 joins the six CLOSED authentic source
    # volumes without cutting them, then voxel remeshes their overlap into
    # ONE new continuous 3D surface. Do not apply Blender Boolean.
    palm=pieces[0]
    root_socket_metrics=seat_finger_roots_inside_palm(pieces,arm)
    bpy.ops.object.select_all(action="DESELECT")
    for piece in pieces:piece.select_set(True)
    bpy.context.view_layer.objects.active=palm
    bpy.ops.object.join()
    require(groups(palm.data)==6, "V21 source volume join lost input islands")
    log("V21 joined 6 authentic skin volumes; union performed by voxel remesh")
    src_skin.name="ROKAS_OriginalSkin_UnmodifiedNotExported"
    palm.name="Keiko_Hand_Skin_Mesh"
    palm.data.name="ROKAS_Keiko_V17_Skin"
    palm.hide_render=False
    # Prevent accidental bone-mapping changes: export original 25-bone rig.
    arm_mods=[m for m in palm.modifiers if m.type=='ARMATURE']
    require(arm_mods,"New skin lost original deform rig")
    source_materials=list(src_skin.data.materials)
    palm.data.materials.clear()
    for m in source_materials:
        palm.data.materials.append(m)
    bdr,bad=boundary_and_bad(palm.data)
    comp=groups(palm.data)
    log(f"V21 raw volumetric source vertices={len(palm.data.vertices)} components={comp} boundary={bdr} nonmanifold={bad}")
    if bdr or bad or comp!=1:
        surface=rebuild_watertight_surface(palm,src_skin)
        bdr,bad=boundary_and_bad(palm.data)
        comp=groups(palm.data)
        log("rebuilt surface="+json.dumps(surface)+
            f" components={comp} open={bdr} nonmanifold={bad}")
    # A candidate cannot pass when the five digits are not attached.
    require(comp==1,"Merged surface remains disconnected: "+str(comp))
    require(bad==0,"Boolean candidate contains nonmanifold edges")
    require(bdr==0,"Boolean candidate still has open seams")
    collar_metrics=locally_relax_mcp_collars(palm,arm)
    # Retopology of the junctions changes vertex POSITION only. Confirm
    # the mesh remains one closed nonmanifold-free component.
    polished_open,polished_bad=boundary_and_bad(palm.data)
    require(groups(palm.data)==1 and polished_open==0 and polished_bad==0,
            "Collar polish introduced a broken topological skin")
    cuff_source_audit=sleeve_radius_audit(src_skin,palm,arm)
    authored_cuff_fit=fit_native_skin_under_native_sleeve(palm,arm)
    weightmap=select_influences(palm,arm,src_skin)
    correctives=build_correctives(palm,arm)
    require(len(correctives)>=5,"Candidate shape keys missing")
    # Blender FBX exporter encodes action names as
    # <rig-object>|<rig-object>|<action>. The original verbose rig name
    # consumed the 63-character FBX name limit so ALL three imported bone
    # actions were indistinguishable. This is a NEW candidate only;
    # bone names and hierarchies stay unchanged for Unity retargeting.
    arm.name="RokasV17Rig"
    arm.data.name="RokasV17Skeleton"
    authored=animator_actions(arm,palm)
    # Diagnostics: distinguish correct animated source shape data from an
    # FBX exporter that silently bakes ALL blendshapes at zero.
    for act in bpy.data.actions:
        if act.name.startswith("V17_") and "Corrective" in act.name:
            values=[(fc.data_path,round(fc.evaluate(1),4),
                     round(fc.evaluate(51),4)) for fc in act.fcurves[:3]]
            log("V17 SHAPE ACTION SOURCE "+act.name+" "+str(values))
    # Export source sleeve/nails and authentic armature with new candidate skin.
    src_skin.hide_render=True
    selected=[arm,palm]
    for name in ("Keiko_Fingernails_Mesh","Keiko_Homewear_Sleeve_Mesh"):
        mesh=next((o for o in bpy.data.objects if o.name==name),None)
        if mesh is not None:selected.append(mesh)
    outfbx=out/"ROKAS_Keiko_FPS_Arm_Rigged_V17_Candidate.fbx"
    bpy.ops.object.select_all(action="DESELECT")
    for o in selected:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active=arm
    bpy.ops.export_scene.fbx(
        filepath=str(outfbx),use_selection=True,object_types={'MESH','ARMATURE'},
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,
        apply_unit_scale=True,global_scale=1.0,
        use_mesh_modifiers=True,bake_anim=True,bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,bake_anim_use_all_bones=True,
        bake_anim_step=1.,bake_anim_simplify_factor=0.,
        path_mode='AUTO',embed_textures=False,
        use_subsurf=False, use_tspace=False)
    require(outfbx.stat().st_size>100_000,"Empty V17 FBX")
    # Blender 3.0 exporter with "All Actions" bakes armature channels
    # but writes zeroed shape-key channels for every generated AnimStack.
    # Export each combined skeletal+corrective clip separately, with BOTH
    # actions explicitly ACTIVE and both FBX multi-action switches OFF.
    # This preserves a complete base candidate and three real clip FBXs
    # without altering the source rig or live V16.
    clip_files=[]
    for clip,probe_frame in (("V17_Grip",51),("V17_Index",43),
                             ("V17_Living",15)):
        a=bpy.data.actions.get(clip)
        k=bpy.data.actions.get(clip+"_Corrective")
        require(a is not None and k is not None,"Missing paired action "+clip)
        arm.animation_data_create().action=a
        palm.data.shape_keys.animation_data_create().action=k
        bpy.context.scene.frame_set(probe_frame)
        chosen={name:round(float(palm.data.shape_keys.key_blocks[name].value),5)
                for name in ("Index_MCP_Corrective","Fist_Grip_Corrective")}
        log("ACTIVE FBX PAIR "+clip+" frame="+str(probe_frame)+
            " correctives="+repr(chosen))
        require(chosen["Index_MCP_Corrective"]>.01,
                "Corrective action not active before FBX export: "+clip)
        clipfile=out/("ROKAS_V17_"+clip+"_Animated.fbx")
        bpy.ops.export_scene.fbx(
            filepath=str(clipfile),use_selection=True,
            object_types={'MESH','ARMATURE'},
            axis_forward='-Z',axis_up='Y',add_leaf_bones=False,
            apply_unit_scale=True,global_scale=1.0,
            use_mesh_modifiers=True,bake_anim=True,
            bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_bones=True,
            bake_anim_step=1.,bake_anim_simplify_factor=0.,
            path_mode='AUTO',embed_textures=False,
            use_subsurf=False,use_tspace=False)
        require(clipfile.stat().st_size>100_000,
                "Single-action FBX did not export: "+clip)
        clip_files.append(clipfile.name)
    arm.animation_data.action=None
    palm.data.shape_keys.animation_data.action=None
    for key in palm.data.shape_keys.key_blocks:
        if key.name!="Basis":key.value=0.
    bpy.context.scene.frame_set(1)
    save_proof(bpy.context.scene,palm,out,"V17_VerificationCamera")
    results=dict(source_vertices=before_v,source_components=before_components,
        candidate_vertices=len(palm.data.vertices),
        candidate_triangles=sum(len(x.vertices)-2 for x in palm.data.polygons),
        candidate_components=comp,exposed_edges=bdr,nonmanifold_edges=bad,
        finger_vertex_owners=weightmap,
        cuff_source_audit=cuff_source_audit,
        authored_cuff_fit=authored_cuff_fit,
        mcp_collar_polish=collar_metrics,
        corrective_shapes=correctives,
        authored_actions=authored,output_fbx=outfbx.name,
        paired_corrective_clip_fbxs=clip_files,
        status="CANDIDATE: NOT YET VERIFIED IN UNITY")
    (out/"ROKAS_V17_BuildMetrics.json").write_text(json.dumps(results,indent=2))
    log(json.dumps(results,indent=2))
    log("Actual V17 candidate FBX created; ORIGINAL V16 NOT MODIFIED")

if __name__=="__main__":
    try:main()
    except Exception:
        traceback.print_exc()
        sys.exit(2)
