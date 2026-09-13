using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnPresentationWorkshopVn10Resolver
    {
        private static readonly VnWorkshopTypographyValues TypographyBaseline = new VnWorkshopTypographyValues
        {
            DialogueFontPreset = VnWorkshopFontPreset.ProjectSans,
            DialogueFontSize = 22f,
            DialogueCharacterSpacing = 0f,
            DialogueLineSpacing = 0f,
            DialogueParagraphSpacing = 0f,
            DialogueAlignment = VnWorkshopTextAlignment.Left,
            SpeakerFontPreset = VnWorkshopFontPreset.ProjectSans,
            SpeakerFontSize = 26f,
            SpeakerCharacterSpacing = 0f
        };

        public static VnWorkshopTypographyValues ResolveTypography(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopTypographyOverride source = EnsureTypography(preset);
            VnWorkshopTypographyValues values = TypographyBaseline;

            if (source.hasDialogueFontPreset) values.DialogueFontPreset = source.dialogueFontPreset;
            if (source.hasDialogueFontSize) values.DialogueFontSize = source.dialogueFontSize;
            if (source.hasDialogueCharacterSpacing) values.DialogueCharacterSpacing = source.dialogueCharacterSpacing;
            if (source.hasDialogueLineSpacing) values.DialogueLineSpacing = source.dialogueLineSpacing;
            if (source.hasDialogueParagraphSpacing) values.DialogueParagraphSpacing = source.dialogueParagraphSpacing;
            if (source.hasDialogueAlignment) values.DialogueAlignment = source.dialogueAlignment;
            if (source.hasSpeakerFontPreset) values.SpeakerFontPreset = source.speakerFontPreset;
            if (source.hasSpeakerFontSize) values.SpeakerFontSize = source.speakerFontSize;
            if (source.hasSpeakerCharacterSpacing) values.SpeakerCharacterSpacing = source.speakerCharacterSpacing;
            return values;
        }

        public static void SetTypographyPreviewOverrides(
            VnPresentationWorkshopPreset preset,
            VnWorkshopFontPreset dialogueFontPreset,
            float dialogueFontSize,
            float dialogueCharacterSpacing,
            float dialogueLineSpacing,
            float dialogueParagraphSpacing,
            VnWorkshopTextAlignment dialogueAlignment,
            VnWorkshopFontPreset speakerFontPreset,
            float speakerFontSize,
            float speakerCharacterSpacing)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            ValidateEnum(dialogueFontPreset, nameof(dialogueFontPreset));
            ValidateEnum(dialogueAlignment, nameof(dialogueAlignment));
            ValidateEnum(speakerFontPreset, nameof(speakerFontPreset));
            RequireRange(dialogueFontSize, 8f, 96f, nameof(dialogueFontSize));
            RequireRange(speakerFontSize, 8f, 96f, nameof(speakerFontSize));
            RequireRange(dialogueCharacterSpacing, -20f, 100f, nameof(dialogueCharacterSpacing));
            RequireRange(dialogueLineSpacing, -20f, 100f, nameof(dialogueLineSpacing));
            RequireRange(dialogueParagraphSpacing, -20f, 100f, nameof(dialogueParagraphSpacing));
            RequireRange(speakerCharacterSpacing, -20f, 100f, nameof(speakerCharacterSpacing));

            VnWorkshopTypographyOverride target = EnsureTypography(preset);
            target.hasDialogueFontPreset = dialogueFontPreset != TypographyBaseline.DialogueFontPreset;
            target.dialogueFontPreset = dialogueFontPreset;
            target.hasDialogueFontSize = !Mathf.Approximately(dialogueFontSize, TypographyBaseline.DialogueFontSize);
            target.dialogueFontSize = dialogueFontSize;
            target.hasDialogueCharacterSpacing = !Mathf.Approximately(dialogueCharacterSpacing, TypographyBaseline.DialogueCharacterSpacing);
            target.dialogueCharacterSpacing = dialogueCharacterSpacing;
            target.hasDialogueLineSpacing = !Mathf.Approximately(dialogueLineSpacing, TypographyBaseline.DialogueLineSpacing);
            target.dialogueLineSpacing = dialogueLineSpacing;
            target.hasDialogueParagraphSpacing = !Mathf.Approximately(dialogueParagraphSpacing, TypographyBaseline.DialogueParagraphSpacing);
            target.dialogueParagraphSpacing = dialogueParagraphSpacing;
            target.hasDialogueAlignment = dialogueAlignment != TypographyBaseline.DialogueAlignment;
            target.dialogueAlignment = dialogueAlignment;
            target.hasSpeakerFontPreset = speakerFontPreset != TypographyBaseline.SpeakerFontPreset;
            target.speakerFontPreset = speakerFontPreset;
            target.hasSpeakerFontSize = !Mathf.Approximately(speakerFontSize, TypographyBaseline.SpeakerFontSize);
            target.speakerFontSize = speakerFontSize;
            target.hasSpeakerCharacterSpacing = !Mathf.Approximately(speakerCharacterSpacing, TypographyBaseline.SpeakerCharacterSpacing);
            target.speakerCharacterSpacing = speakerCharacterSpacing;
        }

        private static VnWorkshopTypographyOverride EnsureTypography(VnPresentationWorkshopPreset preset)
        {
            if (preset.typography == null)
                preset.typography = new VnWorkshopTypographyOverride();
            return preset.typography;
        }

        private static void RequireRange(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(name, value, "Value must be finite and inside the supported Workshop range.");
        }

        private static void ValidateEnum<T>(T value, string name) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(name, value, "Unsupported Workshop enum value.");
        }
    }
}
