using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class HomeWeather3CapturePlayModeTests
    {
        private GameObject root;
        private string saveDirectory;
        private string captureDirectory;
        private Camera captureCamera;
        private Canvas captureCanvas;
        private RenderTexture captureTarget;

        [UnityTest]
        public IEnumerator CaptureCurrentHomeWeatherLightingMatrix()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "rokas-weather3-capture-" + Guid.NewGuid().ToString("N"));
            captureDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults", "HomeWeather3Visual"));
            Directory.CreateDirectory(captureDirectory);

            root = new GameObject("HomeWeather3CaptureFixture");
            RokasBootstrap bootstrap = root.AddComponent<RokasBootstrap>();
            bootstrap.Initialize(saveDirectory);
            Assert.That(bootstrap.View, Is.Not.Null);
            Assert.That(bootstrap.Session, Is.Not.Null);

            ConfigureRenderTextureCapture();
            yield return null;
            yield return null;

            yield return Capture("01-light-on-normal.png");

            Button lamp = FindButton("LampHotspot");
            Assert.That(lamp, Is.Not.Null, "Home runtime must expose the real LampHotspot interaction");
            if (!bootstrap.Session.State.lampOn)
            {
                lamp.onClick.Invoke();
                yield return null;
            }
            Assert.That(bootstrap.Session.State.lampOn, Is.True);

            lamp.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(bootstrap.Session.State.lampOn, Is.False);
            yield return Capture("02-light-off-normal.png");

            lamp.onClick.Invoke();
            yield return null;
            Assert.That(bootstrap.Session.State.lampOn, Is.True);

            ForceLightning(bootstrap.View);
            bootstrap.View.Tick(.02f);
            yield return null;
            yield return null;
            yield return Capture("03-lightning-on.png");

            lamp.onClick.Invoke();
            yield return null;
            ForceLightning(bootstrap.View);
            bootstrap.View.Tick(.02f);
            yield return null;
            yield return null;
            yield return Capture("04-lightning-off.png");

            lamp.onClick.Invoke();
            yield return null;
            for (int i = 0; i < 240; i++)
            {
                bootstrap.View.Tick(1f / 60f);
                yield return null;
            }
            yield return null;
            yield return Capture("05-idle-rain-wet-glass.png");

            Assert.That(File.Exists(Path.Combine(captureDirectory, "01-light-on-normal.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(captureDirectory, "02-light-off-normal.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(captureDirectory, "03-lightning-on.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(captureDirectory, "04-lightning-off.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(captureDirectory, "05-idle-rain-wet-glass.png")), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private void ConfigureRenderTextureCapture()
        {
            captureCamera = root.GetComponentInChildren<Camera>(true);
            captureCanvas = root.GetComponentInChildren<Canvas>(true);
            Assert.That(captureCamera, Is.Not.Null, "Diagnostic capture needs the real RokasCamera");
            Assert.That(captureCanvas, Is.Not.Null, "Diagnostic capture needs the real RokasCanvas");

            captureTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            captureTarget.name = "HomeWeather3DiagnosticCapture";
            captureTarget.Create();

            captureCamera.targetTexture = captureTarget;
            captureCamera.cullingMask = ~0;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.black;

            captureCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            captureCanvas.worldCamera = captureCamera;
            captureCanvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
        }

        private IEnumerator Capture(string fileName)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            captureCamera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = captureTarget;
            Texture2D capture = new Texture2D(captureTarget.width, captureTarget.height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, captureTarget.width, captureTarget.height), 0, 0, false);
            capture.Apply(false, false);
            RenderTexture.active = previous;

            byte[] png = capture.EncodeToPNG();
            UnityEngine.Object.Destroy(capture);
            File.WriteAllBytes(Path.Combine(captureDirectory, fileName), png);
        }

        private Button FindButton(string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            return null;
        }

        private static void ForceLightning(RokasView view)
        {
            object effects = typeof(RokasView)
                .GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(view);
            Assert.That(effects, Is.Not.Null);
            MethodInfo trigger = effects.GetType().GetMethod("ForceLightning", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(trigger, Is.Not.Null);
            trigger.Invoke(effects, null);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (captureCamera != null) captureCamera.targetTexture = null;
            if (captureTarget != null)
            {
                captureTarget.Release();
                UnityEngine.Object.Destroy(captureTarget);
            }
            captureTarget = null;
            captureCamera = null;
            captureCanvas = null;

            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            if (!string.IsNullOrEmpty(saveDirectory) && Directory.Exists(saveDirectory))
                Directory.Delete(saveDirectory, true);
        }
    }
}
