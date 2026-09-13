using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnPresentationWorkshopEditing
    {
        private const float MinScale = .05f;
        private const float MaxScale = 5f;
        private const float Epsilon = .0001f;

        public static void ApplyDrag(VnPresentationWorkshopPreset preset, VnWorkshopElement element,
            Vector2 logicalDelta)
        {
            RequirePreset(preset);
            RequireFinite(logicalDelta, "logicalDelta");
            VnWorkshopElementOverride elementOverride = preset.GetElementOverride(element);
            Vector2 current = elementOverride.hasPositionDelta ? elementOverride.positionDelta : Vector2.zero;
            SetPositionDelta(preset, element, current + logicalDelta);
        }

        public static void Nudge(VnPresentationWorkshopPreset preset, VnWorkshopElement element,
            Vector2 logicalDelta)
        {
            ApplyDrag(preset, element, logicalDelta);
        }

        public static void SetPositionDelta(VnPresentationWorkshopPreset preset, VnWorkshopElement element,
            Vector2 delta)
        {
            RequirePreset(preset);
            RequireFinite(delta, "delta");
            VnWorkshopElementOverride elementOverride = preset.GetElementOverride(element);
            elementOverride.positionDelta = delta;
            elementOverride.hasPositionDelta = delta.sqrMagnitude > Epsilon * Epsilon;
            if (!elementOverride.hasPositionDelta) elementOverride.positionDelta = Vector2.zero;
        }

        public static void SetSizeDelta(VnPresentationWorkshopPreset preset, VnWorkshopElement element,
            Vector2 delta)
        {
            RequirePreset(preset);
            RequireFinite(delta, "delta");
            VnWorkshopElementOverride elementOverride = preset.GetElementOverride(element);
            elementOverride.sizeDelta = delta;
            elementOverride.hasSizeDelta = delta.sqrMagnitude > Epsilon * Epsilon;
            if (!elementOverride.hasSizeDelta) elementOverride.sizeDelta = Vector2.zero;
        }

        public static void SetScaleMultiplier(VnPresentationWorkshopPreset preset, VnWorkshopElement element,
            float multiplier)
        {
            RequirePreset(preset);
            RequireFinite(multiplier, "multiplier");
            if (multiplier < MinScale || multiplier > MaxScale)
                throw new ArgumentOutOfRangeException("multiplier", "Workshop scale must stay between 0.05 and 5.0.");

            VnWorkshopElementOverride elementOverride = preset.GetElementOverride(element);
            elementOverride.scaleMultiplier = multiplier;
            elementOverride.hasScaleMultiplier = !Mathf.Approximately(multiplier, 1f);
            if (!elementOverride.hasScaleMultiplier) elementOverride.scaleMultiplier = 1f;
        }

        private static void RequirePreset(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException("preset");
        }

        private static void RequireFinite(Vector2 value, string name)
        {
            RequireFinite(value.x, name + ".x");
            RequireFinite(value.y, name + ".y");
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Workshop value must be finite: " + name, name);
        }
    }
}
