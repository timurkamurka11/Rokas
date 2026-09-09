# ROKAS Combat 2.0 — verified foundation

## Start and recovered state

Worktree: D:/Rokas/combat2-foundation. Branch: codex/combat2-foundation. Starting commit: 2237ea3ed350f6b17fa08d20f66bc6f176d2e193. No remote combat branch existed at recovery. Existing uncommitted domain, presentation, and tests were preserved and completed; no restart, reset, rebase, merge, or replacement worktree occurred.

The recovered Core foundation already passed 22 scenarios. Continued verification found two real lifecycle defects (pointer exit retained a charge; interruption retained the combo) and one real rendering defect (custom rings lacked CanvasRenderer). Each was reproduced before its narrow fix. There are now 23 shared combat scenarios. No completed combat system was redesigned.

## Combat, input, and HUD

One existing Faceless Commuter encounter remains in the existing scene and contract architecture. CombatService owns the rules; GameSession forwards actions; MissionView/CombatHud update persistent UI objects; CombatInputSurface owns the pointer gesture. CombatTuning centralizes foundation timings.

- LMB tap attacks once on release. Automatic damage and the timed weak-point bonus are retired. Button click/submit cannot duplicate the held release.
- The three-hit combo has .28 s recovery, .32–.85 s continuation, and a perfect beat at .48 ± .08 s. The third strike has 2.4x base damage; a perfect beat applies 1.35x. Spam cannot bypass recovery; timeout or interruption resets the chain.
- Hold at least .22 s for a cut. Releases at .65–1.2 s are charged (1.8x); .90–1.04 s is Perfect Cut (3x). Early/overheld cuts are weak (.65x). The charge circle shows real progress and the best sector.
- RMB Dodge uses the final .42 s before an attack. Space Deflect uses .14 s and deals 30 Seal damage. Both share .65 s defense recovery. The enemy countdown and telegraph read the same timer that resolves attacks; pause/focus do not advance enemy time.
- Enemy HP and Seal, player HP, and Resonance display actual domain values. Seal starts at 100. Strong attacks and Deflect break it; reaching zero starts one .35 s pressure pause and then one ritual.
- Hold LMB on ritual point 1 and drag through 2 then 3 within 3.2 s. Success resolves once, deals 40% maximum enemy HP, and gains 30 Resonance. Wrong order, release, or timeout fails deterministically, restores 50 Seal, and schedules .75 s counter pressure. A final stale pointer-up cannot attack through the transition.
- Resonance caps at 100. R activates once for 5.5 s, with .22 s attack recovery, 1.5x Seal damage, and .035 s wider perfect windows. Perfect combo/cut, Dodge, Deflect, and ritual build the gauge; mistake/damage behavior is covered by the domain tests.
- Enemy phases change at two-thirds and one-third HP: readable opening, alternating quick/delayed middle pressure, then faster/heavier late pressure. No new enemy or boss framework was added.
- Held input cancels on pointer exit, pause, focus loss, UI disable/teardown, death, and Seal Break. Resume cannot fire an old release or preserve an interrupted perfect combo. Ritual cancellation penalizes once.

## Feedback and visual fix

Existing hit sounds, enemy recoil/flash, slash, damage-number pool, and WorldEffects impact are reused. Eight lightweight spark images, an impact flash, .06 s strong-action hit-stop (.12 s ritual payoff), Seal Break/Deflect emphasis, and Resonance gauge/slash feedback complete the slice. No new asset, package, license, or VFX framework was introduced.

CombatTimingRing now explicitly requires CanvasRenderer. The null renderer reproduced the invisible charge circle and ritual rings. The new mesh test asserts a renderer, nonempty submitted geometry, and changed vertex colors while held. The four targeted rendering/input checks passed in combat2-ring-render-green, and all are included in the full final run.

Actual Unity UI camera renders at 1920x1080 from combat2-final-playmode were inspected: the charge circle, bright best sector, and all three ritual rings are visible above the trace panel. The real GraphicRaycaster also reaches the input surface at all three point centers. Images are retained outside Git at D:/Rokas/combat2-evidence/visuals/combat-charge.png and combat-ritual.png; hashes are in results.json. This is runtime visual evidence plus automated real EventSystem gestures, not a claim of human manual play or approval of final balance/feel.

## Fight to payment and existing regressions

IllustratedHomeCanAcceptFightReturnAndClaimExactlyOnce passes through contract acceptance, portal, manual pointer attacks/ritual, victory, ReturnHome, Payment, and ClaimPayment. The lethal held-release regression independently confirms cleared transient HUD/input and a second claim returning false without another reward. Existing Hunter Guild completion/history guards and Messages/save/load/UI regressions pass.

The sole change to GameSessionMessageEventTests is the combat fixture's manual one-HP kill replacing its old automatic tick. Its Messages/payment assertions remain unchanged. Messages production code, Home/2.5D, food, assets, scene files, packages, and project configuration are excluded from the combat commits.

## Fresh final verification

| Gate / run ID | Result |
| --- | --- |
| combat2-final-core | Exit 0; all 23 named combat scenarios, four retirement checks, and all existing Core suites pass |
| combat2-final-editmode | 33/33 passed; failed 0, skipped 0, inconclusive 0; exit 0 |
| combat2-final-playmode | 36/36 passed (16 combat + 20 existing); failed 0, skipped 0, inconclusive 0; exit 0 |
| combat2-final-validator | Exit 1; exact same 13 baseline findings; 0 added, 0 removed |
| git diff --check | Exit 0, empty stdout and stderr |
| git diff --cached --check | Exit 0, empty stdout and stderr |

EditMode ran 2026-09-08 22:55:25 UTC. PlayMode ran 22:55:58–22:56:12 UTC (local date September 9). Full suites used the exact final production/test files, with no later source/test changes. Unity 6000.3.19f1 (7689f4515d75); Core portable official .NET SDK 8.0.424. EditMode used -nographics; full PlayMode used graphics so the mesh/capture path was exercised. The Core console runner prints named scenarios and an aggregate existing-suite result; it does not expose a reliable combined test total.

Portable evidence is under Docs/Verification/Combat2Foundation: core.log, editmode.xml, playmode.xml, and results.json (all case results, baseline failure set, source blob IDs, source commit IDs, raw evidence hashes). Full Unity logs and RED history remain at D:/Rokas/combat2-evidence.

RED-to-GREEN trail: core-red-dotnet8 (21 intended new failures); combat2-editmode-red (21 intended failures); combat2-playmode-red (missing HUD/release path); core-interruption-red (CancelledCombo); combat2-playmode-lifecycle-red (pointer exit); combat2-ring-render-red-2 (null renderer). Corresponding green runs and final full runs establish each correction. An initial mesh-test scaffold used the wrong Unity GetMesh overload; it was corrected to this installed API before the null-renderer reproduction.

### Validator and console findings

The 13 existing validator failures are four unresolved TMP GUIDs, seven below-HD existing images, and two existing stereo PCM findings. Exact paths are preserved in results.json; the validator was not weakened or bypassed.

Final logs contain no new combat NullReference/MissingReference/MissingComponent, mesh/render-order, material/shader/sprite, input, audio, TMP glyph, or save/serialization failures. The Unity licensing token/client diagnostics recur from baseline and do not prevent successful exits. The expected missing Yarn node warning belongs to a negative EditMode test. D3D12 info-queue and Mono/debugger shutdown diagnostics also recur in earlier graphics runs. Existing first-import TMP API/nullable compiler and FSBTool warnings were recorded; they are not combat changes.

## Persistence and branch isolation

Production commits:

- 060c29987c41908e43e5dc0c757631cf003290da — manual domain rules and regressions.
- 85306500760493f7668a24f29ddaa7e7b575ab28 — HUD, gestures, lifecycle/rendering fixes, feedback, and PlayMode coverage.

The verification report is committed separately. An empty VERIFIED COMBAT 2.0 FOUNDATION CHECKPOINT follows it, retaining the identical Git tree. Exact final checkpoint/tree and matched remote IDs are provided in the handoff. Only refs/heads/codex/combat2-foundation is authorized for push; no merge or other branch push is part of this task.

At final recovery, local development was a53cac5e242b97ea75b7ec1c4ab3eeaaaace16f9 and local feature/messages-system was 05e35c488e74d23c708dd8962ddb149966764b8c. They were not modified by this task. Concurrent executions independently advanced development locally and Messages remotely during the interrupted work. Before push, remote development remained 5e494f188b90bdb6916e8e78e35ac38509bd0bce; remote Messages was 8d2ce62c253a37b776a33cb8428c3d8a3f1d9533. No concurrent work was imported into Combat.

The worktree is preserved. Its generated Unity Library/cache state, package lock, ProjectSettings files, and ProjectVersion revision remain locally for inspection. These generated files are not part of the feature commits; therefore overall git status is intentionally dirty for generated state only. All combat source/test changes are committed. No unrelated task files were cleaned or overwritten.

## Supported save lifecycle and limits

The existing save schema is unchanged. Saved HP and encounter timers resume; held gestures, combo, Seal/ritual progress, and Resonance are encounter-only state and reset on reload/re-entry. No stale ritual or held gesture replays after reload. This slice does not persist mid-ritual or Resonance progress. Human timing, final balance, and subjective feedback quality remain the manual inspection task.

## Manual inspection

Open D:/Rokas/combat2-foundation with Unity 6000.3.19f1, then Assets/Rokas/Scenes/Rokas.unity and press Play. Use the existing contract/laptop flow, leave Home, and enter the portal.

1. Tap LMB on the enemy; time the next two releases to the combo beat.
2. Hold LMB and release in the light charge sector; confirm Perfect Cut.
3. Observe the countdown; use RMB near impact, then Space closer to impact on a later attack.
4. Deplete Seal. Hold on point 1 and drag through 2 then 3; inspect success and try a released/incorrect trace on another attempt.
5. Build Resonance to 100 and press R once. Observe expiry and feedback.
6. Pause or lose focus while holding; resume and release. Confirm no old attack fires.
7. Finish manually, return, claim payment once, and confirm it cannot be repeated.

To repeat the full local gates from a normal PowerShell environment, close only this worktree's Unity editor first. Use the portable dotnet.exe to run Tests/Core/Rokas.Core.Tests.csproj in Release with -p:UseSharedCompilation=false -m:1. Run Unity.exe -batchmode -projectPath D:/Rokas/combat2-foundation -runTests -testPlatform EditMode (then PlayMode), with separate -testResults and -logFile paths. Keep graphics enabled for PlayMode. Set ROKAS_COMBAT_CAPTURE_DIR to the external visuals directory only when captures are wanted. The ready local runner/environment notes are in D:/Rokas/combat2-tools/run-tests.mjs and D:/Rokas/combat2-evidence/runner-report.md; its default PlayMode command uses -nographics, so use the graphics-enabled command for the visual gate.

NEXT: USER MANUAL COMBAT 2.0 INSPECTION. Stop after the verified feature push. No merge, additional system, or Combat 2.1 work.
