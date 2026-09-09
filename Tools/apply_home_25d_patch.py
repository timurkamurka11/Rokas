from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
world_path = ROOT / "Assets/Rokas/Scripts/Presentation/WorldEffects.cs"
profile_path = ROOT / "Assets/Rokas/Scripts/Presentation/HomeAtmosphereProfile.cs"
asset_path = ROOT / "Assets/Rokas/Resources/HomeAtmosphereProfile.asset"

world = world_path.read_text(encoding="utf-8")
profile = profile_path.read_text(encoding="utf-8")
asset = asset_path.read_text(encoding="utf-8")

if "HomeOutsideParallax" in world:
    raise SystemExit("Home 2.5D production patch already applied")
if "parallaxIntensity" in profile:
    raise SystemExit("Home 2.5D profile patch already applied")

def replace_once(text, old, new, label):
    if old not in text:
        raise SystemExit("missing patch anchor: " + label)
    return text.replace(old, new, 1)

world = replace_once(
    world,
    '''        private readonly Image stormFlash;
        private readonly RawImage weatherComposite;
        private readonly RawImage wetGlass;
        private readonly RawImage exteriorHaze;
        private readonly RawImage coldWindowBounce;
        private readonly RawImage warmInteriorGlow;
        private readonly RectTransform weatherCanvasRoot;
''',
    '''        private readonly RawImage stormFlash;
        private readonly RawImage weatherComposite;
        private readonly RawImage wetGlass;
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
''',
    "visual fields",
)

world = replace_once(
    world,
    '''        private readonly ParticleSystem[] rainLayers = new ParticleSystem[3];
        private readonly float[] baseEmission = { 18f, 18f, 16f };
''',
    '''        private readonly ParticleSystem[] rainLayers = new ParticleSystem[3];
        private readonly float[] baseEmission = { 22f, 22f, 20f };
''',
    "rain emission",
)

world = replace_once(
    world,
    '''        private float thunderCountdown = -1f;
        private int lightningSerial;
        private bool atHome;
''',
    '''        private float thunderCountdown = -1f;
        private int lightningSerial;
        private Vector2 smoothedCursorParallax;
        private Vector2 roomParallax;
        private readonly Vector2 outsideBasePosition;
        private bool atHome;
''',
    "parallax state",
)

world = replace_once(
    world,
    '''            darkness = ui.Box(parent, "LampMood", 0, 0, 1920, 1080, Color.clear);
            portalLight = ui.Box(parent, "PortalBreath", 0, 100, 1920, 906, Color.clear);

            weatherCanvasRoot = ui.Rect(parent, "WindowRain", 350, 107, 605, 396);
''',
    '''            RawImage sourceArt = background.GetComponent<RawImage>();
            Texture homeSource = sourceArt ? sourceArt.texture : null;

            outsideDepthMask = ui.Rect(parent, "HomeOutsideDepthMask", 350, 107, 605, 396);
            outsideDepthMask.gameObject.AddComponent<RectMask2D>();
            outsideParallax = ui.Art(outsideDepthMask, "HomeOutsideParallax", homeSource, -8, -8, 621, 412);
            outsideParallax.uvRect = SourceUv(342f, 99f, 621f, 412f);
            outsideParallax.color = new Color(.96f, .985f, 1f, 1f);
            outsideBasePosition = outsideParallax.rectTransform.anchoredPosition;

            darkness = ui.Box(parent, "LampMood", 0, 0, 1920, 1080, Color.clear);
            portalLight = ui.Box(parent, "PortalBreath", 0, 100, 1920, 906, Color.clear);

            weatherCanvasRoot = ui.Rect(parent, "WindowRain", 350, 107, 605, 396);
''',
    "outside layer",
)

world = replace_once(
    world,
    '''            radialTexture = CreateRadialTexture();
            exteriorHaze = ui.Art(weatherCanvasRoot, "HomeExteriorHaze", radialTexture, -36, 238, 680, 190);
            exteriorHaze.color = Color.clear;
            wetGlassTexture = CreateWetGlassTexture();
            wetGlass = ui.Art(weatherCanvasRoot, "HomeWetGlass", wetGlassTexture, 0, 0, 605, 396);
            wetGlass.color = Color.clear;
            stormFlash = ui.Box(weatherCanvasRoot, "HomeStormFlash", 0, 0, 605, 396, Color.clear);

            coldWindowBounce = ui.Art(parent, "HomeColdWindowBounce", radialTexture, 285, 55, 760, 510);
            coldWindowBounce.color = Color.clear;
            warmInteriorGlow = ui.Art(parent, "HomeWarmInteriorGlow", radialTexture, 25, 120, 910, 760);
            warmInteriorGlow.color = Color.clear;
''',
    '''            radialTexture = CreateRadialTexture();
            exteriorHaze = ui.Art(weatherCanvasRoot, "HomeExteriorHaze", radialTexture, -36, 238, 680, 190);
            exteriorHaze.color = Color.clear;
            exteriorMistNear = ui.Art(weatherCanvasRoot, "HomeExteriorMistNear", radialTexture, -82, 176, 760, 248);
            exteriorMistNear.color = Color.clear;

            wetGlassTexture = CreateWetGlassTexture();
            wetGlass = ui.Art(weatherCanvasRoot, "HomeWetGlass", wetGlassTexture, 0, 0, 605, 396);
            wetGlass.color = Color.clear;
            wetGlassStreaks = ui.Art(weatherCanvasRoot, "HomeWetGlassStreaks", wetGlassTexture, -5, -4, 615, 404);
            wetGlassStreaks.uvRect = new Rect(.17f, .31f, 1.28f, 1.28f);
            wetGlassStreaks.color = Color.clear;

            stormFlash = ui.Art(weatherCanvasRoot, "HomeStormFlash", radialTexture, -78, -62, 760, 520);
            stormFlash.color = Color.clear;

            foregroundDepth = ui.Art(parent, "HomeForegroundDepth", homeSource, 0, 0, 1920, 1080);
            foregroundDepth.color = new Color(1f, 1f, 1f, 0f);
            CreateArtSlice(ui, foregroundDepth.transform, "FrameTop", homeSource, 326, 83, 653, 30);
            CreateArtSlice(ui, foregroundDepth.transform, "FrameLeft", homeSource, 326, 113, 34, 390);
            CreateArtSlice(ui, foregroundDepth.transform, "FrameRight", homeSource, 945, 113, 34, 390);
            CreateArtSlice(ui, foregroundDepth.transform, "FrameBottom", homeSource, 326, 503, 653, 32);

            coldWindowBounce = ui.Art(parent, "HomeColdWindowBounce", radialTexture, 235, 15, 880, 610);
            coldWindowBounce.color = Color.clear;
            warmInteriorGlow = ui.Art(parent, "HomeWarmInteriorGlow", radialTexture, 25, 120, 910, 760);
            warmInteriorGlow.color = Color.clear;
            stormRoomLift = ui.Art(parent, "HomeStormRoomLift", radialTexture, -165, -90, 1540, 1160);
            stormRoomLift.color = Color.clear;
''',
    "weather depth layers",
)

world = replace_once(
    world,
    '''            rainLayers[0] = CreateRainLayer("HomeRainFar", 48, 2.7f, 4.3f, .035f, .34f, 0);
            rainLayers[1] = CreateRainLayer("HomeRainMid", 36, 1.85f, 6.4f, .055f, .48f, 1);
            rainLayers[2] = CreateRainLayer("HomeRainNear", 24, 1.35f, 8.8f, .085f, .64f, 2);
''',
    '''            rainLayers[0] = CreateRainLayer("HomeRainFar", 48, 2.4f, 5.2f, .045f, .46f, 0);
            rainLayers[1] = CreateRainLayer("HomeRainMid", 36, 1.6f, 7.5f, .075f, .64f, 1);
            rainLayers[2] = CreateRainLayer("HomeRainNear", 24, 1.15f, 10.5f, .14f, .82f, 2);
''',
    "rain layer tuning",
)

world = replace_once(
    world,
    '''            weatherComposite.rectTransform.sizeDelta = weatherCanvasRoot.sizeDelta;
            stormFlash.rectTransform.sizeDelta = weatherCanvasRoot.sizeDelta;
            weatherCamera.aspect = home ? 605f / 396f : 1920f / 906f;
''',
    '''            weatherComposite.rectTransform.sizeDelta = weatherCanvasRoot.sizeDelta;
            stormFlash.rectTransform.sizeDelta = home ? new Vector2(760, 520) : weatherCanvasRoot.sizeDelta;
            stormFlash.rectTransform.anchoredPosition = home ? new Vector2(-78, 62) : Vector2.zero;
            weatherCamera.aspect = home ? 605f / 396f : 1920f / 906f;
''',
    "storm surface sizing",
)

world = replace_once(
    world,
    '''            bool homeDetails = home && atmosphere.enabled;
            wetGlass.gameObject.SetActive(homeDetails);
            exteriorHaze.gameObject.SetActive(homeDetails);
            stormFlash.gameObject.SetActive(homeDetails);
            coldWindowBounce.gameObject.SetActive(homeDetails);
            warmInteriorGlow.gameObject.SetActive(homeDetails);
''',
    '''            bool homeDetails = home && atmosphere.enabled;
            outsideDepthMask.gameObject.SetActive(homeDetails);
            foregroundDepth.gameObject.SetActive(homeDetails);
            wetGlass.gameObject.SetActive(homeDetails);
            wetGlassStreaks.gameObject.SetActive(homeDetails);
            exteriorHaze.gameObject.SetActive(homeDetails);
            exteriorMistNear.gameObject.SetActive(homeDetails);
            stormFlash.gameObject.SetActive(homeDetails);
            coldWindowBounce.gameObject.SetActive(homeDetails);
            warmInteriorGlow.gameObject.SetActive(homeDetails);
            stormRoomLift.gameObject.SetActive(homeDetails);
''',
    "home detail visibility",
)

world = replace_once(
    world,
    '''            if (!home)
            {
                lightningAge = -1f;
                thunderCountdown = -1f;
            }
''',
    '''            if (!home)
            {
                lightningAge = -1f;
                thunderCountdown = -1f;
                smoothedCursorParallax = Vector2.zero;
                roomParallax = Vector2.zero;
                outsideParallax.rectTransform.anchoredPosition = outsideBasePosition;
                foregroundDepth.rectTransform.anchoredPosition = Vector2.zero;
            }
''',
    "location reset",
)

world = replace_once(
    world,
    '''            UpdateRainTuning();
            UpdateStorm(dt);
            ApplyLighting(lampOn);
''',
    '''            UpdateRainTuning();
            UpdateStorm(dt);
            UpdateParallax(dt);
            ApplyLighting(lampOn);
''',
    "parallax tick",
)

world = replace_once(
    world,
    '''            background.anchoredPosition = new Vector2(
                Mathf.Sin(time * 63) * shake + distortion * 8,
                Mathf.Cos(time * 53) * shake);
''',
    '''            background.anchoredPosition = roomParallax + new Vector2(
                Mathf.Sin(time * 63) * shake + distortion * 8,
                Mathf.Cos(time * 53) * shake);
''',
    "room parallax composition",
)

world = replace_once(
    world,
    '''            renderer.velocityScale = .12f;
            renderer.lengthScale = 2.1f + sortingOrder * .55f;
''',
    '''            renderer.velocityScale = .15f;
            renderer.lengthScale = sortingOrder == 0 ? 1.85f : sortingOrder == 1 ? 2.75f : 3.55f;
''',
    "rain streak separation",
)

world = replace_once(
    world,
    '''            float intensity = Mathf.Clamp(atmosphere.rainIntensity, 0f, 1.5f);
            float speed = Mathf.Clamp(atmosphere.rainSpeed, .35f, 2f);
''',
    '''            float readability = Mathf.Clamp(atmosphere.weatherReadability, .5f, 1.5f);
            float intensity = Mathf.Clamp(atmosphere.rainIntensity * readability, 0f, 1.5f);
            float speed = Mathf.Clamp(atmosphere.rainSpeed, .35f, 2f);
''',
    "weather readability",
)

world = replace_once(
    world,
    '''            weatherComposite.color = new Color(.86f, .95f, 1f, Mathf.Clamp01(.55f + intensity * .34f));
''',
    '''            weatherComposite.color = new Color(.88f, .96f, 1f, Mathf.Clamp01(.66f + intensity * .24f));
''',
    "composite readability",
)

world = replace_once(
    world,
    '''        private void UpdateStorm(float dt)
''',
    '''        private void UpdateParallax(float dt)
        {
            if (!atHome || !atmosphere.enabled)
            {
                roomParallax = Vector2.zero;
                return;
            }

            float intensity = Mathf.Clamp(atmosphere.parallaxIntensity, 0f, 1.5f);
            Vector2 ambient = new Vector2(
                Mathf.Sin(time * .23f),
                Mathf.Sin(time * .17f + .70f) - Mathf.Sin(.70f));

            Vector2 cursorTarget = Vector2.zero;
            if (Application.isFocused && Screen.width > 0 && Screen.height > 0)
            {
                Vector3 pointer = Input.mousePosition;
                cursorTarget = new Vector2(
                    Mathf.Clamp(pointer.x / Screen.width * 2f - 1f, -1f, 1f),
                    Mathf.Clamp(pointer.y / Screen.height * 2f - 1f, -1f, 1f));
                cursorTarget *= Mathf.Clamp(atmosphere.cursorParallaxStrength, 0f, .45f);
            }

            float smoothing = Mathf.Max(.5f, atmosphere.parallaxSmoothing);
            float blend = dt <= 0f ? 0f : 1f - Mathf.Exp(-dt * smoothing);
            smoothedCursorParallax = Vector2.Lerp(smoothedCursorParallax, cursorTarget, blend);

            Vector2 driver = ambient * .72f + smoothedCursorParallax;
            roomParallax = Vector2.ClampMagnitude(
                driver * atmosphere.roomParallaxPixels * intensity, 2.20f);

            Vector2 outsideOffset = Vector2.ClampMagnitude(
                driver * atmosphere.farParallaxPixels * intensity *
                Mathf.Clamp(atmosphere.outsideMotionIntensity, 0f, 1.5f), 2.05f);
            outsideParallax.rectTransform.anchoredPosition = outsideBasePosition + outsideOffset;

            Vector2 nearOffset = Vector2.ClampMagnitude(
                driver * atmosphere.nearParallaxPixels * intensity, 4.10f);
            foregroundDepth.rectTransform.anchoredPosition = nearOffset;
        }

        private void UpdateStorm(float dt)
''',
    "parallax method",
)

world = replace_once(
    world,
    '''            darkness.color = new Color(.01f, .045f, .07f,
                atHome && !lampOn ? Mathf.Clamp01(.64f - flash * .10f + slowPulse * .015f) : 0f);
''',
    '''            darkness.color = new Color(.01f, .045f, .07f,
                atHome && !lampOn ? Mathf.Clamp01(.64f - flash * .19f + slowPulse * .015f) : 0f);
''',
    "room flash response",
)

world = replace_once(
    world,
    '''            if (!atHome)
            {
                stormFlash.color = Color.clear;
                wetGlass.color = Color.clear;
                exteriorHaze.color = Color.clear;
                coldWindowBounce.color = Color.clear;
                warmInteriorGlow.color = Color.clear;
                return;
            }

            stormFlash.color = new Color(.76f, .89f, 1f, Mathf.Clamp01(flash * .42f));
            float haze = Mathf.Clamp(atmosphere.hazeIntensity, 0f, 1.5f);
            exteriorHaze.color = new Color(.46f, .69f, .76f,
                Mathf.Clamp01((.055f + slowPulse * .020f) * haze + flash * .055f));
            exteriorHaze.rectTransform.anchoredPosition = new Vector2(
                -36f + Mathf.Sin(time * .10f) * 13f,
                -238f + Mathf.Cos(time * .075f) * 4f);

            wetGlass.uvRect = new Rect(0f, Mathf.Repeat(time * .006f, 1f), 1f, 1f);
            wetGlass.color = new Color(.79f, .93f, .97f,
                Mathf.Clamp01(.22f * Mathf.Clamp(atmosphere.wetGlassIntensity, 0f, 1.5f) + flash * .06f));

            float glow = Mathf.Clamp(atmosphere.glowIntensity, 0f, 1.5f);
            coldWindowBounce.color = new Color(.30f, .59f, .72f,
                Mathf.Clamp01((.028f + slowPulse * .018f) * glow + flash * .12f));
            warmInteriorGlow.color = new Color(1f, .59f, .28f,
                Mathf.Clamp01((lampOn ? .062f : .012f) * glow * (.92f + slowPulse * .08f) + flash * .018f));
''',
    '''            if (!atHome)
            {
                stormFlash.color = Color.clear;
                wetGlass.color = Color.clear;
                wetGlassStreaks.color = Color.clear;
                exteriorHaze.color = Color.clear;
                exteriorMistNear.color = Color.clear;
                coldWindowBounce.color = Color.clear;
                warmInteriorGlow.color = Color.clear;
                stormRoomLift.color = Color.clear;
                return;
            }

            float readability = Mathf.Clamp(atmosphere.weatherReadability, .5f, 1.5f);
            stormFlash.color = new Color(.76f, .90f, 1f, Mathf.Clamp01(flash * .58f));
            outsideParallax.color = new Color(
                Mathf.Lerp(.94f, 1f, Mathf.Clamp01(flash)),
                Mathf.Lerp(.975f, 1f, Mathf.Clamp01(flash)),
                1f, 1f);

            float haze = Mathf.Clamp(atmosphere.hazeIntensity, 0f, 1.5f);
            float exteriorMotion = Mathf.Clamp(atmosphere.outsideMotionIntensity, 0f, 1.5f);
            exteriorHaze.color = new Color(.46f, .69f, .76f,
                Mathf.Clamp01((.080f + slowPulse * .027f) * haze * readability + flash * .085f));
            exteriorHaze.rectTransform.anchoredPosition = new Vector2(
                -36f + Mathf.Sin(time * .13f) * 12f * exteriorMotion,
                -238f + Mathf.Cos(time * .091f) * 4.5f * exteriorMotion);

            exteriorMistNear.color = new Color(.54f, .76f, .82f,
                Mathf.Clamp01((.050f + (.5f + .5f * Mathf.Sin(time * .21f)) * .026f) *
                    haze * readability + flash * .095f));
            exteriorMistNear.rectTransform.anchoredPosition = new Vector2(
                -82f + Mathf.Sin(time * .073f + 1.4f) * 15f * exteriorMotion,
                -176f + Mathf.Cos(time * .057f) * 5f * exteriorMotion);

            float wet = Mathf.Clamp(atmosphere.wetGlassIntensity, 0f, 1.5f);
            wetGlass.uvRect = new Rect(
                Mathf.Repeat(time * .0035f, 1f),
                Mathf.Repeat(time * .018f, 1f), 1f, 1f);
            wetGlass.rectTransform.anchoredPosition = new Vector2(
                Mathf.Sin(time * .12f) * 1.2f,
                Mathf.Cos(time * .087f) * .8f);
            wetGlass.color = new Color(.80f, .94f, .98f,
                Mathf.Clamp01(.31f * wet * readability + flash * .12f));

            wetGlassStreaks.uvRect = new Rect(
                .17f + Mathf.Repeat(time * .0021f, 1f),
                .31f + Mathf.Repeat(time * .0105f, 1f), 1.28f, 1.28f);
            wetGlassStreaks.rectTransform.anchoredPosition = new Vector2(
                -5f + Mathf.Sin(time * .081f + .7f) * 1.5f,
                4f + Mathf.Cos(time * .061f) * 1.1f);
            wetGlassStreaks.color = new Color(.86f, .96f, 1f,
                Mathf.Clamp01(.16f * wet * readability + flash * .14f));

            float glow = Mathf.Clamp(atmosphere.glowIntensity, 0f, 1.5f);
            coldWindowBounce.color = new Color(.30f, .60f, .74f,
                Mathf.Clamp01((.045f + slowPulse * .023f) * glow + flash * .22f));
            warmInteriorGlow.color = new Color(1f, .59f, .28f,
                Mathf.Clamp01((lampOn ? .070f : .014f) * glow * (.90f + slowPulse * .10f) + flash * .024f));
            stormRoomLift.color = new Color(.54f, .76f, .90f,
                Mathf.Clamp01(flash * .16f));
''',
    "environmental lighting",
)

world = replace_once(
    world,
    '''            stormFlash.color = new Color(.78f, .90f, 1f,
                Mathf.Clamp01(.38f * atmosphere.lightningIntensity));
''',
    '''            stormFlash.color = new Color(.78f, .91f, 1f,
                Mathf.Clamp01(.52f * atmosphere.lightningIntensity));
''',
    "immediate soft flash",
)

world = replace_once(
    world,
    '''        private ParticleSystem CreateRainLayer(string name, int maxParticles, float lifetime, float fallSpeed,
''',
    '''        private static Rect SourceUv(float x, float y, float width, float height)
        {
            return new Rect(
                x / 1920f,
                1f - (y + height) / 1080f,
                width / 1920f,
                height / 1080f);
        }

        private static RawImage CreateArtSlice(UiKit ui, Transform parent, string name, Texture source,
            float x, float y, float width, float height)
        {
            RawImage slice = ui.Art(parent, name, source, x, y, width, height);
            slice.uvRect = SourceUv(x, y, width, height);
            slice.color = Color.white;
            return slice;
        }

        private ParticleSystem CreateRainLayer(string name, int maxParticles, float lifetime, float fallSpeed,
''',
    "crop helpers",
)

profile = replace_once(
    profile,
    '''        [Range(0f, 1.5f)] public float rainIntensity = .88f;
        [Range(.35f, 2f)] public float rainSpeed = 1f;
''',
    '''        [Range(0f, 1.5f)] public float rainIntensity = 1.18f;
        [Range(.35f, 2f)] public float rainSpeed = 1.12f;
''',
    "profile rain defaults",
)
profile = replace_once(
    profile,
    '''        [Range(0f, 1.5f)] public float lightningIntensity = .82f;
''',
    '''        [Range(0f, 1.5f)] public float lightningIntensity = 1.02f;
''',
    "profile lightning",
)
profile = replace_once(
    profile,
    '''        [Range(0f, 1.5f)] public float glowIntensity = .62f;
        [Range(0f, 1.5f)] public float hazeIntensity = .56f;
        [Range(0f, 1.5f)] public float wetGlassIntensity = .58f;
''',
    '''        [Range(0f, 1.5f)] public float glowIntensity = .82f;
        [Range(0f, 1.5f)] public float hazeIntensity = .88f;
        [Range(0f, 1.5f)] public float wetGlassIntensity = .92f;

        [Header("2.5D Layered Motion")]
        [Range(0f, 1.5f)] public float parallaxIntensity = .85f;
        [Range(0f, .45f)] public float cursorParallaxStrength = .16f;
        [Range(.5f, 10f)] public float parallaxSmoothing = 4.2f;
        [Range(0f, 2.25f)] public float farParallaxPixels = .72f;
        [Range(0f, 2.25f)] public float roomParallaxPixels = 1.45f;
        [Range(0f, 4.25f)] public float nearParallaxPixels = 3.85f;
        [Range(0f, 1.5f)] public float outsideMotionIntensity = .92f;
        [Range(.5f, 1.5f)] public float weatherReadability = 1.16f;
''',
    "profile 2.5d tunables",
)

asset = replace_once(asset, "  rainIntensity: 0.88\n  rainSpeed: 1\n",
                     "  rainIntensity: 1.18\n  rainSpeed: 1.12\n", "asset rain")
asset = replace_once(asset, "  lightningIntensity: 0.82\n",
                     "  lightningIntensity: 1.02\n", "asset lightning")
asset = replace_once(
    asset,
    "  glowIntensity: 0.62\n  hazeIntensity: 0.56\n  wetGlassIntensity: 0.58\n",
    "  glowIntensity: 0.82\n  hazeIntensity: 0.88\n  wetGlassIntensity: 0.92\n"
    "  parallaxIntensity: 0.85\n"
    "  cursorParallaxStrength: 0.16\n"
    "  parallaxSmoothing: 4.2\n"
    "  farParallaxPixels: 0.72\n"
    "  roomParallaxPixels: 1.45\n"
    "  nearParallaxPixels: 3.85\n"
    "  outsideMotionIntensity: 0.92\n"
    "  weatherReadability: 1.16\n",
    "asset 2.5d tunables",
)

world_path.write_text(world, encoding="utf-8")
profile_path.write_text(profile, encoding="utf-8")
asset_path.write_text(asset, encoding="utf-8")
print("Applied Home Hub 2.5D layered animation production patch")
