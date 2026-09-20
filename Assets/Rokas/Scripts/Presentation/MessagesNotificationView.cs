using System;
using System.Collections.Generic;
using Rokas.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class MessagesNotificationView : IDisposable
    {
        private readonly UiKit ui;
        private readonly MessageService messages;
        private readonly RectTransform layer;
        private int observedSequence;
        private bool dirty;
        private float visibleTime;

        public MessagesNotificationView(UiKit ui, RectTransform stage, GameSession session)
        {
            if (ui == null) throw new ArgumentNullException("ui");
            if (stage == null) throw new ArgumentNullException("stage");
            if (session == null) throw new ArgumentNullException("session");

            this.ui = ui;
            messages = session.Messages;
            layer = ui.Rect(stage, "MessagesNotificationLayer", 0, 0, 1920, 1080);
            layer.SetAsLastSibling();
            observedSequence = HighestSequence();
            messages.Changed += HandleMessagesChanged;
        }

        public void Tick(float dt)
        {
            if (dirty)
            {
                dirty = false;
                int highest = HighestSequence();
                ConversationState unread = NewestUnreadConversation();
                if (unread != null && unread.lastSequence > observedSequence)
                {
                    Show(unread);
                }
                else if (messages.TotalUnread == 0)
                {
                    Hide();
                }
                observedSequence = Math.Max(observedSequence, highest);
            }

            if (visibleTime <= 0f) return;
            visibleTime = Mathf.Max(0f, visibleTime - Mathf.Max(0f, dt));
            if (visibleTime <= 0f) Hide();
        }

        public void Dispose()
        {
            messages.Changed -= HandleMessagesChanged;
        }

        private void HandleMessagesChanged()
        {
            dirty = true;
        }

        private ConversationState NewestUnreadConversation()
        {
            List<ConversationState> ordered = messages.GetOrderedConversations();
            for (int index = 0; index < ordered.Count; index++)
            {
                ConversationState conversation = ordered[index];
                if (conversation != null && conversation.unreadCount > 0 && conversation.entries != null && conversation.entries.Count > 0)
                {
                    return conversation;
                }
            }
            return null;
        }

        private int HighestSequence()
        {
            int highest = 0;
            List<ConversationState> ordered = messages.GetOrderedConversations();
            for (int index = 0; index < ordered.Count; index++)
            {
                ConversationState conversation = ordered[index];
                if (conversation != null) highest = Math.Max(highest, conversation.lastSequence);
            }
            return highest;
        }

        private void Show(ConversationState conversation)
        {
            ui.Clear(layer);
            Image card = ui.Box(layer, "MessagesNotification", 1376, 116, 464, 116,
                new Color(.025f, .075f, .105f, .97f), true);
            ui.Box(card.transform, "MessagesNotificationAccent", 0, 0, 5, 116, new Color(.24f, .73f, .86f));

            Texture2D icon = Resources.Load<Texture2D>("Messages/Icons/MessageNotification");
            if (icon != null) ui.Art(card.transform, "MessagesNotificationIcon", icon, 18, 24, 64, 64);

            MessageEntry entry = conversation.entries[conversation.entries.Count - 1];
            string contactName = ContactName(conversation.contactId);
            ui.Label(card.transform, "MessagesNotificationTitle", "НОВОЕ СООБЩЕНИЕ  /  " + contactName,
                98, 14, 342, 28, 15, UiKit.Paper);
            ui.Label(card.transform, "MessagesNotificationBody", Compact(entry != null ? entry.text : string.Empty, 52),
                98, 45, 342, 52, 16, UiKit.Muted);
            visibleTime = 4f;
        }

        private string ContactName(string contactId)
        {
            List<ContactDefinition> contacts = messages.SearchContacts(contactId ?? string.Empty);
            for (int index = 0; index < contacts.Count; index++)
            {
                if (string.Equals(contacts[index].id, contactId, StringComparison.Ordinal)) return contacts[index].displayName;
            }
            return contactId ?? string.Empty;
        }

        private static string Compact(string value, int max)
        {
            string text = (value ?? string.Empty).Replace('\n', ' ').Trim();
            if (text.Length <= max) return text;
            return text.Substring(0, Math.Max(0, max - 1)).TrimEnd() + "…";
        }

        private void Hide()
        {
            visibleTime = 0f;
            ui.Clear(layer);
        }
    }
}
