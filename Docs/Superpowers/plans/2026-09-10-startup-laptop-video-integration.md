# Startup + Laptop Video Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Play the approved TIM Studio preview before the real Home presentation and play the approved ROKAS boot animation on every explicit Home→Laptop opening, without adding persistent laptop power state or changing unrelated systems.

**Architecture:** Keep the one-scene ROKAS architecture. Add one reusable `VideoSequencePresenter` owned by `RokasBootstrap`; startup playback happens before `Initialize(...)` constructs Home, while laptop playback is a transient `BOOTING → READY` state inside the existing `LaptopView`. Source MP4 bytes live unchanged in `Assets/StreamingAssets/RokasVideo` and are played by URL through one reusable `VideoPlayer`, one managed `AudioSource`, and a temporary RenderTexture released after every sequence.

**Tech Stack:** Unity 6000.3.19f1, C#, UnityEngine.Video.VideoPlayer, uGUI, NUnit/Unity Test Framework, GitHub Actions/GameCI.

**Spec:** Approved design gate in the 2026-09-10 conversation.

## Global Constraints

- Base branch and source of truth: `origin/integration/rokas-unified` at `5eb5d994c141757274f8702e7864caded0e2179d`.
- Work only on `feature/startup-laptop-video-integration` until final verification passes.
- No persistent laptop power-state/save-schema changes.
- Do not modify Weather, lighting, Combat, Messages/Yarn, package manifests, or ProjectVersion.
- Keep both supplied MP4 files byte-identical; no transcoding.
- Startup must never render Home before preview completion/skip/fallback.
- Laptop desktop must not exist during boot; closing/cancelling boot returns safely to Home.
- Completion is event-driven (`loopPointReached`/error) with defensive timeout only as fallback, never duration-based normal completion.
- Cleanup must stop playback, detach callbacks, stop audio, remove UI, and release/destroy the temporary RenderTexture exactly once.

---

### Task 1: RED behavior tests and focused CI

**Files:**
- Create: `Assets/Rokas/Tests/PlayMode/StartupLaptopVideoPlayModeTests.cs`
- Create: `Assets/Rokas/Tests/PlayMode/StartupLaptopVideoPlayModeTests.cs.meta`
- Create temporarily on the feature branch: `.github/workflows/startup-laptop-video-integration.yml`

**Interfaces:**
- Consumes: existing `RokasBootstrap`, `RokasView`, `LaptopView`, Home `LaptopHotspot`.
- Produces: failing behavioral contracts for startup gating and laptop boot gating before production code exists.

- [ ] **Step 1: Write failing startup test**

```csharp
[UnityTest]
public IEnumerator RealStartDoesNotBuildHomeBeforeStartupVideoGate()
{
    root = new GameObject("StartupVideoFixture");
    root.AddComponent<RokasBootstrap>();
    yield return null;
    Assert.That(Find("HomeTitle"), Is.Null);
    Assert.That(Find("StartupVideoSurface"), Is.Not.Null);
}
```

- [ ] **Step 2: Write failing laptop test**

```csharp
[UnityTest]
public IEnumerator LaptopOpeningShowsBootGateBeforeDesktop()
{
    CreateSynchronousBoot();
    Press("LaptopHotspot");
    yield return null;
    Assert.That(Find("LaptopBootSurface"), Is.Not.Null);
    Assert.That(Find("LaptopContracts"), Is.Null);
}
```

- [ ] **Step 3: Run only `Rokas.Tests.StartupLaptopVideoPlayModeTests` in GameCI**

Expected RED: startup test finds current Home immediately and laptop test finds current desktop immediately; failures must be assertion failures caused by the missing feature, not compilation/setup errors.

- [ ] **Step 4: Preserve RED XML/log artifact and commit the tests**

---

### Task 2: Minimal reusable playback presenter and original media

**Files:**
- Create: `Assets/Rokas/Scripts/Presentation/VideoSequencePresenter.cs`
- Create: `Assets/Rokas/Scripts/Presentation/VideoSequencePresenter.cs.meta`
- Create: `Assets/StreamingAssets.meta`
- Create: `Assets/StreamingAssets/RokasVideo.meta`
- Create: `Assets/StreamingAssets/RokasVideo/StartupPreview.mp4` plus `.meta`
- Create: `Assets/StreamingAssets/RokasVideo/LaptopBoot.mp4` plus `.meta`
- Test: `Assets/Rokas/Tests/PlayMode/StartupLaptopVideoPlayModeTests.cs`

**Interfaces:**
- Produces: `VideoSequencePresenter.PlayStartup(...)`, `PlayInHost(...)`, `Cancel()`, `IsPlaying`, and deterministic cleanup.
- Consumes: `Application.streamingAssetsPath`, a host `RectTransform` for laptop playback, callbacks for completion/cancel/fallback, and current video volume.

- [ ] **Step 1: Implement one VideoPlayer/AudioSource owner** with `playOnAwake=false`, `isLooping=false`, `waitForFirstFrame=true`, URL playback, `AudioSource` output, and a black surface shown before first decoded frame.
- [ ] **Step 2: Reveal video only on first frame**; normal completion uses `loopPointReached`; `errorReceived` and a defensive prepare/playback timeout route through the same idempotent finish path.
- [ ] **Step 3: Implement cleanup** by stopping player/audio, detaching handlers, clearing/destroying created UI, releasing/destroying the temporary RenderTexture, and invoking the completion callback at most once.
- [ ] **Step 4: Add both original MP4 bytes unchanged** and verify repository blob hashes against the supplied SHA-256 values outside Unity.

---

### Task 3: Startup integration

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/RokasBootstrap.cs`
- Test: `Assets/Rokas/Tests/PlayMode/StartupLaptopVideoPlayModeTests.cs`

**Interfaces:**
- `RokasBootstrap.Start()` starts startup preview only for the real application-start path when `Session == null`.
- Existing public `Initialize(string saveDirectory)` remains synchronous for fixtures/tests and never waits for the preview.

- [ ] **Step 1: Route real `Start()` through the presenter** so no camera/Home/View is constructed before preview finish/skip/fallback.
- [ ] **Step 2: On startup completion call the existing synchronous `Initialize(...)` exactly once.**
- [ ] **Step 3: Support startup skip after playback has actually begun with `Esc`, `Space`, or `Enter`; all paths share the same finish guard.**
- [ ] **Step 4: Run focused startup tests GREEN.**

---

### Task 4: Laptop transient boot integration

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/RokasView.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/LaptopView.cs`
- Modify minimally: `Assets/Rokas/Scripts/Presentation/RokasAudio.cs`
- Test: `Assets/Rokas/Tests/PlayMode/StartupLaptopVideoPlayModeTests.cs`

**Interfaces:**
- Every explicit `OpenPanel("laptop")` starts a new transient boot sequence.
- `LaptopView` exposes `Booting`; desktop content is built only after the presenter completion callback.
- `Escape`/close during boot cancels playback and uses the existing panel-close path; no persistent state is saved.

- [ ] **Step 1: Build only the existing laptop frame/screen shell and `LaptopBootSurface` during boot; do not build desktop tiles.**
- [ ] **Step 2: On successful/fallback completion remove boot media and call the existing desktop builder.**
- [ ] **Step 3: On close/escape during boot call presenter `Cancel()` and complete the existing close animation/path without constructing desktop.**
- [ ] **Step 4: Feed video audio volume from the existing master×sfx settings seam; do not alter ambience/music behavior.**
- [ ] **Step 5: Run focused laptop tests GREEN, including close/cancel and reopening behavior.**

---

### Task 5: Actual-media runtime/build proof

**Files:**
- Extend: `Assets/Rokas/Tests/PlayMode/StartupLaptopVideoPlayModeTests.cs`
- Extend temporarily: `.github/workflows/startup-laptop-video-integration.yml`

**Interfaces:**
- Actual-media proof must use the committed `StreamingAssets` files rather than mocks/generated clips.

- [ ] **Step 1: Run a focused PlayMode probe that observes startup first-frame readiness, skips it, confirms Home appears once, opens laptop, observes boot playback/first frame, and confirms desktop appears after completion/fallback without duplicate VideoPlayer/AudioSource/RenderTexture ownership.**
- [ ] **Step 2: Run `Rokas/Validate Project`.**
- [ ] **Step 3: Build Windows x64 with `RokasBuild.BuildWindows`; archive build log and built `Rokas.exe` tree manifest.**
- [ ] **Step 4: Verify both MP4 files are present in the built player StreamingAssets and hashes match source bytes.**

---

### Task 6: Review, verification, cleanup, checkpoint, unified

**Files:**
- Remove feature-only `.github/workflows/startup-laptop-video-integration.yml` before checkpoint.
- Update/add concise verification documentation only if needed to record artifact truth.

- [ ] **Step 1: Run structured code review against base `5eb5d994...`; reject any scope creep into Weather/lighting/Combat/Messages/packages/ProjectVersion.**
- [ ] **Step 2: Run verification-before-completion using actual XML/log/build/hash evidence and require a clean feature tree.**
- [ ] **Step 3: Remove the temporary feature CI workflow and verify the final production diff contains only approved startup/laptop video files/tests/docs.**
- [ ] **Step 4: Create a VERIFIED checkpoint commit only after verification passes.**
- [ ] **Step 5: Fast-forward `integration/rokas-unified` to the verified checkpoint with `force=false`; leave `development` untouched.**
- [ ] **Step 6: Re-read unified ref/tree and confirm ancestry/diff after the fast-forward.**
