# Fresh Native FK owns the current frame

During a Native entry/handoff, the previous two-hand IK correction remained marked as applied while the motion player sampled fresh FK. UpdateEquipmentPose then restored that older arm/wrist sample, overwriting current FK. Remove the previous IK correction before LicensedCombatMotionPlayer.Tick and Sample, matching the established Legacy ordering. No curves, grip angles, sockets, mesh, scale, input gates or Combat Core timing change.

The regression distinguishes actual one-hand to two-hand entry from settled grip. Entry closes monotonically over the existing 0.08 s blend; established two-hand transfers stay on the actual 55 mm hilt segment. Primary palm attachment and settled support retain the 1 mm threshold. Persistent Idle follows ConfirmedLicensedProfile, not the previously played Heavy profile; playing Heavy alone is not a stance commit.

Before the ordering fix, Heavy handoff tick 1 left the hilt line by 2.352762 mm. Afterward, Heavy and ReturnHome samples remain on the segment within 1 mm. HeavyPreparation support gap closes 113.2058 -> 61.1992 -> 21.0192 mm before settled contact. NativeFkSupportAndActualIdle-1791558579736 passed 1/1, 2.0338 s. CombatStanceAndBackSocketRegressionTests passed 4/4 in NativeFkOwnershipAndConfirmedIdle-1791558489111: explicit stance, preview cancellation, Guard/Dodge and spine-owned Throw restoration.

Physical mesh penetration remains a separate unresolved defect; these transform/stance tests do not certify physical Heavy grip. Actual serialized-scene UI capture is the next acceptance step. Research evidence is retained outside Git under D:/DD2-Research/Reports/CombatFidelity3-2026-10-08/Validation.
