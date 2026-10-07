using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class PreBattlePortalCleanupPlayModeTests
    {
        private GameObject root;
        private RokasBootstrap boot;
        private string directory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-prebattle-clean-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("PreBattlePortalCleanupFixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory, false);
            yield return null;

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Portal));
        }

        [UnityTest]
        public IEnumerator PortalScreenContainsOnlyBackAndEnterBattleActions()
        {
            GameObject chrome = Find("GlobalChrome");
            Assert.That(chrome, Is.Not.Null);
            Assert.That(chrome.activeInHierarchy, Is.False,
                "Global ROKAS header/footer/stats chrome must stay hidden on the pre-battle Portal state.");

            foreach (string removed in new[]
            {
                "PortalEyebrow", "PortalTitle", "PortalNote",
                "HeaderShade", "HeaderRule", "Logo", "BrandTag", "LocationStatus",
                "Wallet", "Settings", "FooterShade", "Controls", "SaveStatus"
            })
            {
                GameObject item = Find(removed);
                Assert.That(item == null || !item.activeInHierarchy, Is.True, removed);
            }

            Button enter = FindButton("EnterReactivePortal");
            Button back = FindButton("ReturnFromPortal");
            Assert.That(enter, Is.Not.Null);
            Assert.That(back, Is.Not.Null);
            Assert.That(Label(enter), Is.EqualTo("Войти в бой"));
            Assert.That(Label(back), Is.EqualTo("Вернуться домой"));

            RectTransform scene = Find("SceneInteractions").GetComponent<RectTransform>();
            Button[] activeButtons = scene.GetComponentsInChildren<Button>(false);
            CollectionAssert.AreEquivalent(
                new[] { "EnterReactivePortal", "ReturnFromPortal" },
                activeButtons.Select(button => button.name).ToArray());

            string[] visibleText = scene.GetComponentsInChildren<Text>(false)
                .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(label.text))
                .Select(label => label.text.Trim())
                .Distinct()
                .ToArray();
            CollectionAssert.AreEquivalent(new[] { "Войти в бой", "Вернуться домой" }, visibleText,
                "Portal screen may not leak titles, descriptions, debug hints or mojibake.");

            Capture("01_prebattle_portal_clean");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnterBattleUsesExistingActionAndHandsOffToCombat()
        {
            Press(FindButton("EnterReactivePortal"));
            float deadline = Time.realtimeSinceStartup + 3f;
            while (boot.Session.State.phase != RunPhase.Combat && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
            deadline = Time.realtimeSinceStartup + 3f;
            while (Find("ReactiveArena") == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Find("ReactiveArena"), Is.Not.Null,
                "The renamed CTA must keep the authoritative reactive-combat transition.");
            Assert.That(Find("GlobalChrome").activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator BackUsesExistingReturnHomeAction()
        {
            Press(FindButton("ReturnFromPortal"));
            float deadline = Time.realtimeSinceStartup + 3f;
            while (boot.Session.State.phase != RunPhase.Home && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            Assert.That(FindButton("LaptopHotspot"), Is.Not.Null);
            Assert.That(FindButton("WorkbenchHotspot"), Is.Null,
                "Returning from Portal must not resurrect the removed Hub Workbench route.");
            yield return null;
        }

        private void Capture(string name)
        {
            string output = ResolveCaptureDirectory();
            if (string.IsNullOrEmpty(output) ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;

            Canvas canvas = Find("RokasCanvas").GetComponent<Canvas>();
            RectTransform stage = Find("AuthoredStage").GetComponent<RectTransform>();
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            Vector3 oldScale = stage.localScale;
            var cameraObject = new GameObject("PreBattleCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 540f;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 100f;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                stage.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
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

        private static string ResolveCaptureDirectory()
        {
            string output = Environment.GetEnvironmentVariable("ROKAS_PREBATTLE_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(output)) return output;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-prebattleCaptureDir" && i + 1 < args.Length) return args[i + 1];
                const string prefix = "-prebattleCaptureDir=";
                if (args[i].StartsWith(prefix, StringComparison.Ordinal)) return args[i].Substring(prefix.Length);
            }
            return string.Empty;
        }

        private void Press(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.IsInteractable(), Is.True);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private static string Label(Button button)
        {
            Text text = button.GetComponentInChildren<Text>(true);
            return text == null ? string.Empty : text.text;
        }

        private Button FindButton(string name)
        {
            GameObject item = Find(name);
            return item ? item.GetComponent<Button>() : null;
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
            root = null;
            boot = null;
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
