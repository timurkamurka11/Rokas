# Portal final visual convergence — verification

Scope: rendering only in the authoritative R11 working tree. Baseline `4bba762727fdc6a56f2152c9c5c21e70350a4cf2`, branch `r11-finished-ui`.

## Ribbons

Five primary bands, four secondary bands, two micro filaments replace seven evenly spaced strands. Unequal attachment angles, lengths and curls; width follows the curve tangent, with taper, traveling bulges and unsynchronized breathing. Role widths 1 / .48 / .13, coverage 1 / .72 / .45, different shader flow speeds/frequencies. Bands occupy front/side/back depth and some render behind the aperture. Dark cores, violet/magenta body, cyan accents, traveling white-hot segments.

## Lightning

Four major arcs, six connected branches, two fast micro arcs use the existing twelve lines. Biased sectors 2.08 / 1.30 / 3.58 / 5.82 radians; no evenly spaced radial origins. Each child begins at a parent's actual vertex. Unequal curved and angular segments, local width bulges, quantized geometry changes, independently seeded shader flow. Bursts, pauses and double strikes share the paused presentation clock.

## Vortex

Three view-dependent slices: dark slow back flow, medium violet flow, faster fragmented front detail. Speeds .10 / .245 / .39 rad/s, displaced drifting centers, different radial scales, nonlinear twists and asymmetric turbulence. Softer broad emission under localized cores; nine unequal depth veins. Local internal brightness responds to the same angular discharge sectors/pulses. No new portal system, render pipeline or VFX Graph dependency.

## Locked contracts

Arena, emergence class/shader, boundary include, actor models/animations, combat/input/UI/audio code and all Ember assets remain unchanged. `AngularBoundary`, `SetProgress`, `Tick` are byte-equivalent after line-ending normalization to the source baseline (hashes in VerifiedRenderSources.json). Root position/rotation/radii, mask plane and entrance timings are preserved. No monster pose/rig/animation edits.

Timings retained: seed/ribbons ~.70s, open2.10s, reveal2.766667s, exit4.866667s, collapse5.266667–5.866667s, residual gone6.066667s.

## Visual evidence

Reference: `D:/3DMODELS/Example of monsterr apper and animations of portal.MP4`, reviewed again at1x and selected native frames36/53/70/86/120/136/151. No repeat full238-frame timing audit. Seven REF/ROKAS comparisons and three sequential portal sheets are in Visual. Full motion is from the real encounter PlayMode capture, preserving capture realtime durations; held frames are duplicated during30fps encoding, not interpolated animations.

Iterations: fresh baseline → ribbon body/width/depth → clustered lightning → layered vortex → reduced continuous filament density → softer/higher interior volume emission → final visual review. Reference textures/meshes were not copied into runtime assets.

The three broad visual signatures are substantially closer. This is not a pixel-identical reconstruction: ribbon curves remain somewhat sharper/mesh-like, discharge paths and cadence are original, the interior remains more graphic and less volumetric than the reference. The very early .70s comparison also retains the previously locked faint onset. Monster differences were excluded from assessment.

## Tests

Focused portal lifecycle/pixel/emergence/cleanup PlayMode: 14/14 PASS, zero failures/skips. Full relevant Combat PlayMode: 80/80 PASS, zero failures/skips. Actual result XMLs are retained alongside TestResults.json. Core log contains49 named PASS plus the aggregate PASS.

Long-run executes20 planned Normal/Heavy/Throw commands plus filler attacks, defense and multiple waves/two encounters. It verifies exact home/facing/camera restoration, inactive reusable carriers, cleared projectile/trail/contact effects and owned cleanup. The final relevant XML records its exact result/duration.

Three sequential spawns: 3; max particles 69; max materials 20; one active portal and one masked actor maximum; final portal and temporary emergence materials gone; AP unchanged.

## Performance observations

Ribbon geometry672→1408 vertices,658→1386 triangles; owned meshes13→17, materials16→20; renderers39→43. Arrays/MPBs/curves are created once and reused; no explicit new managed allocation path in Tick/UpdateAppearance. Sparks capped at96; atlas payload remains approximately4MiB BC7 (four1024² textures,64frames each, no mipmaps/readable copies). No re-export or change to the EmberGen pipeline.

This is a static/resource/lifecycle check, not a GPU/GC profiler measurement. Shader work and mesh upload costs increased; captured frame intervals include ReadPixels/PNG overhead and are not gameplay FPS. No FPS or overdraw improvement is claimed.

## Validator and scope

Validator baseline136 → final136: 0 new findings. Raw validator exit remains1 due to the recorded historical findings; comparison is green. A transient Unity Test Framework scene appeared during an active run; the final validator was evaluated with the runner closed. Validator source was unchanged.

Final Unity logs reviewed: no shader compile error or runtime exception from the portal changes. Both runs contain the existing licensing access-token refresh error; it did not prevent import, graphics execution or valid XML completion. Test-runner method names containing RecordExceptions and shutdown MemoryLeaks telemetry are not treated as evidence of a portal exception or a measured portal leak.

All84 original user files preserved by SHA256. Unity importer-only trailing whitespace is normalized only after exact semantic comparison against4bba762; no metadata or rig setting changes are included in the commit. Only four portal render sources plus this verification evidence are committed. The source R11 checkpoint is the requested deliverable; this pass does not establish a new unified integration/manual-QA checkpoint.
