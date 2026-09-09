using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Core;
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
        public IEnumerator LampButtonControlsBakedInteriorAndRestoresApprovedOnArt()
        {
            Initialize("rokas-interior-art-");
            yield return null;
            RokasBootstrap boot = Bootstrap();
            boot.enabled = false;
            RawImage background = FindRawImage("WorldIllustration");
            Texture on = background.texture;
            Assert.That(boot.Session.State.lampOn, Is.True);
            boot.View.Tick(.3f);
            CaptureLighting("light-on-normal");

            Press("LampHotspot");
            boot.View.Tick(.1f);
            CaptureLighting("light-off-normal");
            Texture off = background.texture;

            ForceLightning(boot.View);
            boot.View.Tick(.055f);
            CaptureLighting("light-off-lightning");
            boot.View.Tick(.8f);
            boot.View.Tick(2f);
            CaptureLighting("light-off-idle");

            Press("LampHotspot");
            boot.View.Tick(0);
            ForceLightning(boot.View);
            boot.View.Tick(.055f);
            CaptureLighting("light-on-lightning");
            boot.View.Tick(.8f);

            // Reusing the ON illustration while OFF leaves the baked lamps lit.
            Assert.That(off, Is.Not.SameAs(on), "OFF must remove the baked practical light from the room artwork.");
            Assert.That(off, Is.SameAs(Resources.Load<Texture2D>("Home/ApartmentNightLightOff")));
            Assert.That(background.texture, Is.SameAs(on), "ON must restore the approved source art.");
            Assert.That(FindRawImage("HomeWarmInteriorGlow").color.a, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LampOffZeroesWarmContributionWhileWeatherAndLightningRemainIndependent()
        {
            Initialize("rokas-interior-weather-");
            yield return null;
            RokasBootstrap boot = Bootstrap();
            boot.enabled = false;
            Press("LampHotspot");
            boot.View.Tick(0);
            RawImage warm = FindRawImage("HomeWarmInteriorGlow");
            RawImage cold = FindRawImage("HomeColdWindowBounce");
            RawImage room = FindRawImage("HomeStormRoomLift");
            Assert.That(warm.color.a, Is.Zero, "OFF must leave no residual warm practical contribution.");
            float coldBefore = cold.color.a;
            Assert.That(coldBefore, Is.GreaterThan(0));
            Assert.That(FindRawImage("HomeWetGlassDroplets").color.a, Is.GreaterThan(0));
            Assert.That(FindRawImage("HomeExteriorMistNear").color.a, Is.GreaterThan(0));
            Assert.That(FindParticle("HomeRainFar").isPlaying, Is.True);
            Assert.That(FindParticle("HomeRainMid").isPlaying, Is.True);
            Assert.That(FindParticle("HomeRainNear").isPlaying, Is.True);
            Texture off = FindRawImage("WorldIllustration").texture;
            ForceLightning(boot.View);
            boot.View.Tick(.055f);
            Assert.That(cold.color.a, Is.GreaterThan(coldBefore));
            Assert.That(room.color.a, Is.GreaterThan(0));
            Assert.That(warm.color.a, Is.Zero, "Lightning belongs to cold exterior lighting, never the switched-off practical.");
            boot.View.Tick(.8f);
            Assert.That(room.color.a, Is.Zero);
            Assert.That(boot.Session.State.lampOn, Is.False);
            Assert.That(FindRawImage("WorldIllustration").texture, Is.SameAs(off));
            Assert.That(warm.color.a, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LampOffSurvivesMessagesCombatReturnAndSaveReloadWithoutDuplicatingLayers()
        {
            Initialize("rokas-interior-lifecycle-");
            yield return null;
            RokasBootstrap boot = Bootstrap();
            Press("LampHotspot");
            boot.View.Tick(0);
            Texture off = FindRawImage("WorldIllustration").texture;
            int graphics = CountNamedComponents<RawImage>("HomeWarmInteriorGlow");
            int cameras = CountNamedComponents<Camera>("HomeWeatherCamera");
            Press("LaptopHotspot");
            Press("LaptopMessages");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(FindRect("MessagesRoot"), Is.Not.Null);
            boot.View.Escape();
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(boot.View.LaptopOpen, Is.False);
            Assert.That(boot.Session.State.lampOn, Is.False);
            Assert.That(FindRawImage("WorldIllustration").texture, Is.SameAs(off));

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            boot.View.Tick(0);
            Assert.That(FindRawImage("WorldIllustration").texture,
                Is.SameAs(Resources.Load<RokasAssets>("RokasAssets").portal));
            Assert.That(boot.Session.EnterPortal(), Is.True);
            boot.View.Tick(0);
            Assert.That(FindRawImage("WorldIllustration").texture,
                Is.SameAs(Resources.Load<RokasAssets>("RokasAssets").subway));
            Assert.That(FindRawImage("HomeWarmInteriorGlow").gameObject.activeSelf, Is.False);
            boot.Session.State.enemyHp = 1;
            Assert.That(boot.Session.ClickAttack(false), Is.True);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Sealed));
            Assert.That(boot.Session.ReturnHome(), Is.True);
            boot.View.Tick(0);
            Assert.That(boot.Session.State.lampOn, Is.False);
            Assert.That(FindRawImage("WorldIllustration").texture, Is.SameAs(off));
            Assert.That(FindRawImage("HomeWarmInteriorGlow").color.a, Is.Zero);
            Assert.That(CountNamedComponents<RawImage>("HomeWarmInteriorGlow"), Is.EqualTo(graphics));
            Assert.That(CountNamedComponents<Camera>("HomeWeatherCamera"), Is.EqualTo(cameras));
            boot.SaveNow();
            UnityEngine.Object.Destroy(root);
            yield return null;
            root = new GameObject("HomeLightingReloadFixture");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
            yield return null;
            Assert.That(Bootstrap().Session.State.lampOn, Is.False);
            Assert.That(FindRawImage("WorldIllustration").texture,
                Is.SameAs(Resources.Load<Texture2D>("Home/ApartmentNightLightOff")));
            Assert.That(FindRawImage("HomeWarmInteriorGlow").color.a, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private void Press(string name)
        {
            Button button = FindRect(name)?.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }

        private void CaptureLighting(string name)
        {
            string output = Environment.GetEnvironmentVariable("ROKAS_LIGHT_CAPTURE_DIR");
            if (string.IsNullOrEmpty(output)) return;
            Assert.That(SystemInfo.graphicsDeviceType,
                Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null), "Visual proof requires graphics.");
            Directory.CreateDirectory(output);
            var inventory = new System.Text.StringBuilder();
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                inventory.AppendLine(graphic.transform.parent.name + "/" + graphic.name +
                    " active=" + graphic.gameObject.activeInHierarchy + " color=" + graphic.color +
                    " texture=" + (graphic.mainTexture ? graphic.mainTexture.name : "null"));
            File.WriteAllText(Path.Combine(output, name + "-hierarchy.txt"), inventory.ToString());
            var canvas = root.GetComponentInChildren<Canvas>();
            RectTransform stage = FindRect("AuthoredStage");
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float oldPlane = canvas.planeDistance;
            Vector3 oldScale = stage.localScale;
            var cameraObject = new GameObject("HomeLightingCaptureCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 540;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 100;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                stage.localScale = Vector3.one;
                // Preserve the existing real weather render in the comparison frames.
                foreach (Camera weather in root.GetComponentsInChildren<Camera>())
                    if (weather.name == "HomeWeatherCamera") weather.Render();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldPlane;
                stage.localScale = oldScale;
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(pixels);
                UnityEngine.Object.Destroy(cameraObject);
                Canvas.ForceUpdateCanvases();
            }
        }

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
        public IEnumerator AutomaticLightningSchedulingRecoversAfterLeavingHomeDuringActiveFlash()
        {
            Initialize("rokas-weather3-lightning-reentry-");
            yield return null;

            RokasView view = Bootstrap().View;
            RawImage storm = FindRawImage("HomeStormFlash");
            Assert.That(storm, Is.Not.Null);

            ForceLightning(view);
            view.Tick(.03f);
            yield return null;
            Assert.That(storm.color.a, Is.GreaterThan(.001f),
                "precondition: forced lightning must be active before leaving Home");

            object effects = typeof(RokasView)
                .GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(view);
            Assert.That(effects, Is.Not.Null);
            MethodInfo setLocation = effects.GetType().GetMethod("SetLocation", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(setLocation, Is.Not.Null);

            setLocation.Invoke(effects, new object[] { false, false });
            setLocation.Invoke(effects, new object[] { true, false });

            bool automaticLightningObserved = false;
            for (int index = 0; index < 700 && !automaticLightningObserved; index++)
            {
                view.Tick(.05f);
                automaticLightningObserved = storm.color.a > .001f;
            }

            Assert.That(automaticLightningObserved, Is.True,
                "automatic lightning must be scheduled again after Home re-entry when an active flash was interrupted");
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
