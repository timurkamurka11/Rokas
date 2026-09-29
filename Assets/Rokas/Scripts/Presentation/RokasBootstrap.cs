using System;
using System.Collections;
using System.IO;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class RokasBootstrap : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        public RokasView View { get; private set; }
        public VideoSequencePresenter VideoPresenter { get; private set; }
        public bool VideoTransitionsEnabled { get; private set; }
        private SaveStore store;
        private RokasAudio sound;
        private SaveLoadResult initialLoad;
        private MainMenuView mainMenu;
        private IVnIntroProgress vnIntroProgress;
        private RokasVnRuntimeLauncher runtimeVnLauncher;
        private RokasVnRuntimeIntroPackage runtimeVnIntroPackageOverride;
        private bool vnIntroHomeTransition;
        private bool dirty;
        private bool saveBlocked;
        private bool focused = true;
        private bool startupPending;
        private bool enterWorldTransition;
        private float autosave;
        private RunPhase previousPhase;
        private ReactiveInputAdapter reactiveInput;
        private ReactiveCombatSession reactiveBoundCombat;
        private CombatClock reactiveClock;
        private long lastReactiveDeviceUs;
        private long reactivePrepareUntilUs;
        private bool reactiveApproachHold;
        private bool reactiveMotionLocked;

        private void Start()
        {
            if (!Application.isEditor && Debug.isDebugBuild &&
                RokasVideoSmokeRunner.IsRequested(Environment.GetCommandLineArgs()))
            {
                if (!GetComponent<RokasVideoSmokeRunner>()) gameObject.AddComponent<RokasVideoSmokeRunner>();
                return;
            }

            if (Session != null || View != null || startupPending) return;
            VideoTransitionsEnabled = true;
            string saveDirectory = Path.Combine(Application.persistentDataPath, "Profile");
            PrepareRuntime(saveDirectory);
            startupPending = true;
            VideoPresenter.PlayStartup("StartupPreview.mp4", sound.VideoVolume, CompleteStartup);
        }

        // An explicit directory keeps profile ownership separate from the game and permits isolated fixtures.
        // Existing fixtures stay synchronous and video-free unless a focused video test opts in explicitly.
        public void Initialize(string saveDirectory)
        {
            Initialize(saveDirectory, false);
        }

        public void Initialize(string saveDirectory, bool enableVideoTransitions)
        {
            if (Session != null) return;
            VideoTransitionsEnabled = enableVideoTransitions;
            PrepareRuntime(saveDirectory);
            BuildPresentation();
        }

        private void PrepareRuntime(string saveDirectory)
        {
            if (Session != null) return;
            EnsureVideoPresenter();
            var assets = Resources.Load<RokasAssets>("RokasAssets");
            if (!assets || !assets.IsComplete())
                throw new InvalidOperationException("ROKAS presentation assets are missing. Reimport the project and run Rokas/Validate Project.");
            store = new SaveStore(saveDirectory, new UnitySaveCodec());
            initialLoad = store.Load();
            saveBlocked = initialLoad.Status == SaveLoadStatus.FutureVersion || initialLoad.Status == SaveLoadStatus.Corrupt;
            var state = initialLoad.Succeeded ? initialLoad.Data : new SaveData();
            var contract = JsonUtility.FromJson<ContractDefinition>(assets.contract.text);
            if (contract == null || string.IsNullOrEmpty(contract.id) || contract.enemyHealth <= 0)
                throw new InvalidOperationException("ROKAS contract data is invalid.");
            // Unknown content must not be silently mapped to a different contract.
            if (!string.IsNullOrEmpty(state.activeContractId) && state.activeContractId != contract.id)
                saveBlocked = true;

            Session = new GameSession(state, contract, store);
            reactiveInput = new ReactiveInputAdapter();
            previousPhase = state.phase;
            vnIntroProgress = new PlayerPrefsVnIntroProgress();
            var cameraRoot = new GameObject("RokasCamera", typeof(Camera), typeof(AudioListener));
            cameraRoot.transform.SetParent(transform, false);
            var camera = cameraRoot.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.cullingMask = 0;
            if (!EventSystem.current)
            {
                var events = new GameObject("RokasEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
            sound = new RokasAudio(gameObject, assets, state.settings);
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
            focused = Application.isFocused;
        }

        private void CompleteStartup()
        {
            if (!startupPending || View != null) return;
            VideoPresenter.PlayStoryIntro("StoryIntro.mp4", sound.VideoVolume, CompleteStoryIntro);
        }

        private void CompleteStoryIntro()
        {
            if (!startupPending || View != null || mainMenu != null) return;
            var assets = Resources.Load<RokasAssets>("RokasAssets");
            mainMenu = MainMenuView.Create(transform, assets, sound.VideoVolume, PlayUiClick, EnterWorld);
        }

        private void EnterWorld()
        {
            if (!startupPending || View != null || enterWorldTransition) return;
            enterWorldTransition = true;
            sound.EnterGame();
            StartCoroutine(EnterWorldRoutine());
        }

        private IEnumerator EnterWorldRoutine()
        {
            CanvasGroup curtain = CreateEnterWorldCurtain();
            yield return FadeCurtain(curtain, 0f, 1f, .24f);

            MainMenuView menu = mainMenu;
            mainMenu = null;
            if (menu != null) menu.Dispose();
            yield return null;

            if (vnIntroProgress != null && !vnIntroProgress.IsCompleted)
            {
                BuildVnIntro();
                yield return null;
                yield return FadeCurtain(curtain, 1f, 0f, .28f);
                if (curtain) Destroy(curtain.gameObject);
                enterWorldTransition = false;
                yield break;
            }

            startupPending = false;
            BuildPresentation();
            yield return null;

            yield return FadeCurtain(curtain, 1f, 0f, .28f);
            if (curtain) Destroy(curtain.gameObject);
            enterWorldTransition = false;
        }

        private void BuildVnIntro()
        {
            if (runtimeVnLauncher != null && runtimeVnLauncher.HasActivePlayer)
            {
                return;
            }

            RokasVnRuntimeIntroPackage package = runtimeVnIntroPackageOverride;
            if (!package)
            {
                package = Resources.Load<RokasVnRuntimeIntroPackage>(
                    RokasVnRuntimeIntroPackage.ResourcesPath);
            }
            if (!package)
            {
                throw new InvalidOperationException(
                    "ROKAS packaged runtime VN intro is missing. " +
                    "Export the authored Scene Composer project with " +
                    "ROKAS/VN Scene Composer/Обновить runtime intro.");
            }

            if (runtimeVnLauncher == null)
            {
                runtimeVnLauncher = new RokasVnRuntimeLauncher(transform);
            }
            if (!runtimeVnLauncher.TryPlay(package, CompleteIntroAndGoHome))
            {
                throw new InvalidOperationException(
                    "ROKAS runtime VN launcher refused duplicate intro playback.");
            }
        }

        private void CompleteIntroAndGoHome()
        {
            if (vnIntroHomeTransition || View != null)
            {
                return;
            }
            vnIntroHomeTransition = true;
            StartCoroutine(CompleteIntroAndGoHomeRoutine());
        }

        private IEnumerator CompleteIntroAndGoHomeRoutine()
        {
            while (enterWorldTransition)
            {
                yield return null;
            }

            CanvasGroup curtain = CreateEnterWorldCurtain();
            yield return FadeCurtain(curtain, 0f, 1f, .24f);

            DisposeVnIntroRuntime();
            startupPending = false;
            BuildPresentation();

            if (View == null)
            {
                vnIntroHomeTransition = false;
                throw new InvalidOperationException("ROKAS could not create Home after the VN intro. Intro completion was not persisted.");
            }

            // Home is now alive synchronously. Persist completion in the same
            // handoff frame so observers can never see Home with an unfinished intro.
            vnIntroProgress?.MarkCompleted();
            yield return null;

            yield return FadeCurtain(curtain, 1f, 0f, .28f);
            if (curtain) Destroy(curtain.gameObject);

            TryOpenHubGuildIntroAfterVn();
            vnIntroHomeTransition = false;
        }

        private bool TryOpenHubGuildIntroAfterVn()
        {
            bool replayRequested =
                PlayerPrefsVnIntroProgress.HubGuildIntroReplayRequested;
            if (saveBlocked || Session == null || View == null ||
                (Session.State.hubGuildIntroSeen && !replayRequested))
                return false;
            if (!View.OpenGuildIntro())
                return false;

            if (!Session.State.hubGuildIntroSeen)
            {
                if (!Session.MarkHubGuildIntroSeen())
                    return false;
                SaveNow();
            }

            if (replayRequested)
                PlayerPrefsVnIntroProgress.ConsumeHubGuildIntroReplay();
            return true;
        }

        private void DisposeVnIntroRuntime()
        {
            if (sound != null && sound.VnMuted)
            {
                sound.SetVnMuted(false);
            }
            if (runtimeVnLauncher != null)
            {
                runtimeVnLauncher.Dispose();
                runtimeVnLauncher = null;
            }
        }

        private CanvasGroup CreateEnterWorldCurtain()
        {
            var curtainObject = new GameObject("EnterWorldCurtain", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            curtainObject.transform.SetParent(transform, false);
            var rect = (RectTransform)curtainObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var canvas = curtainObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = curtainObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            var black = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var blackRect = (RectTransform)black.transform;
            blackRect.SetParent(rect, false);
            blackRect.anchorMin = Vector2.zero;
            blackRect.anchorMax = Vector2.one;
            blackRect.offsetMin = Vector2.zero;
            blackRect.offsetMax = Vector2.zero;
            var image = black.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            CanvasGroup curtain = curtainObject.GetComponent<CanvasGroup>();
            curtain.alpha = 0f;
            curtain.interactable = false;
            curtain.blocksRaycasts = true;
            return curtain;
        }

        private static IEnumerator FadeCurtain(CanvasGroup curtain, float from, float to, float duration)
        {
            float elapsed = 0f;
            curtain.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .05f);
                curtain.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            curtain.alpha = to;
        }

        private void BuildPresentation()
        {
            if (View != null || Session == null || sound == null) return;
            var assets = Resources.Load<RokasAssets>("RokasAssets");
            View = new RokasView(this, assets, Session, sound, SaveNow);
            Session.Changed += OnChanged;
            ApplyDisplaySettings();

            if (saveBlocked)
            {
                View.ShowStorageBlock(initialLoad != null && initialLoad.Status == SaveLoadStatus.FutureVersion
                    ? "Это сохранение создано более новой версией ROKAS. Откройте проект актуальной версией игры. Файл сохранён без изменений."
                    : "Не удалось безопасно загрузить профиль. Основной файл и резервная копия сохранены без изменений. Проверьте папку профиля перед продолжением.");
            }
            else
            {
                if (initialLoad != null && initialLoad.Status == SaveLoadStatus.RecoveredBackup)
                    View.Toast("Профиль восстановлен из резервной копии.");
                if (initialLoad != null && initialLoad.Status == SaveLoadStatus.NotFound) SaveNow();
            }
        }

        private void EnsureVideoPresenter()
        {
            if (!VideoPresenter) VideoPresenter = gameObject.GetComponent<VideoSequencePresenter>();
            if (!VideoPresenter) VideoPresenter = gameObject.AddComponent<VideoSequencePresenter>();
        }

        private void OnChanged()
        {
            dirty = true;
            if (Session.State.phase != previousPhase)
            {
                previousPhase = Session.State.phase;
                SaveNow();
            }
        }

        public void SaveNow()
        {
            if (Session == null || store == null || saveBlocked || Session.SaveBlocked) return;
            if (Session.CombatMode == CombatMode.ReactiveTurns && Session.State.phase == RunPhase.Combat)
                return; // Active reactive combat writes through the durable battle checkpoint transaction.
            var result = store.Save(Session.State);
            if (result.Succeeded)
            {
                dirty = false;
                autosave = 0;
                if (View != null) View.SetSaveStatus(true);
            }
            else
            {
                dirty = true;
                if (View != null)
                {
                    View.SetSaveStatus(false);
                    View.Toast("Не удалось записать профиль. Проверьте свободное место и доступ к папке сохранений.", 8);
                }
            }
        }

        public void PlayUiClick()
        {
            if (sound != null) sound.Click();
        }

        public void ApplyDisplaySettings()
        {
            if (Session == null) return;
            // Respect the Editor's Game view; change the window mode only in a standalone player.
            if (!Application.isEditor)
                Screen.fullScreenMode = Session.State.settings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        public void SubmitReactiveCommand(CommandKind kind, string skillId)
        {
            if (!CanUseReactiveCombat() || Session.ReactiveCombat.Phase != ReactivePhase.PlayerCommand) return;
            ReactiveCombatSession combat = Session.ReactiveCombat;
            string[] targets = new string[0];
            if (kind == CommandKind.Basic || kind == CommandKind.Skill && skillId != "sweep")
            {
                string selected = combat.SelectedTargetId;
                bool valid = false;
                for (int i = 0; i < combat.ActiveEnemyIds.Count; i++)
                    if (combat.ActiveEnemyIds[i] == selected) { valid = true; break; }
                if (!valid && combat.ActiveEnemyIds.Count > 0) selected = combat.ActiveEnemyIds[0];
                if (string.IsNullOrEmpty(selected)) return;
                targets = new[] { selected };
            }
            var intent = new CommandIntent(Guid.NewGuid().ToString("N"), Session.ReactiveCombat.Revision,
                kind, skillId, targets);
            CommandResult result = Session.ReactiveCombat.SubmitCommand(intent);
            if (!result.Accepted)
                View.Toast(result.Reason == "InsufficientAp" ? "Недостаточно AP." : "Сейчас действие недоступно.");
            else if (reactiveClock != null && !reactiveClock.IsPaused &&
                (kind == CommandKind.Basic || kind == CommandKind.Skill &&
                    (skillId == "seal_strike" || skillId == "sweep" || skillId == "heavy")))
            {
                string approachTarget = targets.Length > 0 ? targets[0] : combat.SelectedTargetId;
                if (string.IsNullOrEmpty(approachTarget) && combat.ActiveEnemyIds.Count > 0)
                    approachTarget = combat.ActiveEnemyIds[0];
                if (View.StartHunterApproach(approachTarget, skillId == "heavy"))
                {
                    // Core accepts the command now. Its authored 0.2 s contact begins when
                    // the visual approach reaches the target, without changing combat rules.
                    reactiveClock.Pause(reactiveInput.DeviceNowUs);
                    reactiveApproachHold = true;
                    reactiveMotionLocked = true;
                    reactiveInput.SetContext(false);
                }
            }
            View.RefreshReactiveCombat();
        }

        public void SelectReactiveTarget(string actorId)
        {
            if (!CanUseReactiveCombat() || Session.ReactiveCombat.Phase != ReactivePhase.PlayerCommand) return;
            if (Session.ReactiveCombat.SelectTarget(actorId))
            {
                Session.SaveReactiveCheckpoint();
                View.RefreshReactiveCombat();
            }
        }

        public void SubmitReactiveDefense(ReactivePressKind kind)
        {
            if (!CanUseReactiveCombat() || reactiveClock == null || reactiveClock.IsPaused) return;
            ReactiveCombatSession combat = Session.ReactiveCombat;
            long deviceUs;
            if (!reactiveInput.TryConsumeUiActivation(out deviceUs)) return;
            long? combatUs = reactiveClock.MapInput(deviceUs, reactiveClock.CurrentEpoch);
            if (!combatUs.HasValue) return;
            DefenseKind defense = kind == ReactivePressKind.Dodge ? DefenseKind.Dodge : DefenseKind.Parry;
            DefenseAttempt attempt = combat.SubmitDefense(new DefenseIntent("ui-" + Guid.NewGuid().ToString("N"),
                combat.InputEpoch, defense, combatUs.Value));
            View.PresentReactiveDefenseAttempt(attempt, combatUs.Value);
            combat.ReleaseDefense(defense, combat.InputEpoch);
            View.RefreshReactiveCombat();
        }

        public void ConfirmReactiveCounter()
        {
            if (!CanUseReactiveCombat() || reactiveClock == null || reactiveClock.IsPaused) return;
            ReactiveCombatSession combat = Session.ReactiveCombat;
            long deviceUs;
            if (!reactiveInput.TryConsumeUiActivation(out deviceUs)) return;
            long? combatUs = reactiveClock.MapInput(deviceUs, reactiveClock.CurrentEpoch);
            if (!combatUs.HasValue) return;
            combat.ConfirmCounter("counter-" + Guid.NewGuid().ToString("N"), combat.InputEpoch, combatUs.Value);
            View.RefreshReactiveCombat();
        }

        public void RetryReactiveSave()
        {
            if (Session == null || !Session.SaveBlocked || saveBlocked) return;
            bool saved = Session.RetryBlockedSave();
            View?.SetSaveStatus(saved);
            View?.RefreshReactiveCombat();
            if (!saved) View?.Toast("Не удалось записать профиль. Проверьте доступ к папке сохранений.", 8);
        }

        private bool CanUseReactiveCombat()
        {
            return Session != null && View != null && reactiveInput != null && focused && !saveBlocked &&
                !Session.SaveBlocked && !View.Paused && Session.State.phase == RunPhase.Combat &&
                Session.CombatMode == CombatMode.ReactiveTurns && Session.ReactiveCombat != null;
        }

        private void ClearReactiveBinding()
        {
            reactiveInput?.SetContext(false);
            reactiveBoundCombat = null;
            reactiveClock = null;
            lastReactiveDeviceUs = 0;
            reactivePrepareUntilUs = 0;
            reactiveApproachHold = false;
            reactiveMotionLocked = false;
        }

        private void PauseReactiveCombat(string reason)
        {
            if (reactiveClock == null || reactiveBoundCombat == null) return;
            long now = reactiveInput.DeviceNowUs;
            if (!reactiveClock.IsPaused) reactiveClock.Pause(now);
            if (reactiveBoundCombat.Phase != ReactivePhase.Suspended)
            {
                reactiveBoundCombat.Suspend(reason);
                reactiveInput.SetContext(false);
                reactiveInput.ForceRearm();
                reactivePrepareUntilUs = 0;
            }
            lastReactiveDeviceUs = now;
        }

        private void TickReactiveCombat()
        {
            ReactiveCombatSession combat = Session.ReactiveCombat;
            if (combat == null || reactiveInput == null) return;
            long now = reactiveInput.DeviceNowUs;
            if (reactiveBoundCombat != combat)
            {
                reactiveInput.SetContext(false);
                reactiveBoundCombat = combat;
                reactiveClock = new CombatClock(now, combat.CurrentCombatUs);
                lastReactiveDeviceUs = now;
                reactivePrepareUntilUs = 0;
                reactiveApproachHold = false;
                reactiveMotionLocked = false;
            }

            if (!focused || saveBlocked || Session.SaveBlocked || View.Paused)
            {
                PauseReactiveCombat(Session.SaveBlocked ? "SaveBlocked" : "Paused");
                View.RefreshReactiveCombat();
                return;
            }

            if (reactiveApproachHold)
            {
                if (!View.HunterApproachComplete)
                {
                    reactiveInput.SetContext(false);
                    lastReactiveDeviceUs = now;
                    View.RefreshReactiveCombat();
                    return;
                }
                reactiveApproachHold = false;
                reactiveMotionLocked = false;
                View.SetReactivePresentationLocked(false);
                if (combat.Phase == ReactivePhase.Suspended)
                    reactivePrepareUntilUs = checked(now + 600000);
                else
                {
                    reactiveClock.Resume(now);
                    reactiveInput.ForceRearm();
                }
                lastReactiveDeviceUs = now;
            }
            if (reactiveMotionLocked && View.HunterAtHome)
            {
                reactiveMotionLocked = false;
                View.SetReactivePresentationLocked(false);
            }

            if (reactiveClock.IsPaused)
            {
                if (reactivePrepareUntilUs == 0) reactivePrepareUntilUs = checked(now + 600000);
                if (now < reactivePrepareUntilUs)
                {
                    View.RefreshReactiveCombat();
                    return;
                }
                reactiveClock.Resume(now);
                combat.Resume(reactiveClock.CurrentEpoch);
                reactiveInput.ForceRearm();
                reactivePrepareUntilUs = 0;
                lastReactiveDeviceUs = now;
            }

            if (now - lastReactiveDeviceUs > 100000)
            {
                reactiveClock.Pause(lastReactiveDeviceUs);
                combat.Suspend("FrameGap");
                reactiveInput.SetContext(false);
                reactiveInput.ForceRearm();
                reactivePrepareUntilUs = checked(now + 600000);
                lastReactiveDeviceUs = now;
                View.RefreshReactiveCombat();
                return;
            }
            lastReactiveDeviceUs = now;

            bool defenseContext = combat.Phase == ReactivePhase.EnemyExecution;
            bool offenseContext = combat.CurrentPlayerSkillId == "heavy";
            bool reactiveInputContext = defenseContext || offenseContext ||
                combat.Phase == ReactivePhase.CounterWindow;
            reactiveInput.SetContext(reactiveInputContext);
            reactiveInput.SetDefenseEnabled(defenseContext);
            reactiveInput.SetOffenseEnabled(offenseContext);
            reactiveInput.Tick();
            reactiveInput.Flush(SubmitReactiveDevicePress);
            reactiveInput.FlushOffense(SubmitReactiveOffensePress);
            if (defenseContext)
            {
                if (reactiveInput.IsReleased(ReactivePressKind.Dodge))
                    combat.ReleaseDefense(DefenseKind.Dodge, combat.InputEpoch);
                if (reactiveInput.IsReleased(ReactivePressKind.Parry))
                    combat.ReleaseDefense(DefenseKind.Parry, combat.InputEpoch);
            }

            long combatNowUs = reactiveClock.CombatTimeAt(now);
            CombatStep step = combat.Advance(combatNowUs, combatNowUs - 40000);
            View.PresentReactiveCombatStep(step);
            foreach (CombatEvent combatEvent in step.Events)
            {
                if (combatEvent.Kind != CombatEventKind.ActionSettled ||
                    combatEvent.ActorId != ReactiveDuelDefinitions.HunterId || View.HunterAtHome) continue;
                reactiveMotionLocked = true;
                View.SetReactivePresentationLocked(true);
            }
            defenseContext = combat.Phase == ReactivePhase.EnemyExecution;
            offenseContext = combat.CurrentPlayerSkillId == "heavy";
            reactiveInput.SetContext(defenseContext || offenseContext ||
                combat.Phase == ReactivePhase.CounterWindow);
            reactiveInput.SetDefenseEnabled(defenseContext);
            reactiveInput.SetOffenseEnabled(offenseContext);

            BattleCheckpoint stable = combat.GetStableCheckpoint();
            if (Session.State.battleCheckpoint == null || stable.revision > Session.State.battleCheckpoint.revision)
                Session.SaveReactiveCheckpoint();
            View.RefreshReactiveCombat();
        }

        private void SubmitReactiveDevicePress(ReactiveDevicePress press)
        {
            if (press.Epoch != reactiveInput.Epoch || reactiveClock == null || reactiveClock.IsPaused ||
                reactiveBoundCombat == null || reactiveBoundCombat.Phase != ReactivePhase.EnemyExecution) return;
            long? combatUs = reactiveClock.MapInput(press.DeviceTimeUs, reactiveClock.CurrentEpoch);
            if (!combatUs.HasValue) return;
            DefenseKind kind = press.Kind == ReactivePressKind.Dodge ? DefenseKind.Dodge : DefenseKind.Parry;
            DefenseAttempt attempt = reactiveBoundCombat.SubmitDefense(new DefenseIntent("device-" + press.InputId,
                reactiveBoundCombat.InputEpoch, kind, combatUs.Value));
            View.PresentReactiveDefenseAttempt(attempt, combatUs.Value);
        }

        private void SubmitReactiveOffensePress(ReactiveOffensePress press)
        {
            if (press.Epoch != reactiveInput.Epoch || reactiveClock == null || reactiveClock.IsPaused ||
                reactiveBoundCombat == null || reactiveBoundCombat.CurrentPlayerSkillId != "heavy") return;
            long? combatUs = reactiveClock.MapInput(press.DeviceTimeUs, reactiveClock.CurrentEpoch);
            if (!combatUs.HasValue) return;
            reactiveBoundCombat.SubmitOffenseTiming("offense-" + press.InputId,
                reactiveBoundCombat.InputEpoch, combatUs.Value);
        }

        private void Update()
        {
            if (Session == null || View == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (focused && Input.GetKeyDown(KeyCode.Escape)) View.Escape();
            if (Session.CombatMode == CombatMode.ReactiveTurns && Session.State.phase == RunPhase.Combat)
                TickReactiveCombat();
            else
                ClearReactiveBinding();
            if (focused && !saveBlocked && Session.CombatMode == CombatMode.Legacy)
                View.HandleCombatInput(Input.GetMouseButtonDown(1), Input.GetKeyDown(KeyCode.Space), Input.GetKeyDown(KeyCode.R));
            if (focused && !saveBlocked && !View.Paused && !View.CombatHitStop) Session.Tick(Mathf.Min(Time.deltaTime, .1f));
            // A frame-gap/focus preparation pause also freezes the arena's visual clock.
            // The intentional approach/windup hold must keep moving until its gate opens.
            bool presentationCanAdvance = focused &&
                (reactiveClock == null || !reactiveClock.IsPaused || reactiveApproachHold);
            View.Tick(dt, presentationCanAdvance);
            sound.Tick(dt, focused);

            if (focused && Input.GetKeyDown(KeyCode.Tab)) View.FocusNext();
            autosave += dt;
            if (dirty && autosave >= 10) SaveNow();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value && Session != null) { PauseReactiveCombat("FocusLost"); View?.CancelCombatInput(); SaveNow(); }
        }

        private void OnApplicationPause(bool value)
        {
            if (value) { PauseReactiveCombat("ApplicationPause"); View?.CancelCombatInput(); SaveNow(); }
        }
        private void OnApplicationQuit() { SaveNow(); }
        private void OnDestroy()
        {
            if (dirty) SaveNow();
            if (Session != null) Session.Changed -= OnChanged;
            if (mainMenu != null) mainMenu.Dispose();
            DisposeVnIntroRuntime();
            if (View != null) View.Dispose();
            if (reactiveInput != null) reactiveInput.Dispose();
            if (sound != null) sound.Dispose();
        }
    }
}
