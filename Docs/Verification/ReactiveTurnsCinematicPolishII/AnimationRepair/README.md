# Polish II animation authoring

Blender 5.2.2 LTS was run in background using the existing Keiko 33-bone and Yokai 65-bone sources. No replacement runtime character was created. Mesh-bearing FBX files are animation sources; the actor library extracts standalone Legacy `.anim` files for the existing models.

## Changed takes

| Take | Duration | Contact | Purpose |
| --- | ---: | ---: | --- |
| Two handed sword block | .8 s | .34 s | Raise the two-handed hilt into a hanging guard, downward blade covers the actual claw height, recoil and return to idle |
| Two handed dodge backstep | .8 s | .24 s | Knee plant and push, small authored hop, landing and recovery |
| Normal preparation | .3 s | — | Compact shoulder guard, held final pose during selection |
| Heavy preparation | .4667 s | — | Hilt raised beside head, blade behind and above shoulder |
| Two handed approach | .9 s loop | — | Full 27-interval source cycle; previous truncated loop removed |
| Two handed return | .7667 s loop | — | Full backward-step source cycle |
| Enter battle corrected | .6 s loop | — | Entry walking take used only while travelling |
| Enter battle settle | .4667 s | — | Stationary grounded stance after reaching the exact slot |
| Claw attack corrected | 1.2 s | .55 s | Existing Normal claw source, accelerated descending contact at source frame 35 |
| Claw heavy corrected | 1.5 s | .8 s | Existing jump claw source, descending upper-body contact at source frame 31 |
| Claw locomotion corrected | .4667 s loop | — | Full existing Yokai walk cycle, in-place extracted copy |

The previous corrected Normal and Hard Jump sword attack takes remain unchanged. All new Keiko poses author both palms on the same shaft, with the left grip 5.5 cm behind the right. The existing support-palm constraint preserves this grip across crossfades.

## Defects established from source data

- The old approach extraction used 24 intervals of a 27-interval run cycle, introducing a foot reset at the loop.
- The original EnterBattle source is a walking cycle. Replaying it after arrival made the feet move while the arena root was stationary. Entry walking and grounded stance settlement now have separate clips.
- The original Yokai Normal attack is 3.5667 seconds; its generic normalized midpoint occurs after the first descending claw strike. Source frame 27 is still behind the shoulder. Frame 35 is forward and descending at body height, confirmed by frame data and rendered poses.
- The original Yokai jump attack contains horizontal Hips travel. Its extracted corrected copy removes X/Z root curves while preserving vertical jump motion.
- Unity contact captures exposed a scale mismatch in the first Guard pose: its upward blade protected above the smaller Yokai's claws. The replacement hanging guard keeps the same raise/contact/recovery timing and both hands on the shaft, while the downward blade crosses the actual lower-body claw trajectory. No monster root lift or Normal timing change was used.

## Runtime contract

- `BeginDefense(dodge, secondsToContact)` starts only for an accepted successful reaction and aligns the authored guard/hop pose to the upcoming contact.
- `ResolveDefenseContact(dodge, hitStopSeconds)` continues the same take from that pose; it does not restart a raise/backstep after contact.
- Pending defence clamps at its contact pose; recovery completes before `DefenseRecoveryComplete` becomes true.
- Dodge displacement rises smoothly to .78 m from .08 to .24 seconds, holds through .38, then returns to zero at .8. It is a temporary model offset; the authoritative arena slot remains unchanged.
- The authored Dodge return has a left step from .40–.60 s and a right step from .58–.80 s. Specific baked leg targets compensate each planted foot for that model displacement; recovery does not drag two static feet back to the slot.
- Final Dodge return authoring targets the current authoritative Arena `SetStandingHeight(4.0f)`, yaw **65 degrees**, and lateral backstep **.78 m**. Fresh Unity PlayMode capture measures the actual rig world scale **3.6006691456**, which is used directly; Unity imported renderer bounds differ from Blender posed mesh height, so dividing the height argument by the posed mesh height would be incorrect. `runtime_rig_scale.json` preserves the captured scale and manifest hash for reproduction. Inverse yaw compensates both source X/Z axes; source validation measures planted-foot world drift below .00015 m. This is the current sword actor solution: Dodge return clips for other character sizes are not supported by this bake.
- Failed reactions do not enter either defence take.
- `SetLocomotionSpeed(worldUnitsPerSecond)` advances step cycles by measured travel distance, including acceleration and deceleration.
- Strides store the original source rig displacement per full cycle: Keiko approach **1.62086**, return **.90815**, entry **1.46354**; Yokai **.68552**. Multiplication by the actual rig world scale converts these values to world distance. Stage height changes and enemy slot root scale therefore change cadence together with the visible foot stride, without assuming a pose-derived stature ratio. Arena's separate world has identity scale, so its local distance/velocity values are world units.
- Horizontal Hips curves and animation audio events are removed from extracted new clips. The arena and explicit presentation event routing remain authoritative.
- `AwaitAttackContact(heavy)` clamps the active swing at its contact pose until the Core resolution watermark is satisfied. Recovery cannot finish during this wait. `HoldAttackAtContact` releases the clamp and preserves the complete authored recovery tail; resolution never rewinds a blade that has already passed contact.
- `CancelPendingAttackContact()` releases a cancelled suffix wait without changing the sampled pose or emitting contact/audio/hit-stop. It preserves the authored tail from the current sample, then settles normally. Authoritative action settlement must stop further sequence hits before return.

## Verification and reproducibility

`roundtrip_report.json` verifies all 11 exported takes retain the original named skeleton and bind matrices. Maximum bind matrix difference is below .000013; maximum exported Keiko palm error is below .0000015 source metres. These checks prove source binding/grip data, not final visual quality.

`AuthoringPreviews` contains 26 Blender renders. Guard contact in front/side/three-quarter views, Dodge apex and recovery steps, Normal/Heavy selection, entrance settle and descending claw poses were inspected. Actual Unity PlayMode evidence is recorded by the parent validation pass and remains necessary.

Editable `.blend` files remain at `D:/Rokas/reactiveturns-b2-staging/polish-ii-animation/`, named after each take. The background recipe and reports are preserved here. `author_polish_ii_animation.py` imports the previous bake utilities from `D:/Rokas/reactiveturns-b2-staging/cinematic/repair_sword_animations.py`; the same utility source is preserved as `base_sword_bake.py` in this folder. Rerunning the main recipe regenerates these 11 animation FBX sources only, and then the existing Unity actor library builder extracts runtime clips.
