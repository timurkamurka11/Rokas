from pathlib import Path

path = Path("Tests/Core/HunterGuildFlowTests.cs")
text = path.read_text(encoding="utf-8")

old_reload = '''                Equal(2, guild.entries.Count, "reload must preserve offer plus one acceptance follow-up");
                MessageEntry restoredOffer = FindContractOffer(guild);
                Equal(true, restoredOffer.attachment.opened, "consumed Guild card must remain consumed after reload");
                Equal("guild-contract-accepted:" + restored.Contract.id, guild.entries[1].eventId,
                    "reload must preserve deterministic acceptance event identity");
                Equal(AcceptedText, guild.entries[1].text, "reload must preserve acceptance copy");
                Equal(1, guild.unreadCount, "acceptance unread state must survive reload");
                Equal(2, yumiko.entries.Count, "reload must preserve Yumiko recommendation plus gift exactly once");
                Equal(2, yumiko.unreadCount, "Yumiko unread state must survive reload independently");
'''
new_reload = '''                Equal(4, guild.entries.Count,
                    "reload must preserve core offer/acceptance plus one recovered live offer and acceptance continuation");
                Equal(1, CountEventPrefix(guild, "guild-contract-offer:"),
                    "core Guild offer must remain exactly once after live recovery");
                Equal(1, CountEventPrefix(guild, "guild-contract-accepted:"),
                    "core Guild acceptance must remain exactly once after live recovery");
                Equal(1, CountEventPrefix(guild, "live:guild:offer:"),
                    "pending Guild offer continuation must recover exactly once");
                Equal(1, CountEventPrefix(guild, "live:guild:accepted:"),
                    "pending Guild acceptance continuation must recover exactly once");
                MessageEntry restoredOffer = FindContractOffer(guild);
                Equal(true, restoredOffer.attachment.opened, "consumed Guild card must remain consumed after reload");
                MessageEntry restoredAcceptance = FindEventPrefix(guild, "guild-contract-accepted:");
                Equal("guild-contract-accepted:" + restored.Contract.id, restoredAcceptance.eventId,
                    "reload must preserve deterministic acceptance event identity");
                Equal(AcceptedText, restoredAcceptance.text, "reload must preserve acceptance copy");
                Equal(3, guild.unreadCount,
                    "core acceptance plus two distinct recovered live chains must remain unread after reload");
                Equal(2, yumiko.entries.Count, "reload must preserve Yumiko recommendation plus gift exactly once");
                Equal(2, yumiko.unreadCount, "Yumiko unread state must survive reload independently");
'''
if old_reload not in text:
    raise SystemExit("Expected HunterGuildFlowTests reload assertion anchor was not found")
text = text.replace(old_reload, new_reload, 1)

old_repeat = '''                Equal(2, guild.entries.Count, "reload/repeated processing must not duplicate Guild history");
                Equal(1, guild.unreadCount, "reload/repeated processing must not increment Guild unread");
                Equal(2, yumiko.entries.Count, "reload/repeated processing must not duplicate Yumiko history");
'''
new_repeat = '''                Equal(4, guild.entries.Count, "reload/repeated processing must not duplicate Guild history");
                Equal(1, CountEventPrefix(guild, "guild-contract-offer:"),
                    "repeated processing must keep the core Guild offer exactly once");
                Equal(1, CountEventPrefix(guild, "guild-contract-accepted:"),
                    "repeated processing must keep the core Guild acceptance exactly once");
                Equal(1, CountEventPrefix(guild, "live:guild:offer:"),
                    "repeated processing must not duplicate the recovered Guild offer continuation");
                Equal(1, CountEventPrefix(guild, "live:guild:accepted:"),
                    "repeated processing must not duplicate the recovered Guild acceptance continuation");
                Equal(3, guild.unreadCount, "reload/repeated processing must not increment Guild unread");
                Equal(2, yumiko.entries.Count, "reload/repeated processing must not duplicate Yumiko history");
'''
if old_repeat not in text:
    raise SystemExit("Expected HunterGuildFlowTests repeated-processing assertion anchor was not found")
text = text.replace(old_repeat, new_repeat, 1)

helper_anchor = '''        private static void Equal<T>(T expected, T actual, string message)
        {
'''
helpers = '''        private static MessageEntry FindEventPrefix(ConversationState conversation, string prefix)
        {
            if (conversation == null || conversation.entries == null) return null;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && (entry.eventId ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal))
                    return entry;
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

        private static void Equal<T>(T expected, T actual, string message)
        {
'''
if helper_anchor not in text:
    raise SystemExit("Expected HunterGuildFlowTests helper anchor was not found")

path.write_text(text.replace(helper_anchor, helpers, 1), encoding="utf-8")
print("Updated Hunter Guild save/load assertions for deterministic live offer and acceptance recovery")
