using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Uses the approved HOME reference atlas while leaving the existing pills untouched.
    public static class HomeReferenceIconBinder
    {
        private const int GlyphCount = 7;
        private const string ReplacementName = "ReferenceGlyph";
        private static Texture2D atlas;
        private static int lastScanFrame = -1;
        private static bool loadWarningShown;

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

            var icons = UnityEngine.Object.FindObjectsByType<HomeActionIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var icon in icons)
            {
                if (!icon || icon.Glyph == HomeActionGlyph.Chevron || icon.name != "ActionGlyph")
                    continue;

                int index = (int)icon.Glyph;
                if (index < 0 || index >= GlyphCount)
                    continue;

                if (!icon.transform.Find(ReplacementName))
                {
                    var replacement = new GameObject(ReplacementName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
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

            var bytesAsset = Resources.Load<TextAsset>("HomeActionIconsBytes");
            if (!bytesAsset)
            {
                WarnOnce("ROKAS HOME icon byte resource could not be loaded from Resources/HomeActionIconsBytes.");
                return;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = "HomeActionIcons_Reference",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (!ImageConversion.LoadImage(texture, bytesAsset.bytes, true))
            {
                UnityEngine.Object.Destroy(texture);
                WarnOnce("ROKAS HOME icon byte resource was found but PNG decoding failed.");
                return;
            }

            atlas = texture;
            loadWarningShown = false;
        }

        private static void WarnOnce(string message)
        {
            if (loadWarningShown) return;
            loadWarningShown = true;
            Debug.LogWarning(message);
        }
    }
}
