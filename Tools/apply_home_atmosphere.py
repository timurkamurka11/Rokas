from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

world_path = ROOT / "Assets/Rokas/Scripts/Presentation/WorldEffects.cs"
audio_path = ROOT / "Assets/Rokas/Scripts/Presentation/RokasAudio.cs"
view_path = ROOT / "Assets/Rokas/Scripts/Presentation/RokasView.cs"
boot_path = ROOT / "Assets/Rokas/Scripts/Presentation/RokasBootstrap.cs"
profile_path = ROOT / "Assets/Rokas/Scripts/Presentation/HomeAtmosphereProfile.cs"
profile_meta_path = ROOT / "Assets/Rokas/Scripts/Presentation/HomeAtmosphereProfile.cs.meta"
asset_path = ROOT / "Assets/Rokas/Resources/HomeAtmosphereProfile.asset"
asset_meta_path = ROOT / "Assets/Rokas/Resources/HomeAtmosphereProfile.asset.meta"

old_world = world_path.read_text(encoding="utf-8")
if "private readonly RectTransform[] rain = new RectTransform[44];" not in old_world:
    raise SystemExit("WorldEffects no longer matches the verified Home baseline; stop for concurrent-change review")

old_audio = audio_path.read_text(encoding="utf-8")
if "private readonly AudioClip reactionCue;" not in old_audio or "homeWeatherMix" in old_audio:
    raise SystemExit("RokasAudio no longer matches the verified Home baseline; stop for concurrent-change review")

view = view_path.read_text(encoding="utf-8")
view_anchor = "effects = new WorldEffects(ui, stage, background.rectTransform, session.State.settings);"
if view_anchor not in view:
    raise SystemExit("Missing RokasView WorldEffects constructor anchor")
view = view.replace(
    view_anchor,
    "effects = new WorldEffects(ui, stage, background.rectTransform, session.State.settings, audio, owner.transform);",
    1,
)
dispose_anchor = "        public void Dispose()\n        {\n            messageNotifications.Dispose();"
if dispose_anchor not in view:
    raise SystemExit("Missing RokasView Dispose anchor")
view = view.replace(
    dispose_anchor,
    "        public void Dispose()\n        {\n            effects.Dispose();\n            messageNotifications.Dispose();",
    1,
)

boot = boot_path.read_text(encoding="utf-8")
boot_anchor = "            if (View != null) View.Dispose();\n        }"
if boot_anchor not in boot:
    raise SystemExit("Missing RokasBootstrap disposal anchor")
boot = boot.replace(
    boot_anchor,
    "            if (View != null) View.Dispose();\n            if (sound != null) sound.Dispose();\n        }",
    1,
)

profile = r'''using UnityEngine;

namespace Rokas.Presentation
{
    [CreateAssetMenu(fileName = "HomeAtmosphereProfile", menuName = "ROKAS/Home Atmosphere Profile")]
    public sealed class HomeAtmosphereProfile : ScriptableObject
    {
        [Header("Master")]
        public bool enabled = true;
        public bool rainEnabled = true;
        public bool lightningEnabled = true;

        [Header("Rain")]
        [Range(0f, 1.5f)] public float rainIntensity = .88f;
        [Range(.35f, 2f)] public float rainSpeed = 1f;
        public bool farRainEnabled = true;
        public bool midRainEnabled = true;
        public bool nearRainEnabled = true;

        [Header("Storm")]
        [Min(5f)] public float lightningMinInterval = 16f;
        [Min(8f)] public float lightningMaxInterval = 34f;
        [Range(0f, 1.5f)] public float lightningIntensity = .82f;
        [Range(0f, 1f)] public float thunderVolume = .56f;

        [Header("Atmosphere Mix")]
        [Range(0f, 1f)] public float ambientRainVolume = .72f;
        [Range(0f, 1.5f)] public float glowIntensity = .62f;
        [Range(0f, 1.5f)] public float hazeIntensity = .56f;
        [Range(0f, 1.5f)] public float wetGlassIntensity = .58f;
    }
}
'''

profile_meta = '''fileFormatVersion: 2
guid: 70b20e8a4e4d4fe6baeb9c377f738c61
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
'''

profile_asset = '''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 70b20e8a4e4d4fe6baeb9c377f738c61, type: 3}
  m_Name: HomeAtmosphereProfile
  m_EditorClassIdentifier:
  enabled: 1
  rainEnabled: 1
  lightningEnabled: 1
  rainIntensity: 0.88
  rainSpeed: 1
  farRainEnabled: 1
  midRainEnabled: 1
  nearRainEnabled: 1
  lightningMinInterval: 16
  lightningMaxInterval: 34
  lightningIntensity: 0.82
  thunderVolume: 0.56
  ambientRainVolume: 0.72
  glowIntensity: 0.62
  hazeIntensity: 0.56
  wetGlassIntensity: 0.58
'''

asset_meta = '''fileFormatVersion: 2
guid: 39d3722a35f14964b1785b405d4648a7
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
'''

world = r'''using Rokas.Core;
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
        private readonly Image darkness;
        private readonly Image portalLight;
        private readonly Image stormFlash;
        private readonly RawImage weatherComposite;
        private readonly RawImage wetGlass;
        private readonly RawImage exteriorHaze;
        private readonly RawImage coldWindowBounce;
        private readonly RawImage warmInteriorGlow;
        private readonly RectTransform weatherCanvasRoot;
        private readonly RectTransform steamRoot;
        private readonly RectTransform[] steam = new RectTransform[7];
        private readonly Image[] tears = new Image[3];
        private readonly GameObject weatherRuntime;
        private readonly Camera weatherCamera;
        private readonly RenderTexture weatherTexture;
        private readonly ParticleSystem[] rainLayers = new ParticleSystem[3];
        private readonly float[] baseEmission = { 18f, 18f, 16f };
        private readonly Material rainMaterial;
        private readonly Texture2D rainTexture;
        private readonly Texture2D wetGlassTexture;
        private readonly Texture2D radialTexture;
        private readonly System.Random stormRandom = new System.Random(20260909);
        private float time;
        private float impact;
        private float glitch;
        private float nextLightning;
        private float lightningAge = -1f;
        private float thunderCountdown = -1f;
        private int lightningSerial;
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

            darkness = ui.Box(parent, "LampMood", 0, 0, 1920, 1080, Color.clear);
            portalLight = ui.Box(parent, "PortalBreath", 0, 100, 1920, 906, Color.clear);

            weatherCanvasRoot = ui.Rect(parent, "WindowRain", 350, 107, 605, 396);
            weatherCanvasRoot.gameObject.AddComponent<RectMask2D>();

            weatherTexture = new RenderTexture(768, 512, 0, RenderTextureFormat.ARGB32)
            {
                name = "HomeWeatherRenderTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            weatherTexture.Create();
            weatherComposite = ui.Art(weatherCanvasRoot, "HomeWeatherComposite", weatherTexture, 0, 0, 605, 396);
            weatherComposite.color = new Color(.86f, .95f, 1f, .84f);

            radialTexture = CreateRadialTexture();
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

            rainTexture = CreateRainTexture();
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

            rainLayers[0] = CreateRainLayer("HomeRainFar", 48, 2.7f, 4.3f, .035f, .34f, 0);
            rainLayers[1] = CreateRainLayer("HomeRainMid", 36, 1.85f, 6.4f, .055f, .48f, 1);
            rainLayers[2] = CreateRainLayer("HomeRainNear", 24, 1.35f, 8.8f, .085f, .64f, 2);
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
            weatherComposite.rectTransform.sizeDelta = weatherCanvasRoot.sizeDelta;
            stormFlash.rectTransform.sizeDelta = weatherCanvasRoot.sizeDelta;
            weatherCamera.aspect = home ? 605f / 396f : 1920f / 906f;
            ResizeEmitterWidth();
            if (weatherVisible)
            {
                for (int i = 0; i < rainLayers.Length; i++)
                    if (rainLayers[i] != null && !rainLayers[i].isPlaying) rainLayers[i].Play(true);
            }

            bool homeDetails = home && atmosphere.enabled;
            wetGlass.gameObject.SetActive(homeDetails);
            exteriorHaze.gameObject.SetActive(homeDetails);
            stormFlash.gameObject.SetActive(homeDetails);
            coldWindowBounce.gameObject.SetActive(homeDetails);
            warmInteriorGlow.gameObject.SetActive(homeDetails);
            steamRoot.gameObject.SetActive(home);
            audio.SetHomeWeatherMix(home ? atmosphere.ambientRainVolume : 1f);
            if (home && nextLightning <= 0f) ScheduleNextLightning();
            if (!home)
            {
                lightningAge = -1f;
                thunderCountdown = -1f;
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
            ApplyLighting(lampOn);

            portalLight.color = new Color(.24f, .47f, .6f,
                atPortal ? .018f + Mathf.Sin(time * .8f) * .011f : 0);
            float shake = settings.screenShake ? impact * 17 : 0;
            float distortion = glitch * Mathf.Clamp(settings.glitchIntensity, 0, 1.5f);
            background.anchoredPosition = new Vector2(
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
            if (rainTexture) Object.Destroy(rainTexture);
            if (wetGlassTexture) Object.Destroy(wetGlassTexture);
            if (radialTexture) Object.Destroy(radialTexture);
            if (atmosphere && (atmosphere.hideFlags & HideFlags.DontSave) != 0) Object.Destroy(atmosphere);
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
            renderer.velocityScale = .12f;
            renderer.lengthScale = 2.1f + sortingOrder * .55f;
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
            float intensity = Mathf.Clamp(atmosphere.rainIntensity, 0f, 1.5f);
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
            weatherComposite.color = new Color(.86f, .95f, 1f, Mathf.Clamp01(.55f + intensity * .34f));
            audio.SetHomeWeatherMix(atHome ? atmosphere.ambientRainVolume : 1f);
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
            stormFlash.color = new Color(.78f, .90f, 1f,
                Mathf.Clamp01(.38f * atmosphere.lightningIntensity));
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
            darkness.color = new Color(.01f, .045f, .07f,
                atHome && !lampOn ? Mathf.Clamp01(.64f - flash * .10f + slowPulse * .015f) : 0f);

            if (!atHome)
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

        private static Texture2D CreateRainTexture()
        {
            const int width = 8;
            const int height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "HomeRainStreakTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float fy = (y + .5f) / height;
                float endFade = Mathf.Sin(fy * Mathf.PI);
                for (int x = 0; x < width; x++)
                {
                    float fx = Mathf.Abs((x + .5f) / width - .5f) * 2f;
                    float side = Mathf.Pow(Mathf.Clamp01(1f - fx), 2.2f);
                    byte alpha = (byte)(Mathf.Clamp01(side * endFade) * 220f);
                    pixels[y * width + x] = new Color32(205, 235, 248, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateWetGlassTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HomeWetGlassTexture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            var random = new System.Random(99173);
            for (int streak = 0; streak < 22; streak++)
            {
                int x = random.Next(2, size - 2);
                int y0 = random.Next(0, size);
                int length = random.Next(18, 72);
                int width = random.Next(1, 3);
                for (int dy = 0; dy < length; dy++)
                {
                    int y = (y0 + dy) % size;
                    float fade = 1f - dy / (float)length;
                    for (int dx = -width; dx <= width; dx++)
                    {
                        int px = Mathf.Clamp(x + dx, 0, size - 1);
                        int index = y * size + px;
                        byte alpha = (byte)(Mathf.Clamp01(fade * (dx == 0 ? .50f : .18f)) * 255f);
                        if (alpha > pixels[index].a) pixels[index] = new Color32(220, 242, 248, alpha);
                    }
                }
            }
            for (int drop = 0; drop < 34; drop++)
            {
                int cx = random.Next(2, size - 2);
                int cy = random.Next(2, size - 2);
                int radius = random.Next(1, 4);
                for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                {
                    float distance = Mathf.Sqrt(x * x + y * y) / Mathf.Max(1f, radius);
                    if (distance > 1f) continue;
                    int index = (cy + y) * size + (cx + x);
                    byte alpha = (byte)(Mathf.Clamp01((1f - distance) * .34f) * 255f);
                    if (alpha > pixels[index].a) pixels[index] = new Color32(225, 246, 250, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateRadialTexture()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HomeAtmosphereSoftGlow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + .5f) / size - .5f) * 2f;
                float ny = ((y + .5f) / size - .5f) * 2f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.1f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
'''

audio = r'''using Rokas.Core;
using UnityEngine;

namespace Rokas.Presentation
{
    public sealed class RokasAudio
    {
        private readonly RokasAssets assets;
        private readonly SettingsData settings;
        private readonly RokasBootstrap bootstrap;
        private readonly AudioSource ambience;
        private readonly AudioSource music;
        private readonly AudioSource[] effects = new AudioSource[4];
        private readonly AudioClip messageArrive;
        private readonly AudioClip reactionCue;
        private AudioClip[] thunderBank;
        private float homeWeatherMix = 1f;
        private int voice;
        private bool mission;
        private bool hasLocation;
        private bool laptopMode;
        private bool lampStateKnown;
        private bool lampOn;

        public RokasAudio(GameObject parent, RokasAssets assets, SettingsData settings)
        {
            this.assets = assets;
            this.settings = settings;
            bootstrap = parent.GetComponent<RokasBootstrap>();
            if (bootstrap && bootstrap.Session != null)
            {
                lampOn = bootstrap.Session.State.lampOn;
                lampStateKnown = true;
            }

            var audioRoot = new GameObject("Audio");
            audioRoot.transform.SetParent(parent.transform, false);
            ambience = MakeSource(audioRoot, true);
            music = MakeSource(audioRoot, true);
            for (int i = 0; i < effects.Length; i++) effects[i] = MakeSource(audioRoot, false);
            messageArrive = Resources.Load<AudioClip>("Messages/Audio/MessageArrive");
            reactionCue = Resources.Load<AudioClip>("Messages/Audio/Reaction");
        }

        private static AudioSource MakeSource(GameObject root, bool loop)
        {
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.volume = 0;
            return source;
        }

        public void SetLocation(bool isMission)
        {
            if (hasLocation && mission == isMission) return;
            mission = isMission;
            hasLocation = true;
            ambience.Stop();
            music.Stop();
            ambience.clip = mission ? assets.subwayAmbience : assets.homeAmbience;
            music.clip = mission ? assets.missionMusic : assets.homeMusic;
            ambience.volume = music.volume = 0;
            if (ambience.clip) ambience.Play();
            if (music.clip) music.Play();
        }

        public void SetHomeWeatherMix(float value)
        {
            homeWeatherMix = Mathf.Clamp(value, .15f, 1f);
        }

        public void SetLaptopMode(bool active) { laptopMode = active; }

        public void Tick(float dt, bool focused)
        {
            float master = focused ? Mathf.Clamp01(settings.masterVolume) : 0;
            float weather = mission ? 1f : homeWeatherMix;
            ambience.volume = Mathf.MoveTowards(ambience.volume, master * settings.sfxVolume * .65f * weather, dt);
            music.volume = Mathf.MoveTowards(music.volume, master * settings.musicVolume * .6f, dt);

            if (bootstrap && bootstrap.Session != null)
            {
                bool currentLampOn = bootstrap.Session.State.lampOn;
                if (!lampStateKnown)
                {
                    lampOn = currentLampOn;
                    lampStateKnown = true;
                }
                else if (currentLampOn != lampOn)
                {
                    lampOn = currentLampOn;
                    Play(lampOn ? assets.lampOn : assets.lampOff);
                }
            }
        }

        public void Play(AudioClip clip)
        {
            if (!clip) return;
            var source = effects[voice++ % effects.Length];
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(settings.masterVolume * settings.sfxVolume);
            source.Play();
        }

        public void PlayMessageCue(string cue)
        {
            if (string.Equals(cue, "Reaction", System.StringComparison.Ordinal))
            {
                PlayScaled(reactionCue ? reactionCue : assets.click, .72f);
                return;
            }
            if (string.Equals(cue, "PlayerSend", System.StringComparison.Ordinal))
            {
                PlayScaled(assets.laptopMouseClick ? assets.laptopMouseClick : assets.click, .30f);
                return;
            }

            AudioClip clip = messageArrive ? messageArrive : (assets.laptopMouseClick ? assets.laptopMouseClick : assets.click);
            float scale = cue == "WorldNotification" ? .92f :
                cue == "LaptopNotification" ? .84f :
                cue == "ActiveReceive" ? .76f :
                cue == "SoftReceive" ? .40f : .70f;
            PlayScaled(clip, scale);
        }

        public void PlayThunder(int variant, float volume)
        {
            EnsureThunderBank();
            int index = Mathf.Abs(variant) % thunderBank.Length;
            PlayScaled(thunderBank[index], Mathf.Clamp01(volume) * .72f);
        }

        private void EnsureThunderBank()
        {
            if (thunderBank != null) return;
            thunderBank = new AudioClip[3];
            for (int i = 0; i < thunderBank.Length; i++) thunderBank[i] = BuildThunder(i);
        }

        private static AudioClip BuildThunder(int variant)
        {
            const int rate = 22050;
            float duration = 2.7f + variant * .42f;
            int count = Mathf.CeilToInt(rate * duration);
            var samples = new float[count];
            uint state = (uint)(0xA341316Cu + variant * 0x9E3779B9u);
            float low = 0f;
            float low2 = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float noise = ((state & 0xFFFFu) / 32767.5f) - 1f;
                low += (noise - low) * (.018f + variant * .002f);
                low2 += (low - low2) * .055f;
                float attack = Mathf.Clamp01(t / (.055f + variant * .018f));
                float decay = Mathf.Exp(-t / (1.15f + variant * .30f));
                float body = Mathf.Sin(2f * Mathf.PI * (31f + variant * 3f) * t) * .17f +
                             Mathf.Sin(2f * Mathf.PI * (47f + variant * 2f) * t) * .08f;
                float distantCrack = variant == 1 ? Mathf.Exp(-Mathf.Pow((t - .17f) / .10f, 2f)) * .08f : 0f;
                samples[i] = Mathf.Clamp((low2 * 1.85f + body + distantCrack) * attack * decay, -.72f, .72f);
            }
            AudioClip clip = AudioClip.Create("HomeThunderDistant0" + (variant + 1), count, 1, rate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayScaled(AudioClip clip, float scale)
        {
            if (!clip) return;
            var source = effects[voice++ % effects.Length];
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(settings.masterVolume * settings.sfxVolume * Mathf.Clamp01(scale));
            source.Play();
        }

        public void Click()
        {
            if (!laptopMode) Play(assets.click);
        }

        public void LaptopMouseClick() { Play(assets.laptopMouseClick); }

        public void Dispose()
        {
            if (thunderBank == null) return;
            for (int i = 0; i < thunderBank.Length; i++)
                if (thunderBank[i]) Object.Destroy(thunderBank[i]);
            thunderBank = null;
        }
    }
}
'''

world_path.write_text(world, encoding="utf-8")
audio_path.write_text(audio, encoding="utf-8")
view_path.write_text(view, encoding="utf-8")
boot_path.write_text(boot, encoding="utf-8")
profile_path.write_text(profile, encoding="utf-8")
profile_meta_path.write_text(profile_meta, encoding="utf-8")
asset_path.write_text(profile_asset, encoding="utf-8")
asset_meta_path.write_text(asset_meta, encoding="utf-8")
print("Applied scoped Home Hub atmosphere/weather/lighting production patch")
