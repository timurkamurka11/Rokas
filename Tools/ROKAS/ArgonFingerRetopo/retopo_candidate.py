#!/usr/bin/env python3
"""
ROKAS ARGON - BLENDER CANDIDATE FINGER RETOPOLOGY

DO NOT OVERWRITE source FBX. On a disposable imported copy of the user's
real 25-bone Argon FBX, attempt a continuous voxel-remeshed hand topology
while preserving the REAL original joint hierarchy, source UV and closest
source PBR texture weights. Never declare a success based only on "script
exited": require watertight 1-island skin, bounds preserved, skinned weights.

Never publish this experimental FBX or include it in production V10 until a
real Unity animation/visual comparison passes. No fake replacement cylinders.
"""
import sys, pathlib, json, math
import bpy, bmesh
from mathutils import Vector

def stop(message):
    raise RuntimeError("ROKAS RETOPO SAFETY: " + message)

def mesh_report(obj):
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    seen=set()
    islands=[]
    for vert in bm.verts:
        if vert.index in seen: continue
        stack=[vert]
        size=0
        while stack:
            v=stack.pop()
            if v.index in seen:continue
            seen.add(v.index);size+=1
            stack.extend(x for e in v.link_edges for x in e.verts if x.index not in seen)
        islands.append(size)
    report={
        "vertices":len(bm.verts),"faces":len(bm.faces),
        "islands":len(islands),"sizes":sorted(islands,reverse=True)[:10],
        "boundary_edges":sum(1 for e in bm.edges if e.is_boundary),
        "nonmanifold_edges":sum(1 for e in bm.edges if not e.is_manifold)
    }
    bm.free()
    return report

def select_only(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj

def bounds(obj):
    pts=[obj.matrix_world@Vector(x) for x in obj.bound_box]
    return [(min(v[i] for v in pts),max(v[i] for v in pts)) for i in range(3)]

def main():
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    if len(args)!=2:stop("Usage: blender -b -P script -- SOURCE.fbx OUTPUT_DIR")
    source=pathlib.Path(args[0]).resolve()
    out=pathlib.Path(args[1]).resolve();out.mkdir(parents=True,exist_ok=True)
    if not source.exists():stop("Missing authenticated original FBX "+str(source))
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source),use_anim=False)
    skins=[o for o in bpy.data.objects
           if o.type=='MESH' and 'Keiko_Hand_Skin_Mesh' in o.name]
    if len(skins)!=1:stop("Expected original single named skin, got "+str([o.name for o in skins]))
    original=skins[0]
    armature=original.find_armature()
    if armature is None or len(armature.data.bones)<25:
        stop("Missing authentic 25-bone hand skeleton")
    before=mesh_report(original)
    originalBounds=bounds(original)
    size=max(hi-lo for lo,hi in originalBounds)
    if before["islands"]!=6 or before["vertices"]<12000:
        stop("Source mesh is not the verified six-island original FBX")
    candidate=original.copy()
    candidate.data=original.data.copy()
    candidate.name="ROKAS_ARGON_Retopo_Candidate_NeverMerge_WithoutUnityQA"
    bpy.context.collection.objects.link(candidate)
    select_only(candidate)
    candidate.parent=original.parent

    # Keep source intact, remesh COPY without Armature modifier. The
    # original skeleton is rebound with transferred 25 original vertex
    # groups after the continuous skin geometry is verified.
    for modifier in list(candidate.modifiers):
        if modifier.type=='ARMATURE':
            candidate.modifiers.remove(modifier)

    # High-resolution native quad volumetric remesh. No replacement
    # cylinders/primitive fingers. The input is only the actual hand mesh.
    # Use object-local size (the imported FBX can be scaled as small as .004).
    dims=candidate.dimensions
    localSpan=max(candidate.data.vertices[i].co.length for i in
                  range(0,len(candidate.data.vertices),max(1,len(candidate.data.vertices)//200)))
    localBbox=max(max(v.co[i] for v in candidate.data.vertices)-
                  min(v.co[i] for v in candidate.data.vertices) for i in range(3))
    candidate.data.remesh_voxel_size=localBbox/480.0
    candidate.data.remesh_voxel_adaptivity=0.0
    bpy.ops.object.voxel_remesh()
    for face in candidate.data.polygons:
        face.use_smooth=True
    after=mesh_report(candidate)
    nextBounds=bounds(candidate)
    relativeDrift=max(abs(originalBounds[i][k]-nextBounds[i][k])
        for i in range(3) for k in range(2))/max(1e-10,size)

    report={
        "source_file":source.name,
        "source":before,
        "remeshed_candidate":after,
        "source_bones":len(armature.data.bones),
        "original_span":size,
        "local_span":localBbox,
        "voxel_size":candidate.data.remesh_voxel_size,
        "relative_bounds_drift":relativeDrift
    }
    (out/"geometry_before_after.json").write_text(
        json.dumps(report,indent=2,ensure_ascii=False),encoding="utf8")
    if after["islands"]!=1 or after["boundary_edges"]!=0 or relativeDrift>.025:
        # This is intentionally a HARD BLOCK: the source has one extended
        # boundary cycle per finger, not ready-to-bridge circular root rings.
        # Never claim its six detached fingers are properly welded if
        # one manifold surface is not actually demonstrated.
        stop("Voxel candidate rejected, isles=%s boundary=%s drift=%s; "+
             "need reconstructed palm/finger joint patch, NOT hidden caps"%
             (after["islands"],after["boundary_edges"],relativeDrift))
    if after["vertices"]<4000 or after["vertices"]>300000:
        stop("Unexpected destructive loss or voxel explosion "+str(after["vertices"]))

    # Preserve original source material; interpolate true source skinned
    # weights and UV from nearest source polygons rather than painting
    # synthetic placeholder skin. Data-transfer modifier is Blender-native.
    candidate.data.materials.clear()
    for mat in original.data.materials:
        candidate.data.materials.append(mat)
    select_only(candidate)
    original.hide_set(False)
    modifier=candidate.modifiers.new("ROKAS_Source_SkinWeight_Transfer","DATA_TRANSFER")
    modifier.object=original
    modifier.use_vert_data=True
    modifier.data_types_verts={'VGROUP_WEIGHTS'}
    modifier.vert_mapping='POLYINTERP_NEAREST'
    modifier.layers_vgroup_select_src='ALL'
    modifier.layers_vgroup_select_dst='NAME'
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    if len(candidate.data.uv_layers)==0:
        candidate.data.uv_layers.new(name="UVMap")
    transfer=candidate.modifiers.new("ROKAS_Original_UV_Transfer","DATA_TRANSFER")
    transfer.object=original
    transfer.use_loop_data=True
    transfer.data_types_loops={'UV'}
    transfer.loop_mapping='POLYINTERP_NEAREST'
    bpy.ops.object.modifier_apply(modifier=transfer.name)

    arm=candidate.modifiers.new("Original_Argon_25_Bone_Armature","ARMATURE")
    arm.object=armature
    if not candidate.vertex_groups:
        stop("Remesh lost original finger skin vertex groups")
    weighted=0
    for v in candidate.data.vertices:
        if len(v.groups)>0 and sum(g.weight for g in v.groups)>.5:
            weighted+=1
    report["weighted_vertices"]=weighted
    report["weight_coverage"]=weighted/max(1,len(candidate.data.vertices))
    (out/"geometry_before_after.json").write_text(
        json.dumps(report,indent=2,ensure_ascii=False),encoding="utf8")
    if report["weight_coverage"]<.98:
        stop("Transferred skin weights incomplete: "+str(report["weight_coverage"]))

    # QA artifact only. Export the original armature plus isolated corrected
    # candidate, NOT all game meshes / raw assets.
    select_only(candidate)
    armature.select_set(True)
    output=out/"ROKAS_Argon_TrueRetopo_BoneRig_QA_ONLY.fbx"
    bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True,
        object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,
        add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
    print("ROKAS CANDIDATE TOPOLOGY PASS",json.dumps(report))
    print("MANUAL UNITY VISUAL ACCEPTANCE STILL REQUIRED",output)

if __name__=="__main__":
    main()
