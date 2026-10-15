# ROKAS Bed Sleep V10 — genuine skinned mesh recontouring + nail surface seating

**Status: independent engineering/artist-review candidate; NOT imported into Unity, NOT merged, NOT reference-perfect.**

Actual binary/source artifact: `ROKAS_BED_SLEEP_V10_VOLUME_AND_NAIL_REFIT_CANDIDATE.zip` (49 files, CRC/SHA-256 verified), delivered in this chat. The GitHub branch only contains status documentation; the binary GLBs and Unity candidates are **not in GitHub**.

## Actual V10 edits to real source assets

- Modified POSITION and NORMAL data **inside the V9 real GLB**, preserving 10,320 skin/sleeve shared vertices, 20,636 closed manifold body faces, 25 skin joints, 8 genuine 19-channel glTF animations. No capsule/box hand substituted. Z-depth recontoured by anatomical region, with subtle dorsal knuckle/tendon relief.
- Independent thickness (P5–P95 Z, millimetres): at x=-0.17m 63.1 -> 41.4; -0.07m 64.4 -> 35.9; +0.035m 65.9 -> 28.6; +0.08m 68.1 -> 25.0; +0.125m 52.7 -> 14.6. Source had unrealistically thick silhouette extrusions. Geometry remains watertight with 30,954 edges incidence=2.
- V9 nail defects measured: ~31% of the original nail XY vertices projected outside the skin; ring nail had **zero** coverage. Corrected using actual distal-joint skin-weight centroids, true dorsal triangle ray interpolation, attached surface Z, and barycentrically interpolated real skin weights. All five new nail footprints achieve 100% local XY coverage over the actual source skin mesh at bind pose. Five individual nail shells remain closed and skinned, without changing their triangle counts or GLB animations.
- Seven independent real watch meshes retained (case, straps, OLED, glass, crown, button); no placeholder production meshes.

## Source-level integration fixes (in downloadable ZIP)

- **Blender 4.x** script imports real glTF and consolidates split skin/sleeve/nail geometry into ONE real skinned FBX object with THREE material slots. Must save and reopen .blend, export genuine binary FBX and re-import it. This script is prepared but **not executed** in the current environment (Blender absent).
- **Unity Editor V10 candidate builder** imports real Blender-exported FBXs, checks 25 bones, three actual submeshes, all 15 finger animation curves, 7 independent watch meshes, and saves only into `Assets/Rokas/Resources/BedSleep/V10Candidate/`. It is source-only and has **not passed native Unity compilation**.
- `KeikoFPSArmRigController` now fails closed if genuine 3 material groups are missing; no 2-material fallback that silently loses V10 nails, and no invented skinned renderer.
- `Smartwatch3DController` uses a disposable OLED material instance rather than mutating the shared imported .mat; runtime cleanup fixes in `BedSleepVisualComposite`.

## Independent data-only tests actually executed

- `14_V10_QA/validate_v10.py`: glTF binary format, 25 bones, 8 x 19-channel clips, 3 real GLB primitive materials, closed manifold body edges, 5 watertight nail shells, all nails on real arm surface in bind pose, 7 distinct watch meshes with no degenerate faces, no shared OLED material mutation. PASS.
- `14_V10_QA/verify_real_skin_deformation.py`: sampled actual glTF weighted vertex deformation over 15 frames of each clip; Raise and Lower nonzero motion. PASS.
- Diagnostic PNGs: `V9_NAIL_SURFACE_DIAGNOSTIC.png`, `V10_NAIL_SURFACE_DIAGNOSTIC.png`, `V10_THICKNESS_COMPARISON.png`. These are mesh diagnostics, **not** an actual Blender/Unity renderer.

## Required further work / nonclaims

1. Run the supplied Blender exporter; confirm saved files load as editable .blend and reimport real FBX, with 3 material slots, 25 skin joints and 8 finger actions.
2. Run native Unity 6000.3.19f1 import, candidate prefab build, PlayMode and watch/OLED input tests.
3. Visually inspect hand skin anatomy, fingers, nails, sleeves, lighting and watch alignment against user's approved two reference images. Automated cross-section shrinking is **not equivalent to professional hand sculpting** and does not prove photorealism.
4. No production installer, no force/reset/clean. Do not edit the user's main `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` without an actual validated import and review.
