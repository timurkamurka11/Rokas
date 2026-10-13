# ROKAS Hub Dust V4 – screenshot-driven visibility repair

Evidence from user Unity screenshot (14 Oct 2026):
- Console: `[ROKAS HubDust V3] rendering 418 motes; room=True hallway=True strength=0.28`.
- The 418 count means V3 authored quads, NOT that the GPU drew visible pixels.
- 0.28 startup strength, compounded by vertex alpha, individual low-opacity
  organic shape profile and 0.7x GameView scaling makes already tiny motes
  visually disappear on the first sampled frame.
- V3 particle default radii 4.2/10.8/25 authored pixels are small at 0.7x.

Repair:
1. The very first active HOME frame starts with visibility=1 instead of
   fading 0->1. Re-entering the home later retains normal fade.
2. Original 432 particle/8 shape/65-25-10 layout is kept.
3. Micro/soft/bokeh particle radii 6.5 / 14 / 31, particleScale=1.28,
   visibilityGain=1.62 to remain readable at 0.7x.
4. Soft shader profile uses exponent .72, preserving zero-alpha edges
   while better defining floating fibers without snow-white rectangles.
5. Console now logs only after strength >.95, and reports:
   submitted N / budget; meanVertexAlpha, maxVertexAlpha;
   rect width x height; shader name.
   These metrics are CPU-side visibility diagnostics, NOT proof
   of a composited GPU pixel.
6. Prior photo light zones, main/hall switch sync, UI layering, YOMI,
   and combat are not changed.

How to validate:
- Close Unity; run the user's ROKAS_SAFE_MERGE_SYNC_V2.bat;
  open local Unity and verify git HEAD includes V4 commit.
- Play home; watch visible motes around left bedside lamp,
  city window, workbench lamp and right corridor door.
- Look for [ROKAS HubDust V4] submitted=... meanVertexAlpha=...
  maxVertexAlpha=... strength=1.00 rect=1920x1080 shader=...
- If view still completely blank, send screenshot of BOTH
  Inspector selection HubDust_ThreeLayerAtmosphere and Console;
  inspect rect/material/shader vs actual GPU behavior before
  further visual tuning.
- V4 keeps 2D UI shader; the original physically-depth-tested
  3D Shuriken prefab from user's ZIP remains unsuitable for
  the existing ScreenSpaceOverlay unless introducing a
  specifically coordinated 3D-to-UI render bridge.

CI source test validates code contracts; full Unity graphics not
certified by that source test. Do not conflate 'submitted' with actual
visible pixels.
