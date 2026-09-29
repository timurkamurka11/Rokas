# VN UI Workshop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an editor-only VN UI Workshop that mirrors the verified VN presentation with real assets, stores override-only presets locally, supports direct tuning/variants/JSON transfer, and proves production runtime remains unchanged.

**Architecture:** Keep the Workshop entirely inside `Rokas.Editor`. A code-defined immutable baseline captures the verified 1920x1080 layout values from source HEAD `04a53a955286bb926365459ee11c6836ae490282`; a separate serializable preset stores only enabled overrides. A pure resolver produces preview geometry for the selected resolution, and an IMGUI EditorWindow renders the real `RokasAssets`/`VnCharacterVisualCatalog` content without touching `VnIntroView`.

**Tech Stack:** Unity 6000.3.19f1, C#, UnityEditor IMGUI, NUnit/EditMode tests, GitHub Actions/game-ci.

**Spec:** `docs/superpowers/specs/2026-09-13-vn-ui-workshop-design.md`

## Global Constraints

- Work only on `feature/vn-ui-workshop`, created exactly from `04a53a955286bb926365459ee11c6836ae490282`.
- Do not update `feature/vn-visual-polish-refs`, `integration/rokas-unified`, or `development`.
- Keep production `VnIntroView`, Controller, Presenter, Yarn files, and `RokasAssets` unchanged unless a genuine blocker is proven.
- All Workshop implementation must compile in the editor-only `Rokas.Editor` assembly.
- Baseline is immutable and exposes exact `SourceHead = 04a53a955286bb926365459ee11c6836ae490282`.
- Presets contain only overrides; baseline state is never copied into saved user data.
- Preview uses real ROKAS assets and authored character UV states; no mock graphics.
- 1920x1080 is the logical reference; 1280x720 and 1024x768 use the production CanvasScaler model.
- Mute/Pause/Skip are labeled `Baked into panel`; only hit regions are independently editable.
- Variants live under `Library/ROKAS/VnUiWorkshop/variants/` and never under `Assets/`.
- Save/Export never applies to production and baseline is never overwritten.
- Import source-head mismatch must be explicit.
- Final state is `READY FOR USER WORKSHOP REVIEW` / `NOT INTEGRATED`.

---

### Task 1: RED test slice and Workshop-only CI guard

**Files:**
- Create: `.github/workflows/vn-ui-workshop.yml`
- Create: `Assets/Rokas/Tests/EditorWorkshop/Rokas.VnWorkshop.EditModeTests.asmdef`
- Create: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTests.cs`
- Create matching `.meta` files for the new test directory, asmdef, and test source.

**Interfaces:**
- Consumes: verified base commit and existing `Rokas.Editor` assembly.
- Produces: failing `Rokas.EditorTools.Tests.VnUiWorkshopTests` and a feature-only Actions workflow that refuses forbidden production diffs.

- [ ] **Step 1: Write the failing tests using reflection so the repository still compiles before Workshop code exists.**

The first test must require type `Rokas.EditorTools.VnUiWorkshop.VnPresentationWorkshopBaseline` from assembly `Rokas.Editor`, then assert exact SourceHead/reference resolution. Additional reflection tests require preset/resolver/storage APIs and baked-control metadata.

- [ ] **Step 2: Add `vn-ui-workshop.yml` with `push` on `feature/vn-ui-workshop`.**

The default jobs are:

```text
source-guard
workshop-editmode
```

`source-guard` must run `git merge-base --is-ancestor 04a53a... HEAD`, `git diff --check`, and reject changed files outside the approved Workshop/docs/workflow/test paths. It must explicitly reject changes to `VnIntroView`, Controller, Presenter, Yarn content, and `RokasAssets`.

- [ ] **Step 3: Push the RED commit and inspect the Actions run.**

Expected: source guard passes; Workshop EditMode fails because the required Workshop type/API is absent. Record the run id and failure evidence before implementing code.

---

### Task 2: Immutable baseline, override preset, resolver, and editing model

**Files:**
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopTypes.cs`
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopBaseline.cs`
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopResolver.cs`
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopEditing.cs`
- Create matching `.meta` files.
- Update: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTests.cs`

**Interfaces:**
- Produces: `VnWorkshopElement`, `VnWorkshopResolution`, `VnWorkshopRect`, `VnWorkshopElementOverride`, `VnWorkshopFocusOverride`, `VnPresentationWorkshopPreset`, `VnPresentationWorkshopBaseline`, `VnPresentationWorkshopResolver`, `VnPresentationWorkshopEditing`.

- [ ] **Step 1: Extend failing tests for exact baseline constants and override-only semantics.**

Tests assert:

```text
SourceHead == 04a53a955286bb926365459ee11c6836ae490282
ReferenceResolution == (1920,1080)
Default preset HasAnyOverride == false
ResetElement clears one group
ResetAll clears all groups
```

- [ ] **Step 2: Verify RED.**

Expected: tests fail on missing model members while source guard remains green.

- [ ] **Step 3: Implement minimal immutable baseline and override containers.**

Baseline constants mirror verified runtime values: panel anchors `.04/.96`, bottom `-98`, aspect `2048/682`; Mina height `1240`, y factor `.37`; speaker/dialogue anchors+offsets; Mute/Pause/Skip and Back/Next anchors/sizes; two-character offset `310`; active scale `1.05`; inactive scale `.94`; inactive brightness `.76`; inactive alpha `.84`.

- [ ] **Step 4: Implement pure resolution helpers.**

Use:

```csharp
float logWidth = Mathf.Log(screenWidth / 1920f, 2f);
float logHeight = Mathf.Log(screenHeight / 1080f, 2f);
float scaleFactor = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, .5f));
return new Vector2(screenWidth / scaleFactor, screenHeight / scaleFactor);
```

Resolve panel and child rectangles from anchors/offsets against the selected virtual canvas. Apply preset position/size/scale deltas without mutating baseline.

- [ ] **Step 5: Implement drag/nudge/reset editing.**

`Nudge(element, delta)` and `ApplyDrag(element, logicalDelta)` update only the selected override group. Arrow callers use 1px; Shift+arrow callers use 10px.

- [ ] **Step 6: Verify GREEN.**

Expected: Workshop EditMode test job passes and source guard passes.

---

### Task 3: Variant storage and portable JSON

**Files:**
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopSerialization.cs`
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopStorage.cs`
- Create matching `.meta` files.
- Update: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTests.cs`

**Interfaces:**
- Produces: `VnPresentationWorkshopDocument`, `VnWorkshopImportResult`, serializer validation, and local variant CRUD.

- [ ] **Step 1: Add RED tests for JSON round-trip, invalid data, source mismatch, and local paths.**

Tests assert:

```text
round trip preserves enabled overrides only
NaN/Infinity or invalid sizes are rejected
mismatched sourceHead returns SourceHeadMismatch=true and names both heads
variant directory resolves under <project>/Library/ROKAS/VnUiWorkshop/variants
safe variant names cannot escape that directory
```

- [ ] **Step 2: Verify RED.**

- [ ] **Step 3: Implement serializer envelope.**

Envelope fields: schemaVersion, sourceHead, variantName, preset. Current schema version is `1`. The preset object remains override-only.

- [ ] **Step 4: Implement validation and source mismatch reporting.**

Deserialize into a temporary document; validate before replacing live state. Never silently accept incompatible schema or non-finite/unsafe values.

- [ ] **Step 5: Implement local variant CRUD.**

Use project-root-relative `Library/ROKAS/VnUiWorkshop/variants`. Save/load/duplicate/rename/delete operate only there. No AssetDatabase production writes.

- [ ] **Step 6: Verify GREEN.**

---

### Task 4: Real-asset mirror renderer and EditorWindow

**Files:**
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopPreviewRenderer.cs`
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.cs`
- Create matching `.meta` files.
- Update: `Assets/Rokas/Tests/EditorWorkshop/VnUiWorkshopTests.cs`

**Interfaces:**
- Consumes: `Resources.Load<RokasAssets>("RokasAssets")`, `VnCharacterVisualCatalog`, resolver, storage.
- Produces: menu `ROKAS/VN UI Workshop` and usable first-version Workshop.

- [ ] **Step 1: Add RED editor tests for asset-backed preview dependencies and baked-control semantics.**

Require real `RokasAssets`, Mina/Keiko authored UV states, and metadata that Mute/Pause/Skip are baked while Back/Next are independent.

- [ ] **Step 2: Verify RED.**

- [ ] **Step 3: Implement preview renderer.**

Draw actual selected background, full authored panel, Mina/Keiko sheet UV body where appropriate, speaker/dialogue text using the real ROKAS font, Back/Next glyphs, and selectable hit-region outlines. Do not draw separate Mute/Pause/Skip icon textures as movable objects.

- [ ] **Step 4: Implement window layout and preview state controls.**

Left rail: preview state, 1920x1080 / 1280x720 / 1024x768, variant controls, Before/After. Center: fitted interactive canvas. Right inspector: selected element numeric controls and focus values.

- [ ] **Step 5: Implement direct manipulation.**

Mouse-down selects, drag applies logical deltas, arrows apply 1px, Shift+arrows 10px. Numeric fields edit current resolved position/size/scale by converting to override deltas. Include Reset Element and confirmed Reset All.

- [ ] **Step 6: Implement import/export UI.**

Export defaults to filename `ROKAS_VN_WORKSHOP_PRESET.json`. Import validates before assignment and uses an explicit confirmation dialog when source head differs.

- [ ] **Step 7: Verify GREEN and open-window smoke behavior in EditMode.**

---

### Task 5: Feature verification and production invariance proof

**Files:**
- Update: `.github/workflows/vn-ui-workshop.yml` only if final verification wiring needs correction.
- Update: `docs/superpowers/plans/2026-09-13-vn-ui-workshop.md` to record final evidence.

**Interfaces:**
- Produces: exact final feature HEAD, green Workshop tests, green existing VN regressions, Windows build/runtime proof, protected-ref invariance evidence.

- [ ] **Step 1: Run the Workshop-only verification on the exact candidate HEAD.**

Require source guard + Workshop EditMode green.

- [ ] **Step 2: Run existing VN regression filters at the same exact HEAD.**

Run the existing VN progress EditMode, VN flow, visual-layout, responsive-layout, runtime-proof-harness, legacy Main Menu interaction/audio, Story/Main Menu, full EditMode, and classified full PlayMode suites.

- [ ] **Step 3: Run Windows development-player build and real `Rokas.exe` runtime proof at the same exact HEAD.**

Require the existing real-player screenshots/JSON proof contract to pass unchanged.

- [ ] **Step 4: Compare final feature HEAD against base.**

Require zero changes to production `VnIntroView`, Controller, Presenter, Yarn files, `RokasAssets`, approved VN PNGs/metas, and no user variant files under `Assets/`.

- [ ] **Step 5: Re-fetch protected refs.**

Require `feature/vn-visual-polish-refs`, `integration/rokas-unified`, and `development` to match their pre-task SHAs unless changed externally; this task must have made no writes to them.

- [ ] **Step 6: Stop without integration.**

Final report must state exactly:

```text
READY FOR USER WORKSHOP REVIEW
NOT INTEGRATED
```

### Remote editor proof evidence

- Automated EditorWindow proof is GREEN on `3ecee701537d17b4cc45a96e8c918f2d66295015` with Workshop EditMode `16/16` passed.
- The proof invokes the real `ROKAS/VN UI Workshop` EditorWindow entry point, resolves authored ROKAS VN assets, builds representative 1280x720 and 1024x768 preview frames, and verifies local variant persistence across window re-instantiation.
- Human visual/interaction judgment is intentionally not claimed by the remote batchmode proof; `MANUAL GUI REVIEW` remains pending user review.
