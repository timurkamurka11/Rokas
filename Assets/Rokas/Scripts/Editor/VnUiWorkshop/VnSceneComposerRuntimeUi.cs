using Rokas.Presentation;
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
        internal const string CompletionTrianglePath = "Assets/Rokas/Scripts/Editor/VnUiWorkshop/RuntimeUi/RokasCompletionArrow.png";
        internal const float TriangleAspect = .90f;
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
        // Crop only transparent canvas margins; keep the supplied arrow and glow intact.
        internal static Rect TriangleUv { get { return new Rect(.23f, .17f, .59f, .65f); } }
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
        public static VnSceneComposerPlaqueLayout Layout(
            VnWorkshopPreviewFrame frame)
        {
            RokasVnPlaqueUiLayout shared =
                RokasVnRuntimeUiSemantics.Layout(
                    frame.DialoguePanel);
            return new VnSceneComposerPlaqueLayout
            {
                Mute = shared.Mute,
                Forward = shared.Forward,
                Menu = shared.Menu,
                Triangle = shared.Triangle
            };
        }

        public static VnSceneComposerTriangleSample SampleTriangle(
            float unscaledSeconds)
        {
            RokasVnTriangleUiSample shared =
                RokasVnRuntimeUiSemantics.SampleTriangle(
                    unscaledSeconds);
            return new VnSceneComposerTriangleSample
            {
                OffsetY = shared.OffsetY,
                Scale = shared.Scale,
                Alpha = shared.Alpha
            };
        }

        public static Color CompletionIndicatorColor(float alpha)
        {
            return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        }
        public static VnSceneComposerButtonSample SampleButton(
            Rect baseline,
            bool hover,
            bool pressed,
            bool enabled,
            float progress)
        {
            RokasVnButtonUiSample shared =
                RokasVnRuntimeUiSemantics.SampleButton(
                    baseline,
                    hover,
                    pressed,
                    enabled,
                    progress);
            return new VnSceneComposerButtonSample
            {
                Rect = shared.Rect,
                Brightness = shared.Brightness,
                Alpha = shared.Alpha
            };
        }
    }
}
