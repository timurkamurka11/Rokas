# Keiko cinematic sword animation repair

Authoritative project: `D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY`.

Blender executable discovered through its Windows installation registry: `D:/3DMODELS/blender.exe`, version 5.2.2 LTS.

Reproducible recipe: `repair_sword_animations.py`. Run through `run_blender.py repair_sword_animations.py`. A second argument `preview` exports into `preview_sources` instead of the project. Original source FBX inputs remain untouched; `animation_bake_report.json` records their SHA-256 hashes alongside each output hash, duration, bone list, palm error and sampled blade-tip trajectory.

## Authored takes

| Take | Duration | Keys at 120 fps | Contact |
|---|---:|---:|---:|
| Two handed battle idle | 2.000 s | 241 | — |
| Normal preparation | 0.2667 s | 33 | — |
| Heavy preparation | 0.400 s | 49 | — |
| Normal attack corrected | 1.100 s | 133 | 0.600 s |
| Hard jump attack corrected | 1.4333 s | 173 | 0.8667 s |
| Two handed approach | 0.800 s | 97 | — |
| Two handed return | 0.800 s | 97 | — |
| Enter battle corrected | 1.200 s | 145 | — |

The original `Enter the batle.fbx` was copied from the supplied sword-attack folder and kept alongside the corrected source. Its corrected take uses the source body transition with a two-hand ready stance.

Normal now has explicit ready, upward anticipation, maximum backswing near 0.44 s, rapid forward arc to contact at 0.60 s, downward followthrough, and recovery to ready. Heavy has an overhead backswing near 0.68 s and a fast descending arc toward contact near 0.8667 s. The original Heavy lift, knees, jump, landing and hip pitch/roll remain. Its repeated full-body spins were replaced by a bounded hip turn from −50 to +10 degrees; neck/head aim remains toward the opponent.

Idle and preparation use the source's initial grounded stance. Idle has a 2 mm hand breathing motion with a matching loop endpoint. Both upper arms, forearms and wrists have actual dense keys. The support palm is authored 5.5 cm behind the main palm on the sword shaft. Fingers are curled into the grip. Dense linear keys and quaternion hemisphere continuity remove interpolation errors.

All eight editable `.blend` files are saved next to the recipe. `render_authored_poses.py` produces textured front, side and three-quarter source renders. `check_fractional_grip.py` verifies both editable takes and reimported FBX at fractional times; `check_fbx_units.py` and `check_export_roundtrip.py` check source/rest-coordinate preservation.

## Unity extraction and runtime

The actor still instantiates the original `Keiko@Idle` Legacy model. FBX source meshes exist only to preserve the original bind pose during export; extracted standalone `.anim` copies contain no source-model references. No second Keiko is instantiated.

FBX uses unit scaling in `FBX_SCALE_UNITS`. The redundant identity `Armature/` wrapper is removed from copied clip bindings. Verification requires every copied binding to resolve against the existing actor and throws on any mismatch. Source root horizontal translation is removed for stage-controlled movement; authored jump height remains.

Only the eight corrected sources bypass the normal model importer compression/resampling policy. Their 120 fps keys stay uncompressed. Original Keiko, Mina and Yokai import policies are preserved.

The small runtime two-bone support-arm correction resolves blended palm positions. It preserves the sampled wrist orientation and restores sampled rotations before the next animation sample.

Public stage APIs are `PlayPreparation(bool heavy)`, `PreparationDuration(bool heavy)`, `PlayEnterBattle(float duration = 0)`, `EnterBattleDuration`, `AttackDuration(bool heavy)`, and `AttackContactSeconds(bool heavy)`. `ActionRecoveryComplete` waits for pose duration and hit stop; `IdleSettled` accepts a direct fully weighted idle or a completed blend, with hit stop finished.

## Regression evidence

Sword EditMode expectations cover corrected clip paths/frame ranges, full support-arm rotation bindings, uncompressed sources, standalone clips and the separate left-hand target. PlayMode checks all eight raw takes at nine fractional samples, validates the Normal backswing/fast contact arc, and checks palm position plus wrist direction across runtime blends and recovery. Capture sheets contain four poses from actual model-relative front, side and three-quarter views.

Unity imports, all suites and live arena captures are serialized by the parent task. Blender source renders and numeric palm checks are evidence for the authored assets; the parent retains the latest Unity test and runtime evidence.
