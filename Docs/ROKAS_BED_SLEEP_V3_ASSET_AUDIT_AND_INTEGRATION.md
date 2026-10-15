# ROKAS Bed Sleep V3 — Binary asset audit and native bridge (2026-10-15)

## Status

The original user-supplied Argon archive and 4.0-second preview video were inspected. The visual reference is a composite sequence, **not proof of live Unity skeletal animation**. Production-quality matching to the supplied two ceiling/hand photographs remains a visual acceptance task.

**Committed in GitHub:** `Assets/Rokas/Scripts/Presentation/RokasBedSleepProductionRigBridge.cs`, a separate optional 3D presentation bridge, with `RokasBedSleepCinematic.cs` preserving `RokasView`'s existing constructor, `Begin/Tick/Cancel/Dispose`, HomeCanvas, Escape and native single-save semantics.

**Delivered as conversation ZIP, NOT hosted in GitHub:** `ROKAS_BedSleep_V3_Validated_Import_And_Bridge.zip`. Use the archive's `INSTALL_V3_ASSETS_SAFE.bat` **only after syncing the GitHub changes**. It refuses to overwrite different existing assets. It does not modify the live `RokasBedSleepCinematic.cs` facade.

## User-supplied package integrity findings

| Submitted file | Verified actual data | Correction |
|---|---|---|
| Keiko_FPS_Arm_Rigged.fbx | Identical binary GLB 2.0, not FBX | Drop false .fbx; keep rigged valid GLB |
| Keiko_FPS_Arm_Rigged.glb | Real GLB, 1 skinned mesh, 25 skin joints, 2 primitives, 0 embedded animation channels | Keep as authoring input; runtime Unity arm uses procedural skinned mesh generator |
| ROKAS_Smartwatch_HighDetail.fbx | Wavefront OBJ text, not FBX | Rename to .obj |
| ROKAS_Smartwatch_HighDetail.glb | JSON glTF with embedded base64 BIN, not binary GLB | Pack into valid binary GLB |
| SmartwatchOLEDPresenter.cs | Invalid literal containing an unescaped newline | Correct string |
| Smartwatch3DController.cs | References missing ProceduralSmartwatchMeshBuilder from previous module | Include isolated V3SmartwatchGeometryFactory based on previously supplied source |
| Input fallback | Direct legacy Input calls outside conditional compilation | Guard ENABLE_LEGACY_INPUT_MANAGER |
| Argon V3 replacement facade | MonoBehaviour with different API from ROKAS native plain C# controller | **Never install**; retain native presentation facade |
| Argon V3 NUnit tests | Depend on invalid replacement facade and lack matching ROKAS assembly setup | Preserved as source, **not automatically installed** |

The valid binary arm GLB is **not automatically imported** by stock ROKAS Unity 6, because `Packages/manifest.json` has no glTF importer. The V3 module currently generates a 25-bone skinned mesh at runtime and the bridge renders it into transparent `1920x1080` RenderTexture. A separate Blender export/retarget stage remains necessary for a true authored FBX/Blender pipeline. There are no genuine `.blend` or `.fbx` files in the supplied archive; the bundled Blender script alone does not produce finished .blend files until Blender runs.

## Native bridge behavior

- Existing 2.5D bed entry/ceiling/dark transition remains in `RokasBedSleepCinematic`.
- If the optional V3 module is present, the 25-bone arm, watch and real WorldSpace OLED are created on an isolated render camera on layer 30.
- PBR skin, normal, roughness and subsurface textures are loaded from `Resources/BedSleep/V3`. Do **not** use the camera-projected screenshot as skin UV albedo.
- Raise and lower are driven from the existing 2.5D phase coroutines; the photographed arm is suppressed when the live 3D rig is available.
- Alarm duration and game/save state are owned solely by the original ROKAS controller; crown steps are forwarded to the 3D watch.
- Missing assets safely fall back to the 2.5D photographic implementation. Cleanup destroys the isolated camera and render texture.
- Old 2D OLED replacement rectangles are hidden in production mode so they don't obscure the real screen.

## Required final visual acceptance

This is an engineering integration, **not a completed photorealistic art sign-off**. The supplied geometry has not been visually validated in the user's Unity editor with the delivered binary resources. Specifically check finger topology and joints, sleeve/skin PBR result, final wrist/watch orientation, OLED pixel readability, crown click-to-step sync, near-plane clipping, transparent compositing and all light states.

The original video has a composed arm that resembles the intended artistic output, but does not demonstrate that the skinned mesh reproduces the reference pose. Only local Unity footage or an actual production asset import into CI can confirm that.

## Installation and verification

1. Close Unity and sync the existing working copy `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` from branch `codex/fidelity3-resume-5779190` without force/reset/clean.
2. Extract the conversation ZIP and run `INSTALL_V3_ASSETS_SAFE.bat`. The installer first verifies the optional bridge exists and refuses different-file collisions.
3. Open Unity; inspect compiler diagnostics and `[ROKAS BedSleep V3]` warnings.
4. Enter Main Room -> bed -> dark transition -> `ПОСТАВИТЬ БУДИЛЬНИК` -> live 3D raise -> dial changes -> confirm -> lower -> eye close.
5. Capture screenshots/video and report mismatches with reference A/B. Run Home/Laptop regressions and focused V3 runtime tests after actual local import.

Do not claim final photorealism, successful local import, or model FBX/Blend deliverables without direct evidence.
