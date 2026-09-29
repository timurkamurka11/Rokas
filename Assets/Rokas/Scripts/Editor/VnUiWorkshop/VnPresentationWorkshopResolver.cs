using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnPresentationWorkshopResolver
    {
        public static Vector2 CalculateVirtualCanvasSize(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0) throw new ArgumentOutOfRangeException("screenWidth");
            if (screenHeight <= 0) throw new ArgumentOutOfRangeException("screenHeight");

            Vector2 reference = VnPresentationWorkshopBaseline.ReferenceResolution;
            float logWidth = Mathf.Log(screenWidth / reference.x, 2f);
            float logHeight = Mathf.Log(screenHeight / reference.y, 2f);
            float logScale = Mathf.Lerp(logWidth, logHeight,
                VnPresentationWorkshopBaseline.CanvasMatchWidthOrHeight);
            float scaleFactor = Mathf.Pow(2f, logScale);
            return new Vector2(screenWidth / scaleFactor, screenHeight / scaleFactor);
        }

        public static Vector2 GetScreenSize(VnWorkshopResolution resolution)
        {
            switch (resolution)
            {
                case VnWorkshopResolution.Reference1920x1080: return new Vector2(1920f, 1080f);
                case VnWorkshopResolution.Wide1280x720: return new Vector2(1280f, 720f);
                case VnWorkshopResolution.FourThree1024x768: return new Vector2(1024f, 768f);
                default: throw new ArgumentOutOfRangeException("resolution", resolution, null);
            }
        }

        public static VnWorkshopFocusValues ResolveFocus(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException("preset");
            VnWorkshopFocusValues values = VnPresentationWorkshopBaseline.GetFocusValues();
            VnWorkshopFocusOverride focus = preset.focus;
            if (focus.hasTwoCharacterOffset) values.TwoCharacterOffset = focus.twoCharacterOffset;
            if (focus.hasActiveScale) values.ActiveScale = focus.activeScale;
            if (focus.hasInactiveScale) values.InactiveScale = focus.inactiveScale;
            if (focus.hasInactiveBrightness) values.InactiveBrightness = focus.inactiveBrightness;
            if (focus.hasInactiveAlpha) values.InactiveAlpha = focus.inactiveAlpha;
            return values;
        }
    }
}
