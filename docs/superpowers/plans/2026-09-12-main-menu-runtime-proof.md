# Main Menu Runtime Proof Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the already-implemented Main Menu hover/audio/Enter-to-Home transition in a real Windows Development Player without changing the feature production tree.

**Architecture:** Reuse the established ROKAS temporary runtime-probe pattern used by `RokasHomeVisitRuntimeProbe` and the existing `RokasVideoSmokeBuild.Build` player build seam. The WIP-only workflow checks out the exact feature HEAD, injects a dormant command-line-gated probe only in the CI workspace, builds/runs Windows, captures factual JSON plus screenshots, and leaves the feature/final tree untouched.

**Tech Stack:** Unity 6000.3.19f1, game-ci/unity-builder@v4, GitHub Actions Ubuntu + Windows runners, Unity UI/EventSystem, ScreenCapture.

**Spec:** Existing ROKAS milestone acceptance in the current task: Main Menu hover feedback, ButtonClick/EnterGame/LaptopMouseClick routing, full-black Home handoff, visual transition sequence, no duplication/truncation.

## Global Constraints

- Exact feature candidate must be verified before build.
- Do not modify production code in the feature branch.
- QA probe exists only in the temporary build workspace/WIP workflow.
- Use existing `Rokas.Editor.RokasVideoSmokeBuild.Build`.
- Require real Windows Player JSON evidence and screenshots before PASS.
- Preserve exact approved WAV source SHA-256 values.

---

### Task 1: Build exact candidate with temporary probe

**Files:**
- Modify temporarily in CI workspace only: `Assets/Rokas/Scripts/Presentation/RokasMainMenuRuntimeProbe.cs`
- Modify WIP only: `.github/workflows/temp-approved-audio-transfer.yml`

**Interfaces:**
- Consumes: current feature HEAD, existing `RokasBootstrap`, `MainMenuView`, `LaptopTileFeedback`, `RokasAudio`, `RokasVideoSmokeBuild.Build`.
- Produces: Windows Development Player and source/build identity evidence.

- [ ] Verify checkout HEAD/tree and exact WAV SHA-256.
- [ ] Inject command-line-gated probe without committing it to feature.
- [ ] Build StandaloneWindows64 Development Player through existing build method.
- [ ] Upload player and identity evidence.

### Task 2: Exercise runtime behavior and capture proof

**Files:**
- Runtime output only: `rokas-main-menu-proof/*.png`, JSON, Player.log.

**Interfaces:**
- Consumes: built player from Task 1.
- Produces: hover, fade, Home handoff, and audio-routing evidence.

- [ ] Reach real Main Menu and capture normal state.
- [ ] Drive Enter/About/Support hover and smooth pointer-exit feedback, capturing frames.
- [ ] Verify About/Support use `ButtonClick`.
- [ ] Press Enter twice, verify exactly one `EnterGame`, no generic click, and capture partial/deeper/full-black/Home-under-black/fade-in/stable-Home states.
- [ ] Verify the same persistent EnterGame source is still playing after Home handoff on Windows.
- [ ] Verify ordinary Home UI uses `ButtonClick`.
- [ ] Open Laptop and verify an internal tile uses `LaptopMouseClick` while generic Laptop click is suppressed.
- [ ] Write JSON and exit nonzero on any failed invariant.

### Task 3: Inspect and gate evidence

**Files:**
- No repository modifications.

**Interfaces:**
- Consumes: JSON/screenshots/logs from Task 2.
- Produces: RUNTIME/VISUAL/AUDIO PASS or a concrete first failure.

- [ ] Require process exit 0 and `finalPass=true`.
- [ ] Inspect every required screenshot rather than trusting workflow conclusion alone.
- [ ] Confirm JSON audio observations match the acceptance contract.
- [ ] If any invariant fails, return to systematic-debugging and fix only the proven cause.
- [ ] Remove this temporary plan/workflow artifact during final WIP cleanup.
