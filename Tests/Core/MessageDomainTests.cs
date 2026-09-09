using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rokas.Core;

namespace Rokas.Core.Tests
{
    public static class MessageDomainTests
    {
        public static void RunAll()
        {
            IncomingMessageIncrementsUnread();
            OpeningConversationClearsItsUnread();
            DuplicateEventIdIsIdempotent();
            DifferentEventIdDeliversAnotherMessage();
            ChoiceSelectionSetsDeterministicBranchState();
            SaveLoadPreservesMessageHistory();
            SaveLoadPreservesUnread();
            SaveLoadPreservesDeliveredEventIds();
            ContactSearchReturnsExpectedContacts();
            ConversationOrderingIsDeterministic();
            OldSaveWithoutMessagesFieldsRemainsCompatible();
        }

        private static void IncomingMessageIncrementsUnread()
        {
            SaveData state = new SaveData();
            dynamic service = NewService(state);

            Equal(0, (int)service.TotalUnread, "new message state should start read");
            True((bool)service.DeliverIncoming("evt-kaito-1", "kaito", "Нужно обсудить то, что я видел сегодня ночью."), "new event should deliver once");
            Equal(1, (int)service.TotalUnread, "new incoming message should increment total unread");
            dynamic conversation = service.GetConversation("kaito");
            Equal(1, (int)conversation.unreadCount, "Kaito unread should increment");
            Equal(1, Entries(conversation).Count, "incoming event should append one message");
        }

        private static void OpeningConversationClearsItsUnread()
        {
            SaveData state = new SaveData();
            dynamic service = NewService(state);
            True((bool)service.DeliverIncoming("evt-kaito-1", "kaito", "Ночное сообщение"), "seed message should deliver");
            True((bool)service.DeliverIncoming("evt-yumiko-1", "yumiko", "Сообщение Юмико"), "second contact should deliver");

            True((bool)service.OpenConversation("kaito"), "opening an unread conversation should change state");

            Equal(0, (int)service.GetConversation("kaito").unreadCount, "opened conversation unread should clear");
            Equal(1, (int)service.GetConversation("yumiko").unreadCount, "other contact unread must remain");
            Equal(1, (int)service.TotalUnread, "total unread should retain other contacts");
        }

        private static void DuplicateEventIdIsIdempotent()
        {
            SaveData state = new SaveData();
            dynamic service = NewService(state);

            True((bool)service.DeliverIncoming("evt-kaito-dup", "kaito", "Первое получение"), "first event delivery should succeed");
            Equal(false, (bool)service.DeliverIncoming("evt-kaito-dup", "kaito", "Повтор не должен появиться"), "same event ID must be ignored");

            dynamic conversation = service.GetConversation("kaito");
            Equal(1, Entries(conversation).Count, "duplicate event must not append a second message");
            Equal(1, (int)conversation.unreadCount, "duplicate event must not increment unread");
        }

        private static void DifferentEventIdDeliversAnotherMessage()
        {
            SaveData state = new SaveData();
            dynamic service = NewService(state);

            True((bool)service.DeliverIncoming("evt-kaito-a", "kaito", "A"), "first unique event should deliver");
            True((bool)service.DeliverIncoming("evt-kaito-b", "kaito", "B"), "different event ID should deliver");

            dynamic conversation = service.GetConversation("kaito");
            Equal(2, Entries(conversation).Count, "two unique events should create two messages");
            Equal(2, (int)conversation.unreadCount, "two unread incoming messages should be counted");
        }

        private static void ChoiceSelectionSetsDeterministicBranchState()
        {
            SaveData state = new SaveData();
            dynamic service = NewService(state);

            True((bool)service.SelectChoice("kaito", "choice-cautious", "Что именно ты видел?", "kaito-cautious"), "first choice should be accepted");
            dynamic conversation = service.GetConversation("kaito");
            Equal("kaito-cautious", (string)conversation.branchState, "choice should persist deterministic branch state");
            Equal(1, Entries(conversation).Count, "choice should append one outgoing message");
            dynamic entry = Entries(conversation)[0];
            Equal(true, (bool)entry.outgoing, "choice entry should be outgoing");
            Equal("choice-cautious", (string)entry.choiceId, "choice entry should retain stable choice ID");
            Equal(false, (bool)service.SelectChoice("kaito", "choice-cautious", "duplicate", "wrong-branch"), "same choice ID must not be applied twice");
            Equal("kaito-cautious", (string)conversation.branchState, "duplicate choice must not mutate branch state");
        }

        private static void SaveLoadPreservesMessageHistory()
        {
            WithTempDirectory(delegate(string directory)
            {
                SaveData state = new SaveData();
                dynamic service = NewService(state);
                True((bool)service.DeliverIncoming("evt-history-1", "kaito", "Первое"), "history seed should deliver");
                True((bool)service.SelectChoice("kaito", "choice-history", "Ответ", "kaito-history"), "history choice should append");
                True((bool)service.DeliverIncoming("evt-history-2", "kaito", "Второе"), "second history event should deliver");

                SaveStore store = new SaveStore(directory, new JsonCodec());
                Equal(SaveWriteStatus.Saved, store.Save(state).Status, "messages state should save through existing SaveStore");
                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status, "messages save should load from primary");

                dynamic restored = NewService(loaded.Data);
                dynamic conversation = restored.GetConversation("kaito");
                Equal(3, Entries(conversation).Count, "message history should survive save/load");
                Equal("kaito-history", (string)conversation.branchState, "branch state should survive save/load");
                dynamic outgoing = Entries(conversation)[1];
                Equal("choice-history", (string)outgoing.choiceId, "selected choice should survive save/load");
            });
        }

        private static void SaveLoadPreservesUnread()
        {
            WithTempDirectory(delegate(string directory)
            {
                SaveData state = new SaveData();
                dynamic service = NewService(state);
                True((bool)service.DeliverIncoming("evt-unread-1", "yumiko", "Непрочитанное"), "unread seed should deliver");

                SaveStore store = new SaveStore(directory, new JsonCodec());
                Equal(SaveWriteStatus.Saved, store.Save(state).Status, "unread state should save");
                dynamic restored = NewService(store.Load().Data);

                Equal(1, (int)restored.TotalUnread, "total unread should survive save/load");
                Equal(1, (int)restored.GetConversation("yumiko").unreadCount, "per-contact unread should survive save/load");
            });
        }

        private static void SaveLoadPreservesDeliveredEventIds()
        {
            WithTempDirectory(delegate(string directory)
            {
                SaveData state = new SaveData();
                dynamic service = NewService(state);
                True((bool)service.DeliverIncoming("evt-persist-id", "mika", "Один раз"), "event should seed delivered IDs");

                SaveStore store = new SaveStore(directory, new JsonCodec());
                Equal(SaveWriteStatus.Saved, store.Save(state).Status, "delivered IDs should save");
                dynamic restored = NewService(store.Load().Data);
                dynamic conversation = restored.GetConversation("mika");
                int before = Entries(conversation).Count;

                Equal(false, (bool)restored.DeliverIncoming("evt-persist-id", "mika", "Дубликат после загрузки"), "delivered event ID should remain idempotent after load");
                Equal(before, Entries(conversation).Count, "duplicate after load must not append history");
            });
        }

        private static void ContactSearchReturnsExpectedContacts()
        {
            SaveData state = new SaveData();
            dynamic service = NewService(state);

            List<string> yu = ContactIds((IEnumerable)service.SearchContacts("yu"));
            Equal(1, yu.Count, "search for yu should return one expected contact");
            Equal("yumiko", yu[0], "search should find Yumiko by name");

            List<string> guild = ContactIds((IEnumerable)service.SearchContacts("guild"));
            Equal(1, guild.Count, "search should match Guild contact");
            Equal("guild", guild[0], "search result should retain stable contact ID");
        }

        private static void ConversationOrderingIsDeterministic()
        {
            SaveData state = new SaveData();
            dynamic service = NewService(state);
            True((bool)service.DeliverIncoming("evt-order-y-1", "yumiko", "Y1"), "Yumiko should seed ordering");
            True((bool)service.DeliverIncoming("evt-order-k-1", "kaito", "K1"), "Kaito should become newest");

            List<string> first = ConversationIds((IEnumerable)service.GetOrderedConversations());
            Equal("kaito", first[0], "most recently messaged conversation should sort first");
            Equal("yumiko", first[1], "older conversation should sort second");

            True((bool)service.DeliverIncoming("evt-order-y-2", "yumiko", "Y2"), "new Yumiko event should update order");
            List<string> second = ConversationIds((IEnumerable)service.GetOrderedConversations());
            Equal("yumiko", second[0], "newest conversation should deterministically move first");
            Equal("kaito", second[1], "previous newest should move second");
        }

        private static void OldSaveWithoutMessagesFieldsRemainsCompatible()
        {
            WithTempDirectory(delegate(string directory)
            {
                JsonCodec codec = new JsonCodec();
                SaveData state = new SaveData();
                JsonNode root = JsonNode.Parse(codec.Serialize(state));
                root.AsObject().Remove("messages");

                SaveStore store = new SaveStore(directory, codec);
                Directory.CreateDirectory(directory);
                File.WriteAllText(store.PrimaryPath, root.ToJsonString(), System.Text.Encoding.UTF8);

                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status, "old save without Messages fields must remain valid");
                dynamic restored = NewService(loaded.Data);
                Equal(0, (int)restored.TotalUnread, "old save should initialize empty Messages state");
                Equal(0, ConversationIds((IEnumerable)restored.GetOrderedConversations()).Count, "old save should not invent conversation history");
            });
        }

        private static dynamic NewService(SaveData state)
        {
            Type type = typeof(SaveData).Assembly.GetType("Rokas.Core.MessageService");
            True(type != null, "MessageService domain type must exist before Messages behavior can pass");
            object instance = Activator.CreateInstance(type, new object[] { state });
            True(instance != null, "MessageService constructor should produce an instance");
            return instance;
        }

        private static IList Entries(dynamic conversation)
        {
            return (IList)conversation.entries;
        }

        private static List<string> ContactIds(IEnumerable values)
        {
            List<string> ids = new List<string>();
            foreach (object value in values)
            {
                FieldInfo field = value.GetType().GetField("id");
                True(field != null, "ContactDefinition should expose stable id field");
                ids.Add((string)field.GetValue(value));
            }
            return ids;
        }

        private static List<string> ConversationIds(IEnumerable values)
        {
            List<string> ids = new List<string>();
            foreach (object value in values)
            {
                FieldInfo field = value.GetType().GetField("contactId");
                True(field != null, "ConversationState should expose stable contactId field");
                ids.Add((string)field.GetValue(value));
            }
            return ids;
        }

        private static void WithTempDirectory(Action<string> action)
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-messages-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                action(directory);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void True(bool actual, string message)
        {
            Equal(true, actual, message);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + ". Expected " + expected + ", got " + actual + ".");
            }
        }

        private sealed class JsonCodec : ISaveCodec
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
