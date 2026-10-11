# ROKAS Laptop Cinematic V11.9 — owner-calibrated LCD V2 in runtime

Date: 2026-10-11
Current source: latest attachment `ROKAS_Laptop_LCD_Calibration(2).json` (supersedes calibration (1) and the original) (formatVersion 2; source image 1672×941; source-image pixels, origin top-left).

## Applied precisely as delivered (refined owner calibration, 2026-10-11)

| Point | X | Y |
|---|---:|---:|
| TL | 608.948974609375 | 412.8598937988281 |
| TR | 1059.767578125 | 412.5315246582031 |
| BR | 1075.6923828125 | 680.4259033203125 |
| BL | 594.830078125 | 680.13330078125 |

Per-corner rounded radii: TL 18; TR 18; BR 6; BL 6 (original-image pixels). Power anchor: 1014, 703. `debugPowerAnchor=false`.

- Owner file was copied with all 20 JSON fields and exact numeric values, under `Assets/Rokas/Resources/LaptopCinematic/screen_calibration_v2.json` (with stable Unity `.meta`).
- The runtime now reads and validates this **new Resources TextAsset first**: format 2, coordinate space, original photo dimensions, convex / non-crossed polygon and finite legal radii. If absent/invalid, the previous legacy config/defaults and insets are retained as a safe fallback, with error logging.
- **Critical:** the final four owner points are DIRECT final inner-LCD points. Do not apply older `32/38/5` safe insets a second time. The old additive insets remain only for legacy/no-V2 fallback.
- `LaptopPerspectiveQuad` has a true 52-edge rounded UV mesh with 18/18/6/6; it matches the UV arc and bilinear mapping algorithm used by the Workshop. `Mask` receives the same rounded mesh (real stencil), so children YOMI and chibi are clipped to a curved inner screen rather than just seeing a fake rounded decoration.
- `LaptopWakeQuad` inherits the SAME mesh generator. Wake glow, physical wallpaper, live desktop clock, both standby/powered mask stencils, and exact original fullscreen-YOMI screenshot share the same calibrated source corners and rounded radii. The original bitmap, physical YOMI snapshot and UI assets are unchanged.
- Latest revision (calibration (2)) changes ONLY the V2 bottom-right corner and corresponding test constants; rounded radii, Power anchor and runtime renderer remain unchanged. Calibration (1) is superseded. Optional workshop V3 edge pins do not change runtime until separately approved.
- Existing POV/seat/stand animations, approved golden controls R/E/ESC (including lower placement, icon art, hover, sound), Combat, VN and room background are not modified.

## Tests

- `Assets/Rokas/Tests/EditMode/LaptopScreenCalibrationV2EditModeTests.cs` verifies V2 coordinates/radii exactly, rounded center-fan mesh 53 vertices / 156 triangle indices, old 4-vertex non-rounded fallback, and same wake/clock mesh parent.
- Source schema and all 20 values checked against the uploaded original; polygon cross products positive and radii feasible.
- WARNING: historical Unity CI on this branch intermittently fails compiling Codex combat WIP `ReactiveCombatArena.cs` due unresolved `CombatShotBounds` and `CombatShotBoundsProjection`. Check new CI runs rather than asserting success without logs.

## Acceptance in local Windows canonical Unity copy

After Codex is idle and local Git changes are safely preserved, run the existing `ROKAS_SAFE_MERGE_SYNC_V2.bat` (do not blind-pull during a merge) for `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`. Open laptop in Game View: inspect corners and bezel fit in both standby chibi and powered/YOMI, no left/right black gaps, correct rounded corners, no tinted or cropped icons. Confirm stand/reseat screen continuity. A real local screenshot and full PlayMode checks are still required for pixel-perfect signoff.
