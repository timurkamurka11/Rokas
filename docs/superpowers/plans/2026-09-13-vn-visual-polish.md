# ROKAS VN Visual Polish Implementation Plan

> Approved bounded design: presentation evolution only. Preserve Yarn flow, one-click-one-beat, Skip/Pause/Mute, persistence, debug replay/reset and safe Home handoff.

**Goal:** Bring the existing VN presentation materially closer to the supplied PNG/MP4 references, including responsive dialogue UI, authored face/blink/expression animation and active-speaker focus, then prove it in the real Windows player at 1280x720 and 1024x768.

**Architecture:** Keep the existing `VnIntroController -> VnIntroDialoguePresenter -> VnIntroView` pipeline. Add only presentation helpers/state required by the approved visual behaviour. `PortraitId` remains the visual state token. No new narrative/save architecture and no new Home beat.

**Tech:** Unity 6000.3.19f1, uGUI, Yarn Spinner, NUnit/Unity Test Framework, GitHub Actions, Windows Development Player runtime proof.

---

## Task 1: Inspect authored character-sheet states

**Files:**
- Create temporary `.github/workflows/vn-visual-state-inspection-temp.yml`
- Inspect existing `Assets/Rokas/Art/VN/Characters/Keiko_CharacterSheet.png`
- Inspect existing `Assets/Rokas/Art/VN/Characters/Mina_CharacterSheet.png`

1. Add a temporary workflow that copies exact source sheets into an artifact and generates coordinate/grid overlays without changing source PNGs.
2. Run it on `feature/vn-visual-polish-refs`.
3. Download/open the artifact and record only visually confirmed cells/states.
4. Do not define blink/expression UVs until this evidence exists.

## Task 2: Write RED presentation tests

**Files:**
- Modify `Assets/Rokas/Tests/PlayMode/VnIntroVisualLayoutPlayModeTests.cs`
- Add helper/test file only if deterministic presentation-motion tests need separation.

1. Add tests asserting `BackButton` exists and is not interactable.
2. Add test asserting `NextButton` invokes the existing continuation callback exactly once.
3. Add tests for responsive bottom-panel/portrait/control bounds and anchors at 16:9 and 4:3 reference canvases.
4. Add tests for authored portrait-state lookup/fallback after Task 1 inspection.
5. Add deterministic tests for pause-safe presentation motion and active/inactive focus targets.
6. Run focused PlayMode tests and verify failure is for the missing approved presentation behaviour, not infrastructure/compiler noise.

## Task 3: Implement responsive target-style dialogue UI

**Files:**
- Modify `Assets/Rokas/Scripts/Presentation/VnIntroView.cs`

1. Recompose dialogue panel, portrait, speaker/name and dialogue text to target proportions.
2. Move controls into the dialogue-panel composition.
3. Keep Mute/Pause/Skip callbacks unchanged.
4. Add visible disabled `BackButton`.
5. Add `NextButton` wired to the same `continueStory` callback as the existing click surface.
6. Preserve dark/light text contrast.
7. Run focused tests to GREEN.

## Task 4: Implement authored face/expression state mapping

**Files:**
- Modify `Assets/Rokas/Scripts/Presentation/VnIntroView.cs`
- Optionally create `Assets/Rokas/Scripts/Presentation/VnCharacterVisualStates.cs` + `.meta` if a separate tested table is clearer.

1. Encode only UVs/states confirmed from Task 1.
2. Keep `keiko_neutral` and `mina_neutral` compatible.
3. Add confirmed blink/expression tokens only where real authored art exists.
4. Unknown expression tokens fall back to that character’s neutral authored state.
5. Do not edit Yarn tokens unless an authored state is intentionally used for an existing beat.
6. Run focused tests to GREEN.

## Task 5: Add presentation-only animation and speaker focus

**Files:**
- Modify `Assets/Rokas/Scripts/Presentation/VnIntroView.cs`
- Optionally add `VnIntroPresentationAnimator.cs` / deterministic motion helper + `.meta`.

1. Drive blink/breathing/focus with unscaled presentation time.
2. Pause freezes local VN animation; never mutate `Time.timeScale`.
3. Implement smooth ~0.18 s focus transitions toward active ~1.05/full-bright/front and inactive ~0.94/slightly dimmed.
4. Support 0/1/2 character slots; do not manufacture a two-character story beat.
5. Ensure animation has no code path into Continue/Skip/dialogue completion/persistence.
6. Run focused presentation and flow tests to GREEN.

## Task 6: Preserve story and persistence semantics

**Files:**
- Inspect `Assets/Rokas/Resources/VN/Intro/VnIntro.yarn`
- Inspect `VnIntroController.cs`, `VnIntroDialoguePresenter.cs`, `VnIntroProgress.cs`, debug menu.

1. Confirm canonical beat order remains bus -> night -> phone.
2. Confirm one click completes at most the current line.
3. Confirm Pause blocks continuation without changing global time scale.
4. Confirm Skip reaches safe Home and persists completion.
5. Confirm completed-intro bypass and debug replay/reset still work.
6. Avoid production changes to controller/presenter/save code unless a focused failure proves necessity.

## Task 7: Extend real-player proof to both resolutions

**Files:**
- Modify `Assets/Rokas/Scripts/Presentation/RokasVnIntroRuntimeProofRunner.cs` only if needed to respect requested player resolution.
- Modify `.github/workflows/vn-intro-tdd.yml` on the feature branch.

1. Remove any hard-coded resolution that prevents external `-screen-width/-screen-height` proof, or make proof resolution explicit via arguments.
2. Run natural runtime proof at 1280x720 and 1024x768 with distinct output folders; retain skip/bypass proof and persistence chain.
3. Extend runtime UI assertions for Back/Next and presentation surface presence without asserting transient animation frames unreliably.
4. Upload both resolution screenshot sets.

## Task 8: Full verification and visual comparison

1. Run focused EditMode/PlayMode and existing legacy regressions.
2. Run full EditMode.
3. Run full PlayMode using the existing exact five Linux VideoPlayer classifier only; never widen it.
4. Build Windows Development Player.
5. Run real `Rokas.exe` proof.
6. Download JSON/log/screenshots, verify exact pixel dimensions, and manually inspect every VN screenshot against supplied target PNG/MP4 composition and behaviour.
7. Verify no target regression in bus/night/phone/Home handoff.

## Task 9: Clean final checkpoint

1. Delete temporary character-sheet inspection workflow.
2. Ensure no generated Unity noise or temporary artifacts are committed.
3. Run `git diff --check` and clean-status preflight in CI.
4. Run the permanent workflow again at the exact final-clean HEAD.
5. Re-download/open final-clean Windows runtime screenshots.
6. Verify feature branch is ahead of, not diverged behind, the approved integration baseline.
7. Do not merge or integrate; report the verified branch/HEAD for user review.
