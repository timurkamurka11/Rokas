# ROKAS VN Editor Russian UX Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to execute this plan task-by-task. Every behavior-changing slice follows RED → validate RED → minimal GREEN → focused GREEN → commit.

**Goal:** Turn the already verified Scene Composer engine into the single normal, intuitive Russian-language VN authoring workspace without deleting functionality, changing the canonical data model, writing Yarn, applying to production, or integrating branches.

**Architecture:** Keep the existing `VnPresentationWorkshopWindow` partial EditorWindow and the proven `VnSceneComposer*` model/playback/renderer/storage stack. The UX rebuild is a presentation and authoring-flow layer over `VnSceneComposerProject → Project Defaults → Scene → Scene Overrides → Scene Elements`. Legacy Presentation Workshop code remains internal for compatibility/tests/import paths, but ordinary `OnGUI` must expose one Scene Composer workspace. Prefer one small editor-only Russian UI string/helper source rather than scattering duplicated labels; do not add a localization package or another editor/data store.

**Tech stack:** Unity 6000.3.19f1, C#, UnityEditor IMGUI, NUnit EditMode, GameCI, existing ROKAS Scene Composer/Presentation Workshop renderer and VN10 resolver.

**Approved working branch:** `feature/vn-scene-composer` only.

**Technical baseline inspected before this plan:**
- baseline marker HEAD `6d9c7880286ec9e385c01e88d70c9c0b90211ca5`
- source TREE `bb13ca4515c766fb87c75812c8674ead278d0320`
- implementation parent `420ac07339a5bceb7c7aa6884350ab4269c2364b`
- prior proof: focused 87/87, actual-source parity 100/100, Full EditMode 178/178, strict known-five Linux VideoPlayer classifier, Windows Player and real `Rokas.exe` GREEN.

## Actual source map

The UX work must use these inspected seams rather than inventing a parallel system.

- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.cs`
  - existing `EditorWindow`, menu/title, `OnGUI`, legacy `currentPreset`, old Workshop draw path.
  - current `OnGUI` draws a workspace selector, then either Scene Composer or legacy Workshop.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposer.cs`
  - Scene Composer activation, workspace mode toolbar, three-column layout, storyboard, preview, inspector, project storage, media/text/character controls, playback toolbar, cache/lifecycle.
  - current authoring video thumbnail opens at fixed `640×360`.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerAuthoring.cs`
  - direct preview selection/drag/nudge, character transform controls, presentation UI, presets, Preview Text, raw presentation foldouts, video preparation status.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerPresentation.cs`
  - canonical Project Defaults / Scene Overrides view and mutation APIs for layout, typography, typewriter, expression, character motion, bounce, background transition, stage, focus, UI feedback, timing and presets.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerAssets.cs`
  - Asset Library onboarding/refresh/background selection UI and editor-only catalog integration.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerVideo.cs`
  - selected preview playback frame bridge.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerMotionMediaEditing.cs`
  - `VnSceneComposerVideoPreview`: one RenderTexture per preview object, `Prepare()` dedupe, first-frame request, Play/Pause/Restart/Dispose, frame-ready callback and error state.
  - deterministic GIF path and motion preview registry cleanup.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnSceneComposerPlaybackController.cs`
  - authoritative Play Scene / From Here / All / Pause / Restart / Previous / Next path; real playback opens target/source video previews at `1280×720` and only plays media when transport requests it.
- `Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerFinalParity.cs`
  - automatic TextCore glyph validation and canonical effective UI-feedback sampling.
- `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerWindowTests.cs`
  - real EditorWindow authoring routes, CRUD, renderer, playback, storage, character catalog, thumbnails, Undo, no-apply guard.
- existing `VnSceneComposer*Tests` plus `VnUiWorkshopTests*`
  - preserve existing SC-A→SC-I, ExternalVideo, Asset-Aware, 100/100 parity and legacy Workshop compatibility.
- `.github/workflows/vn-scene-composer.yml`
  - ordinary pushes run source guard + focused Scene Composer tests; expensive compatibility/regression/player proof remains marker-gated by `[full-scene-composer-proof]`.

## Non-negotiable invariants

1. No new branch/ref/tag, no `update_ref`/force-push workflow for discovery, and no integration.
2. No production `Assets/Rokas/Scripts/Presentation` mutations, no Yarn generation/write, no ApplyToProduction, no save/quest/story hookup.
3. Preserve all 100/100 presentation capabilities; move technical controls behind `Дополнительно` instead of deleting them.
4. Preserve direct character/UI selection, mouse drag, arrow/Shift-arrow nudge, numeric transforms, Undo/Redo, scene CRUD/reorder, media, character states, GIF/video, typewriter, transitions, UI feedback, timing, presets, project persistence and playback transports.
5. Normal Scene Composer authoring must continue to use canonical defaults/overrides and must not depend on legacy `currentPreset`.
6. Manual real-video GUI review and actual Editor screenshots are completion gates; automated tests cannot substitute for them.

## Task 0 — Focused UX harness and CI gate

**Files:**
- Create: `Assets/Rokas/Tests/EditorWorkshop/VnSceneComposerUxTests.cs`
- Create: matching `.meta`
- Modify: `.github/workflows/vn-scene-composer.yml`

**Step 1 — test file first:** Add the UX fixture with only UX-A user-visible contract tests. Do not change product code.

**Step 2 — expose focused CI:** Add a new non-expensive EditMode step `Scene Composer UX-A→H` filtering `Rokas.EditorTools.Tests.VnSceneComposerUxTests`, and include its artifact directory in the focused upload.

**Step 3 — validate RED:** The first CI run with the UX step must fail for actual current behavior: ordinary window still exposes `Presentation Workshop | Scene Composer` as two selectable workspaces and still opens with the old user-facing identity.

**Step 4 — preserve baseline:** Existing SC-A→SC-I steps must still run unchanged on every ordinary push. Do not trigger the expensive marker proof.

## Task 1 — UX-A: one canonical user-facing editor

**Files:**
- Modify: `VnPresentationWorkshopWindow.cs`
- Modify: `VnPresentationWorkshopWindow.SceneComposer.cs`
- Test: `VnSceneComposerUxTests.cs`

**RED contract:**
- the normal menu/window identity is the Russian VN editor (`ROKAS — Редактор новеллы` or concise equivalent);
- the normal `OnGUI` authoring route renders Scene Composer directly;
- Presentation Workshop is not presented as a peer selectable tab/workspace in ordinary authoring;
- Scene Composer remains the canonical authoring workspace;
- legacy Workshop implementation can still be instantiated/called by compatibility tests and old preset paths.

**GREEN implementation:**
- retain `VnPresentationWorkshopWindow` and legacy methods;
- remove the ordinary workspace-toggle draw call from the user-facing route;
- initialize/open into Scene Composer as the normal path;
- do not delete legacy Workshop code or `currentPreset` compatibility state.

**Focused verification:** UX fixture + SC-I + existing Workshop compatibility smoke if affected.

## Task 2 — UX-B: Russian UI string architecture and primary information architecture

**Files:**
- Create if useful: `VnSceneComposerUiText.cs`
- Modify: `VnPresentationWorkshopWindow.SceneComposer.cs`
- Modify as needed: `SceneComposerAssets.cs`, `SceneComposerAuthoring.cs`
- Test: `VnSceneComposerUxTests.cs`

**RED contract:** Require ordinary visible core strings:
`Сцены`, `+ Добавить сцену`, `Предпросмотр`, `Сцена`, `Фон`, `Текст`, `Персонажи`, `+ Добавить персонажа`, `Анимация`, `Воспроизведение`, `Дополнительно`, `Проиграть сцену`, `Проиграть всё`.

Also prove obvious legacy normal-mode labels such as `Storyboard`, `Selected Scene`, `Text / Narration`, `Presentation Workshop`, raw media kind names and seven equal transport labels do not remain as the ordinary path.

**GREEN implementation:**
- keep three-column layout but simplify headings and hierarchy;
- friendly Russian empty states for no scenes/no media/no characters;
- translate ordinary buttons/status/help/error text;
- use concise Russian tooltips for non-obvious controls;
- data filenames/character names/internal enum values remain unchanged internally.

## Task 3 — UX-C: one real scene text

**Files:**
- Modify: `VnPresentationWorkshopWindow.SceneComposer.cs`
- Modify: `VnPresentationWorkshopWindow.SceneComposerAuthoring.cs`
- Test: `VnSceneComposerUxTests.cs`

**RED contract:**
- ordinary inspector exposes exactly one canonical scene-text authoring surface, backed by `scene.previewText` plus `scene.speaker`/`scene.narration`;
- visible labels are `Текст сцены`, `Говорящий`, `Текст без персонажа`, `Текст`;
- Preview/sample text is not rendered as a competing normal text editor;
- sample text remains reachable only under `Дополнительно → Тест оформления текста` and explicitly says it is not scene text;
- glyph warning remains automatic when relevant.

**GREEN implementation:** Move `DrawSceneComposerPreviewTextControls()` behind collapsed Advanced UI, rename it for typography testing, preserve its storage and glyph-check APIs exactly.

## Task 4 — UX-D: contextual character authoring

**Files:**
- Modify: `VnPresentationWorkshopWindow.SceneComposer.cs`
- Modify: `VnPresentationWorkshopWindow.SceneComposerAuthoring.cs`
- Preserve resolver/catalog code unchanged unless a tested defect requires it.
- Test: `VnSceneComposerUxTests.cs`

**RED contract:**
- clean current-scene character list and `+ Добавить персонажа`;
- selected character exposes `Персонаж`, `Поза / эмоция`, `Позиция на сцене`, `Размер` and relevant animation entry points;
- no-character state is concise and does not flood inspector with transform fields;
- left/center/right/free concepts map onto existing stage slot + free X/Y data;
- direct mouse drag/nudge remains functional.

**GREEN implementation:** Contextualize existing controls; do not replace `VnSceneComposerCharacter` fields or merged visual resolver.

## Task 5 — UX-E: animation organization without lost parity

**Files:**
- Modify: `VnPresentationWorkshopWindow.SceneComposerAuthoring.cs`
- Possibly add small editor-only label/enum-display helpers in `VnSceneComposerUiText.cs`
- Test: `VnSceneComposerUxTests.cs`

**RED contract:** Existing serialized presentation properties are discoverable through two understandable groups:
- `Анимация персонажа`: Появление, Исчезновение, Акцент / движение, Смена позы / эмоции.
- `Анимация сцены`: Переход фона, Расположение персонажей, Фокус говорящего, Тайминг сцены.

No duplicate visible `Action Bounce` heading is allowed. Translate user choices without changing enum serialization: Instant → Без анимации, Fade → Плавное появление/переход, Slide+Fade → Появление со сдвигом, Curtain → Шторка, etc.

**GREEN implementation:** Draw the same canonical serialized properties under intent-based Russian groups. Keep numeric duration/easing/offset details expandable rather than always visible.

## Task 6 — Background/media and Asset Library simplification

**Files:**
- Modify: `VnPresentationWorkshopWindow.SceneComposer.cs`
- Modify: `VnPresentationWorkshopWindow.SceneComposerAssets.cs`
- Test: `VnSceneComposerUxTests.cs` plus existing image/media/asset tests.

**RED contract:**
- normal background entry point is `Выбрать фон / медиа` and naturally accepts supported image/GIF/video files;
- library selection is a friendly secondary entry point;
- selected media shows friendly file/type/scale/loop labels, never requiring understanding of `ExistingRokasAsset`, `ExternalVideo`, `ImageStill` or `AssetPurpose`;
- `Библиотека ресурсов` exposes normal actions `Добавить в библиотеку`, `Обновить`, `Открыть папку`; Purpose/managed-editor-only internals are Advanced/help only.

**GREEN implementation:** Reuse existing `ComposerSetExternalImage/Video/Gif`, `ComposerSetExistingRokasAsset`, scale/loop and asset-catalog APIs. Do not import local preview media into production Assets.

## Task 7 — UX-F: video authoring lifecycle

**Files:**
- Modify only as tests justify: `VnSceneComposerMotionMediaEditing.cs`
- Modify: `VnPresentationWorkshopWindow.SceneComposer.cs`
- Modify: `VnPresentationWorkshopWindow.SceneComposerAuthoring.cs`
- Modify if needed: `VnSceneComposerPlaybackController.cs`
- Extend existing ExternalVideo/MotionMedia tests and/or `VnSceneComposerUxTests.cs`.

**RCA before fix:** Instrument/inspect actual calls and state for Prepare, VideoPlayer creation/disposal, RenderTexture creation/reuse, repaint callbacks, scene switching, loop/restart/pause and hidden playback. Do not add sleeps.

**RED contracts:**
- selecting a video must not intentionally replace the last valid/poster visual with an empty black authoring state;
- idle authoring calls Prepare once, obtains/stabilizes the first frame, then leaves the VideoPlayer paused;
- merely editing does not continuously play/decode video;
- `Проиграть сцену` / `Проиграть всё` activate real playback via the existing playback controller;
- Pause is stable, Restart deterministic, loop flag preserved, scene switch/dispose releases callbacks/player/RT exactly once;
- Russian overlay states: `Подготовка видео…` and `Не удалось открыть видео` with concise reason.

**GREEN implementation:** Preserve the current proven prepare/frameReady lifecycle where correct. Separate authoring poster preparation from playback transport clearly and keep/reuse the last valid visual during preparation.

## Task 8 — UX-G: video quality and aspect

**Files:**
- Modify: `VnPresentationWorkshopWindow.SceneComposer.cs`
- Modify if justified: `VnSceneComposerMotionMediaEditing.cs`
- Tests: motion media / ExternalVideo / UX fixture.

**Known inspected issue:** the authoring thumbnail path currently requests ExternalVideo at hard-coded `640×360`, while real playback requests `1280×720`.

**RED contracts:**
- authoring video preview sizing must be resolution-/source-aware rather than an unjustified tiny fixed RT;
- 720p and 1080p sources are not silently forced to a permanently tiny texture when a larger preview is required;
- Fit/Fill/Stretch mapping preserves intended aspect semantics;
- RenderTexture is not recreated every repaint/frame; resizing is bounded and intentional.

**GREEN implementation:** Choose the smallest safe resolution-aware strategy supported by actual VideoPlayer metadata and preview dimensions. Reuse stable resources; do not allocate a large RT every frame.

## Task 9 — UX-H: Basic vs Advanced progressive disclosure

**Files:**
- Modify: `VnPresentationWorkshopWindow.SceneComposerAuthoring.cs`
- Modify: `SceneComposerAssets.cs` / `SceneComposer.cs` where relevant
- Test: `VnSceneComposerUxTests.cs`

**RED contract:** Basic mode does not show hit-region editing, raw JSON import/export, sample Preview Text editor, UI feedback phase/numeric internals, Reference Motion, raw X/Y/W/H technical layout values, glyph diagnostic controls or technical comparison. Opening `Дополнительно` makes all required functionality reachable.

**GREEN implementation:** Move rather than remove:
- Тест оформления текста
- Расширенная типографика
- Разметка интерфейса
- Эффекты интерфейса (#90–93 preserved)
- Reference Motion
- Шаблоны оформления / JSON
- exact layout numbers/easing/debug comparison.

Scope UI becomes friendly `Применить: Только к этой сцене / Ко всем сценам` while still mapping directly to `presentationOverrides` / `defaultPresentation`.

## Task 10 — Playback toolbar and final ordinary-flow cleanup

**Files:**
- Modify: `VnPresentationWorkshopWindow.SceneComposer.cs`
- Test: `VnSceneComposerUxTests.cs`

**RED contract:** Idle toolbar prominently shows only `◀ Назад`, `▶ Проиграть сцену`, `▶ Проиграть всё`, `Далее ▶`. `Пауза` and `Сначала` appear when relevant. `Проиграть отсюда` remains available as a secondary/context action.

Preserve all controller APIs and semantics; this is only control hierarchy.

## Task 11 — Focused UX acceptance and ordinary-language audit

**Files:**
- Extend: `VnSceneComposerUxTests.cs`
- Update docs/help only if source inspection shows an existing appropriate place.

Run and record:
- UX-A single editor
- UX-B Russian core UI
- UX-C one canonical text
- UX-D contextual characters
- UX-E animation grouping
- UX-F video lifecycle
- UX-G video quality/aspect
- UX-H Advanced/simple separation
- SC-A→SC-I unchanged focused suites
- focused Asset-Aware and ExternalVideo subsets where applicable.

Perform a source audit of ordinary Scene Composer UI strings. Remaining English is allowed only for filenames/data names/internal enums/classes/JSON keys/technical logs or legacy compatibility UI that ordinary users cannot reach.

## Task 12 — Manual Unity Editor acceptance

Automated tests are not sufficient.

Using an actual Unity Editor GUI runner/workstation, perform the user flow without touching legacy Workshop:
1. Open VN editor.
2. Add scene.
3. Choose background.
4. Add character.
5. Choose pose.
6. Drag character.
7. Choose speaker.
8. Enter scene text.
9. Choose text speed.
10. Choose character entrance.
11. Choose background transition.
12. Choose manual/automatic advance.
13. Play Scene.
14. Add second scene.
15. Play All.

Record whether any step requires internal-architecture knowledge. Success requires **NO**.

Capture actual Editor screenshots, preferably at 1920×1080:
- empty/first scene
- image background
- character
- text editing
- character animation
- scene animation
- Advanced collapsed
- Advanced expanded
- video preparing
- video prepared first frame
- video playback
- multiple-scene storyboard.

## Task 13 — Manual real-video GUI proof

Use `sky_seamless_loop.mp4` or the same known real test video if it is available to the GUI runner.

Observe and record, do not infer:
- load time until poster/first frame;
- whether a black loading frame appears;
- idle editing smoothness;
- Play Scene smoothness;
- loop;
- pause;
- restart;
- quality;
- aspect handling.

If visually choppy/glitchy/constantly re-preparing, perform RCA and return to UX-F/G. Never call video fixed from automated tests alone.

## Task 14 — Final regression gate

Only after focused UX suites and manual UX/video acceptance are complete:
- SC-A→SC-I
- new UX suite exact counts
- Presentation Workshop compatibility
- VN10 compatibility
- Asset-Aware
- ExternalVideo
- 7-job VN regression matrix
- Full EditMode
- strict Full PlayMode known-five classifier (never broaden)
- Windows Development Player
- runtime/editor leak audit
- real `Rokas.exe` JSON/screenshots
- production diff/no-apply guard
- protected-ref audit
- unexpected-ref audit.

Then and only then create one explicit empty `[full-scene-composer-proof]` marker preserving the source tree and run the established expensive proof once.

## Commit strategy

Use small meaningful commits; expected sequence may be refined by actual RED boundaries:
1. `test: define Russian single-editor UX contracts`
2. `feat: expose Scene Composer as sole normal editor`
3. `feat: simplify Russian scene and media inspector`
4. `feat: unify scene text authoring UX`
5. `feat: simplify character and animation authoring`
6. `fix: improve video authoring preview lifecycle`
7. `feat: move advanced VN tooling behind Advanced`
8. `test: complete VN editor UX acceptance`
9. `docs: update VN editor authoring workflow`

No giant all-in-one UI commit.

## Checkpoint discipline

After each major phase record:
- branch / HEAD / TREE
- phase
- valid RED run ID and exact failure
- GREEN run ID and counts
- user-visible change
- focused regressions
- unexpected refs ABSENT/PRESENT
- protected refs PASS/FAIL.

Do not claim `ROKAS VN EDITOR UX COMPLETE` until information architecture, Russian UI, single text, single editor, Basic/Advanced separation, video UX, manual flow, manual real-video GUI review and all regression gates pass.