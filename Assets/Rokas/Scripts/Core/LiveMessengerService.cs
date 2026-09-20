using System;
using System.Collections.Generic;

namespace Rokas.Core
{
    public enum LiveTopicMode
    {
        Once,
        Repeatable,
        Contextual,
        Cooldown
    }

    public enum LiveMessengerSignalKind
    {
        PlayerSent,
        TypingStarted,
        IncomingDelivered,
        ReactionChanged
    }

    public sealed class LiveTopicOption
    {
        public string Id { get; private set; }
        public string Text { get; private set; }
        public LiveTopicMode Mode { get; private set; }

        internal LiveTopicOption(string id, string text, LiveTopicMode mode)
        {
            Id = id ?? string.Empty;
            Text = text ?? string.Empty;
            Mode = mode;
        }
    }

    public sealed class LiveReplyOption
    {
        public string Id { get; private set; }
        public string Text { get; private set; }

        internal LiveReplyOption(string id, string text)
        {
            Id = id ?? string.Empty;
            Text = text ?? string.Empty;
        }
    }

    public sealed class LiveReactionOption
    {
        public string Id { get; private set; }
        public string Icon { get; private set; }

        internal LiveReactionOption(string id, string icon)
        {
            Id = id ?? string.Empty;
            Icon = icon ?? string.Empty;
        }
    }

    public sealed class LiveMessengerSignal
    {
        public LiveMessengerSignalKind Kind { get; private set; }
        public string ContactId { get; private set; }
        public string MessageId { get; private set; }
        public string ChainId { get; private set; }
        public bool SuppressMainNotification { get; private set; }
        public int Sequence { get; private set; }

        internal LiveMessengerSignal(LiveMessengerSignalKind kind, string contactId, MessageEntry entry)
        {
            Kind = kind;
            ContactId = contactId ?? string.Empty;
            MessageId = entry != null ? entry.messageId ?? string.Empty : string.Empty;
            ChainId = entry != null ? entry.chainId ?? string.Empty : string.Empty;
            SuppressMainNotification = entry != null && entry.suppressMainNotification;
            Sequence = entry != null ? entry.sequence : 0;
        }

        internal LiveMessengerSignal(LiveMessengerSignalKind kind, string contactId, string chainId)
        {
            Kind = kind;
            ContactId = contactId ?? string.Empty;
            MessageId = string.Empty;
            ChainId = chainId ?? string.Empty;
            SuppressMainNotification = false;
            Sequence = 0;
        }
    }

    public sealed class LiveMessengerService
    {
        private enum ContextRule
        {
            None,
            HasContractContext,
            HasReturnContext
        }

        private sealed class ReplyDefinition
        {
            public readonly string id;
            public readonly string text;
            public readonly string[] bubbles;

            public ReplyDefinition(string id, string text, params string[] bubbles)
            {
                this.id = id;
                this.text = text;
                this.bubbles = bubbles ?? new string[0];
            }
        }

        private sealed class TopicDefinition
        {
            public readonly string id;
            public readonly string contactId;
            public readonly string playerText;
            public readonly LiveTopicMode mode;
            public readonly ContextRule contextRule;
            public readonly string[] intro;
            public readonly ReplyDefinition[] replies;

            public TopicDefinition(string id, string contactId, string playerText, LiveTopicMode mode,
                ContextRule contextRule, string[] intro, ReplyDefinition[] replies)
            {
                this.id = id;
                this.contactId = contactId;
                this.playerText = playerText;
                this.mode = mode;
                this.contextRule = contextRule;
                this.intro = intro ?? new string[0];
                this.replies = replies ?? new ReplyDefinition[0];
            }
        }

        private struct ObservedEntry
        {
            public string contactId;
            public MessageEntry entry;
        }

        private static readonly LiveReactionOption[] Reactions =
        {
            new LiveReactionOption("reaction_love", "reaction_love"),
            new LiveReactionOption("reaction_wink", "reaction_wink"),
            new LiveReactionOption("reaction_angry", "reaction_angry"),
            new LiveReactionOption("reaction_surprised", "reaction_surprised"),
            new LiveReactionOption("reaction_cry", "reaction_cry"),
            new LiveReactionOption("reaction_tasty", "reaction_tasty"),
            new LiveReactionOption("reaction_heart", "reaction_heart"),
            new LiveReactionOption("reaction_darkheart", "reaction_darkheart"),
            new LiveReactionOption("reaction_fox", "reaction_fox"),
            new LiveReactionOption("reaction_thumbsup", "reaction_thumbsup")
        };

        private static readonly TopicDefinition[] Topics = BuildTopics();
        private readonly SaveData state;
        private readonly MessageService messages;
        private readonly MessageSaveData data;
        private int observedSequence;
        private string typingContactId = string.Empty;
        private string typingChainId = string.Empty;
        private float typingRemaining;
        private string reactionTimerEventId = string.Empty;
        private float reactionRemaining;

        public event Action Changed;
        public event Action<LiveMessengerSignal> Signal;
        public bool RecoveredPendingContent { get; private set; }

        public LiveMessengerService(SaveData state, MessageService messages)
        {
            this.state = state ?? throw new ArgumentNullException("state");
            this.messages = messages ?? throw new ArgumentNullException("messages");
            data = messages.SaveData;
            RecoverPendingChains();
            RecoverPendingReactions();
            observedSequence = HighestSequence();
            messages.Changed += HandleMessagesChanged;
        }

        public List<LiveTopicOption> GetTopics(string contactId)
        {
            var result = new List<LiveTopicOption>();
            if (!SupportedContact(contactId)) return result;
            ConversationState conversation = messages.GetConversation(contactId);
            if (conversation != null && !string.IsNullOrEmpty(conversation.liveActiveTopicId)) return result;
            if (string.Equals(contactId, "kaito", StringComparison.Ordinal) &&
                !messages.IsDialogueCompleted("kaito", "Kaito_Start")) return result;

            for (int index = 0; index < Topics.Length; index++)
            {
                TopicDefinition topic = Topics[index];
                if (!string.Equals(topic.contactId, contactId, StringComparison.Ordinal)) continue;
                if (!TopicAvailable(topic, conversation)) continue;
                result.Add(new LiveTopicOption(topic.id, topic.playerText, topic.mode));
            }
            return result;
        }

        public bool StartTopic(string contactId, string topicId)
        {
            TopicDefinition topic = FindTopic(topicId);
            if (topic == null || !string.Equals(topic.contactId, contactId, StringComparison.Ordinal)) return false;
            ConversationState existing = messages.GetConversation(contactId);
            if (!TopicAvailable(topic, existing)) return false;
            if (existing != null && !string.IsNullOrEmpty(existing.liveActiveTopicId)) return false;

            int identity = NextLiveIdentity();
            string flow = "topic:" + contactId + ":" + topicId + ":" + identity;
            if (!messages.SendOutgoing(flow + ":player:topic", contactId, topic.playerText, topicId, flow)) return false;
            ConversationState conversation = messages.GetConversation(contactId);
            conversation.liveActiveTopicId = topicId;
            conversation.liveFlowIdentity = flow;
            conversation.liveWaitingForChoice = false;
            QueueChain(contactId, flow + ":intro", "topic-intro:" + topicId, flow);
            NotifyChanged();
            return true;
        }

        public List<LiveReplyOption> GetReplyOptions(string contactId)
        {
            var result = new List<LiveReplyOption>();
            ConversationState conversation = messages.GetConversation(contactId);
            if (conversation == null || !conversation.liveWaitingForChoice || string.IsNullOrEmpty(conversation.liveActiveTopicId))
                return result;
            TopicDefinition topic = FindTopic(conversation.liveActiveTopicId);
            if (topic == null) return result;
            for (int index = 0; index < topic.replies.Length; index++)
                result.Add(new LiveReplyOption(topic.replies[index].id, topic.replies[index].text));
            return result;
        }

        public bool SubmitReply(string contactId, string replyId)
        {
            ConversationState conversation = messages.GetConversation(contactId);
            if (conversation == null || !conversation.liveWaitingForChoice || string.IsNullOrEmpty(conversation.liveActiveTopicId))
                return false;
            TopicDefinition topic = FindTopic(conversation.liveActiveTopicId);
            ReplyDefinition reply = FindReply(topic, replyId);
            if (topic == null || reply == null) return false;
            string flow = conversation.liveFlowIdentity;
            if (!messages.SendOutgoing(flow + ":player:reply:" + reply.id, contactId, reply.text, reply.id, flow)) return false;
            conversation.liveWaitingForChoice = false;
            QueueChain(contactId, flow + ":reply:" + reply.id, "topic-reply:" + topic.id + ":" + reply.id, flow);
            NotifyChanged();
            return true;
        }

        public bool IsTyping(string contactId)
        {
            return typingRemaining > 0f && string.Equals(typingContactId, contactId, StringComparison.Ordinal);
        }

        public string GetPresenceText(string contactId)
        {
            if (IsTyping(contactId))
            {
                if (contactId == "yumiko") return "Юмико печатает...";
                if (contactId == "kaito") return "Кайто печатает...";
                if (contactId == "guild") return "Guild Dispatch обрабатывает запрос...";
            }
            if (contactId == "guild") return "Связь доступна";
            return SupportedContact(contactId) ? "В сети" : "Не в сети";
        }

        public List<LiveReactionOption> GetReactionOptions(string contactId)
        {
            var result = new List<LiveReactionOption>();
            if (!SupportedContact(contactId)) return result;
            for (int index = 0; index < Reactions.Length; index++) result.Add(Reactions[index]);
            return result;
        }

        public bool SetReaction(string contactId, string messageId, string reactionId)
        {
            string next = reactionId ?? string.Empty;
            if (next.Length > 0 && !KnownReaction(next)) return false;
            if (!messages.SetReaction(contactId, messageId, next)) return false;
            Emit(new LiveMessengerSignal(LiveMessengerSignalKind.ReactionChanged, contactId, next));
            NotifyChanged();
            return true;
        }

        public void Tick(float seconds)
        {
            float elapsed = Math.Max(0f, seconds);
            TickPendingReaction(elapsed);
            if (data.livePendingChains == null || data.livePendingChains.Count == 0)
            {
                ClearTyping();
                return;
            }

            LivePendingChainState pending = data.livePendingChains[0];
            string[] bubbles = ResolveBubbles(pending.scriptId);
            if (bubbles == null || pending.nextBubbleIndex >= bubbles.Length)
            {
                FinishChain(pending);
                return;
            }

            if (typingRemaining <= 0f || !string.Equals(typingChainId, pending.chainId, StringComparison.Ordinal))
                BeginTyping(pending);
            typingRemaining = Math.Max(0f, typingRemaining - elapsed);
            if (typingRemaining > 0f) return;

            DeliverPendingBubble(pending, bubbles[pending.nextBubbleIndex]);
            pending.nextBubbleIndex++;
            ClearTyping();
            if (pending.nextBubbleIndex >= bubbles.Length) FinishChain(pending);
            NotifyChanged();
        }

        private void RecoverPendingChains()
        {
            if (data.livePendingChains == null || data.livePendingChains.Count == 0) return;
            RecoveredPendingContent = true;
            while (data.livePendingChains.Count > 0)
            {
                LivePendingChainState pending = data.livePendingChains[0];
                string[] bubbles = ResolveBubbles(pending.scriptId);
                if (bubbles == null)
                {
                    data.livePendingChains.RemoveAt(0);
                    continue;
                }
                while (pending.nextBubbleIndex < bubbles.Length)
                {
                    DeliverPendingBubble(pending, bubbles[pending.nextBubbleIndex]);
                    pending.nextBubbleIndex++;
                }
                FinishChain(pending);
            }
            ClearTyping();
        }

        private void RecoverPendingReactions()
        {
            if (data.livePendingReactions == null || data.livePendingReactions.Count == 0) return;
            RecoveredPendingContent = true;
            while (data.livePendingReactions.Count > 0)
            {
                LivePendingReactionState pending = data.livePendingReactions[0];
                messages.SetNpcReaction(pending.contactId, pending.messageId, pending.reactionId);
                data.livePendingReactions.RemoveAt(0);
            }
            ClearReactionTimer();
        }

        private void QueueChain(string contactId, string chainId, string scriptId, string flowIdentity)
        {
            for (int index = 0; index < data.livePendingChains.Count; index++)
                if (string.Equals(data.livePendingChains[index].chainId, chainId, StringComparison.Ordinal)) return;
            string[] bubbles = ResolveBubbles(scriptId);
            if (bubbles == null || bubbles.Length == 0) return;
            bool complete = true;
            for (int index = 0; index < bubbles.Length; index++)
                if (!messages.HasDeliveredEvent(chainId + ":bubble:" + (index + 1))) { complete = false; break; }
            if (complete) return;
            data.livePendingChains.Add(new LivePendingChainState
            {
                contactId = contactId,
                chainId = chainId,
                scriptId = scriptId,
                flowIdentity = flowIdentity ?? string.Empty,
                nextBubbleIndex = 0
            });
            NotifyChanged();
        }

        private void DeliverPendingBubble(LivePendingChainState pending, string text)
        {
            string eventId = pending.chainId + ":bubble:" + (pending.nextBubbleIndex + 1);
            messages.DeliverIncomingLive(eventId, pending.contactId, text, null, pending.chainId,
                pending.nextBubbleIndex > 0);
        }

        private void FinishChain(LivePendingChainState pending)
        {
            int index = data.livePendingChains.IndexOf(pending);
            if (index >= 0) data.livePendingChains.RemoveAt(index);
            if (pending.scriptId.StartsWith("topic-intro:", StringComparison.Ordinal))
            {
                ConversationState conversation = messages.GetConversation(pending.contactId);
                if (conversation != null && string.Equals(conversation.liveFlowIdentity, pending.flowIdentity, StringComparison.Ordinal))
                    conversation.liveWaitingForChoice = true;
            }
            else if (pending.scriptId.StartsWith("topic-reply:", StringComparison.Ordinal))
            {
                CompleteTopic(pending.contactId, pending.flowIdentity);
            }
            ClearTyping();
            NotifyChanged();
        }

        private void CompleteTopic(string contactId, string flowIdentity)
        {
            ConversationState conversation = messages.GetConversation(contactId);
            if (conversation == null || !string.Equals(conversation.liveFlowIdentity, flowIdentity, StringComparison.Ordinal)) return;
            TopicDefinition topic = FindTopic(conversation.liveActiveTopicId);
            if (topic != null)
            {
                LiveTopicProgress progress = GetOrCreateProgress(conversation, topic.id);
                progress.completedCount++;
                progress.lastContext = CurrentContext();
            }
            conversation.liveActiveTopicId = string.Empty;
            conversation.liveFlowIdentity = string.Empty;
            conversation.liveWaitingForChoice = false;
        }

        private void BeginTyping(LivePendingChainState pending)
        {
            typingContactId = pending.contactId;
            typingChainId = pending.chainId;
            string[] bubbles = ResolveBubbles(pending.scriptId);
            string text = bubbles != null && pending.nextBubbleIndex >= 0 && pending.nextBubbleIndex < bubbles.Length
                ? bubbles[pending.nextBubbleIndex] : string.Empty;
            typingRemaining = TypingDelay(pending.chainId, text, pending.nextBubbleIndex);
            Emit(new LiveMessengerSignal(LiveMessengerSignalKind.TypingStarted, pending.contactId, pending.chainId));
        }

        private void ClearTyping()
        {
            typingContactId = string.Empty;
            typingChainId = string.Empty;
            typingRemaining = 0f;
        }

        private static float TypingDelay(string chainId, string text, int bubbleIndex)
        {
            int length = (text ?? string.Empty).Trim().Length;
            float minimum;
            float maximum;
            if (length <= 28)
            {
                minimum = 3.0f;
                maximum = 3.6f;
            }
            else if (length <= 64)
            {
                minimum = 3.5f;
                maximum = 4.3f;
            }
            else
            {
                minimum = 4.2f;
                maximum = 4.9f;
            }
            uint hash = StableHash((chainId ?? string.Empty) + "|" + bubbleIndex + "|" + (text ?? string.Empty));
            float unit = (hash % 1000u) / 999f;
            float delay = minimum + (maximum - minimum) * unit;
            if (bubbleIndex > 0) delay = Math.Min(5.0f, delay + .12f);
            return delay;
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string source = value ?? string.Empty;
                for (int index = 0; index < source.Length; index++)
                {
                    hash ^= source[index];
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private bool TopicAvailable(TopicDefinition topic, ConversationState conversation)
        {
            if (!ContextAvailable(topic.contextRule)) return false;
            LiveTopicProgress progress = FindProgress(conversation, topic.id);
            if (topic.mode == LiveTopicMode.Once) return progress == null || progress.completedCount == 0;
            if (topic.mode == LiveTopicMode.Cooldown && progress != null && progress.completedCount > 0)
                return !string.Equals(progress.lastContext, CurrentContext(), StringComparison.Ordinal);
            return true;
        }

        private bool ContextAvailable(ContextRule rule)
        {
            if (rule == ContextRule.None) return true;
            if (rule == ContextRule.HasContractContext)
                return state.completedRuns > 0 || state.phase != RunPhase.Home;
            if (rule == ContextRule.HasReturnContext)
                return state.completedRuns > 0 || state.phase == RunPhase.Payment;
            return false;
        }

        private string CurrentContext()
        {
            return state.contractRunSequence + ":" + state.completedRuns;
        }

        private int NextLiveIdentity()
        {
            if (data.nextLiveSequence < int.MaxValue) data.nextLiveSequence++;
            return data.nextLiveSequence;
        }

        private void HandleMessagesChanged()
        {
            int highest = HighestSequence();
            if (highest <= observedSequence) return;
            var fresh = new List<ObservedEntry>();
            List<ConversationState> conversations = messages.GetOrderedConversations();
            for (int conversationIndex = 0; conversationIndex < conversations.Count; conversationIndex++)
            {
                ConversationState conversation = conversations[conversationIndex];
                if (conversation == null || conversation.entries == null) continue;
                for (int entryIndex = conversation.entries.Count - 1; entryIndex >= 0; entryIndex--)
                {
                    MessageEntry entry = conversation.entries[entryIndex];
                    if (entry == null) continue;
                    if (entry.sequence <= observedSequence) break;
                    fresh.Add(new ObservedEntry { contactId = conversation.contactId, entry = entry });
                }
            }
            fresh.Sort(delegate(ObservedEntry left, ObservedEntry right)
            {
                return left.entry.sequence.CompareTo(right.entry.sequence);
            });
            for (int index = 0; index < fresh.Count; index++) ProcessNewEntry(fresh[index].contactId, fresh[index].entry);
            observedSequence = Math.Max(observedSequence, highest);
        }

        private void ProcessNewEntry(string contactId, MessageEntry entry)
        {
            if (entry.outgoing)
            {
                Emit(new LiveMessengerSignal(LiveMessengerSignalKind.PlayerSent, contactId, entry));
                QueueNpcReaction(contactId, entry);
                return;
            }
            Emit(new LiveMessengerSignal(LiveMessengerSignalKind.IncomingDelivered, contactId, entry));
            if (!string.IsNullOrEmpty(entry.eventId) && !entry.eventId.StartsWith("live:", StringComparison.Ordinal))
                QueueProactive(contactId, entry);
        }

        private void QueueProactive(string contactId, MessageEntry entry)
        {
            string eventId = entry.eventId ?? string.Empty;
            if (contactId == "guild")
            {
                if (eventId.StartsWith("guild-contract-offer:", StringComparison.Ordinal))
                    QueueChain("guild", "live:guild:offer:" + eventId, "proactive:guild.offer", string.Empty);
                else if (eventId.StartsWith("guild-contract-accepted:", StringComparison.Ordinal))
                {
                    QueueChain("guild", "live:guild:accepted:" + eventId, "proactive:guild.accepted", string.Empty);
                    if (messages.IsDialogueCompleted("kaito", "Kaito_Start"))
                        QueueChain("kaito", "live:kaito:accepted:" + eventId, "proactive:kaito.accepted", string.Empty);
                }
                else if (eventId.StartsWith("guild-contract-completed:", StringComparison.Ordinal))
                {
                    string contractId = StoryContractId(eventId, "guild-contract-completed:", 1);
                    QueueFirstStoryChain("guild", "live:guild:completed:story:" + contractId,
                        "live:guild:completed:guild-contract-completed:" + contractId + ":", "proactive:guild.completed");
                    if (messages.IsDialogueCompleted("kaito", "Kaito_Start"))
                        QueueFirstStoryChain("kaito", "live:kaito:completed:story:" + contractId,
                            "live:kaito:completed:guild-contract-completed:" + contractId + ":", "proactive:kaito.completed");
                }
            }
            else if (contactId == "yumiko")
            {
                if (eventId.StartsWith("yumiko-food-first:", StringComparison.Ordinal))
                    QueueChain("yumiko", "live:yumiko:purchase:" + eventId, "proactive:yumiko.purchase", string.Empty);
                else if (eventId.StartsWith("yumiko-return:", StringComparison.Ordinal))
                {
                    string contractId = StoryContractId(eventId, "yumiko-return:", 2);
                    QueueFirstStoryChain("yumiko", "live:yumiko:return:story:" + contractId,
                        "live:yumiko:return:yumiko-return:" + contractId + ":", "proactive:yumiko.return");
                    if (messages.IsDialogueCompleted("kaito", "Kaito_Start"))
                        QueueFirstStoryChain("kaito", "live:kaito:return:story:" + contractId,
                            "live:kaito:return:yumiko-return:" + contractId + ":", "proactive:kaito.return");
                }
            }
            else if (contactId == "kaito" && entry.attachment != null && entry.attachment.kind == MessageAttachmentKind.Coordinates)
            {
                QueueChain("kaito", "live:kaito:coordinates:" + eventId, "proactive:kaito.coordinates", string.Empty);
            }
        }

        private void QueueFirstStoryChain(string contactId, string chainId, string legacyEventPrefix, string scriptId)
        {
            if (ConversationHasEventPrefix(contactId, chainId + ":bubble:") ||
                ConversationHasEventPrefix(contactId, legacyEventPrefix)) return;
            QueueChain(contactId, chainId, scriptId, string.Empty);
        }

        private bool ConversationHasEventPrefix(string contactId, string prefix)
        {
            ConversationState conversation = messages.GetConversation(contactId);
            if (conversation == null || conversation.entries == null || string.IsNullOrEmpty(prefix)) return false;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && (entry.eventId ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string StoryContractId(string eventId, string prefix, int suffixSegments)
        {
            if (string.IsNullOrEmpty(eventId) || !eventId.StartsWith(prefix, StringComparison.Ordinal)) return string.Empty;
            string value = eventId.Substring(prefix.Length);
            for (int index = 0; index < suffixSegments; index++)
            {
                int separator = value.LastIndexOf(':');
                if (separator <= 0) return string.Empty;
                value = value.Substring(0, separator);
            }
            return value;
        }

        private void QueueNpcReaction(string contactId, MessageEntry entry)
        {
            if (entry == null || !entry.outgoing || !string.IsNullOrEmpty(entry.npcReactionId)) return;
            string reactionId = AuthoredNpcReaction(contactId, entry.choiceId);
            if (string.IsNullOrEmpty(reactionId)) return;
            string eventId = "npc-reaction:" + entry.messageId + ":" + reactionId;
            for (int index = 0; index < data.livePendingReactions.Count; index++)
            {
                LivePendingReactionState existing = data.livePendingReactions[index];
                if (existing != null && string.Equals(existing.eventId, eventId, StringComparison.Ordinal)) return;
            }
            data.livePendingReactions.Add(new LivePendingReactionState
            {
                contactId = contactId,
                messageId = entry.messageId,
                reactionId = reactionId,
                eventId = eventId
            });
            NotifyChanged();
        }

        private void TickPendingReaction(float seconds)
        {
            if (data.livePendingReactions == null || data.livePendingReactions.Count == 0)
            {
                ClearReactionTimer();
                return;
            }
            LivePendingReactionState pending = data.livePendingReactions[0];
            if (!string.Equals(reactionTimerEventId, pending.eventId, StringComparison.Ordinal))
            {
                reactionTimerEventId = pending.eventId;
                reactionRemaining = ReactionDelay(pending.eventId);
            }
            reactionRemaining = Math.Max(0f, reactionRemaining - seconds);
            if (reactionRemaining > 0f) return;
            bool changed = messages.SetNpcReaction(pending.contactId, pending.messageId, pending.reactionId);
            data.livePendingReactions.RemoveAt(0);
            ClearReactionTimer();
            if (changed)
            {
                Emit(new LiveMessengerSignal(LiveMessengerSignalKind.ReactionChanged, pending.contactId, pending.messageId));
                NotifyChanged();
            }
        }

        private void ClearReactionTimer()
        {
            reactionTimerEventId = string.Empty;
            reactionRemaining = 0f;
        }

        private static float ReactionDelay(string eventId)
        {
            return .85f + (StableHash(eventId ?? string.Empty) % 300u) / 1000f;
        }

        private static string AuthoredNpcReaction(string contactId, string choiceId)
        {
            if (contactId == "yumiko")
            {
                switch (choiceId ?? string.Empty)
                {
                    case "yumiko.food.tea": return "reaction_tasty";
                    case "yumiko.how.real": return "reaction_heart";
                    case "yumiko.before-hunt.safe": return "reaction_thumbsup";
                    case "yumiko.returned.ok": return "reaction_love";
                    case "yumiko.news.laugh": return "reaction_wink";
                    case "yumiko.unusual.want": return "reaction_surprised";
                }
            }
            else if (contactId == "kaito")
            {
                switch (choiceId ?? string.Empty)
                {
                    case "kaito.coordinates.ack": return "reaction_thumbsup";
                    case "kaito.tactics.normal": return "reaction_angry";
                    case "kaito.place.simple": return "reaction_thumbsup";
                }
            }
            return string.Empty;
        }

        private static bool KnownReaction(string reactionId)
        {
            for (int index = 0; index < Reactions.Length; index++)
                if (string.Equals(Reactions[index].Id, reactionId, StringComparison.Ordinal)) return true;
            return false;
        }

        private int HighestSequence()
        {
            int highest = 0;
            List<ConversationState> conversations = messages.GetOrderedConversations();
            for (int index = 0; index < conversations.Count; index++)
                if (conversations[index] != null) highest = Math.Max(highest, conversations[index].lastSequence);
            return highest;
        }

        private string[] ResolveBubbles(string scriptId)
        {
            if (scriptId.StartsWith("topic-intro:", StringComparison.Ordinal))
            {
                TopicDefinition topic = FindTopic(scriptId.Substring("topic-intro:".Length));
                return topic != null ? topic.intro : null;
            }
            if (scriptId.StartsWith("topic-reply:", StringComparison.Ordinal))
            {
                string rest = scriptId.Substring("topic-reply:".Length);
                for (int topicIndex = 0; topicIndex < Topics.Length; topicIndex++)
                {
                    TopicDefinition topic = Topics[topicIndex];
                    string prefix = topic.id + ":";
                    if (!rest.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    ReplyDefinition reply = FindReply(topic, rest.Substring(prefix.Length));
                    return reply != null ? reply.bubbles : null;
                }
                return null;
            }
            switch (scriptId)
            {
                case "proactive:guild.offer": return new[] { "Карточка содержит подтверждённые данные по цели. Дополнительные сведения доступны по запросу." };
                case "proactive:guild.accepted": return new[] { "Статус обновлён: контракт активен. Самовольное изменение маршрута не рекомендуется." };
                case "proactive:guild.completed": return new[] { "Отчёт принят. Запись о выполнении добавлена в архив Гильдии." };
                case "proactive:yumiko.purchase": return new[] { "И не смотри так. Я просто не хочу потом слушать, как ты снова пошёл голодным." };
                case "proactive:yumiko.return": return new[] { "Сначала выдохни. Потом расскажешь, что там было." };
                case "proactive:kaito.accepted": return new[] { "Вижу, ты взял контракт. Не спеши с первым выводом — Кисараги любит повторять знакомые детали неправильно." };
                case "proactive:kaito.completed": return new[] { "Значит, маршрут всё-таки замкнулся. Я сверю это с тем, что видел раньше." };
                case "proactive:kaito.return": return new[] { "Запомни первое, что показалось тебе неуместным. Обычно именно это и важно." };
                case "proactive:kaito.coordinates": return new[] { "Если маркер сместится хотя бы на один сектор — не следуй за ним автоматически." };
                default: return null;
            }
        }

        private static TopicDefinition FindTopic(string topicId)
        {
            if (string.IsNullOrEmpty(topicId)) return null;
            for (int index = 0; index < Topics.Length; index++)
                if (string.Equals(Topics[index].id, topicId, StringComparison.Ordinal)) return Topics[index];
            return null;
        }

        private static ReplyDefinition FindReply(TopicDefinition topic, string replyId)
        {
            if (topic == null || string.IsNullOrEmpty(replyId)) return null;
            for (int index = 0; index < topic.replies.Length; index++)
                if (string.Equals(topic.replies[index].id, replyId, StringComparison.Ordinal)) return topic.replies[index];
            return null;
        }

        private static LiveTopicProgress FindProgress(ConversationState conversation, string topicId)
        {
            if (conversation == null || conversation.liveTopicProgress == null) return null;
            for (int index = 0; index < conversation.liveTopicProgress.Count; index++)
                if (conversation.liveTopicProgress[index] != null &&
                    string.Equals(conversation.liveTopicProgress[index].topicId, topicId, StringComparison.Ordinal))
                    return conversation.liveTopicProgress[index];
            return null;
        }

        private static LiveTopicProgress GetOrCreateProgress(ConversationState conversation, string topicId)
        {
            LiveTopicProgress progress = FindProgress(conversation, topicId);
            if (progress != null) return progress;
            progress = new LiveTopicProgress { topicId = topicId };
            conversation.liveTopicProgress.Add(progress);
            return progress;
        }

        private static bool SupportedContact(string contactId)
        {
            return contactId == "yumiko" || contactId == "guild" || contactId == "kaito";
        }

        private void Emit(LiveMessengerSignal value)
        {
            Action<LiveMessengerSignal> handler = Signal;
            if (handler != null) handler(value);
        }

        private void NotifyChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }

        private static ReplyDefinition R(string id, string text, params string[] bubbles)
        {
            return new ReplyDefinition(id, text, bubbles);
        }

        private static TopicDefinition T(string id, string contact, string text, LiveTopicMode mode,
            ContextRule rule, string[] intro, params ReplyDefinition[] replies)
        {
            return new TopicDefinition(id, contact, text, mode, rule, intro, replies);
        }

        private static TopicDefinition[] BuildTopics()
        {
            return new[]
            {
                T("yumiko.food", "yumiko", "Что посоветуешь поесть?", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Смотря куда идёшь.", "Если снова Кисараги — не экспериментируй." },
                    R("yumiko.food.tea", "Хорошо, возьму чай.", "Вот и договорились. Только не пей его залпом перед выходом."),
                    R("yumiko.food.stronger", "А есть что-нибудь посильнее?", "Есть. Но перед Кисараги важнее не перегрузить голову, чем набить инвентарь."),
                    R("yumiko.food.none", "Я и без еды справлюсь.", "Конечно справишься. Вопрос только — зачем специально делать ночь хуже?")),
                T("yumiko.before-hunt", "yumiko", "Что взять перед охотой?", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Воду. Что-нибудь простое поесть. И голову на плечах.", "Последнее особенно не забудь." },
                    R("yumiko.before-hunt.safe", "Ладно, без экспериментов.", "Вот. Уже звучишь как человек, которого можно отпустить ночью одного."),
                    R("yumiko.before-hunt.risk", "А если хочется рискнуть?", "Рискуй с целью, не с подготовкой. Это разные вещи.")),
                T("yumiko.how", "yumiko", "Как ты?", LiveTopicMode.Cooldown, ContextRule.None,
                    new[] { "Нормально. Кухня пережила ещё одну ночь.", "А ты спрашиваешь потому что правда интересно или потому что тебе что-то нужно?" },
                    R("yumiko.how.real", "Правда интересно.", "Тогда спасибо. Я устала, но это хорошая усталость."),
                    R("yumiko.how.escape", "Можно я не буду отвечать?", "Можно. Но я всё равно запомню, как ловко ты ушёл от вопроса.")),
                T("yumiko.news", "yumiko", "Что нового?", LiveTopicMode.Cooldown, ContextRule.None,
                    new[] { "У Гильдии снова половина людей делает вид, что не ест ночью.", "А потом в два часа все внезапно хотят онигири." },
                    R("yumiko.news.laugh", "Звучит знакомо.", "Вот именно. Поэтому я тебе и не верю, когда ты говоришь «мне ничего не надо»."),
                    R("yumiko.news.work", "Ты вообще отдыхаешь?", "Иногда. Обычно когда кухня закрыта и никто не пишет мне про чай.")),
                T("yumiko.last-contract", "yumiko", "О последнем контракте", LiveTopicMode.Contextual, ContextRule.HasContractContext,
                    new[] { "Я видела название.", "Мне уже не нравится место, если тебе интересно." },
                    R("yumiko.last-contract.careful", "Буду осторожен.", "Вот эту фразу я сохраню. Потом сравним с реальностью."),
                    R("yumiko.last-contract.details", "Почему не нравится?", "Слишком много людей возвращаются оттуда тихими. Не ранеными — именно тихими.")),
                T("yumiko.returned", "yumiko", "О том, что я вернулся", LiveTopicMode.Contextual, ContextRule.HasReturnContext,
                    new[] { "Я заметила.", "И да, прежде чем спросишь — я рада." },
                    R("yumiko.returned.ok", "Я тоже рад быть дома.", "Тогда сначала поешь. Подвиги обсудим потом."),
                    R("yumiko.returned.tease", "Переживала?", "Нет, конечно. Просто пять раз проверила сообщения совершенно случайно.")),
                T("yumiko.about", "yumiko", "Кто ты вообще такая?", LiveTopicMode.Once, ContextRule.None,
                    new[] { "Юмико. Я кормлю охотников и иногда мешаю им делать совсем глупые вещи.", "Вторая часть почему-то занимает больше времени." },
                    R("yumiko.about.job", "Это официальная должность?", "Нет. Если бы была официальная, я бы требовала надбавку за тебя."),
                    R("yumiko.about.thanks", "Звучит полезно.", "Запомни это чувство. Оно редкое.")),
                T("yumiko.unusual", "yumiko", "Есть что-нибудь необычное на кухне?", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Есть. Но «необычное» и «полезное перед охотой» редко совпадают." },
                    R("yumiko.unusual.want", "Теперь ещё интереснее.", "После задания. Тогда дам попробовать и не буду чувствовать себя соучастницей."),
                    R("yumiko.unusual.safe", "Тогда оставим на потом.", "Разумное решение. Мне даже немного непривычно.")),

                T("guild.contract", "guild", "Текущий контракт", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Запрос принят.", "Текущий контракт отображается в системе YOMI. Приоритет — подтверждённые условия карточки." },
                    R("guild.contract.ack", "Принято.", "Подтверждение зарегистрировано."),
                    R("guild.contract.more", "Есть дополнительные условия?", "Дополнительных обязательных условий на текущий момент не опубликовано.")),
                T("guild.target", "guild", "Данные о цели", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Дополнительные сведения ограничены.", "Последняя подтверждённая активность связана с платформой Кисараги." },
                    R("guild.target.enough", "Этого достаточно.", "Запрос закрыт."),
                    R("guild.target.limit", "Кто ограничил доступ?", "Уровень доступа установлен внутренним протоколом Гильдии. Основание не раскрывается.")),
                T("guild.reward", "guild", "Награда", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Размер награды определяется карточкой контракта и подтверждается после закрытия задания." },
                    R("guild.reward.ack", "Понял.", "Принято."),
                    R("guild.reward.when", "Когда перечисление?", "После подтверждённого завершения и регистрации отчёта.")),
                T("guild.status", "guild", "Статус задания", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Статус синхронизирован с вашим текущим этапом задания." },
                    R("guild.status.ack", "Принято.", "Запрос завершён."),
                    R("guild.status.risk", "Есть предупреждения?", "Соблюдайте маршрут и не считайте отсутствие новой информации подтверждением безопасности.")),
                T("guild.intel", "guild", "Запросить информацию", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Запрос принят.", "Доступны только сведения, прошедшие внутреннюю проверку." },
                    R("guild.intel.activity", "Последняя активность?", "Последняя подтверждённая активность зафиксирована в районе закрытой платформы."),
                    R("guild.intel.victims", "Есть данные по жертвам?", "Данные неполные. Публикация неподтверждённых чисел запрещена протоколом."),
                    R("guild.intel.close", "Закрыть запрос.", "Запрос закрыт.")),

                T("kaito.place", "kaito", "Что знаешь об этом месте?", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Достаточно, чтобы туда не торопиться.", "У Кисараги есть привычка выглядеть знакомо ровно до момента, когда ты начинаешь доверять маршруту." },
                    R("kaito.place.signs", "Что искать?", "Повторяющиеся детали. Часы, объявления, одинаковые следы. Если что-то повторяется слишком точно — считай это сигналом."),
                    R("kaito.place.simple", "Можно проще?", "Не верь месту только потому, что оно похоже на станцию.")),
                T("kaito.target", "kaito", "Что известно о цели?", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Меньше, чем хотелось бы.", "И это само по себе информация." },
                    R("kaito.target.trace", "Что подтверждено?", "След не похож на обычного ёкая. Он появляется там, где маршрут уже должен быть пустым."),
                    R("kaito.target.guild", "Гильдия знает больше?", "Возможно. Но если бы данные были надёжными, они бы уже лежали в карточке.")),
                T("kaito.tactics", "kaito", "Что бы ты сделал?", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "Сначала перестал бы торопиться.", "У таких мест есть ритм. Если его не замечаешь — уже опоздал." },
                    R("kaito.tactics.normal", "Говори нормально.", "Смотри, что меняется после каждого твоего шага. Не атакуй первое, что хочет выглядеть угрозой."),
                    R("kaito.tactics.search", "Что именно искать?", "Несовпадения. Звук без источника. Свет без лампы. Выход, который появился слишком вовремя.")),
                T("kaito.rumors", "kaito", "Есть новые слухи?", LiveTopicMode.Cooldown, ContextRule.None,
                    new[] { "Один.", "Кто-то слышал поезд на линии, которая уже несколько лет не используется." },
                    R("kaito.rumors.where", "Где?", "Недалеко от восточного сектора. Пока это только слух — не строй на нём маршрут."),
                    R("kaito.rumors.fake", "Похоже на выдумку.", "Возможно. Я предпочитаю помечать сомнительное, а не выбрасывать его.")),
                T("kaito.coordinates", "kaito", "О координатах", LiveTopicMode.Repeatable, ContextRule.None,
                    new[] { "B-7 — точка наблюдения, не обещание безопасного входа.", "Если координата начнёт вести себя как цель — остановись." },
                    R("kaito.coordinates.ack", "Запомнил.", "Хорошо. Координаты полезны только пока ты понимаешь, что они означают."),
                    R("kaito.coordinates.shift", "А если маркер сместится?", "Не преследуй его сразу. Сначала проверь, изменилось ли место вокруг тебя.")),
                T("kaito.source", "kaito", "Как ты вообще это узнаёшь?", LiveTopicMode.Once, ContextRule.None,
                    new[] { "Смотрю туда, куда остальные обычно смотрят слишком поздно.", "И записываю вещи, которые удобно считать совпадением." },
                    R("kaito.source.people", "У тебя есть люди?", "Иногда. Но хороший источник — не обязательно человек."),
                    R("kaito.source.mystery", "Опять загадками.", "Нет. Просто некоторые ответы становятся опаснее, если произнести их слишком рано."))
            };
        }
    }
}
