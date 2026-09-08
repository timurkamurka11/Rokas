using System;
using System.Reflection;
using Rokas.Core;

namespace Rokas.Core.Tests
{
    public static class CoordinateAttachmentActionTests
    {
        public static void RunAll()
        {
            CoordinateAttachmentActionActivatesExistingDestinationState();
        }

        private static void CoordinateAttachmentActionActivatesExistingDestinationState()
        {
            SaveData state = new SaveData();
            MessageService service = new MessageService(state);
            MessageAttachment attachment = new MessageAttachment
            {
                kind = MessageAttachmentKind.Coordinates,
                id = "kaito_coordinates_attachment",
                title = "Координаты",
                body = "Восточный район, Сектор B-7",
                targetId = "east-b7"
            };

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
            Equal(1, conversation.entries.Count,
                "activating coordinates must not create a duplicate message");
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
    }
}
