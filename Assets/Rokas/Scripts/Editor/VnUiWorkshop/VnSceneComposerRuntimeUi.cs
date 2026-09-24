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
        internal const string ControlSheetPath = "Assets/Rokas/Scripts/Editor/VnUiWorkshop/RuntimeUi/RokasFinalControlSheet.png";
        internal const string MutedSpeakerPath = "Assets/Rokas/Art/VN/UI/Controls/VN_Icon_Mute.png";
        internal const string CompletionTrianglePath = "Assets/Rokas/Scripts/Editor/VnUiWorkshop/RuntimeUi/RokasFinalCompletionTriangle.png";
        internal const float TriangleAspect = 1.25f;
        internal static Texture2D ControlSheet { get { return AssetDatabase.LoadAssetAtPath<Texture2D>(ControlSheetPath); } }
        internal static Texture2D MutedSpeaker { get { return AssetDatabase.LoadAssetAtPath<Texture2D>(MutedSpeakerPath); } }
        internal static Texture2D CompletionTriangle { get { return AssetDatabase.LoadAssetAtPath<Texture2D>(CompletionTrianglePath); } }
        // Pixel-exact UV crops of the supplied transparent sheet: mute / forward / menu.
        internal static Rect ButtonUv(int index)
        {
            return new Rect((63f + 661f * index) / 2048f, 49f / 682f, 600f / 2048f, 600f / 682f);
        }
        internal static Texture2D ButtonTexture(int index, bool muted)
        {
            return index == 0 && muted ? MutedSpeaker : ControlSheet;
        }
        internal static Rect ButtonTextureUv(int index, bool muted)
        {
            return index == 0 && muted ? new Rect(0f, 0f, 1f, 1f) : ButtonUv(index);
        }
        internal static Rect TriangleUv { get { return new Rect(180f / 1254f, 204f / 1254f, 900f / 1254f, 720f / 1254f); } }
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
            float diameter = Mathf.Min(p.width * .038f, p.height * .10f);
            float y = p.y + p.height * .595f;
            float gap = diameter * 1.32f;
            float right = p.xMax - p.width * .06f;
            return new VnSceneComposerPlaqueLayout
            {
                Mute = Center(right - 2 * gap, y, diameter),
                Forward = Center(right - gap, y, diameter),
                Menu = Center(right, y, diameter),
                Triangle = new Rect(
                    p.x + p.width * .918f - diameter * .36f,
                    Mathf.Max(24f, p.y + p.height * .23f) - diameter * .36f / TriangleAspect,
                    diameter * .72f, diameter * .72f / TriangleAspect)
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
            float scale = Mathf.Lerp(.97f, enabled && pressed ? .95f : enabled && hover ? 1f : .97f, t);
            Vector2 size = baseline.size * scale;
            return new VnSceneComposerButtonSample
            {
                Rect = new Rect(baseline.center - size * .5f, size),
                Brightness = enabled ? Mathf.Lerp(.9f, pressed ? .92f : hover ? .97f : .9f, t) : .55f,
                Alpha = enabled ? .9f : .42f
            };
        }
    }
}
