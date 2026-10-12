# ROKAS — V7.2 supplied Q / LEDs

- The left back arrow is removed by the current LaptopCinematicSequence, while ESC and R remain.
- The PNG `power_control_atlas.png` is derived from the user-supplied Q button, green ON and blue OFF light images. 470x224 with top Q 470x152; bottom green 64x64 at x0, and blue 64x64 at x72. Alpha transparency preserved.
- No overlapping gold actions: R x82 w402, E x482 w515, ESC x1007 w410, Q x1430 w450; optical lower edge aligned with R.
- Power LED is drawn in the exact same calibrated physical spot from source image. Green powered, blue standby.
- Photo ON/OFF lamp masks are unchanged. Luminance-driven YOMI bloom/specular is amplified only in dark room with physical laptop ON. Smoothing 0.16s / GPU sample 0.12s for responsive screen updates. No arbitrary flickering.
- Visual Unity screenshot validation remains necessary, old GitHub combat compilation may block unrelated CI.
