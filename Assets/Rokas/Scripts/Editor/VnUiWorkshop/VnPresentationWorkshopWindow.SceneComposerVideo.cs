using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        public VnSceneComposerPlaybackFrame ComposerBuildSelectedPreviewPlaybackFrame()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            int index = FindSceneIndex(scene.sceneId);
            Texture media = ComposerGetSceneThumbnail(index);
            if (scene.media != null && scene.media.kind == VnSceneComposerMediaKind.ExternalVideo)
            {
                VnSceneComposerVideoPreview video = GetSelectedComposerVideoPreview();
                if (video == null || !video.HasVisibleFrame) media = null;
            }
            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                _sceneComposerProject, scene, previewResolution, media as Texture2D);
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
            return new VnSceneComposerPlaybackFrame(frame, sample, visual, visual);
        }
    }
}
