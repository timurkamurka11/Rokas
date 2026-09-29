using System;
using System.Collections;
using System.Collections.Generic;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class RokasView : IDisposable
    {
        private readonly RokasBootstrap owner;
        private readonly GameSession session;
        private readonly RokasAssets assets;
        private readonly RokasAudio audio;
        private readonly Action save;
        private readonly UiKit ui;
        private readonly RectTransform stage;
        private readonly RectTransform scene;
        private readonly CanvasGroup sceneInput;
        private readonly RectTransform globalChrome;
        private readonly Button settingsButton;
        private readonly RectTransform panels;
        private readonly RectTransform transitions;
        private readonly Text wallet;
        private readonly Text status;
        private readonly Text saved;
        private readonly Text toast;
        private readonly RawImage background;
        private readonly WorldEffects effects;
        private readonly HomeView home;
        private readonly MissionView mission;
        private readonly ContractPanels contracts;
        private readonly LaptopView laptop;
        private readonly MessagesNotificationView messageNotifications;
        private readonly HubDialogueController hubDialogue;
        private RunPhase phase;
        private string panel;
        private bool transition;
        private bool storageBlocked;
        private float toastTime;
        private int laptopOpenedFrame = -1;
        private int observedMessageSequence;

        public string LastMessageAudioCue { get; private set; }
        public int MessageAudioCueCount { get; private set; }

        public bool Paused
        {
            get
            {
                return transition || storageBlocked || panel == "settings" ||
                    (hubDialogue != null && hubDialogue.IsOpen);
            }
        }
        public bool HubDialogueOpen => hubDialogue != null && hubDialogue.IsOpen;
        public bool HubVoicePlaying => audio != null && audio.HubVoicePlaying;
        public int GenericClickCount => audio != null ? audio.ClickCount : 0;
        public bool CombatHitStop { get { return mission.HitStopRemaining > 0; } }
        public string SelectedReactiveTargetId { get { return mission.SelectedReactiveTargetId; } }
        public int AnimatedReactiveEnemyCount { get { return mission.AnimatedEnemyCount; } }
        public bool ReactiveActorsReady { get { return mission.AnimatedActorsReady; } }
        public bool HunterApproachComplete { get { return mission.HunterApproachComplete; } }
        public bool HunterAtHome { get { return mission.HunterAtHome; } }
        public bool StartHunterApproach(string enemyId) { return mission.StartHunterApproach(enemyId); }
        public void CancelHunterMotion() { mission.CancelHunterMotion(); }
        public void SetReactivePresentationLocked(bool value) { mission.SetReactivePresentationLocked(value); }
        public void CancelCombatInput() { mission.CancelInput(); }
        public void HandleCombatInput(bool dodge, bool deflect, bool resonance) { mission.HandleInput(dodge, deflect, resonance); }
        public bool LaptopOpen { get { return panel == "laptop"; } }

        public RokasView(RokasBootstrap owner, RokasAssets assets, GameSession session, RokasAudio audio, Action save)
        {
            this.owner = owner;
            this.assets = assets;
            this.session = session;
            this.audio = audio;
            this.save = save;
            ui = new UiKit(assets, audio.Click);
            var canvasObject = new GameObject("RokasCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(owner.transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            stage = ui.Rect(canvasObject.transform, "AuthoredStage", 0, 0, 1920, 1080);
            stage.anchorMin = stage.anchorMax = new Vector2(.5f, .5f);
            stage.pivot = new Vector2(.5f, .5f);
            background = ui.Art(stage, "WorldIllustration", assets.home, 0, 0, 1920, 1080);
            effects = new WorldEffects(ui, stage, background.rectTransform, session.State.settings, audio, owner.transform);
            scene = ui.Rect(stage, "SceneInteractions", 0, 0, 1920, 1080);
            sceneInput = scene.gameObject.AddComponent<CanvasGroup>();

            globalChrome = ui.Rect(stage, "GlobalChrome", 0, 0, 1920, 1080);
            ui.Box(globalChrome, "HeaderShade", 0, 0, 1920, 100, new Color(.025f, .045f, .05f, .86f));
            ui.Box(globalChrome, "HeaderRule", 56, 99, 1808, 1, new Color(.5f, .6f, .55f, .3f));
            ui.Label(globalChrome, "Logo", "R O K A S", 58, 15, 305, 66, 36, UiKit.Paper, true);
            ui.Label(globalChrome, "BrandTag", "YOKAI CONTRACT HUNTER", 370, 23, 320, 25, 14, UiKit.Muted);
            status = ui.Label(globalChrome, "LocationStatus", "ТВОЙ ДОМ", 370, 47, 520, 26, 17, UiKit.Gold);
            wallet = ui.Label(globalChrome, "Wallet", "", 1155, 18, 475, 58, 22, UiKit.Paper, false, TextAnchor.MiddleRight);
            settingsButton = ui.Button(globalChrome, "Settings", "Настройки", 1675, 23, 185, 52, () => OpenPanel("settings"));
            ui.Box(globalChrome, "FooterShade", 0, 1006, 1920, 74, new Color(.025f, .045f, .05f, .88f));
            ui.Label(globalChrome, "Controls", "TAB  выбрать предмет    ENTER  взаимодействовать    ESC  пауза / назад", 58, 1018, 1220, 37, 17, UiKit.Muted);
            saved = ui.Label(globalChrome, "SaveStatus", "ПРОФИЛЬ СОХРАНЁН", 1420, 1018, 440, 37, 15, UiKit.Muted, false, TextAnchor.MiddleRight);
            panels = ui.Rect(stage, "Panels", 0, 0, 1920, 1080);
            toast = ui.Label(stage, "Toast", "", 310, 925, 1300, 60, 23, UiKit.Paper, false, TextAnchor.MiddleCenter);
            var toastOutline = toast.gameObject.AddComponent<Outline>();
            toastOutline.effectColor = new Color(0, 0, 0, .9f);
            toastOutline.effectDistance = new Vector2(2, -2);
            transitions = ui.Rect(stage, "Transitions", 0, 0, 1920, 1080);
            hubDialogue = new HubDialogueController(
                ui,
                stage,
                assets,
                audio,
                SetSceneInteractionsEnabled,
                OpenHubMenu);
            home = new HomeView(
                ui, assets, session, audio,
                OpenPanel, Act, Travel, ToastShort, OpenHubDialogue);
            mission = new MissionView(ui, assets, session, audio, Act, Travel, ToastShort, effects, () => Paused,
                owner.SubmitReactiveCommand, owner.SubmitReactiveDefense, owner.ConfirmReactiveCounter,
                owner.RetryReactiveSave, owner.SelectReactiveTarget);
            contracts = new ContractPanels(ui, session, Act, RefreshPanel, Travel, ClosePanel);
            laptop = new LaptopView(ui, assets, session, contracts, Act, audio.LaptopMouseClick, ToastShort, ClosePanel,
                owner.VideoPresenter, () => audio.VideoVolume, () => owner.VideoTransitionsEnabled);
            messageNotifications = new MessagesNotificationView(ui, stage, session);
            observedMessageSequence = HighestMessageSequence();
            LastMessageAudioCue = string.Empty;
            MessageAudioCueCount = 0;
            session.Messages.Changed += HandleMessageRoutingChanged;
            session.LiveMessages.Signal += HandleLiveMessengerSignal;
            session.Changed += Refresh;
            session.Combat.Hit += OnHit;
            session.Combat.ActionResolved += mission.OnAction;
            phase = session.State.phase;
            RebuildScene();
            Tick(0);
            Refresh();
        }

        private void HandleLiveMessengerSignal(LiveMessengerSignal signal)
        {
            if (signal == null || signal.Kind != LiveMessengerSignalKind.ReactionChanged) return;
            LastMessageAudioCue = "Reaction";
            MessageAudioCueCount++;
            audio.PlayMessageCue("Reaction");
        }

        private void HandleMessageRoutingChanged()
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

        private void Act(Func<bool> action, string message)
        {
            if (storageBlocked || transition) return;
            if (action())
            {
                save();
                if (!string.IsNullOrEmpty(message)) Toast(message);
            }
            else Toast("Сейчас это действие недоступно.");
        }

        private void Travel(Func<bool> action, string caption)
        {
            if (storageBlocked || transition) return;
            owner.StartCoroutine(TravelRoutine(action, caption));
        }

        private IEnumerator TravelRoutine(Func<bool> action, string caption)
        {
            transition = true;
            ui.Clear(transitions);
            var shade = ui.Box(transitions, "TravelCurtain", 0, 0, 1920, 1080, Color.black, true);
            var group = shade.gameObject.AddComponent<CanvasGroup>();
            ui.Label(shade.transform, "JourneyCaption", caption, 280, 410, 1360, 170, 38, UiKit.Paper, true, TextAnchor.MiddleCenter);
            for (float t = 0; t < .3f; t += Time.unscaledDeltaTime)
            { group.alpha = t / .3f; yield return null; }
            group.alpha = 1;
            ClosePanel();
            audio.Play(assets.portalSound);
            if (action()) save();
            yield return new WaitForSecondsRealtime(.3f);
            for (float t = 0; t < .45f; t += Time.unscaledDeltaTime)
            { group.alpha = 1 - t / .45f; yield return null; }
            ui.Clear(transitions);
            transition = false;
        }

        private void Refresh()
        {
            RunPhase nextPhase = session.State.phase;
            if (phase != nextPhase)
            {
                bool wasHomeLocation = IsHomeLocation(phase);
                bool isHomeLocation = IsHomeLocation(nextPhase);
                phase = nextPhase;
                if (!wasHomeLocation && isHomeLocation) laptop.BeginHomeVisit();
                RebuildScene();
            }
            wallet.text = "¥ " + session.State.yen.ToString("N0") + "     /     РЕП " + session.State.reputation + "     /     ПЕПЕЛ " + session.State.spiritAsh;
            home.Refresh();
            mission.Refresh();
        }

        public void RefreshReactiveCombat() { mission.Refresh(); }
        public void PresentReactiveCombatStep(CombatStep step) { mission.PresentReactiveCombatStep(step); }

        private static bool IsHomeLocation(RunPhase value)
        {
            return value == RunPhase.Home || value == RunPhase.Accepted || value == RunPhase.Payment;
        }

        private void RebuildScene()
        {
            if (hubDialogue != null && hubDialogue.IsOpen)
                hubDialogue.Close();
            ui.Clear(scene);
            home.ClearReferences();
            mission.ClearReferences();
            bool otherSide = phase == RunPhase.Combat || phase == RunPhase.Sealed || phase == RunPhase.Failed;
            bool homeLocation = !otherSide && phase != RunPhase.Portal;
            globalChrome.gameObject.SetActive(!homeLocation);
            Texture2D reactiveBackground = phase == RunPhase.Combat &&
                session.CombatMode == CombatMode.ReactiveTurns
                ? Resources.Load<Texture2D>("CombatB/AbyssArenaBackground") : null;
            background.texture = reactiveBackground != null ? reactiveBackground :
                otherSide ? assets.subway : phase == RunPhase.Portal ? assets.portal : assets.home;
            audio.SetLocation(otherSide || phase == RunPhase.Portal, phase == RunPhase.Combat);
            effects.SetLocation(!otherSide && phase != RunPhase.Portal, phase == RunPhase.Portal);
            if (otherSide || phase == RunPhase.Portal) mission.Build(scene);
            else home.Build(scene);
            status.text = reactiveBackground != null ? "ПРОПАСТЬ  /  ИСКАЖЁННЫЙ КОНТРАКТ" :
                otherSide ? "КИСАРАГИ  /  ЗАКРЫТАЯ ПЛАТФОРМА" :
                phase == RunPhase.Portal ? "ГОРОД  /  ЗАБЫТОЕ СВЯТИЛИЩЕ" : "ТВОЙ ДОМ  /  НОЧЬ, ДОЖДЬ";
        }

        private void OnHit(CombatHit hit) { mission.OnHit(hit); }

        private void OpenPanel(string value)
        {
            if (transition || storageBlocked ||
                (hubDialogue != null && hubDialogue.IsOpen))
                return;
            if (laptop.IsClosing) return;
            if (value == "laptop")
            {
                laptop.Reset();
                laptopOpenedFrame = Time.frameCount;
            }
            mission.CancelInput();
            panel = value;
            audio.SetLaptopMode(panel == "laptop");
            RefreshPanel();
        }

        private void RefreshPanel()
        {
            ui.Clear(panels);
            bool sceneEnabled =
                string.IsNullOrEmpty(panel) &&
                (hubDialogue == null || !hubDialogue.IsOpen);
            sceneInput.interactable = sceneEnabled;
            sceneInput.blocksRaycasts = sceneEnabled;
            settingsButton.interactable = string.IsNullOrEmpty(panel);
            if (string.IsNullOrEmpty(panel)) return;
            ui.Box(panels, "ModalShade", 0, 0, 1920, 1080, new Color(0, .015f, .02f, .76f), true);
            if (panel == "settings")
            {
                SettingsPanel.Build(ui, panels, session.State.settings, () => { owner.ApplyDisplaySettings(); save(); }, ClosePanel, () => { save(); Application.Quit(); });
                FocusFirst(panels);
                return;
            }
            if (panel == "laptop")
            {
                laptop.Build(panels);
                return;
            }
            contracts.Build(panels, panel);
            FocusFirst(panels);
        }

        private void ClosePanel()
        {
            if (panel == "laptop") { laptop.BeginClose(FinishClosePanel); return; }
            FinishClosePanel();
        }

        private void FinishClosePanel()
        {
            bool fromLaptop = panel == "laptop";
            panel = null;
            audio.SetLaptopMode(false);
            ui.Clear(panels);
            bool sceneEnabled =
                hubDialogue == null || !hubDialogue.IsOpen;
            sceneInput.interactable = sceneEnabled;
            sceneInput.blocksRaycasts = sceneEnabled;
            settingsButton.interactable = true;
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
                if (fromLaptop)
                    foreach (var button in scene.GetComponentsInChildren<Button>())
                        if (button.name == "LaptopHotspot" && button.IsInteractable())
                        { EventSystem.current.SetSelectedGameObject(button.gameObject); break; }
            }
        }

        private static void FocusFirst(Transform root)
        {
            if (!EventSystem.current) return;
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                if (!button.IsInteractable() || !button.isActiveAndEnabled) continue;
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                return;
            }
        }

        public void Escape()
        {
            if (transition || storageBlocked) return;
            if (hubDialogue != null && hubDialogue.IsOpen)
            {
                audio.Click();
                hubDialogue.Close();
                return;
            }
            if (panel == "laptop" && laptop.BackToDesktop()) { audio.Click(); return; }
            if (string.IsNullOrEmpty(panel)) OpenPanel("settings"); else { audio.Click(); ClosePanel(); }
        }

        public void FocusNext()
        {
            if (transition || storageBlocked || !EventSystem.current) return;
            if (hubDialogue != null && hubDialogue.IsOpen)
            {
                hubDialogue.FocusNextControl();
                return;
            }
            var buttons = stage.GetComponentsInChildren<Button>();
            int selected = -1;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].gameObject == EventSystem.current.currentSelectedGameObject) selected = i;
            for (int n = 1; n <= buttons.Length; n++)
            {
                var button = buttons[(selected + n) % buttons.Length];
                if (!button.IsInteractable() || !button.isActiveAndEnabled) continue;
                if (!string.IsNullOrEmpty(panel) && !button.transform.IsChildOf(panels)) continue;
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                break;
            }
        }

        public void Tick(float dt)
        {
            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            stage.localScale = new Vector3(scale, scale, 1);
            effects.Tick(dt, session.State.lampOn);
            home.Tick(dt);
            mission.Tick(dt, Paused);
            hubDialogue?.Tick(dt);
            if (panel == "laptop")
            {
                if (Time.frameCount != laptopOpenedFrame && Input.GetMouseButtonDown(0))
                    audio.LaptopMouseClick();
                laptop.Tick(dt);
            }
            messageNotifications.SetSuppressed(session.CombatMode == CombatMode.ReactiveTurns &&
                session.State.phase == RunPhase.Combat);
            messageNotifications.Tick(dt);
            if (toastTime > 0)
            {
                toastTime -= dt;
                if (toastTime <= 0) toast.text = "";
            }
        }

        public bool OpenGuildIntro()
        {
            return OpenHubDialogue(
                HubDialogueConfig.GuildIntro());
        }

        private bool OpenHubDialogue(HubDialogueDefinition definition)
        {
            if (definition == null || transition || storageBlocked ||
                !string.IsNullOrEmpty(panel) ||
                hubDialogue == null || hubDialogue.IsOpen)
                return false;

            toast.text = string.Empty;
            toastTime = 0f;
            return hubDialogue.Open(definition);
        }

        private void OpenHubMenu()
        {
            if (hubDialogue != null && hubDialogue.IsOpen)
                hubDialogue.Close();
            OpenPanel("settings");
        }

        private void SetSceneInteractionsEnabled(bool value)
        {
            bool enabled =
                value &&
                string.IsNullOrEmpty(panel) &&
                !transition &&
                !storageBlocked;
            sceneInput.interactable = enabled;
            sceneInput.blocksRaycasts = enabled;
        }

        private void ToastShort(string value) { Toast(value); }
        public void Toast(string value, float seconds = 4) { toast.text = value; toastTime = seconds; }
        public void SetSaveStatus(bool success) { saved.text = success ? "ПРОФИЛЬ СОХРАНЁН" : "ПРОФИЛЬ НЕ ЗАПИСАН"; saved.color = success ? UiKit.Muted : UiKit.Gold; }

        public void ShowStorageBlock(string message)
        {
            storageBlocked = true;
            ui.Clear(transitions);
            ui.Box(transitions, "StorageBlock", 0, 0, 1920, 1080, UiKit.Ink, true);
            ui.Label(transitions, "StorageTitle", "Профиль защищён", 360, 310, 1200, 110, 48, UiKit.Paper, true);
            ui.Label(transitions, "StorageMessage", message, 360, 440, 1200, 210, 27);
            ui.Button(transitions, "ExitProtectedProfile", "Закрыть игру", 360, 690, 360, 64, Application.Quit);
        }

        public void Dispose()
        {
            mission.ClearReferences();
            effects.Dispose();
            hubDialogue?.Dispose();
            messageNotifications.Dispose();
            session.Messages.Changed -= HandleMessageRoutingChanged;
            session.LiveMessages.Signal -= HandleLiveMessengerSignal;
            session.Changed -= Refresh;
            session.Combat.Hit -= OnHit;
            session.Combat.ActionResolved -= mission.OnAction;
        }
    }
}
