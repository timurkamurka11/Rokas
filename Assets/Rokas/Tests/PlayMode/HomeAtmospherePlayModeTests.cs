using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class HomeAtmospherePlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator HomeBuildsLayeredParticleRainAndWindowDepth()
        {
            Initialize("rokas-home-atmosphere-");
            yield return null;

            ParticleSystem far = FindParticle("HomeRainFar");
            ParticleSystem mid = FindParticle("HomeRainMid");
            ParticleSystem near = FindParticle("HomeRainNear");

            Assert.That(far, Is.Not.Null, "Home rain must include a real far ParticleSystem layer");
            Assert.That(mid, Is.Not.Null, "Home rain must include a real mid ParticleSystem layer");
            Assert.That(near, Is.Not.Null, "Home rain must include a real near ParticleSystem layer");
            Assert.That(far.main.maxParticles, Is.LessThanOrEqualTo(48), "far rain must keep a bounded particle budget");
            Assert.That(mid.main.maxParticles, Is.LessThanOrEqualTo(36), "mid rain must keep a bounded particle budget");
            Assert.That(near.main.maxParticles, Is.LessThanOrEqualTo(24), "near rain must keep a bounded particle budget");
            Assert.That(far.isPlaying && mid.isPlaying && near.isPlaying, Is.True,
                "all three rain layers must actively simulate in the Home Hub");

            Assert.That(FindRect("HomeWeatherComposite"), Is.Not.Null,
                "particle rain must be composited into the existing home window rather than drawn as UI sticks");
            Assert.That(FindRect("HomeWetGlass"), Is.Not.Null, "window must have a restrained wet-glass layer");
            Assert.That(FindRect("HomeExteriorHaze"), Is.Not.Null, "window must carry subtle exterior haze/depth");
            Assert.That(FindRect("HomeColdWindowBounce"), Is.Not.Null, "window must contribute cold night bounce");
            Assert.That(FindRect("HomeWarmInteriorGlow"), Is.Not.Null, "home must keep a subtle warm interior counter-light");
            Assert.That(FindRect("HomeStormFlash"), Is.Not.Null, "home must expose a localized storm flash surface");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ProfileIsSerializedAndStormRoutesOneDelayedThunderCue()
        {
            Initialize("rokas-home-storm-");
            yield return null;

            UnityEngine.Object profile = Resources.Load("HomeAtmosphereProfile");
            Assert.That(profile, Is.Not.Null, "Home atmosphere tunables must live in a serialized Resources profile");
            AssertField(profile, "rainIntensity");
            AssertField(profile, "rainSpeed");
            AssertField(profile, "lightningMinInterval");
            AssertField(profile, "lightningMaxInterval");
            AssertField(profile, "lightningIntensity");
            AssertField(profile, "thunderVolume");
            AssertField(profile, "ambientRainVolume");
            AssertField(profile, "hazeIntensity");
            AssertField(profile, "wetGlassIntensity");
            AssertField(profile, "parallaxIntensity");
            AssertField(profile, "outsideMotionIntensity");
            AssertField(profile, "weatherReadability");

            object effects = typeof(RokasView)
                .GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(Bootstrap().View);
            Assert.That(effects, Is.Not.Null);
            MethodInfo trigger = effects.GetType().GetMethod("ForceLightning", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(trigger, Is.Not.Null,
                "atmosphere owner must expose a tuning/debug lightning trigger without adding gameplay UI");

            int beforeThunderSources = CountAssignedThunderSources();
            trigger.Invoke(effects, null);
            yield return null;

            Graphic flash = FindGraphic("HomeStormFlash");
            Assert.That(flash, Is.Not.Null);
            Assert.That(flash, Is.TypeOf<RawImage>(),
                "lightning must use a soft textured environmental surface rather than a flat rectangular Image box");
            Assert.That(((RawImage)flash).texture, Is.Not.Null,
                "soft lightning surface must have a falloff texture");
            Assert.That(flash.color.a, Is.GreaterThan(.01f), "forced lightning must visibly affect the home window immediately");
            Assert.That(CountAssignedThunderSources(), Is.EqualTo(beforeThunderSources),
                "thunder must be delayed rather than firing synchronously with the flash");

            for (int index = 0; index < 40; index++)
            {
                Bootstrap().View.Tick(.10f);
                yield return null;
            }

            Assert.That(CountAssignedThunderSources(), Is.EqualTo(beforeThunderSources + 1),
                "one lightning event must schedule exactly one distant-thunder cue");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HomeBuildsLogical25DPlanesAndReadableDepth()
        {
            Initialize("rokas-home-25d-layers-");
            yield return null;

            RawImage world = FindRawImage("WorldIllustration");
            RawImage outside = FindRawImage("HomeOutsideParallax");
            RawImage foreground = FindRawImage("HomeForegroundDepth");
            RawImage mistNear = FindRawImage("HomeExteriorMistNear");
            RawImage wetStreaks = FindRawImage("HomeWetGlassStreaks");

            Assert.That(world, Is.Not.Null);
            Assert.That(outside, Is.Not.Null,
                "Home must isolate the existing outside/window art into its own 2.5D plane");
            Assert.That(outside.texture, Is.SameAs(world.texture),
                "outside plane must reuse the existing Home illustration instead of replacing the art style");
            Assert.That(foreground, Is.Not.Null,
                "Home must include a restrained near-depth visual plane without moving UI");
            Assert.That(mistNear, Is.Not.Null,
                "outside atmosphere must contain a second independently drifting depth plane");
            Assert.That(wetStreaks, Is.Not.Null,
                "wet glass must include a second sparse moving streak layer for readable rain-on-glass motion");
            Assert.That(FindRect("HomeStormRoomLift"), Is.Not.Null,
                "storm lightning must have a room-wide environmental response layer");

            ParticleSystem far = FindParticle("HomeRainFar");
            ParticleSystem mid = FindParticle("HomeRainMid");
            ParticleSystem near = FindParticle("HomeRainNear");
            Assert.That(far.main.startSize.constant, Is.LessThan(mid.main.startSize.constant));
            Assert.That(mid.main.startSize.constant, Is.LessThan(near.main.startSize.constant));
            Assert.That(near.main.startSize.constant, Is.GreaterThanOrEqualTo(far.main.startSize.constant * 3f),
                "near rain must be materially larger than far rain so depth reads at a glance");

            ParticleSystemRenderer farRenderer = far.GetComponent<ParticleSystemRenderer>();
            ParticleSystemRenderer midRenderer = mid.GetComponent<ParticleSystemRenderer>();
            ParticleSystemRenderer nearRenderer = near.GetComponent<ParticleSystemRenderer>();
            Assert.That(farRenderer.lengthScale, Is.LessThan(midRenderer.lengthScale));
            Assert.That(midRenderer.lengthScale, Is.LessThan(nearRenderer.lengthScale));
            Assert.That(nearRenderer.lengthScale, Is.GreaterThanOrEqualTo(farRenderer.lengthScale * 1.8f),
                "near rain streak length must separate clearly from far rain");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HybridParallaxMovesOnlyVisualPlanesAndStaysBounded()
        {
            Initialize("rokas-home-25d-parallax-");
            yield return null;

            RectTransform world = FindRect("WorldIllustration");
            RectTransform outside = FindRect("HomeOutsideParallax");
            RectTransform foreground = FindRect("HomeForegroundDepth");
            RectTransform wet = FindRect("HomeWetGlass");
            RectTransform haze = FindRect("HomeExteriorHaze");
            RectTransform laptop = FindRect("LaptopHotspot");
            Assert.That(world, Is.Not.Null);
            Assert.That(outside, Is.Not.Null);
            Assert.That(foreground, Is.Not.Null);
            Assert.That(wet, Is.Not.Null);
            Assert.That(haze, Is.Not.Null);
            Assert.That(laptop, Is.Not.Null);

            Vector2 worldStart = world.anchoredPosition;
            Vector2 outsideStart = outside.anchoredPosition;
            Vector2 foregroundStart = foreground.anchoredPosition;
            Vector2 wetStart = wet.anchoredPosition;
            Vector2 hazeStart = haze.anchoredPosition;
            Vector2 laptopStart = laptop.anchoredPosition;
            Rect wetUvStart = FindRawImage("HomeWetGlass").uvRect;

            for (int index = 0; index < 300; index++)
                Bootstrap().View.Tick(1f / 60f);
            yield return null;

            Vector2 worldDelta = world.anchoredPosition - worldStart;
            Vector2 outsideDelta = outside.anchoredPosition - outsideStart;
            Vector2 foregroundDelta = foreground.anchoredPosition - foregroundStart;
            Vector2 wetDelta = wet.anchoredPosition - wetStart;
            Vector2 hazeDelta = haze.anchoredPosition - hazeStart;

            Assert.That(worldDelta.magnitude, Is.GreaterThan(.03f),
                "room/base plane must have a barely perceptible ambient drift");
            Assert.That(worldDelta.magnitude, Is.LessThanOrEqualTo(2.25f),
                "room/base parallax must stay subtle");
            Assert.That(outsideDelta.magnitude, Is.LessThanOrEqualTo(2.25f),
                "outside/far parallax must remain almost imperceptible");
            Assert.That(foregroundDelta.magnitude, Is.GreaterThan(worldDelta.magnitude),
                "near depth plane should move a little more than the room base");
            Assert.That(foregroundDelta.magnitude, Is.LessThanOrEqualTo(4.25f),
                "near parallax must remain controlled, never wallpaper wobble");
            Assert.That(Vector2.Distance(outsideDelta, foregroundDelta), Is.GreaterThan(.05f),
                "far and near planes must not share one flat motion vector");
            Assert.That(wetDelta.magnitude, Is.LessThanOrEqualTo(4.5f));
            Assert.That(hazeDelta.magnitude, Is.LessThanOrEqualTo(18f));
            Assert.That(Mathf.Abs(FindRawImage("HomeWetGlass").uvRect.y - wetUvStart.y), Is.GreaterThan(.01f),
                "wet glass must visibly slide over time");
            Assert.That(laptop.anchoredPosition, Is.EqualTo(laptopStart),
                "Home interaction/UI roots must remain stationary while visual planes move");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RepeatedTicksReuseTheBoundedWeatherRig()
        {
            Initialize("rokas-home-weather-pool-");
            yield return null;

            int particleSystems = root.GetComponentsInChildren<ParticleSystem>(true).Length;
            int cameras = CountNamedComponents<Camera>("HomeWeatherCamera");
            int composites = CountNamedComponents<RawImage>("HomeWeatherComposite");

            for (int index = 0; index < 300; index++)
                Bootstrap().View.Tick(1f / 60f);
            yield return null;

            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(particleSystems),
                "weather Tick must reuse ParticleSystems instead of allocating new emitters");
            Assert.That(CountNamedComponents<Camera>("HomeWeatherCamera"), Is.EqualTo(cameras),
                "weather Tick must reuse its offscreen camera");
            Assert.That(CountNamedComponents<RawImage>("HomeWeatherComposite"), Is.EqualTo(composites),
                "weather Tick must reuse the composite surface");
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize(string prefix)
        {
            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("HomeAtmosphereFixture");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
        }

        private RokasBootstrap Bootstrap()
        {
            RokasBootstrap boot = root != null ? root.GetComponent<RokasBootstrap>() : null;
            Assert.That(boot, Is.Not.Null);
            return boot;
        }

        private ParticleSystem FindParticle(string name)
        {
            if (root == null) return null;
            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
                if (system.name == name) return system;
            return null;
        }

        private RectTransform FindRect(string name)
        {
            if (root == null) return null;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            return null;
        }

        private Graphic FindGraphic(string name)
        {
            RectTransform rect = FindRect(name);
            return rect != null ? rect.GetComponent<Graphic>() : null;
        }

        private RawImage FindRawImage(string name)
        {
            RectTransform rect = FindRect(name);
            return rect != null ? rect.GetComponent<RawImage>() : null;
        }

        private int CountAssignedThunderSources()
        {
            int count = 0;
            if (root == null) return count;
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip != null && source.clip.name.StartsWith("HomeThunder", StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private int CountNamedComponents<T>(string name) where T : Component
        {
            int count = 0;
            if (root == null) return count;
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) count++;
            return count;
        }

        private static void AssertField(UnityEngine.Object value, string fieldName)
        {
            FieldInfo field = value.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "missing serialized atmosphere tunable: " + fieldName);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
