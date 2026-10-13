# ROKAS Hub Dust V2 — visible, photo-registered motes

## Root cause found after user's Unity screenshot

V1 had 192 particles at only 1.6px / 4.4px / 13.5px world-size and
the central micro dust was multiplied by alpha=.090 * illumination.
The effective brightest particles blended at ~1% opacity and were
mostly smaller than one pixel at 0.7x Game View. They were essentially
invisible in the full 1920x1080 photograph. The hallway spotlight was
misregistered at photo x355 on the LEFT, even though in ROKAS the
corridor door is on the RIGHT (HomeView DoorHotspot x1748).

## Corrective changes

- 320 bounded particles (208 micro, 80 mid soft motes, 32 foreground bokeh).
- Billboard sizes now 3.5px / 8.2px / 18px before seeded per-mote scaling.
- Alpha controlled by actual light participation and capped at .34
  rather than an imperceptible subpixel 0.004 glow. Dark corners stay dark.
- Hard-grounded spotlight coordinates, from HomeView.cs:
  floor lamp hotspot (138.5,400), desk lamp hotspot (1151,194.9),
  window (696.6,128.8), exit door (1748,132.9).
  Broad particle-response Gaussians are centred below hotspots to represent
  lit AIR near the floor lamp, workbench and corridor opening.
- A steady low-level cool window response works independently of lamp switches.
- Right hallway becomes bright only when session.HallwayLightOn.
- Three visual forms stay procedural in the UI shader; the user's supplied
  original 3D particle package remains a reference (it cannot be inserted
  directly into the already-existing ScreenSpaceOverlay home composition).
- The shader and graphics layer stay BELOW interactive home hotspots.
- A one-time Unity PlayMode log reports number of visible motes:
  [ROKAS HubDust V2] rendering <N> motes; room=... hallway=...
- Read-only visibility diagnostic: HubDustCanvasGraphic.LastVisibleMotes
- Unit tests assert light-dependent actual alpha is meaningfully above 0
  near each of the four real visual sources and low in a dark corner.

## Local verification

1. Close Unity, run ROKAS_SAFE_MERGE_SYNC_V2.bat in your existing working copy.
2. Confirm Git HEAD contains the V2 commit, reopen Unity and Play.
3. In Hierarchy:
   Rokas/Rokas/RokasCanvas/AuthoredStage/HomeCameraRig2_5D/
   HubDust_ThreeLayerAtmosphere
4. Check Console for "[ROKAS HubDust V2] rendering ... motes" on the first
   visible frame. If missing, check shader compilation and Console errors.
5. Turn room lamp ON/OFF; dust near floor lamp and workbench must react.
   In the dark room some cool particles remain near the city window.
6. Toggle the hallway light: bright motes only near the RIGHT doorway.
7. Check no snowstorm, no UI click blocking, no laptop V11 visual changes.

Full Unity CI is separately blocked by pre-existing missing
CombatShotBounds / CombatShotBoundsProjection in ReactiveCombatArena.cs;
the small GitHub source contract cannot certify actual GPU appearance.
