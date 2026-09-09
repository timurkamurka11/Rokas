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
