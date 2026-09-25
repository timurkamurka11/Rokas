using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Presentation
{
    public struct RokasVnRuntimeCharacterSample
    {
        public string characterId;
        public string stateId;
        public bool visible;
        public Vector2 position;
        public float scale;
        public float alpha;
        public float brightness;
        public int stageSlot;
    }

    public struct RokasVnRuntimeReplicaEffectSample
    {
        public bool active;
        public Vector2 offset;
        public float scale;
        public Color flash;
    }

    public sealed class RokasVnRuntimePlaybackState
    {
        private const float VirtualCanvasWidth = 1920f;

        private struct CharacterState
        {
            public string stateId;
            public bool visible;
            public float alpha;
            public int slot;
            public Vector2 position;
            public float scale;
            public float scaleMultiplier;
        }

        private readonly RokasVnRuntimeIntroSnapshot snapshot;
        private bool started;
        private bool forceDialogueVisible;
        private bool completionRaised;
        private float beatElapsedSeconds;
        private float terminalFadeElapsed;

        public event Action VnSequenceCompleted;

        public int CurrentSceneIndex { get; private set; } = -1;
        public int CurrentBeatIndex { get; private set; } = -1;
        public bool IsTerminalFadeActive { get; private set; }
        public bool IsSequenceCompleted { get; private set; }
        public float TerminalFadeAlpha { get; private set; }

        public RokasVnRuntimeSceneSnapshot CurrentScene
        {
            get
            {
                if (CurrentSceneIndex < 0 || snapshot.scenes == null ||
                    CurrentSceneIndex >= snapshot.scenes.Count) return null;
                return snapshot.scenes[CurrentSceneIndex];
            }
        }

        public RokasVnRuntimeBeatSnapshot CurrentBeat
        {
            get
            {
                RokasVnRuntimeSceneSnapshot scene = CurrentScene;
                if (scene == null || scene.dialogueBeats == null ||
                    CurrentBeatIndex < 0 || CurrentBeatIndex >= scene.dialogueBeats.Count)
                    return null;
                return scene.dialogueBeats[CurrentBeatIndex];
            }
        }

        public float BeatElapsedSeconds => beatElapsedSeconds;

        public int VisibleDialogueCharacters
        {
            get
            {
                RokasVnRuntimeBeatSnapshot beat = CurrentBeat;
                if (beat == null || string.IsNullOrEmpty(beat.text)) return 0;
                if (forceDialogueVisible) return beat.text.Length;
                RokasVnRuntimePresentationSnapshot presentation = Presentation();
                return CalculateVisibleCharacters(beat.text, beatElapsedSeconds, presentation);
            }
        }

        public bool IsDialogueRevealComplete
        {
            get
            {
                RokasVnRuntimeBeatSnapshot beat = CurrentBeat;
                return beat == null || string.IsNullOrEmpty(beat.text) ||
                       VisibleDialogueCharacters >= beat.text.Length;
            }
        }

        public RokasVnRuntimePlaybackState(RokasVnRuntimeIntroSnapshot runtimeSnapshot)
        {
            snapshot = runtimeSnapshot ?? throw new ArgumentNullException(nameof(runtimeSnapshot));
        }

        public void StartFromBeginning()
        {
            if (snapshot.scenes == null || snapshot.scenes.Count == 0)
                throw new InvalidOperationException("Runtime VN snapshot contains no Scenes.");

            started = true;
            completionRaised = false;
            IsSequenceCompleted = false;
            IsTerminalFadeActive = false;
            TerminalFadeAlpha = 0f;
            terminalFadeElapsed = 0f;
            EnterScene(0);
        }

        public void AdvanceTime(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime < 0f || float.IsNaN(unscaledDeltaTime) ||
                float.IsInfinity(unscaledDeltaTime))
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            if (!started || IsSequenceCompleted) return;

            if (IsTerminalFadeActive)
            {
                terminalFadeElapsed += unscaledDeltaTime;
                float duration = CurrentScene != null
                    ? Mathf.Max(.0001f, CurrentScene.terminalFadeDuration)
                    : .0001f;
                TerminalFadeAlpha = Mathf.Clamp01(terminalFadeElapsed / duration);
                if (TerminalFadeAlpha >= 1f)
                    CompleteSequenceOnce();
                return;
            }

            beatElapsedSeconds += unscaledDeltaTime;
        }

        public bool RequestAdvance()
        {
            if (!started || IsSequenceCompleted || IsTerminalFadeActive) return false;
            RokasVnRuntimeBeatSnapshot beat = CurrentBeat;
            if (beat != null && !string.IsNullOrEmpty(beat.text) &&
                VisibleDialogueCharacters < beat.text.Length)
            {
                forceDialogueVisible = true;
                return false;
            }
            return AdvanceDialogue();
        }

        public bool AdvanceDialogue()
        {
            if (!started || IsSequenceCompleted || IsTerminalFadeActive) return false;
            RokasVnRuntimeSceneSnapshot scene = CurrentScene;
            if (scene == null) return false;

            int beatCount = scene.dialogueBeats != null ? scene.dialogueBeats.Count : 0;
            if (CurrentBeatIndex >= 0 && CurrentBeatIndex + 1 < beatCount)
            {
                CurrentBeatIndex++;
                BeginBeat();
                return true;
            }

            if (scene.isTerminal)
            {
                BeginTerminalFade();
                return true;
            }

            int nextScene = CurrentSceneIndex + 1;
            if (nextScene < snapshot.scenes.Count)
            {
                EnterScene(nextScene);
                return true;
            }

            CompleteSequenceOnce();
            return true;
        }

        public RokasVnRuntimeCharacterSample SampleCharacter(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                throw new ArgumentException("Character identity is required.", nameof(characterId));
            RokasVnRuntimeSceneSnapshot scene = CurrentScene;
            if (scene == null)
                throw new InvalidOperationException("Runtime VN playback has no active Scene.");

            Dictionary<string, CharacterState> states = BuildCharacterStates(scene);
            CharacterState state;
            if (!states.TryGetValue(characterId, out state))
                throw new ArgumentException(
                    "Character '" + characterId + "' is not part of the current runtime Scene.",
                    nameof(characterId));

            var sample = new RokasVnRuntimeCharacterSample
            {
                characterId = characterId,
                stateId = state.stateId ?? string.Empty,
                visible = state.visible,
                position = state.position,
                scale = Mathf.Max(.01f, state.scale * Mathf.Max(.01f, state.scaleMultiplier)),
                alpha = Mathf.Clamp01(state.alpha),
                brightness = 1f,
                stageSlot = state.slot
            };

            ApplySpeakerFocus(scene, states, characterId, ref sample);
            return sample;
        }

        public RokasVnRuntimeReplicaEffectSample SampleReplicaEffect()
        {
            var result = new RokasVnRuntimeReplicaEffectSample
            {
                active = false,
                offset = Vector2.zero,
                scale = 1f,
                flash = Color.clear
            };

            RokasVnRuntimeBeatSnapshot beat = CurrentBeat;
            RokasVnRuntimeReplicaEffectSnapshot effect =
                beat != null ? beat.replicaEffect : null;
            if (effect == null || effect.type == 0) return result;

            float duration = Mathf.Clamp(effect.duration, .05f, 5f);
            float elapsed = Mathf.Max(0f, beatElapsedSeconds);
            if (elapsed >= duration) return result;

            float progress = Mathf.Clamp01(elapsed / duration);
            float intensity = Mathf.Clamp01(effect.intensity);
            result.active = true;

            switch (effect.type)
            {
                case 1: // Shake
                    float envelope = Mathf.Pow(
                        1f - progress, Mathf.Clamp(effect.decay, .25f, 4f));
                    float cycle = elapsed * Mathf.Clamp(effect.frequency, 1f, 30f) *
                                  Mathf.PI * 2f;
                    result.offset = new Vector2(
                        Mathf.Sin(cycle) * 10f,
                        Mathf.Cos(cycle * 1.31f) * 6f) * intensity * envelope;
                    break;
                case 2: // Punch
                    float impulse = Mathf.Sin(Mathf.PI * progress) * intensity;
                    Vector2 direction = effect.direction.sqrMagnitude > .0001f
                        ? effect.direction.normalized : Vector2.right;
                    result.offset = direction * (30f * impulse);
                    result.scale = 1f + .035f * impulse;
                    break;
                case 3: // Flash
                    Color color = effect.flashColor;
                    color.a = Mathf.Clamp01(
                        color.a * intensity * (1f - progress) * (1f - progress));
                    result.flash = color;
                    break;
                case 4: // Pulse
                    result.scale =
                        1f + .03f * intensity * Mathf.Sin(Mathf.PI * progress);
                    break;
                default:
                    result.active = false;
                    break;
            }
            return result;
        }

        private void EnterScene(int sceneIndex)
        {
            CurrentSceneIndex = sceneIndex;
            CurrentBeatIndex = 0;
            beatElapsedSeconds = 0f;
            forceDialogueVisible = false;
            IsTerminalFadeActive = false;
            TerminalFadeAlpha = 0f;
            terminalFadeElapsed = 0f;

            RokasVnRuntimeSceneSnapshot scene = CurrentScene;
            if (scene == null)
                throw new InvalidOperationException("Runtime VN contains a null Scene.");

            if (scene.dialogueBeats == null || scene.dialogueBeats.Count == 0)
            {
                if (scene.isTerminal) BeginTerminalFade();
                else if (sceneIndex + 1 < snapshot.scenes.Count) EnterScene(sceneIndex + 1);
                else CompleteSequenceOnce();
            }
        }

        private void BeginBeat()
        {
            beatElapsedSeconds = 0f;
            forceDialogueVisible = false;
        }

        private void BeginTerminalFade()
        {
            if (IsTerminalFadeActive || IsSequenceCompleted) return;
            IsTerminalFadeActive = true;
            terminalFadeElapsed = 0f;
            TerminalFadeAlpha = 0f;
        }

        private void CompleteSequenceOnce()
        {
            if (completionRaised) return;
            completionRaised = true;
            IsSequenceCompleted = true;
            IsTerminalFadeActive = false;
            TerminalFadeAlpha = 1f;
            Action handler = VnSequenceCompleted;
            if (handler != null) handler();
        }

        private Dictionary<string, CharacterState> BuildCharacterStates(
            RokasVnRuntimeSceneSnapshot scene)
        {
            var states = new Dictionary<string, CharacterState>(
                StringComparer.OrdinalIgnoreCase);
            if (scene.characters == null) return states;

            RokasVnRuntimePresentationSnapshot presentation =
                scene.presentation ?? new RokasVnRuntimePresentationSnapshot();

            for (int i = 0; i < scene.characters.Count; i++)
            {
                RokasVnRuntimeCharacterSnapshot authored = scene.characters[i];
                if (authored == null || string.IsNullOrWhiteSpace(authored.characterId))
                    continue;

                bool initialVisible =
                    ResolveInitialVisibility(scene, authored.characterId);
                var state = new CharacterState
                {
                    stateId = authored.stateId ?? string.Empty,
                    visible = initialVisible,
                    alpha = initialVisible ? 1f : 0f,
                    slot = authored.stageSlot,
                    position = SlotPosition(authored.stageSlot, presentation) +
                        (authored.hasPositionOffset
                            ? authored.positionOffset
                            : Vector2.zero),
                    scale = SlotScale(authored.stageSlot, presentation),
                    scaleMultiplier = authored.hasScaleMultiplier
                        ? authored.scaleMultiplier
                        : 1f
                };

                if (scene.dialogueBeats != null)
                {
                    int limit = Mathf.Min(
                        CurrentBeatIndex,
                        scene.dialogueBeats.Count - 1);
                    for (int b = 0; b <= limit; b++)
                    {
                        RokasVnRuntimeBeatSnapshot beat = scene.dialogueBeats[b];
                        if (beat == null) continue;
                        float elapsed = b < CurrentBeatIndex
                            ? float.MaxValue
                            : beatElapsedSeconds;
                        ApplyStateOverride(beat, authored.characterId, elapsed, ref state);
                        ApplyStaging(
                            beat, authored.characterId, elapsed,
                            presentation, ref state);
                        ApplyMovement(
                            beat, authored.characterId, elapsed,
                            presentation, ref state);
                    }
                }
                states[authored.characterId] = state;
            }

            return states;
        }

        private static void ApplyStateOverride(
            RokasVnRuntimeBeatSnapshot beat,
            string characterId,
            float elapsed,
            ref CharacterState state)
        {
            if (beat.hasStateOverride &&
                !string.IsNullOrWhiteSpace(beat.stateId) &&
                string.Equals(
                    beat.targetCharacterId ?? string.Empty,
                    characterId,
                    StringComparison.OrdinalIgnoreCase))
                state.stateId = beat.stateId;

            if (beat.characterStaging == null) return;
            for (int i = 0; i < beat.characterStaging.Count; i++)
            {
                RokasVnRuntimeStagingSnapshot row = beat.characterStaging[i];
                if (row == null || row.delaySeconds > elapsed + .00001f ||
                    !row.hasStateOverride || string.IsNullOrWhiteSpace(row.stateId) ||
                    !string.Equals(
                        row.characterId ?? string.Empty,
                        characterId,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                state.stateId = row.stateId;
            }
        }

        private static void ApplyStaging(
            RokasVnRuntimeBeatSnapshot beat,
            string characterId,
            float elapsed,
            RokasVnRuntimePresentationSnapshot presentation,
            ref CharacterState state)
        {
            if (beat.characterStaging == null) return;
            for (int i = 0; i < beat.characterStaging.Count; i++)
            {
                RokasVnRuntimeStagingSnapshot row = beat.characterStaging[i];
                if (row == null ||
                    !string.Equals(
                        row.characterId ?? string.Empty,
                        characterId,
                        StringComparison.OrdinalIgnoreCase) ||
                    row.delaySeconds > elapsed + .00001f)
                    continue;

                if (row.visibility == 1)
                {
                    state.visible = true;
                    state.alpha = 1f;
                }
                else if (row.visibility == 2)
                {
                    state.visible = false;
                    state.alpha = 0f;
                }

                switch (row.position)
                {
                    case 1:
                        SetSlot(0, presentation, ref state);
                        break;
                    case 2:
                        SetSlot(1, presentation, ref state);
                        break;
                    case 3:
                        SetSlot(2, presentation, ref state);
                        break;
                    case 4:
                        state.position =
                            SlotPosition(state.slot, presentation) +
                            row.customPositionOffset;
                        break;
                }
            }
        }

        private static void ApplyMovement(
            RokasVnRuntimeBeatSnapshot beat,
            string characterId,
            float elapsed,
            RokasVnRuntimePresentationSnapshot presentation,
            ref CharacterState state)
        {
            RokasVnRuntimeMovementSnapshot movement = beat.movement;
            if (movement == null) return;

            RokasVnRuntimeMovementActionSnapshot primary =
                movement.primary ?? new RokasVnRuntimeMovementActionSnapshot();
            RokasVnRuntimeMovementActionSnapshot secondary =
                movement.secondary ?? new RokasVnRuntimeMovementActionSnapshot();

            bool primaryTargets = Targets(primary, characterId);
            bool secondaryTargets = Targets(secondary, characterId);
            float secondaryStart = movement.secondaryTiming == 1
                ? 0f
                : EffectiveDuration(primary);

            if (primaryTargets)
            {
                float primaryElapsed = Mathf.Max(0f, elapsed);
                if (secondaryTargets && movement.secondaryTiming == 0 &&
                    elapsed >= secondaryStart)
                    SampleAction(
                        primary, float.MaxValue, presentation, ref state);
                else
                    SampleAction(
                        primary, primaryElapsed, presentation, ref state);
            }

            if (secondaryTargets && elapsed + .00001f >= secondaryStart)
                SampleAction(
                    secondary,
                    Mathf.Max(0f, elapsed - secondaryStart),
                    presentation,
                    ref state);
        }

        private static bool Targets(
            RokasVnRuntimeMovementActionSnapshot action,
            string characterId)
        {
            return action != null && action.action != 0 &&
                   !string.IsNullOrWhiteSpace(action.characterId) &&
                   string.Equals(
                       action.characterId, characterId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static float EffectiveDuration(
            RokasVnRuntimeMovementActionSnapshot action)
        {
            if (action == null || action.action == 0 || action.action == 1)
                return 0f;
            return Mathf.Clamp(action.duration, .05f, 10f);
        }

        private static void SampleAction(
            RokasVnRuntimeMovementActionSnapshot action,
            float elapsed,
            RokasVnRuntimePresentationSnapshot presentation,
            ref CharacterState state)
        {
            if (action == null || action.action == 0 || action.action == 1) return;

            float duration = Mathf.Clamp(action.duration, .05f, 10f);
            float raw = elapsed >= float.MaxValue * .5f
                ? 1f
                : Mathf.Clamp01(elapsed / duration);
            float t = raw * raw * (3f - (2f * raw));

            Vector2 startPosition = state.position;
            float startScale = state.scale;
            float startAlpha = Mathf.Clamp01(state.alpha);
            bool startVisible = state.visible;

            switch (action.action)
            {
                case 5: // MoveLeft
                    SampleMove(
                        0, startPosition, startScale, startAlpha,
                        startVisible, t, presentation, ref state);
                    break;
                case 6: // MoveCenter
                    SampleMove(
                        1, startPosition, startScale, startAlpha,
                        startVisible, t, presentation, ref state);
                    break;
                case 7: // MoveRight
                    SampleMove(
                        2, startPosition, startScale, startAlpha,
                        startVisible, t, presentation, ref state);
                    break;
                case 2: // ExitLeft
                case 3: // ExitRight
                    float sign = action.action == 2 ? -1f : 1f;
                    Vector2 exit = new Vector2(
                        sign * VirtualCanvasWidth * .75f,
                        startPosition.y);
                    state.position =
                        Vector2.LerpUnclamped(startPosition, exit, t);
                    state.alpha =
                        Mathf.LerpUnclamped(startAlpha, 0f, t);
                    state.visible = startVisible && raw < 1f;
                    break;
                case 4: // Disappear
                    state.position = startPosition;
                    state.alpha =
                        Mathf.LerpUnclamped(startAlpha, 0f, t);
                    state.visible = startVisible && raw < 1f;
                    break;
            }
        }

        private static void SampleMove(
            int targetSlot,
            Vector2 startPosition,
            float startScale,
            float startAlpha,
            bool startVisible,
            float t,
            RokasVnRuntimePresentationSnapshot presentation,
            ref CharacterState state)
        {
            Vector2 targetPosition =
                SlotPosition(targetSlot, presentation);
            float targetScale = SlotScale(targetSlot, presentation);
            state.position =
                Vector2.LerpUnclamped(startPosition, targetPosition, t);
            state.scale =
                Mathf.LerpUnclamped(startScale, targetScale, t);
            state.alpha =
                Mathf.LerpUnclamped(startAlpha, 1f, t);
            state.visible = startVisible || t > .00001f;
            state.slot = targetSlot;
            if (t >= .99999f)
            {
                state.position = targetPosition;
                state.scale = targetScale;
                state.alpha = 1f;
                state.visible = true;
            }
        }

        private static void SetSlot(
            int slot,
            RokasVnRuntimePresentationSnapshot presentation,
            ref CharacterState state)
        {
            state.slot = slot;
            state.position = SlotPosition(slot, presentation);
            state.scale = SlotScale(slot, presentation);
        }

        private static Vector2 SlotPosition(
            int slot,
            RokasVnRuntimePresentationSnapshot presentation)
        {
            switch (slot)
            {
                case 0:
                    return new Vector2(
                        presentation.leftX, presentation.slotY);
                case 1:
                    return new Vector2(
                        presentation.centerX, presentation.slotY);
                case 2:
                    return new Vector2(
                        presentation.rightX, presentation.slotY);
                default:
                    return new Vector2(
                        presentation.centerX, presentation.slotY);
            }
        }

        private static float SlotScale(
            int slot,
            RokasVnRuntimePresentationSnapshot presentation)
        {
            switch (slot)
            {
                case 0: return presentation.leftScale;
                case 1: return presentation.centerScale;
                case 2: return presentation.rightScale;
                default: return presentation.centerScale;
            }
        }

        private static bool ResolveInitialVisibility(
            RokasVnRuntimeSceneSnapshot scene,
            string characterId)
        {
            if (scene.dialogueBeats == null) return true;
            for (int b = 0; b < scene.dialogueBeats.Count; b++)
            {
                RokasVnRuntimeBeatSnapshot beat = scene.dialogueBeats[b];
                if (beat == null || beat.characterStaging == null) continue;
                for (int r = 0; r < beat.characterStaging.Count; r++)
                {
                    RokasVnRuntimeStagingSnapshot row =
                        beat.characterStaging[r];
                    if (row == null ||
                        !string.Equals(
                            row.characterId ?? string.Empty,
                            characterId,
                            StringComparison.OrdinalIgnoreCase) ||
                        row.visibility == 0)
                        continue;
                    return row.visibility != 1;
                }
            }
            return true;
        }

        private void ApplySpeakerFocus(
            RokasVnRuntimeSceneSnapshot scene,
            Dictionary<string, CharacterState> states,
            string requestedCharacterId,
            ref RokasVnRuntimeCharacterSample sample)
        {
            if (!sample.visible) return;

            var visibleIds = new List<string>();
            if (scene.characters != null)
            {
                for (int i = 0; i < scene.characters.Count; i++)
                {
                    RokasVnRuntimeCharacterSnapshot character =
                        scene.characters[i];
                    if (character == null) continue;
                    CharacterState state;
                    if (states.TryGetValue(
                            character.characterId ?? string.Empty,
                            out state) &&
                        state.visible && state.alpha > .0001f)
                        visibleIds.Add(character.characterId);
                }
            }
            if (visibleIds.Count <= 1) return;

            string activeId = ResolveBeatCharacterId(
                CurrentBeat, visibleIds);
            string previousId = CurrentBeatIndex > 0 &&
                                scene.dialogueBeats != null
                ? ResolveBeatCharacterId(
                    scene.dialogueBeats[CurrentBeatIndex - 1],
                    visibleIds)
                : activeId;
            if (string.IsNullOrEmpty(activeId))
                activeId = visibleIds[0];
            if (string.IsNullOrEmpty(previousId))
                previousId = activeId;

            RokasVnRuntimePresentationSnapshot presentation =
                Presentation();
            float duration =
                Mathf.Max(0f, presentation.speakerFocusTransitionDuration);
            float raw = duration <= .0001f
                ? 1f
                : Mathf.Clamp01(beatElapsedSeconds / duration);
            float t = EvaluateEasing(
                raw, presentation.speakerFocusEasing);

            bool startActive = string.Equals(
                requestedCharacterId, previousId,
                StringComparison.OrdinalIgnoreCase);
            bool endActive = string.Equals(
                requestedCharacterId, activeId,
                StringComparison.OrdinalIgnoreCase);

            float startScale = startActive
                ? presentation.speakerActiveScale
                : presentation.speakerInactiveScale;
            float endScale = endActive
                ? presentation.speakerActiveScale
                : presentation.speakerInactiveScale;
            float startBrightness = startActive
                ? presentation.speakerActiveBrightness
                : presentation.speakerInactiveBrightness;
            float endBrightness = endActive
                ? presentation.speakerActiveBrightness
                : presentation.speakerInactiveBrightness;
            float startAlpha = startActive
                ? 1f
                : presentation.speakerInactiveAlpha;
            float endAlpha = endActive
                ? 1f
                : presentation.speakerInactiveAlpha;
            float startForward = startActive
                ? presentation.speakerActiveForwardOffset
                : 0f;
            float endForward = endActive
                ? presentation.speakerActiveForwardOffset
                : 0f;

            sample.scale *= Mathf.LerpUnclamped(
                startScale, endScale, t);
            sample.brightness = Mathf.LerpUnclamped(
                startBrightness, endBrightness, t);
            sample.alpha *= Mathf.LerpUnclamped(
                startAlpha, endAlpha, t);
            sample.position += new Vector2(
                0f, Mathf.LerpUnclamped(
                    startForward, endForward, t));
        }

        private static string ResolveBeatCharacterId(
            RokasVnRuntimeBeatSnapshot beat,
            IList<string> visibleIds)
        {
            if (beat == null) return string.Empty;
            string requested = !string.IsNullOrWhiteSpace(
                beat.targetCharacterId)
                ? beat.targetCharacterId
                : beat.speaker;
            if (string.IsNullOrWhiteSpace(requested))
                return string.Empty;
            for (int i = 0; i < visibleIds.Count; i++)
            {
                if (string.Equals(
                        visibleIds[i], requested,
                        StringComparison.OrdinalIgnoreCase))
                    return visibleIds[i];
            }
            return string.Empty;
        }

        private RokasVnRuntimePresentationSnapshot Presentation()
        {
            RokasVnRuntimeSceneSnapshot scene = CurrentScene;
            return scene != null && scene.presentation != null
                ? scene.presentation
                : new RokasVnRuntimePresentationSnapshot();
        }

        private static int CalculateVisibleCharacters(
            string text,
            float elapsedSeconds,
            RokasVnRuntimePresentationSnapshot values)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (!values.typewriterEnabled) return text.Length;
            if (elapsedSeconds < 0f) return 0;

            float time = Mathf.Max(0f, values.typewriterLineStartDelay);
            if (elapsedSeconds < time) return 0;
            float characterDelay =
                1f / Mathf.Max(
                    .0001f, values.typewriterCharactersPerSecond) +
                Mathf.Max(0f, values.typewriterBaseCharacterDelay);
            int visible = 0;
            for (int i = 0; i < text.Length; i++)
            {
                time += characterDelay;
                if (elapsedSeconds + .00001f < time) return visible;
                visible = i + 1;
                time += GetPunctuationPause(text, i, values);
                if (elapsedSeconds + .00001f < time) return visible;
            }
            return visible;
        }

        private static float GetPunctuationPause(
            string text,
            int index,
            RokasVnRuntimePresentationSnapshot values)
        {
            char c = text[index];
            if (c == ',') return values.typewriterCommaPause;
            if (c == '…') return values.typewriterEllipsisPause;
            if (c == '?') return values.typewriterQuestionPause;
            if (c == '!') return values.typewriterExclamationPause;
            if (c != '.') return 0f;

            bool inAsciiEllipsis =
                (index > 0 && text[index - 1] == '.') ||
                (index + 1 < text.Length && text[index + 1] == '.');
            if (!inAsciiEllipsis)
                return values.typewriterPeriodPause;
            bool isTerminalEllipsisDot =
                index >= 2 && text[index - 1] == '.' &&
                text[index - 2] == '.' &&
                (index + 1 >= text.Length || text[index + 1] != '.');
            return isTerminalEllipsisDot
                ? values.typewriterEllipsisPause
                : 0f;
        }

        private static float EvaluateEasing(float value, int easing)
        {
            float t = Mathf.Clamp01(value);
            switch (easing)
            {
                case 0: // Linear
                    return t;
                case 1: // EaseIn
                    return t * t;
                case 2: // EaseOut
                    return 1f - ((1f - t) * (1f - t));
                case 3: // EaseInOut
                    return t * t * (3f - (2f * t));
                default:
                    return t;
            }
        }
    }
}
