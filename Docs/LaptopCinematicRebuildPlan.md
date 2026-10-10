# ROKAS Laptop Cinematic Rebuild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace low-poly double-hand laptop intro with a realistic right-only animated interaction, physical one-shot click, LCD wake and one original boot followed by a 0.5s hold.

**Architecture:** Preserve Home `LaptopHotspot → RokasView.OpenFromHomeLaptopHotspot → LaptopCinematicSequence → LaptopView.Build → VideoSequencePresenter.PlayInHost`. Timeline tracks one-shot contact and wake phases. The existing LaptopView remains sole owner of `LaptopBoot.mp4`; the cinematic only adds visuals and SFX before that handoff. New right-hand art must be produced and reviewed from a suitable high-quality licensed source; old PSX 84 frames must not be claimed final.

**Tech Stack:** Unity 6000.3.19f1 / C# / Unity UI / Unity Test Framework / Blender (when available) / GitHub Actions GameCI.

**Spec:** User-approved `ROKAS_Laptop_Cinematic_SUPERPOWERS_MASTER_PROMPT_20261010.md` (attached to this conversation; exact copy also provided in local handoff package).

## Global constraints
- Local production target is `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`, inaccessible to GitHub/cloud connector. This feature branch is a staged patch source, *not* local deployment.
- Preserve user's current POV PNG byte-for-byte and screen calibration until verified in Game View.
- Do not edit/overwrite unrelated local changes. Never `git reset --hard`, `git clean`, force branch checkout, clone into review worktrees or delete old backup.
- Single right hand with realistic skin and loose graphite sleeve; real finger contact; no false claims about art.
- Only home hotspot invokes cinematic; hallway/programmatic routes retain previous behavior.
- One mechanical click only after contact; original LaptopBoot.mp4 exactly once; 0.5s after its actual completion; do not truncate movie.
- Full PlayMode visual/audio footage required before production-ready claim.

## Review focus
1. Boot returned from video callback while cinematic/requested window is closing → must not build ready UI.
2. Repeated click/ESC before press → must not trigger delayed click or open YOMI twice.
3. No art or wrong importer sizes → diagnostics and safe original laptop route; don't pretend final.
4. Missing or damaged boot MP4 → fallback finishes once; 0.5s hold scoped to cinematic.
5. Screen quad corners / key position unverified → no claim of exact visual contact.

---

### Task 1: Power one-shot timeline, frame-independent wake
**Files:** Add `Assets/Rokas/Scripts/Presentation/LaptopPowerTimeline.cs` and `.meta`; add `Assets/Rokas/Tests/EditMode/LaptopPowerTimelineEditModeTests.cs` and `.meta`; update CI filter.
**Interface:** `LaptopPowerTimeline.Advance(float):bool`, `Cancel():void`, `StageAt(float):WakeStage`. 3.10 contact, 3.35 glow end, 3.75 dark-awake end.
- [x] Write tests for pre-contact/no double-click, cancellation, 24/30/60/120fps, wake order.
- [ ] Run cloud EditMode RED: compilation or assertions fail before implementation; keep exact workflow URL.
- [ ] Implement timeline.
- [ ] Run cloud EditMode GREEN and verify XML evidence; commit.

### Task 2: Only-one-boot handoff and 0.5s ready hold
**Files:** Modify `LaptopView.cs`, `RokasView.cs`; add `Assets/Rokas/Tests/PlayMode/LaptopCinematicBootHandoffPlayModeTests.cs`.
**Interface:** scoped `RequestCinematicPostBootHold()` called after `laptop.Reset()` and before `laptop.Build()` only from successful home cinematic. Existing `VideoSequencePresenter` owns one video.
- [ ] Write PlayMode test for one YomiLaptop, video Booting after handoff, hold from actual video completion and ReadyFrame after 0.5s.
- [ ] Run RED, implement minimal scoped hold state, then run GREEN.
- [ ] Cover close during boot, skip during hand approach and programmatic open.

### Task 3: New right-hand production art
**Files:** `Assets/Rokas/Art/LaptopCinematic/Source/*`; `Assets/Rokas/Resources/LaptopCinematic/right_hand_manifest.json`, `HandsRight/*`; importer meta.
- [ ] Secure high-quality licensed sculpt/rig source, documented URI and license; no low-poly PSX hand.
- [ ] Calibrate physical Power key with visible evidence, update `screen_calibration.json` only after review.
- [ ] Rig finger depression and roomy cloth sleeve, match scene lens/lighting. Save editable model/animation.
- [ ] Render alpha/shadow 24–60fps; provide proof frames and full Unity capture.
- [ ] Validate only right hand appears; natural contact and no popping.

### Task 4: Audio & screen wake
**Files:** `LaptopCinematicSequence.cs`; resource `power_click.wav`; appropriate import meta; `LaptopWakeQuad`.
- [ ] Write click/wake PlayMode test that fails without one-shot event and power sound.
- [ ] Integrate one click through `RokasAudio.Play` respecting SFX settings.
- [ ] Render subtle backlight masked *inside the physical screen* from exact corrected quad; stage transition before boot.
- [ ] Verify one click at contact, wake stages and no stray playback on skip/cancel; screenshot verify.

### Task 5: Local installation, evidence and regression
**Files:** `Docs/LaptopCinematicRebuildReport.md`, exact safe installer if needed.
- [ ] Addressed backups and post-install file-hash verification in local `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`.
- [ ] Unity 6000.3.19f1 EditMode/PlayMode + existing Home/Hallway/Workbench and Laptop lifecycle tests.
- [ ] Unity screenshots/video: no hand, right hand entry, precontact, depression, glow, awake LCD, boot, ready UI.
- [ ] 10 cycles home→laptop→close and skip/cancel tests.
- [ ] Reviewer signs off spec and architecture; never merge draft PR #7 until all gates pass.

## Execution evidence ledger
- Before any changes: installed hand frames were PSX low-poly, with screenshots in user's Bandicam video. They are rejected as production art.
- GitHub branch is `codex/laptop-pov-cinematic-20261009`; base `r11-finished-ui`.
- Connected tools can push to this branch and trigger cloud GameCI; no direct local Windows D: filesystem or interactive editor.
- Superpowers plugin skills present in this session and read via installed skill tools.
- Blender unavailable in this container. Realistic replacement art and actual local PlayMode proof remain blocked pending an accessible art/editor environment, so they are not marked completed.
