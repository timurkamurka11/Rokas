using System;
using System.Collections.Generic;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnSceneComposerCharacterMotionPreview
    {
        public string characterId = string.Empty;
        public bool entering;
        public bool exiting;
        public VnWorkshopCharacterTransitionSample sample;
    }

    public sealed class VnSceneComposerExpressionPreview
    {
        public string characterId = string.Empty;
        public VnWorkshopExpressionTransitionSample sample;
    }

    public sealed class VnSceneComposerPreviewTimingPlan
    {
        public bool usesPreviewAutoDuration;
        public float previewAutoDuration;
        public float typewriterDuration;
        public float settleDuration;
        public float breathingRoom;
        public float sequenceGap;
    }

    public sealed class VnSceneComposerTransitionSnapshot
    {
        public VnWorkshopBackgroundTransitionSample background;
        public VnWorkshopActionBounceSample bounce;
        public VnWorkshopStageTransitionSample[] stage = Array.Empty<VnWorkshopStageTransitionSample>();
        public VnSceneComposerCharacterMotionPreview[] characterMotions = Array.Empty<VnSceneComposerCharacterMotionPreview>();
        public VnSceneComposerExpressionPreview[] expressions = Array.Empty<VnSceneComposerExpressionPreview>();
        public VnWorkshopSpeakerFocusSample[] focus = Array.Empty<VnWorkshopSpeakerFocusSample>();
        public string visibleText = string.Empty;
        public VnSceneComposerPreviewTimingPlan timing;
    }

    public static class VnSceneComposerTransitionSampler
    {
        public static VnSceneComposerTransitionSnapshot Sample(
            VnSceneComposerProject project,
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            float normalizedProgress)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (fromScene == null) throw new ArgumentNullException(nameof(fromScene));
            if (toScene == null) throw new ArgumentNullException(nameof(toScene));

            float progress = Mathf.Clamp01(normalizedProgress);
            VnPresentationWorkshopPreset preset = VnSceneComposerComposition.ResolvePresentation(project, toScene);

            VnWorkshopBackgroundTransitionValues backgroundValues =
                VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(preset);
            VnWorkshopActionBounceValues bounceValues =
                VnPresentationWorkshopVn10Resolver.ResolveActionBounce(preset);
            VnWorkshopStageLayoutValues stageValues =
                VnPresentationWorkshopVn10Resolver.ResolveStageLayout(preset);
            VnWorkshopCharacterTransitionValues characterValues =
                VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(preset);
            VnWorkshopExpressionTransitionValues expressionValues =
                VnPresentationWorkshopVn10Resolver.ResolveExpressionTransition(preset);
            VnWorkshopSpeakerFocusValues focusValues =
                VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(preset);
            VnWorkshopTypewriterValues typewriterValues =
                VnPresentationWorkshopVn10Resolver.ResolveTypewriter(preset);

            string text = toScene.previewText ?? string.Empty;
            float typewriterDuration = VnPresentationWorkshopVn10Resolver.CalculateTypewriterDuration(text, typewriterValues);
            int visibleCharacters = typewriterDuration <= 0f
                ? VnPresentationWorkshopVn10Resolver.InstantCompleteVisibleCharacters(text)
                : VnPresentationWorkshopVn10Resolver.CalculateTypewriterVisibleCharacters(
                    text, typewriterDuration * progress, typewriterValues);

            return new VnSceneComposerTransitionSnapshot
            {
                background = VnPresentationWorkshopVn10Resolver.SampleBackgroundTransition(
                    SnapProgress(progress, backgroundValues.Duration), backgroundValues),
                bounce = VnPresentationWorkshopVn10Resolver.SampleActionBounce(
                    toScene.transition != null && toScene.transition.triggerActionBounce,
                    SnapProgress(progress, bounceValues.Duration),
                    bounceValues),
                stage = SampleStage(CountCharacters(fromScene), CountCharacters(toScene), progress, stageValues),
                characterMotions = SampleCharacterMotions(fromScene, toScene, progress, characterValues),
                expressions = SampleExpressions(fromScene, toScene, progress, expressionValues),
                focus = SampleFocus(fromScene, toScene, progress, focusValues),
                visibleText = text.Substring(0, Mathf.Clamp(visibleCharacters, 0, text.Length)),
                timing = ResolveTiming(project, toScene)
            };
        }

        public static VnSceneComposerPreviewTimingPlan ResolveTiming(
            VnSceneComposerProject project,
            VnSceneComposerScene scene)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            VnPresentationWorkshopPreset preset = VnSceneComposerComposition.ResolvePresentation(project, scene);
            VnWorkshopTypewriterValues typewriter = VnPresentationWorkshopVn10Resolver.ResolveTypewriter(preset);
            VnWorkshopTimingValues timing = VnPresentationWorkshopVn10Resolver.ResolveTiming(preset);
            bool auto = scene.timing != null &&
                        scene.timing.previewAdvanceMode == VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration;

            return new VnSceneComposerPreviewTimingPlan
            {
                usesPreviewAutoDuration = auto,
                previewAutoDuration = auto ? Mathf.Max(0f, scene.timing.previewAutoDuration) : 0f,
                typewriterDuration = VnPresentationWorkshopVn10Resolver.CalculateTypewriterDuration(
                    scene.previewText ?? string.Empty, typewriter),
                settleDuration = Mathf.Max(0f, timing.MinimumBeatSettleDuration),
                breathingRoom = Mathf.Max(0f, timing.PostTransitionBreathingRoom),
                sequenceGap = Mathf.Max(0f, timing.AutoPreviewSequenceGap)
            };
        }

        private static VnWorkshopStageTransitionSample[] SampleStage(
            int fromCount,
            int toCount,
            float progress,
            VnWorkshopStageLayoutValues values)
        {
            if (fromCount == 0 && toCount == 0)
                return Array.Empty<VnWorkshopStageTransitionSample>();

            if (fromCount > 0 && toCount > 0)
                return VnPresentationWorkshopVn10Resolver.SampleStageTransition(fromCount, toCount, progress, values);

            bool snap = values.RepositionDuration <= 0f;
            float raw = snap ? 1f : Mathf.Clamp01(progress);
            int count = Math.Max(fromCount, toCount);
            VnWorkshopStageTarget[] targets = VnPresentationWorkshopVn10Resolver.ResolveStageTargets(count, values);
            var result = new VnWorkshopStageTransitionSample[count];
            bool entering = fromCount == 0;
            for (int i = 0; i < count; i++)
            {
                VnWorkshopStageTarget target = targets[i];
                result[i] = new VnWorkshopStageTransitionSample
                {
                    CharacterIndex = i,
                    Slot = target.Slot,
                    Position = target.Position,
                    Scale = target.Scale,
                    Visible = entering ? raw > 0f : raw < 1f,
                    Entering = entering,
                    Exiting = !entering,
                    Complete = snap || raw >= 1f
                };
            }
            return result;
        }

        private static VnSceneComposerCharacterMotionPreview[] SampleCharacterMotions(
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            float progress,
            VnWorkshopCharacterTransitionValues values)
        {
            var result = new List<VnSceneComposerCharacterMotionPreview>();
            List<VnSceneComposerCharacter> from = fromScene.characters ?? new List<VnSceneComposerCharacter>();
            List<VnSceneComposerCharacter> to = toScene.characters ?? new List<VnSceneComposerCharacter>();
            float sampledProgress = SnapProgress(progress, values.Duration);

            for (int i = 0; i < to.Count; i++)
            {
                VnSceneComposerCharacter character = to[i];
                string id = ResolveCharacterId(character);
                if (FindCharacter(from, id) >= 0) continue;
                result.Add(new VnSceneComposerCharacterMotionPreview
                {
                    characterId = id,
                    entering = true,
                    sample = VnPresentationWorkshopVn10Resolver.SampleCharacterEnter(sampledProgress, values)
                });
            }

            for (int i = 0; i < from.Count; i++)
            {
                VnSceneComposerCharacter character = from[i];
                string id = ResolveCharacterId(character);
                if (FindCharacter(to, id) >= 0) continue;
                result.Add(new VnSceneComposerCharacterMotionPreview
                {
                    characterId = id,
                    exiting = true,
                    sample = VnPresentationWorkshopVn10Resolver.SampleCharacterExit(sampledProgress, values)
                });
            }
            return result.ToArray();
        }

        private static VnSceneComposerExpressionPreview[] SampleExpressions(
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            float progress,
            VnWorkshopExpressionTransitionValues values)
        {
            var result = new List<VnSceneComposerExpressionPreview>();
            List<VnSceneComposerCharacter> from = fromScene.characters ?? new List<VnSceneComposerCharacter>();
            List<VnSceneComposerCharacter> to = toScene.characters ?? new List<VnSceneComposerCharacter>();
            float sampledProgress = SnapProgress(progress, values.Duration);

            for (int i = 0; i < to.Count; i++)
            {
                VnSceneComposerCharacter target = to[i];
                string id = ResolveCharacterId(target);
                int previousIndex = FindCharacter(from, id);
                if (previousIndex < 0) continue;
                VnSceneComposerCharacter previous = from[previousIndex];
                if (string.Equals(previous.stateId, target.stateId, StringComparison.Ordinal)) continue;
                result.Add(new VnSceneComposerExpressionPreview
                {
                    characterId = id,
                    sample = VnPresentationWorkshopVn10Resolver.SampleExpressionTransition(
                        previous.stateId, target.stateId, sampledProgress, values)
                });
            }
            return result.ToArray();
        }

        private static VnWorkshopSpeakerFocusSample[] SampleFocus(
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            float progress,
            VnWorkshopSpeakerFocusValues values)
        {
            int count = CountCharacters(toScene);
            if (count == 0) return Array.Empty<VnWorkshopSpeakerFocusSample>();
            if (count == 1)
            {
                return new[]
                {
                    VnPresentationWorkshopVn10Resolver.SampleSpeakerFocus(1, 0, 0, 0, progress, values)
                };
            }

            int previous = FindSpeakerIndex(toScene, fromScene.speaker);
            int active = FindSpeakerIndex(toScene, toScene.speaker);
            if (previous < 0 && active < 0)
            {
                var neutral = new VnWorkshopSpeakerFocusSample[count];
                for (int i = 0; i < count; i++)
                {
                    neutral[i] = new VnWorkshopSpeakerFocusSample
                    {
                        PositionOffset = Vector2.zero,
                        Scale = 1f,
                        Brightness = 1f,
                        Alpha = 1f,
                        Complete = true
                    };
                }
                return neutral;
            }
            if (previous < 0) previous = active;
            if (active < 0) active = previous;

            var result = new VnWorkshopSpeakerFocusSample[count];
            for (int i = 0; i < count; i++)
                result[i] = VnPresentationWorkshopVn10Resolver.SampleSpeakerFocus(
                    count, previous, active, i, progress, values);
            return result;
        }

        private static int FindSpeakerIndex(VnSceneComposerScene scene, string speaker)
        {
            if (scene == null || scene.characters == null || string.IsNullOrEmpty(speaker)) return -1;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                string id = ResolveCharacterId(scene.characters[i]);
                if (string.Equals(id, speaker, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        private static int FindCharacter(List<VnSceneComposerCharacter> characters, string characterId)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (string.Equals(ResolveCharacterId(characters[i]), characterId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string ResolveCharacterId(VnSceneComposerCharacter character)
        {
            if (character == null) return string.Empty;
            if (!string.IsNullOrEmpty(character.characterId)) return character.characterId;
            if (!string.IsNullOrEmpty(character.stateId) &&
                VnCharacterVisualCatalog.TryResolve(character.stateId, out VnCharacterVisualState state))
                return state.Character;
            return string.Empty;
        }

        private static int CountCharacters(VnSceneComposerScene scene)
        {
            int count = scene != null && scene.characters != null ? scene.characters.Count : 0;
            if (count < 0 || count > 3)
                throw new ArgumentOutOfRangeException(nameof(scene), count,
                    "Scene Composer supports zero to three visible authored characters.");
            return count;
        }

        private static float SnapProgress(float progress, float duration)
        {
            return duration <= 0f ? 1f : Mathf.Clamp01(progress);
        }
    }
}
