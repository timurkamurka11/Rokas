from pathlib import Path

path = Path("Tests/Core/YumikoFoodIntegrationTests.cs")
text = path.read_text(encoding="utf-8")

old_reload = '''                Equal(1, restoredYumiko.entries.Count, "reload must not synthesize a second first-purchase reaction");
                Equal("yumiko-food-first:" + FoodService.GreenTeaId, restoredYumiko.entries[0].eventId,
                    "reload must preserve the deterministic first-purchase event");
'''
new_reload = '''                Equal(2, restoredYumiko.entries.Count,
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
'''
if old_reload not in text:
    raise SystemExit("Expected Yumiko first-purchase reload assertion anchor was not found")
text = text.replace(old_reload, new_reload, 1)

old_post = '''                Equal(1, restoredYumiko.entries.Count, "post-load purchase must not redeliver historical first-purchase reaction");
'''
new_post = '''                Equal(2, restoredYumiko.entries.Count,
                    "post-load purchase must not redeliver the core reaction or recovered live continuation");
                Equal(1, CountEventPrefix(restoredYumiko, "yumiko-food-first:"),
                    "post-load purchase must keep the core first-purchase event exactly once");
                Equal(1, CountEventPrefix(restoredYumiko, "live:yumiko:purchase:"),
                    "post-load purchase must keep the recovered live continuation exactly once");
'''
if old_post not in text:
    raise SystemExit("Expected Yumiko post-load purchase assertion anchor was not found")
text = text.replace(old_post, new_post, 1)

old_return_reload = '''                Equal(yumiko.entries.Count, restoredYumiko.entries.Count,
                    "reload/bootstrap must not redeliver any historical post-battle reaction");
'''
new_return_reload = '''                Equal(yumiko.entries.Count + 2, restoredYumiko.entries.Count,
                    "reload must preserve history plus one deterministic live continuation for each real return");
                Equal(1, CountEventPrefix(restoredYumiko,
                    "yumiko-return:" + contract.id + ":1:sealed"),
                    "first core post-battle return reaction must remain exactly once");
                Equal(1, CountEventPrefix(restoredYumiko,
                    "yumiko-return:" + contract.id + ":2:failed"),
                    "second core post-battle return reaction must remain exactly once");
                Equal(2, CountEventPrefix(restoredYumiko, "live:yumiko:return:"),
                    "two real returns must recover exactly two distinct live continuations");
'''
if old_return_reload not in text:
    raise SystemExit("Expected Yumiko post-battle reload assertion anchor was not found")
text = text.replace(old_return_reload, new_return_reload, 1)

helper_anchor = '''        private static int ReadIntField(SaveData state, string name)
        {
'''
helper = '''        private static int CountEventPrefix(ConversationState conversation, string prefix)
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
'''
if helper_anchor not in text:
    raise SystemExit("Expected YumikoFoodIntegrationTests helper anchor was not found")

path.write_text(text.replace(helper_anchor, helper, 1), encoding="utf-8")
print("Updated Yumiko first-purchase and return save/load assertions for deterministic live recovery")
