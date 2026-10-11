# ROKAS Laptop Cinematic V12.1 — unified room/hallway lighting

## Goal

The seated 1672×941 laptop POV must reflect the **same two existing electrical switches** that control the hub and hallway. No new gameplay switches, independent flags, or permanent photo replacement.

- **Main room:** `session.State.lampOn`, the original HomeView floor/desk lamp switch.
- **Hallway:** `session.HallwayLightOn`, the original HallwayView wall switch.
- Both states are already persisted by GameSession; do not duplicate persistence here.

## Runtime implementation

1. `LaptopCinematicLightPresentation.cs` attaches a private, owned UI material to **only** the existing `UnmodifiedScreenOffPOV` RawImage. Its source texture remains the approved original `LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF`.
2. `Shaders/LaptopCinematicLightStates.shader` blends TWO aligned 1672×941 photographic references (the existing warm ON PNG and the user-supplied exact-position blue/dark OFF PNG) in original top-left pixels with independently feathered regions:
   - **Main room:** all other photo pixels outside the architecturally defined left interior doorway, with restrained warm illumination and warm practicals.
   - **Left interior doorway / corridor:** six-sided feathered polygon x≈294–388,y≈0–511, extended over the curtain and photographed bottom sill, controlled solely by `_HallwayLightOn`; photo blue/night ambience remains even when hallway practicals are OFF.
   - **Door frame/floor spill:** only when hallway is lit, two soft localized areas near the opening. Unlike a full-screen dark rectangle, the source remains detailed.
   - Both OFF: the shader renders the new exact photographic OFF PNG; both ON: it renders the existing photographic ON PNG with no grading. Mixed states use a separate door polygon, plus a restrained photographic light spill. The real laptop screen, standby chibi and powered YOMI are *separate overlays above this backdrop* and are not recolored by the shader.
3. `RokasView` wires `() => session.State.lampOn` and `() => session.HallwayLightOn` into the cinematic constructor and updates via `laptopCinematic.UpdateLighting(dt)` on the host game loop. It uses existing session updates, so light changes made in either scene are automatically visible on entry to the POV.
4. On initial build, `Bind()` calls `Snap()` before the first frame to avoid a wrong-state flash. Changes while the scene exists interpolate over ~0.18 seconds, as in the existing hallway presentation. The same material stays attached through sit, boot, full YOMI handoff and stand/return. `Cleanup()` releases the material and unbinds it.

## State matrix

| Main room light | Hallway light | Result inside laptop POV |
|---|---|---|
| OFF | OFF | User-supplied exact 1672×941 dark-blue photographic OFF reference |
| ON | OFF | Room receives warm lift; doorway retains its original cool/no-electric state |
| OFF | ON | Main room remains cold; corridor doorway becomes lit, soft spill across inner threshold |
| ON | ON | Warm room plus independently illuminated corridor |

The legacy resource name includes `screen_off` because the LAPTOP SCREEN is OFF, not because apartment practical lights are OFF. That legacy photograph is visually warm / room-lit and remains the full ON reference. The user-provided 1672×941 dark photograph is the genuine room-and-corridor OFF reference. **Both are required** for high-fidelity mixed states; never infer room lighting from the laptop-screen state. Fine per-pixel artistic adjustments to the corridor polygon and warm grading should be reviewed from real Game View captures.

## Safety boundaries

- No edits to `screen_calibration_v4.json`, V4 UV mesh/Bezier points, screen mask, chibi, original YOMI screenshot, gold R/E/ESC prompts, audio, hands, POV timing, camera, VN, or combat.
- No new weather/time-of-day logic, unrelated apartment art or changes to the existing HallwayLightPresentation/WorldEffects lighting.
- No color-changing of final YOMI graphics. The shader affects the **underlying photographic scene only**.
- If the shader is absent, the original photo remains visible with an error. If the separately supplied dark OFF PNG is missing, a clearly logged approximate fallback remains, but this is NOT an acceptable final photographic result. The asset has to be installed under `Assets/Rokas/Resources/LaptopCinematic/LaptopPOV_lights_off_APPROVED.png` with exactly 1672×941 pixels. The photo's binary data is supplied in the accompanying local installation archive, not in this text-only GitHub code checkpoint.

## Unity verification plan

Test all four state combinations by switching lamps in the existing hub and hallway, entering the laptop scene after returning to the main room:

1. OFF / OFF: exact supplied blue/dark photographed scene (all warm baked-in lights absent). Both night ambient and laptop screen preserved.
2. ON / OFF: existing exact warm original for the room, cool OFF photograph only within left-door polygon.
3. OFF / ON: supplied dark photo throughout the room; warm photographic corridor visible only through left doorway and a soft floor/jamb spill.
4. ON / ON: existing exact full warm photographed scene, no overall color grading.
5. For every state, sit, open/close YOMI, stand, sit again; check no 1-frame light flash, broken rounded LCD V4 mask, or missing gold UI.
6. In Game View inspect left jamb / door sill for halo seams or spill into furniture. Door polygon is initially authored from the photo and may require visual fine-tuning after a local screenshot.
7. EditMode `LaptopTwoZoneLightingEditModeTests` checks shader loading, four states, immediate entry, independent fades and owned material disposal. The larger Unity CI may still be blocked by unrelated Codex combat WIP.

## Local sync

Canonical user copy: `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`. After Codex stops writing and local changes have been safely preserved, use the existing `ROKAS_SAFE_MERGE_SYNC_V2.bat`. Do not use `git reset --hard` or blindly discard untracked assets.


## Visual diagnostic / release gate

- Select `RokasCanvas / AuthoredStage / Transitions / LaptopCinematicOverlay / ApprovedPOV / UnmodifiedScreenOffPOV` during Play Mode.
- Its RawImage material must be `ROKAS-Laptop-Two-Zone-Lighting` with `_OffPhotoReady = 1` and a non-null `_OffTex`. Missing resource logs `ROKAS laptop lighting: the 1672x941 photographic OFF reference is missing`.
- Inspector `Show zone mask (diagnostic only)` at 1 colors the corridor green, room blue and spill red; return it to 0 after checking the mask.
- Capture *four actual Unity Game View screenshots* with the two physical switches and inspect the desk, window, curtain, left door lintel/sill and spilled light. Code-level test passes do not replace visual acceptance.
- The local canonical working copy may have unpublished commits and unstaged unrelated asset .meta changes. Preserve them; no reset/clean/force checkout. Do not claim a full Unity test PASS until Unity compilation and render evidence are available.

## V3.1 STRICT PHOTOGRAPHIC MODE

The user's Game View recording proved that grading the warm ON image produces visible glowing lamps underneath the dark filter. This is **rejected**. Shader code no longer contains a grading fallback; `LaptopCinematicLightPresentation.Bind` refuses to attach the lighting material when either photographic source is missing or of the wrong dimensions. A clear Unity Console error identifies the missing resource. Correct installation is the bundled exact user's OFF asset at `Assets/Rokas/Resources/LaptopCinematic/LaptopPOV_lights_off_APPROVED.png`.

`_MainTex` is the old warm ON photo (name includes screen_off ONLY because the LCD is off). `_OffTex` is the actual supplied dark-blue photo. `roomMask = 1 - CorridorDoorMask`, `hallwayMask = CorridorDoorMask`, and `spillMask` covers only the nearby jamb and threshold. Explicitly check physical pixel alignment and preserve all laptop overlays. Never claim that the non-installed OFF photo is available merely because the shader was successfully imported.
