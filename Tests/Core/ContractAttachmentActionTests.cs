using System;
using System.IO;
using System.Text.Json;
using Rokas.Core;

namespace Rokas.Core.Tests
{
    public static class ContractAttachmentActionTests
    {
        public static void RunAll()
        {
            AcceptsRealContractOnce();
            RejectsInvalidIdentity();
            RejectsBlockedLifecycle();
            SaveLoadPreservesAcceptance();
        }

        private static GameSession Fixture(string target, RunPhase phase = RunPhase.Home)
        {
            var session = new GameSession(new SaveData { phase = phase }, new ContractDefinition());
            session.Messages.DeliverIncoming("guild-contract-test", "guild", "Contract", new MessageAttachment
            {
                kind = MessageAttachmentKind.Contract, id = "guild-contract-card", targetId = target
            });
            return session;
        }

        private static MessageEntry Entry(GameSession session) { return session.Messages.GetConversation("guild").entries[0]; }
        private static MessageAttachmentActionResult Act(GameSession session)
        {
            return session.Messages.ActivateAttachment("guild", Entry(session).messageId);
        }

        // Catches a cosmetic opened flag without the real ContractService acceptance side effects.
        private static void AcceptsRealContractOnce()
        {
            var session = Fixture("contract_subway_001");
            session.State.enemyHp = 90;
            int messages = 0;
            int sessions = 0;
            session.Messages.Changed += () => messages++;
            session.Changed += () =>
            {
                sessions++;
                Equal(true, Entry(session).attachment.opened, "session observers must see the consumed card with acceptance");
            };
            Equal("Activated", Act(session).Status.ToString(), "valid contract must activate");
            Equal(RunPhase.Accepted, session.State.phase, "contract must enter accepted lifecycle");
            Equal("contract_subway_001", session.State.activeContractId, "real contract identity must be accepted");
            Equal(0f, session.State.enemyHp, "acceptance must use existing combat reset");
            Equal(true, Entry(session).attachment.opened, "successful card must persist opened state");
            Equal(2, session.Messages.GetConversation("guild").entries.Count,
                "acceptance must keep one Guild offer plus one Guild follow-up");
            Equal(2, session.Messages.GetConversation("yumiko").entries.Count,
                "acceptance must add one Yumiko recommendation plus one one-time gift");
            Equal(3, messages, "one acceptance must publish the three distinct authored message deliveries");
            Equal(3, sessions, "session observers must receive the same three authored message changes");

            int messagesAfterAcceptance = messages;
            int sessionsAfterAcceptance = sessions;
            Equal("AlreadyActive", Act(session).Status.ToString(), "repeat activation must be idempotent");
            Equal(messagesAfterAcceptance, messages, "repeat click must not emit another message event");
            Equal(sessionsAfterAcceptance, sessions, "repeat click must not emit another session change");
            Equal(2, session.Messages.GetConversation("guild").entries.Count,
                "repeat click must not duplicate Guild history");
            Equal(2, session.Messages.GetConversation("yumiko").entries.Count,
                "repeat click must not duplicate Yumiko history");

            session.ReturnHome();
            Equal("AlreadyActive", Act(session).Status.ToString(), "consumed card must not restart cancelled contract");
            Equal(RunPhase.Home, session.State.phase, "consumed card must remain consumed after returning home");
        }

        // Catches accepting the session's default contract when the attachment points elsewhere.
        private static void RejectsInvalidIdentity()
        {
            foreach (string id in new[] { "", "unknown-contract", "CONTRACT_SUBWAY_001" })
            {
                var session = Fixture(id);
                int changed = 0;
                session.Changed += () => changed++;
                Equal(id.Length == 0 ? "MissingContract" : "UnknownContract", Act(session).Status.ToString(), "invalid identity diagnostic");
                Equal(RunPhase.Home, session.State.phase, "invalid card cannot accept a contract");
                Equal(false, Entry(session).attachment.opened, "invalid card remains unopened");
                Equal(0, changed, "invalid action must emit no changes");
            }
        }

        // Catches bypassing GameSession lifecycle guards for an unopened card.
        private static void RejectsBlockedLifecycle()
        {
            foreach (RunPhase phase in Enum.GetValues(typeof(RunPhase)))
            {
                if (phase == RunPhase.Home) continue;
                var session = Fixture("contract_subway_001", phase);
                int changed = 0;
                session.Changed += () => changed++;
                Equal("ContractUnavailable", Act(session).Status.ToString(), "blocked lifecycle diagnostic: " + phase);
                Equal(phase, session.State.phase, "blocked action preserves lifecycle");
                Equal(false, Entry(session).attachment.opened, "blocked card remains unopened");
                Equal(0, changed, "blocked action emits no event");
            }
        }

        // Catches persistence losing the contract target, consumed state or delivered event identity.
        private static void SaveLoadPreservesAcceptance()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-contract-" + Guid.NewGuid().ToString("N"));
            try
            {
                var original = Fixture("contract_subway_001");
                Act(original);
                var store = new SaveStore(directory, new Codec());
                Equal(SaveWriteStatus.Saved, store.Save(original.State).Status, "save succeeds");
                var loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status, "load succeeds");
                var restored = new GameSession(loaded.Data, new ContractDefinition());
                Equal("guild-contract-card", Entry(restored).attachment.id, "attachment identity survives");
                Equal("contract_subway_001", Entry(restored).attachment.targetId, "target survives");
                Equal(true, Entry(restored).attachment.opened, "opened state survives");
                Equal(RunPhase.Accepted, restored.State.phase, "accepted phase survives");
                int changes = 0;
                restored.Changed += () => changes++;
                Equal("AlreadyActive", Act(restored).Status.ToString(), "reload click is no-op");
                Equal(false, restored.Messages.DeliverIncoming("guild-contract-test", "guild", "duplicate"), "no redelivery");
                Equal(2, restored.Messages.GetConversation("guild").entries.Count, "offer plus accepted follow-up remain exactly once");
                Equal(2, restored.Messages.GetConversation("yumiko").entries.Count,
                    "Yumiko recommendation and one-time gift survive reload exactly once");
                Equal(0, changes, "reload repeat emits no events");
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual)) throw new InvalidOperationException(message + ". Expected " + expected + ", got " + actual + ".");
        }

        private sealed class Codec : ISaveCodec
        {
            private readonly JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };
            public string Serialize(SaveData data) { return JsonSerializer.Serialize(data, options); }
            public bool TryDeserialize(string text, out SaveData data, out string error)
            {
                try { data = JsonSerializer.Deserialize<SaveData>(text, options); error = ""; return data != null; }
                catch (Exception e) { data = null; error = e.Message; return false; }
            }
        }
    }
}
