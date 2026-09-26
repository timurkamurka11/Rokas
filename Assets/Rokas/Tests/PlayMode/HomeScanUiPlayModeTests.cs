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
        public IEnumerator HomeUsesReferenceScanCompositionInsteadOfLegacyPills()
        {
            Initialize("rokas-home-scan-ui-");
            yield return null;

            Text title = Find<Text>("HomeScanTitle");
            Assert.That(title, Is.Not.Null);
            Assert.That(title.text, Is.EqualTo("Variant 4 — scan mode reveal"));
            Assert.That(title.font, Is.Not.Null);
            Assert.That(title.fontSize, Is.InRange(26, 32));
            Assert.That(title.color.r, Is.GreaterThan(.86f));
            Assert.That(title.color.g, Is.GreaterThan(.84f));

            RectTransform rule = FindRect("HomeScanHeaderRule");
            Assert.That(rule, Is.Not.Null);
            AssertRect(rule, 44f, 73f, 360f, 3f, 3f);

            Assert.That(FindRect("HomeScanOutlineOverlay"), Is.Not.Null,
                "Reference composition requires the amber object-contour overlay.");
            Graphic outline = Find<Graphic>("HomeScanOutlineOverlay");
            Assert.That(outline, Is.Not.Null);
            Assert.That(outline.raycastTarget, Is.False);

            AssertMarker("LampHotspot", 139f, 402f);
            AssertMarker("WindowHotspot", 698f, 129f);
            AssertMarker("DeskLampHotspot", 1157f, 194f);
            AssertMarker("WorkbenchHotspot", 1404f, 299f);
            AssertMarker("DoorHotspot", 1743f, 136f);
            AssertMarker("LaptopHotspot", 1094f, 490f);
            AssertMarker("TeaHotspot", 832f, 594f);
            AssertMarker("MameHotspot", 299f, 784f);

            foreach (string hotspot in new[]
            {
                "LampHotspot", "WindowHotspot", "DeskLampHotspot", "WorkbenchHotspot",
                "DoorHotspot", "LaptopHotspot", "TeaHotspot", "MameHotspot"
            })
            {
                RectTransform rootRect = FindRect(hotspot);
                Assert.That(rootRect, Is.Not.Null, hotspot);
                Assert.That(rootRect.GetComponent<Button>(), Is.Not.Null,
                    hotspot + " must remain interactable.");
                Assert.That(rootRect.Find("ScanBadge"), Is.Not.Null,
                    hotspot + " must use the circular reference badge.");
                Assert.That(rootRect.Find("ScanConnector"), Is.Not.Null,
                    hotspot + " must use the vertical dotted connector.");
                Assert.That(rootRect.Find("PillFace"), Is.Null,
                    hotspot + " must not retain the old pill UI.");
                Assert.That(rootRect.Find("PillGold"), Is.Null);
                Assert.That(rootRect.Find("Chevron"), Is.Null);
                Assert.That(rootRect.Find("Title"), Is.Null);
                Assert.That(rootRect.Find("YokaiAccent"), Is.Null);
            }

            foreach (string legacyChrome in new[]
            {
                "HeaderShade", "HeaderRule", "Logo", "BrandTag", "LocationStatus",
                "Wallet", "Settings", "FooterShade", "Controls", "SaveStatus"
            })
            {
                RectTransform rect = FindRect(legacyChrome);
                Assert.That(rect, Is.Not.Null, legacyChrome + " must still exist for non-Home screens.");
                Assert.That(rect.gameObject.activeSelf, Is.False,
                    legacyChrome + " must be hidden while Home uses the scan-mode oracle.");
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ExistingHomeActionsRemainFunctionalUnderReferenceMarkers()
        {
            Initialize("rokas-home-scan-actions-");
            yield return null;

            foreach (string hotspot in new[]
            {
                "LampHotspot", "WindowHotspot", "DeskLampHotspot", "WorkbenchHotspot",
                "DoorHotspot", "LaptopHotspot", "TeaHotspot", "MameHotspot"
            })
            {
                Button button = Find<Button>(hotspot);
                Assert.That(button, Is.Not.Null, hotspot);
                Assert.That(button.IsInteractable(), Is.True, hotspot);
            }

            Button lamp = Find<Button>("LampHotspot");
            Button deskLamp = Find<Button>("DeskLampHotspot");
            Assert.That(lamp.onClick.GetPersistentEventCount(), Is.Zero,
                "Runtime-authored listeners are expected; the button must not depend on prefab persistent events.");
            lamp.onClick.Invoke();
            deskLamp.onClick.Invoke();
            yield return null;

            LogAssert.NoUnexpectedReceived();
        }

        private void AssertMarker(string name, float centerX, float centerY)
        {
            RectTransform rect = FindRect(name);
            Assert.That(rect, Is.Not.Null, name);
            Vector2 center = new Vector2(
                rect.anchoredPosition.x + rect.sizeDelta.x * .5f,
                -rect.anchoredPosition.y + rect.sizeDelta.y * .5f);
            Assert.That(center.x, Is.EqualTo(centerX).Within(5f), name + " X");
            Assert.That(center.y, Is.EqualTo(centerY).Within(5f), name + " Y");
            Assert.That(rect.sizeDelta.x, Is.InRange(64f, 80f), name + " marker width");
            Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(68f), name + " marker height");
        }

        private static void AssertRect(RectTransform rect, float x, float y, float width, float height, float tolerance)
        {
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(x).Within(tolerance));
            Assert.That(-rect.anchoredPosition.y, Is.EqualTo(y).Within(tolerance));
            Assert.That(rect.sizeDelta.x, Is.EqualTo(width).Within(tolerance));
            Assert.That(rect.sizeDelta.y, Is.EqualTo(height).Within(tolerance));
        }

        private void Initialize(string prefix)
        {
            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("HomeScanUiFixture");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
        }

        private T Find<T>(string name) where T : Component
        {
            if (root == null) return null;
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            return null;
        }

        private RectTransform FindRect(string name)
        {
            return Find<RectTransform>(name);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
