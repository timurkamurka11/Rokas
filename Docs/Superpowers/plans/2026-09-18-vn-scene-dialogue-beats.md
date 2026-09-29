# VN Scene Dialogue Beats Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Every behavior-changing slice follows RED → validate RED → minimal GREEN → focused GREEN → commit.

**Goal:** Let one Scene Composer visual scene own multiple ordered dialogue beats (`Реплики`) so dialogue can progress without resetting scene media, characters, scene-entry animation, or video timeline.

**Architecture:** Replace scene-level single-dialogue fields with one canonical schema-v2 `dialogueBeats` list plus explicit schema-v1 migration. Keep `VnSceneComposerPlaybackController` as the sole playback progression owner, adding `CurrentBeatIndex` and `BeatElapsedSeconds` so scene presentation time and dialogue reveal time are independent. Resolve effective dialogue into `VnWorkshopPreviewFrame`; the renderer stays beat-model agnostic. Actual Scene boundaries continue to use existing M-B/M-C/M-C2 lifecycle and toolbar Previous/Next remain Scene-level.

**Tech Stack:** Unity 6000.3.19f1, C#, UnityEditor IMGUI, `JsonUtility`, NUnit EditMode, GameCI, existing Scene Composer renderer/transition/media/storage stack.

**Spec:** `docs/superpowers/specs/2026-09-18-vn-scene-dialogue-beats-design.md`

## Global Constraints

- Approved branch only: `feature/vn-scene-composer`.
- Accepted product base: HEAD `ad19cadd06598e9d188e459260a5d12343e0e3fe`, TREE `533b34b9175e248f11dd80950d0b56737a88e893`.
- M-A, M-B, M-C, M-D, and M-C2 stay closed unless new exact evidence proves a regression.
- No Production or Yarn changes; no integration/development/protected-ref mutations; no Apply-to-Production or narrative write path.
- No new feature branch, scratch/probe ref, tag, reset, force-push, history rewrite, or casual clean.
- M-DIALOGUE v1 is linear speaker/name + text/narration beats only. No branching, choices, per-beat media, per-beat full scene overrides, audio/BGM, panel redesign, localization database, timeline, M-E/F/G/TRANSITION work.
- `dialogueBeats` is the only canonical v2 writable dialogue authority. Legacy `speaker` / `previewText` / `narration` are migration input only.
- Beat selection in authoring is not project story data. Playback beat state is transient and creates no Undo records.
- Beat-to-beat progression inside one Scene must not call `ResetScene`, `OpenMedia`, `Prepare`, `ReleaseMedia`, `Dispose`, restart video, or re-run scene-entry animation.
- Existing toolbar `Previous` and `Next` remain Scene navigation.
- After automated GREEN, stop for manual Unity M-DIALOGUE review. Do not start M-E.

## Source map / file responsibilities

- `.github/workflows/vn-scene-composer.yml`
  - focused source guard and SC-A→SC-I/UX test routing. Add only the two exact required M-DIALOGUE doc paths to `allowed_exact`.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerTypes.cs`
  - schema version, `VnSceneComposerDialogueBeat`, canonical scene beat list.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerDialogue.cs` (new)
  - beat normalization/resolution/editing primitives that do not know about IMGUI or playback media.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerSerialization.cs`
  - schema-v2 serialization and schema-v1 dialogue migration/validation.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerEditing.cs`
  - Scene CRUD/duplication; regenerate cloned beat identities.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerComposition.cs`
  - build frame from Scene + effective beat; character focus uses effective speaker.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerTransitionSampler.cs`
  - timing/typewriter/focus resolution from effective beats rather than removed Scene text fields.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerElapsedTransitionSampler.cs`
  - combine scene-lifetime sampling with beat-lifetime text/focus sampling.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerPlaybackController.cs`
  - authoritative transient `CurrentBeatIndex`, `BeatElapsedSeconds`, `AdvanceDialogue`, automatic beat advance, final-beat scene boundary.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposer.cs`
  - beat selection state, creator-facing `Реплики` inspector/actions, authoring frame resolution, storyboard summary, playback on-panel Next routing.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerAuthoring.cs`
  - only if required to keep baseline/comparison preview beat-aware; do not broaden direct-manipulation semantics.
- Existing SC-A/B/E/F/G/H/I and UX tests
  - extend established fixtures rather than introducing a parallel test harness.
- `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerPlaybackTests.DialogueBeats.cs` (new)
  - focused SC-G beat progression/media-continuity tests.

---

### Task 0: Admit the required M-DIALOGUE docs without weakening source guard

**Files:**
- Modify: `.github/workflows/vn-scene-composer.yml`
- Existing docs: `docs/superpowers/specs/2026-09-18-vn-scene-dialogue-beats-design.md`
- Existing docs: `docs/superpowers/plans/2026-09-18-vn-scene-dialogue-beats.md`

**Interfaces:**
- Consumes: existing `allowed_exact` source-guard set.
- Produces: source guard accepts exactly the two required M-DIALOGUE docs while all arbitrary docs/runtime/Yarn paths remain forbidden.

- [ ] **Step 1: Confirm the docs-only guard failure is classification-only**

Inspect the workflow run for the spec/plan commit. Expected first divergence: `Forbidden Scene Composer diff paths` listing the new spec/plan paths; no product compile/test failure is relevant because source guard blocks downstream jobs.

- [ ] **Step 2: Extend `allowed_exact` minimally**

Add exactly:

```python
'docs/superpowers/specs/2026-09-18-vn-scene-dialogue-beats-design.md',
'docs/superpowers/plans/2026-09-18-vn-scene-dialogue-beats.md',
```

Do not add a broad `docs/superpowers/` prefix.

- [ ] **Step 3: Verify guard**

Push the workflow-only change. Expected: `Scene Composer source / production guard` PASS. Focused existing suites should still run unchanged.

- [ ] **Step 4: Commit**

Commit message:

```text
ci: admit scene dialogue beat docs
```

---

### Task 1: Canonical beat model and schema-v1 compatibility

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerTypes.cs`
- Create: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerDialogue.cs`
- Create: matching `.meta`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerSerialization.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerTests.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerPersistenceTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class VnSceneComposerDialogueBeat`
  - `public List<VnSceneComposerDialogueBeat> dialogueBeats` on Scene
  - `VnSceneComposerContract.SchemaVersion == 2`
  - focused resolver/normalizer methods in `VnSceneComposerDialogue`, including resolving beat by index/ID without playback mutation.
  - `DeserializePortable` accepts schema 1 through explicit dialogue migration and schema 2 natively.

- [ ] **Step 1: Write model RED**

Extend SC-A using reflection so the test compiles against the pre-feature assembly:

```csharp
[Test]
public void MD_NewSceneOwnsOneCanonicalDialogueBeat()
{
    object scene = Activator.CreateInstance(RequireType("VnSceneComposerScene"));
    FieldInfo beatsField = scene.GetType().GetField("dialogueBeats");
    Assert.That(beatsField, Is.Not.Null, "M-DIALOGUE requires one canonical beat list.");
    IList beats = (IList)beatsField.GetValue(scene);
    Assert.That(beats, Has.Count.EqualTo(1));
    Assert.That((string)Get(beats[0], "beatId"), Has.Length.EqualTo(32));
    Assert.That((string)Get(beats[0], "speaker"), Is.Empty);
    Assert.That((string)Get(beats[0], "text"), Is.Empty);
}
```

Also assert the canonical v2 scene type no longer exposes public instance fields named `speaker`, `previewText`, or `narration`.

- [ ] **Step 2: Write legacy migration RED**

In SC-H, construct schema-v1 JSON containing one Scene with `speaker`, `previewText`, `narration`; call `DeserializePortable`; assert success and exactly one canonical beat with those values. Also assert serializing the loaded project emits schema 2 with `dialogueBeats` and does not emit the old dialogue field names as Scene dialogue authority.

Representative legacy JSON body:

```json
{
  "schemaVersion": 1,
  "sourceHead": "ad19cadd06598e9d188e459260a5d12343e0e3fe",
  "projectId": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "title": "Legacy",
  "scenes": [
    {
      "sceneId": "11111111111111111111111111111111",
      "label": "Legacy Scene",
      "speaker": "Aiko",
      "previewText": "Привет 你好 Hello",
      "narration": false
    }
  ]
}
```

- [ ] **Step 3: Run RED through existing focused workflow**

Expected:
- SC-A fails because `dialogueBeats` / beat type are absent.
- SC-H fails because v1 has no canonical beat migration and current schema remains 1.
- Source guard PASS after Task 0.

Do not accept compile/harness failure as the behavioral RED if reflection tests can reach assertions.

- [ ] **Step 4: Implement minimal model**

In `VnSceneComposerTypes.cs`:

```csharp
public const int SchemaVersion = 2;

[Serializable]
public sealed class VnSceneComposerDialogueBeat
{
    public string beatId = VnSceneComposerScene.NewStableId();
    public string speaker = string.Empty;
    [TextArea(3, 10)] public string text = string.Empty;
    public bool narration;
}
```

Replace the three Scene dialogue fields with:

```csharp
public List<VnSceneComposerDialogueBeat> dialogueBeats =
    new List<VnSceneComposerDialogueBeat> { new VnSceneComposerDialogueBeat() };
```

- [ ] **Step 5: Implement focused dialogue helper**

`VnSceneComposerDialogue` provides deterministic list/ID resolution without inspecting media. Required semantics:

```csharp
internal static VnSceneComposerDialogueBeat Resolve(VnSceneComposerScene scene, int index)
internal static int FindIndex(VnSceneComposerScene scene, string beatId)
internal static VnSceneComposerDialogueBeat ResolveById(VnSceneComposerScene scene, string beatId)
internal static void NormalizeForAuthoring(VnSceneComposerScene scene)
```

`Resolve` is playback-safe: if authored data is malformed, return a non-persisted empty effective beat/fallback rather than mutating the Scene. `NormalizeForAuthoring` is allowed to create one empty beat and normalize null strings because it is called from authoring/migration normalization paths.

- [ ] **Step 6: Implement explicit v1 migration**

Add minimal legacy transport types local to serialization:

```csharp
[Serializable]
private sealed class LegacyDialogueProjectV1
{
    public List<LegacyDialogueSceneV1> scenes = new List<LegacyDialogueSceneV1>();
}

[Serializable]
private sealed class LegacyDialogueSceneV1
{
    public string speaker = string.Empty;
    public string previewText = string.Empty;
    public bool narration;
}
```

When parsed `schemaVersion == 1`, deserialize both the current common model and this legacy dialogue transport, then replace each scene's canonical list with exactly one migrated beat before current normalization/validation. Schema 2 follows normal path; every other schema remains unsupported.

- [ ] **Step 7: Focused GREEN**

Expected SC-A model RED and SC-H migration RED both PASS. Existing schema error test is updated to mutate current `"schemaVersion": 2` to 999 while retaining explicit schema-1 compatibility coverage.

- [ ] **Step 8: Commit**

```text
feat: add canonical scene dialogue beats
```

---

### Task 2: Beat CRUD, validation, scene duplication, and Unicode persistence

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerDialogue.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerEditing.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerSerialization.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerCrudTests.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerPersistenceTests.cs`

**Interfaces:**
- Produces stable-ID canonical operations:

```csharp
internal static VnSceneComposerDialogueBeat AddBeat(VnSceneComposerScene scene, string afterBeatId)
internal static VnSceneComposerDialogueBeat DuplicateBeat(VnSceneComposerScene scene, string beatId)
internal static string DeleteBeat(VnSceneComposerScene scene, string beatId)
internal static bool MoveBeat(VnSceneComposerScene scene, string beatId, int targetIndex)
```

`DeleteBeat` returns the deterministic beat ID to select after deletion.

- [ ] **Step 1: Write CRUD RED**

SC-B proves:

```text
initial: [A]
Add after A -> [A, B(empty)] and B has unique ID
Duplicate A -> [A, A-copy, B] with copied dialogue but unique ID
Move B to index 0 -> [B, A, A-copy]
Delete selected B -> deterministic neighboring ID
Delete until one remains; deleting last replaces it with one new empty beat
```

Assert list/object independence rather than only counts.

- [ ] **Step 2: Write persistence/duplication RED**

SC-H proves a four-beat Scene containing:

```text
Aiko — Привет
Tim — 你好
Aiko — Hello
Tim — line1\nline2
```

round-trips in exact order with exact Unicode/line breaks and IDs. Duplicate the Scene via `VnSceneComposerEditing.DuplicateScene`; assert all speaker/text/narration values are preserved, cloned `sceneId` differs, every cloned `beatId` differs from the corresponding source ID, and editing clone beat 2 does not change original beat 2.

Add v2 validation assertions for null beat, missing beat ID, duplicate beat ID.

- [ ] **Step 3: Validate RED**

Expected SC-B/SC-H behavioral failures: operations absent; duplicate Scene currently reuses cloned beat IDs; validation does not yet inspect beats.

- [ ] **Step 4: Implement minimal CRUD**

Use list operations only. Do not clone Scene visual data per beat. Duplicate a beat as a new object containing copied `speaker`, `text`, `narration` and a new stable ID.

For deleting the last beat, replace with:

```csharp
new VnSceneComposerDialogueBeat()
```

and return its ID.

- [ ] **Step 5: Regenerate beat IDs on Scene duplicate**

After existing JSON deep clone and new `sceneId`, loop cloned `dialogueBeats`, ensure non-null, and assign each `beatId = VnSceneComposerScene.NewStableId()`.

- [ ] **Step 6: Validate canonical beats during serialization**

Reject v2 persisted scenes with zero/null beats, empty beat IDs, or duplicate beat IDs. Normalize nullable strings to `string.Empty` only in the normal authoring/serialization normalization path.

- [ ] **Step 7: Focused GREEN and commit**

SC-B + SC-H PASS, then commit:

```text
feat: add dialogue beat editing and persistence
```

---

### Task 3: Resolve effective dialogue into composition and transition sampling

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerComposition.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerTransitionSampler.cs`
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerElapsedTransitionSampler.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerCompositionTests.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerTransitionTimingTests.cs`

**Interfaces:**
- Produces beat-aware frame/sampler overloads. Concrete direction:

```csharp
internal static VnWorkshopPreviewFrame BuildFrame(
    VnSceneComposerProject project,
    VnSceneComposerScene scene,
    VnSceneComposerDialogueBeat beat,
    VnWorkshopResolution resolution,
    Texture2D backgroundOverride = null)
```

Keep a compatibility overload without beat only where existing call sites/tests require it; it resolves beat 0 and is not a second dialogue authority.

Elapsed sampling consumes both scene and beat elapsed time plus effective previous/current beats; it must never manufacture cloned mutable Scene objects merely to change dialogue.

- [ ] **Step 1: Write composition RED**

Create one Scene with two beats, same characters/media, different speakers/text. Call beat-aware composition for beat 0 and beat 1. Assert:

```text
frame0.Dialogue == beat0.text
frame0.Speaker == beat0.speaker
frame1.Dialogue == beat1.text
frame1.Speaker == beat1.speaker
character visual geometry remains identical
active speaker focus follows effective beat speaker
```

- [ ] **Step 2: Write sampler RED for separate clocks**

SC-F creates a scene with visible entry motion and two dialogue beats. Sample beat 2 with a large `sceneElapsedSeconds` (entry already complete) but `beatElapsedSeconds == 0`.

Expected:
- character/background/stage sample remains at the completed scene presentation state;
- beat 2 `visibleText` starts from current typewriter beginning;
- focus transition may begin from prior beat speaker according to existing focus resolver;
- no scene-entry component returns to its start state.

- [ ] **Step 3: Validate RED**

Expected failures: current composition/samplers still read removed/old Scene dialogue and have no separate beat clock.

- [ ] **Step 4: Implement effective-dialogue resolution**

Refactor frame construction so renderer-facing `Speaker` and `Dialogue` come only from the passed beat. `FindActiveIndex` takes effective `narration/speaker` rather than reading Scene dialogue fields.

- [ ] **Step 5: Split elapsed sampling**

Preserve existing visual sampling with `sceneElapsedSeconds`. Resolve typewriter visible text with active beat text and `beatElapsedSeconds`. Resolve speaker focus from previous effective beat to current effective beat using the existing focus sampler and beat clock. Keep existing scene-boundary source/target focus behavior for real Scene entry.

- [ ] **Step 6: Focused GREEN and commit**

SC-E + SC-F PASS, then commit:

```text
feat: resolve active dialogue beats in preview frames
```

---

### Task 4: Controller-owned beat progression with zero media reset

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerPlaybackController.cs`
- Create: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerPlaybackTests.DialogueBeats.cs`
- Create: matching `.meta`

**Interfaces:**
- Produces transient public-read state:

```csharp
public int CurrentBeatIndex { get; private set; }
public float BeatElapsedSeconds { get; private set; }
public void AdvanceDialogue()
```

`ResetScene` sets beat index/time to zero. Same-scene `AdvanceDialogue` never calls `ResetScene` for a non-final beat.

- [ ] **Step 1: Write primary playback RED**

Test `MD_BeatAdvanceKeepsSceneAndVideoResourceAlive` using the established fake/decode-free video preview factory pattern:

```text
Scene 0: video X, Beat A, Beat B, ManualBeat
PlayScene(0)
record CurrentSceneIndex, video resource object/texture, Prepare count, MediaTimeSeconds
Advance scene time/media once
AdvanceDialogue()
```

Assert:

```text
CurrentSceneIndex unchanged
CurrentBeatIndex becomes 1
BeatElapsedSeconds == 0
SceneElapsedSeconds is NOT reset
same video resource/texture identity
Prepare count unchanged
Dispose count unchanged
MediaTimeSeconds not reset
CurrentFrame dialogue == Beat B
```

Include a character enter transition and assert its renderer-facing body/alpha does not jump back to entry-start after beat advance.

- [ ] **Step 2: Write final-beat boundary RED**

Two scenes, ordered PlayAll:

```text
Scene 0: Beat A, Beat B
Scene 1: Beat C
```

Manual advance once keeps Scene 0 and goes A→B. Manual advance again performs one real Scene 0→1 boundary and starts `CurrentBeatIndex == 0`. Existing M-C2 scene-boundary behavior remains responsible for video continuity.

- [ ] **Step 3: Validate RED**

Expected SC-G failures: no beat state/action exists and current progression is Scene-only.

- [ ] **Step 4: Implement transient beat state**

Initialize/reset beat state on constructor/real scene reset/empty-stop. Increment both `SceneElapsedSeconds` and `BeatElapsedSeconds` in `Advance`; increment `MediaTimeSeconds` exactly as before.

- [ ] **Step 5: Implement `AdvanceDialogue()` as single authority**

Pseudo-flow:

```csharp
if (CurrentSceneIndex < 0) return;
if (CurrentBeatIndex + 1 < BeatCount(scene)) {
    previousBeat = current;
    CurrentBeatIndex++;
    BeatElapsedSeconds = 0f;
    RebuildFrameForCurrentBeat(previousBeat);
    return;
}
AdvanceAfterFinalBeatAccordingToScope();
```

The non-final path must not touch media ownership/lifecycle.

- [ ] **Step 6: Make `RebuildFrame` beat-aware**

Pass current beat, previous beat context, `SceneElapsedSeconds`, and `BeatElapsedSeconds` to the sampler/composition path. Real Scene reset still constructs source Scene context and starts beat 0.

- [ ] **Step 7: Focused GREEN and commit**

SC-G dialogue-beat tests PASS while existing M-C and M-C2 tests remain PASS. Commit:

```text
feat: advance dialogue beats without scene reset
```

---

### Task 5: Automatic timing, Play Scene/All/From Here, pause/restart, and manual final semantics

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerPlaybackController.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerPlaybackTests.DialogueBeats.cs`
- Regression test: existing `VnSceneComposerPlaybackTests.cs`

**Interfaces:**
- Consumes `AdvanceDialogue()` from Task 4.
- Produces exact transport semantics without redefining Scene-level `Next`/`Previous`.

- [ ] **Step 1: Write transport REDs**

Add tests:

```text
MD_PreviewAutoDurationAdvancesBeatsBeforeSceneBoundary
MD_ManualBeatNeverAutoAdvancesDialogue
MD_PlaySceneStopsAfterFinalBeat
MD_PlayAllAdvancesAllBeatsThenNextScene
MD_PlayFromHereStartsSelectedSceneAtBeatZero
MD_PausePreservesCurrentBeatAndClocks
MD_RestartReturnsCurrentPlaybackSceneToBeatZero
MD_ToolbarNextPreviousRemainSceneNavigation
MD_OneBeatScenePreservesLegacyTransportBehavior
```

For auto mode, `sequenceGap` must apply only after final beat before a real Scene boundary, not between same-scene beats.

- [ ] **Step 2: Validate RED**

Expected first failures should identify incorrect auto/final/restart semantics, not compile/harness errors.

- [ ] **Step 3: Implement automatic beat progression**

Use `BeatElapsedSeconds` against the existing scene-authored `PreviewAutoDuration` for each active beat. When a non-final beat expires, call the same internal beat-advance transition without applying scene `sequenceGap`. On the final beat, preserve existing sequence-gap handling before the real Scene boundary.

`ManualBeat` never advances on elapsed time.

- [ ] **Step 4: Preserve scope semantics**

- `PlayScene*`: start beat 0 and stop after final beat.
- `PlayAll`: Scene 0 beat 0 → all beats → real scene boundary → next scene beat 0.
- `PlayFromHere*`: selected Scene beat 0 clean start, then ordered continuation.
- `Pause`: do not reset beat state.
- `Restart`: same current Scene/scope, beat 0, existing media restart behavior.
- `Next`/`Previous`: real Scene navigation and destination beat 0.

- [ ] **Step 5: Focused GREEN and commit**

SC-G full fixture PASS, including historical `PreviewAutoDurationCanAdvanceStoryboardWhileManualBeatNeverAutoAdvances` with its expectations adapted only where the canonical model requires fixture construction changes. Commit:

```text
feat: integrate dialogue beats with playback transport
```

---

### Task 6: Creator-facing `Реплики` editor, authoring selection, and Undo

**Files:**
- Modify: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposer.cs`
- Modify if required: `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerAuthoring.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerWindowTests.cs`
- Test: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerUxTests.cs`

**Interfaces:**
- Produces window APIs suitable for functional tests:

```csharp
public string ComposerGetSelectedDialogueBeatId()
public void ComposerSelectDialogueBeat(string beatId)
public void ComposerAddDialogueBeat()
public void ComposerDuplicateSelectedDialogueBeat()
public void ComposerDeleteSelectedDialogueBeat()
public void ComposerMoveSelectedDialogueBeat(int direction)
public void ComposerAdvanceDialogue()
```

Public editor methods delegate to canonical dialogue/controller operations; project persistence does not store selected beat ID.

- [ ] **Step 1: Write editor RED**

SC-I/UX prove the normal `Текст` inspector exposes creator-facing labels/actions:

```text
Реплики
+ Реплика
Дублировать
Удалить
↑
↓
Говорящий
```

Use existing functional window testing where possible; source-string assertions may supplement but must not be the sole proof of beat mutations.

- [ ] **Step 2: Write functional selection/Undo RED**

Instantiate the window/project through existing SC-I patterns. Prove:

```text
select beat B -> selection ID == B
move B -> selection remains B
add -> newly inserted beat selected
duplicate -> copy selected and independent
remove -> deterministic neighbor selected
Undo after add/delete/reorder restores canonical list/order and a valid selection
```

Also serialize the project after changing selection and assert selected beat ID is absent from portable JSON.

- [ ] **Step 3: Write authoring-media no-op selection RED**

Prime/open a selected video authoring preview using existing video factory patterns, change selected beat only, then assert no additional Open/Prepare/Dispose/restart and the scene thumbnail/media resource identity stays the same.

- [ ] **Step 4: Implement selection and list UX**

Add serialized EditorWindow-only `_sceneComposerSelectedDialogueBeatId`; ensure it is valid when scene selection/load/add/duplicate/delete changes. Draw compact rows with selected style and abbreviated speaker/text. Use existing `RecordSceneComposerUndo` only around actual data mutations, never simple selection.

Edit narration/speaker/text on the selected canonical beat. Keep typography/typewriter controls scene-level.

- [ ] **Step 5: Make authoring preview beat-aware**

`ComposerBuildSelectedPreviewFrame` and display/baseline path resolve the selected beat and call beat-aware composition. Beat selection calls `Repaint()` only; no playback/media reset.

Storyboard summary resolves a sensible authored beat (first beat is sufficient) rather than removed Scene dialogue fields.

- [ ] **Step 6: Wire explicit playback dialogue advance**

`ComposerAdvanceDialogue()` delegates to controller `AdvanceDialogue()`, syncs scene selection only if a real scene boundary occurred, and repaints/ticks.

During active playback only, clicking the existing renderer-facing `VnWorkshopElement.Next` hit rect may invoke this action. Do not enable authoring drag/select handling during playback and do not change toolbar `ComposerNext()` semantics.

- [ ] **Step 7: Focused GREEN and commit**

SC-I + UX + affected SC-E/G video-authoring tests PASS. Commit:

```text
feat: author multiple dialogue beats per scene
```

---

### Task 7: Full M-DIALOGUE focused regression and automated closure gate

**Files:**
- Test-only fixes only if a demonstrated fixture assumption still references removed v1 Scene dialogue fields.
- No feature expansion.

**Interfaces:**
- Produces fresh CI evidence and exact manual-review checkpoint.

- [ ] **Step 1: Run the established workflow on the final M-DIALOGUE HEAD**

The existing GitHub workflow must execute unchanged fixture filters:

```text
SC-A  Rokas.EditorTools.Tests.VnSceneComposerTests
SC-B  Rokas.EditorTools.Tests.VnSceneComposerCrudTests
SC-C  Rokas.EditorTools.Tests.VnSceneComposerImageMediaTests
SC-D  Rokas.EditorTools.Tests.VnSceneComposerMotionMediaTests
SC-E  Rokas.EditorTools.Tests.VnSceneComposerCompositionTests
SC-F  Rokas.EditorTools.Tests.VnSceneComposerTransitionTimingTests
SC-G  Rokas.EditorTools.Tests.VnSceneComposerPlaybackTests
SC-H  Rokas.EditorTools.Tests.VnSceneComposerPersistenceTests
SC-I  Rokas.EditorTools.Tests.VnSceneComposerWindowTests
UX    Rokas.EditorTools.Tests.VnSceneComposerUxTests
```

Source / production guard must PASS.

- [ ] **Step 2: Inspect fresh XML artifacts**

Report exact total/passed/failed/skipped for every focused fixture, especially the new SC-G count. Do not reuse the historical 81 count and do not call skipped SC-J/full jobs PASS.

- [ ] **Step 3: Verify mandatory feature contracts from fresh tests**

Required GREEN evidence:

```text
legacy v1 -> one beat
canonical v2 multi-beat model
serialization/order/Unicode
scene duplication independence
Undo add/delete/duplicate/reorder/edit
beat selection stability
Scene starts beat 0
beat->beat Scene index unchanged
beat->beat media resource unchanged
Prepare count unchanged
SceneElapsed not reset; BeatElapsed reset
scene-entry animation not replayed
last beat -> next Scene
Play Scene / Play All / Play From Here
ManualBeat and PreviewAutoDuration
single-beat regression
M-C first-Prepare regression
M-C2 same/different-video scene boundary regression
M-D focused preview/duration regression
```

- [ ] **Step 4: Fresh repository safety check**

Verify final branch HEAD/TREE, compare against accepted base, changed paths remain editor/tests/docs/workflow only, Production/Yarn unchanged, integration not performed, and `__invalid_do_not_create` absent.

- [ ] **Step 5: Automated gate**

Only with fresh successful evidence report:

```text
M-DIALOGUE AUTOMATED CLOSED: YES
M-DIALOGUE MANUAL CLOSED: NO
MANUAL UNITY REVIEW REQUIRED: YES
```

Then STOP. Do not start M-E.

## TDD order summary

1. CI/doc guard narrowly admits required design/plan files.
2. RED model + legacy migration → canonical schema-v2 model GREEN.
3. RED CRUD/persistence/duplication/Unicode → editing/persistence GREEN.
4. RED effective frame + separate clocks → composition/sampling GREEN.
5. RED beat progression/media identity → controller GREEN.
6. RED transport scope/auto/manual semantics → transport GREEN.
7. RED creator UX/selection/Undo/no-media-work → editor GREEN.
8. Full focused regression → automated gate → STOP for manual Unity acceptance.

Each product slice must show a real RED before its corresponding product fix. If the first failure is compile/harness/infrastructure rather than the intended contract, classify and repair the harness before counting it as behavioral RED. If three distinct fixes fail against the same underlying behavior, stop and reassess architecture before attempting a fourth workaround.
