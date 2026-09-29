# ROKAS Messages System — Implementation Plan

Date: 2026-09-08
Branch: `feature/messages-system`
Baseline: `5e494f188b90bdb6916e8e78e35ac38509bd0bce`

## Milestone 1 — Asset intake

Files:
- `Assets/Rokas/Resources/Messages/Portraits/*`
- `Assets/Rokas/Resources/Messages/Icons/*`
- matching `.meta`

Steps:
1. Inspect all six approved references and existing `RokasAssets`/wallpaper.
2. Extract only clean reusable portraits/icons; no baked labels or full screenshots.
3. Configure UI texture import metas for Sprite/UI, mipmaps off, alpha where applicable.
4. Validate dimensions/PNG signatures and exact resource names.
5. `git diff --check` equivalent on staged text/meta changes; checkpoint `wip: checkpoint messages assets`.

## Milestone 2 — Yarn package + domain/dialogue foundation

Files:
- `Packages/manifest.json`
- `Assets/Rokas/Scripts/Core/State.cs`
- `Assets/Rokas/Scripts/Core/SaveJsonShape.cs`
- `Assets/Rokas/Scripts/Core/SaveStore.cs`
- `Assets/Rokas/Scripts/Core/GameSession.cs`
- new core Messages domain/service files
- `Assets/Rokas/Dialogues/Messages/*` (actual path verified during implementation)
- EditMode tests

Steps:
1. Package-only checkpoint: add current Yarn Spinner 3.x dependency, no unrelated package changes.
2. RED: add focused MessageService/save tests for unread, mark-read, duplicate event IDs, choice checkpoint, search, attachment hooks and gated events.
3. Implement serializable Messages save state and migrate old saves safely; bump save version only as required.
4. Implement `MessageService` and expose it through `GameSession` without a second event/gameplay subsystem.
5. Add Yarn project/Kaito authored script using current Yarn 3 APIs and custom adapter boundary.
6. Add Yarn asset/node/choice validation tests.
7. Checkpoint `wip: checkpoint messages domain` / `wip: checkpoint messages dialogue` as milestones become independently coherent.

## Milestone 3 — Messages shell UI

Files:
- new `LaptopMessagesView` and small presentation helpers
- `LaptopView.cs`
- `Rokas.Presentation.asmdef`
- PlayMode tests as needed

Steps:
1. RED: add smoke expectation that Laptop → Messages creates contact list/search/header/conversation root.
2. Route section 6 to custom Messages view.
3. Build dark-glass two-pane layout matching reference 03, using existing laptop wallpaper.
4. Implement functional TMP search, selectable contacts, independent contact/conversation scrolling, status/unread badges.
5. Preserve existing Back/ESC/focus and global mouse audio behavior.
6. Checkpoint `wip: checkpoint messages ui`.

## Milestone 4 — Branching Kaito vertical slice

Steps:
1. RED: smoke test at least one displayed option, outgoing reply, subsequent incoming reply.
2. Implement custom Yarn presenter/adapter using current Yarn 3 presenter callbacks.
3. Kaito: several sequential authored branch points, 2–4 choices, persisted checkpoint/history.
4. Render incoming/outgoing bubbles, timestamps/date separators and auto-scroll rules.
5. Checkpoint `wip: checkpoint messages dialogue`.

## Milestone 5 — Attachments

Steps:
1. RED: coordinate action hook and contract attachment identity/state tests.
2. Render coordinate card `Восточный район, Сектор B-7` and tested domain action hook.
3. Render Guild contract card using existing `ContractDefinition.Id`; acceptance delegates to `GameSession.AcceptContract` only when valid.
4. Checkpoint `wip: checkpoint messages attachments`.

## Milestone 6 — Events, unread, notifications, persistence

Steps:
1. RED: delivery idempotency across save/load and unread persistence.
2. Wire only existing GameSession state transitions to deterministic authored message delivery.
3. Implement compact notification presenter, no per-frame spam and no redundant unread on currently open conversation.
4. Verify close/reopen and save/reload preserve history, branch checkpoint, delivered IDs and unread.
5. Checkpoint `wip: checkpoint messages persistence`.

## Milestone 7 — Other contacts

Bring Guild, Yumiko, Mika, Unknown and Merchant to usable authored states. Unknown may be genuinely gated; Merchant must not invent unsupported trade gameplay. Checkpoint after the roster is coherent.

## Milestone 8 — Polish

Verify visual hierarchy against references 1–6, crisp portrait crops, scrolling, clipping, hover/focus, subtle arrival/notification animation, keyboard navigation and zero double mouse SFX. Keep this presentation-only.

## Milestone 9 — Integration verification

1. Review complete feature-branch diff against baseline; reject unrelated changes.
2. `git diff --check`.
3. Request code review.
4. Safely integrate feature branch into current `development` without force push/rollback.
5. Use existing automatic GameCI and inspect real package resolution, compilation, EditMode, PlayMode, Yarn/Messages smoke results.
6. On any new failure: `systematic-debugging`, first root cause only, scoped fix + checkpoint + rerun.
7. Final report records exact checkpoint SHAs and final development HEAD. `LIVE VERIFIED = NO` unless a true interactive Unity runtime was available.
