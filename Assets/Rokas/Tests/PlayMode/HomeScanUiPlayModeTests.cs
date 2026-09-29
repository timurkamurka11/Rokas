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
    public sealed class HomeScanUiPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator HomeUsesAuthoredCustomGlowRuntimeInsteadOfProceduralOutline()
        {
            Initialize("rokas-home-custom-glow-");
            yield return null;

            Assert.That(FindRect("HomeScanOutlineOverlay"), Is.Null,
                "The obsolete procedural amber outline must not be instantiated in runtime Home.");
            Assert.That(FindRect("HomeScanTitle"), Is.Null,
                "The workshop-only Variant 4 helper title must not leak into runtime Home.");
            Assert.That(FindRect("HomeScanHeaderRule"), Is.Null);

            RectTransform glowRoot = FindRect("HomeCustomGlowRuntime");
            Assert.That(glowRoot, Is.Not.Null,
                "Runtime Home must instantiate the user's baked CustomGlow composition.");
            Image[] glowImages = glowRoot.GetComponentsInChildren<Image>(true);
            Assert.That(glowImages.Length, Is.GreaterThan(0));
            foreach (Image image in glowImages)
            {
                Assert.That(image.sprite, Is.Not.Null, image.name);
                Assert.That(image.raycastTarget, Is.False, image.name);
                Assert.That(image.material, Is.Not.Null, image.name);
                Assert.That(image.material.shader.name, Is.EqualTo("ROKAS/Home/CustomGlowRuntime"), image.name);
            }

            foreach (string hotspot in new[]
            {
                "LampHotspot", "WindowHotspot", "DeskLampHotspot", "WorkbenchHotspot",
                "DoorHotspot", "LaptopHotspot", "TeaHotspot", "MameHotspot"
            })
            {
                Button button = Find<Button>(hotspot);
                Assert.That(button, Is.Not.Null, hotspot);
                Assert.That(button.IsInteractable(), Is.True, hotspot);
                Assert.That(button.transform.Find("ScanBadge"), Is.Null, hotspot);
            }

            foreach (string legacyChrome in new[]
            {
                "HeaderShade", "HeaderRule", "Logo", "BrandTag", "LocationStatus",
                "Wallet", "Settings", "FooterShade", "Controls", "SaveStatus"
            })
            {
                RectTransform rect = FindRect(legacyChrome);
                Assert.That(rect, Is.Not.Null, legacyChrome);
                Assert.That(rect.gameObject.activeInHierarchy, Is.False, legacyChrome);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ExistingHomeActionsRemainFunctionalAboveAuthoredGlow()
        {
            Initialize("rokas-home-custom-glow-actions-");
            yield return null;
            Assert.That(FindRect("HomeOverlayRuntimeIcons"), Is.Not.Null);
            Assert.That(FindRect("HomeCustomGlowRuntime"), Is.Not.Null);
            foreach (string hotspot in new[]
            {
                "LampHotspot", "WindowHotspot", "DeskLampHotspot", "WorkbenchHotspot",
                "DoorHotspot", "LaptopHotspot", "TeaHotspot", "MameHotspot"
            })
                Assert.That(Find<Button>(hotspot), Is.Not.Null, hotspot);
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize(string prefix)
        {
            HomeCustomGlowRuntimeLayout layout = Resources.Load<HomeCustomGlowRuntimeLayout>(HomeCustomGlowRuntimePresenter.ResourcesPath);
            Assert.That(layout, Is.Not.Null, "Baked HomeCustomGlowRuntimeLayout.asset is required before PlayMode validation.");
            Assert.That(layout.Entries.Count, Is.GreaterThan(0));
            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("HomeCustomGlowRuntimeFixture");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
        }

        private T Find<T>(string name) where T : Component
        {
            if (root == null) return null;
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            return null;
        }

        private RectTransform FindRect(string name) { return Find<RectTransform>(name); }

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