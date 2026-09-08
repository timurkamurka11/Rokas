using System;
using System.Collections;
using System.IO;
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
    public sealed class HunterGuildPlayModeTests
    {
        private const string AcceptedText = "Контракт принят. Подготовьтесь к выходу на задание.";
        private const string AcceptedPreview = "Контракт принят. Подготовьтесь к вых…";
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator GuildAcceptanceFollowUpPersistsWithoutPopupOrDuplicate()
        {
            Initialize();
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_guild");
            yield return null;

            RokasBootstrap boot = Bootstrap();
            ConversationState guild = boot.Session.Messages.GetConversation("guild");
            Assert.That(guild, Is.Not.Null);
            Assert.That(guild.entries.Count, Is.EqualTo(1), "Guild should begin with one authored contract offer");
            Assert.That(guild.unreadCount, Is.EqualTo(0), "opening Guild should read the offer");
            MessageEntry offer = FindContractOffer(guild);
            Button card = FindButton("MessagesAttachment_" + offer.attachment.id);
            Assert.That(card, Is.Not.Null);
            Assert.That(card.IsInteractable(), Is.True);

            Press(card.name);
            for (int frame = 0; frame < 4; frame++) yield return null;

            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Accepted));
            Assert.That(offer.attachment.opened, Is.True);
            Assert.That(guild.entries.Count, Is.EqualTo(2), "acceptance must append one Guild follow-up");
            Assert.That(guild.entries[1].eventId, Is.EqualTo("guild-contract-accepted:" + boot.Session.Contract.id));
            Assert.That(guild.entries[1].text, Is.EqualTo(AcceptedText));
            Assert.That(guild.unreadCount, Is.EqualTo(0),
                "active Guild conversation should consume its own incoming unread through existing behavior");
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "active Guild acceptance follow-up must not create a redundant popup");
            Assert.That(FindTextUnder("MessagesContactFace_guild", "Preview"), Is.EqualTo(AcceptedPreview),
                "Guild contact preview must compact the latest real authored message without showing stale offer copy");
            Assert.That(FindButton("MessagesAttachment_" + offer.attachment.id).IsInteractable(), Is.False);

            boot.SaveNow();
            yield return DestroyAndRecreate();
            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_guild");
            for (int frame = 0; frame < 4; frame++) yield return null;

            RokasBootstrap restoredBoot = Bootstrap();
            ConversationState restored = restoredBoot.Session.Messages.GetConversation("guild");
            Assert.That(restored.entries.Count, Is.EqualTo(2),
                "Stop/Play must preserve offer plus exactly one acceptance follow-up");
            MessageEntry restoredOffer = FindContractOffer(restored);
            Assert.That(restoredOffer.attachment.opened, Is.True);
            Assert.That(FindButton("MessagesAttachment_" + restoredOffer.attachment.id).IsInteractable(), Is.False);
            Assert.That(restored.entries[1].eventId,
                Is.EqualTo("guild-contract-accepted:" + restoredBoot.Session.Contract.id));
            Assert.That(FindTextUnder("MessagesContactFace_guild", "Preview"), Is.EqualTo(AcceptedPreview));
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "historical Guild follow-up must not become a fresh notification after reload");
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-hunter-guild-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("HunterGuildFixture");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
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
            root = new GameObject("HunterGuildFixtureReloaded");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
            yield return null;
        }

        private static MessageEntry FindContractOffer(ConversationState guild)
        {
            if (guild == null || guild.entries == null) return null;
            for (int index = 0; index < guild.entries.Count; index++)
            {
                MessageEntry entry = guild.entries[index];
                if (entry != null && entry.attachment != null && entry.attachment.kind == MessageAttachmentKind.Contract)
                    return entry;
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
            if (root == null) return null;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name && button.gameObject.activeInHierarchy) return button;
            return null;
        }

        private RectTransform FindRect(string name)
        {
            if (root == null) return null;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name && rect.gameObject.activeInHierarchy) return rect;
            return null;
        }

        private string FindTextUnder(string parentName, string childName)
        {
            RectTransform parent = FindRect(parentName);
            if (parent == null) return null;
            foreach (TextMeshProUGUI text in parent.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.name == childName) return text.text;
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
