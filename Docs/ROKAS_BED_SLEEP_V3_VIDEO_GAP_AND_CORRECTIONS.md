# ROKAS BedSleep V3 — user-video gap analysis and source corrections
Date: 2026-10-15

## Evidence, not assumptions

Inspected two user-supplied recordings:
- Bandicam Unity capture `bandicam 2026-10-15 13-28-10-071.mp4`: 23.6s, 2432x1260.
- Argon V3 `ROKAS_BedSleep_V3_FullCycle_Video.mp4`: 4s, 1672x940.
Also inspected all 55 entries of `ROKAS_BedSleep_V3_Production3D_Package(1).zip`, including actual GLB accessors, source scripts, eight AnimationClip YAMLs, PBR maps and screenshots.

The visual difference is confirmed: Bandicam's watch has external `-30m / +30m / УСТАНОВИТЬ` controls and a mostly static arm presentation. Argon preview uses a dark cinematic transition, bed ceiling without the arm, bottom `ПОСТАВИТЬ БУДИЛЬНИК` and subsequently a large watch as the primary interface, with no floating +/- buttons over the room.

The 4-second video is a **visual target**, not an independently verified recording of the actual 25-bone rig playing in ROKAS Unity.

## Root causes and fixes

1. **Wrong presentation camera angle**: V3 smartwatch case/glass/OLED has an XZ-plane display (normal +Y), but the previous isolated camera looked along +Z and therefore saw the watch edge. Corrected bridge to face -Y, with image up aligned to +Z, in `RokasBedSleepProductionRigBridge.cs`.
2. **Crash when spawning OLED**: supplied `BedSleepCinematicController` constructed a plain Transform and its `SmartwatchOLEDPresenter` dereferenced missing `RectTransform`. Corrected in the downloadable V3 integration ZIP by creating `GameObject(..., typeof(RectTransform))` at inception.
3. **Invisible/nonfunctional OLED dial ring**: supplied OLED source declared `radialRingFillImage` and `ringKnobIndicatorPivot` but did not create them; the downloadable ZIP now generates a live radial ring and indicator once per watch.
4. **Missing watch generator dependency**: old source called `ROKAS.Hub.SleepInteraction.ProceduralSmartwatchMeshBuilder`, which V3 package did not contain. Updated to the included `ROKAS.BedSleep.V3SmartwatchGeometryFactory`.
5. **Double mouse input**: V3's InputMapper and native ROKAS controller can both process wheel/keyboard. GitHub bridge disables the V3 input mapper while preserving the ROKAS native user input and animates wrist/watch continuously during selection.
6. **2D controls covering OLED**: GitHub bed presenter hides fallback photographic LCD and external +/-/confirm buttons while the real 3D path is active. Keep the transparent watch interaction hit area, mouse wheel/drag/Enter and normal native ROKAS `Confirm`.
7. **Missing PBR Resources**: old archive stored skin and sleeve maps under Textures only, while GitHub bridge expects `Resources.Load<Texture2D>("BedSleep/V3/...")`. Revised ZIP includes the matching `Resources/BedSleep/V3` paths.
8. **Incorrect model extensions**: the .fbx arm was GLB renamed .fbx; watch .fbx was OBJ; watch .glb was JSON-based glTF rather than GLB. Revised ZIP keeps the valid arm GLB, correct OBJ and corrected binary watch GLB.
9. **No real baked GLB animation**: skin has 25 joints, 17815 vertices, 2 primitives, but no glTF animation channels. Eight separate Unity .anim assets contain bone curve keys. The current live rig is assembled via AnatomicalArmRuntimeLoader and posed by FPSArmAnimationController in C#.
10. **Safe non-destructive installation**: downloadable ZIP only imports optional V3 source/texture/mesh resources. It does not replace `RokasBedSleepCinematic.cs` or `RokasView.cs`; if different V3 files exist, the installer requires an explicit `REPLACE` with verified _Recovery backup, then `INSTALL`.

## Current truth / remaining gate

GitHub's bridge and native control flow compile independently of the optional Argon module. **A cloud PASS without installing the binary V3 files is not a validation of the actual hand appearance or a perfect match to the Argon video.** Further verification must be performed after installing the corrected ZIP inside the live `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY` working copy in Unity.

Required visual checks: real mesh present (SkinnedMeshRenderer 25 joints), correct front-facing OLED, hand truly rises/lowers rather than fading, interactive watch UI only on 3D screen, skin/strap textures, lighting ON/OFF, ceiling transition and Escape. Capture video and compare to the supplied 4-second V3 reference. Do not claim exact visual parity until such a capture has passed.

No genuine Blender .blend or true FBX master exists in the provided archive; modeling/retargeting may still be required to get high-end realism instead of the existing mathematical anatomical mesh.


## Follow-up input, art, and asset installer corrections

- The isolated camera exposes the **actual projected OLED position** to ROKAS so the transparent UI event hit rectangle follows the live watch rather than the photographed placeholder position.
- 270-degree circle clicks set 1–12 hours in 30-minute steps; drag or scroll steps by 30 minutes; clicking the lower section confirms. The fallback external +/- buttons are hidden only when the live 3D watch exists.
- `RokasBedSleepProductionRigBridge.TickIdle()` updates the skeletal arm, crown and OLED continuously during the selection phase. The genuine V3 InputMapper remains inactive while native ROKAS owns input, avoiding doubled ticks.
- A corrected ZIP named `ROKAS_BedSleep_V3_Corrected_Source_And_3D_Assets.zip` accompanies this revision. It contains the actual user-supplied 3D GLB, 8 Unity AnimationClips, watch mesh, six correctly placed PBR Resources maps, UI fixes, true Blender-export helper and source/visual validation files. The installer demands verified backups and two explicit confirmations before replacing previously installed *different* V3 files.
- The ZIP is a **conversation attachment**, not an asset that GitHub currently contains; the GitHub-only Unity CI compiles the optional bridge, not the binary V3 runtime mesh. Verify the full combined package after local installation.

### No unverified claims

The Blender FBX export helper is included but has not run in this environment. The original Argon preview may be precomposed rather than an in-engine live 3D demonstration. Final photorealistic visual parity, actual imported 3D wrist pose, full mesh skinning in Unity and real sound synchronization remain unconfirmed until a fresh local screen recording.
