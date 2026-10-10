# ROKAS Laptop Cinematic V11 — Verified QA / Release Hold

Date: 2026-10-10
Repository: `timurkamurka11/Rokas`
Branch: `codex/laptop-pov-cinematic-20261009`
Existing PR: [#7](https://github.com/timurkamurka11/Rokas/pull/7) — OPEN / DRAFT.
**Exact Unity-tested code HEAD:** `f7fc0779bbe16c1b2dd4ffc25dc4ddc2d69d74dc`.
Any later documentation-only commit must not be mistaken for a newly tested code snapshot.

## Test evidence (actual, not inferred)

| Check | GitHub Actions | Outcome | Limits |
| --- | --- | --- | --- |
| Unity 6000.3.19f1: Power EditMode, cinematic fallback PlayMode, dedicated V11 5-test PlayMode | [38085553868](https://github.com/timurkamurka11/Rokas/actions/runs/38085553868) | **SUCCESS** at code `f7fc077` | Test-only Unity environment; no approved original room, no local SFX |
| Unity 6000.3.19f1: real 49-hand-frames Powered Desktop PlayMode | [38085553865](https://github.com/timurkamurka11/Rokas/actions/runs/38085553865) | **SUCCESS** at code `f7fc077` | Synthetic test-only POV; physical 3D keyboard-depth collision cannot be inferred |
| Unity 6000.3.19f1: real hand art, seat / return / repeated Power V3.3 PlayMode | [38085184270](https://github.com/timurkamurka11/Rokas/actions/runs/38085184270) | **SUCCESS** at `f494e9c` | Prior but compatible code; same pinned 49-frame art |
| Windows PS5.1 installer protection | [38081352711](https://github.com/timurkamurka11/Rokas/actions/runs/38081352711) | **SUCCESS** | Tests existing V10 source installer in a temporary fixture, NOT a new V11 packaged installation |
| Generic project Core behavior | [38085557541](https://github.com/timurkamurka11/Rokas/actions/runs/38085557541) | **FAILURE** | Known project-wide art resolution / audio WAV validation blockers; no global release PASS |

V11 LCD regression discovered real nonopaque pixels (7662 pixels) in the first run [38085187111](https://github.com/timurkamurka11/Rokas/actions/runs/38085187111). Fixed by alpha compositing UI accents into an **opaque** runtime texture in `f7fc077`. Exact rerun `38085553868` subsequently passed all tests. This is a real verified bug fix, not a weakened test.

## Implemented V11 source changes (code-level)

- `LaptopView.cs`: prebuild real YOMI UI under the boot video before video finishes; remove half-second black `CinematicPostBootHold`; track first visible frame and window opacity.
- `LaptopCinematicSequence.cs`: retain original approved POV overlay under YOMI until the new UI is visually ready; instantly prewarm seated POV when closing YOMI; preserve all hand sprites/timing and Power contact logic; drive common room focus camera.
- `RokasView.cs`: atomic panel ordering and underlay during open/close; full live Home camera rig carries world background, weather, CustomGlow and hotspot visuals together.
- `LaptopPhysicalDesktop.cs`: replace fixed physical-desktop image with runtime RGBA YOMI app-catalog/unread/system-clock atlas in the existing calibrated LCD quad; add subtle idle/hover/press response on original PNG controls. This is a **synchronized presentation** of the real YOMI app catalog/state, NOT a full pixel-identical independent YOMI instance or direct app use on the physical LCD. Clicking physical LCD opens the actual full YOMI.
- `RokasAudio.cs`: both sitting and standing events use `Resources.Load<AudioClip>("LaptopCinematic/stand_up")` at the same .77 gain scale, through the existing master/SFX/mute bus. Actual sound loudness, clipping and asset availability cannot be verified by the synthetic cloud scene.
- Dedicated regression `LaptopV11ContinuumPlayModeTests.cs`: nontransparent dynamic LCD, unread update, original PNG interactions, restart/one boot, Home visual parent continuity, no black hold.
- V3.3/V3.4 installed-art tests updated to assert YOMI is composited **above** the outgoing retained POV and the latter is released once YOMI becomes visible. No weakening of the no-Hub-flash safety criterion.

**Hand preservation:** Git comparison of V11 against preceding `c863b588a88eba8398ffdc785ac9a1c8f0ce083f` modifies only the five runtime C# files, Unity tests, and the profile CI workflow. It contains **no modifications** to Blender source, generated hand assets, UV, animation, skin, sleeve, geometry or 49 PNG frames. Pinned Blender production source `a38e6cba9b1f34fd2976ade27f50cdf3328e9a7f`, Blender→Unity run [38077862962](https://github.com/timurkamurka11/Rokas/actions/runs/38077862962).

## Before / After: engineering fixes, native visual QA still outstanding

| Reported issue | V11 engineering change | Evidence / limitation |
| --- | --- | --- |
| Interactive yellow lines vanish at interaction | Live Home background + WorldEffects + interactive Home UI share one focal transform; never globally disable CustomGlow on entering cinematic | Home rig PlayMode PASS; the user's original room/glow frames are NOT visually validated |
| Sharp standard-image substitution on camera approach | Focus entire live Home 2.5D scene, fade into preserved original POV (no stale `OriginalHomeApproach` art layer) | Code-level continuity; exact 2D-to-POV perspective match still requires user real-photo frame-by-frame review |
| Static, unresponsive original PNGs | Idle pulse, hover glow/scale and press compress, with OnDisable cleanup | Hover PlayMode PASS; three actual local PNG images must be checked |
| Physical laptop still desktop screenshot | Opaque runtime LCD renders shared app catalog, unread messages and OS date/time; click opens actual YOMI | Unread-pixel/alpha PlayMode PASS; per-pixel parity with fullscreen YOMI not claimed |
| Repeated Power/Boot | Existing `LaptopPowerSession` lifecycle retained; first completed boot persists until game restart | Powered Desktop PlayMode PASS |
| Blue Power light timing | Preserved pinned hand-contact timeline, LED extinguished at skin contact | Powered Desktop PlayMode PASS |
| Black gap after Boot | Prewarmed YOMI behind video, no 0.5s dark hold | V11 prewarm PlayMode PASS; real LaptopBoot.mp4 visual transition pending |
| Flash of Hub on closing YOMI | Prewarm returning POV before closing YOMI; panel renders above retained POV and fades away | V3.3 seated-return PlayMode PASS; 1-frame real footage pending |
| Wrong standing/return sequence | YOMI → existing powered seated POV → explicit Stand → live Hub | V3.3 PlayMode PASS in test-only scene |
| Different/quiet sit/stand sounds | Both events point to original Stand Up SFX; unified .77 level preserves master/mute | Source verified; actual stand_up asset is absent from cloud CI; NO listening/clipping acceptance |

## Acceptance status and blockers

**Code implementation:** changes entered into existing PR #7. **Focused cloud Unity QA: PASS** as above.
**User-approved real POV normal-speed gameplay video: NOT GENERATED.**
**Before/after captured from original room video: NOT VERIFIED.**
**Real user PNG visual hover + original-photo color/perspective: PENDING.**
**Real stand_up audio presence/mix/peak level: PENDING.**
**User local main Unity project at `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`: NOT UPDATED by cloud tooling / NOT VERIFIED.**
**Project-wide generic Core behavior CI: FAIL.**
**Production FINAL package/manifest: BLOCKED.** Do not silently grant `VISUAL_AND_UNITY_QA_APPROVED`; V10 BAT/ZIP only installed the pinned 49 hand PNGs and manifest, NOT these V11 C# changes.

## Precise next action

1. Safely synchronize **only five V11 presentation C# files** into local working copy after backing up and checking for local edits and divergence. Original 49 hand resources remain pinned. Do NOT blindly replace customized local files. The prior V10 asset BAT is not a V11 runtime-code updater.
2. With original approved POV + existing user PNG/SFX and correct LaptopBoot.mp4, record local Unity Game View sequence: Hub with CustomGlow → E/approach → seated POV → hand to blue key/contact → boot → YOMI → close (NO Hub frame) → seated live LCD → stand → Hub → reenter powered laptop without second boot. Review frame-by-frame in original room.
3. Validate audio shared Stand Up clip for two distinct actions under Master SFX and mute at full peak, compare mixed loudness without clipping; evaluate original UI color/idle-hover-press with user artwork and color grading.
4. If and only if native video and local acceptance pass, sign off a safe V11 release/rollback installer with source SHA and actual payload SHA-256. Investigate generic Core CI independently without expanding the FPS-hand scope.

**This report is intentionally evidence-bounded.** Cloud test-only synthetic POV and absence of actual audio/scene cannot be represented as native visual approval.


## V11 Windows code-sync review supplement (post-runtime QA)

- V11 code-only integration tool (does not alter runtime source): `Tools/LaptopCinematic/v11_code_review/V11_CODE_SYNC.ps1`. It is **not** the V10 art installer, and it never touches 49 hand PNGs or other assets.
- Isolated Windows PS5.1 security and rollback test: [38086344713](https://github.com/timurkamurka11/Rokas/actions/runs/38086344713) **SUCCESS**; review-only ZIP artifact 11682232992. Exact extracted SHA256 `d258596bfd0d9fa4bfb88888f09f1a4443eb6a3e7f9080827e22afe1b3724fb9`.
- Default CLI read-only audit: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\V11_CODE_SYNC.ps1 -Target 'D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY'`. It checks 5 C# baseline vs tested V11 source hashes. Any user-modified/different source is blocked from automatic replacement.
- Apply only with explicit `-Mode Apply -ConfirmCodeSync` and Unity closed; rollback requires `-Mode Rollback -ConfirmCodeSync` and hash-consistent original backup. No local changes were performed from cloud.
- After safety confirmation at HEAD, pre-existing V10 installer Windows safety [38086379520](https://github.com/timurkamurka11/Rokas/actions/runs/38086379520) also **SUCCESS**; base Blender/Unity gameplay dependencies remain unchanged.
- **Final V11 release remains blocked** by real approved-room 2.5D screen visual QA, user PNG hover, actual audio listening/peaks, native Game View capture, and project-wide Core behavior validation.
