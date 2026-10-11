# Combat Polish4 — composition checkpoint (2026-10-11)

Starting committed base: bc15123a586f358b9b7a236dcae4532a5b29857a, branch codex/fidelity3-resume-5779190. Previous Combat checkpoint81908699 is an ancestor. Later Laptop V11 work stays intact.

Actual authored Rokas.unity runtime with isolated save:
- BEFORE skin height Keiko0.26993H, Yokai0.18624/0.16053/0.18659H.
- AFTER first compact proposal Keiko0.38913H, Yokai0.32238/0.28800/0.32078H.
- Front root distance2.72854→2.14385world units (-21.43%). Adjacent source rank step0.9 retained.
- Tactical local camera(0,.822227,-16.17633)→(1.75406,.822227,-11.32343). Perspective/source fieldFOV38 preserved; existing906/1080 projection gate still widens effective optics. This is an own larger composition adaptation, not a recovered DD2 camera constant.
- Remove old .83/.77 rank shrink for one-to-three enemies; depth retains perspective.
- Attack target offsets, current contact endpoints, approach/return ownership and source action shot curves unchanged.

Existing actual DD2 screenshot comparison: SourceTacticalComposition.md/json in research folder. Front human≈.324H/feet≈.656H, small enemy .374/.336H, broad giant .427H. Our Keiko is deliberately larger (≈20%) than measured human; own one-versus-three meshes and silhouettes differ. No exact1:1 DD2 match claimed.

Acceptance red/green:
- BeforeTactical-1791695669460 failed expected Keiko height .26993 below .36.
- AfterTactical-1791695805803 passed1/1 actual Normal+return43.66s.
- HpOverlapBefore-1791696046902 failed expected card crossing E2.
- HpLayoutAfter-1791696138547 passed1/1 transforms43.59s, but actual PNG still exposed stale per-refresh card reset. This was insufficient pixel evidence, fixed subsequently by removing the reset rather than relying only on post-render transforms.
- HpRenderedAfter: PNG correctly moved HP cards below feet, but frame-gap pause hid target buttons before the test inspected them.
- TacticalRenderedRearm-1791696983314: 1/1PASS44.31s after waiting for actual Core/button rearm following screenshot readback; current actual screenshot below feet and full Normal/return verified.
- FirstLiveThrowPreviewReusesInactiveProjectileAndNativeCommandBank: 1/1PASS; 12 stable production copies, one inactive flying dagger, zero duplicate creation across Throw/cancel/Heavy/cancel/Normal.
- Bank prewarm EditMode 11/11PASS. Actual first-Throw frame-time improvement remains to be measured from genuine new gameplay.

Preservation baseline outside project: D:/DD2-Research/Reports/CombatPolish4-2026-10-11/ProtectedWorkingTreeBefore.json,684untracked+47later committed paths (731hashes). Current validator baseline273 existingFAIL rows, not historical135: later local files/assets changed baseline before this task. No broad validator repair.

Stage A is a local feature checkpoint only; canonical integrated suites/push/manual handoff not yet verified. Heavy finger penetration remains a separately confirmed defect; no animation/weapon physical PASS implied by tactical acceptance.
