from pathlib import Path

path = Path("Assets/Rokas/Scripts/Core/GameSession.cs")
text = path.read_text(encoding="utf-8")
old = '''            LiveMessages = new LiveMessengerService(state, Messages);\n            LiveMessages.Changed += NotifyChanged;\n'''
new = '''            LiveMessages = new LiveMessengerService(state, Messages);\n'''
if old not in text:
    raise SystemExit("Expected LiveMessages Changed subscription was not present after core patch")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
print("Preserved existing GameSession.Changed notification cardinality")

contract_path = Path("Tests/Core/ContractAttachmentActionTests.cs")
contract = contract_path.read_text(encoding="utf-8")
old_assert = '''                Equal(2, restored.Messages.GetConversation("guild").entries.Count, "offer plus accepted follow-up remain exactly once");\n                Equal(2, restored.Messages.GetConversation("yumiko").entries.Count,\n                    "Yumiko recommendation and one-time gift survive reload exactly once");\n'''
new_assert = '''                ConversationState restoredGuild = restored.Messages.GetConversation("guild");\n                Equal(3, restoredGuild.entries.Count,\n                    "offer and accepted follow-up remain exactly once while pending live acceptance context recovers once");\n                Equal(1, CountEventPrefix(restoredGuild, "guild-contract-test"),\n                    "original Guild contract offer identity remains exactly once after live recovery");\n                Equal(1, CountEventPrefix(restoredGuild, "guild-contract-accepted:"),\n                    "original Guild acceptance follow-up remains exactly once after live recovery");\n                Equal(1, CountEventPrefix(restoredGuild, "live:guild:accepted:"),\n                    "pending live acceptance continuation resolves exactly once after reload");\n                Equal(2, restored.Messages.GetConversation("yumiko").entries.Count,\n                    "Yumiko recommendation and one-time gift survive reload exactly once");\n'''
if old_assert not in contract:
    raise SystemExit("Expected ContractAttachmentActionTests save/load assertion anchor was not found")
contract = contract.replace(old_assert, new_assert, 1)
helper_anchor = '''        private static void Equal<T>(T expected, T actual, string message)\n        {\n'''
helper = '''        private static int CountEventPrefix(ConversationState conversation, string prefix)\n        {\n            int count = 0;\n            if (conversation == null || conversation.entries == null) return count;\n            for (int index = 0; index < conversation.entries.Count; index++)\n            {\n                MessageEntry entry = conversation.entries[index];\n                if (entry != null && (entry.eventId ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)) count++;\n            }\n            return count;\n        }\n\n        private static void Equal<T>(T expected, T actual, string message)\n        {\n'''
if helper_anchor not in contract:
    raise SystemExit("Expected ContractAttachmentActionTests helper anchor was not found")
contract_path.write_text(contract.replace(helper_anchor, helper, 1), encoding="utf-8")
print("Updated stale contract reload count while preserving exactly-once event checks")

message_events_path = Path("Tests/Core/GameSessionMessageEventTests.cs")
events = message_events_path.read_text(encoding="utf-8")n