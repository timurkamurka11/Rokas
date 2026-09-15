using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static partial class VnPresentationWorkshopPreviewRenderer
    {
        public static Rect ResolveUiFeedbackLogicalRect(
            VnWorkshopPreviewFrame frame,
            VnWorkshopElement element,
            VnWorkshopUiFeedbackSample sample)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            Rect baseline = frame.GetElementRect(element);
            if (!sample.UsesIndependentTransform) return baseline;

            Vector2 center = baseline.center + sample.PositionOffset;
            Vector2 size = baseline.size * Mathf.Max(.01f, sample.ScaleMultiplier);
            return new Rect(center - size * .5f, size);
        }

        public static bool ShouldDrawUiFeedbackPreview(VnWorkshopUiFeedbackSample sample)
        {
            return !Mathf.Approximately(sample.ScaleMultiplier, 1f) ||
                   sample.PositionOffset.sqrMagnitude > .000001f ||
                   !Mathf.Approximately(sample.Brightness, 1f) ||
                   !Mathf.Approximately(sample.Alpha, 1f) ||
                   !Mathf.Approximately(sample.OverlayHighlight, 0f);
        }

        public static bool ShouldReplaceIndependentUiFeedbackControl(
            VnWorkshopElement feedbackElement,
            VnWorkshopElement renderedElement,
            VnWorkshopUiFeedbackSample sample)
        {
            return feedbackElement == renderedElement &&
                   (renderedElement == VnWorkshopElement.Back || renderedElement == VnWorkshopElement.Next) &&
                   ShouldDrawUiFeedbackPreview(sample);
        }

        public static void DrawUiFeedbackPreview(
            Rect previewRect,
            VnWorkshopPreviewFrame frame,
            VnWorkshopElement element,
            VnWorkshopUiFeedbackSample sample)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (!ShouldDrawUiFeedbackPreview(sample)) return;

            Rect canvasRect = FitAspect(previewRect, frame.ScreenSize.x / Mathf.Max(1f, frame.ScreenSize.y));
            Rect logical = ResolveUiFeedbackLogicalRect(frame, element, sample);
            Rect rect = LogicalToPreview(canvasRect, logical, frame);

            Color previous = GUI.color;
            try
            {
                float alpha = Mathf.Clamp01(sample.Alpha);
                float brightness = Mathf.Max(0f, sample.Brightness);
                float highlight = Mathf.Clamp01(sample.OverlayHighlight);

                if (element == VnWorkshopElement.Back || element == VnWorkshopElement.Next)
                {
                    if (highlight > 0f)
                    {
                        GUI.color = new Color(1f, 1f, 1f, highlight * .35f * alpha);
                        GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    }

                    GUI.color = new Color(brightness, brightness, brightness, alpha);
                    DrawText(
                        rect,
                        element == VnWorkshopElement.Back ? "‹" : "›",
                        frame.DialogueFont,
                        34,
                        FontStyle.Bold,
                        TextAnchor.MiddleCenter);
                    return;
                }

                float darkness = Mathf.Clamp01(1f - brightness);
                float lightness = Mathf.Clamp01(brightness - 1f);
                float alphaLoss = 1f - alpha;
                if (darkness > 0f || alphaLoss > 0f)
                {
                    GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(darkness * .7f + alphaLoss * .45f));
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                }
                if (lightness > 0f || highlight > 0f)
                {
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(lightness * .5f + highlight * .35f) * alpha);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                }
                DrawOutline(rect, 1f);
            }
            finally
            {
                GUI.color = previous;
            }
        }
    }
}
