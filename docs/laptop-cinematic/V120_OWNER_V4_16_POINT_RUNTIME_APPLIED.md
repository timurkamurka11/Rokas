# ROKAS Laptop Cinematic — owner-approved V4 applied to runtime

## Source of truth

Owner supplied `ROKAS_Laptop_LCD_Calibration(5).json` (11 October 2026). The **entire** JSON is installed in `Assets/Rokas/Resources/LaptopCinematic/screen_calibration_v4.json`, imported with `screen_calibration_v4.json.meta`. Dimensions are **1672×941 source-photo pixels**, origin top left. Values must not be recomputed from a Game View screenshot.

| Control | X | Y |
|---|---:|---:|
| TL | 608.948974609375 | 412.8598937988281 |
| TR | 1059.767578125 | 412.5315246582031 |
| BR | 1075.6923828125 | 680.4259033203125 |
| BL | 594.830078125 | 680.13330078125 |
| TM | 834.3582763671875 | 412.6957092285156 |
| RM | 1067.72998046875 | 546.4786987304688 |
| BM | 835.26123046875 | 680.2796020507812 |
| LM | 601.4862670898438 | 546.4616088867188 |
| TL-H | undefined | undefined |
| TL-V | undefined | undefined |
| TR-H | undefined | undefined |
| TR-V | undefined | undefined |
| BR-H | undefined | undefined |
| BR-V | undefined | undefined |
| BL-H | undefined | undefined |
| BL-V | undefined | undefined |

Rounded LCD corners: TL=14, TR=14, BR=0, BL=0 source pixels. Power anchor remains (1014,703).

`bezelReference` is stored untouched alongside the fitted controls; it is **workshop diagnostic only**. The live LCD uses the 16 FIT controls, not the separately traced gold reference.

## Changes in runtime

- `LaptopCinematicSequence` loads the V4 Resources TextAsset first. A validation check enforces the declared format, source-photo dimensions, convex outer corners, reasonable radii, four edge offsets, eight local tangent offsets and an unfolded 24×24 UV sample grid.
- Failure to validate V4 falls back to the unchanged prior **V2** resource. If V2 is also missing/invalid, the legacy path remains.
- The V4 points are already the exact final inner LCD coordinates. **Do not reapply old 32/38/5px legacy insets.**
- `LaptopPerspectiveQuad` uses exactly the workshop's V3 mid-edge basis and V4 local smooth tangent basis in source photo UV. It builds a **10-ring tessellated mesh**, including the source's true rounded corners, for YOMI texture, backdrop, standby mask, live clock, wake glow and exact YOMI projection. `LaptopWakeQuad` inherits the same geometry.
- The rectangular bounding `RectMask2D` / click target now encloses the entire warped photo UV mesh, with a conservative 2-source-pixel allowance only for V4. This avoids a 3px clip above the top bezel near TR-H.
- R and E prompts remain unchanged. Only the golden `ESC — НАЗАД` PNG is lifted by 8 stage pixels (optical baseline correction `+13` → `+5`) so all three prompts visually align. Golden source art, hover/effects, cinematic camera, hands, background art, combat and VN remain untouched.

## Tests / verification

- `Assets/Rokas/Tests/EditMode/LaptopScreenCalibrationV4EditModeTests.cs`: asserts 16 exact input coordinates, radius values, tangent UV mapping, the 281-vertex / 1596-index rounded LCD mesh and V2 fallbacks.
- Offline geometry validation: the warped grid uses all controls exactly and does not fold (minimum positive signed UV cell cross).
- The project's GitHub Unity CI may still fail on separate Codex combat WIP, historically `CombatShotBounds` and `CombatShotBoundsProjection`. Report actual result; do not treat static checks as screenshot/PlayMode signoff.
- The user's canonical Unity copy remains `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`. When Codex is idle, use the existing `ROKAS_SAFE_MERGE_SYNC_V2.bat` workflow (preserving local untracked files, stashes and WIP) to sync; never blind-reset.
- Verify Game View **OFF chibi**, **power-on**, **YOMI fully loaded** and **sit/stand/return** for gaps, stencil clipping, exact image fit and golden prompt alignment. Send screenshots to review.

## Rollback

Non-destructive: remove or rename ONLY `screen_calibration_v4.json` and its Unity meta file in a controlled commit; the reader will automatically select the unchanged V2 JSON. No destructive Git reset is needed.
