#nullable enable
using System;
using System.Collections.Generic;
using Rokas.Core;
using UnityEngine;
using Yarn.Unity;

namespace Rokas.Presentation
{
    public sealed class MessagesYarnController : MonoBehaviour
    {
        private const string CoordinatesCommand = "rokas_coordinates";

        [SerializeField] private YarnProject? yarnProject;
        [SerializeField] private DialogueRunner? dialogueRunner;
        [SerializeField] private MessagesDialoguePresenter? presenter;
        [SerializeField] private string contactId = "kaito";

        private MessageService? messages;
        private DialogueRunner? commandRunner;

        public string LastError { get; private set; } = string.Empty;
        public IReadOnlyList<MessagesReplyOption> CurrentOptions => presenter != null ? presenter.CurrentOptions : Array.Empty<MessagesReplyOption>();

        public event Action<string>? DiagnosticChanged;

        public void Configure(MessageService messageService, YarnProject project, DialogueRunner runner, MessagesDialoguePresenter dialoguePresenter, string boundContactId)
        {
            messages = messageService ?? throw new ArgumentNullException(nameof(messageService));
            yarnProject = project ?? throw new ArgumentNullException(nameof(project));
            dialogueRunner = runner ?? throw new ArgumentNullException(nameof(runner));
            presenter = dialoguePresenter ?? throw new ArgumentNullException(nameof(dialoguePresenter));
            if (string.IsNullOrEmpty(boundContactId))
            {
                throw new ArgumentException("Messages Yarn controller requires a contact id.", nameof(boundContactId));
            }
            contactId = boundContactId;

            presenter.Bind(messages, contactId);
            dialogueRunner.SetProject(yarnProject);
            dialogueRunner.DialoguePresenters = new[] { presenter };
            RegisterCommands(dialogueRunner);
            RestoreBranchState();
            SetDiagnostic(string.Empty);
        }

        public async YarnTask StartDialogue(string nodeName)
        {
            if (!TryValidateStart(nodeName, out string error))
            {
                SetDiagnostic(error);
                throw new InvalidOperationException(error);
            }

            SetDiagnostic(string.Empty);
            if (messages!.IsDialogueCompleted(contactId, nodeName))
            {
                return;
            }

            try
            {
                await dialogueRunner!.StartDialogue(nodeName);
                messages.MarkDialogueCompleted(contactId, nodeName);
            }
            catch (Exception exception)
            {
                string failure = "Messages dialogue failed [contact=" + contactId + ", node=" + nodeName + "]: " + exception.Message;
                SetDiagnostic(failure);
                throw;
            }
        }

        public bool SubmitChoice(string choiceId)
        {
            if (presenter == null)
            {
                SetDiagnostic("Messages dialogue choice failed [contact=" + contactId + "]: presenter is not configured.");
                return false;
            }
            if (!presenter.SubmitChoice(choiceId))
            {
                SetDiagnostic("Messages dialogue choice failed [contact=" + contactId + ", choice=" + (choiceId ?? string.Empty) + "]: choice is unavailable or already recorded.");
                return false;
            }
            SetDiagnostic(string.Empty);
            return true;
        }

        private bool TryValidateStart(string nodeName, out string error)
        {
            if (messages == null || yarnProject == null || dialogueRunner == null || presenter == null)
            {
                error = "Messages dialogue cannot start [contact=" + contactId + ", node=" + (nodeName ?? string.Empty) + "]: controller is not configured.";
                return false;
            }
            if (string.IsNullOrEmpty(nodeName))
            {
                error = "Messages dialogue cannot start [contact=" + contactId + "]: node identity is empty.";
                return false;
            }

            string[] nodes = yarnProject.NodeNames;
            for (int index = 0; index < nodes.Length; index++)
            {
                if (string.Equals(nodes[index], nodeName, StringComparison.Ordinal))
                {
                    error = string.Empty;
                    return true;
                }
            }
            error = "Messages dialogue cannot start [contact=" + contactId + ", node=" + nodeName + "]: node was not found in the bound ROKAS Yarn project.";
            return false;
        }

        private void RegisterCommands(DialogueRunner runner)
        {
            if (commandRunner != null)
            {
                commandRunner.RemoveCommandHandler(CoordinatesCommand);
            }
            commandRunner = runner;
            commandRunner.AddCommandHandler<string, string, string, string>(CoordinatesCommand, DeliverCoordinates);
        }

        private void DeliverCoordinates(string eventId, string title, string body, string targetId)
        {
            if (messages == null)
            {
                SetDiagnostic("Messages attachment failed [contact=" + contactId + "]: MessageService is not configured.");
                return;
            }
            MessageAttachment attachment = new MessageAttachment
            {
                kind = MessageAttachmentKind.Coordinates,
                id = eventId ?? string.Empty,
                title = title ?? string.Empty,
                body = body ?? string.Empty,
                targetId = targetId ?? string.Empty
            };
            messages.DeliverIncoming(eventId, contactId, title ?? string.Empty, attachment);
        }

        private void RestoreBranchState()
        {
            if (messages == null || dialogueRunner == null)
            {
                return;
            }
            ConversationState? conversation = messages.GetConversation(contactId);
            if (conversation == null)
            {
                return;
            }
            if (string.Equals(conversation.branchState, "details", StringComparison.Ordinal) ||
                string.Equals(conversation.branchState, "skeptic", StringComparison.Ordinal))
            {
                dialogueRunner.VariableStorage.SetValue("$kaitoApproach", conversation.branchState);
            }
        }

        private void SetDiagnostic(string value)
        {
            LastError = value ?? string.Empty;
            Action<string>? handler = DiagnosticChanged;
            handler?.Invoke(LastError);
            if (!string.IsNullOrEmpty(LastError))
            {
                Debug.LogWarning(LastError, this);
            }
        }

        private void OnDestroy()
        {
            if (commandRunner != null)
            {
                commandRunner.RemoveCommandHandler(CoordinatesCommand);
                commandRunner = null;
            }
        }
    }
}
