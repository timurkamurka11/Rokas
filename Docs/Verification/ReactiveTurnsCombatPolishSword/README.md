# ReactiveTurns Combat Polish + Keiko sword — R11

Authoritative project: `D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY`.
Unity: **6000.3.19f1**. Branch: **r11-finished-ui**.
Initial HEAD: `65141068f3317410e117d5a8bd08c55d4faacab0`.
Initial TREE: `3cde7d9afa97133f58f72d578073391f46f14419`.
Verified implementation checkpoint: `0c0f6e938f182113caab732294f447e07194cc6d`.
Implementation TREE: `c475dd1f48ee78d2223b4eb934a490978f103dd7`.
The following evidence-only checkpoint preserves this exact implementation.

This continues the existing nine-stage polish implementation. The latest continuation explicitly selects this R11 folder and focused Combat validation; it does not use a BAT, review clone, or another Unity project. Existing Home/VN tooling and the separate dirty `D:/Rokas/Rokas` folder were preserved.

## Archive, assets and rig

The archive `D:/3DMODELS/Keiko sword attack/Keiko sword attack.rar` contains exactly four source files. Every imported source matches its extracted SHA256; see [keiko-sword-import-map.json](keiko-sword-import-map.json).

| Extracted source | Imported asset |
| --- | --- |
| Normal attack.fbx | Assets/Rokas/Art/CombatActors/Keiko/Normal attack.fbx |
| Hard jump attack.fbx | Assets/Rokas/Art/CombatActors/Keiko/Hard jump attack.fbx |
| metal+sword+3d+model.fbx | Assets/Rokas/Art/CombatActors/Weapons/KeikoSword/metal+sword+3d+model.fbx |
| metal+sword+3d+model.fbm/metal+sword+3d+model_basecolor.jpg | Assets/Rokas/Art/CombatActors/Weapons/KeikoSword/metal+sword+3d+model_basecolor.jpg |

Reusable weapon prefab: `Assets/Rokas/Art/CombatActors/Weapons/KeikoSword/KeikoSword.prefab`.
Material: `Assets/Rokas/Art/CombatActors/Weapons/KeikoSword/KeikoSword.mat` (Standard, supplied 4096×4096 BaseColor, metallic .28, glossiness .32).
The existing `Keiko/Keiko@Idle.fbx` character is still the model. No second character mesh is spawned from either new animation source.

The exact right-hand path is:

```text
mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder/mixamorig:RightArm/mixamorig:RightForeArm/mixamorig:RightHand
```

`WeaponSocket_R` is created/reused directly below that hand. Relative to the hand, its local position is **(0, .033, .006)**, Euler rotation **(0, 180, 0)**, scale **(1, 1, 1)**. Relative to the socket, the equipped prefab root is position **(0, 0, 0)**, rotation **identity**, scale **(1, 1, 1)**.

The prefab's mesh wrapper compensates for the source pivot without modifying the mesh: grip point `(-.34207, .90139, .00035)`, rotation `FromToRotation((.75112, -.8907, -.00035).normalized, Vector3.forward)`, uniform scale **.55**, offset `-(rotation * grip) * .55`. A small fixed finger-pose adjustment on idle/run restores the supplied hand sample before each application, avoiding accumulated rotations. Authored attack finger poses remain intact.

`ReactiveCombatWeaponAttachment.Equip(prefab)` can replace/remove a weapon and is idempotent for the current prefab. Equip/unequip and repeated attacks are covered by PlayMode tests.

Both supplied animation sources contain compatible 33-bone rigs and matching bind data. The existing **Legacy** path-bound pipeline is reused; no Humanoid conversion or new Avatar is introduced. EditMode validates every extracted curve path against the existing character.

| Command / runtime alias | Extracted clip | Import |
| --- | --- | --- |
| Normal / Attack | Clips/Keiko_Normal_attack.anim | Frames 0–38 at30fps, 1.2667s; Legacy, no compression, loop off |
| Heavy / Heavy | Clips/Keiko_Hard_jump_attack.anim | Frames 0–57 at30fps, 1.9s; Legacy, no compression, loop off |

Both clips have no AnimationEvents. Hips local X/Z translation is removed only in extracted runtime clips. Bone-local jump Y is retained; the arena controller owns world travel, ensuring no doubled movement. Source FBX bytes are unchanged. Sword mesh imports normals, Mikk tangents, no animations/material extraction, non-readable mesh.

## Combat presentation

1. **One SFX:** offensive command commitment and its buttons no longer emit swing/UI click cues. The first damaging player `HitResolved` per action emits `Keiko hit attack` once, including multi-target actions. Volume remains `.56 × master × SFX`; settings changes are tested. Core remains authoritative for damage and defense.
2. **Normal/Heavy contact:** attack anticipation plays after arrival; Core resumes for its existing contact schedule. At the authoritative `HitResolved`, the visual is aligned to Normal frame20/38 or Heavy31/57 and held for .08s. Heavy's marker was calibrated from live target-contact renders, moving it from the late follow-through to the downward target-facing stroke. SFX, damage text and target reaction consume that same event.
3. **Keiko travel:** approach .84s, return .72s, near-linear cruise with short eased ends; exact home is restored. Five Normal and five Heavy alternating attacks verify no position/rotation drift or duplicate weapon. A new command selected while its target is returning aims at the target's permanent slot; both controlled and live capture tests cover this.
4. **Enemy travel:** approach .65s, return .68s, only the acting enemy moves. Authored multi-hit impact times drive separate strikes while the attacker remains at contact range. Other slots remain stable.
5. **Reaction UI:** incoming enemy attacks alone show the cursor bar. Nested Dodge/Block/Perfect zones derive from the actual DefenseWindow and each hit's response mask. Device timestamps freeze the exact submitted cursor; the result holds .30s, hides, then reopens for the next hit. Heavy has its own authored offensive window, independent of incoming defense.
6. **Actions/readability:** only Normal and Heavy are permanent attack buttons. Other existing combat systems remain inactive in this action UI. Q/E are contextual defense controls. Feedback is compact and local; global impact is reduced.
7. **Hit feedback:** visual-only .08s hit stop, local recoil, damage number, .30s HP-bar tween, selected-target border and .26s result linger. Pause/focus/frame-gap/save blocking freezes visual time without stopping the intentional approach hold.
8. **Death:** Core removes dead actors from targeting immediately. Presentation falls1.35s, holds1.7s, sinks vertically1.2s at unchanged scale, then destroys the corpse. Final victory/defeat preserves the arena4.5s before the existing result screen; terminal state/payment are already saved, and repeated refresh does not restart the delay.

## Verification evidence

Final exact counts and Unity execution timestamps are in [results.json](results.json), [edit-results.xml](edit-results.xml) and [playfocus-results.xml](playfocus-results.xml).

- Fresh current-source Core compile/run: **PASS**, exit0, all `Rokas.Core` behavior tests (including ReactiveTurns, save/progression/payment regressions). The installed .NET SDK compiler fails under Windows CET; the current source was compiled with Unity's bundled Roslyn/runtime and installed .NET reference assemblies. No cached test executable was used. See [polish-core.log](polish-core.log).
- Focused EditMode: **6/6 PASS**: compatible Normal/Heavy curve bindings, prefab dependencies/missing-script checks, active Reactive checkpoint roundtrip, accepted contract/pending payment and valid-backup protection.
- Focused PlayMode: **21/21 PASS**. The final result includes original input, duel, Heavy, waves/victory, eight-enemy, full saved combat loop and resolution tests, plus sword, audio, reaction, repeated motion and live rendered capture tests. Final execution: 2026-09-29 22:35:12–22:37:11 UTC, Unity exit0.
- Unity compiler/runtime exception scan: no C# compilation error or gameplay exception in the final focused logs.
- Four source hashes match; newly imported/generated metadata is unique and references resolve in Unity. Builder stays in the Editor assembly; no Editor dependency was added to runtime assemblies.
- Static validator: **132 baseline →132 final;0 added,0 removed**, exit1 for existing findings. Comparison uses initial tracked HEAD content plus four preexisting ignored HubDialogue user assets, with their hashes/timestamps recorded. The older historical13 count does not describe this R11 tree. See [polish-validator-comparison.json](polish-validator-comparison.json). The validator was not edited.
- Literal `git diff --check` and staged `git diff --cached --check`: **PASS**, exit0 and empty output before the implementation checkpoint. Only generated YAML trailing whitespace was removed to meet this gate; raw execution logs remain in staging, and versioned log copies omit terminal trailing whitespace. Final status is checked again after the evidence checkpoint.

### Rendered PlayMode review

The actual Unity PlayMode scenario uses the existing Bootstrap, battle HUD, Core events, pointer actions and device Q/E input. Rendered frames are inspected by the agent; this is not a human-operated mouse/keyboard session or a standalone player build.

Visual evidence includes idle from front/side, close hand grip, both authored animation frame sheets, approach, Normal/Heavy contact against the selected enemies, return, moving reaction cursor, resolved Dodge/Block, fallen corpse, stationary corpse hold and vertical sink. The live capture manifest records phase, Core time/action, committed and selected targets, health and hunter pose/position. It verifies Heavy selected during E2's return reaches E2's stable slot.

The images are retained under `D:/Rokas/reactiveturns-b2-staging/polish-visuals`, including copied front/side/grip and resolution images. `capture-manifest.json` accompanies the ten live battle captures. Full Unity logs and FBX analysis are also retained in that evidence directory's parent.

## Scope and limitations

All implementation/assets are in the requested R11 folder. No BAT or alternate project was created. This is a focused Combat polish checkpoint, not a claim that the unrelated full-project suites are green: their earlier baseline had four VN EditMode and59 unrelated Home/Messages/VN PlayMode failures. Those systems were not modified to clear their failures. The132 preexisting validator findings remain.

Final HEAD/TREE and clean Git status are recorded in the final chat handoff and the external final-checkpoint evidence (outside the commit to avoid a circular self-reference).
