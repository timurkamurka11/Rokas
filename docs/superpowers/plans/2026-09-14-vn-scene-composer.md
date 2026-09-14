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

---

# Continuation Iteration — Asset-Aware Onboarding + Visual Playback / Preset Polish

> User-approved continuation target: work directly on `feature/vn-scene-composer` from verified checkpoint `92c13f6f3efbbe4c725856cd688bd6c79912aa5d`. Preserve SC-A→SC-J behavior unless a new focused regression proves a required fix. Do not create another feature branch, touch Yarn, apply Composer data to production, or integrate.

## Design decisions

1. **One editor architecture.** Extend the existing `VnPresentationWorkshopWindow` / Scene Composer workspace. Do not create a second window, timeline, transition engine, or runtime VN engine.
2. **Portable onboarded assets vs local previews stay distinct.** Reusable Composer assets live under `Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets/`; external image/GIF/video preview bindings keep their existing local-only SC-H semantics.
3. **Deterministic editor-only catalog.** Add a JSON-backed catalog with stable asset IDs, purpose/category, display name, project-relative path, AssetDatabase GUID, SHA-256 content hash, character/state metadata, and validation status. Portable JSON must never contain absolute user-machine paths.
4. **Production catalog remains read-only.** `VnCharacterVisualCatalog` is an immutable fallback adapter. New full-body PNG states are resolved through an editor-only merged visual resolver; no source-code constant or GUID editing is required.
5. **Full-body authored states are first-class.** One transparent PNG can be an entire pose/state. Onboarded character states use full texture UVs and preserve source pixels/alpha/aspect ratio; there is no face/body separation or skeletal interpolation.
6. **Asset library UX stays inside Scene Composer.** Add `Asset Library`, `Onboard Asset`, `Refresh Assets`, selector-visible metadata/warnings, and conservative `Unregister from Composer`. Unregister never deletes production ROKAS assets and does not delete the underlying onboarded file by default.
7. **Selectors are data-driven.** Character/state and background pickers consume actual production + onboarded assets. No fake state names and no manual GUID entry for normal use.
8. **One observable playback path.** SC-F `VnSceneComposerTransitionSampler`, SC-G `VnSceneComposerPlaybackController`, and `VnPresentationWorkshopPreviewRenderer` remain authoritative. Renderer receives/consumes source/target background transition data instead of discarding it. Legacy Workshop preview buttons delegate to shared sampled rendering rather than maintaining visually dead parallel clocks.
9. **Scene-to-scene transitions use real endpoints.** The source is the actual final visual state of the previous scene, including its background and authored state. Target is the actual intended next scene. No fallback flash/reset unless authored.
10. **Defaults are tuned only after correctness.** First prove rendered-frame differences for bounce, enter/exit, pose crossfade, background fade/curtain, typewriter, stage/focus/UI feedback, replay baseline. Then adjust default amplitudes/durations to clear but restrained values.

## Execution plan

### P1 — Recover exact current branch
- Verify remote branch SHA/tree/recent commits and preserve anything newer than the prior anchor.
- Record local-worktree limitation if no checkout exists.

### P2 — Inspect current visual paths
- Inspect Scene Composer window, composition, transition sampler, SC-G controller, preview renderer, and legacy VN10 Motion Lab.
- Identify where observable transition data is dropped or replaced by default frames.

### P3 — Asset-Aware Onboarding RED contracts
- Add `VnSceneComposerAssetLibraryTests.cs` first.
- RED contracts: transparent PNG onboarding, logical purpose classification, character/state association, selector discovery, refresh, duplicate rejection, missing-file warning, deterministic catalog roundtrip, no absolute paths, production assets read-only, unregister non-destructive.
- Add a focused CI step to the existing non-expensive Composer job only.

### P4 — Generic Asset Library GREEN
- Add editor-only asset types/catalog/storage/onboarder under `Assets/Rokas/Scripts/Editor/VnUiWorkshop`.
- Managed asset root: `.../OnboardedAssets/` with deterministic category subfolders.
- Copy source bytes unchanged, import through AssetDatabase, configure transparent character PNG import settings without altering pixels, hash source, write deterministic catalog, refresh/validate missing files, detect duplicates.

### P5 — Asset-aware selectors and composition
- Add merged visual resolution: onboarded character state first, production `VnCharacterVisualCatalog` fallback.
- Wire existing Scene Composer Character / Authored State popup to actual merged states.
- Add asset-aware background list with display names/thumbnails and direct scene assignment; retain existing ROKAS ObjectField and local media buttons.
- Invalidate only affected thumbnails/cache entries after refresh/onboard/unregister.

### P6 — Visual playback RED contracts
- Add `VnSceneComposerVisualPlaybackTests.cs` first.
- Assert rendered/renderer-facing differences, not only sampler state: bounce midpoint differs/end baseline exact; enter early differs/final exact; pose A/B crossfade contains correct textures/alphas; background fade/curtain source→mid→target differs; typewriter visible count changes; stage/focus changes are visible; restart is deterministic; Play Scene/Play All use the same observable path.
- Add legacy Workshop preview wiring tests that prove buttons influence the actual preview frame/render state.

### P7 — Shared observable renderer/playback GREEN
- Extend renderer with a lightweight transition render context that can draw source/target background fade/curtain and UI feedback without per-frame large texture allocations.
- Make Scene Composer `DrawSceneComposerPreview` pass SC-G playback transition data instead of dropping it.
- Keep SC-F/G as the only Composer transition/playback state machine.
- Route legacy preview effects through the same sampled renderer-facing primitives; remove/deprecate dead duplicate visual-only paths where safe.

### P8 — Correct scene-to-scene endpoints
- Preserve/capture the previous scene final rendered state before SC-G releases target media.
- Resolve previous static image backgrounds accurately; retain the last real previous GIF/video texture for the transition boundary without reopening on every repaint.
- Expression/pose transition is image A→image B crossfade only; duration zero snaps exactly.
- Background FROM auto-resolves previous scene, TO resolves target scene; expose clear labels and only optional override if justified.

### P9 — Preset/default quality
- Audit existing resolver defaults after behavior is visible.
- Tune only values that are effectively invisible or excessive; preserve numeric controls and add at most small Subtle/Normal/Strong quick choices where they materially improve usability.
- Keep characters inside preview safe area unless explicitly authored otherwise.

### P10 — Mina real acceptance
- Use the attached `ROKAS_Mina_Pose_Pack_v2_7png` only after generic onboarding tests are GREEN.
- Read real manifest/filenames, onboard all seven PNGs through the same generic API, preserve `941×1672` RGBA source bytes/alpha, register under Mina with state labels derived from manifest/filenames, and verify seven selector entries/thumbnails.
- Build two scenes using different onboarded Mina states and prove Play Scene / Play All pose transition.

### P11 — Focused proof
- Run new asset-library tests, visual playback tests, existing Composer SC-A→SC-I tests, and existing VN10/Workshop focused tests.
- RCA any failure before changing implementation.

### P12 — Single expensive final proof
- After all focused suites are GREEN, create one explicit final-proof marker and run the existing expensive proof once: 7-job VN regression matrix, full EditMode, strict PlayMode classifier, Windows Development Player + runtime leak audit, real `Rokas.exe` screenshot/JSON proof, diff/no-apply/protected-ref audits.
- Final report must explicitly state local worktree cleanliness limitation unless a real checkout becomes available.

## Acceptance boundary

Success means the editor can onboard a real asset by purpose, discover it without source/GUID editing, select it in the appropriate Scene Composer field, and visibly play existing VN10/SC-F/SC-G transitions between actual scene visuals. It still does **not** mean ApplyToProduction, Yarn generation, runtime story wiring, save/quest integration, or branch integration.
