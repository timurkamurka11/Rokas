# Home Weather 3.0 third-party/source manifest

This milestone uses a small set of CC0 source images plus project-owned spatial masks. The annotated `HOME_WEATHER_ZONES` and `HOME_LIGHTING_ZONES` design references are not runtime assets and are not redistributed in `Assets/`.

## Selected CC0 sources

| Source material | Creator | License | Acquisition URL | Deterministic use | Committed derivative(s) |
| --- | --- | --- | --- | --- | --- |
| `rain_overlay.png` (`rain`) | gmason | CC0 | `https://opengameart.org/sites/default/files/rain_overlay_0.png` | Connected-component extraction of a natural authored streak; alpha dilation/normalization; compact 32x96 RGBA resize. The same source is independently sampled into sparse glass streak components with different selection, placement and transform logic. | `RainStreakAuthored.png`, `WetGlassStreaks.png` |
| `fog01.png` (`Thick Fog`) | LFA | CC0 | `https://opengameart.org/sites/default/files/fog01.png` | Resize to 512x256, retain source alpha, apply transparent edge falloff and reduced opacity for distant exterior haze. | `DistantHaze.png` |
| `fx_cloudalpha05.png` (`Clouds with Transparency`) | WickedInsignia | CC0 | `https://opengameart.org/sites/default/files/oga-textures/136561/fx_cloudalpha05.png` | Crop, resize, alpha strengthen/blur and independent four-edge falloff for near exterior mist. | `NearMist.png` |
| `fx_cloudalpha02.png` (`Clouds with Transparency`) | WickedInsignia | CC0 | `https://opengameart.org/sites/default/files/oga-textures/136561/fx_cloudalpha02.png` | Independent crop, resize, alpha strengthen/blur and edge falloff for irregular exterior storm illumination. | `StormCloudMask.png` |
| `RaindropsOnWindow.jpg` | Wikimedia Commons contributor/source file | CC0 | `https://commons.wikimedia.org/wiki/Special:Redirect/file/RaindropsOnWindow.jpg` | Background is discarded. Local contrast, high-pass and gradient structure are filtered into a transparent compact droplet-detail alpha resource. | `WetGlassDroplets.png` |

## Project-owned authored spatial resources

`ApartmentNight.png` is existing ROKAS Home source art. It is used as an alignment invariant (1672x941) while the approved Home spatial design is encoded into compact masks. No pixels from the user annotation graphics are imported into runtime resources.

- `WindowGlazingMask.png` — 605x396 RGBA alpha mask for the actual three glazing panels; mullion gaps are transparent.
- `WarmPracticalMask.png` — 960x540 localized warm-practical regions for the floor lamp, workbench/hallway-side practical illumination and other authored warm sources.
- `ColdSpillMask.png` — 960x540 feathered irregular cold exterior/window spill.
- `StormRoomLiftMask.png` — 960x540 weak irregular room-side storm response.

## Deterministic authoring

The source-of-truth authoring implementation is `Tools/CI/author_weather3_assets.py` on `ci/home-weather-3`. It validates the expected Home art size, emits the exact compact filenames above, encodes visible structure in RGBA alpha, and writes stable Unity `.meta` GUIDs derived from deterministic resource paths. CI validates transparent outer edges, non-empty alpha, exact output names, presence of `.meta` files, and opaque/transparent sample points across all three glazing panels and mullion gaps.

## Unity import/runtime compatibility

Final PNGs are ordinary non-readable `Texture2D` resources with mipmaps disabled, bilinear filtering, alpha transparency enabled and max texture size 1024. They are consumed by the existing Built-in Render Pipeline-compatible Unity UI / `ParticleSystem` / `RenderTexture` Home architecture. No URP migration, Shader Graph, VFX Graph or third-party runtime weather framework is required.

## Redistribution

The selected external source material is CC0, so the compact transformed derivatives may be redistributed with the project. Only the compact derivatives are committed to the production branch; original high-resolution/source downloads remain CI acquisition artifacts. Existing project-owned Home art and generated spatial masks remain under the project's own repository terms.
