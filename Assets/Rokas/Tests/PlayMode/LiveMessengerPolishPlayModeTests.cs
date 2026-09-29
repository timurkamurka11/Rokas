using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class LiveMessengerPolishPlayModeTests
    {
        private static readonly string[] ReactionIds =
        {
            "reaction_love", "reaction_wink", "reaction_angry", "reaction_surprised", "reaction_cry",
            "reaction_tasty", "reaction_heart", "reaction_darkheart", "reaction_fox", "reaction_thumbsup"
        };

        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator UserAudioAssetsDriveIncomingAndReactionRoutesWithoutReplay()
        {
            Initialize("rokas-live11-audio-");
            yield return null;

            AudioClip incoming = Resources.Load<AudioClip>("Messages/Audio/MessageArrive");
            AudioClip reaction = Resources.Load<AudioClip>("Messages/Audio/Reaction");
            Assert.That(incoming, Is.Not.Null, "user-provided Message sound arive.mp3 must be a real Resources AudioClip");
            Assert.That(reaction, Is.Not.Null, "user-provided emoji sound.mp3 must be a real Resources AudioClip");

            RokasBootstrap boot = Bootstrap();
            Assert.That(boot.Session.Messages.DeliverIncoming("live11-world-audio", "yumiko", "Проверь сообщения."), Is.True);
            yield return null;
            Assert.That(ReadViewString("LastMessageAudioCue"), Is.EqualTo("WorldNotification"));
            Assert.That(HasPlayingOrAssignedClip(incoming.name), Is.True,
                "world incoming route must use the uploaded incoming-message clip, not the old mouse click");

            OpenMessagesContact("yumiko");
            Assert.That(boot.Session.Messages.DeliverIncoming("live11-active-audio", "yumiko", "Я здесь."), Is.True);
            yield return null;
            Assert.That(ReadViewString("LastMessageAudioCue"), Is.EqualTo("ActiveReceive"));
            Assert.That(HasPlayingOrAssignedClip(incoming.name), Is.True,
                "active-chat receive must also use the uploaded incoming-message clip");

            MessageEntry active = FindEvent(boot.Session.Messages.GetConversation("yumiko"), "live11-active-audio");
            int beforeReaction = ReadViewInt("MessageAudioCueCount");
            Assert.That(boot.Session.LiveMessages.SetReaction("yumiko", active.messageId, "reaction_love"), Is.True);
            yield return null;
            Assert.That(ReadViewString("LastMessageAudioCue"), Is.EqualTo("Reaction"));
            Assert.That(ReadViewInt("MessageAudioCueCount"), Is.EqualTo(beforeReaction + 1),
                "one real reaction change must produce exactly one reaction cue");
            Assert.That(HasPlayingOrAssignedClip(reaction.name), Is.True,
                "reaction route must use the uploaded emoji sound clip");

            boot.SaveNow();
            yield return DestroyAndRecreate();
            Assert.That(ReadViewInt("MessageAudioCueCount"), Is.EqualTo(0),
                "historical messages/reactions must not replay audio during bootstrap");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PerBubblePickerUsesAllTenStickerReactionsAndSupportsChangeRemove()
        {
            Initialize("rokas-live11-picker-");
            yield return null;
            RokasBootstrap boot = Bootstrap();
            Assert.That(boot.Session.Messages.DeliverIncoming("live11-picker-message", "yumiko", "Выбирай реакцию."), Is.True);
            MessageEntry entry = FindEvent(boot.Session.Messages.GetConversation("yumiko"), "live11-picker-message");
            OpenMessagesContact("yumiko");
            yield return null;

            string openName = "MessagesReactionOpen_" + entry.messageId;
            Assert.That(FindButton(openName), Is.Not.Null,
                "every incoming NPC bubble must expose its own compact reaction opener");
            Press(openName);
            yield return null;
            Assert.That(CountButtonsWithPrefix("MessagesReactionPick_" + entry.messageId + "_"), Is.EqualTo(10),
                "reaction picker must contain all ten uploaded sticker choices");
            for (int index = 0; index < ReactionIds.Length; index++)
            {
                Texture2D texture = Resources.Load<Texture2D>("Messages/Reactions/" + ReactionIds[index]);
                Assert.That(texture, Is.Not.Null, "missing sliced reaction texture: " + ReactionIds[index]);
                Assert.That(FindButton("MessagesReactionPick_" + entry.messageId + "_" + ReactionIds[index]), Is.Not.Null);
            }

            Press("MessagesReactionPick_" + entry.messageId + "_reaction_love");
            yield return null;
            Assert.That(entry.reactionId, Is.EqualTo("reaction_love"));
            Assert.That(FindRect("MessagesReactionChip_" + entry.messageId), Is.Not.Null,
                "chosen sticker must render as a compact chip under its own bubble");

            Press(openName);
            yield return null;
            Press("MessagesReactionPick_" + entry.messageId + "_reaction_wink");
            yield return null;
            Assert.That(entry.reactionId, Is.EqualTo("reaction_wink"), "second choice must replace rather than duplicate");

            Press(openName);
            yield return null;
            Assert.That(FindButton("MessagesReactionRemove_" + entry.messageId), Is.Not.Null,
                "active player reaction must expose a compact remove action");
            Press("MessagesReactionRemove_" + entry.messageId);
            yield return null;
            Assert.That(entry.reactionId, Is.EqualTo(string.Empty));
            Assert.That(FindRect("MessagesReactionChip_" + entry.messageId), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AuthoredNpcReactionAppearsOncePersistsAndIsSilentOnReload()
        {
            Initialize("rokas-live11-npc-react-");
            yield return null;
            OpenMessagesContact("yumiko");
            RokasBootstrap boot = Bootstrap();

            int before = ReadViewInt("MessageAudioCueCount");
            Assert.That(boot.Session.Messages.SendOutgoing(
                "live11-npc-react", "yumiko", "Хорошо, возьму чай.", "yumiko.food.tea", "live11-npc-flow"), Is.True);
            MessageEntry outgoing = FindEvent(boot.Session.Messages.GetConversation("yumiko"), "live11-npc-react");
            yield return null;
            Assert.That(ReadStringField(outgoing, "npcReactionId"), Is.EqualTo(string.Empty));
            boot.Session.Tick(.35f);
            yield return null;
            Assert.That(ReadStringField(outgoing, "npcReactionId"), Is.EqualTo(string.Empty),
                "NPC reaction should not appear synchronously");
            boot.Session.Tick(1.25f);
            yield return null;
            Assert.That(ReadStringField(outgoing, "npcReactionId"), Is.EqualTo("reaction_tasty"));
            Assert.That(ReadViewString("LastMessageAudioCue"), Is.EqualTo("Reaction"));
            Assert.That(ReadViewInt("MessageAudioCueCount"), Is.EqualTo(before + 2),
                "outgoing send plus one authored NPC reaction should produce exactly two cues");
            Assert.That(FindRect("MessagesNpcReactionChip_" + outgoing.messageId), Is.Not.Null,
                "NPC reaction must render on the outgoing player bubble");

            boot.SaveNow();
            yield return DestroyAndRecreate();
            ConversationState restoredConversation = Bootstrap().Session.Messages.GetConversation("yumiko");
            MessageEntry restored = FindEvent(restoredConversation, "live11-npc-react");
            Assert.That(ReadStringField(restored, "npcReactionId"), Is.EqualTo("reaction_tasty"));
            Assert.That(ReadViewInt("MessageAudioCueCount"), Is.EqualTo(0),
                "persisted NPC reaction must not replay its reaction sound on reload");
            OpenMessagesContact("yumiko");
            yield return null;
            Assert.That(FindRect("MessagesNpcReactionChip_" + restored.messageId), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FullYomiLaptopGlowTracksTotalUnreadAcrossContactsAndClearsSmoothly()
        {
            Initialize("rokas-live11-glow-");
            yield return null;
            RokasBootstrap boot = Bootstrap();
            Assert.That(FindRect("HomeLaptopUnreadGlow"), Is.Not.Null,
                "home must include a whole-YOMI-laptop glow surface, not only the small dot");
            Assert.That(IsVisible("HomeLaptopUnreadGlow"), Is.False);

            Assert.That(boot.Session.Messages.DeliverIncoming("live11-glow-yumiko", "yumiko", "Первое непрочитанное."), Is.True);
            yield return null;
            Assert.That(IsVisible("HomeLaptopUnreadGlow"), Is.True,
                "whole YOMI laptop affordance must pulse while unread exists");
            Assert.That(boot.Session.Messages.DeliverIncoming("live11-glow-kaito", "kaito", "Второе непрочитанное."), Is.True);
            yield return null;

            Assert.That(boot.Session.Messages.OpenConversation("yumiko"), Is.True);
            yield return null;
            Assert.That(IsVisible("HomeLaptopUnreadGlow"), Is.True,
                "reading one contact must not clear glow while another contact still has unread");

            Assert.That(boot.Session.Messages.OpenConversation("kaito"), Is.True);
            for (int frame = 0; frame < 45; frame++) yield return null;
            Assert.That(boot.Session.Messages.TotalUnread, Is.EqualTo(0));
            Assert.That(IsVisible("HomeLaptopUnreadGlow"), Is.False,
                "whole-pill glow must fade away once total unread reaches zero");
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize(string prefix)
        {
            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("LiveMessengerPolishFixture");
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
            root = new GameObject("LiveMessengerPolishFixtureReloaded");
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

        private bool IsVisible(string name)
        {
            RectTransform rect = FindRect(name);
            if (rect == null) return false;
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group != null) return group.alpha > .01f;
            Graphic graphic = rect.GetComponent<Graphic>();
            return graphic == null || graphic.color.a > .01f;
        }

        private bool HasPlayingOrAssignedClip(string clipName)
        {
            if (root == null) return false;
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                if (source.clip != null && string.Equals(source.clip.name, clipName, StringComparison.Ordinal)) return true;
            return false;
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
            Assert.That(property, Is.Not.Null, "RokasView must expose testable routing state: " + propertyName);
            return property.GetValue(view);
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

        private static string ReadStringField(object value, string fieldName)
        {
            if (value == null) return string.Empty;
            FieldInfo field = value.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(value) as string ?? string.Empty : string.Empty;
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
