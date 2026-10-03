# Portal reference — native PTS timing breakdown

Source: `D:\3DMODELS\Example of monsterr apper and animations of portal.MP4`

SHA256: `93f1254f391c4d055a2793a2c4900cee3858ee27b24712ac1a4c1d182fba190f`

Reviewed all 10 native sheets (238 actual decoded frames), plus the individual boundary/capture frames listed in JSON. Source duration 9.900 s; last actual PTS 9.866667 s. Local time zero is first confirmed seed at 0.800 s.

**Use native PTS.** Preliminary 297-frame CFR sheets duplicate frames and their index/fps labels are wrong. Transitions marked representative are gradual; exact physical crossing cannot be inferred from this 2D clip.

| Marker | Native frame | Absolute s | Local s | Observation |
|---|---:|---:|---:|---|
| floor_seed | 19 | 0.800000 | 0.000000 | First confirmed pink/lavender thin floor ellipse; preceding frame 18 is clear. (confirmed visual marker) |
| black_core | 22 | 0.900000 | 0.100000 | First confirmed black kernel with violet edge above the floor ellipse. (confirmed visual marker) |
| ribbon_unfurl | 36 | 1.500000 | 0.700000 | First broad magenta/cyan tapered ribbons unfurl around the growing dark vortex. (confirmed visual marker) |
| formed_open | 70 | 2.900000 | 2.100000 | Clean large vertical aperture with white/lavender irregular electric edge; formative broad ribbons have mostly receded. (phase representative, gradual formation) |
| first_creature_reveal | 86 | 3.566667 | 2.766667 | First green head sliver in the lower inner aperture; frame 85 remains empty. (confirmed visual marker) |
| partial_emergence | 87 | 3.633333 | 2.833333 | Head, shoulder and one forearm visible while most body is hidden. Useful half-emergence capture; not a measurable 50% 3D crossing. (phase representative) |
| full_body_inside | 96 | 4.000000 | 3.200000 | Full small body is readable inside the aperture, before outward movement is complete. (phase representative) |
| exit_overlap | 120 | 5.000000 | 4.200000 | Creature overlaps the front/left edge, HP label is visible, movement toward slot continues. (phase representative) |
| clearly_exited | 136 | 5.666667 | 4.866667 | Creature clearly stands forward-left of portal and has left its center; still in locomotion pose. (conservative fully-exited representative; exact plane crossing not observable from 2D) |
| settled_residual | 144 | 6.000000 | 5.200000 | Crouched slot pose; portal remains fully open behind the creature. (phase representative, settled by this time) |
| collapse_onset | 146 | 6.066667 | 5.266667 | First confirmed inward shrink after full-size frame 145; outer electric ghost outline remains. (confirmed visual marker) |
| core_gone | 160 | 6.666667 | 5.866667 | Opaque black kernel is gone; residual electric ghost wisps remain. (phase representative) |
| residual_gone | 165 | 6.866667 | 6.066667 | No portal kernel, ring, or surrounding electric trace is visibly remaining. (conservative clean frame; very faint tail before this time) |
| final_lineup | 237 | 9.866667 | 9.066667 | Three enemy lineup at the end; later enemies appeared without a fresh full portal formation. (end frame) |

## Timing intervals

| Phase | Absolute s | Local s | Duration s |
|---|---|---|---:|
| seed_growth | 0.800000–1.500000 | 0.000000–0.700000 | 0.700000 |
| ribbon_formation | 1.500000–2.900000 | 0.700000–2.100000 | 1.400000 |
| open_before_reveal | 2.900000–3.566667 | 2.100000–2.766667 | 0.666667 |
| creature_reveal_and_exit | 3.566667–5.666667 | 2.766667–4.866667 | 2.100000 |
| exit_to_settled | 5.666667–6.000000 | 4.866667–5.200000 | 0.333333 |
| residual_hold_after_settled | 6.000000–6.066667 | 5.200000–5.266667 | 0.066667 |
| core_collapse | 6.066667–6.666667 | 5.266667–5.866667 | 0.600000 |
| electric_tail | 6.666667–6.866667 | 5.866667–6.066667 | 0.200000 |

## Shape and palette

- **seed:** Thin horizontal pink/lavender ellipse on the floor before the vertical kernel.
- **formation:** Growing black/purple kernel; broad tapered curved ribbons in cyan/teal, magenta, violet unfurl around it like a pinwheel.
- **open:** Large vertical oval, irregular lively white/lavender electric edge; near-black/navy depth and inward-curving violet/cyan fine filaments. Sparse golden external sparks.
- **size:** Approximately 2 to 2.4 crouched-monster heights. Open silhouette approx x640..1140 y90..540 in a 1280x720 image, visual estimate only.
- **emergence:** Small head first, then shoulder/arm/body, then forward-left locomotion. The creature remains readable against the dark inner depth.
- **collapse:** Black center and rim contract inward; previous-size ghost electric outline survives briefly, then fades. Not a single uniform alpha fade.
- **smoke:** Restrained supporting wisps; broad opaque grey smoke must not replace the dark vortex and electric edge.

## Limits

- These are visual reference annotations, not exact world-space portal plane intersections. Some transitions are continuous; phase representatives are labelled explicitly.
- Second and third monsters fade/appear after the main portal closes, without an observable fresh formation. Use first-monster emergence as design target; do not copy that shortcut.
- The review establishes source presentation direction, not Unity result quality or automated-test proof.

No Unity, Git, runtime or asset changes were performed for this review.
