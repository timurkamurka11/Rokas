# ROKAS V17 — Unity import checkpoint (PR #13, draft)

## Confirmed, with actual artifact evidence

- The original V16 in-game bed/sleep hand, watch, OLED and camera have **not been replaced**.
- Base candidate is checked in at `Artifacts/ArgonV17Candidate/ROKAS_Keiko_FPS_Arm_Rigged_V17_Candidate.fbx`.
- Blender geometry: **28,294 vertices, 56,584 triangles, one joined mesh, zero boundary edges, zero non-manifold edges**, plus 8 nonempty corrective shapes.
- The base FBX preserves all three **skeletal** actions, but Blender 3.x `All Actions` export makes its blendshape animation channels zero. **Do not rely on base FBX alone for animated shape correction.**
- Three separately exported, single-action FBXs are checked in:
  - `ROKAS_V17_V17_Grip_Animated.fbx`
  - `ROKAS_V17_V17_Index_Animated.fbx`
  - `ROKAS_V17_V17_Living_Animated.fbx`
- Every paired FBX has a corresponding `ROKAS_V17_{Grip,Index,Living}_PairedClipQA.json` validated **after re-import into Blender**.
- Tested index MCP shape weights: Grip **0 → 0.5603**, Index **0 → 0.3477**, Living **0.0244 → 0.0605**.
- The index-only skeletal clip changes index vertices and not the other four strongly weighted finger islands.
- GitHub Actions `Argon V17 actual FBX reconstruction` run **39341073413 PASSED**.
- Existing `Core behavior` job fails on unrelated checked-in general image/audio resource validation; it is **not** proof of C# or Unity Play Mode pass.

## Prepared Unity import (not yet run)

`Assets/Rokas/BedSleep/ArgonIntegration/Editor/RokasV17CandidateStaging.cs`
provides the Editor menu:

`ROKAS > Bed Sleep > Argon > Stage V17 verified FBXs (inactive)`

The script reads the *existing checked-in FBX and QA JSON* from the repository,
checks geometry and the three animated shape-key reports, copies exactly four
FBXs under `Assets/.../V17Candidate/Imported/`, and imports them as Generic
with animation and blendshapes.

After import, it requires:
- `Keiko_Hand_Skin_Mesh`, an imported 25-bone SkinnedMeshRenderer.
- 8 or more imported corrective BlendShapes and valid UV0.
- Non-empty playable animation clips in the four imported model assets.

It does **not** create a new scene, replace the active Resources prefab, alter the
watch/OLED or enable an experimental toggle in production. Existing staged
FBXs are backed up under `Library/ROKAS_V17_StageBackups/`.

### Release hold

Actual Unity Editor import, runtime transition/pose order, mapping of the
separate Generic skeletal/morph clips to a single in-game rig, real POV video,
anatomy of the little/index webs during bending, finger collision, OLED/crown
functionality, and normal sleep cycle are **NOT YET VERIFIED**.

**Next technical gate:** run the Editor-stage preflight in the original Unity
project. Verify the actual FBX imported bone hierarchy and BlendShape clip
bindings; explicitly map and blend V17_Grip/Index/Living only AFTER the
existing V16 functional animation path remains intact. Inspect camera-stable
same-angle videos of open hand / half grip / fist / release and both finger
roots. Do not merge PR #13 or publish an installer for V17 until those gates pass.
