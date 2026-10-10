# ROKAS Laptop Cinematic safe installer — NOT RELEASED
This directory stores the canonical PowerShell installation/rollback source and its isolated Windows safety test. Do not distribute it as a complete release.

Required approved release ZIP: INSTALL_SAFE.ps1, RELEASE_MANIFEST.json with validation=VISUAL_AND_UNITY_QA_APPROVED and sourceSha, and payload/Assets/Rokas/Resources/LaptopCinematic/ containing exactly 49 HandsRight/Hand_0000..0048.png plus right_hand_manifest.json.

All 50 payload files are verified by SHA-256 before modifying the target. Existing user files are backed up with hashes. Rollback preflights all installed and saved file hashes, and rejects post-install user changes. The installer blocks symlinks/junctions in affected paths.

The windows-latest test creates a disposable Unity-shaped test project and fake 1×1 PNGs, never the real D: project. Its result does NOT establish Blender visual QA, physical 3D button penetration, user-approved POV correctness or real local Windows Unity behavior. Do not change release gate until those independent checks pass.
