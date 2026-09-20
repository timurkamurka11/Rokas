using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class FirstLoopTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator IllustratedHomeCanAcceptFightReturnAndClaimExactlyOnce()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-playmode-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("FirstLoopFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Assert.That(boot.Session, Is.Not.Null);
            var art = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(art.home, Is.Not.Null);
            Assert.That(art.portal, Is.Not.Null);
            Assert.That(art.subway, Is.Not.Null);
            Assert.That(art.enemy, Is.Not.Null);
            Assert.That(art.familiar, Is.Not.Null);
            Assert.That(art.IsComplete(), Is.True, "Every shipped art, font, contract and audio binding must import.");
            Assert.That(root.GetComponentInChildren<Camera>(), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<Canvas>(), Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(Find("HomeOutsideParallax"), Is.Not.Null, "Home must boot with its outside 2.5D plane.");
            Assert.That(Find("HomeForegroundDepth"), Is.Not.Null, "Home must boot with its foreground depth plane.");
            foreach (string layer in new[] { "HomeOutsideDepthMask", "HomeExteriorMistNear", "HomeExteriorHaze", "HomeWetGlass", "HomeWetGlassStreaks" })
                Assert.That(Find(layer), Is.Not.Null, "Unified Home must retain " + layer);
            Assert.That(Find("HomeRainFar"), Is.Not.Null, "Home weather must include its far layer.");
            Assert.That(Find("HomeRainMid"), Is.Not.Null, "Home weather must include its mid layer.");
            Assert.That(Find("HomeRainNear"), Is.Not.Null, "Home weather must include its near layer.");
            Assert.That(Resources.Load("HomeAtmosphereProfile"), Is.Not.Null,
                "Home atmosphere must use its serialized runtime profile.");
            Assert.That(boot.Session.LiveMessages, Is.Not.Null, "Live Messages must initialize during the real boot.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            Capture("integration-home");
            Press("LaptopHotspot");
            Press("LaptopMessages");
            Assert.That(Find("MessagesRoot"), Is.Not.Null, "Messages must open its real conversation shell.");
            Assert.That(Find("MessagesSearch"), Is.Not.Null);
            Assert.That(Find("MessagesContactList"), Is.Not.Null);
            Assert.That(Find("MessagesHeader"), Is.Not.Null);
            Assert.That(Find("MessagesConversationViewport"), Is.Not.Null);
            Assert.That(FindButton("MessagesContact_kaito"), Is.Not.Null);
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
            Capture("integration-messages");
            boot.View.Escape();
            Press("LaptopContracts");
            Press("AcceptContract");
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Accepted));
            Press("ClosePanel");
            yield return new WaitForSecondsRealtime(.3f);
            Press("TeaHotspot");
            Press("PrepareTea");
            Press("ClosePanel");
            Press("DoorHotspot");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Portal));
            Press("EnterPortal");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));

            foreach (var text in root.GetComponentsInChildren<Text>())
                AssertLegacyCombatTextAbsent(text.text);
            foreach (var text in root.GetComponentsInChildren<TMPro.TMP_Text>())
                AssertLegacyCombatTextAbsent(text.text);
            var combatInput = Find("EnemyAttack").GetComponent<CombatInputSurface>();
            Assert.That(combatInput, Is.Not.Null, "Combat must expose the manual CombatInputSurface.");
            Assert.That(Find("EnemySealBar"), Is.Not.Null);
            Assert.That(Find("ChargeTimingRing"), Is.Not.Null);
            var controls = Find("CombatControls").GetComponent<Text>();
            Assert.That(controls, Is.Not.Null);
            Assert.That(controls.text, Does.Contain("ЗАРЯД"));
            Assert.That(controls.text, Does.Contain("УКЛОНЕНИЕ"));
            Assert.That(controls.text, Does.Contain("ОТРАЖЕНИЕ"));
            Assert.That(controls.text, Does.Contain("РЕЗОНАНС"));
            Assert.That(controls.text, Does.Not.Contain("АВТОАТАК"), "Combat controls must not advertise legacy autoattack.");
            Assert.That(controls.text, Does.Not.Contain("КЛИК / АТАКА"), "Combat controls must not show the old click instruction.");
            for (int i = 1; i <= 3; i++)
            {
                GameObject ritualPoint = Find("RitualPoint" + i);
                Assert.That(ritualPoint, Is.Not.Null, "Combat must build ritual point " + i + " before it becomes active.");
                Assert.That(FindChild(ritualPoint.transform, "PointRing"), Is.Not.Null,
                    "Ritual point " + i + " must have renderable ring geometry.");
            }
            Canvas.ForceUpdateCanvases();
            Capture("integration-combat");

            // The real UI input path must not reach combat through a modal pause panel.
            Press("Settings");
            var enemyButton = FindButton("EnemyAttack");
            Assert.That(enemyButton.IsInteractable(), Is.False);
            float pausedHealth = boot.Session.State.enemyHp;
            float pausedTime = boot.Session.State.combatTime;
            Click(enemyButton);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(pausedHealth));
            Assert.That(boot.Session.State.combatTime, Is.EqualTo(pausedTime));
            Press("CloseSettings");

            float beforeIdleTick = boot.Session.State.enemyHp;
            boot.Session.Tick(boot.Session.Contract.autoInterval + .05f);
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(beforeIdleTick));

            // Exercise the actual domain and the wired hit button, not a duplicate simulation.
            for (int i = 0; i < 200 && boot.Session.State.phase == RunPhase.Combat; i++)
            {
                boot.Session.Tick(.48f);
                if (boot.Session.State.phase != RunPhase.Combat) break;
                if (boot.Session.Combat.Stage == CombatStage.Ritual) TraceRitual();
                else if (boot.Session.Combat.Stage == CombatStage.Fighting) TapEnemy();
            }
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Sealed));
            Press("ReturnHome");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Payment));
            Press("LaptopHotspot");
            Press("LaptopContracts");
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
            Capture("integration-payment");
            long previous = boot.Session.State.yen;
            Press("ClaimPayment");
            Assert.That(boot.Session.State.yen, Is.EqualTo(previous + boot.Session.Contract.reward));
            Assert.That(FindButton("ClaimPayment", false), Is.Null, "The payment action must disappear after claiming.");
            int paidYen = boot.Session.State.yen;
            int paidReputation = boot.Session.State.reputation;
            int paidAsh = boot.Session.State.spiritAsh;
            Assert.That(File.Exists(Path.Combine(directory, "save.json")), Is.True);

            // Recreate the presentation with its real Unity JSON codec and the same isolated profile.
            // File existence alone would also pass with a stale pre-reward save.
            UnityEngine.Object.Destroy(root);
            yield return null;
            root = new GameObject("ReloadedFirstLoopFixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            Assert.That(boot.Session.State.yen, Is.EqualTo(paidYen));
            Assert.That(boot.Session.State.reputation, Is.EqualTo(paidReputation));
            Assert.That(boot.Session.State.spiritAsh, Is.EqualTo(paidAsh));
            Assert.That(boot.Session.State.completedRuns, Is.EqualTo(1));
            Assert.That(boot.Session.State.preparedFoodId, Is.Empty);
            Press("LaptopHotspot");
            Press("LaptopContracts");
            Assert.That(FindButton("ClaimPayment", false), Is.Null);
            Assert.That(FindButton("AcceptContract").IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LaptopFoodPreservesConsumeGuardAndReturnsFocus()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-laptop-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("LaptopFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Press("LaptopHotspot");
            Assert.That(FindButton("LaptopContracts"), Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(FindButton("LaptopContracts").gameObject));
            Assert.That(FindButton("TeaHotspot").IsInteractable(), Is.False, "House input must stay blocked behind the laptop.");
            Assert.That(FindButton("DoorHotspot").IsInteractable(), Is.False);
            int initialYen = boot.Session.State.yen;
            Press("LaptopFood");
            Press("FoodRowRing_5");
            Press("EatButton");
            Assert.That(boot.Session.State.yen, Is.EqualTo(initialYen));
            Assert.That(boot.Session.State.preparedFoodId, Is.EqualTo(FoodService.GreenTeaId));
            Assert.That(FindButton("EatButton").IsInteractable(), Is.False);
            Press("FoodBack");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(FindButton("LaptopFood").gameObject));
            Press("LaptopFood");
            Press("FoodRowRing_5");
            Assert.That(FindButton("EatButton").IsInteractable(), Is.False, "Reopening Food must not permit a second food effect.");
            Click(FindButton("EatButton"));
            Assert.That(boot.Session.State.yen, Is.EqualTo(initialYen));
            Assert.That(boot.Session.State.preparedFoodId, Is.EqualTo(FoodService.GreenTeaId));
            boot.View.Escape();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(FindButton("LaptopFood").gameObject));
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(FindButton("LaptopContracts", false), Is.Null);
            Assert.That(FindButton("TeaHotspot").IsInteractable(), Is.True);
            Assert.That(FindButton("LaptopHotspot").IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(FindButton("LaptopHotspot").gameObject));
            LogAssert.NoUnexpectedReceived();
        }

        private void TapEnemy()
        {
            var target = FindButton("EnemyAttack").gameObject;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
        }

        private void TraceRitual()
        {
            var target = FindButton("EnemyAttack").gameObject;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            for (int i = 1; i <= 3; i++)
            {
                RectTransform point = null;
                foreach (var rect in root.GetComponentsInChildren<RectTransform>()) if (rect.name == "RitualPoint" + i) point = rect;
                Assert.That(point, Is.Not.Null);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, point.TransformPoint(point.rect.center));
                if (i == 1) ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
                else ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
        }

        private void Press(string name)
        {
            var button = FindButton(name);
            Assert.That(button.IsInteractable(), Is.True, name);
            Click(button);
        }

        private static void Click(Button button)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private Button FindButton(string name, bool required = true)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                if (button.name != name) continue;
                return button;
            }
            if (required) Assert.Fail("Missing active interaction: " + name);
            return null;
        }

        private static void AssertLegacyCombatTextAbsent(string text)
        {
            string normalized = System.Text.RegularExpressions.Regex.Replace(text ?? string.Empty, @"\s+", " ").ToUpperInvariant();
            Assert.That(normalized, Does.Not.Contain("АВТОАТАКА АКТИВНА"));
            Assert.That(normalized, Does.Not.Contain("УДАР КЛИНКОМ / +КЛИК"));
            Assert.That(normalized, Does.Not.Contain("НАЖИМАЙТЕ НА ЁКАЯ"));
        }

        private GameObject Find(string name)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == name) return item.gameObject;
            }
            return null;
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            foreach (var item in parent.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == name) return item.gameObject;
            }
            return null;
        }

        // Opt-in runtime evidence; ordinary test runs allocate no capture resources.
        private void Capture(string name)
        {
            string output = Environment.GetEnvironmentVariable("ROKAS_COMBAT_CAPTURE_DIR");
            if (string.IsNullOrEmpty(output) ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;

            var canvas = root.GetComponentInChildren<Canvas>();
            var stage = Find("AuthoredStage").GetComponent<RectTransform>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            var oldScale = stage.localScale;
            var cameraObject = new GameObject("IntegrationCaptureCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
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

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
