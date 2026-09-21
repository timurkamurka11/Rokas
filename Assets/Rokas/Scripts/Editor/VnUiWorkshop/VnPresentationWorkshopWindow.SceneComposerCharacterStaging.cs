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

        public void ComposerSelectDialogueBeatStagingCharacter(string characterId)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null)
                throw new InvalidOperationException("No dialogue Beat is selected.");

            string canonical = ResolveSceneCharacterId(scene, characterId);
            if (string.IsNullOrEmpty(canonical))
                throw new ArgumentException(
                    "Beat staging selection requires an existing Scene character.",
                    nameof(characterId));

            int sceneIndex = FindSceneCharacterIndex(scene, canonical);
            if (sceneIndex < 0)
                throw new ArgumentException(
                    "Character is not part of the selected Scene.", nameof(characterId));

            _sceneComposerSelectedCharacterIndex = sceneIndex;
            VnSceneComposerBeatCharacterStaging row =
                FindCharacterStagingByCharacter(beat, canonical);
            _sceneComposerSelectedCharacterStagingId =
                row != null ? row.stagingId ?? string.Empty : string.Empty;
            Repaint();
        }

        public void ComposerDeleteSelectedDialogueBeatCharacterStaging()
        {
            ComposerResetSelectedDialogueBeatCharacterStaging();
        }

        public void ComposerResetSelectedDialogueBeatCharacterStaging()
        {
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            VnSceneComposerBeatCharacterStaging selected =
                GetSelectedCharacterStaging(beat);
            if (beat == null || selected == null || beat.characterStaging == null)
                return;

            RecordSceneComposerUndo("Reset VN Beat Character Staging");
            beat.characterStaging.Remove(selected);
            _sceneComposerSelectedCharacterStagingId = string.Empty;
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
            if (beat == null)
                throw new InvalidOperationException("No dialogue Beat is selected.");

            string canonical = ResolveSceneCharacterId(scene, characterId);
            if (string.IsNullOrEmpty(canonical))
                throw new ArgumentException("Character staging requires an existing Scene character.",
                    nameof(characterId));

            VnSceneComposerBeatCharacterStaging selected =
                FindCharacterStaging(beat, _sceneComposerSelectedCharacterStagingId);
            VnSceneComposerBeatCharacterStaging existingForCharacter =
                FindCharacterStagingByCharacter(beat, canonical);
            if (existingForCharacter != null)
                selected = existingForCharacter;

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
            if (selected == null)
                selected = EnsureCharacterStagingRow(beat, canonical);
            else if (!string.Equals(
                         selected.characterId, canonical,
                         StringComparison.OrdinalIgnoreCase))
                selected.characterId = canonical;
            _sceneComposerSelectedCharacterStagingId =
                selected.stagingId ?? string.Empty;
            int selectedSceneIndex = FindSceneCharacterIndex(scene, canonical);
            if (selectedSceneIndex >= 0)
                _sceneComposerSelectedCharacterIndex = selectedSceneIndex;

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
                beat.characterStaging =
                    new List<VnSceneComposerBeatCharacterStaging>();

            EditorGUILayout.LabelField(
                "Персонажи текущей реплики", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                "Здесь настраивается постановка персонажей только для текущей реплики. " +
                "Состав сцены меняется во вкладке «Персонажи».",
                EditorStyles.wordWrappedMiniLabel);

            string[] sceneCharacters =
                GetSceneComposerBeatTargetCharacterIds(scene);
            if (sceneCharacters.Length == 0)
            {
                EditorGUILayout.LabelField(
                    "В сцене пока нет персонажей.",
                    EditorStyles.wordWrappedMiniLabel);
                DrawCharacterStagingWarnings(scene, beat);
                return;
            }

            string currentCharacter =
                GetSelectedStagingCharacterId(scene, beat, sceneCharacters);
            EditorGUILayout.LabelField(
                "Персонажи и постановка · " +
                sceneCharacters.Length + " персонажа · " +
                CountLocalCharacterStagingRows(beat) + " изменён здесь",
                EditorStyles.miniLabel);

            for (int i = 0; i < sceneCharacters.Length; i++)
            {
                string characterId = sceneCharacters[i];
                VnSceneComposerBeatCharacterStaging local =
                    FindCharacterStagingByCharacter(beat, characterId);
                bool selected = string.Equals(
                    currentCharacter, characterId,
                    StringComparison.OrdinalIgnoreCase);
                string status = local != null
                    ? "[изменено здесь]"
                    : "[наследуется]";
                if (GUILayout.Button(
                        characterId + "    " + status,
                        selected
                            ? EditorStyles.miniButtonMid
                            : EditorStyles.miniButton))
                {
                    ComposerSelectDialogueBeatStagingCharacter(characterId);
                    currentCharacter = characterId;
                }
            }

            VnSceneComposerBeatCharacterStaging selectedRow =
                FindCharacterStagingByCharacter(beat, currentCharacter);
            VnSceneComposerBeatCharacterStaging display =
                selectedRow ?? new VnSceneComposerBeatCharacterStaging
                {
                    characterId = currentCharacter
                };

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Персонаж", currentCharacter);

            string[] visibilityLabels =
                { "Оставить предыдущее", "Показать", "Скрыть" };
            string[] positionLabels =
                { "Оставить предыдущее", "Слева", "Центр", "Справа", "Свободно" };

            string baseStateId =
                GetSceneComposerBaseStateId(scene, currentCharacter);
            string[] authoredStates = ComposerGetAuthoredStateIds(currentCharacter)
                .Where(id => !string.Equals(
                    id, baseStateId, StringComparison.Ordinal))
                .ToArray();
            string[] stateLabels = new string[authoredStates.Length + 2];
            stateLabels[0] = "Оставить предыдущую";
            stateLabels[1] = "По умолчанию / Базовая";
            int stateIndex = 0;
            if (display.hasStateOverride &&
                string.Equals(
                    display.stateId, baseStateId, StringComparison.Ordinal))
                stateIndex = 1;
            for (int i = 0; i < authoredStates.Length; i++)
            {
                stateLabels[i + 2] =
                    GetSceneComposerBeatStateDisplayName(
                        currentCharacter, authoredStates[i]);
                if (display.hasStateOverride &&
                    string.Equals(
                        display.stateId, authoredStates[i],
                        StringComparison.Ordinal))
                    stateIndex = i + 2;
            }

            EditorGUI.BeginChangeCheck();
            int nextVisibility = EditorGUILayout.Popup(
                "Видимость", (int)display.visibility, visibilityLabels);
            int nextPosition = EditorGUILayout.Popup(
                "Положение", (int)display.position, positionLabels);

            Vector2 nextCustom = display.customPositionOffset;
            if ((VnSceneComposerBeatCharacterPosition)nextPosition ==
                VnSceneComposerBeatCharacterPosition.Custom)
            {
                nextCustom.x =
                    EditorGUILayout.FloatField("Смещение X", nextCustom.x);
                nextCustom.y =
                    EditorGUILayout.FloatField("Смещение Y", nextCustom.y);
            }

            int nextStateIndex =
                EditorGUILayout.Popup(
                    "Эмоция / поза", stateIndex, stateLabels);
            bool nextHasState = nextStateIndex > 0;
            string nextState = nextStateIndex == 1
                ? baseStateId
                : (nextStateIndex > 1
                    ? authoredStates[nextStateIndex - 2]
                    : string.Empty);

            string[] effectLabels =
                { "Без анимации", "Акцент", "Подскок" };
            int currentEffect =
                display.effect == VnSceneComposerBeatEffect.Accent
                    ? 1
                    : (display.effect == VnSceneComposerBeatEffect.Hop
                        ? 2
                        : 0);
            int nextEffect = EditorGUILayout.Popup(
                "Анимация реплики", currentEffect, effectLabels);
            float nextStrength = display.effectStrength;
            float nextDuration = display.effectDuration;
            if (nextEffect != 0)
            {
                nextStrength =
                    EditorGUILayout.Slider(
                        "Сила", nextStrength, 0f, 100f);
                nextDuration = DrawSceneComposerDurationControl(
                    "Длительность", nextDuration, .01f);
            }

            float nextDelay = Mathf.Max(
                0f,
                EditorGUILayout.FloatField(
                    "Задержка, сек", display.delaySeconds));

            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    ComposerSetSelectedDialogueBeatCharacterStaging(
                        currentCharacter,
                        (VnSceneComposerBeatCharacterVisibility)
                            Mathf.Clamp(nextVisibility, 0, 2),
                        (VnSceneComposerBeatCharacterPosition)
                            Mathf.Clamp(nextPosition, 0, 4),
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
                    selectedRow =
                        FindCharacterStagingByCharacter(
                            beat, currentCharacter);
                }
                catch (Exception exception)
                {
                    SetSceneComposerStatus(
                        "Не удалось изменить постановку персонажа: " +
                        exception.Message,
                        MessageType.Warning);
                }
            }

            if (GUILayout.Button("Выбрать PNG позы / эмоции"))
            {
                string source = EditorUtility.OpenFilePanel(
                    "Выбрать PNG позы / эмоции",
                    string.Empty,
                    "png");
                if (!string.IsNullOrEmpty(source))
                {
                    try
                    {
                        ComposerImportSelectedDialogueBeatPosePng(source);
                        selectedRow =
                            FindCharacterStagingByCharacter(
                                beat, currentCharacter);
                    }
                    catch (Exception exception)
                    {
                        SetSceneComposerStatus(
                            "Не удалось импортировать позу: " +
                            exception.Message,
                            MessageType.Error);
                    }
                }
            }

            if (selectedRow != null &&
                GUILayout.Button("Сбросить изменения этой реплики"))
                ComposerResetSelectedDialogueBeatCharacterStaging();

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
                FindCharacterStaging(
                    beat, _sceneComposerSelectedCharacterStagingId);
            if (selected != null) return selected;

            VnSceneComposerScene scene = GetSelectedScene();
            if (scene != null && scene.characters != null &&
                _sceneComposerSelectedCharacterIndex >= 0 &&
                _sceneComposerSelectedCharacterIndex < scene.characters.Count)
            {
                VnSceneComposerCharacter character =
                    scene.characters[_sceneComposerSelectedCharacterIndex];
                string characterId =
                    VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(
                        character);
                return FindCharacterStagingByCharacter(beat, characterId);
            }

            return null;
        }

        private VnSceneComposerBeatCharacterStaging EnsureCharacterStagingRow(
            VnSceneComposerDialogueBeat beat, string characterId)
        {
            if (beat == null || string.IsNullOrWhiteSpace(characterId))
                return null;
            if (beat.characterStaging == null)
                beat.characterStaging =
                    new List<VnSceneComposerBeatCharacterStaging>();

            VnSceneComposerBeatCharacterStaging existing =
                FindCharacterStagingByCharacter(beat, characterId);
            if (existing != null)
            {
                _sceneComposerSelectedCharacterStagingId =
                    existing.stagingId ?? string.Empty;
                return existing;
            }

            var created = new VnSceneComposerBeatCharacterStaging
            {
                characterId = characterId
            };
            beat.characterStaging.Add(created);
            _sceneComposerSelectedCharacterStagingId =
                created.stagingId ?? string.Empty;
            return created;
        }

        private string GetSelectedStagingCharacterId(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            string[] sceneCharacters)
        {
            if (sceneCharacters == null || sceneCharacters.Length == 0)
                return string.Empty;

            if (scene != null && scene.characters != null &&
                _sceneComposerSelectedCharacterIndex >= 0 &&
                _sceneComposerSelectedCharacterIndex < scene.characters.Count)
            {
                string selected =
                    VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(
                        scene.characters[_sceneComposerSelectedCharacterIndex]);
                if (sceneCharacters.Any(id => string.Equals(
                        id, selected, StringComparison.OrdinalIgnoreCase)))
                    return selected;
            }

            VnSceneComposerBeatCharacterStaging row =
                FindCharacterStaging(
                    beat, _sceneComposerSelectedCharacterStagingId);
            if (row != null && sceneCharacters.Any(id => string.Equals(
                    id, row.characterId, StringComparison.OrdinalIgnoreCase)))
                return row.characterId ?? string.Empty;

            string first = sceneCharacters[0];
            int index = FindSceneCharacterIndex(scene, first);
            if (index >= 0)
                _sceneComposerSelectedCharacterIndex = index;
            return first;
        }

        private static int CountLocalCharacterStagingRows(
            VnSceneComposerDialogueBeat beat)
        {
            if (beat == null || beat.characterStaging == null)
                return 0;
            int count = 0;
            for (int i = 0; i < beat.characterStaging.Count; i++)
                if (beat.characterStaging[i] != null)
                    count++;
            return count;
        }

        private static int FindSceneCharacterIndex(
            VnSceneComposerScene scene, string characterId)
        {
            if (scene == null || scene.characters == null ||
                string.IsNullOrWhiteSpace(characterId))
                return -1;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter character = scene.characters[i];
                if (character == null) continue;
                string id =
                    VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(
                        character);
                if (string.Equals(
                    id, characterId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
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
