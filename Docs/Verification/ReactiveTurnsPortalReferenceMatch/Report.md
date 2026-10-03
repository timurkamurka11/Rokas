# ROKAS — Portal Reference Match Pass

## 1–3. Checkpoint и scope

- Initial HEAD: `33fd9a9f03f54283dd6b51f1c208ded152eac0a6`.
- Branch: `r11-finished-ui`.
- Authoritative project: `D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY`.
- Exact Final HEAD/TREE и результат проверки commit сохранены после commit в `D:/Rokas/reactiveturns-b2-staging/portal-reference-match/FinalCheckpoint.json`; они также приведены в финальном ответе.
- Изменения ограничены portal rendering, emergence mask, entrance presentation timing, focused tests и evidence. Combat Core, атаки, damage, UI, audio, save, VN/Home не изменены этим pass.
- Проверялось фактическое R11 рабочее дерево. Все 84 ранее изменённых пользовательских файла сохранены по SHA256 и исключены из commit. Это не проверка отдельного чистого checkout и не unified integration handoff.

## 4–6. Reference и фактические timings

Reference: `D:/3DMODELS/Example of monsterr apper and animations of portal.MP4`.

Видео заново воспроизведено на скорости 1×. Разобраны все 238 реально декодированных кадров по native PTS, десять sequence sheets и кадры границ фаз. Предварительная CFR последовательность с дублированными кадрами исключена из timing анализа. SHA256 и подробные observations находятся в [TimingBreakdown.md](TimingBreakdown.md).

| Фаза | Absolute reference time | Local time от seed |
|---|---:|---:|
| Floor seed | 0.800 s | 0.000 s |
| Black kernel | 0.900 s | 0.100 s |
| Broad ribbons | 1.500 s | 0.700 s |
| Formed aperture | 2.900 s | 2.100 s |
| First monster reveal | 3.566667 s | 2.766667 s |
| Clear exit | 5.666667 s | 4.866667 s |
| Collapse starts | 6.066667 s | 5.266667 s |
| Opaque core gone | 6.666667 s | 5.866667 s |
| Residual gone | 6.866667 s | 6.066667 s |

Clear exit и half-emergence — visual representatives: точную world-space plane intersection из reference MP4 измерить невозможно.

## 7–8. Почему прежний portal выглядел кольцом

Прежняя версия использовала маленький почти геометрически правильный oval rim и сходные скорости слоёв. Opening/closing занимали 0.7/0.45 s, emergence 1.35 s; эффект не успевал пройти выраженную ribbon formation и residual фазу. Простая boundary доминировала над глубиной и дымом.

## 9–13. Новый rendering

- **Shape:** крупный vertical torn aperture, базовые radii `(3.15, 3.35)`, center height `3.4`, rotation Y `15°`. Root scale/center остаются фиксированными; меняется geometry, а не actor transform.
- **Growth:** floor distortion/ellipse → black seed → семь самостоятельных tapered mesh ribbons. Magenta/cyan светящиеся edges окружают тёмную сердцевину ribbons. Они раскрываются над floor и сворачиваются в aperture.
- **Outer edge:** динамический angular boundary с несколькими частотами; white/lavender core, широкий мягкий halo, 12 неодинаковых branch/bridge arcs. Shader меняет thickness и flowing hotspots.
- **Depth:** near-black throat, два independently warped flow slices, девять движущихся curved filaments, отдельный более медленный EmberGen inner vapor. Это несколько слоёв geometry/material/clock, не один flipbook quad.
- **Distortion:** локальный portal shader семплирует существующий arena background с небольшим radial offset. Это refraction текущей иллюстрации; actor RT не содержит фон, поэтому GrabPass не использован.
- **Lighting:** point light только на actor layer, range `5.5`, intensity до `0.68`, без shadows; отдельный floor illumination carrier.
- **Particles:** золотые/белые motes на edge и внутри; отдельные tangential velocities и inward motion при collapse.

## 14–18. EmberGen и Unity assets

Рабочая установка: `D:/3DMODELS/EmberGen/EmberGen.exe`, EmberGen **1.2.6**.

Созданы четыре отдельных `.ember` проекта на основе рабочего wispy smoke preset, с собственными palette, camera/framing и frame windows:

| Project / imported atlas | Native frame window | Runtime role |
|---|---|---|
| Portal_Wisps | first 60, stride 1 | быстрые наружные wisps |
| Portal_InnerVapor | first 75, stride 1 | медленная внутренняя depth layer |
| Portal_EdgeSmoke | first 100, stride 1 | edge breakup |
| Portal_Collapse | first 110, stride 1 | inward residual vapor |

Source projects и settings: [EmberGen/Sources](EmberGen/Sources). Native source/export absolute paths, byte hashes и CLI exit codes: [EmberGen/Manifest.json](EmberGen/Manifest.json).

Все четыре настоящих CLI PNG exports завершились с exit 0 и импортированы без изменения PNG bytes. Atlas каждого — **1024×1024**, **8×8**, **64 frames**, tile **128×128**, premultiplied RGBA. Проверены все 256 tiles: edge alpha max **0**, прозрачные margins сохранены. Ранние exports с обрезанным дымом отклонены; framing исправлено в EmberGen.

Unity destination: `Assets/Rokas/Resources/Combat/ReactiveTurns/Vfx/EmberGen/Portal_{Wisps,InnerVapor,EdgeSmoke,Collapse}.png`.

Import: BC7, sRGB, clamp, bilinear, no mipmaps, `alphaIsTransparency=false`. Используется существующий premultiplied Ember flipbook shader. Общий эффект строится runtime существующим `ReactiveCombatPortalEffect`; второй portal prefab не добавлен.

Original portal shaders: `ReactiveCombatPortalVolume`, `ReactiveCombatPortalRibbon`, `ReactiveCombatPortalLightning`, `ReactiveCombatPortalFloor`, `ReactiveCombatPortalDistortion`, `ReactiveCombatPortalEmergence`; shared `ReactiveCombatPortalBoundary.cginc`.

## 19–21. Masking, crossing, anti-jitter

Для текущего Built-in renderer применён world-space clipping shader на временных копиях материалов существующего skinned monster. За plane тело видно только внутри живой aperture и ограниченной back volume. Впереди plane восстанавливается полноценное освещение тела. CPU rim и mask используют одинаковые boundary coefficients и paused presentation clock.

Monster изначально не рисуется, root поставлен за fixed portal plane до activation. Во время движения mesh fragments пересекают plane непрерывно. Затем восстанавливаются исходные materials. Использованы существующий arena mover и locomotion speed; animation не перезапускается каждый frame. Actor scale/facing не изменяются; в конце восстанавливается exact slot. Root motion, Combat stats и turn scheduler не менялись.

Deferred Destroy не оставляет старый видимый portal рядом со следующим: root немедленно становится inactive, owned resources затем уничтожаются. Renderer/mesh/material lifecycle проверен отдельно.

## 22–26. Финальные presentation timings

Local zero — первый seed. Opening включает growth/formation и open hold.

| Runtime phase | Interval | Duration |
|---|---|---:|
| Seed → formation → open hold | 0–2.766667 s | 2.766667 s |
| Emergence → exact slot | 2.766667–4.866667 s | 2.1 s |
| Residual open | 4.866667–5.266667 s | 0.4 s |
| Inward contraction | 5.266667–5.866667 s | 0.6 s |
| Ghost edge / tail | 5.866667–6.066667 s | 0.2 s |

Aperture становится full-size приблизительно к 2.1 s. Collapse меняет размер ядра по inward curve и оставляет отдельный ghost outline прежнего размера. Следующий portal начинается после close и actor idle settle. Прежние test entrance budgets 18–20 s расширены только для более долгой последовательности portals; action/contact/return assertions сохранены.

## 27. Reference comparison

Сохранены и просмотрены шесть side-by-side pairs: forming, open, partial reveal, crossing, exited, collapse. Native PTS и фактические runtime clocks записаны в [Visual/Comparison.json](Visual/Comparison.json). Все screenshots сделаны в реальном encounter с normal combat framing.

Новая версия воспроизводит staged seed/ribbons/aperture/reveal/residual/contraction grammar, white-violet edge, magenta/cyan energy и тёмную глубину. Старый tiny round-ring look устранён. Pixel-perfect match не заявляется.

## 28–31. PlayMode, sequential portals, performance, regressions

- Focused PlayMode: **19/19 PASS**, valid XML; включает actual skinned clipping, alpha corners, pixel motion, resource destruction и three-wave transitions.
- Full relevant ReactiveTurns PlayMode: **80/80 PASS**, **0 failures**, **0 skipped**; duration **1022.5767678 s**.
- Core/domain: **49 named PASS**, aggregate suite PASS, compiler/runner exit 0.
- Existing live long-run: **20 planned commands PASS**, Normal/Heavy/Throw, preview switch/cancel, defenses, exact returns/camera reset, multiple waves и повторный encounter. Counts находятся в [TestResults.json](TestResults.json).
- Реальный entrance test проверил **три последовательных portals**, максимум один active portal и один masked actor одновременно, постоянный scale/facing, no root teleport, restored materials и отсутствие portal после intro. Три sequence sheets и полный motion MP4 сохранены в [Visual](Visual).
- Full encounter motion воспроизведён в browser на скорости 1×; sequence sheets и alpha/rendering captures просмотрены отдельно.
- Новый portal владеет **16 materials** и **13 distinct meshes**; четыре atlases дают приблизительно **4 MiB BC7** без mipmaps. Captured maximum — **69 particles** при лимите 96, **1 simultaneous portal**. Carrier overdraw ограничен единственным entrance effect; отдельный GPU profiler замер не выполнялся.
- PNG/ReadPixels capture cadence не является FPS игры. Runtime video сохраняет фактические realtime frame durations.
- Validator/comparator: **136 baseline → 136 final**, **0 NEW**, **0 removed**. Baseline — initial HEAD плюс исходные пользовательские изменения и ignored assets; прежние findings сохранены и не приписаны portal pass.
- Focused source review выполнен: clocks, fixed mask plane, material restoration, no actor scale changes, sequential disposal и owned mesh/material cleanup. Найденный во время разработки NaN в fractional power arc endpoint исправлен до финальных runs; оба финальных runs зелёные.

## 32–33. Git checks и сохранение работы

Literal `git diff --check`, focused staged check и commit check выполняются перед/после commit; точные результаты — [GitChecks.json](GitChecks.json) и внешний `FinalCheckpoint.json`.

Unity добавил только trailing whitespace в 28 исходно чистых FBX/texture `.meta`. Этот generated noise удалён после сравнения normalized содержимого с initial HEAD; import settings не изменены. Доказательство — [ImporterWhitespaceCleanup.json](ImporterWhitespaceCleanup.json). Деструктивные Git reset/clean/checkout не использованы.

После focused commit portal scope и index чисты. Общий working tree содержит прежние пользовательские изменения: 84 protected file hashes проверены без изменений. Они не включены в portal commit.

## 34. Реальные оставшиеся отличия / ограничения

- Ribbon силуэты тоньше и более графичные, чем широкие flame-like ribbons reference; формы lightning branches и wisps отличаются.
- Interior vortex ближе к flowing vapor/filaments; у reference более яркий electric swirl и иной bloom.
- Существующий monster имеет меньшую crouched pose и другой силуэт появления, чем morphing/enlarging creature в reference. Его модель, animation и scale не менялись этим portal-only pass. Partial reveal поэтому не pixel-identical.
- У правых slots outer wisps/arc могут выходить за край normal combat framing; creature crossing остаётся читаемым. Portal geometry не переносит enemy slot.
- Later reference creatures появляются без повторного полноценного portal. В ROKAS каждый из трёх spawns получает полный effect и cleanup, поэтому total encounter intro длиннее reference.
- Refraction использует существующую background illustration, а не universal screen-space scene refraction. Atlas native motion поддерживает smoke/energy; procedural mesh/shaders обеспечивают silhouette и lightning.
- GPU profiler FPS/overdraw capture и unified integration в этом focused R11 pass не выполнялись. Canonical `D:/Rokas/Rokas` не изменён и не объявлен новым проверенным manual-QA checkpoint.
