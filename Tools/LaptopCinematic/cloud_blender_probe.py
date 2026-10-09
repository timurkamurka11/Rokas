#!/usr/bin/env python3
"""Cloud Blender proof: import one CC0 anatomical right hand and render one transparent preview.
Not final cinematic footage. No data is written to the Unity project.
Usage: blender -b -t 2 --python cloud_blender_probe.py -- --input /path/hand.glb --output /path/proof
"""
import argparse
import json
import math
import os
import sys
import traceback

import bpy
from mathutils import Vector

def cli():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--input", required=True)
    p.add_argument("--output", required=True)
    return p.parse_args(argv)

def look_at(obj, point):
    direction = Vector(point) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

def main():
    a = cli()
    os.makedirs(a.output, exist_ok=True)
    if not os.path.isfile(a.input) or os.stat(a.input).st_size < 1000:
        raise ValueError("Source GLB missing or implausibly small")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(a.input))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    rigs = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if not meshes:
        raise ValueError("No hand mesh imported from GLB")
    if not rigs:
        raise ValueError("No armature: source not suitable for articulated finger contact")

    # Source contains an auxiliary 42-vertex Icosphere about 2m across.
    # Framing all meshes hides the real ~0.2m anatomical hand.
    render_meshes = [o for o in meshes if len(o.data.vertices) >= 1000]
    if not render_meshes:
        render_meshes = meshes
    for o in meshes:
        if o not in render_meshes:
            o.hide_render = True

    points = [o.matrix_world @ Vector(corner) for o in render_meshes for corner in o.bound_box]
    lo = Vector([min(p[i] for p in points) for i in range(3)])
    hi = Vector([max(p[i] for p in points) for i in range(3)])
    center = (lo + hi) * .5
    extents = hi - lo
    size = max(extents.x, extents.y, extents.z, .1)
    armatures = [{"name":o.name,"bones":[b.name for b in o.data.bones]}
                 for o in rigs]
    report = {
        "input":os.path.basename(a.input),
        "license_claim":"CC0 - source page https://www.innerscene.com/tools/library/3d-parts/posable-anatomical-right-hand-f6098b4f",
        "blender":bpy.app.version_string,
        "meshes": [{"name":o.name,"vertices":len(o.data.vertices),"polygons":len(o.data.polygons), "rendered":o in render_meshes} for o in meshes],
        "armatures":armatures,
        "bounds": {"min":list(lo),"max":list(hi)},
        "status":"IMPORTED_ONLY_NOT_ANIMATED"
    }
    with open(os.path.join(a.output,"rig_report.json"),"w",encoding="utf-8") as f:
        json.dump(report,f,indent=2,ensure_ascii=False)

    camera_data = bpy.data.cameras.new("ROKAS_Probe_Camera")
    camera = bpy.data.objects.new("ROKAS_Probe_Camera",camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = center + Vector((size * 1.5,-size * 2.5,size * 1.5))
    look_at(camera,center)
    camera_data.type="ORTHO"
    camera_data.ortho_scale=size * 2.3
    bpy.context.scene.camera=camera

    for name,pos,power,color in [
        ("ROKAS_Key",(-1.4,-2.8,3.3),360,(1.0,.66,.40)),
        ("ROKAS_Fill",(3.2,-.6,2.0),240,(.47,.66,1.0)),
        ("ROKAS_Rim",(-.4,3.0,1.2),220,(1.0,.79,.59))
    ]:
        lamp_data=bpy.data.lights.new(name, "AREA")
        lamp=bpy.data.objects.new(name,lamp_data)
        bpy.context.collection.objects.link(lamp)
        lamp.location=center + Vector(pos)*size
        lamp_data.energy=power
        lamp_data.color=color
        lamp_data.shape="DISK"
        lamp_data.size=size * 2.0
        look_at(lamp,center)

    scene=bpy.context.scene
    scene.render.engine="CYCLES"
    scene.cycles.samples=16
    scene.render.resolution_x=640
    scene.render.resolution_y=640
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    scene.render.film_transparent=True
    scene.render.filepath=os.path.join(a.output,"right_hand_rig_preview.png")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(a.output,"right_hand_cloud_probe.blend"))
    bpy.ops.render.render(write_still=True)
    assert os.path.getsize(scene.render.filepath)>2000
    print("ROKAS_CLOUD_BLENDER_PASS: imported rig, saved .blend, rendered transparent PNG")
    print("armatures=%d meshes=%d total_bones=%d" % (
        len(rigs),len(meshes),sum(len(o.data.bones) for o in rigs)))

if __name__ == "__main__":
    try:
        main()
    except Exception:
        traceback.print_exc()
        sys.exit(1)
