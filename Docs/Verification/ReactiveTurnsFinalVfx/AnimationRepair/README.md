# Keiko Throw authoring evidence

Authored with `D:/3DMODELS/blender.exe`, Blender 5.2.2 LTS. The source is the
existing `Two handed battle idle.fbx`, using its original 33 bones and bind pose.
Existing Normal, Heavy, Block, Dodge and locomotion FBX sources were untouched.

## New takes

| Source | Duration | Sampling | Last frame | Purpose |
| --- | --- | --- | --- | --- |
| `Assets/Rokas/Art/CombatActors/Keiko/Throw preparation.fbx` | 0.400 s | 120 fps | 48 | Blend from base stance to raised, drawn-back right-hand dagger stance. Clamp the final sample while the player chooses. |
| `Assets/Rokas/Art/CombatActors/Keiko/Throw attack corrected.fbx` | 1.100 s | 120 fps | 132 | Starts at held preparation; extra draw-back, forward acceleration, release, open-hand follow-through, return to original battle stance. |

Release is sample **51**, at **0.425 seconds**. This is zero-based authoring time.
Blender re-import starts the FBX action at frame 1, so its corresponding inspection
frame is 52. Configure Unity's extracted take to frames 0 through 132 at 120 fps.
Runtime owns the one release event and projectile; the animation FBX carries
only the original Keiko mesh and rig. The preview dagger in the `.blend` is
authoring evidence and is excluded from the animation FBX.

The held preparation has one arm and dagger raised behind/outside the right
shoulder, a loaded torso, and a free left hand for balance. During the attack,
the torso turns forward while the right hand accelerates toward the enemy.
Release precedes full arm extension, so the hand continues through the throw.
The fingers relax after release and the original base stance is restored by
1.100 seconds. Horizontal root translation is constant; the arena owns motion.

## Dagger and attachment

`Assets/Rokas/Art/CombatActors/Weapons/KeikoThrowingDagger/KeikoThrowingDagger.fbx`
contains one triangulated mesh with 2,268 triangles. Its grip center is the
origin. Total length is 0.208 source units, with the point at source Z +0.167
and the pommel at Z -0.041. The blade has a diamond spine and honed edges,
the guard is a compact swept polygon, the leather grip has a real winding
seam, and the inlays use restrained cyan emission.

Five material slots: `Dagger_ObsidianSteel`, `Dagger_HonedSteel`,
`Dagger_AgedBronze`, `Dagger_CharcoalWrap`, `Dagger_FrostRune`. Unity creates
the corresponding runtime materials and reusable prefab in its builder.

The Blender source shaft is +Z, with blade-face normal +Y. FBX export uses
`axis_forward=-Z`, `axis_up=Y`, and `bake_space_transform=True`; the exported
mesh's local shaft is +Y. Check the imported model's combined transform, then
orient the complete mesh to prefab **+Z** (normally a +90-degree X rotation).
No grip-position compensation is needed because the pivot is already correct.
Attach to `mixamorig:RightHand` at `(0, 0.033, 0)`, identity socket rotation.
Stow the primary sword during Throw. Apply no two-hand sword grip constraint
to these takes.

Keiko's source rig uses X lateral, Y up, Z forward. With the current runtime
`forwardYaw=65`, the authored release direction is
`(0.422618, 0, 0.906308)`, which faces stage +X after the runtime yaw. The
right-hand +Z axis follows that direction at release.

## Verification

`verify_render_throw.py` imports both new FBX exports again and checks actual
exported bind matrices, bone count, timings, release direction and continuity.
`throw_authoring_report.json` records per-sample hand/root measurements and
source/output SHA-256 hashes. `throw_roundtrip_report.json` records:

- 33 original bones in both exports.
- Maximum rest-bind matrix error: 0.0000117421.
- Preparation starts at original ready pose: maximum matrix error 0.00000171363.
- Preparation end equals attack start: maximum matrix error 0.000000178814.
- Attack recovery equals original ready pose: maximum matrix error 0.00000171363.
- Release hand direction alignment to stage enemy: dot 0.999999955.
- Release hand speed: 2.438 source units/second, including 2.301 forward.
- Horizontal Hips travel: zero.

The actual supplied throw reference contact sheet was inspected at
`D:/Rokas/reactiveturns-b2-staging/final-vfx-reference/throw-overview.jpg`.
Its loaded anticipation and rapid forward release informed this sequence.

Rendered evidence was viewed for the held preparation, deeper windup, release
from two views, empty-hand follow-through, original stance recovery and dagger
detail. This is Blender authoring verification; integrated Unity PlayMode
review remains required for attachment, projectile detachment and camera/VFX.

## Sources and renders

`author_throw_animation.py` is the reproducible source. The three `.blend`
files preserve the dagger and both complete takes. Images named
`Throw_preparation_*`, `Throw_attack_corrected_*` and
`KeikoThrowingDagger_detail.png` show the sampled final authoring poses.
