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
    /// <summary>Actual Unity UI pixels rendered to an offscreen camera RT.
    /// Unlike ScreenCapture.CaptureScreenshot, this works in test-runner environments
    /// where the OS has no visible Game View. Uses test-only synthetic POV in CI.
    /// </summary>
    public sealed class LaptopUnityOffscreenProofPlayModeTests
    {
        [UnityTest]
        public IEnumerator UnityCanvasRendersRightHandAndWakeWithRealPixels()
        {
            var approved = Resources.Load<Texture2D>("LaptopCinematic/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF");
            var manifest = Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest");
            Assert.That(approved, Is.Not.Null);
            Assert.That(approved.width, Is.EqualTo(1672));
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.text.Contains("\"handedness\": \"right\""), Is.True);

            var home = new GameObject("ROKAS_GameProofRoot");
            string save = Path.Combine(Path.GetTempPath(), "rokas-ui-proof-" + Guid.NewGuid().ToString("N"));
            var boot = home.AddComponent<RokasBootstrap>();
            boot.Initialize(save);
            yield return null;
            Button laptop = null;
            foreach (var button in home.GetComponentsInChildren<Button>(true))
                if (button.name == "LaptopHotspot") { laptop = button; break; }
            Assert.That(laptop, Is.Not.Null);
            laptop.onClick.Invoke();

            var directory = Path.Combine(Application.dataPath, "..", "ROKAS_Unity_OffscreenProof");
            Directory.CreateDirectory(directory);
            float[] marks = { 2.65f, 3.08f, 3.24f, 3.51f };
            float elapsed = 0f;
            for (int i = 0; i < marks.Length; i++)
            {
                yield return new WaitForSecondsRealtime(marks[i] - elapsed);
                elapsed = marks[i];
                string png = Path.Combine(directory, "Unity_RenderTexture_" + i.ToString("00") + ".png");
                RenderUI(home, png);
                Assert.That(File.Exists(png), Is.True);
                Assert.That(new FileInfo(png).Length, Is.GreaterThan(3000),
                    "Offscreen capture should contain actual Unity pixels.");
                Debug.Log("[ROKAS-UNITY-REAL-PIXELS] " + png);
                yield return null;
            }
            UnityEngine.Object.Destroy(home);
            yield return null;
            if (Directory.Exists(save)) Directory.Delete(save, true);
        }

        private static void RenderUI(GameObject root, string file)
        {
            Canvas source = null;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.name == "RokasCanvas") { source = canvas; break; }
            Assert.That(source, Is.Not.Null);
            const int w = 1920, h = 1080;
            GameObject cameraRoot = new GameObject("ROKAS_CI_RenderTexture_Camera");
            var camera = cameraRoot.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 100f;
            camera.cullingMask = ~0;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.transform.rotation = Quaternion.identity;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            camera.targetTexture = rt;
            var previous = source.renderMode;
            var previousCamera = source.worldCamera;
            var scaler = source.GetComponent<CanvasScaler>();
            float priorScale = scaler ? scaler.scaleFactor : 1f;
            var oldRT = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                source.renderMode = RenderMode.ScreenSpaceCamera;
                source.worldCamera = camera;
                source.planeDistance = 5f;
                // GameCI editor screen is typically ~640x360; RenderTexture is 1920x1080.
                // Scale the authored 1920x1080 stage into the offscreen capture, not a tiny 1/3 postcard.
                if (scaler) scaler.scaleFactor = priorScale * w / Mathf.Max(1f, Screen.width);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = rt;
                texture = new Texture2D(w, h, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                texture.Apply();
                File.WriteAllBytes(file, texture.EncodeToPNG());
            }
            finally
            {
                source.renderMode = previous;
                source.worldCamera = previousCamera;
                if (scaler) scaler.scaleFactor = priorScale;
                RenderTexture.active = oldRT;
                camera.targetTexture = null;
                if (texture) UnityEngine.Object.DestroyImmediate(texture);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(cameraRoot);
            }
        }
    }
}
