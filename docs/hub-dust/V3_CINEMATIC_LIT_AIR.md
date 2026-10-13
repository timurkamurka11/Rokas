# ROKAS HubDust V3 — Cinematic Lit-Air Overhaul

## Evidence and source audit
* Reviewed user's 23-second Unity gameplay capture (14 Oct, 01:12).
  At reduced GameView scale V2 dust barely registers, despite bright lamp
  art and the running layer. Increase perceptual visibility in light only.
* Re-inspected original RokasHubDust_Package_v1.0(2).zip:
  original 4x2 RGBA 1024x512 atlas (8 distinct guarded shapes,
  all edge alpha = 0), 65% micro / 25% ambient / 10% near bokeh,
  3-octave convective drift, photometric light shafts, depth/near fade,
  soft particle shape and performance guidance.
* Existing ROKAS hub: Unity 6.3 Built-in RP, uGUI
  ScreenSpaceOverlay 1920x1080 (NOT a depth-buffer 3D scene).
  Existing Shuriken 3D prefab cannot replace the UI particle layer
  without an explicit depth/renderer bridge. Avoid broken 3D shadows
  or adding a second Camera.

## V3 implementation
* 432 bounded motes / one UI mesh (281 micro, 108 middle, 43 near bokeh).
* 8 unique shape profiles ported from the package's atlas generator into
  the shader (one soft texture-equivalent guarded cell per mote, no
  hard-edged squares and no external 3D/URP dependencies).
* Seeded distribution: approx 27% window light, 21% floor lamp,
  21% workbench, 14% right hallway, remainder naturally distributed.
* 3 gentle low-frequency drift waves, slow buoyant rise, temporal
  'breathing', transparent edge crossing, capped opacity.
* Runtime-tunable Inspector fields on HubDust_ThreeLayerAtmosphere:
  visibilityGain = 1.38 (range .5–2.0)
  particleScale = 1.12 (range .5–1.6).
  These apply without changing repository code for local art approval.
* Dust response to real session.State.lampOn and
  session.HallwayLightOn remains unchanged.
* UI interaction untouched, raycastTarget=false; no YOMI V11 or combat
  behavior edits. Full-frame weather / rain layers untouched.

## Important limitations
* The 8 shapes are mathematically ported from the supplied archive to
  the existing Built-in uGUI shader; the original atlas PNG and full
  three-dimensional Shuriken system are NOT silently installed.
* The UI scene does not have usable per-pixel 3D depth; these are
  authored 2.5D light volumes rather than physically exact Mie + 3D
  soft-particle shadows.
* GitHub source-contract checks are structural/analytical, not a Unity
  GPU screenshot. Test live PlayMode on the target PC.
* Unity CI has a known unrelated CombatShotBounds and
  CombatShotBoundsProjection compiler blocker.

## Local test
1. Close Unity and run ROKAS_SAFE_MERGE_SYNC_V2.bat.
2. Reopen the main ROKAS scene, Play, stay at Home/MainRoom.
3. In Hierarchy find HomeCameraRig2_5D / HubDust_ThreeLayerAtmosphere.
4. Expected one-time log: [ROKAS HubDust V3] rendering N motes ...
5. Inspect floating particles near window, floor lantern, workbench
   and right door. Switch room lamp / corridor light separately.
6. If desired, adjust Visibility Gain 1.38->1.6 live in Inspector.
   Keep lower for a subtle mode; never adjust all HUD alpha values.
