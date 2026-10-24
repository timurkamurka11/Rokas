using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Tests
{
    /// <summary>
    /// V12 QA: verifies anatomically different upper/lower contours, gradual
    /// activation, negative-space mesh and no permanent binocular rim.
    /// Real Unity graphical acceptance is still a separate PlayMode gate.
    /// </summary>
    public sealed class RokasBinocularSleepV10Tests
    {
        private static Type TypeOf(string name) => typeof(RokasBootstrap).
            Assembly.GetType("Rokas.Presentation." + name, true);
        private static Type EyeType => TypeOf("RokasAnatomicEyelidGraphic");
        private static Type OverlayType => TypeOf("RokasSleepEyeAperture");
        private static MethodInfo Contours => EyeType.GetMethod("GetContourAt",
            BindingFlags.NonPublic | BindingFlags.Static);
        private static MethodInfo Mesh => EyeType.GetMethod("OnPopulateMesh",
            BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo Darkness => EyeType.GetMethod("Darkness",
            BindingFlags.NonPublic | BindingFlags.Static);

        private static bool Evaluate(float u, float openness, bool waking,
            out float upper, out float lower)
        {
            object[] args = { u, openness, waking, 0f, 0f };
            bool hasOpen = (bool)Contours.Invoke(null, args);
            upper = (float)args[3];
            lower = (float)args[4];
            return hasOpen;
        }

        [Test]
        public void FullOpenHasNoPermanentMaskAndFullClosedHasNoAperture()
        {
            Assert.That((float)Darkness.Invoke(null, new object[]{1f}),
                Is.LessThan(.0001f));
            Assert.That((float)Darkness.Invoke(null, new object[]{.90f}),
                Is.InRange(.01f, .99f));
            Assert.That((float)Darkness.Invoke(null, new object[]{.65f}),
                Is.GreaterThan(.97f));
            Assert.That(Evaluate(.25f, 0f, false,
                out float upper, out float lower), Is.False);
            Assert.That(upper, Is.EqualTo(lower).Within(.0001f));
        }

        [Test]
        public void BezierEyeContourIsTaperedNotPerfectSymmetricEllipse()
        {
            Assert.That(Evaluate(.25f, .55f, false,
                out float upper, out float lower), Is.True);
            Assert.That(upper, Is.GreaterThan(lower));
            Assert.That(Evaluate(.25f, .18f, false,
                out float slitTop, out float slitBottom), Is.True);
            Assert.That(slitTop - slitBottom, Is.LessThan(upper - lower));
            Assert.That(slitTop - slitBottom, Is.GreaterThan(.001f));
            Assert.That(Evaluate(.25f, .55f, false,
                out float leftTop, out float leftBottom), Is.True);
            Assert.That(Evaluate(.75f, .55f, false,
                out float rightTop, out float rightBottom), Is.True);
            Assert.That(Mathf.Abs(leftTop - rightTop) +
                Mathf.Abs(leftBottom - rightBottom), Is.GreaterThan(.0001f),
                "Both eyes must have slightly different shape and phase");

            Evaluate(.25f, .8f, false, out float topBefore, out float lowBefore);
            Evaluate(.25f, .3f, false, out float topAfter, out float lowAfter);
            Assert.That(topBefore - topAfter,
                Is.GreaterThan((lowAfter - lowBefore) * 1.4f),
                "Upper eyelid must account for most of the closing motion");
        }

        [Test]
        public void MaskSupportsSevenKeyFramesAndDifferentWakeCurve()
        {
            foreach(float openness in new[]{1f,.85f,.65f,.50f,.35f,.15f,0f})
            {
                Evaluate(.25f, openness, false, out float top, out float bottom);
                Assert.That(top, Is.GreaterThanOrEqualTo(bottom));
                Evaluate(.75f, openness, true, out top, out bottom);
                Assert.That(top, Is.GreaterThanOrEqualTo(bottom));
            }
            Evaluate(.25f, .5f, false, out float closeTop, out float closeBottom);
            Evaluate(.25f, .5f, true, out float wakeTop, out float wakeBottom);
            Assert.That(Mathf.Abs(closeTop - wakeTop) +
                Mathf.Abs(closeBottom - wakeBottom), Is.GreaterThan(.001f),
                "Waking cannot be a mechanical mirror of falling asleep");
        }

        [Test]
        public void RealGraphicHasNoQuadWhenFullyOpenAndNoRaycast()
        {
            GameObject root = new GameObject("V12PlayModeEyeTest",
                typeof(RectTransform),typeof(Canvas));
            IDisposable mask = null;
            try
            {
                var rt=(RectTransform)root.transform;
                rt.sizeDelta = new Vector2(1920f,1080f);
                mask = Activator.CreateInstance(OverlayType,
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance, null, new object[]{rt}, null)
                    as IDisposable;
                Assert.That(mask, Is.Not.Null);
                var graphic = root.GetComponentInChildren(EyeType,true) as MaskableGraphic;
                Assert.That(graphic, Is.Not.Null);
                Assert.That(graphic.raycastTarget, Is.False);
                Assert.That(graphic.maskable, Is.False);
                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                Assert.That(canvases.Length, Is.GreaterThanOrEqualTo(2));
                Assert.That(canvases[canvases.Length-1].sortingOrder,
                    Is.GreaterThanOrEqualTo(30000));

                var vh = new VertexHelper();
                Mesh.Invoke(graphic, new object[]{vh});
                Assert.That(vh.currentVertCount, Is.EqualTo(0));
                vh.Dispose();
                OverlayType.GetMethod("SetOpen").Invoke(mask,new object[]{.35f});
                vh = new VertexHelper();
                Mesh.Invoke(graphic, new object[]{vh});
                Assert.That(vh.currentVertCount, Is.GreaterThan(0));
                Assert.That(vh.currentVertCount, Is.LessThan(65535));
                vh.Dispose();

                OverlayType.GetMethod("SetVisible").Invoke(mask,new object[]{false});
                Assert.That((bool)OverlayType.GetProperty("IsVisible").GetValue(mask),
                    Is.False);
            }
            finally {
                mask?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
