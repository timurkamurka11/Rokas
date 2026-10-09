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
    public sealed class LaptopCinematicInstalledPlayModeTests
    {
        private GameObject root;
        private string directory;

        private RokasBootstrap Initialize(string prefix)
        {
            if (!Resources.Load<Texture2D>(
                    "LaptopCinematic/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF") ||
                !Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest"))
                Assert.Ignore("First install new right-hand Blender art resources; old PSX frames are not acceptable.");

            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("LaptopInstalledCinematicFixture");
            RokasBootstrap boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            return boot;
        }

        private T Find<T>(string name) where T : Component
        {
            if (!root) return null;
            foreach (T element in root.GetComponentsInChildren<T>(true))
                if (element.name == name) return element;
            return null;
        }

        private void PressLaptop()
        {
            Button button = Find<Button>("LaptopHotspot");
            Assert.That(button, Is.Not.Null, "The original hotspot must survive.");
            button.onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator SkipAtPressAndRepeatClickOpenExistingLaptopExactlyOnce()
        {
            RokasBootstrap boot = Initialize("rokas-laptop-alpha-skip-");
            yield return null;
            PressLaptop();
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Not.Null,
                "The installed hand manifest must activate cinematic playback.");
            PressLaptop();
            Assert.That(boot.View.LaptopOpen, Is.False,
                "Repeated clicks during the cinematic must not open an early duplicate laptop panel.");
            yield return new WaitForSecondsRealtime(2.8f);
            boot.View.Escape();
            Assert.That(boot.View.LaptopOpen, Is.True, "ESC must end at the existing Laptop UI.");
            Assert.That(Find<RectTransform>("YomiLaptop"), Is.Not.Null);
            yield return null;
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Null,
                "The cinematic overlay must be destroyed after the skip.");
            Assert.That(Find<RectTransform>("YomiLaptop"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator NaturalCompletionAndCloseRestoreHomeHotspot()
        {
            RokasBootstrap boot = Initialize("rokas-laptop-alpha-complete-");
            yield return null;
            PressLaptop();
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Not.Null);
            yield return new WaitForSecondsRealtime(4.1f);
            Assert.That(boot.View.LaptopOpen, Is.True);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Null);
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(boot.View.LaptopOpen, Is.False,
                "Closing the existing panel must restore the room.");
            Button homeHotspot = Find<Button>("LaptopHotspot");
            Assert.That(homeHotspot, Is.Not.Null);
            Assert.That(homeHotspot.IsInteractable(), Is.True,
                "The old home hotspot must remain enabled.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
