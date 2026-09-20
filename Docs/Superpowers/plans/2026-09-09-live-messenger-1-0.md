# Live Messenger 1.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the existing ROKAS Messages screen into a deterministic authored live messenger for Yumiko, Hunter Guild, and Kaito, while fixing the Yumiko Gift-vs-Recommendation semantic bug.

**Architecture:** Keep `MessageService` as the sole owner of persisted message history, unread counts, attachments, stable event IDs, and save/load normalization. Add one small `LiveMessengerService` above it to own authored topic definitions, deterministic pending chains, topic progression, transient typing state, and delivery signals. Extend the existing `LaptopMessagesView`, `RokasView`, `HomeView`, and `RokasAudio`; do not create a second Messages UI, shop, save system, notification backend, or audio manager.

**Tech Stack:** Unity 6000.3.19f1, C#, uGUI, TextMesh Pro, existing Yarn Spinner 3.1.2 integration, existing GameCI and .NET Core test harness.

**Spec:** `Docs/Superpowers/specs/2026-09-08-rokas-messages-system-design.md` plus the user-approved Live Messenger 1.0 extension brief dated 2026-09-09.

## Global Constraints

- Work only on `feature/messages-system`; do not modify `development`.
- Do not merge PR #4; it must remain open, draft, and unmerged.
- Do not touch Combat 2.0, Mika, Unknown, or Home Hub atmosphere work.
- Reuse existing MessageService, ConversationState, SaveData/SaveStore, unread, notifications, attachments, Kaito Yarn base dialogue, and Yumiko Food integration.
- All player-facing authored text is Russian.
- No free-text AI chat and no wall-clock `DateTime` identities.
- Stable authored IDs only; no random GUID/timestamp gameplay identities.
- No per-frame asset/font/audio-source creation.
- Preserve the existing Messages layout, avatar sizes, attachment cards, palette, and typography hierarchy.
- `git diff --check` must run literally inside a real Git checkout before completion.

---

### Task 1: Yumiko Gift vs Recommendation semantic regression

**Files:**
- Modify: `Assets/Rokas/Scripts/Core/MessageService.cs`
- Modify: `Tests/Core/YumikoFoodIntegrationTests.cs`

**Interfaces:**
- Consumes: existing `DeliverYumikoContractContext`, `FoodGift`, stable event IDs.
- Produces: recommendation copy that explicitly points to YOMI Kitchen and never implies a free item; first scripted gift remains a real claimable FoodGift.

- [ ] **Step 1: Write the failing regression expectation**

Expected recommendation copy:

```text
На Кисараги? Перед выходом загляни в YOMI Kitchen. Зелёный чай там будет кстати.
```

The second legitimate contract acceptance must create a recommendation entry with `attachment == null` and must not redeliver the one-time gift.

- [ ] **Step 2: Run Core tests and verify RED**

Run: `dotnet run --project Tests/Core/Rokas.Core.Tests.csproj --configuration Release`

Expected: failure in Yumiko Food recommendation semantics because current production copy still says `Возьми зелёный чай YOMI`.

- [ ] **Step 3: Make the smallest production change**

Change only the recommendation authored copy. Keep `YumikoGiftEventId`, `YumikoGiftText`, FoodGift target, exactly-once claim, and persistence unchanged.

- [ ] **Step 4: Run Core tests and verify GREEN**

Run the same Core command. Expected: all Core tests pass.

### Task 2: Deterministic Live Messenger core foundation

**Files:**
- Create: `Assets/Rokas/Scripts/Core/LiveMessengerService.cs`
- Create: `Assets/Rokas/Scripts/Core/LiveMessengerService.cs.meta`
- Modify: `Assets/Rokas/Scripts/Core/MessageService.cs`
- Modify: `Assets/Rokas/Scripts/Core/GameSession.cs`
- Create: `Tests/Core/LiveMessengerCoreTests.cs`
- Modify: `Tests/Core/Program.cs`
- Modify: `Tests/Core/Rokas.Core.Tests.csproj`

**Interfaces:**
- `GameSession.LiveMessages : LiveMessengerService`
- `LiveMessengerService.GetTopics(string) : List<LiveTopicOption>`
- `LiveMessengerService.StartTopic(string contactId, string topicId) : bool`
- `LiveMessengerService.GetReplyOptions(string) : List<LiveReplyOption>`
- `LiveMessengerService.SubmitReply(string contactId, string replyId) : bool`
- `LiveMessengerService.IsTyping(string) : bool`
- `LiveMessengerService.GetPresenceText(string) : string`
- `LiveMessengerService.GetReactionOptions(string) : List<LiveReactionOption>`
- `LiveMessengerService.SetReaction(string contactId, string messageId, string reactionId) : bool`
- `LiveMessengerService.Signal : event Action<LiveMessengerSignal>`

- [ ] **Step 1: Write reflection-based Core RED tests**

Tests must fail at runtime, not compile time, before the new service exists. Cover: Yumiko/Guild/Kaito topic floors; no Mika/Unknown topics; Kaito launcher blocked until `Kaito_Start` is completed; player bubble; typing; multi-bubble chain; branching reply; launcher return; once/repeatable/contextual/cooldown availability; contact isolation; partial-chain save recovery; reaction persistence.

- [ ] **Step 2: Run Core tests and verify RED**

Expected: explicit missing `GameSession.LiveMessages` / Live Messenger contract failures.

- [ ] **Step 3: Implement persisted Live state minimally**

Extend existing persisted message objects with null-safe fields for topic progress, active topic identity, waiting-for-choice state, pending chain queue, message chain ID, notification suppression flag, and reaction ID. Normalize all new collections/strings for old saves.

- [ ] **Step 4: Implement `LiveMessengerService`**

Use static authored definitions, deterministic IDs, persisted pending chain state, and transient in-memory typing countdown. Recovery of a partially delivered chain occurs during GameSession construction before notification views subscribe, so historical typing/audio/popup does not replay.

- [ ] **Step 5: Verify Core GREEN**

Run Core tests. Expected: all old and new Core tests pass.

### Task 3: Live Messages UI and interaction

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/LaptopMessagesView.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/LaptopView.cs`
- Create: `Assets/Rokas/Scripts/Presentation/MessageReactionIcon.cs`
- Create: `Assets/Rokas/Scripts/Presentation/MessageReactionIcon.cs.meta`
- Create: `Assets/Rokas/Tests/PlayMode/LiveMessengerPlayModeTests.cs`
- Create: `Assets/Rokas/Tests/PlayMode/LiveMessengerPlayModeTests.cs.meta`

**Interfaces:**
- `LaptopMessagesView.SelectedContactId`
- `LaptopView.MessagesOpen`
- `LaptopView.ActiveMessageContactId`

- [ ] **Step 1: Write PlayMode RED tests**

Expect `MessagesLiveLauncher_*`, topic buttons, outgoing player bubbles, `MessagesTypingIndicator`, response buttons, reaction buttons, persistent reaction display, and contact-specific presence text. Tests use existing UI interaction helpers and do not reference new production types directly.

- [ ] **Step 2: Verify PlayMode RED**

Run GameCI All. Expected failures are missing Live Messenger controls/typing/reaction behavior; existing baseline tests must still compile.

- [ ] **Step 3: Extend existing `LaptopMessagesView`**

Add a launcher (`Написать Юмико`, `Отправить запрос`, `Написать Кайто`), 3–6 currently available topic buttons, response choice buttons, transient typing indicator, outgoing delivery label, vector reaction buttons, persisted reaction display, presence text, and subtle incoming-bubble fade/slide. Keep existing history, attachments, Yarn choices, scroll container, and avatar/contact layout.

- [ ] **Step 4: Verify PlayMode GREEN**

Run GameCI All and focused Live Messenger tests.

### Task 4: Authored contact content and proactive chains

**Files:**
- Modify: `Assets/Rokas/Scripts/Core/LiveMessengerService.cs`
- Modify: `Tests/Core/LiveMessengerCoreTests.cs`

**Interfaces:** static authored topic/chain catalog in `LiveMessengerService`.

- [ ] **Step 1: Cover representative voice/content rules in RED tests**

Yumiko >= 6 topics with small talk and real branching; Guild >= 5 formal request topics and no warm reactions; Kaito >= 6 tactical/intel topics after base Yarn completion. Verify representative branch IDs and distinct replies, not giant text dumps.

- [ ] **Step 2: Add authored definitions**

Yumiko: food, hunt preparation, how-are-you, news, last contract/return, concern, unusual kitchen. Guild: current contract, target, reward, mission status, information request. Kaito: place, target, tactics, rumors, coordinates, information source.

- [ ] **Step 3: Add proactive chains**

Observe genuinely new existing MessageService entries and queue deterministic follow-up chains for Yumiko purchase/contract/gift/return; Guild offer/acceptance/completion; Kaito accepted contract/return/completion/coordinates. Never bootstrap-replay historical entries.

- [ ] **Step 4: Verify Core + PlayMode GREEN**

### Task 5: Message audio routing and anti-spam

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/RokasAudio.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/RokasView.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/MessagesNotificationView.cs`
- Modify: `Assets/Rokas/Tests/PlayMode/LiveMessengerPlayModeTests.cs`

**Interfaces:**
- `RokasAudio.PlayMessageCue(MessageAudioCue)`
- `RokasView.LastMessageAudioCue`
- `RokasView.MessageAudioCueCount`

- [ ] **Step 1: RED audio routing tests**

Player send -> one send cue; active same-contact incoming -> soft receive cue; laptop open/other tab -> notification cue; laptop closed -> world notification cue; suppressed follow-up bubbles -> no extra main cue; historical reload -> no new cue.

- [ ] **Step 2: Implement cached messenger clips in existing audio manager**

Generate four tiny soft procedural clips once in `RokasAudio` (send, active receive, notification, muted world notification) so no external copyrighted asset is introduced. Reuse existing effect AudioSources; do not instantiate per message.

- [ ] **Step 3: Route signals in `RokasView`**

Use `LiveMessengerSignal` plus actual laptop/messages/selected-contact state. Follow-up bubbles marked `suppressMainNotification` may use active-chat pop only; they never produce repeated full notification sounds.

- [ ] **Step 4: Keep compact popup anti-spam consistent**

`MessagesNotificationView` must not replace/retrigger the compact popup for a suppressed follow-up bubble.

### Task 6: Home laptop unread world feedback

**Files:**
- Modify: `Assets/Rokas/Scripts/Presentation/HomeView.cs`
- Modify: `Assets/Rokas/Scripts/Presentation/RokasView.cs`
- Modify: `Assets/Rokas/Tests/PlayMode/LiveMessengerPlayModeTests.cs`

**Interfaces:**
- `HomeView.PulseLaptopUnread()`
- persistent indicator derives from `session.Messages.TotalUnread`.

- [ ] **Step 1: RED world-feedback tests**

Laptop closed + new unread -> one world cue and visible `HomeLaptopUnreadIndicator`; repeated Tick -> no extra cue; historical unread reload -> indicator remains but no fresh cue; reading the relevant conversation clears persistent indicator.

- [ ] **Step 2: Extend the existing Laptop home action**

Add one small vector/shape LED/glow under the existing `LaptopHotspot`, with a stronger one-shot pulse on new chain and a slow low-alpha glow while any real unread remains. No Home Hub redesign.

- [ ] **Step 3: Verify PlayMode GREEN**

### Task 7: Final regression and checkpoint

**Files:**
- All modified files above.

- [ ] **Step 1: Run fresh Core/domain tests**
- [ ] **Step 2: Run fresh Unity EditMode + PlayMode**
- [ ] **Step 3: Confirm old Yumiko Food, Contract, Coordinate, Notifications, lifecycle/save-load, and Kaito Yarn tests remain green**
- [ ] **Step 4: Run validator and classify only pre-existing baseline failures**
- [ ] **Step 5: Search fresh Unity logs for new TMP missing-glyph warnings**
- [ ] **Step 6: Run literal `git diff --check` inside the Actions checkout; require exit 0 and empty output**
- [ ] **Step 7: Create tree-identical checkpoint `VERIFIED LIVE MESSENGER 1.0 YUMIKO GUILD KAITO CHECKPOINT`**
- [ ] **Step 8: Verify remote feature HEAD exact SHA and verify `development` and PR #4 remain unchanged/unmerged**
