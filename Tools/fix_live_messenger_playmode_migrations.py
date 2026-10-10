from pathlib import Path

path = Path("Assets/Rokas/Tests/PlayMode/MessagesLifecyclePlayModeTests.cs")
text = path.read_text(encoding="utf-8")
needle = '''        private static int CountEvent(ConversationState conversation, string eventId)
        {
            int count = 0;
            if (conversation == null || conversation.entries == null) return count;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && string.Equals(entry.eventId, eventId, StringComparison.Ordinal)) count++;
            }
            return count;
        }

'''
first = text.find(needle)
if first < 0:
    raise SystemExit("Missing existing MessagesLifecycle CountEvent helper")
second = text.find(needle, first + len(needle))
if second < 0:
    raise SystemExit("Migration did not create the expected duplicate CountEvent helper")
third = text.find(needle, second + len(needle))
if third >= 0:
    raise SystemExit("Unexpected third MessagesLifecycle CountEvent helper")
text = text[:second] + text[second + len(needle):]
path.write_text(text, encoding="utf-8")
