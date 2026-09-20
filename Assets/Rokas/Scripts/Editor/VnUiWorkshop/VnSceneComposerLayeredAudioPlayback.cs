using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal sealed class VnSceneComposerLayeredAudioPlayback : IDisposable
    {
        private sealed class ActiveCue
        {
            public string sceneId;
            public VnSceneComposerResolvedAdditionalAudioCue cue;
            public AudioSource source;
            public float elapsed;
            public bool fadingOut;
            public float fadeElapsed;
            public float fadeStartVolume;
        }

        private sealed class PendingCue
        {
            public string sceneId;
            public string beatId;
            public bool beatBound;
            public float remaining;
            public VnSceneComposerResolvedAdditionalAudioCue cue;
        }

        private readonly GameObject root;
        private readonly List<AudioSource> sourcePool = new List<AudioSource>();
        private readonly Dictionary<string, ActiveCue> active =
            new Dictionary<string, ActiveCue>(StringComparer.Ordinal);
        private readonly List<PendingCue> pending = new List<PendingCue>();
        private readonly Dictionary<string, int> startCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private string currentSceneId = string.Empty;
        private string currentBeatId = string.Empty;
        private bool disposed;

        public VnSceneComposerLayeredAudioPlayback()
        {
            root = new GameObject("ROKAS Scene Composer Layered Audio");
            root.hideFlags = HideFlags.HideAndDontSave;
        }

        public int ActiveSourceCount { get { return active.Count; } }
        public int StartCount { get; private set; }

        public bool IsActive(string cueId)
        {
            return !string.IsNullOrEmpty(cueId) && active.ContainsKey(cueId);
        }

        public int GetStartCount(string cueId)
        {
            return !string.IsNullOrEmpty(cueId) && startCounts.TryGetValue(cueId, out int count) ? count : 0;
        }

        public int GetSourceInstanceId(string cueId)
        {
            return !string.IsNullOrEmpty(cueId) &&
                   active.TryGetValue(cueId, out ActiveCue running) &&
                   running.source != null
                ? running.source.GetInstanceID()
                : 0;
        }

        public float GetElapsedSeconds(string cueId)
        {
            return !string.IsNullOrEmpty(cueId) &&
                   active.TryGetValue(cueId, out ActiveCue running)
                ? running.elapsed
                : 0f;
        }

        public float GetCurrentVolume(string cueId)
        {
            return !string.IsNullOrEmpty(cueId) &&
                   active.TryGetValue(cueId, out ActiveCue running) &&
                   running.source != null
                ? running.source.volume
                : 0f;
        }

        public void ResetSession()
        {
            if (disposed) return;
            StopAllImmediate();
            pending.Clear();
            currentSceneId = string.Empty;
            currentBeatId = string.Empty;
        }

        public void EnterScene(VnSceneComposerScene scene, string firstBeatId, bool play)
        {
            if (disposed) return;
            currentSceneId = scene != null ? scene.sceneId ?? string.Empty : string.Empty;
            currentBeatId = firstBeatId ?? string.Empty;
            CancelPendingForOtherScene();
            if (!play || scene == null) return;
            TriggerSceneStart(scene);
            TriggerBeatStart(scene, currentBeatId);
        }

        public void ExitCurrentScene(bool allowFade)
        {
            ExitCurrentScene(allowFade, false, string.Empty);
        }

        public void ExitCurrentScene(bool allowFade, bool inheritActive, string incomingSceneId)
        {
            if (disposed || string.IsNullOrEmpty(currentSceneId)) return;
            string leavingScene = currentSceneId;
            string nextScene = incomingSceneId ?? string.Empty;

            // Pending starts belong to the Scene that authored them. Only already-live
            // playback instances participate in cross-Scene continuity.
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (string.Equals(pending[i].sceneId, leavingScene, StringComparison.Ordinal))
                    pending.RemoveAt(i);
            }

            var ids = new List<string>();
            foreach (KeyValuePair<string, ActiveCue> pair in active)
            {
                ActiveCue running = pair.Value;
                if (!string.Equals(running.sceneId, leavingScene, StringComparison.Ordinal))
                    continue;

                if (inheritActive)
                {
                    // Preserve the exact AudioSource, logical elapsed position and fade state.
                    // Ownership moves forward so a later non-inheriting Scene can clean it up.
                    running.sceneId = nextScene;
                }
                else
                {
                    ids.Add(pair.Key);
                }
            }

            for (int i = 0; i < ids.Count; i++)
                StopCue(ids[i], allowFade);

            currentSceneId = inheritActive ? nextScene : string.Empty;
            currentBeatId = string.Empty;
        }

        public void EnterBeat(VnSceneComposerScene scene, string beatId, bool play)
        {
            if (disposed || scene == null) return;
            currentSceneId = scene.sceneId ?? string.Empty;
            currentBeatId = beatId ?? string.Empty;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingCue wait = pending[i];
                if (wait.beatBound &&
                    string.Equals(wait.sceneId, currentSceneId, StringComparison.Ordinal) &&
                    !string.Equals(wait.beatId, currentBeatId, StringComparison.Ordinal))
                    pending.RemoveAt(i);
            }

            if (scene.additionalAudioCues != null)
            {
                for (int i = 0; i < scene.additionalAudioCues.Count; i++)
                {
                    VnSceneComposerAdditionalAudioCue authored = scene.additionalAudioCues[i];
                    if (authored == null || !authored.enabled ||
                        authored.stopMode != VnSceneComposerAudioStopMode.BeatStart ||
                        !string.Equals(authored.stopBeatId ?? string.Empty, currentBeatId, StringComparison.Ordinal))
                        continue;
                    StopCue(authored.cueId, play);
                }
            }

            if (play) TriggerBeatStart(scene, currentBeatId);
        }

        public void Advance(float deltaSeconds)
        {
            if (disposed || deltaSeconds <= 0f) return;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingCue wait = pending[i];
                wait.remaining -= deltaSeconds;
                if (wait.remaining > .0001f) continue;
                pending.RemoveAt(i);
                if (!string.Equals(wait.sceneId, currentSceneId, StringComparison.Ordinal)) continue;
                if (wait.beatBound &&
                    !string.Equals(wait.beatId, currentBeatId, StringComparison.Ordinal)) continue;
                StartCue(wait.sceneId, wait.cue);
            }

            var release = new List<string>();
            foreach (KeyValuePair<string, ActiveCue> pair in active)
            {
                ActiveCue running = pair.Value;
                running.elapsed += deltaSeconds;

                if (running.fadingOut)
                {
                    running.fadeElapsed += deltaSeconds;
                    float duration = Mathf.Max(.0001f, running.cue.FadeOutSeconds);
                    float t = Mathf.Clamp01(running.fadeElapsed / duration);
                    running.source.volume = Mathf.Lerp(running.fadeStartVolume, 0f, t);
                    if (t >= 1f) release.Add(pair.Key);
                    continue;
                }

                if (running.cue.FadeInSeconds > .0001f &&
                    running.elapsed < running.cue.FadeInSeconds)
                {
                    running.source.volume = Mathf.Lerp(
                        0f, running.cue.Volume,
                        Mathf.Clamp01(running.elapsed / running.cue.FadeInSeconds));
                }
                else
                {
                    running.source.volume = running.cue.Volume;
                }

                if (!running.cue.Loop && running.cue.Clip != null &&
                    running.elapsed + .0001f >= running.cue.Clip.length)
                    release.Add(pair.Key);
            }

            for (int i = 0; i < release.Count; i++)
                ReleaseCue(release[i]);
        }

        public void Pause()
        {
            if (disposed) return;
            foreach (ActiveCue running in active.Values)
                if (running.source != null) running.source.Pause();
        }

        public void StopAllImmediate()
        {
            if (disposed) return;
            pending.Clear();
            var ids = new List<string>(active.Keys);
            for (int i = 0; i < ids.Count; i++) ReleaseCue(ids[i]);
        }

        private void TriggerSceneStart(VnSceneComposerScene scene)
        {
            if (scene.additionalAudioCues == null) return;
            for (int i = 0; i < scene.additionalAudioCues.Count; i++)
            {
                VnSceneComposerAdditionalAudioCue authored = scene.additionalAudioCues[i];
                if (authored == null || !authored.enabled ||
                    authored.trigger != VnSceneComposerAudioTrigger.SceneStart) continue;
                ScheduleOrStart(scene.sceneId, string.Empty, false,
                    VnSceneComposerAdditionalAudioResolver.Resolve(authored));
            }
        }

        private void TriggerBeatStart(VnSceneComposerScene scene, string beatId)
        {
            if (scene.additionalAudioCues == null) return;
            for (int i = 0; i < scene.additionalAudioCues.Count; i++)
            {
                VnSceneComposerAdditionalAudioCue authored = scene.additionalAudioCues[i];
                if (authored == null || !authored.enabled ||
                    authored.trigger != VnSceneComposerAudioTrigger.BeatStart ||
                    !string.Equals(authored.startBeatId ?? string.Empty, beatId ?? string.Empty,
                        StringComparison.Ordinal))
                    continue;
                ScheduleOrStart(scene.sceneId, beatId, true,
                    VnSceneComposerAdditionalAudioResolver.Resolve(authored));
            }
        }

        private void ScheduleOrStart(
            string sceneId, string beatId, bool beatBound,
            VnSceneComposerResolvedAdditionalAudioCue cue)
        {
            if (cue == null || !cue.Enabled || string.IsNullOrEmpty(cue.CueId) ||
                cue.Clip == null || cue.MissingAsset) return;
            if (active.ContainsKey(cue.CueId)) return;
            for (int i = 0; i < pending.Count; i++)
                if (string.Equals(pending[i].cue.CueId, cue.CueId, StringComparison.Ordinal)) return;

            if (cue.StartDelaySeconds > .0001f)
            {
                pending.Add(new PendingCue
                {
                    sceneId = sceneId ?? string.Empty,
                    beatId = beatId ?? string.Empty,
                    beatBound = beatBound,
                    remaining = cue.StartDelaySeconds,
                    cue = cue
                });
                return;
            }

            StartCue(sceneId, cue);
        }

        private void StartCue(string sceneId, VnSceneComposerResolvedAdditionalAudioCue cue)
        {
            if (cue == null || cue.Clip == null || cue.MissingAsset ||
                active.ContainsKey(cue.CueId)) return;

            AudioSource source = AcquireSource();
            source.clip = cue.Clip;
            source.loop = cue.Loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = cue.FadeInSeconds > .0001f ? 0f : cue.Volume;
            source.Play();

            active[cue.CueId] = new ActiveCue
            {
                sceneId = sceneId ?? string.Empty,
                cue = cue,
                source = source,
                elapsed = 0f
            };
            StartCount++;
            startCounts[cue.CueId] = GetStartCount(cue.CueId) + 1;
        }

        private void StopCue(string cueId, bool allowFade)
        {
            if (string.IsNullOrEmpty(cueId) || !active.TryGetValue(cueId, out ActiveCue running)) return;
            if (allowFade && running.cue.FadeOutSeconds > .0001f)
            {
                running.fadingOut = true;
                running.fadeElapsed = 0f;
                running.fadeStartVolume = running.source != null ? running.source.volume : running.cue.Volume;
                return;
            }
            ReleaseCue(cueId);
        }

        private void ReleaseCue(string cueId)
        {
            if (!active.TryGetValue(cueId, out ActiveCue running)) return;
            active.Remove(cueId);
            ReleaseSource(running.source);
        }

        private AudioSource AcquireSource()
        {
            for (int i = 0; i < sourcePool.Count; i++)
                if (sourcePool[i] != null && sourcePool[i].clip == null)
                    return sourcePool[i];

            AudioSource created = root.AddComponent<AudioSource>();
            created.playOnAwake = false;
            created.spatialBlend = 0f;
            created.volume = 0f;
            sourcePool.Add(created);
            return created;
        }

        private static void ReleaseSource(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.loop = false;
            source.volume = 0f;
        }

        private void CancelPendingForOtherScene()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
                if (!string.Equals(pending[i].sceneId, currentSceneId, StringComparison.Ordinal))
                    pending.RemoveAt(i);
        }

        public void Dispose()
        {
            if (disposed) return;
            StopAllImmediate();
            disposed = true;
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            sourcePool.Clear();
            pending.Clear();
            active.Clear();
        }
    }
}
