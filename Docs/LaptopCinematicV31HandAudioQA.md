# ROKAS Laptop Cinematic V3.1 — user image and SFX QA

**Scope:** Updates to the existing Laptop Cinematic on `codex/laptop-pov-cinematic-20261009`. Preserve the approved POV, intended blur, current room artwork, and original LaptopBoot.mp4. Never change unrelated ROKAS systems.

## User-supplied visual defect evidence (2026-10-10)
Images are held in the user-delivered handoff ZIP `ROKAS_LAPTOP_V3_1_HAND_AUDIO_REFERENCE_PACK.zip`, not embedded in public GitHub:
- `References/01_Hand_WholeScene.png`: existing overall right-hand cinematic
- `References/02_TooThick_NoNails.png`: excessive finger volume, balloon-like tips, no visibly distinct nails, uneven skin aging
- `References/03_Wrist_CuffGap.png`: an apparent detachment / transparent gap between wrist and dark free-home sleeve

Required: slimmer anatomically credible finger silhouettes (~5–10% reduction only where swollen), natural PIP/DIP articulation, five **thin curved keratin nail plates** physically following their distal phalanges, restrained warm skin shading, no deep aged wrinkles or glossy plastic. Rib-knit cuff must overlap the wrist with no alpha gap or floating disc, preserve naturally **loose graphite fabric**. Scrutinize actual alpha frames at 100–200% and transitions, not only Blender logs. Preserve real fingertip Power contact on (1040,708) source 1672×941 POV.

**Important:** the script `Tools/LaptopCinematic/cloud_blender_right_hand_production.py` now includes first-pass finger slim corrections and nail geometry, but this change does not count as a visual PASS until the rendered closeups prove the nails are visible and the cuff seam is resolved.

## User-owned sound mapping

| Original sound | Runtime WAV in Unity Resources | Event |
| --- | --- | --- |
| `OriginalAudio/PC_CLICK_ON.mp3` | `LaptopCinematic/power_click_user.wav` | Exactly one at actual finger contact, **not** when E is pressed |
| `OriginalAudio/Sitting_sound.mp3` | `LaptopCinematic/sitting.wav` | When the Home laptop hotspot successfully enters cinematic |
| `OriginalAudio/Stand_up_sound_effect.mp3` | `LaptopCinematic/stand_up.wav` | Esc / on-screen Back from PreBootChoice, without starting boot |

Each WAV is PCM 16-bit stereo 48 kHz for compatibility with existing ROKAS audio importer validation. `PC CLICK ON.mp3` was uploaded twice byte-identically: do not duplicate it. The first mechanical click was isolated and faded into a 0.31 second runtime WAV; the two-click original remains in ZIP. Sitting 1.869s, Stand up 1.057s. Do not use the older procedural `power_click.wav` when `power_click_user.wav` is installed. All audio plays through `RokasAudio` and current user SFX/master volume and mute. Do not play stand-up when a separate scene is disposed.

Sound WAV binaries and reference screenshots are delivered in the companion user handoff ZIP for local installation, **not currently committed to GitHub**. Therefore cloud CI without these files cannot verify user-owned SFX timbre.

## UX state machine V3
`IdleRoom → LaptopApproach` (sit sound + camera settle) `→ PreBootChoice` (await indefinitely, blue physical power LED, near-black masked LCD + dim NO SIGNAL, E/Power and Esc/Back hints).
- **E or click the actual Power key:** hide hints, animate right-hand reach, play single click at physical contact, blue LCD wake, original **one** boot video, 0.5s after video completion, YOMI.
- **Esc or on-screen Back while awaiting:** play user stand-up, smooth return, restore Home hotspot, no LaptopBoot or YOMI. Once power press starts, disallow accidental boot before click.

## Explicit release gates
1. Body/wrist geometry match across ALL 49 frames; fingernails detectable in full-resolution Power-closeup; no cuff gap.
2. User-provided SFX actually present under Resources and imported to Unity, event synchronized with animation and emitted once.
3. Visible physical finger contact precedes power glow and mechanical click matches its frame.
4. PlayMode test explicitly verifies no boot before E, Esc cancels, E/Power once, UI state returns on repeated entry.
5. Real Unity pixels/screenshots and an audible gameplay capture, not only static C# tests.
6. Local D: copy remains unchanged until user executes safe installer; cloud tests cannot claim local acceptance.
