# ROKAS HubDust V5 — Atlas + localized lit-air fix

### Why the old patch looked unchanged
The user's Unity console confirms [HubDust V4] submitted=426/432,
meanVertexAlpha=.184 max=.554 strength=1.00 rect=1920x1080
shader=ROKAS/UI/HubDustSoftMotes. This verifies CPU-generated UI
geometry, not actual visible fragment contribution. Small diffuse
shapes on detailed photographed art may not be perceptible.
Prior V1–V4 never loaded the original user's dust atlas.

### V5 changes
* **Actual source atlas**, extracted from
  RokasHubDust_Package_v1.0(2).zip/Textures/T_RokasDustAtlas_4x2.png.
  All 8 original alpha shapes are preserved in an optimized 128x64
  grayscale source-alpha texture, with 32 quantization levels.
* Resource: Assets/Rokas/Resources/HubDust/
  T_RokasDustAtlas_4x2_Alpha.png
* Shader: eight cells sampled at top-left-first 4x2 UVs.
  Procedural shapes remain a fallback only if atlas cannot load.
* CRITICAL: MaskableGraphic.mainTexture now returns the atlas,
  because CanvasRenderer binds Graphic.mainTexture independently
  of any material texture assignment. WhiteTexture here would hide
  the actual source-alpha atlas, producing wrong silhouettes.
* Six soft **local** light ribbons/shafts beneath motes:
  cool city window (always), warm floor/desk (room lamp switch),
  right doorway (corridor switch). No fullscreen blue filter.
* 432 motes, 8 shapes, slow noise drift preserved. Mote radii raised
  slightly for 0.7x Unity GameView (8.2,17.4,39 authored pixels).
* Inspector: VisibilityGain 1.70, ParticleScale 1.34,
  ShaftStrength .90; DebugVisibility false.
  Turn DebugVisibility ON to draw unmistakable saturated magenta
  control pixels and green ribbons in PlayMode. OFF restores art.
  If even debug pixels are absent, examine Canvas draw order/material
  and screenshot, not alpha values.
* Logging [ROKAS HubDust V5] reports source atlas loaded/size and
  actual CPU submitted and shaft count; CPU counts are never
  represented as GPU-proven screen visibility.
* All room/hall switches, HomeFinishedUi, ROKAS V11 YOMI laptop,
  combat and other contexts left unchanged.

### Validation
GitHub source contract tests actual PNG IHDR, correct Resource path,
Graphic.mainTexture, shader sampling and DebugVisibility behavior.
Unity editor tests assert atlas resolution and shader support.
Source-contract success cannot replace a local GPU screenshot.
Earlier Unity CI was blocked on missing combat types or Docker issues.

### User verification
1. Close Unity and run ROKAS_SAFE_MERGE_SYNC_V2.bat.
2. Verify head is the V5 commit; reopen the HOME stage.
3. Console should show:
   [ROKAS HubDust V5] sourceAtlas=128x64 uiShader=ROKAS/UI/HubDustSoftMotes
   and [ROKAS HubDust V5] submitted=N/432 ... shafts=6 ...
4. Main room lit: warm airborne dust at floor and desk.
   Main off: warm dust fades, cool window remains.
   Corridor on/off: dust by RIGHT-hand door changes.
5. If not visible: select HubDust_ThreeLayerAtmosphere in
   HOME hierarchy, set Debug Visibility true and screenshot.
   Bright magenta motes = mesh is composited, tuning may be needed.
   No magenta = investigate Canvas/clipping/shader pipeline.
