# ReactiveTurns final VFX, preview and Throw pass

Source: user master prompt 2026-09-30. Initial feature HEAD
`0eecaaf7ab62afbf5c949430f08cd1b175c9edc4`, branch `r11-finished-ui`.

## Implementation sequence

1. Keep command preview outside Combat Core. UI first click selects a held actor pose and camera framing; same action confirms after the camera restore lifecycle. Switching replaces the pose, Escape/right click cancels. Direct Core command API remains available to domain callers.
2. Add a distinct `throw_blade` skill and preserve exact legacy catalog/checkpoint compatibility. Author a dagger and Throw clips on the existing Keiko rig. Release once, fly continuously, resolve contact once through the existing Core event, then recover at the authoritative slot.
3. Obtain genuine EmberGen exports if the installed executable supports available automation. Record version, preset, export settings and technical failures. Never label generated Unity/Python textures as EmberGen exports.
4. Integrate layered portal and impact presentation. Keep existing emergence masking, sequential readiness and return gating. Differentiate melee trails, heavy smoke, projectile flight and blade-contact block sparks.
5. Run focused UI/throw/audio/entrance regressions and real rendered PlayMode capture sequences. Inspect reference portal/throw videos and captures; unit tests do not prove visual quality.
6. Review and checkpoint the verified feature. Recover exact refs, integrate the explicitly verified feature into `integration/rokas-unified` with history preserved. Run complete Core, EditMode and PlayMode suites and compare asset validator findings against the actual current integration baseline.
7. Publish only a green verified integration checkpoint and safely update the canonical `D:/Rokas/Rokas` folder after preserving local changes and checking collisions/editor state. No main/development push, BAT, review clone or cleanup.

## Interfaces and verification

- Bootstrap owns preview selection/confirmation intent; Arena owns camera/pose restoration completion. Neither preview nor cancel calls `SubmitCommand`.
- Arena settlement remains strict for turn transitions; command readiness separately permits a held preview.
- Throw release and projectile flight are presentation events. Core remains authoritative for AP, damage, exactly-once contact and durable checkpoints.
- Existing Normal/Heavy source clips, target-save retry, enemy return, audio once guards and unrelated Home/Messages responsibilities remain covered by regressions.
- RED evidence: `final-vfx-unity/red-preview-results.xml`, first click currently enters PlayerExecution. Implement only after this observed failure.
- Final report records actual test counts, reviewed captures, validator delta, exact HEAD/TREE and any remaining technical blocker. A failed mandatory gate is not DONE.
