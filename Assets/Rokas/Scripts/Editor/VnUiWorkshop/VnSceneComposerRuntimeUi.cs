using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public struct VnSceneComposerPlaqueLayout
    {
        public Rect Mute, Forward, Menu, Triangle;
    }
    public struct VnSceneComposerTriangleSample
    {
        public float OffsetY, Scale, Alpha;
    }
    public struct VnSceneComposerButtonSample
    {
        public Rect Rect;
        public float Brightness, Alpha;
    }

    public static class VnSceneComposerRuntimeUi
    {
        public const string OriginalPlaqueGuid = "6bbf22755fe125c4482a7fd9e8f351ca";
        public const string DefaultPlaqueGuid = "08ac430bce3843b5a427ff44abe32a12";
        internal static void ConfigureFrame(VnWorkshopPreviewFrame frame, VnPresentationWorkshopPreset preset)
        {
            frame.IsComposerFrame = true;
            var visual = preset.dialoguePanelVisual;
            if (visual == null || !visual.hasAssetGuid || visual.assetGuid == OriginalPlaqueGuid)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(DefaultPlaqueGuid));
                if (texture != null) { frame.DialoguePanelTexture = texture; frame.DialoguePanelWarning = string.Empty; }
            }
        }
        public static VnSceneComposerPlaqueLayout Layout(VnWorkshopPreviewFrame frame)
        {
            Rect p = frame.DialoguePanel;
            float diameter = Mathf.Min(p.width * .043f, p.height * .12f);
            float y = p.y + p.height * .615f;
            float gap = Mathf.Max(diameter * 1.27f, p.width * .053f);
            float right = p.xMax - p.width * .06f;
            return new VnSceneComposerPlaqueLayout
            {
                Mute = Center(right - 2 * gap, y, diameter),
                Forward = Center(right - gap, y, diameter),
                Menu = Center(right, y, diameter),
                Triangle = Center(p.x + p.width * .936f, Mathf.Max(24f, p.y + p.height * .23f), diameter * .72f)
            };
        }
        private static Rect Center(float x, float y, float size) { return new Rect(x-size*.5f,y-size*.5f,size,size); }
        public static VnSceneComposerTriangleSample SampleTriangle(float unscaledSeconds)
        {
            float wave = Mathf.Sin(unscaledSeconds * (Mathf.PI * 2f / .75f));
            return new VnSceneComposerTriangleSample
            {
                OffsetY = wave * 6f,
                Scale = 1f + wave * .12f,
                Alpha = .82f + wave * .18f
            };
        }

        public static Color CompletionIndicatorColor(float alpha)
        {
            return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }
        public static VnSceneComposerButtonSample SampleButton(Rect baseline, bool hover, bool pressed, bool enabled, float progress)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            float scale = Mathf.Lerp(1f, enabled && pressed ? .96f : 1f, t);
            Vector2 size = baseline.size * scale;
            return new VnSceneComposerButtonSample
            {
                Rect = new Rect(baseline.center - size * .5f, size),
                Brightness = enabled ? Mathf.Lerp(1f, hover ? 1.25f : 1f, t) : .7f,
                Alpha = enabled ? 1f : .42f
            };
        }
    }
}
