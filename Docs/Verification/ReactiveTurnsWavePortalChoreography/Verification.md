# Wave Portal Choreography — verified feature evidence

## Checkpoint and scope

- Recovery HEAD: `5b0c8d8ad364771e8435075043273735c568a1aa`; recovery TREE: `c5ab009eb08e9e482e15d3d4ea91a1de25dfa29d`.
- Branch: `r11-finished-ui`. Project: `D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY`.
- Final HEAD/TREE are recorded after the focused commit in `D:/Rokas/reactiveturns-b2-staging/wave-portal-choreography/FinalCheckpoint.json`.
- This is the requested R11 feature checkpoint. No unified integration/manual-QA handoff or remote push is claimed by this pass.

## Lifecycle, queue and counters

1. Previously `OpenNextPortal` created a portal for one queued actor and reopened after that actor's close.
2. Arena now owns one wave portal: Forming → Open/EmittingEnemies → Residual → Collapsing → Closed.
3. Existing `entranceQueue` is reused. `BeginWavePortal` creates once; `PrepareNextYokai` configures only the real actor's temporary mask.
4. Per successful wave: formation=1, open=1, close=1; one persistent real portal root. Three live actors crossed the same root.
5. Each previous body must clear the fixed plane before its mask is released and the next hint starts. Deterministic gaps: .45/.65/.50 seconds.
6. Final slots, scale, facing, monster rig/pose/clips and locomotion implementation are preserved. Shared portal placement guarantees all supported final slots are in front of its plane.
7. Last actor detection uses the actual remaining valid queue, not an index. Late valid entries during residual continue under the same aperture.
8. Residual=.55 seconds. Accepted inward collapse=.60 seconds, ghost=.20 seconds, then cleanup and presentation settle=.16 seconds.
9. Player controls remain gated until all entrance/masking/collapse/settle work finishes. Combat Core, AP, damage, attacks, input/UI/audio/camera code is unchanged.

## White opening diagnosis and fix

10. Baseline persistent-step ablation measured 67 early orbiting sprites at progress .4216867, clock 1.166666s. Disabling only ParticleSystemRenderer removed the white flower while the other layers remained identical.
11. Source: `ReactiveCombatPortalEffect.EmitMote`, `PortalOrbitingSparks`, shared `Rokas/ReactiveCombat/QuietFx` particle material. Tiny aperture radii concentrated HDR-ish white sprites from progress>.02. Bloom/EmberGen/ribbon meshes were not the ablated cause.
12. Mote emission now stays zero until aperture tearing (.55 normalized progress) and ramps through .78. Closing emission stops before the aperture becomes tiny. Motes are smaller, restrained cyan/violet rather than overbright white.
13. Initial distortion → dim uneven cracks (reuse three existing line renderers) → accepted ribbons → aperture tear. The old 1.166s cluster is gone in the fresh matching capture; no replacement cyan/white blob is visible.

## Continuous idle, crossing, hint and floor

14. One continuous presentation clock drives vortex, ribbons, lightning noise and all four existing EmberGen layers. No per-actor time/material/particle reset or second portal system.
15. Each root-plane crossing resolves once and triggers a .22s localized contact field from the actor's world torso point.
16. Existing edge is stretched locally; two existing major lightning channels respond locally, nearby ribbon energy lifts, vortex pulls locally and six restrained particles disperse from contact. No global white/fullscreen flash.
17. Next-Yokai hint uses the same masked actor behind the aperture; `_PortalSilhouette` mutes albedo/emission behind the fixed plane. No duplicate model/render pass. Hint transitions into the existing movement/reveal.
18. One existing local light responds briefly (+.28 intensity); the existing floor shader receives a restrained .09 contact patch. No new large decal or lighting system.
19. Mask architecture remains world-space clipping; each actor receives owned temporary surface materials and restores its durable original materials after clearance.
20. Invalid/missing/dead queued actors release Entrance gates. Disabled/destroyed entering actors release masks and let remaining actors continue. Respawn updates the actual motion actor reference and preserves Entrance against Refresh snapping.
21. Retire restores emergence materials before Ash captures originals, avoiding an owned-material death cleanup race. Dispose releases wave portal, masks, meshes/materials, light and target texture.

## Verification and visual evidence

22. Focused PlayMode: 17/17 PASS, including 1/2/3/5/8 actors, a fresh next wave, remove/disable/destroy, refresh replacement, early mote state and resource cleanup.
23. Relevant Combat PlayMode: 95/95 PASS, valid completed XML. Three live waves to victory: PASS.
24. Core: 49/49 PASS (Unity C# compiler plus domain runner).
25. Live long-run: PASS, 332.403813 seconds. Exact test output/counts: `TestResults.json` and `RelevantCombat-results.xml`.
26. Validator comparison: 136 → 136; 0 NEW findings. Historical findings remain recorded, not treated as a clean validator.
27. Live capture: one root, three entrants, three physical plane crossings; max particles=73 (cap96), max portal materials=20, one light, four native EmberGen layers. Resource count does not grow per actor; temporary resources disappear after close.
28. Fresh full PlayMode video, 1× browser review plus phase captures: `Visual/WavePortalThreeYokai.mp4`. Same portal stays alive between three sequential bodies; one formation and one final collapse; no re-seed or visible quad/black square.
29. Thirteen fresh phases: pre-open dark, cracks, mid formation, open, crossing1, idle1, next hint, crossing2, idle before last, last crossing, residual, inward collapse, gone. `Visual/WaveSequenceSheet.jpg` and `Visual/CaptureManifest.json`.
30. Source gameplay reviewed: `D:/Advanced SystemCare/bandicam 2026-10-03 22-10-52-740.mp4`, full 1× and key frames, source hash recorded. No repeated old reference audit.
31. Literal git diff checks and post-commit status are recorded in `FinalCheckpoint.json`; 84 pre-existing files and 627 locked files are preserved by SHA checks. Only wave portal source/tests/evidence are staged.

## Explicit confirmations and limits

MONSTER POSE / RIG / ANIMATION NOT CHANGED.
PORTAL ART DESIGN NOT REDESIGNED.
COMBAT RULES NOT CHANGED.

No EmberGen re-author/export pipeline change; existing four native atlases reused continuously. No new audio architecture/cues, camera, UI, attacks or unrelated systems.
The hint and contact response are deliberately subtle at the normal combat camera. Existing monster pose differences remain outside scope. Visual video is reconstructed from real timestamped PlayMode captures (147 samples, encoded30fps); it is not a native 60fps recorder stream. No GPU frame-time benchmark is claimed; resource bounds, sustained lifecycle and cleanup were verified.
The working tree intentionally retains the 84 prior user files; only this pass is committed. No BAT, review clone, new branch, main/development/integration merge or push.
