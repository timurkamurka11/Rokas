# Full actor render gate — local Combat checkpoint

The actual UI capture exposed a render-target boundary at logical y=100: Throw clipped Keiko’s head, and Normal clipped the top of the weapon trail. The old actor image/RenderTexture used 1920×906.

ReactiveCombatArena now owns one 1920×1080 actor output gate. Its pre-cull projection embeds the former 906-pixel view at the same physical pixel coordinates, with 100 pixels of upper and 74 pixels of lower overscan. Existing actor scale, source camera position/rotation/FOV fields, Native clocks, stage offsets and animation ownership are preserved. ReactiveMissionView derives feedback bounds from this same image rectangle. No second camera or actor-transform writer is introduced.

This is an explicit ROKAS framing adaptation. The effective optical vertical FOV is wider (source field 18° → effective 21.3835°; 38° → 44.6321°); it must not be reported as an exact DD2 lens. No additional zoom or source timing acceleration is used.

Verification: ViewportAndLegacyNativeAcceptance-1791539961397, six rendered-feedback tests passed, including an independent old-gate camera projection comparison and a formerly out-of-gate point becoming visible without moving existing pixels. Actual Bootstrap/Core/UI screenshot flow and source-offset/live-refresh test passed. The separate continuous real GameView flow passed 1/1 and produced a 209.533s 1770×996 60Hz H.264/AAC recording. Full MP4 decode exit 0, empty error log.

Current real capture: D:/DD2-Research/AnimationStudy/CombatFidelity2026-10-08/LiveCapture-dd5b092b59804466b761877b5be7ff4c. Normal and Throw images confirm the former inner boundary is gone. The physical Heavy finger/mesh defect remains a separate confirmed failure.
