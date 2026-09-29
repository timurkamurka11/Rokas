using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [SerializeField] private string _sceneComposerSelectedAdditionalAudioCueId = string.Empty;
        [SerializeField] private int _sceneComposerAdditionalAudioLibraryIndex;

        public void ComposerSetSelectedSceneKeepPreviousAdditionalAudio(bool keepPrevious)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.keepPreviousAdditionalAudio == keepPrevious) return;
            RecordSceneComposerUndo("Toggle VN Keep Previous Sounds");
            scene.keepPreviousAdditionalAudio = keepPrevious;
            MarkSceneComposerChanged();
        }

        public void ComposerAddAdditionalAudioCue()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.additionalAudioCues == null)
                scene.additionalAudioCues = new System.Collections.Generic.List<VnSceneComposerAdditionalAudioCue>();

            RecordSceneComposerUndo("Add VN Additional Audio Cue");
            var cue = new VnSceneComposerAdditionalAudioCue
            {
                displayName = "Новый звук"
            };
            scene.additionalAudioCues.Add(cue);
            _sceneComposerSelectedAdditionalAudioCueId = cue.cueId;
            MarkSceneComposerChanged();
        }

        public void ComposerSelectAdditionalAudioCue(string cueId)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            if (FindAdditionalAudioCue(scene, cueId) == null)
                throw new ArgumentException("Additional audio cue is not part of the selected Scene.", nameof(cueId));
            _sceneComposerSelectedAdditionalAudioCueId = cueId ?? string.Empty;
            Repaint();
        }

        public void ComposerDuplicateSelectedAdditionalAudioCue()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerAdditionalAudioCue source = ComposerGetSelectedAdditionalAudioCue();
            if (source == null) return;
            RecordSceneComposerUndo("Duplicate VN Additional Audio Cue");
            VnSceneComposerAdditionalAudioCue copy =
                JsonUtility.FromJson<VnSceneComposerAdditionalAudioCue>(JsonUtility.ToJson(source));
            if (copy == null) throw new InvalidOperationException("Could not duplicate additional audio cue.");
            copy.cueId = VnSceneComposerScene.NewStableId();
            copy.displayName = string.IsNullOrWhiteSpace(copy.displayName)
                ? "Копия звука"
                : copy.displayName + " (копия)";
            scene.additionalAudioCues.Add(copy);
            _sceneComposerSelectedAdditionalAudioCueId = copy.cueId;
            MarkSceneComposerChanged();
        }

        public void ComposerDeleteSelectedAdditionalAudioCue()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerAdditionalAudioCue cue = ComposerGetSelectedAdditionalAudioCue();
            if (cue == null) return;
            RecordSceneComposerUndo("Delete VN Additional Audio Cue");
            scene.additionalAudioCues.Remove(cue);
            _sceneComposerSelectedAdditionalAudioCueId =
                scene.additionalAudioCues.Count > 0 && scene.additionalAudioCues[0] != null
                    ? scene.additionalAudioCues[0].cueId ?? string.Empty
                    : string.Empty;
            ComposerStopAdditionalAudioPreview();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedAdditionalAudioClip(AudioClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            string path = AssetDatabase.GetAssetPath(clip);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
                throw new ArgumentException("Additional audio must be a Unity project AudioClip with a stable GUID.", nameof(clip));

            VnSceneComposerAdditionalAudioCue cue = RequireSelectedAdditionalAudioCue();
            RecordSceneComposerUndo("Set VN Additional Audio Clip");
            cue.assetGuid = guid;
            cue.displayName = string.IsNullOrWhiteSpace(cue.displayName) ||
                              string.Equals(cue.displayName, "Новый звук", StringComparison.Ordinal)
                ? clip.name ?? string.Empty
                : cue.displayName;
            ComposerStopAdditionalAudioPreview();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedAdditionalAudioAsset(VnSceneComposerAssetEntry entry)
        {
            if (entry == null || entry.purpose != VnSceneComposerAssetPurpose.Music)
                throw new ArgumentException("Selected Composer asset is not an AudioClip asset.", nameof(entry));
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(entry.assetPath);
            if (clip == null) throw new InvalidOperationException("Audio asset could not be loaded: " + entry.assetPath);
            ComposerSetSelectedAdditionalAudioClip(clip);
        }

        public string ComposerAddExternalAdditionalAudio(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new ArgumentException("Audio file does not exist.", nameof(sourcePath));
            VnSceneComposerAssetOnboardResult result = VnSceneComposerAssetLibrary.Onboard(
                GetProjectRoot(), sourcePath, VnSceneComposerAssetPurpose.Music,
                Path.GetFileNameWithoutExtension(sourcePath), string.Empty, string.Empty);
            if (!result.Success || result.Entry == null)
                throw new InvalidOperationException(result.Error ?? "Could not add audio.");
            ComposerSetSelectedAdditionalAudioAsset(result.Entry);
            return result.Entry.stableAssetId ?? string.Empty;
        }

        public void ComposerPreviewSelectedAdditionalAudio()
        {
            VnSceneComposerAdditionalAudioCue cue = RequireSelectedAdditionalAudioCue();
            VnSceneComposerResolvedAdditionalAudioCue resolved =
                VnSceneComposerAdditionalAudioResolver.Resolve(cue);
            if (!VnSceneComposerAudioAudition.TryPlay(resolved.Clip, resolved.Loop, out string error))
                SetSceneComposerStatus(error, MessageType.Warning);
            else
                SetSceneComposerStatus("Предпрослушивание звука: " +
                    (string.IsNullOrWhiteSpace(resolved.DisplayName) ? resolved.Clip.name : resolved.DisplayName) + ".",
                    MessageType.Info);
        }

        public void ComposerStopAdditionalAudioPreview()
        {
            VnSceneComposerAudioAudition.Stop();
        }

        private VnSceneComposerAdditionalAudioCue ComposerGetSelectedAdditionalAudioCue()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null || scene.additionalAudioCues == null || scene.additionalAudioCues.Count == 0)
                return null;
            VnSceneComposerAdditionalAudioCue selected =
                FindAdditionalAudioCue(scene, _sceneComposerSelectedAdditionalAudioCueId);
            if (selected != null) return selected;
            for (int i = 0; i < scene.additionalAudioCues.Count; i++)
            {
                if (scene.additionalAudioCues[i] == null) continue;
                _sceneComposerSelectedAdditionalAudioCueId = scene.additionalAudioCues[i].cueId ?? string.Empty;
                return scene.additionalAudioCues[i];
            }
            return null;
        }

        private VnSceneComposerAdditionalAudioCue RequireSelectedAdditionalAudioCue()
        {
            VnSceneComposerAdditionalAudioCue cue = ComposerGetSelectedAdditionalAudioCue();
            if (cue == null) throw new InvalidOperationException("No additional audio cue is selected.");
            return cue;
        }

        private static VnSceneComposerAdditionalAudioCue FindAdditionalAudioCue(
            VnSceneComposerScene scene, string cueId)
        {
            if (scene == null || scene.additionalAudioCues == null || string.IsNullOrEmpty(cueId))
                return null;
            for (int i = 0; i < scene.additionalAudioCues.Count; i++)
            {
                VnSceneComposerAdditionalAudioCue cue = scene.additionalAudioCues[i];
                if (cue != null && string.Equals(cue.cueId, cueId, StringComparison.Ordinal))
                    return cue;
            }
            return null;
        }

        private static int FindBeatPopupIndex(VnSceneComposerScene scene, string beatId)
        {
            if (scene == null || scene.dialogueBeats == null) return 0;
            for (int i = 0; i < scene.dialogueBeats.Count; i++)
            {
                VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                if (beat != null && string.Equals(beat.beatId, beatId, StringComparison.Ordinal))
                    return i;
            }
            return 0;
        }

        private static string[] BuildBeatPopupLabels(VnSceneComposerScene scene)
        {
            int count = scene != null && scene.dialogueBeats != null ? scene.dialogueBeats.Count : 0;
            string[] labels = new string[Mathf.Max(1, count)];
            if (count == 0)
            {
                labels[0] = "Нет реплик";
                return labels;
            }
            for (int i = 0; i < count; i++)
            {
                VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                string speaker = beat != null && !string.IsNullOrWhiteSpace(beat.speaker)
                    ? beat.speaker
                    : "Без говорящего";
                labels[i] = (i + 1) + ". " + speaker;
            }
            return labels;
        }

        private static string BeatIdAt(VnSceneComposerScene scene, int index)
        {
            if (scene == null || scene.dialogueBeats == null || scene.dialogueBeats.Count == 0)
                return string.Empty;
            index = Mathf.Clamp(index, 0, scene.dialogueBeats.Count - 1);
            VnSceneComposerDialogueBeat beat = scene.dialogueBeats[index];
            return beat != null ? beat.beatId ?? string.Empty : string.Empty;
        }

        private void DrawSceneComposerAdditionalAudioInspector(VnSceneComposerScene scene)
        {
            if (scene.additionalAudioCues == null)
                scene.additionalAudioCues = new System.Collections.Generic.List<VnSceneComposerAdditionalAudioCue>();

            EditorGUILayout.LabelField("Звуки", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Дополнительные SFX / ambience / music layers работают независимо от основной музыки.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            bool keepPrevious = EditorGUILayout.Toggle(
                "Оставить предыдущие звуки", scene.keepPreviousAdditionalAudio);
            if (EditorGUI.EndChangeCheck())
                ComposerSetSelectedSceneKeepPreviousAdditionalAudio(keepPrevious);

            if (GUILayout.Button("+ Добавить звук")) ComposerAddAdditionalAudioCue();

            VnSceneComposerAdditionalAudioCue selected = ComposerGetSelectedAdditionalAudioCue();
            for (int i = 0; i < scene.additionalAudioCues.Count; i++)
            {
                VnSceneComposerAdditionalAudioCue cue = scene.additionalAudioCues[i];
                if (cue == null) continue;
                bool isSelected = selected != null &&
                                  string.Equals(selected.cueId, cue.cueId, StringComparison.Ordinal);
                string label = (i + 1) + ". " +
                               (string.IsNullOrWhiteSpace(cue.displayName) ? "Звук" : cue.displayName);
                if (GUILayout.Button(label, isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                {
                    ComposerSelectAdditionalAudioCue(cue.cueId);
                    selected = cue;
                }
            }

            selected = ComposerGetSelectedAdditionalAudioCue();
            if (selected == null) return;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Дублировать")) ComposerDuplicateSelectedAdditionalAudioCue();
            if (GUILayout.Button("Удалить")) ComposerDeleteSelectedAdditionalAudioCue();
            EditorGUILayout.EndHorizontal();

            selected = ComposerGetSelectedAdditionalAudioCue();
            if (selected == null) return;

            EditorGUI.BeginChangeCheck();
            string displayName = EditorGUILayout.TextField("Название", selected.displayName ?? string.Empty);
            bool enabled = EditorGUILayout.Toggle("Включен", selected.enabled);
            string[] categoryLabels = { "SFX", "Ambience", "Music Layer" };
            int categoryIndex = EditorGUILayout.Popup("Категория", (int)selected.category, categoryLabels);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Additional Audio Cue");
                selected.displayName = displayName ?? string.Empty;
                selected.enabled = enabled;
                selected.category = (VnSceneComposerAudioCategory)Mathf.Clamp(categoryIndex, 0, 2);
                MarkSceneComposerChanged();
            }

            VnSceneComposerResolvedAdditionalAudioCue resolved =
                VnSceneComposerAdditionalAudioResolver.Resolve(selected);
            EditorGUI.BeginChangeCheck();
            AudioClip nextClip = (AudioClip)EditorGUILayout.ObjectField(
                "AudioClip", resolved.Clip, typeof(AudioClip), false);
            if (EditorGUI.EndChangeCheck() && nextClip != null)
                ComposerSetSelectedAdditionalAudioClip(nextClip);

            VnSceneComposerAssetEntry[] audioAssets = VnSceneComposerAssetLibrary.FindByPurpose(
                GetProjectRoot(), VnSceneComposerAssetPurpose.Music);
            if (audioAssets.Length > 0)
            {
                _sceneComposerAdditionalAudioLibraryIndex = Mathf.Clamp(
                    _sceneComposerAdditionalAudioLibraryIndex, 0, audioAssets.Length - 1);
                string[] assetNames = new string[audioAssets.Length];
                for (int i = 0; i < audioAssets.Length; i++)
                    assetNames[i] = string.IsNullOrWhiteSpace(audioAssets[i].displayName)
                        ? "Аудио " + (i + 1)
                        : audioAssets[i].displayName;
                _sceneComposerAdditionalAudioLibraryIndex = EditorGUILayout.Popup(
                    "Из библиотеки", _sceneComposerAdditionalAudioLibraryIndex, assetNames);
                if (GUILayout.Button("Выбрать из библиотеки"))
                    ComposerSetSelectedAdditionalAudioAsset(audioAssets[_sceneComposerAdditionalAudioLibraryIndex]);
            }

            if (GUILayout.Button("Добавить аудиофайл"))
            {
                string path = EditorUtility.OpenFilePanel("Добавить звук", string.Empty, "mp3,wav");
                if (!string.IsNullOrEmpty(path))
                    TrySceneComposerMusicAction(() => ComposerAddExternalAdditionalAudio(path));
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Предпрослушать")) ComposerPreviewSelectedAdditionalAudio();
            if (GUILayout.Button("Стоп")) ComposerStopAdditionalAudioPreview();
            EditorGUILayout.EndHorizontal();

            string[] triggerLabels = { "Начало сцены", "Начало реплики" };
            string[] stopLabels = { "Естественно", "До конца сцены", "На реплике" };
            string[] beatLabels = BuildBeatPopupLabels(scene);

            EditorGUI.BeginChangeCheck();
            int triggerIndex = EditorGUILayout.Popup("Старт", (int)selected.trigger, triggerLabels);
            VnSceneComposerAudioTrigger trigger =
                (VnSceneComposerAudioTrigger)Mathf.Clamp(triggerIndex, 0, 1);
            string startBeatId = selected.startBeatId ?? string.Empty;
            if (trigger == VnSceneComposerAudioTrigger.BeatStart)
            {
                int startIndex = FindBeatPopupIndex(scene, startBeatId);
                startIndex = EditorGUILayout.Popup("Реплика старта", startIndex, beatLabels);
                startBeatId = BeatIdAt(scene, startIndex);
            }

            float delay = Mathf.Max(0f, EditorGUILayout.FloatField("Задержка старта", selected.startDelaySeconds));
            bool loop = EditorGUILayout.Toggle("Зациклить", selected.loop);
            float volume = EditorGUILayout.Slider("Громкость", selected.volume, 0f, 1f);
            float fadeIn = Mathf.Max(0f, EditorGUILayout.FloatField("Fade In", selected.fadeInSeconds));
            float fadeOut = Mathf.Max(0f, EditorGUILayout.FloatField("Fade Out", selected.fadeOutSeconds));

            int stopIndex = EditorGUILayout.Popup("Остановка", (int)selected.stopMode, stopLabels);
            VnSceneComposerAudioStopMode stopMode =
                (VnSceneComposerAudioStopMode)Mathf.Clamp(stopIndex, 0, 2);
            string stopBeatId = selected.stopBeatId ?? string.Empty;
            if (stopMode == VnSceneComposerAudioStopMode.BeatStart)
            {
                int beatIndex = FindBeatPopupIndex(scene, stopBeatId);
                beatIndex = EditorGUILayout.Popup("Реплика остановки", beatIndex, beatLabels);
                stopBeatId = BeatIdAt(scene, beatIndex);
            }

            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Additional Audio Cue");
                selected.trigger = trigger;
                selected.startBeatId = trigger == VnSceneComposerAudioTrigger.BeatStart
                    ? startBeatId : string.Empty;
                selected.startDelaySeconds = delay;
                selected.loop = loop;
                selected.volume = volume;
                selected.fadeInSeconds = fadeIn;
                selected.fadeOutSeconds = fadeOut;
                selected.stopMode = stopMode;
                selected.stopBeatId = stopMode == VnSceneComposerAudioStopMode.BeatStart
                    ? stopBeatId : string.Empty;
                MarkSceneComposerChanged();
            }

            resolved = VnSceneComposerAdditionalAudioResolver.Resolve(selected);
            if (string.IsNullOrWhiteSpace(selected.assetGuid))
                EditorGUILayout.HelpBox("Выберите AudioClip для этого звука.", MessageType.Info);
            else if (resolved.MissingAsset)
                EditorGUILayout.HelpBox(
                    "Аудиофайл не найден. Ссылка сохранена; остальные данные сцены не изменены.",
                    MessageType.Warning);
        }
    }
}
