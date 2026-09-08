using System;
using System.IO;
using System.Text.Json;
using Rokas.Core;

namespace Rokas.Core.Tests
{
    public static class HunterGuildFlowTests
    {
        private const string AcceptedText = "Контракт принят. Подготовьтесь к выходу на задание.";

        public static void RunAll()
        {
            AcceptanceFollowUpIsAuthoredAndIdempotent();
            AcceptanceFollowUpSurvivesSaveLoad();
        }

        private static void AcceptanceFollowUpIsAuthoredAndIdempotent()
        {
            GameSession session = OfferFixture();
            ConversationState guild = session.Messages.GetConversation("guild");
            MessageEntry offer = FindContractOffer(guild);
            Equal(1, guild.entries.Count, "Guild should start with one deterministic contract offer");
            Equal(true, session.Messages.OpenConversation("guild"), "opening Guild should mark the offer read");
            Equal(0, guild.unreadCount, "opened Guild offer should be read");

            MessageAttachmentActionResult accepted = session.Messages.ActivateAttachment("guild", offer.messageId);

            Equal(MessageAttachmentActionStatus.Activated, accepted.Status, "real Guild card must accept the existing contract");
            Equal(RunPhase.Accepted, session.State.phase, "acceptance must use the existing ContractService lifecycle");
            Equal(session.Contract.id, session.State.activeContractId, "accepted contract identity must be the real contract");
            Equal(true, offer.attachment.opened, "accepted contract card must be consumed");
            Equal(2, guild.entries.Count, "acceptance must append one authored Guild follow-up");
            MessageEntry followUp = guild.entries[1];
            Equal("guild-contract-accepted:" + session.Contract.id, followUp.eventId,
                "acceptance follow-up identity must be deterministic");
            Equal(AcceptedText, followUp.text, "acceptance follow-up must use authored player-facing copy");
            Equal(false, followUp.outgoing, "Guild acceptance follow-up must be incoming");
            Equal(1, guild.unreadCount, "new acceptance follow-up must increment Guild unread once");

            ConversationState yumiko = session.Messages.GetConversation("yumiko");
            Equal(2, yumiko.entries.Count, "same acceptance must create one Yumiko recommendation plus one gift");
            Equal(2, yumiko.unreadCount, "inactive Yumiko contact must keep its two authored acceptance events unread");
            Equal(3, session.Messages.TotalUnread,
                "total unread must combine one Guild follow-up with two isolated Yumiko events");

            MessageAttachmentActionResult repeated = session.Messages.ActivateAttachment("guild", offer.messageId);
            Equal(MessageAttachmentActionStatus.AlreadyActive, repeated.Status, "repeated card activation remains deterministic");
            Equal(2, guild.entries.Count, "repeated activation must not duplicate the acceptance follow-up");
            Equal(1, guild.unreadCount, "repeated activation must not increment Guild unread");
            Equal(2, yumiko.entries.Count, "repeated activation must not duplicate Yumiko history");
            Equal(2, yumiko.unreadCount, "repeated activation must not increment Yumiko unread");
            Equal(3, session.Messages.TotalUnread, "repeated activation must not increment total unread");
        }

        private static void AcceptanceFollowUpSurvivesSaveLoad()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-guild-flow-" + Guid.NewGuid().ToString("N"));
            try
            {
                GameSession original = OfferFixture();
                ConversationState originalGuild = original.Messages.GetConversation("guild");
                MessageEntry offer = FindContractOffer(originalGuild);
                original.Messages.OpenConversation("guild");
                Equal(MessageAttachmentActionStatus.Activated,
                    original.Messages.ActivateAttachment("guild", offer.messageId).Status,
                    "acceptance should succeed before save");

                SaveStore store = new SaveStore(directory, new Codec());
                Equal(SaveWriteStatus.Saved, store.Save(original.State).Status, "Guild acceptance state should save");
                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status, "Guild acceptance state should load");

                GameSession restored = new GameSession(loaded.Data, new ContractDefinition());
                ConversationState guild = restored.Messages.GetConversation("guild");
                ConversationState yumiko = restored.Messages.GetConversation("yumiko");
                Equal(2, guild.entries.Count, "reload must preserve offer plus one acceptance follow-up");
                MessageEntry restoredOffer = FindContractOffer(guild);
                Equal(true, restoredOffer.attachment.opened, "consumed Guild card must remain consumed after reload");
                Equal("guild-contract-accepted:" + restored.Contract.id, guild.entries[1].eventId,
                    "reload must preserve deterministic acceptance event identity");
                Equal(AcceptedText, guild.entries[1].text, "reload must preserve acceptance copy");
                Equal(1, guild.unreadCount, "acceptance unread state must survive reload");
                Equal(2, yumiko.entries.Count, "reload must preserve Yumiko recommendation plus gift exactly once");
                Equal(2, yumiko.unreadCount, "Yumiko unread state must survive reload independently");

                Equal(false, restored.EnsureGuildContractOffer(), "reload must not redeliver the deterministic offer");
                Equal(MessageAttachmentActionStatus.AlreadyActive,
                    restored.Messages.ActivateAttachment("guild", restoredOffer.messageId).Status,
                    "reload click on consumed card must remain a no-op");
                Equal(2, guild.entries.Count, "reload/repeated processing must not duplicate Guild history");
                Equal(1, guild.unreadCount, "reload/repeated processing must not increment Guild unread");
                Equal(2, yumiko.entries.Count, "reload/repeated processing must not duplicate Yumiko history");
                Equal(2, yumiko.unreadCount, "reload/repeated processing must not increment Yumiko unread");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static GameSession OfferFixture()
        {
            GameSession session = new GameSession(new SaveData(), new ContractDefinition());
            Equal(true, session.EnsureGuildContractOffer(), "available contract should create deterministic Guild offer");
            return session;
        }

        private static MessageEntry FindContractOffer(ConversationState guild)
        {
            if (guild == null || guild.entries == null)
                throw new InvalidOperationException("Guild conversation is missing.");
            for (int index = 0; index < guild.entries.Count; index++)
            {
                MessageEntry entry = guild.entries[index];
                if (entry != null && entry.attachment != null && entry.attachment.kind == MessageAttachmentKind.Contract)
                    return entry;
            }
            throw new InvalidOperationException("Guild contract offer is missing.");
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(message + ". Expected " + expected + ", got " + actual + ".");
        }

        private sealed class Codec : ISaveCodec
        {
            private readonly JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };

            public string Serialize(SaveData data) { return JsonSerializer.Serialize(data, options); }

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
