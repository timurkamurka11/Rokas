using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnWorkshopEasing
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    public enum VnWorkshopCharacterTransitionMode
    {
        Instant,
        Fade,
        SlideAndFade
    }

    public enum VnWorkshopSlideDirection
    {
        Left,
        Right
    }

    public enum VnWorkshopBackgroundTransitionMode
    {
        Instant,
        Fade,
        Curtain
    }

    public enum VnWorkshopCurtainDirection
    {
        RightToLeft,
        LeftToRight
    }

    [Serializable]
    public sealed class VnWorkshopExpressionTransitionOverride
    {
        public bool hasDuration;
        public float duration;
        public bool hasEasing;
        public VnWorkshopEasing easing;

        public bool HasAnyOverride
        {
            get { return hasDuration || hasEasing; }
        }

        public void Clear()
        {
            hasDuration = false;
            duration = 0f;
            hasEasing = false;
            easing = VnWorkshopEasing.EaseInOut;
        }
    }

    public struct VnWorkshopExpressionTransitionValues
    {
        public float Duration;
        public VnWorkshopEasing Easing;
    }

    public struct VnWorkshopExpressionTransitionSample
    {
        public string StartStateId;
        public string EndStateId;
        public float StartAlpha;
        public float EndAlpha;
        public bool Complete;
    }

    [Serializable]
    public sealed class VnWorkshopCharacterTransitionOverride
    {
        public bool hasMode;
        public VnWorkshopCharacterTransitionMode mode;
        public bool hasDuration;
        public float duration;
        public bool hasFadeDuration;
        public float fadeDuration;
        public bool hasSlideDistance;
        public float slideDistance;
        public bool hasSlideDirection;
        public VnWorkshopSlideDirection slideDirection;
        public bool hasEasing;
        public VnWorkshopEasing easing;

        public bool HasAnyOverride
        {
            get
            {
                return hasMode || hasDuration || hasFadeDuration || hasSlideDistance ||
                       hasSlideDirection || hasEasing;
            }
        }

        public void Clear()
        {
            hasMode = false;
            mode = VnWorkshopCharacterTransitionMode.Fade;
            hasDuration = false;
            duration = 0f;
            hasFadeDuration = false;
            fadeDuration = 0f;
            hasSlideDistance = false;
            slideDistance = 0f;
            hasSlideDirection = false;
            slideDirection = VnWorkshopSlideDirection.Left;
            hasEasing = false;
            easing = VnWorkshopEasing.EaseInOut;
        }
    }

    public struct VnWorkshopCharacterTransitionValues
    {
        public VnWorkshopCharacterTransitionMode Mode;
        public float Duration;
        public float FadeDuration;
        public float SlideDistance;
        public VnWorkshopSlideDirection SlideDirection;
        public VnWorkshopEasing Easing;
    }

    public struct VnWorkshopCharacterTransitionSample
    {
        public Vector2 PositionOffset;
        public float Alpha;
        public bool Complete;
    }

    [Serializable]
    public sealed class VnWorkshopActionBounceOverride
    {
        public bool hasAmplitude;
        public float amplitude;
        public bool hasDuration;
        public float duration;
        public bool hasScaleEmphasis;
        public float scaleEmphasis;
        public bool hasOvershoot;
        public float overshoot;
        public bool hasEasing;
        public VnWorkshopEasing easing;

        public bool HasAnyOverride
        {
            get { return hasAmplitude || hasDuration || hasScaleEmphasis || hasOvershoot || hasEasing; }
        }

        public void Clear()
        {
            hasAmplitude = false;
            amplitude = 0f;
            hasDuration = false;
            duration = 0f;
            hasScaleEmphasis = false;
            scaleEmphasis = 0f;
            hasOvershoot = false;
            overshoot = 0f;
            hasEasing = false;
            easing = VnWorkshopEasing.EaseInOut;
        }
    }

    public struct VnWorkshopActionBounceValues
    {
        public float Amplitude;
        public float Duration;
        public float ScaleEmphasis;
        public float Overshoot;
        public VnWorkshopEasing Easing;
    }

    public struct VnWorkshopActionBounceSample
    {
        public Vector2 PositionOffset;
        public float ScaleMultiplier;
        public bool Complete;
    }

    [Serializable]
    public sealed class VnWorkshopBackgroundTransitionOverride
    {
        public bool hasMode;
        public VnWorkshopBackgroundTransitionMode mode;
        public bool hasDuration;
        public float duration;
        public bool hasCurtainDarkness;
        public float curtainDarkness;
        public bool hasDirection;
        public VnWorkshopCurtainDirection direction;
        public bool hasEasing;
        public VnWorkshopEasing easing;

        public bool HasAnyOverride
        {
            get { return hasMode || hasDuration || hasCurtainDarkness || hasDirection || hasEasing; }
        }

        public void Clear()
        {
            hasMode = false;
            mode = VnWorkshopBackgroundTransitionMode.Fade;
            hasDuration = false;
            duration = 0f;
            hasCurtainDarkness = false;
            curtainDarkness = 0f;
            hasDirection = false;
            direction = VnWorkshopCurtainDirection.RightToLeft;
            hasEasing = false;
            easing = VnWorkshopEasing.EaseInOut;
        }
    }

    public struct VnWorkshopBackgroundTransitionValues
    {
        public VnWorkshopBackgroundTransitionMode Mode;
        public float Duration;
        public float CurtainDarkness;
        public VnWorkshopCurtainDirection Direction;
        public VnWorkshopEasing Easing;
    }

    public struct VnWorkshopBackgroundTransitionSample
    {
        public float SourceAlpha;
        public float TargetAlpha;
        public float CurtainCoverage;
        public float CurtainPosition;
        public float CurtainDarkness;
        public VnWorkshopCurtainDirection CurtainDirection;
        public bool Complete;
    }
}
