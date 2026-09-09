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
            RepeatedProcessingSameCompletedStateIsIdempotent();
            LegitimateRepeatRunsHaveDistinctAuthoredCompletionContext();
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
            Equal("Контракт закрыт. Награда перечислена. Выполнение №1.", entry.text,
                "completion message copy must identify the legitimate completed run");
            Equal(false, entry.outgoing, "completion event must be incoming");
            Equal(1, guild.unreadCount, "new completion event must increment Guild unread");
            Equal(1, session.Messages.TotalUnread, "new completion event must increment total unread");
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

                GameSession restored = new GameSession(loaded.Data, original.Contract);
                ConversationState guild = restored.Messages.GetConversation("guild");
                True(guild != null, "Guild history must survive reload");
                Equal(1, guild.entries.Count, "reload must preserve one completion event without redelivery");
                Equal(expectedEventId, guild.entries[0].eventId, "reload must preserve deterministic event identity");
                Equal("Контракт закрыт. Награда перечислена. Выполнение №1.", guild.entries[0].text,
                    "reload must preserve authored completion context");
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

        private static void RepeatedProcessingSameCompletedStateIsIdempotent()
        {
            GameSession session = PaymentFixture();
            Equal(true, session.ClaimPayment(), "first payment transition succeeds");
            ConversationState guild = session.Messages.GetConversation("guild");
            int history = guild.entries.Count;
            int unread = guild.unreadCount;
            int completedRuns = session.State.completedRuns;

            Equal(false, session.ClaimPayment(), "same completed state cannot be claimed again");
            Equal(completedRuns, session.State.completedRuns, "repeated processing cannot mutate completedRuns");
            Equal(history, guild.entries.Count, "repeated processing cannot append history");
            Equal(unread, guild.unreadCount, "repeated processing cannot increment unread");
        }

        private static void LegitimateRepeatRunsHaveDistinctAuthoredCompletionContext()
        {
            ContractDefinition contract = new ContractDefinition
            {
                id = "contract_subway_001",
                enemyHealth = 1f,
                enemyDamage = 0f,
                enemyInterval = 10f,
                autoInterval = .1f,
                autoDamage = 10f
            };
            GameSession session = new GameSession(new SaveData(), contract);

            CompleteRealRun(session);
            CompleteRealRun(session);
            CompleteRealRun(session);

            ConversationState guild = session.Messages.GetConversation("guild");
            True(guild != null, "real repeat runs must create Guild completion history");
            Equal(3, guild.entries.Count, "three genuine runs must create exactly three completion events");
            for (int index = 0; index < 3; index++)
            {
                int run = index + 1;
                MessageEntry entry = guild.entries[index];
                Equal("guild-contract-completed:contract_subway_001:" + run, entry.eventId,
                    "repeat-run event IDs must retain contract and completedRuns identity");
                Equal("Контракт закрыт. Награда перечислена. Выполнение №" + run + ".", entry.text,
                    "legitimate repeat runs must not render as indistinguishable completion spam");
            }
            Equal(3, session.State.completedRuns, "three genuine payments must advance completedRuns exactly three times");
            Equal(3, guild.unreadCount, "each genuine new run remains one legitimate unread event");
        }

        private static void CompleteRealRun(GameSession session)
        {
            Equal(true, session.AcceptContract(), "repeatable contract can be accepted from Home");
            Equal(true, session.LeaveHome(), "accepted contract can leave Home");
            Equal(true, session.EnterPortal(), "portal transition begins combat");
            Equal(true, session.ClickAttack(false), "manual combat action seals the one-health fixture");
            Equal(RunPhase.Sealed, session.State.phase, "real combat must seal the contract");
            Equal(true, session.ReturnHome(), "sealed contract returns to Payment");
            Equal(RunPhase.Payment, session.State.phase, "sealed run must enter Payment");
            Equal(true, session.ClaimPayment(), "real repeat-run payment must succeed once");
            Equal(RunPhase.Home, session.State.phase, "claim returns repeatable lifecycle to Home");
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
