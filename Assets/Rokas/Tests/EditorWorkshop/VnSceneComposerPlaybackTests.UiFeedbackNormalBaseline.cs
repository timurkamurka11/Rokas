using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void FinalParity_ComposerUiFeedbackNormalRendersAsExactBaseline()
        {
            VnWorkshopUiFeedbackValues values = VnPresentationWorkshopVn10Resolver.ResolveUiFeedback(
                new VnPresentationWorkshopPreset());

            VnWorkshopUiFeedbackSample normalBack = VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                VnWorkshopElement.Back,
                VnWorkshopUiFeedbackState.Normal,
                1f,
                values);
            VnWorkshopUiFeedbackSample normalMute = VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                VnWorkshopElement.MuteHitRegion,
                VnWorkshopUiFeedbackState.Normal,
                1f,
                values);

            Assert.That(normalBack.ScaleMultiplier, Is.EqualTo(1f).Within(.0001f));
            Assert.That(normalBack.PositionOffset, Is.EqualTo(Vector2.zero));
            Assert.That(normalBack.Brightness, Is.EqualTo(1f).Within(.0001f));
            Assert.That(normalBack.Alpha, Is.EqualTo(1f).Within(.0001f));
            Assert.That(normalBack.OverlayHighlight, Is.EqualTo(0f).Within(.0001f));
            Assert.That(normalMute.ScaleMultiplier, Is.EqualTo(1f).Within(.0001f));
            Assert.That(normalMute.PositionOffset, Is.EqualTo(Vector2.zero));
            Assert.That(normalMute.Brightness, Is.EqualTo(1f).Within(.0001f));
            Assert.That(normalMute.Alpha, Is.EqualTo(1f).Within(.0001f));
            Assert.That(normalMute.OverlayHighlight, Is.EqualTo(0f).Within(.0001f));

            MethodInfo shouldDraw = typeof(VnPresentationWorkshopPreviewRenderer).GetMethod(
                "ShouldDrawUiFeedbackPreview",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(VnWorkshopUiFeedbackSample) },
                null);
            Assert.That(shouldDraw, Is.Not.Null,
                "Normal UI feedback must have an explicit renderer-facing baseline guard so authoring preview adds no overlay at rest.");

            Assert.That((bool)shouldDraw.Invoke(null, new object[] { normalBack }), Is.False,
                "Normal Back/Next preview must resolve to the exact existing renderer baseline.");
            Assert.That((bool)shouldDraw.Invoke(null, new object[] { normalMute }), Is.False,
                "Normal baked Mute/Pause/Skip preview must not add a diagnostic outline or other overlay.");

            VnWorkshopUiFeedbackSample hoverBack = VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                VnWorkshopElement.Back,
                VnWorkshopUiFeedbackState.Hover,
                1f,
                values);
            VnWorkshopUiFeedbackSample pressedMute = VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                VnWorkshopElement.MuteHitRegion,
                VnWorkshopUiFeedbackState.Pressed,
                1f,
                values);
            VnWorkshopUiFeedbackSample releaseBack = VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                VnWorkshopElement.Back,
                VnWorkshopUiFeedbackState.Release,
                .5f,
                values);

            Assert.That((bool)shouldDraw.Invoke(null, new object[] { hoverBack }), Is.True);
            Assert.That((bool)shouldDraw.Invoke(null, new object[] { pressedMute }), Is.True);
            Assert.That((bool)shouldDraw.Invoke(null, new object[] { releaseBack }), Is.True,
                "Release must remain visibly previewable before it reaches its baseline endpoint.");
        }
    }
}
