# Home Hub 2.5D Layered Animation Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing Home Hub read as a living rainy 2.5D illustrated scene within 5–10 seconds while preserving the verified Built-in Render Pipeline atmosphere architecture and all unrelated gameplay/UI flows.

**Architecture:** Keep `WorldIllustration` as the room/base plane, add a masked crop of the existing Home art as a dedicated outside/window plane, then retain the existing RenderTexture weather plane, haze, wet-glass, cold/warm lighting and storm audio as independent depth layers. Add a tiny smoothed hybrid parallax target (ambient drift + cursor/focus response) with depth-specific amplitudes, while weather/glass/haze also retain independent deterministic motion. Rework lightning from a flat box fill into soft radial environmental flashes distributed across window, haze, cold spill, room-wide lift and warm/cold balance.

**Tech Stack:** Unity 6000.3.19f1, Built-in Render Pipeline, Unity UI/RawImage masking, built-in Particle System, RenderTexture, existing `RokasAudio`, deterministic C# animation. No URP, Shader Graph, DOTween/PrimeTween or VFX Graph.

**Spec:** User-approved Home Hub 2.5D Layered Animation Rework with Hybrid parallax option B.

## Global Constraints

- Work only on `feature/messages-system` starting from verified SHA `a53cac5e242b97ea75b7ec1c4ab3eeaaaace16f9` unless the factual remote HEAD is newer.
- `development` must remain `5e494f188b90bdb6916e8e78e35ac38509bd0bce`.
- PR #4 remains open, draft, not merged.
- Do not modify Messages, Live Messenger, Combat, Food, Contract or Map behavior.
- Keep Built-in Render Pipeline; do not add URP, Shader Graph, DOTween, PrimeTween, VFX Graph or a weather framework.
- Reuse the existing Home atmosphere camera, RenderTexture, ParticleSystems, materials/textures, audio owner and visibility guards.
- No per-Tick GameObject/material/texture/audio-clip allocation.
- Visual quality remains a manual QA boundary; automated tests prove structure/runtime safety, not subjective beauty.

---

### Task 1: Focused 2.5D runtime contract

**Files:**
- Modify: `Assets/Rokas/Tests/PlayMode/HomeAtmospherePlayModeTests.cs`

**Interfaces:**
- Consumes: existing runtime-built `RokasView`, `WorldEffects`, named Home atmosphere objects.
- Produces: focused checks for logical planes, bounded parallax, layer differentiation and reuse.

- [ ] **Step 1: Extend the Home atmosphere PlayMode suite with RED checks**

Add tests that require:
- `HomeOutsideParallax` masked outside plane using the existing Home texture;
- `HomeForegroundDepth` near depth overlay;
- far/mid/near rain layers with distinct start size, simulation speed/velocity and stretch length characteristics;
- hybrid parallax offsets that remain bounded to small pixel amplitudes after repeated ticks;
- wet glass and haze to move independently over time;
- lightning to use a soft-textured/radial surface rather than a flat `Image` box while preserving delayed one-shot thunder;
- repeated ticks to keep object, camera, RenderTexture and ParticleSystem counts stable.

- [ ] **Step 2: Run the focused PlayMode gate and confirm RED**

Expected: existing suite compiles; the new checks fail because the layered outside/foreground planes and bounded hybrid-parallax behavior do not yet exist.

- [ ] **Step 3: Commit only the RED test/plan infrastructure if the existing CI workflow requires a persisted trigger**

Do not modify Home production code in this step.

---

### Task 2: Logical Home scene planes and hybrid parallax

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`
- Modify only if needed for pointer/focus input plumbing: `Assets/Rokas/Scripts/Presentation/RokasView.cs`

**Interfaces:**
- Consumes: existing `WorldIllustration` `RawImage`, `UiKit`, window footprint `(350,107,605,396)`, existing weather canvas.
- Produces: `HomeOutsideParallax`, `HomeForegroundDepth`, smoothed bounded hybrid parallax state, independent depth-plane offsets.

- [ ] **Step 1: Create a masked outside-plane crop from the existing Home texture**

Inside the existing Home window footprint, create `HomeOutsideParallax` as a `RawImage` using the same Home texture and a calculated UV crop, overscanned by a few pixels so tiny movement never reveals edges. It must live behind weather/wet-glass but above the static base illustration.

- [ ] **Step 2: Create a restrained near-depth overlay**

Create `HomeForegroundDepth` once using the existing cached soft radial texture/material path. It should provide a subtle edge/foreground depth cue and move slightly more than the room base without obscuring Home UI.

- [ ] **Step 3: Add deterministic ambient drift + tiny cursor/focus response**

Maintain a smoothed normalized parallax target. Ambient drift uses low-frequency sinusoids. Cursor input contributes only a small fraction; keyboard/controller focus may supply the selected UI element direction when available. Clamp target and amplitudes so movement is polish, not camera sway.

Use approximate maximum pixel amplitudes:
- outside/far: <= 1.5 px;
- room/base: <= 2.0 px;
- weather/mid: <= 2.6 px;
- wet glass/haze: <= 3.2 px plus their independent drift;
- near foreground accent: <= 3.8 px.

Use exponential/smooth interpolation, not instant jumps.

- [ ] **Step 4: Keep UI and interaction roots stationary**

Only visual planes move. Do not move Home buttons, headers, laptop UI, interaction roots or modal panels.

- [ ] **Step 5: Run focused PlayMode tests**

Expected: new layer/parallax checks PASS; existing atmosphere runtime safety checks remain PASS.

---

### Task 3: Rain, wet glass and outside-world readability tuning

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/HomeAtmosphereProfile.cs`
- Modify: `Assets/Rokas/Resources/HomeAtmosphereProfile.asset`

**Interfaces:**
- Consumes: three existing bounded ParticleSystems and existing cached rain/wet-glass/radial textures.
- Produces: visibly distinct weather depth and readable wet-window motion.

- [ ] **Step 1: Tune rain for clear layer separation without increasing object count**

Keep particle caps at or below current `48 / 36 / 24`. Increase visual readability through emission-rate tuning, opacity, start size, fall speed, horizontal drift and renderer stretch length. Far remains thinner/softer, mid readable, near largest/fastest/brightest.

- [ ] **Step 2: Strengthen independent outside motion**

Make haze drift visibly but slowly on a separate trajectory from parallax. Add one additional cached mist plane only if necessary for clear depth; create it once and reuse the same radial texture.

- [ ] **Step 3: Strengthen wet-glass animation**

Increase wet-glass visibility and UV drift. Add a second sparse streak layer only if needed, created once from the same cached wet-glass texture with a different UV phase/speed and lower alpha. No texture generation during Tick.

- [ ] **Step 4: Extend profile only for values that must be tunable**

Retain existing fields. Add only small controls such as `parallaxIntensity`, `outsideMotionIntensity` and `weatherReadability` if code otherwise needs hard-coded tuning. Serialize safe readable defaults in `HomeAtmosphereProfile.asset`.

- [ ] **Step 5: Run focused atmosphere tests**

Expected: rain remains bounded, layer parameters are differentiated, wet glass/haze move, reuse tests PASS.

---

### Task 4: Environmental lightning and room-light response

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`

**Interfaces:**
- Consumes: existing deterministic lightning timer, `LightningEnvelope`, delayed thunder routing, cached radial texture, cold/warm overlays.
- Produces: environmental flash without a rectangular light-box read.

- [ ] **Step 1: Replace the flat storm flash appearance with a soft radial flash surface**

Keep the object name `HomeStormFlash` for compatibility, but render it through the existing radial texture/RawImage path with overscan and soft falloff rather than a flat rectangular `Image` fill.

- [ ] **Step 2: Distribute flash energy across the scene**

Drive flash simultaneously through the soft window flash, exterior haze, cold window bounce, a restrained room-wide cold lift, darkness reduction and slight warm-light suppression/contrast. Preserve the double-flash envelope and avoid full-white exposure.

- [ ] **Step 3: Preserve thunder timing and one-shot routing**

Do not change the existing irregular scheduling semantics or delayed exactly-one thunder cue per lightning event.

- [ ] **Step 4: Run focused storm tests**

Expected: manual trigger produces immediate soft environmental flash and exactly one delayed thunder cue.

---

### Task 5: Full verification, persist and checkpoint

**Files:**
- No production changes unless a real failure is discovered.

**Interfaces:**
- Consumes: completed 2.5D Home atmosphere tree.
- Produces: verified feature branch checkpoint suitable for manual visual inspection.

- [ ] **Step 1: Run literal whitespace verification**

Run `git diff --check`; require exit 0 and empty diagnostics.

- [ ] **Step 2: Run Core/domain guard**

Require exact Core behavior PASS. Classify only the previously accepted validator baseline as historical; any new Home-atmosphere finding blocks completion.

- [ ] **Step 3: Run full Unity EditMode and PlayMode suites**

Require all EditMode tests PASS, all PlayMode tests PASS, all focused Home atmosphere tests PASS and all existing Messages/Live Messenger regressions PASS.

- [ ] **Step 4: Scan Unity logs**

Require no new `NullReferenceException`, `MissingReferenceException`, ParticleSystem, RenderTexture, missing material/shader/texture, or AudioSource/audio-clip runtime errors.

- [ ] **Step 5: Verify performance invariants from runtime structure**

Confirm bounded particle counts, reused camera/RenderTexture/materials/textures, no per-Tick object churn and Home visibility guards. Do not claim FPS measurements.

- [ ] **Step 6: Persist production only after GREEN**

Push production to `feature/messages-system`; do not modify `development` and do not merge PR #4.

- [ ] **Step 7: Create tree-identical checkpoint**

Commit message exactly:

`VERIFIED HOME HUB 2.5D LAYERED ANIMATION REWORK CHECKPOINT`

Then confirm feature local/remote SHA equality, `development` unchanged and PR #4 open/draft/not merged.

- [ ] **Step 8: STOP**

Next boundary is `USER MANUAL HOME HUB 2.5D LAYERED ANIMATION INSPECTION`.
