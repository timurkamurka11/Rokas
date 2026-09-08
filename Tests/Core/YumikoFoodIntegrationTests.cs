using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Rokas.Core.Tests
{
    public static class YumikoFoodIntegrationTests
    {
        private const string GiftEventId = "yumiko-gift:kisaragi-green-tea-001";
        private const string PurchaseText = "Зелёный чай YOMI? Хороший выбор. Только не пей его залпом перед выходом.";
        private const string RecommendationText = "На Кисараги? Перед выходом загляни в YOMI Kitchen. Зелёный чай там будет кстати.";
        private const string GiftText = "И ещё. Не спорь — это за мой счёт.";
        private const string SuccessWithFoodText = "Вернулся. Значит, всё-таки пригодилось.";
        private const string FailedWithoutFoodText = "Ты опять пошёл туда без нормальной подготовки?";

        public static void RunAll()
        {
            FirstPaidFoodPurchaseReactionIsExactlyOnceAndReloadSafe();
            ContractAcceptanceCreatesRecommendationAndOneGiftPerRun();
            PostBattleReturnCreatesOneReactionPerRealRun();
            FoodGiftClaimGrantsOneStoredItemAndPersists();
            YumikoUnreadAndHistoryStayIsolatedFromOtherContacts();
        }

        private static void FirstPaidFoodPurchaseReactionIsExactlyOnceAndReloadSafe()
        {
            string directory = TempDirectory("yumiko-food-first");
            try
            {
                GameSession session = Session();
                int startYen = session.State.yen;

                Equal(true, session.PrepareFood(FoodService.GreenTeaId), "real paid Green Tea preparation must succeed");
                Equal(startYen - FoodService.GreenTeaCost, session.State.yen, "first Green Tea purchase must pay the existing price");
                ConversationState yumiko = RequiredConversation(session, "yumiko");
                Equal(1, yumiko.entries.Count, "first paid food purchase must create one Yumiko authored reaction");
                Equal("yumiko-food-first:" + FoodService.GreenTeaId, yumiko.entries[0].eventId,
                    "first-purchase reaction must use deterministic food identity");
                Equal(PurchaseText, yumiko.entries[0].text, "first-purchase copy must be authored Yumiko content");
                Equal(1, yumiko.unreadCount, "inactive Yumiko contact must receive one unread");

                session.State.preparedFoodId = string.Empty;
                Equal(true, session.PrepareFood(FoodService.GreenTeaId), "a later legitimate paid purchase remains supported");
                Equal(1, yumiko.entries.Count, "later purchase of the same food must not replay first-purchase reaction");
                Equal(1, yumiko.unreadCount, "duplicate-suppressed reaction must not increment unread");

                SaveStore store = new SaveStore(directory, new Codec());
                Equal(SaveWriteStatus.Saved, store.Save(session.State).Status, "Yumiko first-purchase state must save");
                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status, "Yumiko first-purchase state must load");
                GameSession restored = new GameSession(loaded.Data, session.Contract);
                ConversationState restoredYumiko = RequiredConversation(restored, "yumiko");
                Equal(2, restoredYumiko.entries.Count,
                    "reload must preserve the core first-purchase reaction plus one recovered live continuation");
                Equal(1, CountEventPrefix(restoredYumiko, "yumiko-food-first:"),
                    "reload must keep the core first-purchase reaction exactly once");
                Equal(1, CountEventPrefix(restoredYumiko, "live:yumiko:purchase:"),
                    "reload must recover the pending live purchase continuation exactly once");
                MessageEntry restoredPurchase = FindEvent(restoredYumiko,
                    "yumiko-food-first:" + FoodService.GreenTeaId);
                True(restoredPurchase != null, "reload must preserve the deterministic first-purchase event");
                Equal(PurchaseText, restoredPurchase.text,
                    "reload must preserve the authored first-purchase copy");
                Equal(2, restoredYumiko.unreadCount,
                    "core purchase reaction plus recovered live continuation must remain unread after reload");

                restored.State.preparedFoodId = string.Empty;
                Equal(true, restored.PrepareFood(FoodService.GreenTeaId), "post-load legitimate purchase remains supported");
                Equal(2, restoredYumiko.entries.Count,
                    "post-load purchase must not redeliver the core reaction or recovered live continuation");
                Equal(1, CountEventPrefix(restoredYumiko, "yumiko-food-first:"),
                    "post-load purchase must keep the core first-purchase event exactly once");
                Equal(1, CountEventPrefix(restoredYumiko, "live:yumiko:purchase:"),
                    "post-load purchase must keep the recovered live continuation exactly once");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void ContractAcceptanceCreatesRecommendationAndOneGiftPerRun()
        {
            string directory = TempDirectory("yumiko-contract");
            try
            {
                GameSession session = Session();
                Equal(true, session.EnsureGuildContractOffer(), "Guild offer must seed the existing real Contract attachment");
                ConversationState guild = RequiredConversation(session, "guild");
                MessageEntry offer = FindAttachment(guild, "Contract");
                True(offer != null, "existing Guild contract offer attachment must exist");

                MessageAttachmentActionResult accepted = session.Messages.ActivateAttachment("guild", offer.messageId);
                Equal("Activated", accepted.Status.ToString(), "real Guild Contract card must accept the contract");
                Equal(1, ReadIntField(session.State, "contractRunSequence"), "real acceptance must allocate run identity 1");

                ConversationState yumiko = RequiredConversation(session, "yumiko");
                MessageEntry recommendation = FindEvent(yumiko,
                    "yumiko-contract-food:" + session.Contract.id + ":1");
                True(recommendation != null, "real acceptance must create one Yumiko food recommendation");
                Equal(RecommendationText, recommendation.text, "recommendation must use authored current-slice copy");
                Equal(null, recommendation.attachment, "recommendation must not imply or carry a free FoodGift");

                MessageEntry gift = FindEvent(yumiko, GiftEventId);
                True(gift != null, "first real acceptance must deliver the one scripted Yumiko gift");
                Equal(GiftText, gift.text, "gift message must use authored Yumiko copy");
                True(gift.attachment != null, "gift message must contain a typed attachment");
                Equal("FoodGift", gift.attachment.kind.ToString(), "gift must use reusable FoodGift attachment kind");
                Equal(FoodService.GreenTeaId, gift.attachment.targetId, "gift payload must carry the real Green Tea food ID");
                Equal(false, gift.attachment.opened, "new gift must begin unclaimed");
                Equal(2, yumiko.entries.Count, "first acceptance should create one recommendation and one one-time gift only");

                SaveStore store = new SaveStore(directory, new Codec());
                Equal(SaveWriteStatus.Saved, store.Save(session.State).Status, "accepted Yumiko context must save");
                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status, "accepted Yumiko context must load");
                GameSession restored = new GameSession(loaded.Data, session.Contract);
                ConversationState restoredYumiko = RequiredConversation(restored, "yumiko");
                Equal(2, restoredYumiko.entries.Count, "reload/bootstrap must not duplicate recommendation or gift");
                Equal(1, ReadIntField(restored.State, "contractRunSequence"), "run identity must persist through reload");

                Equal(true, restored.ReturnHome(), "Accepted contract can use existing cancellation path back Home");
                Equal(true, restored.AcceptContract(), "a legitimate new acceptance must remain supported");
                Equal(2, ReadIntField(restored.State, "contractRunSequence"), "second real acceptance must allocate run identity 2");
                True(FindEvent(restoredYumiko,
                    "yumiko-contract-food:" + restored.Contract.id + ":2") != null,
                    "legitimate new acceptance must receive a new deterministic recommendation");
                Equal(3, restoredYumiko.entries.Count,
                    "second acceptance adds only recommendation; one-time gift must not be delivered twice");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void PostBattleReturnCreatesOneReactionPerRealRun()
        {
            string directory = TempDirectory("yumiko-return");
            try
            {
                ContractDefinition contract = FastWinContract();
                GameSession session = new GameSession(new SaveData(), contract);

                Equal(true, session.AcceptContract(), "first real run must accept");
                Equal(1, ReadIntField(session.State, "contractRunSequence"), "first run identity must be 1");
                Equal(true, session.ConsumeFood(FoodService.MisoId), "existing prepared-food state must record food for this encounter");
                Equal(true, session.LeaveHome(), "first run must leave Home");
                Equal(true, session.EnterPortal(), "first run must begin combat");
                session.Tick(.2f);
                Equal(RunPhase.Sealed, session.State.phase, "first real combat must end in Sealed");
                Equal(true, session.ReturnHome(), "Sealed run must use the real return transition");

                ConversationState yumiko = RequiredConversation(session, "yumiko");
                MessageEntry success = FindEvent(yumiko,
                    "yumiko-return:" + contract.id + ":1:sealed");
                True(success != null, "one real successful return must create one Yumiko return reaction");
                Equal(SuccessWithFoodText, success.text, "successful return with prepared food must use the authored variant");
                int afterFirstReturn = yumiko.entries.Count;
                Equal(false, session.ReturnHome(), "same Payment state cannot process the return transition again");
                Equal(afterFirstReturn, yumiko.entries.Count, "reprocessing the same completed return must not duplicate Yumiko history");

                Equal(true, session.ClaimPayment(), "first run payment must complete before the next legitimate run");
                contract.enemyHealth = 999f;
                contract.autoDamage = 0f;
                contract.autoInterval = 10f;
                contract.enemyDamage = 200f;
                contract.enemyInterval = .1f;

                Equal(true, session.AcceptContract(), "second real run must accept");
                Equal(2, ReadIntField(session.State, "contractRunSequence"), "second run identity must be 2");
                Equal(true, session.LeaveHome(), "second run must leave Home");
                Equal(true, session.EnterPortal(), "second run must begin combat");
                session.Tick(.2f);
                Equal(RunPhase.Failed, session.State.phase, "second real combat must end in Failed");
                Equal(true, session.ReturnHome(), "Failed run must use the existing return transition");

                MessageEntry failed = FindEvent(yumiko,
                    "yumiko-return:" + contract.id + ":2:failed");
                True(failed != null, "legitimate second run must create a distinct deterministic return reaction");
                Equal(FailedWithoutFoodText, failed.text, "failed return without food must use the authored variant");

                SaveStore store = new SaveStore(directory, new Codec());
                Equal(SaveWriteStatus.Saved, store.Save(session.State).Status, "post-battle Yumiko state must save");
                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status, "post-battle Yumiko state must load");
                GameSession restored = new GameSession(loaded.Data, contract);
                ConversationState restoredYumiko = RequiredConversation(restored, "yumiko");
                Equal(yumiko.entries.Count + 2, restoredYumiko.entries.Count,
                    "reload must preserve history plus one deterministic live continuation for each real return");
                Equal(1, CountEventPrefix(restoredYumiko,
                    "yumiko-return:" + contract.id + ":1:sealed"),
                    "first core post-battle return reaction must remain exactly once");
                Equal(1, CountEventPrefix(restoredYumiko,
                    "yumiko-return:" + contract.id + ":2:failed"),
                    "second core post-battle return reaction must remain exactly once");
                Equal(2, CountEventPrefix(restoredYumiko, "live:yumiko:return:"),
                    "two real returns must recover exactly two distinct live continuations");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void FoodGiftClaimGrantsOneStoredItemAndPersists()
        {
            string directory = TempDirectory("yumiko-gift");
            try
            {
                GameSession session = Session();
                Equal(true, session.AcceptContract(), "acceptance must deliver the scripted gift context");
                ConversationState yumiko = RequiredConversation(session, "yumiko");
                MessageEntry gift = FindEvent(yumiko, GiftEventId);
                True(gift != null && gift.attachment != null, "scripted FoodGift attachment must exist");
                Equal(0, ReadIntField(session.State, "storedFoodCount"), "gift reserve must begin empty");

                MessageAttachmentActionResult first = session.Messages.ActivateAttachment("yumiko", gift.messageId);
                Equal("Activated", first.Status.ToString(), "first gift claim must succeed through existing attachment action entry point");
                Equal(FoodService.GreenTeaId, ReadStringField(session.State, "storedFoodId"),
                    "claim must store the real gifted food ID");
                Equal(1, ReadIntField(session.State, "storedFoodCount"), "claim must grant exactly one stored food item");
                Equal(true, gift.attachment.opened, "successful gift claim must persist consumed attachment state");

                MessageAttachmentActionResult repeated = session.Messages.ActivateAttachment("yumiko", gift.messageId);
                Equal("AlreadyActive", repeated.Status.ToString(), "repeated gift claim must be deterministic and idempotent");
                Equal(1, ReadIntField(session.State, "storedFoodCount"), "repeated gift claim must not grant a second item");

                SaveStore store = new SaveStore(directory, new Codec());
                Equal(SaveWriteStatus.Saved, store.Save(session.State).Status, "claimed gift must save");
                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status, "claimed gift must load");
                GameSession restored = new GameSession(loaded.Data, session.Contract);
                ConversationState restoredYumiko = RequiredConversation(restored, "yumiko");
                MessageEntry restoredGift = FindEvent(restoredYumiko, GiftEventId);
                Equal(true, restoredGift.attachment.opened, "gift attachment must stay consumed after reload");
                Equal(FoodService.GreenTeaId, ReadStringField(restored.State, "storedFoodId"),
                    "stored gifted food ID must survive reload");
                Equal(1, ReadIntField(restored.State, "storedFoodCount"), "stored gift count must survive reload");
                Equal("AlreadyActive", restored.Messages.ActivateAttachment("yumiko", restoredGift.messageId).Status.ToString(),
                    "Stop/Play equivalent must not allow a second claim");
                Equal(1, ReadIntField(restored.State, "storedFoodCount"), "post-load repeated claim must keep exactly one item");

                int yenBeforeGiftUse = restored.State.yen;
                Equal(true, restored.PrepareFood(FoodService.GreenTeaId), "stored Green Tea gift must feed the existing preparation API");
                Equal(yenBeforeGiftUse, restored.State.yen, "using the stored Yumiko gift must not charge the normal Green Tea price");
                Equal(0, ReadIntField(restored.State, "storedFoodCount"), "preparing stored Green Tea must consume the one gifted item");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void YumikoUnreadAndHistoryStayIsolatedFromOtherContacts()
        {
            GameSession session = Session();
            Equal(true, session.Messages.DeliverIncoming("seed-kaito", "kaito", "Kaito seed"), "Kaito seed must deliver");
            Equal(true, session.Messages.DeliverIncoming("seed-guild", "guild", "Guild seed"), "Guild seed must deliver");
            ConversationState kaito = RequiredConversation(session, "kaito");
            ConversationState guild = RequiredConversation(session, "guild");

            Equal(true, session.PrepareFood(FoodService.GreenTeaId), "paid food action must trigger Yumiko without touching other contacts");
            ConversationState yumiko = RequiredConversation(session, "yumiko");
            Equal(1, yumiko.entries.Count, "Yumiko event must stay in Yumiko history");
            Equal(1, kaito.entries.Count, "Yumiko event must not alter Kaito history");
            Equal(1, guild.entries.Count, "Yumiko event must not alter Guild history");
            Equal(1, yumiko.unreadCount, "inactive Yumiko event must be unread");
            Equal(1, kaito.unreadCount, "Kaito unread must remain independent");
            Equal(1, guild.unreadCount, "Guild unread must remain independent");

            Equal(true, session.Messages.OpenConversation("yumiko"), "opening Yumiko must consume only Yumiko unread");
            Equal(0, yumiko.unreadCount, "Yumiko unread must clear when opened");
            Equal(1, kaito.unreadCount, "opening Yumiko must not clear Kaito unread");
            Equal(1, guild.unreadCount, "opening Yumiko must not clear Guild unread");
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

        private static ContractDefinition FastWinContract()
        {
            return new ContractDefinition
            {
                id = "contract_subway_001",
                title = "Билет в один конец",
                location = "Закрытая платформа Кисараги",
                enemyHealth = 1f,
                enemyDamage = 0f,
                enemyInterval = 10f,
                autoInterval = .1f,
                autoDamage = 10f,
                clickDamage = 5f,
                reward = 10
            };
        }

        private static ConversationState RequiredConversation(GameSession session, string contactId)
        {
            ConversationState conversation = session.Messages.GetConversation(contactId);
            if (conversation == null)
                throw new InvalidOperationException("Expected conversation for " + contactId + ".");
            return conversation;
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

        private static MessageEntry FindAttachment(ConversationState conversation, string kindName)
        {
            if (conversation == null || conversation.entries == null) return null;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && entry.attachment != null && entry.attachment.kind.ToString() == kindName) return entry;
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

        private static int ReadIntField(SaveData state, string name)
        {
            FieldInfo field = typeof(SaveData).GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field == null) throw new InvalidOperationException("Missing required persisted SaveData field: " + name);
            object value = field.GetValue(state);
            return value == null ? 0 : Convert.ToInt32(value);
        }

        private static string ReadStringField(SaveData state, string name)
        {
            FieldInfo field = typeof(SaveData).GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field == null) throw new InvalidOperationException("Missing required persisted SaveData field: " + name);
            return field.GetValue(state) as string ?? string.Empty;
        }

        private static string TempDirectory(string prefix)
        {
            return Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
        }

        private static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
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
