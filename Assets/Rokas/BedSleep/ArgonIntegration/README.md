# ROKAS Bed Sleep — Argon candidate import (isolated, not active)

Input: ROKAS_ARGON_HAND_SMARTWATCH_UNITY_READY.zip from Argon.

The binary assets are not yet in this GitHub branch. This is a source-only importer and Unity QA scaffold. No game model has been replaced or validated in cloud Unity from the Argon package yet.

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

The ZIP integrity passes. Its GLB has 10 meshes, 25 bones and 8 animations, but only 11 animated channels per clip; individual finger phalanges may remain static. Its GLB does not embed image textures; PNGs are delivered separately. Its eight Unity material YAML files have no texture references. Unity import has not yet been verified for these actual binaries. Never claim production-ready based only on the packaged QA JSON or web HTML demo.

No whole-game archive or EXE is required.
