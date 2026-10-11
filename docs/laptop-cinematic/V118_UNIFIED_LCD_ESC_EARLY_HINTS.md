# ROKAS Laptop Cinematic V11.8 — one physical LCD polygon, ESC replacement, early hints

Date: 2026-10-11. User supplied three images: reference monitor screenshot (`image(20261011-130215).png`), red-annotated LCD bezel screenshot (`image(20261011-130219).png`), original replacement golden `ESC — НАЗАД` artwork (`image(20261011-130447).png`).

## Requested behavior

1. Correct the blue YOMI monitor image leaking beyond physical LCD left edge. Keep original real YOMI capture, no new desktop, wallpaper or icon redraw.
2. Replace existing `ESC — НАЗАД` PNG with EXACT user image, same design pipeline and hover/luminance.
3. Keep the already-approved lower E/R/ESC prompt placement unchanged, but show the prompt art *as the seated POV is appearing*, rather than popping all prompts after ~1.8s of camera movement. Prevent early interaction.

## Runtime changes (laptop only)

`Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs`:

- Declare **one** inner `safeCorners` polygon immediately after optional `screen_calibration` lookup, before constructing the screen layers. The formerly divergent outer LCD corners in `LaptopWakePolygon`, `LaptopPhysicalDesktop`, `PhysicalDesktopLiveClock` and desktop click region now match the polygon used by `ROKASNoSignalScreen`, `PhysicalMiniYomiViewport`, and the exact original YOMI screenshot mesh. This prevents a wide blue *fallback* texture from leaking from outside the inner stencil while keeping the real screenshot UVs from (0,0) to (1,1).
- Fine-tune LEFT edge only: `leftTopInset = 32` and `leftBottomInset = 38` source-art pixels (V11.7 22/26). Keep right = 5 and vertical = 5 source pixels. Preserve art source and screen-scale math.
- During a fresh `TryStart` and when returning from fullscreen YOMI, call `SetPromptVisibility(true, false)` immediately, before the first `Play()` rendered frame. `povGroup.alpha` continues its existing smootherstep fade during camera approach; both golden prompts and original OFF chibi are already present in that fade. `CanvasGroup.blocksRaycasts` and `interactable` stay false until `Phase.PreBootChoice` is reached and `SetPromptVisibility(true)` unlocks click input. No premature Power press. Existing standby-through-first-boot and fade-on-standing remain unchanged.
- Original gold prompt positions from V11.7 (`GoldPromptDrop = 50`, +19 E, +13 ESC optical alignment), R shortcut, original hand timeline and two SFX unchanged.

`Assets/Rokas/Resources/LaptopCinematic/UI/prompt_back.png`:

- Replace PNG in place (existing unique meta/GUID untouched) using exact owner-supplied **RGBA 2048×682**.
- **668761 bytes, SHA256 `bd28f48fdcd757c5a5b6078ffb37181104910068df4f321cda78dfb4a99ca712`**, Git blob SHA1 `fdece526fcf1d3ce48aead6aa8d632efa3f2f664`. Byte-identical to attached source. No PNG recompression, tint, aspect or transparent-background alteration.
- Verified GitHub Action: https://github.com/timurkamurka11/Rokas/actions/runs/38142112416

`Assets/Rokas/Tests/PlayMode/LaptopV111SeamlessPOVPlayModeTests.cs`:

- Requires OFF E / ESC PNG and chibi active on the first seating frame with inputs disabled during approach.
- Checks the backup live wallpaper quad and live clock share identical four calibrated source-pixel corners.
- Existing YOMI original capture, per-asset, stand controls and first boot regression checks remain.

## Verification

- 14/14 static structural assertions of code passed.
- Original ESC file Git blob SHA1 matches exact local source; image transfer action succeeded.
- Unity Art Integration / Main CI for commit `da7dcc3`: pending/result inspect https://github.com/timurkamurka11/Rokas/actions/runs/38142312597 and https://github.com/timurkamurka11/Rokas/actions/runs/38142312718 .
- Prior CI unrelated compile blocker, from Codex WIP combat: `ReactiveCombatArena.cs` missing `CombatShotBounds` and `CombatShotBoundsProjection`. Do not declare visual/PlayMode PASS if this remains unresolved. Do not synthesize Combat types in a laptop-scope edit.

**Acceptance still needed in canonical Windows Unity Game View:** compare left LCD bezel at full 1080p, monitor full YOMI display both before and after reseating, new ESC art proportions and early appearance of all gold hints during the first 1.8s. Do not run destructive `git reset --hard`, `git clean -fd`, or force push. Pause Codex before safely syncing any diverged local working copy.
