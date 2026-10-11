# ROKAS Laptop Cinematic V11.6 — optical baseline, asymmetric left LCD inset, soft return

**Date:** 2026-10-11. User references: four Game View screenshots showing the correct lower R — ВСТАТЬ placement, E/ESC hovering above it, left-side LCD graphics bleeding over the bezel, and laptop/POV popping slightly during seated/stand transitions.

## Limited-scope implementation

Changes are restricted to `Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs` and laptop-only `Assets/Rokas/Tests/PlayMode/LaptopV111SeamlessPOVPlayModeTests.cs`. Nothing in Combat, VN, user PNG art, 49 hand PNG frames, approved room art, sound sources or the native YOMI desktop was modified.

1. **Keep the approved R art exactly where it was.** The earlier code created original `R — ВСТАТЬ` at y=883 and then rebased its texture rectangle to `bottom=1045`, causing confusion about its actual final position. V11.6 creates it directly at that final position: `y = 1045 - (438 * texture.height / texture.width)`. There is no subsequent re-baselining of R.
2. **Align E/ESC to visible PNG artwork, not transparent image bounds.** The original user PNGs have different alpha padding, so setting each RectTransform to the same bottom visibly misaligns the gold outlines. Use current R bottom as the source of truth; `E — ОТКРЫТЬ` and OFF `E — ВКЛЮЧИТЬ` receive a +19 stage-pixel *optical* downward offset and `ESC — НАЗАД` receives +13. These adjust the visually rendered buttons without changing their textures, width, hover, glow, or R's placement.
3. **Only inset the left LCD edge.** Source-picture polygon corners are now `topLeftX+15`, `bottomLeftX+18`; right corners remain `topRightX-5` and `bottomRightX-5`, with top/bottom 5 px margins as before. Both ON-screen YOMI and OFF chibi share this same calibrated stencil.
4. **Smoother sit/stand fades.** Return duration is 0.84 s rather than 0.72 s; first visibility easing starts at t=.78 (previously 1.15) so the approved POV blends with its live room for a longer portion of the transition. A quintic smootherstep drives both opacity and camera-return progress (zero end derivatives).
5. **No early button/LCD pop on stand.** The `BackToRoom()` callback leaves the existing powered LCD and original prompt graphics active; they fade with the POV rather than being `SetActive(false)` immediately. Raycast blocking is disabled upon start of return to avoid clicking hidden controls. The already implemented V11.5 powered LCD persistence remains in place, and 49-frame Power hand timing is unchanged.

## Verification

- Static current-branch code checks: 8/8 passed for R placement, asymmetric stencil, optical offsets, fade curve, no immediate hide and matching PlayMode assertions.
- Regression PlayMode now asserts approved R bottom=1045, E/ESC optical offsets, and that both the powered LCD and POV are visible at the start and during the return fade.
- [Unity integration workflow](https://github.com/timurkamurka11/Rokas/actions/runs/38130773056)
- [Unity EditMode / PlayMode workflow](https://github.com/timurkamurka11/Rokas/actions/runs/38130773065)

**Known independent CI blocker:** The main branch contains Codex's WIP combat commit `e756257`; Unity compilation previously failed with `CS0246` for `CombatShotBounds` / `CombatShotBoundsProjection` in `ReactiveCombatArena.cs`. No unauthorized combat edits or fabricated classes were made. Do not claim runtime Unity PASS for V11.6 unless a new successful test run actually confirms it.

**Real QHD Game View:** exact left-bezel fit and alpha-padding optical alignment remain pending local Windows visual confirmation after safely syncing the canonical working copy `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`. Check Codex's uncommitted work and branch divergence before updating, and never use destructive Git operations.
