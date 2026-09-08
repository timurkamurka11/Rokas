using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace Rokas.Core.Tests
{
    public static class LiveMessengerPolishCoreTests
    {
        private static readonly string[] ReactionIds =
        {
            "reaction_love",
            "reaction_wink",
            "reaction_angry",
            "reaction_surprised",
            "reaction_cry",
            "reaction_tasty",
            "reaction_heart",
            "reaction_darkheart",
            "reaction_fox",
            "reaction_thumbsup"
        };

        public static void RunAll()
        {
            var failures = new List<string>();
            Capture("Narrative completion follow-ups are first-story only", NarrativeCompletionFollowUpsAreFirstStoryOnly, failures);
            Capture("Typing delay is natural deterministic and text-aware", TypingDelayIsNaturalDeterministicAndTextAware, failures);
            Capture("Player reactions use ten stickers and support add change remove", PlayerReactionsUseTenStickersAndSupportAddChangeRemove, failures);
            Capture("Authored NPC reactions are delayed persistent and reload-safe", AuthoredNpcReactionsAreDelayedPersistentAndReloadSafe, failures);

            if (failures.Count > 0)
                throw new InvalidOperationException("Live Messenger 1.1 polish contract failures:\n- " + string.Join("\n- ", failures));
        }

        private static void NarrativeCompletionFollowUpsAreFirstStoryOnly()
        {
            GameSession session = Session();
            session.Messages.MarkDialogueCompleted("kaito", "Kaito_Start");
            string contractId = session.Contract.id;

            Equal(true, session.Messages.DeliverIncoming(
                "guild-contract-completed:" + contractId + ":1", "guild", "Контракт закрыт. Награда перечислена. Выполнение №1."),
                "first repeatable core completion event must still deliver");
            Drive(session, 14, 1f);
            Equal(1, CountEventPrefix(RequiredConversation(session, "guild"), "live:guild:completed:"),
                "first completion should create one Guild narrative continuation");
            Equal(1, CountEventPrefix(RequiredConversation(session, "kaito"), "live:kaito:completed:"),
                "first completion should create one Kaito narrative continuation");

            Equal(true, session.Messages.DeliverIncoming(
                "guild-contract-completed:" + contractId + ":2", "guild", "Контракт закрыт. Награда перечислена. Выполнение №2."),
                "second repeatable core completion event must remain legal");
            Drive(session, 14, 1f);
            Equal(2, CountEventPrefix(RequiredConversation(session, "guild"), "guild-contract-completed:"),
                "core completion history must remain repeatable");
            Equal(1, CountEventPrefix(RequiredConversation(session, "guild"), "live:guild:completed:"),
                "same story completion must not create a second Guild narrative continuation");
            Equal(1, CountEventPrefix(RequiredConversation(session, "kaito"), "live:kaito:completed:"),
                "same story completion must not create a second Kaito narrative continuation");

            Equal(true, session.Messages.DeliverIncoming(
                "yumiko-return:" + contractId + ":1:sealed", "yumiko", "Вернулся. Хорошо."),
                "first return event must deliver");
            Drive(session, 14, 1f);
            Equal(true, session.Messages.DeliverIncoming(
                "yumiko-return:" + contractId + ":2:sealed", "yumiko", "Вернулся. Хорошо."),
                "second repeatable return event must remain legal");
            Drive(session, 14, 1f);
            Equal(2, CountEventPrefix(RequiredConversation(session, "yumiko"), "yumiko-return:"),
                "core return history must remain repeatable");
            Equal(1, CountEventPrefix(RequiredConversation(session, "yumiko"), "live:yumiko:return:"),
                "Yumiko first-story return reaction must be once-only for the contract story");
            Equal(1, CountEventPrefix(RequiredConversation(session, "kaito"), "live:kaito:return:"),
                "Kaito first-story return reaction must be once-only for the contract story");

            GameSession restored = new GameSession(Clone(session.State), session.Contract);
            restored.Messages.MarkDialogueCompleted("kaito", "Kaito_Start");
            Equal(true, restored.Messages.DeliverIncoming(
                "guild-contract-completed:" + contractId + ":3", "guild", "Контракт закрыт. Награда перечислена. Выполнение №3."),
                "third core completion after reload must remain legal");
            Drive(restored, 14, 1f);
            Equal(1, CountEventPrefix(RequiredConversation(restored, "guild"), "live:guild:completed:"),
                "save/load must not re-arm the first-story Guild completion reaction");
            Equal(1, CountEventPrefix(RequiredConversation(restored, "kaito"), "live:kaito:completed:"),
                "save/load must not re-arm the first-story Kaito completion reaction");
        }

        private static void TypingDelayIsNaturalDeterministicAndTextAware()
        {
            GameSession firstSession = Session();
            Equal(true, firstSession.LiveMessages.StartTopic("yumiko", "yumiko.food"), "Yumiko food topic must start");
            ConversationState firstConversation = RequiredConversation(firstSession, "yumiko");
            int baseline = firstConversation.entries.Count;
            float firstDelay = MeasureNextDelivery(firstSession, firstConversation, baseline, 6f);
            True(firstDelay >= 3.0f && firstDelay <= 3.8f,
                "short first NPC bubble should take roughly 3.0-3.8 seconds, got " + firstDelay);

            float secondDelay = MeasureNextDelivery(firstSession, firstConversation, baseline + 1, 6f);
            True(secondDelay >= 3.4f && secondDelay <= 4.6f,
                "longer second NPC bubble should take roughly 3.4-4.6 seconds, got " + secondDelay);
            True(secondDelay > firstDelay,
                "longer authored bubble should have a longer deterministic typing delay than the short bubble");

            GameSession secondSession = Session();
            Equal(true, secondSession.LiveMessages.StartTopic("yumiko", "yumiko.food"), "same topic must start in comparison session");
            ConversationState secondConversation = RequiredConversation(secondSession, "yumiko");
            float repeated = MeasureNextDelivery(secondSession, secondConversation, secondConversation.entries.Count, 6f);
            True(Math.Abs(repeated - firstDelay) <= .051f,
                "same stable chain/text must reproduce the same test-friendly delay; first=" + firstDelay + ", repeated=" + repeated);
        }

        private static void PlayerReactionsUseTenStickersAndSupportAddChangeRemove()
        {
            GameSession session = Session();
            Equal(true, session.Messages.DeliverIncoming("reaction-pack-yumiko", "yumiko", "Проверь снаряжение перед выходом."),
                "reaction seed must deliver");
            MessageEntry entry = LastEntry(RequiredConversation(session, "yumiko"));

            List<LiveReactionOption> yumiko = session.LiveMessages.GetReactionOptions("yumiko");
            Equal(10, yumiko.Count, "player reaction picker must expose all ten uploaded reaction identities");
            for (int index = 0; index < ReactionIds.Length; index++)
                Equal(ReactionIds[index], yumiko[index].Id, "reaction identity/order must stay stable at index " + index);
            Equal(10, session.LiveMessages.GetReactionOptions("guild").Count,
                "player must be able to react to an incoming formal Guild message even if Guild itself rarely reacts back");

            Equal(true, session.LiveMessages.SetReaction("yumiko", entry.messageId, "reaction_love"),
                "player must be able to add a reaction");
            Equal("reaction_love", entry.reactionId, "added reaction must persist on real message entry");
            Equal(true, session.LiveMessages.SetReaction("yumiko", entry.messageId, "reaction_wink"),
                "player must be able to replace an existing reaction");
            Equal("reaction_wink", entry.reactionId, "replacement must overwrite rather than duplicate");

            GameSession restored = new GameSession(Clone(session.State), session.Contract);
            MessageEntry restoredEntry = FindEvent(RequiredConversation(restored, "yumiko"), "reaction-pack-yumiko");
            Equal("reaction_wink", restoredEntry.reactionId, "selected reaction identity must survive save/load");
            Equal(true, restored.LiveMessages.SetReaction("yumiko", restoredEntry.messageId, string.Empty),
                "empty reaction identity must remove the player's existing reaction");
            Equal(string.Empty, restoredEntry.reactionId, "removed reaction must persist as absent");
            Equal(false, restored.LiveMessages.SetReaction("yumiko", restoredEntry.messageId, string.Empty),
                "removing an already absent reaction must not emit a fake change");
        }

        private static void AuthoredNpcReactionsAreDelayedPersistentAndReloadSafe()
        {
            GameSession session = Session();
            int reactionSignals = 0;
            session.LiveMessages.Signal += signal =>
            {
                if (signal != null && signal.Kind == LiveMessengerSignalKind.ReactionChanged) reactionSignals++;
            };

            Equal(true, session.Messages.SendOutgoing(
                "npc-reaction-yumiko-1", "yumiko", "Хорошо, возьму чай.", "yumiko.food.tea", "npc-reaction-flow"),
                "authored outgoing Yumiko reply must be written to history");
            MessageEntry player = FindEvent(RequiredConversation(session, "yumiko"), "npc-reaction-yumiko-1");
            Equal(string.Empty, ReadStringField(player, "npcReactionId"),
                "NPC reaction must not appear synchronously on the player bubble");
            session.Tick(.35f);
            Equal(string.Empty, ReadStringField(player, "npcReactionId"),
                "NPC reaction should retain a short natural delay");
            session.Tick(1.25f);
            Equal("reaction_tasty", ReadStringField(player, "npcReactionId"),
                "Yumiko food acknowledgement should use the authored tasty reaction");
            Equal(1, reactionSignals, "one actual NPC reaction appearance must emit exactly one reaction change signal");

            GameSession restored = new GameSession(Clone(session.State), session.Contract);
            MessageEntry restoredPlayer = FindEvent(RequiredConversation(restored, "yumiko"), "npc-reaction-yumiko-1");
            Equal("reaction_tasty", ReadStringField(restoredPlayer, "npcReactionId"),
                "NPC reaction identity must survive save/load");
            int replaySignals = 0;
            restored.LiveMessages.Signal += signal =>
            {
                if (signal != null && signal.Kind == LiveMessengerSignalKind.ReactionChanged) replaySignals++;
            };
            restored.Tick(2f);
            Equal(0, replaySignals, "historical reload must not replay the NPC reaction as a fresh event");

            Equal(true, restored.Messages.SendOutgoing(
                "npc-reaction-guild-1", "guild", "Принято.", "guild.contract.ack", "npc-reaction-guild-flow"),
                "formal Guild outgoing seed must deliver");
            MessageEntry guildPlayer = FindEvent(RequiredConversation(restored, "guild"), "npc-reaction-guild-1");
            restored.Tick(2f);
            Equal(string.Empty, ReadStringField(guildPlayer, "npcReactionId"),
                "Guild must remain formal and must not automatically add casual emoji to routine acknowledgement");
        }

        private static float MeasureNextDelivery(GameSession session, ConversationState conversation, int startingCount, float timeout)
        {
            const float step = .05f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                session.Tick(step);
                elapsed += step;
                if (conversation.entries.Count > startingCount) return elapsed;
            }
            throw new InvalidOperationException("Timed out waiting for incoming live bubble after " + timeout + " seconds.");
        }

        private static void Drive(GameSession session, int cycles, float seconds)
        {
            for (int index = 0; index < cycles; index++) session.Tick(seconds);
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

        private static MessageEntry LastEntry(ConversationState conversation)
        {
            if (conversation == null || conversation.entries == null || conversation.entries.Count == 0)
                throw new InvalidOperationException("Expected at least one message entry.");
            return conversation.entries[conversation.entries.Count - 1];
        }

        private static MessageEntry FindEvent(ConversationState conversation, string eventId)
        {
            if (conversation == null || conversation.entries == null) return null;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && string.Equals(entry.eventId, eventId, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        private static int CountEventPrefix(ConversationState conversation, string prefix)
        {
            int count = 0;
            if (conversation == null || conversation.entries == null) return count;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && (entry.eventId ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        private static string ReadStringField(object value, string fieldName)
        {
            if (value == null) return string.Empty;
            FieldInfo field = value.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(value) as string ?? string.Empty : string.Empty;
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
