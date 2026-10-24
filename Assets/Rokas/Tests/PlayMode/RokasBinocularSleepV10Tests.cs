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
    /// Regression for the player's V10 video: two BLACK oval stamps on
    /// the ceiling are wrong. V11 must generate mesh polygons ONLY outside
    /// two empty eye windows. These tests exercise actual mesh triangles.
    /// </summary>
    public sealed class RokasBinocularSleepV10Tests
    {
        private static Type MaskType => typeof(RokasBootstrap).Assembly.GetType(
            "Rokas.Presentation.RokasSleepEyeAperture", true);

        private static Type GraphicType => typeof(RokasBootstrap).Assembly.GetType(
            "Rokas.Presentation.RokasDualOvalLidGraphic", true);

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float x1 = b.x - a.x, y1 = b.y - a.y;
            float x2 = c.x - a.x, y2 = c.y - a.y;
            float den = x1 * y2 - y1 * x2;
            if (Mathf.Abs(den) < .0001f) return false;
            float t = ((p.x - a.x) * y2 - (p.y - a.y) * x2) / den;
            float u = (x1 * (p.y - a.y) - y1 * (p.x - a.x)) / den;
            return t >= -.0001f && u >= -.0001f && t + u <= 1.0001f;
        }

        private static bool MeshCovers(List<UIVertex> verts, Vector2 at)
        {
            for (int i = 0; i + 2 < verts.Count; i += 3)
            {
                if (verts[i].color.a == 0 && verts[i+1].color.a == 0 &&
                    verts[i+2].color.a == 0) continue;
                if (InTriangle(at, verts[i].position, verts[i+1].position,
                    verts[i+2].position)) return true;
            }
            return false;
        }

        [Test]
        public void NegativeSpaceMeshNeverDrawsBlackOvalStamps()
        {
            var go = new GameObject("V11NegativeSpaceTest",
                typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                var r = (RectTransform)go.transform;
                r.sizeDelta = new Vector2(1920f, 1080f);
                var graphic = go.AddComponent(GraphicType) as MaskableGraphic;
                Assert.That(graphic, Is.Not.Null);
                graphic.maskable = false;
                graphic.raycastTarget = false;

                var setter = GraphicType.GetProperty("OpenAmount");
                var pop = GraphicType.GetMethod("OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pop, Is.Not.Null);
                foreach (float o in new float[]{ .75f, .50f, .25f })
                {
                    setter.SetValue(graphic, o);
                    var vh = new VertexHelper();
                    pop.Invoke(graphic, new object[]{ vh });
                    var tri = new List<UIVertex>();
                    vh.GetUIVertexStream(tri);
                    Assert.That(tri.Count, Is.GreaterThan(0));

                    var cr = r.rect;
                    Vector2 LeftEye = new Vector2(cr.xMin + cr.width*.25f, cr.center.y);
                    Vector2 RightEye = new Vector2(cr.xMin + cr.width*.75f, cr.center.y);
                    Vector2 Bridge = new Vector2(cr.center.x, cr.center.y);
                    Vector2 Edge = new Vector2(cr.xMin + cr.width*.02f,
                        cr.yMin + cr.height*.02f);

                    Assert.That(MeshCovers(tri, LeftEye), Is.False,
                        "V11 must not cover the LEFT eye center with a black oval");
                    Assert.That(MeshCovers(tri, RightEye), Is.False,
                        "V11 must not cover the RIGHT eye center with a black oval");
                    Assert.That(MeshCovers(tri, Bridge), Is.True,
                        "The middle bridge should be black outside the lenses");
                    Assert.That(MeshCovers(tri, Edge), Is.True,
                        "The top-left periphery should be covered, not the pupil");
                    vh.Dispose();
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void FullOpenMeshEmptyClosedMeshOpaqueAndCanvasUnobstructed()
        {
            var root = new GameObject("V11MaskTestRoot",
                typeof(RectTransform), typeof(Canvas));
            IDisposable mask = null;
            try
            {
                var r = (RectTransform)root.transform;
                r.sizeDelta = new Vector2(1920f, 1080f);
                mask = Activator.CreateInstance(MaskType,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic, null, new object[]{r}, null)
                    as IDisposable;
                Assert.That(mask, Is.Not.Null);
                var graphic = root.GetComponentInChildren(GraphicType, true)
                    as MaskableGraphic;
                Assert.That(graphic, Is.Not.Null);
                Assert.That(graphic.raycastTarget, Is.False);
                Assert.That(graphic.maskable, Is.False);
                var canvases = root.GetComponentsInChildren<Canvas>(true);
                Assert.That(canvases.Length, Is.GreaterThanOrEqualTo(2));
                Assert.That(canvases[canvases.Length - 1].sortingOrder,
                    Is.GreaterThanOrEqualTo(30000));

                MaskType.GetMethod("SetOpen").Invoke(mask, new object[]{1f});
                var vh = new VertexHelper();
                GraphicType.GetMethod("OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(graphic, new object[]{vh});
                Assert.That(vh.currentVertCount, Is.EqualTo(0),
                    "Fully open view must never display a rectangle");
                vh.Dispose();

                MaskType.GetMethod("SetOpen").Invoke(mask, new object[]{0f});
                vh = new VertexHelper();
                GraphicType.GetMethod("OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(graphic, new object[]{vh});
                Assert.That(vh.currentVertCount, Is.GreaterThan(0));
                vh.Dispose();

                Assert.That((bool)MaskType.GetProperty("IsVisible").GetValue(mask), Is.True);
                MaskType.GetMethod("SetVisible").Invoke(mask, new object[]{false});
                Assert.That((bool)MaskType.GetProperty("IsVisible").GetValue(mask), Is.False);
            }
            finally
            {
                mask?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
