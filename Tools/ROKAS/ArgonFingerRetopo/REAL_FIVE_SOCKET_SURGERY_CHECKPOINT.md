# ROKAS — authentic five-finger palm surface reconstruction checkpoint

**Scope:** QA-only branch `codex/bedsleep-argon-import-candidate`, draft PR #13. The game Bed Sleep V10, original Argon FBX, 25-bone armature, OLED watch and the accepted left-sleeve-to-right-fingertip bed POV stay UNCHANGED. Do not merge until true Unity visual approval.

## What REAL Blender proved on source geometry

From Blender's genuine FBX seam atlas [#39047780276](https://github.com/timurkamurka11/Rokas/actions/runs/39047780276), each of the five source fingers has one 148-vertex long open border. It comprises 44 exact duplicate-paired longitudinal seam vertices, 31 coincident tip-pole vertices, and 29 remaining root vertices. Palm has 288 original boundary edges and 83 coincident source lip pairs.

The **non-destructive candidate only** exact same-island lip weld [#39063873012](https://github.com/timurkamurka11/Rokas/actions/runs/39063873012) PASSED: 12,240→11,937 actual vertices, 1,028→436 boundary edges, original surface area change 0, 0 newly nonmanifold edges, still six separate authentic 25-bone mesh islands. This fixes **592 genuine boundary edges** (57.6%) without invented cylinders or texture replacements. The long source tip-pole collapse (5×31 source vertices) is excluded until independent topological safety.

Next source-only experiment with **all coordinate-identical tips additionally welded** confirmed 11,786 vertices and 274 border edges, with palm contour sizes **64 (cuff), 30, 30** and each of five fingers **30** root edges. However the existing two 30-vertex palm openings are NOT where a believable finger root connection is needed.

## Correct positions and missing topology

All coordinates are reported as authentic FBX world coordinates. One source unit is treated as a model unit; do **not** claim centimetres unless validated in Unity.

| Geometry | Ring centre x | Ring centre y | Ring centre z |
|---|---:|---:|---:|
| Existing palm ring A (30) | 0.0766 | −0.0232 | ~0 |
| Existing palm ring B (30) | 0.0698 | +0.0221 | ~0 |
| Thumb actual root | 0.0115 | −0.0228 | −0.0067 |
| Index actual root | 0.0516 | −0.0263 | −0.0005 |
| Middle actual root | 0.0528 | −0.0087 | +0.0013 |
| Ring actual root | 0.0505 | +0.0093 | +0.0012 |
| Little actual root | 0.0466 | +0.0259 | −0.0008 |

Measured shortest full-ring correspondence RMS to palm A: index 0.02561, middle 0.02901; to palm B: ring 0.02487, little 0.02418, all with max gap over the hard limit `handspan×0.05 = 0.02056`. The original remaining two "socket" borders do not map to a plausible 30-vertex finger insertion directly; DO NOT relax the max gap by brute force.

**Consequences:** A `bmesh.ops.bridge_loops` across arbitrary rims, partial `-7..+7` surface quads, or positional welding fingers to the wrong source cuff may give one connected component but visibly destroys the anatomy. The attempted full-ring existing-socket program [#39064761330](https://github.com/timurkamurka11/Rokas/actions/runs/39064761330) therefore FAILED SAFE, appropriately. Do NOT call it a tested success. The original protected production V10 is unchanged.

## Next real development — not thousands of check reruns

1. **Surgically rebuild only the distal metacarpal region of the copied palm** around five authentic finger-root centres. Preserve proximal wrist/forearm, original skin/source PBR references, rig bind poses, sleeve and watch silhouette.
2. Construct **five anatomically placed palm socket rim contours**, one per finger. Use original finger-root ring geometry and local source palm interpolation, not five generated cylinder replacements. Root contours are typically 30 vertices each after source lip repair.
3. Replace the locally cut distal-palm faces with a **shared manifold web surface containing five holes**, so ring, middle and little fingers do not penetrate an intact palm shell. Use a non-crossing constrained triangulation/quadded polygon-with-holes method; enforce no high-degree/nonmanifold edges, no self-intersections, and near-source surface/bounds.
4. Bridge all five source root rings to the newly constructed actual socket rims with 2–3 smoothly transitioning rows. Interpolate original 2048² skin UVs per corner and corrected **independent** per-finger original rig weights; keep creases at anatomical knuckles without rigid collars.
5. Repair the original 31-vertex collapsed fingertip pole regions without wiping distal phalanges or painting flat placeholder materials. Check manifold and zero-area faces **before** enabling any candidate in Unity.
6. Compare 3 real cloud Unity camera-matched 1280×720 views and 72-frame 24fps actual thumb/index/middle/ring/little deformation to the last proven 25-bone video [#39058944007](https://github.com/timurkamurka11/Rokas/actions/runs/39058944007). Candidate source-faithful skin **AO12** already isolated and passed [#39057616459](https://github.com/timurkamurka11/Rokas/actions/runs/39057616459); keep all original albedo/normal/roughness maps.
7. Only after anatomically acceptable hand visuals, restore sleeve-left / right-fingers hero pose, watch straps/OLED readability and cinematic lift into actual game integration. Commit/push to existing GitHub PR; do not silently change production V10.

### Last clean assets and experiments

- Original 25-bone FBX stays under `Assets/Rokas/BedSleep/ArgonSource/Models/ROKAS_Keiko_FPS_Arm_Rigged.fbx`.
- Working source lip experiment: `Tools/ROKAS/ArgonFingerRetopo/source_surface_seam_weld.py`.
- Safe existing source-to-socket-fit diagnostic: `Tools/ROKAS/ArgonFingerRetopo/authentic_palm_socket_match.py`.
- Original pixel-level reference / actual Unity 24fps A/B archive: [run #39058944007](https://github.com/timurkamurka11/Rokas/actions/runs/39058944007).
- No full cosmetic replacement nor premature merge; only source-constrained anatomical surgery can resolve the remaining palm socket mismatch.
