# ROKAS Hub Dust — 2.5D production integration

Source: user-supplied "RokasHubDust_Package_v1.0(1).zip".
Adaptation target: the actual Unity 6000.3.19f1 **Built-in** project.
The supplied package is designed for a 3D world-space Shuriken volume.
ROKAS HOME, however, is an authored 2.5D photograph under
ScreenSpaceOverlay Canvas. Placing its 3D prefab in that scene would not
render above the room illustration. We therefore implemented a deterministic,
three-depth-layer uGUI particle renderer that uses the original package's
65%/25%/10% depth distribution, subtle drift, bokeh/fiber morphology,
and Dust-in-Light response, with a single UI draw call and no new packages.

Runtime:
* GameObject: HomeCameraRig2_5D/HubDust_ThreeLayerAtmosphere
* Behaviors: HubDustCanvasGraphic on the same 1920x1080 photo
* Shader: Resources/Shaders/HubDustSoftMotes (UI Built-in, procedural 3 shapes)
* Data: independent room/hall lighting does NOT exist. Instead, the same
  session.State.lampOn + session.HallwayLightOn drive particles.
* Invisible on non-home scenes, hallway transition, cinematic modal, and
  other panels; never blocks clicks or covers right-side button UI.
* Prefab not required, no scene rewrite or inspector actions. Runtime adds it
  just after the background in RokasView.

The full 3D Shuriken package is intentionally not instantiated in
ScreenSpaceOverlay; its collider-free light-shaft volume is not valid for the
2D composition without a rendering bridge. Source ZIP remains the user's
reference for future actual world-space 3D rooms.

Tests: HubDustCanvasEditModeTests checks shader availability, scene-light
responses, zero raycasts, three-layer bounded particle budget, and hide/show.
No Unity graphical test claimed until cloud tests are completed.
