using System;
using System.IO;
using System.Text.Json;
using Rokas.Core;

namespace Rokas.Core.Tests
{
    public static class GameSessionMessageEventTests
    {
        public static void RunAll()
        {
            PaymentCompletionDeliversDeterministicGuildMessageOnce();
            SaveLoadDoesNotRedeliverPaymentCompletion();
            UnrelatedTransitionDoesNotDeliverPaymentCompletion();
        }

        private static void PaymentCompletionDeliversDeterministicGuildMessageOnce()
        {
            GameSession session = PaymentFixture();
            string contractId = session.Contract.id;

            Equal(true, session.ClaimPayment(), "real payment transition should succeed");

            ConversationState guild = session.Messages.GetConversation("guild");
            True(guild != null, "successful payment must create the authored Guild conversation event");
            Equal(1, guild.entries.Count, "payment completion must deliver exactly one Guild message");
            MessageEntry entry = guild.entries[0];
            Equal("guild-contract-completed:" + contractId + ":1", entry.eventId,
                "completion event identity must be deterministic from contract and completed run");
            Equal("Контракт закрыт. Награда перечислена.", entry.text,
                "completion message copy must be deterministic authored content");
            Equal(false, entry.outgoing, "completion event must be incoming");
            Equal(1, guild.unreadCount, "new completion event must increment Guild unread");
            Equal(1, session.Messages.TotalUnread, "new completion event must increment total unread");

            Equal(false, session.ClaimPayment(), "completed payment cannot be processed twice");
            Equal(1, guild.entries.Count, "repeated payment processing must not duplicate the event");
            Equal(1, guild.unreadCount, "repeated processing must not increment unread again");
        }

        private static void SaveLoadDoesNotRedeliverPaymentCompletion()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-events-" + Guid.NewGuid().ToString("N"));
            try
            {
                GameSession original = PaymentFixture();
                string expectedEventId = "guild-contract-completed:" + original.Contract.id + ":1";
                Equal(true, original.ClaimPayment(), "payment should seed completion event before save");

                SaveStore store = new SaveStore(directory, new Codec());
                Equal(SaveWriteStatus.Saved, store.Save(original.State).Status, "event state should save");
                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status, "event state should load");

                GameSession restored = new GameSession(loaded.Data, new ContractDefinition());
                ConversationState guild = restored.Messages.GetConversation("guild");
                True(guild != null, "Guild history must survive reload");
                Equal(1, guild.entries.Count, "reload must preserve one completion event without redelivery");
                Equal(expectedEventId, guild.entries[0].eventId, "reload must preserve deterministic event identity");
                Equal(1, guild.unreadCount, "unread completion state must survive reload");
                Equal(1, restored.Messages.TotalUnread, "total unread must survive reload");

                Equal(false, restored.ClaimPayment(), "restored completed run cannot process payment again");
                Equal(1, guild.entries.Count, "post-load repeated processing must not duplicate the event");
                Equal(1, guild.unreadCount, "post-load repeated processing must not increment unread");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void UnrelatedTransitionDoesNotDeliverPaymentCompletion()
        {
            GameSession session = new GameSession(new SaveData(), new ContractDefinition());

            Equal(true, session.UpgradeWeapon(), "weapon upgrade should provide a real unrelated GameSession transition");
            Equal(null, session.Messages.GetConversation("guild"),
                "unrelated gameplay transition must not synthesize the payment completion event");
            Equal(0, session.Messages.TotalUnread, "unrelated transition must not create unread Messages state");
        }

        private static GameSession PaymentFixture()
        {
            ContractDefinition contract = new ContractDefinition();
            SaveData state = new SaveData
            {
                phase = RunPhase.Payment,
                activeContractId = contract.id
            };
            return new GameSession(state, contract);
        }

        private static void True(bool actual, string message)
        {
            Equal(true, actual, message);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(message + ". Expected " + expected + ", got " + actual + ".");
        }

        private sealed class Codec : ISaveCodec
        {
            private readonly JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };

            public string Serialize(SaveData data)
            {
                return JsonSerializer.Serialize(data, options);
            }

            public bool TryDeserialize(string text, out SaveData data, out string error)
            {
                try
                {
                    data = JsonSerializer.Deserialize<SaveData>(text, options);
                    error = data == null ? "Save contained null." : string.Empty;
                    return data != null;
                }
                catch (Exception exception)
                {
                    data = null;
                    error = exception.Message;
                    return false;
                }
            }
        }
    }
}
