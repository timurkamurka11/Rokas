using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal sealed class VnSceneComposerResolvedAdditionalAudioCue
    {
        public string CueId { get; internal set; } = string.Empty;
        public string DisplayName { get; internal set; } = string.Empty;
        public string AssetGuid { get; internal set; } = string.Empty;
        public bool Enabled { get; internal set; }
        public VnSceneComposerAudioCategory Category { get; internal set; }
        public float Volume { get; internal set; } = 1f;
        public bool Loop { get; internal set; }
        public VnSceneComposerAudioTrigger Trigger { get; internal set; }
        public string StartBeatId { get; internal set; } = string.Empty;
        public float StartDelaySeconds { get; internal set; }
        public float FadeInSeconds { get; internal set; }
        public float FadeOutSeconds { get; internal set; }
        public VnSceneComposerAudioStopMode StopMode { get; internal set; }
        public string StopBeatId { get; internal set; } = string.Empty;
        public AudioClip Clip { get; internal set; }
        public bool MissingAsset { get; internal set; }
    }

    internal static class VnSceneComposerAdditionalAudioResolver
    {
        public static VnSceneComposerResolvedAdditionalAudioCue Resolve(VnSceneComposerAdditionalAudioCue cue)
        {
            if (cue == null) throw new ArgumentNullException(nameof(cue));
            string guid = cue.assetGuid ?? string.Empty;
            string path = string.IsNullOrWhiteSpace(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return new VnSceneComposerResolvedAdditionalAudioCue
            {
                CueId = cue.cueId ?? string.Empty,
                DisplayName = cue.displayName ?? string.Empty,
                AssetGuid = guid,
                Enabled = cue.enabled,
                Category = cue.category,
                Volume = Mathf.Clamp01(cue.volume),
                Loop = cue.loop,
                Trigger = cue.trigger,
                StartBeatId = cue.startBeatId ?? string.Empty,
                StartDelaySeconds = Mathf.Max(0f, cue.startDelaySeconds),
                FadeInSeconds = Mathf.Max(0f, cue.fadeInSeconds),
                FadeOutSeconds = Mathf.Max(0f, cue.fadeOutSeconds),
                StopMode = cue.stopMode,
                StopBeatId = cue.stopBeatId ?? string.Empty,
                Clip = clip,
                MissingAsset = !string.IsNullOrWhiteSpace(guid) && clip == null
            };
        }
    }
}
