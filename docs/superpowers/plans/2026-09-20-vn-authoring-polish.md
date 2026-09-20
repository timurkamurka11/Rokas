# VN Authoring Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Scene+Speaker color overrides, layout-stable smooth dialogue reveal, and smooth BGM/additional-audio transitions without regressing accepted VN authoring behavior.

**Architecture:** Extend the existing Scene Composer model and existing playback services rather than creating parallel subsystems. Speaker color resolves through one resolver; dialogue reveal remains a playback sample layered over the final authored text layout; audio transitions stay inside the existing music/layered-audio playback owners.

**Tech Stack:** Unity 6000.3.19f1, C#, Unity Editor IMGUI preview, NUnit EditMode tests, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-20-vn-authoring-polish-design.md`

## Global Constraints

- Branch: `feature/vn-scene-composer`; baseline `441180a45d701f3c9a1b3198f6f13549103b117b`.
- Preserve project `3cc2bc9ec7974ea7a5f4d074683bed61` and its assets/.meta/GUIDs.
- Do not alter established speaker/body geometry scopes or project-global plaque geometry.
- Do not reimplement M-SFX Continuity, primary BGM authoring, or video reliability.
- Do not run Final Certification before manual PASS.
- Review BAT must use the established CLASSIC_FIXED standalone workflow.

## Review Focus

- Text-only speakers whose names differ only by surrounding whitespace resolve to the same local Scene key.
- Rich-text + Unicode dialogue reveals without literal tags, split surrogate pairs, or layout reflow.
- Next/Previous/Play From Here cannot leak reveal state into another Beat.
- Repeated Scene navigation cannot leave duplicate BGM/additional AudioSources.
- Explicit zero fades remain instant while non-zero fades are frame-rate independent.

---

### Task 1: Scene+Speaker speaker-color layer

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerTypes.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerSerialization.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerTextStyleResolver.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerTextElements.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerMTextStyleLayoutTests.cs`

**Interfaces:**
- Produces: `ResolveSpeakerKey(VnSceneComposerDialogueBeat)`, Scene-local speaker override list, three-value speaker-color scope.
- Consumers: Task 2 playback frames use the same typography resolver; no geometry API changes.

- [ ] Add failing tests for one Scene with Keiko/White and Mina/Orange, local isolation, cross-Scene isolation, text-only key normalization, empty speaker, Save/Reopen, duplicate, Undo, preview/play parity, and geometry invariance.
- [ ] Run the focused STYLE/LAYOUT fixture and verify RED is specifically missing Scene+Speaker behavior.
- [ ] Add the minimal serialized Scene+Speaker override model and normalization.
- [ ] Resolve color in order Scene+Speaker -> Scene -> Global; preserve shared opacity.
- [ ] Extend UI to three scopes and show current speaker/resolved color.
- [ ] Run STYLE/LAYOUT to GREEN and commit.

### Task 2: Layout-stable smooth dialogue reveal

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerTransitionSampler.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerElapsedTransitionSampler.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerPlaybackController.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopPreviewRenderer.Playback.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopPreviewRenderer.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerPlaybackTests.DialogueBeats.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerMTextTests.cs`

**Interfaces:**
- Consumes: existing `VnWorkshopTypewriterValues` and Beat elapsed time.
- Produces: reveal sample metadata with plain visible text plus full layout/render text and completion state.

- [ ] Add failing tests for stable full layout text, monotonic visible glyph count, final completion, speed, FPS independence, short glyph fade, rich text, Cyrillic, Chinese, long text, complete-first Next, restart/Previous/Play From Here reset, and geometry invariance.
- [ ] Verify RED against the current substring implementation.
- [ ] Implement grapheme/rich-text-aware reveal sampling that keeps full layout text and exposes alpha-masked render text.
- [ ] Make playback frame carry reveal completion/metadata; make renderer use the registered render text while compatibility APIs retain plain visible text.
- [ ] Make AdvanceDialogue first force-complete an incomplete line and only advance on the next call.
- [ ] Run dialogue/M-TEXT focused fixtures to GREEN and commit.

### Task 3: Smooth BGM and layered-audio transitions

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerMusicPlayback.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerLayeredAudioPlayback.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerPlaybackController.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerAudioTests.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerLayeredAudioTests.cs`

**Interfaces:**
- Produces: bounded dual-source BGM crossfade state; existing layered-audio source pool gains deterministic Scene-exit fade ownership.
- Consumers: playback controller continues to call `Apply`, `Advance`, `EnterScene`, and `ExitCurrentScene`.

- [ ] Add failing tests for Keep Previous continuity, A->B crossfade, A->Silence fade, Silence->B fade in, source release, no duplicates, inherited loop continuity, non-inherited Scene-exit fade, Stop Beat fade, Music Layer, one-shot stability, repeated navigation, Stop->Play clean, video/staging/reveal independence.
- [ ] Verify RED against the current single-source sequential BGM fade and any hard Scene-exit additional-audio path.
- [ ] Convert BGM playback to two bounded reusable sources with elapsed-time crossfade while preserving same-track/Keep Previous state.
- [ ] Ensure layered audio exits non-inherited cues through authored fade-out without touching inherited sources.
- [ ] Run audio/layered-audio focused fixtures to GREEN and commit.

### Task 4: Affected regressions, exact checkpoint and review handoff

**Files:**
- Modify only test fixtures if regressions reveal outdated expectations.
- Create after GREEN: `ROKAS_VN_POLISH_REVIEW_<SHORT_SHA>.bat` as conversation artifact, not product code.

**Interfaces:**
- Consumes all three finished behaviors.
- Produces exact automated-GREEN HEAD/TREE/run and manual review copy.

- [ ] Run Source / Production Guard.
- [ ] Run M-TEXT STYLE/LAYOUT, M-TEXT, M-CHARACTER, M-SFX Continuity/M-SFX, M-AUDIO, M-VIDEO Reliability, M-TRANSITION, SC-E, SC-G, SC-H, SC-I, UX-A-H using the existing workflow and report actual counts.
- [ ] Confirm remote tip equals the exact GREEN successor.
- [ ] Build review BAT from the last known-good CLASSIC_FIXED template: direct branch clone when needed, exact HEAD/TREE, automatic Unity discovery, migration of project `3cc2bc9ec7974ea7a5f4d074683bed61`, no TEMP/worktree/reset/clean/gc/repack/prune/delete-old-review.
- [ ] Stop at manual review. Do not run Final Certification.
