#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Rokas.Core;
using UnityEngine;
using Yarn.Unity;

namespace Rokas.Presentation
{
    public sealed class MessagesReplyOption
    {
        public readonly string choiceId;
        public readonly string text;

        public MessagesReplyOption(string choiceId, string text)
        {
            this.choiceId = choiceId ?? string.Empty;
            this.text = text ?? string.Empty;
        }
    }

    public sealed class MessagesDialoguePresenter : DialoguePresenterBase
    {
        private readonly List<MessagesReplyOption> currentOptions = new List<MessagesReplyOption>();
        private DialogueOption[] pendingOptions = Array.Empty<DialogueOption>();
        private YarnTaskCompletionSource<DialogueOption?>? pendingSelection;
        private MessageService? messages;
        private string contactId = string.Empty;
        private bool selectionQueued;
        private bool optionsChangedPending;

        public IReadOnlyList<MessagesReplyOption> CurrentOptions => currentOptions;
        public bool HasPendingOptions => pendingSelection != null;

        public event Action? OptionsChanged;

        public void Bind(MessageService messageService, string boundContactId)
        {
            messages = messageService ?? throw new ArgumentNullException(nameof(messageService));
            if (string.IsNullOrEmpty(boundContactId))
            {
                throw new ArgumentException("Messages Yarn presenter requires a contact id.", nameof(boundContactId));
            }
            contactId = boundContactId;
        }

        public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            EnsureBound();
            string stableId = RequireStableId(line.TextID, "line");
            string text = ExtractLineText(line);
            messages!.DeliverIncoming(stableId, contactId, text);
            return YarnTask.CompletedTask;
        }

        public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken token)
        {
            EnsureBound();
            if (pendingSelection != null)
            {
                throw new InvalidOperationException("Messages Yarn presenter already has a pending option set.");
            }

            selectionQueued = false;
            currentOptions.Clear();
            List<DialogueOption> available = new List<DialogueOption>();
            for (int index = 0; index < dialogueOptions.Length; index++)
            {
                DialogueOption option = dialogueOptions[index];
                if (option == null || !option.IsAvailable)
                {
                    continue;
                }
                RequireStableId(option.Line.TextID, "choice");
                available.Add(option);
            }

            DialogueOption? persisted = FindPersistedSelection(available);
            if (persisted != null)
            {
                return persisted;
            }

            for (int index = 0; index < available.Count; index++)
            {
                DialogueOption option = available[index];
                string stableId = RequireStableId(option.Line.TextID, "choice");
                currentOptions.Add(new MessagesReplyOption(stableId, option.Line.Text.Text.Trim()));
            }

            pendingOptions = available.ToArray();
            pendingSelection = new YarnTaskCompletionSource<DialogueOption?>();
            NotifyOptionsChanged();

            DialogueOption? selected = await pendingSelection.Task;
            pendingSelection = null;
            pendingOptions = Array.Empty<DialogueOption>();
            selectionQueued = false;
            currentOptions.Clear();
            NotifyOptionsChanged();
            return selected;
        }

        public bool SubmitChoice(string choiceId)
        {
            EnsureBound();
            if (pendingSelection == null || selectionQueued || string.IsNullOrEmpty(choiceId))
            {
                return false;
            }

            for (int index = 0; index < pendingOptions.Length; index++)
            {
                DialogueOption option = pendingOptions[index];
                string stableId = RequireStableId(option.Line.TextID, "choice");
                if (!string.Equals(stableId, choiceId, StringComparison.Ordinal))
                {
                    continue;
                }

                selectionQueued = true;
                string branchState = InferBranchState(stableId);
                string text = option.Line.Text.Text.Trim();
                StartCoroutine(CompleteChoiceNextFrame(option, stableId, text, branchState));
                return true;
            }
            return false;
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            pendingSelection?.TrySetResult(null);
            pendingSelection = null;
            pendingOptions = Array.Empty<DialogueOption>();
            selectionQueued = false;
            currentOptions.Clear();
            NotifyOptionsChanged();
            return YarnTask.CompletedTask;
        }

        public override void OnNodeExit(string nodeName)
        {
            EnsureBound();
            messages!.MarkDialogueCompleted(contactId, nodeName);
        }

        private DialogueOption? FindPersistedSelection(List<DialogueOption> available)
        {
            ConversationState? conversation = messages!.GetConversation(contactId);
            if (conversation == null || conversation.selectedChoiceIds == null || conversation.selectedChoiceIds.Count == 0)
            {
                return null;
            }

            for (int optionIndex = 0; optionIndex < available.Count; optionIndex++)
            {
                DialogueOption option = available[optionIndex];
                string stableId = RequireStableId(option.Line.TextID, "choice");
                for (int selectedIndex = 0; selectedIndex < conversation.selectedChoiceIds.Count; selectedIndex++)
                {
                    if (string.Equals(conversation.selectedChoiceIds[selectedIndex], stableId, StringComparison.Ordinal))
                    {
                        return option;
                    }
                }
            }
            return null;
        }

        private IEnumerator CompleteChoiceNextFrame(DialogueOption option, string stableId, string text, string branchState)
        {
            yield return null;
            if (pendingSelection == null || messages == null)
            {
                selectionQueued = false;
                yield break;
            }
            if (!messages.SelectChoice(contactId, stableId, text, branchState))
            {
                selectionQueued = false;
                yield break;
            }
            pendingSelection.TrySetResult(option);
        }

        private void LateUpdate()
        {
            if (!optionsChangedPending)
            {
                return;
            }
            optionsChangedPending = false;
            Action? handler = OptionsChanged;
            handler?.Invoke();
        }

        private void OnDisable()
        {
            optionsChangedPending = false;
        }

        private string InferBranchState(string choiceId)
        {
            if (choiceId.EndsWith("kaito_choice_details", StringComparison.Ordinal))
            {
                return "details";
            }
            if (choiceId.EndsWith("kaito_choice_skeptic", StringComparison.Ordinal))
            {
                return "skeptic";
            }

            ConversationState? conversation = messages!.GetConversation(contactId);
            if (conversation != null && !string.IsNullOrEmpty(conversation.branchState))
            {
                return conversation.branchState;
            }
            return choiceId;
        }

        private void EnsureBound()
        {
            if (messages == null || string.IsNullOrEmpty(contactId))
            {
                throw new InvalidOperationException("Messages Yarn presenter is not bound to MessageService/contact.");
            }
        }

        private static string RequireStableId(string? value, string kind)
        {
            if (string.IsNullOrEmpty(value) || string.Equals(value, "<unknown>", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Messages Yarn " + kind + " is missing an explicit stable #line id.");
            }
            const string prefix = "line:";
            return value.StartsWith(prefix, StringComparison.Ordinal) ? value.Substring(prefix.Length) : value;
        }

        private static string ExtractLineText(LocalizedLine line)
        {
            string text = line.Text.Text.Trim();
            string? characterName = line.CharacterName;
            if (!string.IsNullOrEmpty(characterName))
            {
                string prefix = characterName + ":";
                if (text.StartsWith(prefix, StringComparison.Ordinal))
                {
                    text = text.Substring(prefix.Length).TrimStart();
                }
            }
            return text;
        }

        private void NotifyOptionsChanged()
        {
            optionsChangedPending = true;
        }
    }
}
