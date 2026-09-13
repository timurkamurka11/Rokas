# Combat 3 Phase 0 implementation gates

Execute the accepted Combat 3 study and Phase 0 attachment; this is an implementation checklist, not a new design. Worktree and authoritative source paths are recorded in `Docs/ai-handoffs/combat3-phase0-current.md`.

Architecture: extend existing `CombatService` with a composed, exclusive lane encounter driver. `SaveData` retains HP, run identity and result; `CombatService` retains Seal/Resonance and damage events. `GameSession` provides the separate review portal entry and normal lifecycle. Presentation uses existing MissionView/Bootstrap, uGUI and Built-in proxy geometry. Combat 2 APIs retain their behavior outside the gated encounter.

## Gate 0A — heavy strike, movement, counter

- [x] Baseline Core, EditMode, PlayMode and asset findings recorded.
- [x] RED: shared `Combat3Phase0Cases.cs` in portable Core and EditMode exercises gated entry, lane movement/bounds/queue, telegraph/contact/recovery/window, fresh counter edges, HP ownership, pause/backlog and lifecycle. Confirm expected failure before implementation.
- [x] GREEN: `Combat3Encounter.cs` owns only lane/timing/attack/input state. Fixed 1/60 clock, 0.12 movement, 0.18 held repeat, queue one, simultaneous neutral, swept contacts. Heavy family only, unique instance IDs and resolved/cleanup states. Recovery 0.25 and counter window 1.1. Extend CombatService shared damage/Seal/Resonance through narrow internal operations, no C2 auto tick alongside C3.
- [x] Add opt-in review portal button (Editor/development only), proxy arena/player/enemy, minimal state HUD, and input using existing Bootstrap/MissionView; no alternate bootstrap or wallet.
- [x] Test early/held/late/once counter; successful counter uses contract click damage × weapon multiplier × 1.8. Seal −25, Resonance +10; player damage Resonance −10, immunity 0.65. Pending Seal bonus and R reservation use existing owners, additive multipliers. No drag ritual.
- [x] Core/Unity regressions and real PlayMode visuals prove movement avoids heavy, staying damages player but still opens counter, fresh LMB damages enemy, idle never damages enemy. Record checkpoint before 0B.

## Gate 0B — low wave and dodge

- [x] RED: all-lane collision; RMB stationary/directional adjacent dodge; 0.18 immunity, shared cooldown 0.55; first 0.08 actual prevented contact only grants Perfect (once/dodge and attack), empty dodge grants nothing.
- [x] GREEN: add low wave family and dodge to existing driver, proxy/HUD feedback. Perfect Seal −8, Resonance +8. No damage evasion on ordinary lane steps.
- [x] Verify domain boundary tests, C2 regressions and PlayMode proof. Record checkpoint before 0C.

## Gate 0C — deflectable projectile

- [x] RED: Space 0.14 active, shared cooldown 0.55, early/late/wrong family fail; resolves only colliding instance, siblings survive; lane avoidance always possible.
- [x] GREEN: add marked projectile family and deflect. Seal −12, Resonance +12; return visual never adds damage.
- [x] Verify tests and PlayMode avoidance/deflect proof. Record checkpoint before 0D.

## Gate 0D — locked ink and full Phase 0 verification

- [ ] RED: delayed target lock, no retarget after lock, fixed-lane explosion, residual explicit lifetime; no duplicate hit/result/payment. Test reload restart with same run identity, old/future save safety, pause/focus/read delay, cleanup/reentry, death/victory.
- [ ] GREEN: fourth and final family; finish explicit 6–10 second patterns and presentation.
- [ ] Verify complete Core, EditMode, PlayMode suites including Home/Messages/C2/save/payment, all requested runtime cases and actual rendered captures. Asset validator compared by findings with baseline; relevant Unity logs; literal `git diff --check` exit 0 and empty output; scoped code review.
- [ ] Create verified feature checkpoint only after all gates are green. Preserve evidence and handoff. Stop for user's integration choice; no automatic merge or push. Canonical manual QA follows only after authorized, freshly verified unified integration.

Validation tooling: adapt existing external runner under `D:/Rokas/combat3-phase0-evidence`, with unique evidence filenames. Graphics required for PlayMode/runtime captures. Never rerun an active verification or overwrite its evidence. Each test exercises real domain/view behavior, not source text or a duplicated implementation.
