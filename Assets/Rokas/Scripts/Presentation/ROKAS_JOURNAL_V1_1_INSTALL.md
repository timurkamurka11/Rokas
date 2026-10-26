# ROKAS Quest Journal V1.1 — safe installer after local anchor failure

## Why V1 BAT failed

The old installer matched exact text in the user's customized local `RokasView.cs`, specifically `private readonly HubDialogueController hubDialogue;`. The local version does not contain that exact declaration. **That error happened before the original installer writes any game files.** `git fetch` updated FETCH_HEAD but did not checkout/reset the user's branch.

## V1.1 approach

A new `RokasQuestJournalAutoBootstrap.cs` uses `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` to create a standalone journal host. At runtime it waits for the existing `RokasBootstrap.View` and locates the existing `RokasCanvas/AuthoredStage/Panels` transform. It uses `RokasQuestJournalHud` and the exact three original resources:
`Resources/Rokas/Journal/Journal_Closed.png`, `Journal_Open.png`, `Journal_Panel.png`.

The host is active only for Home/Accepted/Payment in MainRoom after Mina's guild introduction is flagged complete, and only when there is no active dialogue, pause, laptop or other modal. It checks for an existing `ROKAS_Journal_HUD_StageOverlay` to avoid double journals in GitHub's older integrated V1 path.

No changes to `RokasView.cs` or other local game scripts; neither `Cursor.SetCursor` nor the 6 original Keiko PNGs are touched. No per-frame resource loading, global Unity object scans or network calls.

## Installation package

`ROKAS_JOURNAL_V1_1_ANCHOR_FREE_SAFE_INSTALL.zip`, SHA256 `163e6160be8c880a635aed9a741cd982a0e009aa7c4d9ab5a78ad9c8c9d18b06`, 5,208,708 bytes. It contains the user's three ORIGINAL RGBA PNGs unchanged, metadata, one BAT, PowerShell helper and Russian README. It calls `git fetch` + `git show` on the connected user's own private GitHub branch for only `RokasQuestJournalHud.cs` and `RokasQuestJournalAutoBootstrap.cs`, with source sanity checks.

All preflight checks happen before writes; scripts/images/meta with different existing content block installation rather than overwriting; all writes are strictly **new files**. A manifest of created files is written under `Library/ROKAS_JournalV1_1_Backups`; incomplete installations attempt to delete only those newly created files. No `git checkout`, `git reset` or branch switch.

## Limitations / QA

V1.1 is an asset-backed hover/book/modal visual prototype, not a quest-save system. In standalone mode the existing Escape key stays controlled by `RokasView`; close via book click or backdrop click. The private click audio is not hooked up by this local compatibility adapter. The source/static checks and ZIP integrity pass; **Unity compilation, Windows BAT/PowerShell execution and PlayMode behavior have not been independently verified**.

Recommended user test: close Unity, extract full ZIP, run `ROKAS_INSTALL_JOURNAL_V1_1.bat`, confirm PASS, reopen Unity, verify no Console errors, finish Mina guild intro, hover book top-left and click for panel; verify background click closes it and laptop/bed still work.
