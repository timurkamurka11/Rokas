# ROKAS Laptop Cinematic V11.1 — Main Local Combat Integration

Status: **code implemented, focused cloud Unity CI PASS; native local-asset QA remains with user**.

- Canonical main user branch: `codex/fidelity3-resume-5779190`.
- Last fully tested code HEAD: `8af08478922fad9d3504cf215d5e046f4124550c`.
- Focused Unity compile/EditMode/fallback/5 V11 PlayMode PASS: [38090332806](https://github.com/timurkamurka11/Rokas/actions/runs/38090332806).
- Art-integrated GameCI PlayMode PASS with the **same pinned 49 hand PNGs, 24fps manifest and TEST-ONLY synthetic approved-size POV**: [38090332813](https://github.com/timurkamurka11/Rokas/actions/runs/38090332813).
- Last art-integrated case: `LaptopV111SeamlessPOVPlayModeTests.ExistingPOVFillsFrameAndSurvivesYomiWithoutHubBackground` **Passed**. Covers wide/height overscan, unpowered NO SIGNAL, contact-time power latch, YOMI stays over original seated POV, powered live screen and reopen with no repeat boot. This is functional verification, not the user's original photographed-room pixel comparison.
- Baseline local Combat user checkpoint `2b8666a7ba8481524aea21c5bf457d696f02fd5f`, local Safe Mode fix `65176501bb3f2782bea9094bdcbaa1a5f48ca9fd`; special field `ResearchRefreshObserved` preserved in `RokasView`. **No Combat or VN source files were replaced**.

## Nine approved fixes in runtime

1. **Camera 0.72s**: original 1.8s camera phase progresses 2.5× faster, matching 0.72s stand-up reverse. 49 hand frames / finger contact and Power timeline remain at their original timestamps and 24fps.
2. **Brightness**: original gold PNGs rendered untinted Color.white with hover-only brightness gain, no resting dark pulse; arrow Button ColorTint removed.
3. **Edge stripes**: approved POV texture uses fill/cover 1.2% overscan instead of leave-margins contain; original pixel artwork is untouched.
4. **Room perspective**: existing live Home camera rig and CustomGlow stay composited beneath POV; no disconnected screenshot is introduced.
5. **Physical seated background**: YOMI keeps the same live seated POV below the modal, including visible margins, for the whole visit.
6. **Open YOMI gold control**: dynamically rendered luminous gold button with the correct text, hover/pulse/press and click target.
7. **Physical LCD**: ON state uses the runtime YOMI-like 4-column app layout, actual 9 Russian app names/colors, live unread status and real OS clock; it is passive except for opening real YOMI. This is an in-world UI presentation, **not a pixel-identical offscreen capture of the fullscreen Canvas**.
8. **Power persistence**: initial NO SIGNAL retained; completed Power latches at true index contact. Boot video plays once; returning to seated POV reuses the same photo; powered state persists for game session until restart.
9. **Audio**: both entering and standing use the same original Stand Up clip with gain raised .77→1.00 (**+29.9%**, safe capped master/SFX/mute path). Actual loudness/clipping of the user's local source audio must be listened to in local Unity.

**Full-resolution texture support restored**: `Assets/Rokas/Editor/LaptopCinematicTextureImporter.cs` carries NPOT None / maxTextureSize=4096 / uncompressed import / alpha. The earlier art-test failure was caused by **this importer being absent from the user branch**, not by hand animation or code regression. The corrected code and importer passed the full art-integrated Unity test.

## IMPORTANT: one-time local Git transition

Older BAT packages installed these **three compiler-related files plus Unity .meta files without tracking them in Git**:

- `Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs[.meta]`
- `Assets/Rokas/Scripts/Presentation/LaptopPhysicalDesktop.cs[.meta]`
- `Assets/Rokas/Scripts/Presentation/LaptopPowerTimeline.cs[.meta]`

The original importer `Assets/Rokas/Editor/LaptopCinematicTextureImporter.cs[.meta]` might also have been created locally untracked. The first `git pull` to receive now-tracked source files may **block rather than overwrite untracked files**. This is a Git safety feature; do not `git reset --hard` or `git clean`.

Close Unity. In the project-root CMD, run once:

```bat
powershell -NoProfile -Command "$b='D:\Rokas\ROKAS_V111_BEFORE_PULL_BACKUP'; New-Item -ItemType Directory -Force -Path $b | Out-Null; @('Assets\Rokas\Scripts\Presentation\LaptopCinematicSequence.cs','Assets\Rokas\Scripts\Presentation\LaptopCinematicSequence.cs.meta','Assets\Rokas\Scripts\Presentation\LaptopPhysicalDesktop.cs','Assets\Rokas\Scripts\Presentation\LaptopPhysicalDesktop.cs.meta','Assets\Rokas\Scripts\Presentation\LaptopPowerTimeline.cs','Assets\Rokas\Scripts\Presentation\LaptopPowerTimeline.cs.meta','Assets\Rokas\Editor\LaptopCinematicTextureImporter.cs','Assets\Rokas\Editor\LaptopCinematicTextureImporter.cs.meta') | ForEach-Object { if(Test-Path -LiteralPath $_) { $dst=Join-Path $b $_; New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent)|Out-Null; Move-Item -LiteralPath $_ -Destination $dst -ErrorAction Stop } }"
git pull --ff-only
```

The first line moves only these eight specific existing local source/meta paths into a **backup outside the Unity project**, preserving their names and subdirectories; it does **not** touch original approved POV art, 49 sprites, UI PNGs, audio files, combat, VN or any other asset. It is not an executable BAT artifact.

After the one-time transition, use regular `git pull` from the existing tracking branch. If Git refuses because of other uncommitted changes, do not reset; inspect `git status`.

## Acceptance boundary
- Focused cloud Unity QA: PASS on tested code sha.
- Original local full-speed gameplay after installing this update: not yet reviewed.
- User-approved photo brightness/color/edge quality and local WAV mix/peaks: not independently verified in CI.
- Whole project global CI: not claimed green; unrelated preexisting assets/audio issues exist.
