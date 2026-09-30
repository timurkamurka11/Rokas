# ReactiveTurns — Combat Cinematic Polish II

Дата проверки: 30 сентября 2026. Unity 6000.3.19f1. Работа продолжена с текущего состояния Stage 12, без пересоздания боёвки. Combat Core и Bootstrap не изменены.

Результат находится в `D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY`, ветка `r11-finished-ui`. Это локальный боевой checkpoint. Общие проверки проекта содержат сбои вне этого pass; весь проект зелёным не объявляется.

## Итоговый отчёт — 38 пунктов

### 1. Recovery HEAD

`361340d1efe498241fc28e5051dbf3bf6132b5ba`. Recovery TREE: `ad3db4d1c5f9801e2bfb3c2acfce8b3c55e3f58d`.

### 2. Final HEAD

Точный SHA опубликован в финальном сообщении после создания commit. Этот отчёт входит в сам checkpoint, поэтому не содержит собственного commit hash. Для его получения: `git rev-parse HEAD`.

### 3. Final TREE

Точный TREE опубликован в финальном сообщении. Команда: `git rev-parse HEAD^{tree}`.

### 4. Branch

`r11-finished-ui`. Работа выполнена непосредственно в указанной R11 копии. Новые ветки, BAT и review clone не создавались; push не выполнялся.

### 5. Исправленные Stage 12 сценарии

Проверки используют Preparation → Approach → Contact → Impact → Recovery → Full Return → Settle. Добавлены явное подтверждение контакта, восстановление slot/facing и ожидание реальной готовности controls. Wave-сценарий допускает начало волны с enemy turn, ждёт окончания входа всех врагов и проверяет Victory/save. Capture-сценарий ждёт реальные окна Core и снимает отдельно Block, Dodge и попадание по телу. Early Heavy input теперь отправляется в подготовке/подходе, когда окно действительно ещё закрыто: он отвергается; затем проверяются настоящий timed/late input, HP 78/68 и неизменность committed target. Проверен Counter с общим enemy action ID. Добавлена regression отменённого suffix многоударной атаки: возврат завершается без фиктивного второго HitResolved/SFX.

### 6. Перекодированные WAV

`NormalWhoosh`, `HeavyWhoosh`, `NormalFleshContact`, `HeavyFleshContact`, `MonsterFleshContact`, `GuardMetalContact`, `DodgeBackstep` приведены к stereo PCM, 16 bit, 22050 Hz. Проверены RIFF/header/data, ненулевые samples, сохранение `.meta`, уникальность GUID по Assets. Результат: **7/7 PASS**, [аудит](AudioHeadersAndGuids.json).

### 7. Финальный audio-event mapping

| Событие | Ресурс / правило | Volume |
| --- | --- | ---: |
| Keiko Normal vocal | Существующие Keiko attack sound 2/3 | .32 |
| Keiko Heavy vocal | Существующие Keiko hit attack / attack sound 4 | .38 |
| Normal swing | PolishII/NormalWhoosh | .30 |
| Heavy swing | PolishII/HeavyWhoosh | .43 |
| Normal body contact | PolishII/NormalFleshContact | .56 |
| Heavy body contact | PolishII/HeavyFleshContact | .72 |
| Monster vocal | Существующий банк monster attacks | .34 |
| Monster swing | NormalWhoosh как короткий motion layer | .22 |
| Monster body contact | PolishII/MonsterFleshContact | .57 |
| Block / Parry / Perfect / Counter guard | PolishII/GuardMetalContact; flesh подавлен | .64 |
| Dodge | PolishII/DodgeBackstep; body impact подавлен | .26 |

Один semantic event воспроизводится один раз по sequence + actor + hit + event. Одна атака сохраняет отдельные vocal/swing/contact layers. Alias связывает pre-commit presentation ID с Core ID. Heavy metadata учитывает actor, поэтому Counter Кейко не наследует Heavy от врага. Animation audio events из новых runtime clips удалены; legacy generic hit hook подавляется обработанным contact. Monster idle bank звучит только в спокойной разрешённой фазе, volume .15.

### 8. Unity import

Реальный Unity builder завершился exit 0. Runtime library подключает извлечённые clips к существующим моделям. Семь WAV загружены через Resources, проверены channels/frequency/GetData. Новые Assets имеют `.meta`. Ошибок импорта новых audio/animation/VFX ресурсов в релевантных логах не обнаружено.

### 9. Compilation

Компиляция четырёх затронутых assemblies по реальным Unity Bee references — PASS: Presentation, Editor, PlayModeTests, Core.EditModeTests. Последующие реальные Unity EditMode/PlayMode запуски подтверждают компиляцию проекта.

### 10. Console / logs

Ошибок компиляции C#, shader или новых media assets не обнаружено. В логах есть сообщения среды о недоступном access token для обновления лицензии и запросе D3D12 info queue; licensed Editor продолжил работу. Ошибки тестовых assertions вне Combat приведены в пунктах 31–32. Не выдаются за ошибки исправленного patch или доказанно старые дефекты: отдельный baseline запуск этих suites не выполнялся.

### 11. Keiko entrance

Keiko начинает за левой границей кадра, идёт по entrance walk к точному slot, затем проигрывает отдельный неподвижный EnterBattleSettle и принимает двуручную idle stance. Цикл ходьбы больше не продолжается после остановки arena root. Вход занимает 3.2 s, settle .4667 s, затем проверяется IdleSettled и короткий beat .16 s.

### 12. Music gating

Combat BGM разрешается после завершения entrance/stance, с fade .35 s и combat volume .25. Автоматические проверки подтверждают gate и сохранение audio lifecycle. Прослушивание человеком в этом запуске не выполнено; оценка громкости на конкретной аудиосистеме остаётся ручной.

### 13. Portal

Враги входят последовательно после Keiko. Portal formation/open .7 s, emergence/travel 1.35 s, collapse .45 s и beat .16 s. Использованы тёмные процедурные слои объёма и остаточной энергии, clipping тела в aperture и фиксированная плоскость портала. HP cards согласованы с входом и текущей фазой; input не открывается до завершения всех entrances.

### 14. Portal jitter — причина и исправление

Раздельные отрезки reveal/travel передавали transform между фазами; обрезанный locomotion loop дополнительно сбрасывал ноги. Теперь пересечение aperture и движение к slot используют непрерывный путь с easing, а locomotion — полный исходный цикл и cadence по пройденному расстоянию. Контролируются точная конечная позиция и отсутствие смены authority движения. Последовательные Unity кадры просмотрены.

### 15. Enemy locomotion

Anticipation → acceleration → movement → deceleration → plant → claw strike. Нормальная и тяжёлая claw animation извлечены из существующего Yokai rig; horizontal Hips motion удалён, jump Y сохранён. Скорость шага вычисляется из source stride × фактический model/world scale; проверка изменённого масштаба обоих actors прошла.

### 16. Enemy return

Recovery → поворот → движение → точный slot/facing → IdleSettled. Следующая фаза ждёт реальные lifecycle flags. Исправлен найденный deadlock: отменённый Core suffix не посылает HitResolved, поэтому pending contact явно отменяется без contact/audio/hit-stop, с сохранением authored tail. Return очищает sequence и предотвращает дальнейшие swing events отменённого suffix.

### 17. Normal stance

Компактная двуручная shoulder guard отличается от Heavy. Selection presentation .65 s; approach несёт оружие в ready position. Существующий corrected Normal attack сохранён: 1.1 s, contact .6 s, Core-resolution compensation .04 s. На runtime последовательности видны backswing и быстрый forward arc; прежнего продолжительного скольжения с клинком вперёд на проверенных действиях нет.

### 18. Heavy stance

Более высокая двуручная overhead preparation, selection .9 s. Существующий corrected Hard Jump take сохранён: 1.4333 s, contact .8667 s, compensation .09 s. При подходе stance отделена от последующего jump/downstroke. Horizontal motion управляется arena; вертикальный jump остаётся в clip.

### 19. Camera selection

Push-in Normal 6.5%, Heavy 9% показывает разные стойки. Камера возвращается за .26 s к authoritative framing до approach. На contact — небольшой impulse; после возврата проверены position `(0, 2.25, -20)` и ortho size `4.6`, без накопления offset.

### 20. Normal audio / VFX

Vocal, acceleration whoosh и actual-contact sound разделены. Ribbon строится по истории реального blade segment, contact particles привязаны к ближайшей точке blade у поверхности body bounds. Cyan impact, recoil, hit-stop .08 s и HP feedback разрешаются одним contact window. Визуально просмотрены contact и recovery; автоматизированно проверена доставка semantic events.

### 21. Heavy audio / VFX

Отдельные Heavy vocal/whoosh/contact, более выраженные trail/charge и purple contact. Hit-stop .10 s. Contact располагается у настоящего клинка и тела; Counter больше не получает этот Heavy effect по чужому action ID. Heavy anticipation/jump/contact/return просмотрены отдельно.

### 22. Monster audio / impact

Vocal и swing отделены от body impact. Контакт определяется по фактическим wrist/finger/claw transforms у ближайшей поверхности Keiko, а не по скрытому центру actor root. Particle Z вынесен к видимой поверхности skin. Normal и jump-claw contact не требуют подъёма monster root. Body hit снят отдельно от защит.

### 23. Block

В Blender исправлен двуручный hanging guard: hilt поднимается, направленный вниз клинок закрывает фактическую низкую траекторию claws меньшего Yokai. Старый высокий guard проходил над когтями. Take .8 s, contact .34 s; prepare/recoil/recovery не перезапускаются после контакта. Golden sparks расположены в blade/claw contact; opacity выставляется сразу даже при hit-stop dt=0. Flesh SFX подавлен. Runtime contact просмотрен.

### 24. Dodge

Двуручный backstep take .8 s, contact .24 s. Model offset плавно достигает .78 m и возвращается, logical slot остаётся authoritative. Enemy strike проходит через старую позицию; успешный Dodge не даёт body impact и сохраняет HP. Бейк planted-foot компенсации использует фактический Unity scale **3.6006691456**, yaw 65°, SetStandingHeight(4.0), inverse yaw по обеим X/Z осям. Source drift < .00015 m. Runtime backstep/landing/return просмотрены.

### 25. Transparent square — root cause

В Reactive HUD были нетекстурированные `Image` overlays `ReactiveHunterImpact` размером 480×365 и `ReactiveEnemyImpact` 652×395 с alpha .08/.07. Они давали прямоугольный flash поверх сцены. Это отдельный UI дефект, а не EmberGen flipbook.

### 26. Transparent square — fix

Удалены два Reactive impact rectangles. Hit presentation использует world-space blade trail и типизированные contact particles с правильной alpha/edge masking. Старый flash в отдельном legacy combat пути не изменён. Нет runtime debug hitbox visualization.

### 27. Clean captures

На сохранённых Normal, Heavy, monster body hit, Block и Dodge кадрах лишних прозрачных прямоугольников нет. Чистые кадры и motion sequences перечислены в [VisualReview](VisualReview/README.md).

### 28. EmberGen

EmberGen exports в финальной реализации не использованы. Применены Unity procedural portal, blade history ribbon и controlled particles. Нет flipbook carrier с чёрным/серым фоном.

### 29. Blender

Использованы 11 corrected source takes для preparation, locomotion, entrance/settle, Block, Dodge и Yokai claws; список и параметры — [AnimationRepair](AnimationRepair/README.md). Roundtrip: **11/11 PASS**, Keiko 33 bones / Yokai 65 bones. Сохранены 26 authoring previews и recipe. Только Dodge переэкспортирован после фактической Unity scale calibration; остальные правильные takes повторно не пересоздавались. Existing corrected Normal/Heavy attacks сохранены. Editable `.blend` остаются в `D:/Rokas/reactiveturns-b2-staging/polish-ii-animation`.

### 30. Core exact results

Свежий общий console runner: **1/1 PASS, exit 0** после последних production fixes. В логе сохранены named checks Combat2, ReactiveTurns и остальных domain behavior. Это один runner, не придуманное число NUnit cases. [Core.log](Tests/Core.log).

### 31. EditMode exact results

| Реальный запуск | Total | PASS | FAIL | SKIP |
| --- | ---: | ---: | ---: | ---: |
| Full EditMode | 943 | 938 | 5 | 0 |
| Последний relevant EditMode | 37 | 37 | 0 | 0 |

Relevant: CombatFoundation 23, FirstLoopDomain 3, Reactive save codec 1, Sword assets 10. Full failures: HubDialogue default width; VN external PNG runtime export IOException; MG inspector section count; VN focused-background preview wiring; VN authored-character picker. Эти исходники не изменены в pass. Детали и фактические XML — [Tests](Tests/ResultsSummary.json).

### 32. PlayMode exact results

| Реальный запуск | Total | PASS | FAIL | SKIP |
| --- | ---: | ---: | ---: | ---: |
| Final focused batch | 55 | 54 | 1 | 0 |
| Heavy после исправления early-input fixture | 2 | 2 | 0 | 0 |
| Long-run | 1 | 1 | 0 | 0 |
| Compatibility smoke | 39 | 27 | 12 | 0 |

В final batch единственный FAIL был в early-input предпосылке fixture (первый UI frame уже 131253 us при требовании <100000). Исправленный fixture проверен отдельным Heavy 2/2 запуском. **Latest results для 55 уникальных focused cases: 55 PASS / 0 FAIL**, это агрегат двух настоящих запусков, а не отдельный зелёный запуск 55/55. [Агрегат](Tests/LatestCombatCaseResults.json).

Final batch дополнительно включает Motion 4/4, Audio 9/9, PolishII 6/6, FX 5/5, HUD 4/4, cinematic FX 7/7, Sword 4/4, Reaction 3/3, Duel 2/2, Input 2/2, Arena visuals 3/3, EightEnemy 1/1, FullLoop 1/1, Wave 1/1, Capture 1/1.

Smoke: legacy Combat **16/16 PASS**, Laptop contract lifecycle **1/1 PASS**. Остальные результаты: MainMenu 5 PASS/3 FAIL (Home object/audio/handoff не дождались); Messages 0 PASS/4 FAIL (unexpected существующий Home info log); VN intro 5 PASS/5 FAIL (не найден RokasMainMenu в startup wait). Эти failures не скрыты и не исправлялись изменениями вне scope.

### 33. Capture / visual review

Capture test **1/1 PASS**. Последний manifest содержит **147 реальных Unity кадров**; скопированы именно его записи, без подсчёта старых файлов по glob. Просмотрены entrance, последовательные portals, разные stances, Normal/Heavy arc, body hit, Block sparks, Dodge, точные возвраты и death fall/hold/dissolve. Четыре contact sheets помогают видеть порядок движения. Visual judgement отдельно от unit assertions; still/frame review не заменяет слуховую оценку.

### 34. Long-run

**1/1 PASS, 342.0385 s**: Normal×5, Heavy×5, десять alternating commands, четыре дополнительных Normal для AP; два encounters, несколько defenses и три волны первого encounter до Victory. Использованы реальные UI pointer click и InputSystem keyboard через Bootstrap; Core HP/AP/clock не форсировались. Каждый counted command проверяет фактический Core damage, exact home/facing, IdleSettled, один sword child и camera restore. Long-run выполнен до последней visual scale/cadence calibration и CancelPendingContact API; эти последующие изменения проверены финальными focused scenarios/captures и fresh Core/relevant EditMode. Отдельный повтор long-run на последнем состоянии не выдаётся за выполненный.

### 35. Validator

**132 historical / 132 final / 0 added / 0 removed**. Literal validator exit **1** в обоих состояниях из-за historical findings; regression comparator exit **0**. Это PASS сравнения, а не абсолютный validator exit 0. Baseline использует tracked inventory/content initial HEAD и одинаковые четыре preexisting ignored HubDialogue assets для обеих сторон. Их сведения сохранены; unrelated EditMode tests обновляли config timestamps. [Сравнение](polish-ii-validator-comparison.json), baseline/final logs сохранены рядом.

### 36. git diff --check

Финальный checkpoint создаётся только после literal `git diff --check` и `git diff --cached --check` с exit 0 и пустым output. Удаляется только trailing whitespace в изменённых scoped text assets; смысл Unity serialized data сохраняется. Точный результат после commit публикуется в финальном сообщении.

### 37. git status

До commit проверяются staged/unstaged scope и stat. После commit фактический `git status` и exact SHA публикуются в финальном сообщении. Существующие ignored пользовательские assets и внешние evidence сохранены; checkpoint не направлен в main/development/integration и не отправлен на remote.

### 38. Реальные ограничения

- Full EditMode содержит 5 FAIL, compatibility smoke — 12 FAIL вне Combat; полный проект не прошёл зелёный integration/release gate. Их baseline происхождение отдельно не доказано.
- Live hearing и субъективная оценка audio mix не выполнены; подтверждены headers/import/samples/event delivery/deduplication и visual contact alignment.
- Точный `IMG_9694.MP4` и указанный bandicam capture не найдены. Использованы доступные sword/portal videos и собственные runtime sequences; совпадение с отсутствующим reference не заявляется.
- Monster claw archive содержит WAV, не новые FBX; corrected claw motion сделан из существующего Yokai skeleton/takes.
- Dodge planted-foot bake рассчитан для текущего Keiko scale/yaw/backstep. Другие stature/weapon rigs требуют новой калибровки.
- Проверка была реальным PlayMode с automated input/captures и просмотром последовательных кадров. Отдельный пользовательский manual play session, standalone release build и canonical integration handoff в этой итерации не выполнялись.
