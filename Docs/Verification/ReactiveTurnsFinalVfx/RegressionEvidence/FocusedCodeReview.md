# Focused code review — 2026-10-03

Review restricted to the current ReactiveTurns preview/Throw/native Ember diff from recovery HEAD 0eecaaf7. No protected Home, Workbench or VN feature changes were incorporated.

Independent read-only review found no actionable defects in:

- ClickReactiveAction pending-confirm guard and cancellation of both preview flags.
- PreviewConfirmed requiring CameraAtHome, frame-gap and pause gates before Core commit.
- Return completion requiring motion completion, exact slot/facing, idle settlement and announcement gates.
- Throw release ActionId/hunter.Released protection, one contact even for zero damage, effect retirement and cancellation without fake contact.
- TorsoPoint live Spine2/Spine1/Spine goal sampling; front-Z exposure unchanged for existing melee/Block.
- Core-to-presentation audio id binding and PlayOnce semantic layers.
- Ember visibility loop age, owned mesh/material disposal, shared imported texture ownership, arena UI material/RenderTexture disposal.
- New switch/cancel/retry assertions retaining AP/HP/revision/weapon-parent and one projectile checks.

Root review also inspected shared GameSession, ReactiveEightEnemyDefinitions, MissionView, Core test registration and the narrowly scoped Ember PNG validator changes. Previous eight-enemy content catalogs remain loadable; Throw adds a distinct skill without rewriting Combat Core.

Verification is separate from this review: final relevant PlayMode XML contains 78/78 passed, 0 failed, 0 skipped; Unity process exited 0. No broad EditMode green or integrated canonical checkpoint claim is made.
