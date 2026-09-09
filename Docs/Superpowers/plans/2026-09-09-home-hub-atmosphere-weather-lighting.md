# Home Hub Atmosphere / Weather / Lighting Polish — Implementation Plan

**Goal:** Upgrade the existing Home Hub rainy-night presentation without changing the game loop, laptop/messages, combat, food, contracts, map, or render pipeline.

**Source of truth:** existing `ApartmentNight` art and runtime-built Home Hub.

## Actual project constraints

- Unity 6000.3.19f1.
- The project has no URP/Shader Graph package, no GraphicsSettings asset, no DOTween, and no VFX Graph.
- The single `Rokas.unity` scene contains only the runtime bootstrap; presentation is authored in code.
- `WorldEffects` currently owns a 44-rectangle fake rain layer.
- `RokasAudio` already owns looping HomeRain ambience and pooled SFX sources.

Adding URP/Shader Graph only for this milestone would violate the no-pipeline-migration boundary. Adding DOTween is unnecessary for a small deterministic storm state machine. VFX Graph is not justified.

## Scoped architecture

### 1. Focused RED PlayMode contract

Add `HomeAtmospherePlayModeTests` that proves the current Home Hub lacks:
- three real ParticleSystem rain layers (`HomeRainFar`, `HomeRainMid`, `HomeRainNear`),
- wet-glass / exterior haze surfaces,
- storm flash surface and testable lightning trigger,
- controlled particle budgets and Home-only activation,
- thunder routing through the existing audio owner.

Run the existing Unity PR workflow to capture the intentional RED before production changes.

### 2. Replace fake stick rain with a localized weather rig

Modify `WorldEffects` only for Home/Portal visual effects:
- remove the hand-animated 44 UI rain sticks,
- create a small offscreen weather camera + RenderTexture,
- create three built-in ParticleSystems with fixed max-particle budgets and different speed/scale/opacity,
- render the transparent result into the existing window mask as a RawImage,
- disable the weather camera/emitters outside Home so there is no persistent extra render cost.

Particle budgets stay bounded (far <= 48, mid <= 36, near <= 24).

### 3. Window depth and subtle light polish

Inside the existing Home window footprint:
- procedural wet-glass overlay with slow UV drift,
- soft exterior haze/mist layer,
- subtle cold night bounce near the window,
- subtle warm interior counter-light tied to lamp state,
- no fullscreen white flash and no UI obstruction.

A small serialized `HomeAtmosphereProfile` Resources asset will expose rain intensity/speed, layer toggles, lightning interval/intensity, thunder/rain volume, haze/glow/wet-glass intensity.

### 4. Lightning and thunder

Use a deterministic local storm state machine in `WorldEffects`:
- irregular interval between profile min/max,
- short double-flash envelope,
- stronger window flash + restrained interior spill,
- delayed thunder once per lightning event,
- no thunder outside Home,
- no repeated per-frame cue spam.

Extend existing `RokasAudio` with a cached three-variant procedural distant-thunder bank and Home weather mix control. No new audio manager.

### 5. Verification and persist

Run:
- literal `git diff --check`,
- focused Home atmosphere PlayMode tests,
- full Unity EditMode + PlayMode regression suite,
- Core/domain + existing asset validator,
- log scan for new null refs, missing shaders/materials, TMP issues, and audio warnings.

Classify only the already-known validator baseline as historical; any new relevant finding blocks checkpoint.

On a fully verified persisted tree create tree-identical checkpoint:

`VERIFIED HOME HUB ATMOSPHERE WEATHER LIGHTING CHECKPOINT`

Then confirm `development` is unchanged, PR #4 remains open/draft/not merged, report pull commands, and STOP.
