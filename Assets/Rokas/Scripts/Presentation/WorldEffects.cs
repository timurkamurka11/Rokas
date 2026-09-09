using Rokas.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class WorldEffects
    {
        private const int WeatherLayer = 30;
        private readonly RectTransform background;
        private readonly SettingsData settings;
        private readonly RokasAudio audio;
        private readonly HomeAtmosphereProfile atmosphere;
        private readonly Image portalLight;
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
        private readonly System.Random stormRandom = new System.Random(20260909);
        private float time;
        private float impact;
        private float glitch;
        private float nextLightning;
        private float lightningAge = -1f;
        private float thunderCountdown = -1f;
        private int lightningSerial;
        private Vector2 smoothedCursorParallax;
        private Vector2 roomParallax;
        private readonly Vector2 outsideBasePosition;
        private bool atHome;
        private bool atPortal;
        private bool disposed;

        public WorldEffects(UiKit ui, RectTransform parent, RectTransform background, SettingsData settings,
            RokasAudio audio, Transform runtimeParent)
        {
            this.background = background;
            this.settings = settings;
            this.audio = audio;
            atmosphere = Resources.Load<HomeAtmosphereProfile>("HomeAtmosphereProfile");
            if (!atmosphere)
            {
                atmosphere = ScriptableObject.CreateInstance<HomeAtmosphereProfile>();
                atmosphere.hideFlags = HideFlags.HideAndDontSave;
            }

            RawImage sourceArt = background.GetComponent<RawImage>();
            Texture homeSource = sourceArt ? sourceArt.texture : null;

            outsideDepthMask = ui.Rect(parent, "HomeOutsideDepthMask", 350, 107, 605, 396);
            outsideDepthMask.gameObject.AddComponent<RectMask2D>();
            outsideParallax = ui.Art(outsideDepthMask, "HomeOutsideParallax", homeSource, -8, -8, 621, 412);
            outsideParallax.uvRect = SourceUv(342f, 99f, 621f, 412f);
            outsideParallax.color = new Color(.96f, .985f, 1f, 1f);
            outsideBasePosition = outsideParallax.rectTransform.anchoredPosition;

            portalLight = ui.Box(parent, "PortalBreath", 0, 100, 1920, 906, Color.clear);

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

            Shader rainShader = Shader.Find("Particles/Standard Unlit");
            if (!rainShader) rainShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (!rainShader) rainShader = Shader.Find("Sprites/Default");
            if (!rainShader) rainShader = Shader.Find("UI/Default");
            if (rainShader)
            {
                rainMaterial = new Material(rainShader)
                {
                    name = "HomeRainMaterial",
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = rainTexture
                };
            }

            rainLayers[0] = CreateRainLayer("HomeRainFar", 48, 2.4f, 5.2f, .045f, .46f, 0);
            rainLayers[1] = CreateRainLayer("HomeRainMid", 36, 1.6f, 7.5f, .075f, .64f, 1);
            rainLayers[2] = CreateRainLayer("HomeRainNear", 24, 1.15f, 10.5f, .14f, .82f, 2);
            weatherRuntime.SetActive(false);

            steamRoot = ui.Rect(parent, "TeaSteam", 791, 535, 50, 90);
            for (int i = 0; i < steam.Length; i++)
            {
                steam[i] = ui.Box(steamRoot, "Steam" + i, 13, 0, 2, 13,
                    new Color(.96f, .9f, .72f, .08f)).rectTransform;
                steam[i].localRotation = Quaternion.Euler(0, 0, -25 + i * 7);
            }
            for (int i = 0; i < tears.Length; i++)
                tears[i] = ui.Box(parent, "RealityTear" + i, 0, 285 + i * 217, 1920, 2 + i, Color.clear);

            ScheduleNextLightning();
        }

        public void SetLocation(bool home, bool portal)
        {
            atHome = home;
            atPortal = portal;
            bool weatherVisible = atmosphere.enabled && atmosphere.rainEnabled && (home || portal);
            weatherCanvasRoot.gameObject.SetActive(weatherVisible);
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
            steamRoot.gameObject.SetActive(home);
            audio.SetHomeWeatherMix(home ? atmosphere.ambientRainVolume : 1f);
            if (home && nextLightning <= 0f) ScheduleNextLightning();
            if (!home)
            {
                if (lightningAge >= 0f) nextLightning = 0f;
                lightningAge = -1f;
                thunderCountdown = -1f;
                smoothedCursorParallax = Vector2.zero;
                roomParallax = Vector2.zero;
                outsideParallax.rectTransform.anchoredPosition = outsideBasePosition;
                foregroundDepth.rectTransform.anchoredPosition = Vector2.zero;
            }
            if (portal) glitch = .22f;
            impact = 0;
        }

        public void Impact(float strength)
        {
            impact = Mathf.Max(impact, strength * .18f);
            if (strength >= .9f) glitch = .2f;
        }

        public void ForceLightning()
        {
            if (!atHome || !atmosphere.enabled || !atmosphere.lightningEnabled) return;
            TriggerLightning();
        }

        public void Tick(float dt, bool lampOn)
        {
            time += dt;
            impact = Mathf.Max(0, impact - dt);
            glitch = Mathf.Max(0, glitch - dt);
            UpdateRainTuning();
            UpdateStorm(dt);
            UpdateParallax(dt);
            ApplyLighting(lampOn);

            portalLight.color = new Color(.24f, .47f, .6f,
                atPortal ? .018f + Mathf.Sin(time * .8f) * .011f : 0);
            float shake = settings.screenShake ? impact * 17 : 0;
            float distortion = glitch * Mathf.Clamp(settings.glitchIntensity, 0, 1.5f);
            background.anchoredPosition = roomParallax + new Vector2(
                Mathf.Sin(time * 63) * shake + distortion * 8,
                Mathf.Cos(time * 53) * shake);
            for (int i = 0; i < tears.Length; i++)
                tears[i].color = new Color(i == 1 ? .8f : .26f, .62f, .62f, distortion * .25f);

            for (int i = 0; i < steam.Length; i++)
            {
                float t = Mathf.Repeat(time * .32f + i / 7f, 1);
                steam[i].anchoredPosition = new Vector2(18 + Mathf.Sin(t * 6 + i) * 5, -72 + t * 64);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (weatherCamera) weatherCamera.targetTexture = null;
            if (weatherTexture)
            {
                weatherTexture.Release();
                Object.Destroy(weatherTexture);
            }
            if (weatherRuntime) Object.Destroy(weatherRuntime);
            if (rainMaterial) Object.Destroy(rainMaterial);
            if (atmosphere && (atmosphere.hideFlags & HideFlags.DontSave) != 0) Object.Destroy(atmosphere);
        }

        private static Rect SourceUv(float x, float y, float width, float height)
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
            float size, float alpha, int sortingOrder)
        {
            var go = new GameObject(name);
            go.layer = WeatherLayer;
            go.transform.SetParent(weatherRuntime.transform, false);
            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = new Color(.78f, .91f, 1f, alpha);

            var emission = system.emission;
            emission.rateOverTime = maxParticles / Mathf.Max(.2f, lifetime) * .92f;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0, 5.35f, 0);
            shape.scale = new Vector3(18.4f, .12f, .05f);

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-.54f - sortingOrder * .16f);
            velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed);

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(.78f, .91f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, .10f),
                    new GradientAlphaKey(.88f, .78f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .15f;
            renderer.lengthScale = sortingOrder == 0 ? 1.85f : sortingOrder == 1 ? 2.75f : 3.55f;
            renderer.cameraVelocityScale = 0f;
            renderer.sortingOrder = sortingOrder;
            if (rainMaterial) renderer.sharedMaterial = rainMaterial;
            else renderer.enabled = false;
            return system;
        }

        private void ResizeEmitterWidth()
        {
            float width = weatherCamera.orthographicSize * 2f * weatherCamera.aspect * 1.20f;
            for (int i = 0; i < rainLayers.Length; i++)
            {
                if (rainLayers[i] == null) continue;
                var shape = rainLayers[i].shape;
                shape.scale = new Vector3(width, .12f, .05f);
            }
        }

        private void UpdateRainTuning()
        {
            if (!atmosphere) return;
            bool[] enabled = { atmosphere.farRainEnabled, atmosphere.midRainEnabled, atmosphere.nearRainEnabled };
            float readability = Mathf.Clamp(atmosphere.weatherReadability, .5f, 1.5f);
            float intensity = Mathf.Clamp(atmosphere.rainIntensity * readability, 0f, 1.5f);
            float speed = Mathf.Clamp(atmosphere.rainSpeed, .35f, 2f);
            for (int i = 0; i < rainLayers.Length; i++)
            {
                ParticleSystem system = rainLayers[i];
                if (system == null) continue;
                var main = system.main;
                main.simulationSpeed = speed;
                var emission = system.emission;
                emission.enabled = atmosphere.rainEnabled && enabled[i];
                emission.rateOverTime = baseEmission[i] * intensity;
            }
            weatherComposite.color = new Color(.88f, .96f, 1f, Mathf.Clamp01(.66f + intensity * .24f));
            audio.SetHomeWeatherMix(atHome ? atmosphere.ambientRainVolume : 1f);
        }

        private void UpdateParallax(float dt)
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
        {
            if (!atHome || !atmosphere.enabled || !atmosphere.lightningEnabled)
            {
                lightningAge = -1f;
                thunderCountdown = -1f;
                return;
            }

            if (lightningAge >= 0f)
            {
                lightningAge += dt;
                if (lightningAge > .62f)
                {
                    lightningAge = -1f;
                    ScheduleNextLightning();
                }
            }
            else
            {
                nextLightning -= dt;
                if (nextLightning <= 0f) TriggerLightning();
            }

            if (thunderCountdown >= 0f)
            {
                thunderCountdown -= dt;
                if (thunderCountdown <= 0f)
                {
                    thunderCountdown = -1f;
                    audio.PlayThunder(lightningSerial % 3, atmosphere.thunderVolume);
                }
            }
        }

        private void TriggerLightning()
        {
            lightningSerial++;
            lightningAge = .001f;
            thunderCountdown = .72f + (float)stormRandom.NextDouble() * 1.10f;
            nextLightning = float.MaxValue;
            stormFlash.color = new Color(.78f, .91f, 1f,
                Mathf.Clamp01(.52f * atmosphere.lightningIntensity));
        }

        private void ScheduleNextLightning()
        {
            float min = Mathf.Max(5f, atmosphere.lightningMinInterval);
            float max = Mathf.Max(min + 1f, atmosphere.lightningMaxInterval);
            nextLightning = Mathf.Lerp(min, max, (float)stormRandom.NextDouble());
        }

        private void ApplyLighting(bool lampOn)
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

        private static float LightningEnvelope(float age)
        {
            if (age < 0f) return 0f;
            if (age < .055f) return Mathf.SmoothStep(0f, 1f, age / .055f);
            if (age < .12f) return Mathf.Lerp(1f, .18f, (age - .055f) / .065f);
            if (age < .19f) return Mathf.Lerp(.18f, .72f, (age - .12f) / .07f);
            if (age < .62f) return Mathf.Lerp(.72f, 0f, (age - .19f) / .43f);
            return 0f;
        }

        private static Texture2D RequireWeatherTexture(string name)
        {
            Texture2D texture = Resources.Load<Texture2D>("Weather3/" + name);
            if (!texture)
                throw new System.InvalidOperationException("Missing required Home Weather 3 texture: " + name);
            return texture;
        }
    }
}
