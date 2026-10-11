# ROKAS Laptop LCD Calibration Workshop V4 — 16-point corner fit

Editor-only update dated 2026-10-11. The approved, LIVE laptop continues to consume **V2** (`screen_calibration_v2.json`). Neither the runtime nor golden buttons, YOMI assets, cinematic animations, combat or VN were altered by V4.

## Open
After Codex has stopped writing to the canonical Windows project, sync safely, launch Unity and use **ROKAS → Laptop → Screen Calibration Workshop**. **Reset to approved V2** starts with the latest approved 1672×941-pixel laptop screen geometry.

## Exact workflow
1. Enable **4 edge pins** for V3 midpoints **TM / RM / BM / LM**. Enable **8 corner helpers** for V4. They are localized offsets at 18% of each edge near each physical corner and deform the mask itself (not decorative markers).
2. Drag the main **TL / TR / BR / BL** handles to the bounding corners. Then use **TL-H / TL-V, TR-H / TR-V, BR-H / BR-V, BL-H / BL-V** to match the curved edges near each corner. H controls the horizontal top/bottom edge; V controls the vertical left/right edge. The four main corners and edge midpoints are preserved while an adjacent helper moves.
3. Use **Show rounded LCD**, **Show outer polygon**, **Inner safe contour** (0–12 source pixels), and **25 / 50 / 75% markers**. The latter are small visual tick marks, not extra draggable controls. Wheel zoom and the zoom slider go to 12×; **Zoom selected (5x)** and **Zoom 12x** focus on the chosen handle. Arrow keys adjust by 1 source pixel, Shift+arrows by 10 pixels, and numeric fields can accept finer values.
4. **Corner Magnifier:** select any handle and enable **Show selected detail**. Use the 4×, 8×, 12× or 20× loupe to view the *original background photograph* with the current screen (cyan) and traced bezel reference (gold) drawn over it.
5. To obtain a meaningful **Difference / Heatmap**, click **Start tracing bezel reference**. This takes a SEPARATE snapshot of the currently fitted mask and switches to Outline mode (so the photo is not obscured). Trace the genuine inner bezel contour from the photo by moving its gold reference handles. The blue screen mask stays unchanged in this mode.
6. Click **Back to FIT MASK** to resume changing the blue mask. Enable **Difference / heatmap**. Against the individually traced gold reference:
   - **Red** indicates the screen edge lies *outside* that trace.
   - **Cyan** indicates the screen edge lies *inside* it, leaving a gap.
   - **Green** means the edge deviation is within the adjustable **Green tolerance** (default 1.5 source-image px).
   - Short connecting tick marks make differences more visible.
   - **Inner safe contour** is an additional inward offset of the current fitted boundary.
7. Optional **Snap selected pin to traced reference** is a manual, per-point action available only in **FIT MASK** mode after tracing. It displays the source-pixel distance to the corresponding gold reference pin, then moves only the selected fit point when clicked. If the distance is above 20px, it requires explicit confirmation. It never auto-moves all 16 points or writes to the game.
8. Once satisfactory, click **SAVE / EXPORT V4 JSON...** and send that file to ChatGPT. Both your 16-point calibrated screen shape and, when present, the independent manually traced reference are serialized for review.

## CRITICAL ACCURACY LIMITATION
The editor **does not automatically know the real glass/bezel boundary** and does not claim image-based edge detection. Without a manually traced gold reference, red/cyan error colors cannot truthfully indicate physical bezel fit. Creating a reference simply copies the current fit as a STARTING POINT. YOU must align the gold reference to visible photographic LCD glass in trace mode before trusting a green result. The green result means agreement with YOUR traced boundary, not independent proof of perfection.

## 16 handle controls

| Handle(s) | Effect |
|---|---|
| TL, TR, BR, BL | Four structural vertices |
| TM, RM, BM, LM | Smooth mid-edge deformations |
| TL-H, TL-V | Upper-left local top/left transition |
| TR-H, TR-V | Upper-right local top/right transition |
| BR-H, BR-V | Lower-right local bottom/right transition |
| BL-H, BL-V | Lower-left local bottom/left transition |

UV mapping: V2 bilinear quad + V3 midpoint displacement `4u(1-u)`/`4v(1-v)` + V4 localized corner helper basis around u,v=0.18/0.82. Endpoints and midpoints are unchanged by moving a V4 local helper. A dense tessellated UV render target drives the original YOMI screenshot, checker grid, black/mask preview, and colored outline. Export validation rejects folded polygon geometry and excessively displaced helper points.

## V4 export and backward compatibility
`formatVersion: 4` contains all V2/V3 fields plus `enableCornerHelpers`, 16 numeric fields `tlHx/tlHy/tlVx/tlVy`, `trHx/trHy/trVx/trVy`, `brHx/brHy/brVx/brVy`, `blHx/blHy/blVx/blVy`. Optional `bezelReference` contains a `corners` array (4 `Vector2`), a `midpointOffsets` array (4), a `tangentOffsets` array (8), and `useMidpoints/useTangents` flags.

Old V1/V2/V3 files are loadable, and corrupted or folded imported geometry must be rejected without destroying the current hand-edited state. Export is standalone and cannot overwrite the production Resources calibration automatically.

## Scope and verification
Editor source: `Assets/Rokas/Editor/LaptopScreenCalibrationWorkshop.cs`.

The cloud Unity workflow may fail from existing Combat WIP missing `CombatShotBounds` and `CombatShotBoundsProjection` in `ReactiveCombatArena.cs`. Do not claim PlayMode/visual verification if that happens. User's local screenshot of all 16 handles, heatmap reference and JSON export is the final usability acceptance.

