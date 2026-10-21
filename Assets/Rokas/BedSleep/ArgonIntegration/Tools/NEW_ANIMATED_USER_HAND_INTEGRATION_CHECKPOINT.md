# ROKAS — newly uploaded animated hand → existing REAL Bed Sleep scene

Continuation of the same **draft/unmerged PR #13**, superseding prior procedural V17–V22 and the earlier unrigged GLB experiment.

## Exact new user attachments and verified source

- `ROKAS_NewHand_Rigged.zip` contains **one** `ROKAS_NewHand_Rigged.glb`, SHA-256 **`6d5afa3492400a528db48cf07929fd88c8bd84eacd55ba09071510910542d493`** (8,387,316 bytes).
- `ROKAS_NewHand_Animation.mp4`: actual provided 4.00-sec/120-frame, 30-fps, 876x512 recording showing fist opening and closing.
- Parsed GLB v2 JSON: **one skinned textured mesh, one embedded JPEG albedo, one native 23-joint skeleton, one real 4-second animation `ROKAS_Hand_Finger_Test` with 69 animation tracks** (translation/rotation/scale for each node). Geometry has 53,830 indexed skinned vertices; file contains proper `JOINTS_0`, `WEIGHTS_0`, `inverseBindMatrices` and a UV set. This is a **different, already rigged** asset, not the earlier raw 115k-vertex GLB.
- Exact extracted JPEG texture SHA-256 **`54ae7a113c74a5c0e842a5c866d1a5409c4b28fb662c841c1c20362d2973e7c8`**.

## Actual implementation in same PR (no production mutations)

1. `Editor/RokasUserGLBRealBedPreview.cs`: **new Editor Play Mode-only real-bed activation**, auto-detects the existing `ROKAS_Argon_Complete_Hand_Watch_InGame` spawned by the normal bed click. Uses **the converted new GLB FBX**, validates native five finger chains + real imported animation, aligns native `wrist` to gameplay's `Arm_Wrist`, scale calibrated by original forearm length, drives the user's real 4-second clip, and hides only old V16 hand/sleeve/nails/dorsal-veins *during Play Mode*. Existing real titanium smartwatch, silicone strap, physical OLED geometry/collider, Russian sleep timer and input are **left visible and untouched**, thereby sitting at their original in-game wrist position above the new candidate surface. Restores original render states on failure/Play exit. Generic ModelImporter settings affect only this exact new FBX path. **No scene/prefab/base runtime code changes.**
2. Legacy `Editor/RokasV17RealBedAutoPreview.cs` has a guard against double-installing old V17/V22; also the new preview static constructor disables legacy V17 editor preference before Play Mode.
3. `Tools/ROKAS_Convert_UserGLB_To_Unity_FBX.py` uses Blender GLB import, validates native skin and 23 joints, saves original textured .blend and exports Generic-compatible baked .fbx. The user's new GLB requires conversion: current `Packages/manifest.json` has NO native glTF importer.
4. A **single-click Windows BAT with offline original GLB/JPEG** is delivered in ChatGPT: `ROKAS_NEW_ANIMATED_HAND_REAL_BED_D.zip`. It pins the exact PR commit and validates the downloaded new editor C# Git blob before installing **only** the new FBX, extracted source JPEG, and editor preview C#, with automatic backups in `Library/ROKAS_NewHand_InstallBackups`. It launches Unity `D:\UnityEditors\6000.3.19f1\Editor\Unity.exe` for existing `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`. User must have local Blender for conversion; if missing, installer fails safe **before changing the game**. No Git operations, separate scene, EXE or V16 overwrite.

## Release gate

**Not yet verified on user's actual Windows Blender/Unity**. The code is a **reversible Editor Play Mode visual candidate, not shipped-game runtime integration**. Material is PBR from exact original JPG; the watch is reused in place rather than copied/reparented. Placement and clearance against user GLB skin/strap still need actual Game View inspection; do not claim precise physical watch fit without that.

User QA: stop Unity, run BAT, open original Hub, Play → bed, record whole 4-second hand motion, check fingers, nail bases, upper and lower cuff, watch strap/face alignment, OLED interaction and restart/re-entry. If any failure, preserve V16 and use Unity Console message to target the fix. **Do not merge PR #13 or declare production PASS** until these checks pass.
