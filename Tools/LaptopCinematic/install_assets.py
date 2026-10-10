#!/usr/bin/env python3
"""Install the APPROVED ROKAS laptop POV without altering existing Home art.

Run from any directory:
  python Tools/LaptopCinematic/install_assets.py --pack ROKAS_Laptop_Cinematic_Cloud_Pack.zip --project D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY
  python Tools/LaptopCinematic/install_assets.py --pack ROKAS_Laptop_Cinematic_Cloud_Pack.zip --project "D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY" --frames out/transparent_fullsize

The runtime intentionally FALLS BACK to the original Laptop UI until a complete
alpha frame sequence is installed. It never uses a fake glove hand asset.
"""
import argparse
import io
import json
import os
from pathlib import Path
import shutil
import tempfile
import zipfile


def install(pack, project, frames_folder=None):
    pack = Path(pack)
    project = Path(project)
    if not (project / "Assets" / "Rokas").is_dir():
        raise SystemExit("Expected Unity project root containing Assets/Rokas: " + str(project))
    resources = project / "Assets" / "Rokas" / "Resources" / "LaptopCinematic"
    source = project / "Assets" / "Rokas" / "Art" / "LaptopCinematic" / "Source"
    mapping = {
        "assets/room/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF.png":
            resources / "LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF.png",
        "assets/room/ApartmentNight_original.png": source / "ApartmentNight_original.png",
        "assets/hands/extracted/arms_rig.blend": source / "arms_rig.blend",
        "assets/hands/extracted/arms_rig.fbx": source / "arms_rig.fbx",
        "assets/hands/extracted/arms_rig.glb": source / "arms_rig.glb",
        "assets/hands/extracted/arms_01.png": source / "arms_01.png",
        "assets/hands/extracted/arms_uv.png": source / "arms_uv.png",
    }
    with zipfile.ZipFile(pack) as z:
        names = z.namelist()
        prefix = next((name[:-len("START_HERE.md")] for name in names
                       if name.endswith("START_HERE.md")), None)
        if prefix is None:
            raise SystemExit("Missing START_HERE.md in supplied pack.")
        missing = [x for x in mapping if prefix + x not in names]
        if missing:
            raise SystemExit("Incomplete pack: " + ", ".join(missing))
        for member, target in mapping.items():
            target.parent.mkdir(parents=True, exist_ok=True)
            with z.open(prefix + member) as src, target.open("wb") as dst:
                shutil.copyfileobj(src, dst)
            print("Installed", target)

    if frames_folder:
        from PIL import Image

        incoming = sorted(Path(frames_folder).glob("*.png"))
        if len(incoming) < 2:
            raise SystemExit("At least two transparent full-size 1672x941 PNGs are required.")
        destination = resources / "Hands"
        destination.mkdir(parents=True, exist_ok=True)
        manifest = {"fps": 24.0, "frames": []}
        for number, input_file in enumerate(incoming):
            with Image.open(input_file) as picture:
                if picture.size != (1672, 941):
                    raise SystemExit("Invalid source frame dimensions: " + str(input_file))
                rgba = picture.convert("RGBA")
                bbox = rgba.getchannel("A").getbbox() or (0, 0, 1, 1)
                crop = rgba.crop(bbox)
                name = "Hands_{:04d}".format(number)
                target = destination / (name + ".png")
                crop.save(target, optimize=True)
                manifest["frames"].append({
                    "resource": "LaptopCinematic/Hands/" + name,
                    "x": bbox[0], "y": bbox[1],
                    "w": crop.width, "h": crop.height
                })
        (resources / "hands_manifest.json").write_text(
            json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
        )
        print("Installed", len(incoming), "cropped hand frames and manifest")
    else:
        print("POV + unmodified rig installed; no hand render was supplied.")
        print("The existing Laptop UI remains the safe fallback until hand frames are installed.")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pack", type=Path, required=True)
    ap.add_argument("--project", type=Path, required=True)
    ap.add_argument("--frames", type=Path, help="Full-size transparent 1672x941 PNG sequence")
    args = ap.parse_args()
    install(args.pack, args.project, args.frames)


if __name__ == "__main__":
    main()
