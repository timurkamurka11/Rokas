# Combat Fidelity 3 — spatial and stance checkpoint

Base: 22380f2d8e1f8647c2da0d5d976ea12d092e2168, preserving the later Hallway merge and the verified 3a6bb2c Native/camera checkpoint. Working project: D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY, Unity 6000.3.19f1. This is a feature checkpoint, not a verified unified manual-QA handoff.

Source spatial adaptation uses size-one front anchors (-.7,+.7), .9 tactical rank spacing, mirrored team X offsets with raw Y mapped to depth, and one conversion from the source ZoomIn floor Z=-7. Raw source camera curves remain unchanged. Corpses retain their slot through the active shot; living slots compact only after cleanup and settled ownership. The exact DD2 corpse-disable timing and actor tween duration/easing remain unverified; existing approach/return timing is preserved.

Core Basic/heavy commits own the saved sword idle. Preview/cancellation/Throw/Guard/Dodge retain the last confirmed sword stance. Back carry uses one spine-relative bind-frame socket instead of a per-frame world-down rotation writer. Physical Heavy finger penetration and back-blade clearance are separate pending visual/geometry checks.

Actual Unity results: SourceSpatialRefreshRetry-1791496363838 passed 1/1 through Bootstrap/UI/Core, including contact framing, real UI refresh preserving target offset until its profile cue, and tactical restoration. SourceSpatialStanceGreen-1791496226999 passed all four stance/back-socket tests; its spatial probe was corrected to run without capture readback and then rerun. The interrupted refresh attempt hit a Test Runner domain-reload error during new diagnostic import and is not an acceptance result.

Evidence: D:/DD2-Research/Reports/CombatFidelity3-2026-10-08/Validation. Protected baseline: 75/75 untracked files (including the original 70 user files) match original SHA-256. Native clips/model source files are unchanged. Pending: measured Block CPU diagnosis, Heavy mesh repair, explicit new Foley UI wiring, full repeated flow and continuous GameView recording, complete regressions/validator, and safe unified integration.
