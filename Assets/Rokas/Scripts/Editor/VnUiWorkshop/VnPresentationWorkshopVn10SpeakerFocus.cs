using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public struct VnWorkshopSpeakerFocusValues
    {
        public float ActiveScale;
        public float ActiveBrightness;
        public float ActiveForwardOffset;
        public float InactiveScale;
        public float InactiveBrightness;
        public float InactiveAlpha;
        public float TransitionDuration;
        public VnWorkshopEasing Easing;
    }

    public struct VnWorkshopSpeakerFocusSample
    {
        public Vector2 PositionOffset;
        public float Scale;
        public float Brightness;
        public float Alpha;
        public bool Complete;
    }

    public static partial class VnPresentationWorkshopVn10Resolver
    {
        private static readonly VnWorkshopSpeakerFocusValues SpeakerFocusBaseline =
            new VnWorkshopSpeakerFocusValues
            {
                ActiveScale = VnPresentationWorkshopBaseline.ActiveFocusScale,
                ActiveBrightness = 1f,
                ActiveForwardOffset = 12f,
                InactiveScale = VnPresentationWorkshopBaseline.InactiveFocusScale,
                InactiveBrightness = VnPresentationWorkshopBaseline.InactiveBrightness,
                InactiveAlpha = VnPresentationWorkshopBaseline.InactiveAlpha,
                TransitionDuration = .22f,
                Easing = VnWorkshopEasing.EaseInOut
            };

        public static VnWorkshopSpeakerFocusValues ResolveSpeakerFocus(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (preset.focus == null) preset.focus = new VnWorkshopFocusOverride();
            VnWorkshopFocusOverride source = preset.focus;
            VnWorkshopSpeakerFocusValues values = SpeakerFocusBaseline;
            if (source.hasActiveScale) values.ActiveScale = source.activeScale;
            if (source.hasActiveBrightness) values.ActiveBrightness = source.activeBrightness;
            if (source.hasActiveForwardOffset) values.ActiveForwardOffset = source.activeForwardOffset;
            if (source.hasInactiveScale) values.InactiveScale = source.inactiveScale;
            if (source.hasInactiveBrightness) values.InactiveBrightness = source.inactiveBrightness;
            if (source.hasInactiveAlpha) values.InactiveAlpha = source.inactiveAlpha;
            if (source.hasTransitionDuration) values.TransitionDuration = source.transitionDuration;
            if (source.hasEasing) values.Easing = source.easing;
            ValidateSpeakerFocus(values);
            return values;
        }

        public static void SetSpeakerFocusPreviewOverrides(
            VnPresentationWorkshopPreset preset,
            float activeScale,
            float activeBrightness,
            float activeForwardOffset,
            float inactiveScale,
            float inactiveBrightness,
            float inactiveAlpha,
            float transitionDuration,
            VnWorkshopEasing easing)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            var values = new VnWorkshopSpeakerFocusValues
            {
                ActiveScale = activeScale,
                ActiveBrightness = activeBrightness,
                ActiveForwardOffset = activeForwardOffset,
                InactiveScale = inactiveScale,
                InactiveBrightness = inactiveBrightness,
                InactiveAlpha = inactiveAlpha,
                TransitionDuration = transitionDuration,
                Easing = easing
            };
            ValidateSpeakerFocus(values);

            if (preset.focus == null) preset.focus = new VnWorkshopFocusOverride();
            VnWorkshopFocusOverride target = preset.focus;
            target.hasActiveScale = !Mathf.Approximately(activeScale, SpeakerFocusBaseline.ActiveScale);
            target.activeScale = activeScale;
            target.hasActiveBrightness = !Mathf.Approximately(activeBrightness, SpeakerFocusBaseline.ActiveBrightness);
            target.activeBrightness = activeBrightness;
            target.hasActiveForwardOffset = !Mathf.Approximately(activeForwardOffset, SpeakerFocusBaseline.ActiveForwardOffset);
            target.activeForwardOffset = activeForwardOffset;
            target.hasInactiveScale = !Mathf.Approximately(inactiveScale, SpeakerFocusBaseline.InactiveScale);
            target.inactiveScale = inactiveScale;
            target.hasInactiveBrightness = !Mathf.Approximately(inactiveBrightness, SpeakerFocusBaseline.InactiveBrightness);
            target.inactiveBrightness = inactiveBrightness;
            target.hasInactiveAlpha = !Mathf.Approximately(inactiveAlpha, SpeakerFocusBaseline.InactiveAlpha);
            target.inactiveAlpha = inactiveAlpha;
            target.hasTransitionDuration = !Mathf.Approximately(transitionDuration, SpeakerFocusBaseline.TransitionDuration);
            target.transitionDuration = transitionDuration;
            target.hasEasing = easing != SpeakerFocusBaseline.Easing;
            target.easing = easing;
        }

        public static void ResetSpeakerFocusPreviewOverrides(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (preset.focus == null) preset.focus = new VnWorkshopFocusOverride();
            VnWorkshopFocusOverride target = preset.focus;
            target.hasActiveScale = false;
            target.activeScale = 0f;
            target.hasActiveBrightness = false;
            target.activeBrightness = 0f;
            target.hasActiveForwardOffset = false;
            target.activeForwardOffset = 0f;
            target.hasInactiveScale = false;
            target.inactiveScale = 0f;
            target.hasInactiveBrightness = false;
            target.inactiveBrightness = 0f;
            target.hasInactiveAlpha = false;
            target.inactiveAlpha = 0f;
            target.hasTransitionDuration = false;
            target.transitionDuration = 0f;
            target.hasEasing = false;
            target.easing = VnWorkshopEasing.EaseInOut;
        }

        public static VnWorkshopSpeakerFocusSample SampleSpeakerFocus(
            int visibleCharacterCount,
            int previousActiveIndex,
            int activeIndex,
            int characterIndex,
            float normalizedProgress,
            VnWorkshopSpeakerFocusValues values)
        {
            ValidateSpeakerCount(visibleCharacterCount, nameof(visibleCharacterCount));
            ValidateSpeakerFocus(values);
            if (characterIndex < 0 || characterIndex >= visibleCharacterCount)
                throw new ArgumentOutOfRangeException(nameof(characterIndex));
            if (previousActiveIndex < 0 || previousActiveIndex >= visibleCharacterCount)
                throw new ArgumentOutOfRangeException(nameof(previousActiveIndex));
            if (activeIndex < 0 || activeIndex >= visibleCharacterCount)
                throw new ArgumentOutOfRangeException(nameof(activeIndex));

            if (visibleCharacterCount == 1)
            {
                return new VnWorkshopSpeakerFocusSample
                {
                    PositionOffset = Vector2.zero,
                    Scale = 1f,
                    Brightness = 1f,
                    Alpha = 1f,
                    Complete = true
                };
            }

            bool startActive = characterIndex == previousActiveIndex;
            bool endActive = characterIndex == activeIndex;
            bool snap = values.TransitionDuration <= 0f;
            float raw = snap ? 1f : Mathf.Clamp01(normalizedProgress);
            float t = EvaluateSpeakerFocusEasing(raw, values.Easing);

            float startScale = startActive ? values.ActiveScale : values.InactiveScale;
            float endScale = endActive ? values.ActiveScale : values.InactiveScale;
            float startBrightness = startActive ? values.ActiveBrightness : values.InactiveBrightness;
            float endBrightness = endActive ? values.ActiveBrightness : values.InactiveBrightness;
            float startAlpha = startActive ? 1f : values.InactiveAlpha;
            float endAlpha = endActive ? 1f : values.InactiveAlpha;
            float startForward = startActive ? values.ActiveForwardOffset : 0f;
            float endForward = endActive ? values.ActiveForwardOffset : 0f;

            return new VnWorkshopSpeakerFocusSample
            {
                PositionOffset = new Vector2(0f, Mathf.LerpUnclamped(startForward, endForward, t)),
                Scale = Mathf.LerpUnclamped(startScale, endScale, t),
                Brightness = Mathf.LerpUnclamped(startBrightness, endBrightness, t),
                Alpha = Mathf.LerpUnclamped(startAlpha, endAlpha, t),
                Complete = snap || startActive == endActive || raw >= 1f
            };
        }

        private static void ValidateSpeakerFocus(VnWorkshopSpeakerFocusValues values)
        {
            RequireSpeakerRange(values.ActiveScale, .1f, 3f, nameof(values.ActiveScale));
            RequireSpeakerRange(values.ActiveBrightness, 0f, 1.5f, nameof(values.ActiveBrightness));
            RequireSpeakerRange(values.ActiveForwardOffset, 0f, 200f, nameof(values.ActiveForwardOffset));
            RequireSpeakerRange(values.InactiveScale, .1f, 3f, nameof(values.InactiveScale));
            RequireSpeakerRange(values.InactiveBrightness, 0f, 1.5f, nameof(values.InactiveBrightness));
            RequireSpeakerRange(values.InactiveAlpha, 0f, 1f, nameof(values.InactiveAlpha));
            RequireSpeakerRange(values.TransitionDuration, 0f, 10f, nameof(values.TransitionDuration));
            if (!Enum.IsDefined(typeof(VnWorkshopEasing), values.Easing))
                throw new ArgumentOutOfRangeException(nameof(values.Easing), values.Easing, null);
        }

        private static void ValidateSpeakerCount(int value, string name)
        {
            if (value < 1 || value > 3)
                throw new ArgumentOutOfRangeException(name, value, "Workshop speaker focus supports one to three visible characters.");
        }

        private static void RequireSpeakerRange(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(name, value, null);
        }

        private static float EvaluateSpeakerFocusEasing(float value, VnWorkshopEasing easing)
        {
            float t = Mathf.Clamp01(value);
            switch (easing)
            {
                case VnWorkshopEasing.Linear: return t;
                case VnWorkshopEasing.EaseIn: return t * t;
                case VnWorkshopEasing.EaseOut: return 1f - ((1f - t) * (1f - t));
                case VnWorkshopEasing.EaseInOut: return t * t * (3f - (2f * t));
                default: throw new ArgumentOutOfRangeException(nameof(easing), easing, null);
            }
        }
    }
}
