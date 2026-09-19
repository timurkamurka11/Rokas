using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal sealed class VnSceneComposerMusicPlayback : IDisposable
    {
        private enum FadeState { None, FadeOut, FadeIn }

        private readonly GameObject root;
        private readonly AudioSource source;
        private VnSceneComposerResolvedMusic current = new VnSceneComposerResolvedMusic
        {
            Mode = VnSceneComposerMusicMode.Silence
        };
        private VnSceneComposerResolvedMusic pending;
        private FadeState fadeState;
        private float fadeElapsed;
        private float fadeDuration;
        private float fadeStartVolume;
        private bool started;
        private bool disposed;

        public VnSceneComposerMusicPlayback()
        {
            root = new GameObject("ROKAS Scene Composer BGM");
            root.hideFlags = HideFlags.HideAndDontSave;
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }

        public string CurrentAssetGuid { get { return current != null ? current.AssetGuid ?? string.Empty : string.Empty; } }
        public float ConfiguredVolume { get { return current != null ? current.Volume : 0f; } }
        public int StartCount { get; private set; }
        public int ActiveSourceCount { get { return started && source != null && source.clip != null ? 1 : 0; } }

        public void Apply(VnSceneComposerResolvedMusic resolved, bool play, bool forceRestart)
        {
            if (disposed) return;
            resolved = resolved ?? new VnSceneComposerResolvedMusic { Mode = VnSceneComposerMusicMode.Silence };

            bool sameTrack = !forceRestart &&
                             resolved.Mode == VnSceneComposerMusicMode.Track &&
                             current != null &&
                             current.Mode == VnSceneComposerMusicMode.Track &&
                             string.Equals(current.AssetGuid, resolved.AssetGuid, StringComparison.Ordinal) &&
                             resolved.Clip != null;

            if (sameTrack)
            {
                current = resolved;
                source.loop = resolved.Loop;
                if (fadeState == FadeState.None) source.volume = resolved.Volume;
                if (play && !started) StartCurrent();
                return;
            }

            if (!play)
            {
                SwitchNow(resolved, false);
                return;
            }

            if (started && source.clip != null && current != null &&
                current.FadeOutSeconds > .0001f)
            {
                pending = resolved;
                fadeState = FadeState.FadeOut;
                fadeElapsed = 0f;
                fadeDuration = current.FadeOutSeconds;
                fadeStartVolume = source.volume;
                return;
            }

            SwitchNow(resolved, true);
        }

        public void Advance(float deltaSeconds)
        {
            if (disposed || deltaSeconds <= 0f || fadeState == FadeState.None) return;
            fadeElapsed += deltaSeconds;
            float t = fadeDuration <= .0001f ? 1f : Mathf.Clamp01(fadeElapsed / fadeDuration);

            if (fadeState == FadeState.FadeOut)
            {
                source.volume = Mathf.Lerp(fadeStartVolume, 0f, t);
                if (t >= 1f)
                {
                    VnSceneComposerResolvedMusic next = pending;
                    pending = null;
                    SwitchNow(next, true);
                }
                return;
            }

            source.volume = Mathf.Lerp(0f, current != null ? current.Volume : 0f, t);
            if (t >= 1f) fadeState = FadeState.None;
        }

        public void Pause()
        {
            if (disposed || !started) return;
            source.Pause();
        }

        public void Restart(VnSceneComposerResolvedMusic resolved)
        {
            if (disposed) return;
            SwitchNow(resolved, true);
        }

        public void StopImmediate()
        {
            if (disposed) return;
            source.Stop();
            source.clip = null;
            source.volume = 0f;
            started = false;
            pending = null;
            fadeState = FadeState.None;
            current = new VnSceneComposerResolvedMusic { Mode = VnSceneComposerMusicMode.Silence };
        }

        private void SwitchNow(VnSceneComposerResolvedMusic resolved, bool play)
        {
            source.Stop();
            started = false;
            pending = null;
            fadeState = FadeState.None;
            current = resolved ?? new VnSceneComposerResolvedMusic { Mode = VnSceneComposerMusicMode.Silence };

            if (current.Mode != VnSceneComposerMusicMode.Track || current.Clip == null || current.MissingAsset)
            {
                source.clip = null;
                source.volume = 0f;
                return;
            }

            source.clip = current.Clip;
            source.loop = current.Loop;
            source.volume = current.FadeInSeconds > .0001f && play ? 0f : current.Volume;
            if (play) StartCurrent();
        }

        private void StartCurrent()
        {
            if (current == null || current.Clip == null) return;
            source.clip = current.Clip;
            source.loop = current.Loop;
            source.Play();
            started = true;
            StartCount++;
            if (current.FadeInSeconds > .0001f)
            {
                source.volume = 0f;
                fadeState = FadeState.FadeIn;
                fadeElapsed = 0f;
                fadeDuration = current.FadeInSeconds;
            }
            else
            {
                source.volume = current.Volume;
                fadeState = FadeState.None;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            StopImmediate();
            disposed = true;
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
