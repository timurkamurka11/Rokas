using System;
using System.Collections;
using System.IO;
using Rokas.Core;
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
        private bool dirty;
        private bool saveBlocked;
        private bool focused = true;
        private bool startupPending;
        private bool enterWorldTransition;
        private float autosave;
        private RunPhase previousPhase;

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

            Session = new GameSession(state, contract);
            previousPhase = state.phase;
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

            startupPending = false;
            BuildPresentation();
            yield return null;

            yield return FadeCurtain(curtain, 1f, 0f, .28f);
            if (curtain) Destroy(curtain.gameObject);
            enterWorldTransition = false;
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
            if (Session == null || store == null || saveBlocked) return;
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

        private void Update()
        {
            if (Session == null || View == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (focused && Input.GetKeyDown(KeyCode.Escape)) View.Escape();
            if (focused && !saveBlocked)
                View.HandleCombatInput(Input.GetMouseButtonDown(1), Input.GetKeyDown(KeyCode.Space), Input.GetKeyDown(KeyCode.R));
            if (focused && !saveBlocked && !View.Paused && !View.CombatHitStop) Session.Tick(Mathf.Min(Time.deltaTime, .1f));
            View.Tick(dt);
            sound.Tick(dt, focused);

            if (focused && Input.GetKeyDown(KeyCode.Tab)) View.FocusNext();
            autosave += dt;
            if (dirty && autosave >= 10) SaveNow();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value && Session != null) { View?.CancelCombatInput(); SaveNow(); }
        }

        private void OnApplicationPause(bool value) { if (value) { View?.CancelCombatInput(); SaveNow(); } }
        private void OnApplicationQuit() { SaveNow(); }
        private void OnDestroy()
        {
            if (dirty) SaveNow();
            if (Session != null) Session.Changed -= OnChanged;
            if (mainMenu != null) mainMenu.Dispose();
            if (View != null) View.Dispose();
            if (sound != null) sound.Dispose();
        }
    }
}