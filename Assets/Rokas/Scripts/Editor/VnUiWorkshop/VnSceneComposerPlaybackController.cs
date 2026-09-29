using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnSceneComposerSceneTransitionPhase
    {
        None,
        Cover,
        Hold,
        Reveal
    }

    public sealed class VnSceneComposerSceneTransitionOverlaySample
    {
        public bool Active { get; internal set; }
        public VnSceneComposerSceneTransitionType Mode { get; internal set; }
        public VnSceneComposerSceneTransitionPhase Phase { get; internal set; }
        public VnSceneComposerSceneTransitionDirection Direction { get; internal set; }
        public float Coverage { get; internal set; }
        public bool FullCover { get; internal set; }
    }

    public sealed class VnSceneComposerPlaybackFrame
    {
        internal VnSceneComposerPlaybackFrame(
            VnWorkshopPreviewFrame workshopFrame,
            VnWorkshopBackgroundTransitionSample backgroundTransition,
            Texture sourceBackground,
            Texture targetBackground)
            : this(workshopFrame, backgroundTransition, sourceBackground, targetBackground,
                VnSceneComposerMediaScaleMode.Fit, VnSceneComposerMediaScaleMode.Fit)
        {
        }

        internal VnSceneComposerPlaybackFrame(
            VnWorkshopPreviewFrame workshopFrame,
            VnWorkshopBackgroundTransitionSample backgroundTransition,
            Texture sourceBackground,
            Texture targetBackground,
            VnSceneComposerMediaScaleMode sourceScaleMode,
            VnSceneComposerMediaScaleMode targetScaleMode)
            : this(workshopFrame, backgroundTransition, sourceBackground, targetBackground,
                sourceScaleMode, targetScaleMode, new VnSceneComposerSceneTransitionOverlaySample(), true)
        {
        }

        internal VnSceneComposerPlaybackFrame(
            VnWorkshopPreviewFrame workshopFrame,
            VnWorkshopBackgroundTransitionSample backgroundTransition,
            Texture sourceBackground,
            Texture targetBackground,
            VnSceneComposerMediaScaleMode sourceScaleMode,
            VnSceneComposerMediaScaleMode targetScaleMode,
            VnSceneComposerSceneTransitionOverlaySample sceneTransitionOverlay,
            bool showDialogueUi)
        {
            WorkshopFrame = workshopFrame;
            ComposerBackgroundTransition = backgroundTransition;
            SourceBackground = sourceBackground;
            TargetBackground = targetBackground;
            SourceScaleMode = sourceScaleMode;
            TargetScaleMode = targetScaleMode;
            SceneTransitionOverlay = sceneTransitionOverlay ?? new VnSceneComposerSceneTransitionOverlaySample();
            ShowDialogueUi = showDialogueUi;
            ShowDialoguePanel = showDialogueUi;
            ShowCharacters = true;
            ShowDialogueText = showDialogueUi;
            Dialogue = workshopFrame != null ? workshopFrame.Dialogue : string.Empty;
            VnPresentationWorkshopPreviewRenderer.RegisterPlaybackFrame(this);
        }

        public VnWorkshopPreviewFrame WorkshopFrame { get; }
        public VnWorkshopBackgroundTransitionSample ComposerBackgroundTransition { get; }
        public Texture SourceBackground { get; }
        public Texture TargetBackground { get; }
        public VnSceneComposerMediaScaleMode SourceScaleMode { get; }
        public VnSceneComposerMediaScaleMode TargetScaleMode { get; }
        public VnSceneComposerSceneTransitionOverlaySample SceneTransitionOverlay { get; }
        public VnSceneComposerReplicaEffectSample ReplicaEffect { get; internal set; }
        public float ForegroundAlpha { get; internal set; } = 1f;
        public float TerminalFadeAlpha { get; internal set; }
        public bool ShowDialogueUi { get; }
        public VnSceneComposerPlaybackController Owner { get; internal set; }
        public bool ShowDialoguePanel { get; internal set; }
        public bool ShowCharacters { get; internal set; }
        public bool ShowDialogueText { get; internal set; }
        public string Dialogue { get; internal set; }
        public VnSceneComposerDialogueRevealSample DialogueReveal { get; internal set; }
        public VnWorkshopPreviewCharacter[] ComposerCharacters
        {
            get { return WorkshopFrame != null ? WorkshopFrame.ComposerCharacters : Array.Empty<VnWorkshopPreviewCharacter>(); }
        }
    }

    public sealed partial class VnSceneComposerPlaybackController : IDisposable
    {
        // Deterministic presentation staging after media readiness. These are not
        // decoder sleeps: the clock starts only after the requested media is genuinely ready.
        private const float SceneEntryPlaqueLeadSeconds = .08f;
        private const float SceneEntryDialogueLeadSeconds = .16f;

        private enum PlaybackScope
        {
            SingleScene,
            OrderedRange
        }

        private enum SceneBoundaryTransitionPhase
        {
            None,
            Cover,
            Hold,
            Reveal
        }

        private readonly VnSceneComposerProject project;
        private PlaybackScope scope;
        private int rangeStart;
        private int rangeEnd;
        private bool disposed;
        private VnSceneComposerImagePreview imagePreview;
        private VnSceneComposerVideoPreview videoPreview;
        private bool ownsVideoPreview;
        private VnSceneComposerGifPreview gifPreview;
        private VnSceneComposerImagePreview sourceImagePreview;
        private VnSceneComposerVideoPreview sourceVideoPreview;
        private bool ownsSourceVideoPreview;
        private bool retainedOutgoingVideoSource;
        private VnSceneComposerGifPreview sourceGifPreview;
        private Texture sourceMediaTexture;
        private VnSceneComposerScene currentSourceScene;
        private bool suppressCurrentBackgroundTransition;
        private bool suppressCurrentSceneEntryPresentation;
        private bool sceneEntryPresentationEnabled;
        private float sceneEntryPresentationElapsedSeconds;
        private string openedVideoSignature = string.Empty;
        private readonly VnSceneComposerMusicPlayback musicPlayback;
        private readonly VnSceneComposerLayeredAudioPlayback layeredAudioPlayback;
        private const float MaxVideoPreparationHoldSeconds = 8f;
        private int videoRecoveryAttemptCount;
        private float sceneTransitionVideoWaitElapsed;
        private string currentVideoWarning = string.Empty;
        private readonly HashSet<string> cancelledCharacterStagingIds =
            new HashSet<string>(StringComparer.Ordinal);
        private bool forceCompleteCurrentDialogueReveal;
        private bool terminalFadeActive;
        private float terminalFadeElapsed;
        private float terminalFadeDuration = 1.5f;
        private float terminalFadeAlpha;
        private bool completionSignalRaised;

        private SceneBoundaryTransitionPhase sceneBoundaryTransitionPhase;
        private int pendingSceneTransitionTargetIndex = -1;
        private float sceneTransitionPhaseElapsed;
        private float sceneTransitionDuration;
        private VnSceneComposerSceneTransitionType sceneTransitionType;
        private VnSceneComposerSceneTransitionDirection sceneTransitionDirection =
            VnSceneComposerSceneTransitionDirection.LeftToRight;
        private bool sceneTransitionPlayMedia;
        private bool sceneTransitionPreserveCompatibleVideoTimeline;
        private bool sceneTransitionHasSwapped;
        private bool sceneTransitionVideoPrepareRequested;
        private VnSceneComposerScene sceneTransitionSourceScene;
        private VnSceneComposerPlaybackFrame outgoingPresentation;
        private bool sceneTransitionAtomicNoneAwaitingVideo;
        private VnSceneComposerVideoPreview pendingAtomicNoneVideoPreview;
        private bool ownsPendingAtomicNoneVideoPreview;

        public VnSceneComposerPlaybackController(VnSceneComposerProject project)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            musicPlayback = new VnSceneComposerMusicPlayback();
            layeredAudioPlayback = new VnSceneComposerLayeredAudioPlayback();
            if (this.project.scenes == null) this.project.scenes = new List<VnSceneComposerScene>();
            CurrentSceneIndex = this.project.scenes.Count > 0 ? 0 : -1;
            rangeStart = CurrentSceneIndex;
            rangeEnd = CurrentSceneIndex;
            if (CurrentSceneIndex >= 0) ResetScene(CurrentSceneIndex, false);
        }

        public int CurrentSceneIndex { get; private set; }
        public int CurrentBeatIndex { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsTerminalFadeActive { get { return terminalFadeActive; } }
        public bool IsSequenceCompleted { get; private set; }
        public event Action VnSequenceCompleted;
        public float SceneElapsedSeconds { get; private set; }
        public float BeatElapsedSeconds { get; private set; }
        public float MediaTimeSeconds { get; private set; }
        public Texture CurrentMediaTexture { get; private set; }
        public VnSceneComposerTransitionSnapshot CurrentSnapshot { get; private set; }
        public VnSceneComposerPlaybackFrame CurrentFrame { get; private set; }
        public string CurrentMusicAssetGuid { get { return musicPlayback.CurrentAssetGuid; } }
        public float CurrentMusicVolume { get { return musicPlayback.ConfiguredVolume; } }
        public int MusicStartCount { get { return musicPlayback.StartCount; } }
        public int ActiveMusicSourceCount { get { return musicPlayback.ActiveSourceCount; } }
        public int ActiveAdditionalAudioSourceCount { get { return layeredAudioPlayback.ActiveSourceCount; } }
        public int AdditionalAudioStartCount { get { return layeredAudioPlayback.StartCount; } }
        public string CurrentVideoWarning { get { return currentVideoWarning ?? string.Empty; } }
        public bool IsAdditionalAudioCueActive(string cueId) { return layeredAudioPlayback.IsActive(cueId); }
        public int GetAdditionalAudioCueStartCount(string cueId) { return layeredAudioPlayback.GetStartCount(cueId); }
        public int GetAdditionalAudioCueSourceInstanceId(string cueId) { return layeredAudioPlayback.GetSourceInstanceId(cueId); }
        public float GetAdditionalAudioCueElapsedSeconds(string cueId) { return layeredAudioPlayback.GetElapsedSeconds(cueId); }
        public float GetAdditionalAudioCueCurrentVolume(string cueId) { return layeredAudioPlayback.GetCurrentVolume(cueId); }
        public bool IsSceneTransitionActive
        {
            get { return sceneTransitionAtomicNoneAwaitingVideo ||
                         sceneBoundaryTransitionPhase != SceneBoundaryTransitionPhase.None; }
        }
        public bool SceneTransitionInputLocked
        {
            get { return IsSceneTransitionActive || terminalFadeActive; }
        }
        public bool SceneTransitionHasSwapped { get { return sceneTransitionHasSwapped; } }
        public int PendingSceneTransitionTargetIndex { get { return pendingSceneTransitionTargetIndex; } }
        public int SceneTransitionStartCount { get; private set; }
        public bool RequiresTick { get { return IsPlaying || IsSceneTransitionActive; } }

        public void RefreshCurrentMusic()
        {
            if (CurrentSceneIndex < 0 || CurrentSceneIndex >= project.scenes.Count) return;
            musicPlayback.Apply(VnSceneComposerMusicResolver.Resolve(project, CurrentSceneIndex), IsPlaying, false);
        }

        public void RefreshCurrentFrame()
        {
            if (CurrentSceneIndex < 0 || CurrentSceneIndex >= project.scenes.Count) return;
            RebuildFrame(SceneElapsedSeconds, true);
        }

        public void PlayScene(int sceneIndex)
        {
            ResetTerminalCompletionState();
            RequireSceneIndex(sceneIndex);
            CancelSceneBoundaryTransition();
            scope = PlaybackScope.SingleScene;
            rangeStart = rangeEnd = sceneIndex;
            IsPlaying = true;
            layeredAudioPlayback.ResetSession();
            ResetScene(sceneIndex, true);
        }

        internal void PlaySceneFromNeutralStart(int sceneIndex)
        {
            ResetTerminalCompletionState();
            RequireSceneIndex(sceneIndex);
            CancelSceneBoundaryTransition();
            scope = PlaybackScope.SingleScene;
            rangeStart = rangeEnd = sceneIndex;
            IsPlaying = true;
            layeredAudioPlayback.ResetSession();
            ResetSceneFromNeutralStart(sceneIndex, true);
        }

        public void PlayFromHere(int sceneIndex)
        {
            PlayFromHere(sceneIndex, 0);
        }

        public void PlayFromHere(int sceneIndex, int beatIndex)
        {
            ResetTerminalCompletionState();
            RequireSceneIndex(sceneIndex);
            RequireBeatIndex(project.scenes[sceneIndex], beatIndex);
            CancelSceneBoundaryTransition();
            scope = PlaybackScope.OrderedRange;
            rangeStart = sceneIndex;
            rangeEnd = project.scenes.Count - 1;
            IsPlaying = true;
            layeredAudioPlayback.ResetSession();
            ResetScene(sceneIndex, true, beatIndex);
        }

        internal void PlayFromHereFromNeutralStart(int sceneIndex)
        {
            PlayFromHereFromNeutralStart(sceneIndex, 0);
        }

        internal void PlayFromHereFromNeutralStart(int sceneIndex, int beatIndex)
        {
            ResetTerminalCompletionState();
            RequireSceneIndex(sceneIndex);
            RequireBeatIndex(project.scenes[sceneIndex], beatIndex);
            CancelSceneBoundaryTransition();
            scope = PlaybackScope.OrderedRange;
            rangeStart = sceneIndex;
            rangeEnd = project.scenes.Count - 1;
            IsPlaying = true;
            layeredAudioPlayback.ResetSession();
            ResetSceneFromNeutralStart(sceneIndex, true, beatIndex);
        }

        public void PlayAll()
        {
            ResetTerminalCompletionState();
            CancelSceneBoundaryTransition();
            if (project.scenes.Count == 0)
            {
                StopEmpty();
                return;
            }
            scope = PlaybackScope.OrderedRange;
            rangeStart = 0;
            rangeEnd = project.scenes.Count - 1;
            IsPlaying = true;
            layeredAudioPlayback.ResetSession();
            ResetSceneFromNeutralStart(0, true);
        }

        public void Pause()
        {
            IsPlaying = false;
            if (videoPreview != null) videoPreview.Pause();
            musicPlayback.Pause();
            layeredAudioPlayback.Pause();
        }

        public void Restart()
        {
            if (CurrentSceneIndex < 0) return;
            ResetTerminalCompletionState();
            CancelSceneBoundaryTransition();
            IsPlaying = true;
            CurrentBeatIndex = 0;
            forceCompleteCurrentDialogueReveal = false;
            SceneElapsedSeconds = 0f;
            BeatElapsedSeconds = 0f;
            MediaTimeSeconds = 0f;
            sceneEntryPresentationEnabled = true;
            sceneEntryPresentationElapsedSeconds = 0f;
            suppressCurrentSceneEntryPresentation = false;
            cancelledCharacterStagingIds.Clear();
            if (gifPreview != null) gifPreview.Restart();
            if (videoPreview != null)
            {
                videoPreview.Restart();
                videoPreview.Play();
            }
            musicPlayback.Restart(VnSceneComposerMusicResolver.Resolve(project, CurrentSceneIndex));
            layeredAudioPlayback.ResetSession();
            VnSceneComposerScene restartScene = project.scenes[CurrentSceneIndex];
            VnSceneComposerDialogueBeat restartBeat = ResolveBeat(restartScene, 0);
            layeredAudioPlayback.EnterScene(
                restartScene, restartBeat != null ? restartBeat.beatId : string.Empty, true);
            RefreshMediaTexture();
            RebuildFrame(0f);
        }

        public void Previous()
        {
            if (disposed || IsMenuOpen || project.scenes.Count == 0 || IsSceneTransitionActive) return;
            int target = Mathf.Clamp(CurrentSceneIndex - 1, 0, project.scenes.Count - 1);
            if (target == CurrentSceneIndex) return;
            BeginSceneBoundaryTransition(target, IsPlaying, false);
        }

        public void Next()
        {
            if (disposed || IsMenuOpen || project.scenes.Count == 0 || IsSceneTransitionActive) return;
            int target = Mathf.Clamp(CurrentSceneIndex + 1, 0, project.scenes.Count - 1);
            if (target == CurrentSceneIndex)
            {
                IsPlaying = false;
                if (videoPreview != null) videoPreview.Pause();
                musicPlayback.Pause();
                layeredAudioPlayback.StopAllImmediate();
                return;
            }
            BeginSceneBoundaryTransition(target, IsPlaying, false);
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds),
                    "Scene Composer playback delta must be finite and non-negative.");
            UiElapsedSeconds += deltaSeconds;
            InputTick++;
            if (disposed || IsMenuOpen) return;
            if (terminalFadeActive)
            {
                AdvanceTerminalFade(deltaSeconds);
                return;
            }
            if (IsSceneTransitionActive)
            {
                AdvanceSceneBoundaryTransition(deltaSeconds);
                return;
            }
            if (!IsPlaying || CurrentSceneIndex < 0) return;

            SceneElapsedSeconds += deltaSeconds;
            BeatElapsedSeconds += deltaSeconds;
            MediaTimeSeconds += deltaSeconds;
            musicPlayback.Advance(deltaSeconds);
            layeredAudioPlayback.Advance(deltaSeconds);
            TryRecoverCurrentVideoPlayback();
            if (gifPreview != null) gifPreview.Advance(deltaSeconds);
            if (videoPreview != null && string.IsNullOrEmpty(videoPreview.warning) &&
                !videoPreview.IsPlaying && !videoPreview.IsPreparing)
                videoPreview.Play();
            RefreshMediaTexture();
            if (sceneEntryPresentationEnabled &&
                CurrentBeatIndex == 0 &&
                IsCurrentVideoPresentationReady())
            {
                sceneEntryPresentationElapsedSeconds += deltaSeconds;
            }

            VnSceneComposerScene scene = project.scenes[CurrentSceneIndex];
            VnSceneComposerDialogueBeat activeBeat = ResolveBeat(scene, CurrentBeatIndex);
            VnSceneComposerPreviewTimingPlan timing =
                VnSceneComposerTransitionSampler.ResolveTiming(project, scene, activeBeat);
            float duration = ResolveScenePreviewDuration(timing);
            RebuildFrame(SceneElapsedSeconds, true);

            if (!timing.usesPreviewAutoDuration || BeatElapsedSeconds < duration) return;

            if (CurrentBeatIndex + 1 < BeatCount(scene))
            {
                AdvanceDialogue();
                return;
            }

            if (scene.isTerminal)
            {
                BeginTerminalFade(scene);
                return;
            }

            if (scope == PlaybackScope.OrderedRange && CurrentSceneIndex < rangeEnd)
            {
                float sequenceGap = Mathf.Max(0f, timing.sequenceGap);
                if (BeatElapsedSeconds + .00001f < duration + sequenceGap)
                {
                    RebuildFrame(1f);
                    return;
                }
                BeginSceneBoundaryTransition(CurrentSceneIndex + 1, true, true);
                return;
            }

            IsPlaying = false;
            if (videoPreview != null) videoPreview.Pause();
            musicPlayback.Pause();
            layeredAudioPlayback.StopAllImmediate();
            RebuildFrame(1f);
        }

        public void AdvanceDialogue()
        {
            if (disposed || IsMenuOpen || CurrentSceneIndex < 0 || IsSceneTransitionActive) return;

            VnSceneComposerScene scene = project.scenes[CurrentSceneIndex];
            VnSceneComposerDialogueBeat activeBeat =
                ResolveBeat(scene, CurrentBeatIndex);
            VnPresentationWorkshopPreset activePresentation =
                VnSceneComposerComposition.ResolvePresentation(project, scene);
            VnWorkshopTypewriterValues activeTypewriter =
                VnPresentationWorkshopVn10Resolver.ResolveTypewriter(activePresentation);
            VnSceneComposerDialogueRevealSample activeReveal =
                VnSceneComposerDialogueReveal.Sample(
                    activeBeat.text ?? string.Empty,
                    GetEffectiveDialogueElapsedSeconds(),
                    activeTypewriter,
                    forceCompleteCurrentDialogueReveal);

            if (!activeReveal.Complete)
            {
                forceCompleteCurrentDialogueReveal = true;
                RebuildFrame(SceneElapsedSeconds, true);
                return;
            }

            int beatCount = BeatCount(scene);
            if (CurrentBeatIndex + 1 < beatCount)
            {
                VnSceneComposerDialogueBeat outgoingBeat = ResolveBeat(scene, CurrentBeatIndex);
                VnSceneComposerBeatCharacterStagingResolver.CollectPendingStagingIds(
                    outgoingBeat, BeatElapsedSeconds,
                    cancelledCharacterStagingIds);
                suppressCurrentSceneEntryPresentation = true;
                sceneEntryPresentationEnabled = false;
                forceCompleteCurrentDialogueReveal = false;
                CurrentBeatIndex++;
                BeatElapsedSeconds = 0f;
                VnSceneComposerDialogueBeat enteredBeat = ResolveBeat(scene, CurrentBeatIndex);
                layeredAudioPlayback.EnterBeat(
                    scene, enteredBeat != null ? enteredBeat.beatId : string.Empty, IsPlaying);
                RebuildFrame(SceneElapsedSeconds, true);
                return;
            }

            if (scene.isTerminal)
            {
                BeginTerminalFade(scene);
                return;
            }

            if (scope == PlaybackScope.OrderedRange && CurrentSceneIndex < rangeEnd)
            {
                BeginSceneBoundaryTransition(CurrentSceneIndex + 1, true, true);
                return;
            }

            IsPlaying = false;
            if (videoPreview != null) videoPreview.Pause();
            musicPlayback.Pause();
            layeredAudioPlayback.StopAllImmediate();
            RebuildFrame(1f);
        }

        public void PreviousDialogue()
        {
            if (disposed || IsMenuOpen || CurrentSceneIndex < 0 || IsSceneTransitionActive || CurrentBeatIndex <= 0) return;
            suppressCurrentSceneEntryPresentation = true;
            sceneEntryPresentationEnabled = false;
            forceCompleteCurrentDialogueReveal = false;
            CurrentBeatIndex--;
            BeatElapsedSeconds = 0f;
            cancelledCharacterStagingIds.Clear();
            RebuildFrame(SceneElapsedSeconds, true);
        }

        public void Dispose()
        {
            if (disposed) return;
            ReleaseOwnedMute();
            IsMenuOpen = false;
            disposed = true;
            IsPlaying = false;
            ReleaseMedia();
            ReleaseSourceMedia();
            musicPlayback.Dispose();
            layeredAudioPlayback.Dispose();
            currentSourceScene = null;
            suppressCurrentBackgroundTransition = false;
            suppressCurrentSceneEntryPresentation = false;
            sceneEntryPresentationEnabled = false;
            sceneEntryPresentationElapsedSeconds = 0f;
            forceCompleteCurrentDialogueReveal = false;
            terminalFadeActive = false;
            terminalFadeElapsed = 0f;
            terminalFadeAlpha = 0f;
            IsSequenceCompleted = false;
            completionSignalRaised = false;
            CancelSceneBoundaryTransition();
            CurrentMediaTexture = null;
            CurrentSnapshot = null;
            CurrentFrame = null;
        }

        private void ResetScene(int sceneIndex, bool playMedia)
        {
            ResetScene(sceneIndex, playMedia, 0);
        }

        private void ResetScene(int sceneIndex, bool playMedia, int initialBeatIndex)
        {
            ResetScene(sceneIndex, playMedia, ResolveSourceScene(sceneIndex), false,
                false, false, initialBeatIndex > 0, true, true, false, initialBeatIndex);
        }

        private void ResetSceneFromNeutralStart(
            int sceneIndex, bool playMedia, int initialBeatIndex = 0)
        {
            ResetScene(sceneIndex, playMedia, CreatePreviewBaseline(), true, false, true,
                initialBeatIndex > 0, true, true, true, initialBeatIndex);
        }

        private void ResetScene(int sceneIndex, bool playMedia, VnSceneComposerScene sourceScene,
            bool suppressBackgroundTransition, bool preserveCompatibleVideoTimeline = false,
            bool forceMusicRestart = false, bool suppressSceneEntryPresentation = false,
            bool applyMusic = true, bool applyAdditionalAudio = true, bool forceVideoRestart = false,
            int initialBeatIndex = 0)
        {
            RequireSceneIndex(sceneIndex);
            VnSceneComposerScene targetScene = project.scenes[sceneIndex];
            RequireBeatIndex(targetScene, initialBeatIndex);
            videoRecoveryAttemptCount = 0;
            currentVideoWarning = string.Empty;
            bool sameVideoBoundary = preserveCompatibleVideoTimeline &&
                                     sceneIndex != CurrentSceneIndex &&
                                     CanReuseVideoPreview(targetScene);
            bool retainOutgoingVideo = preserveCompatibleVideoTimeline &&
                                       sceneIndex != CurrentSceneIndex &&
                                       !sameVideoBoundary &&
                                       CanRetainCurrentVideoAsOutgoing(targetScene);
            bool reuseVideoPreview = (sceneIndex == CurrentSceneIndex || sameVideoBoundary) &&
                                     CanReuseVideoPreview(targetScene);
            float continuedMediaTime = sameVideoBoundary ? MediaTimeSeconds : 0f;
            Texture continuedVideoTexture = sameVideoBoundary ? CurrentMediaTexture : null;

            if (applyAdditionalAudio && CurrentSceneIndex != sceneIndex)
            {
                bool inheritAdditionalAudio =
                    targetScene != null &&
                    targetScene.keepPreviousAdditionalAudio &&
                    sceneIndex > CurrentSceneIndex;
                layeredAudioPlayback.ExitCurrentScene(
                    playMedia && IsPlaying,
                    inheritAdditionalAudio,
                    inheritAdditionalAudio ? targetScene.sceneId : string.Empty);
            }

            ReleaseSourceMedia();
            if (retainOutgoingVideo) PromoteCurrentVideoToSource();
            else if (!reuseVideoPreview) ReleaseMedia();

            currentSourceScene = sourceScene ?? CreatePreviewBaseline();
            suppressCurrentBackgroundTransition = suppressBackgroundTransition;
            suppressCurrentSceneEntryPresentation = suppressSceneEntryPresentation;
            sceneEntryPresentationEnabled =
                !suppressSceneEntryPresentation && initialBeatIndex == 0;
            sceneEntryPresentationElapsedSeconds = 0f;
            CurrentSceneIndex = sceneIndex;
            CurrentBeatIndex = initialBeatIndex;
            forceCompleteCurrentDialogueReveal = false;
            cancelledCharacterStagingIds.Clear();
            SceneElapsedSeconds = 0f;
            BeatElapsedSeconds = 0f;
            MediaTimeSeconds = continuedMediaTime;
            if (applyMusic)
                musicPlayback.Apply(
                    VnSceneComposerMusicResolver.Resolve(project, sceneIndex), playMedia, forceMusicRestart);
            if (applyAdditionalAudio)
            {
                VnSceneComposerDialogueBeat firstAudioBeat =
                    ResolveBeat(targetScene, CurrentBeatIndex);
                layeredAudioPlayback.EnterScene(
                    targetScene,
                    firstAudioBeat != null ? firstAudioBeat.beatId : string.Empty,
                    playMedia && IsPlaying);
            }
            if (sameVideoBoundary) sourceMediaTexture = continuedVideoTexture;
            else if (!retainOutgoingVideo) OpenSourceMedia(currentSourceScene);
            if (!reuseVideoPreview) OpenMedia(targetScene);
            if (playMedia && videoPreview != null)
            {
                if (forceVideoRestart && videoPreview.IsPrepared)
                    videoPreview.Restart();
                if (!sameVideoBoundary || !videoPreview.IsPlaying)
                    videoPreview.Play();
            }
            RefreshSourceMediaTexture();
            if (sameVideoBoundary) sourceMediaTexture = continuedVideoTexture;
            RefreshMediaTexture();
            RebuildFrame(0f);
        }

        private void RebuildFrame(float progressOrElapsedSeconds, bool useElapsedSeconds = false)
        {
            if (CurrentSceneIndex < 0)
            {
                CurrentSnapshot = null;
                CurrentFrame = null;
                return;
            }

            // Cover owns only the curtain. Keep the exact last visible composition,
            // including the current Beat's staging/pose, until the safe swap point.
            if (!sceneTransitionHasSwapped && outgoingPresentation != null && IsSceneTransitionActive)
            {
                var frozen = outgoingPresentation;
                CurrentFrame = new VnSceneComposerPlaybackFrame(
                    frozen.WorkshopFrame, frozen.ComposerBackgroundTransition,
                    frozen.SourceBackground, frozen.TargetBackground,
                    frozen.SourceScaleMode, frozen.TargetScaleMode,
                    BuildSceneBoundaryOverlaySample(), false);
                CurrentFrame.Owner = this;
                CurrentFrame.ShowDialoguePanel = frozen.ShowDialoguePanel;
                CurrentFrame.ShowCharacters = frozen.ShowCharacters;
                CurrentFrame.ShowDialogueText = frozen.ShowDialogueText;
                CurrentFrame.Dialogue = frozen.Dialogue;
                CurrentFrame.DialogueReveal = frozen.DialogueReveal;
                CurrentFrame.ReplicaEffect = VnSceneComposerReplicaEffects.Sample(null, 0f);
                CurrentFrame.ForegroundAlpha = SampleSceneBoundaryForegroundAlpha();
                CurrentFrame.TerminalFadeAlpha = terminalFadeAlpha;
                return;
            }

            VnSceneComposerScene targetScene = project.scenes[CurrentSceneIndex];
            VnSceneComposerScene sourceScene = currentSourceScene ?? ResolveSourceScene(CurrentSceneIndex);
            VnSceneComposerDialogueBeat targetBeat = ResolveBeat(targetScene, CurrentBeatIndex);
            VnSceneComposerDialogueBeat previousBeat = CurrentBeatIndex > 0
                ? ResolveBeat(targetScene, CurrentBeatIndex - 1)
                : ResolveFirstBeat(sourceScene);
            float progress = useElapsedSeconds
                ? Mathf.Max(0f, progressOrElapsedSeconds)
                : Mathf.Clamp01(progressOrElapsedSeconds);
            bool entrySequencing =
                sceneEntryPresentationEnabled &&
                !suppressCurrentSceneEntryPresentation &&
                CurrentBeatIndex == 0;
            float dialogueElapsed = entrySequencing
                ? Mathf.Max(
                    0f,
                    sceneEntryPresentationElapsedSeconds -
                    SceneEntryDialogueLeadSeconds)
                : BeatElapsedSeconds;

            // Preserve the established Scene/Beat transition clock exactly.
            // Scene-entry polish is an additional visibility/typewriter layer, not a
            // replacement timing system for background, character, bounce or staging.
            VnSceneComposerTransitionSnapshot sample = useElapsedSeconds
                ? VnSceneComposerElapsedTransitionSampler.Sample(
                    project, sourceScene, targetScene, previousBeat, targetBeat,
                    progress, BeatElapsedSeconds,
                    forceCompleteCurrentDialogueReveal)
                : VnSceneComposerTransitionSampler.Sample(
                    project, sourceScene, targetScene, previousBeat, targetBeat, progress);
            VnSceneComposerTransitionSnapshot endpoint =
                VnSceneComposerTransitionSampler.Sample(
                    project, sourceScene, targetScene, previousBeat, targetBeat, 1f);

            if (entrySequencing && useElapsedSeconds)
            {
                // Only dialogue reveal waits until the final entry phase. All legacy
                // visual transition samples keep their original BeatElapsedSeconds clock.
                VnWorkshopTypewriterValues typewriter =
                    VnPresentationWorkshopVn10Resolver.ResolveTypewriter(
                        VnSceneComposerComposition.ResolvePresentation(
                            project, targetScene));
                VnSceneComposerDialogueRevealSample reveal =
                    VnSceneComposerDialogueReveal.Sample(
                        targetBeat.text ?? string.Empty,
                        dialogueElapsed,
                        typewriter,
                        forceCompleteCurrentDialogueReveal);
                sample.dialogueReveal = reveal;
                sample.visibleText = reveal.PlainVisibleText;
            }
            if (suppressCurrentBackgroundTransition)
                sample.background = endpoint.background;
            if (sceneTransitionHasSwapped && sceneTransitionType == VnSceneComposerSceneTransitionType.Fade)
            {
                // The scene boundary owns this blend. Reuse the existing soft background
                // fade sampler with the boundary's authored duration and transition clock.
                var smooth = new VnWorkshopBackgroundTransitionValues
                {
                    Mode = VnWorkshopBackgroundTransitionMode.Fade,
                    Duration = sceneTransitionDuration,
                    Easing = VnWorkshopEasing.EaseInOut
                };
                float half = Mathf.Max(.0001f, sceneTransitionDuration * .5f);
                float reveal = sceneBoundaryTransitionPhase == SceneBoundaryTransitionPhase.Reveal
                    ? Mathf.Clamp01(sceneTransitionPhaseElapsed / half) : 0f;
                sample.background = VnPresentationWorkshopVn10Resolver.SampleBackgroundTransition(reveal, smooth);
            }
            if (suppressCurrentSceneEntryPresentation)
            {
                sample.background = endpoint.background;
                sample.bounce = endpoint.bounce;
                sample.stage = endpoint.stage;
                sample.characterMotions = endpoint.characterMotions;
                sample.expressions = endpoint.expressions;
            }

            Texture2D targetBackground = CurrentMediaTexture as Texture2D;
            VnWorkshopPreviewFrame targetFrame = VnSceneComposerComposition.BuildFrame(
                project, targetScene, targetBeat, VnWorkshopResolution.Reference1920x1080,
                targetBackground, BeatElapsedSeconds, cancelledCharacterStagingIds, true);
            VnWorkshopPreviewFrame sourceFrame = VnSceneComposerComposition.BuildFrame(
                project, sourceScene, ResolveFirstBeat(sourceScene),
                VnWorkshopResolution.Reference1920x1080, sourceMediaTexture as Texture2D);

            ApplyRendererFacingSample(targetFrame, sourceFrame, sample, endpoint);
            Texture targetVisual = CurrentMediaTexture != null ? CurrentMediaTexture : targetFrame.BackgroundTexture;
            Texture sourceVisual = sourceMediaTexture != null ? sourceMediaTexture : sourceFrame.BackgroundTexture;
            VnSceneComposerMediaScaleMode sourceScaleMode = sourceScene.media != null
                ? sourceScene.media.scaleMode : VnSceneComposerMediaScaleMode.Fit;
            bool routingRetainedOutgoing = retainedOutgoingVideoSource && videoPreview != null &&
                                           !videoPreview.HasVisibleFrame && sourceMediaTexture != null &&
                                           ReferenceEquals(CurrentMediaTexture, sourceMediaTexture);
            VnSceneComposerMediaScaleMode targetScaleMode = routingRetainedOutgoing
                ? sourceScaleMode
                : (targetScene.media != null ? targetScene.media.scaleMode : VnSceneComposerMediaScaleMode.Fit);

            bool videoReady = IsCurrentVideoPresentationReady();
            bool mediaReady = !IsSceneTransitionActive && videoReady;
            bool fadeReveal = videoReady && IsSceneTransitionActive && sceneTransitionHasSwapped &&
                sceneTransitionType == VnSceneComposerSceneTransitionType.Fade &&
                sceneBoundaryTransitionPhase == SceneBoundaryTransitionPhase.Reveal;
            bool showPanel = mediaReady || fadeReveal;
            bool showCharacters = showPanel &&
                (!entrySequencing ||
                 sceneEntryPresentationElapsedSeconds + .00001f >=
                 SceneEntryPlaqueLeadSeconds);
            bool showDialogueText = showPanel &&
                (!entrySequencing ||
                 sceneEntryPresentationElapsedSeconds + .00001f >=
                 SceneEntryDialogueLeadSeconds);

            CurrentSnapshot = sample;
            CurrentFrame = new VnSceneComposerPlaybackFrame(
                targetFrame, sample.background, sourceVisual, targetVisual,
                sourceScaleMode, targetScaleMode,
                BuildSceneBoundaryOverlaySample(), showPanel);
            CurrentFrame.Owner = this;
            CurrentFrame.ShowDialoguePanel = showPanel;
            CurrentFrame.ShowCharacters = showCharacters;
            CurrentFrame.ShowDialogueText = showDialogueText;
            CurrentFrame.ForegroundAlpha = SampleSceneBoundaryForegroundAlpha();
            CurrentFrame.TerminalFadeAlpha = terminalFadeAlpha;
            // Keep reveal state/data authoritative even while the renderer gate hides it.
            // This preserves first-click completion semantics without drawing text before
            // the Scene-entry character/text phase is allowed to become visible.
            CurrentFrame.Dialogue = sample.visibleText ?? string.Empty;
            CurrentFrame.ReplicaEffect = VnSceneComposerReplicaEffects.Sample(
                mediaReady && showDialogueText && targetBeat != null
                    ? targetBeat.replicaEffect : null,
                entrySequencing ? dialogueElapsed : BeatElapsedSeconds);
            CurrentFrame.DialogueReveal = sample.dialogueReveal;
        }

        private float SampleSceneBoundaryForegroundAlpha()
        {
            if (!IsSceneTransitionActive || sceneTransitionType != VnSceneComposerSceneTransitionType.Fade)
                return 1f;
            float progress = Mathf.Clamp01(sceneTransitionPhaseElapsed /
                Mathf.Max(.0001f, sceneTransitionDuration * .5f));
            switch (sceneBoundaryTransitionPhase)
            {
                case SceneBoundaryTransitionPhase.Cover: return 1f - progress;
                case SceneBoundaryTransitionPhase.Reveal: return progress;
                default: return 0f;
            }
        }

        private VnSceneComposerSceneTransitionOverlaySample BuildSceneBoundaryOverlaySample()
        {
            // A pending Transition=None video preparation is transport state only:
            // it must never paint a curtain/black cover over the outgoing frame.
            if (sceneTransitionAtomicNoneAwaitingVideo || !IsSceneTransitionActive)
                return new VnSceneComposerSceneTransitionOverlaySample
                {
                    Active = false,
                    Mode = VnSceneComposerSceneTransitionType.None,
                    Phase = VnSceneComposerSceneTransitionPhase.None,
                    Direction = sceneTransitionDirection,
                    Coverage = 0f,
                    FullCover = false
                };

            float half = Mathf.Max(.0001f, sceneTransitionDuration * .5f);
            float coverage;
            VnSceneComposerSceneTransitionPhase phase;
            switch (sceneBoundaryTransitionPhase)
            {
                case SceneBoundaryTransitionPhase.Cover:
                    phase = VnSceneComposerSceneTransitionPhase.Cover;
                    coverage = Mathf.Clamp01(sceneTransitionPhaseElapsed / half);
                    break;
                case SceneBoundaryTransitionPhase.Hold:
                    phase = VnSceneComposerSceneTransitionPhase.Hold;
                    coverage = 1f;
                    break;
                case SceneBoundaryTransitionPhase.Reveal:
                    phase = VnSceneComposerSceneTransitionPhase.Reveal;
                    coverage = 1f - Mathf.Clamp01(sceneTransitionPhaseElapsed / half);
                    break;
                default:
                    phase = VnSceneComposerSceneTransitionPhase.None;
                    coverage = 0f;
                    break;
            }

            if (sceneTransitionType == VnSceneComposerSceneTransitionType.Fade)
                coverage *= .22f;
            return new VnSceneComposerSceneTransitionOverlaySample
            {
                Active = phase != VnSceneComposerSceneTransitionPhase.None,
                Mode = sceneTransitionType,
                Phase = phase,
                Direction = sceneTransitionDirection,
                Coverage = coverage,
                FullCover = coverage >= .999f
            };
        }

        private static void ApplyRendererFacingSample(
            VnWorkshopPreviewFrame targetFrame,
            VnWorkshopPreviewFrame sourceFrame,
            VnSceneComposerTransitionSnapshot sample,
            VnSceneComposerTransitionSnapshot endpoint)
        {
            targetFrame.Dialogue = sample.dialogueReveal != null
                ? sample.dialogueReveal.RenderText
                : sample.visibleText ?? string.Empty;
            VnWorkshopPreviewCharacter[] targetCharacters = targetFrame.ComposerCharacters ?? Array.Empty<VnWorkshopPreviewCharacter>();
            var rendered = new List<VnWorkshopPreviewCharacter>(targetCharacters.Length + 3);

            for (int i = 0; i < targetCharacters.Length; i++)
            {
                VnWorkshopPreviewCharacter character = Clone(targetCharacters[i]);
                if (character == null) continue;

                ApplyStageDelta(character, sample, endpoint, i);
                ApplyFocusDelta(character, sample, endpoint, i);

                VnSceneComposerCharacterMotionPreview motion = FindMotion(sample.characterMotions, character.CharacterId, true);
                if (motion != null)
                {
                    Rect body = character.Body;
                    body.position += motion.sample.PositionOffset;
                    character.Body = body;
                    character.Alpha *= Mathf.Clamp01(motion.sample.Alpha);
                }

                VnSceneComposerExpressionPreview expression = FindExpression(sample.expressions, character.CharacterId);
                if (expression != null)
                    character.Alpha *= Mathf.Clamp01(expression.sample.EndAlpha);

                ApplyBounce(character, sample.bounce);
                if (!string.IsNullOrEmpty(sample.beatEffectCharacterId) &&
                    string.Equals(character.CharacterId, sample.beatEffectCharacterId,
                        StringComparison.OrdinalIgnoreCase))
                    ApplyBounce(character, sample.beatEffect);
                rendered.Add(character);
            }

            AppendExpressionSources(rendered, sourceFrame, sample.expressions);
            AppendExitingCharacters(rendered, sourceFrame, sample.characterMotions);
            targetFrame.ComposerCharacters = rendered.ToArray();
        }

        private static void ApplyStageDelta(
            VnWorkshopPreviewCharacter character,
            VnSceneComposerTransitionSnapshot current,
            VnSceneComposerTransitionSnapshot endpoint,
            int index)
        {
            if (current.stage == null || endpoint.stage == null ||
                index >= current.stage.Length || index >= endpoint.stage.Length) return;
            VnWorkshopStageTransitionSample now = current.stage[index];
            VnWorkshopStageTransitionSample end = endpoint.stage[index];
            Rect body = character.Body;
            body.center += now.Position - end.Position;
            if (end.Scale > .0001f)
                body.size *= Mathf.Max(.01f, now.Scale / end.Scale);
            character.Body = body;
        }

        private static void ApplyFocusDelta(
            VnWorkshopPreviewCharacter character,
            VnSceneComposerTransitionSnapshot current,
            VnSceneComposerTransitionSnapshot endpoint,
            int index)
        {
            if (current.focus == null || endpoint.focus == null ||
                index >= current.focus.Length || index >= endpoint.focus.Length) return;
            VnWorkshopSpeakerFocusSample now = current.focus[index];
            VnWorkshopSpeakerFocusSample end = endpoint.focus[index];
            Rect body = character.Body;
            body.center += now.PositionOffset - end.PositionOffset;
            if (end.Scale > .0001f) body.size *= Mathf.Max(.01f, now.Scale / end.Scale);
            character.Body = body;
            if (end.Alpha > .0001f) character.Alpha *= Mathf.Clamp01(now.Alpha / end.Alpha);
            if (end.Brightness > .0001f) character.Brightness *= Mathf.Max(0f, now.Brightness / end.Brightness);
        }

        private static void ApplyBounce(VnWorkshopPreviewCharacter character, VnWorkshopActionBounceSample bounce)
        {
            Rect body = character.Body;
            body.center += bounce.PositionOffset;
            body.size *= Mathf.Max(.01f, bounce.ScaleMultiplier);
            character.Body = body;
        }

        private static void AppendExpressionSources(
            List<VnWorkshopPreviewCharacter> rendered,
            VnWorkshopPreviewFrame sourceFrame,
            VnSceneComposerExpressionPreview[] expressions)
        {
            if (sourceFrame == null || sourceFrame.ComposerCharacters == null || expressions == null) return;
            for (int i = 0; i < expressions.Length; i++)
            {
                VnSceneComposerExpressionPreview expression = expressions[i];
                VnWorkshopPreviewCharacter source = FindCharacter(sourceFrame.ComposerCharacters, expression.characterId);
                if (source == null || expression.sample.StartAlpha <= 0f) continue;
                VnWorkshopPreviewCharacter clone = Clone(source);
                clone.Alpha *= Mathf.Clamp01(expression.sample.StartAlpha);
                rendered.Add(clone);
            }
        }

        private static void AppendExitingCharacters(
            List<VnWorkshopPreviewCharacter> rendered,
            VnWorkshopPreviewFrame sourceFrame,
            VnSceneComposerCharacterMotionPreview[] motions)
        {
            if (sourceFrame == null || sourceFrame.ComposerCharacters == null || motions == null) return;
            for (int i = 0; i < motions.Length; i++)
            {
                VnSceneComposerCharacterMotionPreview motion = motions[i];
                if (motion == null || !motion.exiting || motion.sample.Alpha <= 0f) continue;
                VnWorkshopPreviewCharacter source = FindCharacter(sourceFrame.ComposerCharacters, motion.characterId);
                if (source == null) continue;
                VnWorkshopPreviewCharacter clone = Clone(source);
                Rect body = clone.Body;
                body.position += motion.sample.PositionOffset;
                clone.Body = body;
                clone.Alpha *= Mathf.Clamp01(motion.sample.Alpha);
                rendered.Add(clone);
            }
        }

        private void BeginSceneBoundaryTransition(
            int targetIndex, bool playMedia, bool preserveCompatibleVideoTimeline)
        {
            if (IsSceneTransitionActive || targetIndex == CurrentSceneIndex) return;
            RequireSceneIndex(targetIndex);
            VnSceneComposerScene target = project.scenes[targetIndex];
            VnSceneComposerTransition transition = target != null ? target.transition : null;
            bool animate = transition != null &&
                           (transition.sceneTransitionType == VnSceneComposerSceneTransitionType.DarkCurtain ||
                            transition.sceneTransitionType == VnSceneComposerSceneTransitionType.Fade) &&
                           transition.sceneTransitionDuration > .0001f;
            if (!animate)
            {
                // Transition=None must remain a coherent same-frame swap. If the
                // destination is a NEW external video that is not ready yet, prewarm it
                // without changing CurrentSceneIndex or the outgoing rendered frame.
                if (TryBeginAtomicNoneVideoWait(
                        targetIndex, playMedia, preserveCompatibleVideoTimeline))
                    return;

                // Transition=None suppresses only the old/new background blend. The
                // incoming Scene's own bounce/stage/expression sampling must still start
                // at Scene time zero so PlayAll matches PlayScene/PlayFromHere.
                ResetScene(targetIndex, playMedia, ResolveSourceScene(targetIndex), true,
                    preserveCompatibleVideoTimeline, suppressSceneEntryPresentation: false);
                return;
            }

            outgoingPresentation = CurrentFrame;
            if (videoPreview != null) videoPreview.Pause();
            sceneTransitionSourceScene =
                CurrentSceneIndex >= 0 && CurrentSceneIndex < project.scenes.Count
                    ? project.scenes[CurrentSceneIndex]
                    : CreatePreviewBaseline();
            pendingSceneTransitionTargetIndex = targetIndex;
            sceneTransitionDuration = Mathf.Clamp(transition.sceneTransitionDuration, .0001f, 10f);
            sceneTransitionType = transition.sceneTransitionType;
            sceneTransitionDirection = transition.sceneTransitionDirection;
            sceneTransitionPlayMedia = playMedia;
            // Animated boundaries always preserve the established compatible-video path so
            // the destination can prepare under full cover without exposing a stale/black frame.
            sceneTransitionPreserveCompatibleVideoTimeline = true;
            sceneTransitionHasSwapped = false;
            sceneTransitionVideoPrepareRequested = false;
            sceneTransitionVideoWaitElapsed = 0f;
            sceneTransitionPhaseElapsed = 0f;
            bool inheritAdditionalAudio =
                target != null &&
                target.keepPreviousAdditionalAudio &&
                targetIndex > CurrentSceneIndex;
            layeredAudioPlayback.ExitCurrentScene(
                playMedia && IsPlaying,
                inheritAdditionalAudio,
                inheritAdditionalAudio ? target.sceneId : string.Empty);
            sceneBoundaryTransitionPhase = SceneBoundaryTransitionPhase.Cover;
            SceneTransitionStartCount++;
            RebuildFrame(SceneElapsedSeconds, true);
        }

        private void AdvanceSceneBoundaryTransition(float deltaSeconds)
        {
            if (!IsSceneTransitionActive) return;
            musicPlayback.Advance(deltaSeconds);
            layeredAudioPlayback.Advance(deltaSeconds);

            if (sceneTransitionAtomicNoneAwaitingVideo)
            {
                AdvanceAtomicNoneVideoWait(deltaSeconds);
                return;
            }

            if (sceneTransitionHasSwapped) TryRecoverCurrentVideoPlayback();
            float remaining = Mathf.Max(0f, deltaSeconds);
            int safety = 0;

            while (IsSceneTransitionActive && safety++ < 6)
            {
                float half = Mathf.Max(.0001f, sceneTransitionDuration * .5f);
                if (sceneBoundaryTransitionPhase == SceneBoundaryTransitionPhase.Cover)
                {
                    float need = Mathf.Max(0f, half - sceneTransitionPhaseElapsed);
                    float consume = Mathf.Min(remaining, need);
                    sceneTransitionPhaseElapsed += consume;
                    remaining -= consume;
                    RebuildFrame(SceneElapsedSeconds, true);
                    if (sceneTransitionPhaseElapsed + .000001f < half) return;

                    sceneBoundaryTransitionPhase = SceneBoundaryTransitionPhase.Hold;
                    sceneTransitionPhaseElapsed = 0f;
                    RebuildFrame(SceneElapsedSeconds, true);
                    PerformCoveredSceneSwap();
                    if (!IsSceneTransitionActive) return;
                    if (!IsSceneTransitionTargetReadyOrTimedOut(remaining))
                    {
                        RebuildFrame(SceneElapsedSeconds, true);
                        return;
                    }
                    BeginSceneTransitionReveal();
                    if (remaining <= 0f)
                    {
                        RebuildFrame(SceneElapsedSeconds, true);
                        return;
                    }
                    continue;
                }

                if (sceneBoundaryTransitionPhase == SceneBoundaryTransitionPhase.Hold)
                {
                    RefreshMediaTexture();
                    TryRecoverCurrentVideoPlayback();
                    if (!IsSceneTransitionTargetReadyOrTimedOut(remaining))
                    {
                        RebuildFrame(SceneElapsedSeconds, true);
                        return;
                    }
                    BeginSceneTransitionReveal();
                    if (remaining <= 0f)
                    {
                        RebuildFrame(SceneElapsedSeconds, true);
                        return;
                    }
                    continue;
                }

                if (sceneBoundaryTransitionPhase == SceneBoundaryTransitionPhase.Reveal)
                {
                    float need = Mathf.Max(0f, half - sceneTransitionPhaseElapsed);
                    float consume = Mathf.Min(remaining, need);
                    sceneTransitionPhaseElapsed += consume;
                    remaining -= consume;
                    RefreshMediaTexture();
                    RebuildFrame(SceneElapsedSeconds, true);
                    if (sceneTransitionPhaseElapsed + .000001f < half) return;
                    CompleteSceneBoundaryTransition();
                    return;
                }

                CancelSceneBoundaryTransition();
                return;
            }
        }

        private bool TryBeginAtomicNoneVideoWait(
            int targetIndex, bool playMedia, bool preserveCompatibleVideoTimeline)
        {
            VnSceneComposerScene target = project.scenes[targetIndex];
            if (target == null || target.media == null ||
                target.media.kind != VnSceneComposerMediaKind.ExternalVideo)
                return false;

            // Same-video continuity already has an authoritative reusable preview.
            if (CanReuseVideoPreview(target))
                return false;

            VnSceneComposerVideoPreview pending;
            bool ownsPending = false;
            if (!VnSceneComposerPreparedVideoRegistry.TryBorrow(project, target, out pending))
            {
                pending = VnSceneComposerMediaEditing.OpenVideoPreview(target.media, 1280, 720);
                ownsPending = true;
            }

            if (pending == null)
                return false;

            if (!string.IsNullOrEmpty(pending.warning))
            {
                if (ownsPending) pending.Dispose();
                return false;
            }

            outgoingPresentation = CurrentFrame;
            sceneTransitionSourceScene =
                CurrentSceneIndex >= 0 && CurrentSceneIndex < project.scenes.Count
                    ? project.scenes[CurrentSceneIndex]
                    : CreatePreviewBaseline();
            pendingSceneTransitionTargetIndex = targetIndex;
            sceneTransitionPlayMedia = playMedia;
            sceneTransitionPreserveCompatibleVideoTimeline = preserveCompatibleVideoTimeline;
            sceneTransitionVideoWaitElapsed = 0f;
            sceneTransitionVideoPrepareRequested = true;
            sceneTransitionHasSwapped = false;
            sceneTransitionAtomicNoneAwaitingVideo = true;
            pendingAtomicNoneVideoPreview = pending;
            ownsPendingAtomicNoneVideoPreview = ownsPending;

            // Prepare exactly once. A registry-borrowed in-flight preview simply keeps
            // its existing request; VnSceneComposerVideoPreview.Prepare is idempotent.
            pending.Prepare();

            if (pending.IsReadyForCurrentRequest)
            {
                CommitAtomicNoneVideoSwap();
                return true;
            }

            RebuildFrame(SceneElapsedSeconds, true);
            return true;
        }

        private void AdvanceAtomicNoneVideoWait(float deltaSeconds)
        {
            VnSceneComposerVideoPreview pending = pendingAtomicNoneVideoPreview;
            if (pending == null)
            {
                CancelSceneBoundaryTransition();
                RebuildFrame(SceneElapsedSeconds, true);
                return;
            }

            if (!string.IsNullOrEmpty(pending.warning) || pending.HasFailed)
            {
                currentVideoWarning = !string.IsNullOrEmpty(pending.warning)
                    ? pending.warning
                    : "Подготовка следующего видео завершилась ошибкой.";
                CancelSceneBoundaryTransition();
                RebuildFrame(SceneElapsedSeconds, true);
                return;
            }

            if (pending.IsReadyForCurrentRequest)
            {
                CommitAtomicNoneVideoSwap();
                return;
            }

            sceneTransitionVideoWaitElapsed += Mathf.Max(0f, deltaSeconds);
            if (sceneTransitionVideoWaitElapsed >= MaxVideoPreparationHoldSeconds)
            {
                currentVideoWarning =
                    "Подготовка следующего видео не завершилась вовремя. Исходящая сцена сохранена.";
                pending.AbortCurrentRequest(currentVideoWarning);
                CancelSceneBoundaryTransition();
                RebuildFrame(SceneElapsedSeconds, true);
                return;
            }

            // Keep the exact outgoing Scene/frame authoritative while waiting.
            RebuildFrame(SceneElapsedSeconds, true);
        }

        private void CommitAtomicNoneVideoSwap()
        {
            if (!sceneTransitionAtomicNoneAwaitingVideo ||
                pendingSceneTransitionTargetIndex < 0 ||
                pendingAtomicNoneVideoPreview == null)
                return;

            int targetIndex = pendingSceneTransitionTargetIndex;
            bool playMedia = sceneTransitionPlayMedia;
            VnSceneComposerVideoPreview prepared = pendingAtomicNoneVideoPreview;
            bool transferOwnership = ownsPendingAtomicNoneVideoPreview;
            VnSceneComposerScene target = project.scenes[targetIndex];

            // Register first so the existing ResetScene/OpenMedia path borrows this exact
            // prepared request instead of opening/preparing the same target a second time.
            VnSceneComposerPreparedVideoRegistry.Prime(project, target, prepared);

            pendingAtomicNoneVideoPreview = null;
            ownsPendingAtomicNoneVideoPreview = false;
            sceneTransitionAtomicNoneAwaitingVideo = false;
            sceneBoundaryTransitionPhase = SceneBoundaryTransitionPhase.None;
            pendingSceneTransitionTargetIndex = -1;
            sceneTransitionVideoPrepareRequested = false;
            sceneTransitionVideoWaitElapsed = 0f;
            sceneTransitionHasSwapped = false;
            sceneTransitionSourceScene = null;
            outgoingPresentation = null;

            // The target video is already ready, so no outgoing-video retention is needed
            // for this atomic commit even when the caller came from auto-advance.
            // Media is ready, so commit atomically, then let the incoming Scene use
            // its normal scene-local presentation clock. Only the background blend is
            // suppressed to keep Transition=None a coherent same-frame swap.
            ResetScene(targetIndex, playMedia, ResolveSourceScene(targetIndex), true,
                false, suppressSceneEntryPresentation: false);

            if (ReferenceEquals(videoPreview, prepared))
                ownsVideoPreview = transferOwnership;
            else if (transferOwnership)
                prepared.Dispose();
        }

        private void ReleasePendingAtomicNoneVideo()
        {
            if (pendingAtomicNoneVideoPreview != null && ownsPendingAtomicNoneVideoPreview)
                pendingAtomicNoneVideoPreview.Dispose();
            pendingAtomicNoneVideoPreview = null;
            ownsPendingAtomicNoneVideoPreview = false;
        }

        private void PerformCoveredSceneSwap()
        {
            if (sceneTransitionHasSwapped) return;
            int targetIndex = pendingSceneTransitionTargetIndex;
            RequireSceneIndex(targetIndex);
            VnSceneComposerScene source = sceneTransitionSourceScene ?? CreatePreviewBaseline();

            outgoingPresentation = null;
            ResetScene(targetIndex, false, source, true,
                sceneTransitionPreserveCompatibleVideoTimeline, false, false, false, false);

            musicPlayback.Apply(
                VnSceneComposerMusicResolver.Resolve(project, targetIndex),
                sceneTransitionPlayMedia && IsPlaying, false);
            VnSceneComposerScene incomingAudioScene = project.scenes[targetIndex];
            VnSceneComposerDialogueBeat incomingAudioBeat = ResolveBeat(incomingAudioScene, 0);
            layeredAudioPlayback.EnterScene(
                incomingAudioScene,
                incomingAudioBeat != null ? incomingAudioBeat.beatId : string.Empty,
                sceneTransitionPlayMedia && IsPlaying);

            sceneTransitionHasSwapped = true;
            sceneTransitionVideoPrepareRequested = false;
            PrepareTransitionTargetVideoIfNeeded();
            RefreshMediaTexture();
            RebuildFrame(SceneElapsedSeconds, true);
        }

        private void PrepareTransitionTargetVideoIfNeeded()
        {
            if (!sceneTransitionHasSwapped || videoPreview == null ||
                videoPreview.IsReadyForCurrentRequest ||
                !string.IsNullOrEmpty(videoPreview.warning))
                return;
            if (sceneTransitionVideoPrepareRequested) return;
            videoPreview.Pause();
            videoPreview.Prepare();
            sceneTransitionVideoPrepareRequested = true;
        }

        private bool IsSceneTransitionTargetReady()
        {
            if (!sceneTransitionHasSwapped) return false;
            VnSceneComposerScene scene = CurrentSceneIndex >= 0 && CurrentSceneIndex < project.scenes.Count
                ? project.scenes[CurrentSceneIndex] : null;
            if (scene == null || scene.media == null ||
                scene.media.kind != VnSceneComposerMediaKind.ExternalVideo)
                return true;
            if (videoPreview == null || !string.IsNullOrEmpty(videoPreview.warning)) return true;
            if (videoPreview.IsReadyForCurrentRequest) return true;
            PrepareTransitionTargetVideoIfNeeded();
            return false;
        }

        private bool IsSceneTransitionTargetReadyOrTimedOut(float elapsedWait)
        {
            if (IsSceneTransitionTargetReady())
            {
                sceneTransitionVideoWaitElapsed = 0f;
                return true;
            }

            sceneTransitionVideoWaitElapsed += Mathf.Max(0f, elapsedWait);
            if (sceneTransitionVideoWaitElapsed < MaxVideoPreparationHoldSeconds)
                return false;

            currentVideoWarning =
                "Подготовка видео не завершилась вовремя. Видеосостояние сброшено; следующая команда сможет повторить попытку.";
            if (videoPreview != null)
                videoPreview.AbortCurrentRequest(currentVideoWarning);
            return true;
        }

        private void BeginSceneTransitionReveal()
        {
            sceneBoundaryTransitionPhase = SceneBoundaryTransitionPhase.Reveal;
            sceneTransitionPhaseElapsed = 0f;
            if (sceneTransitionPlayMedia && IsPlaying && videoPreview != null &&
                string.IsNullOrEmpty(videoPreview.warning) &&
                !videoPreview.IsPlaying && !videoPreview.IsPreparing)
                videoPreview.Play();
            RebuildFrame(SceneElapsedSeconds, true);
        }

        private void CompleteSceneBoundaryTransition()
        {
            sceneBoundaryTransitionPhase = SceneBoundaryTransitionPhase.None;
            pendingSceneTransitionTargetIndex = -1;
            sceneTransitionPhaseElapsed = 0f;
            sceneTransitionDuration = 0f;
            sceneTransitionType = VnSceneComposerSceneTransitionType.None;
            sceneTransitionPlayMedia = false;
            sceneTransitionPreserveCompatibleVideoTimeline = false;
            sceneTransitionVideoPrepareRequested = false;
            sceneTransitionVideoWaitElapsed = 0f;
            sceneTransitionSourceScene = null;
            outgoingPresentation = null;
            sceneTransitionHasSwapped = false;
            RebuildFrame(SceneElapsedSeconds, true);
        }

        private void CancelSceneBoundaryTransition()
        {
            ReleasePendingAtomicNoneVideo();
            sceneTransitionAtomicNoneAwaitingVideo = false;
            sceneBoundaryTransitionPhase = SceneBoundaryTransitionPhase.None;
            pendingSceneTransitionTargetIndex = -1;
            sceneTransitionPhaseElapsed = 0f;
            sceneTransitionDuration = 0f;
            sceneTransitionType = VnSceneComposerSceneTransitionType.None;
            sceneTransitionPlayMedia = false;
            sceneTransitionPreserveCompatibleVideoTimeline = false;
            sceneTransitionVideoPrepareRequested = false;
            sceneTransitionVideoWaitElapsed = 0f;
            sceneTransitionSourceScene = null;
            outgoingPresentation = null;
            sceneTransitionHasSwapped = false;
        }

        private void OpenSourceMedia(VnSceneComposerScene scene)
        {
            if (scene == null || scene.media == null) return;
            switch (scene.media.kind)
            {
                case VnSceneComposerMediaKind.ExistingRokasAsset:
                case VnSceneComposerMediaKind.ExternalImage:
                    sourceImagePreview = VnSceneComposerMediaEditing.OpenImagePreview(scene.media);
                    break;
                case VnSceneComposerMediaKind.ExternalVideo:
                    sourceVideoPreview = VnSceneComposerMediaEditing.OpenVideoPreview(scene.media, 1280, 720);
                    ownsSourceVideoPreview = true;
                    retainedOutgoingVideoSource = false;
                    break;
                case VnSceneComposerMediaKind.ExternalGif:
                    sourceGifPreview = VnSceneComposerMediaEditing.OpenGifPreview(scene.media);
                    break;
            }
        }

        private bool CanRetainCurrentVideoAsOutgoing(VnSceneComposerScene targetScene)
        {
            return targetScene != null && targetScene.media != null &&
                   targetScene.media.kind == VnSceneComposerMediaKind.ExternalVideo &&
                   videoPreview != null && videoPreview.texture != null && videoPreview.HasVisibleFrame &&
                   CurrentMediaTexture != null;
        }

        private void PromoteCurrentVideoToSource()
        {
            sourceVideoPreview = videoPreview;
            ownsSourceVideoPreview = ownsVideoPreview;
            retainedOutgoingVideoSource = true;
            sourceMediaTexture = CurrentMediaTexture != null ? CurrentMediaTexture : videoPreview.texture;
            sourceVideoPreview.Pause();
            videoPreview = null;
            ownsVideoPreview = false;
            openedVideoSignature = string.Empty;
            CurrentMediaTexture = sourceMediaTexture;
        }

        private void RefreshSourceMediaTexture()
        {
            if (sourceGifPreview != null) sourceMediaTexture = sourceGifPreview.currentTexture;
            else if (sourceVideoPreview != null) sourceMediaTexture = sourceVideoPreview.texture;
            else if (sourceImagePreview != null) sourceMediaTexture = sourceImagePreview.texture;
            else sourceMediaTexture = null;
        }

        private void ReleaseSourceMedia()
        {
            if (sourceImagePreview != null) sourceImagePreview.Dispose();
            if (sourceVideoPreview != null && ownsSourceVideoPreview) sourceVideoPreview.Dispose();
            if (sourceGifPreview != null) sourceGifPreview.Dispose();
            sourceImagePreview = null;
            sourceVideoPreview = null;
            ownsSourceVideoPreview = false;
            retainedOutgoingVideoSource = false;
            sourceGifPreview = null;
            sourceMediaTexture = null;
        }

        private void OpenMedia(VnSceneComposerScene scene)
        {
            if (scene == null || scene.media == null) return;
            switch (scene.media.kind)
            {
                case VnSceneComposerMediaKind.ExistingRokasAsset:
                case VnSceneComposerMediaKind.ExternalImage:
                    imagePreview = VnSceneComposerMediaEditing.OpenImagePreview(scene.media);
                    break;
                case VnSceneComposerMediaKind.ExternalVideo:
                    if (VnSceneComposerPreparedVideoRegistry.TryBorrow(project, scene,
                        out VnSceneComposerVideoPreview preparedVideo))
                    {
                        videoPreview = preparedVideo;
                        ownsVideoPreview = false;
                    }
                    else
                    {
                        videoPreview = VnSceneComposerMediaEditing.OpenVideoPreview(scene.media, 1280, 720);
                        ownsVideoPreview = true;
                    }
                    openedVideoSignature = BuildVideoSignature(scene.media);
                    break;
                case VnSceneComposerMediaKind.ExternalGif:
                    gifPreview = VnSceneComposerMediaEditing.OpenGifPreview(scene.media);
                    break;
            }
        }

        private bool CanReuseVideoPreview(VnSceneComposerScene scene)
        {
            return scene != null && scene.media != null &&
                   scene.media.kind == VnSceneComposerMediaKind.ExternalVideo &&
                   videoPreview != null && videoPreview.IsRequestReusable &&
                   string.Equals(openedVideoSignature, BuildVideoSignature(scene.media), StringComparison.Ordinal);
        }

        private static string BuildVideoSignature(VnSceneComposerMediaReference media)
        {
            if (media == null) return string.Empty;
            return (media.reference ?? string.Empty) + "|" + (media.contentHash ?? string.Empty) + "|" + media.loop;
        }

        private void RefreshMediaTexture()
        {
            if (gifPreview != null) CurrentMediaTexture = gifPreview.currentTexture;
            else if (videoPreview != null)
            {
                if (!string.IsNullOrEmpty(videoPreview.warning))
                    currentVideoWarning = videoPreview.warning;
                else if (videoPreview.HasVisibleFrame)
                    currentVideoWarning = string.Empty;

                if (retainedOutgoingVideoSource && !videoPreview.HasVisibleFrame &&
                    sourceMediaTexture != null)
                    CurrentMediaTexture = sourceMediaTexture;
                else
                    CurrentMediaTexture = videoPreview.HasVisibleFrame ? videoPreview.texture : null;
            }
            else if (imagePreview != null) CurrentMediaTexture = imagePreview.texture;
            else CurrentMediaTexture = null;
        }

        private void ReleaseMedia()
        {
            if (imagePreview != null) imagePreview.Dispose();
            if (videoPreview != null)
            {
                if (ownsVideoPreview) videoPreview.Dispose();
                else videoPreview.Pause();
            }
            if (gifPreview != null) gifPreview.Dispose();
            imagePreview = null;
            videoPreview = null;
            ownsVideoPreview = false;
            gifPreview = null;
            openedVideoSignature = string.Empty;
        }

        private bool TryRecoverCurrentVideoPlayback()
        {
            if (CurrentSceneIndex < 0 || CurrentSceneIndex >= project.scenes.Count ||
                videoPreview == null)
                return false;

            VnSceneComposerScene scene = project.scenes[CurrentSceneIndex];
            if (scene == null || scene.media == null ||
                scene.media.kind != VnSceneComposerMediaKind.ExternalVideo)
                return false;

            if (!videoPreview.HasFailed && string.IsNullOrEmpty(videoPreview.warning))
                return false;

            currentVideoWarning = string.IsNullOrEmpty(videoPreview.warning)
                ? "Видео требует повторной подготовки."
                : videoPreview.warning;

            if (videoRecoveryAttemptCount >= 1)
                return false;

            videoRecoveryAttemptCount++;
            videoPreview.Play();
            if (string.IsNullOrEmpty(videoPreview.warning))
                currentVideoWarning = string.Empty;
            return true;
        }

        private bool IsCurrentVideoPresentationReady()
        {
            if (CurrentSceneIndex < 0 || CurrentSceneIndex >= project.scenes.Count)
                return true;

            VnSceneComposerScene scene = project.scenes[CurrentSceneIndex];
            if (scene == null || scene.media == null ||
                scene.media.kind != VnSceneComposerMediaKind.ExternalVideo)
                return true;

            if (videoPreview == null) return true;
            if (!string.IsNullOrEmpty(videoPreview.warning)) return true;
            return videoPreview.IsReadyForCurrentRequest;
        }

        private VnSceneComposerScene ResolveSourceScene(int targetIndex)
        {
            if (targetIndex > 0) return project.scenes[targetIndex - 1];
            return CreatePreviewBaseline();
        }

        private static VnSceneComposerScene CreatePreviewBaseline()
        {
            return new VnSceneComposerScene
            {
                sceneId = "__scene_composer_preview_baseline__",
                label = "Preview Baseline",
                previewText = string.Empty,
                speaker = string.Empty,
                narration = true,
                timing = new VnSceneComposerTiming()
            };
        }

        private static int BeatCount(VnSceneComposerScene scene)
        {
            return scene != null && scene.dialogueBeats != null && scene.dialogueBeats.Count > 0
                ? scene.dialogueBeats.Count
                : 1;
        }

        private static VnSceneComposerDialogueBeat ResolveFirstBeat(VnSceneComposerScene scene)
        {
            return ResolveBeat(scene, 0);
        }

        private static VnSceneComposerDialogueBeat ResolveBeat(VnSceneComposerScene scene, int beatIndex)
        {
            if (scene != null && scene.dialogueBeats != null &&
                beatIndex >= 0 && beatIndex < scene.dialogueBeats.Count &&
                scene.dialogueBeats[beatIndex] != null)
                return scene.dialogueBeats[beatIndex];

            return new VnSceneComposerDialogueBeat
            {
                beatId = string.Empty,
                speaker = string.Empty,
                text = string.Empty,
                narration = false
            };
        }

        private float GetEffectiveCharacterElapsedSeconds()
        {
            return sceneEntryPresentationEnabled &&
                   !suppressCurrentSceneEntryPresentation &&
                   CurrentBeatIndex == 0
                ? Mathf.Max(
                    0f,
                    sceneEntryPresentationElapsedSeconds -
                    SceneEntryPlaqueLeadSeconds)
                : BeatElapsedSeconds;
        }

        private float GetEffectiveDialogueElapsedSeconds()
        {
            return sceneEntryPresentationEnabled &&
                   !suppressCurrentSceneEntryPresentation &&
                   CurrentBeatIndex == 0
                ? Mathf.Max(
                    0f,
                    sceneEntryPresentationElapsedSeconds -
                    SceneEntryDialogueLeadSeconds)
                : BeatElapsedSeconds;
        }

        private static float ResolveScenePreviewDuration(VnSceneComposerPreviewTimingPlan timing)
        {
            if (timing == null) return 0f;
            if (timing.usesPreviewAutoDuration) return Mathf.Max(0f, timing.previewAutoDuration);
            return Mathf.Max(.0001f,
                timing.typewriterDuration + timing.settleDuration + timing.breathingRoom);
        }

        private static void RequireBeatIndex(VnSceneComposerScene scene, int beatIndex)
        {
            int count = BeatCount(scene);
            if (beatIndex < 0 || beatIndex >= count)
                throw new ArgumentOutOfRangeException(
                    nameof(beatIndex), beatIndex, "Dialogue Beat index is outside the current Scene.");
        }

        private void RequireSceneIndex(int sceneIndex)
        {
            if (sceneIndex < 0 || sceneIndex >= project.scenes.Count)
                throw new ArgumentOutOfRangeException(nameof(sceneIndex), sceneIndex, "Scene index is outside the Composer project.");
        }

        private void BeginTerminalFade(VnSceneComposerScene scene)
        {
            if (scene == null || terminalFadeActive || IsSequenceCompleted) return;
            terminalFadeActive = true;
            terminalFadeElapsed = 0f;
            terminalFadeDuration = Mathf.Clamp(scene.terminalFadeDuration, .05f, 10f);
            terminalFadeAlpha = 0f;
            RebuildFrame(SceneElapsedSeconds, true);
        }

        private void AdvanceTerminalFade(float deltaSeconds)
        {
            if (!terminalFadeActive) return;
            terminalFadeElapsed += Mathf.Max(0f, deltaSeconds);
            float raw = Mathf.Clamp01(
                terminalFadeElapsed / Mathf.Max(.05f, terminalFadeDuration));
            terminalFadeAlpha = raw * raw * (3f - (2f * raw));
            RebuildFrame(SceneElapsedSeconds, true);
            if (raw < 1f) return;

            terminalFadeActive = false;
            terminalFadeAlpha = 1f;
            IsPlaying = false;
            if (videoPreview != null) videoPreview.Pause();
            musicPlayback.Pause();
            layeredAudioPlayback.StopAllImmediate();
            IsSequenceCompleted = true;
            if (!completionSignalRaised)
            {
                completionSignalRaised = true;
                VnSequenceCompleted?.Invoke();
            }
            RebuildFrame(SceneElapsedSeconds, true);
        }

        private void ResetTerminalCompletionState()
        {
            terminalFadeActive = false;
            terminalFadeElapsed = 0f;
            terminalFadeDuration = 1.5f;
            terminalFadeAlpha = 0f;
            IsSequenceCompleted = false;
            completionSignalRaised = false;
        }

        private void StopEmpty()
        {
            IsPlaying = false;
            CurrentSceneIndex = -1;
            CurrentBeatIndex = 0;
            SceneElapsedSeconds = 0f;
            BeatElapsedSeconds = 0f;
            MediaTimeSeconds = 0f;
            cancelledCharacterStagingIds.Clear();
            ReleaseMedia();
            ReleaseSourceMedia();
            musicPlayback.StopImmediate();
            videoRecoveryAttemptCount = 0;
            currentVideoWarning = string.Empty;
            currentSourceScene = null;
            suppressCurrentBackgroundTransition = false;
            suppressCurrentSceneEntryPresentation = false;
            sceneEntryPresentationEnabled = false;
            sceneEntryPresentationElapsedSeconds = 0f;
            forceCompleteCurrentDialogueReveal = false;
            CancelSceneBoundaryTransition();
            CurrentMediaTexture = null;
            CurrentSnapshot = null;
            CurrentFrame = null;
        }

        private static VnSceneComposerCharacterMotionPreview FindMotion(
            VnSceneComposerCharacterMotionPreview[] motions, string characterId, bool entering)
        {
            if (motions == null) return null;
            for (int i = 0; i < motions.Length; i++)
            {
                VnSceneComposerCharacterMotionPreview motion = motions[i];
                if (motion != null && motion.entering == entering &&
                    string.Equals(motion.characterId, characterId, StringComparison.OrdinalIgnoreCase)) return motion;
            }
            return null;
        }

        private static VnSceneComposerExpressionPreview FindExpression(
            VnSceneComposerExpressionPreview[] expressions, string characterId)
        {
            if (expressions == null) return null;
            for (int i = 0; i < expressions.Length; i++)
            {
                VnSceneComposerExpressionPreview expression = expressions[i];
                if (expression != null && string.Equals(expression.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return expression;
            }
            return null;
        }

        private static VnWorkshopPreviewCharacter FindCharacter(
            VnWorkshopPreviewCharacter[] characters, string characterId)
        {
            if (characters == null) return null;
            for (int i = 0; i < characters.Length; i++)
            {
                VnWorkshopPreviewCharacter character = characters[i];
                if (character != null && string.Equals(character.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return character;
            }
            return null;
        }

        private static VnWorkshopPreviewCharacter Clone(VnWorkshopPreviewCharacter source)
        {
            if (source == null) return null;
            return new VnWorkshopPreviewCharacter
            {
                CharacterId = source.CharacterId,
                StateId = source.StateId,
                Slot = source.Slot,
                Texture = source.Texture,
                Uv = source.Uv,
                Body = source.Body,
                Active = source.Active,
                Alpha = source.Alpha,
                Brightness = source.Brightness
            };
        }
    }
}
