using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    [Serializable]
    public sealed class VnPresentationWorkshopDocument
    {
        public int schemaVersion = VnPresentationWorkshopSerialization.SchemaVersion;
        public string sourceHead = VnPresentationWorkshopBaseline.SourceHead;
        public string variantName = string.Empty;
        public string previewSampleText = VnWorkshopPreviewSampleStore.DefaultText;
        public VnPresentationWorkshopPreset preset = new VnPresentationWorkshopPreset();
    }

    public sealed class VnWorkshopImportResult
    {
        public bool Success { get; private set; }
        public bool SourceHeadMismatch { get; private set; }
        public string Error { get; private set; }
        public string ImportedSourceHead { get; private set; }
        public string ExpectedSourceHead { get; private set; }
        public VnPresentationWorkshopDocument Document { get; private set; }

        internal static VnWorkshopImportResult Failed(string error)
        {
            return new VnWorkshopImportResult
            {
                Success = false,
                Error = error ?? "Unknown Workshop import error.",
                ExpectedSourceHead = VnPresentationWorkshopBaseline.SourceHead,
                ImportedSourceHead = string.Empty
            };
        }

        internal static VnWorkshopImportResult Loaded(VnPresentationWorkshopDocument document)
        {
            string importedHead = document.sourceHead ?? string.Empty;
            bool mismatch = !string.Equals(importedHead, VnPresentationWorkshopBaseline.SourceHead,
                StringComparison.Ordinal);
            return new VnWorkshopImportResult
            {
                Success = true,
                SourceHeadMismatch = mismatch,
                Error = mismatch
                    ? "Workshop preset source HEAD mismatch. Imported: " + importedHead +
                      "; expected: " + VnPresentationWorkshopBaseline.SourceHead + "."
                    : string.Empty,
                ImportedSourceHead = importedHead,
                ExpectedSourceHead = VnPresentationWorkshopBaseline.SourceHead,
                Document = document
            };
        }
    }

    public static class VnPresentationWorkshopSerialization
    {
        public const int SchemaVersion = 2;
        private const int LegacySchemaVersion = 1;
        private const float MaxCoordinateMagnitude = 10000f;

        public static string Serialize(
            VnPresentationWorkshopPreset preset,
            string variantName = "",
            string previewSampleText = null)
        {
            NormalizePreset(preset);

            string validationError;
            if (!ValidatePreset(preset, out validationError))
                throw new ArgumentException(validationError, "preset");

            string sample = previewSampleText ?? VnWorkshopPreviewSampleStore.Get(preset);
            VnWorkshopPreviewSampleStore.Set(preset, sample);

            var document = new VnPresentationWorkshopDocument
            {
                schemaVersion = SchemaVersion,
                sourceHead = VnPresentationWorkshopBaseline.SourceHead,
                variantName = variantName ?? string.Empty,
                previewSampleText = sample,
                preset = preset
            };
            return JsonUtility.ToJson(document, true);
        }

        public static VnWorkshopImportResult Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return VnWorkshopImportResult.Failed("Workshop preset JSON is empty.");
            if (json.IndexOf("\"schemaVersion\"", StringComparison.Ordinal) < 0)
                return VnWorkshopImportResult.Failed("Workshop preset JSON is missing schemaVersion metadata.");
            if (json.IndexOf("\"sourceHead\"", StringComparison.Ordinal) < 0)
                return VnWorkshopImportResult.Failed("Workshop preset JSON is missing sourceHead metadata.");

            VnPresentationWorkshopDocument document;
            try
            {
                document = JsonUtility.FromJson<VnPresentationWorkshopDocument>(json);
            }
            catch (Exception exception)
            {
                return VnWorkshopImportResult.Failed("Workshop preset JSON could not be parsed: " + exception.Message);
            }

            if (document == null)
                return VnWorkshopImportResult.Failed("Workshop preset JSON did not contain a document.");

            int importedSchemaVersion = document.schemaVersion;
            if (importedSchemaVersion != LegacySchemaVersion && importedSchemaVersion != SchemaVersion)
            {
                return VnWorkshopImportResult.Failed(
                    "Unsupported Workshop preset schema version: " + importedSchemaVersion +
                    ". Supported versions are " + LegacySchemaVersion + " and " + SchemaVersion + ".");
            }
            if (string.IsNullOrWhiteSpace(document.sourceHead))
                return VnWorkshopImportResult.Failed("Workshop preset sourceHead metadata is empty.");
            if (document.preset == null)
                return VnWorkshopImportResult.Failed("Workshop preset JSON does not contain override data.");

            NormalizePreset(document.preset);
            if (document.previewSampleText == null)
                document.previewSampleText = VnWorkshopPreviewSampleStore.DefaultText;

            string validationError;
            if (!ValidatePreset(document.preset, out validationError))
                return VnWorkshopImportResult.Failed(validationError);

            document.schemaVersion = SchemaVersion;
            VnWorkshopPreviewSampleStore.Set(document.preset, document.previewSampleText);
            return VnWorkshopImportResult.Loaded(document);
        }

        public static bool ValidatePreset(VnPresentationWorkshopPreset preset, out string error)
        {
            if (preset == null)
            {
                error = "Workshop preset is missing.";
                return false;
            }

            NormalizePreset(preset);

            foreach (VnWorkshopElement element in Enum.GetValues(typeof(VnWorkshopElement)))
            {
                VnWorkshopElementOverride elementOverride = preset.GetElementOverride(element);
                if (elementOverride == null)
                {
                    error = "Workshop override group is missing: " + element + ".";
                    return false;
                }

                if (elementOverride.hasPositionDelta &&
                    !ValidateVector(elementOverride.positionDelta, MaxCoordinateMagnitude, element + " position", out error))
                    return false;
                if (elementOverride.hasSizeDelta &&
                    !ValidateVector(elementOverride.sizeDelta, MaxCoordinateMagnitude, element + " size", out error))
                    return false;
                if (elementOverride.hasScaleMultiplier &&
                    (!IsFinite(elementOverride.scaleMultiplier) || elementOverride.scaleMultiplier < .05f ||
                     elementOverride.scaleMultiplier > 5f))
                {
                    error = element + " scale must be finite and between 0.05 and 5.0.";
                    return false;
                }
            }

            VnWorkshopFocusOverride focus = preset.focus;
            if (focus.hasTwoCharacterOffset &&
                (!IsFinite(focus.twoCharacterOffset) || focus.twoCharacterOffset < 0f || focus.twoCharacterOffset > 2000f))
            {
                error = "Two-character offset must be finite and between 0 and 2000.";
                return false;
            }
            if (focus.hasActiveScale && !ValidateRange(focus.activeScale, .1f, 3f, "Active focus scale", out error))
                return false;
            if (focus.hasInactiveScale && !ValidateRange(focus.inactiveScale, .1f, 3f, "Inactive focus scale", out error))
                return false;
            if (focus.hasInactiveBrightness &&
                !ValidateRange(focus.inactiveBrightness, 0f, 1.5f, "Inactive brightness", out error))
                return false;
            if (focus.hasInactiveAlpha && !ValidateRange(focus.inactiveAlpha, 0f, 1f, "Inactive alpha", out error))
                return false;

            try
            {
                VnPresentationWorkshopVn10Resolver.ResolveTypography(preset);
                VnPresentationWorkshopVn10Resolver.ResolveTypewriter(preset);
                VnPresentationWorkshopVn10Resolver.ResolveTiming(preset);
                VnPresentationWorkshopVn10Resolver.ResolveExpressionTransition(preset);
                VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(preset);
                VnPresentationWorkshopVn10Resolver.ResolveActionBounce(preset);
                VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(preset);
                VnPresentationWorkshopVn10Resolver.ResolveStageLayout(preset);
                VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(preset);
                VnPresentationWorkshopVn10Resolver.ResolveUiFeedback(preset);
            }
            catch (Exception exception)
            {
                error = "Invalid VN10 presentation settings: " + exception.Message;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void NormalizePreset(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) return;

            if (preset.dialoguePanel == null) preset.dialoguePanel = new VnWorkshopElementOverride();
            if (preset.dialoguePanelVisual == null) preset.dialoguePanelVisual = new VnWorkshopDialoguePanelVisualOverride();
            if (preset.dialoguePanelVisual.assetGuid == null) preset.dialoguePanelVisual.assetGuid = string.Empty;
            if (preset.minaBody == null) preset.minaBody = new VnWorkshopElementOverride();
            if (preset.speakerName == null) preset.speakerName = new VnWorkshopElementOverride();
            if (preset.dialogueText == null) preset.dialogueText = new VnWorkshopElementOverride();
            if (preset.back == null) preset.back = new VnWorkshopElementOverride();
            if (preset.next == null) preset.next = new VnWorkshopElementOverride();
            if (preset.muteHitRegion == null) preset.muteHitRegion = new VnWorkshopElementOverride();
            if (preset.pauseHitRegion == null) preset.pauseHitRegion = new VnWorkshopElementOverride();
            if (preset.skipHitRegion == null) preset.skipHitRegion = new VnWorkshopElementOverride();
            if (preset.focus == null) preset.focus = new VnWorkshopFocusOverride();
            if (preset.typography == null) preset.typography = new VnWorkshopTypographyOverride();
            if (preset.typography.dialogueFontAssetGuid == null)
                preset.typography.dialogueFontAssetGuid = string.Empty;
            if (preset.typography.speakerFontAssetGuid == null)
                preset.typography.speakerFontAssetGuid = string.Empty;
            if (preset.typewriter == null) preset.typewriter = new VnWorkshopTypewriterOverride();
            if (preset.timing == null) preset.timing = new VnWorkshopTimingOverride();
            if (preset.expressionTransition == null) preset.expressionTransition = new VnWorkshopExpressionTransitionOverride();
            if (preset.characterTransition == null) preset.characterTransition = new VnWorkshopCharacterTransitionOverride();
            if (preset.actionBounce == null) preset.actionBounce = new VnWorkshopActionBounceOverride();
            if (preset.backgroundTransition == null) preset.backgroundTransition = new VnWorkshopBackgroundTransitionOverride();
            if (preset.stageLayout == null) preset.stageLayout = new VnWorkshopStageLayoutOverride();
            if (preset.uiFeedback == null) preset.uiFeedback = new VnWorkshopUiFeedbackOverride();
        }

        private static bool ValidateVector(Vector2 value, float magnitudeLimit, string label, out string error)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || Mathf.Abs(value.x) > magnitudeLimit ||
                Mathf.Abs(value.y) > magnitudeLimit)
            {
                error = label + " must contain finite values within +/-" + magnitudeLimit + ".";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateRange(float value, float minimum, float maximum, string label, out string error)
        {
            if (!IsFinite(value) || value < minimum || value > maximum)
            {
                error = label + " must be finite and between " + minimum + " and " + maximum + ".";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
