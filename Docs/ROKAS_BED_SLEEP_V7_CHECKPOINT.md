# ROKAS Bed Sleep V7 — asset engineering checkpoint (NOT merged)

This branch is an isolated review/staging branch. It does not replace `RokasBedSleepCinematic`, Laptop V11, GameSession, or any runtime assets on the working branch.

## V6 asset findings and actual V7 candidate changes

- Corrected skinned arm **GLB binary**, preserving 25-joint skin, weights, UV, and 8 glTF animations. The V6 arm mesh used inward-facing winding; the candidate reverses winding and normals, and signed volume changes from -0.001405462 to +0.001405462 cubic metres.
- Corrected seven-mesh smartwatch **GLB binary** by removing 20 zero-area triangles. Individual real case, top/bottom straps, rotary crown, side button, OLED screen and glass meshes stay separate.
- Replaced the V6 *screen-projected skin image containing the room background* with independent 4096x4096 skin Albedo & Normal, 2048x2048 Roughness, AO, SSS and DetailNormal **procedural baseline** maps. These are not final scanned / hand-painted photoreal assets; Blender/Unity material visual sign-off remains mandatory.
- Corrected C# source candidate for crown jumps (24° * every half-hour step), hit-tested OLED dragging (no drag initiated off-screen), queued tactile audio steps, local authored crown rotation rest offset and missing-polygon prefabs **without creating stand-in limbs/buttons**.
- Created a Blender 4.x *actual import → skin/material assignment → .blend save → reopen → genuine binary FBX export* script. It has **not been run**, because the current execution container has no Blender installation.
- Added a Unity Editor AssetDatabase audit requiring real 25-bone SkinnedMeshRenderer, 7 distinct watch Mesh objects, valid renderer/collider/Animator/material references and non-static arm clips. It has **not been run**, because Unity is unavailable in this container.
- Created a fail-closed non-mutating Git-only preflight script. The V6 installer with its unsafe non-Git acceptance is **not authorized for use**.

### Binary asset delivery

The corrected GLB and materials cannot be delivered through this text-only GitHub connector. They are packaged as a downloadable conversation ZIP: **ROKAS_BED_SLEEP_V7_ENGINEERING_CHECKPOINT.zip**. The ZIP is a candidate for further artistic development, **NOT a verified Unity install package**. It excludes the V6 fake/unverified .blend and unre-exported FBX masters.

### Required quality gates before any merge

1. Run the supplied Blender script *on the corrected V7 GLBs* with Blender 4.x and keep its native generated .blend/.fbx/validation logs.
2. Check organic hand anatomy and UV paint against the actual Reference A video. A surface-normal fix **alone does not sculpt realistic fingers**.
3. Import Blender-exported FBX into a separate full Unity project, use the included Editor AssetDatabase validator, ensure no MissingScripts/meshes/materials and run both EditMode/PlayMode.
4. Record actual Unity Game View of all 11 phases including skinned Raise/Lower and mouse-driven OLED; compare visually to the two user supplied reference shots.
5. Only after native passes, prepare a branch-specific safe commit with the real assets and tests. No force/reset/clean, no modifications to main user's branch without validation.

This documentation is an exact checkpoint status. Do not interpret CI green for text-only files as proof of a photoreal 3D scene.
