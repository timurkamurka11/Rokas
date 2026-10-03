# ReactiveTurns — Final VFX / EmberGen / Throw: Stage 12 recovery

## 1–4. Checkpoint и границы

- Recovery HEAD: `0eecaaf7ab62afbf5c949430f08cd1b175c9edc4`.
- Recovery TREE: `2da736a6b56de0037113efd7e294ebbc48b6da27`.
- Branch: `r11-finished-ui`.
- Проверяемая рабочая версия: `D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY`.
- Final HEAD/TREE записываются после commit в итоговом ответе и внешнем `D:/Rokas/reactiveturns-b2-staging/final-vfx-unity/final-checkpoint.json`. Этот документ входит в сам checkpoint и не содержит самоссылочного SHA.

Current-scope Combat проверки GREEN. Это feature checkpoint. Canonical `D:/Rokas/Rokas` и `integration/rokas-unified` в этой continuation не обновлялись; готовность общей интеграции для routine manual QA не заявляется.

## 5–6. Предыдущие 70/77: все семь failures

Исходный валидный XML сохранён: [previous-70-of-77.xml](RegressionEvidence/previous-70-of-77.xml). Exact expected/actual и классификация: [FailureClassification.json](RegressionEvidence/FailureClassification.json). Все семь прошли focused recovery и финальный relevant run.

| Test (Rokas.Tests) | Категория / причина | Исправление |
| --- | --- | --- |
| ReactiveArenaVisualPlayModeTests.ArenaCanBeCapturedAtAuthoredAndSmallScreenSizes | B: один click теперь stationary preview, X оставался -5.25 | Проверка неподвижного preview, второй настоящий UI click; сохранены approach/return/audio/resolution assertions |
| ReactiveDuelPlayModeTests.FocusAndSettingsPauseFreezeCombatThenResume | B: focus loss до camera-confirm commit отменял ещё не начавшуюся атаку | Дождаться committed PlayerExecution и approach; сохранить проверки остановки часов/позиции и возврата |
| ReactiveDuelPlayModeTests.PortalOffersPlayableReactiveDuelWithProjectArt | B: second click ошибочно считался синхронным PlayerExecution | Проверить pending confirmation, дождаться base camera / actual commit; сохранить defense/revision assertions |
| ReactiveEightEnemyPlayModeTests.PortalBuildsThreeAnimatedEnemiesAndPersistsTheChosenCommandTarget | B: ожидание немедленного commit | Дождаться camera restoration; сохранить persisted target, target lock, damage/save/reload проверки |
| ReactiveHeavyTimingPlayModeTests.HeavyAcceptsOneTimedDevicePressAndKeepsItsCommittedTarget | B: Basic setup и Heavy ожидали immediate execution | Дождаться реального commit; сохранить device press, AP, timing, damage и target assertions |
| ReactiveHeavyTimingPlayModeTests.HeavyRejectsEarlyAndLateDevicePressesAndStillSettles | B: та же устаревшая confirmation semantics | Тот же lifecycle wait; early/late rejection и settle сохранены |
| ReactivePolishIILiveLongRunPlayModeTests.FiveNormalFiveHeavyThenTenAlternatingLiveCommandsCompleteWithDefenseAndExactReturns | C: previewChecked переживал encounter; новая arena ещё имела 7 cold carriers, а test ожидал 10 warmed | Сброс warm-check на каждой arena; реальные три preview/cancel, exact count 10 и disabled-renderer проверки сохранены |

B — outdated expectation; C — harness defect. Ни один из этих семи не исправлялся ослаблением Core damage/return/defense контракта.

## 7–8. Attack-start и return

Первый click только выбирает action/stance и удерживает camera preview; AP, HP и Core revision не меняются. Второй click той же action подтверждает; переключение заменяет preview; Escape/right-click отменяют его. Commit происходит после восстановления base camera. Затем preparation → approach (melee) / release (Throw) → contact → recovery → exact slot/facing → idle settle → следующий presentation.

Player/enemy controls и следующий actor gated до полного return/settle. Проверены Keiko и enemy, focus/settings pause, target persistence, Heavy real device timing, waves/victory, legacy duel и full cycle. Combat Core не переписывался.

## 9–10. VFX counter и cleanup

Counter должен различать реальные active effects и reusable carriers. Cold arena содержит 7 Ember carriers, Throw добавляет 3 лениво. После прогрева стабильно 10; все их renderers выключены на settlement. GameObjects внутри pool допустимы.

Final long-run также требует: inactive projectile; disabled charge/trail/contact lines; zero particles у ContactSparks, ContactVapor, ThrowReleaseAndFlightMotes. Независимая death ash исключена из преждевременного нулевого particle count. После уничтожения старой arena подтверждено уничтожение её carriers и owned meshes/materials; общие Resources atlases остаются доступны.

## 11. Torso/bone lookup: дополнительный реальный runtime defect

Throw goal раньше вычислялся через ClosestPoint inflated skin bounds от release-hand position; projectile/contact проходили выше тела. Теперь live Spine2 (production fallback Spine1/Spine, затем body center) задаёт X/Y contact при release; существующий front-Z exposure сохранён. Общий melee/Block helper не менялся.

Новый regression сначала дал RED на старом runtime: expected torso X1004.0656, actual1001.1465; endpoint Y1002.41 был выше Head Y1002.274. После минимального runtime fix — GREEN, endpoint X/Y совпали с live torso.

Test helper теперь ищет bones в фактическом ReactiveCombatWorld, расположенном отдельно от UI root; namespace-independent suffix допускает rig prefix. Test требует реальные Spine2 и Head, не маскирует их отсутствие fallback. [RED XML](RegressionEvidence/throw-torso-red-results.xml), [GREEN XML](RegressionEvidence/throw-torso-green-results.xml).

## 12–14. Throw regression / visuals / cleanup

Throw presentation suite: **7/7 PASS** в focused/full run. Preview suite: **6/6 PASS**. Дополнительный fresh switch/cancel/retry run: **1/1 PASS**. Проверены Throw→Heavy, Throw→Normal, Cancel, повторный Throw после cancel, AP/HP/revision, скрытие held prop, one projectile/contact/damage, audio semantic layers, exact recovery и retirement всех effects.

Blender assets сохранены: game-ready dagger 2268 triangles / 5 materials; current 33-bone rig, preparation 48 frames/.4s, attack132 frames/1.1s @120fps. Release frame51/.425s, contact .785s, flight .36s. Prefab: `Assets/Rokas/Art/CombatActors/Weapons/KeikoThrowingDagger/KeikoThrowingDagger.prefab`. Sources и roundtrip proof: [AnimationRepair](AnimationRepair/README.md).

RH socket dagger: `(0,.033,0)`, mesh rotation X90, scale1. Sword во время Throw stowed на Spine2 `( .06,-.08,-.14 )`, направлен вниз за корпусом, затем возвращается WeaponSocket_R. Уникальные release/contact audio aliases, 22050Hz stereo PCM16; повторный semantic dispatch подавляется.

Реальные PlayMode captures финального run просмотрены: Throw preview, release, mid-flight, target torso contact, blue impact, recovery. Sword не пересекает голову/руку, dagger не дублируется, trail следует projectile, нет teleport/прямоугольного VFX. [Throw sequence](VisualReview/throw-detail-sheet.jpg), [preview/recovery](VisualReview/preview-throw-sheet.jpg).

## 15–17. EmberGen / portal / differentiated effects

Работает настоящий EmberGen **1.2.6** из `D:/3DMODELS/EmberGen`; семь native CLI exports exit0 реально используются Unity. Sources, presets/settings, export logs, hashes и alpha/framing proof: [NativeExportReview](EmberGen/NativeExportReview.md), [Manifest](EmberGen/Manifest.json).

| Export | Source preset | Size / frames | Runtime роль |
| --- | --- | --- | --- |
| PortalVapor | wispy_smoke_1 | 2048² /64 | Два wispy vapor слоя formation/emergence/residual/collapse |
| NormalMist | wispy_smoke_1 | 1024² /64 | Тонкий cyan slash/contact support; continuous preparation support |
| HeavyBurst | gas_explosion_01 | 1024² /64 | Более плотный purple .52s contact |
| ThrowEnergy | magic_projectile | 1024² /64 | Hand charge, release .22s, flight .36s |
| ThrowImpact | game_explosion_magic_1 | 1024² /64 | Компактный cyan .28s projectile contact |
| BlockBurst | smoke_shockwave_2 | 1024² /64 | Localized gold .24s blade contact support |
| ClawImpact | bullet_impact_concrete | 1024² /64 | Monster physical contact support |

PNG RGBA, native premultiplied alpha, 8×8 grid; native export bytes сохранены. Все 448 frame borders имеют alpha0. Zero-alpha RGB ThrowEnergy сохранён, shader clip предотвращает carrier rectangle. Unity sRGB / no mips / Clamp / Bilinear / BC7; half-texel inset, correct top-first frame, One/OneMinusSrcAlpha. Arena RawImage использует отдельный premultiplied UI shader, исключающий alpha².

Portal сохраняет depth/rim/seed/particles/distortion/clipping, реальные native vapor layers, последовательный creature crossing/readiness gate и collapse. Portal sequence просмотрена: [portal-sequence](VisualReview/portal-sequence.jpg). Native smoke не перекрывает героя целиком. Normal/Heavy/Throw имеют разные trails/contact масштабы и цвет; Block/monster contact идут из реальной точки касания. [contacts/cleanup](VisualReview/contact-and-cleanup.jpg).

GPU atlas budget около10MiB без mipmaps; bounded10 warmed combat carriers, entrance с одним portal — максимум12. Hard GPU frame-time profiling не выполнялся. Установка/лицензия EmberGen не модифицировались.

## 18–20. Exact automated evidence

| Run | Passed / total | Failed | XML в RegressionEvidence |
| --- | --- | --- | --- |
| focused-recovery | 23/23 | 0 | focused-recovery-results.xml |
| reactive-final | 78/78 | 0 | reactive-final-results.xml |
| edit | 34/34 | 0 | edit-results.xml |
| longrun-final | 1/1 | 0 | longrun-final-results.xml |
| throw-switch-final | 1/1 | 0 | throw-switch-final-results.xml |
| throw-torso-green | 1/1 | 0 | throw-torso-green-results.xml |
| preview-throw-vfx-final | 21/21 | 0 | preview-throw-vfx-final-results.xml |

Final relevant PlayMode: **78/78 PASS, 0 skipped**, Unity exit0, valid XML, 2026-10-03 10:40:14Z → 2026-10-03 10:53:15Z, duration 780,5118667s. Это original77 плюс torso regression; все previous7 GREEN. [final XML](RegressionEvidence/reactive-final-results.xml).

Core: **49 named tests PASS**, compiler/host exit0; [Core-final.log](RegressionEvidence/Core-final.log). Python validator tests: **13/13 PASS**. Ember lifecycle6 и actual GPU composite3 входят в final PlayMode; unit tests не доказывают visual quality.

Полный широкоформатный EditMode: **938/943 PASS, 5 unrelated failures**, exit2. [XML](RegressionEvidence/all-editmode-938-of-943.xml), [read-only classification](RegressionEvidence/unrelated-editmode-review.md). Hub выбирает ignored user override y232 вместо default128; три VN assertions противоречат identical baseline source; VN CopyAsset failure в unchanged protected code — точная IO/harness причина и историческое воспроизведение не доказаны. Полный EditMode НЕ заявляется GREEN.

## 21–22. Final long-run и actual review

Fresh strengthened long-run **1/1 PASS**, затем повторён на том же финальном коде внутри full78/78. План20 counted commands: 9Normal, 8Heavy, 3Throw, плюс fillerNormals; два encounters, multiple waves/portals, real device Block/Dodge/Heavy timing, preview switch/cancel. Проверены real damage, exact home/facing/base camera, no stuck phase, no duplicate audio/projectile/contact, no active VFX/trail/particle leak и owned resource disposal.

Captures финального run сохранены как реальные PNG с hashes/mtime: [OriginalCaptures](VisualReview/OriginalCaptures.json), [Combat timing manifest](VisualReview/CombatCaptureManifest.json). Contact sheets сделаны только из этих кадров без дорисовки. Review включает Portal/Heavy/Block, Throw flight/contact и post-action cleanup; visible black box, carrier border, matte rectangle не найден.

Оба доступных reference MP4 фактически просмотрены ранее: `Example of monsterr apper and animations of portal.MP4`, `Keiko throw a sword.MP4`. Перенесены staged emergence, wind-up/release/flight/contact принципы. Portal остаётся сдержанным фиолетовым oval с умеренной depth; равенство высококачественному reference не заявляется. Sequence capture review не заменяет человеческую субъективную QA всего cinematic качества.

## 23. Validator old/new

Recorded R11 recovery reconstruction: **132 findings**. Final: **136**. Добавлены4 уже существующих unrelated Workbench reference PNG below-HD findings; **0 NEW current-patch findings**. Raw comparator exit1 отражает эти4 unrelated additions и не скрывается. Правило Ember PNG ограничено Combat/ReactiveTurns/Vfx/EmberGen и проверяет resolution/grid/RGBA/native carrier requirements. Историческое canonical значение13 не переносилось на R11 без проверки.

## 24–25. Git gates и сохранение пользовательской работы

Literal `git diff --check`: **exit0, empty output** перед commit; [literal proof](RegressionEvidence/literal-diff-check.log). Stage включает только current Combat implementation/assets/tests/evidence. Shared RokasView staged частично: preview forwarding/action click; unrelated Workbench и Home changes остаются в рабочем дереве. BOM/CR/trailing-space cleanup имеет byte backups и не включён в feature commit для unrelated файлов.

`git diff --cached --check` также exit0 / empty. В восьми скопированных evidence файлах удалены только trailing spaces/нормализованы line endings (Ember ASCII banners и один XML stack trace). Исходные run files вне проекта сохранены byte-for-byte; XML result/test attributes и failure messages не изменились. Exact before/after hashes: [evidence-whitespace-changes](RegressionEvidence/evidence-whitespace-changes.json).

Unity-generated orphan runner scenes и empty EditMode fixture leftovers проверены по timestamp/path/content, архивированы с SHA вне Assets. Пользовательские scenes/assets/source не удалены. Git status после commit отражает сохранённые unrelated изменения; full working tree clean не заявляется. Exact final status сохранён внешним checkpoint JSON.

## 26. Реальные ограничения

- Широкий EditMode остаётся 938/943, один protected VN CopyAsset harness/IO defect не локализован окончательно.
- Unrelated пользовательские изменения сохранены; full working tree не clean.
- Это R11 feature checkpoint; canonical unified integration/full integrated suites/manual QA ещё не выполнены в этом pass.
- Portal reference fidelity и аппаратный GPU frame-time не подтверждаются unit tests или приведённым texture budget.
