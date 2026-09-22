# ROKAS ReactiveTurns — точный контракт правил

Версия дизайна1.0 ·20.09.2026 · **DESIGN DECISION, ещё не продуктовый код и не разрешение реализации**.

Этот документ компактно фиксирует обязательные правила сохранённого [полного отчёта](D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/ROKAS-Reactive-Combat-Research.md). Числа согласованы с [аудитом](D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/Numerical-Consistency-Audit.md). Обнаруженное будущее противоречие нужно исправить в обоих документах с объяснением; исполнитель не может молча выбрать более удобный вариант.

## R01. Владение состоянием и режим

Один Hunter, один ReactiveCombatSession authority. Legacy Combat2 остаётся отдельным combat mode. Состояния encounter/action/hit не выводятся из Animator/UGUI. В реальном времени исполняется максимум **одна damage sequence**; до4 enemies могут одновременно находиться на арене. Visual supports не создают самостоятельный урон вне текущего sequence.

PlayerCommand не ограничен временем. Command preview бесплатен. Commit требует актуальную revision, живые targets и достаточный AP; создаёт actionId, списывает AP один раз, закрывает команды до settlement. Повторный commandId возвращает прежний ответ без эффекта.

## R02. Очередь

Actor ordering:nextTick,spawnOrdinal,actorId. Recovery=ceil(delay×100/speed),speed80–125. P initial tick0,enemies60/90/120/150; будущий summon≥now+60. Самое раннее валидное событие dispatch выполняется с monotonic nowTick.

До очередного enemy dispatch проверяются2actions/6hits/8authoredSeconds с полным worst-case counter cost. При превышении Hunter получает forced response; dispatch=max(now,min(Pdue,earliestEdue)). Enemy due сохраняется. Budget сбрасывается при принятии следующей команды Hunter, а не на counter/wave/pause.

Skipped enemy slot не расходует action/hit budget, но назначает новый due=dispatch+100 в начальном наборе. Attack-specific recovery допускается только в данных; boss storyboard перечисляет его явно. Forecast вызывает тот же алгоритм на clone; неизвестный intent помечается неопределённым.

## R03. Начало и конец атаки

EnemyTurnStart фиксирует attack ID/targets/seed, показывает intent и запускает один authored clock. AI не меняет выбранный паттерн внутри sequence. Approach/telegraph/contact/recovery — части одного action.

Каждый hit имеет уникальный ID(run,attempt,action,localHit),impactUs,responseMask,windowProfile,effects. Между contacts≥350ms. Sequence завершается только после finalization всех сохранившихся hits и counter close/finish. Следующий attacker не открывает окно, пока предыдущий action не settled.

Pressure budget — абсолютное ограничение; ultimate не обходит его. Boss8hits обязан быть разделён на2actions с настоящим player command между ними.

## R04. Время и input

Initiative ticks, combat microseconds и monotonic device time — разные величины. Combat clock использует piecewise segments speed/pause. Внутри кадра: flush input → сортировка поdevice timestamp/inputId → mapping/epoch validation → proposals → Advance → effects → presentation.

Одинаковый inputId не применяется дважды. Input epoch меняется приpause/focus/rearm. Все holds требуют release до нового combat press. UI menu input и combat input имеют одного владельца по context; Space из UI не проходит одновременно в Parry.

При одновременном Pause и combat input timestamp пауза имеет приоритет: input на границе и после неё отвергается; более ранний input остаётся valid proposal. Paused clock не доходит до будущих impacts.

## R05. Стандартные окна и исходы

| Input offset отT | Dodge | Parry |
|---|---|---|
| Раньше−400ms | Ignored,no buffer | Ignored,no buffer |
| −400…−240.001ms | EarlyFail | EarlyFail |
| −240…−110.001ms | Dodge | EarlyFail |
| −110…−40.001ms | Dodge | Parry |
| −40…+25ms | Dodge | Perfect |
| +25.001…+45ms | Dodge | Parry |
| +45.001…+60ms | Dodge | LateFail |
| Позже+60ms | Ignored; отсутствие valid attempt даёт Miss | То же |

Все точные endpoints включены. Таблица выражена с шагом1µs, не предполагает округление input кmilliseconds.

Acquisition interval Standard=[T−400000,T+60000]µs. Первое **Dodge/Parry press** в acquisition фиксирует попытку, даже если выбранный response запрещён grammar. WrongDefense=Fail,полный damage; исправить вторым press тот же hit нельзя. Другие кнопки не тратят attempt. Одновременные Dodge+Parry выбирают Dodge.

Если acquisition соседних hits пересекается, ввод адресуется самому раннему unresolved hit. После locked attempt следующему hit нельзя принять press до предыдущего impact и release. Debounce90ms подавляет дубликаты, но не блокируетновые допустимые hits сgap≥350ms. Одна ошибка не блокирует оставшиеся hits.

## R06. Grammar и урон защиты

Normal: Dodge/Parry/Perfect предотвращаютHP. Heavy: Dodge/Perfect предотвращаютHP;ordinaryParry даёт damage×0.5 без AP/Seal. Grab и Area в первом наборе допускают только Dodge; Parry=WrongDefense. Projectile:Parry разрешён только приreflectable=true. Обязательного parry-only в первом контенте нет.

Miss/EarlyFail/LateFail/WrongDefense дают полный authored damage, далее Defend/difficulty. Successful Dodge даёт0AP/0Seal. NormalParry даёт1AP/12Seal;Perfect2AP/20Seal сcapsR09.

Входящий damage сHP effects финализируется после late window+delivery watermark, максимум около100ms послеT в обычной Standard конфигурации. НаT можно показать neutral contact; нельзя сначала снятьHP,а потом отменить его приvalidlateinput. Ранний известный успешный outcome можно применить наT.

## R07. Доставка, pause, spikes и hitstop

Delivery allowance40ms — заявленный контракт adapter. Finalization использует watermark mapped(wallNow−40ms). Все уже доставленные events разбираются до Advance, поэтому50–90ms frame spike сам по себе не означает потерю timestamps.

Input свалидным timestamp, пришедший **после окончательной finalization**, не изменяет HP/AP задним числом: diagnostic LateDelivery, pause на ближайшей безопасной границе, fairness test считается проваленным. Произвольная неограниченная задержка не объявлена поддержанной. До shipping adapter должен подтвердить delivery contract; менять игровые окна, скрывая технический lag, запрещено.

Raw frame gap>100ms проверяется до clamp: suspend наlast-presented time, inputs невидимого промежутка отвергаются новым epoch. Resume даёт600ms preparation unresolved suffix; resolved IDs/effects сохраняются. После обычной pause frozen clock продолжается, клавиши перевооружаются только послеrelease.

Внутри active defensive windows global hitstop отключён. После финального hit допускается40–70ms freeze общего combat/presentation clock; input в freeze не буферизуется в следующую атаку. Background messages продолжаютdata updates,toast ждётCommand.

## R08. Player offense и counter

Basic:Power1,20startdamage,Seal12,delay100,cost0,+2AP один impact. SealStrike:Power2.4,48damage,Seal35,delay115,cost3. Defend:cost0,delay80,incoming attackdamage×0.5 до следующего Pturnstart; не лечит.

Sweep:cost3,24damage/12Seal каждойcommit-target,delay130. Heavy:cost5,68/50,delay140;optional timing−100…+50ms даётHP×1.15,miss×1.0. Anchor:cost2,16/15,delay100,+35targetdue один раз до следующего естественного действия цели.

Counter:≥2Parry-or-Perfect иfinalPerfect;single-hit толькоexplicit flag. FreshConfirm в600ms после закрытия sequence;damage20 (Broken25),AP0,Seal0,noinitiative/status tick. One token;deadattacker cancels;counter не создаёт counter. Пропуск бесплатен. Offer открывается после lastImpactUs + profile.acquireLateUs +40000 авторских микросекунд и finalization всех hits; Standard даёт Tпоследнего+100ms. Counter input interval включает обе границы [open,open+600000]; позднее нажатие игнорируется.

Heavy с late offensive input не наносит сначала базовый damage с последующим увеличением: damage ожидает закрытия окна + delivery allowance, затем применяется один раз с×1.0 или×1.15.

## R09. AP, Seal и ограничение награды

AP initial3,+1 наnatural Pturnstart,cap6. Defense AP cap2 **за интервал между Pturnstarts**; сброс cap совпадает с началом следующего player turn. Basic+2 не входит в defense cap. Turn gain,parrygain иBasic обрезаются cap6 сучётомovercap. Wave transitions/counter не дают AP.

Seal60normal/100elite/160boss. DefenseSeal cap40 наenemyAction. ПриSeal≤0:Broken,одинskip token,HP vulnerability1.25. Current hit завершается;interruptible suffix отменяется. Triggering action не расходует будущую vulnerability. Следующий offensive player command поцели расходует её;counter не расходует. Если цель игнорируют,конец второго следующего Pturn завершает vulnerability.

После consumption/expiry Seal=max,Seal damage0 вrefractory до1completed enemy action или2дляboss. Skipped slots не уменьшают refractory. Death приоритетнееBreak;deadtarget не получаетcounter/status. Старый40%ritual не переносится.

## R10. Damage и statuses

Damage = floor(max(0, Power × Attack × 100/(100+Defense) × Element × Status × Crit × Timing × Broken × Difficulty)+0.5). Attack = 4 × legacy clickDamage × (1+0.2×(weaponLevel−1)); Defense≥0. Prototype Crit=1, без случайного промаха.

DefenseOutcome и Defend применяются до единственного округления. Defend относится к enemy attack damage, но не к Burn/periodic damage. Barrier поглощает финальный damage до HP. HP ограничен [0,max]. Один event ID создаёт один batch эффектов.

Natural turn: periodic effects → death → skip → AP/start command. Defend снимается перед новым Hunter turn; periodic damage не получает его reduction. Counter не тикает statuses. Stun в начальном status catalog разрешён только enemies, чтобы не отнимать гарантированное окно ответа Hunter.

## R11. Target / cancel / death / terminal

Targets фиксируются на commit. Invalid before commit: AP и recovery не расходуются. Target погиб до первого gameplay effect: cancel, полный refund AP и recovery; новый target автоматически не выбирается. После первого эффекта refund отсутствует; остаток по dead target отменяется. AoE не цепляет поздний summon.

HP0 Hunter → Defeat, включая same-event double KO. Последний обязательный enemy погиб при живом Hunter и отсутствии pending summon → WaveClear или Victory. Death удаляет будущие queue slots. Terminal outcome неизменяем внутри attempt; после него новые hits/commands отклоняются.

## R12. Waves и boss

Состояния Pending → Spawning → Active → Cleared → Transitioned имеют unique waveInstanceId. Transition обычно2s; HP/AP переносятся, бесплатного turn gain нет. Rest +20HP после wave3 допустим только в соответствующих encounter data; applied flag сохраняется. Reserve actors не имеют active Animator.

Boss thresholds65%/30% проверяются после action settlement. Переход однократный; пересечение обоих порогов не создаёт две последовательные intros. Add cap2; объявленный opening delay≥60ticks. Ultimate preparation занимает собственный slot, исполнение — последующий; в первом boss4hits/44raw damage. Break отменяет preparation до commit; после commit действует общая interrupt policy. Boss не обходит pressure budget.

## R13. Retry / save / reward

EconomicRunId = contractId + runSequence; attemptId увеличивается при retry. Все HitIDs включают attempt. Текущий принятый контракт оплачивается один раз; боевых попыток может быть несколько.

- Entry записывает начальный checkpoint до intro; IO failure оставляет Portal.
- Stable checkpoints: pre-action, settlement, wave entry/clear, terminal. Mid-action autosave записывает последний stable battle snapshot, не смешивая revisions.
- Выход в главное меню, закрытие и crash: Continue начинает незавершённое действие заново с тем же seed и AP до commit.
- Defeat → Retry encounter: HP100, AP3+food starting bonus, wave1, тот же seed, новый attemptId. Currency/inventory не откатываются; food не расходуется повторно.
- Assist wave retry восстанавливает точный wave-entry snapshot HP/AP/queue/rest flags и создаёт новый attemptId. Нет полного лечения или второй передышки.
- Retreat → Failed → Home, без награды; новый Accept создаёт новый economic run/seed.
- Victory → Sealed → ReturnHome → Payment. Claim сначала записывает currency/phase/messages/claimed ledger, затем показывает success.
- Ключ права на оплату — economicRunId, не attemptId. First-clear rewardId постоянен в профиле; balance/content hash не сбрасывает его.
- SaveBlocked останавливает следующий action/claim и повторяет тот же delta/nonce. Устаревшая копия профиля не перезаписывает новые Messenger updates.

v1 active Combat завершается в Legacy; v1 Payment сохраняется; v2 migrator явный. Защиты future-version/corrupt/backup остаются.

## R14. Difficulty и доступность

Story: incoming damage×0.8, Dodge/Parry widths×1.5, Perfect по умолчанию Standard. Получаются Dodge[−360,+90]ms, Parry[−165,+67.5]ms, Perfect[−40,+25]ms. Acquisition расширяется до[−400,+90]ms. Отдельный assist может также масштабировать Perfect. Validator всегда проверяет вложенность текущего профиля, а не использует жёсткий Standard late60 для всех режимов.

Standard incoming damage×1.0, Veteran×1.15, Challenge×1.3; windows автоматически не сужаются. Difficulty/speed changes вступают в силу на stable boundary, не посреди hit. AutoDodge assist даёт ordinary Dodge без AP/Seal. Visual/audio/shape дублируют cue. Timing assist не блокирует сюжетную награду.

## R15. Проверка контракта

Обязательные golden cases: duel§58;8/20 ledgers численного аудита; boss§60; early/late boundaries; pause/focus/held/double input; AP cap/Seal cap; double KO; missing content; mid-action crash; payment IO failure; wave retry/rest; duplicate first-clear; legacy Home/Messages/Combat/save.

Технический способ реализации может меняться. Наблюдаемые правила R01–R15 меняются только после объяснённого обновления spec. Полный unified integration gate и пользовательская manual QA обязательны для каждого согласованного milestone.
