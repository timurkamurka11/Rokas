# ROKAS Laptop Cinematic — Persistent Continuum Status
Updated: 2026-10-10. Verified against GitHub Actions before this checkpoint commit.
Branch: codex/laptop-pov-cinematic-20261009
HEAD before checkpoint commit: cb0e15bbc787ca5e6d36c450a3d7ea0a3337f472
PR: https://github.com/timurkamurka11/Rokas/pull/7 — open draft, unmerged. PR #97 not found.
Local D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY NOT modified from cloud.

## Latest Blender
- Full: https://github.com/timurkamurka11/Rokas/actions/runs/38076165129, Blender art job SUCCESS; overall run FAILURE because Unity Game View PlayMode failed.
- Raw source 49 PNG + editable ROKAS_RightHand_Editable.blend: artifact 11678497101, source SHA cb0e15bbc787ca5e6d36c450a3d7ea0a3337f472.
- Cropped Unity Resources with manifest: artifact 11678691854, same source SHA.
- Unity Game View diagnostic artifact 11679011805; job failure requires separate investigation.
- Four strategic Blender poses: run 38076165131 SUCCESS, artifact 11679140574.
- Generator: Tools/LaptopCinematic/cloud_blender_right_hand_production.py.
- Full workflow: .github/workflows/laptop-blender-to-unity.yml.
- Blender 4.5.14, 49 frames at 24fps, 1672x941 RGBA, Power screen anchor (1040,708). Historic rig 16 bones, ~8120 vertices; reconfirm actual report.

## Unity and QA
- Baseline Cinematic Unity run 38076165135 SUCCESS at source cb0e15bb; does not establish actual hand visual quality.
- Generic Core run 38076167226 FAILURE.
- Earlier independent Unity offscreen run 38068226150 SUCCESS on older source d182486a, not confirmation of new render.
- Blender frame-by-frame visual QA, complete Power surface penetration, nail/cuff appearance not yet marked PASS.
- Runtime: Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs.
- Approved room/background, E/Esc/YOMI/audio/UI and camera must be preserved.
- Final installer based on new 49 frames NOT verified or released. Rollback script not confirmed in branch; do not run destructive tests.

## Latest V8 visual QA and fix batch,- Latest original Blender frames inspected: source cb0e15bb, 49/49 PNG valid, original report 16 bones/8120 vertices, contact skin screen positions 1040,708 -> 1040,710.2 -> 1040,708 for frames 30-34.,- Visual QA: FAIL for conspicuously raised, pale oval nail plates; the index finger screen-plane contact alone does not establish actual collision with laptop surface (photo-based background).,- New source patch prepared with smaller, embedded nail plates and less prominent nail surfaces. Source re-render required; prior 49 frames must NOT be labeled final.,- Real Unity Game View failure diagnosed from artifacts/playmode-results.xml: 'No real Game View PNG produced in cloud Unity batch mode' at the old ScreenCapture test.,- Real Power capture PlayMode updated to click Power after PreBootChoice and use RenderTexture screenshots via the existing verified helper. Await new run; no PASS claim yet.,- Avoid releasing old frame artifact 11678497101 as new polish.,,## Precise next action
1. Fetch NEW Blender artifacts after this fix batch. Inspect all 49 frames, production_report.json, nail/cuff closeups and video; only use new source commit SHA. Previous cb0e15bb had visual FAIL.
2. Check current full Blender→Unity run and new RenderTexture Power click capture, not the old screenshot artifact. Do not mark completed before Unity test proves real power frames.
3. Recheck UI, audio, desktop/system time/YOMI, scene interaction and test outputs on the same source SHA.
4. Build only safe, verified installer with correct 49 new frames and backups, never overwrite local D: copy remotely.
5. Update this checkpoint with new run/artifact/commit IDs and remaining work. Avoid frequent redundant CI polling.

## V8 source patch evidence (2026-10-10, 19:05 UTC)
- Latest art source SHA before this checkpoint: a38e6cba9b1f34fd2976ade27f50cdf3328e9a7f (inset nail geometry, Power RenderTexture C# test).
- Full new Blender→Unity run: https://github.com/timurkamurka11/Rokas/actions/runs/38077862962, status IN PROGRESS, Blender rendering 49 frames at last inspection. DO NOT start duplicate.
- Four-pose Blender diagnostics: run 38077862965 SUCCESS, artifact 11678567790, source SHA a38e6cba. Nail-only mask reduced from 490 to 220 opaque pixels at reduced-size QA frame; full-size 49-frame Visual QA still PENDING.
- Offscreen Unity proof: run 38077863044 SUCCESS, artifact 11678508307, BUT workflow uses old frame artifact from run 38067367922. NOT proof for a38e6cba hand resources.
- Last full art QA on cb0e15bb: all 49 frames and full Unity crop reconstruction structurally PASS. Visual FAIL for pale detached-looking nails; corrected candidate requires new full render.
- Installer and rollback drafts exist only as UNRELEASED working-container templates; no production installer has been signed off. Local D: not touched.
- NEXT: wait for run 38077862962 art job to finish, download raw+Unity artifacts from that exact run, inspect full 49-frame motion and finger contact before creating release. Verify real Power interaction in Unity capture job. Do not mistake old offscreen artifact for new version.

## Power LED contact-timing fix candidate
- Found source defect: SetPromptVisibility(false) turned blue Power LED off immediately on E/Power confirmation, before the animated fingertip reaches the physical button.
- Patch keeps the standby LED visible during hand approach and hides it at LaptopPowerTimeline.ContactTime; PlayMode test extended to check before/after contact. Requires new Unity run, no PASS claim until tested.
- No alterations to user-approved background, UI textures, SFX, persistence, YOMI, or Blender art from source a38e6cba.

## V8 FULL NATIVE RENDER VERIFIED (2026-10-10, approx 19:15 UTC)
- Source SHA a38e6cba9b1f34fd2976ade27f50cdf3328e9a7f.
- Full Blender art job of run 38077862962 completed SUCCESS (overall run Unity job STILL IN PROGRESS when checked).
- NEW raw source 49 PNG + editable .blend: artifact 11679419922, 15525638 bytes, source a38e6cba.
- NEW cropped Unity Resources + 49 manifest: artifact 11679634496, 1221523 bytes, same SHA.
- Verified in local sandbox: exact 49/49 RGBA source files 1672x941 24fps, 16 bones, 8120 vertices, lossless visible-pixel roundtrip of all 49 Unity crops, first and last frame empty, Power alpha contact 30–34 and release at 35, evaluated skin-tip 2D screen error 0px. STRUCTURAL PASS, not physical button 3D collision proof.
- New 6-second normal+slow Blender QA MP4/contact sheet/Power closeups produced from EXACT artifact 11679419922, uploaded to Firestorage: https://firestorage.ai/ja/f/1D20YyTaD0ST (not actual Unity game capture).
- Visual inspection: smaller/less bright nail plates and full approach→press→retract visible; source camera cannot prove actual finger penetration because the laptop is the user-approved 2D POV photograph, not 3D button geometry.
- New art Unity Game View capture job from run 38077862962 IN PROGRESS at checkpoint update; verify NUnit test outcome and PNG capture files before marking PASS.
- Runtime LED fix f3a422e44f9e961be6236a13721463aefdb93705 tested: V3.4 Power session run 38078513722 PASS 1/1 NUnit test, artifact 11679229958; V3.3 seated choice run 38078513725 PASS 3/3 tests, artifact 11679524478. Both use separately pinned/older art resources, not a proof of newest combined 49 frames.
- Local D: project UNMODIFIED. Restricted rollback-safe asset-only installer templates built but NOT Windows runtime-tested and NOT released as final. Full integration in approved room must be verified on local Windows Unity.
- NEXT: inspect final state of run 38077862962 Unity Game View job; if FAIL inspect playmode-results.xml for actual blocker. If PASS review actual RenderTexture stage captures (must show real Power click, not merely static UI), then prepare restricted 49-frame SHA-manifest asset package and dry-run safely before local installation. Update this checkpoint with final CI/run IDs; do not rerender from scratch.

## V8 FINAL CLOUD QA AND HAND-ASSET HANDOFF (2026-10-10)
- Branch prior to this documentation update: 6338f172511798391d9d5951f680326c12452228; existing PR #7 is open / draft=true; not merged.
- Full Blender→Unity run: https://github.com/timurkamurka11/Rokas/actions/runs/38077862962, status=completed, conclusion=success, art source SHA a38e6cba9b1f34fd2976ade27f50cdf3328e9a7f.
- Original raw 49-frame Blender source artifact 11679419922; Unity PNG Resources artifact 11679634496. Both proven source SHA a38e6cba.
- Unity Game View capture artifact 11680315164: exact NUnit case Rokas.Tests.LaptopCinematicCapturePlayModeTests.RenderRightHandContactAndWakeFromLiveUnity PASSED, seven nonempty 1920x1080 RenderTexture PNG frames present (Stage_00 through Stage_06). Backend uses labeled synthetic POV, not user's private approved background.
- Runtime Power LED/session regression source f3a422e44f9e961be6236a13721463aefdb93705: Unity V3.4 run 38078513722 artifact 11679229958 Passed 1/1 relevant NUnit; Unity V3.3 run 38078513725 artifact 11679524478 Passed 3/3 relevant NUnit. Blue LED remains on until contact and goes dark at the contact timeline event.
- Structural and contact screen-plane tests PASS for latest 49 Blender frames: 24 fps, 1672x941 RGBA, 16-bone 8120-vertex hand, exact Unity crop reconstruction, Power alpha on frames 30-34, no contact on 29/35, empty entrance/end frame. This is screen-space proof only; no modeled physical key normal/surface to prove volumetric collision.
- Latest QA video, contact sheet, Power closeups, Unity seven-stage montage and hand-asset installer archive were uploaded to Firestorage at https://firestorage.ai/ja/f/1D20YyTaD0ST (14-day temporary sharing).
- Cloud-verified resource-only package built: ROKAS_LAPTOP_V8_CLOUD_VERIFIED_INSTALLER.zip. 49 cropped hand PNGs + right_hand_manifest.json (50 manifest entries), each SHA-256 checked. Includes 00_ONE_CLICK_INSTALL_AND_OPEN.bat, INSTALL_SAFE.ps1, 01_ROLLBACK_LAST_INSTALL.bat and mandatory target/precondition checks, backup and rollback. No C# runtime source, original room art, SFX or UI assets are overwritten by it.
- CRITICAL: package's Windows PowerShell scripts have not been executed or smoke-tested on Windows. Local Unity D: remains unchanged; original approved-room composition, real user SFX and local UI still require user-local PlayMode acceptance. Do not label local production deployed or final visual acceptance COMPLETE.
- NEXT PRECISE ACTION: On local Windows, first save/merge runtime source f3a422e4 without losing any local modifications. Close Unity. Extract package outside project; inspect RELEASE_MANIFEST.json; execute powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\\INSTALL_SAFE.ps1 -DryRun; if successful run 00_ONE_CLICK_INSTALL_AND_OPEN.bat. In D:\\Rokas\\Rokas-FULL-R11-FINISHED-UI COPY validate actual approved POV, contact, system date/clock, original three UI PNG hover, audio (+5dB/+3dB), E/Esc, one boot per session, YOMI. If regression detected, close Unity and run 01_ROLLBACK_LAST_INSTALL.bat. Save local screenshots/video and final acceptance back to checkpoint. No destructive git operation authorized.


## V9 verified continuation — 2026-10-10
- Verified HEAD before this checkpoint update: 5a9280ab11cd4912d9e49d34f5d1af23bf7f41e1. Branch codex/laptop-pov-cinematic-20261009; PR #7 open DRAFT.
- Latest approved-for-review, NOT approved for release, hand source: a38e6cba9b1f34fd2976ade27f50cdf3328e9a7f.
- Full Blender→Unity workflow: https://github.com/timurkamurka11/Rokas/actions/runs/38077862962 SUCCESS. Blender raw 49 PNG and editable .blend artifact 11679419922; Unity PNG/manifest artifact 11679634496; genuine Unity RenderTexture captures and PlayMode results artifact 11680315164.
- Art structural checks: 49 PNGs 1672x941, 24fps, 16 bones, 8120 vertices; RGBA crop reconstruction exact; first/last frame empty; sampled Power 2D overlap at frames 30-34 and release at 35. Finger/button 3D depth/penetration cannot be proven because Power is from approved 2D photo, not independently modeled as a physical 3D button.
- Unity Game View capture: 7 real RenderTexture PNGs from 38077862962 PASS, including actual power click and transition to YOMI; synthetic TEST-ONLY laptop POV used in CI. Not proof of the real approved photographed room. Earlier independent Unity offscreen run 38077863044 success uses older hand art, not a substitute.
- Latest baseline Cinematic Unity: run 38079134773 PASS on head 6338f172511798391d9d5951f680326c12452228. Generic Core behavior has baseline failures; inspect separately before global release.
- New canonical gated installer source: Tools/LaptopCinematic/release/INSTALL_SAFE.ps1 and isolated Windows test_install_windows.ps1 (NO production art released). Windows workflow .github/workflows/laptop-installer-windows-qa.yml.
- Windows Windows-latest isolated PowerShell suite: https://github.com/timurkamurka11/Rokas/actions/runs/38079916382 PASS at commit 5a9280ab11cd4912d9e49d34f5d1af23bf7f41e1. Log marker ROKAS_WINDOWS_INSTALLER_SAFETY_PASS. Tests: SHA256, dryrun, invalid/missing payload, rejected QA flag, interrupted installation recovery, full mock install, backup copy, hash verification, guarded rollback and tampered backup rejection. All tests used temporary synthetic 1px PNGs in a temp Unity-shaped folder, NOT local D:.
- INSTALLER QA is verified for these tested paths, but NOT approved as a production install package. A review-only artifact ROKAS_V8_a38e_REVIEW_NOT_RELEASE.zip exists in conversation container (not in Git); its manifest explicitly rejects actual installation. Do not bypass release gate.
- Local D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY NOT touched. Original user-approved POV and sounds absent from public CI; runtime combined acceptance requires local Windows Unity inspection.
- Public diagnostic (source a38e6cba, Blender-only video and contact sheet): https://firestorage.ai/ja/f/1D20YyTaD0ST (if link active; not final Unity proof).

### Precise remaining gates
1. Human visual review in approved real POV at normal speed: finger/bone anatomy, apparent button top-down depression, cuff and nails, smooth entering/leaving and frame timing; treat true 3D penetration as unverifiable with photographic 2D button.
2. Verify approved local POV, original UI PNG, sitting/stand-up/user power SFX and their mixed volumes on target Unity; cloud synthetic tests do not verify these private assets.
3. Ensure Windows installer release package includes same approved 49 source frames, sourceSha/artifact identity, batch launcher, safety-script SHA, backup/rollback. Do NOT change validation to VISUAL_AND_UNITY_QA_APPROVED until these user-visible gates pass.
4. Perform local Unity acceptance on D: only when the user elects to install. Never claim a cloud workflow changed local user files.


## V9 final Windows hardening checkpoint — 2026-10-10
- Branch: codex/laptop-pov-cinematic-20261009, PR #7 open DRAFT.
- Code HEAD before this checkpoint: e9bad215f794f37140ebfa547b1643ddf91be72c.
- Canonical installer scripts staged (not approved release): Tools/LaptopCinematic/release/INSTALL_SAFE.ps1, 00_ONE_CLICK_INSTALL_AND_OPEN.bat, 01_ROLLBACK_LAST_INSTALL.bat, test_install_windows.ps1.
- Production assets are NOT in that source folder; do not distribute it as a completed installer. The release manifest must remain gated (VISUAL_AND_UNITY_QA_APPROVED only after complete real-scene acceptance).
- Original Windows safety suite: https://github.com/timurkamurka11/Rokas/actions/runs/38079916382 PASS.
- Expanded Windows PowerShell **5.1** safety suite: https://github.com/timurkamurka11/Rokas/actions/runs/38080156757 PASS, source commit e9bad215f794f37140ebfa547b1643ddf91be72c, marker ROKAS_WINDOWS_INSTALLER_SAFETY_PASS.
- Verified on isolated temporary ROKAS-shaped project: dry run, SHA256/missing/tampered resources, review-only release rejection, interrupted install recovery, backup/rollback, corruption and user-edit protection, wrong Unity root rejection and Windows junction defense. No local D: access or mutations.
- Blender source still a38e6cba9b1f34fd2976ade27f50cdf3328e9a7f; full 49-frame and Unity real RenderTexture art job 38077862962 SUCCESS; artifacts raw 11679419922, cropped 11679634496, Unity game-capture 11680315164. No rerender is required.
- Visual open issue: contact proven in 2D screen pixels, but not true 3D button surface penetration in the original photographed POV. Last CI Game View uses explicit synthetic POV. Do not claim native room/user audio acceptance.
- Local target D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY remains untouched; visual and runtime acceptance in real Unity must precede release flag and installing package.
- NEXT EXACT STEP: Obtain/verify real approved room POV + original UI/audio in target Unity, watch full normal-speed animation and Power contact, record user acceptance; only then populate signed-off SHA256 manifest and make final release archive with 49 new frames and canonical tested installer. Investigate generic Core behavior pre-existing failures separately if global merge is needed.


## V10 persistent checkpoint — 2026-10-10, latest verified results
- Branch codex/laptop-pov-cinematic-20261009, PR #7 open Draft.
- HEAD immediately before this checkpoint commit: 17fcbedc01ea79a8e5615419809c2455577ad409.
- FPS Blender source SHA **a38e6cba9b1f34fd2976ade27f50cdf3328e9a7f**; previous full Blender 49 + Unity RenderTexture run **38077862962** SUCCESS. Native RGBA + .blend artifact **11679419922**, Unity PNG manifest artifact **11679634496**, Unity 7-stage captured pixels artifact **11680315164**.
- Blender data: 49 frames / 24fps / 1672x941, source mesh 8120 vertices, 16 bones. Structural/alpha/sequence/contact pixel (30-34) gates PASS. Actual 3D button collision depth cannot be proven with the approved 2D photograph.
- Windows PowerShell 5.1 baseline hardening run **38080156757** SUCCESS.
- V10 installer path & manifest hardening source commit **17fcbedc01ea79a8e5615419809c2455577ad409**, Windows run https://github.com/timurkamurka11/Rokas/actions/runs/38080447353 **SUCCESS**. Verified explicit traversal, absolute/UNC manifest injection, filename case restrictions, inner right_hand_manifest 49-order validation, idempotent reinstallation, invalid SHA, missing files, interrupted install recovery, user-edit protected rollback, backup integrity, wrong Unity root and junction/out-of-scope write avoidance.
- Installer canonical source: `Tools/LaptopCinematic/release/INSTALL_SAFE.ps1`, `00_ONE_CLICK_INSTALL_AND_OPEN.bat`, `01_ROLLBACK_LAST_INSTALL.bat`, `test_install_windows.ps1`. Windows tests run in isolated temp fixture with synthetic 1x1 PNGs, NEVER user local `D:`.
- **IMPORTANT SECURITY NOTE**: Any older `ROKAS_LAPTOP_V8_CLOUD_VERIFIED_INSTALLER.zip` claiming `VISUAL_AND_UNITY_QA_APPROVED` should NOT be treated as a finalized deployment. That flag is broader than evidence: approved real POV and local audio/game acceptance remain missing. Do not distribute that prior installer as production.
- V10 review-only evidence/archive: `ROKAS_LAPTOP_V10_QA_REVIEW_ONLY.zip`, Firestorage https://firestorage.ai/ja/f/gN5uEZiOfWu6 (expires 2026-10-24). Built directly from pinned Unity Resources artifact 11679634496 and source .blend from 11679419922; 49 PNG + JSON manifest, Blender MP4/contact sheet, Power zoom, Unity montage, SHA256SUMS and QA report. Strict flag `REVIEW_ONLY_NOT_INSTALLABLE` and no executable installer scripts. SHA256 archive: **c8fdd6d6879ab60624ccf4d49ad7deca174a8021fd7a82bbe5e36aa509b61a18**.
- Repo `Core behavior` run **38080450206** FAILED at `Validate artwork, audio, metas and scene bindings`: HD image sizing on multiple unrelated UI/scene assets and stereo PCM sources (EnterGame.wav, LaptopMouseClick.wav, UIWood.wav, Keiko attacks). Do NOT mark full repository CI all green. Not caused by V10 PowerShell installer changes.
- **LOCAL TARGET UNMODIFIED**: `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` inaccessible to cloud. Approved original laptop POV, UI hover PNGs, real SFX/volumes, YOMI clocks and normal-speed screenshot recording still require local Unity acceptance.
- **FINAL RELEASE GATE: BLOCKED/PENDING**, by real-room user-facing visual/audio/local Unity validation and the global Core CI failures. No installable release manifest or final installer package issued in V10.

### Next exact action
1. In real local Unity, verify that the approved POV and user UI/audio are present and runtime source matches the branch; check no local uncommitted changes are overwritten. Capture normal-speed entry→Power→release→YOMI and repeated-entry video, and verify system clock/audio and original PNG hover.
2. If real-room visual and functional acceptance passes, authorize the matching 49 cropped PNGs from Blender SHA a38e6cba; only then construct final Windows installation ZIP with the canonical V10 tested scripts, release manifest SHA-256 and locked source SHA. Do NOT flip release flag without this evidence.
3. Address or explicitly disposition `Core behavior` HD/WAV failures before declaring the entire repo release ready; do not modify unrelated game systems under FPS-hand-only scope.
4. Keep checkpoint and PR updated after actual verified steps, do not duplicate Blender render run 38077862962.


## V11 Continuum — cloud implementation + verified QA (2026-10-10)

- Existing PR: [#7](https://github.com/timurkamurka11/Rokas/pull/7) **OPEN/DRAFT**; branch `codex/laptop-pov-cinematic-20261009`.
- **Verified Unity-tested code HEAD**: `f7fc0779bbe16c1b2dd4ffc25dc4ddc2d69d74dc`. Most recent doc-only pre-checkpoint HEAD: `9bffec8a49af214e1f1573f744072f9c8b3dc681`; later docs updates do not imply another Unity-tested runtime.
- Full QA report: [docs/laptop-cinematic/V11_QA_REPORT.md](./V11_QA_REPORT.md). This is the evidence and before/after + release-gate source of truth.
- V11 source: only five Presentation .cs files modified (LaptopView, LaptopCinematicSequence, LaptopPhysicalDesktop, RokasView, RokasAudio), focused Unity tests and one existing workflow. NO changes to Blender source, 49 pinned hand frames, Combat, VN Composer, or unrelated scene code.
- Live Home 2.5D focal transform retains CustomGlow, hotspots and WorldEffects until original POV is visible; original off-state POV source/calibration untouched; 2D→POV perspective quality remains a **visual acceptance gate**, not proven just by code.
- Original PNGs retained: idle pulse, hover and press anim; YOMI-on physical LCD is live opaque 640x360 app-catalog/unread/system-clock presentation with original calibrated screen corners. Clicking opens the same full YOMI. Not pixel-identical fullscreen YOMI.
- Power persists until game restart; no second boot; hand/Power timeline preserved. YOMI desktop prewarmed under boot video (0.5s artificial black hold removed), YOMI now overlays retained approved POV until ready; YOMI→POV prewarm prevents intermediary Hub exposure. Both Sit and Stand use existing Stand Up AudioClip at .77 scale respecting master/mute.
- Focused Unity 6000.3.19f1 EditMode + fallback + **5/5 V11 PlayMode tests** all PASS: https://github.com/timurkamurka11/Rokas/actions/runs/38085553868, code `f7fc077`. Pixel alpha bug exposed by initial failed run `38085187111` fixed with proper compositing and verified on `38085553868`.
- Art-integrated 49-frame right-hand powered laptop Unity PlayMode PASS at same verified code: https://github.com/timurkamurka11/Rokas/actions/runs/38085553865.
- Art-integrated seat/close/reenter/stand Unity PlayMode PASS: https://github.com/timurkamurka11/Rokas/actions/runs/38085184270 (code `f494e9c`, preceding only the live-LCD alpha compositor change and docs).
- Existing V10 Windows PS5.1 installer safety run `38081352711` PASS. Do not mislabel it as a V11 local patch installer: V10 only ships 49 hand PNGs + JSON manifest.
- Generic `Core behavior` at tested HEAD run `38085557541` FAIL (known unrelated project-wide HD image and audio/WAV asset validators). Do not mark whole-project CI PASS.
- **Native user-approved room/color/perspective, original local PNGs, full normal-speed video, real Stand Up WAV mix/clipping, actual local Unity use NOT verified.** Cloud current runs use synthetic POV and lack user SFX. Local `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` NOT remotely modified.
- **FINAL RELEASE remains BLOCKED** by required real-room video/audio/native Unity acceptance and global Core release validation. No `VISUAL_AND_UNITY_QA_APPROVED` flag, no final V11 installer/ZIP, no deceptive PASS claim.
- **NEXT EXACT ACTION**: Safely merge only the five V11 C# changes into local main Unity copy after verifying/backup of local edited files, ensure hand PNG + approved POV + user SFX are present, run exact end-to-end flow (Hub → POV → contact → Boot → YOMI → POV → Stand → Hub → repeat powered). Obtain per-frame recording and audio peak/listening QA. Then authorize scoped V11 installer, release only after user-visible signoff.


## V11 code-only Windows staging bundle — verified 2026-10-10

- Latest **code-only integration tool** Git HEAD: `9acd1ca4978f275106f3e8bc922928273c63a877`. Earlier tested 5-C# Unity runtime remains pinned at `f7fc0779bbe16c1b2dd4ffc25dc4ddc2d69d74dc`; the new integration tool does not change runtime or 49 hand frames.
- New project directory: `Tools/LaptopCinematic/v11_code_review/` with `V11_CODE_SYNC.ps1`, `test_v11_code_sync_windows.ps1`, `README.md`. GitHub workflow: `.github/workflows/laptop-v11-code-review-windows-qa.yml`.
- **Real Windows PowerShell 5.1 QA PASS:** https://github.com/timurkamurka11/Rokas/actions/runs/38086344713, marker `ROKAS_V11_CODE_SYNC_WINDOWS_PASS`. Tested read-only default audit, explicit apply/confirm, exactly 5 allowlisted source files, pinned baseline/new SHA256, refusal of user-diverged code and tampered payload/manifest, refusal of fake review gate as final release, interrupted copy restore, backup, idempotent repeat install, rollback protection after user edit, wrong Unity root, junction/symlink refusal, and no out-of-scope sentinel mutation. **Only Windows temporary Unity-shaped fixture used; local D: never touched.**
- **Code-only review ZIP** `ROKAS_LAPTOP_V11_CODE_REVIEW_ONLY.zip` is GitHub Actions artifact **11682232992** in run 38086344713; exact inner ZIP SHA256 `d258596bfd0d9fa4bfb88888f09f1a4443eb6a3e7f9080827e22afe1b3724fb9`, byte size 36207. The archive has exactly 5 C# payload files, `V11_CODE_MANIFEST.json` with `V11_CODE_REVIEW_ONLY_NOT_FINAL_RELEASE`, one PowerShell script and README. All five manifest afterSha entries checked against ZIP contents. Bundle does NOT include hand PNGs, original POV, UI images, audio or game executable; DOES NOT authorize a final release.
- Default command `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\V11_CODE_SYNC.ps1 -Target 'D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY'` **only audits**; applies ZERO changes. To stage the 5 files for local real-room QA, close Unity and use `-Mode Apply -ConfirmCodeSync` **ONLY** if all 5 files report BASELINE. Script explicitly fails on local divergent edits or mixed version; no overwrite override. `-Mode Rollback -ConfirmCodeSync` preflights all 5 installed/backup hashes and blocks user-modified sources.
- Existing V10 installer source, Blender scripts and all Tools were verified preserved after follow-up Git commit; prior V10 Windows installer safety still **PASS** on HEAD: https://github.com/timurkamurka11/Rokas/actions/runs/38086379520.
- **Still pending** native local Unity end-to-end normal-speed recorded full scene, pixel-level real POV/CustomGlow transition, existing user-approved 3 PNGs and sound presence/volume/clipping, global Core validator (continues FAIL). No final QA release or `VISUAL_AND_UNITY_QA_APPROVED` flag. User-controlled code-only QA staging is distinct from forbidden final installer. Do not assume `D:` synchronization has already happened.
- **Next exact step**: user performs non-writing audit in their Unity project using this verified ZIP. If local hashes are BASELINE and all prerequisites present, the user may elect to stage code-only patch for native video/audio QA; if hashes diverge, compare and merge safely (never force replacement). Then capture whole Hub→laptop→Power→Boot→YOMI→seated POV→stand→Hub→reentry with audio, review user original artwork, and only afterward consider release.
