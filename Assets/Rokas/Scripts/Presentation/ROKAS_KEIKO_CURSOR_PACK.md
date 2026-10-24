# ROKAS — six Keiko hand cursors (candidate in draft PR #13)

The user supplied **six transparent 1254x1254 original PNGs** on October 24, 2026. The six images are NOT redrawn, recoloured, AI-generated or replaced with stock illustrations. They are proportionally downscaled to **96x96** with Lanczos antialiasing for native Windows/Unity cursor usability. Import via `Assets/Resources/Rokas/Cursors`, one image for each of:

| Original file | Unity resource | Game interaction |
| --- | --- | --- |
| image(20261023-224536).png | Keiko_Point | Default pointing hand / ordinary UI hover |
| image(20261023-224540).png | Keiko_Magic | Special interaction: sleeping bed, portal, confirm, alarm, laptop power |
| image(20261023-224544).png | Keiko_Press | Primary left mouse button held / click |
| image(20261023-224550).png | Keiko_OpenHand | Drag-ready `IDragHandler` UI object (e.g., watch crown slider) |
| image(20261023-224554).png | Keiko_Grab | Actual UI dragging while primary button held and mouse moved |
| image(20261023-224558).png | Keiko_Blocked | Hovering a disabled or noninteractable Unity `Selectable` |

## Code

- `RokasCursorPackRuntime.cs` — passive Unity `Cursor.SetCursor` visual router; uses already-existing `EventSystem.RaycastAll`, `Button`, `Selectable` and `IDragHandler` to choose a cursor. Does NOT issue clicks, suppress user events, move game objects or interact with save state.
- `RokasCursorPackImporter.cs` — constrains ONLY these six Resources assets to cursor-compatible readable, uncompressed, transparent, no-mipmap import.
- `RokasBootstrap.cs` — creates exactly one cursor router in existing `PrepareRuntime()`; no other game scene, UI, hub, watch, hand, V16 or input logic modified.

Unity 6000 Cursor.SetCursor official contract: Cursor import type / Read-Write, hotspot within pixels (measured from top left), no mip maps. At application focus-loss and destruction, restore system cursor.

## Integration distribution

GitHub connector writes source code/text but **not binary PNG files**. The self-contained small ZIP `ROKAS_KEIKO_CURSORS_D_V1.zip` holds all six original-image-derived 96px PNGs and a single BAT/PowerShell installer for **EXISTING** `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` project. The script refuses to operate while Unity is open, performs narrow anchor patch to the local Bootstrap to preserve unknown local edits, verifies SHA256 for the six packaged PNGs, downloads exact source from pinned immutable PR commit and verifies Git blobs, saves backups, and rolls back on error. No Blender, EXE, PR merge, branch switch or scene modification.

**QA pending on user's actual Windows Unity Game View.** Check point, special bed, pressed mouse, watch wheel/slider drag-open and drag-grab, disabled Selectable warning, tooltip/hover events unchanged, and normal arrow returns after Unity loses focus. PR #13 remains DRAFT/unmerged until real Game Play Mode approval.
