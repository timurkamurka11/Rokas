# ROKAS Laptop Cinematic — V8 user-approved controls & blue LCD bounce

Scope: only LaptopCinematicSequence.cs, LaptopScreenBounceEstimator.cs,
LaptopCinematicLightStates.shader and 5 new photographic button textures.

## Approved OFF/ON layout (stage 1920 x 1080)
- OFF: ESC upper right x1624 y19 width268, E power-on centered x745 width430,
  optical baseline y1044.
- ON: ESC at exactly the same upper-right position. R stand x82 width292,
  E open YOMI x740 width440, Q shutdown x1446 width402; all bottom
  optical edges y1044.
- Source PNG images retain original colors, transparency and highlight details.
  Existing images remain fallback, not replaced.

## Laptop power LED
- Single calibrated physical keyboard power key; green when ON, blue when OFF.
- Original user blue/green atlas is drawn with 50px sharp LED + 64px
  soft overlay, centered at the same calibrated position.
- LEDs never both active, and don't turn on full-room lighting.

## Light on photographed desk
- No fake global grade and no additional full-screen color filter.
- Existing scene/corridor photographic masks unchanged. YOMI LCD sample
  drives blue colour, highlight bloom/specular, left-right positional bias.
- Boost blue/cyan luma on woodgrain; 0 light if screen OFF or room lamp ON.
- Keep filmic shoulder. Do not affect YOMI, hand animation, camera, Chibi,
  music, combat, or persistent power-state semantics.

## Test
- GitHub Actions imports and validates exact PNGs via SHA-256 and CRC.
- ROKAS Unity PlayMode screenshots should be verified by user after merge.
- Known unrelated combat compiler errors may block full CI.
