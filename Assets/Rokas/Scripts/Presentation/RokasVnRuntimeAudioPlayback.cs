using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Presentation
{
    /// <summary>
    /// Runtime-only owner for authored VN BGM/SFX. It consumes project-owned
    /// package bindings and never resolves authoring GUIDs through UnityEditor.
    /// </summary>
    internal sealed class RokasVnRuntimeAudioPlayback : IDisposable
    {
        private sealed class MusicSlot
        {
            public AudioSource Source;
            public float LogicalVolume;
            public float StartVolume;
            public float TargetVolume;
            public float FadeElapsed;
            public float FadeDuration;
            public bool StopWhenDone;
        }

        private sealed class ActiveCue
        {
            public string SceneId = string.Empty;
            public RokasVnRuntimeAudioCueSnapshot Cue;
            public AudioSource Source;
            public float Elapsed;
            public bool FadingOut;
            public float FadeElapsed;
            public float FadeStartVolume;
        }

        private sealed class PendingCue
        {
            public string SceneId = string.Empty;
            public string BeatId = string.Empty;
            public bool BeatBound;
            public float Remaining;
            public RokasVnRuntimeAudioCueSnapshot Cue;
        }

        private readonly RokasVnRuntimeIntroPackage package;
        private readonly GameObject root;
        private readonly MusicSlot musicA;
        private readonly MusicSlot musicB;
        private readonly List<AudioSource> cueSources = new List<AudioSource>();
        private readonly Dictionary<string, ActiveCue> active =
            new Dictionary<string, ActiveCue>(StringComparer.Ordinal);
        private readonly List<PendingCue> pending = new List<PendingCue>();

        private MusicSlot currentMusic;
        private MusicSlot outgoingMusic;
        private RokasVnRuntimeMusicSnapshot currentMusicDefinition;
        private string currentSceneId = string.Empty;
        private string currentBeatId = string.Empty;
        private bool muted;
        private bool paused;
        private bool disposed;

        public RokasVnRuntimeAudioPlayback(
            Transform parent,
            RokasVnRuntimeIntroPackage runtimePackage)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            package = runtimePackage
                ? runtimePackage
                : throw new ArgumentNullException(nameof(runtimePackage));

            root = new GameObject("VnRuntimeAudio");
            root.transform.SetParent(parent, false);
            musicA = new MusicSlot { Source = CreateSource(root) };
            musicB = new MusicSlot { Source = CreateSource(root) };
        }

        public void EnterScene(
            RokasVnRuntimeSceneSnapshot scene,
            string firstBeatId)
        {
            if (disposed || scene == null) return;

            string incomingSceneId = scene.sceneId ?? string.Empty;
            if (!string.IsNullOrEmpty(currentSceneId))
                ExitAdditionalAudio(
                    scene.keepPreviousAdditionalAudio,
                    incomingSceneId);

            currentSceneId = incomingSceneId;
            currentBeatId = firstBeatId ?? string.Empty;
            CancelPendingForOtherScene();
            ApplyMusic(scene.music);

            TriggerSceneStart(scene);
            TriggerBeatStart(scene, currentBeatId);
        }

        public void EnterBeat(
            RokasVnRuntimeSceneSnapshot scene,
            string beatId)
        {
            if (disposed || scene == null) return;
            currentSceneId = scene.sceneId ?? string.Empty;
            currentBeatId = beatId ?? string.Empty;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingCue wait = pending[i];
                if (wait.BeatBound &&
                    string.Equals(
                        wait.SceneId,
                        currentSceneId,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        wait.BeatId,
                        currentBeatId,
                        StringComparison.Ordinal))
                    pending.RemoveAt(i);
            }

            if (scene.additionalAudioCues != null)
            {
                for (int i = 0; i < scene.additionalAudioCues.Count; i++)
                {
                    RokasVnRuntimeAudioCueSnapshot cue =
                        scene.additionalAudioCues[i];
                    if (cue == null || !cue.enabled ||
                        cue.stopMode != 2 ||
                        !string.Equals(
                            cue.stopBeatId ?? string.Empty,
                            currentBeatId,
                            StringComparison.Ordinal))
                        continue;
                    StopCue(cue.cueId, true);
                }
            }

            TriggerBeatStart(scene, currentBeatId);
        }

        public void Advance(float deltaSeconds)
        {
            if (disposed || deltaSeconds <= 0f) return;

            AdvanceMusicSlot(currentMusic, deltaSeconds);
            if (outgoingMusic != null &&
                !ReferenceEquals(outgoingMusic, currentMusic))
                AdvanceMusicSlot(outgoingMusic, deltaSeconds);

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingCue wait = pending[i];
                wait.Remaining -= deltaSeconds;
                if (wait.Remaining > .0001f) continue;
                pending.RemoveAt(i);
                if (!string.Equals(
                        wait.SceneId,
                        currentSceneId,
                        StringComparison.Ordinal))
                    continue;
                if (wait.BeatBound &&
                    !string.Equals(
                        wait.BeatId,
                        currentBeatId,
                        StringComparison.Ordinal))
                    continue;
                StartCue(wait.SceneId, wait.Cue);
            }

            var release = new List<string>();
            foreach (KeyValuePair<string, ActiveCue> pair in active)
            {
                ActiveCue running = pair.Value;
                running.Elapsed += deltaSeconds;

                if (running.FadingOut)
                {
                    running.FadeElapsed += deltaSeconds;
                    float duration =
                        Mathf.Max(.0001f, running.Cue.fadeOutSeconds);
                    float logical = Mathf.Lerp(
                        running.FadeStartVolume,
                        0f,
                        Mathf.Clamp01(running.FadeElapsed / duration));
                    SetOutputVolume(running.Source, logical);
                    if (running.FadeElapsed >= duration)
                        release.Add(pair.Key);
                    continue;
                }

                float volume = Mathf.Clamp01(running.Cue.volume);
                if (running.Cue.fadeInSeconds > .0001f &&
                    running.Elapsed < running.Cue.fadeInSeconds)
                {
                    volume *= Mathf.Clamp01(
                        running.Elapsed /
                        running.Cue.fadeInSeconds);
                }
                SetOutputVolume(running.Source, volume);

                if (!running.Cue.loop &&
                    running.Source != null &&
                    running.Source.clip != null &&
                    running.Elapsed + .0001f >=
                    running.Source.clip.length)
                    release.Add(pair.Key);
            }

            for (int i = 0; i < release.Count; i++)
                ReleaseCue(release[i]);
        }

        public void SetMuted(bool value)
        {
            if (disposed || muted == value) return;
            muted = value;
            RefreshAllVolumes();
        }

        public void SetPaused(bool value)
        {
            if (disposed || paused == value) return;
            paused = value;

            SetSourcePaused(musicA.Source, paused);
            SetSourcePaused(musicB.Source, paused);
            foreach (ActiveCue running in active.Values)
                SetSourcePaused(running.Source, paused);
        }

        private void ApplyMusic(RokasVnRuntimeMusicSnapshot music)
        {
            music = music ?? new RokasVnRuntimeMusicSnapshot();
            if (music.mode == 2) return; // KeepPrevious

            if (music.mode == 0)
            {
                BeginMusicOutgoing(
                    currentMusic,
                    currentMusicDefinition != null
                        ? currentMusicDefinition.fadeOutSeconds
                        : 0f);
                currentMusic = null;
                currentMusicDefinition = music;
                return;
            }

            AudioClip clip = ResolveClip(music.assetGuid);
            if (clip == null)
            {
                BeginMusicOutgoing(
                    currentMusic,
                    currentMusicDefinition != null
                        ? currentMusicDefinition.fadeOutSeconds
                        : 0f);
                currentMusic = null;
                currentMusicDefinition = music;
                return;
            }

            if (currentMusic != null &&
                currentMusic.Source != null &&
                currentMusic.Source.clip == clip)
            {
                currentMusicDefinition = music;
                currentMusic.Source.loop = music.loop;
                ConfigureMusicFade(
                    currentMusic,
                    currentMusic.LogicalVolume,
                    Mathf.Clamp01(music.volume),
                    0f,
                    false);
                return;
            }

            MusicSlot previous = currentMusic;
            RokasVnRuntimeMusicSnapshot previousDefinition =
                currentMusicDefinition;

            MusicSlot incoming =
                ReferenceEquals(previous, musicA)
                    ? musicB
                    : musicA;
            ClearMusicSlot(incoming);
            incoming.Source.clip = clip;
            incoming.Source.loop = music.loop;
            incoming.Source.playOnAwake = false;

            float target = Mathf.Clamp01(music.volume);
            float fadeIn = Mathf.Max(0f, music.fadeInSeconds);
            incoming.LogicalVolume =
                fadeIn > .0001f ? 0f : target;
            SetOutputVolume(
                incoming.Source,
                incoming.LogicalVolume);
            incoming.Source.Play();
            if (paused) incoming.Source.Pause();
            ConfigureMusicFade(
                incoming,
                incoming.LogicalVolume,
                target,
                fadeIn,
                false);

            currentMusic = incoming;
            currentMusicDefinition = music;

            if (previous != null)
            {
                outgoingMusic = previous;
                BeginMusicOutgoing(
                    previous,
                    previousDefinition != null
                        ? previousDefinition.fadeOutSeconds
                        : 0f);
            }
        }

        private void BeginMusicOutgoing(
            MusicSlot slot,
            float fadeSeconds)
        {
            if (slot == null || slot.Source == null ||
                slot.Source.clip == null)
                return;

            float duration = Mathf.Max(0f, fadeSeconds);
            if (duration <= .0001f)
            {
                ClearMusicSlot(slot);
                if (ReferenceEquals(outgoingMusic, slot))
                    outgoingMusic = null;
                return;
            }

            outgoingMusic = slot;
            ConfigureMusicFade(
                slot,
                slot.LogicalVolume,
                0f,
                duration,
                true);
        }

        private void ConfigureMusicFade(
            MusicSlot slot,
            float from,
            float to,
            float duration,
            bool stopWhenDone)
        {
            slot.StartVolume = Mathf.Clamp01(from);
            slot.TargetVolume = Mathf.Clamp01(to);
            slot.FadeElapsed = 0f;
            slot.FadeDuration = Mathf.Max(0f, duration);
            slot.StopWhenDone = stopWhenDone;
            if (slot.FadeDuration <= .0001f)
            {
                slot.LogicalVolume = slot.TargetVolume;
                SetOutputVolume(
                    slot.Source,
                    slot.LogicalVolume);
                if (slot.StopWhenDone &&
                    slot.TargetVolume <= .0001f)
                    ClearMusicSlot(slot);
            }
        }

        private void AdvanceMusicSlot(
            MusicSlot slot,
            float deltaSeconds)
        {
            if (slot == null || slot.Source == null ||
                slot.Source.clip == null ||
                slot.FadeDuration <= .0001f)
                return;

            slot.FadeElapsed += deltaSeconds;
            float t = Mathf.Clamp01(
                slot.FadeElapsed / slot.FadeDuration);
            slot.LogicalVolume = Mathf.Lerp(
                slot.StartVolume,
                slot.TargetVolume,
                t);
            SetOutputVolume(
                slot.Source,
                slot.LogicalVolume);

            if (t >= 1f)
            {
                slot.FadeDuration = 0f;
                if (slot.StopWhenDone &&
                    slot.TargetVolume <= .0001f)
                {
                    ClearMusicSlot(slot);
                    if (ReferenceEquals(outgoingMusic, slot))
                        outgoingMusic = null;
                }
            }
        }

        private void TriggerSceneStart(
            RokasVnRuntimeSceneSnapshot scene)
        {
            if (scene.additionalAudioCues == null) return;
            for (int i = 0; i < scene.additionalAudioCues.Count; i++)
            {
                RokasVnRuntimeAudioCueSnapshot cue =
                    scene.additionalAudioCues[i];
                if (cue == null || !cue.enabled ||
                    cue.trigger != 0)
                    continue;
                ScheduleOrStart(
                    scene.sceneId,
                    string.Empty,
                    false,
                    cue);
            }
        }

        private void TriggerBeatStart(
            RokasVnRuntimeSceneSnapshot scene,
            string beatId)
        {
            if (scene.additionalAudioCues == null) return;
            for (int i = 0; i < scene.additionalAudioCues.Count; i++)
            {
                RokasVnRuntimeAudioCueSnapshot cue =
                    scene.additionalAudioCues[i];
                if (cue == null || !cue.enabled ||
                    cue.trigger != 1 ||
                    !string.Equals(
                        cue.startBeatId ?? string.Empty,
                        beatId ?? string.Empty,
                        StringComparison.Ordinal))
                    continue;
                ScheduleOrStart(
                    scene.sceneId,
                    beatId,
                    true,
                    cue);
            }
        }

        private void ScheduleOrStart(
            string sceneId,
            string beatId,
            bool beatBound,
            RokasVnRuntimeAudioCueSnapshot cue)
        {
            if (cue == null ||
                string.IsNullOrWhiteSpace(cue.cueId) ||
                ResolveClip(cue.assetGuid) == null ||
                active.ContainsKey(cue.cueId))
                return;

            for (int i = 0; i < pending.Count; i++)
            {
                if (string.Equals(
                    pending[i].Cue.cueId,
                    cue.cueId,
                    StringComparison.Ordinal))
                    return;
            }

            if (cue.startDelaySeconds > .0001f)
            {
                pending.Add(new PendingCue
                {
                    SceneId = sceneId ?? string.Empty,
                    BeatId = beatId ?? string.Empty,
                    BeatBound = beatBound,
                    Remaining =
                        Mathf.Max(0f, cue.startDelaySeconds),
                    Cue = cue
                });
                return;
            }

            StartCue(sceneId, cue);
        }

        private void StartCue(
            string sceneId,
            RokasVnRuntimeAudioCueSnapshot cue)
        {
            if (cue == null ||
                active.ContainsKey(cue.cueId))
                return;

            AudioClip clip = ResolveClip(cue.assetGuid);
            if (clip == null) return;

            AudioSource source = AcquireCueSource();
            source.clip = clip;
            source.loop = cue.loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            float logical = cue.fadeInSeconds > .0001f
                ? 0f
                : Mathf.Clamp01(cue.volume);
            SetOutputVolume(source, logical);
            source.Play();
            if (paused) source.Pause();

            active[cue.cueId] = new ActiveCue
            {
                SceneId = sceneId ?? string.Empty,
                Cue = cue,
                Source = source,
                Elapsed = 0f
            };
        }

        private void ExitAdditionalAudio(
            bool inheritActive,
            string incomingSceneId)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (string.Equals(
                    pending[i].SceneId,
                    currentSceneId,
                    StringComparison.Ordinal))
                    pending.RemoveAt(i);
            }

            var stop = new List<string>();
            foreach (KeyValuePair<string, ActiveCue> pair in active)
            {
                ActiveCue running = pair.Value;
                if (!string.Equals(
                        running.SceneId,
                        currentSceneId,
                        StringComparison.Ordinal))
                    continue;

                if (inheritActive)
                    running.SceneId =
                        incomingSceneId ?? string.Empty;
                else
                    stop.Add(pair.Key);
            }

            for (int i = 0; i < stop.Count; i++)
                StopCue(stop[i], true);
        }

        private void StopCue(
            string cueId,
            bool allowFade)
        {
            ActiveCue running;
            if (string.IsNullOrEmpty(cueId) ||
                !active.TryGetValue(cueId, out running))
                return;

            if (allowFade &&
                running.Cue.fadeOutSeconds > .0001f)
            {
                running.FadingOut = true;
                running.FadeElapsed = 0f;
                running.FadeStartVolume =
                    Mathf.Clamp01(running.Cue.volume);
                return;
            }

            ReleaseCue(cueId);
        }

        private void ReleaseCue(string cueId)
        {
            ActiveCue running;
            if (!active.TryGetValue(cueId, out running))
                return;
            active.Remove(cueId);
            ReleaseCueSource(running.Source);
        }

        private AudioSource AcquireCueSource()
        {
            for (int i = 0; i < cueSources.Count; i++)
            {
                AudioSource source = cueSources[i];
                if (source != null && source.clip == null)
                    return source;
            }

            AudioSource created = CreateSource(root);
            cueSources.Add(created);
            return created;
        }

        private void ReleaseCueSource(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.loop = false;
            source.volume = 0f;
        }

        private AudioClip ResolveClip(string authoredKey)
        {
            RokasVnRuntimeAssetBinding binding;
            if (string.IsNullOrWhiteSpace(authoredKey) ||
                !package.TryGetAsset(
                    authoredKey,
                    out binding) ||
                binding == null)
                return null;
            return binding.asset as AudioClip;
        }

        private static AudioSource CreateSource(
            GameObject owner)
        {
            AudioSource source =
                owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private void RefreshAllVolumes()
        {
            RefreshMusicSlotVolume(musicA);
            RefreshMusicSlotVolume(musicB);
            foreach (ActiveCue running in active.Values)
            {
                float logical =
                    running.FadingOut
                        ? Mathf.Lerp(
                            running.FadeStartVolume,
                            0f,
                            Mathf.Clamp01(
                                running.FadeElapsed /
                                Mathf.Max(
                                    .0001f,
                                    running.Cue.fadeOutSeconds)))
                        : Mathf.Clamp01(
                            running.Cue.volume) *
                          (running.Cue.fadeInSeconds > .0001f
                              ? Mathf.Clamp01(
                                  running.Elapsed /
                                  running.Cue.fadeInSeconds)
                              : 1f);
                SetOutputVolume(
                    running.Source,
                    logical);
            }
        }

        private void RefreshMusicSlotVolume(MusicSlot slot)
        {
            if (slot == null || slot.Source == null) return;
            SetOutputVolume(
                slot.Source,
                slot.LogicalVolume);
        }

        private void SetOutputVolume(
            AudioSource source,
            float logicalVolume)
        {
            if (source == null) return;
            source.volume =
                muted ? 0f : Mathf.Clamp01(logicalVolume);
        }

        private static void SetSourcePaused(
            AudioSource source,
            bool value)
        {
            if (source == null || source.clip == null) return;
            if (value)
                source.Pause();
            else
                source.UnPause();
        }

        private void CancelPendingForOtherScene()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(
                    pending[i].SceneId,
                    currentSceneId,
                    StringComparison.Ordinal))
                    pending.RemoveAt(i);
            }
        }

        private void ClearMusicSlot(MusicSlot slot)
        {
            if (slot == null || slot.Source == null) return;
            slot.Source.Stop();
            slot.Source.clip = null;
            slot.Source.loop = false;
            slot.Source.volume = 0f;
            slot.LogicalVolume = 0f;
            slot.FadeDuration = 0f;
            slot.StopWhenDone = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            pending.Clear();
            var ids = new List<string>(active.Keys);
            for (int i = 0; i < ids.Count; i++)
                ReleaseCue(ids[i]);

            ClearMusicSlot(musicA);
            ClearMusicSlot(musicB);
            currentMusic = null;
            outgoingMusic = null;
            currentMusicDefinition = null;

            if (root)
            {
                root.SetActive(false);
                root.transform.SetParent(null, false);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
