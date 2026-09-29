using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnSceneComposerResolvedMusic
    {
        public VnSceneComposerMusicMode Mode { get; internal set; }
        public string AssetGuid { get; internal set; } = string.Empty;
        public string DisplayName { get; internal set; } = string.Empty;
        public AudioClip Clip { get; internal set; }
        public float Volume { get; internal set; } = 1f;
        public bool Loop { get; internal set; } = true;
        public float FadeInSeconds { get; internal set; }
        public float FadeOutSeconds { get; internal set; }
        public bool MissingAsset { get; internal set; }
        public bool Inherited { get; internal set; }
        public bool ExplicitSilence { get { return Mode == VnSceneComposerMusicMode.Silence; } }
    }

    public static class VnSceneComposerMusicResolver
    {
        public static VnSceneComposerResolvedMusic Resolve(VnSceneComposerProject project, int sceneIndex)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (project.scenes == null || sceneIndex < 0 || sceneIndex >= project.scenes.Count)
                throw new ArgumentOutOfRangeException(nameof(sceneIndex));

            bool inherited = false;
            for (int index = sceneIndex; index >= 0; index--)
            {
                VnSceneComposerScene scene = project.scenes[index];
                VnSceneComposerMusic music = scene != null ? scene.music : null;
                if (music == null) music = new VnSceneComposerMusic();

                if (music.mode == VnSceneComposerMusicMode.KeepPrevious)
                {
                    inherited = true;
                    continue;
                }

                if (music.mode == VnSceneComposerMusicMode.Silence)
                    return Silence(inherited);

                string guid = music.assetGuid ?? string.Empty;
                string path = string.IsNullOrEmpty(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
                AudioClip clip = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                return new VnSceneComposerResolvedMusic
                {
                    Mode = VnSceneComposerMusicMode.Track,
                    AssetGuid = guid,
                    DisplayName = music.displayName ?? string.Empty,
                    Clip = clip,
                    Volume = Mathf.Clamp01(music.volume),
                    Loop = music.loop,
                    FadeInSeconds = Mathf.Max(0f, music.fadeInSeconds),
                    FadeOutSeconds = Mathf.Max(0f, music.fadeOutSeconds),
                    MissingAsset = clip == null,
                    Inherited = inherited
                };
            }

            return Silence(inherited);
        }

        private static VnSceneComposerResolvedMusic Silence(bool inherited)
        {
            return new VnSceneComposerResolvedMusic
            {
                Mode = VnSceneComposerMusicMode.Silence,
                AssetGuid = string.Empty,
                Clip = null,
                Volume = 0f,
                Loop = false,
                FadeInSeconds = 0f,
                FadeOutSeconds = 0f,
                MissingAsset = false,
                Inherited = inherited
            };
        }
    }
}
