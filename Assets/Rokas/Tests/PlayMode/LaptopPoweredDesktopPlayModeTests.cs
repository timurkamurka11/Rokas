using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class LaptopPoweredDesktopPlayModeTests
    {
        private GameObject root;
        private string savePath;

        private T Find<T>(string name) where T : Component
        {
            if (!root) return null;
            foreach (T item in root.GetComponentsInChildren<T>(true))
                if (item.name == name) return item;
            return null;
        }

        [UnityTest]
        public IEnumerator BootOnceThenOpenLivePhysicalDesktopWithoutRerunningHand()
        {
            LaptopPowerSession.ResetForTests();
            Assert.That(LaptopPowerSession.PoweredOn, Is.False);
            if (!Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest") ||
                !Resources.Load<Texture2D>("LaptopCinematic/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF"))
                Assert.Ignore("Run with production right-hand frames and approved POV installed.");

            savePath = Path.Combine(Path.GetTempPath(), "rokas-laptop-powered-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("ROKAS_PoweredLaptop_TestFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(savePath);
            yield return null;
            var hotspot = Find<Button>("LaptopHotspot");
            Assert.That(hotspot, Is.Not.Null);
            hotspot.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.05f);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Not.Null);
            Assert.That(Find<RectTransform>("PowerKeyBlueStandby").gameObject.activeInHierarchy, Is.True);
            Find<Button>("PowerKeyClickTarget").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.30f);
            Assert.That(Find<RectTransform>("PowerKeyBlueStandby").gameObject.activeInHierarchy, Is.True,
                "Blue Power LED must remain lit until the finger makes real contact.");
            yield return new WaitForSecondsRealtime(1.20f);
            Assert.That(Find<RectTransform>("PowerKeyBlueStandby").gameObject.activeInHierarchy, Is.False,
                "Blue Power LED must go dark at finger contact, not at initial E confirmation.");
            yield return new WaitForSecondsRealtime(1.05f);
            Assert.That(boot.View.LaptopOpen, Is.True);

            // VideoPresenter is independently responsible for the real
            // LaptopBoot.mp4 completion. Simulate its single success event here.
            LaptopPowerSession.CompleteFirstBoot();
            Assert.That(LaptopPowerSession.PoweredOn, Is.True);
            Assert.That(LaptopPowerSession.CompletedBoots, Is.EqualTo(1));

            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.85f);
            Assert.That(boot.View.LaptopOpen, Is.False);
            var clock = Find<LaptopPhysicalDesktopClock>("PhysicalDesktopLiveClock");
            Assert.That(clock, Is.Not.Null);
            Assert.That(clock.gameObject.activeInHierarchy, Is.True);
            clock.UpdateClockIfNecessary(force: true);
            Assert.That(Find<RectTransform>("PowerKeyBlueStandby").gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Text>("StandbyNoSignal").gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Button>("PhysicalDesktopClickTarget").gameObject.activeInHierarchy, Is.True);
            Assert.That(Find<RectTransform>("PoweredLaptopOpenHint").gameObject.activeInHierarchy, Is.True);
            Find<Button>("PhysicalDesktopClickTarget").onClick.Invoke();
            yield return null;
            Assert.That(boot.View.LaptopOpen, Is.True);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Null);
            Assert.That(LaptopPowerSession.CompletedBoots, Is.EqualTo(1));

            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.85f);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Not.Null);
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Null);
            Assert.That(LaptopPowerSession.PoweredOn, Is.True, "Standing up must not turn the laptop off.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root) UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            LaptopPowerSession.ResetForTests();
            if (!string.IsNullOrEmpty(savePath) && Directory.Exists(savePath))
                Directory.Delete(savePath, true);
        }
    }
}
