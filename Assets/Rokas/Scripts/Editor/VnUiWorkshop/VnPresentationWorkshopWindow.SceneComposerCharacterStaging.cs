using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [SerializeField] private string _sceneComposerSelectedCharacterStagingId = string.Empty;

        public string ComposerAddSelectedDialogueBeatCharacterStaging(string characterId)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null) throw new InvalidOperationException("No dialogue Beat is selected.");
            string canonical = ResolveSceneCharacterId(scene, characterId);
            if (string.IsNullOrEmpty(canonical))
                throw new ArgumentException("Character staging requires an existing Scene character.",
                    nameof(characterId));

            if (beat.characterStaging == null)
                beat.characterStaging = new List<VnSceneComposerBeatCharacterStaging>();

            VnSceneComposerBeatCharacterStaging existing =
                FindCharacterStagingByCharacter(beat, canonical);
            if (existing != null)
            {
                _sceneComposerSelectedCharacterStagingId = existing.stagingId ?? string.Empty;
                Repaint();
                return _sceneComposerSelectedCharacterStagingId;
            }

            RecordSceneComposerUndo("Add VN Beat Character Staging");
            var staging = new VnSceneComposerBeatCharacterStaging
            {
                characterId = canonical
            };
            beat.characterStaging.Add(staging);
            _sceneComposerSelectedCharacterStagingId = staging.stagingId;
            MarkSceneComposerChanged();
            return staging.stagingId;
        }

        public void ComposerSelectDialogueBeatCharacterStaging(string stagingId)
        {
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null || FindCharacterStaging(beat, stagingId) == null)
                throw new ArgumentException("Character staging row is not part of the selected Beat.",
                    nameof(stagingId));
            _sceneComposerSelectedCharacterStagingId = stagingId ?? string.Empty;
            Repaint();
        }

        public void ComposerDeleteSelectedDialogueBeatCharacterStaging()
        {
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            VnSceneComposerBeatCharacterStaging selected = GetSelectedCharacterStaging(beat);
            if (beat == null || selected == null || beat.characterStaging == null) return;

            RecordSceneComposerUndo("Delete VN Beat Character Staging");
            beat.characterStaging.Remove(selected);
            _sceneComposerSelectedCharacterStagingId =
                beat.characterStaging.Count > 0 && beat.characterStaging[0] != null
                    ? beat.characterStaging[0].stagingId ?? string.Empty
                    : string.Empty;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedDialogueBeatCharacterStaging(
            string characterId,
            VnSceneComposerBeatCharacterVisibility visibility,
            VnSceneComposerBeatCharacterPosition position,
            Vector2 customPositionOffset,
            bool hasStateOverride,
            string stateId,
            VnSceneComposerBeatEffect effect,
            float effectStrength,
            float effectDuration,
            float delaySeconds)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            VnSceneComposerBeatCharacterStaging selected = GetSelectedCharacterStaging(beat);
            if (beat == null || selected == null)
                throw new InvalidOperationException("No Beat character staging row is selected.");

            string canonical = ResolveSceneCharacterId(scene, characterId);
            if (string.IsNullOrEmpty(canonical))
                throw new ArgumentException("Character staging requires an existing Scene character.",
                    nameof(characterId));

            if (beat.characterStaging != null)
            {
                for (int i = 0; i < beat.characterStaging.Count; i++)
                {
                    VnSceneComposerBeatCharacterStaging candidate = beat.characterStaging[i];
                    if (candidate == null || ReferenceEquals(candidate, selected)) continue;
                    if (string.Equals(candidate.characterId, canonical, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "This Beat already has a staging row for " + canonical + ".");
                }
            }

            if (!Enum.IsDefined(typeof(VnSceneComposerBeatCharacterVisibility), visibility))
                throw new ArgumentOutOfRangeException(nameof(visibility));
            if (!Enum.IsDefined(typeof(VnSceneComposerBeatCharacterPosition), position))
                throw new ArgumentOutOfRangeException(nameof(position));
            if (!Enum.IsDefined(typeof(VnSceneComposerBeatEffect), effect))
                throw new ArgumentOutOfRangeException(nameof(effect));
            if (float.IsNaN(customPositionOffset.x) || float.IsInfinity(customPositionOffset.x) ||
                float.IsNaN(customPositionOffset.y) || float.IsInfinity(customPositionOffset.y))
                throw new ArgumentOutOfRangeException(nameof(customPositionOffset));
            if (float.IsNaN(delaySeconds) || float.IsInfinity(delaySeconds) || delaySeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));

            string nextState = hasStateOverride ? stateId ?? string.Empty : string.Empty;
            if (hasStateOverride)
            {
                if (!VnSceneComposerCharacterStateResolver.TryResolve(
                        nextState, out VnSceneComposerResolvedCharacterState state))
                    throw new ArgumentException("Unknown character state: " + nextState, nameof(stateId));
                if (!string.Equals(state.Character, canonical, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(
                        "Character state belongs to " + state.Character + ", not " + canonical + ".",
                        nameof(stateId));
            }

            RecordSceneComposerUndo("Edit VN Beat Character Staging");
            selected.characterId = canonical;
            selected.visibility = visibility;
            selected.position = position;
            selected.customPositionOffset = customPositionOffset;
            selected.hasStateOverride = hasStateOverride;
            selected.stateId = nextState;
            selected.effect = effect;
            selected.effectStrength = Mathf.Max(0f, effectStrength);
            selected.effectDuration = Mathf.Clamp(effectDuration, .01f, 10f);
            selected.delaySeconds = Mathf.Max(0f, delaySeconds);

            if (effect == VnSceneComposerBeatEffect.Accent && beat.characterStaging != null)
            {
                for (int i = 0; i < beat.characterStaging.Count; i++)
                {
                    VnSceneComposerBeatCharacterStaging candidate = beat.characterStaging[i];
                    if (candidate == null || ReferenceEquals(candidate, selected)) continue;
                    candidate.effect = VnSceneComposerBeatEffect.None;
                }
            }

            MarkSceneComposerChanged();
        }

        private void DrawSceneComposerBeatCharacterStagingInspector(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat)
        {
            if (scene == null || beat == null) return;
            if (beat.characterStaging == null)
                beat.characterStaging = new List<VnSceneComposerBeatCharacterStaging>();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Персонажи в этой реплике", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Здесь меняется только постановка персонажей текущей реплики. Фон, видео, музыка и звуки не перезапускаются.",
                MessageType.Info);

            string[] sceneCharacters = GetSceneComposerBeatTargetCharacterIds(scene);
            string addCharacter = FindFirstUnusedCharacter(beat, sceneCharacters);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(addCharacter)))
            {
                if (GUILayout.Button("+ Добавить персонажа"))
                    ComposerAddSelectedDialogueBeatCharacterStaging(addCharacter);
            }

            VnSceneComposerBeatCharacterStaging selected = GetSelectedCharacterStaging(beat);
            for (int i = 0; i < beat.characterStaging.Count; i++)
            {
                VnSceneComposerBeatCharacterStaging staging = beat.characterStaging[i];
                if (staging == null) continue;
                bool isSelected = selected != null &&
                                  string.Equals(selected.stagingId, staging.stagingId,
                                      StringComparison.Ordinal);
                string label = string.IsNullOrWhiteSpace(staging.characterId)
                    ? "Персонаж"
                    : staging.characterId;
                if (GUILayout.Button(
                        (i + 1) + ". " + label,
                        isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                {
                    ComposerSelectDialogueBeatCharacterStaging(staging.stagingId);
                    selected = staging;
                }
            }

            selected = GetSelectedCharacterStaging(beat);
            if (selected == null)
            {
                if (sceneCharacters.Length == 0)
                    EditorGUILayout.HelpBox(
                        "Сначала добавьте персонажа в список персонажей сцены.",
                        MessageType.Info);
                DrawCharacterStagingWarnings(scene, beat);
                return;
            }

            int characterIndex = 0;
            for (int i = 0; i < sceneCharacters.Length; i++)
            {
                if (string.Equals(
                        sceneCharacters[i], selected.characterId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    characterIndex = i;
                    break;
                }
            }

            string[] visibilityLabels = { "Оставить предыдущее", "Показать", "Скрыть" };
            string[] positionLabels =
            {
                "Оставить предыдущее", "Слева", "Центр", "Справа", "Свободно"
            };

            string currentCharacter =
                sceneCharacters.Length > 0
                    ? sceneCharacters[Mathf.Clamp(characterIndex, 0, sceneCharacters.Length - 1)]
                    : selected.characterId ?? string.Empty;
            string[] stateIds = string.IsNullOrEmpty(currentCharacter)
                ? Array.Empty<string>()
                : ComposerGetAuthoredStateIds(currentCharacter);
            string[] stateLabels = new string[stateIds.Length + 1];
            stateLabels[0] = "Оставить предыдущее";
            int stateIndex = 0;
            for (int i = 0; i < stateIds.Length; i++)
            {
                stateLabels[i + 1] = GetSceneComposerBeatStateDisplayName(
                    currentCharacter, stateIds[i]);
                if (selected.hasStateOverride &&
                    string.Equals(stateIds[i], selected.stateId, StringComparison.Ordinal))
                    stateIndex = i + 1;
            }

            EditorGUI.BeginChangeCheck();
            int nextCharacterIndex = sceneCharacters.Length > 0
                ? EditorGUILayout.Popup("Персонаж", characterIndex, sceneCharacters)
                : 0;
            string nextCharacter = sceneCharacters.Length > 0
                ? sceneCharacters[nextCharacterIndex]
                : currentCharacter;
            int nextVisibility = EditorGUILayout.Popup(
                "Видимость", (int)selected.visibility, visibilityLabels);
            int nextPosition = EditorGUILayout.Popup(
                "Положение", (int)selected.position, positionLabels);

            Vector2 nextCustom = selected.customPositionOffset;
            if ((VnSceneComposerBeatCharacterPosition)nextPosition ==
                VnSceneComposerBeatCharacterPosition.Custom)
            {
                nextCustom.x = EditorGUILayout.FloatField("Смещение X", nextCustom.x);
                nextCustom.y = EditorGUILayout.FloatField("Смещение Y", nextCustom.y);
            }

            if (!string.Equals(nextCharacter, currentCharacter, StringComparison.OrdinalIgnoreCase))
            {
                stateIds = ComposerGetAuthoredStateIds(nextCharacter);
                stateLabels = new string[stateIds.Length + 1];
                stateLabels[0] = "Оставить предыдущее";
                for (int i = 0; i < stateIds.Length; i++)
                    stateLabels[i + 1] =
                        GetSceneComposerBeatStateDisplayName(nextCharacter, stateIds[i]);
                stateIndex = 0;
            }

            int nextStateIndex = EditorGUILayout.Popup("Эмоция / поза", stateIndex, stateLabels);
            bool nextHasState = nextStateIndex > 0;
            string nextState = nextHasState ? stateIds[nextStateIndex - 1] : string.Empty;

            string[] effectLabels = { "Без анимации", "Акцент", "Подскок" };
            int currentEffect = selected.effect == VnSceneComposerBeatEffect.Accent
                ? 1
                : (selected.effect == VnSceneComposerBeatEffect.Hop ? 2 : 0);
            int nextEffect = EditorGUILayout.Popup(
                "Анимация реплики", currentEffect, effectLabels);
            float nextStrength = selected.effectStrength;
            float nextDuration = selected.effectDuration;
            if (nextEffect != 0)
            {
                nextStrength = EditorGUILayout.Slider("Сила", nextStrength, 0f, 100f);
                nextDuration = DrawSceneComposerDurationControl(
                    "Длительность", nextDuration, .01f);
            }

            float nextDelay = Mathf.Max(
                0f, EditorGUILayout.FloatField("Задержка, сек", selected.delaySeconds));

            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    ComposerSetSelectedDialogueBeatCharacterStaging(
                        nextCharacter,
                        (VnSceneComposerBeatCharacterVisibility)Mathf.Clamp(nextVisibility, 0, 2),
                        (VnSceneComposerBeatCharacterPosition)Mathf.Clamp(nextPosition, 0, 4),
                        nextCustom,
                        nextHasState,
                        nextState,
                        nextEffect == 1
                            ? VnSceneComposerBeatEffect.Accent
                            : (nextEffect == 2
                                ? VnSceneComposerBeatEffect.Hop
                                : VnSceneComposerBeatEffect.None),
                        nextStrength,
                        nextDuration,
                        nextDelay);
                }
                catch (Exception exception)
                {
                    SetSceneComposerStatus(
                        "Не удалось изменить постановку персонажа: " + exception.Message,
                        MessageType.Warning);
                }
            }

            if (GUILayout.Button("Удалить постановку персонажа"))
                ComposerDeleteSelectedDialogueBeatCharacterStaging();

            DrawCharacterStagingWarnings(scene, beat);
        }

        private void DrawCharacterStagingWarnings(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            string[] warnings =
                VnSceneComposerBeatCharacterStagingResolver.CollectWarnings(scene, beat);
            for (int i = 0; i < warnings.Length; i++)
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }

        private VnSceneComposerBeatCharacterStaging GetSelectedCharacterStaging(
            VnSceneComposerDialogueBeat beat)
        {
            if (beat == null || beat.characterStaging == null ||
                beat.characterStaging.Count == 0)
                return null;

            VnSceneComposerBeatCharacterStaging selected =
                FindCharacterStaging(beat, _sceneComposerSelectedCharacterStagingId);
            if (selected != null) return selected;

            for (int i = 0; i < beat.characterStaging.Count; i++)
            {
                if (beat.characterStaging[i] == null) continue;
                _sceneComposerSelectedCharacterStagingId =
                    beat.characterStaging[i].stagingId ?? string.Empty;
                return beat.characterStaging[i];
            }
            return null;
        }

        private static VnSceneComposerBeatCharacterStaging FindCharacterStaging(
            VnSceneComposerDialogueBeat beat, string stagingId)
        {
            if (beat == null || beat.characterStaging == null ||
                string.IsNullOrEmpty(stagingId))
                return null;
            for (int i = 0; i < beat.characterStaging.Count; i++)
            {
                VnSceneComposerBeatCharacterStaging staging = beat.characterStaging[i];
                if (staging != null &&
                    string.Equals(staging.stagingId, stagingId, StringComparison.Ordinal))
                    return staging;
            }
            return null;
        }

        private static VnSceneComposerBeatCharacterStaging FindCharacterStagingByCharacter(
            VnSceneComposerDialogueBeat beat, string characterId)
        {
            if (beat == null || beat.characterStaging == null ||
                string.IsNullOrWhiteSpace(characterId))
                return null;
            for (int i = 0; i < beat.characterStaging.Count; i++)
            {
                VnSceneComposerBeatCharacterStaging staging = beat.characterStaging[i];
                if (staging != null &&
                    string.Equals(
                        staging.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return staging;
            }
            return null;
        }

        private static string FindFirstUnusedCharacter(
            VnSceneComposerDialogueBeat beat, string[] sceneCharacters)
        {
            if (sceneCharacters == null) return string.Empty;
            for (int i = 0; i < sceneCharacters.Length; i++)
            {
                string id = sceneCharacters[i];
                if (FindCharacterStagingByCharacter(beat, id) == null)
                    return id;
            }
            return string.Empty;
        }

        private static string ResolveSceneCharacterId(
            VnSceneComposerScene scene, string requested)
        {
            if (scene == null || scene.characters == null ||
                string.IsNullOrWhiteSpace(requested))
                return string.Empty;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter character = scene.characters[i];
                if (character == null) continue;
                string id = VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(character);
                if (string.Equals(id, requested, StringComparison.OrdinalIgnoreCase))
                    return id;
            }
            return string.Empty;
        }
    }
}
