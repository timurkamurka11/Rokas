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
    public sealed class HubDialoguePlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator Window_OpensTalk_ThenCompletionAndClose()
        {
            Initialize("rokas-hub-dialogue-window-");
            yield return null;

            Button window = Find<Button>("WindowHotspot");
            Assert.That(window, Is.Not.Null);
            window.onClick.Invoke();
            yield return null;

            RectTransform hub = FindRect("HubDialogueRoot");
            Assert.That(hub, Is.Not.Null);
            Assert.That(hub.gameObject.activeInHierarchy, Is.True);

            RawImage portrait = Find<RawImage>("PortraitImage");
            Assert.That(portrait, Is.Not.Null);
            Assert.That(portrait.texture, Is.Not.Null);
            Assert.That(portrait.uvRect.y, Is.EqualTo(0f).Within(.001f),
                "Talk frames occupy the lower atlas row.");

            Button advance = Find<Button>("HubForwardButton");
            Assert.That(advance, Is.Not.Null);
            advance.onClick.Invoke();
            yield return null;

            Assert.That(
                portrait.uvRect.y,
                Is.EqualTo(.5f).Within(.001f),
                "Finishing the typewriter must switch portrait to Idle.");
            RectTransform arrow = FindRect("CompletionArrow");
            Assert.That(arrow, Is.Not.Null);
            Assert.That(arrow.gameObject.activeInHierarchy, Is.True);

            advance.onClick.Invoke();
            yield return null;
            Assert.That(hub.gameObject.activeInHierarchy, Is.False);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InspectHotspots_OpenHub_ButLaptopDoesNot()
        {
            Initialize("rokas-hub-dialogue-routing-");
            yield return null;

            Find<Button>("LampHotspot").onClick.Invoke();
            yield return null;
            Assert.That(FindRect("HubDialogueRoot").gameObject.activeInHierarchy, Is.True);
            Find<Button>("HubForwardButton").onClick.Invoke();
            Find<Button>("HubForwardButton").onClick.Invoke();
            yield return null;

            Find<Button>("MameHotspot").onClick.Invoke();
            yield return null;
            Assert.That(FindRect("HubDialogueRoot").gameObject.activeInHierarchy, Is.True);
            Find<Button>("HubForwardButton").onClick.Invoke();
            Find<Button>("HubForwardButton").onClick.Invoke();
            yield return null;

            Find<Button>("LaptopHotspot").onClick.Invoke();
            yield return null;
            Assert.That(FindRect("HubDialogueRoot").gameObject.activeInHierarchy, Is.False,
                "Laptop remains a direct action and must not open Hub Dialogue.");
            Assert.That(FindRect("YomiLaptop"), Is.Not.Null);

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RepeatedInspectClick_DoesNotCreateDuplicatePlaques()
        {
            Initialize("rokas-hub-dialogue-duplicate-");
            yield return null;

            Button window = Find<Button>("WindowHotspot");
            window.onClick.Invoke();
            window.onClick.Invoke();
            yield return null;

            int count = 0;
            foreach (RectTransform rect in
                     root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "HubDialogueRoot")
                    count++;

            Assert.That(count, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize(string prefix)
        {
            directory = Path.Combine(
                Path.GetTempPath(),
                prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("HubDialogueFixture");
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
            if (!string.IsNullOrEmpty(directory) &&
                Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
