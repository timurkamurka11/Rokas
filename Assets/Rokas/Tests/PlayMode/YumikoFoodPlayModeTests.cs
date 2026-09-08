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
    public sealed class YumikoFoodPlayModeTests
    {
        private const string GiftEventId = "yumiko-gift:kisaragi-green-tea-001";
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator FoodGiftCardUsesYumikoIdentityClaimsOnceAndPersists()
        {
            Initialize("rokas-yumiko-gift-");
            yield return null;

            RokasBootstrap boot = Bootstrap();
            Assert.That(boot.Session.AcceptContract(), Is.True, "real acceptance must deliver Yumiko context");
            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_yumiko");
            for (int frame = 0; frame < 4; frame++) yield return null;

            ConversationState yumiko = boot.Session.Messages.GetConversation("yumiko");
            MessageEntry gift = FindEvent(yumiko, GiftEventId);
            Assert.That(gift, Is.Not.Null);
            Assert.That(gift.attachment, Is.Not.Null);
            Assert.That(gift.attachment.kind, Is.EqualTo(MessageAttachmentKind.FoodGift));
            Button card = FindButton("MessagesAttachment_" + gift.attachment.id);
            Assert.That(card, Is.Not.Null, "FoodGift attachment must render as an actionable Messages card");
            Assert.That(card.IsInteractable(), Is.True);
            AssertSenderIdentity(card, "Messages/Portraits/Yumiko");
            Assert.That(FindTextUnder(card.transform, "AttachmentAction"), Is.EqualTo("ЗАБРАТЬ"));

            Press(card.name);
            for (int frame = 0; frame < 4; frame++) yield return null;

            Assert.That(boot.Session.State.storedFoodId, Is.EqualTo(FoodService.GreenTeaId));
            Assert.That(boot.Session.State.storedFoodCount, Is.EqualTo(1), "claim must grant exactly one real stored food item");
            Assert.That(gift.attachment.opened, Is.True);
            Button consumed = FindButton("MessagesAttachment_" + gift.attachment.id);
            Assert.That(consumed, Is.Not.Null);
            Assert.That(consumed.IsInteractable(), Is.False, "claimed gift card must become consumed");
            Assert.That(FindTextUnder(consumed.transform, "AttachmentAction"), Is.EqualTo("ПОЛУЧЕНО"));
            AssertSenderIdentity(consumed, "Messages/Portraits/Yumiko");

            boot.SaveNow();
            yield return DestroyAndRecreate();
            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_yumiko");
            for (int frame = 0; frame < 4; frame++) yield return null;

            RokasBootstrap restoredBoot = Bootstrap();
            ConversationState restoredYumiko = restoredBoot.Session.Messages.GetConversation("yumiko");
            MessageEntry restoredGift = FindEvent(restoredYumiko, GiftEventId);
            Assert.That(restoredGift.attachment.opened, Is.True, "Stop/Play must keep gift consumed");
            Assert.That(restoredBoot.Session.State.storedFoodId, Is.EqualTo(FoodService.GreenTeaId));
            Assert.That(restoredBoot.Session.State.storedFoodCount, Is.EqualTo(1));
            Button restoredCard = FindButton("MessagesAttachment_" + restoredGift.attachment.id);
            Assert.That(restoredCard.IsInteractable(), Is.False);
            Assert.That(FindTextUnder(restoredCard.transform, "AttachmentAction"), Is.EqualTo("ПОЛУЧЕНО"));
            AssertSenderIdentity(restoredCard, "Messages/Portraits/Yumiko");
            Assert.That(restoredBoot.Session.Messages.ActivateAttachment("yumiko", restoredGift.messageId).Status,
                Is.EqualTo(MessageAttachmentActionStatus.AlreadyActive));
            Assert.That(restoredBoot.Session.State.storedFoodCount, Is.EqualTo(1), "reload repeat claim must not grant a second item");
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "historical claimed gift must not become a fresh notification after reload");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PaidTeaReactionCreatesUnreadNotificationButReloadDoesNotReplayIt()
        {
            Initialize("rokas-yumiko-notify-");
            yield return null;

            RokasBootstrap boot = Bootstrap();
            Assert.That(boot.Session.PrepareFood(FoodService.GreenTeaId), Is.True);
            yield return null;

            ConversationState yumiko = boot.Session.Messages.GetConversation("yumiko");
            Assert.That(yumiko, Is.Not.Null);
            Assert.That(yumiko.entries.Count, Is.EqualTo(1));
            Assert.That(yumiko.entries[0].eventId, Is.EqualTo("yumiko-food-first:" + FoodService.GreenTeaId));
            Assert.That(yumiko.unreadCount, Is.EqualTo(1));
            Assert.That(CountRects("MessagesNotification"), Is.EqualTo(1),
                "inactive Yumiko must use the existing compact notification surface");

            boot.SaveNow();
            yield return DestroyAndRecreate();
            for (int frame = 0; frame < 4; frame++) yield return null;

            ConversationState restored = Bootstrap().Session.Messages.GetConversation("yumiko");
            Assert.That(restored.entries.Count, Is.EqualTo(1), "reload must preserve exactly one historical purchase reaction");
            Assert.That(restored.unreadCount, Is.EqualTo(1), "historical unread state must survive without redelivery");
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "bootstrap must not turn historical unread into a fresh popup");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ActiveYumikoConversationConsumesPaidTeaUnreadWithoutPopup()
        {
            Initialize("rokas-yumiko-active-");
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_yumiko");
            yield return null;
            Assert.That(FindRect("MessagesNotification"), Is.Null);

            RokasBootstrap boot = Bootstrap();
            Assert.That(boot.Session.PrepareFood(FoodService.GreenTeaId), Is.True);
            for (int frame = 0; frame < 4; frame++) yield return null;

            ConversationState yumiko = boot.Session.Messages.GetConversation("yumiko");
            Assert.That(yumiko, Is.Not.Null);
            Assert.That(yumiko.entries.Count, Is.EqualTo(1));
            Assert.That(yumiko.unreadCount, Is.EqualTo(0),
                "currently open Yumiko conversation must consume its own incoming unread");
            Assert.That(boot.Session.Messages.TotalUnread, Is.EqualTo(0));
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "active Yumiko arrival must not create a redundant popup");
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize(string prefix)
        {
            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("YumikoFoodFixture");
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
            root = new GameObject("YumikoFoodFixtureReloaded");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
            yield return null;
        }

        private static MessageEntry FindEvent(ConversationState conversation, string eventId)
        {
            if (conversation == null || conversation.entries == null) return null;
            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry != null && entry.eventId == eventId) return entry;
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

        private static string FindTextUnder(Transform parent, string childName)
        {
            if (parent == null) return null;
            foreach (TextMeshProUGUI text in parent.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.name == childName) return text.text;
            return null;
        }

        private static void AssertSenderIdentity(Button card, string resource)
        {
            RawImage identity = null;
            foreach (RawImage image in card.GetComponentsInChildren<RawImage>(true))
            {
                if (image.name == "AttachmentSenderIdentity")
                {
                    identity = image;
                    break;
                }
            }
            Assert.That(identity, Is.Not.Null, "FoodGift card must reuse generic sender identity");
            Texture2D portrait = Resources.Load<Texture2D>(resource);
            Assert.That(portrait, Is.Not.Null);
            Assert.That(identity.texture, Is.SameAs(portrait), "FoodGift card must reuse the existing Yumiko portrait");
            Assert.That(identity.raycastTarget, Is.False, "sender identity must not steal card hit-area");
            Assert.That(identity.rectTransform.sizeDelta, Is.EqualTo(new Vector2(64, 64)));
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
