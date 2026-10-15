# ROKAS Bed Sleep V9 — skinned fingernails and importer QA checkpoint

**Engineering-only checkpoint; do NOT merge or install in the primary ROKAS worktree.** Existing Laptop V11, HomeView, main GameSession and combat are unchanged.

## Real binary change (provided in conversation ZIP)

Artifact: `ROKAS_BED_SLEEP_V9_REAL_NAILS_AND_PREFAB_CANDIDATE.zip` (29 entries with SHA-256 verification, see `V9_SHA256_MANIFEST.json`). This GitHub connection does **not** upload binary GLB or Unity source from the archive; the ZIP is the authoritative artifact.

- Source: V8 25-bone skinned arm / eight 19-channel animations, all preserved. The original two GLB primitives (skin and sleeve) and their accessor bytes stay unchanged.
- Added third GLB primitive: **5 separate rounded/tapered closed 3D fingernail shells** with real UVs and a dedicated physically based nail material, skinned rigidly to `Thumb_03_L`, `Index_03_L`, `Middle_03_L`, `Ring_03_L`, `Pinky_03_L`. Added 1170 genuine mesh vertices and 2320 triangles; each nail is watertight and has positive signed volume. These are not floating UI decals.
- A real Blender 4.x script `V9_BUILD_REAL_BLENDER_MASTERS.py` imports the three-material GLB, retains skin/sleeve/nail material groups, saves native .blend, reopens, exports FBX and imports FBX for verification. **This script has NOT executed**: Blender is not installed in this execution environment.
- A V9 Unity Editor candidate-prefab builder requires 3 separate arm material primitives (not V8's 2), 25 imported bones, all 15 finger animation curves, 7 separate watch meshes, actual materials and imported FBX clips. It generates candidate assets only under `Resources/BedSleep/V9Candidate`; it does not overwrite production prefabs.
- V8 rig/watch/visual-composite source is included as an integration baseline, but has not undergone native Unity compile in V9.

## Offline independent validation

- `glTF` 2.0 header and JSON roundtrip pass.
- 25 skin bones and 8 animation clips with 19 channels each preserved.
- Original binary vertex/index/weights/UV and animation buffers preserved byte-for-byte.
- Trimesh independently sees 3 GLB geometries and **5 watertight nail connected components**.
- Seven separate watch mesh objects still exist.
- ZIP CRC and all SHA-256 match.

## Visual QA caveat

This fixes one clear anatomical omission **but is NOT yet a finished photorealistic hand**. V8 base mesh is still derived from an extruded silhouette, rather than an artist-quality sculpt. The nail-placement preview is a mesh orthographic diagnostic, **not a real game render**; placement, penetration and colors require review in Blender under reference lighting. Also watch pose, OLED and animation must still be tested in Unity Game View against the user's reference video.

Native gates before modifying the main ROKAS branch: Blender 4.x save/reopen/FBX export+reimport; Unity 6000.3.19f1 import + exact prefab build and PlayMode; live raise/lower and time-selection recording; image-to-image acceptance.
