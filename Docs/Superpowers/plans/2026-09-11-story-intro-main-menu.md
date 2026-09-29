# Story Intro + Main Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Insert the supplied 50.888333-second story intro and a new animated ROKAS main menu between the already-verified Startup Preview and the existing Home, without changing unrelated gameplay systems or the established Startup Preview/Laptop boot semantics.

**Architecture:** Keep `RokasBootstrap` as the startup state-machine owner. Extend `VideoSequencePresenter` with a narrowly scoped story-cinematic playback entry that reuses its existing black-first-frame/fallback/cleanup path but does not change `PlayStartup` or `PlayInHost` behavior. Add a dedicated `MainMenuView` that owns only the menu canvas, button interactions, and looping background playback; entering the world disposes the menu and calls the existing `BuildPresentation()` path exactly once.

**Tech Stack:** Unity 6000.3.19f1, Built-in Render Pipeline, uGUI, UnityEngine.Video, NUnit/Unity Test Framework, StreamingAssets.

**Spec:** User-approved startup-flow prompt in the 2026-09-11 conversation.

## Global Constraints

- Existing Startup Preview flow remains intact and still runs first.
- Final runtime order: Startup Preview -> Story Intro -> Main Menu -> `ВОЙТИ В МИР` -> existing Home.
- Do not touch Combat, Weather, Messages/Yarn, SaveData schema, contract/economy systems, packages, or unrelated presentation systems.
- `development` must not be modified.
- Story asset source is the supplied `ROKAS_intro_51s_original_SFX_plus_full_voice(1).mp4` (50.888333 s, H.264/AAC, 910x512, SHA-256 `4f1cf02df5e52f8dd75645ceb1c6b0d5b55b0eb952eee1371d742ca54395c6d4`).
- Menu loop source is the supplied `ROKAS_seamless_loop(1).mp4` (9.4 s, H.264/AAC, 910x512, SHA-256 `bb2eb60336c4b1e63a5b2a177c0db904b6d32bee5f59c48568def81cfa8beef5`).
- Preserve `StartupSkipHintOverlay`, first-frame visibility gating, `Input.anyKeyDown` skip semantics for the existing Startup Preview, and `Finish()` as the terminal owner for that existing path.
- Do not make Home visible behind the story intro or menu.

---

### Task 1: RED — startup sequence contract

**Files:**
- Create: `Assets/Rokas/Tests/PlayMode/StoryIntroMainMenuPlayModeTests.cs`
- Create: `Assets/Rokas/Tests/PlayMode/StoryIntroMainMenuPlayModeTests.cs.meta`

**Interfaces:**
- Consumes current `RokasBootstrap`, `VideoSequencePresenter`, existing `HomeTitle` naming.
- Produces executable behavior contract for story gate, menu gate, and single Home entry.

- [ ] **Step 1: Add failing tests** proving: after skipping the existing Startup Preview, Home is still absent and `StoryIntroVideoSurface` owns the screen; after completing/skipping the story test seam, `RokasMainMenu` exists while Home remains absent; pressing `EnterWorldButton` removes the menu and builds exactly one `HomeTitle`; explicit fixture `Initialize(...)` remains synchronous and bypasses the launch-only story/menu flow.
- [ ] **Step 2: Run only this test class in CI and verify RED for missing story/menu behavior, not compile/setup errors.**

### Task 2: GREEN — story-cinematic gate without regressing existing video semantics

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/VideoSequencePresenter.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/RokasBootstrap.cs`
- Test: `Assets/Rokas/Tests/PlayMode/StoryIntroMainMenuPlayModeTests.cs`

**Interfaces:**
- Add a dedicated story playback entry using the same presenter lifecycle and cleanup path.
- Existing `PlayStartup(...)` and `PlayInHost(...)` contracts remain unchanged.

- [ ] **Step 1: Implement the minimum story playback API needed by the RED tests.** Story playback uses a playback deadline long enough for the supplied 50.888333 s asset and safe fallback to the menu.
- [ ] **Step 2: Change only the real `Start()` callback chain from `StartupPreview -> BuildPresentation` to `StartupPreview -> StoryIntro -> MainMenu`; leave explicit `Initialize(...)` behavior unchanged.**
- [ ] **Step 3: Run focused story/menu tests and existing startup/laptop video tests; require GREEN.**

### Task 3: GREEN — dedicated animated main menu

**Files:**
- Create: `Assets/Rokas/Scripts/Presentation/MainMenuView.cs`
- Create: `Assets/Rokas/Scripts/Presentation/MainMenuView.cs.meta`
- Modify: `Assets/Rokas/Scripts/Presentation/RokasBootstrap.cs`
- Test: `Assets/Rokas/Tests/PlayMode/StoryIntroMainMenuPlayModeTests.cs`

**Interfaces:**
- `MainMenuView` owns a `RokasMainMenu` overlay, looping menu background, and three buttons named `EnterWorldButton`, `DevelopersButton`, `SupportDevelopmentButton` with the exact Russian labels in the supplied design.
- `EnterWorldButton` invokes one callback into `RokasBootstrap` to dispose the menu and call existing `BuildPresentation()` once.
- Secondary buttons remain contained in the menu and must not mutate gameplay/session state.

- [ ] **Step 1: Extend RED tests for exact menu object/button names and single-entry behavior.**
- [ ] **Step 2: Implement `MainMenuView` to match the supplied composition: full-screen animated Tokyo/yokai loop, centered ROKAS branding already present in the loop, gold highlighted `ВОЙТИ В МИР`, dark secondary buttons `О РАЗРАБОТЧИКАХ` and `ПОДДЕРЖАТЬ РАЗРАБОТКУ`.**
- [ ] **Step 3: Run focused tests and existing startup/laptop suites; require GREEN.**

### Task 4: Media integration

**Files:**
- Create: `Assets/StreamingAssets/RokasVideo/StoryIntro.mp4`
- Create: `Assets/StreamingAssets/RokasVideo/MainMenuLoop.mp4`

**Interfaces:**
- Story playback resolves `StoryIntro.mp4` through the existing StreamingAssets URL/path mechanism.
- Main menu resolves `MainMenuLoop.mp4` from the same `RokasVideo` directory.

- [ ] **Step 1: Commit exact supplied bytes under stable production filenames.**
- [ ] **Step 2: Verify repository blob hashes against the supplied source hashes.**
- [ ] **Step 3: Run real-media PlayMode proof: both committed files decode a first frame; story completes/falls through to menu; menu loop remains alive until `EnterWorldButton`.**

### Task 5: Runtime proof and regression gates

**Files:**
- Add/modify feature-scoped CI workflow only if needed to run bounded Unity tests and Windows Player proof.

- [ ] **Step 1: Run focused StoryIntro/MainMenu GREEN.**
- [ ] **Step 2: Run existing `StartupLaptopVideoPlayModeTests`, startup skip/home-visit semantics, and exact lifecycle bounded suites.**
- [ ] **Step 3: Build/launch a real Windows Player long enough to capture Startup Preview -> Story Intro -> Main Menu, then activate `ВОЙТИ В МИР` and capture existing Home.**
- [ ] **Step 4: Inspect actual screenshots/video frames for no Home flash, white frame, stretched/frozen transition, or duplicate Home.**
- [ ] **Step 5: Run validator comparison, media hashes, `git diff --check`, and exact scope review.**

### Task 6: Review, verification, checkpoint, integration

- [ ] **Step 1: Apply `superpowers:requesting-code-review`; resolve Critical/Important findings.**
- [ ] **Step 2: Apply `superpowers:verification-before-completion` with fresh evidence.**
- [ ] **Step 3: Create a tree-identical verified checkpoint commit.**
- [ ] **Step 4: Prove `integration/rokas-unified` is an ancestor, then no-force fast-forward it to the verified checkpoint.**
- [ ] **Step 5: Prove `development` remained unchanged and stop.**
