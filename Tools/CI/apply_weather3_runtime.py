from __future__ import annotations

import re
import sys
from pathlib import Path

if len(sys.argv) != 2:
    raise SystemExit("usage: apply_weather3_runtime.py <WorldEffects.cs>")

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
original = text

new_fields = '''        private readonly Image portalLight;
        private readonly RawImage stormFlash;
        private readonly RawImage weatherComposite;
        private readonly RawImage glazingMaskGraphic;
        private readonly Mask glazingMask;
        private readonly RawImage wetGlass;
        private readonly RawImage wetGlassDroplets;
        private readonly RawImage wetGlassStreaks;
        private readonly RawImage exteriorHaze;
        private readonly RawImage exteriorMistNear;
        private readonly RawImage outsideParallax;
        private readonly RawImage foregroundDepth;
        private readonly RawImage coldWindowBounce;
        private readonly RawImage warmInteriorGlow;
        private readonly RawImage stormRoomLift;
        private readonly RectTransform outsideDepthMask;
        private readonly RectTransform weatherCanvasRoot;
        private readonly RectTransform steamRoot;
        private readonly RectTransform[] steam = new RectTransform[7];
        private readonly Image[] tears = new Image[3];
        private readonly GameObject weatherRuntime;
        private readonly Camera weatherCamera;
        private readonly RenderTexture weatherTexture;
        private readonly ParticleSystem[] rainLayers = new ParticleSystem[3];
        private readonly float[] baseEmission = { 22f, 22f, 20f };
        private readonly Material rainMaterial;
        private readonly Texture2D rainTexture;
        private readonly Texture2D wetGlassDropletsTexture;
        private readonly Texture2D wetGlassStreaksTexture;
        private readonly Texture2D distantHazeTexture;
        private readonly Texture2D nearMistTexture;
        private readonly Texture2D stormCloudTexture;
        private readonly Texture2D glazingTexture;
        private readonly Texture2D coldSpillTexture;
        private readonly Texture2D warmPracticalTexture;
        private readonly Texture2D stormRoomLiftTexture;
'''

text, count = re.subn(
    r'        private readonly Image darkness;\n.*?        private readonly Texture2D radialTexture;\n',
    new_fields,
    text,
    count=1,
    flags=re.S,
)
assert count == 1, "field block did not match exactly once"

new_constructor = '''            portalLight = ui.Box(parent, "PortalBreath", 0, 100, 1920, 906, Color.clear);

            rainTexture = RequireWeatherTexture("RainStreakAuthored");
            wetGlassDropletsTexture = RequireWeatherTexture("WetGlassDroplets");
            wetGlassStreaksTexture = RequireWeatherTexture("WetGlassStreaks");
            distantHazeTexture = RequireWeatherTexture("DistantHaze");
            nearMistTexture = RequireWeatherTexture("NearMist");
            stormCloudTexture = RequireWeatherTexture("StormCloudMask");
            glazingTexture = RequireWeatherTexture("WindowGlazingMask");
            coldSpillTexture = RequireWeatherTexture("ColdSpillMask");
            warmPracticalTexture = RequireWeatherTexture("WarmPracticalMask");
            stormRoomLiftTexture = RequireWeatherTexture("StormRoomLiftMask");
            wetGlassDropletsTexture.wrapMode = TextureWrapMode.Repeat;
            wetGlassStreaksTexture.wrapMode = TextureWrapMode.Repeat;

            weatherCanvasRoot = ui.Rect(parent, "WindowRain", 350, 107, 605, 396);
            weatherCanvasRoot.gameObject.AddComponent<RectMask2D>();
            glazingMaskGraphic = ui.Art(weatherCanvasRoot, "HomeGlazingMask", glazingTexture, 0, 0, 605, 396);
            glazingMaskGraphic.color = Color.white;
            glazingMask = glazingMaskGraphic.gameObject.AddComponent<Mask>();
            glazingMask.showMaskGraphic = false;

            RectTransform glazingRoot = glazingMaskGraphic.rectTransform;
            exteriorHaze = ui.Art(glazingRoot, "HomeExteriorHaze", distantHazeTexture, -36, 238, 680, 190);
            exteriorHaze.color = Color.clear;
            exteriorMistNear = ui.Art(glazingRoot, "HomeExteriorMistNear", nearMistTexture, -82, 176, 760, 248);
            exteriorMistNear.color = Color.clear;

            weatherTexture = new RenderTexture(768, 512, 0, RenderTextureFormat.ARGB32)
            {
                name = "HomeWeatherRenderTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            weatherTexture.Create();
            weatherComposite = ui.Art(glazingRoot, "HomeWeatherComposite", weatherTexture, 0, 0, 605, 396);
            weatherComposite.color = new Color(.86f, .95f, 1f, .84f);

            wetGlassDroplets = ui.Art(glazingRoot, "HomeWetGlassDroplets", wetGlassDropletsTexture, 0, 0, 605, 396);
            wetGlassDroplets.color = Color.clear;
            wetGlassStreaks = ui.Art(glazingRoot, "HomeWetGlassStreaks", wetGlassStreaksTexture, -5, -4, 615, 404);
            wetGlassStreaks.uvRect = new Rect(.17f, .31f, 1.28f, 1.28f);
            wetGlassStreaks.color = Color.clear;

            // Compatibility anchor retained for the established Home 2.5D regression contract.
            // It is transparent; the visible droplet treatment is HomeWetGlassDroplets above.
            wetGlass = ui.Art(glazingRoot, "HomeWetGlass", wetGlassDropletsTexture, 0, 0, 605, 396);
            wetGlass.color = Color.clear;

            stormFlash = ui.Art(glazingRoot, "HomeStormFlash", stormCloudTexture, -78, -62, 760, 520);
            stormFlash.color = Color.clear;

            foregroundDepth = ui.Art(parent, "HomeForegroundDepth", homeSource, 0, 0, 1920, 1080);
            foregroundDepth.color = new Color(1f, 1f, 1f, 0f);
            CreateArtSlice(ui, foregroundDepth.transform, "FrameTop", homeSource, 326, 83, 653, 30);
            CreateArtSlice(ui, foregroundDepth.transform, "FrameLeft", homeSource, 326, 113, 34, 390);
            CreateArtSlice(ui, foregroundDepth.transform, "FrameRight", homeSource, 945, 113, 34, 390);
            CreateArtSlice(ui, foregroundDepth.transform, "FrameBottom", homeSource, 326, 503, 653, 32);

            coldWindowBounce = ui.Art(parent, "HomeColdWindowBounce", coldSpillTexture, 0, 0, 1920, 1080);
            coldWindowBounce.color = Color.clear;
            warmInteriorGlow = ui.Art(parent, "HomeWarmInteriorGlow", warmPracticalTexture, 0, 0, 1920, 1080);
            warmInteriorGlow.color = Color.clear;
            stormRoomLift = ui.Art(parent, "HomeStormRoomLift", stormRoomLiftTexture, 0, 0, 1920, 1080);
            stormRoomLift.color = Color.clear;

            weatherRuntime = new GameObject("HomeWeatherRuntime");
            weatherRuntime.transform.SetParent(runtimeParent, false);
            weatherCamera = new GameObject("HomeWeatherCamera").AddComponent<Camera>();
            weatherCamera.transform.SetParent(weatherRuntime.transform, false);
            weatherCamera.transform.localPosition = new Vector3(0, 0, -10f);
            weatherCamera.orthographic = true;
            weatherCamera.orthographicSize = 5f;
            weatherCamera.aspect = 605f / 396f;
            weatherCamera.clearFlags = CameraClearFlags.SolidColor;
            weatherCamera.backgroundColor = Color.clear;
            weatherCamera.cullingMask = 1 << WeatherLayer;
            weatherCamera.allowHDR = false;
            weatherCamera.allowMSAA = false;
            weatherCamera.depth = -100f;
            weatherCamera.targetTexture = weatherTexture;

'''

text, count = re.subn(
    r'            darkness = ui\.Box\(parent, "LampMood".*?            rainTexture = CreateRainTexture\(\);\n',
    new_constructor,
    text,
    count=1,
    flags=re.S,
)
assert count == 1, "constructor visual block did not match exactly once"

new_location = '''            weatherCanvasRoot.gameObject.SetActive(weatherVisible);
            weatherRuntime.SetActive(weatherVisible);
            weatherCanvasRoot.anchoredPosition = home ? new Vector2(350, -107) : new Vector2(0, -100);
            weatherCanvasRoot.sizeDelta = home ? new Vector2(605, 396) : new Vector2(1920, 906);
            glazingMaskGraphic.rectTransform.sizeDelta = weatherCanvasRoot.sizeDelta;
            glazingMaskGraphic.rectTransform.anchoredPosition = Vector2.zero;
            glazingMask.enabled = home;
            glazingMaskGraphic.enabled = home;
            weatherComposite.rectTransform.sizeDelta = weatherCanvasRoot.sizeDelta;
            weatherComposite.rectTransform.anchoredPosition = Vector2.zero;
            stormFlash.rectTransform.sizeDelta = home ? new Vector2(760, 520) : weatherCanvasRoot.sizeDelta;
            stormFlash.rectTransform.anchoredPosition = home ? new Vector2(-78, 62) : Vector2.zero;
            weatherCamera.aspect = home ? 605f / 396f : 1920f / 906f;
            ResizeEmitterWidth();
            if (weatherVisible)
            {
                for (int i = 0; i < rainLayers.Length; i++)
                    if (rainLayers[i] != null && !rainLayers[i].isPlaying) rainLayers[i].Play(true);
            }

            bool homeDetails = home && atmosphere.enabled;
            outsideDepthMask.gameObject.SetActive(homeDetails);
            foregroundDepth.gameObject.SetActive(homeDetails);
            wetGlass.gameObject.SetActive(homeDetails);
            wetGlassDroplets.gameObject.SetActive(homeDetails);
            wetGlassStreaks.gameObject.SetActive(homeDetails);
            exteriorHaze.gameObject.SetActive(homeDetails);
            exteriorMistNear.gameObject.SetActive(homeDetails);
            stormFlash.gameObject.SetActive(homeDetails);
            coldWindowBounce.gameObject.SetActive(homeDetails);
            warmInteriorGlow.gameObject.SetActive(homeDetails);
            stormRoomLift.gameObject.SetActive(homeDetails);
'''

text, count = re.subn(
    r'            weatherCanvasRoot\.gameObject\.SetActive\(weatherVisible\);\n.*?            stormRoomLift\.gameObject\.SetActive\(homeDetails\);\n',
    new_location,
    text,
    count=1,
    flags=re.S,
)
assert count == 1, "SetLocation weather block did not match exactly once"

text = text.replace('            if (rainTexture) Object.Destroy(rainTexture);\n', '')
text = text.replace('            if (wetGlassTexture) Object.Destroy(wetGlassTexture);\n', '')
text = text.replace('            if (radialTexture) Object.Destroy(radialTexture);\n', '')

new_lighting = '''        private void ApplyLighting(bool lampOn)
        {
            float flash = LightningEnvelope(lightningAge) * Mathf.Clamp(atmosphere.lightningIntensity, 0f, 1.5f);
            float slowPulse = .5f + .5f * Mathf.Sin(time * .29f);

            if (!atHome)
            {
                stormFlash.color = Color.clear;
                wetGlass.color = Color.clear;
                wetGlassDroplets.color = Color.clear;
                wetGlassStreaks.color = Color.clear;
                exteriorHaze.color = Color.clear;
                exteriorMistNear.color = Color.clear;
                coldWindowBounce.color = Color.clear;
                warmInteriorGlow.color = Color.clear;
                stormRoomLift.color = Color.clear;
                return;
            }

            float readability = Mathf.Clamp(atmosphere.weatherReadability, .5f, 1.5f);
            stormFlash.color = new Color(.96f, .985f, 1f, Mathf.Clamp01(flash * .62f));
            outsideParallax.color = new Color(
                Mathf.Lerp(.94f, 1f, Mathf.Clamp01(flash)),
                Mathf.Lerp(.975f, 1f, Mathf.Clamp01(flash)),
                1f, 1f);

            float haze = Mathf.Clamp(atmosphere.hazeIntensity, 0f, 1.5f);
            float exteriorMotion = Mathf.Clamp(atmosphere.outsideMotionIntensity, 0f, 1.5f);
            exteriorHaze.color = new Color(.82f, .93f, .97f,
                Mathf.Clamp01((.105f + slowPulse * .025f) * haze * readability + flash * .13f));
            exteriorHaze.rectTransform.anchoredPosition = new Vector2(
                -36f + Mathf.Sin(time * .13f) * 12f * exteriorMotion,
                -238f + Mathf.Cos(time * .091f) * 4.5f * exteriorMotion);

            exteriorMistNear.color = new Color(.88f, .96f, 1f,
                Mathf.Clamp01((.070f + (.5f + .5f * Mathf.Sin(time * .21f)) * .026f) *
                    haze * readability + flash * .17f));
            exteriorMistNear.rectTransform.anchoredPosition = new Vector2(
                -82f + Mathf.Sin(time * .073f + 1.4f) * 15f * exteriorMotion,
                -176f + Mathf.Cos(time * .057f) * 5f * exteriorMotion);

            float wet = Mathf.Clamp(atmosphere.wetGlassIntensity, 0f, 1.5f);
            Rect dropletUv = new Rect(
                Mathf.Repeat(time * .0035f, 1f),
                Mathf.Repeat(time * .018f, 1f), 1f, 1f);
            Vector2 dropletOffset = new Vector2(
                Mathf.Sin(time * .12f) * 1.2f,
                Mathf.Cos(time * .087f) * .8f);
            wetGlass.uvRect = dropletUv;
            wetGlass.rectTransform.anchoredPosition = dropletOffset;
            wetGlass.color = Color.clear;
            wetGlassDroplets.uvRect = dropletUv;
            wetGlassDroplets.rectTransform.anchoredPosition = dropletOffset;
            wetGlassDroplets.color = new Color(.92f, .98f, 1f,
                Mathf.Clamp01(.25f * wet * readability + flash * .18f));

            wetGlassStreaks.uvRect = new Rect(
                .17f + Mathf.Repeat(time * .0021f, 1f),
                .31f + Mathf.Repeat(time * .0105f, 1f), 1.28f, 1.28f);
            wetGlassStreaks.rectTransform.anchoredPosition = new Vector2(
                -5f + Mathf.Sin(time * .081f + .7f) * 1.5f,
                4f + Mathf.Cos(time * .061f) * 1.1f);
            wetGlassStreaks.color = new Color(.94f, .985f, 1f,
                Mathf.Clamp01(.14f * wet * readability + flash * .20f));

            float glow = Mathf.Clamp(atmosphere.glowIntensity, 0f, 1.5f);
            coldWindowBounce.color = new Color(.88f, .96f, 1f,
                Mathf.Clamp01((.042f + slowPulse * .018f) * glow + flash * .30f));
            warmInteriorGlow.color = new Color(1f, .94f, .84f,
                Mathf.Clamp01((lampOn ? .115f : .008f) * glow * (.91f + slowPulse * .09f) + flash * .012f));
            stormRoomLift.color = new Color(.90f, .97f, 1f,
                Mathf.Clamp01(flash * .10f));
        }
'''

text, count = re.subn(
    r'        private void ApplyLighting\(bool lampOn\)\n        \{.*?\n        \}\n\n        private static float LightningEnvelope',
    new_lighting + '\n        private static float LightningEnvelope',
    text,
    count=1,
    flags=re.S,
)
assert count == 1, "ApplyLighting did not match exactly once"

start = text.index('        private static Texture2D CreateRainTexture()')
end = text.rindex('    }\n}')
helper = '''        private static Texture2D RequireWeatherTexture(string name)
        {
            Texture2D texture = Resources.Load<Texture2D>("Weather3/" + name);
            if (!texture)
                throw new System.InvalidOperationException("Missing required Home Weather 3 texture: " + name);
            return texture;
        }
'''
text = text[:start] + helper + text[end:]

assert text != original, "runtime patch produced no change"
assert 'LampMood' not in text, "legacy LampMood must be removed"
assert 'CreateRainTexture' not in text
assert 'CreateWetGlassTexture' not in text
assert 'CreateRadialTexture' not in text
assert 'HomeAtmosphereSoftGlow' not in text
assert 'HomeWetGlassTexture' not in text
assert 'HomeRainStreakTexture' not in text
for required in [
    'HomeWetGlassDroplets',
    'WindowGlazingMask',
    'RainStreakAuthored',
    'DistantHaze',
    'NearMist',
    'StormCloudMask',
    'ColdSpillMask',
    'WarmPracticalMask',
    'StormRoomLiftMask',
]:
    assert required in text, required

path.write_text(text, encoding="utf-8")
print(f"patched {path}")
