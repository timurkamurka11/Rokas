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
    public sealed class MessagesLifecyclePlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator DestroyAndReloadPersistsDirtyMessageHistory()
        {
            Initialize("rokas-messages-reload-dirty-");
            yield return null;

            RokasBootstrap first = Bootstrap();
            Assert.That(first.Session.Messages.DeliverIncoming(
                "manual-reload-mika-1", "mika", "Сообщение до остановки Play Mode."), Is.True);
            Assert.That(CountEntries(first.Session.Messages.GetConversation("mika")), Is.EqualTo(1));

            yield return DestroyAndRecreate();

            ConversationState restored = Bootstrap().Session.Messages.GetConversation("mika");
            Assert.That(restored, Is.Not.Null,
                "destroy/recreate must restore Messages changes made before Unity exits Play Mode");
            Assert.That(CountEntries(restored), Is.EqualTo(1),
                "re-entering Play Mode must restore the same history without losing or duplicating it");
            Assert.That(CountEvent(restored, "manual-reload-mika-1"), Is.EqualTo(1));
            Assert.That(restored.unreadCount, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CompletedDialogueDoesNotAutoReplayAfterReload()
        {
            Initialize("rokas-messages-reload-dialogue-");
            yield return null;

            yield return CompleteKaitoDialogue();
            RokasBootstrap first = Bootstrap();
            ConversationState before = first.Session.Messages.GetConversation("kaito");
            Assert.That(before, Is.Not.Null);
            string[] idsBefore = MessageIds(before);
            first.SaveNow();

            yield return DestroyAndRecreate();
            Press("LaptopHotspot");
            Press("LaptopMessages");
            yield return null;
            Press("MessagesContact_kaito");
            for (int frame = 0; frame < 12; frame++) yield return null;

            RokasBootstrap restoredBoot = Bootstrap();
            ConversationState after = restoredBoot.Session.Messages.GetConversation("kaito");
            string liveCoordinatesEventId = "live:kaito:coordinates:kaito_coordinates_attachment:bubble:1";
            string[] idsAfter = MessageIds(after);
            Assert.That(idsAfter.Length, Is.EqualTo(idsBefore.Length + 1),
                "reload may append only the one persisted deterministic Kaito coordinate continuation");
            for (int index = 0; index < idsBefore.Length; index++)
                Assert.That(idsAfter[index], Is.EqualTo(idsBefore[index]),
                    "reload must not append, reorder, or replace any pre-existing Yarn/dialogue entry at index " + index);
            Assert.That(CountEvent(after, "kaito_intro_01"), Is.EqualTo(1));
            Assert.That(CountEvent(after, "kaito_details_reply"), Is.EqualTo(1));
            Assert.That(CountEvent(after, "kaito_anomaly_01"), Is.EqualTo(1));
            Assert.That(CountEvent(after, "kaito_guild_details"), Is.EqualTo(1));
            Assert.That(CountEvent(after, "kaito_coordinates_01"), Is.EqualTo(1));
            Assert.That(CountEvent(after, "kaito_coordinates_attachment"), Is.EqualTo(1));
            Assert.That(CountChoice(after, "kaito_choice_details"), Is.EqualTo(1));
            Assert.That(CountChoice(after, "kaito_choice_guild"), Is.EqualTo(1));
            Assert.That(after.completedDialogueIds, Does.Contain("Kaito_Start"),
                "completed Yarn node must remain completed after recovery");
            Assert.That(CountEvent(after, liveCoordinatesEventId), Is.EqualTo(1),
                "pending Kaito coordinate continuation must recover exactly once");
            MessageEntry continuation = FindEvent(after, liveCoordinatesEventId);
            Assert.That(continuation, Is.Not.Null);
            Assert.That(continuation.messageId, Is.EqualTo("msg-9"));
            Assert.That(continuation.sequence, Is.EqualTo(9));
            Assert.That(continuation.outgoing, Is.False, "recovered continuation must remain an incoming Kaito bubble");
            Assert.That(continuation.chainId, Is.EqualTo("live:kaito:coordinates:kaito_coordinates_attachment"));
            Assert.That(continuation.text,
                Is.EqualTo("Если маркер сместится хотя бы на один сектор — не следуй за ним автоматически."));
            Assert.That(FindButton("MessagesChoice_kaito_choice_details"), Is.Null,
                "a completed persisted dialogue must not expose its first choice set again");
            Assert.That(FindButton("MessagesChoice_kaito_choice_skeptic"), Is.Null,
                "a completed persisted dialogue must not visually restart from Kaito_Start");

            restoredBoot.SaveNow();
            yield return DestroyAndRecreate();
            for (int frame = 0; frame < 4; frame++) yield return null;

            ConversationState afterSecondReload = Bootstrap().Session.Messages.GetConversation("kaito");
            Assert.That(afterSecondReload.entries.Count, Is.EqualTo(idsBefore.Length + 1),
                "a second reload must not append another Kaito coordinate continuation");
            Assert.That(CountEvent(afterSecondReload, liveCoordinatesEventId), Is.EqualTo(1));
            Assert.That(CountEvent(afterSecondReload, "kaito_coordinates_attachment"), Is.EqualTo(1));
            Assert.That(CountChoice(afterSecondReload, "kaito_choice_details"), Is.EqualTo(1));
            Assert.That(CountChoice(afterSecondReload, "kaito_choice_guild"), Is.EqualTo(1));
            Assert.That(afterSecondReload.completedDialogueIds, Does.Contain("Kaito_Start"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ConsumedCoordinateAttachmentRemainsDisabledAfterReload()
        {
            Initialize("rokas-messages-reload-attachment-");
            yield return null;

            yield return CompleteKaitoDialogue();
            Press("MessagesAttachment_kaito_coordinates_attachment");
            yield return null;

            RokasBootstrap first = Bootstrap();
            ConversationState before = first.Session.Messages.GetConversation("kaito");
            MessageEntry coordinateBefore = FindAttachment(before, "kaito_coordinates_attachment");
            Assert.That(coordinateBefore, Is.Not.Null);
            Assert.That(coordinateBefore.attachment.opened, Is.True);
            Assert.That(first.Session.State.activeDestinationId, Is.EqualTo("east-b7"));
            first.SaveNow();

            yield return DestroyAndRecreate();
            Press("LaptopHotspot");
            Press("LaptopMessages");
            yield return null;
            Press("MessagesContact_kaito");
            for (int frame = 0; frame < 8; frame++) yield return null;

            ConversationState restored = Bootstrap().Session.Messages.GetConversation("kaito");
            Assert.That(CountAttachment(restored, "kaito_coordinates_attachment"), Is.EqualTo(1),
                "reload must not create a fresh visual/domain copy of a consumed attachment");
            MessageEntry coordinateAfter = FindAttachment(restored, "kaito_coordinates_attachment");
            Assert.That(coordinateAfter.attachment.opened, Is.True,
                "consumed attachment state must survive save/load");
            Assert.That(Bootstrap().Session.State.activeDestinationId, Is.EqualTo("east-b7"));
            Button card = FindButton("MessagesAttachment_kaito_coordinates_attachment");
            Assert.That(card, Is.Not.Null, "historical attachment card should remain visible in conversation history");
            Assert.That(card.IsInteractable(), Is.False,
                "a consumed historical attachment must render disabled rather than as a fresh action");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HistoricalUnreadDoesNotCreateFreshNotificationAfterReload()
        {
            Initialize("rokas-messages-reload-notification-");
            yield return null;

            RokasBootstrap first = Bootstrap();
            Assert.That(first.Session.Messages.DeliverIncoming(
                "manual-reload-unread-1", "mika", "Непрочитанное историческое сообщение."), Is.True);
            first.SaveNow();

            yield return DestroyAndRecreate();
            for (int frame = 0; frame < 8; frame++) yield return null;

            ConversationState restored = Bootstrap().Session.Messages.GetConversation("mika");
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.unreadCount, Is.EqualTo(1),
                "historical unread state should persist without incrementing on bootstrap");
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "already persisted historical unread must not produce a new-arrival popup on reload");
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator CompleteKaitoDialogue()
        {
            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_kaito");
            yield return WaitForButton("MessagesChoice_kaito_choice_details", 180);
            Press("MessagesChoice_kaito_choice_details");
            yield return WaitForButton("MessagesChoice_kaito_choice_guild", 180);
            Press("MessagesChoice_kaito_choice_guild");
            yield return WaitForButton("MessagesAttachment_kaito_coordinates_attachment", 180);
        }

        private IEnumerator DestroyAndRecreate()
        {
            UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            root = new GameObject("MessagesLifecycleFixtureReloaded");
            RokasBootstrap boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;
        }

        private IEnumerator WaitForButton(string name, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                if (FindButton(name) != null) yield break;
                yield return null;
            }
            Assert.Fail("Timed out waiting for active interaction: " + name);
        }

        private void Initialize(string prefix)
        {
            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("MessagesLifecycleFixture");
            RokasBootstrap boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
        }

        private RokasBootstrap Bootstrap()
        {
            RokasBootstrap boot = root != null ? root.GetComponent<RokasBootstrap>() : null;
            Assert.That(boot, Is.Not.Null);
            return boot;
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
            if (root == null) return null;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name && button.gameObject.activeInHierarchy) return button;
            }
            return null;
        }

        private RectTransform FindRect(string name)
        {
            if (root == null) return null;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name == name && rect.gameObject.activeInHierarchy) return rect;
            }
            return null;
        }

        private static int CountEntries(ConversationState conversation)
        {
            return conversation != null && conversation.entries != null ? conversation.entries.Count : 0;
        }

        private static int CountEvent(ConversationState conversation, string eventId)
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

        private static int CountChoice(ConversationState conversation, string choiceId)
        {
            int count = 0;
            if (conversation == null || conversation.entries == null) return count;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && string.Equals(entry.choiceId, choiceId, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        private static MessageEntry FindEvent(ConversationState conversation, string eventId)
        {
            if (conversation == null || conversation.entries == null) return null;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && string.Equals(entry.eventId, eventId, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        private static string[] MessageIds(ConversationState conversation)
        {
            if (conversation == null || conversation.entries == null) return Array.Empty<string>();
            string[] result = new string[conversation.entries.Count];
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                result[index] = entry != null ? entry.messageId : string.Empty;
            }
            return result;
        }

        private static MessageEntry FindAttachment(ConversationState conversation, string attachmentId)
        {
            if (conversation == null || conversation.entries == null) return null;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && entry.attachment != null &&
                    string.Equals(entry.attachment.id, attachmentId, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        private static int CountAttachment(ConversationState conversation, string attachmentId)
        {
            int count = 0;
            if (conversation == null || conversation.entries == null) return count;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && entry.attachment != null &&
                    string.Equals(entry.attachment.id, attachmentId, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
