# ROKAS — Cinematic Screen Bloom V6

## Visual design
Designed specifically for the 1672x941 approved photographic POV inside
the Screen Space - Overlay canvas. **Do not add Unity camera post-processing**
or replace the calibrated physical YOMI mini-desktop.

- Preserve the existing actual ON/OFF photographic room + hallway mixing.
- Preserve the existing `HybridScreenBounceMasks.png` (wood, keys, lip, metal).
- New `HybridCinematicBloomMasks.png` (256x144, packed RGBA linear data):
  - R = broad local screen halo on wood, with feathered nonuniform shape.
  - G = narrow glossy wood streaks.
  - B = tabletop rim response.
  - A = stable low-frequency breakup modulation (no time-based flicker).
- Screen bounce still comes from the physical `LaptopPowerSession.PoweredOn`
  and the actual `LaptopYomiMirror.Current`, falling back to YOMI wallpaper.
- `LaptopScreenBounceEstimator` downscales 32x18, samples on GPU 4 times/s,
  computes mean screen tint + luma and an actual bright-pixel histogram using
  threshold 0.58 and soft knee 0.22. It sends independent diffuse,
  bloom and specular values through the existing material.
- UI transitions ease over 0.28s. No arbitrary pulsing, random animation,
  full-frame blur, neon on laptop metal, or added generic light assets.
- All three passes are gated by physical screen power and room OFF; hallway
  lighting remains independent.
- Final filmic shoulder compresses only added reflection; the underlying
  photographic textures remain untouched.

## Inspector debug
Select `UnmodifiedScreenOffPOV` at Play Mode.
- `_ScreenGlow`, `_ScreenBounceAmount`, `_ScreenBloomAmount`,
  `_ScreenSpecAmount`: should rise while powered, 0 at OFF after decay.
- `_BounceMasksTex` and `_BloomMasksTex` must both be non-null.
- `_MaskDebug = 1`: room/door zones. `_MaskDebug = 2`:
  optical mask preview (R = halo, G = specular, B = rim).
- `_BloomGain` and `_SpecularGain` are tunable 0..2, default 1.
- Room ON means exactly zero additional reflected light in the shader,
  regardless of estimator values. Room OFF + laptop OFF also zero.

## Acceptance
1. Room OFF, laptop ON, YOMI dark: mild wood bounce.
2. Switch YOMI to white page: more visible halo and streak, no flashing.
3. Room ON, laptop ON: no additional bloom or tinted wash.
4. Room OFF, laptop OFF: no bounce.
5. Room OFF, hallway ON: correct doorway mask + screen light independent.
6. Return from YOMI and stand/sit again: no disposed sampler errors.
7. No changes to V4 calibration, gold buttons, original photos, hands or combat.

## Known CI limitation
Old CI builds of this branch report missing `CombatShotBounds` and
`CombatShotBoundsProjection` from `ReactiveCombatArena.cs`. V6 intentionally
does not touch unrelated combat code. Full Unity Game View acceptance is
still required and must not be inferred from static or mask tests.
