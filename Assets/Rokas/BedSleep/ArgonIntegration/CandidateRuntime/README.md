# ROKAS Bed Sleep — Argon living hand + interactive smartwatch (candidate only)

This is a **real source-code runtime implementation**, not an encoded video effect.
DO NOT merge into production V10 until the cinematography is accepted and a
separate integration PlayMode gate verifies the active room/bed scene.

## Assets and geometry

Use the existing authentic Argon 25-bone skinned arm and seven real FBX watch
meshes in `ArgonCandidate/PF_Argon_Keiko_Hand_Watch_Preview.prefab`.
Camera and 2D layout remain unchanged: sleeve LEFT, watch CENTER-LEFT,
fingers RIGHT. No replacement mesh, generated watch, overlaid clock graphic,
new bed backdrop or changed save logic.

## New modules

- `ArgonLivingRig` -- pass in the instantiated source FBX prefab. After each
  sampled animation frame, use `Apply(timeSeconds, amount, isAdjusting,
  camera.right, camera.up)`. Adds sub-4-mm breathing drift, <1.5-degree wrist
  motion and unique PIP/DIP curvature to genuine finger bones without
  cumulative deformation.
- `ArgonWatchControls` -- add to the instance GameObject and initialize
  with the active FPS camera, real `SmartwatchOLEDPresenter` component,
  source `Watch_OLED_Display` and `Watch_DigitalCrown_Rotary` MeshRenderers:
  `controls.Initialize(camera, presenter, oledRenderer, crownRenderer)`.
  Supports mouse wheel over physical face/crown with both legacy and new Input
  Systems. A physical click confirms. For keyboard/gamepad/touch adapters use
  public `Step(int)`, `Confirm()`, `ResetConfirmation()` and
  `SetDurationSteps(int)`. The same gameplay OLED RenderTexture is updated,
  no fake text is printed.
- `ArgonWatchFoley` -- self-contained, original procedural PCM at 44.1 kHz:
  subtle cloth/friction, tactile crown double-click, and soft two-part
  confirmation chime. `ArgonWatchControls` plays them via AudioSource in
  PlayMode. Editor QA mixes the **same samples** into a real WAV and muxes
  it into MP4, so the new preview can be reviewed with synchronized audio.

## QA scope

`ArgonBedUnityVideoTests` instantiates the authentic prefabs, samples original
FBX animations, adds living motion, uses the real game OLED UI methods and
generates 720p 24fps Unity-rendered frames. The isolated QA skin material
uses the original Argon albedo and meshes but reduces problematic normal
aliasing and hard mirror highlights based on the comparison of four renders
in successful GitHub run #38984231016. The premium physical OLED shader
adds subtle black-level/contrast/glass-edge polish.

The MP4 is rendered **over a real captured BedCeilingPOV Game View still**.
It is not yet a fully interactive Unity PlayMode room. Do not advertise this
as finished integration until a specific game-scene PlayMode test is passed.
No proprietary source binary should be uploaded or advertised as private
while the GitHub repo remains public.

Visual pass criteria: preserved 2D positioning, actual subtle motion across
frames, PIP/DIP motion with no broken bones, clear physical OLED, natural
matte skin and audible quiet cloth/scroll/confirm cues.

## Reusing production V10 input safely

The actual V10 `SmartwatchInputRouter` supports crown wheel, drag ring,
screen dial taps, keyboard arrow keys, confirm and cancel. Do **not**
parallel-process those inputs again in the Argon candidate. When the
preview is installed into a separate PlayMode rig (NOT merged yet), call:

```csharp
argonControls.BindExistingV10InputRouter(existingV10Router);
```

This uses the real V10 events without a new assembly dependency and
automatically disables duplicate local pointer reads. Call
`argonControls.UnbindV10InputRouter()` before unloading the candidate;
`OnDestroy` also releases subscriptions. The original V10 controller is
never changed by the candidate. Real V10 duration bounds are **2..24
half-hour steps**. Its OLED now redraws only when the selection or
confirmed state actually changes. The 2,000-frame no-drift regression
validates the wrist/root and PIP motion without a full video render.

To avoid stacking additive secondary motions during runtime, call
`living.NotifyAnimationPoseReset()` only when the base Animator has
already overwritten the authored wrist/root bone pose since the previous
`Apply`. If there was no base pose reset, `Apply` removes its own prior
offset before adding the current bounded sway.
