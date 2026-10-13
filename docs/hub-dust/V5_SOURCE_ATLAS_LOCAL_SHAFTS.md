# ROKAS HubDust V5 FINAL – source-authored photographic air

## Root cause and verification
V4 PlayMode Console confirmed shader loaded and 426/432 vertices authored;
the statistic meanVertexAlpha=0.184 represented CPU vertices, NOT displayed
pixels. Prior versions used tiny soft silhouettes and then multiplied by
a low-alpha shader profile, so their visible contribution was weak at 0.7x.
The nine-factor double-shadow-crush in the user-supplied **3D URP** shader
is a separate implementation and was NOT the Built-in UI shader used in V4.
V5 nevertheless removes any possible double fading by using a SINGLE CPU
lighting-derived vertex alpha and true premultiplied output.

## Five key corrections
1. Shader: Blend One OneMinusSrcAlpha, out.rgb is premultiplied once
   (RGB = tint * outAlpha + a bounded emissive micro-core). Diffuse and
   halo are balanced; shape outside guarded region is still alpha=0.
   Hot core = exp(-radius²*18) * 1.6, plus broader optical halo.
2. Exact 1024x512 RGBA32 source-authored 4x2 atlas is procedurally built
   once using the eight mathematical shapes copied/adapted from
   RokasHubDust/Scripts/Editor/RokasHubDustEditorTools.cs in user's ZIP.
   Each tile 256x256, full 8-bit (not old Q32), Clamp + Bilinear, 0 alpha
   for radius>=.88. Uses NO 128x64 image and NO compressed PNG.
   Generated in Awake only; generation time is hardware-dependent.
3. CanvasRenderer: DustAtlasTex assigned on the material, mainTexture
   returns the same actual atlas, and UpdateMaterial forwards both
   SetTexture(atlas) and SetMaterial(materialForRendering,0). No MPB.
   MaskableGraphic maskable=false, local RectTransform coordinates, UI
   canvas ordering after the photo and before SceneInteractions, no clicks.
4. One mesh with shaft silhouettes 8/9 drawn BEFORE the three dust layers:
   a) window's cool volumetric ambient ribbons, b) lamp-controlled warm
   cones near floor lamp/workbench, c) corridor-controlled soft right
   doorway shaft. Never a frame-wide filter.
   Particle micro alpha under direct practical light targets 0.55-0.85,
   medium ~0.30-0.45, bokeh ~0.30-0.45 before scene gain.
5. Inspector mode enum in HubDustCanvasGraphic:
   Normal (0) – cinematic lighting.
   HighContrastBoost (1) – 2.5x opacity and a mild luminous core boost.
   DiagnosticTestPattern (2) – opaque neon-magenta motes and
   yellow-gold shaft outlines. If diagnostic invisible, inspect
   UI sibling ordering, RectTransform, active Canvas, and material.
   The diagnostics report vertex alpha, atlas size and mode; only a
   local Unity GPU render confirms visible output.

## Automated verification
Source gate checks real 1024x512 RGBA32 atlas generator, high-res source
shape cases and guards, custom shader and premultiplied blend, mainTexture
and SetTexture/SetMaterial dual binding, 3 modes, non-raycast draw order,
and regression tests. Unity compilation/PlayMode run separately and may
fail due to pre-existing CombatShotBounds issues outside this scope.

## Install & visual acceptance
Close Unity. Run existing ROKAS_SAFE_MERGE_SYNC_V2.bat (same working copy).
Confirm Git HEAD includes V5 FINAL commit. Play the main room and
observe window, floor lamp, workbench and right corridor door.
In Inspector select HubDust_ThreeLayerAtmosphere > HubDustCanvasGraphic.
Temporarily choose DiagnosticTestPattern: clear magenta shapes and golden
shaft rings MUST be visible. Then choose HighContrastBoost; if clear,
return to Normal. Toggle room/corridor light separately and check switch
mapping. Inspect Console "[ROKAS HubDust V5] ... atlas=1024x512".
Do not modify YOMI V11, the agreed gold HUD or combat.
