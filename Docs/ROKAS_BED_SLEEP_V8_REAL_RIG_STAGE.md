# ROKAS Bed Sleep V8 — actual skinned-finger animation + native Unity prefab staging

**Status:** Engineering candidate, not installed/merged. This stage continues V7 in isolated branch `codex/bedsleep-v7-production-qa`. Main ROKAS, Laptop V11 and GameSession unchanged.

## Verified real binary changes (in conversation ZIP)

Artifact: `ROKAS_BED_SLEEP_V8_REAL_RIG_ANIMATION_CANDIDATE.zip` (43 files, SHA256 verified). Binary assets are supplied through the chat archive, not committed with this text-only GitHub connection.

1. The 25-joint arm **GLB** retains original imported geometry, 2 material groups and 8 animations. Each of the 8 actions now contains **19 glTF transform channels** instead of 4: original root/forearm/wrist plus 15 finger-joint rotation channels. There is no additional fake mesh.
2. CPU glTF skin evaluation samples 15 frames per clip, computes weighted deformed vertex positions, and confirms Raise/Lower max displacements ~0.16858 m; InspectIdle 0.00538 m peak, CrownFeedback 0.00388 m, Confirm 0.00625 m. These are **actual skinned vertex movements**, not only nonzero animation data. See `14_V8_REAL_IMPORT/V8_VERTEX_SKINNING_REPORT.json`.
3. GLB topology pass: skin has 25 joints, 8 clips with 19 channels each, zero boundary edges (30,954 manifold edges), and smartwatch has seven independent meshes without zero-area faces. See `14_V8_REAL_IMPORT/V8_BINARY_QA_REPORT.json`.
4. `KeikoFPSArmRigController.ApplyPose` now multiplies twist/wrist rotations by each bone's imported local bind quaternion; no more snapping to identity. `Smartwatch3DController` searches the entire imported FBX hierarchy for the actual rotary crown, button, OLED mesh/collider; it refuses missing parts rather than cloning placeholders. `BedSleepVisualComposite` assigns imported arm/watch children to render layer 30 and scopes the isolated camera culling mask to that layer.
5. New **Unity Editor** `ROKASBedSleepV8PrefabBuilder.cs` builds three *separate candidate prefabs* under `Assets/Rokas/Resources/BedSleep/V8Candidate/`, never overwriting the existing assets. It uses real imported FBX Meshes, materials and a real 25-bone skinned renderer, seven distinct watch MeshFilters and screen collider. It refuses missing imported FBX animation actions/finger animation curves and creates a controller with eight genuine FBX-backed animation states and transitions, rather than using the old static-finger `.anim` clips.
6. Blender script `V8_BUILD_REAL_BLENDER_MASTERS.py` imports V8 GLB, verifies armature, eight actions, real mesh, skin weights, material assignments; saves/reopens native Blender files; exports FBX **with all actions** and re-imports the FBX to verify actual structure. Script has been prepared but **not executed**.
7. All existing standalone FBX/.blend/.prefab from V6/V7 are intentionally excluded from the V8 ZIP because they have not been re-exported with this changed skin/animation; do NOT treat them as V8 deliverables.

## Native acceptance still REQUIRED before mainline

- Run Blender 4.x and inspect natural hand shape, fingers, fabric/sleeve, UV, materials, bone deformation and exact approved reference pose. The underlying geometry is still silhouette-derived; skin art/sculpting are **not** declared photorealistic or finished.
- Import newly generated Blender FBX into a separate Unity 6000.3.19f1 working copy, run menu `ROKAS > Bed Sleep > V8 > Build REAL Imported Candidate Prefabs` and a native AssetDatabase test. Check animations import with finger curves and camera receives only dedicated 3D layers.
- Capture actual Unity Game View video showing 11-state flow and compare with two approved reference images before review merge.
- Never invoke the old V6 installer; never force/reset/clean Git or mutate the primary `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` without approved staging and verification.

The ZIP contains full source files, human-readable diffs, independent validation, instructions. **GitHub branch currently contains checkpoint documentation only; no binary files have been pushed to GitHub.**
