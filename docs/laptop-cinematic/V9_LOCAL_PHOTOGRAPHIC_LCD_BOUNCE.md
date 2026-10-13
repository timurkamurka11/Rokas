# ROKAS Laptop Cinematic — V9 balanced real-screen reflections

## Problem confirmed by 2026-10-13 Unity screenshots

The V8 light pass saturated the front tabletop with a cyan strip unrelated
to the physical laptop geometry. The estimator also forced a .62 minimum
blue tint and at least .43 intensity even for very dark LCD frames; shader
then multiplied the diffuse masks by 2.22 and bloom by 2.18.

## Fix

- Reuse the approved photograph pair, V5 wood/lip mask and V6 photographic
  specular/bloom masks; no new downloaded generic asset or dark filter.
- Real 48x27 YOMI sample determines screen colour, intensity, highlight
  energy and directional offset. Neutral screens remain neutral, dark
  frames remain dark.
- Elliptical attenuation in registered 1672x941 photo coordinates at
  (830,746), with 505x216 radii and a second fade after y=793. The
  front edge no longer receives a flat glow strip.
- Reduce optical gains, bias toward photographed wood/grain breakup,
  preserve separate hallway/room masks and exact off/on/power safety.
- Smooth colour changes over 0.23s to avoid UI icon flickering.

## RenderTexture warning

LaptopScreenBounceEstimator.Dispose now detaches its *own* staging
RenderTexture if currently active before releasing. Await pending readback
first. The screenshot only shows the UnityEngine.RenderTexture.Release
frame and is not a complete stack; if a warning remains, expand the full
Console stack to identify a different caller.

## Regression

EditMode tests validate black, blue and neutral YOMI colours; attenuated
front edge and distant pixels; existing room/corridor masks and state
switches remain unchanged. Full graphical validation should be done in
Unity for bright/dark YOMI and room ON/OFF, no new UI positions.
