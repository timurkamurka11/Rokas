using System;
using System.Linq;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [SerializeField] private VnWorkshopElement _sceneComposerUiFeedbackPreviewElement = VnWorkshopElement.Back;
        [SerializeField] private VnWorkshopUiFeedbackState _sceneComposerUiFeedbackPreviewState = VnWorkshopUiFeedbackState.Normal;
        [SerializeField] private float _sceneComposerUiFeedbackPreviewProgress = 1f;

        public string ComposerGetPreviewTextGlyphWarning()
        {
            string sample = ComposerGetPreviewSampleText();
            if (string.IsNullOrEmpty(sample)) return string.Empty;

            try
            {
                VnPresentationWorkshopPreset active = comparisonView == VnWorkshopComparisonView.Original
                    ? new VnPresentationWorkshopPreset()
                    : ComposerGetActivePresentationPreset();
                VnWorkshopTypographyValues typography = VnPresentationWorkshopVn10Resolver.ResolveTypography(active);
                RokasAssets assets = VnPresentationWorkshopPreviewRenderer.LoadAssets();
                Font dialogueFont = typography.DialogueFontPreset == VnWorkshopFontPreset.ProjectSerif
                    ? assets.serif : assets.sans;
                Font speakerFont = typography.SpeakerFontPreset == VnWorkshopFontPreset.ProjectSerif
                    ? assets.serif : assets.sans;
                char[] missing = sample
                    .Where(c => !char.IsControl(c) &&
                        (!ComposerFontHasGlyph(dialogueFont, c) || !ComposerFontHasGlyph(speakerFont, c)))
                    .Distinct()
                    .Take(12)
                    .ToArray();
                return missing.Length == 0
                    ? string.Empty
                    : "Selected project font is missing glyphs: " + new string(missing) + ". Choose another supported preset.";
            }
            catch (Exception exception)
            {
                return "Could not validate preview glyph coverage: " + exception.Message;
            }
        }

        public VnWorkshopUiFeedbackSample ComposerSampleUiFeedback(
            VnWorkshopElement element,
            VnWorkshopUiFeedbackState state,
            float normalizedProgress)
        {
            EnsureSceneComposerProject();
            VnPresentationWorkshopPreset effective = VnSceneComposerComposition.ResolvePresentation(
                _sceneComposerProject,
                RequireSelectedScene());
            VnWorkshopUiFeedbackValues values = VnPresentationWorkshopVn10Resolver.ResolveUiFeedback(effective);
            return VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                element,
                state,
                Mathf.Clamp01(normalizedProgress),
                values);
        }

        public void ComposerSetUiFeedbackPreview(
            VnWorkshopElement element,
            VnWorkshopUiFeedbackState state,
            float normalizedProgress)
        {
            if (!IsSceneComposerUiFeedbackElement(element))
                throw new ArgumentOutOfRangeException(nameof(element), element, "UI feedback preview supports Back, Next, Mute, Pause and Skip controls.");
            if (!Enum.IsDefined(typeof(VnWorkshopUiFeedbackState), state))
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
            if (float.IsNaN(normalizedProgress) || float.IsInfinity(normalizedProgress))
                throw new ArgumentOutOfRangeException(nameof(normalizedProgress));

            _sceneComposerUiFeedbackPreviewElement = element;
            _sceneComposerUiFeedbackPreviewState = state;
            _sceneComposerUiFeedbackPreviewProgress = Mathf.Clamp01(normalizedProgress);
            Repaint();
        }

        public VnWorkshopElement ComposerGetUiFeedbackPreviewElement()
        {
            return _sceneComposerUiFeedbackPreviewElement;
        }

        public VnWorkshopUiFeedbackState ComposerGetUiFeedbackPreviewState()
        {
            return _sceneComposerUiFeedbackPreviewState;
        }

        public float ComposerGetUiFeedbackPreviewProgress()
        {
            return _sceneComposerUiFeedbackPreviewProgress;
        }

        private void DrawSceneComposerUiFeedbackPreview(Rect previewRect, VnWorkshopPreviewFrame frame)
        {
            if (frame == null || comparisonView != VnWorkshopComparisonView.Current) return;
            if (!IsSceneComposerUiFeedbackElement(_sceneComposerUiFeedbackPreviewElement))
                _sceneComposerUiFeedbackPreviewElement = VnWorkshopElement.Back;

            VnWorkshopUiFeedbackSample sample = ComposerSampleUiFeedback(
                _sceneComposerUiFeedbackPreviewElement,
                _sceneComposerUiFeedbackPreviewState,
                _sceneComposerUiFeedbackPreviewProgress);
            VnPresentationWorkshopPreviewRenderer.DrawUiFeedbackPreview(
                previewRect,
                frame,
                _sceneComposerUiFeedbackPreviewElement,
                sample);
        }

        private static bool IsSceneComposerUiFeedbackElement(VnWorkshopElement element)
        {
            return element == VnWorkshopElement.Back ||
                   element == VnWorkshopElement.Next ||
                   element == VnWorkshopElement.MuteHitRegion ||
                   element == VnWorkshopElement.PauseHitRegion ||
                   element == VnWorkshopElement.SkipHitRegion;
        }

        private static bool ComposerFontHasGlyph(Font font, char character)
        {
            if (font == null) return false;
            FontEngineError load = FontEngine.LoadFontFace(font);
            if (load != FontEngineError.Success)
                throw new InvalidOperationException("Could not load font face for glyph validation: " + load + ".");

            try
            {
                Glyph glyph;
                return FontEngine.TryGetGlyphWithUnicodeValue(
                    character,
                    GlyphLoadFlags.LOAD_NO_BITMAP,
                    out glyph);
            }
            finally
            {
                FontEngine.UnloadFontFace();
            }
        }
    }
}
