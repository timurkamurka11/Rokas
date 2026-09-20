using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnPresentationWorkshopBaseline
    {
        public const string SourceHead = "04a53a955286bb926365459ee11c6836ae490282";
        public const float CanvasMatchWidthOrHeight = .5f;
        public const float DialoguePanelAspect = 2048f / 682f;
        public const float MinaBodyHeight = 1240f;
        public const float MinaBodyYFactor = .37f;
        public const float KeikoBodyHeight = 980f;
        public const float TwoCharacterOffset = 310f;
        public const float ActiveFocusScale = 1.05f;
        public const float InactiveFocusScale = .94f;
        public const float InactiveBrightness = .76f;
        public const float InactiveAlpha = .84f;

        public static Vector2 ReferenceResolution
        {
            get { return new Vector2(1920f, 1080f); }
        }

        public static Vector2 DialoguePanelAnchorMin
        {
            get { return new Vector2(.04f, 0f); }
        }

        public static Vector2 DialoguePanelAnchorMax
        {
            get { return new Vector2(.96f, 0f); }
        }

        public static Vector2 DialoguePanelAnchoredPosition
        {
            get { return new Vector2(0f, -98f); }
        }

        public static Vector2 SpeakerAnchorMin
        {
            get { return new Vector2(.17f, .69f); }
        }

        public static Vector2 SpeakerAnchorMax
        {
            get { return new Vector2(.42f, .81f); }
        }

        public static Vector2 SpeakerOffsetMin
        {
            get { return new Vector2(12f, 0f); }
        }

        public static Vector2 SpeakerOffsetMax
        {
            get { return new Vector2(-8f, 0f); }
        }

        public static Vector2 DialogueAnchorMin
        {
            get { return new Vector2(.075f, .25f); }
        }

        public static Vector2 DialogueAnchorMax
        {
            get { return new Vector2(.86f, .66f); }
        }

        public static Vector2 DialogueOffsetMin
        {
            get { return new Vector2(18f, 8f); }
        }

        public static Vector2 DialogueOffsetMax
        {
            get { return new Vector2(-18f, -8f); }
        }

        public static string GetControlPresentationKind(string controlName)
        {
            if (string.Equals(controlName, "Mute", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controlName, "Pause", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controlName, "Skip", StringComparison.OrdinalIgnoreCase))
            {
                return "Baked into panel";
            }

            if (string.Equals(controlName, "Back", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controlName, "Next", StringComparison.OrdinalIgnoreCase))
            {
                return "Independent";
            }

            return "Unknown";
        }

        public static Vector2 GetControlAnchor(VnWorkshopElement element)
        {
            switch (element)
            {
                case VnWorkshopElement.MuteHitRegion: return new Vector2(.962f, .585f);
                case VnWorkshopElement.PauseHitRegion: return new Vector2(.962f, .440f);
                case VnWorkshopElement.SkipHitRegion: return new Vector2(.962f, .297f);
                case VnWorkshopElement.Back: return new Vector2(.900f, .440f);
                case VnWorkshopElement.Next: return new Vector2(.900f, .297f);
                default: throw new ArgumentException("Element is not a Workshop control.", "element");
            }
        }

        public static Vector2 GetControlSize(VnWorkshopElement element)
        {
            switch (element)
            {
                case VnWorkshopElement.MuteHitRegion:
                case VnWorkshopElement.PauseHitRegion:
                case VnWorkshopElement.SkipHitRegion:
                    return new Vector2(76f, 76f);
                case VnWorkshopElement.Back:
                case VnWorkshopElement.Next:
                    return new Vector2(64f, 72f);
                default:
                    throw new ArgumentException("Element is not a Workshop control.", "element");
            }
        }

        public static VnWorkshopFocusValues GetFocusValues()
        {
            VnWorkshopFocusValues values = new VnWorkshopFocusValues();
            values.TwoCharacterOffset = TwoCharacterOffset;
            values.ActiveScale = ActiveFocusScale;
            values.InactiveScale = InactiveFocusScale;
            values.InactiveBrightness = InactiveBrightness;
            values.InactiveAlpha = InactiveAlpha;
            return values;
        }
    }
}
