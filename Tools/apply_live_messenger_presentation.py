from pathlib import Path


def replace_once(path, old, new, label):
    file = Path(path)
    text = file.read_text(encoding="utf-8")
    if old not in text:
        raise SystemExit(f"Missing patch anchor: {label} in {path}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")


messages = "Assets/Rokas/Scripts/Presentation/LaptopMessagesView.cs"
replace_once(messages,
'''        private bool subscribed;\n        private bool refreshPending;\n''',
'''        private bool subscribed;\n        private bool liveSubscribed;\n        private bool liveTopicsOpen;\n        private bool refreshPending;\n''', "messages live fields")

replace_once(messages,
'''        public LaptopMessagesView(UiKit ui, RokasAssets assets, GameSession session)\n''',
'''        public string ActiveContactId\n        {\n            get { return root != null && selected != null ? selected.id : string.Empty; }\n        }\n\n        public LaptopMessagesView(UiKit ui, RokasAssets assets, GameSession session)\n''', "messages active contact property")

replace_once(messages,
'''            session.Messages.Changed += HandleMessagesChanged;\n            subscribed = true;\n''',
'''            session.Messages.Changed += HandleMessagesChanged;\n            subscribed = true;\n            session.LiveMessages.Changed += HandleLiveMessagesChanged;\n            liveSubscribed = true;\n''', "messages live subscription")

replace_once(messages,
'''            if (search != null)\n            {\n                search.onValueChanged.RemoveListener(OnSearchChanged);\n            }\n''',
'''            if (liveSubscribed)\n            {\n                session.LiveMessages.Changed -= HandleLiveMessagesChanged;\n                liveSubscribed = false;\n            }\n            if (search != null)\n            {\n                search.onValueChanged.RemoveListener(OnSearchChanged);\n            }\n''', "messages live unsubscription")

replace_once(messages,
'''            refreshPending = false;\n            refreshPreserveScroll = true;\n''',
'''            refreshPending = false;\n            refreshPreserveScroll = true;\n            liveTopicsOpen = false;\n''', "messages hide live state")

replace_once(messages,
'''            selected = contact;\n            if (string.Equals(contact.id, "guild", StringComparison.Ordinal))\n''',
'''            selected = contact;\n            if (!wasSelected) liveTopicsOpen = false;\n            if (string.Equals(contact.id, "guild", StringComparison.Ordinal))\n''', "messages contact live reset")

replace_once(messages,
'''                TmpLabel(header, "MessagesHeaderStatus", selected.online ? "В сети" : "Не в сети",\n                    1012, 18, 94, 34, 13, Soft, TextAlignmentOptions.MidlineLeft);\n''',
'''                TmpLabel(header, "MessagesHeaderStatus", session.LiveMessages.GetPresenceText(selected.id),\n                    908, 18, 198, 34, 13, Soft, TextAlignmentOptions.MidlineRight);\n''', "messages live presence")

replace_once(messages,
'''            ConversationState conversation = session.Messages.GetConversation(selected.id);\n            if (conversation == null || conversation.entries == null || conversation.entries.Count == 0)\n            {\n                TmpLabel(conversationContent, "MessagesEmptyTitle", "Нет сообщений", 190, 180, 714, 48, 28, White,\n                    TextAlignmentOptions.Center);\n                TmpLabel(conversationContent, "MessagesEmptyNote", "История появится здесь после первого события или ответа.",\n                    190, 228, 714, 58, 16, Soft, TextAlignmentOptions.Center);\n                conversationContent.sizeDelta = new Vector2(0, 538);\n                return;\n            }\n\n            float y = 10;\n''',
'''            ConversationState conversation = session.Messages.GetConversation(selected.id);\n            float y = 10;\n            if (conversation == null || conversation.entries == null || conversation.entries.Count == 0)\n            {\n                TmpLabel(conversationContent, "MessagesEmptyTitle", "Нет сообщений", 190, 104, 714, 48, 28, White,\n                    TextAlignmentOptions.Center);\n                TmpLabel(conversationContent, "MessagesEmptyNote", "Выберите тему, чтобы начать разговор.",\n                    190, 152, 714, 58, 16, Soft, TextAlignmentOptions.Center);\n                y = 244;\n                BuildLiveControls(ref y);\n                BuildDialogueChoices(ref y);\n                conversationContent.sizeDelta = new Vector2(0, Mathf.Max(538, y + 10));\n                return;\n            }\n\n''', "messages empty live controls")

replace_once(messages,
'''            BuildDialogueChoices(ref y);\n            conversationContent.sizeDelta = new Vector2(0, Mathf.Max(538, y + 10));\n''',
'''            BuildReactionControls(conversation, ref y);\n            BuildLiveControls(ref y);\n            BuildDialogueChoices(ref y);\n            conversationContent.sizeDelta = new Vector2(0, Mathf.Max(538, y + 10));\n''', "messages live controls placement")

live_methods = r'''        private void BuildReactionControls(ConversationState conversation, ref float y)
        {
            if (selected == null || conversation == null || conversation.entries == null) return;
            List<LiveReactionOption> reactions = session.LiveMessages.GetReactionOptions(selected.id);
            if (reactions == null || reactions.Count == 0) return;

            MessageEntry target = null;
            for (int index = conversation.entries.Count - 1; index >= 0; index--)
            {
                MessageEntry candidate = conversation.entries[index];
                if (candidate != null && !candidate.outgoing && !string.IsNullOrEmpty(candidate.messageId))
                {
                    target = candidate;
                    break;
                }
            }
            if (target == null) return;

            y += 2;
            TmpLabel(conversationContent, "MessagesReactionCaption", "РЕАКЦИЯ", 26, y, 130, 24, 11, Soft,
                TextAlignmentOptions.MidlineLeft);
            float x = 160;
            for (int index = 0; index < reactions.Count; index++)
            {
                LiveReactionOption option = reactions[index];
                string messageId = target.messageId;
                string reactionId = option.Id;
                string label = option.Icon == "heart" ? "ТЕПЛО" : option.Icon == "dots" ? "..." : "ОК";
                LaptopSurface face = Surface(conversationContent,
                    "MessagesReactionFace_" + reactionId, x, y, 104, 28, 12,
                    new Color(.035f, .12f, .15f, .96f), true);
                Button button = face.gameObject.AddComponent<Button>();
                button.name = "MessagesReaction_" + messageId + "_" + reactionId;
                StyleButton(button, face);
                button.onClick.AddListener(() =>
                {
                    if (session.LiveMessages.SetReaction(selected.id, messageId, reactionId))
                        RefreshActiveContact(true);
                });
                TmpLabel(face.transform, "Label", label, 8, 2, 88, 24, 11, White, TextAlignmentOptions.Center);
                x += 112;
            }
            y += 38;
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

'''
replace_once(messages,
'''        private void BuildDialogueChoices(ref float y)\n''',
live_methods + '''        private void BuildDialogueChoices(ref float y)\n''', "messages live methods")

replace_once(messages,
'''            if (root == null || selected == null || !string.Equals(selected.id, "kaito", StringComparison.Ordinal) || yarnController != null)\n''',
'''            if (root == null || selected == null || !string.Equals(selected.id, "kaito", StringComparison.Ordinal) ||\n                yarnController != null || session.Messages.IsDialogueCompleted("kaito", "Kaito_Start"))\n''', "messages completed Kaito guard")

replace_once(messages,
'''        private void HandleMessagesChanged()\n        {\n''',
'''        private void HandleLiveMessagesChanged()\n        {\n            if (root != null) QueueRefresh(true);\n        }\n\n        private void HandleMessagesChanged()\n        {\n''', "messages live change handler")

laptop = "Assets/Rokas/Scripts/Presentation/LaptopView.cs"
replace_once(laptop,
'''        public bool IsClosing { get; private set; }\n''',
'''        public bool IsClosing { get; private set; }\n        public bool MessagesOpen { get { return section == 6 && !IsClosing; } }\n        public string ActiveMessageContactId { get { return MessagesOpen ? messages.ActiveContactId : string.Empty; } }\n''', "laptop message routing properties")

home = "Assets/Rokas/Scripts/Presentation/HomeView.cs"
replace_once(home,
'''        private Text prepared;\n        private float time;\n''',
'''        private Text prepared;\n        private CanvasGroup laptopUnreadIndicator;\n        private float time;\n''', "home unread field")

replace_once(home,
'''            ActionButton(parent, "LaptopHotspot", "YOMI  /  Ноутбук", HomeActionGlyph.Laptop, 950, 638, 390,\n                () => open("laptop"));\n''',
'''            ActionButton(parent, "LaptopHotspot", "YOMI  /  Ноутбук", HomeActionGlyph.Laptop, 950, 638, 390,\n                () => open("laptop"));\n            RectTransform unread = ui.Rect(parent, "HomeLaptopUnreadIndicator", 1260, 628, 34, 34);\n            laptopUnreadIndicator = unread.gameObject.AddComponent<CanvasGroup>();\n            Surface(unread, "UnreadGlow", 0, 0, 34, 34, 17, new Color(.18f, .86f, .90f, .86f));\n            Surface(unread, "UnreadCore", 10, 10, 14, 14, 7, new Color(.90f, .98f, 1f, .95f));\n            UpdateLaptopUnreadIndicator();\n''', "home unread build")

replace_once(home,
'''            if (weaponWard) weaponWard.gameObject.SetActive(session.State.weaponLevel >= 2);\n        }\n\n        public void Tick(float dt)\n''',
'''            if (weaponWard) weaponWard.gameObject.SetActive(session.State.weaponLevel >= 2);\n            UpdateLaptopUnreadIndicator();\n        }\n\n        private void UpdateLaptopUnreadIndicator()\n        {\n            if (laptopUnreadIndicator == null) return;\n            if (session.Messages.TotalUnread <= 0)\n            {\n                laptopUnreadIndicator.alpha = 0f;\n                return;\n            }\n            laptopUnreadIndicator.alpha = .68f + .22f * (.5f + .5f * Mathf.Sin(time * 3.2f));\n        }\n\n        public void Tick(float dt)\n''', "home unread refresh")

replace_once(home,
'''            time += dt;\n            mameReaction = Mathf.Max(0, mameReaction - dt);\n''',
'''            time += dt;\n            mameReaction = Mathf.Max(0, mameReaction - dt);\n            UpdateLaptopUnreadIndicator();\n''', "home unread tick")

replace_once(home,
'''        public void ClearReferences() { mame = null; weaponWard = null; objective = null; prepared = null; }\n''',
'''        public void ClearReferences()\n        {\n            mame = null; weaponWard = null; objective = null; prepared = null; laptopUnreadIndicator = null;\n        }\n''', "home unread clear")

audio = "Assets/Rokas/Scripts/Presentation/RokasAudio.cs"
replace_once(audio,
'''        public void Click()\n        {\n            if (!laptopMode) Play(assets.click);\n        }\n\n        public void LaptopMouseClick() { Play(assets.laptopMouseClick); }\n''',
'''        public void PlayMessageCue(string cue)\n        {\n            AudioClip clip = assets.laptopMouseClick ? assets.laptopMouseClick : assets.click;\n            float scale = cue == "WorldNotification" ? .34f :\n                cue == "LaptopNotification" ? .30f :\n                cue == "ActiveReceive" ? .22f :\n                cue == "SoftReceive" ? .16f : .26f;\n            PlayScaled(clip, scale);\n        }\n\n        private void PlayScaled(AudioClip clip, float scale)\n        {\n            if (!clip) return;\n            var source = effects[voice++ % effects.Length];\n            source.Stop();\n            source.clip = clip;\n            source.volume = Mathf.Clamp01(settings.masterVolume * settings.sfxVolume * Mathf.Clamp01(scale));\n            source.Play();\n        }\n\n        public void Click()\n        {\n            if (!laptopMode) Play(assets.click);\n        }\n\n        public void LaptopMouseClick() { Play(assets.laptopMouseClick); }\n''', "audio message cue")

view = "Assets/Rokas/Scripts/Presentation/RokasView.cs"
replace_once(view,
'''using System.Collections;\nusing Rokas.Core;\n''',
'''using System.Collections;\nusing System.Collections.Generic;\nusing Rokas.Core;\n''', "view generic collections")

replace_once(view,
'''        private int laptopOpenedFrame = -1;\n''',
'''        private int laptopOpenedFrame = -1;\n        private int observedMessageSequence;\n\n        public string LastMessageAudioCue { get; private set; }\n        public int MessageAudioCueCount { get; private set; }\n''', "view routing state")

replace_once(view,
'''            laptop = new LaptopView(ui, assets, session, contracts, Act, audio.Click, ToastShort, ClosePanel);\n            messageNotifications = new MessagesNotificationView(ui, stage, session);\n            session.Changed += Refresh;\n''',
'''            laptop = new LaptopView(ui, assets, session, contracts, Act, audio.Click, ToastShort, ClosePanel);\n            messageNotifications = new MessagesNotificationView(ui, stage, session);\n            observedMessageSequence = HighestMessageSequence();\n            LastMessageAudioCue = string.Empty;\n            MessageAudioCueCount = 0;\n            session.Messages.Changed += HandleMessageRoutingChanged;\n            session.Changed += Refresh;\n''', "view routing subscription")

routing_methods = r'''        private void HandleMessageRoutingChanged()
        {
            int highest = HighestMessageSequence();
            for (int sequence = observedMessageSequence + 1; sequence <= highest; sequence++)
            {
                string contactId;
                MessageEntry entry = FindMessageBySequence(sequence, out contactId);
                if (entry == null) continue;
                string cue;
                if (entry.outgoing)
                {
                    cue = "PlayerSend";
                }
                else if (entry.suppressMainNotification)
                {
                    cue = LaptopOpen && laptop.MessagesOpen &&
                        string.Equals(laptop.ActiveMessageContactId, contactId, StringComparison.Ordinal)
                        ? "ActiveReceive" : "SoftReceive";
                }
                else if (LaptopOpen && laptop.MessagesOpen &&
                    string.Equals(laptop.ActiveMessageContactId, contactId, StringComparison.Ordinal))
                {
                    cue = "ActiveReceive";
                }
                else
                {
                    cue = LaptopOpen ? "LaptopNotification" : "WorldNotification";
                }
                LastMessageAudioCue = cue;
                MessageAudioCueCount++;
                audio.PlayMessageCue(cue);
            }
            observedMessageSequence = Math.Max(observedMessageSequence, highest);
        }

        private MessageEntry FindMessageBySequence(int sequence, out string contactId)
        {
            contactId = string.Empty;
            List<ConversationState> conversations = session.Messages.GetOrderedConversations();
            for (int conversationIndex = 0; conversationIndex < conversations.Count; conversationIndex++)
            {
                ConversationState conversation = conversations[conversationIndex];
                if (conversation == null || conversation.entries == null) continue;
                for (int entryIndex = 0; entryIndex < conversation.entries.Count; entryIndex++)
                {
                    MessageEntry entry = conversation.entries[entryIndex];
                    if (entry != null && entry.sequence == sequence)
                    {
                        contactId = conversation.contactId ?? string.Empty;
                        return entry;
                    }
                }
            }
            return null;
        }

        private int HighestMessageSequence()
        {
            int highest = 0;
            List<ConversationState> conversations = session.Messages.GetOrderedConversations();
            for (int index = 0; index < conversations.Count; index++)
                if (conversations[index] != null) highest = Math.Max(highest, conversations[index].lastSequence);
            return highest;
        }

'''
replace_once(view,
'''        private void Act(Func<bool> action, string message)\n''',
routing_methods + '''        private void Act(Func<bool> action, string message)\n''', "view routing methods")

replace_once(view,
'''            messageNotifications.Dispose();\n            session.Changed -= Refresh;\n''',
'''            messageNotifications.Dispose();\n            session.Messages.Changed -= HandleMessageRoutingChanged;\n            session.Changed -= Refresh;\n''', "view routing disposal")

print("Applied Live Messenger presentation, reaction, audio routing and world unread patch")
