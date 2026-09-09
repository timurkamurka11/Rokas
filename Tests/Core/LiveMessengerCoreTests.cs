using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace Rokas.Core.Tests
{
    public static class LiveMessengerCoreTests
    {
        private const string HonestRecommendation =
            "На Кисараги? Перед выходом загляни в YOMI Kitchen. Зелёный чай там будет кстати.";

        public static void RunAll()
        {
            var failures = new List<string>();
            Capture("Yumiko recommendation is semantically honest", RecommendationIsSemanticallyHonest, failures);
            Capture("Live service exposes contact topic floors", TopicFloorsAndContactIsolation, failures);
            Capture("Topic flow produces player bubble typing chain and branch", TopicFlowTypingAndBranching, failures);
            Capture("Topic modes are deterministic", TopicModesAreDeterministic, failures);
            Capture("Partial chain recovers without duplicate or fake typing", PartialChainRecoveryIsDeterministic, failures);
            Capture("Reactions persist and remain contact appropriate", ReactionsPersistAndStayContactAppropriate, failures);
            Capture("Proactive chains are stable and reload safe", ProactiveChainsAreStable, failures);

            if (failures.Count > 0)
                throw new InvalidOperationException("Live Messenger contract failures:\n- " + string.Join("\n- ", failures));
        }

        private static void RecommendationIsSemanticallyHonest()
        {
            GameSession session = Session();
            Equal(true, session.AcceptContract(), "first contract acceptance must succeed");
            ConversationState yumiko = RequiredConversation(session, "yumiko");
            MessageEntry firstRecommendation = FindEventPrefix(yumiko, "yumiko-contract-food:");
            NotNull(firstRecommendation, "first recommendation must exist");
            Equal(HonestRecommendation, firstRecommendation.text,
                "recommendation copy must direct the player to YOMI Kitchen instead of sounding like a free item");
            Equal(null, firstRecommendation.attachment, "recommendation must not carry a FoodGift attachment");

            MessageEntry gift = FindEvent(yumiko, "yumiko-gift:kisaragi-green-tea-001");
            NotNull(gift, "the existing first scripted gift must still exist");
            NotNull(gift.attachment, "real gift copy must have a real attachment");
            Equal(MessageAttachmentKind.FoodGift, gift.attachment.kind, "real gift must remain typed FoodGift");

            Equal(true, session.ReturnHome(), "accepted run cancellation must return Home for a second acceptance");
            Equal(true, session.AcceptContract(), "second legitimate acceptance must succeed");
            MessageEntry secondRecommendation = FindEvent(yumiko,
                "yumiko-contract-food:" + session.Contract.id + ":2");
            NotNull(secondRecommendation, "second run recommendation must exist");
            Equal(HonestRecommendation, secondRecommendation.text, "later recommendation must keep honest copy");
            Equal(null, secondRecommendation.attachment, "later recommendation must not fake a free gift");
            Equal(1, CountEvent(yumiko, "yumiko-gift:kisaragi-green-tea-001"),
                "one-time scripted gift must remain exactly once");
        }

        private static void TopicFloorsAndContactIsolation()
        {
            GameSession session = Session();
            object live = Live(session);

            True(Items(Invoke(live, "GetTopics", "yumiko")).Count >= 6,
                "Yumiko must expose at least six authored topics across the initial/contextual set");
            True(Items(Invoke(live, "GetTopics", "guild")).Count >= 5,
                "Guild must expose at least five request topics");
            Equal(0, Items(Invoke(live, "GetTopics", "kaito")).Count,
                "Kaito live topics must stay blocked while the existing mandatory Kaito_Start dialogue is unresolved");

            session.Messages.MarkDialogueCompleted("kaito", "Kaito_Start");
            True(Items(Invoke(live, "GetTopics", "kaito")).Count >= 6,
                "Kaito must expose at least six live topics after the base Yarn dialogue completes");
            Equal(0, Items(Invoke(live, "GetTopics", "mika")).Count, "Mika must not enter Live Messenger 1.0");
            Equal(0, Items(Invoke(live, "GetTopics", "unknown")).Count, "Unknown must not enter Live Messenger 1.0");
        }

        private static void TopicFlowTypingAndBranching()
        {
            GameSession session = Session();
            object live = Live(session);
            object topic = FindById(Items(Invoke(live, "GetTopics", "yumiko")), "yumiko.food");
            NotNull(topic, "Yumiko food topic must be available");

            Equal(true, InvokeBool(live, "StartTopic", "yumiko", "yumiko.food"), "topic must start");
            ConversationState yumiko = RequiredConversation(session, "yumiko");
            True(yumiko.entries.Count >= 1 && yumiko.entries[yumiko.entries.Count - 1].outgoing,
                "starting a topic must append the player's selected text as a real outgoing history bubble");

            session.Tick(.10f);
            Equal(true, InvokeBool(live, "IsTyping", "yumiko"), "new authored reply must enter transient typing state");
            DriveUntilChoices(session, live, "yumiko");
            Equal(false, InvokeBool(live, "IsTyping", "yumiko"), "typing must clear after the bubble chain arrives");

            List<object> replies = Items(Invoke(live, "GetReplyOptions", "yumiko"));
            True(replies.Count >= 2 && replies.Count <= 4, "micro-dialogue must expose two to four player replies");
            string replyId = ReadString(replies[0], "Id");
            int beforeReply = yumiko.entries.Count;
            Equal(true, InvokeBool(live, "SubmitReply", "yumiko", replyId), "available reply must submit");
            True(yumiko.entries.Count > beforeReply && yumiko.entries[yumiko.entries.Count - 1].outgoing,
                "selected reply must become readable outgoing history");
            DriveUntilSettled(session, live, "yumiko");
            Equal(0, Items(Invoke(live, "GetReplyOptions", "yumiko")).Count,
                "resolved micro-dialogue must clear its reply choices");
            NotNull(FindById(Items(Invoke(live, "GetTopics", "yumiko")), "yumiko.food"),
                "repeatable topic must return to the launcher after resolution");
        }

        private static void TopicModesAreDeterministic()
        {
            GameSession session = Session();
            object live = Live(session);

            CompleteTopicWithFirstReply(session, live, "yumiko", "yumiko.about");
            Equal(null, FindById(Items(Invoke(live, "GetTopics", "yumiko")), "yumiko.about"),
                "once topic must disappear after successful completion");

            CompleteTopicWithFirstReply(session, live, "yumiko", "yumiko.food");
            NotNull(FindById(Items(Invoke(live, "GetTopics", "yumiko")), "yumiko.food"),
                "repeatable food topic must remain intentionally repeatable");

            CompleteTopicWithFirstReply(session, live, "yumiko", "yumiko.how");
            Equal(null, FindById(Items(Invoke(live, "GetTopics", "yumiko")), "yumiko.how"),
                "cooldown small talk must not immediately spam-repeat in the same gameplay context");
            session.State.contractRunSequence++;
            NotNull(FindById(Items(Invoke(live, "GetTopics", "yumiko")), "yumiko.how"),
                "cooldown small talk must unlock after deterministic gameplay context advances");

            Equal(null, FindById(Items(Invoke(live, "GetTopics", "yumiko")), "yumiko.last-contract"),
                "last-contract topic must stay hidden before any completed/return context exists");
        }

        private static void PartialChainRecoveryIsDeterministic()
        {
            GameSession session = Session();
            object live = Live(session);
            Equal(true, InvokeBool(live, "StartTopic", "yumiko", "yumiko.food"), "topic must start before recovery test");
            session.Tick(.10f);
            Equal(true, InvokeBool(live, "IsTyping", "yumiko"), "chain must be in transient typing before save clone");

            SaveData saved = Clone(session.State);
            int beforeReload = RequiredConversation(session, "yumiko").entries.Count;
            GameSession restored = new GameSession(saved, session.Contract);
            object restoredLive = Live(restored);
            Equal(false, InvokeBool(restoredLive, "IsTyping", "yumiko"),
                "historical load must not replay a fake typing state");
            ConversationState restoredYumiko = RequiredConversation(restored, "yumiko");
            True(restoredYumiko.entries.Count > beforeReload,
                "pending authored content must resolve consistently during load recovery");
            True(Items(Invoke(restoredLive, "GetReplyOptions", "yumiko")).Count >= 2,
                "recovered intro chain must land on the real reply state");

            int once = restoredYumiko.entries.Count;
            GameSession restoredAgain = new GameSession(Clone(restored.State), restored.Contract);
            Equal(once, RequiredConversation(restoredAgain, "yumiko").entries.Count,
                "loading recovered state again must not duplicate already delivered bubbles");
        }

        private static void ReactionsPersistAndStayContactAppropriate()
        {
            GameSession session = Session();
            object live = Live(session);
            Equal(true, session.Messages.DeliverIncoming("reaction-yumiko-1", "yumiko", "Только без геройства, ладно?"),
                "reaction seed must deliver");
            ConversationState yumiko = RequiredConversation(session, "yumiko");
            MessageEntry entry = yumiko.entries[yumiko.entries.Count - 1];

            List<object> yumikoReactions = Items(Invoke(live, "GetReactionOptions", "yumiko"));
            True(yumikoReactions.Count >= 2, "Yumiko should allow a small warm reaction set");
            string reactionId = ReadString(yumikoReactions[0], "Id");
            Equal(true, InvokeBool(live, "SetReaction", "yumiko", entry.messageId, reactionId),
                "supported reaction must persist onto message history");
            Equal(reactionId, ReadStringField(entry, "reactionId"), "reaction id must be stored on the real MessageEntry");

            session.Messages.MarkDialogueCompleted("kaito", "Kaito_Start");
            True(Items(Invoke(live, "GetReactionOptions", "kaito")).Count >= 1,
                "Kaito should allow a limited reaction set");
            Equal(10, Items(Invoke(live, "GetReactionOptions", "guild")).Count,
                "player reaction picker must expose the 10 stable reactions on Guild incoming messages; Guild formality is enforced by authored NPC reaction rules");

            GameSession restored = new GameSession(Clone(session.State), session.Contract);
            MessageEntry restoredEntry = FindEvent(RequiredConversation(restored, "yumiko"), "reaction-yumiko-1");
            Equal(reactionId, ReadStringField(restoredEntry, "reactionId"), "reaction history must survive save/load");
        }

        private static void ProactiveChainsAreStable()
        {
            GameSession session = Session();
            object live = Live(session);
            Equal(true, session.EnsureGuildContractOffer(), "real Guild offer must deliver");
            ConversationState guild = RequiredConversation(session, "guild");
            int directCount = guild.entries.Count;
            DrivePending(session, live, 8);
            True(guild.entries.Count > directCount, "Guild offer should gain a restrained authored follow-up chain");
            True(HasEventPrefix(guild, "live:guild:offer:"), "Guild follow-up must use a stable live event namespace");

            int after = guild.entries.Count;
            GameSession restored = new GameSession(Clone(session.State), session.Contract);
            object restoredLive = Live(restored);
            DrivePending(restored, restoredLive, 8);
            Equal(after, RequiredConversation(restored, "guild").entries.Count,
                "historical proactive Guild chain must not replay after load");
        }

        private static void CompleteTopicWithFirstReply(GameSession session, object live, string contactId, string topicId)
        {
            Equal(true, InvokeBool(live, "StartTopic", contactId, topicId), "topic " + topicId + " must start");
            DriveUntilChoices(session, live, contactId);
            List<object> replies = Items(Invoke(live, "GetReplyOptions", contactId));
            True(replies.Count > 0, "topic " + topicId + " must produce replies");
            Equal(true, InvokeBool(live, "SubmitReply", contactId, ReadString(replies[0], "Id")),
                "first reply must submit for " + topicId);
            DriveUntilSettled(session, live, contactId);
        }

        private static void DriveUntilChoices(GameSession session, object live, string contactId)
        {
            for (int index = 0; index < 20; index++)
            {
                if (Items(Invoke(live, "GetReplyOptions", contactId)).Count > 0) return;
                session.Tick(index % 2 == 0 ? .15f : 1.5f);
            }
            throw new InvalidOperationException("Timed out waiting for live replies for " + contactId + ".");
        }

        private static void DriveUntilSettled(GameSession session, object live, string contactId)
        {
            for (int index = 0; index < 12; index++)
            {
                session.Tick(index % 2 == 0 ? .15f : 1.5f);
                if (!InvokeBool(live, "IsTyping", contactId) && Items(Invoke(live, "GetReplyOptions", contactId)).Count == 0)
                    return;
            }
            throw new InvalidOperationException("Timed out waiting for live flow to settle for " + contactId + ".");
        }

        private static void DrivePending(GameSession session, object live, int cycles)
        {
            for (int index = 0; index < cycles; index++)
            {
                session.Tick(index % 2 == 0 ? .15f : 1.5f);
            }
        }

        private static object Live(GameSession session)
        {
            var property = typeof(GameSession).GetProperty("LiveMessages");
            if (property == null)
                throw new InvalidOperationException("GameSession.LiveMessages is missing.");
            object value = property.GetValue(session);
            if (value == null) throw new InvalidOperationException("GameSession.LiveMessages is null.");
            return value;
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            var methods = target.GetType().GetMethods();
            for (int index = 0; index < methods.Length; index++)
            {
                if (methods[index].Name == method && methods[index].GetParameters().Length == args.Length)
                    return methods[index].Invoke(target, args);
            }
            throw new InvalidOperationException("Missing Live Messenger method: " + method + ".");
        }

        private static bool InvokeBool(object target, string method, params object[] args)
        {
            object value = Invoke(target, method, args);
            return value is bool && (bool)value;
        }

        private static List<object> Items(object enumerable)
        {
            var result = new List<object>();
            IEnumerable values = enumerable as IEnumerable;
            if (values == null) return result;
            foreach (object value in values) result.Add(value);
            return result;
        }

        private static object FindById(List<object> values, string id)
        {
            for (int index = 0; index < values.Count; index++)
                if (ReadString(values[index], "Id") == id) return values[index];
            return null;
        }

        private static string ReadString(object value, string member)
        {
            if (value == null) return string.Empty;
            var property = value.GetType().GetProperty(member);
            if (property != null) return property.GetValue(value) as string ?? string.Empty;
            var field = value.GetType().GetField(member);
            return field != null ? field.GetValue(value) as string ?? string.Empty : string.Empty;
        }

        private static string ReadStringField(object value, string fieldName)
        {
            if (value == null) return string.Empty;
            var field = value.GetType().GetField(fieldName);
            if (field == null) throw new InvalidOperationException("Missing field " + fieldName + " on " + value.GetType().Name + ".");
            return field.GetValue(value) as string ?? string.Empty;
        }

        private static SaveData Clone(SaveData state)
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            return JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(state, options), options);
        }

        private static GameSession Session()
        {
            return new GameSession(new SaveData(), new ContractDefinition
            {
                id = "contract_subway_001",
                title = "Билет в один конец",
                location = "Закрытая платформа Кисараги",
                enemyHealth = 180f,
                enemyDamage = 8f,
                enemyInterval = 2.6f,
                autoInterval = 1.2f,
                autoDamage = 8f,
                clickDamage = 5f,
                reward = 1800,
                reputationReward = 10,
                ashReward = 3
            });
        }

        private static ConversationState RequiredConversation(GameSession session, string contactId)
        {
            ConversationState conversation = session.Messages.GetConversation(contactId);
            if (conversation == null) throw new InvalidOperationException("Expected conversation for " + contactId + ".");
            return conversation;
        }

        private static MessageEntry FindEvent(ConversationState conversation, string eventId)
        {
            if (conversation == null || conversation.entries == null) return null;
            for (int index = 0; index < conversation.entries.Count; index++)
                if (conversation.entries[index] != null && conversation.entries[index].eventId == eventId) return conversation.entries[index];
            return null;
        }

        private static MessageEntry FindEventPrefix(ConversationState conversation, string prefix)
        {
            if (conversation == null || conversation.entries == null) return null;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && (entry.eventId ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        private static int CountEvent(ConversationState conversation, string eventId)
        {
            int count = 0;
            if (conversation == null || conversation.entries == null) return count;
            for (int index = 0; index < conversation.entries.Count; index++)
                if (conversation.entries[index] != null && conversation.entries[index].eventId == eventId) count++;
            return count;
        }

        private static bool HasEventPrefix(ConversationState conversation, string prefix)
        {
            return FindEventPrefix(conversation, prefix) != null;
        }

        private static void Capture(string name, Action action, List<string> failures)
        {
            try { action(); }
            catch (Exception exception) { failures.Add(name + ": " + Unwrap(exception).Message); }
        }

        private static Exception Unwrap(Exception exception)
        {
            while (exception.InnerException != null) exception = exception.InnerException;
            return exception;
        }

        private static void NotNull(object value, string message)
        {
            if (value == null) throw new InvalidOperationException(message + ".");
        }

        private static void True(bool value, string message)
        {
            Equal(true, value, message);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(message + ". Expected " + expected + ", got " + actual + ".");
        }
    }
}
