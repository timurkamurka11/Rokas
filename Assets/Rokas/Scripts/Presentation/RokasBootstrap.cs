using System;
using System.IO;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rokas.Presentation
{
    public sealed class RokasBootstrap : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        public RokasView View { get; private set; }
        private SaveStore store;
        private RokasAudio sound;
        private bool dirty;
        private bool saveBlocked;
        private bool focused = true;
        private float autosave;
        private RunPhase previousPhase;

        private void Start()
        {
            if (Session == null) Initialize(Path.Combine(Application.persistentDataPath, "Profile"));
        }

        // An explicit directory keeps profile ownership separate from the game and permits isolated fixtures.
        public void Initialize(string saveDirectory)
        {
            if (Session != null) return;
            var assets = Resources.Load<RokasAssets>("RokasAssets");
            if (!assets || !assets.IsComplete())
                throw new InvalidOperationException("ROKAS presentation assets are missing. Reimport the project and run Rokas/Validate Project.");
            store = new SaveStore(saveDirectory, new UnitySaveCodec());
            var loaded = store.Load();
            saveBlocked = loaded.Status == SaveLoadStatus.FutureVersion || loaded.Status == SaveLoadStatus.Corrupt;
            var state = loaded.Succeeded ? loaded.Data : new SaveData();
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
            View = new RokasView(this, assets, Session, sound, SaveNow);
            Session.Changed += OnChanged;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
            focused = Application.isFocused;
            ApplyDisplaySettings();

            if (saveBlocked)
            {
                View.ShowStorageBlock(loaded.Status == SaveLoadStatus.FutureVersion
                    ? "Это сохранение создано более новой версией ROKAS. Откройте проект актуальной версией игры. Файл сохранён без изменений."
                    : "Не удалось безопасно загрузить профиль. Основной файл и резервная копия сохранены без изменений. Проверьте папку профиля перед продолжением.");
            }
            else
            {
                if (loaded.Status == SaveLoadStatus.RecoveredBackup)
                    View.Toast("Профиль восстановлен из резервной копии.");
                if (loaded.Status == SaveLoadStatus.NotFound) SaveNow();
            }
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
            if (focused && !saveBlocked && !View.Paused) Session.Tick(Mathf.Min(Time.deltaTime, .1f));
            View.Tick(dt);
            sound.Tick(dt, focused);
            if (focused && Input.GetKeyDown(KeyCode.Escape)) View.Escape();
            if (focused && Input.GetKeyDown(KeyCode.Tab)) View.FocusNext();
            autosave += dt;
            if (dirty && autosave >= 10) SaveNow();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value && Session != null) SaveNow();
        }

        private void OnApplicationPause(bool value) { if (value) SaveNow(); }
        private void OnApplicationQuit() { SaveNow(); }
        private void OnDestroy()
        {
            if (Session != null) Session.Changed -= OnChanged;
            if (View != null) View.Dispose();
        }
    }
}
