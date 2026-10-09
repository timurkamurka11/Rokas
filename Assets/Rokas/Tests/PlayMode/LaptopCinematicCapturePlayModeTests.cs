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
    /// <summary>
    /// Records real Unity Game View stages in a Linux GameCI runner.
    /// Cloud workflow supplies generated right-hand art and a clearly labeled
    /// synthetic POV placeholder, not the user's private approved background.
    /// </summary>
    public sealed class LaptopCinematicCapturePlayModeTests
    {
        [UnityTest]
        public IEnumerator RenderRightHandContactAndWakeFromLiveUnity()
        {
            Texture2D pov = Resources.Load<Texture2D>(
                "LaptopCinematic/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF");
            TextAsset manifest = Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest");
            if (!pov || !manifest)
                Assert.Ignore("CI must inject right-hand alpha frames and 1672x941 test POV.");

            Assert.That(pov.width, Is.EqualTo(1672));
            Assert.That(pov.height, Is.EqualTo(941));
            Assert.That(manifest.text.Contains("\"handedness\": \"right\""), Is.True);

            string saves = Path.Combine(Path.GetTempPath(), "rokas-cloud-art-"+Guid.NewGuid().ToString("N"));
            GameObject root = new GameObject("RokasCloudArtGameViewCapture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(saves);
            yield return null;

            Button hotspot = null;
            foreach (Button btn in root.GetComponentsInChildren<Button>(true))
                if (btn.name == "LaptopHotspot") { hotspot = btn; break; }
            Assert.That(hotspot, Is.Not.Null);
            hotspot.onClick.Invoke();

            RectTransform overlay = null;
            foreach (RectTransform tr in root.GetComponentsInChildren<RectTransform>(true))
                if (tr.name == "LaptopCinematicOverlay") { overlay = tr; break; }
            Assert.That(overlay, Is.Not.Null,
                "Missing right-hand manifest or wrong NPOT importer size: cinematic fell back.");

            string folder = Path.Combine(Application.dataPath, "..", "ROKAS_Unity_Art_Captures");
            Directory.CreateDirectory(folder);
            float[] stamps = { 1.85f, 2.6f, 3.08f, 3.22f, 3.51f, 4.16f };
            float elapsed = 0f;
            for (int i = 0; i < stamps.Length; i++)
            {
                float wait = Mathf.Max(.01f, stamps[i] - elapsed);
                yield return new WaitForSecondsRealtime(wait);
                elapsed = stamps[i];
                string target = Path.Combine(folder, "Unity_Laptop_Stage_" + i.ToString("00") + ".png");
                ScreenCapture.CaptureScreenshot(target);
                // Batchmode can log a screenshot path without writing a single image.
                // Do not report visual proof unless the actual PNG exists and has bytes.
                for (int attempt = 0; attempt < 60 && !File.Exists(target); attempt++)
                    yield return null;
                Assert.That(File.Exists(target), Is.True,
                    "No real Game View PNG produced in cloud Unity batch mode.");
                Assert.That(new FileInfo(target).Length, Is.GreaterThan(256),
                    "Empty Unity screenshot file.");
                Debug.Log("[ROKAS-UNITY-CAPTURE-VERIFIED] "+stamps[i]+"s -> "+target);
            }
            Assert.That(boot.View.LaptopOpen, Is.True);
            UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(saves)) Directory.Delete(saves,true);
        }
    }
}
