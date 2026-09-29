# VN Scene Dialogue Beats Design

**Status:** Approved for implementation

**SourceHead:** `ad19cadd06598e9d188e459260a5d12343e0e3fe`

## Goal

Allow one Scene Composer visual scene to contain an ordered sequence of dialogue turns (`Реплики`) while keeping that scene's background/video, characters, transforms, scene animations, and media lifetime active. Advancing from one beat to the next changes dialogue state only. Only the final beat may advance to the next real scene.

## Hard boundaries

- Work only on `feature/vn-scene-composer` and continue from the accepted M-C2 checkpoint `ad19cadd06598e9d188e459260a5d12343e0e3fe` / tree `533b34b9175e248f11dd80950d0b56737a88e893`.
- M-A, M-B, M-C, M-D, and M-C2 are closed and must not be reopened without new exact evidence.
- Do not modify Production, Yarn, `integration/rokas-unified`, `development`, or protected refs.
- Do not integrate as part of M-DIALOGUE.
- Do not start M-E, M-F, M-AUDIO, M-G, or M-TRANSITION.
- M-DIALOGUE v1 is a linear ordered beat list. No branching, choices, conditions, scripting graph, per-beat media/background, full per-beat scene overrides, audio system, dialogue-panel redesign, timeline, or localization database.

## Existing architecture and problem

At the accepted base, `VnSceneComposerScene` is both the visual scene model and the single dialogue turn. It stores `speaker`, `previewText`, and `narration` directly. `VnSceneComposerComposition.BuildFrame` reads those fields into `VnWorkshopPreviewFrame.Speaker` and `.Dialogue`. `VnSceneComposerTransitionSampler` and `VnSceneComposerElapsedTransitionSampler` also read scene dialogue directly for speaker focus, typewriter duration, and visible text.

`VnSceneComposerPlaybackController` owns only `CurrentSceneIndex` plus one `SceneElapsedSeconds` clock. `Advance` progresses the whole scene and a real ordered boundary calls `ResetScene`, which owns media/open/prepare/release behavior. This is correct for scene boundaries and must remain the only place where M-C2 continuity is involved.

The current elapsed sampler uses the same scene elapsed time for background transition, character entry/exit, stage, expression, bounce, speaker focus, and typewriter. Resetting `SceneElapsedSeconds` merely to reveal a new dialogue line would therefore replay scene-entry presentation. M-DIALOGUE must separate scene lifetime from beat lifetime.

## Canonical data model

### `VnSceneComposerDialogueBeat`

Add one lightweight serializable beat type:

```csharp
[Serializable]
public sealed class VnSceneComposerDialogueBeat
{
    public string beatId = VnSceneComposerScene.NewStableId();
    public string speaker = string.Empty;
    [TextArea(3, 10)] public string text = string.Empty;
    public bool narration;
}
```

The beat intentionally contains only dialogue-turn data required by v1. It does not own media, characters, scene animation, timing overrides, or layout.

### `VnSceneComposerScene`

The canonical v2 scene owns:

```csharp
public List<VnSceneComposerDialogueBeat> dialogueBeats =
    new List<VnSceneComposerDialogueBeat> { new VnSceneComposerDialogueBeat() };
```

`dialogueBeats` is the single writable dialogue authority. The v2 Scene model no longer exposes permanent writable `speaker`, `previewText`, or `narration` fields.

A normal newly-created scene starts with one empty beat so single-line authoring remains immediate.

### Stable beat identity

Every beat has a stable 32-character hexadecimal `beatId`, using the existing Scene Composer stable-ID convention. IDs allow editor selection to follow a beat through reordering and allow future references without making list indices persistent identity.

Beat IDs must be unique inside a scene. Serialization validation rejects missing or duplicate IDs rather than silently rewriting authored v2 data.

## Schema v2 and legacy compatibility

Bump `VnSceneComposerContract.SchemaVersion` from 1 to 2.

There must not be two permanent writable dialogue authorities. Version 2 portable JSON contains only `dialogueBeats` for dialogue content.

For schema-v1 input, `VnSceneComposerSerialization.DeserializePortable` performs an explicit compatibility migration:

1. Deserialize the common project/scene fields into the current project model; unknown removed v1 dialogue fields are ignored by the v2 model.
2. Deserialize the same JSON into a small legacy dialogue transport containing only `scenes[].speaker`, `scenes[].previewText`, and `scenes[].narration`.
3. For every v1 scene, replace/initialize its canonical beat list with exactly one new beat containing the legacy values.
4. Normalize and validate the resulting v2 project in memory.
5. Saving/exporting writes schema v2 only.

This preserves old projects without manual JSON edits, does not lose or duplicate old dialogue, and avoids an ad-hoc second migration system. Unsupported schemas other than 1 and the current schema remain rejected.

A migrated v1 beat receives a new stable ID because v1 had no beat identity. Once exported as v2, the ID round-trips normally.

## Dialogue normalization and resolution

Introduce one focused dialogue model helper (for example `VnSceneComposerDialogue`) responsible for pure/low-level beat invariants and resolution:

- ensure/normalize a scene has at least one beat when creating or migrating editor-owned data;
- resolve the first beat;
- resolve a beat by bounded index;
- resolve a beat by stable ID for authoring selection;
- return an empty-safe effective beat without mutating saved project during playback recovery;
- validate beat IDs and nullable strings during serialization.

Playback must never repair/mutate saved beat data as a side effect of rendering. Authoring/serialization normalization owns persistent normalization.

## Scene duplication

`VnSceneComposerEditing.DuplicateScene` continues to deep-clone the whole scene through JSON. After cloning it must generate:

- a new `sceneId`;
- a new independent `beatId` for every cloned beat.

Beat order, speaker, text, and narration are preserved. No beat/list object is shared between original and copy.

## Beat editing operations

Add focused dialogue editing operations using canonical list order:

- Add beat: insert a new empty beat after the selected beat (or append if no valid selection).
- Duplicate beat: deep-copy speaker/text/narration, assign a new `beatId`, insert immediately after source.
- Delete beat: remove the selected beat; if it was the last remaining beat, replace it with one new empty beat so the scene always remains immediately editable.
- Move beat up/down: reorder the existing beat object so selection follows stable identity.

The window wraps these mutations with existing `RecordSceneComposerUndo(...)` / `Undo.RegisterCompleteObjectUndo`, then `MarkSceneComposerChanged()`.

Actual authoring mutation follows the existing Scene Composer policy of resetting/stopping playback preview where necessary. Merely selecting a beat is editor state and must not reset, open, prepare, dispose, or restart scene media.

## Editor authoring selection

Add an editor-window selection field such as `_sceneComposerSelectedDialogueBeatId`. This is transient authoring state relative to portable project data; it is not stored inside `VnSceneComposerProject` JSON.

Selection rules:

- Selecting a scene selects that scene's first beat unless a valid remembered editor selection for that scene is available cheaply; v1 may simply select beat 0.
- Adding/duplicating selects the new beat.
- Deleting selects the next beat if available, otherwise previous, otherwise the replacement empty beat.
- Reordering keeps selection on the same beat ID.
- Loading a project selects the first scene's first beat.

Playback active beat state is separate from authoring selected beat state.

## Text inspector UX

Inside the existing `Текст` inspector, add a compact `Реплики` section. The storyboard remains one card per visual Scene.

The beat list shows ordered rows such as:

```text
1. Aiko — Привет...
2. Tim — Привет.
3. Aiko — Как дела?
```

Required creator actions:

- `+ Реплика`
- `Дублировать`
- `Удалить`
- `↑`
- `↓`

Selecting a row exposes/edits the selected beat using the current creator-facing controls:

- `Текст без персонажа`
- `Говорящий`
- dialogue text area

Typography/typewriter controls remain scene-level and unchanged. No internal beat IDs or serialization details are shown.

The list stays compact and does not replace the central preview or create a separate Dialogue Workshop.

## Storyboard summary

Existing storyboard cards remain Scene-based. Their speaker/text summary resolves the first/current authoring beat rather than removed scene-level fields. A cheap beat-count label may be added only if it naturally fits; it is not required for acceptance.

## Authoring preview

`VnSceneComposerComposition` gains a dialogue-aware frame build path. The renderer remains ignorant of editor selection and of the beat model: it continues to draw `VnWorkshopPreviewFrame.Speaker` and `.Dialogue`.

Conceptually:

```text
Scene + effective Dialogue Beat -> VnWorkshopPreviewFrame
```

The authoring preview passes the selected authoring beat. Selecting a different beat updates name/text/focus in the preview without changing scene media ownership or preparing/reopening video.

Comparison/baseline preview cloning must preserve the selected beat's effective dialogue while continuing to clear only presentation overrides as it does today.

## Playback state ownership

`VnSceneComposerPlaybackController` remains the single owner of transient playback progression. Add:

```text
CurrentBeatIndex
BeatElapsedSeconds
```

`CurrentSceneIndex` remains the identity of a real visual scene.

On every real scene entry/reset:

- `CurrentBeatIndex = 0`
- `BeatElapsedSeconds = 0`
- scene media/characters/entry transition are initialized exactly once through existing `ResetScene` behavior.

Beat progression never uses fake scene indices.

## Separate scene and beat clocks

`SceneElapsedSeconds` is the visual-scene lifetime clock. It continues across beat changes within one Scene and drives:

- background transition
- character enter/exit
- stage movement
- expression transition
- scene action/bounce timing
- other scene-entry presentation.

`BeatElapsedSeconds` resets to zero whenever the active beat changes and drives:

- active beat text reveal/typewriter
- speaker-focus transition caused by changing speaker, using existing focus semantics rather than a new focus subsystem
- per-beat preview completion/advance timing.

This split is mandatory: advancing a beat must not replay scene-entry animation.

## Effective transition sampling

Adapt the existing transition samplers to accept the effective dialogue context instead of reading removed scene dialogue fields directly.

The sampled snapshot still contains one `visibleText` and focus state. Scene visual components are sampled with `SceneElapsedSeconds`; text and beat speaker-focus components are sampled with `BeatElapsedSeconds`.

At scene entry, the dialogue source for focus can resolve from the outgoing scene's final beat to the incoming scene's first beat. Within the same scene, focus resolves from the previous active beat to the new active beat. This preserves established speaker-focus behavior without causing a scene reset.

Static authoring preview resolves the selected beat directly at its endpoint; it does not mutate playback state.

## Beat duration and automatic progression

Existing `VnSceneComposerTiming` remains scene-authored presentation timing. M-DIALOGUE v1 does not add a per-beat timeline or timestamp editor.

For `PreviewAutoDuration`, each active beat uses the existing resolved preview-auto duration contract as its preview hold/advance duration. `BeatElapsedSeconds` determines when to move to the next beat. Scene-level `sequenceGap` applies only when leaving the final beat for the next real Scene, not between beats in the same scene.

For `ManualBeat`, time never automatically advances to another beat or scene, preserving the established manual contract.

## Explicit manual beat advance

At the accepted base there is no playback click wired to the on-panel `Next` element; it is rendered/selectable for authoring, while the toolbar `Следующая` calls `ComposerNext()` and is real Scene navigation.

M-DIALOGUE therefore adds exactly one controller-owned action, conceptually `AdvanceDialogue()`, with these semantics:

- if another beat exists in the current scene: advance `CurrentBeatIndex`, reset only `BeatElapsedSeconds`, and rebuild dialogue/focus without `ResetScene`;
- if the current beat is the final beat and the playback scope allows ordered continuation: perform the existing real scene boundary;
- if the scope is `Play Scene`: stop at the selected scene end according to existing single-scene semantics;
- no project mutation or Undo record.

The existing toolbar `Previous` / `Next` remain Scene-level navigation and are not overloaded.

The existing on-panel `Next` hit region is the natural VN dialogue-advance control. During playback, the window may route a click on that hit target to `AdvanceDialogue()` without enabling the authoring drag/select behavior. This is a focused transport integration, not a new UI framework. Controller-level behavior is the source of truth and is independently testable.

## Play Scene

`Проиграть сцену` starts the selected visual Scene from beat 0 using the existing neutral clean-start behavior. It progresses through that scene's beats. After the final beat, single-scene playback stops; it does not spill into the next Scene.

## Play All

`Проиграть всё` starts Scene 0 / Beat 0. Within each scene it advances beats without media/scene reset. Only after the final beat does the existing ordered scene boundary execute, including M-C2 same/different-video continuity. The next scene starts at beat 0.

## Play From Here

`Проиграть отсюда` starts the selected Scene from beat 0 using the existing M-B neutral clean-start semantics. It then advances remaining beats and subsequent real scenes normally. It does not inherit a stale authoring-selected beat or stale video timestamp.

## Pause, restart, and Scene navigation

Pause preserves current Scene, current beat, scene elapsed time, beat elapsed time, and existing media timeline behavior.

Restart resets the active playback scope's current Scene to beat 0 and resets beat progression consistently with existing restart semantics. It must not mutate project data.

Toolbar `Previous` and `Next` remain Scene navigation. A real scene navigation/reset starts the destination scene at beat 0. Dialogue beat reordering/navigation belongs in the `Реплики` editor and explicit dialogue-advance path, not in toolbar scene navigation.

## Media continuity contract

Within one visual scene, Beat A -> Beat B must perform zero scene-media lifecycle work:

- no `ResetScene`
- no `OpenMedia`
- no `Prepare`
- no `ReleaseMedia`
- no `Dispose`
- no video restart
- no background reload
- no BLACK frame introduced by beat progression

The same `VideoPlayer`/preview resource and media timeline continue. M-C2 is only for actual Scene boundaries after the final beat.

Image backgrounds and characters similarly remain active; scene-entry animation is not replayed merely because dialogue changes.

## Persistence and Unicode

Portable schema v2 preserves exact beat order, stable IDs, speaker, text, narration, empty strings, line breaks, and Unicode. Automated coverage must include representative Russian, Chinese, and Latin text such as `Привет`, `你好`, and `Hello`.

Editor-local storage continues to use the existing `Library/ROKAS/VnSceneComposer` path and must not write production Assets.

## Validation and bounds safety

Serialization validation requires every authored v2 scene to have at least one non-null beat with a stable unique ID. Strings normalize null to empty strings.

Transient authoring/playback selection must safely clamp or fall back if a stale beat ID/index is encountered. Playback recovery may select a valid effective beat in memory but must not silently rewrite saved project data.

Deleting the last beat through normal authoring operations creates one new empty beat, so a valid editor-authored Scene is never left with undefined dialogue state.

## Undo and project mutation boundaries

Authoring mutations use existing Scene Composer Undo:

- add beat
- delete beat
- duplicate beat
- reorder beat
- edit speaker/narration/text

Playback beat changes create no Undo records and never rewrite beat order, selection, scene data, defaults, or overrides.

## Testing strategy

Use the existing Scene Composer fixture families rather than a separate harness.

### Model / compatibility / persistence

Prove:

- new Scene starts with one beat;
- legacy schema-v1 single dialogue imports as exactly one beat;
- schema-v2 multi-beat serialization round-trips deterministically;
- ordering and Unicode survive round-trip;
- duplicate beat creates independent identity;
- duplicate scene copies all beats but regenerates scene/beat identities;
- delete-last-beat normalization leaves one editable empty beat;
- duplicate/missing beat IDs are rejected in portable v2 validation;
- editor/playback beat selection does not leak into portable JSON.

### Playback

Prove:

- Scene begins at beat 0;
- beat -> beat keeps `CurrentSceneIndex` unchanged;
- beat -> beat keeps media resource identity and Prepare count unchanged;
- `SceneElapsedSeconds` continues while `BeatElapsedSeconds` resets;
- scene-entry character/background animation is not replayed on beat advance;
- final beat -> next real Scene uses existing ordered boundary;
- `Play Scene` stops after its final beat;
- `Play All` crosses beats and scenes in order;
- `Play From Here` starts selected Scene beat 0 cleanly;
- manual beat action obeys `ManualBeat` semantics;
- restart resets beat progression consistently;
- one-beat scenes preserve historical behavior;
- existing M-C/M-C2 video tests remain green.

### Editor / Undo

Prove with existing window/UX test style:

- `Текст` exposes `Реплики` and `+ Реплика`;
- add/delete/duplicate/reorder actions mutate canonical beat list;
- selection follows stable beat identity after reorder;
- speaker/text edits target only selected beat;
- selecting a beat alone does not reset media/playback or mutate project data;
- normal Undo restores beat mutations.

## Manual acceptance after automated GREEN

Automated closure does not equal manual closure. After focused and established regression suites are green, stop for real Unity review.

Manual acceptance includes:

- one video scene, one character, four beats (`Aiko — Первая реплика`, `Tim — Вторая реплика`, `Aiko — 第三句话`, `Tim — Fourth line`);
- continuous video timeline and scene visuals across all four beats;
- no character re-entry, BLACK, video restart, or scene reset between beats;
- correct speaker/text order;
- Play Scene stops according to selected-scene semantics after final beat;
- Play All crosses the true Scene boundary only after the final beat and then uses normal M-C2 behavior;
- static-image multi-beat scene has no background reload/flash;
- save/reload preserves order/text/speakers;
- duplicate scene is independent;
- Undo works for add/delete/reorder and established text-edit Undo semantics;
- creator-facing beat list is understandable without documentation.

After automated GREEN: `M-DIALOGUE AUTOMATED CLOSED = YES`, `M-DIALOGUE MANUAL CLOSED = NO`, then STOP. M-E remains blocked until explicit manual acceptance.

## Out of scope

Do not implement during M-DIALOGUE:

- branching dialogue or choices;
- conditions, variables, labels, goto, quest/script actions;
- per-beat background/video/media;
- per-beat arbitrary scene animations or full scene overrides;
- new BGM/audio/voice-over system;
- dialogue panel redesign/custom PNG panel;
- cinematic curtain transition;
- major M-E UI cleanup;
- localization database;
- keyframe/timeline editor;
- one storyboard card per beat.
