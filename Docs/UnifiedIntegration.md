# ROKAS unified integration — verification record

The verified Messages/Home and Combat 2.0 histories now coexist in the permanent staging branch, integration/rokas-unified. The normal user project is D:/Rokas/Rokas, Unity 6000.3.19f1, scene Assets/Rokas/Scenes/Rokas.unity. The permanent milestone rule is in [AGENTS.md](../AGENTS.md).

## START

Recovered the existing D:/Rokas/unified-integration checkout on integration/rokas-unified at 8a3becc243c6d51a59725ae15770cb9f3471c449, tree d30b51a4b506391f3872ca7d818c4fa7ea1c897b. Pending work consisted of the integrated runtime test, workflow/plan documents, and Unity-generated local settings/lockfile. The remote integration branch did not yet exist. No additional worktree was created during continuation.

## SOURCE CHECKPOINTS

- Home/Messages: c68763fb482665ea53cb42c48b2638cc5314c3c0; tree f6318724b3dff93857011803ff09679e9c8d69b5.
- Combat: fa4c44d620debae7bf12984a5db9195794c1aa1a; tree a09aa1cee13271efe4147c4f949734da22a68ac6.
- Real merge: 8a3becc243c6d51a59725ae15770cb9f3471c449. Its two parents are exactly those checkpoints, in that order. Both histories remain ancestors of the final checkpoint.

## CONTINUATION PROOF

The existing merge and semantic review were reused. The already-corrected Yumiko fixture remains intact. No Combat, Home, Messages, economy or progression feature was added. No Weather 3 work or separate Sol work was included.

## PACKAGE / UNITY IMPORT

The original first import completed normally. Existing Yarn Spinner v3.1.2 resolved to e1ba6f471d923270d2a0ba7b4f8ae81dc3f76adb. Packages/manifest.json was unchanged; no removal, reinstall, upgrade or repeated editor restart was needed. Unity 6000.3.19f1 compiled the unified project and completed both full test platforms with exit 0. Local runner used .NET SDK 8.0.424 for Core. No GameCI change was needed.

## SEMANTIC INTEGRATION

There were zero textual conflicts. Six shared files merged automatically and were reviewed semantically: GameSession.cs, RokasBootstrap.cs, RokasView.cs, GameSessionMessageEventTests.cs, Tests/Core/Program.cs and Tests/Core/Rokas.Core.Tests.csproj. Messages initialization, run identity, LiveMessages ticking, reaction/audio subscriptions and disposal coexist with manual Combat entry points, input cancellation, hit-stop and HUD wiring.

The one compatibility failure was a Yumiko test fixture expecting Tick(.2f) to kill a one-health enemy automatically. Only the successful-run setup changed to an asserted ClickAttack(false). Reaction, idempotence, payment, save and subsequent death assertions were retained. Automatic combat damage was not restored. The original RED and corrected GREEN logs remain in the external evidence directory.

Runtime proof additions are committed as 48b2d30a3bf80eff0bb01eaffac99eb01afd0c9c. They assert the integrated Home/Messages/Combat shell and retired text, and optionally capture actual runtime canvases. Two 0.3-second waits allow the existing laptop opening fades to complete before Messages/payment screenshots. No production behavior changed for this proof.

## CORE

Final unified-final-core-2 run: exit 0, all existing Core behavior suites PASS, all 23 named Combat scenarios PASS and four retirement checks PASS. The console runner does not print a reliable grand total for all existing assertions. See [Core output](Verification/UnifiedIntegration/core.log).

## EDITMODE

33/33 passed; failed 0, skipped 0, inconclusive 0. Final run on 2026-09-09 at 13:43:26 UTC, Unity exit 0. Includes 23 Combat scenarios, first-loop guards, Yarn import/dialogue validation and Unity save/backup tests. See [XML](Verification/UnifiedIntegration/editmode.xml).

## PLAYMODE

54/54 passed; failed 0, skipped 0, inconclusive 0. Final run on 2026-09-09 at 13:44:03–13:44:23 UTC, Unity exit 0, graphics enabled. Includes Combat (16), Home atmosphere (5), first loop/food (2), and all current Messages, Live Messenger, reactions/audio, attachments, resources, font and lifecycle suites. See [XML](Verification/UnifiedIntegration/playmode.xml) and the individual cases in [results.json](Verification/UnifiedIntegration/results.json).

## MESSAGES

The real RokasBootstrap first-loop test opens Home, enters LaptopMessages, verifies the contact/search/header/conversation shell, and then proceeds through the existing contract flow into Combat in the same session. The full suite also covers typing, reaction audio, unread state, conversation resume, notifications and duplicate-event guards.

## COMBAT 2.0

Actual integrated captures show the new manual Combat HUD, player HP, enemy HP/Seal, charge/Perfect Cut ring, combo and Resonance. The real EventSystem flow covers pointer tap/release and held ritual tracing. Specialized tests exercise dodge, deflect, Seal Break, ordered 1 → 2 → 3 held ritual, Resonance, interruption, focus and pause handling. Charge/ritual captures show rendered ring geometry.

All active Unity Text and TMP_Text are checked for absence of the retired autoattack instructions (АВТОАТАКА АКТИВНА, Удар клинком / +клик, Нажимайте на ёкая), normalizing whitespace and case. Idle ticks cause zero automatic enemy damage.

## PAYMENT

The integrated first loop wins through manual attacks/ritual, reaches Sealed, returns Home, shows payment, claims once, and reloads reward/progression. The same-session payment capture shows the completed contract and 1800 yen / 3 ash / 10 reputation reward before claim. Existing domain and runtime duplicate-claim assertions pass.

## SAVE / LIFECYCLE

Save reload, accepted contract/pending payment, backup protection, run identity, duplicate reactions/messages, combat cancellation, pause and focus tests pass. Existing profile behavior was preserved.

## HOME 2.5D

WorldEffects.cs, HomeAtmosphereProfile.cs, its serialized asset and HomeAtmospherePlayModeTests.cs retain the exact verified Home checkpoint blobs, recorded in results.json. All five Home tests pass, including layers, parallax bounds, profile/thunder routing and reuse. The unified first loop also asserts the existing window/depth/rain/mist/haze/wet-glass objects. No Weather 3 changes were added.

## VALIDATOR

Validator exits 1 for the same 13 historical findings: four unresolved TMP GUIDs, seven below-HD images and two stereo PCM sources. Final comparison: 0 added, 0 removed. The integration introduced no validator findings. Exact baseline/final lists are preserved beside the test results.

## LOG SCAN

Final EditMode and PlayMode logs contain 0 relevant compile/runtime errors. No NullReference, missing-component/reference, render texture, particle, audio, input, save or Yarn runtime failures were found. Licensing token diagnostics subsequently resolve and both runs execute successfully. The Missing_Node warning is the expected negative EditMode test; D3D12 info-queue and Mono/debugger shutdown messages are recorded separately in results.json. Raw Unity logs remain external.

## DIFF

Literal git diff --check and staged diff checks returned exit 0 with empty output. Source scope consists of the real integration, Yumiko setup compatibility and integrated runtime proof; the remaining additions establish policy and preserve verification evidence. Generated Unity settings and package lockfiles were excluded from the commits and preserved locally.

## CODE REVIEW

Independent semantic and final post-GREEN reviews: APPROVE, no important actionable findings. The final review inspected the final test delta and 33/33 + 54/54 results. See [review](Verification/UnifiedIntegration/code-review.md). Publication and safe canonical switching are verified separately by the executing agent.

## VISUAL RUNTIME PROOF

Actual 1920×1080 Unity runtime captures from the final unified PlayMode run are stored outside Git at D:/Rokas/unified-integration-evidence/visuals:

- integration-home.png and integration-messages.png: Home and the real Messages shell.
- integration-combat.png: new Combat HUD from that same first-loop session.
- integration-payment.png: victorious return/payment in that same session.
- combat-charge.png, combat-hud.png, combat-ritual.png: specialized runtime proof from the same complete suite.

These are actual rendered test sessions, visually inspected by the agent; user manual gameplay/feel inspection remains the next step. SHA-256 and byte lengths are in results.json. No screenshot from the separate historical Combat worktree is used as unified proof.

The tested Assets tree is 51cf7a2dd516e4bd0bd4bc5ea699bde33c0da3e7; Tests tree is 50b2070e12933422898c80aaf3fd0fac83c0e654; package manifest blob is b0bb4553d2a88a2154d105dd95460fc79990bb0b. Only documentation changes follow these final tests. Final checkpoint publication must confirm these exact fingerprints.

## CHECKPOINT / REMOTE

The publication marker is VERIFIED ROKAS UNIFIED INTEGRATION CHECKPOINT, created after this evidence commit with the identical tree. Exact checkpoint SHA, tree, direct parent, merge parents and remote equality are reported in the final handoff and D:/Rokas/unified-integration-evidence/final-handoff.json. This document records verification before publication and does not pre-claim that later operation.

Only integration/rokas-unified may be pushed. Both approved source branches remain untouched. PR #4 remains outside this task; it must not be merged or closed.

## DEVELOPMENT

Protected remote development baseline: 5e494f188b90bdb6916e8e78e35ac38509bd0bce. Existing local development: a53cac5e242b97ea75b7ec1c4ab3eeaaaace16f9. Neither is reset, merged into or pushed. Existing local feature/messages-system remains 05e35c488e74d23c708dd8962ddb149966764b8c; verified remote source remains c68763fb482665ea53cb42c48b2638cc5314c3c0.

## CANONICAL LOCAL PROJECT

After remote checkpoint equality is confirmed, safely switch D:/Rokas/Rokas to integration/rokas-unified at that exact SHA using Git. User confirmed the editor is closed. Preserve the backed-up 28 untracked files and the generated ProjectVersion revision line; inspect for new changes and case-insensitive target collisions immediately before switching. Compare SHA-256 for all 29 preserved files afterward. Keep all existing worktrees.

The normal folder may retain that documented local dirty state while its tracked runtime/source matches the checkpoint. Do not claim all local files are clean. Never copy project folders, force checkout, reset, clean or overwrite user assets. AGENTS.md records the permanent canonical workflow for later milestones.

## NEXT

USER MANUAL ROKAS UNIFIED INTEGRATION INSPECTION in D:/Rokas/Rokas, Unity 6000.3.19f1, Assets/Rokas/Scenes/Rokas.unity. Stop after remote verification and the safe canonical switch; wait for user QA.
