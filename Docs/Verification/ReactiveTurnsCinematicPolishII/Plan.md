# Combat Cinematic Polish II

Authoritative folder requested for this pass: `D:/Rokas/Rokas-FULL-R11-FINISHED-UI COPY`.
Initial HEAD `361340d1efe498241fc28e5051dbf3bf6132b5ba`, TREE `ad3db4d1c5f9801e2bfb3c2acfce8b3c55e3f58d`, branch `r11-finished-ui`.

Initial local changes: 12 FBX metadata files, 96 trailing spaces introduced by Unity serialization. Every file was compared with HEAD after trimming whitespace; no semantic differences were found. Only whitespace was normalized. No user work was discarded.

## Implementation and verification sequence

1. Examine the two provided video references and supplied audio content; record missing exact reference names separately.
2. Retain Combat Core and strict return/idle gates. Extend presentation with separate selection, camera return, travel, swing/contact and recovery phases.
3. Author current-rig guard/backstep, separate entrance locomotion from stationary settling, repair loop/contact frames and cadence.
4. Gate battle BGM until exact arrival and settled battle idle.
5. Replace flat portal appearance with animated layered geometry, smoke and aperture/depth clipping; use one continuous emergence curve per enemy.
6. Dispatch vocal, acceleration and typed contact audio exactly once per logical event. Preserve existing vocals; suppress flesh contact on successful guard/miss.
7. Replace rectangular actor UI flashes with blade-following trails and actual contact effects. Keep every Dodge free of body impact/camera impulse.
8. Run builder, targeted Core/EditMode/PlayMode and sufficient combat integration tests. Capture real runtime entry, portal crossing, Normal/Heavy, enemy hit, Block/Dodge and long-run returns.
9. Compare asset-validator findings with this exact baseline; inspect Unity logs and literal `git diff --check`.
10. Review and commit verified in-scope changes and evidence. Report exact final checkpoint and real limitations. No BAT, clone, new prototype, destructive Git operation or push.

## Confirmed causes before editing

- `ReactiveHunterImpact` and `ReactiveEnemyImpact` were untextured UI rectangles whose alpha was raised for every hit/defense feedback. Their rectangular coverage produced the unwanted translucent artifact.
- EnterBattle reused a walking take after translation had already stopped, so feet continued walking at the fixed combat slot.
- Yokai default contact normalized value `.5` selected the wrong source pose; source inspection found claw contact at frame 35 and descending heavy contact around frame 31.
- Selection camera emphasis persisted across approach and attack, while selection poses were not held long enough to read.

Visual correctness is verified by runtime frames, not inferred from unit assertions or Blender preview alone.
