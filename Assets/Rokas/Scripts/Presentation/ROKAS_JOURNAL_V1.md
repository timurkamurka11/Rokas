# ROKAS — authored Quest Journal V1 (visual/interaction pass)

The journal is added to the existing 1920x1080 `RokasView` stage, above room hotspots and below the laptop/settings and VN/transition layers. No quest/story text is baked into these images or rendered by this iteration.

## Required original art

Copy the three user-authored **transparent PNGs** to `Assets/Rokas/Resources/Rokas/Journal/` with these exact names:

- `Journal_Closed.png` — original closed gold/black sci-fi book, 1254x1254 RGBA
- `Journal_Open.png` — original open gold/black book, 1254x1254 RGBA
- `Journal_Panel.png` — original dark-blue quest window, 1448x1086 RGBA

The binary files were delivered in `ROKAS_JOURNAL_V1_GITHUB_ASSETS_BAT.zip`, retaining the source bytes. **They are not added as binary repository assets by the GitHub source-code connector.** Until they are copied/imported, `RokasQuestJournalHud` deliberately remains hidden with one Console warning rather than substituting temporary visual assets.

## Behavior

- Appears only in the Home main room after `hubGuildIntroSeen` and after the Mina dialogue closes.
- Small authored closed book at top-left. On hover it crossfades to the supplied opened book, with subtle scale/lift/amber highlight.
- Click opens the supplied 4:3 empty quest panel, animated fade/scale/vertical ease with a dim backdrop.
- Click any blank background or press Escape to dismiss.
- Six invisible navigation hit slots over the supplied left column; active row highlighted with restrained amber. No quest text or saving until next content iteration.
- Journal hides/retracts for laptop, bed sleep, transition, VN/hub dialogue, portal, hallway and combat; no changes to those existing mechanics.
- Reuses existing `UiKit`, `RokasView`, EventSystem and UI sound; no parallel scene, no `OnGUI`, no cursor modifications.
- `Resources.Load` executed once per `RokasView` construction; no per-frame asset loads or RaycastAll.

## Validation before accepting

1. Import all three original PNGs (check Unity Console for absent-resource warning).
2. Finish Mina guild introduction and verify the icon appears upper-left.
3. Hover repeatedly, confirm the book opens without flicker; click to display window.
4. Confirm Esc, backdrop click, six left-hand hit areas and hover suppression on VN/laptop/bed.
5. Check resize/scaling (16:9), UI click sound and cursor Keiko V16.2 unchanged.
6. Check Editor Console for compile errors; verify both EditMode tests and at least one GameView PlayMode pass.

The changes have received **source-level review only**. Actual Unity compilation, imagery, controls and performance remain to be verified on the user's machine.
