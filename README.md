# ROKAS

Японский ночной город, квартира охотника и контракты на ёкаев. Текущий этап — подключение иллюстрированных Unity-экранов к существующему Core первого контракта.

## Открыть проект

Используется существующий репозиторий `timurkamurka11/Rokas`, ветка `development`, Unity **6000.3.19f1**. Для первой загрузки на диск D:

```powershell
git clone --branch development https://github.com/timurkamurka11/Rokas.git D:\Games\Rokas
```

Для обновления уже загруженного проекта из его папки:

```powershell
git status
git pull --ff-only origin development
```

В Unity Hub выберите **Add project from disk → D:\Games\Rokas**. Дождитесь импорта и компиляции, откройте `Assets/Rokas/Scenes/Rokas.unity`, затем Play. Стартовая сцена содержит `RokasBootstrap`; он создаёт интерфейс при запуске из назначенных ассетов. Пустой вид сцены до Play ожидаем.

## Текущий цикл

Дом → ноутбук YOMI → принять контракт → чай → выход → портал → бой → возвращение домой → оплата в YOMI. Бой использует существующие автоматические и ручные атаки Core. Появляющаяся печать даёт двойной урон при точном нажатии. Улучшение клинка доступно дома между контрактами.

Мышь взаимодействует с предметами и ёкаем. Tab выбирает кнопку, Enter нажимает её, Esc открывает настройки/паузу или закрывает панель. Дождь, пар, Мамэ, свет портала, реакции на удары и короткие искажения анимируются отдельно от фоновой иллюстрации.

Профиль сохраняется в `Application.persistentDataPath/Profile/save.json`, рядом резервная копия `save.json.bak`. На Windows стандартный путь — `%USERPROFILE%\AppData\LocalLow\Rokas\ROKAS\Profile`. Повреждённый или более новый профиль блокирует перезапись; перед ручным восстановлением сохраните оба файла.

## Проверка и сборка

```text
dotnet run --project Tests/Core/Rokas.Core.Tests.csproj --configuration Release
python3 Tools/validate_assets.py
```

GitHub Actions запускает компиляцию и поведенческие тесты Core, затем проверку ассетов, GUID-ссылок и стартовой сцены. Эта проверка не компилирует Unity Presentation.

В Unity: **Rokas → Validate Project**, затем **Window → General → Test Runner**, EditMode и PlayMode. Validate Project проверяет импортированные ассеты и открывает стартовую сцену в отдельной preview scene для поиска Missing Script и проверки bootstrap; открытая пользовательская сцена не пересохраняется. Проверка первого цикла: `IllustratedHomeCanAcceptFightReturnAndClaimExactlyOnce`. Она использует UI pointer handlers и включает паузу и повторную загрузку профиля после оплаты. Сам тест здесь ещё не запускался.

Сборка: установите модуль Windows Build Support для указанного Editor и выберите **Rokas → Build Windows x64**. Результат — `Builds/Windows/Rokas.exe`; запуск player проверяется отдельно.

Unity Editor, PlayMode и Windows player в среде подготовки этого checkpoint недоступны и ещё не запускались. Следующая точная проверка записана в `Docs/SESSION_HANDOFF.md`.

## Основные файлы

- `Assets/Rokas/Scripts/Core/` — существующая игровая логика.
- `Assets/Rokas/Scripts/Presentation/` — экраны и подключения к `GameSession`.
- `Assets/Rokas/Resources/RokasAssets.asset` — ссылки на иллюстрации, шрифты, аудио и контракт.
- `Assets/Rokas/Scenes/Rokas.unity` — стартовая сцена.
- `Docs/ASSET_SOURCES.md` — происхождение ассетов и лицензия шрифтов.
- `Docs/PROJECT_STATE.md`, `Docs/NEXT_STEPS.md` — фактическое состояние и ближайшая работа.
