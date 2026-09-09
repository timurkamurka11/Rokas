using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class HomeWeather3PlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator LampOffDoesNotUseFullStageFlatDarknessCard()
        {
            Initialize("rokas-weather3-darkness-");
            yield return null;

            RectTransform lampMood = FindRect("LampMood");
            if (lampMood == null)
            {
                Assert.Pass("Weather 3 may remove the legacy LampMood surface entirely.");
                yield break;
            }

            Image flatImage = lampMood.GetComponent<Image>();
            Vector2 size = lampMood.rect.size;
            bool isFullStageFlatCard = flatImage != null && size.x >= 1800f && size.y >= 1000f;

            Assert.That(isFullStageFlatCard, Is.False,
                "Home OFF lighting must not depend on the legacy full-stage flat LampMood Image card.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WetGlassAndAtmosphereUseIndependentTextureSources()
        {
            Initialize("rokas-weather3-sources-");
            yield return null;

            RawImage droplets = FindRawImage("HomeWetGlassDroplets");
            RawImage streaks = FindRawImage("HomeWetGlassStreaks");
            RawImage haze = FindRawImage("HomeExteriorHaze");
            RawImage mist = FindRawImage("HomeExteriorMistNear");
            RawImage storm = FindRawImage("HomeStormFlash");

            Assert.That(droplets, Is.Not.Null,
                "Weather 3 wet glass must expose a dedicated droplet layer instead of the legacy shared procedural surface.");
            Assert.That(streaks, Is.Not.Null);
            Assert.That(haze, Is.Not.Null);
            Assert.That(mist, Is.Not.Null);
            Assert.That(storm, Is.Not.Null);

            Assert.That(droplets.texture, Is.Not.Null);
            Assert.That(streaks.texture, Is.Not.Null);
            Assert.That(haze.texture, Is.Not.Null);
            Assert.That(mist.texture, Is.Not.Null);
            Assert.That(storm.texture, Is.Not.Null);

            Assert.That(droplets.texture, Is.Not.SameAs(streaks.texture),
                "droplets and streaks must use independent wet-glass source textures");
            Assert.That(haze.texture, Is.Not.SameAs(mist.texture),
                "distant haze and near mist must not reuse one procedural primitive");

            Assert.That(droplets.texture.name, Is.Not.EqualTo("HomeWetGlassTexture"));
            Assert.That(streaks.texture.name, Is.Not.EqualTo("HomeWetGlassTexture"));
            Assert.That(haze.texture.name, Is.Not.EqualTo("HomeAtmosphereSoftGlow"));
            Assert.That(mist.texture.name, Is.Not.EqualTo("HomeAtmosphereSoftGlow"));
            Assert.That(storm.texture.name, Is.Not.EqualTo("HomeAtmosphereSoftGlow"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ForcedLightningUsesMultipleIndependentEnvironmentalSources()
        {
            Initialize("rokas-weather3-storm-");
            yield return null;

            RawImage storm = FindRawImage("HomeStormFlash");
            RawImage haze = FindRawImage("HomeExteriorHaze");
            RawImage mist = FindRawImage("HomeExteriorMistNear");
            RawImage cold = FindRawImage("HomeColdWindowBounce");
            RawImage roomLift = FindRawImage("HomeStormRoomLift");

            Assert.That(storm, Is.Not.Null);
            Assert.That(haze, Is.Not.Null);
            Assert.That(mist, Is.Not.Null);
            Assert.That(cold, Is.Not.Null);
            Assert.That(roomLift, Is.Not.Null);

            float stormBefore = storm.color.a;
            float hazeBefore = haze.color.a;
            float mistBefore = mist.color.a;
            float coldBefore = cold.color.a;
            float roomBefore = roomLift.color.a;

            ForceLightning(Bootstrap().View);
            Bootstrap().View.Tick(.03f);
            yield return null;

            int changedResponders = 0;
            if (storm.color.a > stormBefore + .001f) changedResponders++;
            if (haze.color.a > hazeBefore + .001f) changedResponders++;
            if (mist.color.a > mistBefore + .001f) changedResponders++;
            if (cold.color.a > coldBefore + .001f) changedResponders++;
            if (roomLift.color.a > roomBefore + .001f) changedResponders++;
            Assert.That(changedResponders, Is.GreaterThanOrEqualTo(4),
                "one lightning event must drive several environmental response layers, not only one flash Graphic");

            var sources = new HashSet<Texture>();
            AddTexture(sources, storm);
            AddTexture(sources, haze);
            AddTexture(sources, mist);
            AddTexture(sources, cold);
            AddTexture(sources, roomLift);
            Assert.That(sources.Count, Is.GreaterThanOrEqualTo(3),
                "Weather 3 storm lighting must use multiple independent spatial masks instead of one shared radial primitive");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Weather3PreservesBoundedRigAndStationaryLaptopHotspot()
        {
            Initialize("rokas-weather3-safety-");
            yield return null;

            ParticleSystem far = FindParticle("HomeRainFar");
            ParticleSystem mid = FindParticle("HomeRainMid");
            ParticleSystem near = FindParticle("HomeRainNear");
            RectTransform laptop = FindRect("LaptopHotspot");

            Assert.That(far, Is.Not.Null);
            Assert.That(mid, Is.Not.Null);
            Assert.That(near, Is.Not.Null);
            Assert.That(laptop, Is.Not.Null);
            Assert.That(far.main.maxParticles, Is.LessThanOrEqualTo(48));
            Assert.That(mid.main.maxParticles, Is.LessThanOrEqualTo(36));
            Assert.That(near.main.maxParticles, Is.LessThanOrEqualTo(24));

            int particleSystems = root.GetComponentsInChildren<ParticleSystem>(true).Length;
            int weatherCameras = CountNamedComponents<Camera>("HomeWeatherCamera");
            int composites = CountNamedComponents<RawImage>("HomeWeatherComposite");
            Vector2 laptopStart = laptop.anchoredPosition;

            for (int index = 0; index < 300; index++)
                Bootstrap().View.Tick(1f / 60f);
            yield return null;

            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(particleSystems));
            Assert.That(CountNamedComponents<Camera>("HomeWeatherCamera"), Is.EqualTo(weatherCameras));
            Assert.That(CountNamedComponents<RawImage>("HomeWeatherComposite"), Is.EqualTo(composites));
            Assert.That(laptop.anchoredPosition, Is.EqualTo(laptopStart),
                "Weather 3 visual motion must never move the LaptopHotspot interaction root");
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize(string prefix)
        {
            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("HomeWeather3Fixture");
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

        private RawImage FindRawImage(string name)
        {
            RectTransform rect = FindRect(name);
            return rect != null ? rect.GetComponent<RawImage>() : null;
        }

        private int CountNamedComponents<T>(string name) where T : Component
        {
            int count = 0;
            if (root == null) return count;
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) count++;
            return count;
        }

        private static void ForceLightning(RokasView view)
        {
            object effects = typeof(RokasView)
                .GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(view);
            Assert.That(effects, Is.Not.Null);
            MethodInfo trigger = effects.GetType().GetMethod("ForceLightning", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(trigger, Is.Not.Null);
            trigger.Invoke(effects, null);
        }

        private static void AddTexture(ISet<Texture> textures, RawImage image)
        {
            if (image != null && image.texture != null) textures.Add(image.texture);
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
