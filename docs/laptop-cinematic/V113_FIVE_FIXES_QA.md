# ROKAS Laptop Cinematic V11.3 — original UI, bounce SFX, timing and true YOMI mirror

Branch: `codex/fidelity3-resume-5779190`.
Runtime code acceptance source: `06a37d3701a0607df42e9c00bc3ad3a0cd4b1670`.
Scope: user video `bandicam 2026-10-11 02-48-53-902.mp4`, particularly seconds 19–25; original gold PNGs and user MP3.

## Implemented changes (original assets preserved)

1. **Gold prompt visibility**: Original `prompt_power`, `prompt_open`, `prompt_back` and `back_arrow` are displayed unmodified and white-tinted. An extra non-interactive layer of the *same original PNG* restores golden translucency over the illuminated desk without recoloring or recreating the source. Hover animation on the original button stays active.
2. **Chibi collision SFX**: Exact original `free-sound-1674855877.mp3` imported at `Assets/Rokas/Resources/LaptopCinematic/chibi_bounce.mp3` with stable Unity AudioImporter meta. Git blob SHA `b1093803a55708c55665cc4eef4d4c0a6005acba` equals user-provided original. Each actual direction-changing wall contact emits one event. `RokasAudio.PlayChibiBounce` plays at 0.52 times master/SFX volume, respects mute, uses a dedicated AudioSource and restarts instead of layering overlapping sounds.
3. **No early standby disappearance**: `TryPressPower` latches input but no longer hides chibi/PNG immediately. The hand reach and calibrated physical Power contact continue with standby art visible. On initial first boot, standby remains until an actual first `LaptopBoot.mp4` frame is render-ready.
4. **No pre-boot YOMI flash**: An opaque black `LaptopBootPrivacyShield` occludes prewarmed desktop beneath VideoPlayer while it prepares. The modal layer stays *behind* the real seated POV until the first actual boot frame is ready, when it is raised and standby elements hidden in the same Tick. The shield is destroyed only after the boot finishes.
5. **Original YOMI 1:1 physical monitor**: In graphical Unity, `LaptopYomiMirror.Capture` copies pixels from the existing `LaptopScreen` RectTransform (original artwork/icons/text/wallpaper) at the end of the last fully displayed fullscreen YOMI frame, before closing. This genuine captured image replaces synthetic mini-atlas/glyph overlays on the physical LCD. Contain-fit preserves native aspect ratio instead of stretching. The screenshot represents the YOMI at the last close; it is not a continuously re-rendered live desktop. The old mini-atlas remains only as graceful fallback if capturing is unavailable.

## CI acceptance — exact code commit 06a37d3

- [Unity 6000.3 art-integrated PlayMode](https://github.com/timurkamurka11/Rokas/actions/runs/38097834914): **PASS 2/2**; collision callback and original asset/standby regressions. Real original 49 hand PNGs, chibi, gold PNGs and user MP3 all loaded. Inspect `playmode-results.xml` and `playmode.log`; no invalid PNG GUID or missing chibi warning.
- [Unity 6000.3 focused EditMode and PlayMode](https://github.com/timurkamurka11/Rokas/actions/runs/38097834917): all three primary Unity steps succeeded (EditMode, fallback PlayMode, V11 continuum PlayMode).
- MP3 bytes verified against exact original Git blob SHA; no transcoding or waveform replacement.

Important: GitHub GameCI uses a *synthetic test-only* 1672x941 room photograph. The end-of-frame *screen capture* is deliberately bypassed in headless/batch mode because WaitForEndOfFrame does not run reliably there. Thus CI verifies compile, state transitions, assets, bounce events and no regressions, **not the final pixel appearance of the real local QHD Game View**, perfect screen-polygon fit, golden brightness perceived against the original photo, or the actually audible in-game mix. These must be accepted in the existing Windows Unity project. Do not describe them as complete local visual QA.

## Scope safety

No changes to the approved original POV photo, 49 hand frames, Blender source, unrelated Combat, VN or asset packs. Runtime changes were restricted to `LaptopCinematicSequence`, `LaptopPhysicalDesktop`, `LaptopView`, `RokasView`, `RokasAudio` and targeted PlayMode tests. The new sound is an original user-provided MP3, not proprietary DD2 audio. Do not remove user local changes via destructive git operations.

## Windows manual review after safe pull

Close Unity, update only the existing canonical Unity project `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` on its tracking branch. If untracked PNGs block the pull, back them up first (e.g., previously prepared safe pull script); never use `git reset --hard` or `git clean -fd` on user assets. Verify on QHD Game View:

1. Before Power, real DVD chibi and bright gold PNGs; listen for one moderate click sound on every wall reflection.
2. Press E; PNG/chibi persist while real hand reaches Power, then disappear simultaneously with actual boot video, not earlier.
3. Inspect 19–25s playback path at native FPS: no false loaded YOMI frame before original loading.
4. After YOMI finishes, close and return to seated POV; physical LCD must show exact captured fullscreen artwork, with no duplicate synthetic icons and correct aspect ratio.
5. Reopen/close repeatedly; verify one boot per process, no regressions to interactions, user SFX mute, Windows focus and UI navigation.

