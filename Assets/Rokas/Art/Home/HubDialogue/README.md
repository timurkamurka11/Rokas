# Hub Dialogue runtime assets

Source: user-provided `UI HUB TALK.rar`.

Runtime portrait atlas preserves the six Idle frames and six Talk frames in source order.

| Runtime frame | Source |
| --- | --- |
| Idle 01–06 | `KEIKI IDLE/IDLE 1.png` … `IDLE 6.png` |
| Talk 01 | `KEIKI TALK/TALK 1.png` |
| Talk 02 | `KEIKI TALK/TALK 2.png` |
| Talk 03 | `KEIKI TALK/TALK 3.png` |
| Talk 04 | `KEIKI TALK/TALK 4.png` |
| Talk 05 | `KEIKI TALK/TALK 5.png` |
| Talk 06 | `KEIKI TALK/TALK 7.png` |

Atlas layout is six columns by two rows: Idle on the top row, Talk on the bottom row. The source archive is not modified.

`HubDialoguePlaque.png` is derived directly from `UI PLANK/PLANK.png` and retains its original transparent visual design.

The project uses its existing `Texture2D` + `RawImage` runtime UI pipeline (the same convention used by current VN plaque assets), so these runtime textures intentionally remain Texture assets rather than introducing a parallel Sprite/prefab pipeline.
