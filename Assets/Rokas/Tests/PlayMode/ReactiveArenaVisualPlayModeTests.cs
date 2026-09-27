using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class ReactiveArenaVisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator ArenaCanBeCapturedAtAuthoredAndSmallScreenSizes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-reactive-visual-" + Guid.NewGuid().ToString("N"));
            var root = new GameObject("ReactiveArenaVisualFixture");
            try
            {
                var boot = root.AddComponent<RokasBootstrap>();
                boot.Initialize(directory);
                boot.SendMessage("OnApplicationFocus", true);
                yield return null;
                Assert.That(boot.Session.AcceptContract(), Is.True);
                Assert.That(boot.Session.LeaveHome(), Is.True);
                yield return null;
                Button button = Find(root, "EnterReactivePortal").GetComponent<Button>();
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
                yield return new WaitForSecondsRealtime(2f);
                Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
                Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(Rokas.Core.ReactiveTurns.ReactivePhase.PlayerCommand));
                Assert.That(Find(root, "MessagesNotification")?.activeInHierarchy, Is.Not.True,
                    "Battle HUD must remain readable when an unread message arrives.");

                Capture(root, 1920, 1080);
                Capture(root, 1280, 720);
                button = Find(root, "ReactiveSealStrike").GetComponent<Button>();
                Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
                float deadline = Time.realtimeSinceStartup + 4f;
                while (boot.Session.ReactiveCombat.Phase != ReactivePhase.EnemyExecution &&
                       Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.EnemyExecution));
                long startUs = boot.Session.ReactiveCombat.CurrentActionStartUs;
                while (boot.Session.ReactiveCombat.CurrentCombatUs - startUs < 800000 &&
                       Time.realtimeSinceStartup < deadline) yield return null;
                Capture(root, 1920, 1080, "-windup");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void Capture(GameObject root, int width, int height, string suffix = "")
        {
            Canvas canvas = root.GetComponentInChildren<Canvas>();
            RectTransform stage = Find(root, "AuthoredStage").GetComponent<RectTransform>();
            Assert.That(canvas, Is.Not.Null);
            var cameraObject = new GameObject("ReactiveCaptureCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = ~0;
            var render = new RenderTexture(width, height, 24);
            render.Create();
            camera.targetTexture = render;
            RenderTexture prior = RenderTexture.active;
            RenderMode priorMode = canvas.renderMode;
            Camera priorCamera = canvas.worldCamera;
            Vector3 priorScale = stage.localScale;
            string path = Path.Combine(Path.GetTempPath(), "rokas-reactive-arena-" + width + "x" + height + suffix + ".png");
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                stage.localScale = Vector3.one * Mathf.Min(width / 1920f, height / 1080f);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = render;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(path, image.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(10000));
                TestContext.WriteLine(path);
            }
            finally
            {
                canvas.renderMode = priorMode;
                canvas.worldCamera = priorCamera;
                stage.localScale = priorScale;
                RenderTexture.active = prior;
                camera.targetTexture = null;
                render.Release();
                UnityEngine.Object.DestroyImmediate(render);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static GameObject Find(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }
    }
}
