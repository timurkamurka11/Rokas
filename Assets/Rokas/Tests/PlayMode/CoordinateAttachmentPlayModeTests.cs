using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class CoordinateAttachmentPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator CoordinateAttachmentClickActivatesDestinationWithoutDuplicatingHistory()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-messages-coordinate-action-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("MessagesCoordinateActionFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_kaito");

            yield return WaitForButton("MessagesChoice_kaito_choice_details", 180);
            Press("MessagesChoice_kaito_choice_details");
            yield return WaitForButton("MessagesChoice_kaito_choice_guild", 180);
            Press("MessagesChoice_kaito_choice_guild");

            yield return WaitForButton("MessagesAttachment_kaito_coordinates_attachment", 180);
            ConversationState before = boot.Session.Messages.GetConversation("kaito");
            int historyCount = before.entries.Count;
            Assert.That(boot.Session.State.activeDestinationId, Is.Empty,
                "coordinate destination should remain inactive until the player opens the attachment");

            Press("MessagesAttachment_kaito_coordinates_attachment");
            yield return null;

            Assert.That(boot.Session.State.activeDestinationId, Is.EqualTo("east-b7"),
                "coordinate attachment click must hand off the authored target to existing game state");
            ConversationState after = boot.Session.Messages.GetConversation("kaito");
            Assert.That(after.entries.Count, Is.EqualTo(historyCount),
                "coordinate attachment action must not add a duplicate message");
            MessageEntry coordinate = FindCoordinateAttachment(after, "kaito_coordinates_attachment");
            Assert.That(coordinate, Is.Not.Null);
            Assert.That(coordinate.attachment.opened, Is.True,
                "coordinate attachment opened state must persist through MessageService");
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator WaitForButton(string name, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                if (FindButton(name) != null)
                {
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("Timed out waiting for Messages interaction: " + name);
        }

        private static MessageEntry FindCoordinateAttachment(ConversationState conversation, string attachmentId)
        {
            if (conversation == null || conversation.entries == null)
            {
                return null;
            }
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && entry.attachment != null &&
                    entry.attachment.kind == MessageAttachmentKind.Coordinates &&
                    string.Equals(entry.attachment.id, attachmentId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }
            return null;
        }

        private void Press(string name)
        {
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, "Missing active interaction: " + name);
            Assert.That(button.IsInteractable(), Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private Button FindButton(string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                {
                    return button;
                }
            }
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
