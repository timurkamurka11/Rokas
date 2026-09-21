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
            return Sample(project, fromScene, toScene,
                ResolveFirstBeat(fromScene), ResolveFirstBeat(toScene),
                sceneElapsedSeconds, sceneElapsedSeconds);
        }

        internal static VnSceneComposerTransitionSnapshot Sample(
            VnSceneComposerProject project,
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            VnSceneComposerDialogueBeat previousBeat,
            VnSceneComposerDialogueBeat activeBeat,
            float sceneElapsedSeconds,
            float beatElapsedSeconds,
            bool forceDialogueComplete = false)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (fromScene == null) throw new ArgumentNullException(nameof(fromScene));
            if (toScene == null) throw new ArgumentNullException(nameof(toScene));
            if (previousBeat == null) throw new ArgumentNullException(nameof(previousBeat));
            if (activeBeat == null) throw new ArgumentNullException(nameof(activeBeat));
            ValidateElapsed(sceneElapsedSeconds, nameof(sceneElapsedSeconds));
            ValidateElapsed(beatElapsedSeconds, nameof(beatElapsedSeconds));

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

            VnSceneComposerTransitionSnapshot result =
                VnSceneComposerTransitionSampler.Sample(
                    project, fromScene, toScene, previousBeat, activeBeat, 0f);
            result.background = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, previousBeat, activeBeat,
                Progress(sceneElapsedSeconds, background.Duration)).background;
            result.bounce = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, previousBeat, activeBeat,
                Progress(sceneElapsedSeconds, bounce.Duration)).bounce;
            result.stage = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, previousBeat, activeBeat,
                Progress(sceneElapsedSeconds, stage.RepositionDuration)).stage;
            result.characterMotions = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, previousBeat, activeBeat,
                Progress(sceneElapsedSeconds, character.Duration)).characterMotions;
            result.expressions = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, previousBeat, activeBeat,
                Progress(sceneElapsedSeconds, expression.Duration)).expressions;
            result.focus = VnSceneComposerTransitionSampler.Sample(
                project, fromScene, toScene, previousBeat, activeBeat,
                Progress(beatElapsedSeconds, focus.TransitionDuration)).focus;
            VnSceneComposerDialogueRevealSample dialogueReveal =
                VnSceneComposerDialogueReveal.Sample(
                    activeBeat.text ?? string.Empty,
                    beatElapsedSeconds,
                    typewriter,
                    forceDialogueComplete);
            result.dialogueReveal = dialogueReveal;
            result.visibleText = dialogueReveal.PlainVisibleText;

            result.beatEffectCharacterId = string.Empty;
            result.beatEffect = VnPresentationWorkshopVn10Resolver.SampleActionBounce(
                false, 1f, bounce);

            if (VnSceneComposerBeatCharacterStagingResolver.TryResolveAccent(
                    toScene, activeBeat, beatElapsedSeconds,
                    out string stagingAccentCharacter,
                    out float stagingAccentStrength,
                    out float stagingAccentDuration,
                    out float stagingAccentElapsed))
            {
                VnWorkshopActionBounceValues beatBounce = bounce;
                beatBounce.Amplitude = stagingAccentStrength;
                beatBounce.Duration = stagingAccentDuration;
                result.beatEffectCharacterId = stagingAccentCharacter;
                result.beatEffect = VnPresentationWorkshopVn10Resolver.SampleActionBounce(
                    true,
                    Progress(stagingAccentElapsed, beatBounce.Duration),
                    beatBounce);
            }
            else if (VnSceneComposerBeatCharacterStagingResolver.TryResolveHop(
                         toScene, activeBeat, beatElapsedSeconds,
                         out string stagingHopCharacter,
                         out float stagingHopStrength,
                         out float stagingHopDuration,
                         out float stagingHopElapsed))
            {
                result.beatEffectCharacterId = stagingHopCharacter;
                result.beatEffect = SampleHop(
                    stagingHopElapsed, stagingHopStrength, stagingHopDuration);
            }
            else if (activeBeat.effect == VnSceneComposerBeatEffect.Accent &&
                     !string.IsNullOrWhiteSpace(activeBeat.targetCharacterId))
            {
                VnWorkshopActionBounceValues beatBounce = bounce;
                beatBounce.Amplitude = Mathf.Max(0f, activeBeat.effectStrength);
                beatBounce.Duration = Mathf.Max(.01f, activeBeat.effectDuration);
                result.beatEffectCharacterId = activeBeat.targetCharacterId;
                result.beatEffect = VnPresentationWorkshopVn10Resolver.SampleActionBounce(
                    true,
                    Progress(beatElapsedSeconds, beatBounce.Duration),
                    beatBounce);
            }
            else if (activeBeat.effect == VnSceneComposerBeatEffect.Hop &&
                     !string.IsNullOrWhiteSpace(activeBeat.targetCharacterId))
            {
                result.beatEffectCharacterId = activeBeat.targetCharacterId;
                result.beatEffect = SampleHop(
                    beatElapsedSeconds,
                    Mathf.Max(0f, activeBeat.effectStrength),
                    Mathf.Max(.01f, activeBeat.effectDuration));
            }

            result.timing = VnSceneComposerTransitionSampler.ResolveTiming(project, toScene, activeBeat);
            return result;
        }

        internal static VnWorkshopActionBounceSample SampleHop(
            float elapsedSeconds, float strength, float duration)
        {
            float safeDuration = Mathf.Max(.01f, duration);
            float t = Mathf.Clamp01(Mathf.Max(0f, elapsedSeconds) / safeDuration);
            float height = Mathf.Max(0f, strength) * Mathf.Sin(Mathf.PI * t);
            return new VnWorkshopActionBounceSample
            {
                PositionOffset = new Vector2(0f, -height),
                ScaleMultiplier = 1f,
                Complete = t >= 1f
            };
        }

        private static void ValidateElapsed(float elapsedSeconds, string parameterName)
        {
            if (elapsedSeconds < 0f || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(parameterName,
                    "Elapsed time must be finite and non-negative.");
        }

        private static VnSceneComposerDialogueBeat ResolveFirstBeat(VnSceneComposerScene scene)
        {
            if (scene != null && scene.dialogueBeats != null && scene.dialogueBeats.Count > 0 &&
                scene.dialogueBeats[0] != null)
                return scene.dialogueBeats[0];

            return new VnSceneComposerDialogueBeat
            {
                beatId = string.Empty,
                speaker = string.Empty,
                text = string.Empty,
                narration = false
            };
        }

        private static float Progress(float elapsedSeconds, float durationSeconds)
        {
            return durationSeconds <= 0f ? 1f : Mathf.Clamp01(elapsedSeconds / durationSeconds);
        }
    }
}
