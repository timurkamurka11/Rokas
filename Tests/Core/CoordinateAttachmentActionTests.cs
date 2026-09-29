using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Rokas.Core;

namespace Rokas.Core.Tests
{
    public static class CoordinateAttachmentActionTests
    {
        public static void RunAll()
        {
            CoordinateAttachmentActionActivatesExistingDestinationState();
            UnknownCoordinateDestinationReturnsTypedDiagnostic();
            MissingCoordinateDestinationReturnsTypedDiagnostic();
            RepeatedCoordinateActionIsIdempotent();
            SaveLoadPreservesCoordinateAttachmentAndPreventsRedelivery();
        }

        private static void CoordinateAttachmentActionActivatesExistingDestinationState()
        {
            SaveData state = new SaveData();
            MessageService service = new MessageService(state);
            MessageAttachment attachment = CoordinateAttachment("kaito_coordinates_attachment", "east-b7");

            True(service.DeliverIncoming("evt-coordinate-action", "kaito", "Координаты", attachment),
                "coordinate event should deliver before its action is invoked");
            ConversationState conversation = service.GetConversation("kaito");
            Equal(1, conversation.entries.Count, "coordinate event should create exactly one history entry");
            MessageEntry entry = conversation.entries[0];

            MethodInfo action = typeof(MessageService).GetMethod(
                "ActivateAttachment", new[] { typeof(string), typeof(string) });
            True(action != null,
                "MessageService.ActivateAttachment(contactId, messageId) must exist for real coordinate actions");

            object result = action.Invoke(service, new object[] { "kaito", entry.messageId });
            True(result != null, "coordinate action must return a typed result");
            PropertyInfo succeeded = result.GetType().GetProperty("Succeeded");
            True(succeeded != null && (bool)succeeded.GetValue(result),
                "coordinate action result must report successful activation");

            FieldInfo destination = typeof(SaveData).GetField("activeDestinationId");
            True(destination != null,
                "coordinate action must hand off its stable target to existing SaveData game state");
            Equal("east-b7", (string)destination.GetValue(state),
                "coordinate action must activate the authored east-b7 destination");
            Equal("east-b7", entry.attachment.targetId,
                "coordinate attachment must retain stable destination data");
            Equal(true, entry.attachment.opened,
                "successful coordinate action must persist attachment opened state");
            Equal(1, conversation.entries.Count,
                "activating coordinates must not create a duplicate message");
        }

        private static void UnknownCoordinateDestinationReturnsTypedDiagnostic()
        {
            SaveData state = new SaveData();
            MessageService service = new MessageService(state);
            MessageAttachment attachment = CoordinateAttachment("bad-coordinate-attachment", "unknown-sector");

            True(service.DeliverIncoming("evt-coordinate-unknown", "kaito", "Координаты", attachment),
                "invalid coordinate target still arrives as authored message data");
            ConversationState conversation = service.GetConversation("kaito");
            MessageAttachmentActionResult result = service.ActivateAttachment("kaito", conversation.entries[0].messageId);

            Equal("UnknownDestination", result.Status.ToString(),
                "unknown coordinate target must return a distinct typed diagnostic");
            Equal(false, result.Succeeded,
                "unknown destination must never report fake coordinate-action success");
            Equal(string.Empty, state.activeDestinationId,
                "unknown destination must not mutate active game destination state");
            Equal(false, conversation.entries[0].attachment.opened,
                "failed unknown destination action must not mark attachment opened");
            Equal(1, conversation.entries.Count,
                "failed coordinate action must not duplicate message history");
        }

        private static void MissingCoordinateDestinationReturnsTypedDiagnostic()
        {
            SaveData state = new SaveData();
            MessageService service = new MessageService(state);
            MessageAttachment attachment = CoordinateAttachment("missing-coordinate-target", string.Empty);

            True(service.DeliverIncoming("evt-coordinate-missing", "kaito", "Координаты", attachment),
                "coordinate message with missing target should remain inspectable as authored data");
            ConversationState conversation = service.GetConversation("kaito");
            MessageAttachmentActionResult result = service.ActivateAttachment("kaito", conversation.entries[0].messageId);

            Equal("MissingDestination", result.Status.ToString(),
                "missing coordinate target must return a distinct typed diagnostic");
            Equal(false, result.Succeeded, "missing destination cannot be successful");
            Equal(string.Empty, state.activeDestinationId,
                "missing destination must not mutate active game destination state");
            Equal(false, conversation.entries[0].attachment.opened,
                "missing destination must not mark attachment opened");
        }

        private static void RepeatedCoordinateActionIsIdempotent()
        {
            SaveData state = new SaveData();
            MessageService service = new MessageService(state);
            MessageAttachment attachment = CoordinateAttachment("repeat-coordinate", "east-b7");
            True(service.DeliverIncoming("evt-coordinate-repeat", "kaito", "Координаты", attachment),
                "repeat test should seed one coordinate event");
            ConversationState conversation = service.GetConversation("kaito");
            string messageId = conversation.entries[0].messageId;

            MessageAttachmentActionResult first = service.ActivateAttachment("kaito", messageId);
            MessageAttachmentActionResult second = service.ActivateAttachment("kaito", messageId);

            Equal("Activated", first.Status.ToString(), "first coordinate action should activate destination");
            Equal("AlreadyActive", second.Status.ToString(),
                "repeated coordinate action should return typed idempotent result");
            Equal(true, second.Succeeded,
                "already-active coordinate remains a successful no-op for UI interaction");
            Equal("east-b7", state.activeDestinationId,
                "repeat action must preserve the same active destination");
            Equal(1, conversation.entries.Count,
                "repeat coordinate click must never create duplicate messages");
        }

        private static void SaveLoadPreservesCoordinateAttachmentAndPreventsRedelivery()
        {
            WithTempDirectory(delegate(string directory)
            {
                SaveData state = new SaveData();
                MessageService service = new MessageService(state);
                MessageAttachment attachment = CoordinateAttachment("persist-coordinate", "east-b7");
                True(service.DeliverIncoming("evt-coordinate-persist", "kaito", "Координаты", attachment),
                    "persistence test should seed coordinate event");
                ConversationState conversation = service.GetConversation("kaito");
                Equal("Activated", service.ActivateAttachment("kaito", conversation.entries[0].messageId).Status.ToString(),
                    "coordinate should activate before save");

                SaveStore store = new SaveStore(directory, new JsonCodec());
                Equal(SaveWriteStatus.Saved, store.Save(state).Status,
                    "coordinate attachment state should save through existing SaveStore");
                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.LoadedPrimary, loaded.Status,
                    "coordinate attachment state should load from primary save");

                MessageService restored = new MessageService(loaded.Data);
                ConversationState restoredConversation = restored.GetConversation("kaito");
                Equal(1, restoredConversation.entries.Count,
                    "save/load must preserve exactly one coordinate message");
                MessageAttachment restoredAttachment = restoredConversation.entries[0].attachment;
                Equal(MessageAttachmentKind.Coordinates, restoredAttachment.kind,
                    "save/load must preserve coordinate attachment kind");
                Equal("persist-coordinate", restoredAttachment.id,
                    "save/load must preserve stable attachment identity");
                Equal("east-b7", restoredAttachment.targetId,
                    "save/load must preserve stable coordinate target");
                Equal(true, restoredAttachment.opened,
                    "save/load must preserve opened coordinate state");
                Equal("east-b7", loaded.Data.activeDestinationId,
                    "save/load must preserve coordinate handoff in existing game state");

                Equal(false, restored.DeliverIncoming(
                        "evt-coordinate-persist", "kaito", "Дубликат", CoordinateAttachment("duplicate", "east-b7")),
                    "stable event ID must prevent coordinate attachment redelivery after reload");
                Equal(1, restoredConversation.entries.Count,
                    "reload followed by same event must not duplicate attachment history");
            });
        }

        private static MessageAttachment CoordinateAttachment(string id, string targetId)
        {
            return new MessageAttachment
            {
                kind = MessageAttachmentKind.Coordinates,
                id = id,
                title = "Координаты",
                body = "Восточный район, Сектор B-7",
                targetId = targetId
            };
        }

        private static void WithTempDirectory(Action<string> action)
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-coordinate-tests-" + Guid.NewGuid().ToString("N"));
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
