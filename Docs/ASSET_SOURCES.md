# Ассеты текущего первого контракта

Пять иллюстраций созданы для ROKAS инструментом генерации изображений; готовые изображения повторно использованы при восстановлении сессии. Это исходные ассеты проекта, не скриншоты запущенной Unity-игры. Финальная компоновка и анимация формируются Presentation.

| Файл относительно `Assets/Rokas/` | Формат / назначение | Происхождение |
| --- | --- | --- |
| `Art/Home/ApartmentNight.png` | 1672×941, квартира ночью | Генерация для ROKAS |
| `Art/Locations/ShrinePortal.png` | 1672×941, святилище/портал | Генерация для ROKAS |
| `Art/Locations/AbandonedSubway.png` | 1672×941, платформа | Генерация для ROKAS |
| `Art/Yokai/FacelessCommuter.png` | 1024×1536 RGBA, ёкай | Генерация для ROKAS |
| `Art/Familiars/Mame.png` | 1254×1254 RGBA, Мамэ | Генерация для ROKAS |
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

PNG импортируются как UI-текстуры без mip maps, с максимальным размером 2048. В персонажах сохранён alpha. `RokasAssets.asset` содержит назначения всех 17 бинарных ассетов и JSON первого контракта. Дубли в Resources не создаются: используются прямые GUID-ссылки.
