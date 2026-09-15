# ROKAS VN Editor Manual Stabilization Continuation Plan

> Continuation of `docs/superpowers/plans/2026-09-15-vn-editor-russian-ux-cleanup.md`. UX-A→H and the visual-polish pass are historical GREEN foundation and are not reopened wholesale.

**Goal:** Stabilize the real Unity Scene Composer so ordinary VN authoring is simple, direct, predictable, and visual: click/select, drag/move, edit and see the result immediately, Backspace/Delete scene-local removable objects, and Play without leaving the editor stuck in a stale state.

**Approved branch:** `feature/vn-scene-composer` only.

**Continuation baseline:** product checkpoint `3056a3ca6aa887c2bc8fcbc5d817c4368ac71e2d` / tree `ae3c07401602e9d63589886038ed7aad25e2dd61`. A directly attributable descendant may contain RED tests for this continuation. Never reset/repoint the branch to discard them.

## Invariants

- No new branch/ref/tag, reset, force-push, integration, production mutation, Yarn write, or ApplyToProduction.
- Preserve the completed UX-A→H, SC-A→SC-I and visual-polish architecture unless a focused manual-stabilization regression proves a change is required.
- For each behavioral slice: inspect → source-map/reproduce → focused behavioral RED → validate RED → minimal GREEN → focused GREEN → relevant regressions → checkpoint.
- Compile/reflection/fixture/filter failures are not valid RED evidence.
- Automated proof does not certify drag feel, animation smoothness, real-video startup feel, or panel visual quality; those remain explicit manual-acceptance gates.

## M-A — Editor state and direct manipulation

Priority: explain and fix why normal authoring can stop reacting after playback/certain actions.

1. Verify activation state and hidden legacy comparison state; Scene Composer must always author the Current preview.
2. Trace Play Scene, Pause, natural completion, Restart, Previous, Next and any authoring mutation after playback. A stale playback `CurrentFrame` must not make the preview/input path non-authoring after playback has stopped or the user edits.
3. Make ordinary preview hit-testing/select/drag available for canonical directly editable scene objects without requiring Advanced layout mode, respecting the existing ownership model.
4. Implement Backspace/Delete only for removable scene-local objects/overrides and preserve Undo. Structural/default assets must be reverted/disabled rather than globally destroyed.
5. Ensure text style, character transform, panel transform and other canonical mutations immediately affect the authoring preview.
6. Focused tests first; manual acceptance remains required for actual pointer/drag feel in Unity.

## M-B — Clean scene start

Remove legacy hidden base-art fallback for empty scenes, make no-media scenes black, and separate isolated `Проиграть сцену` semantics from multi-scene source/target transitions so selected-scene preview does not flash previous art.

## M-C — Video first-play latency

Trace authoring versus playback VideoPlayer preparation and the observed ~30-second first-play wait. Prefer reuse/promotion/prewarm consistent with ownership; keep a valid poster/frame and responsive `Подготовка видео…` state. Verify against the supplied real recording and known local video during manual acceptance.

## M-D — Animation usability

Trace `SceneElapsedSeconds` through normalized progress and transition sampling. Prove that authored durations correspond to real preview time, remove unintended coarse/snap behavior, widen practical duration entry, and provide simple per-effect preview where architecture supports it. Manual smoothness review is required.

## M-E — UI simplification

Demote technical trees, typography test, raw presets/JSON and exact-engine parameters into one clearly secondary Advanced area without deleting capability.

## M-F — Default panel and custom elements

Integrate the supplied dialogue-panel asset only from an actual binary asset, preserving name/text/control hit regions and direct editing. Add the simplest canonical scene-local custom visual-element path; no new graph/prefab/asset-database architecture.

## M-G — Screenshot-driven polish

Only after functional acceptance: improve tiny section/transport controls, header density, scene-card readability/selection and local spacing. Preserve the three-column layout and dominant center preview.

## Phase report format

For every phase report: recovered HEAD/TREE; phase; RCA (symptom/source/root cause/proof); RED run/test/message/expected/observed/valid; GREEN commit/tests/result; user-visible result; manual acceptance required/performed; remaining problem; ref safety; production/Yarn/integration status.

## Current execution gate

Begin with M-A only. Do not start panel cosmetics, visual spacing, video latency or animation timing until the M-A state/input failure has a focused behavioral explanation and fix.