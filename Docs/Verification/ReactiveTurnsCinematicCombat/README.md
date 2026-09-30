# ReactiveTurns — финальный cinematic combat pass

Дата: 30 сентября 2026. Unity: **6000.3.19f1**.

Рабочий проект: **D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY**. Ветка: **r11-finished-ui**.

## Checkpoint

- Initial HEAD continuation: `53aa206a99d52bcb3f3234239fa7289fa6ba6ce2`.
- Initial TREE: `af5a46cd44dde9d2e83d00b6cfa282bdee5183f9`.
- Проверенная реализация: `de14922679d3c65fb76d8681e21f79cd2a560389`.
- TREE реализации: `df2cf8a37712770971f42720c69cd04b2bf40677`.
- Этот отчёт добавляется отдельным evidence commit. Его окончательные HEAD/TREE, чистый status и literal diff check записаны после commit в `D:/Rokas/reactiveturns-b2-staging/cinematic-final-checkpoint.json` и финальном сообщении чата. Это исключает самоссылку SHA внутри собственного commit.

## Результат continuation — 27 пунктов

1. **Исходное состояние.** Продолжение началось с указанного HEAD и существующего незакоммиченного pass. Этапы 1–10 не запускались заново. Ранее готовые UI, очередь presentation, исправленные FBX, intro, порталы, reaction panel, ash death и camera emphasis сохранены.
2. **Final HEAD.** Полный SHA после evidence commit находится в финальной записи checkpoint; implementation SHA указан выше.
3. **Final TREE.** Полный SHA записывается вместе с Final HEAD после последнего commit.
4. **Branch.** `r11-finished-ui`; новая ветка не создавалась.
5. **До continuation.** Уже существовали clean HUD, компактные карточки целей, блокировка следующего актёра до возврата, двуручные authored takes, вход Кейко, последовательные враги, reaction UI, dissolve и focused fixtures. В continuation завершены последние pose fixes и их фактическая проверка.
6. **Idle root cause.** Исходный idle содержал заметное движение всего тела. Дополнительно автоматическое Legacy Animation на импортированной модели могло перезаписать вручную выставленную позу между Update и рендером. В runtime хват отличался от корректного прямого семплирования FBX.
7. **Idle fix.** В Blender создан стабильный grounded stance с дыханием кистей 2 мм и совпадающими концами loop. Runtime выключает autoplay/native animation и семплирует выбранные clips напрямую; переходы смешивают локальные transform poses за 0,08 с. Поза и часы полностью принадлежат arena presenter. Накопительного reset корпуса или слота для маскировки idle не добавлено.
8. **Heavy root cause.** Исходный Hard jump attack содержал лишние полные вращения Hips вокруг Y, мешавшие направлению атаки и видимости рук/клинка.
9. **Heavy fix.** Authored yaw ограничен диапазоном −50…+10°; сохранены lift, колени, прыжок, landing и hip pitch/roll. Голова направлена к противнику. После return восстанавливаются точный slot, исходный facing и устойчивый idle.
10. **Normal.** Ready → anticipation → backswing → быстрый forward arc → contact → follow-through → recovery. При approach меч находится в поднятой двуручной carry pose; swing начинается в strike range. В просмотренных реальных кадрах бывший паттерн «медленное движение с мечом вперёд как копьём» отсутствует.
11. **Heavy.** Отдельная подготовка, выраженный overhead backswing, ускоренный удар и более сильный hit stop. Неконтролируемого вращения корпуса в финальной последовательности не видно.
12. **Двуручный хват.** Обе руки реально анимированы во всех восьми clips. Raw clip sampling проверяет ладонь без runtime correction. Небольшой support-arm solver удерживает palm на `LeftHandGrip` во время crossfade и сохраняет authored wrist orientation. Front/side/three-quarter и крупный кадр хвата просмотрены.
13. **Intro.** Поле первоначально без врагов. Кейко входит слева за 2,4 с, занимает slot, проигрывает исправленный `Enter the batle` transition 1,2 с, затем settle. Используется прежняя `Keiko@Idle` character model.
14. **Portals.** Каждый враг появляется отдельно: portal opening 0,35 с → движение к slot 0,75 с → закрытие 0,3 с → settle. Исправлен alpha blend в прозрачной actor texture. На actual arena captures виден тёмный портал с тонким пурпурным ободом; он не закрывает всю сцену.
15. **Turn sequencing.** Core может логически выбрать следующего актёра раньше конца visuals. Именно немедленная обработка его AttackStarted раньше создавала overlap. Теперь next-actor events/sequence удерживаются, combat clock и input закрыты до полного recovery → return → exact Home → IdleSettled → beat → announcement fade. Victory/Defeat также завершают нужный return при отсутствии ActionSettled. New waves ждут portals; focus/settings pause сохраняет hold и получает новый input epoch.
16. **Reaction UI.** Compact bar и Dodge/Block находятся снизу по центру; timing X=690, Y=668 в authored 1920×1080 layout. Старое крупное «Нажми вовремя» artwork выключено. Panel появляется только у фактической incoming presentation и исчезает после атаки/при hold.
17. **Target cards / HUD.** Карточки 205×43, над врагами, без портретов. Доступны для выбора только в разблокированный player command. Скрыты во время action, return, reaction и announcements, затем возвращаются. Home chrome, постоянный forecast, wave/debug clutter и нижний prototype UI скрыты в combat; вне combat функции сохранены.
18. **Death.** Логическая смерть и исключение из targets/turn order происходят сразу. Visual fall 1,35 с → hold 1,0 с → dissolve 1,2 с → cleanup. Shader clips стабильным fragment noise, сохраняет material data; dark ash берётся с поверхности skinned mesh. Корневое положение и scale тела не опускаются под землю.
19. **Combat Core.** Console runner **1/1 PASS**, exit 0. Выполнены **23/23** отдельно логируемых CombatFoundation сценария и **16** групп RunAll (ReactiveTurns, save, waves, progression, Messages и другие domain regressions). Это счётчики запусков/групп, не выдуманное число внутренних assertions. Core sources не изменены.
20. **Focused EditMode.** **30/30 PASS**, 0 failed, 0 skipped: Sword assets 3; Unity save codec 1; CombatFoundation 23; FirstLoopDomain 3.
21. **Focused PlayMode.** **34/34 PASS**, 0 failed, 0 skipped. Включены Core input adapter, legacy duel, eight enemies/target save, full loop, all waves/victory, Heavy input timing, one-SFX dispatch, presentation lifecycle, motion, grip, HUD, FX и live capture. Точные имена и XML сохранены в `results.json`, `playfocus-results.xml`.
22. **Visual review.** Просмотрены chronological sequences из **108 реальных PlayMode кадров**, 1920×1080 key shots, 960×540 motion frames, 1280×720 resolution capture, model-relative studies и render FX checks. Проверены entrance, idle, Normal backswing/contact/recovery/return, Heavy anticipation/contact/facing, enemy attack, Dodge, Block, missed hit, карточки и ash. Gallery/GIF используют реальные кадры существующего bootstrap/Core/Input System, без вручную выставленных HP/clock/slots.
23. **Validator.** **132 existing / 0 new / 0 removed** относительно initial HEAD и четырёх нетронутых ignored HubDialogue user assets. Raw validator exit 1 из-за этих существующих findings; comparison exit 0. C# runtime/compilation/shader error scan финальных Unity logs пуст.
24. **git diff --check.** Literal unstaged и staged checks перед implementation commit: exit 0, пустые stdout/stderr. Финальный check повторён после evidence commit и записан в final checkpoint.
25. **git status.** Implementation commit содержит только combat presentation/assets/tests. Финальный clean status проверяется после evidence commit. Значимые пользовательские данные и старые evidence сохранены.
26. **Final project.** `D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY`. BAT, review clone и отдельный prototype project не создавались. Push/merge не выполнялись.
27. **Ограничения.** Visual review выполнен по отрисованным Unity PlayMode motion frames и GIF, без отдельного интерактивного видео-сеанса. Остаточных дефектов данного pass в проверенных последовательностях не обнаружено. Полные unrelated Home/VN/Messenger Unity suites не перезапускались по актуальной recovery-инструкции; их исторические результаты из предыдущего pass не считаются текущими failures или текущим PASS. Dense animation sources оставлены несжатыми для точного хвата.

## Точные timings и transforms

| Параметр | Normal | Heavy |
|---|---:|---:|
| Authored preparation | 0,2667 с | 0,4000 с |
| Arena approach | 0,84 с | 0,84 с |
| Полный attack clip | 1,1000 с | 1,4333 с |
| Максимальный backswing, clip time | около 0,44 с | около 0,68 с |
| Contact, clip time | 0,6000 с | 0,8667 с |
| Быстрый swing до contact | около 0,1667 с | около 0,1867 с |
| Contact hit stop | 0,08 с | 0,10 с |
| Оставшаяся attack recovery после contact | 0,50 с | 0,5667 с |
| Arena return | 0,72 с | 0,72 с |

Presentation preplays windup while Core clock is held, затем сохраняет исходный Core contact через 0,2 с. Return ждёт действительного `ActionRecoveryComplete`; следующая атака ждёт `IdleSettled` и fade. Минимальный result linger 0,26 с и settle beat 0,16 с дополняют lifecycle, не заменяют его случайным ожиданием.

- `WeaponSocket_R`: local position `(0, .033, 0)`, Euler `(0, 0, 0)`, scale `(1, 1, 1)` на прежней RightHand bone.
- `LeftHandGrip`: local position `(0, 0, -.055)`, Euler `(0, 0, 180)`, scale `(1, 1, 1)`.
- `SwordMesh`: local position `(-.13434848, -.113350056, .5002808)`; quaternion `(-.540639, -.45591643, 0, .70700055)`; scale `(.55, .55, .55)`.
- Horizontal travel принадлежит arena. Horizontal Hips curves и Blender Armature wrapper удалены из standalone clips; jump Y сохранён. Native root motion выключен. Авторитетный model facing 65°; возврат проверяет точное исходное положение/вращение.
- Camera easing: Normal 6,5%, Heavy 9%, небольшой attacker/target reframe; после действия возвращается общее framing. Generic click SFX у attack controls отсутствует; каждый contact dispatch один раз, animation audio events пусты.

## Длительная проверка

`FiveNormalFiveHeavyAndTenAlternatingAttacksStaySettledWithoutDrift` проходит:

- 5 Normal подряд, 5 Heavy подряд, ещё 10 alternating;
- после каждой атаки точный slot/rotation, IdleSettled, двадцать дополнительных idle samples без root wobble, удержание support palm, единственный sword;
- остановленная presentation не меняет clip time; Core commit не перезапускает windup;
- ещё четыре enemy actions с exact return;
- отдельный actual bootstrap test проверяет запрещённые команды/Q в hold, остановленные Core time/HP, оба объявления и возвращение UI;
- live capture проводит реальные Q Dodge и E Block, затем Heavy и dissolve; дальнейшие missed hits также видны в capture.

## Blender и tooling

Исправлены восемь derived FBX и сохранены восемь editable `.blend` в `D:/Rokas/reactiveturns-b2-staging/cinematic`. Blender **5.2.2 LTS**, установленный executable `D:/3DMODELS/blender.exe`. Оригинальные Normal/Heavy FBX сохранены; supplied `Enter the batle.fbx` импортирован отдельно и используется как animation source.

Recipe, bake report, roundtrip/fractional checks находятся в `AnimationRepair/`. SHA-256 всех восьми текущих FBX совпадает с bake report. Каждый clip полностью привязан: **295/295** bindings к существующему rig. 120 fps keys, no resampling/compression применяются только к восьми исправленным источникам. Mina/Yokai clips и оригинальные import policies сохранены.

Одноразовые `edit_*`/`update_*` migration scripts оставлены в внешнем staging, не внесены в Assets/runtime. Полезные verification launchers и gallery также остаются там; этот каталог содержит результаты и authoring tooling, не копию Unity project. Ignored HubDialogue user assets проверены по сохранённым hashes.

## Evidence

- [Точные результаты и hashes](results.json).
- [EditMode XML](edit-results.xml), [PlayMode XML](playfocus-results.xml), [Core log](polish-core.log).
- [Validator comparison](cinematic-validator-comparison.json).
- [Capture manifest](capture-manifest.json) с durable raw paths и hashes; raw frames сохранены в `D:/Rokas/reactiveturns-b2-staging/cinematic-visuals/raw`.
- [Normal motion GIF](Screenshots/normal-motion.gif), [Heavy motion GIF](Screenshots/heavy-motion.gif).
- [Intro sequence](Screenshots/intro-sequence.png), [Normal sequence](Screenshots/normal-motion-sequence.png), [Heavy sequence](Screenshots/heavy-motion-sequence.png).
- [Two-hand close-up](Screenshots/rokas-sword-grip-close.png), [Stable arena idle](Screenshots/stable-idle-after-dodge-block.png), [Portal](Screenshots/intro-018.png), [Dissolve](Screenshots/corpse-dissolve.png).

Все screenshots сделаны рендером Unity, а не image generation.
