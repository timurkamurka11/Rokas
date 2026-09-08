using System;
using System.Collections.Generic;

namespace Rokas.Core
{
    public enum MessageAttachmentKind
    {
        None,
        Coordinates,
        Contract,
        FoodGift
    }

    public enum MessageAttachmentActionStatus
    {
        Activated,
        AlreadyActive,
        MissingDestination,
        UnknownDestination,
        MissingContract,
        UnknownContract,
        ContractUnavailable,
        MissingFood,
        UnknownFood,
        FoodStorageUnavailable,
        MessageNotFound,
        MissingAttachment,
        UnsupportedAttachment
    }

    public sealed class MessageAttachmentActionResult
    {
        public MessageAttachmentActionStatus Status { get; private set; }
        public string TargetId { get; private set; }
        public bool Succeeded
        {
            get
            {
                return Status == MessageAttachmentActionStatus.Activated ||
                    Status == MessageAttachmentActionStatus.AlreadyActive;
            }
        }

        public MessageAttachmentActionResult(MessageAttachmentActionStatus status, string targetId)
        {
            Status = status;
            TargetId = targetId ?? string.Empty;
        }
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
        public string chainId = string.Empty;
        public bool suppressMainNotification;
        public string reactionId = string.Empty;
        public MessageAttachment attachment;
    }

    [Serializable]
    public sealed class LiveTopicProgress
    {
        public string topicId = string.Empty;
        public int completedCount;
        public string lastContext = string.Empty;
    }

    [Serializable]
    public sealed class LivePendingChainState
    {
        public string contactId = string.Empty;
        public string chainId = string.Empty;
        public string scriptId = string.Empty;
        public string flowIdentity = string.Empty;
        public int nextBubbleIndex;
    }

    [Serializable]
    public sealed class ConversationState
    {
        public string contactId = string.Empty;
        public int unreadCount;
        public int lastSequence;
        public string branchState = string.Empty;
        public string liveActiveTopicId = string.Empty;
        public string liveFlowIdentity = string.Empty;
        public bool liveWaitingForChoice;
        public List<LiveTopicProgress> liveTopicProgress = new List<LiveTopicProgress>();
        public List<string> selectedChoiceIds = new List<string>();
        public List<string> completedDialogueIds = new List<string>();
        public List<MessageEntry> entries = new List<MessageEntry>();
    }

    [Serializable]
    public sealed class MessageSaveData
    {
        public int nextSequence;
        public int nextLiveSequence;
        public List<string> deliveredEventIds = new List<string>();
        public List<ConversationState> conversations = new List<ConversationState>();
        public List<LivePendingChainState> livePendingChains = new List<LivePendingChainState>();
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
        private const string EastB7DestinationId = "east-b7";
        private const string YumikoGiftEventId = "yumiko-gift:kisaragi-green-tea-001";
        private const string YumikoPurchaseText = "Зелёный чай YOMI? Хороший выбор. Только не пей его залпом перед выходом.";
        private const string YumikoRecommendationText = "На Кисараги? Перед выходом загляни в YOMI Kitchen. Зелёный чай там будет кстати.";
        private const string YumikoGiftText = "И ещё. Не спорь — это за мой счёт.";

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
        private readonly ContractDefinition contract;
        private readonly ContractService contracts;
        private readonly FoodService food;

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
            : this(state, null, null, new FoodService())
        {
        }

        internal MessageService(SaveData state, ContractDefinition contract, ContractService contracts)
            : this(state, contract, contracts, new FoodService())
        {
        }

        internal MessageService(SaveData state, ContractDefinition contract, ContractService contracts, FoodService food)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            this.state = state;
            this.contract = contract;
            this.contracts = contracts;
            this.food = food;
            if (state.messages == null)
            {
                state.messages = new MessageSaveData();
            }
            state.activeDestinationId = state.activeDestinationId ?? string.Empty;
            data = state.messages;
            Normalize();
        }

        public bool DeliverIncoming(string eventId, string contactId, string text)
        {
            return DeliverIncomingInternal(eventId, contactId, text, null, string.Empty, false);
        }

        public bool DeliverIncoming(string eventId, string contactId, string text, MessageAttachment attachment)
        {
            return DeliverIncomingInternal(eventId, contactId, text, attachment, string.Empty, false);
        }

        public bool DeliverIncomingLive(string eventId, string contactId, string text, MessageAttachment attachment,
            string chainId, bool suppressMainNotification)
        {
            return DeliverIncomingInternal(eventId, contactId, text, attachment, chainId, suppressMainNotification);
        }

        private bool DeliverIncomingInternal(string eventId, string contactId, string text, MessageAttachment attachment,
            string chainId, bool suppressMainNotification)
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
                chainId = chainId ?? string.Empty,
                suppressMainNotification = suppressMainNotification,
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

        public bool SendOutgoing(string eventId, string contactId, string text, string choiceId, string chainId)
        {
            if (string.IsNullOrEmpty(eventId) || !KnownContact(contactId) || text == null ||
                ContainsOrdinal(data.deliveredEventIds, eventId))
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
                outgoing = true,
                choiceId = choiceId ?? string.Empty,
                sequence = sequence,
                chainId = chainId ?? string.Empty
            });
            conversation.lastSequence = sequence;
            data.deliveredEventIds.Add(eventId);
            NotifyChanged();
            return true;
        }

        public bool SetReaction(string contactId, string messageId, string reactionId)
        {
            ConversationState conversation = FindConversation(contactId);
            if (conversation == null || string.IsNullOrEmpty(messageId)) return false;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry == null || !string.Equals(entry.messageId, messageId, StringComparison.Ordinal)) continue;
                string next = reactionId ?? string.Empty;
                if (string.Equals(entry.reactionId ?? string.Empty, next, StringComparison.Ordinal)) return false;
                entry.reactionId = next;
                NotifyChanged();
                return true;
            }
            return false;
        }

        public bool HasDeliveredEvent(string eventId)
        {
            return !string.IsNullOrEmpty(eventId) && ContainsOrdinal(data.deliveredEventIds, eventId);
        }

        internal MessageSaveData SaveData { get { return data; } }

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

        public bool IsDialogueCompleted(string contactId, string dialogueId)
        {
            if (!KnownContact(contactId) || string.IsNullOrEmpty(dialogueId))
            {
                return false;
            }

            ConversationState conversation = FindConversation(contactId);
            return conversation != null && ContainsOrdinal(conversation.completedDialogueIds, dialogueId);
        }

        public bool MarkDialogueCompleted(string contactId, string dialogueId)
        {
            if (!KnownContact(contactId) || string.IsNullOrEmpty(dialogueId))
            {
                return false;
            }

            ConversationState conversation = EnsureConversation(contactId);
            if (ContainsOrdinal(conversation.completedDialogueIds, dialogueId))
            {
                return false;
            }

            conversation.completedDialogueIds.Add(dialogueId);
            NotifyChanged();
            return true;
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

        public MessageAttachmentActionResult ActivateAttachment(string contactId, string messageId)
        {
            ConversationState conversation = FindConversation(contactId);
            if (conversation == null || string.IsNullOrEmpty(messageId))
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.MessageNotFound, string.Empty);
            }

            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry == null || !string.Equals(entry.messageId, messageId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (entry.attachment == null)
                {
                    return new MessageAttachmentActionResult(MessageAttachmentActionStatus.MissingAttachment, string.Empty);
                }
                if (entry.attachment.kind == MessageAttachmentKind.Contract)
                {
                    return ActivateContractAttachment(entry.attachment);
                }
                if (entry.attachment.kind == MessageAttachmentKind.FoodGift)
                {
                    return ActivateFoodGiftAttachment(entry.attachment);
                }
                if (entry.attachment.kind != MessageAttachmentKind.Coordinates)
                {
                    return new MessageAttachmentActionResult(
                        MessageAttachmentActionStatus.UnsupportedAttachment, entry.attachment.targetId);
                }

                string targetId = entry.attachment.targetId ?? string.Empty;
                if (targetId.Length == 0)
                {
                    return new MessageAttachmentActionResult(MessageAttachmentActionStatus.MissingDestination, string.Empty);
                }
                if (!IsKnownCoordinateDestination(targetId))
                {
                    return new MessageAttachmentActionResult(MessageAttachmentActionStatus.UnknownDestination, targetId);
                }
                if (entry.attachment.opened && string.Equals(state.activeDestinationId, targetId, StringComparison.Ordinal))
                {
                    return new MessageAttachmentActionResult(MessageAttachmentActionStatus.AlreadyActive, targetId);
                }

                state.activeDestinationId = targetId;
                entry.attachment.opened = true;
                NotifyChanged();
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.Activated, targetId);
            }

            return new MessageAttachmentActionResult(MessageAttachmentActionStatus.MessageNotFound, string.Empty);
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

        internal bool DeliverYumikoPaidFoodPurchase(string foodId)
        {
            if (foodId != FoodService.GreenTeaId)
            {
                return false;
            }
            return DeliverIncoming(
                "yumiko-food-first:" + foodId,
                "yumiko",
                YumikoPurchaseText);
        }

        internal bool DeliverYumikoContractContext(string contractId)
        {
            if (string.IsNullOrEmpty(contractId) || state.contractRunSequence <= 0)
            {
                return false;
            }

            string chainId = "live:yumiko:contract:" + contractId + ":" + state.contractRunSequence;
            bool delivered = DeliverIncomingLive(
                "yumiko-contract-food:" + contractId + ":" + state.contractRunSequence,
                "yumiko",
                YumikoRecommendationText,
                null,
                chainId,
                false);
            bool giftDelivered = DeliverIncomingLive(
                YumikoGiftEventId,
                "yumiko",
                YumikoGiftText,
                new MessageAttachment
                {
                    kind = MessageAttachmentKind.FoodGift,
                    id = "yumiko_green_tea_gift_attachment",
                    title = "Зелёный чай YOMI",
                    body = "Подарок от Юмико ×1",
                    targetId = FoodService.GreenTeaId
                },
                chainId,
                true);
            return delivered || giftDelivered;
        }

        internal bool DeliverYumikoReturn(string contractId, int runIdentity, RunPhase result, string preparedFoodId)
        {
            if (string.IsNullOrEmpty(contractId) || runIdentity <= 0 ||
                (result != RunPhase.Sealed && result != RunPhase.Failed))
            {
                return false;
            }

            bool usedFood = !string.IsNullOrEmpty(preparedFoodId);
            string resultId;
            string text;
            if (result == RunPhase.Sealed)
            {
                resultId = "sealed";
                text = usedFood
                    ? "Вернулся. Значит, всё-таки пригодилось."
                    : "Вернулся. Хорошо. В следующий раз хотя бы возьми что-нибудь с собой.";
            }
            else
            {
                resultId = "failed";
                text = usedFood
                    ? "Вернулся — уже хорошо. В следующий раз подберём что-нибудь получше."
                    : "Ты опять пошёл туда без нормальной подготовки?";
            }

            return DeliverIncoming(
                "yumiko-return:" + contractId + ":" + runIdentity + ":" + resultId,
                "yumiko",
                text);
        }

        private MessageAttachmentActionResult ActivateContractAttachment(MessageAttachment attachment)
        {
            string targetId = attachment.targetId ?? string.Empty;
            if (attachment.opened)
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.AlreadyActive, targetId);
            }
            if (targetId.Length == 0)
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.MissingContract, string.Empty);
            }
            if (contract == null || !string.Equals(targetId, contract.id, StringComparison.Ordinal))
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.UnknownContract, targetId);
            }
            if (contracts == null || state.phase != RunPhase.Home)
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.ContractUnavailable, targetId);
            }
            if (!contracts.Accept(state, contract))
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.ContractUnavailable, targetId);
            }

            attachment.opened = true;
            bool delivered = DeliverIncoming(
                "guild-contract-accepted:" + targetId,
                "guild",
                "Контракт принят. Подготовьтесь к выходу на задание.");
            bool yumikoDelivered = DeliverYumikoContractContext(targetId);
            if (!delivered && !yumikoDelivered)
            {
                NotifyChanged();
            }
            return new MessageAttachmentActionResult(MessageAttachmentActionStatus.Activated, targetId);
        }

        private MessageAttachmentActionResult ActivateFoodGiftAttachment(MessageAttachment attachment)
        {
            string targetId = attachment.targetId ?? string.Empty;
            if (attachment.opened)
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.AlreadyActive, targetId);
            }
            if (targetId.Length == 0)
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.MissingFood, string.Empty);
            }
            if (food == null || !food.IsKnownFood(targetId))
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.UnknownFood, targetId);
            }
            if (!food.GrantStoredFood(state, targetId))
            {
                return new MessageAttachmentActionResult(MessageAttachmentActionStatus.FoodStorageUnavailable, targetId);
            }

            attachment.opened = true;
            NotifyChanged();
            return new MessageAttachmentActionResult(MessageAttachmentActionStatus.Activated, targetId);
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
            if (data.livePendingChains == null)
            {
                data.livePendingChains = new List<LivePendingChainState>();
            }
            if (data.nextLiveSequence < 0)
            {
                data.nextLiveSequence = 0;
            }
            for (int pendingIndex = data.livePendingChains.Count - 1; pendingIndex >= 0; pendingIndex--)
            {
                LivePendingChainState pending = data.livePendingChains[pendingIndex];
                if (pending == null || string.IsNullOrEmpty(pending.contactId) || string.IsNullOrEmpty(pending.chainId) ||
                    string.IsNullOrEmpty(pending.scriptId))
                {
                    data.livePendingChains.RemoveAt(pendingIndex);
                    continue;
                }
                pending.flowIdentity = pending.flowIdentity ?? string.Empty;
                if (pending.nextBubbleIndex < 0) pending.nextBubbleIndex = 0;
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
                if (conversation.completedDialogueIds == null)
                {
                    conversation.completedDialogueIds = new List<string>();
                }
                if (conversation.branchState == null)
                {
                    conversation.branchState = string.Empty;
                }
                conversation.liveActiveTopicId = conversation.liveActiveTopicId ?? string.Empty;
                conversation.liveFlowIdentity = conversation.liveFlowIdentity ?? string.Empty;
                if (conversation.liveTopicProgress == null)
                {
                    conversation.liveTopicProgress = new List<LiveTopicProgress>();
                }
                for (int progressIndex = conversation.liveTopicProgress.Count - 1; progressIndex >= 0; progressIndex--)
                {
                    LiveTopicProgress progress = conversation.liveTopicProgress[progressIndex];
                    if (progress == null || string.IsNullOrEmpty(progress.topicId))
                    {
                        conversation.liveTopicProgress.RemoveAt(progressIndex);
                        continue;
                    }
                    progress.lastContext = progress.lastContext ?? string.Empty;
                    if (progress.completedCount < 0) progress.completedCount = 0;
                }
                if (conversation.unreadCount < 0)
                {
                    conversation.unreadCount = 0;
                }
                Deduplicate(conversation.selectedChoiceIds);
                Deduplicate(conversation.completedDialogueIds);
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
                    entry.chainId = entry.chainId ?? string.Empty;
                    entry.reactionId = entry.reactionId ?? string.Empty;
                    if (entry.attachment != null)
                    {
                        entry.attachment.id = entry.attachment.id ?? string.Empty;
                        entry.attachment.title = entry.attachment.title ?? string.Empty;
                        entry.attachment.body = entry.attachment.body ?? string.Empty;
                        entry.attachment.targetId = entry.attachment.targetId ?? string.Empty;
                    }
                    highestSequence = Math.Max(highestSequence, entry.sequence);
                }
            }
            data.nextSequence = highestSequence;
        }

        private static bool IsKnownCoordinateDestination(string targetId)
        {
            return string.Equals(targetId, EastB7DestinationId, StringComparison.Ordinal);
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
