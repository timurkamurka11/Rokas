# Keiko Bed Sleep V6 — Oct 24 real Game View correction

Continue **existing draft PR #13**; do not restart, change V16, existing new hand/FBX, smartwatch/OLED/Hand Studio, scene or branch.

## Ground truth — user recording
- Actual user screen recording `bandicam 2026-10-24 01-28-39-360.mp4`, 1920×1040, 29.95fps, 74.90s. Examined 25 overview frames spaced 3s and audio signal:
  - ~12–16s first bed transition home→ceiling appears too abrupt, sometimes with initial lag.
  - 21–42s original new rigged hand and watch functional; **do not alter**.
  - ~42–46s eyes closing effect not distinctly visible.
  - ~46–68s **Keiko dream video DOES play** for ~22sec (three cycles, contrary to user 5–7sec target), audible level in game screen capture only 0.005–0.009 RMS while original V5 AAC loop has ~0.04 RMS.
  - ~68–71s view snaps back to ceiling without visible dark semioval-eye opening. ~71s onward standing lacks the required ocular-close/blackout combination.
- Old `RokasAudio.Tick` independently fades looping room rain and music, but `WorldEffects` triggers randomized synthesized thunder `RokasAudio.PlayThunder` using **separate one-shot effects channels**, which previous ducking did not mute.
- Old `RokasSleepEyeAperture` generated 33 separate 640×360 textures synchronously from first bed click: ~7.6 million pixel passes with repeated Mathf.Exp/Mathf.Pow in the entry path, plausibly causing the lag. Eye mask was also below the dream video and lacked explicit top ordering at wake.

## PR #13 changes (still DRAFT)
- `RokasSleepVideoAndEyes.cs`: reduced cached mask generation to 17×320×180 (~0.98m pixels), removed per-pixel expensive exp/pow for central bridge, retains independent feathered asymmetric left/right oval openings. Exposes `BringToFront()` on both eyelid mask and existing actual Keiko video to explicitly control ScreenSpaceOverlay draw order.
- `RokasBedSleepCinematic.cs`: uses explicit topmost original black curtain for smooth **1.25-sec entry darken + 0.16-sec black hold +1.25-sec reveal** and exit .94s. On confirmed yawn/eyes close, brings oval mask above real ceiling. Displays the actual original Keiko video once for **0.9s fade in + 5.25s middle + 0.9s fade out ~7.05 seconds**, not ~22s. During playback, dream video is above closed eyelids; at wake, the eye mask explicitly rises above ceiling and opens over ~3s of ceiling blur as alarm sounds. After waking and playing the already selected sit/stand foley, the eyes **close again** over 0.85 sec before transition to full black and Hub. Gameplay time jump, save, existing watch and authored UI untouched.
- Original provided `KeikoSleepLoop_WithAudio_V5.mp4` holds a perfectly usable 7.208s stereo AAC track, but `VideoPlayer.audioOutputMode=AudioSource` on Windows appears to have rendered video with *inaudible audio*. Cinematic now separately loads `Resources/BedSleep/Audio/KeikoDreamAudio.wav` — the **exact user's V5 soundtrack decoded from actual Keiko video** (7.208sec, stereo 44.1 kHz PCM), into a dedicated `AudioSource` independent of muted game BGM/rain and fade-synchronized with video alpha. When separate clip is present, the original VideoPlayer audio volume is set to 0 to prevent doubling.
- `RokasAudio.cs`: at the approved `EyesClose` transition (after watch confirmed, not at first bed click), stops any currently playing `HomeThunderDistant` effect clip in its own FX channels and suppresses further `PlayThunder` calls throughout bed sleep until the scene is complete; keeps approved music/rain fade and restores afterward.

## One BAT user handoff
`ROKAS_BED_SLEEP_TRANSITIONS_EYES_AUDIO_V6_D.zip` (966,274 bytes, SHA256 `bb3bd779e622dd7b7f92545bb02001428229f6de12ab86c543e1e37cc0c72cb7`):
- original 7.208-second soundtrack `KeikoDreamAudio.wav` SHA256 `dab61477f5e77a9c54492d038c0213a2b840383e9f9522a4098302d6632d5289`; installer BAT+PowerShell; detailed RU readme. No duplicate video binary.
- Targets user's EXISTING `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` and already installed V4/V5, requires Unity Editor closed; downloads exact three C# source files from immutable GitHub commit `6d484ae599827dd4858a89100a08127106e0905a` with matching Git blob SHA1; **only replaces narrowly-scoped methods/blocks** in local cinematic/audio and entire helper only if it matches known V4/V5/full V6 SHA; backs up every file byte-for-byte under Library; refuses unknown edits to targeted functionality; rollback on errors. Existing GLB/FBX, watch, Hand Studio, HomeView/Hub scene, save, branch untouched. Does not run older installers or Blender.
- ZIP CRC PASS and soundtrack SHA PASS. Installer syntax/static patch markers checked against baseline and latest GitHub source; not executed on the user's Windows PowerShell/Unity here. Real Unity playback/visual/audio acceptance **PENDING**, no production PASS.

## Acceptance tests after user BAT
1. Click bed: no stall or hard cut, smooth fade to entirely black, soft reveal ceiling.
2. Check original 23-joint user hand/watch and authored gold buttons unchanged.
3. Set alarm, confirm; start yawn, music/rain fade. Thunder cannot fire during actual sleep.
4. Two curved dark eyelids visibly close over the real POV, no squared horizontal bars.
5. Chibi Keiko and cloud sequence appears with USER AUDIO audible, plays ~7 sec (one cycle) and fades audio+visual together.
6. Alarm rings as eyes begin opening through **two separately curved oval shutters**, ceiling blur clears naturally.
7. Stand: brief oval eye closure, full black, then smooth reveal Hub and original music/rain resume.

Keep PR #13 draft/unmerged until captured real Unity Game View PASS.
