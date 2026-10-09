#!/usr/bin/env python3
"""Lossless crop and stage Blender's RIGHT-only alpha frames for ROKAS Unity Resources.
Never alters the approved laptop POV. See GitHub CI artifact for binaries.
"""
import argparse,json,os,sys
from pathlib import Path
from PIL import Image

def build(input_dir,output_dir,count=49,fps=24):
    inp=Path(input_dir);out=Path(output_dir)
    res=out/"Assets/Rokas/Resources/LaptopCinematic"
    dest=res/"HandsRight"
    dest.mkdir(parents=True,exist_ok=True)
    manifest={"fps":float(fps),"handedness":"right","frames":[]}
    for i in range(count):
        source=inp/f"HandsRight_{i:04d}.png"
        if not source.is_file():
            raise RuntimeError(f"Missing Blender frame {source}")
        img=Image.open(source).convert("RGBA")
        if img.size!=(1672,941):
            raise RuntimeError(f"Wrong source resolution #{i}: {img.size}, expected 1672x941")
        bbox=img.getchannel("A").getbbox()
        if bbox:
            left,top,right,bottom=bbox
            crop=img.crop(bbox)
        else:
            left,top=0,0
            crop=Image.new("RGBA",(1,1))
        w,h=crop.size
        name=f"Hand_{i:04d}"
        crop.save(dest/(name+".png"),optimize=True)
        manifest["frames"].append({"resource":f"LaptopCinematic/HandsRight/{name}",
            "x":left,"y":top,"w":w,"h":h})
        if i in (0,6,22,31,32,41,count-1):
            canvas=Image.new("RGBA",img.size)
            canvas.alpha_composite(crop,(left,top))
            if canvas.tobytes()!=img.tobytes():
                raise RuntimeError(f"Lossless alpha reconstruction mismatch #{i}")
    (res/"right_hand_manifest.json").write_text(
        json.dumps(manifest,indent=2,ensure_ascii=False),encoding="utf-8")
    assert manifest["handedness"]=="right" and len(manifest["frames"])==count
    print(f"RIGHT_HAND_UNITY_PACK_PASS {count} frames @ {fps}fps; output={out}")
    return manifest

if __name__=="__main__":
    ap=argparse.ArgumentParser()
    ap.add_argument("--frames",required=True)
    ap.add_argument("--output",required=True)
    ap.add_argument("--count",type=int,default=49)
    opts=ap.parse_args()
    build(opts.frames,opts.output,opts.count)
