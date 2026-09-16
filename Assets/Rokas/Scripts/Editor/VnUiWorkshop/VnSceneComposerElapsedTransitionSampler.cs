using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal static class VnSceneComposerElapsedTransitionSampler
    {
        internal static VnSceneComposerTransitionSnapshot Sample(
            VnSceneComposerProject project,
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            float sceneElapsedSeconds)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (fromScene == null) throw new ArgumentNullException(nameof(fromScene));
            if (toScene == null) throw new ArgumentNullException(nameof(toScene));
            if (sceneElapsedSeconds < 0f || float.IsNaN(sceneElapsedSeconds) || float.IsInfinity(sceneElapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(sceneElapsedSeconds),
                    "Scene elapsed time must be finite and non-negative.");

            VnPresentationWorkshopPreset preset = VnSceneComposerComposition.ResolvePresentation(project, toScene);
            VnWorkshopBackgroundTransitionValues background =
                VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(preset);
            VnWorkshopActionBounceValues bounce =
                VnPresentationWorkshopVn10Resolver.ResolveActionBounce(preset);
            VnWorkshopStageLayoutValues stage =
                VnPresentationWorkshopVn10Resolver.ResolveStageLayout(preset);
            VnWorkshopCharacterTransitionValues character =
                VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(preset);
            VnWorkshopExpressionTransitionValues expression =
                VnPresentationWorkshopVn10Resolver.ResolveExpressionTransition(preset);
            VnWorkshopSpeakerFocusValues focus =
                VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(preset);
            VnWorkshopTypewriterValues typewriter =
                VnPresentationWorkshopVn10Resolver.ResolveTypewriter(preset);
            float typewriterDuration = VnPresentationWorkshopVn10Resolver.CalculateTypewriterDuration(
                toScene.previewText ?? string.Empty, typewriter);

            VnSceneComposerTransitionSnapshot result =
                VnSceneComposerTransitionSampler.Sample(project, fromScene, toScene, 0f);
            result.background = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, Progress(sceneElapsedSeconds, background.Duration)).background;
            result.bounce = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, Progress(sceneElapsedSeconds, bounce.Duration)).bounce;
            result.stage = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, Progress(sceneElapsedSeconds, stage.RepositionDuration)).stage;
            result.characterMotions = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, Progress(sceneElapsedSeconds, character.Duration)).characterMotions;
            result.expressions = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, Progress(sceneElapsedSeconds, expression.Duration)).expressions;
            result.focus = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, Progress(sceneElapsedSeconds, focus.TransitionDuration)).focus;
            result.visibleText = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, Progress(sceneElapsedSeconds, typewriterDuration)).visibleText;
            return result;
        }

        private static float Progress(float elapsedSeconds, float durationSeconds)
        {
            return durationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsedSeconds / durationSeconds);
        }
    }
}
