using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rokas.Tests
{
    public sealed class ReactiveCombatCompositePlayModeTests
    {
        private const string Resource = "Combat/ReactiveCombatPremultipliedUi";
        private static readonly Color Premultiplied = new Color(.4f, .2f, .1f, .5f);

        [UnityTest]
        public IEnumerator ActorRenderTexturePreservesPremultipliedRgbOnTheRealRawImage()
        {
            using (var ui = new CompositeFixture())
            {
                ui.SetSource(Premultiplied);
                yield return null;
                Color source = ui.SourcePixel();
                AssertColor(source, Premultiplied);
                Color result = ui.RenderPixel(Color.black);
                AssertColor(result, new Color(source.r, source.g, source.b, 1f));

                // The previous carrier multiplies the already-premultiplied RGB
                // by source alpha again. Exercise that actual UI material too.
                ui.Image.material = null;
                yield return null;
                Color previous = ui.RenderPixel(Color.black);
                Assert.That(previous.r, Is.EqualTo(source.r * source.a).Within(.015f));
                Assert.That(previous.g, Is.EqualTo(source.g * source.a).Within(.015f));
                Assert.That(previous.b, Is.EqualTo(source.b * source.a).Within(.015f));
                Assert.That(result.r, Is.GreaterThan(previous.r + .15f));
                TestContext.WriteLine("Colour space=" + QualitySettings.activeColorSpace +
                    "; source=" + source + "; premultiplied UI=" + result + "; previous UI=" + previous);
            }
        }

        [UnityTest]
        public IEnumerator TransparentPixelsAndUiFadesPreservePremultipliedCoverage()
        {
            using (var ui = new CompositeFixture())
            {
                // A zero-alpha carrier must never reveal colour from empty pixels.
                ui.SetSource(new Color(.4f, .2f, .1f, 0f));
                yield return null;
                AssertColor(ui.RenderPixel(Color.clear), Color.clear);

                ui.SetSource(Premultiplied);
                ui.Image.color = new Color(1f, 1f, 1f, .5f);
                yield return null;
                Color expected = Premultiplied * .5f;
                AssertColor(ui.RenderPixel(Color.clear), expected);

                ui.Image.color = Color.white;
                ui.Group.alpha = .5f;
                yield return null;
                AssertColor(ui.RenderPixel(Color.clear), expected);

                ui.Group.alpha = 0f;
                yield return null;
                AssertColor(ui.RenderPixel(Color.clear), Color.clear);
            }
        }

        [UnityTest]
        public IEnumerator RectMaskAndStencilClipBothColourAndCoverage()
        {
            foreach (bool stencil in new[] { false, true })
            {
                using (var ui = new CompositeFixture())
                {
                    ui.SetSource(Premultiplied);
                    ui.AddLeftHalfMask(stencil);
                    yield return null;
                    AssertColor(ui.RenderPixel(Color.clear, 16, 32), Premultiplied);
                    AssertColor(ui.OutputPixel(48, 32), Color.clear);
                    TestContext.WriteLine((stencil ? "Stencil Mask" : "RectMask2D") + " keeps the left half and clears the right half.");
                }
                yield return null;
            }
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.015f), "Red: " + actual);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.015f), "Green: " + actual);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.015f), "Blue: " + actual);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(.015f), "Coverage alpha: " + actual);
        }

        private sealed class CompositeFixture : IDisposable
        {
            private const int Side = 64;
            private const int Layer = 31;
            private readonly GameObject root;
            private readonly Camera camera;
            private readonly RectTransform canvasRect;
            private readonly Material material;
            private readonly RenderTexture source;
            private readonly RenderTexture output;
            private readonly Texture2D readback;
            public RawImage Image { get; }
            public CanvasGroup Group { get; }

            public CompositeFixture()
            {
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "This is an actual GPU compositing test.");
                Assert.That(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat), Is.True);
                var shader = Resources.Load<Shader>(Resource);
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.isSupported, Is.True);

                // Explicit linear storage/sampling avoids project colour-space
                // assumptions. White tint and black clear need no sRGB conversion.
                source = MakeTexture("Known premultiplied actor sample", 0);
                output = MakeTexture("Reactive UI composite probe", 24);
                readback = new Texture2D(Side, Side, TextureFormat.RGBAFloat, false, true);
                material = new Material(shader) { name = "Reactive composite regression material" };
                root = new GameObject("ReactiveCompositeTestRoot");

                var cameraObject = new GameObject("ReactiveCompositeTestCamera", typeof(Camera));
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
                camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 1f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 20f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.cullingMask = 1 << Layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = output;
                camera.enabled = false;

                var canvasObject = new GameObject("ReactiveCompositeTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                canvasObject.transform.SetParent(root.transform, false);
                canvasObject.layer = Layer;
                canvasRect = canvasObject.GetComponent<RectTransform>();
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Group = canvasObject.GetComponent<CanvasGroup>();

                var imageObject = new GameObject("ReactiveCompositeTestImage", typeof(RectTransform), typeof(RawImage));
                imageObject.transform.SetParent(canvasRect, false);
                imageObject.layer = Layer;
                var rect = imageObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                Image = imageObject.GetComponent<RawImage>();
                Image.raycastTarget = false;
                Image.texture = source;
                Image.material = material;
                Image.color = Color.white;
            }

            private static RenderTexture MakeTexture(string name, int depth)
            {
                var texture = new RenderTexture(Side, Side, depth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                {
                    name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                    antiAliasing = 1, useMipMap = false
                };
                Assert.That(texture.Create(), Is.True);
                Assert.That(texture.sRGB, Is.False);
                return texture;
            }

            public void SetSource(Color value)
            {
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = source;
                    GL.Clear(false, true, value);
                }
                finally { RenderTexture.active = previous; }
            }

            public void AddLeftHalfMask(bool stencil)
            {
                var maskObject = new GameObject("ReactiveCompositeHalfMask", typeof(RectTransform));
                maskObject.transform.SetParent(canvasRect, false);
                maskObject.layer = Layer;
                var maskRect = maskObject.GetComponent<RectTransform>();
                maskRect.anchorMin = maskRect.anchorMax = new Vector2(.5f, .5f);
                maskRect.sizeDelta = new Vector2(Side * .5f, Side);
                maskRect.anchoredPosition = new Vector2(-Side * .25f, 0f);
                if (stencil)
                {
                    maskObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
                    maskObject.AddComponent<Mask>().showMaskGraphic = false;
                }
                else maskObject.AddComponent<RectMask2D>();

                var rect = Image.rectTransform;
                rect.SetParent(maskRect, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(Side, Side);
                rect.anchoredPosition = new Vector2(Side * .25f, 0f);
            }

            public Color RenderPixel(Color clear, int x = Side / 2, int y = Side / 2)
            {
                Canvas.ForceUpdateCanvases();
                camera.backgroundColor = clear;
                camera.Render();
                return OutputPixel(x, y);
            }
            public Color SourcePixel() => ReadPixel(source, Side / 2, Side / 2);
            public Color OutputPixel(int x, int y) => ReadPixel(output, x, y);
            private Color ReadPixel(RenderTexture texture, int x, int y)
            {
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = texture;
                    readback.ReadPixels(new Rect(0, 0, Side, Side), 0, 0);
                    readback.Apply(false, false);
                    return readback.GetPixel(x, y);
                }
                finally { RenderTexture.active = previous; }
            }

            public void Dispose()
            {
                if (camera != null) camera.targetTexture = null;
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(readback);
                source.Release(); output.Release();
                Object.DestroyImmediate(source); Object.DestroyImmediate(output);
            }
        }
    }
}
