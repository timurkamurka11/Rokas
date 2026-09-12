#nullable enable
using System;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

namespace Rokas.Presentation
{
    public readonly struct VnIntroBeatState
    {
        public readonly string BackgroundId;
        public readonly string Speaker;
        public readonly string PanelStyle;
        public readonly string PortraitId;

        public VnIntroBeatState(string backgroundId, string speaker, string panelStyle, string portraitId)
        {
            BackgroundId = backgroundId ?? string.Empty;
            Speaker = speaker ?? string.Empty;
            PanelStyle = panelStyle ?? string.Empty;
            PortraitId = portraitId ?? string.Empty;
        }
    }

    public sealed class VnIntroController : MonoBehaviour
    {
        private const string BeatCommand = "vn_beat";
        private const string IntroNode = "VnIntro";

        private YarnProject? yarnProject;
        private DialogueRunner? dialogueRunner;
        private VnIntroDialoguePresenter? presenter;
        private DialogueRunner? commandRunner;
        private bool configured;
        private bool completionRaised;

        public VnIntroBeatState CurrentBeat { get; private set; }
        public bool IsPaused => presenter != null && presenter.IsPaused;
        public bool HasPendingLine => presenter != null && presenter.HasPendingLine;

        public event Action<VnIntroBeatState>? BeatChanged;
        public event Action<string, string>? LinePresented;
        public event Action? DialogueCompleted;

        public void Configure(YarnProject project, DialogueRunner runner, VnIntroDialoguePresenter dialoguePresenter)
        {
            yarnProject = project ?? throw new ArgumentNullException(nameof(project));
            dialogueRunner = runner ?? throw new ArgumentNullException(nameof(runner));
            presenter = dialoguePresenter ?? throw new ArgumentNullException(nameof(dialoguePresenter));

            presenter.LinePresented -= HandleLinePresented;
            presenter.LinePresented += HandleLinePresented;
            dialogueRunner.SetProject(yarnProject);
            dialogueRunner.DialoguePresenters = new[] { presenter };
            RegisterCommands(dialogueRunner);
            dialogueRunner.onDialogueComplete ??= new UnityEvent();
            dialogueRunner.onDialogueComplete.RemoveListener(HandleDialogueComplete);
            dialogueRunner.onDialogueComplete.AddListener(HandleDialogueComplete);
            configured = true;
            completionRaised = false;
        }

        public async YarnTask StartIntro()
        {
            EnsureConfigured();
            if (!HasNode(IntroNode))
            {
                throw new InvalidOperationException("VN intro Yarn project is missing node '" + IntroNode + "'.");
            }
            completionRaised = false;
            await dialogueRunner!.StartDialogue(IntroNode);
        }

        public bool Continue()
        {
            return presenter != null && presenter.Continue();
        }

        public void SetPaused(bool paused)
        {
            presenter?.SetPaused(paused);
        }

        public async void Skip()
        {
            if (!configured || dialogueRunner == null)
            {
                return;
            }

            presenter?.SetPaused(false);
            presenter?.CancelCurrentLine();
            if (dialogueRunner.IsDialogueRunning)
            {
                await dialogueRunner.Stop();
            }
            else
            {
                HandleDialogueComplete();
            }
        }

        public static VnIntroBeatState MapBeat(string backgroundId, string speaker, string panelStyle, string portraitId)
        {
            if (!string.Equals(backgroundId, "bus_stop", StringComparison.Ordinal) &&
                !string.Equals(backgroundId, "night_sky", StringComparison.Ordinal) &&
                !string.Equals(backgroundId, "phone", StringComparison.Ordinal))
            {
                throw new ArgumentException("Unknown VN intro background id: " + (backgroundId ?? string.Empty), nameof(backgroundId));
            }
            if (!string.Equals(panelStyle, "dark", StringComparison.Ordinal) &&
                !string.Equals(panelStyle, "light", StringComparison.Ordinal))
            {
                throw new ArgumentException("Unknown VN intro panel style: " + (panelStyle ?? string.Empty), nameof(panelStyle));
            }
            return new VnIntroBeatState(backgroundId, speaker, panelStyle, portraitId);
        }

        private void ApplyBeat(string backgroundId, string speaker, string panelStyle, string portraitId)
        {
            CurrentBeat = MapBeat(backgroundId, speaker, panelStyle, portraitId);
            Action<VnIntroBeatState>? handler = BeatChanged;
            handler?.Invoke(CurrentBeat);
        }

        private void HandleLinePresented(string speaker, string text)
        {
            Action<string, string>? handler = LinePresented;
            handler?.Invoke(speaker, text);
        }

        private void HandleDialogueComplete()
        {
            if (completionRaised)
            {
                return;
            }
            completionRaised = true;
            presenter?.CancelCurrentLine();
            Action? handler = DialogueCompleted;
            handler?.Invoke();
        }

        private bool HasNode(string nodeName)
        {
            if (yarnProject == null)
            {
                return false;
            }
            string[] nodes = yarnProject.NodeNames;
            for (int index = 0; index < nodes.Length; index++)
            {
                if (string.Equals(nodes[index], nodeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private void RegisterCommands(DialogueRunner runner)
        {
            if (commandRunner != null)
            {
                commandRunner.RemoveCommandHandler(BeatCommand);
            }
            commandRunner = runner;
            commandRunner.AddCommandHandler<string, string, string, string>(BeatCommand, ApplyBeat);
        }

        private void EnsureConfigured()
        {
            if (!configured || yarnProject == null || dialogueRunner == null || presenter == null)
            {
                throw new InvalidOperationException("VN intro controller is not configured.");
            }
        }

        private void OnDestroy()
        {
            if (presenter != null)
            {
                presenter.LinePresented -= HandleLinePresented;
                presenter.CancelCurrentLine();
            }
            if (dialogueRunner != null && dialogueRunner.onDialogueComplete != null)
            {
                dialogueRunner.onDialogueComplete.RemoveListener(HandleDialogueComplete);
            }
            if (commandRunner != null)
            {
                commandRunner.RemoveCommandHandler(BeatCommand);
                commandRunner = null;
            }
        }
    }
}
