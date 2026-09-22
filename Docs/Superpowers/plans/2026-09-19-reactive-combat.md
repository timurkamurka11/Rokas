# ROKAS Reactive Turns Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task after explicit user approval. Steps use checkbox (`- [ ]`) syntax for tracking. Исследовательский запрос не разрешает начать реализацию.

**Goal:** Встроить реактивный пошаговый режим Пропасти в существующий ROKAS, доказав честные тайминги и целостность Home→Combat→Payment→Save.

**Architecture:** Pure C# Core владеет очередью, временем, outcomes и checkpoint. Unity presentation отображает typed events; отдельный input adapter передаёт исходные timestamps. Legacy Combat2 и ReactiveTurns сосуществуют через явный mode router.

**Tech Stack:** Unity6000.3.19f1; C#; существующие UGUI/Core/Presentation assemblies; .NET8 console tests; Unity EditMode/PlayMode. Предлагаемый новый пакет — Input System1.20.0 только после проверки совместимости; Cinemachine/NavMesh/DOTS не требуются.

**Spec:** [ROKAS Reactive Combat Research, 66 разделов](D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/ROKAS-Reactive-Combat-Research.md), [нормативные правила R01–R15](D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/Combat-Rule-Contract.md), [численный audit](D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/Numerical-Consistency-Audit.md). Финальная сверка20.09.2026; исходное имя файла сохранено.

## Global Constraints

- Пользователь запросил сначала исследование и отчёт; **продуктовый код пока не изменять**. Этот план — будущая работа, все checkboxes намеренно пустые.
- Canonical folder: `D:/Rokas/Rokas`; Unity `6000.3.19f1`; integration branch `integration/rokas-unified`.
- Не обновлять development, не merge PR#4, не force-push, не удалять worktrees/evidence.
- Current research base: `fd5d99afd0ecb04c20edfdb8ac5916a4be41b139`. При начале реализации заново восстановить реальные refs и local changes.
- Lane Combat3 verified checkpoint `b9caeaffc161a0c5105246c1d81f3f736693865b` не является reactive spec; более поздние dirty changes не одобрены к включению.
- Сохранить Home2.5D/weather, Live Messenger reactions/typing/unread/audio, manual Combat2, save/payment lifecycle. Не восстанавливать autoattack.
- 1 Hunter; обычно1–3 active enemies, максимум4 после проверки; boss+0–2adds;20 означает total waves.
- AP0–6; +1 natural PlayerTurnStart; Basic+2; defense cap2 AP/player interval; Seal defense cap40/action.
- Pressure budget:2 enemy actions/6 hits/8 authored seconds между player commands.
- Standard windows: Dodge−240…+60ms; Parry−110…+45ms; Perfect−40…+25ms; inclusive.
- Первый slice содержит duel, Basic, Seal Strike, Defend, triple, Dodge/Parry/Perfect, counter, Break, save/victory/payment.
- Fresh asset audit33 findings против historical13:20 additions,0 removals. Не считать33 утверждённым новым baseline.
- Каждый milestone для пользовательской QA проходит **полную** интеграцию и проверку по AGENTS.md; feature-only green не является handoff.

## Review Focus

1. Input callback доставлен позже кадра контакта: timestamp-based outcome сохраняется, нет двойного урона — Tasks2/4.
2. Старый v1 save в Combat/Payment: текущий run/неоплаченная награда не теряются — Task6.
3. Menu click одновременно приходит из legacy UI и нового ActionMap: одна команда, никакой случайный Parry — Tasks2/7.
4. Break/lethal/phase/summon в одном settlement: terminal outcome и очередь определяются один раз — Tasks5/8/9/10.
5. Crash/IO error на final result или payment: нет выданной до сохранения/повторной награды — Tasks6/7/13.

## Порядок и размер milestones

```text
T1 Definitions ─┬─ T2 Clock/Input ─┐
               └─ T3 Loop/Queue ─┼─ T4 Defense ─ T5 Offense/Break
                                 └─ T6 Save/Router ──────────────┐
T5 + T6 + minimal assets ─ T7 Integrated Duel ─ T8 Multi ─ T9 Waves
T9 ─ T10 Boss ─ T11 Polish/Authoring ─ T12 Balance ─ T13 Rollout
```

T6 может разрабатываться послеT1 параллельно логически, но отдельные агенты этим планом не назначаются. Реальный порядок выбирается при исполнении с учётом shared files. Документ не требует распараллеливания.

Milestone A: T1–T7, duel в каноническом проекте. Milestone B: T8–T9,8-enemy portal. Milestone C: T10–T12, boss/content/20-total experiment. Milestone D: T13, выбранный rollout. После каждого согласованного milestone — общий integration gate в конце плана и остановка на manual QA.

## Карта файлов

Существующие точки изменения:

| Файл | Предполагаемое изменение |
|---|---|
| [GameSession.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Core/GameSession.cs) | Mode/router и stable snapshot adapter; сохранить Messages и lifecycle |
| [State.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Core/State.cs) | v2 migration contract; optional battle checkpoint |
| [ContractDefinition.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Core/ContractDefinition.cs) | combatMode/encounterId с Legacy default |
| [SaveStore.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Core/SaveStore.cs) | Version-aware migration до текущей validation |
| [SaveJsonShape.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Core/SaveJsonShape.cs) | Required shape по версии, не один новый обязательный объект для всех |
| [EconomyService.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Core/EconomyService.cs) | Сохранить формулы; вызывать на staged clone |
| [FoodService.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Core/FoodService.cs) | Mode-specific effect GreenTea |
| [RokasBootstrap.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Presentation/RokasBootstrap.cs:415) | Raw gap/input ownership/clock mapping до clamp |
| [MissionView.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Presentation/MissionView.cs) | Route legacy vs reactive presenter |
| [RokasView.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Presentation/RokasView.cs) | Paused/hitstop ownership, disposal, reactive event routing |
| [RokasAudio.cs](D:/Rokas/Rokas/Assets/Rokas/Scripts/Presentation/RokasAudio.cs) | Cue bus/schedule cancellation/ducking |
| [Rokas.Core.Tests.csproj](D:/Rokas/Rokas/Tests/Core/Rokas.Core.Tests.csproj:24) | Recursive Core include и новые shared cases |
| [Program.cs](D:/Rokas/Rokas/Tests/Core/Program.cs) | Запустить новые suites, не заменить существующие |
| Packages/manifest.json + packages-lock.json | Только выбранная input dependency, в отдельном обозримом diff |

Новые директории будущей реализации:

- `D:/Rokas/Rokas/Assets/Rokas/Scripts/Core/ReactiveTurns/`: CombatDefinitions.cs, ReactiveCombatState.cs, CombatClock.cs, TurnScheduler.cs, DefenseResolver.cs, DamageResolver.cs, EnemyPolicy.cs, ReactiveCombatSession.cs, CombatRuntimeRouter.cs, CombatCheckpointCodec.cs, RunResultCommitter.cs.
- `D:/Rokas/Rokas/Assets/Rokas/Scripts/Presentation/ReactiveTurns/`: ReactiveCombatInput.cs, ReactiveBattlePresenter.cs, ReactiveCombatHud.cs, CombatCameraRig.cs, CombatCuePlayer.cs, CombatDebugOverlay.cs.
- `D:/Rokas/Rokas/Assets/Rokas/Scripts/Presentation/ReactiveTurns/Authoring/`: CombatActorAsset.cs, AttackSequenceAsset.cs, SkillAsset.cs, WaveAsset.cs, PortalEncounterAsset.cs.
- `D:/Rokas/Rokas/Assets/Rokas/Scripts/Editor/ReactiveTurns/`: CombatContentValidator.cs, AttackSequencePreview.cs, CombatExport.cs.
- `D:/Rokas/Rokas/Assets/Rokas/Resources/Combat/ReactiveTurns/`: только утверждённые SO/fixtures/presentation refs, каждому Unity asset соответствующий .meta.
- `D:/Rokas/Rokas/Assets/Rokas/Tests/EditMode/ReactiveTurns/`: deterministic shared cases, authoring/save validation.
- `D:/Rokas/Rokas/Assets/Rokas/Tests/PlayMode/ReactiveTurns/`: input/runtime/flow/visual/lifecycle tests.
- `D:/Rokas/Rokas/Assets/Rokas/Tests/Scenes/ReactiveCombatSandbox.unity`: developer sandbox, не новый canonical QA project.

Проверенная граница сборок: [Rokas.Editor.asmdef](D:/Rokas/Rokas/Assets/Rokas/Scripts/Editor/Rokas.Editor.asmdef:1) ссылается на Core/Presentation. Поэтому новые SO располагаются внутри Presentation/ReactiveTurns/Authoring, а не в default Assembly-CSharp. Editor preview/export остаётся в Editor assembly. Для Editor-tool tests добавить Rokas.Editor в существующий EditMode test asmdef; runtime tests не должны ссылаться на Editor.

Не создавать все пустые классы заранее: файл появляется в задаче, где получает реальную ответственность и тест.

## Контракты между задачами

Имена ниже — предлагаемая API спецификация, не уже существующие symbols. Сохранить согласованность при реализации либо обновить spec/всех consumers одним change.

```csharp
// Definitions and state, T1.
enum CombatMode { Legacy, ReactiveTurns }
enum DefenseKind { Dodge, Parry }
enum DefenseOutcome { Ignored, Fail, Dodge, Parry, Perfect }
enum CombatOutcome { Victory, Defeat }
enum CommandKind { Basic, Skill, Defend, Retreat }

// IDs are stable strings; runtime counters/times are long.
ValidationResult CombatDefinitions.Validate();
ReactiveCombatSession(CombatDefinitions definitions, BattleCheckpoint checkpoint);
BattleCheckpoint ReactiveCombatSession.GetStableCheckpoint();
CommandResult ReactiveCombatSession.SubmitCommand(CommandIntent command);
void ReactiveCombatSession.SubmitDefense(DefenseIntent input);
CombatStep ReactiveCombatSession.Advance(long combatUs);
void ReactiveCombatSession.Suspend(string reason);
void ReactiveCombatSession.Resume(long newEpoch);

// Pure services used by session, not UI.
DefenseOutcome DefenseResolver.Classify(DefenseKind kind, long errorUs,
                                        DefenseWindowProfile profile);
TurnDecision TurnScheduler.SelectNext(QueueState state, CombatDefinitions defs);
TurnForecast TurnScheduler.Preview(QueueState state, CommandIntent candidate,
                                   CombatDefinitions defs, int count);
long CombatClock.MapInput(long deviceUs, long epoch);
DamageResult DamageResolver.Resolve(HitRequest hit, ActorState target);
```

Value types: `ValidationResult` errors/warnings; `CommandIntent` commandId/revision/kind/skillId/targetIds; `DefenseIntent` inputId/epoch/kind/combatUs; `CombatStep` orderedEvents/stateRevision/terminalResult; `CommandResult` accepted/reason/newRevision; `BattleCheckpoint` поля §42/48; `QueueState` currentTick/actor entries/budget; `TurnDecision` actorId/dispatchTick/forcedResponse; `TurnForecast` slots/uncertainty; `HitRequest` eventId/sourceId/targetId/power/modifiers/outcome; `DamageResult` applied/HPdelta/SealDelta/APdelta/death/break.

Псевдокод/проверки ниже задают алгоритмы и ожидаемые значения. Это содержимое плана, а не требование генерировать весь production код до review.

## Task 1 — FOUNDATION: валидируемые definitions и идентичность

**Goal/Systems:** immutable combat data и типы state/events без Unity dependencies.
**Dependencies:** approved direction и известный integration base.
**Files:** новые CombatDefinitions.cs, ReactiveCombatState.cs; изменить ContractDefinition.cs; csproj recursive include; новые EditMode/ReactiveTurns/DefinitionCases.cs и standalone registration.

**Consumes:** contract/weapon/food values через adapter. **Produces:** CombatDefinitions.Validate, все DTO выше, Legacy default.

- [ ] Сохранить тестовые v1 fixtures и новый definition fixture triple из spec§42.
- [ ] Написать failing cases: duplicate actor ID; missing sequence; negative AP; Perfect за Parry; hit gap349999us; wave5 actors приcap4; duration больше budget; неизвестный mode должен безопасно отклоняться.
- [ ] Запустить Core host и подтвердить конкретные failures новых cases при зелёных legacy cases.
- [ ] Реализовать immutable normalized DTO; валидировать все refs одним проходом dictionaries и все временные intervals как long.
- [ ] Подключить новые shared cases в console host и Unity wrapper; проверить, что файл из вложенной Core/ReactiveTurns действительно попал в compilation.
- [ ] Запустить Core+EditMode targeted, сохранить результаты; зафиксировать только reviewed files.

Алгоритм проверки окон:

```text
require 0 <= PerfectEarly <= ParryEarly <= AcquireEarly
require 0 <= PerfectLate <= ParryLate <= AcquireLate
require 0 <= DodgeEarly <= AcquireEarly
require 0 <= DodgeLate <= AcquireLate
require unique(hit.id) and strictlyIncreasing(hit.impactUs)
require lastLateClosure <= durationUs
```

**Risk:** DTO расходится с SO; пока SO не создавать, fixture — единственный вход.
**DoD:** плохие данные отвергаются с точным asset/id/field; старый контракт без mode работает как Legacy.

## Task 2 — FOUNDATION: clock и input adapter

**Goal/Systems:** исходный timestamp, pause segments, input epoch/release latch; scoped Input System.
**Dependencies:** T1.
**Files:** CombatClock.cs, ReactiveCombatInput.cs; Packages files; Rokas.Presentation.asmdef для ссылки выбранного Input System; RokasBootstrap.Update/focus callbacks; тесты ClockCases.cs, ReactiveInputPlayModeTests.cs.

**Consumes:** device event time, focus/pause/raw delta. **Produces:** DefenseIntent, CommandIntent; MapInput.

- [ ] Восстановить package compatibility6000.3/1.20.0; проверить existing StandaloneInputModule, activeInputHandler0 и legacy routes до установки.
- [ ] Добавить clock tests для pause1–3s: input wall3.2s преобразуется combat1.2s; input внутри pause отвергается; speed0.75 segment сохраняет непрерывность.
- [ ] Добавить PlayMode test двух press/release между frames, held-key resume, mouse+UGUI double route и remap.
- [ ] Подключить package минимальным manifest diff; выбрать Both только если Home/UGUI остаётся на old input. Новый ActionMap включён лишь для ReactiveTurns.
- [ ] В Bootstrap читать raw gap до существующего clamp. При gap>100ms Suspend; не Advance на невидимую часть.
- [ ] Сначала собрать inputs, затем Advance. Использовать event timestamp, а не callback time.
- [ ] Прогнать30/60/120 и input-device capture; измерить deliveryLag. Если реальные events позднее allowance, уменьшить suspension threshold/изменить watermark, а не считать их честными misses.
- [ ] Проверить Home click, laptop, Messenger, pause, combat legacy; commit только после совместимости.

Mapping:

```text
segment = segmentContaining(deviceUs)
if segment.paused or epoch != currentEpoch: reject
combatUs = segment.combatStartUs
           + round((deviceUs - segment.deviceStartUs) * segment.speed)
```

**Risk:** Both создаёт два одинаковых action. **DoD:** один input owner, log содержит original timestamps, нет auto defense на resume.

## Task 3 — CORE LOOP: state machine и CTB forecast

**Goal/Systems:** deterministic dispatch, commit/revision, pressure budget и forecast.
**Dependencies:** T1; T2 для realtime, но pure queue tests независимы.
**Files:** ReactiveCombatSession.cs, TurnScheduler.cs, EnemyPolicy.cs; StateMachineCases.cs, TurnSchedulerCases.cs.

**Consumes:** DTO, CommandIntent, queue. **Produces:** CombatStep/events/TurnForecast, stable state.

- [ ] Ввести тесты переходов Preparing→Command→Execution→Settlement; invalid command оставляет state/AP/revision неизменными.
- [ ] Queue cases: P0 Basic→E60→E90→P100; P0 Heavy→E60→E90→forcedP120; 7-й defensive hit никогда не dispatch до нового P; action duration over8s отклоняется вT1.
- [ ] Equal ticks: порядок поordinal/id; dead actor исчезает; new summon no earlierthan now+60; long arithmetic не переполняется на длинном replay.
- [ ] Реализовать один SelectNext для real dispatch и cloned forecast. Forced dispatch не уменьшает nowTick.
- [ ] AI выбирает attack только в turn start; зафиксировать seed, intent, cooldown; preview неизвестного intent показывает неопределённость.
- [ ] На1000 fixed seeds сравнить replay hash и forecast first slot с реальным следующим actor; проверить отсутствие starvation.
- [ ] Подтвердить, что пауза в меню10min не меняет очередь и не вызывает auto damage.

Core dispatch:

```text
candidate = minAlive(nextTick, spawnOrdinal, actorId)
if candidate.enemy && wouldExceedBudget(candidate.attack):
    candidate = Hunter
    dispatch = max(now, min(Hunter.due, earliestEnemy.due))
else:
    dispatch = max(now, candidate.due)
nextDue[candidate] = dispatch + ceil(delay * 100 / speed)
```

**Risk:** forecast и actual queue дублируют logic. **DoD:** один algorithm, deterministic stable ties, публичный forced-response icon data.

## Task 4 — DEFENSE: окна, first attempt и multi-hit

**Goal/Systems:** outcomes для каждого hit, поздний input, caps и cancellation.
**Dependencies:** T2/T3.
**Files:** DefenseResolver.cs; ReactiveCombatSession; DefenseWindowCases.cs, MultiHitCases.cs; input runtime tests.

**Consumes:** InputIntent epoch/time + authored hits. **Produces:** exactly-once HitResolved proposal; AP/Seal reward request дляT5.

- [ ] Написать точные cases для−110001/−110000/−40001/−40000/+25000/+25001/+45000/+45001 и Dodge bounds.
- [ ] Проверить EarlyFail первого нажатия с последующим valid press; две кнопки одного timestamp дают Dodge priority; holding не повторяет attempts.
- [ ] Реализовать pending hit ledger: одна первая попытка; уже resolved ID отвергает повтор; следующий hit принимает press только после previous impact/release.
- [ ] Finalize unresolved fail только после late margin и delivery watermark. Не наносить damage наT до закрытия late window.
- [ ] Вести raw contact neutral event отдельно от outcome feedback. Ввод в allowed late interval не вызывает видимого HP rollback.
- [ ] Replay triple с Parry/Fail/Perfect: ровно8HP damage,2AP,32Seal, counter eligibility true. Focus послеh1 не повторяет его награду.
- [ ] Прогнать identical timestamps через30/60/120 frame schedules; затем настоящий runtime input delivery. Arithmetic equality не выдавать за проверку Unity adapter.

Пример обязательного unit test на pure API:

```csharp
Assert.AreEqual(DefenseOutcome.Parry,
    DefenseResolver.Classify(DefenseKind.Parry, -110000, standard));
Assert.AreEqual(DefenseOutcome.Fail,
    DefenseResolver.Classify(DefenseKind.Parry, -110001, standard));
Assert.AreEqual(DefenseOutcome.Perfect,
    DefenseResolver.Classify(DefenseKind.Parry, 25000, standard));
Assert.AreEqual(DefenseOutcome.Parry,
    DefenseResolver.Classify(DefenseKind.Parry, 25001, standard));
```

`standard` — DefenseWindowProfile с числами Global Constraints, созданный в fixture T1.
**Risk:** ждать outcome без нейтрального contact выглядит как lag; проверить на runtime. **DoD:** правильные boundaries, независимые hits, измеренная доставка, no duplicate reward.

## Task 5 — OFFENSE: AP, damage, Seal, counter, statuses

**Goal/Systems:** полный headless duel.
**Dependencies:** T3/T4.
**Files:** DamageResolver.cs, ReactiveCombatSession.cs, definitions/effects; DamageCases.cs, ResourceCases.cs, BreakCounterCases.cs, StatusCases.cs.

**Consumes:** committed action, defense outcomes. **Produces:** immutable ordered effects и terminal candidate.

- [ ] Damage cases:20vsDefense25=16;48×0.8=38;68×0.8×1.15=63; blocked0; no per-factor rounding.
- [ ] AP cases: initial3→firststart4; skill3 leaves1; defense cap2; nextstart4; Basic+2 один раз; invalid command no charge.
- [ ] Implement Basic/SealStrike/Defend before optional skills. Commit AP once; revision protects double submit.
- [ ] Implement Seal states intact/broken/refractory; one skip token; next offensive command vulnerability; current triggering action не расходует token; boss2 completed natural actions immunity.
- [ ] Counter: eligible2 parries+finalPerfect, freshConfirm600ms, no AP/Seal/queue tick, one token; dead attacker cancels.
- [ ] Для первого slice ограничить offensive content Basic/Seal Strike/Defend. Sweep/Anchor добавляются в T8, Heavy — в T10; не расширять ранний patch всем каталогом.
- [ ] Проверить durations Broken/Defend, отсутствие status tick от counter и same-event double KO=Defeat. Burn/Weaken/Haste/Exposed/Barrier/Stun — последующий content scope T11, не обязательный duel.
- [ ] Воспроизвести ledger spec§58: enemy180→132→107→47→27→0, Hunter92,4 player commands. Любое расхождение исправить в resolver/spec, не подгонять testexpected к багу.
- [ ] Core/targeted EditMode green; сохранить deterministic replay evidence.

**Risk:** Break/AP/counter положительная обратная связь. **DoD:** caps/refractory проверены; бесплатный Basic остаётся; terminal result только один.

## Task 6 — MIGRATION foundations: сохранения, retry и router

**Goal/Systems:** v2 persistence и безопасное сосуществование режимов до подключения нового UI.
**Dependencies:** T1/T3; полный damage runtime не нужен для migration fixtures.
**Files:** CombatRuntimeRouter.cs, CombatCheckpointCodec.cs, RunResultCommitter.cs; State.cs, SaveStore.cs, SaveJsonShape.cs, GameSession.cs; SaveMigrationCases.cs, RetryRewardCases.cs, UnityReactiveSaveTests.cs.

**Consumes:** current SaveData/SaveStore, BattleCheckpoint, immutable CombatResult.
**Produces:** `CombatCheckpointCodec.MigrateV1(SaveData legacy)`; `RunResultCommitter.TryCommit(CombatResult result)`; `TryClaimPayment(string economicRunId)`; `RetryEncounter()`; `RetryWave()`. Результат каждой mutation: accepted/persisted/reason; UI success возможен только при persisted=true.

- [ ] Добавить v1 fixtures: Home, Accepted, Portal, Combat, Sealed, Payment; их currency/messages/food/runSequence должны сохраняться побайтно по значениям после migration.
- [ ] Проверить v1 Combat остаётся Legacy; новый default не переводит незавершённый run.
- [ ] Добавить fake file writer fault points: до temp write, после temp flush до replace, после replace до UI publish. После reload допустимо только целое старое или целое новое состояние, без смешанной выплаты.
- [ ] Ввести economicRunId=(contractId,runSequence), attemptId, event IDs; terminal immutable на attempt, entitlement на run.
- [ ] Добавить stable pre-action/settlement snapshot, wave-entry snapshot и seed; autosave не пишет transient held keys/Animator time.
- [ ] Implement retry согласно R13: same seed, new attemptId, no new food consume, no inventory rollback, no AP/rest accumulation.
- [ ] Payment candidate собирать из актуального profile + semantic delta; сохранить currency/phase/persistent guild message/first-clear ledger одной записью. Changed/unread, появившиеся после начала action, не затирать старым clone.
- [ ] Prove: win→save blocked→retry save→ReturnHome→claim double click→reload даёт ровно1800yen/10rep/3ash; ни один промежуточный wave/retry их не выдаёт.
- [ ] Prove: rare first-clear rewardId повторно не выдаётся новому run; обычная repeatable оплата новому Accept сохраняется.
- [ ] Прогнать Core, EditMode serialization и существующие UnitySave/FirstLoop/Yumiko/LiveMessenger cases.

Normative transaction:

```text
candidate = clone(latestProfile)
require expectedBattleRevision == latestProfile.battleRevision
applySemanticDeltaOnce(candidate, economicRunId, attemptId, eventId)
includePersistentMessagesAndRewardLedger(candidate)
if SaveStore.Save(candidate).success:
    publish(candidate)
else:
    keepCurrentDurableRevision()
    enterSaveBlocked(sameDelta)
```

**Risks:** future-version protection; потеря messages при stale clone; IO failure выдаёт UI успех раньше диска.
**DoD:** migration/retry/crash fixtures green; никакой выплаты по визуальному событию; Legacy first loop green.

## Task 7 — OFFENSE presentation: первый integrated duel

**Goal/Systems:** реальная видимая1v1 арена, сначала single strike, затем triple; complete portal lifecycle.
**Dependencies:** T2–T6, одобренные prototype rig/clips.
**Files:** ReactiveBattlePresenter.cs, ReactiveCombatHud.cs, CombatCameraRig.cs, CombatCuePlayer.cs; MissionView.cs, RokasView.cs, RokasBootstrap.cs, RokasAudio.cs; минимальные authoring assets/scene; ReactiveDuelPlayModeTests.cs, ReactiveFirstLoopPlayModeTests.cs.

**Consumes:** CombatStep events и read-only state. **Produces:** Unity presentation и CommandIntent; никаких setters HP в presenter.

- [ ] Сделать failing PlayMode fixture реального Bootstrap: выбрать ReactiveTurns test encounter, войти через Portal, увидеть Hunter/enemy/queue/HP/AP.
- [ ] Подключить отдельную arena camera/RT в Mission region; явно владеть resources/subscriptions. Не менять Home camera/weather.
- [ ] Single strike: visible windup/contact, Dodge/Parry, damage, player Basic и победа. Перед triple вручную проверить, что один удар читается без debug bars.
- [ ] Добавить approach/return curves и fixed contact anchor; in-place clip не управляет Core position. Basic impact20 ровно один.
- [ ] Подключить triple из T4, optional counter, Broken; contact markers сравнить с event timestamps.
- [ ] Добавить real UI pointer/keyboard/controller tests; legacy CombatInputSurface/raw RMB route выключены только для нового mode.
- [ ] Проверить actual runtime pause/focus на h2, held resume, missing clip SafeError, resize1280×720/1920×1080, muted sound.
- [ ] First loop: Home→contract→portal→duel→Sealed→Home→Payment→Claim→reload; проверить Messages/Yumiko и exactly once.
- [ ] Снять screenshots/video **того же current runtime** и записать content/build hash. Проверить visible contact, HP feedback, нет HUD overlap.
- [ ] Выполнить общий unified integration gate. Передать только canonical folder и остановиться на manual BAT A.

**Risks:** полное отсутствие готового combat rig в текущих Assets; временные кубы не доказывают читабельность production animation.
**DoD:** пользователь может сыграть одну честную дуэль, а exit/save/payment работают в общей игре.

**Manual BAT A:** прочитать single windup без подсказки; Dodge5раз; Parry5раз; ошибиться наh2 и успешно защититьh3; выиграть, получить оплату один раз, повторно открыть save. Отметить субъективное «поздно/непонятно» вместе с recording/timing log.

## Task 8 — MULTI ENEMY: targeting, AoE, queue и active slots

**Goal/Systems:** сначала3, затем4 enemies с одним активным sequence.
**Dependencies:** accepted milestoneA/T7.
**Files:** actor collection в ReactiveCombatState/Session; target rules/TurnScheduler; ReactiveBattlePresenter/Hud; MultiEnemyCases.cs, ReactiveMultiEnemyPlayModeTests.cs.

**Consumes:** active cap/actor IDs/Sweep/Anchor. **Produces:** stable target sets, projected bars, readable forecast.

- [ ] Test queue ties/death/remove/summon ID: мёртвый actor не атакует, новый ID не наследует его hit ledger.
- [ ] Test single-target cancellation before first effect refunds exactly once; after first effect no refund/no silent retarget.
- [ ] Добавить Sweep и Anchor из spec: Sweep фиксирует3targets на commit; summoned четвёртый не получает retroactive hit. Anchor имеет один Delay lock до следующего естественного действия цели. Все HP/Seal deltas адресованы actorId.
- [ ] Реализовать keyboard/D-pad stable slot order, hovered target preview, projected HP/Seal layout.
- [ ] Proof4 enemies: одно damage sequence одновременно; после максимум2actions/6hits/8s предоставлен player response.
- [ ] Проверить невозможность repeated Delay lock; forecast при Basic/Heavy/Anchor меняется согласно одному scheduler.
- [ ] На720p проверить silhouettes, target labels, enemy intent, attack origin, camera lock. Если4не читаются, content cap остаётся3 до исправления постановки.
- [ ] Core+targeted PlayMode, затем включить эти cases в full milestoneB gate.

**Risk:** active slots ошибочно превращаются в parallel attack emitters.
**DoD:** target identity не теряется;4-actor layout доказан runtime capture либо явно ограничен3.

## Task 9 — WAVES: 8 enemies и организация 20 total

**Goal/Systems:** wave director, carryover, reserves, clear/spawn/save.
**Dependencies:** T6/T8.
**Files:** WaveDefinition/Encounter definitions, session WaveTransition, checkpoint codec; WaveCases.cs, ReactiveWavePlayModeTests.cs; assets порталов на8 и20 врагов.

**Consumes:** actor spawning/death, queue, stable snapshot. **Produces:** WaveStarted/Cleared events и единственный encounter result.

- [ ] Написать cases для3+3+2=8 и3+3+4+4+3+3=20: точное общее число, max active≤cap, отсутствие повторного spawn.
- [ ] Реализовать clear только после смерти всех обязательных enemies, завершения pending summons и settlement. Промежуточная wave не переводит игру в Sealed.
- [ ] Сохранить HP/AP/queue budget. Wave transition не вызывает PlayerTurnStart и не начисляет AP.
- [ ] Добавить одну передышку после wave3 для20-total. Save/reload и assist wave retry восстанавливают точные flags/HP, не прибавляя20HP повторно.
- [ ] Реализовать короткий preview и preload следующей группы; inactive reserves остаются DTO.
- [ ] Воспроизвести policy численного аудита:8 enemies —14 commands/24 enemy actions/44 presses;20 —26/42/54. Сначала сравнить gameplay ledger, затем отдельно длительность представления.
- [ ] PlayMode: выход на последнем ударе wave1, crash до/после wave save, Continue, pending summon до clear, отсутствие payment до финала.
- [ ] Полный unified gate для8-enemy milestone B; manual BAT B. Портал20-total остаётся development-only до fatigue gate T12.

**DoD:** один encounter из нескольких волн без повторного AP/heal/reward; новые due не создают мгновенную атаку после spawn.
**Manual BAT B:** пройти8-enemy портал, выйти и продолжить в wave2, завершить и получить одну оплату; проверить читаемость очереди и наличие решений между сериями.

## Task 10 — BOSS: phases, add, ultimate и Break protection

**Goal/Systems:** boss использует общие правила и явные исключения данных.
**Dependencies:** T5/T9.
**Files:** EnemyPolicy phase data, boss definitions/sequence assets, session settlement; BossPhaseCases.cs, ReactiveBossPlayModeTests.cs.

**Consumes:** AP/Seal/queue/waves/counter. **Produces:** phase changed, preparation intent, add; budget остаётся общим.

- [ ] Cases:480HP пересекает312 → phase2 один раз; пересекает144 → phase3 один раз; один hit пересёк оба порога → один переход в достигнутую фазу, без лечения.
- [ ] Add24HP получает due=phaseTransitionTick+180. Проверить cap boss+2 и отсутствие атаки во время cinematic.
- [ ] Ultimate имеет отдельное preparation action; четыре contacts на0.9/1.45/2.0/2.7s с damage10/10/10/14. Полная duration4s включает late closure и counter. Превышение budget отвергает validator.
- [ ] Break до ultimate commit отменяет preparation; после commit отменяет только unresolved suffix на разрешённой границе. Refractory завершается после двух исполненных естественных действий босса.
- [ ] Добавить Heavy: cost5, damage68, Seal50, delay140; optional timing×1.15, промах оставляет базовый damage.
- [ ] Golden scenario §60:10 commands,20 defensive presses,5 counters,480 effective boss damage,24 damage add, Hunter87HP, finalAP3.
- [ ] Проверить lethal+Break+threshold одного hit: финальный counter kill не создаёт фазу или add после Victory.
- [ ] PlayMode: камера зафиксирована во время ultimate; heavy final отличается позой/формой/звуком; Defend работает без обязательного Perfect.
- [ ] Core и boss tests, затем actual runtime capture с AP/queue/intents. Включить suite в полный gate milestone C.

**Risk:** presentation незаметно меняет initiative ради катсцены.
**DoD:** общие resolver и budget, правила задаются данными; короткий уверенный маршрут70–90s признаётся допустимым.

## Task 11 — POLISH: authoring, animation, feedback и accessibility

**Goal/Systems:** новый паттерн создаётся без копирования логики; feedback помогает читать timing.
**Dependencies:** T7–T10; минимальные cues уже обязательны в T7.
**Files:** AttackSequenceAsset/SkillAsset/WaveAsset/PortalEncounterAsset внутри Presentation; Editor validator/preview/export; Rokas.Core.EditModeTests.asmdef с Editor-only reference для tool tests; camera/cue/debug modules; ContentValidationTests, AnimationSyncPlayModeTests, AccessibilityPlayModeTests.

**Consumes:** validated Core DTO и outcomes. **Produces:** deterministic SO export, marker hash, переиспользуемое представление.

- [ ] Authoring fixture: изменение contact marker меняет export impact. Duplicate/missing ref и окно вне acquisition дают точную ошибку asset/field.
- [ ] Animation Events используются только для bake/preview. Повторный runtime callback не меняет HP/AP.
- [ ] Scrub timeline показывает clip/contact/windows/input error. Replay с тем же definition hash даёт те же outcomes.
- [ ] Сначала fixed camera; lock от первогоT−400ms до закрытия последнего late window; blends только между actions.
- [ ] Добавить cue priority, ducking, отмену и повторное расписание audio при pause. Без звука паттерн остаётся читаемым.
- [ ] Добавить remap, window profiles, speed0.75, pause, flash/shake controls и auto ordinary Dodge assist.
- [ ] Проверить Story profile: Dodge[−360,+90], Parry[−165,+67.5], Perfect Standard. Acquisition расширяется до+90; невложенный профиль отвергается.
- [ ] Реализовать обучение по §56; Perfect необязателен для прогресса.
- [ ] При добавлении продвинутого status content использовать §32: Burn/Weaken/Haste/Exposed/Barrier и enemy-only Stun. Cases: DoT death до action, skipped slot тикает DoT, counter не тикает duration, no stacking beyond declared rule. Не активировать статусы, которых нет в согласованном encounter.
- [ ] Прогнать50 entry/exit циклов. После warmup RT/material/voice/subscription counts не растут; измерить p95/p99 и GC при4 actors.
- [ ] Visual QA с реальными clips на30/60/120 и720p; измерить contact alignment с целями±33ms при30 и±16.7ms при60.

**Risk:** polish разрастается в graph editor, party system и большой cinematic framework.
**DoD:** дизайнер создаёт второй attack и второй archetype на существующей grammar без нового combat C#; accessibility работает в runtime.

## Task 12 — BALANCE: policy simulation, telemetry и fatigue

**Goal/Systems:** проверить выборы и темп в настоящем бою.
**Dependencies:** T9/T10/T11.
**Files:** CombatDebugOverlay.cs, локальная development telemetry/replay, Core policy tests/content tuning. Внешняя аналитика не подключается.

**Consumes:** build/content hash, input/outcomes. **Produces:** воспроизводимый balance report и согласованный стартовый контент.

- [ ] Собирать device/FPS/error/delivery lag/HP/AP/Seal/overcap/action mix/wait time/seed/assist flags.
- [ ] Debug controls: Replay Attack, Next Attack, Slow Motion, Frame Step, Force Break, Kill Enemy, Reset. Cheat runs исключать из balance statistics.
- [ ] Проверить12-turn AP baseline: defensiveAP0 →7skills/5Basic/436damage;1 →10/2/520;2 →12/0/576.
- [ ] Прогнать1000 фиксированных seeds на состав с weapon1/3/5 и вложенными вероятностями защиты0/50/80/95%. Измерить win rate, p90 duration, starvation, доминирующую команду.
- [ ] Играть8 и20 enemies, затем три повторных портала; измерить усталость, abandonment, presses/minute и разнообразие решений.
- [ ] Сравнить расчёт20-total198–250s/54presses с новичком и опытным игроком. Если повседневное повторение утомляет, обычный контент ограничить8–12 enemies,20 оставить специальным испытанием.
- [ ] Менять один параметр за раз с обновлением ledger: HP/Seal/AP/AI weights/sequence cost. Не сужать windows, скрывая дисбаланс ресурсов.
- [ ] Manual BAT C: boss+8+20; зафиксировать конкретные причины выбирать Dodge, Parry, Defend и разные offensive skills.
- [ ] Записать согласованные значения и неопределённости; полный milestone C gate перед handoff.

**DoD:** ресурсы ограничены, темп измерен, комфорт20-total принят либо контент явно ограничен.
**Risk:** математическая модель предполагает успешное чтение телеграфа; manual play остаётся обязательным.

## Task 13 — MIGRATION rollout и production default

**Goal/Systems:** согласованный режим становится default только для новых runs.
**Dependencies:** пользовательская приёмка milestones A–C и свежий ref audit.
**Files:** encounter/contract mode config; migrator/router; FoodService и описание эффекта; build/resource inclusion; документация; RolloutRegressionCases.

- [ ] Прочитать актуальные refs, local diffs и editor state; выбрать точный verified source checkpoint. Не включать неизвестную более позднюю работу.
- [ ] Проверить матрицу v1 Home/Combat/Payment, v2 mid-action/wave/Failed/Sealed/Payment, first-clear ledger, content mismatch и future version.
- [ ] Новый default действует только на новые runs; v1 active Combat заканчивается в Legacy. Mode записан в checkpoint.
- [ ] Green Tea даёт ReactiveTurns startingAP+1 с новой понятной подписью; существующий consume/clear lifecycle сохраняется. Не придумывать эффекты остальных data-only блюд.
- [ ] Проверить однократные Yumiko return/guild message/reaction audio при retry/return/claim/reload.
- [ ] Не удалять Combat2, branches/worktrees и не мигрировать неизвестное lane C3 state. Cleanup требует отдельного решения.
- [ ] Выполнить общий unified integration gate и canonical manual BAT D.
- [ ] Остановиться на согласованном milestone, сообщить exact SHA, local/remote equality, evidence и ограничения.

**DoD:** новый режим работает в канонической игре, прежние Home/Messages/manual Combat/save/payment сохранены.
**Rollback:** reviewed config возвращает Legacy для новых runs; v2 reader и backups сохраняются. Не запускать старую версию поверх v2 save и не сбрасывать пользовательские файлы.

## Verification commands и evidence

Команды предназначены для будущего исполнения в выбранном рабочем дереве. Перед запуском проверить инструменты; пути найдены в исторических evidence и не подтверждают состояние Editor/лицензии.

Core — существующий console runner:

```powershell
& 'D:/Rokas/combat2-tools/dotnet-8.0.424/dotnet.exe' run --project Tests/Core/Rokas.Core.Tests.csproj --configuration Release
```

Asset validator и whitespace:

```powershell
& 'C:/Python314/python.exe' -B Tools/validate_assets.py
git diff --check
git diff --cached --check
```

Unity tests запускать раздельно при закрытом Editor данного worktree. Пути project/evidence выбираются после проверки безопасного tree. Ниже пример канонической финальной проверки; не запускать второй Unity процесс над уже открытым проектом.

```powershell
& 'D:/BOT/6000.3.19f1/Editor/Unity.exe' -batchmode -projectPath 'D:/Rokas/Rokas' -runTests -testPlatform EditMode -testResults 'D:/Rokas/reactive-combat-evidence/editmode.xml' -logFile 'D:/Rokas/reactive-combat-evidence/editmode.log'
& 'D:/BOT/6000.3.19f1/Editor/Unity.exe' -batchmode -projectPath 'D:/Rokas/Rokas' -runTests -testPlatform PlayMode -testResults 'D:/Rokas/reactive-combat-evidence/playmode.xml' -logFile 'D:/Rokas/reactive-combat-evidence/playmode.log'
```

Не использовать `-nographics` для visual runtime proof. Exit0 недостаточен без XML counts/failed/skipped и проверки логов. Существующие workflow push triggers на development не гарантируют запуск при integration push; нужен явный dispatch на правильном ref либо локальные suites.

Полные regression areas: Core/domain; Combat2 manual input/HUD и отсутствие autoattack; Home weather/lights/parallax; VN/startup/laptop; Messages/Live Messenger/typing/unread/reactions/audio; contract/food/Yumiko; SaveStore/backup/future/corrupt; money/progression/payment; scene/camera/input lifecycle.

## Unified integration gate каждого milestone

- [ ] Feature checkpoint проверен; source SHA выбран явно, а не взят как последний worktree HEAD.
- [ ] Выполнен merge в integration/rokas-unified с сохранением истории; shared Core/input/UI/save конфликты разрешены по смыслу.
- [ ] Полные integrated Core, EditMode и PlayMode suites зелёные; current runtime visuals просмотрены.
- [ ] Asset validator сопоставлен с recorded baseline.33 замечания текущего research не объявлены обязательной массовой предварительной работой. Отличия от historical13 требуют объяснения/решения перед green handoff.
- [ ] Relevant Unity logs проверены; literal `git diff --check` и staged check дают exit0 и пустой вывод.
- [ ] `VERIFIED ROKAS UNIFIED INTEGRATION CHECKPOINT` создан только после зелёного integrated tree.
- [ ] Push выполнен **только** в integration/rokas-unified; exact local/remote SHA совпадают.
- [ ] До обновления canonical folder проверены status, staged/unstaged diffs, untracked collisions и editor state. Значимые локальные изменения сохранены; reset/clean/force checkout не используются.
- [ ] `D:/Rokas/Rokas` безопасно обновлён до verified SHA; сохранность прежних локальных файлов проверена.
- [ ] Пользователь получает manual BAT/review только в canonical folder, Unity6000.3.19f1, с шагами, evidence и ограничениями. Работа останавливается на запрошенном milestone.

**Manual BAT в этом плане** — боевой приёмочный прогон пользователя в каноническом Unity-проекте. Это не требование создать Windows .bat-файл и не разрешение объявлять субъективную оценку пройденной за пользователя.

## Self-review / coverage

| Требование | Задачи |
|---|---|
| Session/arena/state machine | T1/T3/T7 |
| Queue/active attacker/hit tracks | T1/T3/T4/T8 |
| Dodge/Parry/Perfect/edge inputs | T2/T4/T11 |
| Offense/movement/skills/resources/Break | T5/T7 |
| Multi/waves/20 total | T8/T9/T12 |
| Boss | T10 |
| Camera/animation/UI/audio/VFX | T7/T11 |
| Save/retry/reward/first-clear | T6/T9/T13 |
| Pause/spikes/lifecycle | T2/T4/T7/T11 |
| Telemetry/debug/sandbox/tutorial | T7/T11/T12 |
| Automated/PlayMode/manual QA/checkpoints | Все задачи и общий gate |

План не является подтверждением готовности; ни один checkbox не отмечен. Сначала пользователь принимает spec и разрешает конкретный milestone. После этого исполнитель следует утверждённым правилам, не начинает новое исследование вместо реализации.
