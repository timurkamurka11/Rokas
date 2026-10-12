# ROKAS V7 — Physical Laptop Power Off & Reactive Screen Lighting

## Implemented
- Keep the exact approved room/hallway photographed ON/OFF state masks, real YOMI LCD, V4 calibration, POV and scene.
- Increase photographic wood-only screen diffuse, content-driven bloom and specular via the existing YOMI GPU sampler, while retaining a filmlike shoulder and room-ON/laptop-OFF zero gating.
- Power OFF from the GREEN calibrated physical key, a fourth gold hover action to the right of ESC, or Q key. E still opens YOMI; R still stands up.
- Single aligned bottom action row with R / E / ESC / Q while powered. The optional **exact user artwork** `Assets/Rokas/Resources/LaptopCinematic/UI/prompt_shutdown.png` is preferred if installed. A self-contained gold uGUI button fallback works from Git with no additional install.
- Green physical 6px LED while powered, blue only in standby. Existing right-hand frames reach identical calibrated power key, contact sound fires once, power toggles OFF at contact, the hand reverses to idle, old jumping chibi/no-signal resumes. Remain seated; do not open YOMI on shutdown.
- `LaptopPowerSession.Shutdown()` is idempotent and only changes runtime power, retaining session boot count. New boot uses fresh `LaptopPowerTimeline`.

## Key QA
1. Off lamp + powered screen: stronger glow on wood; opening brighter YOMI pages responds smoothly.
2. Lit room OR powered OFF: extra reflected LCD light equals zero.
3. Boot first time: original hand/contact/boot sequence unchanged.
4. Powered state: gold button on far right of ESC, green physical LED.
5. Click fourth button/Q/physical green key: reverse hand + click, chibi appears, blue standby LED, no YOMI opening.
6. Press E/blue key to boot again; repeat shutdown/boot cycle; verify no disposed or stale state.
7. Stand with R and return; no wallpaper state reset.
8. Verify native gold fallback if user artwork not installed; if available, exact user PNG replaces fallback.
9. Tests and CI; full Unity visual approval required.

## CI isolation
Unrelated legacy CombatShotBounds / CombatShotBoundsProjection compile failures can block test runs. Do not alter combat as part of this UI/light feature.
