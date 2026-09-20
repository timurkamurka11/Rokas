from pathlib import Path


def replace_once(path, old, new, label):
    file = Path(path)
    text = file.read_text(encoding="utf-8")
    if old not in text:
        raise SystemExit(f"Missing Live Messenger 1.1 presentation anchor: {label} in {path}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")


# Audio: cache the two uploaded Resources clips once and route incoming/reaction cues to them.
audio = "Assets/Rokas/Scripts/Presentation/RokasAudio.cs"
replace_once(audio,
'''        private readonly AudioSource[] effects = new AudioSource[4];\n        private int voice;\n''',
'''        private readonly AudioSource[] effects = new AudioSource[4];\n        private readonly AudioClip messageArrive;\n        private readonly AudioClip reactionCue;\n        private int voice;\n''', "audio cached clips")
replace_once(audio,
'''            for (int i = 0; i < effects.Length; i++) effects[i] = MakeSource(audioRoot, false);\n        }\n''',
'''            for (int i = 0; i < effects.Length; i++) effects[i] = MakeSource(audioRoot, false);\n            messageArrive = Resources.Load<AudioClip>("Messages/Audio/MessageArrive");\n            reactionCue = Resources.Load<AudioClip>("Messages/Audio/Reaction");\n        }\n''', "audio resource load")
replace_once(audio,
'''        public void PlayMessageCue(string cue)\n        {\n            AudioClip clip = assets.laptopMouseClick ? assets.laptopMouseClick : assets.click;\n            float scale = cue == "WorldNotification" ? .34f :\n                cue == "LaptopNotification" ? .30f :\n                cue == "ActiveReceive" ? .22f :\n                cue == "SoftReceive" ? .16f : .26f;\n            PlayScaled(clip, scale);\n        }\n''',
'''        public void PlayMessageCue(string cue)\n        {\n            if (string.Equals(cue, "Reaction", System.StringComparison.Ordinal))\n            {\n                PlayScaled(reactionCue ? reactionCue : assets.click, .72f);\n                return;\n            }\n            if (string.Equals(cue, "PlayerSend", System.StringComparison.Ordinal))\n            {\n                PlayScaled(assets.laptopMouseClick ? assets.laptopMouseClick : assets.click, .30f);\n                return;\n            }\n\n            AudioClip clip = messageArrive ? messageArrive : (assets.laptopMouseClick ? assets.laptopMouseClick : assets.click);\n            float scale = cue == "WorldNotification" ? .92f :\n                cue == "LaptopNotification" ? .84f :\n                cue == "ActiveReceive" ? .76f :\n                cue == "SoftReceive" ? .40f : .70f;\n            PlayScaled(clip, scale);\n        }\n''', "audio message routing")


# RokasView: reactions are signals rather than new message sequences, so route their cue separately.
view = "Assets/Rokas/Scripts/Presentation/RokasView.cs"
replace_once(view,
'''            session.Messages.Changed += HandleMessageRoutingChanged;\n            session.Changed += Refresh;\n''',
'''            session.Messages.Changed += HandleMessageRoutingChanged;\n            session.LiveMessages.Signal += HandleLiveMessengerSignal;\n            session.Changed += Refresh;\n''', "view live signal subscribe")
replace_once(view,
'''        private void HandleMessageRoutingChanged()\n        {\n''',
'''        private void HandleLiveMessengerSignal(LiveMessengerSignal signal)\n        {\n            if (signal == null || signal.Kind != LiveMessengerSignalKind.ReactionChanged) return;\n            LastMessageAudioCue = "Reaction";\n            MessageAudioCueCount++;\n            audio.PlayMessageCue("Reaction");\n        }\n\n        private void HandleMessageRoutingChanged()\n        {\n''', "view reaction audio handler")
# Dispose may be near file end; insert unsubscribe alongside Messages unsubscribe if present.
replace_once(view,
'''            session.Messages.Changed -= HandleMessageRoutingChanged;\n''',
'''            session.Messages.Changed -= HandleMessageRoutingChanged;\n            session.LiveMessages.Signal -= HandleLiveMessengerSignal;\n''', "view live signal unsubscribe")


# Home: add a whole-pill glow behind the YOMI laptop affordance while preserving the existing dot.
home = "Assets/Rokas/Scripts/Presentation/HomeView.cs"
replace_once(home,
'''        private CanvasGroup laptopUnreadIndicator;\n        private float time;\n''',
'''        private CanvasGroup laptopUnreadIndicator;\n        private CanvasGroup laptopUnreadGlow;\n        private float time;\n''', "home glow field")
replace_once(home,
'''            ActionButton(parent, "LaptopHotspot", "YOMI  /  Ноутбук", HomeActionGlyph.Laptop, 950, 638, 390,\n                () => open("laptop"));\n            RectTransform unread = ui.Rect(parent, "HomeLaptopUnreadIndicator", 1260, 628, 34, 34);\n''',
'''            RectTransform glow = ui.Rect(parent, "HomeLaptopUnreadGlow", 938, 626, 414, 106);\n            laptopUnreadGlow = glow.gameObject.AddComponent<CanvasGroup>();\n            laptopUnreadGlow.alpha = 0f;\n            LaptopSurface glowSurface = Surface(glow, "GlowSurface", 0, 0, 414, 106, 53, new Color(.12f, .82f, .72f, .30f));\n            glowSurface.raycastTarget = false;\n            ActionButton(parent, "LaptopHotspot", "YOMI  /  Ноутбук", HomeActionGlyph.Laptop, 950, 638, 390,\n                () => open("laptop"));\n            RectTransform unread = ui.Rect(parent, "HomeLaptopUnreadIndicator", 1260, 628, 34, 34);\n''', "home whole laptop glow")
replace_once(home,
'''        private void UpdateLaptopUnreadIndicator()\n        {\n            if (laptopUnreadIndicator == null) return;\n            if (session.Messages.TotalUnread <= 0)\n            {\n                laptopUnreadIndicator.alpha = 0f;\n                return;\n            }\n            laptopUnreadIndicator.alpha = .68f + .22f * (.5f + .5f * Mathf.Sin(time * 3.2f));\n        }\n''',
'''        private void UpdateLaptopUnreadIndicator()\n        {\n            bool unread = session.Messages.TotalUnread > 0;\n            if (laptopUnreadIndicator != null)\n                laptopUnreadIndicator.alpha = unread ? .68f + .22f * (.5f + .5f * Mathf.Sin(time * 3.2f)) : 0f;\n            if (laptopUnreadGlow != null)\n            {\n                if (unread)\n                    laptopUnreadGlow.alpha = .20f + .18f * (.5f + .5f * Mathf.Sin(time * 2.35f));\n                else\n                    laptopUnreadGlow.alpha = Mathf.MoveTowards(laptopUnreadGlow.alpha, 0f, .08f);\n            }\n        }\n''', "home unread glow behavior")
replace_once(home,
'''            mame = null; weaponWard = null; objective = null; prepared = null; laptopUnreadIndicator = null;\n''',
'''            mame = null; weaponWard = null; objective = null; prepared = null; laptopUnreadIndicator = null; laptopUnreadGlow = null;\n''', "home glow clear")


# Messages: replace the old single-last-message text reaction row with a per-message sticker picker.
messages = "Assets/Rokas/Scripts/Presentation/LaptopMessagesView.cs"
replace_once(messages,
'''        private bool liveTopicsOpen;\n        private bool refreshPending;\n''',
'''        private bool liveTopicsOpen;\n        private string reactionPickerMessageId = string.Empty;\n        private bool refreshPending;\n''', "messages picker field")
replace_once(messages,
'''            liveTopicsOpen = false;\n        }\n''',
'''            liveTopicsOpen = false;\n            reactionPickerMessageId = string.Empty;\n        }\n''', "messages picker hide")
replace_once(messages,
'''            if (!wasSelected) liveTopicsOpen = false;\n''',
'''            if (!wasSelected)\n            {\n                liveTopicsOpen = false;\n                reactionPickerMessageId = string.Empty;\n            }\n''', "messages picker contact reset")
replace_once(messages,
'''                if (entry.attachment != null &&\n                    (entry.attachment.kind == MessageAttachmentKind.Coordinates ||\n                     entry.attachment.kind == MessageAttachmentKind.Contract ||\n                     entry.attachment.kind == MessageAttachmentKind.FoodGift))\n                {\n                    y = BuildActionAttachment(entry, y);\n                }\n            }\n\n            BuildReactionControls(conversation, ref y);\n''',
'''                if (entry.attachment != null &&\n                    (entry.attachment.kind == MessageAttachmentKind.Coordinates ||\n                     entry.attachment.kind == MessageAttachmentKind.Contract ||\n                     entry.attachment.kind == MessageAttachmentKind.FoodGift))\n                {\n                    y = BuildActionAttachment(entry, y);\n                }\n                BuildMessageReactions(entry, ref y);\n            }\n\n''', "messages per-bubble reaction placement")

file = Path(messages)
text = file.read_text(encoding="utf-8")
start = text.find("        private void BuildReactionControls(ConversationState conversation, ref float y)\n")
end = text.find("        private void BuildLiveControls(ref float y)\n", start)
if start < 0 or end < 0:
    raise SystemExit("Missing old Messages reaction controls block")
new_methods = r'''        private void BuildMessageReactions(MessageEntry entry, ref float y)
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

'''
file.write_text(text[:start] + new_methods + text[end:], encoding="utf-8")

# Two nullable-annotation warnings in the Yarn test are from nullable annotations in an otherwise disabled annotation context.
yarn_tests = "Assets/Rokas/Tests/EditMode/MessagesYarnTests.cs"
replace_once(yarn_tests,
'''using System;\n''',
'''#nullable enable annotations\nusing System;\n''', "Yarn nullable annotation context")

print("Applied Live Messenger 1.1 presentation/audio/reaction/glow patch")
