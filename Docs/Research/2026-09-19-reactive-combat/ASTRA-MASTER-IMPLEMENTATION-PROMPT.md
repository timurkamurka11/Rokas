# MASTER IMPLEMENTATION PROMPT FOR ASTRA — ROKAS ReactiveTurns

Этот документ подготовлен для будущего запуска **после отдельного одобрения пользователем дизайна и начала реализации**. Его создание в исследовательской задаче не разрешает менять продуктовый код. Ниже — готовое задание исполнителю.

---

Реализуй утверждённую реактивную пошаговую боёвку Пропасти в существующем проекте ROKAS. Выполняй готовую спецификацию по проверяемым этапам. Не начинай исследование заново и не заменяй правила более простой системой без конкретного выявленного противоречия.

Если пользователь не указал более узкий или другой milestone, первая разрешённая цель после команды начать реализацию — **Milestone A: один Hunter против одного enemy, от Portal до сохранённого результата и однократной оплаты**. После integrated checkpoint и подготовки canonical manual BAT остановись. Следующие milestones начинаются после пользовательского review.

## 1. Сначала восстанови реальные документы и проект

Прочитай существующие файлы, не пересоздавай их по памяти:

1. `D:/Rokas/Rokas/AGENTS.md`.
2. `D:/Rokas/Rokas/Docs/UnifiedIntegration.md`.
3. `D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/ROKAS-Reactive-Combat-Research.md`.
4. `D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/Numerical-Consistency-Audit.md`.
5. `D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/Combat-Rule-Contract.md`.
6. `D:/Rokas/Rokas/Docs/Superpowers/plans/2026-09-19-reactive-combat.md`.
7. Соответствующие JSON evidence и существующие tests из этих документов.

Затем проверь текущие branch/HEAD/status/staged+unstaged diff/untracked files/worktrees/editor state. Исследовательский HEAD был `fd5d99afd0ecb04c20edfdb8ac5916a4be41b139`; это историческая точка чтения, а не разрешение сбросить текущую работу к ней. Заново установи актуальный integration checkpoint и явно одобренный источник.

Канонический пользовательский проект: **D:/Rokas/Rokas, Unity6000.3.19f1**. Главная staging/manual-QA ветка: **integration/rokas-unified**. Feature worktrees — внутренняя инфраструктура. Пользователь не должен тестировать отдельные Home/Combat/Messages folders.

При неизвестной более поздней работе сначала выясни provenance. Не включай её в merge просто потому, что это latest HEAD. Значимые локальные изменения сохраняй; не используй reset/clean/force checkout. Не закрывай Editor и не запускай второй Unity над открытым проектом без проверки состояния.

## 2. Зафиксируй факты и границы

Проверяй классы и функции в текущем repository до изменения. В исследованной версии были:

- Core GameSession/CombatService/ContractService/EconomyService/SaveStore/State/FoodService;
- UGUI MissionView/CombatHud/CombatInputSurface/RokasView/RokasBootstrap/RokasAudio;
- один scalar enemyHp и legacy input polling;
- custom .NET8 console test host, Unity EditMode/PlayMode;
- Home2.5D/weather, VN/startup, Messages/Live Messenger и однократная оплата;
- отдельный lane Combat3 branch, который **не является** готовой reactive turn-based реализацией.

Новые ReactiveCombatSession, TurnScheduler, DefenseResolver и другие названия из плана — **DESIGN PROPOSAL**, а не факт уже существующего кода. Создавай их только там, где они получают ответственность и тест. Не придумывай отсутствующий Animator rig/clip, Input System package или готовую3Dарену.

Проверь `Tests/Core/Rokas.Core.Tests.csproj`: исторически Core include был нерекурсивным. Новая папка Core/ReactiveTurns должна реально компилироваться и тестироваться standalone host.

## 3. Следуй утверждённому игровому контракту

Источник точных правил — Combat-Rule-Contract R01–R15. Сохрани:

- один Hunter; обычно1–3 активных enemies, максимум4 после проверки;
- **одна** damage sequence одновременно;
-20 total enemies через6 waves:3+3+4+4+3+3, а не20 simultaneous attackers;
- видимую CTB очередь, unlimited thinking в PlayerCommand;
- pressure budget: максимум2enemy actions,6defensive hits,8authored seconds до очередного решения Hunter;
- отдельный результат каждого hit: ошибкаh2 не блокируетh3/h4;
- Dodge и Parry; Perfect — качество того же Parry, отдельная кнопка не нужна;
- Basic без обязательного QTE; один optional timed skill, если входит в текущий milestone;
- ручной короткий counter, без автоматической цепочки counters;
- AP и Seal/Break; третий ресурс/party/jump/free aim не добавлять в первый slice.

Начальные Standard windows относительно контакта:
Dodge−240…+60ms; Parry−110…+45ms; Perfect−40…+25ms; inclusive microseconds. Acquisition−400…+60ms. First attempt, release latch, epoch, duplicate IDs, overlap arbitration и late-delivery semantics — именно как в контракте.

AP initial3, first turn gain1 → AP4. Cap6, Basic+2, skill3AP. Defensive AP не больше2 между PlayerTurnStart. Defensive Seal не больше40 за enemy action. Normal Parry1AP/12Seal, Perfect2AP/20Seal. Dodge0AP/0Seal. Не добавляй uncapped reward каждому hit длинной серии.

Seal60/100/160 дляnormal/elite/boss. Broken даёт один skip и×1.25 HP damage следующему offensive command; counter бонус получает, но срок не расходует. Refractory1 natural enemy action/2boss actions. Старый ritual40%damage не переносить.

У босса общие queue/input/damage rules. Ultimate preparation и execution — разные actions; первая ultimate4hits сdamage10/10/10/14. Ни phase transition, ни cinematic не обходят pressure budget.

## 4. Время и ввод должны быть проверяемыми

Отдели initiative ticks, combat clock и исходный device timestamp. Сначала прочитай queued input events, затем Advance, затем outcomes/presentation. Не сравнивай окно с временем callback или числом frames.

Scoped Input System1.20.0 — предложенная зависимость для Unity6000.3; проверь текущую совместимость перед установкой. Если legacy UI остаётся на StandaloneInputModule, настрой совместимость без двойной обработки. Один physical press не должен пройти через raw Update, Button и новый ActionMap одновременно.

Сохрани:
- raw gap detection **до** clamp delta;
- suspension при raw gap>100ms;
- pause/focus/input epoch/release-to-rearm;
-600ms безопасной подготовки unresolved suffix после невидимого промежутка;
- resolved hits и rewards не воспроизводятся повторно;
- нейтральный contact до окончательного результата late window;
-40ms delivery allowance/watermark и диагностику LateDelivery;
- отсутствие global hitstop внутри defensive windows.

Поздняя доставка после finalization не переписывает HP задним числом. Если она возникает при нормальном вводе, timing gate не пройден. Нельзя назвать систему FPS-independent только потому, что арифметический тест прошёл.

## 5. Архитектура и представление

Core без Unity references владеет combat state, actions, hits, queue, AP, Seal, statuses и snapshots. Unity presentation получает typed outcomes и actor IDs. Ни Animator callback, ни VFX, ни HUD не меняют HP напрямую.

Используй существующий масштаб проекта:
- простые constructor dependencies;
- один небольшой session coordinator;
- focused pure services для scheduler/defense/damage;
- данные атак вместо отдельного класса для каждого enemy;
- SO authoring → validated immutable DTO/export;
- отдельный presenter/camera/input adapter.

Не вводи giant CombatManager, глобальную event-bus архитектуру, сетевой rollback, DOTS, сложный node editor, NavMesh или новую UI framework без доказанной необходимости.

Арена: Hunter в ближнем плане, enemies впереди, anchors для approach/contact/return. Movement — presentation curve/in-place clips; physics collision не определяет reactive outcome. Камера фиксируется во время окна; zoom/shake не закрывают telegraph. Telegraph читается позой, формой и звуком; цвет не единственный сигнал.

Готовые rigs/clips в audited Assets не найдены. Prototype art должен быть явно обозначен; его отсутствие нельзя скрыть ложным заявлением о production quality. Проверяй actual runtime visuals, а не только код и Inspector.

## 6. Save, retry и reward — часть первого slice

EconomicRunId=(contractId,runSequence), attemptId увеличивается при retry. Все event IDs включают attempt. Terminal outcome неизменяем в attempt, право на оплату привязано к economicRunId.

Точные правила:
- Entry сначала сохраняет initial checkpoint, потом intro.
- Stable snapshots: pre-action, settlement, wave boundaries, terminal.
- Mid-action autosave сохраняет последний stable battle state.
- Close/crash/Continue повторяет незавершённое действие с тем же seed и AP до commit.
- Retry encounter начинаетwave1,HP100,initialAP+foodbonus; тот же run/seed, новыйattempt; food не расходуется повторно, inventory/currency не откатываются.
- Assist wave retry восстанавливает точный wave-entry snapshot, без полного heal и повторной передышки.
- Retreat возвращает через Failed→Home без оплаты.
- Победа использует существующий Sealed→ReturnHome→Payment→ClaimPayment lifecycle.
- Candidate payment включает currency/phase/persistent messages/claimed ledger; запись должна завершиться до UI success.
- SaveBlocked повторяет тот же semantic delta/nonce и не выдаёт второй reward.
- First-clear reward имеет постоянный rewardId, который не сбрасывается новым run или balance/content hash.

v1 Combat остаётся Legacy до завершения текущего run; v1 Payment не теряет награду. v2 migration явная; existing future-version/corrupt/backup protection сохраняется. Нельзя переписать новые unread/reactions устаревшим clone profile.

## 7. Реализуй по vertical slices

Используй отдельный implementation plan и навык executing-plans. Не превращай все13 задач в один большой patch.

**Milestone A — duel, Tasks1–7.**
Начни с одного readable single strike, Dodge/Parry/damage и Basic в sandbox. Затем добавь triple с независимыми hits, AP/Seal/Break/counter и настоящий Portal→Home→Payment→reload. После full integrated checkpoint — manual BAT и остановка.

**Milestone B — multiple enemies и waves, Tasks8–9.**
Сначала3enemies/targeting/queue/AoE, затем проверяемый cap4;8-enemy portal с3waves и сохранением carryover.20-total orchestration можно проверить данными, но не объявлять комфорт доказанным.

**Milestone C — boss, authoring, polish, balance, Tasks10–12.**
Boss phases/ultimate/add, animation/camera/audio/UI/accessibility, replay/debug, измерение fatigue и20-total portal. Отдельные checkpoints допустимы и предпочтительны, если пользователь согласовал более мелкую разбивку.

**Milestone D — rollout, Task13.**
Default только новым runs; legacy compatibility и v2 saves; food text/effect mapping; cleanup отдельным решением.

На каждой задаче: failing behavior case → подтвердить failure → minimal implementation → focused tests → relevant regressions → reviewed commit. Общий checkpoint допускается только после integration gate.

## 8. Используй численные acceptance fixtures

Не меняй expected values, чтобы скрыть ошибку реализации.

Duel: enemy180HP/60Seal. Commands4, enemy actions2, defense presses4, counter1. Enemy HP180→132→107→47→27→0; Hunter92HP; finalAP2. Calculated duration23.65–31.65s с2–4s решений.

8-enemy accounting policy:3waves,cap3,14commands,24enemy actions,44defensive presses,finalAP0. TotalHP660. Calculated125.65–153.65s.

20-enemy policy:6waves,cap4,total20/1160HP,26commands,42enemy actions,54presses,finalAP0. Calculated198.2–250.2s.

Boss storyboard:480HP+add24HP,3phases,10commands,20defensive presses,5counters,Hunter87HP,finalAP3. Calculated69.8–89.8s. Это короткий первый boss, а не обещание2.5–4.5min.

Эти durations — accounting estimates, не замеры Unity. При реализации отдельно сравни gameplay ledger и реальное presentation/decision time. Policy inputs/AI/recovery перечислены в audit и JSON; не сравнивай разные policies как один test.

## 9. Автоматические и ручные проверки

Обязательно:
- Core deterministic queue/forecast/pressure budget;
- exact timing boundaries, first attempt, repeated/held/simultaneous input;
-30/60/120+ FPS и реальные mouse/keyboard/gamepad paths;
- frame spikes, pause, focus, slowdown и input delivery;
- AP/Seal caps, counter dedup, Break expiry/refractory;
- target death/cancellation/AoE/summon/queue ties;
- double KO, boss threshold/ultimate cancellation;
- wave clear/reload/rest/attempt identity;
- v1/v2/future/corrupt/backup saves;
- crash/IO failures на result/payment;
- repeatable reward нового run и once-only first-clear reward;
- scene/camera/RT/audio subscriptions после многократного entry/exit.

Сохрани полный Core/domain, Unity EditMode и Unity PlayMode regression: Home/weather/VN/startup/laptop, Messages/Live Messenger, manual Combat2, food/Yumiko, save/progression/payment. Не возвращай autoattack ради старого теста.

Manual BAT — пользовательский боевой приёмочный прогон в **D:/Rokas/Rokas**. На каждом major milestone подготовь конкретные шаги и реальные captures; не объявляй субъективное ощущение/удобство проверенными без пользователя.

## 10. Политика integration и сохранность проекта

Следуй AGENTS.md:

1. Изолированный feature worktree при необходимости; branch prefix codex/.
2. Выбрать exact verified source checkpoint; остановить incorporation неизвестной более поздней работы.
3. Реальный merge в integration/rokas-unified с сохранением истории.
4. Семантически сохранить shared responsibilities Core/input/UI/save/Home/Messages.
5. Полные integrated Core/EditMode/PlayMode suites и actual runtime visual review.
6. Asset validator + comparison, relevant logs, literal git diff --check:exit0/empty.
7. Только после green tree создать VERIFIED ROKAS UNIFIED INTEGRATION CHECKPOINT.
8. Push только integration/rokas-unified; exact local/remote SHA match.
9. Безопасно обновить canonical folder после status/diff/untracked collision/editor checks; сохранить локальные изменения.
10. Передать canonical manual BAT, точный SHA/evidence/limits и остановиться на согласованном milestone.

Не обновляй development, не merge PR#4, не force-push и не удаляй worktrees/evidence. Integration approval не является development approval.

Research validator показал33 observations против historical13:20 additions/0 removals. Это existing debt/historical mismatch, а не требование исправить33пункта до первого prototype. Одновременно нельзя тихо принять33как новый допустимый baseline. Перед unified green handoff отличия должны получить объяснение/решение; true compile/save/timing blockers должны быть устранены.

## 11. Правила отчётности и изменения scope

В каждом handoff различай:
- VERIFIED CURRENT CODE;
- DESIGN DECISION;
- CALCULATION;
- PLAYTEST HYPOTHESIS;
- выполненную проверку и ещё не выполненную manual QA.

Сообщай что изменилось, какой observable behavior доказан, результаты suites, runtime evidence, точный checkpoint и ограничения. Не используй старые screenshot/test counts как доказательство текущей сборки.

Если реальный проект отличается от audited snapshot, сначала установи факты. Если изменение влияет на правило боя или сохранность данных, предложи конкретную минимальную поправку с причиной и последствиями. Не перепроектируй боёвку заново из-за удобства реализации.

Не расширяй scope до party, network,free aim,jump,new gear economy,ultimate resource или50enemy assets. Optional ideas остаются optional. Сначала докажи текущий milestone.

Начни с чтения документов и проверки актуального repository. Затем выполняй разрешённый milestone по плану, сохраняя существующий ROKAS и останавливаясь на согласованных manual gates.
