using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [NonSerialized] private VnPresentationWorkshopPreset _sceneComposerFocusedPreviewPreset;

        public void ComposerPreviewFocusedEffect(VnWorkshopPreviewEffect effect)
        {
            if (!_sceneComposerWorkspaceActive)
                throw new InvalidOperationException("Focused Scene Composer preview requires the Scene Composer workspace.");

            VnSceneComposerScene scene = RequireSelectedScene();
            _sceneComposerFocusedPreviewPreset = VnSceneComposerComposition.ResolvePresentation(_sceneComposerProject, scene);
            StartPreviewEffect(effect, ResolveSceneComposerFocusedPreviewDuration(effect, _sceneComposerFocusedPreviewPreset));
        }

        private float ResolveSceneComposerFocusedPreviewDuration(
            VnWorkshopPreviewEffect effect,
            VnPresentationWorkshopPreset preset)
        {
            switch (effect)
            {
                case VnWorkshopPreviewEffect.Expression:
                    return Mathf.Max(.01f, VnPresentationWorkshopVn10Resolver.ResolveExpressionTransition(preset).Duration);
                case VnWorkshopPreviewEffect.CharacterEnter:
                case VnWorkshopPreviewEffect.CharacterExit:
                    return Mathf.Max(.01f, VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(preset).Duration);
                case VnWorkshopPreviewEffect.Bounce:
                    return Mathf.Max(.01f, VnPresentationWorkshopVn10Resolver.ResolveActionBounce(preset).Duration);
                case VnWorkshopPreviewEffect.BackgroundTransition:
                    return Mathf.Max(.01f, VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(preset).Duration);
                case VnWorkshopPreviewEffect.StageOneTwo:
                case VnWorkshopPreviewEffect.StageTwoThree:
                case VnWorkshopPreviewEffect.StageThreeTwo:
                case VnWorkshopPreviewEffect.StageTwoOne:
                    return Mathf.Max(.01f, VnPresentationWorkshopVn10Resolver.ResolveStageLayout(preset).RepositionDuration);
                case VnWorkshopPreviewEffect.SpeakerSwitch:
                    return Mathf.Max(.01f, VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(preset).TransitionDuration);
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect,
                        "This effect is not supported by the focused Scene Composer animation preview.");
            }
        }

        private VnPresentationWorkshopPreset ResolveMotionPreviewPreset()
        {
            if (_sceneComposerWorkspaceActive && _sceneComposerFocusedPreviewPreset != null)
                return JsonUtility.FromJson<VnPresentationWorkshopPreset>(JsonUtility.ToJson(_sceneComposerFocusedPreviewPreset))
                    ?? new VnPresentationWorkshopPreset();

            return comparisonView == VnWorkshopComparisonView.Original
                ? new VnPresentationWorkshopPreset()
                : JsonUtility.FromJson<VnPresentationWorkshopPreset>(JsonUtility.ToJson(CurrentPreset))
                    ?? new VnPresentationWorkshopPreset();
        }

        private bool IsSceneComposerFocusedPreviewActive()
        {
            UpdatePreviewClock();
            if (!_sceneComposerWorkspaceActive || _sceneComposerFocusedPreviewPreset == null) return false;
            if (previewEffect == VnWorkshopPreviewEffect.None || previewEffect == VnWorkshopPreviewEffect.UiPress) return false;
            return previewPlaying || previewPaused || previewProgress < 1f;
        }

        private void CancelSceneComposerFocusedPreview()
        {
            if (_sceneComposerFocusedPreviewPreset == null) return;
            _sceneComposerFocusedPreviewPreset = null;
            previewEffect = VnWorkshopPreviewEffect.None;
            previewPlaying = false;
            previewPaused = false;
            previewProgress = 0f;
        }

        private void CompleteSceneComposerFocusedPreview()
        {
            if (!_sceneComposerWorkspaceActive || _sceneComposerFocusedPreviewPreset == null) return;
            if (previewPlaying || previewPaused || previewProgress < 1f) return;
            _sceneComposerFocusedPreviewPreset = null;
        }
    }
}
