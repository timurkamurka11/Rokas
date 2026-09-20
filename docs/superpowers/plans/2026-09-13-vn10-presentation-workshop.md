# VN 10 Presentation Workshop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the completed VN/UI Workshop into an editor-only cinematic presentation workbench for typography, motion, pacing, staging and interaction tuning without applying any values to production.

**Architecture:** Reuse the existing override-only `VnPresentationWorkshopPreset`, mirror renderer, EditorWindow, variants and JSON workflow. Add editor-only VN10 parameter groups plus deterministic pure evaluators; the preview window owns ephemeral playback state and passes sampled values to the renderer. Production presentation/story assemblies remain read-only.

**Tech Stack:** Unity 6000.3.19f1, C#, Unity Editor IMGUI, NUnit EditMode tests, existing `RokasAssets`/character visual catalog, GitHub Actions GameCI.

**Spec:** User-approved VN10 brief supplied 2026-09-13; base Workshop commit `43699c841e24ed806f377fbd9a7bec4d18f6cb1d`.

## Global Constraints

- Repository: `timurkamurka11/Rokas` only.
- Branch: `feature/vn10-presentation-workshop` from exact base `43699c841e24ed806f377fbd9a7bec4d18f6cb1d`.
- No production/runtime/story/Yarn/save/progression mutations.
- No `ApplyToProduction` path.
- Original baseline remains immutable; user experimental local layout is not imported or committed.
- Reuse `ROKAS → VN UI Workshop`, variants, import/export and responsive preview resolutions.
- Preview state such as current animation frame, hover state and selection is never serialized.
- Use focused RED→GREEN EditMode TDD for each slice; expensive Windows/player proof only after all slices are green.

---

### Task 1: Phase A — Typography + safe-frame clipping

**Files:**
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopVn10Types.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopTypes.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopBaseline.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopPreviewRenderer.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseA.cs`

**Interfaces:**
- Produces `VnWorkshopFontPreset`, `VnWorkshopTextAlignment`, `VnWorkshopTypographyOverride`, `VnWorkshopTypographyValues`.
- Produces `ResolveTypography(preset)` and `ClipLogicalRectToViewport(rect, frame)`.

- [ ] Write tests asserting default typography equals immutable baseline, sans/serif presets are selectable, size/spacing/alignment overrides resolve deterministically, and Mina visible geometry is clipped to `[0..virtualCanvas]` while dialogue remains above characters.
- [ ] Run `Rokas.EditorTools.Tests.VnUiWorkshopTestsVn10PhaseA` and confirm RED because VN10 typography/clipping APIs do not exist.
- [ ] Add only the typography model/resolver and viewport-clipped preview draw path needed by those tests.
- [ ] Re-run Workshop EditMode and confirm GREEN; commit checkpoint.

### Task 2: Phase B — Typewriter + pacing model

**Files:**
- Modify: `VnPresentationWorkshopVn10Types.cs`
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopVn10Resolver.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseB.cs`

**Interfaces:**
- Produces `VnWorkshopTypewriterOverride`, `VnWorkshopTimingOverride`, `VnWorkshopTypewriterValues`.
- Produces `CalculateTypewriterVisibleCharacters(text, elapsed, values)` and `CalculateTypewriterDuration(text, values)`.

- [ ] RED tests cover deterministic reveal, comma/period/ellipsis/question/exclamation pauses, instant complete, and no automatic story advancement concept.
- [ ] Implement pure timing/reveal evaluator and restrained defaults.
- [ ] GREEN focused Workshop tests; commit checkpoint.

### Task 3: Phase C — Expression crossfade + character enter/exit

**Files:**
- Modify: `VnPresentationWorkshopVn10Types.cs`
- Modify: `VnPresentationWorkshopVn10Resolver.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseC.cs`

**Interfaces:**
- Produces `VnWorkshopExpressionTransitionOverride`, `VnWorkshopCharacterTransitionOverride` and sampled transition structs.
- Evaluators accept normalized progress and return authored-state blend alpha/position/alpha only.

- [ ] RED tests prove exact start/end values, easing bounds, authored-state identifiers preserved, fade and slide+fade interpolation deterministic.
- [ ] Implement minimal pure evaluators; no fake emotions or blink states.
- [ ] GREEN tests; commit checkpoint.

### Task 4: Phase D — Authored action bounce

**Files:**
- Modify: `VnPresentationWorkshopVn10Types.cs`
- Modify: `VnPresentationWorkshopVn10Resolver.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseD.cs`

**Interfaces:**
- Produces `VnWorkshopBounceOverride`, `SampleBounce(progress, values)`.

- [ ] RED tests require zero offset/scale delta at progress 0 and 1, positive subtle emphasis mid-event and no persistent idle offset.
- [ ] Implement one-shot event curve with optional overshoot; do not add idle bobbing.
- [ ] GREEN tests; commit checkpoint.

### Task 5: Phase E — Background fade / curtain transition

**Files:**
- Modify: `VnPresentationWorkshopVn10Types.cs`
- Modify: `VnPresentationWorkshopVn10Resolver.cs`
- Modify: `VnPresentationWorkshopPreviewRenderer.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseE.cs`

**Interfaces:**
- Produces `VnWorkshopBackgroundTransitionMode`, `VnWorkshopCurtainDirection`, transition override/sample.
- Renderer consumes two existing ROKAS backgrounds and sampled wipe geometry.

- [ ] RED tests assert instant/fade/curtain completion and left/right curtain direction geometry.
- [ ] Implement deterministic transition sample and preview overlay/reveal.
- [ ] GREEN tests; commit checkpoint.

### Task 6: Phase F — 1/2/3 character staging

**Files:**
- Modify: `VnPresentationWorkshopVn10Types.cs`
- Modify: `VnPresentationWorkshopVn10Resolver.cs`
- Modify: `VnPresentationWorkshopPreviewRenderer.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseF.cs`

**Interfaces:**
- Produces `VnWorkshopStageOverride`, deterministic slot layouts and `SampleStageReposition`.

- [ ] RED tests cover 1 center, 2 left/right, 3 left/center/right and interpolation to exact target slots.
- [ ] Implement editor-only stage model with restrained defaults and no forced overlap.
- [ ] GREEN tests; commit checkpoint.

### Task 7: Phase G — Active speaker focus integration

**Files:**
- Modify: `VnPresentationWorkshopTypes.cs`
- Modify: `VnPresentationWorkshopResolver.cs`
- Modify: `VnPresentationWorkshopVn10Types.cs`
- Modify: `VnPresentationWorkshopVn10Resolver.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseG.cs`

**Interfaces:**
- Extends focus overrides with duration/easing/optional forward offset while preserving existing scale/brightness/alpha fields.

- [ ] RED tests prove one visible character remains exactly stable and multi-character focus interpolates to active/inactive values.
- [ ] Implement focus sampling without solo pulsing.
- [ ] GREEN tests; commit checkpoint.

### Task 8: Phase H — UI hover/pressed/release feedback

**Files:**
- Modify: `VnPresentationWorkshopVn10Types.cs`
- Modify: `VnPresentationWorkshopVn10Resolver.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseH.cs`

**Interfaces:**
- Produces `VnWorkshopUiFeedbackOverride`, `VnWorkshopButtonVisualState`, sampled scale/offset/brightness.

- [ ] RED tests prove pressed state changes independent Back/Next transforms and returns exactly to normal; baked Mute/Pause/Skip are marked overlay-only.
- [ ] Implement feedback evaluator with honest baked-control mode.
- [ ] GREEN tests; commit checkpoint.

### Task 9: Phase I — Motion lab UI + preview playback commands

**Files:**
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopVn10Preview.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopPreviewRenderer.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseI.cs`

**Interfaces:**
- Produces editor-only `VnWorkshopPreviewEffect`, playback controller and public preview-command methods.

- [ ] RED tests require Play/Pause/Restart/Step/instant-complete semantics, explicit effect commands and non-serialized playback state.
- [ ] Add collapsible `Presentation / Motion` sections: Typography, Text Reveal, Character Motion, Stage Layout, Background Transition, Speaker Focus, UI Feedback, Timing.
- [ ] Wire preview buttons without advancing Yarn/story state and repaint only while preview is active.
- [ ] GREEN tests; commit checkpoint.

### Task 10: Phase J — schema v2 / variants / Reference Motion Preview

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopSerialization.cs`
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopVn10Profiles.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTestsVn10PhaseJ.cs`

**Interfaces:**
- Schema 2 serializes VN10 tunables while accepting schema 1 with default VN10 values.
- Produces `ApplyReferenceMotionPreview(preset)` as an explicit non-Original starting profile.

- [ ] RED tests cover v2 round-trip, v1 compatibility, deterministic export filename, no ephemeral state, variants, immutable Original and Reference Motion Preview values.
- [ ] Implement schema upgrade/validation and explicit Reference Motion Preview button/profile.
- [ ] GREEN full Workshop EditMode; commit checkpoint.

### Task 11: Final technical proof

**Files:**
- Modify: `.github/workflows/vn10-presentation-workshop.yml` to add final-only regression/build/runtime jobs behind `[full-vn10-proof]`.

- [ ] Run source/production guard, all Workshop tests, schema/renderer/model tests, existing VN regression matrix, Full EditMode, Full PlayMode classifier, Windows Development Player and real `Rokas.exe` proof.
- [ ] Compare `43699c841e24ed806f377fbd9a7bec4d18f6cb1d..HEAD`; only editor Workshop/tests/docs/CI paths may differ.
- [ ] Re-check protected refs and confirm no integration/force-push.
- [ ] Report `READY FOR VN10 USER TUNING / MANUAL GUI REVIEW / TUNING PENDING / NOT INTEGRATED` and stop without applying any preset.
