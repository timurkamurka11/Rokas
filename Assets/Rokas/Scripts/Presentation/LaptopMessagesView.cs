using System;
using System.Collections.Generic;
using Rokas.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yarn.Unity;

namespace Rokas.Presentation
{
    // Presentation-only Messages shell. Conversation state/search/unread remain owned by MessageService.
    public sealed class LaptopMessagesView
    {
        private static readonly Color White = new Color(.94f, .97f, .98f);
        private static readonly Color Soft = new Color(.57f, .69f, .76f);
        private static readonly Color Cyan = new Color(.24f, .73f, .86f);
        private static readonly Color Red = new Color(.92f, .24f, .29f);
        private static readonly Color Panel = new Color(.018f, .045f, .071f, .94f);
        private static readonly Color PanelSoft = new Color(.025f, .061f, .09f, .91f);
        private static readonly Color Selected = new Color(.055f, .16f, .21f, .98f);
        private static readonly string[] PreferredContactOrder =
            { "kaito", "guild", "yumiko", "mika", "unknown", "merchant" };

        private readonly UiKit ui;
        private readonly RokasAssets assets;
        private readonly GameSession session;

        private RectTransform root;
        private RectTransform contactContent;
        private RectTransform conversationContent;
        private ScrollRect contactScroll;
        private ScrollRect conversationScroll;
        private TMP_InputField search;
        private TMP_FontAsset tmpFont;
        private ContactDefinition selected;
        private GameObject dialogueHost;
        private MessagesYarnController yarnController;
        private MessagesDialoguePresenter dialoguePresenter;
        private string query = string.Empty;
        private bool subscribed;
        private bool liveSubscribed;
        private bool liveTopicsOpen;
        private string reactionPickerMessageId = string.Empty;
        private bool refreshPending;
        private bool refreshPreserveScroll = true;

        public string ActiveContactId
        {
            get { return root != null && selected != null ? selected.id : string.Empty; }
        }

        public LaptopMessagesView(UiKit ui, RokasAssets assets, GameSession session)
        {
            this.ui = ui;
            this.assets = assets;
            this.session = session;
        }

        public void Build(RectTransform parent)
        {
            Hide();
            EnsureFont();

            root = Surface(parent, "MessagesRoot", 38, 92, 1668, 684, 22,
                new Color(.009f, .027f, .045f, .88f), true).rectTransform;
            Surface(root, "MessagesOuterGlow", 0, 0, 1668, 684, 22, new Color(.20f, .70f, .82f, .12f));

            RectTransform left = Surface(root, "MessagesLeftPanel", 12, 12, 492, 660, 18, Panel, true).rectTransform;
            RectTransform right = Surface(root, "MessagesRightPanel", 518, 12, 1138, 660, 18, Panel, true).rectTransform;
            ui.Box(root, "MessagesDivider", 509, 28, 1, 628, new Color(.35f, .72f, .82f, .18f));

            BuildSearch(left);
            BuildContactScroll(left);
            BuildHeader(right);
            BuildConversationScroll(right);

            session.Messages.Changed += HandleMessagesChanged;
            subscribed = true;
            session.LiveMessages.Changed += HandleLiveMessagesChanged;
            liveSubscribed = true;

            List<ContactDefinition> contacts = OrderedMatches(string.Empty);
            if (selected == null && contacts.Count > 0)
            {
                selected = contacts[0];
            }

            BuildContacts();
            RefreshActiveContact(false);
            EnsureDialogueForSelectedContact();
        }

        public void Hide()
        {
            if (subscribed)
            {
                session.Messages.Changed -= HandleMessagesChanged;
                subscribed = false;
            }
            if (liveSubscribed)
            {
                session.LiveMessages.Changed -= HandleLiveMessagesChanged;
                liveSubscribed = false;
            }
            if (search != null)
            {
                search.onValueChanged.RemoveListener(OnSearchChanged);
            }
            if (dialoguePresenter != null)
            {
                dialoguePresenter.OptionsChanged -= HandleDialogueOptionsChanged;
            }
            if (dialogueHost != null)
            {
                UnityEngine.Object.Destroy(dialogueHost);
            }
            dialogueHost = null;
            yarnController = null;
            dialoguePresenter = null;
            root = null;
            contactContent = null;
            conversationContent = null;
            contactScroll = null;
            conversationScroll = null;
            search = null;
            refreshPending = false;
            refreshPreserveScroll = true;
            liveTopicsOpen = false;
            reactionPickerMessageId = string.Empty;
        }

        public void Tick(float dt)
        {
            if (!refreshPending || root == null)
            {
                return;
            }

            bool preserveScroll = refreshPreserveScroll;
            refreshPending = false;
            refreshPreserveScroll = true;
            BuildContacts();
            RefreshActiveContact(preserveScroll);
        }

        private void BuildSearch(RectTransform parent)
        {
            TmpLabel(parent, "MessagesSearchCaption", "ПОИСК", 20, 16, 120, 24, 13, Soft, TextAlignmentOptions.MidlineLeft);
            LaptopSurface face = Surface(parent, "MessagesSearch", 18, 42, 456, 50, 13,
                new Color(.018f, .075f, .105f, .96f), true);
            ui.Box(face.transform, "MessagesSearchRule", 0, 48, 456, 2, new Color(Cyan.r, Cyan.g, Cyan.b, .40f));

            RectTransform viewport = ui.Rect(face.transform, "MessagesSearchViewport", 16, 6, 424, 38);
            viewport.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI value = TmpLabel(viewport, "MessagesSearchText", query, 0, 0, 424, 38, 18, White,
                TextAlignmentOptions.MidlineLeft);
            TextMeshProUGUI placeholder = TmpLabel(viewport, "MessagesSearchPlaceholder", "Имя или роль...", 0, 0, 424, 38, 18,
                new Color(Soft.r, Soft.g, Soft.b, .65f), TextAlignmentOptions.MidlineLeft);

            search = face.gameObject.AddComponent<TMP_InputField>();
            search.targetGraphic = face;
            search.textViewport = viewport;
            search.textComponent = value;
            search.placeholder = placeholder;
            search.lineType = TMP_InputField.LineType.SingleLine;
            search.contentType = TMP_InputField.ContentType.Standard;
            search.text = query;
            search.onValueChanged.AddListener(OnSearchChanged);
        }

        private void BuildContactScroll(RectTransform parent)
        {
            RectTransform scrollRoot = ui.Rect(parent, "MessagesContactScroll", 18, 108, 456, 534);
            contactScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            contactScroll.horizontal = false;
            contactScroll.vertical = true;
            contactScroll.movementType = ScrollRect.MovementType.Clamped;
            contactScroll.scrollSensitivity = 34;

            RectTransform viewport = ui.Rect(scrollRoot, "MessagesContactList", 0, 0, 442, 534);
            viewport.gameObject.AddComponent<RectMask2D>();
            contactContent = ui.Rect(viewport, "MessagesContactContent", 0, 0, 442, 534);
            contactContent.anchorMin = new Vector2(0, 1);
            contactContent.anchorMax = new Vector2(1, 1);
            contactContent.pivot = new Vector2(.5f, 1);
            contactScroll.viewport = viewport;
            contactScroll.content = contactContent;

            Scrollbar bar = BuildScrollbar(scrollRoot, "MessagesContactScrollbar", 446, 0, 10, 534);
            contactScroll.verticalScrollbar = bar;
            contactScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void BuildHeader(RectTransform parent)
        {
            Surface(parent, "MessagesHeader", 0, 0, 1138, 88, 18, new Color(.025f, .072f, .102f, .96f));
        }

        private void BuildConversationScroll(RectTransform parent)
        {
            RectTransform scrollRoot = ui.Rect(parent, "MessagesConversationScroll", 14, 102, 1110, 538);
            conversationScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            conversationScroll.horizontal = false;
            conversationScroll.vertical = true;
            conversationScroll.movementType = ScrollRect.MovementType.Clamped;
            conversationScroll.scrollSensitivity = 38;

            RectTransform viewport = ui.Rect(scrollRoot, "MessagesConversationViewport", 0, 0, 1094, 538);
            viewport.gameObject.AddComponent<RectMask2D>();
            conversationContent = ui.Rect(viewport, "MessagesConversationContent", 0, 0, 1094, 538);
            conversationContent.anchorMin = new Vector2(0, 1);
            conversationContent.anchorMax = new Vector2(1, 1);
            conversationContent.pivot = new Vector2(.5f, 1);
            conversationScroll.viewport = viewport;
            conversationScroll.content = conversationContent;

            Scrollbar bar = BuildScrollbar(scrollRoot, "MessagesConversationScrollbar", 1098, 0, 10, 538);
            conversationScroll.verticalScrollbar = bar;
            conversationScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void BuildContacts()
        {
            if (contactContent == null)
            {
                return;
            }
            ui.Clear(contactContent);
            List<ContactDefinition> matches = OrderedMatches(query);
            const float rowHeight = 94;
            const float gap = 8;
            float y = 0;
            Button preferred = null;

            for (int index = 0; index < matches.Count; index++)
            {
                ContactDefinition contact = matches[index];
                bool isSelected = selected != null && string.Equals(selected.id, contact.id, StringComparison.Ordinal);
                ConversationState conversation = session.Messages.GetConversation(contact.id);
                MessageEntry last = LastEntry(conversation);

                LaptopSurface ring = Surface(contactContent, "MessagesContactRing_" + contact.id, 0, y, 430, rowHeight, 14,
                    isSelected ? new Color(Cyan.r, Cyan.g, Cyan.b, .55f) : new Color(.20f, .42f, .49f, .18f), true);
                LaptopSurface face = Surface(ring.transform, "MessagesContactFace_" + contact.id, 2, 2, 426, rowHeight - 4, 12,
                    isSelected ? Selected : PanelSoft, true);
                Button button = ring.gameObject.AddComponent<Button>();
                StyleButton(button, face);
                button.name = "MessagesContact_" + contact.id;
                button.onClick.AddListener(() => SelectContact(contact));
                if (isSelected)
                {
                    preferred = button;
                }

                Texture2D portrait = Resources.Load<Texture2D>(contact.portraitResource);
                if (portrait != null)
                {
                    RawImage art = ui.Art(face.transform, "Portrait", portrait, 8, 8, 74, 74);
                    art.color = contact.online ? Color.white : new Color(.65f, .69f, .72f, .92f);
                }
                else
                {
                    Surface(face.transform, "PortraitFallback", 8, 8, 74, 74, 12, new Color(.10f, .18f, .22f));
                }

                TmpLabel(face.transform, "Name", contact.displayName, 96, 11, 212, 28, 20, White, TextAlignmentOptions.MidlineLeft);
                TmpLabel(face.transform, "Preview", last != null ? Compact(last.text, 37) : contact.role,
                    96, 48, 242, 27, 14, Soft, TextAlignmentOptions.MidlineLeft);
                TmpLabel(face.transform, "Time", last != null ? "СЕЙЧАС" : string.Empty,
                    326, 10, 78, 22, 11, Soft, TextAlignmentOptions.MidlineRight);

                Surface(face.transform, "Status", 391, 66, 9, 9, 5,
                    contact.online ? new Color(.26f, .85f, .66f) : new Color(.42f, .48f, .52f));
                if (conversation != null && conversation.unreadCount > 0)
                {
                    Surface(face.transform, "UnreadBadge", 356, 55, 28, 24, 12, Red);
                    TmpLabel(face.transform, "UnreadCount", Mathf.Min(conversation.unreadCount, 99).ToString(),
                        356, 55, 28, 24, 12, White, TextAlignmentOptions.Center);
                }
                y += rowHeight + gap;
            }

            float contentHeight = Mathf.Max(534, y > 0 ? y - gap : 534);
            contactContent.sizeDelta = new Vector2(0, contentHeight);
            if (contactScroll != null)
            {
                contactScroll.verticalNormalizedPosition = 1;
            }
            if (preferred != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(preferred.gameObject);
            }
        }

        private void SelectContact(ContactDefinition contact)
        {
            if (contact == null)
            {
                return;
            }

            bool wasSelected = selected != null && string.Equals(selected.id, contact.id, StringComparison.Ordinal);
            selected = contact;
            if (!wasSelected)
            {
                liveTopicsOpen = false;
                reactionPickerMessageId = string.Empty;
            }
            if (string.Equals(contact.id, "guild", StringComparison.Ordinal))
            {
                session.EnsureGuildContractOffer();
            }
            session.Messages.OpenConversation(contact.id);
            if (!wasSelected)
            {
                QueueRefresh(false);
            }
            EnsureDialogueForSelectedContact();
        }

        private void RefreshActiveContact(bool preserveScroll)
        {
            if (selected == null || root == null)
            {
                return;
            }
            RectTransform right = FindChild(root, "MessagesRightPanel");
            if (right == null)
            {
                return;
            }

            RectTransform header = FindChild(right, "MessagesHeader");
            if (header != null)
            {
                ui.Clear(header);
                Texture2D portrait = Resources.Load<Texture2D>(selected.portraitResource);
                if (portrait != null)
                {
                    ui.Art(header, "MessagesHeaderPortrait", portrait, 22, 12, 62, 62);
                }
                TmpLabel(header, "MessagesHeaderName", selected.displayName, 102, 12, 440, 31, 25, White,
                    TextAlignmentOptions.MidlineLeft);
                TmpLabel(header, "MessagesHeaderRole", selected.role, 102, 43, 440, 24, 14, Soft,
                    TextAlignmentOptions.MidlineLeft);
                Surface(header, "MessagesHeaderStatusDot", 993, 29, 10, 10, 5,
                    selected.online ? new Color(.26f, .85f, .66f) : new Color(.42f, .48f, .52f));
                TmpLabel(header, "MessagesHeaderStatus", session.LiveMessages.GetPresenceText(selected.id),
                    908, 18, 198, 34, 13, Soft, TextAlignmentOptions.MidlineRight);
            }

            BuildConversation(preserveScroll);
        }

        private void BuildConversation(bool preserveScroll)
        {
            if (conversationContent == null)
            {
                return;
            }
            float previous = conversationScroll != null ? conversationScroll.verticalNormalizedPosition : 0;
            bool wasBottom = !preserveScroll || previous <= .06f;
            ui.Clear(conversationContent);

            ConversationState conversation = session.Messages.GetConversation(selected.id);
            float y = 10;
            if (conversation == null || conversation.entries == null || conversation.entries.Count == 0)
            {
                TmpLabel(conversationContent, "MessagesEmptyTitle", "Нет сообщений", 190, 104, 714, 48, 28, White,
                    TextAlignmentOptions.Center);
                TmpLabel(conversationContent, "MessagesEmptyNote", "Выберите тему, чтобы начать разговор.",
                    190, 152, 714, 58, 16, Soft, TextAlignmentOptions.Center);
                y = 244;
                BuildLiveControls(ref y);
                BuildDialogueChoices(ref y);
                conversationContent.sizeDelta = new Vector2(0, Mathf.Max(538, y + 10));
                return;
            }

            TmpLabel(conversationContent, "MessagesDateSeparator", "СЕГОДНЯ", 410, y, 274, 28, 12, Soft,
                TextAlignmentOptions.Center);
            y += 38;

            for (int index = 0; index < conversation.entries.Count; index++)
            {
                MessageEntry entry = conversation.entries[index];
                if (entry == null)
                {
                    continue;
                }
                float width = entry.outgoing ? 650 : 720;
                float x = entry.outgoing ? 1094 - width - 26 : 26;
                float height = EstimateBubbleHeight(entry.text);
                Color bubbleColor = entry.outgoing ? new Color(.055f, .22f, .27f, .97f) : new Color(.055f, .09f, .125f, .97f);
                LaptopSurface bubble = Surface(conversationContent,
                    (entry.outgoing ? "OutgoingBubble_" : "IncomingBubble_") + entry.sequence,
                    x, y, width, height, 17, bubbleColor);
                TmpLabel(bubble.transform, "MessageText", entry.text ?? string.Empty, 20, 12, width - 40, height - 34,
                    17, White, TextAlignmentOptions.TopLeft);
                TmpLabel(bubble.transform, "MessageSequence", entry.sequence > 0 ? "#" + entry.sequence.ToString("000") : string.Empty,
                    width - 108, height - 23, 88, 17, 10, Soft, TextAlignmentOptions.MidlineRight);
                y += height + 14;

                if (entry.attachment != null &&
                    (entry.attachment.kind == MessageAttachmentKind.Coordinates ||
                     entry.attachment.kind == MessageAttachmentKind.Contract ||
                     entry.attachment.kind == MessageAttachmentKind.FoodGift))
                {
                    y = BuildActionAttachment(entry, y);
                }
                BuildMessageReactions(entry, ref y);
            }

            BuildLiveControls(ref y);
            BuildDialogueChoices(ref y);
            conversationContent.sizeDelta = new Vector2(0, Mathf.Max(538, y + 10));
            Canvas.ForceUpdateCanvases();
            if (conversationScroll != null)
            {
                conversationScroll.verticalNormalizedPosition = wasBottom ? 0 : previous;
            }
        }

        private float BuildActionAttachment(MessageEntry entry, float y)
        {
            MessageAttachment attachment = entry != null ? entry.attachment : null;
            if (attachment == null || string.IsNullOrEmpty(attachment.id) || string.IsNullOrEmpty(entry.messageId))
            {
                return y;
            }

            const float width = 720;
            const float height = 112;
            LaptopSurface card = Surface(conversationContent, "MessagesAttachment_" + attachment.id,
                26, y, width, height, 16, new Color(.025f, .12f, .16f, .98f), true);
            Button button = card.gameObject.AddComponent<Button>();
            button.name = "MessagesAttachment_" + attachment.id;
            StyleButton(button, card);
            button.interactable = !attachment.opened;
            string contactId = selected.id;
            string messageId = entry.messageId;
            button.onClick.AddListener(() => ActivateAttachment(contactId, messageId));
            ui.Box(card.transform, "AttachmentAccent", 0, 0, 5, height, new Color(Cyan.r, Cyan.g, Cyan.b, .82f));

            if (attachment.kind == MessageAttachmentKind.Coordinates)
            {
                Texture2D icon = Resources.Load<Texture2D>("Messages/Icons/CoordinateAttachment");
                if (icon != null)
                {
                    ui.Art(card.transform, "CoordinateAttachmentIcon", icon, 18, 24, 64, 64);
                }
            }
            else if ((attachment.kind == MessageAttachmentKind.Contract ||
                      attachment.kind == MessageAttachmentKind.FoodGift) &&
                     selected != null && !string.IsNullOrEmpty(selected.portraitResource))
            {
                Texture2D senderIdentity = Resources.Load<Texture2D>(selected.portraitResource);
                if (senderIdentity != null)
                {
                    ui.Art(card.transform, "AttachmentSenderIdentity", senderIdentity, 18, 24, 64, 64);
                }
            }

            float textWidth = attachment.kind == MessageAttachmentKind.FoodGift ? 420 : 586;
            TmpLabel(card.transform, "AttachmentTitle", attachment.title ?? string.Empty,
                102, 16, textWidth, 32, 19, White, TextAlignmentOptions.MidlineLeft);
            TmpLabel(card.transform, "AttachmentBody", attachment.body ?? string.Empty,
                102, 50, textWidth, 38, 16, Soft, TextAlignmentOptions.MidlineLeft);
            if (attachment.kind == MessageAttachmentKind.FoodGift)
            {
                TmpLabel(card.transform, "AttachmentAction", attachment.opened ? "ПОЛУЧЕНО" : "ЗАБРАТЬ",
                    548, 30, 140, 52, 14, attachment.opened ? Soft : White, TextAlignmentOptions.Center);
            }
            return y + height + 14;
        }

        private void ActivateAttachment(string contactId, string messageId)
        {
            if (string.IsNullOrEmpty(contactId) || string.IsNullOrEmpty(messageId))
            {
                return;
            }
            session.Messages.ActivateAttachment(contactId, messageId);
        }

        private void BuildMessageReactions(MessageEntry entry, ref float y)
        {
            if (entry == null || string.IsNullOrEmpty(entry.messageId) || selected == null) return;
            if (entry.outgoing)
            {
                if (!string.IsNullOrEmpty(entry.npcReactionId))
                {
                    Texture2D npcTexture = Resources.Load<Texture2D>("Messages/Reactions/" + entry.npcReactionId);
                    LaptopSurface npcChip = Surface(conversationContent, "MessagesNpcReactionChip_" + entry.messageId,
                        1094 - 86, y - 8, 56, 42, 18, new Color(.025f, .09f, .11f, .96f));
                    if (npcTexture != null)
                    {
                        RawImage art = ui.Art(npcChip.transform, "Sticker", npcTexture, 9, 2, 38, 38);
                        art.raycastTarget = false;
                    }
                    y += 38;
                }
                return;
            }

            string messageId = entry.messageId;
            float chipX = 26;
            LaptopSurface openerFace = Surface(conversationContent, "MessagesReactionOpenFace_" + messageId,
                chipX, y - 5, 40, 30, 15, new Color(.03f, .10f, .13f, .96f), true);
            Button opener = openerFace.gameObject.AddComponent<Button>();
            opener.name = "MessagesReactionOpen_" + messageId;
            StyleButton(opener, openerFace);
            opener.onClick.AddListener(() =>
            {
                reactionPickerMessageId = string.Equals(reactionPickerMessageId, messageId, StringComparison.Ordinal)
                    ? string.Empty : messageId;
                RefreshActiveContact(true);
            });
            TmpLabel(openerFace.transform, "Icon", "+", 0, 0, 40, 30, 17, Soft, TextAlignmentOptions.Center);

            if (!string.IsNullOrEmpty(entry.reactionId))
            {
                Texture2D selectedTexture = Resources.Load<Texture2D>("Messages/Reactions/" + entry.reactionId);
                LaptopSurface chip = Surface(conversationContent, "MessagesReactionChip_" + messageId,
                    74, y - 8, 54, 36, 18, new Color(.035f, .13f, .16f, .98f));
                if (selectedTexture != null)
                {
                    RawImage art = ui.Art(chip.transform, "Sticker", selectedTexture, 9, 0, 36, 36);
                    art.raycastTarget = false;
                }
            }
            y += 32;

            if (!string.Equals(reactionPickerMessageId, messageId, StringComparison.Ordinal)) return;
            List<LiveReactionOption> reactions = session.LiveMessages.GetReactionOptions(selected.id);
            if (reactions == null || reactions.Count == 0) return;

            LaptopSurface picker = Surface(conversationContent, "MessagesReactionPicker_" + messageId,
                26, y, 342, 126, 16, new Color(.016f, .065f, .085f, .99f), true);
            for (int index = 0; index < reactions.Count; index++)
            {
                LiveReactionOption option = reactions[index];
                int column = index % 5;
                int row = index / 5;
                string reactionId = option.Id;
                LaptopSurface pickFace = Surface(picker.transform, "PickFace_" + reactionId,
                    9 + column * 65, 8 + row * 58, 54, 52, 12, new Color(.035f, .11f, .14f, .98f), true);
                Button pick = pickFace.gameObject.AddComponent<Button>();
                pick.name = "MessagesReactionPick_" + messageId + "_" + reactionId;
                StyleButton(pick, pickFace);
                pick.onClick.AddListener(() =>
                {
                    reactionPickerMessageId = string.Empty;
                    session.LiveMessages.SetReaction(selected.id, messageId, reactionId);
                    RefreshActiveContact(true);
                });
                Texture2D texture = Resources.Load<Texture2D>("Messages/Reactions/" + reactionId);
                if (texture != null)
                {
                    RawImage art = ui.Art(pickFace.transform, "Sticker", texture, 5, 4, 44, 44);
                    art.raycastTarget = false;
                }
            }
            if (!string.IsNullOrEmpty(entry.reactionId))
            {
                LaptopSurface removeFace = Surface(picker.transform, "RemoveFace", 276, 93, 56, 25, 10,
                    new Color(.18f, .055f, .07f, .96f), true);
                Button remove = removeFace.gameObject.AddComponent<Button>();
                remove.name = "MessagesReactionRemove_" + messageId;
                StyleButton(remove, removeFace);
                remove.onClick.AddListener(() =>
                {
                    reactionPickerMessageId = string.Empty;
                    session.LiveMessages.SetReaction(selected.id, messageId, string.Empty);
                    RefreshActiveContact(true);
                });
                TmpLabel(removeFace.transform, "Label", "×", 0, 0, 56, 25, 15, White, TextAlignmentOptions.Center);
            }
            y += 134;
        }

        private void BuildLiveControls(ref float y)
        {
            if (selected == null) return;
            string contactId = selected.id;

            if (session.LiveMessages.IsTyping(contactId))
            {
                y += 4;
                LaptopSurface typing = Surface(conversationContent, "MessagesTypingIndicator", 26, y, 312, 42, 15,
                    new Color(.035f, .10f, .13f, .96f));
                string typingText = contactId == "guild" ? "Обработка запроса..." : "печатает...";
                TmpLabel(typing.transform, "Label", typingText, 16, 4, 280, 34, 14, Soft,
                    TextAlignmentOptions.MidlineLeft);
                y += 50;
            }

            List<LiveReplyOption> replies = session.LiveMessages.GetReplyOptions(contactId);
            if (replies != null && replies.Count > 0)
            {
                y += 4;
                TmpLabel(conversationContent, "MessagesLiveChoicesCaption", "ВАШ ОТВЕТ", 26, y, 1042, 24, 12, Soft,
                    TextAlignmentOptions.MidlineLeft);
                y += 30;
                for (int index = 0; index < replies.Count; index++)
                {
                    LiveReplyOption option = replies[index];
                    string replyId = option.Id;
                    LaptopSurface face = Surface(conversationContent, "MessagesLiveChoiceFace_" + replyId,
                        26, y, 1042, 56, 14, new Color(.035f, .14f, .18f, .98f), true);
                    Button button = face.gameObject.AddComponent<Button>();
                    button.name = "MessagesLiveChoice_" + replyId;
                    StyleButton(button, face);
                    button.onClick.AddListener(() =>
                    {
                        if (session.LiveMessages.SubmitReply(contactId, replyId))
                        {
                            liveTopicsOpen = false;
                            RefreshActiveContact(false);
                        }
                    });
                    TmpLabel(face.transform, "Label", option.Text, 18, 7, 1006, 42, 16, White,
                        TextAlignmentOptions.MidlineLeft);
                    y += 64;
                }
                return;
            }

            List<LiveTopicOption> topics = session.LiveMessages.GetTopics(contactId);
            if (topics == null || topics.Count == 0) return;
            y += 6;
            if (!liveTopicsOpen)
            {
                string launcherText = contactId == "guild" ? "Отправить запрос" :
                    contactId == "kaito" ? "Написать Кайто" : "Написать Юмико";
                LaptopSurface face = Surface(conversationContent, "MessagesLiveLauncherFace_" + contactId,
                    26, y, 1042, 58, 15, new Color(.035f, .16f, .19f, .98f), true);
                Button launcher = face.gameObject.AddComponent<Button>();
                launcher.name = "MessagesLiveLauncher_" + contactId;
                StyleButton(launcher, face);
                launcher.onClick.AddListener(() =>
                {
                    liveTopicsOpen = true;
                    RefreshActiveContact(false);
                });
                TmpLabel(face.transform, "Label", launcherText, 18, 8, 1006, 42, 16, White,
                    TextAlignmentOptions.Center);
                y += 66;
                return;
            }

            TmpLabel(conversationContent, "MessagesLiveTopicsCaption", contactId == "guild" ? "ЗАПРОСЫ" : "ТЕМЫ",
                26, y, 1042, 24, 12, Soft, TextAlignmentOptions.MidlineLeft);
            y += 30;
            for (int index = 0; index < topics.Count; index++)
            {
                LiveTopicOption option = topics[index];
                string topicId = option.Id;
                LaptopSurface face = Surface(conversationContent, "MessagesLiveTopicFace_" + topicId,
                    26, y, 1042, 54, 14, new Color(.028f, .115f, .15f, .98f), true);
                Button button = face.gameObject.AddComponent<Button>();
                button.name = "MessagesLiveTopic_" + topicId;
                StyleButton(button, face);
                button.onClick.AddListener(() =>
                {
                    liveTopicsOpen = false;
                    if (session.LiveMessages.StartTopic(contactId, topicId))
                        RefreshActiveContact(false);
                });
                TmpLabel(face.transform, "Label", option.Text, 18, 6, 1006, 42, 15, White,
                    TextAlignmentOptions.MidlineLeft);
                y += 62;
            }
        }

        private void BuildDialogueChoices(ref float y)
        {
            if (selected == null || !string.Equals(selected.id, "kaito", StringComparison.Ordinal) || dialoguePresenter == null)
            {
                return;
            }
            IReadOnlyList<MessagesReplyOption> options = dialoguePresenter.CurrentOptions;
            if (options == null || options.Count == 0)
            {
                return;
            }

            y += 4;
            TmpLabel(conversationContent, "MessagesChoicesCaption", "ВЫБЕРИТЕ ОТВЕТ", 26, y, 1042, 24, 12, Soft,
                TextAlignmentOptions.MidlineLeft);
            y += 30;
            for (int index = 0; index < options.Count; index++)
            {
                MessagesReplyOption option = options[index];
                string choiceId = option.choiceId;
                LaptopSurface face = Surface(conversationContent, "MessagesChoiceFace_" + choiceId,
                    26, y, 1042, 58, 14, new Color(.035f, .14f, .18f, .98f), true);
                Button button = face.gameObject.AddComponent<Button>();
                button.name = "MessagesChoice_" + choiceId;
                StyleButton(button, face);
                button.onClick.AddListener(() => SubmitDialogueChoice(button, choiceId));
                TmpLabel(face.transform, "ChoiceText", option.text, 18, 8, 1006, 42, 16, White,
                    TextAlignmentOptions.MidlineLeft);
                y += 66;
            }
        }

        private void EnsureDialogueForSelectedContact()
        {
            if (root == null || selected == null || !string.Equals(selected.id, "kaito", StringComparison.Ordinal) ||
                yarnController != null || session.Messages.IsDialogueCompleted("kaito", "Kaito_Start"))
            {
                return;
            }

            YarnProject project = Resources.Load<YarnProject>("Messages/Dialogue/ROKASMessages");
            if (project == null)
            {
                throw new InvalidOperationException(
                    "ROKAS Messages requires Assets/Rokas/Resources/Messages/Dialogue/ROKASMessages.yarnproject.");
            }

            dialogueHost = new GameObject("MessagesKaitoDialogue");
            dialogueHost.transform.SetParent(root, false);
            DialogueRunner runner = dialogueHost.AddComponent<DialogueRunner>();
            dialoguePresenter = dialogueHost.AddComponent<MessagesDialoguePresenter>();
            yarnController = dialogueHost.AddComponent<MessagesYarnController>();
            dialoguePresenter.OptionsChanged += HandleDialogueOptionsChanged;
            yarnController.Configure(session.Messages, project, runner, dialoguePresenter, "kaito");
            _ = yarnController.StartDialogue("Kaito_Start");
        }

        private void SubmitDialogueChoice(Button button, string choiceId)
        {
            if (yarnController == null || button == null || string.IsNullOrEmpty(choiceId))
            {
                return;
            }
            if (!yarnController.SubmitChoice(choiceId))
            {
                return;
            }
            button.interactable = false;
            button.name = "MessagesChoiceConsumed_" + choiceId;
        }

        private void HandleDialogueOptionsChanged()
        {
            if (root != null && selected != null && string.Equals(selected.id, "kaito", StringComparison.Ordinal))
            {
                QueueRefresh(true);
            }
        }

        private void HandleLiveMessagesChanged()
        {
            if (root != null) QueueRefresh(true);
        }

        private void HandleMessagesChanged()
        {
            if (root == null)
            {
                return;
            }
            if (selected != null)
            {
                ConversationState active = session.Messages.GetConversation(selected.id);
                if (active != null && active.unreadCount > 0)
                {
                    session.Messages.OpenConversation(selected.id);
                }
            }
            QueueRefresh(true);
        }

        private void QueueRefresh(bool preserveScroll)
        {
            if (root == null)
            {
                return;
            }
            if (!refreshPending)
            {
                refreshPreserveScroll = preserveScroll;
            }
            else
            {
                refreshPreserveScroll &= preserveScroll;
            }
            refreshPending = true;
        }

        private void OnSearchChanged(string value)
        {
            query = value ?? string.Empty;
            BuildContacts();
        }

        private List<ContactDefinition> OrderedMatches(string value)
        {
            List<ContactDefinition> matches = session.Messages.SearchContacts(value);
            List<ContactDefinition> ordered = new List<ContactDefinition>(matches.Count);
            for (int order = 0; order < PreferredContactOrder.Length; order++)
            {
                string id = PreferredContactOrder[order];
                for (int index = 0; index < matches.Count; index++)
                {
                    if (string.Equals(matches[index].id, id, StringComparison.Ordinal))
                    {
                        ordered.Add(matches[index]);
                        break;
                    }
                }
            }
            return ordered;
        }

        private static MessageEntry LastEntry(ConversationState conversation)
        {
            if (conversation == null || conversation.entries == null || conversation.entries.Count == 0)
            {
                return null;
            }
            return conversation.entries[conversation.entries.Count - 1];
        }

        private void EnsureFont()
        {
            if (tmpFont != null)
            {
                return;
            }
            tmpFont = Resources.Load<TMP_FontAsset>("RokasSans TMP");
            if (tmpFont == null)
            {
                throw new InvalidOperationException(
                    "ROKAS Messages requires the persistent TMP font asset at Assets/Rokas/Resources/RokasSans TMP.asset.");
            }
        }

        private TextMeshProUGUI TmpLabel(Transform parent, string name, string value, float x, float y, float w, float h,
            float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = ui.Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = tmpFont;
            text.fontSize = size;
            text.color = color;
            text.text = value ?? string.Empty;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private LaptopSurface Surface(Transform parent, string name, float x, float y, float w, float h,
            float radius, Color color, bool blocks = false)
        {
            GameObject go = ui.Rect(parent, name, x, y, w, h).gameObject;
            if (!go.TryGetComponent<CanvasRenderer>(out _))
            {
                go.AddComponent<CanvasRenderer>();
            }
            LaptopSurface surface = go.AddComponent<LaptopSurface>();
            surface.Radius = radius;
            surface.color = color;
            surface.raycastTarget = blocks;
            return surface;
        }

        private static void StyleButton(Button button, Graphic target)
        {
            button.targetGraphic = target;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.16f, 1.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.78f, .88f, .92f);
            colors.fadeDuration = .10f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        }

        private Scrollbar BuildScrollbar(Transform parent, string name, float x, float y, float w, float h)
        {
            Image track = ui.Box(parent, name, x, y, w, h, new Color(.08f, .15f, .18f, .58f), true);
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            RectTransform handle = ui.Rect(track.transform, "Handle", 1, 0, w - 2, 92);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(Cyan.r, Cyan.g, Cyan.b, .52f);
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle;
            return scrollbar;
        }

        private static RectTransform FindChild(Transform parent, string name)
        {
            foreach (RectTransform rect in parent.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name == name)
                {
                    return rect;
                }
            }
            return null;
        }

        private static float EstimateBubbleHeight(string text)
        {
            int length = string.IsNullOrEmpty(text) ? 0 : text.Length;
            int rows = Mathf.Max(1, Mathf.CeilToInt(length / 52f));
            return Mathf.Clamp(50 + rows * 22, 72, 150);
        }

        private static string Compact(string value, int max)
        {
            string text = (value ?? string.Empty).Replace('\n', ' ').Trim();
            if (text.Length <= max)
            {
                return text;
            }
            return text.Substring(0, Mathf.Max(0, max - 1)).TrimEnd() + "…";
        }
    }
}
