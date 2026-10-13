# Laptop Cinematic V8.1 — user screenshot-matched vertical right-side menu

Scope: `LaptopCinematicSequence.cs` and the dedicated PlayMode visual regression.
No changes to YOMI, screen light shader, room/hallway masks, camera, chibi,
hand animation or persistent power state. All five photographic gold PNGs V8
continue to be loaded unchanged with the same keyboard/mouse actions.

Source screenshots are 1672×940/941, represented on Unity 1920×1080 stage.

| Power | Button | Unity stage x | Unity stage y | Width |
|---|---|---:|---:|---:|
| OFF | E Power On | 1446 | 254 | 396 |
| OFF | ESC Back | 1490 | 385 | 350 |
| ON | R Stand | 1480 | 181 | 355 |
| ON | E Open | 1452 | 287 | 390 |
| ON | Q Shutdown | 1490 | 419 | 353 |
| ON | ESC Back | 1490 | 531 | 350 |

Vertical gold rule with outlined diamond terminals: ON center x1847 y122..677;
OFF center x1847 y207..486. Decorative, non-interactive, and visibility gated
to the matching seated state. The two ESC instances are separate UI elements
so hover logic cannot jump when power state changes.

Acceptance: OFF only E+ESC rail, ON only R+E+Q+ESC rail; keyboard E/Q/R/ESC
and physical power key continue to work without new runtime switches.
Full Unity validation requires compilation (pre-existing combat missing
CombatShotBounds and CombatShotBoundsProjection can block unrelated CI).
