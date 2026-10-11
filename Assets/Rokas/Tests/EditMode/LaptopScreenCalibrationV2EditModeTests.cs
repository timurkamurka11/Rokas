using System;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Tests
{
    // Only for EditMode geometry tests; runtime never instantiates this helper.
    public sealed class LaptopCalibrationQuadProbe : LaptopPerspectiveQuad
    {
        public void Build(VertexHelper vertices) { base.OnPopulateMesh(vertices); }
    }

    public sealed class LaptopScreenCalibrationV2EditModeTests
    {
        [Serializable]
        private sealed class Preset
        {
            public int formatVersion;
            public string coordinateSpace;
            public int sourceWidth, sourceHeight;
            public float topLeftX, topLeftY, topRightX, topRightY;
            public float bottomRightX, bottomRightY, bottomLeftX, bottomLeftY;
            public float powerX, powerY;
            public bool enableRoundedMask;
            public float topLeftRadius, topRightRadius;
            public float bottomRightRadius, bottomLeftRadius;
        }

        [Test]
        public void ResourceContainsExactUserV2InnerCorners_NotAnExtraLegacyInset()
        {
            TextAsset original = Resources.Load<TextAsset>(
                "LaptopCinematic/screen_calibration_v2");
            Assert.That(original, Is.Not.Null,
                "The exported V2 calibration must be bundled as a Unity TextAsset.");
            Preset p = JsonUtility.FromJson<Preset>(original.text);
            Assert.That(p.formatVersion, Is.EqualTo(2));
            Assert.That(p.coordinateSpace, Is.EqualTo("source_image_pixels_top_left"));
            Assert.That(p.sourceWidth, Is.EqualTo(1672));
            Assert.That(p.sourceHeight, Is.EqualTo(941));
            Assert.That(p.topLeftX, Is.EqualTo(608.9489746f).Within(.001f));
            Assert.That(p.topLeftY, Is.EqualTo(412.8598938f).Within(.001f));
            Assert.That(p.topRightX, Is.EqualTo(1059.7675781f).Within(.001f));
            Assert.That(p.topRightY, Is.EqualTo(412.5315247f).Within(.001f));
            Assert.That(p.bottomRightX, Is.EqualTo(1075.6923828f).Within(.001f));
            Assert.That(p.bottomRightY, Is.EqualTo(680.4259033f).Within(.001f));
            Assert.That(p.bottomLeftX, Is.EqualTo(594.8300781f).Within(.001f));
            Assert.That(p.bottomLeftY, Is.EqualTo(680.1333008f).Within(.001f));
            Assert.That(p.powerX, Is.EqualTo(1014f));
            Assert.That(p.powerY, Is.EqualTo(703f));
            Assert.That(p.enableRoundedMask, Is.True);
            Assert.That(p.topLeftRadius, Is.EqualTo(18f));
            Assert.That(p.topRightRadius, Is.EqualTo(18f));
            Assert.That(p.bottomRightRadius, Is.EqualTo(6f));
            Assert.That(p.bottomLeftRadius, Is.EqualTo(6f));
        }

        [Test]
        public void RoundedMeshIsActualUVGeometry_NotJustAnOutline()
        {
            GameObject host = new GameObject("TestLCDRounded", typeof(RectTransform),
                typeof(CanvasRenderer));
            try
            {
                var graphic = host.AddComponent<LaptopCalibrationQuadProbe>();
                graphic.SetCorners(new[]
                {
                    new Vector2(608.94897f, 412.85989f),
                    new Vector2(1059.76758f, 412.53152f),
                    new Vector2(1075.69238f, 680.42590f),
                    new Vector2(594.83008f, 680.13330f)
                }, 1f);
                graphic.SetRoundedCorners(true, 18f, 18f, 6f, 6f);
                Assert.That(graphic.RoundedCornersEnabled, Is.True);
                Assert.That(graphic.CornerRadiiSourcePixels,
                    Is.EqualTo(new Vector4(18f, 18f, 6f, 6f)));

                using (var vertices = new VertexHelper())
                {
                    graphic.Build(vertices);
                    // 4 arcs of 13 points + center; 52 triangles, same
                    // tessellation as the JSON V2 Calibration Workshop preview.
                    Assert.That(vertices.currentVertCount, Is.EqualTo(53));
                    Assert.That(vertices.currentIndexCount, Is.EqualTo(156));
                    UIVertex center = UIVertex.simpleVert;
                    vertices.PopulateUIVertex(ref center, 0);
                    Assert.That(center.uv0.x, Is.EqualTo(.5f).Within(.0001f));
                    Assert.That(center.uv0.y, Is.EqualTo(.5f).Within(.0001f));
                }

                // Old pre-V2 projects still have an exact 4-point fallback.
                graphic.SetRoundedCorners(false, 18f, 18f, 6f, 6f);
                using (var legacy = new VertexHelper())
                {
                    graphic.Build(legacy);
                    Assert.That(legacy.currentVertCount, Is.EqualTo(4));
                    Assert.That(legacy.currentIndexCount, Is.EqualTo(6));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void WakeQuadAndExactYomiInheritOneRoundedMeshImplementation()
        {
            Assert.That(typeof(LaptopWakeQuad).IsSubclassOf(
                typeof(LaptopPerspectiveQuad)), Is.True);
            Assert.That(typeof(LaptopPhysicalDesktopClock).IsSubclassOf(
                typeof(LaptopPerspectiveQuad)), Is.True);
        }
    }
}
