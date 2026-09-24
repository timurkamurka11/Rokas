using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [SerializeField] private int _sceneComposerMusicLibraryIndex;

        internal void ComposerEnsureSceneMusic(VnSceneComposerScene scene)
        {
            if (scene != null && scene.music == null) scene.music = new VnSceneComposerMusic();
        }

        public void ComposerSetSelectedSceneMusicTrack(AudioClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            string path = AssetDatabase.GetAssetPath(clip);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
                throw new ArgumentException("Music must be a Unity project AudioClip with a stable GUID.", nameof(clip));

            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureSceneMusic(scene);
            RecordSceneComposerUndo("Set VN Scene Music");
            scene.music.mode = VnSceneComposerMusicMode.Track;
            scene.music.assetGuid = guid;
            scene.music.displayName = clip.name ?? string.Empty;
            ComposerStopMusicPreview();
            RefreshSceneComposerMusicPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedSceneMusicAsset(VnSceneComposerAssetEntry entry)
        {
            if (entry == null || entry.purpose != VnSceneComposerAssetPurpose.Music)
                throw new ArgumentException("Selected Composer asset is not music.", nameof(entry));
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(entry.assetPath);
            if (clip == null) throw new InvalidOperationException("Music asset could not be loaded: " + entry.assetPath);
            ComposerSetSelectedSceneMusicTrack(clip);
        }

        public string ComposerAddExternalMusic(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new ArgumentException("Music file does not exist.", nameof(sourcePath));
            VnSceneComposerAssetOnboardResult result = VnSceneComposerAssetLibrary.Onboard(
                GetProjectRoot(), sourcePath, VnSceneComposerAssetPurpose.Music,
                Path.GetFileNameWithoutExtension(sourcePath), string.Empty, string.Empty);
            if (!result.Success || result.Entry == null)
                throw new InvalidOperationException(result.Error ?? "Could not add music.");
            ComposerSetSelectedSceneMusicAsset(result.Entry);
            return result.Entry.stableAssetId ?? string.Empty;
        }

        public void ComposerSetSelectedSceneMusicVolume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume))
                throw new ArgumentException("Music volume must be finite.", nameof(volume));
            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureSceneMusic(scene);
            RecordSceneComposerUndo("Change VN Scene Music Volume");
            scene.music.volume = Mathf.Clamp01(volume);
            RefreshSceneComposerMusicPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedSceneMusicLoop(bool loop)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureSceneMusic(scene);
            RecordSceneComposerUndo("Toggle VN Scene Music Loop");
            scene.music.loop = loop;
            RefreshSceneComposerMusicPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedSceneMusicFades(float fadeInSeconds, float fadeOutSeconds)
        {
            if (float.IsNaN(fadeInSeconds) || float.IsInfinity(fadeInSeconds) ||
                float.IsNaN(fadeOutSeconds) || float.IsInfinity(fadeOutSeconds))
                throw new ArgumentException("Music fade durations must be finite.");
            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureSceneMusic(scene);
            RecordSceneComposerUndo("Change VN Scene Music Fades");
            scene.music.fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
            scene.music.fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
            RefreshSceneComposerMusicPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerClearSelectedSceneMusic()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureSceneMusic(scene);
            RecordSceneComposerUndo("Clear VN Scene Music");
            scene.music = new VnSceneComposerMusic
            {
                mode = VnSceneComposerMusicMode.Silence,
                volume = 1f,
                loop = true
            };
            ComposerStopMusicPreview();
            RefreshSceneComposerMusicPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerKeepPreviousSceneMusic()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureSceneMusic(scene);
            RecordSceneComposerUndo("Keep Previous VN Scene Music");
            scene.music.mode = VnSceneComposerMusicMode.KeepPrevious;
            scene.music.assetGuid = string.Empty;
            scene.music.displayName = string.Empty;
            ComposerStopMusicPreview();
            RefreshSceneComposerMusicPlayback();
            MarkSceneComposerChanged();
        }

        internal AudioClip ComposerResolveSelectedSceneMusicClip()
        {
            int index = FindSceneIndex(_sceneComposerSelectedSceneId);
            if (index < 0) return null;
            return VnSceneComposerMusicResolver.Resolve(_sceneComposerProject, index).Clip;
        }

        public void ComposerPreviewSelectedSceneMusic()
        {
            int index = GetSelectedSceneIndexOrThrow();
            VnSceneComposerResolvedMusic resolved = VnSceneComposerMusicResolver.Resolve(_sceneComposerProject, index);
            if (!VnSceneComposerAudioAudition.TryPlay(resolved.Clip, resolved.Loop, out string error))
                SetSceneComposerStatus(error, MessageType.Warning);
            else
                SetSceneComposerStatus("Предпрослушивание музыки: " +
                    (string.IsNullOrWhiteSpace(resolved.DisplayName) ? resolved.Clip.name : resolved.DisplayName) + ".",
                    MessageType.Info);
        }

        public void ComposerStopMusicPreview()
        {
            VnSceneComposerAudioAudition.Stop();
        }

        internal string ComposerGetSelectedSceneMusicWarning()
        {
            int index = FindSceneIndex(_sceneComposerSelectedSceneId);
            if (index < 0) return string.Empty;
            VnSceneComposerResolvedMusic resolved = VnSceneComposerMusicResolver.Resolve(_sceneComposerProject, index);
            if (resolved.MissingAsset)
                return "Музыкальный файл не найден. Ссылка сохранена, остальные данные сцены не изменены.";
            return string.Empty;
        }

        private void RefreshSceneComposerMusicPlayback()
        {
            if (_sceneComposerPlayback == null) return;
            int selectedIndex = FindSceneIndex(_sceneComposerSelectedSceneId);
            if (selectedIndex == _sceneComposerPlayback.CurrentSceneIndex)
                _sceneComposerPlayback.RefreshCurrentMusic();
        }

        internal void TrySceneComposerMusicAction(Action action)
        {
            try
            {
                action();
                SetSceneComposerStatus("Музыка сцены обновлена.", MessageType.Info);
            }
            catch (Exception exception)
            {
                SetSceneComposerStatus(exception.Message, MessageType.Error);
            }
        }
    }
}
