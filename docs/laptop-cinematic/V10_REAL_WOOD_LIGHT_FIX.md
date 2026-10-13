# ROKAS V10 — Correcting the missing screen reflection (13 Oct 2026)

## Root cause established by GitHub Actions mask probe
- V5 red channel on the original 1672x941 photographic POV:
  (824,718) wood R=0; (824,842) R=.745;
  (824,875) R=.784; (824,910) R=.627.
- V6 optical masks: (824,875) G=.871, but zero at Y718.
- V9 shader vignette was centered at (824,718) and faded nearly
  completely by Y875, so **light was calculated where wood masks were zero**.
  The extra V9 color compression and dark grain response made it invisible.
- A previous automated EditMode test accidentally enforced that invalid
  geometry by requiring illumination <.05 at Y910.

## V10 changes
- Two distinct receivers: real wood centered near Y855 and a tiny keyboard
  lobe near Y745. Both reuse the original V5/V6 packed masks and the approved
  photograph; zero light outside masked geometry, no universal blue overlay.
- Wood falloff stays active at Y842..910, then fades to ~0.035 by Y940.
- Black LCD has zero perceptual light energy (amount=.10 baseline).
  Normal YOMI screenshot pixels produce a visibly brighter wood reflection.
- Grain/albedo come from BOTH approved photographic light-reference images;
  discrete glints from the real V6 green channel show up at Y875.
- Preserve room/hallway switches, Q power, side bias, original gold UI.
- EditMode unit tests corrected for the **real** mask coordinates.
- A standalone GitHub Actions gate checks numeric intersection between
  real JPEG/PNG mask values and falloff independently of Unity compilation.

## Acceptance
- ON + room OFF: visible wood reflection in front of the laptop, bright
  glints on natural grain (not a cyan band across the full lower screen).
- YOMI dark/warm/blue content: hue and strength follow on-screen pixels.
- ON + room ON and laptop OFF: screen bounce effectively zero.
- Existing CombatShotBounds compiler failures can still prevent full
  Unity CI; graphical Unity screenshot acceptance is not automatic.
