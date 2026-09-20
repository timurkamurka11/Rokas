using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        public VnSceneComposerPlaybackFrame ComposerBuildSelectedPreviewPlaybackFrame()
        {
            if (IsSceneComposerFocusedPreviewActive())
            {
                VnSceneComposerPlaybackFrame focused = CurrentMotionPreviewFrame;
                if (focused != null) return focused;
            }

            VnSceneComposerScene scene = RequireSelectedScene();
            int index = FindSceneIndex(scene.sceneId);
            Texture media = ComposerGetSceneThumbnail(index);
            if (scene.media != null && scene.media.kind == VnSceneComposerMediaKind.ExternalVideo)
            {
                VnSceneComposerVideoPreview video = GetSelectedComposerVideoPreview();
                if (video == null || !video.HasVisibleFrame) media = null;
            }
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                _sceneComposerProject, scene, beat, previewResolution, media as Texture2D);
            Texture visual = media != null ? media : frame.BackgroundTexture;
            var sample = new VnWorkshopBackgroundTransitionSample
            {
                SourceAlpha = 0f,
                TargetAlpha = 1f,
                CurtainCoverage = 0f,
                CurtainPosition = 1f,
                CurtainDarkness = 0f,
                CurtainDirection = VnWorkshopCurtainDirection.LeftToRight,
                Complete = true
            };
            VnSceneComposerMediaScaleMode scaleMode = scene.media != null
                ? scene.media.scaleMode : VnSceneComposerMediaScaleMode.Fit;
            return new VnSceneComposerPlaybackFrame(frame, sample, visual, visual, scaleMode, scaleMode);
        }
    }
}
