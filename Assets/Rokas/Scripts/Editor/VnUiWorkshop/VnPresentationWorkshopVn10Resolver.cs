using System;
using Rokas.Presentation;
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

        private static readonly VnWorkshopExpressionTransitionValues ExpressionTransitionBaseline =
            new VnWorkshopExpressionTransitionValues
            {
                Duration = .24f,
                Easing = VnWorkshopEasing.EaseInOut
            };

        private static readonly VnWorkshopCharacterTransitionValues CharacterTransitionBaseline =
            new VnWorkshopCharacterTransitionValues
            {
                Mode = VnWorkshopCharacterTransitionMode.Fade,
                Duration = .30f,
                FadeDuration = .30f,
                SlideDistance = 64f,
                SlideDirection = VnWorkshopSlideDirection.Left,
                Easing = VnWorkshopEasing.EaseInOut
            };

        private static readonly VnWorkshopActionBounceValues ActionBounceBaseline =
            new VnWorkshopActionBounceValues
            {
                Amplitude = 18f,
                Duration = .28f,
                ScaleEmphasis = .03f,
                Overshoot = .15f,
                Easing = VnWorkshopEasing.EaseInOut
            };

        private static readonly VnWorkshopBackgroundTransitionValues BackgroundTransitionBaseline =
            new VnWorkshopBackgroundTransitionValues
            {
                Mode = VnWorkshopBackgroundTransitionMode.Fade,
                Duration = .40f,
                CurtainDarkness = .85f,
                Direction = VnWorkshopCurtainDirection.RightToLeft,
                Easing = VnWorkshopEasing.EaseInOut
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

        public static VnWorkshopExpressionTransitionValues ResolveExpressionTransition(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopExpressionTransitionOverride source = EnsureExpressionTransition(preset);
            VnWorkshopExpressionTransitionValues values = ExpressionTransitionBaseline;
            if (source.hasDuration) values.Duration = source.duration;
            if (source.hasEasing) values.Easing = source.easing;
            return values;
        }

        public static void SetExpressionTransitionPreviewOverrides(
            VnPresentationWorkshopPreset preset,
            float duration,
            VnWorkshopEasing easing)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            RequireRange(duration, 0f, 10f, nameof(duration));
            ValidateEnum(easing, nameof(easing));

            VnWorkshopExpressionTransitionOverride target = EnsureExpressionTransition(preset);
            target.hasDuration = !Mathf.Approximately(duration, ExpressionTransitionBaseline.Duration);
            target.duration = duration;
            target.hasEasing = easing != ExpressionTransitionBaseline.Easing;
            target.easing = easing;
        }

        public static VnWorkshopExpressionTransitionSample SampleExpressionTransition(
            string startStateId,
            string endStateId,
            float normalizedProgress,
            VnWorkshopExpressionTransitionValues values)
        {
            VnCharacterVisualState start = ResolveAuthoredState(startStateId, nameof(startStateId));
            VnCharacterVisualState end = ResolveAuthoredState(endStateId, nameof(endStateId));
            if (!string.Equals(start.Character, end.Character, StringComparison.Ordinal))
                throw new ArgumentException("Expression transition states must belong to the same authored character.");
            ValidateEnum(values.Easing, nameof(values.Easing));

            float raw = Mathf.Clamp01(normalizedProgress);
            float eased = EvaluateEasing(raw, values.Easing);
            return new VnWorkshopExpressionTransitionSample
            {
                StartStateId = start.Id,
                EndStateId = end.Id,
                StartAlpha = 1f - eased,
                EndAlpha = eased,
                Complete = raw >= 1f
            };
        }

        public static VnWorkshopCharacterTransitionValues ResolveCharacterTransition(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopCharacterTransitionOverride source = EnsureCharacterTransition(preset);
            VnWorkshopCharacterTransitionValues values = CharacterTransitionBaseline;
            if (source.hasMode) values.Mode = source.mode;
            if (source.hasDuration) values.Duration = source.duration;
            if (source.hasFadeDuration) values.FadeDuration = source.fadeDuration;
            if (source.hasSlideDistance) values.SlideDistance = source.slideDistance;
            if (source.hasSlideDirection) values.SlideDirection = source.slideDirection;
            if (source.hasEasing) values.Easing = source.easing;
            return values;
        }

        public static void SetCharacterTransitionPreviewOverrides(
            VnPresentationWorkshopPreset preset,
            VnWorkshopCharacterTransitionMode mode,
            float duration,
            float fadeDuration,
            float slideDistance,
            VnWorkshopSlideDirection slideDirection,
            VnWorkshopEasing easing)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            ValidateEnum(mode, nameof(mode));
            ValidateEnum(slideDirection, nameof(slideDirection));
            ValidateEnum(easing, nameof(easing));
            RequireRange(duration, 0f, 10f, nameof(duration));
            RequireRange(fadeDuration, 0f, 10f, nameof(fadeDuration));
            RequireRange(slideDistance, 0f, 2000f, nameof(slideDistance));

            VnWorkshopCharacterTransitionOverride target = EnsureCharacterTransition(preset);
            target.hasMode = mode != CharacterTransitionBaseline.Mode;
            target.mode = mode;
            target.hasDuration = !Mathf.Approximately(duration, CharacterTransitionBaseline.Duration);
            target.duration = duration;
            target.hasFadeDuration = !Mathf.Approximately(fadeDuration, CharacterTransitionBaseline.FadeDuration);
            target.fadeDuration = fadeDuration;
            target.hasSlideDistance = !Mathf.Approximately(slideDistance, CharacterTransitionBaseline.SlideDistance);
            target.slideDistance = slideDistance;
            target.hasSlideDirection = slideDirection != CharacterTransitionBaseline.SlideDirection;
            target.slideDirection = slideDirection;
            target.hasEasing = easing != CharacterTransitionBaseline.Easing;
            target.easing = easing;
        }

        public static VnWorkshopCharacterTransitionSample SampleCharacterEnter(
            float normalizedProgress,
            VnWorkshopCharacterTransitionValues values)
        {
            return SampleCharacterTransition(normalizedProgress, values, true);
        }

        public static VnWorkshopCharacterTransitionSample SampleCharacterExit(
            float normalizedProgress,
            VnWorkshopCharacterTransitionValues values)
        {
            return SampleCharacterTransition(normalizedProgress, values, false);
        }

        public static VnWorkshopActionBounceValues ResolveActionBounce(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopActionBounceOverride source = EnsureActionBounce(preset);
            VnWorkshopActionBounceValues values = ActionBounceBaseline;
            if (source.hasAmplitude) values.Amplitude = source.amplitude;
            if (source.hasDuration) values.Duration = source.duration;
            if (source.hasScaleEmphasis) values.ScaleEmphasis = source.scaleEmphasis;
            if (source.hasOvershoot) values.Overshoot = source.overshoot;
            if (source.hasEasing) values.Easing = source.easing;
            return values;
        }

        public static void SetActionBouncePreviewOverrides(
            VnPresentationWorkshopPreset preset,
            float amplitude,
            float duration,
            float scaleEmphasis,
            float overshoot,
            VnWorkshopEasing easing)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            RequireRange(amplitude, 0f, 1000f, nameof(amplitude));
            RequireRange(duration, .01f, 10f, nameof(duration));
            RequireRange(scaleEmphasis, 0f, 1f, nameof(scaleEmphasis));
            RequireRange(overshoot, 0f, 2f, nameof(overshoot));
            ValidateEnum(easing, nameof(easing));

            VnWorkshopActionBounceOverride target = EnsureActionBounce(preset);
            target.hasAmplitude = !Mathf.Approximately(amplitude, ActionBounceBaseline.Amplitude);
            target.amplitude = amplitude;
            target.hasDuration = !Mathf.Approximately(duration, ActionBounceBaseline.Duration);
            target.duration = duration;
            target.hasScaleEmphasis = !Mathf.Approximately(scaleEmphasis, ActionBounceBaseline.ScaleEmphasis);
            target.scaleEmphasis = scaleEmphasis;
            target.hasOvershoot = !Mathf.Approximately(overshoot, ActionBounceBaseline.Overshoot);
            target.overshoot = overshoot;
            target.hasEasing = easing != ActionBounceBaseline.Easing;
            target.easing = easing;
        }

        public static VnWorkshopActionBounceSample SampleActionBounce(
            bool triggered,
            float normalizedProgress,
            VnWorkshopActionBounceValues values)
        {
            ValidateEnum(values.Easing, nameof(values.Easing));
            if (!triggered)
            {
                return new VnWorkshopActionBounceSample
                {
                    PositionOffset = Vector2.zero,
                    ScaleMultiplier = 1f,
                    Complete = true
                };
            }

            float raw = Mathf.Clamp01(normalizedProgress);
            if (raw <= 0f)
            {
                return new VnWorkshopActionBounceSample
                {
                    PositionOffset = Vector2.zero,
                    ScaleMultiplier = 1f,
                    Complete = false
                };
            }
            if (raw >= 1f)
            {
                return new VnWorkshopActionBounceSample
                {
                    PositionOffset = Vector2.zero,
                    ScaleMultiplier = 1f,
                    Complete = true
                };
            }

            float eased = EvaluateEasing(raw, values.Easing);
            float primary = Mathf.Sin(Mathf.PI * eased);
            float rebound = Mathf.Sin(Mathf.PI * 2f * eased);
            float y = -Mathf.Max(0f, values.Amplitude) *
                      (primary + (Mathf.Max(0f, values.Overshoot) * .25f * rebound));
            float scale = 1f + (Mathf.Max(0f, values.ScaleEmphasis) * primary);
            return new VnWorkshopActionBounceSample
            {
                PositionOffset = new Vector2(0f, y),
                ScaleMultiplier = scale,
                Complete = false
            };
        }

        public static VnWorkshopBackgroundTransitionValues ResolveBackgroundTransition(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopBackgroundTransitionOverride source = EnsureBackgroundTransition(preset);
            VnWorkshopBackgroundTransitionValues values = BackgroundTransitionBaseline;
            if (source.hasMode) values.Mode = source.mode;
            if (source.hasDuration) values.Duration = source.duration;
            if (source.hasCurtainDarkness) values.CurtainDarkness = source.curtainDarkness;
            if (source.hasDirection) values.Direction = source.direction;
            if (source.hasEasing) values.Easing = source.easing;
            return values;
        }

        public static void SetBackgroundTransitionPreviewOverrides(
            VnPresentationWorkshopPreset preset,
            VnWorkshopBackgroundTransitionMode mode,
            float duration,
            float curtainDarkness,
            VnWorkshopCurtainDirection direction,
            VnWorkshopEasing easing)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            ValidateEnum(mode, nameof(mode));
            ValidateEnum(direction, nameof(direction));
            ValidateEnum(easing, nameof(easing));
            RequireRange(duration, 0f, 10f, nameof(duration));
            RequireRange(curtainDarkness, 0f, 1f, nameof(curtainDarkness));

            VnWorkshopBackgroundTransitionOverride target = EnsureBackgroundTransition(preset);
            target.hasMode = mode != BackgroundTransitionBaseline.Mode;
            target.mode = mode;
            target.hasDuration = !Mathf.Approximately(duration, BackgroundTransitionBaseline.Duration);
            target.duration = duration;
            target.hasCurtainDarkness = !Mathf.Approximately(curtainDarkness, BackgroundTransitionBaseline.CurtainDarkness);
            target.curtainDarkness = curtainDarkness;
            target.hasDirection = direction != BackgroundTransitionBaseline.Direction;
            target.direction = direction;
            target.hasEasing = easing != BackgroundTransitionBaseline.Easing;
            target.easing = easing;
        }

        public static void ResetBackgroundTransitionPreviewOverrides(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            EnsureBackgroundTransition(preset).Clear();
        }

        public static VnWorkshopBackgroundTransitionSample SampleBackgroundTransition(
            float normalizedProgress,
            VnWorkshopBackgroundTransitionValues values)
        {
            ValidateEnum(values.Mode, nameof(values.Mode));
            ValidateEnum(values.Direction, nameof(values.Direction));
            ValidateEnum(values.Easing, nameof(values.Easing));
            RequireRange(values.CurtainDarkness, 0f, 1f, nameof(values.CurtainDarkness));

            float raw = Mathf.Clamp01(normalizedProgress);
            float eased = EvaluateEasing(raw, values.Easing);
            var result = new VnWorkshopBackgroundTransitionSample
            {
                SourceAlpha = 1f,
                TargetAlpha = 0f,
                CurtainCoverage = 0f,
                CurtainPosition = values.Direction == VnWorkshopCurtainDirection.RightToLeft ? 1f - eased : eased,
                CurtainDarkness = 0f,
                CurtainDirection = values.Direction,
                Complete = raw >= 1f
            };

            if (raw <= 0f)
                return result;

            if (raw >= 1f)
            {
                result.SourceAlpha = 0f;
                result.TargetAlpha = 1f;
                result.CurtainCoverage = 0f;
                result.CurtainDarkness = 0f;
                return result;
            }

            switch (values.Mode)
            {
                case VnWorkshopBackgroundTransitionMode.Instant:
                    bool targetVisible = raw >= .5f;
                    result.SourceAlpha = targetVisible ? 0f : 1f;
                    result.TargetAlpha = targetVisible ? 1f : 0f;
                    return result;
                case VnWorkshopBackgroundTransitionMode.Fade:
                    result.SourceAlpha = 1f - eased;
                    result.TargetAlpha = eased;
                    return result;
                case VnWorkshopBackgroundTransitionMode.Curtain:
                    result.CurtainCoverage = 1f - Mathf.Abs((2f * eased) - 1f);
                    result.CurtainDarkness = result.CurtainCoverage * values.CurtainDarkness;
                    bool afterCover = eased >= .5f;
                    result.SourceAlpha = afterCover ? 0f : 1f;
                    result.TargetAlpha = afterCover ? 1f : 0f;
                    return result;
                default:
                    throw new ArgumentOutOfRangeException(nameof(values.Mode), values.Mode, "Unsupported background transition mode.");
            }
        }

        private static VnWorkshopCharacterTransitionSample SampleCharacterTransition(
            float normalizedProgress,
            VnWorkshopCharacterTransitionValues values,
            bool entering)
        {
            ValidateEnum(values.Mode, nameof(values.Mode));
            ValidateEnum(values.SlideDirection, nameof(values.SlideDirection));
            ValidateEnum(values.Easing, nameof(values.Easing));
            float raw = Mathf.Clamp01(normalizedProgress);

            if (values.Mode == VnWorkshopCharacterTransitionMode.Instant)
            {
                return new VnWorkshopCharacterTransitionSample
                {
                    PositionOffset = Vector2.zero,
                    Alpha = entering ? 1f : 0f,
                    Complete = true
                };
            }

            float motion = EvaluateEasing(raw, values.Easing);
            float fadeRaw;
            if (raw >= 1f)
            {
                fadeRaw = 1f;
            }
            else if (values.FadeDuration <= 0f)
            {
                fadeRaw = 1f;
            }
            else if (values.Duration <= 0f)
            {
                fadeRaw = raw > 0f ? 1f : 0f;
            }
            else
            {
                fadeRaw = Mathf.Clamp01((raw * values.Duration) / values.FadeDuration);
            }
            float fade = EvaluateEasing(fadeRaw, values.Easing);

            Vector2 offset = Vector2.zero;
            if (values.Mode == VnWorkshopCharacterTransitionMode.SlideAndFade)
            {
                float sign = values.SlideDirection == VnWorkshopSlideDirection.Left ? -1f : 1f;
                float distance = Mathf.Max(0f, values.SlideDistance);
                float scalar = entering ? 1f - motion : motion;
                offset = new Vector2(sign * distance * scalar, 0f);
            }

            return new VnWorkshopCharacterTransitionSample
            {
                PositionOffset = offset,
                Alpha = entering ? fade : 1f - fade,
                Complete = raw >= 1f
            };
        }

        private static VnCharacterVisualState ResolveAuthoredState(string stateId, string argumentName)
        {
            VnCharacterVisualState state;
            if (string.IsNullOrEmpty(stateId) || !VnCharacterVisualCatalog.TryResolve(stateId, out state))
                throw new ArgumentException("Unknown authored VN character visual state: " + (stateId ?? string.Empty), argumentName);
            return state;
        }

        private static float EvaluateEasing(float value, VnWorkshopEasing easing)
        {
            float t = Mathf.Clamp01(value);
            switch (easing)
            {
                case VnWorkshopEasing.Linear:
                    return t;
                case VnWorkshopEasing.EaseIn:
                    return t * t;
                case VnWorkshopEasing.EaseOut:
                    return 1f - ((1f - t) * (1f - t));
                case VnWorkshopEasing.EaseInOut:
                    return t * t * (3f - (2f * t));
                default:
                    throw new ArgumentOutOfRangeException(nameof(easing), easing, "Unsupported Workshop easing.");
            }
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

        private static VnWorkshopExpressionTransitionOverride EnsureExpressionTransition(VnPresentationWorkshopPreset preset)
        {
            if (preset.expressionTransition == null)
                preset.expressionTransition = new VnWorkshopExpressionTransitionOverride();
            return preset.expressionTransition;
        }

        private static VnWorkshopCharacterTransitionOverride EnsureCharacterTransition(VnPresentationWorkshopPreset preset)
        {
            if (preset.characterTransition == null)
                preset.characterTransition = new VnWorkshopCharacterTransitionOverride();
            return preset.characterTransition;
        }

        private static VnWorkshopActionBounceOverride EnsureActionBounce(VnPresentationWorkshopPreset preset)
        {
            if (preset.actionBounce == null)
                preset.actionBounce = new VnWorkshopActionBounceOverride();
            return preset.actionBounce;
        }

        private static VnWorkshopBackgroundTransitionOverride EnsureBackgroundTransition(VnPresentationWorkshopPreset preset)
        {
            if (preset.backgroundTransition == null)
                preset.backgroundTransition = new VnWorkshopBackgroundTransitionOverride();
            return preset.backgroundTransition;
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
