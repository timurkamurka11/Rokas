# ROKAS Laptop LCD Workshop V3 — 8 contact points (Editor-only)

## Why
The first two workshop iterations could calibrate **only four LCD corners**. The photographed laptop has small irregular edge deviations, and the user wants clear intermediate contact markers to refine the glass fit before finalizing the game.

## OPEN
1. After the local Codex agent stops editing files, safely sync the canonical Unity working copy `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` using the already-established safe sync workflow.
2. Unity menu **ROKAS → Laptop → Screen Calibration Workshop**.
3. Press **Reset to approved V2** if you want to start directly from the currently approved 2026-10-11 calibration (2) JSON, instead of an older open editor state.
4. Make your usual TL/TR/BR/BL placement and per-corner radii adjustments.
5. Tick **Enable 4 edge pins** to show four new magenta handles, exactly on each existing edge at first:
   - **TM**: top midpoint; **RM**: right midpoint; **BM**: bottom midpoint; **LM**: left midpoint.
   - Drag these pins to the real INNER EDGE of the LCD glass. The preview YOMI texture, UV grid, black screen, mask, and green rounded outline use the SAME curved mapping. Straight-edge reference remains available via **Show outer polygon**.
   - Corner handles still define the four actual corner locations; midpoints are stored relative to those corners, so dragging a corner preserves the nearby bend.
6. For precision: **12× Zoom**, **Zoom selected (5x)**, mouse wheel, Alt+drag or middle mouse panning, direct numerical X/Y, arrow keys ±1 original-image pixel, Shift+arrow ±10px. The **25 / 50 / 75% markers** provide extra visual landmarks along each curved side without increasing control complexity.
7. **Show curved edge lines** draws a magenta deformation line; **Show outer polygon** remains the original gold straight-sided reference; **Show rounded LCD** is the green actually visible masked border. Toggle preview opacity to inspect the bezel under the screen.
8. Export via **SAVE / EXPORT V3 JSON...** and attach the result to ChatGPT for review and future V3 runtime integration. If you disable **Enable 4 edge pins**, the unchanged corner-only V2 export remains available via **SAVE / EXPORT V2 JSON...**.

## V3 schema

Extends all V2 fields and `formatVersion = 3` with:
```json
{
  "enableEdgeControlPoints": true,
  "topMidX": 0.0,
  "topMidY": 0.0,
  "rightMidX": 0.0,
  "rightMidY": 0.0,
  "bottomMidX": 0.0,
  "bottomMidY": 0.0,
  "leftMidX": 0.0,
  "leftMidY": 0.0
}
```
Coordinates here are SOURCE PHOTO PIXELS and reflect editable contact points; the zero values above are **field examples only**, not valid LCD calibration points. On fresh load, each midpoint starts at the average of its two side corners and therefore has zero bend.

Mathematically, a physical point at normalized UV (u,v) is the base four-corner bilinear interpolation plus:
```
4u(1-u) * lerp(topMidOffset, bottomMidOffset, v)
+ 4v(1-v) * lerp(leftMidOffset, rightMidOffset, u)
```
`topMidOffset = topMid - (TL+TR)/2`, etc. This maps exactly through all eight handles, leaves original four corners unchanged, and uses the SAME function for the original YOMI/guide UV preview and the curved mask outline. Renderer uses 10 radial ring subdivisions to keep YOMI preview texture aligned with the guides. Validation rejects corners outside the original image, invalid/folding UV grids, degenerate quads, extreme midpoints, and illegal rounded radii. Bad V3 loading restores previous hand-edited corners **and** edge offsets.

## Scope / non-destructive contract

- **The currently approved calibration (2) V2 is ALREADY installed as the live Unity Resources JSON**, with BR (1075.6923828125, 680.4259033203125). Existing runtime rounded `LaptopPerspectiveQuad` uses the approved **four corners only**.
- **V3 with extra midpoint warping is experimental in the Editor workshop only.** It does *not* automatically alter the game. Once approved, implement a separate V3 runtime mapper/stencil and adjust tests; do **not** copy V3 directly over `Assets/Rokas/Resources/LaptopCinematic/screen_calibration_v2.json`.
- Do not touch golden prompts, YOMI original screenshot, POV/hand animations, combat Codex WIP, VN, or original room imagery.
