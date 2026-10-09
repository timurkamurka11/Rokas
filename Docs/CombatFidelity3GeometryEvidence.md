# Heavy physical geometry evidence

BakeMesh(true) followed by renderer.TransformPoint is independently validated against linear blend skinning at the actual renderer hierarchy scale. Original actual max residual is1.104micrometres; the earlier52cm discrepancy came from mixing scale conventions, not corrected physical grip.

The72-pose production FK measurement covers Heavy,Normal,Throw,Guard,and both confirmed stance Idles. Heavy actual worldspace penetration remains25.638mm; geometry FAIL. A bounded3-pass candidate was fully revalidated across all72poses and rejected:129flipped triangles,edge stretch10.675, and no acceptable Heavy improvement. No candidate mesh,retargetclip,boneweight or swordscale change was bound into production. Throw40openboundaryedges remain a measurement limitation.

The Editor-only actor entry selects Heavy using production CommitCombatStance+PlayIdle; it cannot run during an active licensed action. Closed/open topology tests passed and actual original geometry audit completed. FullCOPYEditMode observed974PASS5unrelatedHub/VNFAIL1SKIP; do not label physical Heavygrip or integratedtreePASS.

Evidence: D:/DD2-Research/Reports/CombatFidelity3-2026-10-08/HeavySkinning/HeavyOriginalGeometry.json and HeavySkinningCandidateEvidence.json. Existing private licensed source clips unchanged.
