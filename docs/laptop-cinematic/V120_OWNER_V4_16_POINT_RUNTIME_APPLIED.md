# ROKAS Laptop Cinematic — owner-approved V4 applied to runtime

## Source of truth

Owner supplied `ROKAS_Laptop_LCD_Calibration(4).json` (11 October 2026). The **entire** JSON is installed in `Assets/Rokas/Resources/LaptopCinematic/screen_calibration_v4.json`, imported with `screen_calibration_v4.json.meta`. Dimensions are **1672×941 source-photo pixels**, origin top left. Values must not be recomputed from a Game View screenshot.

| Control | X | Y |
|---|---:|---:|
| TL | 610.594482421875 | 412.7184753417969 |
| TR | 1060.366455078125 | 412.9056396484375 |
| BR | 1075.3397216796875 | 680.20751953125 |
| BL | 595.5665893554688 | 680.201171875 |
| TM | 835.48046875 | 412.81207275390625 |
| RM | 1067.85302734375 | 546.5565795898438 |
| BM | 831.5167236328125 | 679.70220947265625 |
| LM | 601.0888061523438 | 544.46343994140625 |
| TL-H | 692.2182006835938 | 411.7046203613281 |
| TL-V | 606.3240966796875 | 460.2456359863281 |
| TR-H | 979.3407592773438 | 409.651611328125 |
| TR-V | 1063.0616455078125 | 461.0199890136719 |
| BR-H | 986.656494140625 | 679.909912109375 |
| BR-V | 1072.64453125 | 632.0932006835938 |
| BL-H | 677.22491455078125 | 680.10137939453125 |
| BL-V | 598.7528076171875 | 630.08740234375 |

Rounded LCD corners: TL=12, TR=12, BR=0, BL=0 source pixels. Power anchor remains (1014,703).

`bezelReference` is stored untouched alongside the fitted controls; it is **workshop diagnostic only**. The live LCD uses the 16 FIT controls, not the separately traced gold reference.

## Changes in runtime

- `LaptopCinematicSequence` loads the V4 Resources TextAsset first. A validation check enforces the declared format, source-photo dimensions, convex outer corners, reasonable radii, four edge offsets, eight local tangent offsets and an unfolded 24×24 UV sample grid.
- Failure to validate V4 falls back to the unchanged prior **V2** resource. If V2 is also missing/invalid, the legacy path remains.
- The V4 points are already the exact final inner LCD coordinates. **Do not reapply old 32/38/5px legacy insets.**
- `LaptopPerspectiveQuad` uses exactly the workshop's V3 mid-edge basis and V4 local smooth tangent basis in source photo UV. It builds a **10-ring tessellated mesh**, including the source's true rounded corners, for YOMI texture, backdrop, standby mask, live clock, wake glow and exact YOMI projection. `LaptopWakeQuad` inherits the same geometry.
- The rectangular bounding `RectMask2D` / click target now encloses the entire warped photo UV mesh, with a conservative 2-source-pixel allowance only for V4. This avoids a 3px clip above the top bezel near TR-H.
- Buttons R/E/ESC, golden art/hover/effects, cinematic camera, hands, background art, combat and VN are untouched.

## Tests / verification

- `Assets/Rokas/Tests/EditMode/LaptopScreenCalibrationV4EditModeTests.cs`: asserts 16 exact input coordinates, radius values, tangent UV mapping, the 281-vertex / 1596-index rounded LCD mesh and V2 fallbacks.
- Offline geometry validation: the warped grid uses all controls exactly and does not fold (minimum positive signed UV cell cross).
- The project's GitHub Unity CI may still fail on separate Codex combat WIP, historically `CombatShotBounds` and `CombatShotBoundsProjection`. Report actual result; do not treat static checks as screenshot/PlayMode signoff.
- The user's canonical Unity copy remains `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`. When Codex is idle, use the existing `ROKAS_SAFE_MERGE_SYNC_V2.bat` workflow (preserving local untracked files, stashes and WIP) to sync; never blind-reset.
- Verify Game View **OFF chibi**, **power-on**, **YOMI fully loaded** and **sit/stand/return** for gaps, stencil clipping, exact image fit and golden prompt alignment. Send screenshots to review.

## Rollback

Non-destructive: remove or rename ONLY `screen_calibration_v4.json` and its Unity meta file in a controlled commit; the reader will automatically select the unchanged V2 JSON. No destructive Git reset is needed.
