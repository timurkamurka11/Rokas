using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnWorkshopUiFeedbackState
    {
        Normal,
        Hover,
        Pressed,
        Release
    }

    [Serializable]
    public sealed class VnWorkshopUiFeedbackOverride
    {
        public bool hasHoverScale;
        public float hoverScale;
        public bool hasPressedScale;
        public float pressedScale;
        public bool hasPressedOffset;
        public Vector2 pressedOffset;
        public bool hasDuration;
        public float duration;
        public bool hasEasing;
        public VnWorkshopEasing easing;
        public bool hasHoverBrightness;
        public float hoverBrightness;
        public bool hasPressedBrightness;
        public float pressedBrightness;
        public bool hasHoverAlpha;
        public float hoverAlpha;
        public bool hasPressedAlpha;
        public float pressedAlpha;
        public bool hasHoverOverlayHighlight;
        public float hoverOverlayHighlight;
        public bool hasPressedOverlayHighlight;
        public float pressedOverlayHighlight;

        public bool HasAnyOverride
        {
            get
            {
                return hasHoverScale || hasPressedScale || hasPressedOffset || hasDuration || hasEasing ||
                       hasHoverBrightness || hasPressedBrightness || hasHoverAlpha || hasPressedAlpha ||
                       hasHoverOverlayHighlight || hasPressedOverlayHighlight;
            }
        }

        public void Clear()
        {
            hasHoverScale = hasPressedScale = hasPressedOffset = hasDuration = hasEasing = false;
            hasHoverBrightness = hasPressedBrightness = hasHoverAlpha = hasPressedAlpha = false;
            hasHoverOverlayHighlight = hasPressedOverlayHighlight = false;
            hoverScale = pressedScale = duration = 0f;
            pressedOffset = Vector2.zero;
            easing = VnWorkshopEasing.EaseOut;
            hoverBrightness = pressedBrightness = hoverAlpha = pressedAlpha = 0f;
            hoverOverlayHighlight = pressedOverlayHighlight = 0f;
        }
    }

    public struct VnWorkshopUiFeedbackValues
    {
        public float HoverScale;
        public float PressedScale;
        public Vector2 PressedOffset;
        public float Duration;
        public VnWorkshopEasing Easing;
        public float HoverBrightness;
        public float PressedBrightness;
        public float HoverAlpha;
        public float PressedAlpha;
        public float HoverOverlayHighlight;
        public float PressedOverlayHighlight;
    }

    public struct VnWorkshopUiFeedbackSample
    {
        public float ScaleMultiplier;
        public Vector2 PositionOffset;
        public float Brightness;
        public float Alpha;
        public float OverlayHighlight;
        public bool UsesIndependentTransform;
        public bool Complete;
    }

    public static partial class VnPresentationWorkshopVn10Resolver
    {
        private static readonly VnWorkshopUiFeedbackValues UiFeedbackBaseline = new VnWorkshopUiFeedbackValues
        {
            HoverScale = 1.06f,
            PressedScale = .94f,
            PressedOffset = new Vector2(0f, -5f),
            Duration = .12f,
            Easing = VnWorkshopEasing.EaseOut,
            HoverBrightness = 1.08f,
            PressedBrightness = .88f,
            HoverAlpha = 1f,
            PressedAlpha = .90f,
            HoverOverlayHighlight = .18f,
            PressedOverlayHighlight = .48f
        };

        public static VnWorkshopUiFeedbackValues ResolveUiFeedback(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopUiFeedbackOverride source = EnsureUiFeedback(preset);
            VnWorkshopUiFeedbackValues values = UiFeedbackBaseline;
            if (source.hasHoverScale) values.HoverScale = source.hoverScale;
            if (source.hasPressedScale) values.PressedScale = source.pressedScale;
            if (source.hasPressedOffset) values.PressedOffset = source.pressedOffset;
            if (source.hasDuration) values.Duration = source.duration;
            if (source.hasEasing) values.Easing = source.easing;
            if (source.hasHoverBrightness) values.HoverBrightness = source.hoverBrightness;
            if (source.hasPressedBrightness) values.PressedBrightness = source.pressedBrightness;
            if (source.hasHoverAlpha) values.HoverAlpha = source.hoverAlpha;
            if (source.hasPressedAlpha) values.PressedAlpha = source.pressedAlpha;
            if (source.hasHoverOverlayHighlight) values.HoverOverlayHighlight = source.hoverOverlayHighlight;
            if (source.hasPressedOverlayHighlight) values.PressedOverlayHighlight = source.pressedOverlayHighlight;
            ValidateUiFeedback(values);
            return values;
        }

        public static void SetUiFeedbackPreviewOverrides(
            VnPresentationWorkshopPreset preset,
            float hoverScale,
            float pressedScale,
            Vector2 pressedOffset,
            float duration,
            VnWorkshopEasing easing,
            float hoverBrightness,
            float pressedBrightness,
            float hoverAlpha,
            float pressedAlpha,
            float hoverOverlayHighlight,
            float pressedOverlayHighlight)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            var values = new VnWorkshopUiFeedbackValues
            {
                HoverScale = hoverScale,
                PressedScale = pressedScale,
                PressedOffset = pressedOffset,
                Duration = duration,
                Easing = easing,
                HoverBrightness = hoverBrightness,
                PressedBrightness = pressedBrightness,
                HoverAlpha = hoverAlpha,
                PressedAlpha = pressedAlpha,
                HoverOverlayHighlight = hoverOverlayHighlight,
                PressedOverlayHighlight = pressedOverlayHighlight
            };
            ValidateUiFeedback(values);

            VnWorkshopUiFeedbackOverride target = EnsureUiFeedback(preset);
            target.hasHoverScale = !Mathf.Approximately(hoverScale, UiFeedbackBaseline.HoverScale); target.hoverScale = hoverScale;
            target.hasPressedScale = !Mathf.Approximately(pressedScale, UiFeedbackBaseline.PressedScale); target.pressedScale = pressedScale;
            target.hasPressedOffset = pressedOffset != UiFeedbackBaseline.PressedOffset; target.pressedOffset = pressedOffset;
            target.hasDuration = !Mathf.Approximately(duration, UiFeedbackBaseline.Duration); target.duration = duration;
            target.hasEasing = easing != UiFeedbackBaseline.Easing; target.easing = easing;
            target.hasHoverBrightness = !Mathf.Approximately(hoverBrightness, UiFeedbackBaseline.HoverBrightness); target.hoverBrightness = hoverBrightness;
            target.hasPressedBrightness = !Mathf.Approximately(pressedBrightness, UiFeedbackBaseline.PressedBrightness); target.pressedBrightness = pressedBrightness;
            target.hasHoverAlpha = !Mathf.Approximately(hoverAlpha, UiFeedbackBaseline.HoverAlpha); target.hoverAlpha = hoverAlpha;
            target.hasPressedAlpha = !Mathf.Approximately(pressedAlpha, UiFeedbackBaseline.PressedAlpha); target.pressedAlpha = pressedAlpha;
            target.hasHoverOverlayHighlight = !Mathf.Approximately(hoverOverlayHighlight, UiFeedbackBaseline.HoverOverlayHighlight); target.hoverOverlayHighlight = hoverOverlayHighlight;
            target.hasPressedOverlayHighlight = !Mathf.Approximately(pressedOverlayHighlight, UiFeedbackBaseline.PressedOverlayHighlight); target.pressedOverlayHighlight = pressedOverlayHighlight;
        }

        public static void ResetUiFeedbackPreviewOverrides(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            EnsureUiFeedback(preset).Clear();
        }

        public static VnWorkshopUiFeedbackSample SampleUiFeedback(
            VnWorkshopElement element,
            VnWorkshopUiFeedbackState state,
            float normalizedProgress,
            VnWorkshopUiFeedbackValues values)
        {
            ValidateUiFeedback(values);
            if (!Enum.IsDefined(typeof(VnWorkshopUiFeedbackState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            bool independent = element == VnWorkshopElement.Back || element == VnWorkshopElement.Next;
            bool baked = element == VnWorkshopElement.MuteHitRegion ||
                         element == VnWorkshopElement.PauseHitRegion ||
                         element == VnWorkshopElement.SkipHitRegion;
            if (!independent && !baked)
                throw new ArgumentException("UI feedback is only defined for Workshop controls.", nameof(element));

            bool snap = values.Duration <= 0f;
            float raw = snap ? 1f : Mathf.Clamp01(normalizedProgress);
            float t = EvaluateUiFeedbackEasing(raw, values.Easing);

            float targetScale = 1f;
            Vector2 targetOffset = Vector2.zero;
            float targetBrightness = 1f;
            float targetAlpha = 1f;
            float targetOverlay = 0f;
            float startScale = 1f;
            Vector2 startOffset = Vector2.zero;
            float startBrightness = 1f;
            float startAlpha = 1f;
            float startOverlay = 0f;

            switch (state)
            {
                case VnWorkshopUiFeedbackState.Normal:
                    raw = 1f;
                    t = 1f;
                    break;
                case VnWorkshopUiFeedbackState.Hover:
                    targetScale = values.HoverScale;
                    targetBrightness = values.HoverBrightness;
                    targetAlpha = values.HoverAlpha;
                    targetOverlay = values.HoverOverlayHighlight;
                    break;
                case VnWorkshopUiFeedbackState.Pressed:
                    targetScale = values.PressedScale;
                    targetOffset = values.PressedOffset;
                    targetBrightness = values.PressedBrightness;
                    targetAlpha = values.PressedAlpha;
                    targetOverlay = values.PressedOverlayHighlight;
                    break;
                case VnWorkshopUiFeedbackState.Release:
                    startScale = values.PressedScale;
                    startOffset = values.PressedOffset;
                    startBrightness = values.PressedBrightness;
                    startAlpha = values.PressedAlpha;
                    startOverlay = values.PressedOverlayHighlight;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }

            float scale = Mathf.LerpUnclamped(startScale, targetScale, t);
            Vector2 offset = Vector2.LerpUnclamped(startOffset, targetOffset, t);
            if (!independent)
            {
                scale = 1f;
                offset = Vector2.zero;
            }

            return new VnWorkshopUiFeedbackSample
            {
                ScaleMultiplier = scale,
                PositionOffset = offset,
                Brightness = Mathf.LerpUnclamped(startBrightness, targetBrightness, t),
                Alpha = Mathf.LerpUnclamped(startAlpha, targetAlpha, t),
                OverlayHighlight = Mathf.LerpUnclamped(startOverlay, targetOverlay, t),
                UsesIndependentTransform = independent,
                Complete = snap || state == VnWorkshopUiFeedbackState.Normal || raw >= 1f
            };
        }

        private static VnWorkshopUiFeedbackOverride EnsureUiFeedback(VnPresentationWorkshopPreset preset)
        {
            if (preset.uiFeedback == null) preset.uiFeedback = new VnWorkshopUiFeedbackOverride();
            return preset.uiFeedback;
        }

        private static void ValidateUiFeedback(VnWorkshopUiFeedbackValues values)
        {
            RequireUiRange(values.HoverScale, .1f, 3f, nameof(values.HoverScale));
            RequireUiRange(values.PressedScale, .1f, 3f, nameof(values.PressedScale));
            RequireUiRange(values.PressedOffset.x, -200f, 200f, nameof(values.PressedOffset));
            RequireUiRange(values.PressedOffset.y, -200f, 200f, nameof(values.PressedOffset));
            RequireUiRange(values.Duration, 0f, 10f, nameof(values.Duration));
            RequireUiRange(values.HoverBrightness, 0f, 1.5f, nameof(values.HoverBrightness));
            RequireUiRange(values.PressedBrightness, 0f, 1.5f, nameof(values.PressedBrightness));
            RequireUiRange(values.HoverAlpha, 0f, 1f, nameof(values.HoverAlpha));
            RequireUiRange(values.PressedAlpha, 0f, 1f, nameof(values.PressedAlpha));
            RequireUiRange(values.HoverOverlayHighlight, 0f, 1f, nameof(values.HoverOverlayHighlight));
            RequireUiRange(values.PressedOverlayHighlight, 0f, 1f, nameof(values.PressedOverlayHighlight));
            if (!Enum.IsDefined(typeof(VnWorkshopEasing), values.Easing))
                throw new ArgumentOutOfRangeException(nameof(values.Easing));
        }

        private static void RequireUiRange(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(name, value, null);
        }

        private static float EvaluateUiFeedbackEasing(float value, VnWorkshopEasing easing)
        {
            float t = Mathf.Clamp01(value);
            switch (easing)
            {
                case VnWorkshopEasing.Linear: return t;
                case VnWorkshopEasing.EaseIn: return t * t;
                case VnWorkshopEasing.EaseOut: return 1f - ((1f - t) * (1f - t));
                case VnWorkshopEasing.EaseInOut: return t * t * (3f - (2f * t));
                default: throw new ArgumentOutOfRangeException(nameof(easing));
            }
        }
    }
}
