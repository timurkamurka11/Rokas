# ROKAS — Hybrid Cinematic Lighting V5

## Implementation
- Existing warm `_MainTex` and locally installed approved dark `_OffTex` are unchanged.
- `DoorwayPolygon` and `ThresholdSpill` remain independent of screen illumination.
- `HybridScreenBounceMasks.png` is an **actual committed RGBA texture**, 256x144 in photo-aligned UV space:
  - R = wood diffuse reflectance
  - G = tiny keyboard highlight response
  - B = polished tabletop front-lip reflection
  - A = laptop metal occlusion (excludes metallic casing from wood reflection).
- `LaptopScreenBounceEstimator` reads 16x16 downsampled pixels from `LaptopYomiMirror.Current` (the actual display) every 0.25 seconds using nonblocking `AsyncGPUReadback`, or the existing wallpaper if no mirror is available. Tint is desaturated; intensity is bounded, transitions are eased.
- The existing `LaptopPowerSession.PoweredOn` is authoritative; the existing `session.State.lampOn` controls night suppression; hallway stays independent.
- No new toggle, new screen, lighting package dependency, ambient pseudo-flicker, video loop or external paid asset.
- Exactly room ON or laptop OFF: bounce equals zero; the two-photo logic still shows the correct photographic scene.
- The packed mask is an authored response texture aligned to the original 1672x941 composition, imported with sRGB OFF and texture compression OFF.
- If a mask is missing, the 2-zone lighting still works and screen bounce is disabled with a clear log error.

## Release tests
1. OFF / OFF / laptop OFF => **no** screen reflection.
2. OFF / OFF / laptop ON => weak wood-only specular bounce; aluminum remains dark.
3. ON / OFF / laptop ON => no added reflection tint.
4. OFF / ON / laptop ON => independent corridor lights and bounced LCD reflection.
5. Open light vs dark YOMI pages while seated; response should fade smoothly, not pulse on a timer.
6. Power ON startup and return from YOMI do not flash a bright wrong-state base frame.
7. Do NOT rewrite original supplied OFF photo, YOMI calibration V4 or prefab setup.

## CI caution
The previous GitHub Unity workflow failed on separate `ReactiveCombatArena.cs`
references to `CombatShotBounds` and `CombatShotBoundsProjection`. Those
combat files have intentionally not been modified by this light pass.
A passing visual Unity Game View inspection is still required before calling this final.

## V5.1 visible-reflection correction

Screenshot after initial V5 showed no visually perceptible change even after the successful BAT merge. Root causes identified from actual source: extremely low compounded shader coefficients (below visible 8-bit contrast) and a photo importer that forced the packed data texture to sRGB. V5.1 uses a linear texture import for LightMasks, stronger wood-only reflection and a photographic shoulder capped at 0.145 per RGB channel. OFF laptop / lit room still gives exact zero. Existing OFF/ON room and hallway photo blending is untouched. In Unity, if the packed mask was previously imported as sRGB, right-click HybridScreenBounceMasks.png and Reimport after updating the Editor script.
