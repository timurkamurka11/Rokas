using System;
using System.Collections.Generic;

namespace Rokas.Core
{
    public enum MessageAttachmentKind
    {
        None,
        Coordinates,
        Contract
    }

    [Serializable]
    public sealed class MessageAttachment
    {
        public MessageAttachmentKind kind;
        public string id = string.Empty;
        public string title = string.Empty;
        public string body = string.Empty;
        public string targetId = string.Empty;
        public bool opened;
    }

    [Serializable]
    public sealed class MessageEntry
    {
        public string messageId = string.Empty;
        public string eventId = string.Empty;
        public string text = string.Empty;
        public bool outgoing;
        public string choiceId = string.Empty;
        public int sequence;
        public MessageAttachment attachment;
    }

    [Serializable]
    public sealed class ConversationState
    {
        public string contactId = string.Empty;
        public int unreadCount;
        public int lastSequence;
        public string branchState = string.Empty;
        public List<string> selectedChoiceIds = new List<string>();
        public List<MessageEntry> entries = new List<MessageEntry>();
    }

    [Serializable]
    public sealed class MessageSaveData
    {
        public int nextSequence;
        public List<string> deliveredEventIds = new List<string>();
        public List<ConversationState> conversations = new List<ConversationState>();
    }

    public sealed class ContactDefinition
    {
        public readonly string id;
        public readonly string displayName;
        public readonly string role;
        public readonly string portraitResource;
        public readonly bool online;

        public ContactDefinition(string id, string displayName, string role, string portraitResource, bool online)
        {
            this.id = id ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.role = role ?? string.Empty;
            this.portraitResource = portraitResource ?? string.Empty;
            this.online = online;
        }
    }

    public sealed class MessageService
    {
        private static readonly ContactDefinition[] Contacts =
        {
            new ContactDefinition("kaito", "Kaito", "Field Contact", "Messages/Portraits/Kaito", true),
            new ContactDefinition("yumiko", "Yumiko", "YOMI Kitchen", "Messages/Portraits/Yumiko", true),
            new ContactDefinition("mika", "Mika", "Hunter Contact", "Messages/Portraits/Mika", true),
            new ContactDefinition("guild", "Guild", "Guild Dispatch", "Messages/Portraits/Guild", true),
            new ContactDefinition("unknown", "Unknown", "Unknown Sender", "Messages/Portraits/Unknown", false),
            new ContactDefinition("merchant", "Merchant", "Night Merchant", "Messages/Portraits/Merchant", true)
        };

        private readonly SaveData state;
        private readonly MessageSaveData data;

        public event Action Changed;

        public int TotalUnread
        {
            get
            {
                int total = 0;
                for (int index = 0; index < data.conversations.Count; index++)
                {
                    ConversationState conversation = data.conversations[index];
                    if (conversation == null || conversation.unreadCount <= 0)
                    {
                        continue;
                    }
                    if (int.MaxValue - total < conversation.unreadCount)
                    {
                        return int.MaxValue;
                    }
                    total += conversation.unreadCount;
                }
                return total;
            }
        }

        public MessageService(SaveData state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            this.state = state;
            if (state.messages == null)
            {
                state.messages = new MessageSaveData();
            }
            data = state.messages;
            Normalize();
        }

        public bool DeliverIncoming(string eventId, string contactId, string text)
        {
            return DeliverIncoming(eventId, contactId, text, null);
        }

        public bool DeliverIncoming(string eventId, string contactId, string text, MessageAttachment attachment)
        {
            if (string.IsNullOrEmpty(eventId) || !KnownContact(contactId) || text == null)
            {
                return false;
            }
            if (ContainsOrdinal(data.deliveredEventIds, eventId))
            {
                return false;
            }

            ConversationState conversation = EnsureConversation(contactId);
            int sequence = NextSequence();
            conversation.entries.Add(new MessageEntry
            {
                messageId = "msg-" + sequence,
                eventId = eventId,
                text = text,
                outgoing = false,
                sequence = sequence,
                attachment = attachment
            });
            conversation.lastSequence = sequence;
            if (conversation.unreadCount < int.MaxValue)
            {
                conversation.unreadCount++;
            }
            data.deliveredEventIds.Add(eventId);
            NotifyChanged();
            return true;
        }

        public bool SelectChoice(string contactId, string choiceId, string text, string branchState)
        {
            if (!KnownContact(contactId) || string.IsNullOrEmpty(choiceId) || text == null || branchState == null)
            {
                return false;
            }

            ConversationState conversation = EnsureConversation(contactId);
            if (ContainsOrdinal(conversation.selectedChoiceIds, choiceId))
            {
                return false;
            }

            int sequence = NextSequence();
            conversation.selectedChoiceIds.Add(choiceId);
            conversation.branchState = branchState;
            conversation.entries.Add(new MessageEntry
            {
                messageId = "msg-" + sequence,
                text = text,
                outgoing = true,
                choiceId = choiceId,
                sequence = sequence
            });
            conversation.lastSequence = sequence;
            NotifyChanged();
            return true;
        }

        public bool OpenConversation(string contactId)
        {
            if (!KnownContact(contactId))
            {
                return false;
            }

            ConversationState conversation = EnsureConversation(contactId);
            if (conversation.unreadCount == 0)
            {
                return false;
            }

            conversation.unreadCount = 0;
            NotifyChanged();
            return true;
        }

        public ConversationState GetConversation(string contactId)
        {
            return FindConversation(contactId);
        }

        public List<ContactDefinition> SearchContacts(string query)
        {
            string needle = (query ?? string.Empty).Trim();
            List<ContactDefinition> result = new List<ContactDefinition>();
            for (int index = 0; index < Contacts.Length; index++)
            {
                ContactDefinition contact = Contacts[index];
                if (needle.Length == 0 ||
                    ContainsIgnoreCase(contact.id, needle) ||
                    ContainsIgnoreCase(contact.displayName, needle) ||
                    ContainsIgnoreCase(contact.role, needle))
                {
                    result.Add(contact);
                }
            }
            return result;
        }

        public List<ConversationState> GetOrderedConversations()
        {
            List<ConversationState> result = new List<ConversationState>();
            for (int index = 0; index < data.conversations.Count; index++)
            {
                ConversationState conversation = data.conversations[index];
                if (conversation != null && !string.IsNullOrEmpty(conversation.contactId))
                {
                    result.Add(conversation);
                }
            }
            result.Sort(delegate(ConversationState left, ConversationState right)
            {
                int sequence = right.lastSequence.CompareTo(left.lastSequence);
                if (sequence != 0)
                {
                    return sequence;
                }
                return string.Compare(left.contactId, right.contactId, StringComparison.Ordinal);
            });
            return result;
        }

        public bool MarkAttachmentOpened(string contactId, string messageId)
        {
            ConversationState conversation = FindConversation(contactId);
            if (conversation == null || string.IsNullOrEmpty(messageId))
            {
                return false;
            }

            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && entry.messageId == messageId && entry.attachment != null && !entry.attachment.opened)
                {
                    entry.attachment.opened = true;
                    NotifyChanged();
                    return true;
                }
            }
            return false;
        }

        private ConversationState EnsureConversation(string contactId)
        {
            ConversationState conversation = FindConversation(contactId);
            if (conversation != null)
            {
                return conversation;
            }

            conversation = new ConversationState { contactId = contactId };
            data.conversations.Add(conversation);
            return conversation;
        }

        private ConversationState FindConversation(string contactId)
        {
            if (string.IsNullOrEmpty(contactId))
            {
                return null;
            }
            for (int index = 0; index < data.conversations.Count; index++)
            {
                ConversationState conversation = data.conversations[index];
                if (conversation != null && string.Equals(conversation.contactId, contactId, StringComparison.Ordinal))
                {
                    return conversation;
                }
            }
            return null;
        }

        private int NextSequence()
        {
            if (data.nextSequence < int.MaxValue)
            {
                data.nextSequence++;
            }
            return data.nextSequence;
        }

        private void Normalize()
        {
            if (data.deliveredEventIds == null)
            {
                data.deliveredEventIds = new List<string>();
            }
            if (data.conversations == null)
            {
                data.conversations = new List<ConversationState>();
            }

            Deduplicate(data.deliveredEventIds);
            int highestSequence = Math.Max(0, data.nextSequence);
            for (int index = data.conversations.Count - 1; index >= 0; index--)
            {
                ConversationState conversation = data.conversations[index];
                if (conversation == null || string.IsNullOrEmpty(conversation.contactId))
                {
                    data.conversations.RemoveAt(index);
                    continue;
                }
                if (conversation.entries == null)
                {
                    conversation.entries = new List<MessageEntry>();
                }
                if (conversation.selectedChoiceIds == null)
                {
                    conversation.selectedChoiceIds = new List<string>();
                }
                if (conversation.branchState == null)
                {
                    conversation.branchState = string.Empty;
                }
                if (conversation.unreadCount < 0)
                {
                    conversation.unreadCount = 0;
                }
                Deduplicate(conversation.selectedChoiceIds);
                highestSequence = Math.Max(highestSequence, conversation.lastSequence);
                for (int entryIndex = conversation.entries.Count - 1; entryIndex >= 0; entryIndex--)
                {
                    MessageEntry entry = conversation.entries[entryIndex];
                    if (entry == null)
                    {
                        conversation.entries.RemoveAt(entryIndex);
                        continue;
                    }
                    entry.messageId = entry.messageId ?? string.Empty;
                    entry.eventId = entry.eventId ?? string.Empty;
                    entry.text = entry.text ?? string.Empty;
                    entry.choiceId = entry.choiceId ?? string.Empty;
                    highestSequence = Math.Max(highestSequence, entry.sequence);
                }
            }
            data.nextSequence = highestSequence;
        }

        private static void Deduplicate(List<string> values)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = values.Count - 1; index >= 0; index--)
            {
                string value = values[index];
                if (string.IsNullOrEmpty(value) || seen.Contains(value))
                {
                    values.RemoveAt(index);
                    continue;
                }
                seen.Add(value);
            }
        }

        private static bool ContainsOrdinal(List<string> values, string value)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool KnownContact(string contactId)
        {
            if (string.IsNullOrEmpty(contactId))
            {
                return false;
            }
            for (int index = 0; index < Contacts.Length; index++)
            {
                if (string.Equals(Contacts[index].id, contactId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsIgnoreCase(string value, string needle)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void NotifyChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
