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

            // In Linux batch mode ScreenCapture may report a destination
            // without actually producing a file. Use genuine offscreen camera
            // RenderTexture pixels from the proven LaptopUnityOffscreenProof test.
            Button power = null;
            foreach (Button btn in root.GetComponentsInChildren<Button>(true))
                if (btn.name == "PowerKeyClickTarget") { power = btn; break; }
            yield return new WaitForSecondsRealtime(1.90f);
            Assert.That(power, Is.Not.Null);
            Assert.That(power.gameObject.activeInHierarchy, Is.True,
                "Power selection must be visible before the finger interaction.");
            power.onClick.Invoke();

            string folder = Path.Combine(Application.dataPath, "..", "ROKAS_Unity_Art_Captures");
            Directory.CreateDirectory(folder);
            // These timestamps are seconds after clicking Power. The 49-frame
            // overlay starts at local t=1.8 and contacts Power near t=3.1.
            float[] offsets = { .04f, .70f, 1.17f, 1.30f, 1.38f, 1.54f, 2.65f };
            float elapsed = 0f;
            for (int i = 0; i < offsets.Length; i++)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(.01f, offsets[i] - elapsed));
                elapsed = offsets[i];
                string target = Path.Combine(folder, "Unity_Laptop_Stage_" + i.ToString("00") + ".png");
                LaptopUnityOffscreenProofPlayModeTests.RenderUI(root, target);
                Assert.That(File.Exists(target), Is.True, "Missing Unity RenderTexture PNG: " + target);
                Assert.That(new FileInfo(target).Length, Is.GreaterThan(3000),
                    "Empty Unity offscreen screenshot at offset " + offsets[i]);
                Debug.Log("[ROKAS-UNITY-REAL-POWER-CAPTURE] " + offsets[i] + "s -> " + target);
            }
            Assert.That(boot.View.LaptopOpen, Is.True,
                "The first Power interaction must complete and open the existing YOMI.");
            UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(saves)) Directory.Delete(saves,true);
        }
    }
}
