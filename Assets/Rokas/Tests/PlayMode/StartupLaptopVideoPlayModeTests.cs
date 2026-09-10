using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class StartupLaptopVideoPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator RealStartDoesNotBuildHomeBeforeStartupVideoGate()
        {
            root = new GameObject("StartupVideoFixture");
            root.AddComponent<RokasBootstrap>();

            yield return null;

            Assert.That(Find("HomeTitle"), Is.Null,
                "The real application Start path must not construct Home before the startup preview finishes, skips, or falls back.");
            Assert.That(Find("StartupVideoSurface"), Is.Not.Null,
                "Startup must expose a black-backed video surface while the preview owns presentation startup.");
        }

        [UnityTest]
        public IEnumerator LaptopOpeningShowsBootGateBeforeDesktop()
        {
            CreateSynchronousBoot();
            yield return null;

            Press("LaptopHotspot");
            yield return null;

            Assert.That(Find("LaptopBootSurface"), Is.Not.Null,
                "Every explicit Home to Laptop opening must enter the transient boot gate first.");
            Assert.That(Find("LaptopContracts"), Is.Null,
                "Laptop desktop tiles must not be constructed while the boot animation owns the screen.");
        }

        private void CreateSynchronousBoot()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-video-gate-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("LaptopVideoFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
        }

        private void Press(string name)
        {
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, "Missing active interaction: " + name);
            Assert.That(button.IsInteractable(), Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private Button FindButton(string name)
        {
            if (root == null) return null;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            return null;
        }

        private GameObject Find(string name)
        {
            if (root == null) return null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
