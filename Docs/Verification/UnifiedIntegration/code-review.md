# Unified integration semantic review

Verdict: APPROVE the reviewed code/spec integration. No actionable important code findings identified. This is not a declaration that the final integration gate, publication, or canonical checkout switch has completed.

## Scope and evidence

Read integration-review-brief.md, integration-review.diff, request.txt, the permanent AGENTS.md rule included in the review package, runtime-proof-report.md, current GameSession.cs, MissionView.cs, RokasView.cs, FirstLoopTests.cs, and Core/focused runtime logs. Reviewed merge 8a3becc243c6d51a59725ae15770cb9f3471c449 against the explicitly supplied Messages/Home and Combat parents. Source-feature retention review already performed by root was treated as established scope rather than unnecessarily repeated. No repository edits, Git changes, test executions, or Unity launches were performed in this review.

## Spec compliance

- The merge package preserves the Messages/Home session initialization, run identity normalization, food and return reactions, and LiveMessages ticking alongside Combat encounter reset and all manual action entry points. Payment remains guarded by EconomyService before delivery of the deterministic completion message; the review shows no additional reward path.
- Bootstrap retains focus/save blocking and combat cancellation, Escape handling before combat input, modal pause and hit-stop gating, view/effects ticking, and sound ticking/disposal. MissionView checks pause and combat phase before dodge, deflect and Resonance dispatch.
- RokasView retains current WorldEffects construction with audio/owner context, message routing and reaction audio subscriptions, while also subscribing Combat ActionResolved to the actual MissionView. Matching disposal is present for both responsibility sets. Panel opening cancels held combat input.
- FirstLoop proof uses a real RokasBootstrap and real laptop navigation, asserts Home depth/weather/profile objects, opens the Messages conversation shell, enters Combat through the portal, verifies the manual input component and new HUD controls/seal/charge/ritual geometry, completes the fight through pointer events, returns home, claims payment, and reloads the saved profile to verify reward/progression persistence. Its idle tick explicitly requires zero automatic enemy damage. Existing payment disappearance and reload assertions remain intact.
- The permanent workflow rule captures the requested integration branch, canonical folder, exact checkpoint selection, complete integrated gates, protected development/source history, safe switch, and no cleanup before user inspection.

## Fixture correction and code quality

The Yumiko successful-run fixture previously expected Tick(.2f) to kill a one-health enemy. The failing merged Core log demonstrates that assumption no longer holds after Combat 2.0 removes automatic damage. Replacing only that tick with an asserted ClickAttack(false) is the appropriate semantic fixture correction; it exercises the real manual action and retains the Sealed transition, food-dependent reaction, run identity, idempotence, payment and persistence checks. No production workaround was introduced.

The Core runner/project include the Combat scenarios and the newer Yumiko/Live Messenger suites together. unified-core-fixture-green.log contains 23 PASS Combat2 scenario entries, the four additional Combat red-test success messages, and PASS: all Rokas.Core behavior tests. This evidence supersedes the documented stale-fixture failure without hiding it.

The capture helper is test-only, opt-in and disabled for a null graphics device. It captures the actual instantiated canvas using a temporary render target/camera, restores render mode, camera, stage scale and active target, and releases/destroys allocated resources in finally. Its existence/object assertions supplement rather than replace the retained specialized behavior tests and required visual inspection.

## Remaining release evidence

Root must finish and record the full integrated EditMode and PlayMode results, inspect the produced Home/Combat images, compare validator findings with the recorded baseline, scan final Unity logs, and obtain literal git diff --check exit 0 with empty output. At review time the available focused-runtime log ends during initial asset import and does not establish a runtime test pass or screenshot result. The implementation of the proof is approved; its executed outcome is not asserted here.

Only after those gates are green should root create/push the final verified checkpoint, verify exact remote equality, and safely switch the canonical folder according to the independent safety audit. No further code change is requested by this review.

## Final independent review after unified GREEN

Final verdict: APPROVE. No important actionable findings in the final scoped change. The previous semantic integration approval remains applicable; no production logic changed after that review.

Read after-green-review-brief.md, after-green-review.diff, final-test-results.json, and unified-final-core-2.log. The final Core log passes all 23 Combat scenarios, four retirement checks, and all Core behavior suites. The supplied results enumerate 33/33 passing EditMode tests and 54/54 passing PlayMode tests, with zero failures, skipped, or inconclusive tests. Coverage includes the complete integrated first loop and reward reload, Combat input/charge/ritual/pause/focus, Home depth/parallax/weather/thunder, Messages lifecycle, typing, reactions, audio, and unread behavior.

The final test delta adds remaining Home layer assertions and scans active Unity UI and TMP text for prohibited legacy combat instructions with whitespace/case normalization. It adds Messages and payment captures while preserving the real UI flow and reward assertions. The 0.3-second waits allow the existing laptop fade to finish before capture; they do not bypass a guard, alter production behavior, or replace an assertion. No automatic attack logic is restored. No test weakening or source-history mutation is present in the reviewed delta.

The root's final brief records inspection of the same-tree Home/Combat/Messages/payment captures after panel fade, unchanged 13-finding validator baseline with zero additions, and clean literal/staged diff checks. Those are root-owned evidence; this review independently inspected the supplied test results and code delta and did not rerun tests or claim a separate visual inspection. Both explicitly verified source parents remain the basis of the previously reviewed merge.

The earlier pending test-evidence qualification is now satisfied by the supplied GREEN results. Proceed with the authorized verified integration checkpoint/publication and safe canonical checkout preparation. Remote equality, unchanged protected refs, and safe local-switch completion must still be verified by root before final handoff; this review does not claim those future operations have occurred.
