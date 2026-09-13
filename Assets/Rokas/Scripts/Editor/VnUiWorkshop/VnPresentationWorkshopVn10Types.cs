using System;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnWorkshopFontPreset
    {
        ProjectSans,
        ProjectSerif
    }

    public enum VnWorkshopTextAlignment
    {
        Left,
        Center,
        Right
    }

    [Serializable]
    public sealed class VnWorkshopTypographyOverride
    {
        public bool hasDialogueFontPreset;
        public VnWorkshopFontPreset dialogueFontPreset;
        public bool hasDialogueFontSize;
        public float dialogueFontSize;
        public bool hasDialogueCharacterSpacing;
        public float dialogueCharacterSpacing;
        public bool hasDialogueLineSpacing;
        public float dialogueLineSpacing;
        public bool hasDialogueParagraphSpacing;
        public float dialogueParagraphSpacing;
        public bool hasDialogueAlignment;
        public VnWorkshopTextAlignment dialogueAlignment;

        public bool hasSpeakerFontPreset;
        public VnWorkshopFontPreset speakerFontPreset;
        public bool hasSpeakerFontSize;
        public float speakerFontSize;
        public bool hasSpeakerCharacterSpacing;
        public float speakerCharacterSpacing;

        public bool HasAnyOverride
        {
            get
            {
                return hasDialogueFontPreset || hasDialogueFontSize ||
                       hasDialogueCharacterSpacing || hasDialogueLineSpacing ||
                       hasDialogueParagraphSpacing || hasDialogueAlignment ||
                       hasSpeakerFontPreset || hasSpeakerFontSize ||
                       hasSpeakerCharacterSpacing;
            }
        }

        public void Clear()
        {
            hasDialogueFontPreset = false;
            dialogueFontPreset = VnWorkshopFontPreset.ProjectSans;
            hasDialogueFontSize = false;
            dialogueFontSize = 0f;
            hasDialogueCharacterSpacing = false;
            dialogueCharacterSpacing = 0f;
            hasDialogueLineSpacing = false;
            dialogueLineSpacing = 0f;
            hasDialogueParagraphSpacing = false;
            dialogueParagraphSpacing = 0f;
            hasDialogueAlignment = false;
            dialogueAlignment = VnWorkshopTextAlignment.Left;

            hasSpeakerFontPreset = false;
            speakerFontPreset = VnWorkshopFontPreset.ProjectSans;
            hasSpeakerFontSize = false;
            speakerFontSize = 0f;
            hasSpeakerCharacterSpacing = false;
            speakerCharacterSpacing = 0f;
        }
    }

    public struct VnWorkshopTypographyValues
    {
        public VnWorkshopFontPreset DialogueFontPreset;
        public float DialogueFontSize;
        public float DialogueCharacterSpacing;
        public float DialogueLineSpacing;
        public float DialogueParagraphSpacing;
        public VnWorkshopTextAlignment DialogueAlignment;
        public VnWorkshopFontPreset SpeakerFontPreset;
        public float SpeakerFontSize;
        public float SpeakerCharacterSpacing;
    }
}
