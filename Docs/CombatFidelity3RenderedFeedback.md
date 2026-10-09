# Rendered combat feedback checkpoint

Real D3D11 Unity 6000.3.19f1 focused runtime: 10/10 PASS, 651.90 s, RenderedDamageFix-2026-10-09/Tests.xml. This checkpoint follows 5e14d774.

Intro HUD hiding left ReactiveHitFeedback inactive. Accepted real contact/defense feedback now activates it once; arena-owned presentation time controls expiry and deactivation. Real actor-camera post-render updates its anchor from freshly rendered skinned body bounds without advancing any clocks or writing actor/camera transforms. Duplicate contacts do not restart feedback.

Cinematic veil uses decoded BasicDamage hold/fade timing and an explicitly own intensity adaptation (.24); it is excluded for defense, Heavy AOE, death and cancellation. Native pose/recovery and source-camera ownership remain intact.

Actual Bootstrap/Core/UI captures: D:/DD2-Research/AnimationStudy/CombatFidelity2026-10-08/LiveCapture-8dfb141d436444289e3c8fc5dabc4e12. Normal/Heavy/Throw damage is visibly rendered; live refresh preserves target offset until the current profile cue; corpse is retained through the real death path. Capture readback costs are not frame-time evidence.

Full suite status is not green: EditMode 974 PASS / 5 FAIL / 1 SKIP; PlayMode 209 PASS / 84 FAIL / 1 SKIP before this focused correction. Remaining failures include stale combat expectations and confirmed Heavy support-grip defects, alongside protected Home/VN regressions. Full pass or canonical integration is not claimed.

New visible limitation: actor RenderTexture begins at screen y=100, clipping Throw head and Normal trail at that boundary. This is being investigated separately without adding camera zoom/FOV changes. Heavy finger mesh candidate was rejected and is not bound.

Native prewarm and registration evidence remain unchanged; separate CPU V3 after-prewarm run: 8 genuine Block contacts, complete CPU windows, no contact AddClip, 0 clean frames >33.33 ms, max22.0889 ms. Full cold launch and other profile switches are not covered.
