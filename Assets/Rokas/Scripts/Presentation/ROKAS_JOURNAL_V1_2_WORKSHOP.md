# ROKAS Quest Journal V1.2 — live book motion, authored foley, editor workshop

User requirements: large original closed/open book art in upper-left; while hovered the opened book slowly levitates up and down without moving the pointer hit area; opening journal once must play the user's exact `Open book.mp3`; a sizeable Unity workshop must let the developer drag/drop a portrait in the round panel socket and author all quest UI text/layout/hover settings.

## Runtime implementation

- `RokasQuestJournalHud.cs`: unchanged 3 original book/panel PNGs; book's **visual child** (not hit-area) moves 5px at 0.45 Hz only while hovered/open and crossfades the two original PNGs. Hit area remains fixed to prevent enter/exit thrashing.
- Plays `Resources/Rokas/Journal/Journal_OpenSfx.mp3` through a dedicated nonspatial `AudioSource.PlayOneShot` exactly once per opening click, when provided and no override selected. No duplicated generic button sound. Clip length ~1.793 seconds; original audio supplied by user.
- Loads optional `Resources/Rokas/Journal/JournalSettings.asset` ScriptableObject (creates a transient defaults-only instance if absent). No editor callbacks, per-frame global searching or Resource.Load in journal Tick.
- Editable circular avatar mask generated locally from a real round stencil; optional user-selected Texture2D texture appears inside, no default fake portrait.
- Editable dynamic UI labels for avatar name, quest heading, description, notes, 6 tabs and 4 objectives. **None of these labels is baked into the original PNGs**; empty strings remain empty, as before.
- Positions and sizes are in the existing authored 1920x1080 UI coordinate system. Source script does not edit VN, bed sleep, combat, Keiko cursor or gameplay progression.

## Workshop

Unity editor menu: **ROKAS → Quest Journal Workshop → Open**.

Standalone window `RokasJournalWorkshopWindow.cs` exposes five areas:
1. Artwork & Audio — drag/drop any Texture2D avatar, sound AudioClip, volume and inspect original assets.
2. Text & Quests — name, heading, multiline description/notes, six editable navigation titles, four objective strings, colors/font/sizes.
3. Layout — HUD icon position/size, popup position/size, selected row tint, avatar region and all text rectangles, category and objective spacing.
4. Hover & Motion — bob amplitude/frequency, hover scale/rise/response/glow, menu speed/slide/scale/dimmer.
5. Preview — closed book/open hover journal art and original empty panel with editable drag-to-position overlays. Current drag selection is outlined in cyan **editor only**; runtime draws no inspector boxes.

Saves to `Assets/Rokas/Resources/Rokas/Journal/JournalSettings.asset`. Editable asset can be reused without recompilation; effects appear at next Play Mode startup. The workshop does not automatically alter existing ROKAS scenes.

## BAT installer

External package: `ROKAS_JOURNAL_V1_2_WORKSHOP_OPEN_BOOK_BAT.zip`, SHA256 `3fa0e8d2f875a616012580ec25feccc8f95e45fae31d59d572715e6972d51eb3`, size 28,374 bytes. Contains the original user `Open book.mp3` (relabelled `Journal_OpenSfx.mp3`) and guarded Windows BAT + PowerShell importer, not a newly synthesized sound. Requires V1.1 and all three original PNGs already present. Imports 5 source/meta files by exact Git blob SHA from this private branch; refuses to overwrite unexpected locally changed files. Only allowed replacement is original V1.1 `RokasQuestJournalHud.cs` blob `802ec3c7471d1c6da6733ae1522fe279feabf34e`. Backup + rollback in Library/ROKAS_JournalV1_2_Backups. It never text-patches `RokasView.cs`, checks out, resets, or builds EXE.

## Verification

Source asset references and ZIP SHA/CRC verified. The C# source has been committed and static-checked. **Cannot assert successful Unity compile, Windows BAT execution or rendered hover, audio and workshop behavior without user's local test.**

QA: open Unity, check editor Console, find ROKAS menu, open Workshop, try avatar Textures and texts, save, re-enter Play, test hover duration >3s, repeated clicks/open book single foley, background close, hidden journal while laptop/bed/VN and no new periodic FPS stalls.
