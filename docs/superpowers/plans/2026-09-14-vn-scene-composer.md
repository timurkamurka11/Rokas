# ROKAS VN Scene Composer / Storyboard Editor — Implementation Plan

> Source of truth for this iteration: verified VN10 final proof commit `f58f1db08fc225c2831ff59d50d42e8c07ea15ce` (tree `71ae454dd56c591fc592e03395a7263fa69d4cab`).

## Goal

Extend the existing editor-only `ROKAS/VN UI Workshop` into a reusable Scene Composer workspace that assembles an ordered VN sequence from Scene Cards while reusing the verified Workshop renderer and VN10 presentation systems. The Composer must never become a second narrative engine and must not write to production Yarn, runtime VN classes, prefabs, or production assets.

## Architecture

Keep all Composer implementation under `Assets/Rokas/Scripts/Editor/VnUiWorkshop`. Reuse `VnPresentationWorkshopWindow` as a partial EditorWindow, `VnPresentationWorkshopPreviewRenderer` for the real ROKAS mirror preview, `VnPresentationWorkshopVn10Resolver` and existing VN10 override types for presentation, `VnCharacterVisualCatalog` for authored character states, and the current Workshop resolution mapping. Composer state is an editor-only project model with stable IDs, project defaults plus scene overrides, and local/portable media metadata kept distinct.

External image/video/GIF files are preview dependencies only. They must not be copied into production `Assets`. Local cache/project state belongs below `Library/ROKAS/VnSceneComposer/`. Existing ROKAS assets keep repository references/GUID semantics. Video preview follows the already proven prepare/handler/stop/release lifecycle pattern without changing runtime `VideoSequencePresenter`. No GIF package is currently present, so GIF support must use the smallest editor-only implementation with deterministic frame timing/cache cleanup and no new runtime dependency.

## TDD slices

### SC-A — Contract, model, production guard
- Add a dedicated branch CI guard diffing from the exact VN10 source commit.
- Add reflection-first RED tests for `VnSceneComposerContract`, project/scene/media/character models and VN10 preset reuse.
- Implement only the minimal serializable editor model required to make the SC-A contract GREEN.
- Keep all runtime/Yarn/Packages paths forbidden.

### SC-B — Scene list CRUD
- RED tests for Add, Duplicate, Delete, Rename, Move Up/Down/reorder.
- Stable scene IDs survive reordering; duplicate receives a new ID and deep-copies mutable scene data.
- Keep serialization order deterministic.

### SC-C — Existing ROKAS asset + external image media
- Add media resolution abstraction for repository ROKAS assets and PNG/JPEG local previews.
- Validate missing files and distinguish portable repository refs from local preview refs.
- Add viewport-fit/fill behavior and cache invalidation without production import.

### SC-D — Video + GIF preview
- Add editor-only video controller with play/pause/restart/loop and deterministic teardown.
- Add smallest safe animated GIF decoder/cache if no repository support exists; respect per-frame timing.
- Release callbacks, textures and caches on scene change, stop, window close and domain reload.

### SC-E — Characters, dialogue and VN10 composition
- Consume authored character/state catalog only; no arbitrary character importer.
- Support 0–3 visible characters, authored state IDs, stage slots, speaker, multiline text and narration.
- Resolve project default VN10 preset then apply scene override data without duplicating the VN10 engine.

### SC-F — Transitions and timing
- Compose existing VN10 background/character/expression/staging/bounce/focus/timing primitives per scene.
- Preserve one-click-one-beat as production semantics; authoring auto duration remains preview-only.

### SC-G — Deterministic storyboard playback
- Implement Play Scene, Play From Here, Play All, Pause, Restart, Previous and Next.
- Reset all transient offsets/alpha/focus/bounce/media state on restart and scene boundaries.
- Never touch Yarn, story progress or save data.

### SC-H — Persistence/import/export
- Editor-local save/load below `Library/ROKAS/VnSceneComposer/projects/`.
- Versioned deterministic JSON with source metadata, stable IDs and ordered scenes.
- Validate schema/version/duplicate IDs/media refs/character states/source mismatch.
- Never serialize ephemeral playback state or pretend local-only media is portable.

### SC-I — Usability
- Storyboard thumbnails with dirty/cache rules, selection-to-preview/inspector sync, drag/drop reorder plus Move Up/Down fallback.
- Unity Undo/Redo for destructive/major edits where practical.
- Clear empty state, warnings, keyboard affordances and non-programmer-friendly layout.
- Retain existing Motion Lab and existing Workshop controls.

### SC-J — Final proof
- Extend CI to run Composer focused EditMode tests, all existing Workshop/VN10 tests, 7/7 VN regressions, full EditMode, strict full PlayMode classifier, Windows Development Player and real `Rokas.exe` proof.
- Audit diff against `f58f1db08fc225c2831ff59d50d42e8c07ea15ce`, runtime assembly leak, protected refs and no production apply path.
- Final state: ready for Scene Composer user review; manual GUI review may remain pending if no GUI runner is available; not applied to production; not integrated.

## Checkpoint discipline

For SC-A through SC-I: write the focused failing test first, prove RED in CI, implement the smallest GREEN change, rerun focused CI, then checkpoint. Do not run the expensive player proof after every editor-only slice. Use the full proof only for SC-J.

## Non-goals

No production apply, no Yarn generation/editing, no runtime VN mutation, no prefab rewrite, no external-media import into production assets, no new narrative engine, no arbitrary character importer, no Timeline dependency unless separately justified, no new reusable skills, and no integration into protected branches.
