using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Tests
{
    /// <summary>
    /// Prevent a regression to a single opaque fade or solid rectangular
    /// eye shutters. Inspects the exact pixels used by the real runtime
    /// Texture2D generation, without requiring a GPU/headless screenshot.
    /// </summary>
    public sealed class RokasBinocularSleepV10Tests
    {
        private static Type MaskType
        {
            get
            {
                var type = typeof(RokasBootstrap).Assembly.GetType(
                    "Rokas.Presentation.RokasSleepEyeAperture", false);
                Assert.That(type, Is.Not.Null, "ROKAS binocular runtime missing");
                return type;
            }
        }

        private static Color32[] Frame(float openness)
        {
            var method = MaskType.GetMethod("RasterizeMask",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null,
                "The production mask must offer its real CPU raster for QA");
            var result = method.Invoke(null, new object[] { openness }) as Color32[];
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(320 * 180));
            return result;
        }

        private static int Alpha(Color32[] pixels, float u, float v)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(u * 320), 0, 319);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * 180), 0, 179);
            return pixels[y * 320 + x].a;
        }

        [Test]
        public void TwoSeparateOvalOpeningsShrinkInHeightButKeepWidth()
        {
            var open = Frame(1f);
            var large = Frame(.75f);
            var medium = Frame(.50f);
            var slit = Frame(.25f);
            var closed = Frame(0f);

            Assert.That(Alpha(open, 0f, 0f), Is.EqualTo(0),
                "Open eyes must have a transparent full screen, no rectangle");
            Assert.That(Alpha(large, .25f, .50f), Is.LessThan(20),
                "Left oval interior must show the room");
            Assert.That(Alpha(large, .75f, .50f), Is.LessThan(20),
                "Right oval interior must show the room");
            Assert.That(Alpha(large, .50f, .50f), Is.GreaterThan(220),
                "Two separate eyes must have an opaque black middle bridge");
            Assert.That(Alpha(large, .02f, .04f), Is.GreaterThan(240),
                "Periphery must be black, not a uniform fullscreen fade");

            Assert.That(Alpha(medium, .25f, .70f), Is.LessThan(60),
                "Midway through transition the eye must still be open");
            Assert.That(Alpha(slit, .25f, .70f), Is.GreaterThan(230),
                "Upper/lower curved boundaries must actually close inwards");
            Assert.That(Alpha(slit, .25f, .50f), Is.LessThan(20),
                "An oval slit must remain before full closure");
            Assert.That(Alpha(slit, .75f, .50f), Is.LessThan(20));
            Assert.That(Alpha(slit, .08f, .50f), Is.LessThan(60),
                "At nearly closed, the opening must stay horizontally wide");
            Assert.That(Alpha(closed, .25f, .50f), Is.EqualTo(255),
                "Two windows must eventually close fully");
        }

        [Test]
        public void DedicatedOverlayCanvasIsVisibleAndDoesNotBlockMouse()
        {
            GameObject root = null;
            IDisposable mask = null;
            try
            {
                root = new GameObject("BinocularEye_TestRoot",
                    typeof(RectTransform), typeof(Canvas));
                var type = MaskType;
                mask = Activator.CreateInstance(type,
                    BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public, null,
                    new object[] { (RectTransform)root.transform }, null)
                    as IDisposable;
                Assert.That(mask, Is.Not.Null);

                type.GetMethod("SetOpen").Invoke(mask, new object[] { .50f });
                var canvas = root.GetComponentInChildren<Canvas>(true);
                // GetComponentInChildren returns root Canvas first. Find the
                // dedicated nested overlay by its exact authored name.
                var overlays = root.GetComponentsInChildren<Canvas>(true);
                Canvas overlay = null;
                foreach (Canvas candidate in overlays)
                    if (candidate.name.StartsWith("ROKAS_Eyelids_ScreenOverlay"))
                        overlay = candidate;
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.overrideSorting, Is.True);
                Assert.That(overlay.sortingOrder, Is.GreaterThanOrEqualTo(30000));

                var graphic = overlay.GetComponentInChildren<RawImage>(true);
                Assert.That(graphic, Is.Not.Null);
                Assert.That(graphic.raycastTarget, Is.False);
                Assert.That(graphic.maskable, Is.False);
                Assert.That(graphic.texture, Is.Not.Null);
                Assert.That(graphic.texture.name, Does.StartWith("ROKAS_DualOvalVision_"));
                Assert.That((bool)type.GetProperty("IsVisible").GetValue(mask), Is.True);

                type.GetMethod("SetVisible").Invoke(mask, new object[] { false });
                Assert.That((bool)type.GetProperty("IsVisible").GetValue(mask), Is.False);
                type.GetMethod("SetVisible").Invoke(mask, new object[] { true });
                Assert.That((bool)type.GetProperty("IsVisible").GetValue(mask), Is.True);
            }
            finally
            {
                mask?.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
