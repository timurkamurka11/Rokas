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

    [Serializable]
    public sealed class VnWorkshopTypewriterOverride
    {
        public bool hasEnabled;
        public bool enabled;
        public bool hasCharactersPerSecond;
        public float charactersPerSecond;
        public bool hasBaseCharacterDelay;
        public float baseCharacterDelay;
        public bool hasCommaPause;
        public float commaPause;
        public bool hasPeriodPause;
        public float periodPause;
        public bool hasEllipsisPause;
        public float ellipsisPause;
        public bool hasQuestionPause;
        public float questionPause;
        public bool hasExclamationPause;
        public float exclamationPause;
        public bool hasLineStartDelay;
        public float lineStartDelay;

        public bool HasAnyOverride
        {
            get
            {
                return hasEnabled || hasCharactersPerSecond || hasBaseCharacterDelay ||
                       hasCommaPause || hasPeriodPause || hasEllipsisPause ||
                       hasQuestionPause || hasExclamationPause || hasLineStartDelay;
            }
        }

        public void Clear()
        {
            hasEnabled = false;
            enabled = false;
            hasCharactersPerSecond = false;
            charactersPerSecond = 0f;
            hasBaseCharacterDelay = false;
            baseCharacterDelay = 0f;
            hasCommaPause = false;
            commaPause = 0f;
            hasPeriodPause = false;
            periodPause = 0f;
            hasEllipsisPause = false;
            ellipsisPause = 0f;
            hasQuestionPause = false;
            questionPause = 0f;
            hasExclamationPause = false;
            exclamationPause = 0f;
            hasLineStartDelay = false;
            lineStartDelay = 0f;
        }
    }

    public struct VnWorkshopTypewriterValues
    {
        public bool Enabled;
        public float CharactersPerSecond;
        public float BaseCharacterDelay;
        public float CommaPause;
        public float PeriodPause;
        public float EllipsisPause;
        public float QuestionPause;
        public float ExclamationPause;
        public float LineStartDelay;
    }

    [Serializable]
    public sealed class VnWorkshopTimingOverride
    {
        public bool hasMinimumBeatSettleDuration;
        public float minimumBeatSettleDuration;
        public bool hasPostTransitionBreathingRoom;
        public float postTransitionBreathingRoom;
        public bool hasAutoPreviewSequenceGap;
        public float autoPreviewSequenceGap;

        public bool HasAnyOverride
        {
            get
            {
                return hasMinimumBeatSettleDuration || hasPostTransitionBreathingRoom ||
                       hasAutoPreviewSequenceGap;
            }
        }

        public void Clear()
        {
            hasMinimumBeatSettleDuration = false;
            minimumBeatSettleDuration = 0f;
            hasPostTransitionBreathingRoom = false;
            postTransitionBreathingRoom = 0f;
            hasAutoPreviewSequenceGap = false;
            autoPreviewSequenceGap = 0f;
        }
    }

    public struct VnWorkshopTimingValues
    {
        public float MinimumBeatSettleDuration;
        public float PostTransitionBreathingRoom;
        public float AutoPreviewSequenceGap;
    }
}
