# ROKAS Laptop Cinematic V12.1 — unified room/hallway lighting

## Goal

The seated 1672×941 laptop POV must reflect the **same two existing electrical switches** that control the hub and hallway. No new gameplay switches, independent flags, or permanent photo replacement.

- **Main room:** `session.State.lampOn`, the original HomeView floor/desk lamp switch.
- **Hallway:** `session.HallwayLightOn`, the original HallwayView wall switch.
- Both states are already persisted by GameSession; do not duplicate persistence here.

## Runtime implementation

1. `LaptopCinematicLightPresentation.cs` attaches a private, owned UI material to **only** the existing `UnmodifiedScreenOffPOV` RawImage. Its source texture remains the approved original `LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF`.
2. `Shaders/LaptopCinematicLightStates.shader` works in original, top-left **1672×941 photo pixels**, with independently feathered regions:
   - **Main room:** all other photo pixels outside the architecturally defined left interior doorway, with restrained warm illumination and warm practicals.
   - **Left interior doorway / corridor:** five-sided feathered polygon x≈296–383,y≈70–508, controlled solely by `_HallwayLightOn`; photo blue/night ambience remains even when hallway practicals are OFF.
   - **Door frame/floor spill:** only when hallway is lit, two soft localized areas near the opening. Unlike a full-screen dark rectangle, the source remains detailed.
   - Both OFF: the shader returns the original texture pixel without lighting adjustments. The real laptop screen, standby chibi and powered YOMI are *separate overlays above this backdrop* and are not recolored by the shader.
3. `RokasView` wires `() => session.State.lampOn` and `() => session.HallwayLightOn` into the cinematic constructor and updates via `laptopCinematic.UpdateLighting(dt)` on the host game loop. It uses existing session updates, so light changes made in either scene are automatically visible on entry to the POV.
4. On initial build, `Bind()` calls `Snap()` before the first frame to avoid a wrong-state flash. Changes while the scene exists interpolate over ~0.18 seconds, as in the existing hallway presentation. The same material stays attached through sit, boot, full YOMI handoff and stand/return. `Cleanup()` releases the material and unbinds it.

## State matrix

| Main room light | Hallway light | Result inside laptop POV |
|---|---|---|
| OFF | OFF | Original cold photo, no added warm practicals |
| ON | OFF | Room receives warm lift; doorway retains its original cool/no-electric state |
| OFF | ON | Main room remains cold; corridor doorway becomes lit, soft spill across inner threshold |
| ON | ON | Warm room plus independently illuminated corridor |

The default screen photo is an **OFF** reference. This patch recreates practical illumination through image-space color masks without claiming it is a separately photographed 1:1 ON reference. Fine per-pixel artistic adjustments to the corridor polygon and warm grading should be reviewed from real Game View captures.

## Safety boundaries

- No edits to `screen_calibration_v4.json`, V4 UV mesh/Bezier points, screen mask, chibi, original YOMI screenshot, gold R/E/ESC prompts, audio, hands, POV timing, camera, VN, or combat.
- No new weather/time-of-day logic, unrelated apartment art or changes to the existing HallwayLightPresentation/WorldEffects lighting.
- No color-changing of final YOMI graphics. The shader affects the **underlying photographic scene only**.
- If the new shader asset is absent from an incomplete local checkout, the RawImage keeps displaying the original approved photo (and logs an error), instead of blocking cinematic boot. Import the full branch to activate the feature.

## Unity verification plan

Test all four state combinations by switching lamps in the existing hub and hallway, entering the laptop scene after returning to the main room:

1. OFF / OFF: exact original cool scene. Both night ambient and laptop screen preserved.
2. ON / OFF: warm room, left door dark/cool.
3. OFF / ON: room cool, doorway lit, threshold soft glow.
4. ON / ON: both lit.
5. For every state, sit, open/close YOMI, stand, sit again; check no 1-frame light flash, broken rounded LCD V4 mask, or missing gold UI.
6. In Game View inspect left jamb / door sill for halo seams or spill into furniture. Door polygon is initially authored from the photo and may require visual fine-tuning after a local screenshot.
7. EditMode `LaptopTwoZoneLightingEditModeTests` checks shader loading, four states, immediate entry, independent fades and owned material disposal. The larger Unity CI may still be blocked by unrelated Codex combat WIP.

## Local sync

Canonical user copy: `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`. After Codex stops writing and local changes have been safely preserved, use the existing `ROKAS_SAFE_MERGE_SYNC_V2.bat`. Do not use `git reset --hard` or blindly discard untracked assets.
