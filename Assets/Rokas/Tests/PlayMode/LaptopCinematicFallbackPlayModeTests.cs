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
    public sealed class LaptopCinematicFallbackPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator AbsentCinematicFramesKeepOriginalLaptopFunctional()
        {
            // The cloud branch contains the controller but intentionally does not
            // pretend the final 3D hand render sequence was produced.
            TextAsset manifest = Resources.Load<TextAsset>("LaptopCinematic/hands_manifest");
            if (manifest)
                Assert.Ignore("Fallback fixture applies only when hand frames are not installed.");

            directory = Path.Combine(Path.GetTempPath(),
                "rokas-laptop-cinematic-fallback-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("LaptopCinematicFallbackFixture");
            RokasBootstrap boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Button hotspot = null;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == "LaptopHotspot") { hotspot = button; break; }

            Assert.That(hotspot, Is.Not.Null);
            hotspot.onClick.Invoke();
            Assert.That(boot.View.LaptopOpen, Is.True,
                "Without hand assets, the original Laptop UI must open immediately.");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(boot.View.LaptopOpen, Is.False);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
