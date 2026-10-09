# ROKAS — laptop POV cinematic (feature branch)

## Scope / grounded project hooks

Baseline: `r11-finished-ui` at `4ce87ea23784c022b83882faab1ad45f03bee46a`.
Unity: `6000.3.19f1`. The main Home scene is assembled programmatically:
`HomeView.Build` creates `LaptopHotspot`; `RokasView.OpenPanel("laptop")`
calls the existing `LaptopView`. No new desktop/panel or duplicated YOMI logic.

Only the Laptop hotspot in the Home room now routes through
`RokasView.OpenFromHomeLaptopHotspot`. Other calls to
`OpenPanel("laptop")`, including Hallway/YOMI programmatic entries,
keep their existing behavior.

## What is actually implemented in this branch

- A guarded cinematic controller in `LaptopCinematicSequence.cs`.
- ~5.7 second elapsed-time flow: original scene approach, approved 1672x941
  POV crossfade, RGBA hands frames, power press cue and a separate
  four-corner, dim screen wake polygon.
- Escape/skip opens the existing `LaptopView` exactly once and discards
  the overlay; scene rebuild, save block and dispose cancel cleanly.
- The original world background image, Home overlay and hotspot transforms
  are not edited. Other hotspots are disabled only during the cinematic.
- **Fail-open by design**: if POV or complete hand frame manifest is absent,
  the pre-existing laptop opening behavior runs. Missing visual assets
  are not treated as a finished animation.
- Initial screen corner / power anchor estimates are in
  `Assets/Rokas/Resources/LaptopCinematic/screen_calibration.json`.
  They are calibration START VALUES, **not visually verified**.

## Install the exact approved assets

Use the ZIP delivered by the user and run:

```powershell
python Tools/LaptopCinematic/install_assets.py --pack "ROKAS_Laptop_Cinematic_Cloud_Pack(1).zip" --project "D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY"
```

The script installs the exact binary `LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF.png`
into `Assets/Rokas/Resources/LaptopCinematic/`, and imports the original
FBX/GLB/Blend, bare hands texture and UVs into
`Assets/Rokas/Art/LaptopCinematic/Source/`. It does **not** replace
`ApartmentNight.png`; intentional background blur is untouched.
The glove variant is not installed as the final style.

### Transparent hands sequence requirement (not yet produced)

With Blender or a real rig-aware render environment:
1. Use `arms_rig.blend` / FBX (hand rig, 52 joints; includes push/relax samples).
2. Adapt the rig to an illustrated POV with **bare fingers** and **loose matte
   graphite/navy sleeves**. Author left brace, right index reach, calibrated
   contact/depression, release and withdrawal; do not render gloves.
3. Produce 1672x941 transparent PNG frames at 24fps, with the camera locked
   to the approved POV perspective. Align the fingertip to the *actual*
   power key; do not rely on the unverified default `powerX=1040,powerY=708`.
4. With Pillow available (`pip install pillow`), run:

```powershell
python Tools/LaptopCinematic/install_assets.py --pack "ROKAS_Laptop_Cinematic_Cloud_Pack(1).zip" --project "D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY" --frames "D:\Rokas\LaptopHandRender\rgba_1672x941"
```

This crops transparent frames losslessly and creates
`Assets/Rokas/Resources/LaptopCinematic/hands_manifest.json` with exact
per-frame crop/pivot placement. Maintain ordered `0000.png` naming. Budget
about 83 frames for `2.0s..5.45s` at 24fps. Final frame count, animation
timing, alpha/lighting and Blender sleeve geometry are **still pending**.

## Unity verification gates (NOT EXECUTED in this cloud runtime)

- Unity EditMode and PlayMode compilation and project validators.
- `LaptopCinematicFallbackPlayModeTests` and previous Laptop lifecycle tests.
- Click/double-click, press, screen wakes, skip at every stage, Escape,
  interrupted UI, return, new home visit, other hotspots / dialogue.
- Captures at wide room, camera cut, left brace, precise index contact,
  screen wake, existing Laptop UI and cleanly restored room.
- Validate transparent edges, screen polygon alignment, scaling,
  intentional DOF blur and whether the finished sleeves match the 2D art.

A safe feature branch/PR is the delivery surface. Do **not** merge this into
the unified or mainline game until visual assets exist and Unity tests pass.
No local Windows project or editor was modified by the cloud process.

## Source and license

Drillimpact, **PSX First Person Arms** (CC0 / public domain),
https://drillimpact.itch.io/psx-first-person-arms-free

The provided archive does not carry its own LICENSE text, so this is the
publisher's explicit publicly stated license. Keep this attribution note as
provenance; the original source files are not authored by ROKAS.
