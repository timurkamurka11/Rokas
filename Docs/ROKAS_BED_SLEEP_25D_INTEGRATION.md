# ROKAS main-room bed sleep — native 2.5D handoff

## Compatibility

ROKAS is **not** a 3D free-roam bedroom. The main-room art is shown on the 1920×1080 screen-space Canvas. `HomeView` owns the existing bed hotspot, `RokasView.StartBedSleep()` manages interaction lock and profile protection, and `RokasBedSleepCinematic` owns the single in-hub presentation state. It preserves Laptop Cinematic V11, Hallway, combat and dust.

The original Argon source module is preserved as **reference-only** in the install ZIP, not copied as 10 unbound live scripts into `Assets`: it assumes a separate scene/prefab workflow and contains no existing `RokasView` adapter. Installing it directly in addition to the native controller can introduce competing sleep states and visuals.

## Assets required for the approved two photographic shots

The GitHub connector used to create this change could write source text, but did not upload binary PNG references. Install the separately supplied **ROKAS_BedSleep_2_5D_ReferenceAssets_V2.zip** using its `INSTALL_BED_SLEEP_ASSETS.bat` or manually copy:

- `Assets/Rokas/Resources/BedSleep/BedCeilingPOV.png` — approved lying-on-bed ceiling shot; no hand
- `Assets/Rokas/Resources/BedSleep/BedWatchArmPOV.png` — approved lying-on-bed hand/watch shot
- `Assets/Rokas/Resources/BedSleep/BedCeilingPOV_Soft.jpg` — softened ceiling plate for optical transition

The C# code loads these images via `Resources.Load<Texture2D>("BedSleep/...")`. It never changes `Camera.main` and never creates runtime capsule/cube arm geometry. **Both images must be present** to enable the alarm CTA; otherwise Unity warns and the button stays disabled with an explanatory label. The installer never overwrites different existing files.

## User flow

1. Click the bed hotspot in `HomeView`.
2. Existing `RokasView` blocks other home interactions. Bed presentation darkens the room for 0.60 s, swaps to the ceiling POV at full darkness and reopens in 0.65 s.
3. Ceiling presentation breathes subtly; after 0.35 s the bottom **ПОСТАВИТЬ БУДИЛЬНИК** CTA fades up.
4. CTA shows the approved hand/watch POV with 0.82 s opacity handoff. Use scroll, keyboard arrows, or mouse drag on the watch. Each step is 30 minutes; tick audio is synthesized in memory.
5. Confirm the alarm. The watch controls hide, the hand view dissolves away in 0.76 s, and the eyelids close with a preparatory heavy blink.
6. Only at `SleepCommitted` are `hubClockHours`, `hubDay`, `hubFatigue`, `hubSleepCount` updated and the existing `save` action called. `Escape` before commit restores scene without changing the save.

## Verification and limitations

Unit/EditMode tests cover quantized durations, crossing midnight, saved fields and eleven deterministic presentation phases. Static CI checks that no primitive world-space hand was reintroduced. Unity CI can compile **code**, but is not a substitute for live visual verification with the installed PNGs.

The provided two photographs are static full-frame composites. They have not been reconstructed as an editable SkinnedMeshRenderer, nor does the photographed watch have a physically rotatable crown. The central live OLED numbers are aligned approximately to the reference photo and require a 1920×1080 Unity screenshot for final placement. Likewise, transitioning from one full-frame reference photo to another is a **photographic dissolve**, not yet a true independently moving hand. This is deliberately preferred over Argon's flat cartoon arm and the prior primitive FPS geometry.

Do not describe it as a fully rigged high-definition 3D hand or claim that Argon's 10 separate scripts are all live inside this build.
