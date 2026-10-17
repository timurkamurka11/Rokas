# ROKAS Bed Sleep — Argon candidate import (isolated, not active)

Input: ROKAS_ARGON_HAND_SMARTWATCH_UNITY_READY.zip from Argon.

**SECURITY GATE (2026-10-16): This repository is currently PUBLIC.**
Do NOT upload the proprietary Argon asset ZIP as a GitHub Release asset while
the repository is public. Public repository releases are visible to everyone.
The GitHub workflow explicitly refuses to download such assets unless the
repository has first been changed to PRIVATE.

CURRENT STATUS (2026-10-17): The original Blender-reexported authentic Argon FBX, PNG maps, 25-bone skin and 7-part physical watch ARE committed on this isolated PR branch. Real Unity import and cinematics have passed candidate QA. This is NOT yet a production Bed Sleep V10 model replacement.

The existing Bed Sleep V10 remains active. This importer only writes under Assets/Rokas/BedSleep/ArgonCandidate/ and never overwrites V10ReleaseCandidate, save logic, clocks, hub, combat or its preview camera.

## Input source path

Unpack the ZIP's inner ROKAS_ARGON_HAND_SMARTWATCH_UNITY_READY directory under Assets/Rokas/BedSleep/ArgonSource/ so it contains:

- Models/ROKAS_Keiko_FPS_Arm_Rigged.fbx
- Models/ROKAS_Smartwatch_HighDetail.fbx
- Animations/SleepArm_*.fbx (eight files)
- Textures/Skin/*.png
- Textures/Sleeve/*.png
- Textures/Watch/*.png

Do not copy Materials/*.mat from the ZIP over ROKAS assets. They are partial Unity YAML and do not contain texture references. The importer constructs isolated Standard PBR materials with explicit texture references.

## Unity QA

In Unity 6000.3.19f1 use: ROKAS > Bed Sleep > Argon > Validate and build isolated candidate.

Or upload the original ZIP (as an asset) to a private GitHub release tagged bedsleep-argon-assets and run the manual cloud Action: ROKAS Argon Candidate — Private Release Import QA. The Action should stage the asset into its temporary checkout and run actual Unity EditMode validation without changing the checked-in game.

The importer checks three genuine SkinnedMeshRenderers with 25 bones/bind poses each, all seven independent physical watch meshes, live-compatible OLED UVs/collider, dedicated watch socket, eight native FBX animation clips, physically plausible imported units, real materials and texture maps. It writes an independently inspectable candidate prefab.

## Why not swap directly into V10

The active V10 controller requires one SkinnedMeshRenderer with three submeshes and bones named Keiko_FPS_LeftArm_Root / WatchSocket_L. Argon has three separate SkinnedMeshRenderers and bones Arm_ForearmRoot / Wrist_WatchAttachment. Direct replacement would break the V10 animation/controller reference contract.

After successful cloud FBX import and visual QA, add a separate runtime adapter and only then enable it behind an explicit mode toggle. Preserve the original bed, game save, input, OLED RenderTexture and camera infrastructure. Visual QA must include: arm entering bottom-left, real wrist anatomy, watch fit and screen readability, crown input and full arm lowering.

## Source limitations detected

The ZIP integrity passes. Its GLB has 10 meshes, 25 bones and 8 animations, but only 11 animated channels per clip; individual finger phalanges may remain static. Its GLB does not embed image textures; PNGs are delivered separately. Its eight Unity material YAML files have no texture references. Imported original FBX/PBR/OLED binaries were subsequently verified in Unity candidate QA; the unresolved defect is the source hand/finger-base mesh shape. Never claim production-ready based only on the packaged QA JSON or web HTML demo.

No whole-game archive or EXE is required.

## Current cloud QA + binary staging

The authentic GLB/Blender re-export has passed Unity EditMode import (run 38926185033), with a 3-skinned-mesh, 25-bone, 8-clip real FBX candidate. The original imported OLED geometry now uses an emissive live-texture shader; pixel check improved from 1 to 448 in the Unity screenshot test. This is only a **visual candidate**, not a production Bed Sleep replacement: raw camera framing shows the cuff entering from the top, and skin normal/lighting still needs checking.

A separate first-person camera screenshot gate (run 38928867250) evaluates the actual original Raise/Inspect/Lower clips with camera-up oriented toward the hand and rigid root translation for a lower-left entrance. Do not use the live game merge until those frames are reviewed.

After QA passes, the disposable Actions checkout may publish **genuine original FBX, PNG textures, generated .meta, materials, and preview prefab** ONLY to the isolated Argon candidate branch if the QA request marker contains an exact line `PUBLISH_CANDIDATE=YES`. With no such line, the job remains QA-only. The gate never writes to the production Bed Sleep Resources prefab or default branch. The repository is still public, so derived assets published to this branch will be public too; the user has already intentionally published the source ZIP in a public release.

## Living hand / audio candidate checkpoint (2026-10-17)

Cloud Unity 6000.3.19f1 build [#38986183629](https://github.com/timurkamurka11/Rokas/actions/runs/38986183629) PASS: real Argon skinned hand and physical watch over authentic captured bed ceiling, 167 actual 1280×720/24fps frames and synced original Foley MP4/WAV (artifact `12083567201`). Horizontal framing remains APPROVED. Smooth matte skin preview avoids source normal-map aliasing.

Separate isolated runtime candidate contains `ArgonLivingRig` (non-accumulating realistic breathing/wrist/finger sway), `ArgonWatchControls` (physical OLED & crown, existing V10 input router binding, no duplicate input), and `ArgonWatchFoley` (cloth, wheel clicks, confirmation). See `CandidateRuntime/README.md`.

**Known blocker before any merge:** visible original FBX finger-base rectangular geometry seam and insufficient source finger detail, observed even in the older pre-living-motion render. Further 3D topology repair and manual visual acceptance required; neither the new procedural PIP/DIP curls nor specular controls can legitimately conceal broken topology.

A focused Unity [no-drift / V10 input test](https://github.com/timurkamurka11/Rokas/actions/runs/38987162675) is running; do not call it successful until its final status confirms PASS.

Production V10 scene, user game state and default branch are UNCHANGED. Draft PR #13 remains DO NOT MERGE.
