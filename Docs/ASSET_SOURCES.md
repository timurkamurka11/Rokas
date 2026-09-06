# Ассеты текущего первого контракта

Пять иллюстраций созданы для ROKAS инструментом генерации изображений; готовые изображения повторно использованы при восстановлении сессии. Это исходные ассеты проекта, не скриншоты запущенной Unity-игры. Финальная компоновка и анимация формируются Presentation.

| Файл относительно `Assets/Rokas/` | Формат / назначение | Происхождение |
| --- | --- | --- |
| `Art/Home/ApartmentNight.png` | 1672×941, квартира ночью | Генерация для ROKAS |
| `Art/Locations/ShrinePortal.png` | 1672×941, святилище/портал | Генерация для ROKAS |
| `Art/Locations/AbandonedSubway.png` | 1672×941, платформа | Генерация для ROKAS |
| `Art/Yokai/FacelessCommuter.png` | 1024×1536 RGBA, ёкай | Генерация для ROKAS |
| `Art/Familiars/Mame.png` | 1254×1254 RGBA, Мамэ | Генерация для ROKAS |
| `Art/UI/LaptopWallpaper.png` | 1672×941, рабочий стол YOMI | Built-in imagegen по референсу пользователя; 2026-09-06 |
| `Art/UI/Fonts/RokasSans.ttf` | DejaVu Sans, кириллица | Системная копия DejaVu, шрифт не изменён |
| `Art/UI/Fonts/RokasSerif.ttf` | DejaVu Serif, заголовки | Системная копия DejaVu, шрифт не изменён |
| `Audio/HomeRain.wav` | Дом, дождь | Собственный синтез `Tools/produce_audio.py` |
| `Audio/HomeNocturne.wav` | Дом, музыкальный фон | Тот же генератор |
| `Audio/SubwayHum.wav` | Метро, окружение | Тот же генератор |
| `Audio/OtherSide.wav` | Задание, музыкальный фон | Тот же генератор |
| `Audio/UIWood.wav` | Интерфейс | Тот же генератор |
| `Audio/BladeHit.wav` | Удар | Тот же генератор |
| `Audio/SealBreak.wav` | Точный удар по печати | Тот же генератор |
| `Audio/PortalCrossing.wav` | Переход | Тот же генератор |
| `Audio/ContractSealed.wav` | Завершение боя | Тот же генератор |
| `Audio/MameMurmur.wav` | Реакция Мамэ | Тот же генератор |

Лицензионные уведомления DejaVu сохранены в `Docs/Licenses/DejaVu.txt`; переименованы файлы, а не внутренние имена шрифтов. В аудио нет внешних музыкальных записей или сэмплов. Все WAV — stereo PCM16, 22050 Hz. Генератор использует Python и NumPy; для открытия Unity готовых WAV достаточно.

PNG импортируются как UI-текстуры без mip maps, с максимальным размером 2048. В персонажах сохранён alpha. `RokasAssets.asset` содержит назначения всех 18 бинарных ассетов и JSON первого контракта. Дубли в Resources не создаются: используются прямые GUID-ссылки.

## Wallpaper ноутбука YOMI

Новый фон сохранён в `Assets/Rokas/Art/UI/LaptopWallpaper.png`; существующие игровые иллюстрации, шрифты и аудио переиспользованы. Девять иконок приложений рисуются векторной геометрией `LaptopIcon.cs`. Фон — ассет, не скриншот Unity.

Промпт built-in imagegen: “Full-bleed 16:9 cinematic illustrated wallpaper matching the user reference: rainy blue-hour Japanese city viewed from a wooded shrine hillside, distant mountain and slender orange Tokyo-style tower. Dark calm left half for launcher tiles. Right foreground: weathered red shrine post and eaves, blank warm paper lantern, wet stone steps, small warm stone lanterns, foliage. Restrained navy palette, warm lights, mist and rain. No portal, people, hardware, frame, UI, icons, overlays, text, signage, logos or watermarks.”
