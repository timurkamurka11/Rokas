using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnWorkshopElement
    {
        DialoguePanel,
        MinaBody,
        SpeakerName,
        DialogueText,
        Back,
        Next,
        MuteHitRegion,
        PauseHitRegion,
        SkipHitRegion
    }

    public enum VnWorkshopResolution
    {
        Reference1920x1080,
        Wide1280x720,
        FourThree1024x768
    }

    [Serializable]
    public sealed class VnWorkshopElementOverride
    {
        public bool hasPositionDelta;
        public Vector2 positionDelta;
        public bool hasSizeDelta;
        public Vector2 sizeDelta;
        public bool hasScaleMultiplier;
        public float scaleMultiplier = 1f;

        public bool HasAnyOverride
        {
            get { return hasPositionDelta || hasSizeDelta || hasScaleMultiplier; }
        }

        public void Clear()
        {
            hasPositionDelta = false;
            positionDelta = Vector2.zero;
            hasSizeDelta = false;
            sizeDelta = Vector2.zero;
            hasScaleMultiplier = false;
            scaleMultiplier = 1f;
        }
    }

    [Serializable]
    public sealed class VnWorkshopFocusOverride
    {
        public bool hasTwoCharacterOffset;
        public float twoCharacterOffset;
        public bool hasActiveScale;
        public float activeScale;
        public bool hasInactiveScale;
        public float inactiveScale;
        public bool hasInactiveBrightness;
        public float inactiveBrightness;
        public bool hasInactiveAlpha;
        public float inactiveAlpha;

        public bool HasAnyOverride
        {
            get
            {
                return hasTwoCharacterOffset || hasActiveScale || hasInactiveScale ||
                       hasInactiveBrightness || hasInactiveAlpha;
            }
        }

        public void Clear()
        {
            hasTwoCharacterOffset = false;
            twoCharacterOffset = 0f;
            hasActiveScale = false;
            activeScale = 0f;
            hasInactiveScale = false;
            inactiveScale = 0f;
            hasInactiveBrightness = false;
            inactiveBrightness = 0f;
            hasInactiveAlpha = false;
            inactiveAlpha = 0f;
        }
    }

    [Serializable]
    public sealed class VnPresentationWorkshopPreset
    {
        public VnWorkshopElementOverride dialoguePanel = new VnWorkshopElementOverride();
        public VnWorkshopElementOverride minaBody = new VnWorkshopElementOverride();
        public VnWorkshopElementOverride speakerName = new VnWorkshopElementOverride();
        public VnWorkshopElementOverride dialogueText = new VnWorkshopElementOverride();
        public VnWorkshopElementOverride back = new VnWorkshopElementOverride();
        public VnWorkshopElementOverride next = new VnWorkshopElementOverride();
        public VnWorkshopElementOverride muteHitRegion = new VnWorkshopElementOverride();
        public VnWorkshopElementOverride pauseHitRegion = new VnWorkshopElementOverride();
        public VnWorkshopElementOverride skipHitRegion = new VnWorkshopElementOverride();
        public VnWorkshopFocusOverride focus = new VnWorkshopFocusOverride();
        public VnWorkshopTypographyOverride typography = new VnWorkshopTypographyOverride();
        public VnWorkshopTypewriterOverride typewriter = new VnWorkshopTypewriterOverride();
        public VnWorkshopTimingOverride timing = new VnWorkshopTimingOverride();
        public VnWorkshopExpressionTransitionOverride expressionTransition = new VnWorkshopExpressionTransitionOverride();
        public VnWorkshopCharacterTransitionOverride characterTransition = new VnWorkshopCharacterTransitionOverride();

        public bool HasAnyOverride
        {
            get
            {
                return dialoguePanel.HasAnyOverride || minaBody.HasAnyOverride ||
                       speakerName.HasAnyOverride || dialogueText.HasAnyOverride ||
                       back.HasAnyOverride || next.HasAnyOverride ||
                       muteHitRegion.HasAnyOverride || pauseHitRegion.HasAnyOverride ||
                       skipHitRegion.HasAnyOverride || focus.HasAnyOverride ||
                       (typography != null && typography.HasAnyOverride) ||
                       (typewriter != null && typewriter.HasAnyOverride) ||
                       (timing != null && timing.HasAnyOverride) ||
                       (expressionTransition != null && expressionTransition.HasAnyOverride) ||
                       (characterTransition != null && characterTransition.HasAnyOverride);
            }
        }

        public VnWorkshopElementOverride GetElementOverride(VnWorkshopElement element)
        {
            switch (element)
            {
                case VnWorkshopElement.DialoguePanel: return dialoguePanel;
                case VnWorkshopElement.MinaBody: return minaBody;
                case VnWorkshopElement.SpeakerName: return speakerName;
                case VnWorkshopElement.DialogueText: return dialogueText;
                case VnWorkshopElement.Back: return back;
                case VnWorkshopElement.Next: return next;
                case VnWorkshopElement.MuteHitRegion: return muteHitRegion;
                case VnWorkshopElement.PauseHitRegion: return pauseHitRegion;
                case VnWorkshopElement.SkipHitRegion: return skipHitRegion;
                default: throw new ArgumentOutOfRangeException("element", element, null);
            }
        }

        public void ResetElement(VnWorkshopElement element)
        {
            GetElementOverride(element).Clear();
        }

        public void ResetAll()
        {
            dialoguePanel.Clear();
            minaBody.Clear();
            speakerName.Clear();
            dialogueText.Clear();
            back.Clear();
            next.Clear();
            muteHitRegion.Clear();
            pauseHitRegion.Clear();
            skipHitRegion.Clear();
            focus.Clear();
            if (typography == null) typography = new VnWorkshopTypographyOverride();
            typography.Clear();
            if (typewriter == null) typewriter = new VnWorkshopTypewriterOverride();
            typewriter.Clear();
            if (timing == null) timing = new VnWorkshopTimingOverride();
            timing.Clear();
            if (expressionTransition == null) expressionTransition = new VnWorkshopExpressionTransitionOverride();
            expressionTransition.Clear();
            if (characterTransition == null) characterTransition = new VnWorkshopCharacterTransitionOverride();
            characterTransition.Clear();
        }
    }

    public struct VnWorkshopFocusValues
    {
        public float TwoCharacterOffset;
        public float ActiveScale;
        public float InactiveScale;
        public float InactiveBrightness;
        public float InactiveAlpha;
    }
}
