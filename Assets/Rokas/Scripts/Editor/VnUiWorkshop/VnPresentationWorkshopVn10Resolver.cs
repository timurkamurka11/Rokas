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

        private static readonly VnWorkshopTypewriterValues TypewriterBaseline = new VnWorkshopTypewriterValues
        {
            Enabled = true,
            CharactersPerSecond = 36f,
            BaseCharacterDelay = 0f,
            CommaPause = .10f,
            PeriodPause = .24f,
            EllipsisPause = .36f,
            QuestionPause = .20f,
            ExclamationPause = .20f,
            LineStartDelay = .04f
        };

        private static readonly VnWorkshopTimingValues TimingBaseline = new VnWorkshopTimingValues
        {
            MinimumBeatSettleDuration = .12f,
            PostTransitionBreathingRoom = .08f,
            AutoPreviewSequenceGap = .40f
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

        public static VnWorkshopTypewriterValues ResolveTypewriter(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopTypewriterOverride source = EnsureTypewriter(preset);
            VnWorkshopTypewriterValues values = TypewriterBaseline;
            if (source.hasEnabled) values.Enabled = source.enabled;
            if (source.hasCharactersPerSecond) values.CharactersPerSecond = source.charactersPerSecond;
            if (source.hasBaseCharacterDelay) values.BaseCharacterDelay = source.baseCharacterDelay;
            if (source.hasCommaPause) values.CommaPause = source.commaPause;
            if (source.hasPeriodPause) values.PeriodPause = source.periodPause;
            if (source.hasEllipsisPause) values.EllipsisPause = source.ellipsisPause;
            if (source.hasQuestionPause) values.QuestionPause = source.questionPause;
            if (source.hasExclamationPause) values.ExclamationPause = source.exclamationPause;
            if (source.hasLineStartDelay) values.LineStartDelay = source.lineStartDelay;
            return values;
        }

        public static void SetTypewriterPreviewOverrides(
            VnPresentationWorkshopPreset preset,
            bool enabled,
            float charactersPerSecond,
            float baseCharacterDelay,
            float commaPause,
            float periodPause,
            float ellipsisPause,
            float questionPause,
            float exclamationPause,
            float lineStartDelay)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            RequireRange(charactersPerSecond, 1f, 240f, nameof(charactersPerSecond));
            RequireRange(baseCharacterDelay, 0f, 5f, nameof(baseCharacterDelay));
            RequireRange(commaPause, 0f, 5f, nameof(commaPause));
            RequireRange(periodPause, 0f, 5f, nameof(periodPause));
            RequireRange(ellipsisPause, 0f, 5f, nameof(ellipsisPause));
            RequireRange(questionPause, 0f, 5f, nameof(questionPause));
            RequireRange(exclamationPause, 0f, 5f, nameof(exclamationPause));
            RequireRange(lineStartDelay, 0f, 5f, nameof(lineStartDelay));

            VnWorkshopTypewriterOverride target = EnsureTypewriter(preset);
            target.hasEnabled = enabled != TypewriterBaseline.Enabled;
            target.enabled = enabled;
            target.hasCharactersPerSecond = !Mathf.Approximately(charactersPerSecond, TypewriterBaseline.CharactersPerSecond);
            target.charactersPerSecond = charactersPerSecond;
            target.hasBaseCharacterDelay = !Mathf.Approximately(baseCharacterDelay, TypewriterBaseline.BaseCharacterDelay);
            target.baseCharacterDelay = baseCharacterDelay;
            target.hasCommaPause = !Mathf.Approximately(commaPause, TypewriterBaseline.CommaPause);
            target.commaPause = commaPause;
            target.hasPeriodPause = !Mathf.Approximately(periodPause, TypewriterBaseline.PeriodPause);
            target.periodPause = periodPause;
            target.hasEllipsisPause = !Mathf.Approximately(ellipsisPause, TypewriterBaseline.EllipsisPause);
            target.ellipsisPause = ellipsisPause;
            target.hasQuestionPause = !Mathf.Approximately(questionPause, TypewriterBaseline.QuestionPause);
            target.questionPause = questionPause;
            target.hasExclamationPause = !Mathf.Approximately(exclamationPause, TypewriterBaseline.ExclamationPause);
            target.exclamationPause = exclamationPause;
            target.hasLineStartDelay = !Mathf.Approximately(lineStartDelay, TypewriterBaseline.LineStartDelay);
            target.lineStartDelay = lineStartDelay;
        }

        public static int CalculateTypewriterVisibleCharacters(string text, float elapsedSeconds, VnWorkshopTypewriterValues values)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (!values.Enabled) return text.Length;
            if (elapsedSeconds < 0f) return 0;

            float time = values.LineStartDelay;
            if (elapsedSeconds < time) return 0;
            float characterDelay = 1f / Mathf.Max(.0001f, values.CharactersPerSecond) + values.BaseCharacterDelay;
            int visible = 0;
            for (int i = 0; i < text.Length; i++)
            {
                time += characterDelay;
                if (elapsedSeconds + .00001f < time) return visible;
                visible = i + 1;
                time += GetPunctuationPause(text, i, values);
                if (elapsedSeconds + .00001f < time) return visible;
            }
            return visible;
        }

        public static float CalculateTypewriterDuration(string text, VnWorkshopTypewriterValues values)
        {
            if (string.IsNullOrEmpty(text) || !values.Enabled) return 0f;
            float characterDelay = 1f / Mathf.Max(.0001f, values.CharactersPerSecond) + values.BaseCharacterDelay;
            float duration = values.LineStartDelay;
            for (int i = 0; i < text.Length; i++)
                duration += characterDelay + GetPunctuationPause(text, i, values);
            return duration;
        }

        public static int InstantCompleteVisibleCharacters(string text)
        {
            return string.IsNullOrEmpty(text) ? 0 : text.Length;
        }

        public static VnWorkshopTimingValues ResolveTiming(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopTimingOverride source = EnsureTiming(preset);
            VnWorkshopTimingValues values = TimingBaseline;
            if (source.hasMinimumBeatSettleDuration) values.MinimumBeatSettleDuration = source.minimumBeatSettleDuration;
            if (source.hasPostTransitionBreathingRoom) values.PostTransitionBreathingRoom = source.postTransitionBreathingRoom;
            if (source.hasAutoPreviewSequenceGap) values.AutoPreviewSequenceGap = source.autoPreviewSequenceGap;
            return values;
        }

        public static void SetTimingPreviewOverrides(
            VnPresentationWorkshopPreset preset,
            float minimumBeatSettleDuration,
            float postTransitionBreathingRoom,
            float autoPreviewSequenceGap)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            RequireRange(minimumBeatSettleDuration, 0f, 10f, nameof(minimumBeatSettleDuration));
            RequireRange(postTransitionBreathingRoom, 0f, 10f, nameof(postTransitionBreathingRoom));
            RequireRange(autoPreviewSequenceGap, 0f, 10f, nameof(autoPreviewSequenceGap));

            VnWorkshopTimingOverride target = EnsureTiming(preset);
            target.hasMinimumBeatSettleDuration = !Mathf.Approximately(minimumBeatSettleDuration, TimingBaseline.MinimumBeatSettleDuration);
            target.minimumBeatSettleDuration = minimumBeatSettleDuration;
            target.hasPostTransitionBreathingRoom = !Mathf.Approximately(postTransitionBreathingRoom, TimingBaseline.PostTransitionBreathingRoom);
            target.postTransitionBreathingRoom = postTransitionBreathingRoom;
            target.hasAutoPreviewSequenceGap = !Mathf.Approximately(autoPreviewSequenceGap, TimingBaseline.AutoPreviewSequenceGap);
            target.autoPreviewSequenceGap = autoPreviewSequenceGap;
        }

        private static float GetPunctuationPause(string text, int index, VnWorkshopTypewriterValues values)
        {
            char c = text[index];
            if (c == ',') return values.CommaPause;
            if (c == '…') return values.EllipsisPause;
            if (c == '?') return values.QuestionPause;
            if (c == '!') return values.ExclamationPause;
            if (c != '.') return 0f;

            bool inAsciiEllipsis = (index > 0 && text[index - 1] == '.') || (index + 1 < text.Length && text[index + 1] == '.');
            if (!inAsciiEllipsis) return values.PeriodPause;
            bool isTerminalEllipsisDot = index >= 2 && text[index - 1] == '.' && text[index - 2] == '.' &&
                                         (index + 1 >= text.Length || text[index + 1] != '.');
            return isTerminalEllipsisDot ? values.EllipsisPause : 0f;
        }

        private static VnWorkshopTypographyOverride EnsureTypography(VnPresentationWorkshopPreset preset)
        {
            if (preset.typography == null)
                preset.typography = new VnWorkshopTypographyOverride();
            return preset.typography;
        }

        private static VnWorkshopTypewriterOverride EnsureTypewriter(VnPresentationWorkshopPreset preset)
        {
            if (preset.typewriter == null)
                preset.typewriter = new VnWorkshopTypewriterOverride();
            return preset.typewriter;
        }

        private static VnWorkshopTimingOverride EnsureTiming(VnPresentationWorkshopPreset preset)
        {
            if (preset.timing == null)
                preset.timing = new VnWorkshopTimingOverride();
            return preset.timing;
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
