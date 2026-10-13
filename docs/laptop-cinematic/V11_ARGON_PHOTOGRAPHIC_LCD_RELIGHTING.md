# ROKAS V11 Argon visual-lighting integration

Source: user's Argon analysis/patch for **old** commit 6dc2b8d.
Applied to current branch base 71a0078, preserving later V9/V10 improvements.
Unlike the submitted unified diff, paths are under Assets/Rokas/.

## Adopted
- emissive-luminance weighting: bright YOMI icons / lantern affect hue,
  intensity, bloom and side bias even against dark wallpaper;
- balanced chroma preserving neutral/amber content;
- registered ON/OFF photo-delta for actual timber, photo-derived grain;
- wider rectangular LCD emitter, *retaining V10 wood y855 alignment*;
- alpha channel of V5 photo mask illuminates physical aluminium case;
- keyboard and gloss mask pass independent of wood, filmic compression;
- existing room/corridor light switches, powered Q shutdown, active
  RenderTexture restore, hover/menu/camera/chibi/V4/YOMI unchanged.

## Not copied literally
The Argon patch targets y738 for its main lobe; V10 mask probes proved that
the real V5 timber receiver peaks around y842..910. Copying the old lobe
would yield an invisible reflection again. Old Argon screenshot math is
therefore re-centered on registered wood, using rectangular emitter width.
Argon's initial targetAmount=.46 / targetBloom=.26 was not adopted because
it could create phantom light before the first real GPU sample. V10's
black-screen .10 baseline remains and zeroes physical light.

## Validation
- Pure EditMode regression on emissive weighting, dark/warm/neutral hue,
  falloff on photographed timber, keyboard and edge, RenderTexture.active.
- Standalone GitHub Actions gate measures the saved V5 PNG pixels and verifies
  shader/estimator constants in the V11 lobe.
- Full Unity compilation / PlayMode may still be blocked by unrelated
  pre-existing errors in ReactiveCombatArena (CombatShotBounds types).
- Actual appearance must be checked on local screen in Unity. No unsupported
  claim that cloud-side numeric tests prove final photographic quality.
