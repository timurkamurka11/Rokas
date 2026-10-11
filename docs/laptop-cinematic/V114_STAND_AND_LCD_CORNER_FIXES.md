# ROKAS Laptop Cinematic V11.4 — approved gold stand PNG + LCD corner accuracy

Scope: user annotations from 2026-10-11 showing (a) black display leaking beyond upper-left LCD bezel, (b) bottom-right LCD area not fully filled, and (c) exact transparent gold image reading `E — ВСТАТЬ`. Also retains the earlier request to lower existing gold prompt art and advance the start of chibi playback slightly during seating.

## Implementation

- `Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs`
  - `AddLcdStencil` now first ensures **CanvasRenderer** exists before attaching `LaptopPerspectiveQuad`, `Mask` and `RectMask2D`. This fixes the `MissingComponentException` in old failed GameCI before this patch.
  - OFF chibi and ON YOMI share **identical asymmetric four-corner LCD polygon**, inset 5 source-art pixels from the actual screen corners. The four original corners remain calibrated; the approved room/POV artwork is not altered.
  - Original YOMI image is proportionally **cover-fitted** into the mask (not distorted or shrunk), eliminating the unfilled bottom-right strip without crossing the bezel at top-left.
  - Original gold `prompt_power`, `prompt_back` and original arrow were dropped by 33/33/26 stage units, respectively; the powered open prompt likewise lowered, as previously approved. The original source PNGs and hover/glow are unchanged.
  - Chibi is visible for the final 0.12 s of the approach, reducing the visual pop after sitting; right-hand 49-frame animation and original scene art remain unchanged.
  - In powered seated POV, the previous tiny `LaptopBackChoice` arrow hides and the **exact user `prompt_stand.png`** at `PoweredStandOriginalPNG` replaces it in the lower-left region. It has the existing `LaptopChoiceHover`, same original-PNG duplicate glow, `Button.targetGraphic`, and `onClick = BackToRoom()`. During OFF state the original back arrow is preserved. Keyboard `E` continues to open YOMI (previous agreed non-disruptive option), and `Esc` continues to exit; **mouse click on the E ВСТАТЬ graphic actually stands**.
  - Removes an accidental duplicate `SetPromptVisibility(false)` at startup handoff to keep the original standby state until the first boot frame, as V11.3 intended.
- `Assets/Rokas/Resources/LaptopCinematic/UI/prompt_stand.png`
  - Exact original RGBA **2048×682**, **671384 bytes**, SHA-256 **fb5617960a9ba48a95043fdc7e2be517b258abeb8a60cac654449dbedd9debbb**; copied without redraw, altered colors or PNG recompression. Metadata `prompt_stand.png.meta` contains a stable unique 32-digit Unity GUID and full `TextureImporter` configuration.
- `Assets/Rokas/Tests/PlayMode/LaptopV111SeamlessPOVPlayModeTests.cs`
  - Requires original stand PNG to import in Unity and checks that in powered state it replaces the old arrow, has exact source texture, full white tint and hover/glow, and clicking it actually runs stand-up/return-to-room. Original LCD polygon mask and prompt positions also checked.
- `.github/workflows/laptop-v114-import-stand-png.yml`
  - Safe one-time idempotent **import** with SHA-256 verification and meta validation; no local BAT/review clone.

## Verification

- GitHub Actions exact original transparent PNG import: [PASS](https://github.com/timurkamurka11/Rokas/actions/runs/38114096592).
- Unity PlayMode with original pinned hand PNGs and complete laptop interactions: [V11.4 run](https://github.com/timurkamurka11/Rokas/actions/runs/38114172963).
- Unity focused EditMode + PlayMode: [V11.4 run](https://github.com/timurkamurka11/Rokas/actions/runs/38114172944).

Cloud CI covers resource imports, code compilation, lifecycle and UI scene-graph tests; it does **not** substitute for checking precise positioning, anti-aliasing, brightness or video/SFX visually in the user's **Windows QHD Game View** with real room artwork. Preserve actual screenshot-based visual acceptance as pending until confirmed by user.

## Delivery

Use only canonical Windows working copy `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`. Close Unity before fast-forwarding the tracking branch. Do not delete local untracked art; use the previously supplied collision-safe pull script if Git prevents overwriting untracked files. Do not modify unrelated combat, VN, sounds or photography.
