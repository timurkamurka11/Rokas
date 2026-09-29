using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal sealed class VnSceneComposerMusicPlayback : IDisposable
    {
        private readonly GameObject root;
        private readonly AudioSource sourceA;
        private readonly AudioSource sourceB;

        private VnSceneComposerResolvedMusic current = Silence();
        private AudioSource currentSource;
        private AudioSource outgoingSource;

        private bool incomingFadeActive;
        private float incomingFadeElapsed;
        private float incomingFadeDuration;

        private bool outgoingFadeActive;
        private float outgoingFadeElapsed;
        private float outgoingFadeDuration;
        private float outgoingFadeStartVolume;

        private bool disposed;

        public VnSceneComposerMusicPlayback()
        {
            root = new GameObject("ROKAS Scene Composer BGM");
            root.hideFlags = HideFlags.HideAndDontSave;
            sourceA = CreateSource();
            sourceB = CreateSource();
        }

        public string CurrentAssetGuid
        {
            get { return current != null ? current.AssetGuid ?? string.Empty : string.Empty; }
        }

        public float ConfiguredVolume
        {
            get { return current != null ? current.Volume : 0f; }
        }

        public int StartCount { get; private set; }

        public int ActiveSourceCount
        {
            get
            {
                int count = 0;
                if (sourceA != null && sourceA.clip != null) count++;
                if (sourceB != null && sourceB.clip != null) count++;
                return count;
            }
        }

        public void Apply(VnSceneComposerResolvedMusic resolved, bool play, bool forceRestart)
        {
            if (disposed) return;
            resolved = resolved ?? Silence();

            bool validIncoming = IsPlayableTrack(resolved);
            bool sameTrack = !forceRestart &&
                             validIncoming &&
                             current != null &&
                             current.Mode == VnSceneComposerMusicMode.Track &&
                             string.Equals(current.AssetGuid, resolved.AssetGuid, StringComparison.Ordinal) &&
                             currentSource != null &&
                             currentSource.clip != null;

            if (sameTrack)
            {
                current = resolved;
                currentSource.loop = resolved.Loop;
                if (!incomingFadeActive)
                    currentSource.volume = resolved.Volume;
                if (play && !currentSource.isPlaying)
                    currentSource.UnPause();
                return;
            }

            if (forceRestart)
            {
                StopSourcesImmediate();
                current = resolved;
                if (validIncoming)
                    BeginIncoming(resolved, sourceA, play);
                return;
            }

            if (!play)
            {
                StopSourcesImmediate();
                current = resolved;
                if (validIncoming)
                    PrepareIncomingWithoutPlay(resolved, sourceA);
                return;
            }

            // A new authored target owns the logical current state immediately.
            // Any older third state is discarded so the player remains bounded to two sources.
            if (outgoingSource != null)
            {
                StopAndClear(outgoingSource);
                outgoingSource = null;
                outgoingFadeActive = false;
            }

            AudioSource previous = currentSource;
            VnSceneComposerResolvedMusic previousResolved = current;

            if (!validIncoming)
            {
                current = resolved;
                currentSource = null;
                incomingFadeActive = false;
                if (previous != null && previous.clip != null)
                    BeginOutgoing(previous, previousResolved);
                return;
            }

            AudioSource incoming = previous == sourceA ? sourceB : sourceA;
            StopAndClear(incoming);

            current = resolved;
            currentSource = incoming;
            BeginIncoming(resolved, incoming, true);

            if (previous != null && previous.clip != null)
                BeginOutgoing(previous, previousResolved);
        }

        public void Advance(float deltaSeconds)
        {
            if (disposed || deltaSeconds <= 0f) return;

            if (outgoingSource != null && outgoingFadeActive)
            {
                outgoingFadeElapsed += deltaSeconds;
                float t = outgoingFadeDuration <= .0001f
                    ? 1f
                    : Mathf.Clamp01(outgoingFadeElapsed / outgoingFadeDuration);
                outgoingSource.volume = Mathf.Lerp(outgoingFadeStartVolume, 0f, t);
                if (t >= 1f)
                {
                    StopAndClear(outgoingSource);
                    outgoingSource = null;
                    outgoingFadeActive = false;
                }
            }

            if (currentSource != null && incomingFadeActive)
            {
                incomingFadeElapsed += deltaSeconds;
                float t = incomingFadeDuration <= .0001f
                    ? 1f
                    : Mathf.Clamp01(incomingFadeElapsed / incomingFadeDuration);
                currentSource.volume = Mathf.Lerp(0f, current != null ? current.Volume : 0f, t);
                if (t >= 1f)
                {
                    currentSource.volume = current != null ? current.Volume : 0f;
                    incomingFadeActive = false;
                }
            }
        }

        public void Resume()
        {
            if (disposed) return;
            if (currentSource != null && currentSource.clip != null) currentSource.UnPause();
            if (outgoingSource != null && outgoingSource.clip != null) outgoingSource.UnPause();
        }

        public void Pause()
        {
            if (disposed) return;
            if (currentSource != null && currentSource.clip != null) currentSource.Pause();
            if (outgoingSource != null && outgoingSource.clip != null) outgoingSource.Pause();
        }

        public void Restart(VnSceneComposerResolvedMusic resolved)
        {
            if (disposed) return;
            StopSourcesImmediate();
            current = resolved ?? Silence();
            if (IsPlayableTrack(current))
                BeginIncoming(current, sourceA, true);
        }

        public void StopImmediate()
        {
            if (disposed) return;
            StopSourcesImmediate();
            current = Silence();
        }

        private AudioSource CreateSource()
        {
            AudioSource source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private void BeginIncoming(
            VnSceneComposerResolvedMusic resolved,
            AudioSource source,
            bool play)
        {
            currentSource = source;
            source.clip = resolved.Clip;
            source.loop = resolved.Loop;
            source.volume = play && resolved.FadeInSeconds > .0001f
                ? 0f
                : resolved.Volume;

            incomingFadeActive = play && resolved.FadeInSeconds > .0001f;
            incomingFadeElapsed = 0f;
            incomingFadeDuration = Mathf.Max(0f, resolved.FadeInSeconds);

            if (!play) return;
            source.Play();
            StartCount++;
        }

        private void PrepareIncomingWithoutPlay(
            VnSceneComposerResolvedMusic resolved,
            AudioSource source)
        {
            currentSource = source;
            source.clip = resolved.Clip;
            source.loop = resolved.Loop;
            source.volume = resolved.Volume;
            incomingFadeActive = false;
            incomingFadeElapsed = 0f;
            incomingFadeDuration = 0f;
        }

        private void BeginOutgoing(
            AudioSource source,
            VnSceneComposerResolvedMusic resolved)
        {
            float duration = resolved != null
                ? Mathf.Max(0f, resolved.FadeOutSeconds)
                : 0f;
            if (duration <= .0001f)
            {
                StopAndClear(source);
                return;
            }

            outgoingSource = source;
            outgoingFadeActive = true;
            outgoingFadeElapsed = 0f;
            outgoingFadeDuration = duration;
            outgoingFadeStartVolume = source.volume;
        }

        private void StopSourcesImmediate()
        {
            StopAndClear(sourceA);
            StopAndClear(sourceB);
            currentSource = null;
            outgoingSource = null;
            incomingFadeActive = false;
            outgoingFadeActive = false;
            incomingFadeElapsed = 0f;
            outgoingFadeElapsed = 0f;
            incomingFadeDuration = 0f;
            outgoingFadeDuration = 0f;
            outgoingFadeStartVolume = 0f;
        }

        private static void StopAndClear(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }

        private static bool IsPlayableTrack(VnSceneComposerResolvedMusic resolved)
        {
            return resolved != null &&
                   resolved.Mode == VnSceneComposerMusicMode.Track &&
                   resolved.Clip != null &&
                   !resolved.MissingAsset;
        }

        private static VnSceneComposerResolvedMusic Silence()
        {
            return new VnSceneComposerResolvedMusic
            {
                Mode = VnSceneComposerMusicMode.Silence
            };
        }

        public void Dispose()
        {
            if (disposed) return;
            StopSourcesImmediate();
            disposed = true;
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
