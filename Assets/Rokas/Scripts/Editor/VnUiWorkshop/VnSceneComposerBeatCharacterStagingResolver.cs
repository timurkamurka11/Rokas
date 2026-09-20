using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnSceneComposerResolvedBeatCharacterStaging
    {
        public string CharacterId { get; internal set; } = string.Empty;
        public bool Visible { get; internal set; } = true;
        public string StateId { get; internal set; } = string.Empty;
        public VnWorkshopStageSlot StageSlot { get; internal set; } = VnWorkshopStageSlot.Center;
        public bool HasPositionOffset { get; internal set; }
        public Vector2 PositionOffset { get; internal set; }
        public string[] Warnings { get; internal set; } = Array.Empty<string>();
    }

    public static class VnSceneComposerBeatCharacterStagingResolver
    {
        public static VnSceneComposerResolvedBeatCharacterStaging Resolve(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            string characterId,
            float beatElapsedSeconds)
        {
            return Resolve(scene, beat, characterId, beatElapsedSeconds, null);
        }

        internal static VnSceneComposerResolvedBeatCharacterStaging Resolve(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            string characterId,
            float beatElapsedSeconds,
            ISet<string> cancelledStagingIds)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (beat == null) throw new ArgumentNullException(nameof(beat));
            if (string.IsNullOrWhiteSpace(characterId))
                throw new ArgumentException("Character identity is required.", nameof(characterId));
            if (beatElapsedSeconds < 0f || float.IsNaN(beatElapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(beatElapsedSeconds));

            VnSceneComposerCharacter authored = FindAuthoredCharacter(scene, characterId);
            if (authored == null)
                throw new ArgumentException(
                    "Character '" + characterId + "' is not part of the current Scene roster.",
                    nameof(characterId));

            int beatIndex = FindBeatIndex(scene, beat);
            if (beatIndex < 0)
                throw new ArgumentException("Dialogue Beat is not part of the current Scene.", nameof(beat));

            var warnings = new List<string>();
            var resolved = new VnSceneComposerResolvedBeatCharacterStaging
            {
                CharacterId = characterId,
                Visible = ResolveInitialVisibility(scene, characterId),
                StateId = authored.stateId ?? string.Empty,
                StageSlot = authored.stageSlot,
                HasPositionOffset = authored.hasPositionOffset,
                PositionOffset = authored.positionOffset
            };

            ValidateOrPreserveState(resolved, characterId, resolved.StateId, "Scene base", warnings);

            for (int i = 0; i <= beatIndex; i++)
            {
                VnSceneComposerDialogueBeat current = scene.dialogueBeats[i];
                if (current == null) continue;

                ApplyLegacyM_F1State(current, characterId, resolved, warnings);

                if (current.characterStaging == null) continue;
                for (int r = 0; r < current.characterStaging.Count; r++)
                {
                    VnSceneComposerBeatCharacterStaging staging = current.characterStaging[r];
                    if (staging == null ||
                        !string.Equals(staging.characterId ?? string.Empty, characterId,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (cancelledStagingIds != null &&
                        !string.IsNullOrEmpty(staging.stagingId) &&
                        cancelledStagingIds.Contains(staging.stagingId))
                        continue;

                    bool targetBeat = i == beatIndex;
                    if (targetBeat && staging.delaySeconds > beatElapsedSeconds + .00001f)
                        continue;

                    ApplyStaging(staging, characterId, resolved, warnings);
                }
            }

            resolved.Warnings = warnings.ToArray();
            return resolved;
        }

        internal static string[] CollectWarnings(VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            var warnings = new List<string>();
            if (scene == null || beat == null || scene.dialogueBeats == null) return warnings.ToArray();
            int beatIndex = FindBeatIndex(scene, beat);
            if (beatIndex < 0) return warnings.ToArray();

            for (int i = 0; i <= beatIndex; i++)
            {
                VnSceneComposerDialogueBeat current = scene.dialogueBeats[i];
                if (current == null || current.characterStaging == null) continue;
                for (int r = 0; r < current.characterStaging.Count; r++)
                {
                    VnSceneComposerBeatCharacterStaging staging = current.characterStaging[r];
                    if (staging == null) continue;
                    string characterId = staging.characterId ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(characterId))
                    {
                        AddWarning(warnings, "Character staging row has no character.");
                        continue;
                    }

                    VnSceneComposerCharacter character = FindAuthoredCharacter(scene, characterId);
                    if (character == null)
                    {
                        AddWarning(warnings,
                            "Character staging references missing Scene character '" + characterId + "'.");
                        continue;
                    }

                    if (staging.hasStateOverride)
                    {
                        if (!VnSceneComposerCharacterStateResolver.TryResolve(
                                staging.stateId, out VnSceneComposerResolvedCharacterState state))
                        {
                            AddWarning(warnings,
                                "Character staging state '" + (staging.stateId ?? string.Empty) +
                                "' is missing for '" + characterId + "'.");
                        }
                        else if (!string.Equals(
                                     state.Character, characterId, StringComparison.OrdinalIgnoreCase))
                        {
                            AddWarning(warnings,
                                "Character staging state '" + staging.stateId + "' belongs to '" +
                                state.Character + "', not '" + characterId + "'.");
                        }
                    }
                }
            }

            return warnings.ToArray();
        }

        internal static void CollectPendingStagingIds(
            VnSceneComposerDialogueBeat beat,
            float beatElapsedSeconds,
            ISet<string> destination)
        {
            if (beat == null || destination == null || beat.characterStaging == null) return;
            for (int i = 0; i < beat.characterStaging.Count; i++)
            {
                VnSceneComposerBeatCharacterStaging staging = beat.characterStaging[i];
                if (staging == null || string.IsNullOrEmpty(staging.stagingId)) continue;
                if (staging.delaySeconds > beatElapsedSeconds + .00001f)
                    destination.Add(staging.stagingId);
            }
        }

        internal static bool TryResolveAccent(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            float beatElapsedSeconds,
            out string characterId,
            out float strength,
            out float duration,
            out float elapsedSinceTrigger)
        {
            characterId = string.Empty;
            strength = 0f;
            duration = 0f;
            elapsedSinceTrigger = 0f;
            if (scene == null || beat == null || beat.characterStaging == null) return false;

            for (int i = 0; i < beat.characterStaging.Count; i++)
            {
                VnSceneComposerBeatCharacterStaging staging = beat.characterStaging[i];
                if (staging == null || staging.effect != VnSceneComposerBeatEffect.Accent ||
                    staging.delaySeconds > beatElapsedSeconds + .00001f)
                    continue;
                if (FindAuthoredCharacter(scene, staging.characterId) == null) continue;

                characterId = staging.characterId ?? string.Empty;
                strength = Mathf.Max(0f, staging.effectStrength);
                duration = Mathf.Clamp(staging.effectDuration, .01f, 10f);
                elapsedSinceTrigger = Mathf.Max(0f, beatElapsedSeconds - staging.delaySeconds);
                return !string.IsNullOrWhiteSpace(characterId);
            }
            return false;
        }

        private static void ApplyLegacyM_F1State(
            VnSceneComposerDialogueBeat beat,
            string characterId,
            VnSceneComposerResolvedBeatCharacterStaging resolved,
            List<string> warnings)
        {
            if (!beat.hasStateOverride ||
                !string.Equals(beat.targetCharacterId ?? string.Empty, characterId,
                    StringComparison.OrdinalIgnoreCase))
                return;

            TryApplyState(resolved, characterId, beat.stateId, "Dialogue Beat M-F1", warnings);
        }

        private static void ApplyStaging(
            VnSceneComposerBeatCharacterStaging staging,
            string characterId,
            VnSceneComposerResolvedBeatCharacterStaging resolved,
            List<string> warnings)
        {
            switch (staging.visibility)
            {
                case VnSceneComposerBeatCharacterVisibility.Show:
                    resolved.Visible = true;
                    break;
                case VnSceneComposerBeatCharacterVisibility.Hide:
                    resolved.Visible = false;
                    break;
            }

            switch (staging.position)
            {
                case VnSceneComposerBeatCharacterPosition.Left:
                    resolved.StageSlot = VnWorkshopStageSlot.Left;
                    resolved.HasPositionOffset = false;
                    resolved.PositionOffset = Vector2.zero;
                    break;
                case VnSceneComposerBeatCharacterPosition.Center:
                    resolved.StageSlot = VnWorkshopStageSlot.Center;
                    resolved.HasPositionOffset = false;
                    resolved.PositionOffset = Vector2.zero;
                    break;
                case VnSceneComposerBeatCharacterPosition.Right:
                    resolved.StageSlot = VnWorkshopStageSlot.Right;
                    resolved.HasPositionOffset = false;
                    resolved.PositionOffset = Vector2.zero;
                    break;
                case VnSceneComposerBeatCharacterPosition.Custom:
                    resolved.HasPositionOffset = true;
                    resolved.PositionOffset = staging.customPositionOffset;
                    break;
            }

            if (staging.hasStateOverride)
                TryApplyState(resolved, characterId, staging.stateId, "Character staging", warnings);
        }

        private static void TryApplyState(
            VnSceneComposerResolvedBeatCharacterStaging resolved,
            string characterId,
            string stateId,
            string context,
            List<string> warnings)
        {
            if (!VnSceneComposerCharacterStateResolver.TryResolve(
                    stateId, out VnSceneComposerResolvedCharacterState state))
            {
                AddWarning(warnings,
                    context + " state '" + (stateId ?? string.Empty) +
                    "' is missing for '" + characterId + "'. Keeping previous state.");
                return;
            }

            if (!string.Equals(state.Character, characterId, StringComparison.OrdinalIgnoreCase))
            {
                AddWarning(warnings,
                    context + " state '" + stateId + "' belongs to '" + state.Character +
                    "', not '" + characterId + "'. Keeping previous state.");
                return;
            }

            resolved.StateId = stateId ?? string.Empty;
        }

        private static void ValidateOrPreserveState(
            VnSceneComposerResolvedBeatCharacterStaging resolved,
            string characterId,
            string stateId,
            string context,
            List<string> warnings)
        {
            if (VnSceneComposerCharacterStateResolver.TryResolve(
                    stateId, out VnSceneComposerResolvedCharacterState state) &&
                string.Equals(state.Character, characterId, StringComparison.OrdinalIgnoreCase))
                return;

            AddWarning(warnings,
                context + " state '" + (stateId ?? string.Empty) +
                "' cannot be resolved for '" + characterId + "'.");
        }

        private static bool ResolveInitialVisibility(VnSceneComposerScene scene, string characterId)
        {
            if (scene.dialogueBeats == null) return true;
            for (int i = 0; i < scene.dialogueBeats.Count; i++)
            {
                VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                if (beat == null || beat.characterStaging == null) continue;
                for (int r = 0; r < beat.characterStaging.Count; r++)
                {
                    VnSceneComposerBeatCharacterStaging staging = beat.characterStaging[r];
                    if (staging == null ||
                        !string.Equals(staging.characterId ?? string.Empty, characterId,
                            StringComparison.OrdinalIgnoreCase) ||
                        staging.visibility == VnSceneComposerBeatCharacterVisibility.KeepPrevious)
                        continue;

                    if (staging.visibility == VnSceneComposerBeatCharacterVisibility.Show)
                        return false;
                    return true;
                }
            }
            return true;
        }

        private static VnSceneComposerCharacter FindAuthoredCharacter(
            VnSceneComposerScene scene, string characterId)
        {
            if (scene == null || scene.characters == null || string.IsNullOrWhiteSpace(characterId))
                return null;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter candidate = scene.characters[i];
                if (candidate == null) continue;
                string candidateId = VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(candidate);
                if (string.Equals(candidateId, characterId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private static int FindBeatIndex(VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (scene == null || scene.dialogueBeats == null || beat == null) return -1;
            for (int i = 0; i < scene.dialogueBeats.Count; i++)
            {
                VnSceneComposerDialogueBeat candidate = scene.dialogueBeats[i];
                if (ReferenceEquals(candidate, beat)) return i;
                if (candidate != null && !string.IsNullOrEmpty(beat.beatId) &&
                    string.Equals(candidate.beatId, beat.beatId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static void AddWarning(List<string> warnings, string warning)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(warning)) return;
            if (!warnings.Contains(warning)) warnings.Add(warning);
        }
    }
}
