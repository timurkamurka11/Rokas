using System.Collections.Generic;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnSceneComposerPlaybackController
    {
        private static readonly HashSet<VnSceneComposerPlaybackController> MuteOwners = new HashSet<VnSceneComposerPlaybackController>();
        private static float volumeBeforeMasterMute = 1f;
        private static bool masterMuteAppliedByController;
        private long lastAdvanceInputTick = long.MinValue;
        private bool resumeVideoAfterMenu;
        public bool IsMenuOpen { get; private set; }
        public bool IsMuted { get { return AudioListener.volume <= .0001f; } }
        public long InputTick { get; private set; }
        public float UiElapsedSeconds { get; private set; }
        public bool ShowCompletionIndicator
        {
            get { return !disposed && IsPlaying && !IsSceneTransitionActive && CurrentFrame != null &&
                CurrentFrame.ShowDialogueText && CurrentFrame.DialogueReveal != null && CurrentFrame.DialogueReveal.Complete; }
        }

        // Every visible advance affordance shares this command and the editor update token.
        public bool RequestAdvance(long inputTick)
        {
            if (disposed || IsMenuOpen || !IsPlaying || IsSceneTransitionActive || inputTick == lastAdvanceInputTick) return false;
            lastAdvanceInputTick = inputTick;
            AdvanceDialogue();
            return true;
        }

        public void SetMuted(bool muted)
        {
            if (disposed) return;
            if (muted)
            {
                if (!IsMuted)
                {
                    volumeBeforeMasterMute = AudioListener.volume;
                    masterMuteAppliedByController = true;
                }
                MuteOwners.Add(this);
                AudioListener.volume = 0f;
            }
            else
            {
                MuteOwners.Clear();
                masterMuteAppliedByController = false;
                if (IsMuted) AudioListener.volume = Mathf.Max(.0002f, volumeBeforeMasterMute);
            }
        }

        private void ReleaseOwnedMute()
        {
            if (!MuteOwners.Remove(this) || MuteOwners.Count != 0) return;
            if (masterMuteAppliedByController && IsMuted)
                AudioListener.volume = Mathf.Max(.0002f, volumeBeforeMasterMute);
            masterMuteAppliedByController = false;
        }

        public void SetMenuOpen(bool open)
        {
            if (disposed || IsMenuOpen == open) return;
            IsMenuOpen = open;
            if (open)
            {
                resumeVideoAfterMenu = videoPreview != null && videoPreview.IsPlaying;
                if (videoPreview != null) videoPreview.Pause();
                musicPlayback.Pause();
                layeredAudioPlayback.Pause();
            }
            else if (IsPlaying)
            {
                if (resumeVideoAfterMenu && videoPreview != null) videoPreview.ResumePresentation();
                musicPlayback.Resume();
                layeredAudioPlayback.Resume();
            }
            if (!open) resumeVideoAfterMenu = false;
        }
    }
}
