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
    public sealed class LaptopPreBootChoicePlayModeTests
    {
        private GameObject root;
        private string saveDirectory;

        private RokasBootstrap StartGame()
        {
            if (!Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest") ||
                !Resources.Load<Texture2D>("LaptopCinematic/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF"))
                Assert.Ignore("Install 49 right-hand alpha frames and the approved/synthetic POV for this test.");

            saveDirectory = Path.Combine(Path.GetTempPath(), "rokas-v3-choice-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("ROKAS_PreBootChoice_TestFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(saveDirectory);
            return boot;
        }

        private T Find<T>(string name) where T : Component
        {
            foreach (var value in root.GetComponentsInChildren<T>(true))
                if (value.name == name) return value;
            return null;
        }

        [UnityTest]
        public IEnumerator EscFromPreBootNeverEntersLaptopOrPlaysBoot()
        {
            var boot = StartGame();
            yield return null;
            Find<Button>("LaptopHotspot").onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.05f);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Not.Null);
            Assert.That(Find<Text>("StandbyNoSignal"), Is.Not.Null);
            Assert.That(Find<RectTransform>("PowerKeyBlueStandby"), Is.Not.Null);
            Assert.That(Find<Text>("PowerChoiceHint").gameObject.activeInHierarchy, Is.True);
            Assert.That(boot.View.LaptopOpen, Is.False);
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(1.15f);
            Assert.That(boot.View.LaptopOpen, Is.False, "Escape must not launch LaptopBoot.");
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Null,
                "All standby hints and the mask must be removed on return.");
            Assert.That(Find<Button>("LaptopHotspot").IsInteractable(), Is.True);
        }

        [UnityTest]
        public IEnumerator ConfirmViaPhysicalPowerHotspotStartsOnceAndOpensExistingLaptop()
        {
            var boot = StartGame();
            yield return null;
            Find<Button>("LaptopHotspot").onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.05f);
            Assert.That(boot.View.LaptopOpen, Is.False,
                "Cinematic must await E/Power instead of auto starting.");
            var key = Find<Button>("PowerKeyClickTarget");
            Assert.That(key, Is.Not.Null);
            key.onClick.Invoke();
            key.onClick.Invoke(); // should not restart / cause two opens
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.That(boot.View.LaptopOpen, Is.True,
                "E / power must hand off to the existing original Laptop UI.");
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Null);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            if (!string.IsNullOrEmpty(saveDirectory) && Directory.Exists(saveDirectory))
                Directory.Delete(saveDirectory, true);
        }
    }
}
