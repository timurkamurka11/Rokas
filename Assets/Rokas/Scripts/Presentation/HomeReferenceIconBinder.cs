using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.IO.Compression;

namespace Rokas.Presentation
{
    public static class HomeReferenceIconBinder
    {
        private const int GlyphCount = 7;
        private const int AtlasWidth = 896;
        private const int AtlasHeight = 128;
        private const string ReplacementName = "ReferenceGlyph";
        private const string AlphaGzipBase64 = "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";

        private static Texture2D atlas;
        private static int lastScanFrame = -1;
        private static bool warningShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Canvas.willRenderCanvases -= ApplyReferenceIcons;
            Canvas.willRenderCanvases += ApplyReferenceIcons;
        }

        private static void ApplyReferenceIcons()
        {
            if (lastScanFrame == Time.frameCount) return;
            lastScanFrame = Time.frameCount;

            EnsureAtlas();
            if (!atlas) return;

            var icons = UnityEngine.Object.FindObjectsByType<HomeActionIcon>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var icon in icons)
            {
                if (!icon || icon.Glyph == HomeActionGlyph.Chevron || icon.name != "ActionGlyph")
                    continue;

                int index = (int)icon.Glyph;
                if (index < 0 || index >= GlyphCount)
                    continue;

                if (!icon.transform.Find(ReplacementName))
                {
                    var replacement = new GameObject(
                        ReplacementName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    var rect = (RectTransform)replacement.transform;
                    rect.SetParent(icon.transform, false);
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = Vector2.zero;

                    var image = replacement.GetComponent<RawImage>();
                    image.texture = atlas;
                    image.uvRect = new Rect(index / (float)GlyphCount, 0f, 1f / GlyphCount, 1f);
                    image.color = Color.white;
                    image.raycastTarget = false;
                }

                icon.enabled = false;
            }
        }

        private static void EnsureAtlas()
        {
            if (atlas) return;

            try
            {
                byte[] compressed = System.Convert.FromBase64String(AlphaGzipBase64);
                byte[] alpha = new byte[AtlasWidth * AtlasHeight];

                using (var input = new MemoryStream(compressed))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                {
                    int offset = 0;
                    while (offset < alpha.Length)
                    {
                        int read = gzip.Read(alpha, offset, alpha.Length - offset);
                        if (read <= 0) break;
                        offset += read;
                    }

                    if (offset != alpha.Length)
                    {
                        WarnOnce("ROKAS HOME embedded icon alpha data length mismatch.");
                        return;
                    }
                }

                var pixels = new Color32[alpha.Length];
                var gold = new Color32(227, 189, 102, 255);
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(gold.r, gold.g, gold.b, alpha[i]);

                var texture = new Texture2D(AtlasWidth, AtlasHeight, TextureFormat.RGBA32, false, false)
                {
                    name = "HomeActionIcons_Reference",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                atlas = texture;
                warningShown = false;
            }
            catch (System.Exception ex)
            {
                WarnOnce("ROKAS HOME embedded icon atlas creation failed: " + ex.GetType().Name);
            }
        }

        private static void WarnOnce(string message)
        {
            if (warningShown) return;
            warningShown = true;
            Debug.LogWarning(message);
        }
    }
}
