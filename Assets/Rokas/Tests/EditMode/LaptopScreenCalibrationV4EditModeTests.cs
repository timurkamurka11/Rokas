using System;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class LaptopV4MeshProbe : LaptopPerspectiveQuad
    {
        public void Build(VertexHelper helper) { base.OnPopulateMesh(helper); }
    }

    public sealed class LaptopScreenCalibrationV4EditModeTests
    {
        [Serializable]
        private sealed class Preset
        {
            public int formatVersion, sourceWidth, sourceHeight;
            public string coordinateSpace;
            public float topLeftX, topLeftY, topRightX, topRightY;
            public float bottomRightX, bottomRightY, bottomLeftX, bottomLeftY;
            public float powerX, powerY;
            public bool enableRoundedMask, enableEdgeControlPoints, enableCornerHelpers;
            public float topLeftRadius, topRightRadius, bottomRightRadius, bottomLeftRadius;
            public float topMidX, topMidY, rightMidX, rightMidY;
            public float bottomMidX, bottomMidY, leftMidX, leftMidY;
            public float tlHx, tlHy, tlVx, tlVy, trHx, trHy, trVx, trVy;
            public float brHx, brHy, brVx, brVy, blHx, blHy, blVx, blVy;
        }

        private static readonly Vector2[] TangentUV =
        {
            new Vector2(.18f, 0f), new Vector2(0f, .18f),
            new Vector2(.82f, 0f), new Vector2(1f, .18f),
            new Vector2(.82f, 1f), new Vector2(1f, .82f),
            new Vector2(.18f, 1f), new Vector2(0f, .82f)
        };

        private static Preset Load()
        {
            TextAsset original = Resources.Load<TextAsset>(
                "LaptopCinematic/screen_calibration_v4");
            Assert.That(original, Is.Not.Null,
                "Latest owner V4 calibration must be bundled as Resources TextAsset.");
            return JsonUtility.FromJson<Preset>(original.text);
        }

        private static Vector2[] Corners(Preset p) => new[]
        {
            new Vector2(p.topLeftX, p.topLeftY),
            new Vector2(p.topRightX, p.topRightY),
            new Vector2(p.bottomRightX, p.bottomRightY),
            new Vector2(p.bottomLeftX, p.bottomLeftY)
        };

        private static Vector2[] Middle(Preset p) => new[]
        {
            new Vector2(p.topMidX, p.topMidY),
            new Vector2(p.rightMidX, p.rightMidY),
            new Vector2(p.bottomMidX, p.bottomMidY),
            new Vector2(p.leftMidX, p.leftMidY)
        };

        private static Vector2[] Tangents(Preset p) => new[]
        {
            new Vector2(p.tlHx, p.tlHy), new Vector2(p.tlVx, p.tlVy),
            new Vector2(p.trHx, p.trHy), new Vector2(p.trVx, p.trVy),
            new Vector2(p.brHx, p.brHy), new Vector2(p.brVx, p.brVy),
            new Vector2(p.blHx, p.blHy), new Vector2(p.blVx, p.blVy)
        };

        private static void Deltas(Preset p, out Vector2[] edge,
            out Vector2[] helper)
        {
            Vector2[] corners = Corners(p), middle = Middle(p);
            edge = new Vector2[4];
            for (int i = 0; i < 4; i++)
                edge[i] = middle[i] -
                    (corners[i] + corners[(i + 1) % 4]) * .5f;

            helper = new Vector2[8];
            Vector2[] tangent = Tangents(p);
            for (int i = 0; i < 8; i++)
                helper[i] = tangent[i] - LaptopPerspectiveQuad.MapUV(
                    TangentUV[i], corners, edge, new Vector2[8], true);
        }

        private static void Near(Vector2 actual, Vector2 expected, float epsilon = .01f)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(epsilon));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(epsilon));
        }

        [Test]
        public void BundledV4MatchesFinalOwnerCornersAndRadii()
        {
            Preset p = Load();
            Assert.That(p.formatVersion, Is.EqualTo(4));
            Assert.That(p.coordinateSpace, Is.EqualTo("source_image_pixels_top_left"));
            Assert.That(p.sourceWidth, Is.EqualTo(1672));
            Assert.That(p.sourceHeight, Is.EqualTo(941));
            Assert.That(p.enableEdgeControlPoints && p.enableCornerHelpers &&
                p.enableRoundedMask, Is.True);
            Near(new Vector2(p.topLeftX,p.topLeftY), new Vector2(608.9489746f, 412.8598938f));
            Near(new Vector2(p.topRightX,p.topRightY), new Vector2(1059.7675781f, 412.5315247f));
            Near(new Vector2(p.bottomRightX,p.bottomRightY), new Vector2(1075.6923828f, 680.4259033f));
            Near(new Vector2(p.bottomLeftX,p.bottomLeftY), new Vector2(594.8300781f, 680.1333008f));
            Assert.That(p.topLeftRadius, Is.EqualTo(14f));
            Assert.That(p.topRightRadius, Is.EqualTo(14f));
            Assert.That(p.bottomRightRadius, Is.EqualTo(0f));
            Assert.That(p.bottomLeftRadius, Is.EqualTo(0f));
            Assert.That(p.powerX, Is.EqualTo(1014f));
            Assert.That(p.powerY, Is.EqualTo(703f));
            Vector2[] actualMidpoints = Middle(p);
            Vector2[] expectedMidpoints =
            {
                new Vector2(834.3582764f, 412.6957092f),
                new Vector2(1067.7299805f, 546.4786987f),
                new Vector2(835.2612305f, 680.2796021f),
                new Vector2(601.4862671f, 546.4616089f)
            };
            for (int i = 0; i < 4; i++)
                Near(actualMidpoints[i], expectedMidpoints[i], .0002f);

            Vector2[] exactTangentPoints =
            {
                new Vector2(690.0963135f, 412.8007813f),
                new Vector2(606.3204956f, 460.8742676f),
                new Vector2(978.6202393f, 412.5906372f),
                new Vector2(1062.6340332f, 460.7525024f),
                new Vector2(989.137146f, 680.37323f),
                new Vector2(1072.8259277f, 632.204895f),
                new Vector2(681.3853149f, 680.1859741f),
                new Vector2(596.2125854f, 633.5982666f)
            };
            Vector2[] actualTangents = Tangents(p);
            for (int i = 0; i < 8; i++)
                Near(actualTangents[i], exactTangentPoints[i], .0002f);
        }

        [Test]
        public void WarpedMeshHitsAllSixteenOwnerControlPoints()
        {
            Preset p = Load();
            Deltas(p, out Vector2[] edges, out Vector2[] helpers);
            GameObject host = new GameObject("V4LCDProbe", typeof(RectTransform),
                typeof(CanvasRenderer));
            try
            {
                LaptopV4MeshProbe quad = host.AddComponent<LaptopV4MeshProbe>();
                quad.SetCorners(Corners(p), 1f);
                quad.SetRoundedCorners(p.enableRoundedMask, p.topLeftRadius,
                    p.topRightRadius, p.bottomRightRadius, p.bottomLeftRadius);
                quad.SetWarpOffsets(edges, helpers);
                Assert.That(quad.UsingV4Warp, Is.True);

                Vector2[] uvCorners =
                {
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up
                };
                for (int i = 0; i < 4; i++)
                    Near(quad.EvaluateLCDPixel(uvCorners[i]), Corners(p)[i]);
                Vector2[] uvMid =
                {
                    new Vector2(.5f,0f), new Vector2(1f,.5f),
                    new Vector2(.5f,1f), new Vector2(0f,.5f)
                };
                for (int i = 0; i < 4; i++)
                    Near(quad.EvaluateLCDPixel(uvMid[i]), Middle(p)[i]);
                for (int i = 0; i < 8; i++)
                    Near(quad.EvaluateLCDPixel(TangentUV[i]), Tangents(p)[i]);

                using (var vertices = new VertexHelper())
                {
                    quad.Build(vertices);
                    // Top rounded arcs each 13 points; two sharp bottom
                    // points -> 28 boundary vertices; 10 rings + center.
                    Assert.That(vertices.currentVertCount, Is.EqualTo(281));
                    Assert.That(vertices.currentIndexCount, Is.EqualTo(1596));
                    UIVertex center = UIVertex.simpleVert;
                    vertices.PopulateUIVertex(ref center, 0);
                    Near(center.uv0, new Vector2(.5f, .5f), .0001f);
                    UIVertex outer = UIVertex.simpleVert;
                    vertices.PopulateUIVertex(ref outer, 1 + 9 * 28);
                    Vector2[] bounds = Corners(p);
                    float height = (Vector2.Distance(bounds[0], bounds[3]) +
                        Vector2.Distance(bounds[1], bounds[2])) * .5f;
                    // First actual rounded boundary point (TL, 180 deg).
                    Vector2 firstUV = new Vector2(0f, p.topLeftRadius / height);
                    Near(outer.uv0, new Vector2(firstUV.x, 1f - firstUV.y), .0001f);
                    Near(new Vector2(outer.position.x, -outer.position.y),
                        quad.EvaluateLCDPixel(firstUV), .02f);
                }
                // Exactly the same screen component is used as physical
                // wake glow, chibi/YOMI stencils, exact desktop and fallback.
                Assert.That(typeof(LaptopWakeQuad).IsSubclassOf(
                    typeof(LaptopPerspectiveQuad)), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void WarpingDoesNotDestroyV2FourCornerFallback()
        {
            GameObject host = new GameObject("V4FallbackTest",
                typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                LaptopV4MeshProbe q = host.AddComponent<LaptopV4MeshProbe>();
                q.SetCorners(Corners(Load()), 1f);
                q.SetRoundedCorners(false, 0f, 0f, 0f, 0f);
                q.SetWarpOffsets(null, null);
                using (var vertices = new VertexHelper())
                {
                    q.Build(vertices);
                    Assert.That(vertices.currentVertCount, Is.EqualTo(4));
                    Assert.That(vertices.currentIndexCount, Is.EqualTo(6));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
