# Unity visual review — Polish II

Последний `capture-manifest.json` содержит **147 кадров** из реального Unity PlayMode. Только эти записи скопированы из runtime output; stale файлы предыдущих запусков не включены в этот count. Motion кадры сняты 960×540, ключевые contact кадры — 1920×1080. Дополнительно сохранены четыре sequence sheets.

## Просмотренные сценарии

| Сценарий | Наблюдение |
| --- | --- |
| Entrance | Пустая arena, вход Keiko слева, walk заканчивается grounded two-hand settle; затем последовательные portals |
| Normal | Shoulder ready при подходе; затем отдельный backswing и быстрый swing; contact и authored follow-through; точный idle return |
| Heavy | Отличающаяся высокая preparation, затем jump/downstroke; purple contact и recovery |
| Enemy hit | Claw перед телом Keiko; contact particles у skin surface, а не actor root center |
| Block | Обе руки на hilt; вниз направленный hanging blade пересекает низкую claw траекторию; golden sparks видны у blade/claw |
| Dodge | Backstep от прежней позиции; attack проходит мимо; нет body effect, landing и return непрерывны |
| Death | Fall, краткая задержка, ash/dissolve при постоянной floor позиции; whole corpse sinking отсутствует |
| HUD / turn | Компактная нижняя reaction panel, contextual announcements, controls gated до return/settle |

На Normal/Heavy/enemy/Block/Dodge кадрах нет прежнего прозрачного UI rectangle. Разные selection stances, sword arc и контакт просмотрены по последовательным кадрам; automated tests отдельно проверяют lifecycle и exact transforms. Здесь не заявляется слуховой review.

## Навигация

Полный порядок, phase, action/target, poses, clock, presentation hold, HP и transforms находятся в [capture-manifest.json](capture-manifest.json). Motion sequences и ключевые contact screenshots лежат в этой папке. Общий отчёт с точными результатами и ограничениями — [README](../README.md).
