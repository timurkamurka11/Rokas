using System;
using Rokas.Presentation;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class RokasVnRuntimeIntroExporter
    {
        public const string ProjectId = RokasVnRuntimeIntroPackage.ExpectedProjectId;

        public static RokasVnRuntimeIntroSnapshot BuildSnapshot(
            VnSceneComposerProject project,
            string sourceProjectSha256)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (!string.Equals(project.projectId, ProjectId, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Runtime intro export requires the authoritative authored Project ID.",
                    nameof(project));
            if (string.IsNullOrWhiteSpace(sourceProjectSha256))
                throw new ArgumentException(
                    "Source project SHA256 is required for deterministic runtime export.",
                    nameof(sourceProjectSha256));

            var result = new RokasVnRuntimeIntroSnapshot
            {
                projectId = project.projectId ?? string.Empty,
                title = project.title ?? string.Empty,
                sourceProjectSha256 = sourceProjectSha256,
                sceneCount = 0,
                beatCount = 0
            };

            if (project.scenes == null) return result;

            for (int sceneIndex = 0; sceneIndex < project.scenes.Count; sceneIndex++)
            {
                VnSceneComposerScene scene = project.scenes[sceneIndex];
                if (scene == null) continue;

                var mapped = new RokasVnRuntimeSceneSnapshot
                {
                    sceneId = scene.sceneId ?? string.Empty,
                    label = scene.label ?? string.Empty,
                    media = MapMedia(scene.media),
                    music = MapMusic(scene.music),
                    keepPreviousAdditionalAudio = scene.keepPreviousAdditionalAudio,
                    sceneTransitionType = scene.transition != null
                        ? (int)scene.transition.sceneTransitionType : 0,
                    sceneTransitionDirection = scene.transition != null
                        ? (int)scene.transition.sceneTransitionDirection : 0,
                    sceneTransitionDuration = scene.transition != null
                        ? scene.transition.sceneTransitionDuration : .7f,
                    triggerActionBounce = scene.transition != null &&
                        scene.transition.triggerActionBounce,
                    isTerminal = scene.isTerminal,
                    terminalFadeDuration = scene.terminalFadeDuration
                };

                if (scene.additionalAudioCues != null)
                {
                    for (int i = 0; i < scene.additionalAudioCues.Count; i++)
                    {
                        VnSceneComposerAdditionalAudioCue cue = scene.additionalAudioCues[i];
                        if (cue != null) mapped.additionalAudioCues.Add(MapAudioCue(cue));
                    }
                }

                if (scene.characters != null)
                {
                    for (int i = 0; i < scene.characters.Count; i++)
                    {
                        VnSceneComposerCharacter character = scene.characters[i];
                        if (character != null) mapped.characters.Add(MapCharacter(character));
                    }
                }

                if (scene.dialogueBeats != null)
                {
                    for (int i = 0; i < scene.dialogueBeats.Count; i++)
                    {
                        VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                        if (beat == null) continue;
                        mapped.dialogueBeats.Add(MapBeat(beat));
                        result.beatCount++;
                    }
                }

                result.scenes.Add(mapped);
                result.sceneCount++;
            }

            return result;
        }

        private static RokasVnRuntimeMediaSnapshot MapMedia(VnSceneComposerMediaReference media)
        {
            media = media ?? new VnSceneComposerMediaReference();
            return new RokasVnRuntimeMediaSnapshot
            {
                kind = (int)media.kind,
                reference = media.reference ?? string.Empty,
                displayName = media.displayName ?? string.Empty,
                contentHash = media.contentHash ?? string.Empty,
                scaleMode = (int)media.scaleMode,
                loop = media.loop
            };
        }

        private static RokasVnRuntimeMusicSnapshot MapMusic(VnSceneComposerMusic music)
        {
            music = music ?? new VnSceneComposerMusic();
            return new RokasVnRuntimeMusicSnapshot
            {
                mode = (int)music.mode,
                assetGuid = music.assetGuid ?? string.Empty,
                displayName = music.displayName ?? string.Empty,
                volume = music.volume,
                loop = music.loop,
                fadeInSeconds = music.fadeInSeconds,
                fadeOutSeconds = music.fadeOutSeconds
            };
        }

        private static RokasVnRuntimeAudioCueSnapshot MapAudioCue(
            VnSceneComposerAdditionalAudioCue cue)
        {
            return new RokasVnRuntimeAudioCueSnapshot
            {
                cueId = cue.cueId ?? string.Empty,
                displayName = cue.displayName ?? string.Empty,
                assetGuid = cue.assetGuid ?? string.Empty,
                enabled = cue.enabled,
                category = (int)cue.category,
                volume = cue.volume,
                loop = cue.loop,
                trigger = (int)cue.trigger,
                startBeatId = cue.startBeatId ?? string.Empty,
                startDelaySeconds = cue.startDelaySeconds,
                fadeInSeconds = cue.fadeInSeconds,
                fadeOutSeconds = cue.fadeOutSeconds,
                stopMode = (int)cue.stopMode,
                stopBeatId = cue.stopBeatId ?? string.Empty
            };
        }

        private static RokasVnRuntimeCharacterSnapshot MapCharacter(
            VnSceneComposerCharacter character)
        {
            return new RokasVnRuntimeCharacterSnapshot
            {
                characterId = character.characterId ?? string.Empty,
                stateId = character.stateId ?? string.Empty,
                stageSlot = (int)character.stageSlot,
                hasPositionOffset = character.hasPositionOffset,
                positionOffset = character.positionOffset,
                hasScaleMultiplier = character.hasScaleMultiplier,
                scaleMultiplier = character.scaleMultiplier
            };
        }

        private static RokasVnRuntimeBeatSnapshot MapBeat(VnSceneComposerDialogueBeat beat)
        {
            var mapped = new RokasVnRuntimeBeatSnapshot
            {
                beatId = beat.beatId ?? string.Empty,
                speaker = beat.speaker ?? string.Empty,
                text = beat.text ?? string.Empty,
                narration = beat.narration,
                targetCharacterId = beat.targetCharacterId ?? string.Empty,
                hasStateOverride = beat.hasStateOverride,
                stateId = beat.stateId ?? string.Empty,
                effect = (int)beat.effect,
                effectStrength = beat.effectStrength,
                effectDuration = beat.effectDuration,
                replicaEffect = MapReplicaEffect(beat.replicaEffect),
                movement = MapMovement(beat.movement)
            };

            if (beat.characterStaging != null)
            {
                for (int i = 0; i < beat.characterStaging.Count; i++)
                {
                    VnSceneComposerBeatCharacterStaging row = beat.characterStaging[i];
                    if (row != null) mapped.characterStaging.Add(MapStaging(row));
                }
            }
            return mapped;
        }

        private static RokasVnRuntimeReplicaEffectSnapshot MapReplicaEffect(
            VnSceneComposerReplicaEffect effect)
        {
            effect = effect ?? new VnSceneComposerReplicaEffect();
            return new RokasVnRuntimeReplicaEffectSnapshot
            {
                type = (int)effect.type,
                intensity = effect.intensity,
                duration = effect.duration,
                frequency = effect.frequency,
                decay = effect.decay,
                direction = effect.direction,
                flashColor = effect.flashColor
            };
        }

        private static RokasVnRuntimeMovementSnapshot MapMovement(
            VnSceneComposerBeatMovement movement)
        {
            movement = movement ?? new VnSceneComposerBeatMovement();
            return new RokasVnRuntimeMovementSnapshot
            {
                primary = MapMovementAction(movement.primary),
                secondary = MapMovementAction(movement.secondary),
                secondaryTiming = (int)movement.secondaryTiming
            };
        }

        private static RokasVnRuntimeMovementActionSnapshot MapMovementAction(
            VnSceneComposerCharacterMovementAction action)
        {
            action = action ?? new VnSceneComposerCharacterMovementAction();
            return new RokasVnRuntimeMovementActionSnapshot
            {
                characterId = action.characterId ?? string.Empty,
                action = (int)action.action,
                duration = action.duration
            };
        }

        private static RokasVnRuntimeStagingSnapshot MapStaging(
            VnSceneComposerBeatCharacterStaging row)
        {
            return new RokasVnRuntimeStagingSnapshot
            {
                stagingId = row.stagingId ?? string.Empty,
                characterId = row.characterId ?? string.Empty,
                visibility = (int)row.visibility,
                position = (int)row.position,
                customPositionOffset = row.customPositionOffset,
                hasStateOverride = row.hasStateOverride,
                stateId = row.stateId ?? string.Empty,
                effect = (int)row.effect,
                effectStrength = row.effectStrength,
                effectDuration = row.effectDuration,
                delaySeconds = row.delaySeconds
            };
        }
    }
}
