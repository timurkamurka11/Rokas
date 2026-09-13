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
}
