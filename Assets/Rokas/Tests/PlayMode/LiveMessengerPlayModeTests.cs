using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class LiveMessengerPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator YumikoLauncherTypingChoicesAndReactionAreLive()
        {
            Initialize("rokas-live-yumiko-");
            yield return null;

            OpenMessagesContact("yumiko");
            yield return null;

            Button launcher = FindButton("MessagesLiveLauncher_yumiko");
            Assert.That(launcher, Is.Not.Null, "Yumiko must expose the persistent live topic launcher");
            Assert.That(FindTextUnder(launcher.transform, "Label"), Does.Contain("Юмико"));
            Press(launcher.name);
            Assert.That(CountButtonsWithPrefix("MessagesLiveTopic_"), Is.GreaterThanOrEqualTo(4),
                "initial Yumiko launcher should expose a substantial topic subset");

            Press("MessagesLiveTopic_yumiko.food");
            Assert.That(CountRectsWithPrefix("OutgoingBubble_"), Is.GreaterThan(0),
                "choosing a topic must leave the player's sent message in readable history");

            Bootstrap().Session.Tick(.10f);
            yield return null;
            Assert.That(FindRect("MessagesTypingIndicator"), Is.Not.Null,
                "a live Yumiko response must show transient typing before delivery");
            Assert.That(FindText("MessagesHeaderStatus"), Does.Contain("печатает").IgnoreCase,
                "presence text must reflect transient Yumiko typing");

            for (int step = 0; step < 8 && CountButtonsWithPrefix("MessagesLiveChoice_") == 0; step++)
            {
                Bootstrap().Session.Tick(1.5f);
                yield return null;
            }
            int choices = CountButtonsWithPrefix("MessagesLiveChoice_");
            Assert.That(choices, Is.InRange(2, 4), "Yumiko micro-dialogue must expose two to four real reply choices");
            Button firstChoice = FindButtonWithPrefix("MessagesLiveChoice_");
            Assert.That(firstChoice, Is.Not.Null);
            Press(firstChoice.name);
            Assert.That(CountRectsWithPrefix("OutgoingBubble_"), Is.GreaterThanOrEqualTo(2),
                "selected response must become another player bubble");

            for (int step = 0; step < 10 && FindButton("MessagesLiveLauncher_yumiko") == null; step++)
            {
                Bootstrap().Session.Tick(1.5f);
                yield return null;
            }
            Assert.That(FindButton("MessagesLiveLauncher_yumiko"), Is.Not.Null,
                "resolved Yumiko micro-dialogue must return cleanly to the topic launcher");
            Assert.That(FindButtonWithPrefix("MessagesReactionOpen_"), Is.Not.Null,
                "Yumiko incoming history should expose the per-message Live Messenger 1.1 reaction opener");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GuildUsesFormalRequestLauncherAndKeepsContractCard()
        {
            Initialize("rokas-live-guild-");
            yield return null;

            OpenMessagesContact("guild");
            yield return null;

            Button launcher = FindButton("MessagesLiveLauncher_guild");
            Assert.That(launcher, Is.Not.Null, "Guild must expose a formal request launcher");
            Assert.That(FindAllText(launcher.transform), Does.Contain("запрос").IgnoreCase,
                "Guild launcher must use request language, not friendly chat copy");
            Press(launcher.name);
            Assert.That(CountButtonsWithPrefix("MessagesLiveTopic_"), Is.GreaterThanOrEqualTo(5),
                "Guild must expose at least five request topics");
            Assert.That(FindButtonWithPrefix("MessagesReactionOpen_"), Is.Not.Null,
                "player reaction picker must remain available on Guild incoming messages; Guild formality is enforced by authored NPC reaction rules");

            ConversationState guild = Bootstrap().Session.Messages.GetConversation("guild");
            Assert.That(guild, Is.Not.Null);
            MessageEntry contract = FindAttachment(guild, MessageAttachmentKind.Contract);
            Assert.That(contract, Is.Not.Null, "existing Guild Contract attachment must remain available");
            Assert.That(FindButton("MessagesAttachment_" + contract.attachment.id), Is.Not.Null,
                "Live Messenger must not replace the existing Contract card");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator KaitoLauncherCoexistsWithCoordinateAttachmentAfterBaseDialogue()
        {
            Initialize("rokas-live-kaito-");
            yield return null;

            RokasBootstrap boot = Bootstrap();
            boot.Session.Messages.MarkDialogueCompleted("kaito", "Kaito_Start");
            Assert.That(boot.Session.Messages.DeliverIncoming(
                "live-kaito-coordinate-seed",
                "kaito",
                "Проверь сектор B-7.",
                new MessageAttachment
                {
                    kind = MessageAttachmentKind.Coordinates,
                    id = "live_kaito_coordinate_attachment",
                    title = "Координаты",
                    body = "Восточный район, Сектор B-7",
                    targetId = "east-b7"
                }), Is.True);

            OpenMessagesContact("kaito");
            yield return null;
            Button launcher = FindButton("MessagesLiveLauncher_kaito");
            Assert.That(launcher, Is.Not.Null, "Kaito live topics must appear after the mandatory base Yarn dialogue is complete");
            Press(launcher.name);
            Assert.That(CountButtonsWithPrefix("MessagesLiveTopic_"), Is.GreaterThanOrEqualTo(6),
                "Kaito must expose at least six tactical/intel topics");
            Assert.That(FindButton("MessagesLiveTopic_kaito.coordinates"), Is.Not.Null,
                "Kaito must expose an authored coordinates topic when coordinate history exists");
            Assert.That(FindButton("MessagesAttachment_live_kaito_coordinate_attachment"), Is.Not.Null,
                "existing Coordinate attachment must remain actionable beside live topics");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WorldLaptopUnreadFeedbackTriggersOnceAndHistoricalReloadIsSilent()
        {
            Initialize("rokas-live-world-");
            yield return null;

            RokasBootstrap boot = Bootstrap();
            Assert.That(FindRect("HomeLaptopUnreadIndicator"), Is.Not.Null,
                "home laptop must own a persistent unread indicator object even while currently idle");
            Assert.That(boot.Session.Messages.DeliverIncoming(
                "live-world-yumiko-1", "yumiko", "Ты дома? Тогда проверь сообщения."), Is.True);
            yield return null;

            Assert.That(IsVisible("HomeLaptopUnreadIndicator"), Is.True,
                "new unread while laptop is closed must light the existing home laptop affordance");
            Assert.That(ReadViewString("LastMessageAudioCue"), Is.EqualTo("WorldNotification"),
                "laptop-closed new unread must route to the muted world notification cue");
            Assert.That(ReadViewInt("MessageAudioCueCount"), Is.EqualTo(1),
                "one authored arrival must produce one main world notification cue");

            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.That(ReadViewInt("MessageAudioCueCount"), Is.EqualTo(1),
                "repeated Tick must not retrigger the world notification sound");

            boot.SaveNow();
            yield return DestroyAndRecreate();
            Assert.That(IsVisible("HomeLaptopUnreadIndicator"), Is.True,
                "historical unread must keep the subtle persistent indicator after reload");
            Assert.That(ReadViewInt("MessageAudioCueCount"), Is.EqualTo(0),
                "historical unread reload must not replay a fresh notification sound");
            Assert.That(FindRect("MessagesTypingIndicator"), Is.Null,
                "historical reload must not replay fake typing presentation");
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "historical unread reload must not create a new popup");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ActiveContactUsesSoftReceiveInsteadOfWorldOrPopup()
        {
            Initialize("rokas-live-active-audio-");
            yield return null;

            OpenMessagesContact("yumiko");
            yield return null;
            RokasBootstrap boot = Bootstrap();
            Assert.That(boot.Session.Messages.DeliverIncoming(
                "live-active-yumiko-1", "yumiko", "Не пропадай так надолго."), Is.True);
            yield return null;

            Assert.That(ReadViewString("LastMessageAudioCue"), Is.EqualTo("ActiveReceive"),
                "incoming bubble in the already-open same contact must use the soft in-chat receive route");
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "same active contact must not produce a redundant compact popup");
            Assert.That(boot.Session.Messages.GetConversation("yumiko").unreadCount, Is.EqualTo(0),
                "existing active-contact unread behavior must remain intact");
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize(string prefix)
        {
            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("LiveMessengerFixture");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
        }

        private void OpenMessagesContact(string contactId)
        {
            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_" + contactId);
        }

        private RokasBootstrap Bootstrap()
        {
            RokasBootstrap boot = root != null ? root.GetComponent<RokasBootstrap>() : null;
            Assert.That(boot, Is.Not.Null);
            return boot;
        }

        private IEnumerator DestroyAndRecreate()
        {
            UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            root = new GameObject("LiveMessengerFixtureReloaded");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
            yield return null;
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
                if (button.name == name && button.gameObject.activeInHierarchy) return button;
            return null;
        }

        private Button FindButtonWithPrefix(string prefix)
        {
            if (root == null) return null;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.gameObject.activeInHierarchy && button.name.StartsWith(prefix, StringComparison.Ordinal)) return button;
            return null;
        }

        private int CountButtonsWithPrefix(string prefix)
        {
            int count = 0;
            if (root == null) return count;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.gameObject.activeInHierarchy && button.name.StartsWith(prefix, StringComparison.Ordinal)) count++;
            return count;
        }

        private RectTransform FindRect(string name)
        {
            if (root == null) return null;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name && rect.gameObject.activeInHierarchy) return rect;
            return null;
        }

        private int CountRectsWithPrefix(string prefix)
        {
            int count = 0;
            if (root == null) return count;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.gameObject.activeInHierarchy && rect.name.StartsWith(prefix, StringComparison.Ordinal)) count++;
            return count;
        }

        private bool IsVisible(string name)
        {
            RectTransform rect = FindRect(name);
            if (rect == null) return false;
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group != null) return group.alpha > .01f;
            Graphic graphic = rect.GetComponent<Graphic>();
            return graphic == null || graphic.color.a > .01f;
        }

        private string FindText(string name)
        {
            if (root == null) return null;
            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.name == name && text.gameObject.activeInHierarchy) return text.text;
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
                if (text.name == name && text.gameObject.activeInHierarchy) return text.text;
            return null;
        }

        private static string FindTextUnder(Transform parent, string childName)
        {
            if (parent == null) return null;
            foreach (TextMeshProUGUI text in parent.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.name == childName) return text.text;
            foreach (Text text in parent.GetComponentsInChildren<Text>(true))
                if (text.name == childName) return text.text;
            return null;
        }

        private static string FindAllText(Transform parent)
        {
            if (parent == null) return string.Empty;
            string value = string.Empty;
            foreach (TextMeshProUGUI text in parent.GetComponentsInChildren<TextMeshProUGUI>(true)) value += " " + text.text;
            foreach (Text text in parent.GetComponentsInChildren<Text>(true)) value += " " + text.text;
            return value.Trim();
        }

        private string ReadViewString(string propertyName)
        {
            object value = ReadViewProperty(propertyName);
            return value != null ? value.ToString() : string.Empty;
        }

        private int ReadViewInt(string propertyName)
        {
            object value = ReadViewProperty(propertyName);
            return value == null ? 0 : Convert.ToInt32(value);
        }

        private object ReadViewProperty(string propertyName)
        {
            object view = Bootstrap().View;
            PropertyInfo property = view.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "RokasView must expose testable message routing state: " + propertyName);
            return property.GetValue(view);
        }

        private static MessageEntry FindAttachment(ConversationState conversation, MessageAttachmentKind kind)
        {
            if (conversation == null || conversation.entries == null) return null;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && entry.attachment != null && entry.attachment.kind == kind) return entry;
            }
            return null;
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
