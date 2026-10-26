# ROKAS — Authoritative user GLB hand source switch (WIP)

**Continuation of existing draft PR #13; do not merge or activate in production.**

## New authoritative visual source

- User attachment: `hand with logo sleeve 3d(1).glb`
- SHA-256: `0e64e84423cbaca378e5bfdbb9f142ee775dce476eec4808910a43577e476a27`
- Original source: **115,317 vertices; 215,292 triangles; 147 disconnected mesh islands**, one combined textured GLB mesh, one embedded image, no original skeleton or clips.
- The pictured bone chart is **placement guidance only**, not a usable rig and not a replacement skin mesh.
- Stop reusing rebuilt V17–V22 procedural **hand geometry** as the candidate. Keep previously approved original ROKAS bed scene, camera, **existing high-detail watch + Russian OLED**, controls, trigger, and V16 fallback.

## Actual local continuation in ChatGPT

The user GLB has been parsed directly and its original POSITION, indices, UVs, vertex normals, material and image are preserved in an experimental rigged GLB. A 24-joint hand skeleton with five separate digit chains and six WIP animation tracks was created. Actual source 3D digit heights rather than 2D-only Y coordinates were used to place the fingers, and disconnected distal finger islands were forbidden from borrowing weights from adjacent finger chains. The working candidate is named `ROKAS_UserGLB_Rigged_WatchAnchor_WIP.glb`; generated files were supplied in-chat, **not yet uploaded to GitHub**.

- Original GLB's texture and mesh unchanged: exact data checks via `ROKAS_NewGLB_RealQA.py`.
- Rest pose position deviation essentially zero (under 1e-7 model units).
- 24 deform joints and 6 animation tracks, one explicitly named `PartialGrip_WIP_NotFullFist`: **do not call it a production-quality full fist**.
- 330,462 unique mesh edges examined. Initial crude rig had visible tears; calibrated mild curl has **zero edges meeting simultaneous stretch >3x and posed length >8 mm**, at test frames in all six clips. This is a **limited analytic safety gate**, NOT full anatomy approval.
- Forearm/sleeve x< -0.12 is assigned zero finger-joint skin influence.
- An **empty transform only**, `ROKAS_WatchMount_Candidate`, was parented to the candidate wrist bone, with preliminary relative translation (0.015, 0, 0.080) **requiring fit calibration** against the existing ROKAS smartwatch FBX and strap. The watch mesh itself has **NOT** been added or visually fitted.
- No Blender executable available in the current local environment: cannot truthfully claim `.blend`, FBX export, Blender roundtrip, or real Unity Game View PASS.

## Why hand completion is still blocked

The **actual source model itself contains 147 disconnected surface islands**, including finger-adjacent regions. A generic weight diffusion cannot turn these into a continuous deformation-ready palm when requesting a deep fist: raising flexion amplitude produced new tearing, so full fist, root/webbing retopology and physical watch/strap fit remain **FAIL / unverified**. Do not conceal this with old runtime triangle trims or caps.

## Next actionable step

1. Bring **this exact SHA-256 GLB** into the same PR using an explicit binary-asset transfer approved by the user; keep it as an immutable source.
2. Run Blender retopology/welding in palm/MCP/webbing on the true source, preserving source shape and projected UV texture and original cuff; bind weights to the 24-joint rig.
3. Rebuild natural deep fist, index/other finger isolated curls, thumb pinch and bed entrance/exit **against the new source**, with real close-up Blender renders.
4. Export `ROKAS_Hand_Rigged.blend/.glb/.fbx`; independently reimport GLB/FBX in Blender and Unity Generic rig.
5. Fit existing `Assets/Rokas/BedSleep/ArgonSource/Models/ROKAS_Smartwatch_HighDetail.fbx` and the approved Russian OLED to the correct wrist mount; verify physical strap attachment rather than creating a fake watch.
6. Activate a reversible preview of the new rig **only** in the original Hub → Bed sequence; keep V16 as fallback; record true Unity Game View, upper/lower cuff, underside, thumb/index/little root and sleep-timer footage. Merge only after user visual approval.

**No production change or PR merge is authorized by this WIP checkpoint.**
