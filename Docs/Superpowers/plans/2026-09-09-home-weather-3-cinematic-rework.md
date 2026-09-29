# Home Weather 3.0 Cinematic Asset-Based Atmosphere Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the visually geometric/procedural Home weather presentation with a lightweight authored-asset-driven rainy-night composition that preserves the verified Home 2.5D architecture, removes visible light/dark rectangular boundaries, and remains safe on Unity 6000.3.19f1 Built-in Render Pipeline.

**Architecture:** Keep `WorldIllustration`, `HomeWeatherCamera`, the 768x512 RenderTexture, the three bounded rain ParticleSystems, Hybrid-B parallax, existing `RokasAudio`, and Home visibility guards. Replace the flat `LampMood` darkness path and the shared procedural radial/rain/glass primitives with distinct feathered authored masks/textures and small Built-in-compatible materials; lightning becomes a multi-layer response instead of one masked flash card. Runtime visual proof is captured before and after implementation, and the final checkpoint is created only if real captures show no obvious rectangular weather/light boundary.

**Tech Stack:** Unity 6000.3.19f1, Built-in Render Pipeline, Unity UI `Image`/`RawImage`, ParticleSystem, RenderTexture, lightweight hand-written Built-in shader only if required, NUnit/Unity Test Framework, existing ROKAS Core and `RokasAudio`, GitHub Actions/GameCI.

**Spec:** User-approved Home Weather 3.0 master prompt from 2026-09-09.

## Global Constraints

- Starting verified Home branch: `feature/messages-system` at `c68763fb482665ea53cb42c48b2638cc5314c3c0`, tree `f6318724b3dff93857011803ff09679e9c8d69b5`.
- Production branch for this milestone: `feature/home-weather-3`.
- `development` must not be modified or merged.
- Do not merge PR #4.
- Do not merge or modify Combat for this milestone.
- Preserve Messages, Live Messenger, save/resume, Home 2.5D, and stationary UI interaction roots.
- No URP/HDRP migration, Shader Graph, VFX Graph, DOTween/PrimeTween, or large weather framework.
- No final placeholder squares, flat test masks, watermarked imagery, or web screenshots used as runtime assets.
- Third-party source/license must be documented before source asset files are committed.
- Runtime quality is a manual/visual gate; automated tests protect structure, lifecycle, references and regressions but cannot prove beauty.
- No per-frame GameObject, Texture2D, RenderTexture, Material, AudioClip, or repeated Resources.Load creation.
- Final `git diff --check` must exit 0 with empty output.
- Historical validator baseline is 13 findings; Weather 3.0 must add 0 new relevant findings.

---

### Task 1: Establish factual Weather 3.0 baseline and diagnostic runtime capture

**Files:**
- Read: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`
- Read: `Assets/Rokas/Scripts/Presentation/HomeAtmosphereProfile.cs`
- Read: `Assets/Rokas/Resources/HomeAtmosphereProfile.asset`
- Read: `Assets/Rokas/Tests/PlayMode/HomeAtmospherePlayModeTests.cs`
- CI-only create on `ci/home-weather-3`: `.github/workflows/home-weather-3.yml`
- CI-only create: `Assets/Rokas/Tests/PlayMode/HomeWeather3CapturePlayModeTests.cs`

**Interfaces:**
- Consumes: verified Home 2.5D tree at `c68763fb...`.
- Produces: baseline screenshots and logs for light ON, light OFF, forced lightning, wet-glass/idle weather; exact object-level RCA evidence.

- [ ] **Step 1: Record code-level RCA candidates before changing production**

Record these factual current paths in the diagnostic notes:

```text
LampMood: full-screen 1920x1080 Image created by ui.Box.
OFF state: alpha ~= .64 via ApplyLighting(...).
HomeStormFlash: RawImage using generated HomeAtmosphereSoftGlow.
HomeStormFlash parent: WindowRain, which has RectMask2D and is 605x396 at Home.
HomeExteriorHaze/HomeExteriorMistNear/HomeColdWindowBounce/HomeWarmInteriorGlow/HomeStormRoomLift: reuse the same generated 96x96 radial texture.
HomeRainStreakTexture and HomeWetGlassTexture are generated procedurally in WorldEffects.
```

Expected: code evidence predicts the OFF dark card and explains why the flash/weather system can still expose geometric container boundaries, but this remains a hypothesis until runtime captures confirm it.

- [ ] **Step 2: Create isolated diagnostic CI branch from the plan commit**

Create `ci/home-weather-3` from current `feature/home-weather-3`. CI-only files must not be copied into the production tree unless they are genuine reusable tests.

- [ ] **Step 3: Add runtime capture fixture**

The fixture must initialize the normal ROKAS Home runtime, wait for layout/render stabilization, capture normal light ON, toggle the real Home lamp state OFF and capture, force `WorldEffects.ForceLightning()` and capture one or more flash frames, then tick several seconds and capture a wet-glass/rain idle frame. Save PNGs beneath `TestResults/HomeWeather3Visual/`.

The capture helper must use the real runtime hierarchy, not editor mockups or reconstructed images.

- [ ] **Step 4: Run diagnostic workflow with known-good GameCI pin**

Use:

```yaml
uses: game-ci/unity-test-runner@0ff419b913a3630032cbe0de48a0099b5a9f0ed9
```

Expected: Unity activates, diagnostic PlayMode test executes, screenshots and logs upload as an artifact. A licensing failure is infrastructure, not Weather RED.

- [ ] **Step 5: Inspect captures against reproduction matrix**

Inspect:

```text
A. normal Home, room light ON
B. normal Home, room light OFF
C. forced lightning while ON
D. forced lightning while OFF if the public/runtime lamp path permits it
E. several-seconds idle rain/glass/mist
```

For each screenshot identify any straight boundary and correlate it to the runtime object hierarchy. Do not edit production before this is understood.

---

### Task 2: Finalize asset audit and legally safe asset selection

**Files:**
- Create: `Docs/ThirdParty/Weather3.md`
- Later create runtime textures under: `Assets/Rokas/Resources/Weather3/`
- Preserve original source/license references in documentation; avoid importing entire third-party packages when a few CC0 source images suffice.

**Interfaces:**
- Consumes: baseline capture/RCA and current generated texture architecture.
- Produces: selected authored texture sources, license proof and KEEP/REWORK/REPLACE classification.

- [ ] **Step 1: Classify current resources**

```text
KEEP: WorldIllustration/Home source art; HomeWeatherCamera; 768x512 RenderTexture; 3 bounded ParticleSystems; RokasAudio rain/thunder architecture; Hybrid-B parallax; window/outside masks where they do not expose visible edges.
REWORK: rain material/texture; wet-glass layers; mist/haze layers; storm response; warm/cold lighting masks.
REPLACE: full-screen flat LampMood darkness card; shared generated HomeAtmosphereSoftGlow as the primary final weather/light art; generated HomeRainStreakTexture and HomeWetGlassTexture as final authored-looking resources.
```

- [ ] **Step 2: Document researched candidates**

Document at minimum:

```text
Rain Maker - 2D and 3D Rain Particle System for Unity
- Unity Asset Store, free, Standard Unity Asset Store EULA, 9.2 MB, version 2.1.2.
- Listing marks Built-in compatible on Unity 2019.4.35.
- Recent Unity 6 reviews conflict; one reports camera-follow breakage, another reports success.
- REJECT as primary dependency for this milestone: unnecessary framework/code intrusion and uncertain Unity 6 behavior; also avoid redistributing Asset Store source through GitHub when CC0 texture alternatives fit the existing architecture.

OpenGameArt `rain` by gmason
- file: rain_overlay.png, transparent authored light-rain texture, CC0.
- candidate for rain-streak source/alpha shaping after project-side crop/normalization.

OpenGameArt `Thick Fog` by LFA
- file: fog01.png, 1024x512 scrolling fog texture, CC0.
- candidate for distant haze.

OpenGameArt `Clouds with Transparency` by WickedInsignia
- 2K transparent cloud alpha textures, CC0.
- candidate for irregular lightning/cloud response mask and/or near mist source.

OpenGameArt `Fog Animation` by AntumDeluge
- 640x480 tileable fog animation, CC0.
- candidate/reference for near mist; prefer one/few source frames plus independent UV motion over importing an unnecessary large animation if static alpha motion is enough.

Wikimedia Commons `RaindropsOnWindow.jpg` / other CC0 raindrops-on-window sources
- CC0; candidate source photography for deriving droplet/streak alpha masks.
```

- [ ] **Step 3: Select the smallest sufficient set**

Preferred first implementation set:

```text
rain: CC0 rain_overlay.png adapted into one compact alpha texture for existing ParticleSystems
haze: CC0 fog01.png
near mist/lightning irregularity: one CC0 transparent cloud alpha from `Clouds with Transparency`
wet glass: one CC0 raindrops-on-window photograph converted offline into two compact project-owned alpha resources: droplets + streak/detail mask
```

Do not import Rain Maker unless the existing ParticleSystem architecture proves unable to meet quality after authored textures are integrated.

- [ ] **Step 4: Record exact files, transformations and source URLs before commit**

`Docs/ThirdParty/Weather3.md` must record creator, source page, exact downloaded file, CC0 status, project output filename, any crop/alpha/levels changes, Built-in compatibility reasoning, and why it is included.

---

### Task 3: Define focused Weather 3.0 RED contracts

**Files:**
- Create: `Assets/Rokas/Tests/PlayMode/HomeWeather3PlayModeTests.cs`
- Preserve: `Assets/Rokas/Tests/PlayMode/HomeAtmospherePlayModeTests.cs`

**Interfaces:**
- Consumes: actual current Home runtime.
- Produces: meaningful RED tests that fail specifically because the old procedural/flat Weather 2.5D architecture is still present.

- [ ] **Step 1: Add light-off architecture contract**

Test must find `LampMood` and prove the new implementation does not use a full-screen flat `Image` alpha card for Home OFF dimming. The initial test should fail against the current code because `LampMood` is exactly that path.

- [ ] **Step 2: Add authored-resource separation contract**

Require distinct non-null textures for at least:

```text
HomeWetGlassDroplets
HomeWetGlassStreaks
HomeExteriorHaze
HomeExteriorMistNear
HomeStormFlash/irregular outside-light response
```

Require droplet and streak resources not to be the same Texture instance; haze/mist must not share one primitive texture.

- [ ] **Step 3: Add storm multi-layer contract**

Force lightning and assert that outside response, haze/mist response, glass response and room-side response all change without relying solely on one `Graphic` surface.

- [ ] **Step 4: Preserve safety contracts**

Assert:

```text
far/mid/near ParticleSystem max counts remain bounded
HomeWeatherCamera count remains constant over repeated Tick
HomeWeatherComposite count remains constant
LaptopHotspot anchoredPosition remains unchanged
parallax stays within existing few-pixel limits
weather becomes inactive outside Home/Portal lifecycle where expected
```

- [ ] **Step 5: Run focused PlayMode RED**

Expected: failures only for new Weather 3.0 contracts; existing Home 2.5D and regression tests remain green. Record exact failures and distinguish any CI/license issue from feature RED.

---

### Task 4: Import/process the selected CC0 visual resources

**Files:**
- Create: `Assets/Rokas/Resources/Weather3/RainStreakAuthored.png`
- Create: `Assets/Rokas/Resources/Weather3/WetGlassDroplets.png`
- Create: `Assets/Rokas/Resources/Weather3/WetGlassStreaks.png`
- Create: `Assets/Rokas/Resources/Weather3/DistantHaze.png`
- Create: `Assets/Rokas/Resources/Weather3/NearMist.png`
- Create: `Assets/Rokas/Resources/Weather3/StormCloudMask.png`
- Create matching `.meta` files with stable GUIDs/import settings.
- Update: `Docs/ThirdParty/Weather3.md`

**Interfaces:**
- Consumes: selected CC0 source imagery.
- Produces: compact runtime-ready alpha resources loaded once by `WorldEffects`.

- [ ] **Step 1: Download only selected source files from canonical source URLs**

No screenshots, watermarks, login bypass or paid content.

- [ ] **Step 2: Prepare compact project runtime assets offline**

Use lossless PNG with feathered alpha. Crop/resize only as needed for the 605x396 window and 768x512 weather composition. Prefer 512-1024 dimensions where sufficient; do not blindly retain multi-megabyte/high-resolution source photography.

For droplet source photography, derive transparent/highlight alpha masks from real droplet structure instead of shipping the photographic background itself.

- [ ] **Step 3: Validate texture edges**

All final masks must have transparent/feathered outer boundaries or intentional seamless wrapping. No straight alpha border may become visible when tint/brightness increases.

- [ ] **Step 4: Commit assets and manifest as one logical asset commit**

Expected message: `assets: add licensed Weather 3.0 visual resources`.

---

### Task 5: Remove the factual rectangular light/dark sources

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/HomeAtmosphereProfile.cs`
- Modify: `Assets/Rokas/Resources/HomeAtmosphereProfile.asset`
- Optional create if required by capture evidence: `Assets/Rokas/Art/Home/Shaders/HomeWeatherSoftMask.shader`

**Interfaces:**
- Consumes: Weather3 masks and RED contracts.
- Produces: spatial lighting with no flat full-screen OFF card and no single masked flash-card dependency.

- [ ] **Step 1: Replace `LampMood` full-screen uniform darkness path**

Do not just lower its alpha. Remove Home OFF dependence on the flat `Image`. Implement OFF state as reduction/removal of localized warm illumination plus subtle shaped/feathered cool-shadow/vignette treatment only if capture evidence shows it is needed. No uniform rectangular darkness layer may become visible.

- [ ] **Step 2: Replace primary `HomeStormFlash` radial primitive**

Use `StormCloudMask` with fully feathered irregular alpha and overscan. If the rectangular `RectMask2D` window boundary itself is the visible flash edge, keep exterior rain clipping but move/compose the bright environmental response on appropriately overscanned irregular surfaces whose transparent edges die out before clipping.

- [ ] **Step 3: Give room-side responses independent masks**

Cold spill, warm glow and room lift must no longer all reuse one 96x96 procedural radial texture. Use shaped/irregular authored masks and/or direct modulation of relevant art layers.

- [ ] **Step 4: Run focused RED tests to GREEN**

Expected: new light-off/storm/resource contracts pass without weakening old tests.

---

### Task 6: Rework rain with authored texture while preserving bounded emitters

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/HomeAtmosphereProfile.cs`
- Modify: `Assets/Rokas/Resources/HomeAtmosphereProfile.asset`

**Interfaces:**
- Consumes: `RainStreakAuthored.png`, existing three ParticleSystems.
- Produces: natural far/mid/near rainfall with controlled variation and slight wind.

- [ ] **Step 1: Replace generated rain texture with authored rain alpha texture loaded/cached once**

Keep one shared rain material if one texture/material can serve all three layers without visual compromise.

- [ ] **Step 2: Tune depth independently**

Preserve initial max caps unless runtime proof requires a change:

```text
Far <= 48: small, soft, lower contrast
Mid <= 36: primary readable rain body
Near <= 24: longer/brighter variable streaks, still behind glass
```

- [ ] **Step 3: Add controlled wind/variation**

Use MinMaxCurve/ParticleSystem settings to vary speed/length/opacity/position without per-frame allocations. Avoid perfectly vertical identical streaks.

- [ ] **Step 4: Verify exterior clipping**

Rain must not visually cross laptop/buttons/text/interior foreground.

---

### Task 7: Rework wet glass into distinct droplets and streaks

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`
- Modify profile files from Task 6 as needed.

**Interfaces:**
- Consumes: `WetGlassDroplets.png`, `WetGlassStreaks.png`.
- Produces: independent water-on-glass systems visually separate from exterior rain.

- [ ] **Step 1: Bind separate textures**

Rename/use runtime surfaces semantically as `HomeWetGlassDroplets` and `HomeWetGlassStreaks` (preserve legacy name compatibility only where tests require a migration shim).

- [ ] **Step 2: Implement independent slow movement**

Use different UV scale/speeds and small transform drift; droplets mostly static/slow, streaks lower-frequency downward motion. No synchronized looping.

- [ ] **Step 3: Add flash response through texture detail**

Lightning raises droplet/streak highlights proportionally, never by illuminating a uniform glass rectangle.

---

### Task 8: Rework distant haze and near mist

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`

**Interfaces:**
- Consumes: `DistantHaze.png`, `NearMist.png`.
- Produces: independent atmospheric planes with irregular edges.

- [ ] **Step 1: Replace shared radial texture**

Distant haze uses broad low-contrast authored fog; near mist uses a different cloud/mist alpha source.

- [ ] **Step 2: Preserve independent drift**

Move at different rates/scales, with overscan sufficient to hide seams under Hybrid-B parallax.

- [ ] **Step 3: Tune lightning response**

Near mist catches more flash than distant haze, giving pseudo-volumetric depth.

---

### Task 9: Rework lightning/thunder into spatial multi-layer storm events

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/HomeAtmosphereProfile.cs`
- Modify: `Assets/Rokas/Resources/HomeAtmosphereProfile.asset`
- Read/modify only if necessary for existing APIs: `Assets/Rokas/Scripts/Presentation/RokasAudio.cs`

**Interfaces:**
- Consumes: existing deterministic storm scheduler and delayed thunder architecture.
- Produces: outside + haze + mist + glass + cold spill + weak room lift response.

- [ ] **Step 1: Keep irregular timing/delayed thunder**

Do not synchronize thunder to flash. Preserve deterministic testability while varying delay within a plausible safe range.

- [ ] **Step 2: Drive multiple response weights from one flash envelope**

Each spatial plane gets a distinct multiplier; outside strongest, near mist and wet glass next, cold room spill restrained, room-wide lift weakest.

- [ ] **Step 3: Preserve/extend thunder variation only with legitimate existing or documented assets**

Do not replace existing audio merely for novelty. If no superior legally safe clips are selected, preserve current thunder bank.

---

### Task 10: Preserve Hybrid-B parallax, lifecycle and performance boundaries

**Files:**
- Modify only as needed: `Assets/Rokas/Scripts/Presentation/WorldEffects.cs`
- Tests: `Assets/Rokas/Tests/PlayMode/HomeWeather3PlayModeTests.cs`

**Interfaces:**
- Consumes: new weather layers.
- Produces: coherent depth motion with stationary UI and cached resources.

- [ ] **Step 1: Attach new layers to the correct depth behavior**

Far haze/rain = smallest exterior relationship; near mist = slightly stronger independent drift; glass = independent window motion; room/UI interactions unchanged.

- [ ] **Step 2: Audit for seams and overscan**

No transparent/black crop gaps as cursor/ambient parallax moves.

- [ ] **Step 3: Audit runtime allocations/lifetime**

Repeated Tick must not increase ParticleSystem, Camera, RenderTexture, Material, or weather Graphic counts. Destroy runtime-created material/texture instances only on teardown; imported Resources textures are not manually destroyed.

---

### Task 11: Run integrated GREEN and produce real visual proof

**Files:**
- CI-only update: `.github/workflows/home-weather-3.yml`
- CI-only update/capture helper as needed.

**Interfaces:**
- Consumes: persisted production candidate tree.
- Produces: test artifact plus before/after runtime capture evidence.

- [ ] **Step 1: Run focused Weather/Home PlayMode tests**

All `HomeAtmospherePlayModeTests` and `HomeWeather3PlayModeTests` must pass.

- [ ] **Step 2: Run full applicable Core/domain suite**

Record exact command/count/result. Existing Messages/Home/Core behavior must remain green.

- [ ] **Step 3: Run complete Unity EditMode and PlayMode**

Record exact total/passed/failed/skipped/inconclusive. Required failures = 0.

- [ ] **Step 4: Run validator and literal diff check**

Validator may contain only the known historical 13 baseline findings; new Weather relevant findings = 0. Run exactly `git diff --check`; required exit 0 and empty output.

- [ ] **Step 5: Scan fresh Unity logs**

Explicitly scan for `NullReferenceException`, `MissingReferenceException`, `MissingComponentException`, relevant `ArgumentException`, RenderTexture, ParticleSystem, shader/material/texture, AudioSource/AudioClip, masking/Canvas, Home weather and unhandled exceptions. Separate benign Unity/package/license-client noise from failures that prevent execution.

- [ ] **Step 6: Capture final runtime visual matrix**

Capture light ON, light OFF, normal rain, wet glass, lightning, and a normal frame at the old rectangle location.

- [ ] **Step 7: Perform actual visual edge review**

For every final capture inspect left/right/top/bottom effect boundaries, window border, adjacent wall/floor and darkest regions. If any bright/dark/fog/glass rectangle is visually identifiable, do not checkpoint; return to the specific RCA/fix task.

---

### Task 12: Independent code review and final persistence

**Files:**
- Review full feature diff and `Docs/ThirdParty/Weather3.md`.

**Interfaces:**
- Consumes: GREEN implementation and visual captures.
- Produces: reviewed production tree and verified checkpoint.

- [ ] **Step 1: Request independent code review**

Review focus:

```text
RCA correctness
rectangle source removed rather than hidden
asset licensing/source and repository safety
Built-in compatibility
asset bloat/import settings
material/texture lifetime
particle bounds
weather visibility lifecycle
UI/LaptopHotspot stability
Messages regression risk
scope discipline
test quality
```

- [ ] **Step 2: Validate and address only real review findings**

Use `receiving-code-review` if findings exist. Any production/test change after verification invalidates that verification and requires rerunning the relevant gates.

- [ ] **Step 3: Persist clean production commit**

Suggested message:

```text
feat: rework Home Weather 3.0 cinematic atmosphere
```

- [ ] **Step 4: Fresh final verification against exact persisted production tree**

CI must checkout/assert exact production SHA and tree. Upload artifact `home-weather-3-final-results` containing XML/logs/Core/validator/diff-check and runtime visual captures.

- [ ] **Step 5: Create tree-identical verified checkpoint only after all technical and visual gates pass**

Exact message:

```text
VERIFIED HOME WEATHER 3.0 CINEMATIC ASSET-BASED REWORK CHECKPOINT
```

The checkpoint tree must exactly equal the freshly tested production tree.

- [ ] **Step 6: Push and prove remote safety**

Verify `origin/feature/home-weather-3` equals the checkpoint, `development` remains factual/untouched, and PR #4 remains open/draft/not merged. Do not merge.

- [ ] **Step 7: Stop for manual QA**

Handoff state:

```text
USER MANUAL HOME WEATHER 3.0 INSPECTION
```

Do not start Weather 3.1 or another feature.
