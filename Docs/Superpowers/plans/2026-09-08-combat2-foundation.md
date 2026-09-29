# Combat 2.0 Foundation Implementation Plan

> Use superpowers:executing-plans with TDD and verification-before-completion. The user approved the design and execution; stop after verified feature push.

**Goal:** One playable Faceless Commuter hunt: attack, react, break Seal, trace ritual, build Resonance.
**Architecture:** Extend CombatService using existing SaveData HP, timers and RunPhase. Keep encounter-only action state on the service and reset on each entry/reload; preserve the existing save schema. MissionView owns HUD/feedback; a small pointer adapter supplies held/released/drag input. Reuse Rokas scene, art, sound, WorldEffects, and reward flow.
**Tech stack:** Plain C# domain; Unity 6000.3.19f1 uGUI/legacy input; Core console tests plus Unity NUnit EditMode/PlayMode.
**Spec:** User's approved Combat 2.0 request attached to this task. No design approval pending.

## Constraints
- Work only in D:/Rokas/combat2-foundation on codex/combat2-foundation, starting 2237ea3ed350f6b17fa08d20f66bc6f176d2e193.
- Never modify development or Messages production files, merge, add enemies, integrate food, or broaden polish.
- Preserve contract accept/portal/result/payment guards and serialized field names.
- Baseline validator: 13 pre-existing failures; compare exact failure set after edits.

## Task 1: Combat rules (RED -> GREEN)
Files: CombatService.cs, new CombatTuning.cs; GameSession.cs wrappers/reset; shared CombatFoundationCases.cs, NUnit CombatFoundationTests.cs, Core Program/csproj, existing tests for intentionally replaced auto/weak-point behavior.
- [x] RED: idle fight leaves enemy HP unchanged; .15s spam rejected; timed three-hit damage exceeds three basics; old weak-point window no longer activates.
- [x] Add typed cases for charge (tap/early/charged/perfect/overheld), .42s dodge versus .14s deflect, shared cooldown, 100 Seal depletion, pressure paused during .35s break then 3.2s ordered held trace, failure recovery, gains/cap/R activation/5.5s expiry, HP phases, retry and cancellation.
- [x] Centralize .28s attack cooldown, .48 +/- .08s perfect combo, .85s chain expiry; charge threshold .22s, charged .65-1.2s, perfect .90-1.04s; damage multipliers and Seal rewards.
- [x] Implement minimum rules, run all domain cases and existing domain suite.

## Task 2: Playable presentation (RED -> GREEN)
Files: MissionView.cs, new CombatInputSurface.cs and CombatTimingRing.cs; RokasView.cs and focused input forwarding in RokasBootstrap.cs; focused CombatFoundationPlayModeTests.cs, update combat section of FirstLoopTests.cs.
- [x] RED real EventSystem pointer down/up/drag against live combat UI: no damage on down, tap hits, held release charge, ordered ritual trace, paused pointer/keyboard guard.
- [x] Show HP+Seal, combo beat cue, charge ring with best sector, enemy countdown/phase, defense rewards, 3 connected ritual points, Resonance gauge and R activation.
- [x] Reuse hit sounds/slash/recoil/numbers/shake; add small impact spark pool, clear break/deflect/ritual emphasis and short hit stop.
- [x] Confirm pointer gestures cancel on settings, focus loss, death and scene rebuild. Avoid double attacks from button click + pointer release.

## Task 3: Fresh verification and checkpoint
- [x] Core/domain full run; Unity EditMode + PlayMode including combat cases; exact baseline validator comparison; literal git diff --check.
- [x] Review changed code/requirements, address only new failures.
- [x] Record start/RED/GREEN run IDs, timing and files in Docs/Combat2Foundation.md.
- Final handoff: Commit VERIFIED COMBAT 2.0 FOUNDATION CHECKPOINT only if requirements are verified, push feature branch, verify ls-remote exact SHA and report protected refs, including external concurrent advances.
- Mandatory post-push action: STOP for user manual inspection. No merge.

## Final execution record

All implementation and final gates completed; see Docs/Combat2Foundation.md and Docs/Verification/Combat2Foundation/results.json. Core: 23 combat scenarios plus all existing suites. EditMode: 33/33. PlayMode: 36/36. Validator: 13 existing findings, zero new. Source/test blobs are frozen at the verified state. Checkpoint/push is the final mechanical handoff, followed immediately by the mandated stop. Generated Unity state is retained locally as explicitly requested.
