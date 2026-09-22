# ROKAS ReactiveTurns — численная сверка

Дата завершения:20.09.2026. Основа — сохранённый [отчёт](D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/ROKAS-Reactive-Combat-Research.md), без повторного исследования.

**FACT:** нового combat runtime пока нет. **CALCULATION:** ниже проверена арифметика выбранных правил и четырёх сценариев. **DESIGN DECISION:** ограничения давления/ресурсов являются нормативными. **HYPOTHESIS:** ощущение боя, человеческое время решений и production FPS требуют Unity/playtest.

## Что было восстановлено и дополнено

Восстановлены66 разделов отчёта, JSON расчётов AP/тайминга, validator evidence и начатый планTasks1–5. Аудит архитектуры/референсов не повторялся. Завершены:

- проверка состава волн и экономики;
- конкретные длительности и число defensive presses;
- уточнение Broken/counter и AP overcap;
- economicRunId/attemptId, retry/quit/checkpoint/first-clear rewards;
- проверка существования66 первоначальных локальных ссылок и корректности указанных строк;
- отделение текущего кода от предлагаемых symbols.

## Общие инварианты

| Величина | Значение | Согласование |
|---|---:|---|
| Active actors enemies | обычно1–3, hard content cap4 | Boss+2adds максимум3 enemies |
| Active damage sequence | **1** | Active enemy slot не равен одновременно атакующему |
| Pressure interval | ≤2 enemy actions,≤6 hits,≤8 authored seconds | Очередь принудительно даёт ответ Hunter до превышения |
| Normal triple | 3 hits:1.000/1.650/2.550s | Worst-case3.85s включает counter budget |
| Две triple |6 hits,7.7s | Укладываются в лимит |
| AP | initial3,cap6,+1/PlayerTurnStart | Первый command AP4 |
| Basic | damage20,+2AP,Seal12 | Одно начисление независимо от количества callbacks |
| Seal Strike | damage48,cost3,Seal35 | Broken damage60 |
| Parry / Perfect | AP1/2,Seal12/20 | AP≤2 за player interval;Seal≤40 за enemy action |
| Counter | damage20,приBroken25 | AP0,Seal0,нового хода нет |
| Rounding | floor(raw+0.5) | Один раз после всех множителей |

Серия любой длины не может бесконечно генерировать AP: при cap2 даже20 успешных parries в одном интервале дали бы максимум2, а такой authored action дополнительно отвергнут лимитом hits/длительности.

## Четыре сценария

| Показатель | Duel |8 enemies |20 enemies |Boss |
|---|---:|---:|---:|---:|
| Waves |1 |3 |6 |1 с3 фазами |
| Всего уникальных enemies |1 |8 |20 |2:boss+один add |
| Max active enemies |1 |3 |4 |2 |
| Player commands |4 |14 |26 |10 |
| Из них offensive / Defend |4/0 |14/0 |26/0 |9/1 |
| Enemy actions |2 |24 |42 |10,включая1 preparation без hits |
| Break-skipped slots |1 |3 |3 |1 использован; финальный token не нужен |
| Defensive presses |4 |44 |54 |20 |
| Ручные counters |1 |0,игрок пропускает |0,игрок пропускает |5 |
| Initial / final AP |3/2 |3/0 |3/0 |3/3 |
| Final HunterHP |92 |100 в безошибочной policy |100 в безошибочной policy |87 |
| Расчётное время при2–4s решений |23.65–31.65s |125.65–153.65s |198.2–250.2s |69.8–89.8s |

Количество presses — именно defense input, без подтверждения команд/выбора targets/counter. Поэтому20 hits и5 counters босса не означают20 total button presses.

8/20 модели используют детерминированную policy: против single/projectile — Dodge; против elite triple — Parry/Dodge/Perfect; counter declined; enemy recovery100; Basic при AP<3, иначе Sweep по≥2живым целям или Seal Strike по одной. Учитываются queue/budget, AP caps, Seal/Break/skip/refractory и смерти. Нет weakness, еды, crit, DoT, случайных ошибок. Это модель правил в исследовательском расчёте, не новая игровая реализация.

Полный журнал каждого P/E/skip, AP, HP, Seal: [encounter-accounting.json](D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/encounter-accounting.json). Состав/AP rotations: [balance-model.json](D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/balance-model.json).

Время8/20 использует **полный authored budget** даже у прерванной elite sequence/пропущенного counter, поэтому execution estimate консервативен. Длительное человеческое обдумывание, pause, load hitch и поражения сюда не входят.

### Duel: проверка ledger

Damage:48+25+60+20+48=201 raw; enemyHP180,overkill21. Hunter получает8,остаётся92. AP:3+4turn gains+2Basic+2defense−9cost=2. Seal:60−35−12−20→Broken;P2 получает60damage,Sealreset60;P3 не снимает Seal в refractory;E260 завершает refractory.

Time:3skills×1.6+Basic1.4=6.2;triple3.85+single1.6=5.45;intro/outro4;decisions8–16. Total23.65–31.65. Queue ticks0/60/115/160skip/230/260/330 не противоречат recovery.

###8 enemies: волны и cap

| Wave | Состав/HP | Commands |Enemy actions |Defense presses |
|---|---|---:|---:|---:|
|1 |3×60=180 |4 |6 |6 |
|2 |60+60+120=240 |5 |8 |12 |
|3 |120+120=240 |5 |10 |26 |
|Total |8 enemies/660HP |14 |24 |44 |

12skills×3=36AP spent;2Basic=4AP;14turn gains;15defensive AP после caps. 3+4+14+15−36=0. Все effective enemyHP уменьшения суммируются до660, HP никогда не отрицателен.

Time:player22.0+enemy67.65+transitions4+intro/outro4+decisions28–56=125.65–153.65s.

Сценарий§57 отчёта показывает **другую допустимую ветку**, где normal enemies парируются ради быстрого третьего Sweep. Он не претендует на тот же ledger, что policy Dodge-only по normal здесь. Оба соблюдают cap. До50defensive presses за2–3min — потенциальная усталость; это предмет manual gate.

###20 enemies: сумма и длительность

|Wave |Normal40 |Ranged60 |Elite120 |Count |HP |Commands |Enemy actions |Presses |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
|1 |3 |0 |0 |3 |120 |3 |4 |4 |
|2 |2 |1 |0 |3 |140 |4 |7 |7 |
|3 |3 |0 |1 |4 |240 |4 |5 |9 |
|4 |3 |1 |0 |4 |180 |5 |10 |10 |
|5 |0 |2 |1 |3 |240 |5 |8 |12 |
|6 |0 |2 |1 |3 |240 |5 |8 |12 |
|Total |11 |6 |3 |**20** |**1160** |**26** |**42** |**54** |

19skills×3=57AP spent;7Basic=14AP;26turn gains;14defensive AP. 3+14+26+14−57=0. Maxactive4,не20. Между решениями не больше2enemy actions;10 раз forced-response scheduler предотвращает более длинную серию.

Time:player40.2+enemy92+transitions10+intro/outro4+decisions52–104=198.2–250.2s.

Ранее параметрическая оценка24–34команд/1.5enemy actions per command давала185.6–325.1s. Новая конкретная policy находится внутри этого диапазона; это не противоречие, а конкретизация. Дизайн-цель3.5–6min остаётся ориентиром для реальных игроков; безошибочный3.3min маршрут допустим.

Повтор3 таких порталов подряд: примерно9.9–12.5min и162defensive presses по этой policy; это серьёзный fatigue budget. Нельзя рекомендовать20-total как обязательный ежедневный формат без повторного playtest. Обычный8-enemy маршрут3раз —6.3–7.7min и132presses, то есть меньший total enemy count сам по себе не гарантирует меньше реакционной нагрузки: важны elite triples.

### Boss: проверка урона/AP/времени

Boss480HP,add24HP. Player raw damage по boss384;counter raw105;total489,overkill9 на финальном counter. Effective boss damage480,add24 уничтожен Sweep. Hunter теряет8 в phase2 и5 подDefend на ultimate:100−8−5=87.

AP:initial3 +10turn gains +4Basic +12defense grants −23skill costs −3overcap=3. Три потери cap: PlayerTurnStartP4,ParryE945,PlayerTurnStartP10. Именно их легко пропустить при суммировании экономики.

Enemy actions:4triples +1Heavy +2Normal +1preparation +1ultimate +1add =10. Presses:4×3+1+2+0+4+1=20. Counters5. Ultimate4hits,не5 и не8; максимум44 rawdamage, подDefend22 при полном провале. Scripted route пропускает только один обычный hit10 подDefend, поэтому получает5.

Time:player14.8;enemy4×3.85+2.2+2×1.6+1.2+4+2=28.0;phase transitions3;intro/outro4;decisions20–40. Total69.8–89.8s.

### Внесённые исправления

1. 8-enemy defensive target20–40 расширен до20–45 после конкретного результата44.
2. 20-total ориентир55–100presses уточнён до50–100: рассчитанная policy54.
3. Boss duration2.5–4.5min заменена на1.2–3min: точный уверенный route69.8–89.8s. Число HP480 не увеличивалось ради искусственного заполнения времени.
4. AP overcap босса явно учтён; финальный AP3,а не5.
5. Duel duration приведена к той же модели intro/outro4s:23.65–31.65s.
6. Break-triggering action не расходует vulnerability следующего offensive command; counter не расходует её.
7. Сохранения различают economicRunId/attemptId/actionId; payment entitlement принадлежит run, а не attempt.
8. Seed retry стабилен; новый seed только у нового Accept. First-clear reward ledger не зависит от retry/content balance hash.

## Timing audit

[96 vectors](D:/Rokas/Rokas/Docs/Research/2026-09-19-reactive-combat/timing-boundary-model.json) покрывают16offsets×2defenses×3frame rates. При обычной доставке на следующем кадре30FPS lag≤33.334ms,60≤16.667,120≤8.334; allowance40ms покрывает эту модель.

Это не доказывает доставку реального устройства/Unity. Native timestamp adapter, input flush before Advance, abnormal lag/focus suspension и реальные30/60/120PlayMode tests остаются обязательными. Input после закрытого watermark не переписывает resolved hit; оно даёт диагностический LateDelivery. Если такое наблюдается в normal runtime, fairness gate провален.

## Static audit:33 не равно33 обязательным блокерам

FACT:validator33 observations, historical13, added20/removed0; exit1. Категории:4missing TMP GUID,1unknown assembly mapping,25below-HD,3audio format observations.

- Existing debt: проблемы ресурсов, существовавшие до новой боёвки; не требуют массового исправления ради headless prototype.
- Historical mismatch:20добавлений относительно старой записи; надо объяснить перед будущим unified checkpoint, а не тихо назвать33 новым допустимым baseline.
- True blocker: только проверенная проблема, мешающая конкретному milestone, например compile error, отсутствующий обязательный новый clip/telegraph, нерешённый save/payment regression. Validator «unknown assembly» сам по себе не является доказанным Unity compile error.

## Дополнительная проверка архитектуры

FACT: Rokas.Editor assembly ссылается на Core и Presentation; EditMode tests уже ссылаются на Presentation. Поэтому будущие authoring SO перенесены в плане под Presentation/ReactiveTurns/Authoring, а tool tests получают Editor-only reference. Исходный вариант новой общей папки Authoring мог бы попасть в Assembly-CSharp и нарушить доступ из asmdef. Продуктовые asmdef сейчас не изменены.

## Границы подтверждения

Не запускались новый combat code, Unity runtime, полные suite или пользовательская manual QA. Числа — согласованная спецификация и аналитический baseline. При будущей реализации fixtures должны воспроизвести эти ledgers; расхождение требует объяснения, а не молчаливого изменения expected values.

