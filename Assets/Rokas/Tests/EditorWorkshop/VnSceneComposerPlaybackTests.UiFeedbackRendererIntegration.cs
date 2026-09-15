using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void FinalParity_ComposerUiFeedbackReplacesIndependentBaselineInsideExistingRenderer()
        {
            Type renderer = typeof(VnPresentationWorkshopPreviewRenderer);
            MethodInfo integratedDraw = renderer.GetMethod(
                "Draw",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(Rect),
                    typeof(VnWorkshopPreviewFrame),
                    typeof(VnWorkshopElement?),
                    typeof(bool),
                    typeof(VnWorkshopElement?),
                    typeof(VnWorkshopUiFeedbackSample?)
                },
                null);
            Assert.That(integratedDraw, Is.Not.Null,
                "The existing preview renderer must accept the authoring feedback sample directly so Back/Next can replace their baseline glyph instead of ghosting over it.");

            MethodInfo shouldReplace = renderer.GetMethod(
                "ShouldReplaceIndependentUiFeedbackControl",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(VnWorkshopElement),
                    typeof(VnWorkshopElement),
                    typeof(VnWorkshopUiFeedbackSample)
                },
                null);
            Assert.That(shouldReplace, Is.Not.Null,
                "Renderer-facing feedback needs an explicit replacement policy for independently drawn Back/Next controls.");

            VnWorkshopUiFeedbackValues values = VnPresentationWorkshopVn10Resolver.ResolveUiFeedback(
                new VnPresentationWorkshopPreset());
            VnWorkshopUiFeedbackSample pressedBack = VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                VnWorkshopElement.Back,
                VnWorkshopUiFeedbackState.Pressed,
                1f,
                values);
            VnWorkshopUiFeedbackSample normalBack = VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                VnWorkshopElement.Back,
                VnWorkshopUiFeedbackState.Normal,
                1f,
                values);
            VnWorkshopUiFeedbackSample pressedMute = VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                VnWorkshopElement.MuteHitRegion,
                VnWorkshopUiFeedbackState.Pressed,
                1f,
                values);

            Assert.That((bool)shouldReplace.Invoke(null, new object[]
            {
                VnWorkshopElement.Back, VnWorkshopElement.Back, pressedBack
            }), Is.True,
                "Pressed Back must replace the existing baseline glyph so scale/offset/alpha/brightness are visually authoritative.");
            Assert.That((bool)shouldReplace.Invoke(null, new object[]
            {
                VnWorkshopElement.Back, VnWorkshopElement.Back, normalBack
            }), Is.False,
                "Normal Back must keep the ordinary renderer baseline.");
            Assert.That((bool)shouldReplace.Invoke(null, new object[]
            {
                VnWorkshopElement.MuteHitRegion, VnWorkshopElement.MuteHitRegion, pressedMute
            }), Is.False,
                "Baked Mute/Pause/Skip artwork stays in the panel and receives overlay/brightness/alpha feedback rather than independent replacement.");

            string workspace = ReadProjectSource(
                "Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposer.cs");
            Assert.That(workspace, Does.Contain("bool advancedLayout = staticAuthoringPreview && IsSceneComposerAdvancedLayoutEditingVisible();"),
                "Hit-region/layout authoring must be available only through the advanced Дополнительно context.");
            Assert.That(workspace, Does.Contain("bool advancedUiFeedback = staticAuthoringPreview && IsSceneComposerAdvancedUiFeedbackPreviewVisible();"),
                "UI Feedback preview must remain available only through the advanced Дополнительно context.");
            Assert.That(workspace, Does.Contain("if (advancedUiFeedback && comparisonView == VnWorkshopComparisonView.Current)"),
                "The advanced preview must continue sampling canonical UI Feedback state.");
            Assert.That(workspace, Does.Contain("uiFeedbackSample = ComposerSampleUiFeedback("),
                "Advanced UI Feedback must still be sampled from the canonical presentation state.");
            Assert.That(workspace,
                Does.Contain("VnPresentationWorkshopPreviewRenderer.Draw(previewRect, frame, selectedUi, advancedLayout,")
                    .And.Contain("uiFeedbackElement, uiFeedbackSample);"),
                "The central Scene Preview must continue passing its canonical feedback element/sample into the existing renderer while hit-region drawing stays advanced-only.");
            Assert.That(workspace,
                Does.Not.Contain("VnPresentationWorkshopPreviewRenderer.Draw(previewRect, frame, selectedUi, staticAuthoringPreview,"),
                "UX-H must not re-enable engineering hit-region rendering merely because the preview is static.");

            string authoring = ReadProjectSource(
                "Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            Assert.That(authoring, Does.Not.Contain("DrawSceneComposerUiFeedbackPreview(previewRect, frame);"),
                "Central authoring must not draw a second Back/Next feedback glyph after the baseline renderer pass.");
        }
    }
}
