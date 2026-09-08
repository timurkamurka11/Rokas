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
            AssertGuildIdentity(card);

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
            Button consumedCard = FindButton("MessagesAttachment_" + offer.attachment.id);
            Assert.That(consumedCard.IsInteractable(), Is.False);
            AssertGuildIdentity(consumedCard);

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
            Button restoredCard = FindButton("MessagesAttachment_" + restoredOffer.attachment.id);
            Assert.That(restoredCard.IsInteractable(), Is.False);
            AssertGuildIdentity(restoredCard);
            Assert.That(restored.entries[1].eventId,
                Is.EqualTo("guild-contract-accepted:" + restoredBoot.Session.Contract.id));
            Assert.That(FindTextUnder("MessagesContactFace_guild", "Preview"), Is.EqualTo(AcceptedPreview));
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "historical Guild follow-up must not become a fresh notification after reload");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CompletionNotificationDoesNotRepeatForSameCompletedState()
        {
            Initialize();
            yield return null;

            RokasBootstrap boot = Bootstrap();
            boot.Session.State.phase = RunPhase.Payment;
            boot.Session.State.activeContractId = boot.Session.Contract.id;

            Assert.That(boot.Session.ClaimPayment(), Is.True, "one real Payment transition must succeed");
            yield return null;

            ConversationState guild = boot.Session.Messages.GetConversation("guild");
            Assert.That(guild, Is.Not.Null);
            Assert.That(guild.entries.Count, Is.EqualTo(1));
            Assert.That(guild.entries[0].eventId,
                Is.EqualTo("guild-contract-completed:" + boot.Session.Contract.id + ":1"));
            Assert.That(guild.unreadCount, Is.EqualTo(1));
            Assert.That(CountRects("MessagesNotification"), Is.EqualTo(1),
                "one real completion should create one notification surface");

            int history = guild.entries.Count;
            int unread = guild.unreadCount;
            Assert.That(boot.Session.ClaimPayment(), Is.False,
                "same already-completed state must not process payment a second time");
            for (int frame = 0; frame < 4; frame++) yield return null;

            Assert.That(guild.entries.Count, Is.EqualTo(history), "same state must not append completion history");
            Assert.That(guild.unreadCount, Is.EqualTo(unread), "same state must not increment unread");
            Assert.That(CountRects("MessagesNotification"), Is.EqualTo(1),
                "same state must not create a second notification popup");
            LogAssert.NoUnexpectedReceived();
        }

        private void AssertGuildIdentity(Button card)
        {
            Assert.That(card, Is.Not.Null);
            RawImage identity = null;
            foreach (RawImage image in card.GetComponentsInChildren<RawImage>(true))
            {
                if (image.name == "AttachmentSenderIdentity")
                {
                    identity = image;
                    break;
                }
            }
            Assert.That(identity, Is.Not.Null,
                "Guild Contract card must render reusable sender identity rather than an anonymous system block");
            Texture2D guildPortrait = Resources.Load<Texture2D>("Messages/Portraits/Guild");
            Assert.That(guildPortrait, Is.Not.Null, "existing Guild portrait resource must remain available");
            Assert.That(identity.texture, Is.SameAs(guildPortrait),
                "Contract card identity must reuse the existing Guild contact portrait");
            Assert.That(identity.raycastTarget, Is.False,
                "sender identity must not steal the Contract card button hit-area");
            Assert.That(identity.rectTransform.sizeDelta, Is.EqualTo(new Vector2(64, 64)),
                "sender identity must stay inside the existing 64px attachment identity slot");
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

        private int CountRects(string name)
        {
            int count = 0;
            if (root == null) return count;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name && rect.gameObject.activeInHierarchy) count++;
            return count;
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
