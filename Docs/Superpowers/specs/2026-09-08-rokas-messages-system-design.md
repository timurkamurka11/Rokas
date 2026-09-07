# ROKAS Messages System — Integration Design

Date: 2026-09-08
Baseline development: `5e494f188b90bdb6916e8e78e35ac38509bd0bce`
Working branch: `feature/messages-system`

## Existing architecture facts

- Messages already has a laptop desktop tile (`LaptopMessages`, section 6) but currently routes to the generic placeholder in `LaptopView`.
- The project uses one strict `SaveData` / `SaveJsonShape` / `SaveStore` persistence path. Messages state will extend it; no second save file.
- `GameSession` is the gameplay façade and `ContractService` is the existing contract API. Messages will call/adapt these APIs rather than create duplicate quest/contract systems.
- There is no generic game event bus. Message delivery hooks will be explicit, deterministic adapters around existing `GameSession` state transitions.
- Global laptop physical mouse audio already belongs to `RokasView.Tick`; Messages controls must not add a second physical-click sound.
- Existing GameCI (`.github/workflows/unity-ci.yml`) remains the verification gate and is not redesigned.
- Yarn Spinner is not currently installed. Current official package line is Yarn Spinner 3.x (`dev.yarnspinner.unity`); custom ROKAS presentation will derive from current Yarn presenter APIs rather than use generic Yarn UI.

## Target shape

### Core/domain

Add serializable Messages state to `SaveData`, with stable IDs for delivered events, message entries, conversations, unread counts, selected branches, attachment state, and current authored dialogue checkpoint. A `MessageService` owns deterministic delivery, read/unread, search, history and gameplay attachment actions.

Canonical contacts for v1: Kaito, Guild, Yumiko, Mika, Unknown, Merchant.

### Dialogue

Yarn Spinner owns authored branching and choice generation. `MessageDialogueAdapter` / a custom Yarn presenter translate Yarn lines/options/commands into the existing Messages domain. The saved dialogue checkpoint is explicit so reopening the laptop resumes without replaying already-delivered history.

Kaito is the first full vertical slice with several branch points, 2–4 replies, and the coordinate attachment `Восточный район, Сектор B-7`.

### Presentation

`LaptopMessagesView` is integrated as laptop section 6. The composition follows approved reference 03 first: left contact column, large right conversation pane, dark midnight-blue glass surfaces, cyan controlled focus glow, red unread/notification accents, existing Japanese night laptop wallpaper behind it.

Runtime message/contact/choice text is TextMeshPro. Contact list and conversation use independent `ScrollRect`/layout-driven content; bubbles are not manually positioned per message. Search is a real TMP input field.

### Assets

Do not use full reference screenshots as runtime UI. Reuse existing ROKAS wallpaper. Prepare only production portrait/icon crops needed from approved sheets; panels, borders, badges and bubbles are real Unity UI / reusable surfaces.

### Events and attachments

Existing real gameplay state only:
- contract availability/acceptance/completion/payment state may deliver authored Guild messages;
- reputation/completed-run thresholds may gate authored contacts/messages where meaningful;
- coordinate action uses a clean tested hook because no world-map destination API currently exists;
- contract attachment references the existing `ContractDefinition.Id` and accepts only through existing `GameSession.AcceptContract` semantics.

## Verification strategy

Logic is test-first. Required EditMode coverage includes unread, mark-read, idempotency, deterministic choices/checkpoints, save/load history and unread, coordinate hook, contract identity/state, search and gated delivery. Add Yarn asset/compile checks and one focused PlayMode Messages smoke test. Final integration into `development` is allowed only after fresh existing GameCI evidence.
