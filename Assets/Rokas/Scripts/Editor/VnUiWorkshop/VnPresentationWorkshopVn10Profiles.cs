using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnPresentationWorkshopVn10Profiles
    {
        public const string ReferenceMotionPreviewName = "Reference Motion Preview";

        public static void ApplyReferenceMotionPreview(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new System.ArgumentNullException(nameof(preset));

            preset.ResetAll();

            VnPresentationWorkshopVn10Resolver.SetTypewriterPreviewOverrides(
                preset,
                true,
                31f,
                .01f,
                .13f,
                .29f,
                .43f,
                .23f,
                .24f,
                .06f);

            VnPresentationWorkshopVn10Resolver.SetTimingPreviewOverrides(preset, .14f, .10f, .45f);
            VnPresentationWorkshopVn10Resolver.SetExpressionTransitionPreviewOverrides(
                preset, .29f, VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetCharacterTransitionPreviewOverrides(
                preset,
                VnWorkshopCharacterTransitionMode.SlideAndFade,
                .38f,
                .28f,
                76f,
                VnWorkshopSlideDirection.Left,
                VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetActionBouncePreviewOverrides(
                preset, 16f, .31f, .025f, .12f, VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetBackgroundTransitionPreviewOverrides(
                preset,
                VnWorkshopBackgroundTransitionMode.Curtain,
                .54f,
                .82f,
                VnWorkshopCurtainDirection.RightToLeft,
                VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetStageLayoutPreviewOverrides(
                preset,
                -390f,
                0f,
                390f,
                10f,
                .98f,
                1.02f,
                .98f,
                300f,
                .39f,
                VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetSpeakerFocusPreviewOverrides(
                preset,
                1.06f,
                1f,
                10f,
                .93f,
                .72f,
                .81f,
                .25f,
                VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetUiFeedbackPreviewOverrides(
                preset,
                1.05f,
                .95f,
                new Vector2(0f, -4f),
                .11f,
                VnWorkshopEasing.EaseOut,
                1.07f,
                .89f,
                1f,
                .91f,
                .17f,
                .46f);
        }
    }

    public sealed partial class VnPresentationWorkshopWindow
    {
        public void ApplyReferenceMotionPreview()
        {
            currentPreset = new VnPresentationWorkshopPreset();
            VnPresentationWorkshopVn10Profiles.ApplyReferenceMotionPreview(currentPreset);
            comparisonView = VnWorkshopComparisonView.Current;
            variantName = VnPresentationWorkshopVn10Profiles.ReferenceMotionPreviewName;
            SetPreviewSampleText(VnWorkshopPreviewSampleStore.DefaultText);
            VnWorkshopPreviewSampleStore.Set(CurrentPreset, PreviewSampleText);
            SetVariantStatus(
                "Loaded Reference Motion Preview. This is a non-final Workshop starting profile; production is unchanged.",
                UnityEditor.MessageType.Info);
            Repaint();
        }
    }
}
