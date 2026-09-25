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
            return TryResolveEffect(
                scene, beat, beatElapsedSeconds, VnSceneComposerBeatEffect.Accent,
                out characterId, out strength, out duration, out elapsedSinceTrigger);
        }

        internal static bool TryResolveHop(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            float beatElapsedSeconds,
            out string characterId,
            out float strength,
            out float duration,
            out float elapsedSinceTrigger)
        {
            return TryResolveEffect(
                scene, beat, beatElapsedSeconds, VnSceneComposerBeatEffect.Hop,
                out characterId, out strength, out duration, out elapsedSinceTrigger);
        }

        private static bool TryResolveEffect(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            float beatElapsedSeconds,
            VnSceneComposerBeatEffect requestedEffect,
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
                if (staging == null || staging.effect != requestedEffect ||
                    staging.delaySeconds > beatElapsedSeconds + .00001f)
                    continue;
                if (FindAuthoredCharacter(scene, staging.characterId) == null) continue;

                characterId = staging.characterId ?? string.Empty;
                strength = Mathf.Max(0f, staging.effectStrength);
                duration = Mathf.Clamp(staging.effectDuration, .01f, 10f);
                elapsedSinceTrigger =
                    Mathf.Max(0f, beatElapsedSeconds - staging.delaySeconds);
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

    public sealed class VnSceneComposerResolvedMovementSample
    {
        public string CharacterId { get; internal set; } = string.Empty;
        public bool Visible { get; internal set; } = true;
        public float Alpha { get; internal set; } = 1f;
        public VnWorkshopStageSlot StageSlot { get; internal set; } = VnWorkshopStageSlot.Center;
        public Vector2 StagePosition { get; internal set; }
        public float StageScale { get; internal set; } = 1f;
    }

    public static class VnSceneComposerMovementResolver
    {
        private struct MovementState
        {
            public bool Visible;
            public float Alpha;
            public VnWorkshopStageSlot Slot;
            public Vector2 Position;
            public float Scale;
        }

        public static VnSceneComposerResolvedMovementSample Sample(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            string characterId,
            float beatElapsedSeconds,
            VnWorkshopStageLayoutValues stage,
            float virtualCanvasWidth)
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

            var state = new MovementState
            {
                Visible = ResolveInitialVisibility(scene, characterId),
                Alpha = ResolveInitialVisibility(scene, characterId) ? 1f : 0f,
                Slot = authored.stageSlot,
                Position = SlotPosition(authored.stageSlot, stage) +
                    (authored.hasPositionOffset ? authored.positionOffset : Vector2.zero),
                Scale = SlotScale(authored.stageSlot, stage)
            };

            for (int i = 0; i <= beatIndex; i++)
            {
                VnSceneComposerDialogueBeat current = scene.dialogueBeats[i];
                if (current == null) continue;
                float elapsed = i < beatIndex ? float.MaxValue : beatElapsedSeconds;
                ApplyStaging(current, characterId, elapsed, stage, ref state);
                ApplyMovement(current, characterId, elapsed, stage, virtualCanvasWidth, ref state);
            }

            return new VnSceneComposerResolvedMovementSample
            {
                CharacterId = characterId,
                Visible = state.Visible,
                Alpha = Mathf.Clamp01(state.Alpha),
                StageSlot = state.Slot,
                StagePosition = state.Position,
                StageScale = Mathf.Max(.01f, state.Scale)
            };
        }

        private static void ApplyStaging(
            VnSceneComposerDialogueBeat beat,
            string characterId,
            float elapsed,
            VnWorkshopStageLayoutValues stage,
            ref MovementState state)
        {
            if (beat.characterStaging == null) return;
            for (int i = 0; i < beat.characterStaging.Count; i++)
            {
                VnSceneComposerBeatCharacterStaging row = beat.characterStaging[i];
                if (row == null ||
                    !string.Equals(row.characterId ?? string.Empty, characterId,
                        StringComparison.OrdinalIgnoreCase) ||
                    row.delaySeconds > elapsed + .00001f)
                    continue;

                switch (row.visibility)
                {
                    case VnSceneComposerBeatCharacterVisibility.Show:
                        state.Visible = true;
                        state.Alpha = 1f;
                        break;
                    case VnSceneComposerBeatCharacterVisibility.Hide:
                        state.Visible = false;
                        state.Alpha = 0f;
                        break;
                }

                switch (row.position)
                {
                    case VnSceneComposerBeatCharacterPosition.Left:
                        SetSlot(VnWorkshopStageSlot.Left, stage, ref state);
                        break;
                    case VnSceneComposerBeatCharacterPosition.Center:
                        SetSlot(VnWorkshopStageSlot.Center, stage, ref state);
                        break;
                    case VnSceneComposerBeatCharacterPosition.Right:
                        SetSlot(VnWorkshopStageSlot.Right, stage, ref state);
                        break;
                    case VnSceneComposerBeatCharacterPosition.Custom:
                        state.Position = SlotPosition(state.Slot, stage) + row.customPositionOffset;
                        break;
                }
            }
        }

        private static void ApplyMovement(
            VnSceneComposerDialogueBeat beat,
            string characterId,
            float elapsed,
            VnWorkshopStageLayoutValues stage,
            float virtualCanvasWidth,
            ref MovementState state)
        {
            VnSceneComposerBeatMovement movement = beat.movement;
            if (movement == null) return;

            VnSceneComposerCharacterMovementAction primary =
                movement.primary ?? new VnSceneComposerCharacterMovementAction();
            VnSceneComposerCharacterMovementAction secondary =
                movement.secondary ?? new VnSceneComposerCharacterMovementAction();

            bool primaryTargets = Targets(primary, characterId);
            bool secondaryTargets = Targets(secondary, characterId);

            float secondaryStart =
                movement.secondaryTiming == VnSceneComposerMovementTiming.Simultaneous
                    ? 0f
                    : EffectiveDuration(primary);

            if (primaryTargets)
            {
                float primaryElapsed = Mathf.Max(0f, elapsed);
                if (secondaryTargets &&
                    movement.secondaryTiming == VnSceneComposerMovementTiming.AfterPrimary &&
                    elapsed >= secondaryStart)
                {
                    SampleAction(primary, float.MaxValue, stage, virtualCanvasWidth, ref state);
                }
                else
                {
                    SampleAction(primary, primaryElapsed, stage, virtualCanvasWidth, ref state);
                }
            }

            if (secondaryTargets && elapsed + .00001f >= secondaryStart)
            {
                SampleAction(
                    secondary,
                    Mathf.Max(0f, elapsed - secondaryStart),
                    stage,
                    virtualCanvasWidth,
                    ref state);
            }
        }

        private static bool Targets(
            VnSceneComposerCharacterMovementAction action, string characterId)
        {
            return action != null &&
                   action.action != VnSceneComposerMovementActionType.None &&
                   !string.IsNullOrWhiteSpace(action.characterId) &&
                   string.Equals(action.characterId, characterId, StringComparison.OrdinalIgnoreCase);
        }

        private static float EffectiveDuration(VnSceneComposerCharacterMovementAction action)
        {
            if (action == null ||
                action.action == VnSceneComposerMovementActionType.None ||
                action.action == VnSceneComposerMovementActionType.Stay)
                return 0f;
            return Mathf.Clamp(action.duration, .05f, 10f);
        }

        private static void SampleAction(
            VnSceneComposerCharacterMovementAction action,
            float elapsed,
            VnWorkshopStageLayoutValues stage,
            float virtualCanvasWidth,
            ref MovementState state)
        {
            if (action == null ||
                action.action == VnSceneComposerMovementActionType.None ||
                action.action == VnSceneComposerMovementActionType.Stay)
                return;

            float duration = Mathf.Clamp(action.duration, .05f, 10f);
            float raw = elapsed >= float.MaxValue * .5f
                ? 1f
                : Mathf.Clamp01(elapsed / duration);
            float t = raw * raw * (3f - (2f * raw));

            Vector2 startPosition = state.Position;
            float startScale = state.Scale;
            float startAlpha = Mathf.Clamp01(state.Alpha);
            bool startVisible = state.Visible;

            switch (action.action)
            {
                case VnSceneComposerMovementActionType.MoveLeft:
                    SampleMove(VnWorkshopStageSlot.Left, startPosition, startScale, startAlpha,
                        startVisible, t, stage, ref state);
                    break;
                case VnSceneComposerMovementActionType.MoveCenter:
                    SampleMove(VnWorkshopStageSlot.Center, startPosition, startScale, startAlpha,
                        startVisible, t, stage, ref state);
                    break;
                case VnSceneComposerMovementActionType.MoveRight:
                    SampleMove(VnWorkshopStageSlot.Right, startPosition, startScale, startAlpha,
                        startVisible, t, stage, ref state);
                    break;
                case VnSceneComposerMovementActionType.ExitLeft:
                case VnSceneComposerMovementActionType.ExitRight:
                    float sign = action.action == VnSceneComposerMovementActionType.ExitLeft ? -1f : 1f;
                    Vector2 exit = new Vector2(
                        sign * Mathf.Max(1f, virtualCanvasWidth) * .75f,
                        startPosition.y);
                    state.Position = Vector2.LerpUnclamped(startPosition, exit, t);
                    state.Alpha = Mathf.LerpUnclamped(startAlpha, 0f, t);
                    state.Visible = startVisible && raw < 1f;
                    break;
                case VnSceneComposerMovementActionType.Disappear:
                    state.Position = startPosition;
                    state.Alpha = Mathf.LerpUnclamped(startAlpha, 0f, t);
                    state.Visible = startVisible && raw < 1f;
                    break;
            }
        }

        private static void SampleMove(
            VnWorkshopStageSlot target,
            Vector2 startPosition,
            float startScale,
            float startAlpha,
            bool startVisible,
            float t,
            VnWorkshopStageLayoutValues stage,
            ref MovementState state)
        {
            Vector2 targetPosition = SlotPosition(target, stage);
            float targetScale = SlotScale(target, stage);
            state.Position = Vector2.LerpUnclamped(startPosition, targetPosition, t);
            state.Scale = Mathf.LerpUnclamped(startScale, targetScale, t);
            state.Alpha = Mathf.LerpUnclamped(startAlpha, 1f, t);
            state.Visible = startVisible || t > .00001f;
            state.Slot = target;
            if (t >= .99999f)
            {
                state.Position = targetPosition;
                state.Scale = targetScale;
                state.Alpha = 1f;
                state.Visible = true;
            }
        }

        private static void SetSlot(
            VnWorkshopStageSlot slot,
            VnWorkshopStageLayoutValues stage,
            ref MovementState state)
        {
            state.Slot = slot;
            state.Position = SlotPosition(slot, stage);
            state.Scale = SlotScale(slot, stage);
        }

        private static Vector2 SlotPosition(
            VnWorkshopStageSlot slot, VnWorkshopStageLayoutValues stage)
        {
            switch (slot)
            {
                case VnWorkshopStageSlot.Left:
                    return new Vector2(stage.LeftX, stage.SlotY);
                case VnWorkshopStageSlot.Center:
                    return new Vector2(stage.CenterX, stage.SlotY);
                case VnWorkshopStageSlot.Right:
                    return new Vector2(stage.RightX, stage.SlotY);
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        private static float SlotScale(
            VnWorkshopStageSlot slot, VnWorkshopStageLayoutValues stage)
        {
            switch (slot)
            {
                case VnWorkshopStageSlot.Left: return stage.LeftScale;
                case VnWorkshopStageSlot.Center: return stage.CenterScale;
                case VnWorkshopStageSlot.Right: return stage.RightScale;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        private static bool ResolveInitialVisibility(
            VnSceneComposerScene scene, string characterId)
        {
            if (scene.dialogueBeats == null) return true;
            for (int i = 0; i < scene.dialogueBeats.Count; i++)
            {
                VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                if (beat == null || beat.characterStaging == null) continue;
                for (int r = 0; r < beat.characterStaging.Count; r++)
                {
                    VnSceneComposerBeatCharacterStaging row = beat.characterStaging[r];
                    if (row == null ||
                        !string.Equals(row.characterId ?? string.Empty, characterId,
                            StringComparison.OrdinalIgnoreCase) ||
                        row.visibility == VnSceneComposerBeatCharacterVisibility.KeepPrevious)
                        continue;
                    return row.visibility != VnSceneComposerBeatCharacterVisibility.Show;
                }
            }
            return true;
        }

        private static VnSceneComposerCharacter FindAuthoredCharacter(
            VnSceneComposerScene scene, string characterId)
        {
            if (scene == null || scene.characters == null) return null;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter candidate = scene.characters[i];
                if (candidate == null) continue;
                string id = VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(candidate);
                if (string.Equals(id, characterId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private static int FindBeatIndex(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
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
    }

}
