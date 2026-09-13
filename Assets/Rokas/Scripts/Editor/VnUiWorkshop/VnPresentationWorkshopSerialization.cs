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
        public const int SchemaVersion = 1;
        private const float MaxCoordinateMagnitude = 10000f;

        public static string Serialize(VnPresentationWorkshopPreset preset, string variantName = "")
        {
            string validationError;
            if (!ValidatePreset(preset, out validationError))
                throw new ArgumentException(validationError, "preset");

            var document = new VnPresentationWorkshopDocument
            {
                schemaVersion = SchemaVersion,
                sourceHead = VnPresentationWorkshopBaseline.SourceHead,
                variantName = variantName ?? string.Empty,
                preset = preset
            };
            return JsonUtility.ToJson(document, true);
        }

        public static VnWorkshopImportResult Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return VnWorkshopImportResult.Failed("Workshop preset JSON is empty.");

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
            if (document.schemaVersion != SchemaVersion)
                return VnWorkshopImportResult.Failed("Unsupported Workshop preset schema version: " +
                                                    document.schemaVersion + ". Expected " + SchemaVersion + ".");
            if (document.preset == null)
                return VnWorkshopImportResult.Failed("Workshop preset JSON does not contain override data.");

            string validationError;
            if (!ValidatePreset(document.preset, out validationError))
                return VnWorkshopImportResult.Failed(validationError);

            return VnWorkshopImportResult.Loaded(document);
        }

        public static bool ValidatePreset(VnPresentationWorkshopPreset preset, out string error)
        {
            if (preset == null)
            {
                error = "Workshop preset is missing.";
                return false;
            }

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

            if (preset.focus == null)
            {
                error = "Workshop focus override group is missing.";
                return false;
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

            error = string.Empty;
            return true;
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
