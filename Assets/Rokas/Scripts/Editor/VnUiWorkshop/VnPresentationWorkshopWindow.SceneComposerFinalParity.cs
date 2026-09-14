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
            VnWorkshopUiFeedbackValues values =
                VnPresentationWorkshopVn10Resolver.ResolveUiFeedback(ComposerGetActivePresentationPreset());
            return VnPresentationWorkshopVn10Resolver.SampleUiFeedback(
                element,
                state,
                Mathf.Clamp01(normalizedProgress),
                values);
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
