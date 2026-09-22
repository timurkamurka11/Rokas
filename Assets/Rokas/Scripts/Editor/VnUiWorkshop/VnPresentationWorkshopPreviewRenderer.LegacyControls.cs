using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static partial class VnPresentationWorkshopPreviewRenderer
    {
        private static void DrawLegacyWorkshopControls(Rect localCanvas,VnWorkshopPreviewFrame frame,bool showHitRegions,
            VnWorkshopElement? uiFeedbackElement,VnWorkshopUiFeedbackSample? uiFeedbackSample)
        {
            bool replaceBack=uiFeedbackElement.HasValue&&uiFeedbackSample.HasValue&&ShouldReplaceIndependentUiFeedbackControl(uiFeedbackElement.Value,VnWorkshopElement.Back,uiFeedbackSample.Value);
            bool replaceNext=uiFeedbackElement.HasValue&&uiFeedbackSample.HasValue&&ShouldReplaceIndependentUiFeedbackControl(uiFeedbackElement.Value,VnWorkshopElement.Next,uiFeedbackSample.Value);
            if(!replaceBack) DrawText(LogicalToPreview(localCanvas,frame.Back,frame),"‹",frame.DialogueFont,34,FontStyle.Bold,TextAnchor.MiddleCenter);
            if(!replaceNext) DrawText(LogicalToPreview(localCanvas,frame.Next,frame),"›",frame.DialogueFont,34,FontStyle.Bold,TextAnchor.MiddleCenter);
            if(showHitRegions){DrawHitRegion(localCanvas,frame,frame.MuteHitRegion,"Mute — Baked into panel");DrawHitRegion(localCanvas,frame,frame.PauseHitRegion,"Pause — Baked into panel");DrawHitRegion(localCanvas,frame,frame.SkipHitRegion,"Skip — Baked into panel");}
            if(uiFeedbackElement.HasValue&&uiFeedbackSample.HasValue) DrawUiFeedbackPreview(localCanvas,frame,uiFeedbackElement.Value,uiFeedbackSample.Value);
        }
    }
}
