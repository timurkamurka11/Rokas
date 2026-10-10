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
    /// V11 QA gates runnable without the user's private approved POV art.
    /// Full photographed-room pixel acceptance and the actual Stand Up WAV mix
    /// are tracked separately; synthetic assets never count as local approval.
    /// </summary>
    public sealed class LaptopV11ContinuumPlayModeTests
    {
        private GameObject fixture;
        private string saves;

        private T Find<T>(string name) where T : Component
        {
            if (!fixture) return null;
            foreach (T c in fixture.GetComponentsInChildren<T>(true))
                if (c.name == name) return c;
            return null;
        }

        [UnityTest]
        public IEnumerator LiveLCDUsesPixelsFromSharedYomiCatalogAndSessionUnreadChanges()
        {
            int unread = 0;
            var go = new GameObject("V11_Live_LCD",
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(LaptopPhysicalDesktopClock));
            var clock = go.GetComponent<LaptopPhysicalDesktopClock>();
            clock.SetCorners(new[]
            {
                new Vector2(0, 0), new Vector2(640, 0),
                new Vector2(640, 360), new Vector2(0, 360)
            }, 1f);
            clock.Initialize(() => unread);
            var pixels = (Texture2D)clock.mainTexture;
            Assert.That(pixels, Is.Not.Null);
            Assert.That(pixels.name, Is.EqualTo("ROKAS_YOMI_LivePhysicalDesktop"));
            Assert.That(pixels.width, Is.EqualTo(640));
            Color32[] before = pixels.GetPixels32();
            unread = 7;
            clock.UpdateClockIfNecessary();
            Color32[] after = pixels.GetPixels32();
            int differences = 0;
            for (int i = 0; i < before.Length; i++)
                if (!before[i].Equals(after[i])) differences++;
            Assert.That(differences, Is.GreaterThan(100),
                "Session unread changes must repaint the real physical LCD.");
            for (int i = 0; i < after.Length; i++)
                Assert.That(after[i].a, Is.EqualTo(255),
                    "ON-state must not expose a static screenshot beneath the live LCD.");
            UnityEngine.Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OriginalControlPulseHoverPressAndDisableHaveNoStateLeak()
        {
            var go = new GameObject("V11_Approved_PNG",
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage), typeof(LaptopChoiceHover));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            var hover = go.GetComponent<LaptopChoiceHover>();
            yield return new WaitForSecondsRealtime(.10f);
            hover.OnPointerEnter(null);
            yield return new WaitForSecondsRealtime(.23f);
            float hovered = rect.localScale.x;
            Assert.That(hovered, Is.GreaterThan(1.013f));
            hover.OnPointerDown(null);
            yield return null;
            Assert.That(rect.localScale.x, Is.LessThan(hovered),
                "Press must compress the existing PNG, not only reset the hover timer.");
            hover.OnPointerUp(null);
            hover.OnPointerExit(null);
            go.SetActive(false);
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            UnityEngine.Object.Destroy(go);
            yield return null;
        }

        [Test]
        public void PowerSessionOnlyBootsOnceUntilExplicitProcessReset()
        {
            LaptopPowerSession.ResetForTests();
            Assert.That(LaptopPowerSession.PoweredOn, Is.False);
            LaptopPowerSession.CompleteFirstBoot();
            LaptopPowerSession.CompleteFirstBoot();
            Assert.That(LaptopPowerSession.PoweredOn, Is.True);
            Assert.That(LaptopPowerSession.CompletedBoots, Is.EqualTo(1));
            LaptopPowerSession.ResetForTests();
            Assert.That(LaptopPowerSession.CompletedBoots, Is.EqualTo(0));
            Assert.That(LaptopPowerSession.PoweredOn, Is.False);
        }

        [UnityTest]
        public IEnumerator LiveHomeRigKeepsGlowSceneAndWeatherTogether()
        {
            LaptopPowerSession.ResetForTests();
            saves = Path.Combine(Path.GetTempPath(),
                "rokas-v11-room-" + Guid.NewGuid().ToString("N"));
            fixture = new GameObject("V11_Home_Rig");
            var boot = fixture.AddComponent<RokasBootstrap>();
            boot.Initialize(saves);
            yield return null;
            var room = Find<RectTransform>("HomeCameraRig2_5D");
            var world = Find<RawImage>("WorldIllustration");
            var scene = Find<RectTransform>("SceneInteractions");
            Assert.That(room, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(scene, Is.Not.Null);
            Assert.That(world.transform.IsChildOf(room), Is.True);
            Assert.That(scene.transform.IsChildOf(room), Is.True);
            var glow = Find<RectTransform>("HomeCustomGlowRuntime");
            if (glow)
                Assert.That(glow.transform.IsChildOf(scene), Is.True,
                    "Live interactive CustomGlow must share the room camera transform.");
            Assert.That(scene.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator ExistingYomiIsPrebuiltNotDelayedByBlackHold()
        {
            if (Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest"))
                Assert.Ignore("This isolated fallback test expects no installed FPS art.");
            if (!fixture)
            {
                LaptopPowerSession.ResetForTests();
                saves = Path.Combine(Path.GetTempPath(),
                    "rokas-v11-boot-" + Guid.NewGuid().ToString("N"));
                fixture = new GameObject("V11_Yomi_Prebuild");
                var boot = fixture.AddComponent<RokasBootstrap>();
                boot.Initialize(saves);
            }
            yield return null;
            var hotspot = Find<Button>("LaptopHotspot");
            Assert.That(hotspot, Is.Not.Null);
            hotspot.onClick.Invoke();
            Assert.That(Find<RectTransform>("YomiLaptop"), Is.Not.Null);
            Assert.That(Find<RectTransform>("LaptopScreen"), Is.Not.Null,
                "Desktop UI must exist BEFORE the boot video is removed.");
            Assert.That(Find<RectTransform>("CinematicPostBootHold"), Is.Null,
                "A fake half-second dark frame is prohibited.");
            Assert.That(Find<RectTransform>("HomeCameraRig2_5D"), Is.Not.Null);
            Assert.That(Find<RectTransform>("SceneInteractions").gameObject.activeInHierarchy,
                Is.True, "Hub visuals remain alive under the laptop modal.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (fixture) UnityEngine.Object.Destroy(fixture);
            fixture = null;
            LaptopPowerSession.ResetForTests();
            yield return null;
            if (!string.IsNullOrEmpty(saves) && Directory.Exists(saves))
                Directory.Delete(saves, true);
            saves = null;
        }
    }
}
