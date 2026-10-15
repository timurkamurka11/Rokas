using System;
using System.Collections;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rokas.Presentation
{
    /// <summary>
    /// ROKAS main-room (ScreenSpaceOverlay) sleep presentation.
    /// Uses two high-resolution authored 2D/2.5D POV shots, NOT a FPS
    /// camera, world-space bed, or procedural 3D hand/cube watch.
    /// Required Resources/BedSleep/BedCeilingPOV and BedWatchArmPOV are
    /// installed separately from the supplied visual reference package.
    /// </summary>
    public sealed class RokasBedSleepCinematic : IDisposable
    {
        public enum State
        {
            Idle, BedHotspotPressed, TransitionIn, LyingViewActive,
            AlarmButtonVisible, WatchRaise, TimeSelection,
            ConfirmSelection, HandLower, EyesClose, SleepCommitted
        }

        public const float MinimumHours = 1f;
        public const float MaximumHours = 12f;
        public const float StepHours = .5f;

        private readonly MonoBehaviour runner;
        private readonly UiKit ui;
        private readonly Transform layer;
        private readonly GameSession session;
        private readonly Action save;
        private readonly Action onFinished;
        private readonly Action<float> moveHomeCamera;
        private readonly Texture originalRoom;
        private RectTransform root;
        private RawImage lyingPov, armPov, blurredPov, real3DArmPov;
        private RokasBedSleepProductionRigBridge productionRig;
        private Image curtain, topEyelid, bottomEyelid;
        private CanvasGroup alarmControls, watchControls;
        private Text currentClock, selectedDuration, wakeClock, alarmHint;
        private Button alarmButton, confirmButton;
        private AudioSource audio;
        private AudioClip tickClip;
        private Coroutine sequence;
        private State state = State.Idle;
        private float selectedHours = 7f, startHours;
        private int startDay;
        private bool committed, cancelRequested, armWatchAvailable;
        private float elapsedVisual;
        private float handOpacity;

        public State CurrentState => state;
        public bool Active => state != State.Idle;
        public float SelectedHours => selectedHours;
        public float ArmRaise01 => handOpacity;

        public RokasBedSleepCinematic(MonoBehaviour runner, UiKit ui,
            Transform layer, Texture originalRoom,
            GameSession session, Action save, Action onFinished,
            Action<float> moveHomeCamera)
        {
            this.runner = runner;
            this.ui = ui;
            this.layer = layer;
            this.originalRoom = originalRoom;
            this.session = session;
            this.save = save;
            this.onFinished = onFinished;
            this.moveHomeCamera = moveHomeCamera;
        }

        public static float QuantizeDuration(float hours)
        {
            return Mathf.Clamp(Mathf.Round(hours / StepHours) * StepHours,
                MinimumHours, MaximumHours);
        }

        public static float WakeHour(float currentHour, float duration)
        {
            return Mathf.Repeat(currentHour + QuantizeDuration(duration), 24f);
        }

        public static int WakeDay(int day, float currentHour, float duration)
        {
            return Mathf.Max(1, day) +
                Mathf.FloorToInt((currentHour + QuantizeDuration(duration)) / 24f);
        }

        public static string ClockText(float hours)
        {
            int m = Mathf.RoundToInt(Mathf.Repeat(hours, 24f) * 60f) % 1440;
            return (m / 60).ToString("00") + ":" +
                (m % 60).ToString("00");
        }

        public void Begin()
        {
            if (Active || runner == null || session == null) return;
            state = State.BedHotspotPressed;
            cancelRequested = committed = false;
            selectedHours = 7f;
            startHours = session.State.hubSleepInitialized
                ? Mathf.Repeat(session.State.hubClockHours, 24f) : 23.5f;
            startDay = session.State.hubSleepInitialized
                ? Mathf.Max(1, session.State.hubDay) : 1;
            elapsedVisual = handOpacity = 0f;
            BuildOverlay();
            sequence = runner.StartCoroutine(Play());
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            elapsedVisual += Mathf.Max(0f, dt);
            // Very restrained breathing and cursor parallax in the authored
            // photographic plate; only a small movement, never a 3D camera.
            if (lyingPov != null)
            {
                float breath = Mathf.Sin(elapsedVisual * 1.42f);
                float pointerX = 0f, pointerY = 0f;
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                {
                    Vector2 m = Mouse.current.position.ReadValue();
                    pointerX = Mathf.Clamp((m.x / Mathf.Max(1, Screen.width) -
                        .5f) * 2f, -1f, 1f);
                    pointerY = Mathf.Clamp((m.y / Mathf.Max(1, Screen.height) -
                        .5f) * 2f, -1f, 1f);
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                Vector3 m = Input.mousePosition;
                pointerX = Mathf.Clamp((m.x / Mathf.Max(1, Screen.width) -
                    .5f) * 2f, -1f, 1f);
                pointerY = Mathf.Clamp((m.y / Mathf.Max(1, Screen.height) -
                    .5f) * 2f, -1f, 1f);
#endif
                lyingPov.rectTransform.localScale =
                    Vector3.one * (1.003f + .002f * breath);
                lyingPov.rectTransform.anchoredPosition =
                    new Vector2(-4f * pointerX, 4f * pointerY);
            }

            if (state != State.TimeSelection) return;
            float scroll = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                scroll = Mouse.current.scroll.ReadValue().y;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.upArrowKey.wasPressedThisFrame ||
                    Keyboard.current.rightArrowKey.wasPressedThisFrame) Step(1);
                if (Keyboard.current.downArrowKey.wasPressedThisFrame ||
                    Keyboard.current.leftArrowKey.wasPressedThisFrame) Step(-1);
                if (Keyboard.current.enterKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame) Confirm();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            scroll = Input.mouseScrollDelta.y;
            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.RightArrow)) Step(1);
            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.Space)) Confirm();
#endif
            if (scroll > .01f) Step(1);
            else if (scroll < -.01f) Step(-1);
        }

        public void Step(int direction)
        {
            if (state != State.TimeSelection || direction == 0) return;
            float value = QuantizeDuration(
                selectedHours + Mathf.Sign(direction) * StepHours);
            if (Mathf.Approximately(value, selectedHours)) return;
            selectedHours = value;
            RefreshWatch();
            if (productionRig != null && productionRig.Ready)
            {
                productionRig.SetDuration(selectedHours);
                productionRig.CrownStep(direction);
            }
            PlayTick();
        }

        public void Confirm()
        {
            if (state == State.TimeSelection && sequence == null)
                sequence = runner.StartCoroutine(FinishSleep());
        }

        public void Cancel()
        {
            if (!Active && root == null) return;
            cancelRequested = true;
            if (runner != null && sequence != null)
                runner.StopCoroutine(sequence);
            sequence = null;
            Complete();
        }

        public void Dispose() => Cancel();

        private void BuildOverlay()
        {
            root = ui.Rect(layer, "BedSleep_2_5D_PresentationRoot",
                0, 0, 1920, 1080);
            ui.Box(root, "SleepModalInputBlocker", 0, 0, 1920, 1080,
                new Color(0f, 0f, 0f, .001f), true);

            var ceiling = Resources.Load<Texture2D>(
                "BedSleep/BedCeilingPOV");
            var arm = Resources.Load<Texture2D>(
                "BedSleep/BedWatchArmPOV");
            var soft = Resources.Load<Texture2D>(
                "BedSleep/BedCeilingPOV_Soft");
            armWatchAvailable = ceiling != null && arm != null;
            if (!armWatchAvailable)
                Debug.LogWarning("[ROKAS BedSleep 2.5D] Missing reference " +
                    "Resources/BedSleep/BedCeilingPOV and/or BedWatchArmPOV. " +
                    "Install the Bed Sleep Reference Assets ZIP.");

            lyingPov = ui.Art(root, "LyingCeilingPOV_Approved",
                ceiling != null ? ceiling : originalRoom,
                0, 0, 1920, 1080);
            // Begin in the untouched home view, then swap under darkness.
            lyingPov.color = new Color(1f, 1f, 1f, 0f);
            blurredPov = soft == null ? null :
                ui.Art(root, "LyingCeilingPOV_SoftBlur",
                    soft, 0, 0, 1920, 1080);
            if (blurredPov != null)
                blurredPov.color = new Color(1f, 1f, 1f, 0f);
            armPov = ui.Art(root, "KeikoArmWatch_ApprovedComposite",
                arm != null ? arm : ceiling != null ? ceiling : originalRoom,
                0, 0, 1920, 1080);
            armPov.color = new Color(1f, 1f, 1f, 0f);

            // If the corrected V3 production asset package is installed,
            // place the REAL skinned 3D hand/watch above the ceiling plate.
            // Without it the previous photo presentation remains available.
            productionRig = RokasBedSleepProductionRigBridge.TryCreate(
                startHours, session.State.lampOn);
            if (productionRig != null && productionRig.Ready)
            {
                real3DArmPov = ui.Art(root,
                    "Keiko_ProductionV3_SkinnedArm_RenderTexture",
                    productionRig.Texture, 0, 0, 1920, 1080);
                real3DArmPov.color = new Color(1f, 1f, 1f, 0f);
                real3DArmPov.raycastTarget = false;
                armWatchAvailable = ceiling != null;
                Debug.Log("[ROKAS BedSleep V3] REAL 25-bone 3D arm and " +
                    "OLED watch composite active");
            }

            // Controls are deliberately staged: alarm button FIRST, then
            // arm and interactive OLED. No always-on bottom watch menu.
            alarmControls = ui.Rect(root, "Bottom_SetAlarm", 0, 0,
                1920, 1080).gameObject.AddComponent<CanvasGroup>();
            alarmControls.alpha = 0f;
            alarmControls.blocksRaycasts = false;
            alarmControls.interactable = false;
            alarmButton = ui.Button(alarmControls.transform, "SetAlarm",
                "ПОСТАВИТЬ БУДИЛЬНИК", 722, 941, 476, 75,
                OpenAlarm, true);
            if (!armWatchAvailable)
            {
                alarmButton.interactable = false;
                ui.Label(alarmControls.transform, "ReferenceAssetsRequired",
                    "НЕ НАЙДЕНЫ POV-АССЕТЫ: установите архив BedSleep",
                    585, 880, 820, 46, 19, UiKit.Red,
                    false, TextAnchor.MiddleCenter);
            }

            watchControls = ui.Rect(root, "Smartwatch_DiegeticControls",
                0, 0, 1920, 1080).gameObject.AddComponent<CanvasGroup>();
            watchControls.alpha = 0f;
            watchControls.blocksRaycasts = false;
            watchControls.interactable = false;

            // Interactive dark OLED centre is aligned to the watch screen
            // in the supplied 1672x940 reference; the photographic bezel
            // and ring remain visible.
            ui.Box(watchControls.transform, "LiveOledCentre",
                804, 652, 155, 104, new Color(.025f, .035f, .073f, .96f));
            selectedDuration = ui.Label(watchControls.transform,
                "WatchSleepDuration", "", 811, 664, 148, 58,
                39, UiKit.Paper, false, TextAnchor.MiddleCenter);
            wakeClock = ui.Label(watchControls.transform, "WatchAlarmTime",
                "", 810, 723, 150, 27, 15, UiKit.Jade,
                false, TextAnchor.MiddleCenter);
            var hit = ui.Box(watchControls.transform, "WatchCrownDragZone",
                757, 578, 227, 236,
                new Color(1f, 1f, 1f, .001f), true);
            var pointer = hit.gameObject.AddComponent<RokasBedWatchPointer>();
            pointer.Configure(Step);
            // Clock face itself takes mouse wheel/drag. Small external
            // affordances are secondary, not the primary selection UI.
            ui.Button(watchControls.transform, "WatchMinus", "−30м",
                675, 772, 130, 53, () => Step(-1));
            ui.Button(watchControls.transform, "WatchPlus", "+30м",
                1010, 772, 130, 53, () => Step(1));
            confirmButton = ui.Button(watchControls.transform,
                "WatchConfirm", "✓  УСТАНОВИТЬ", 806, 826, 259, 55,
                Confirm, true);
            currentClock = ui.Label(watchControls.transform,
                "CurrentGameTime", "", 739, 908, 460, 39,
                18, UiKit.Muted, false, TextAnchor.MiddleCenter);
            alarmHint = ui.Label(watchControls.transform,
                "WatchControlsHint",
                "КОЛЁСИКО / ПЕРЕТАСКИВАНИЕ  •  30 МИН",
                630, 958, 700, 33, 17, UiKit.Muted,
                false, TextAnchor.MiddleCenter);

            ui.Button(root, "ReturnFromBed", "ВСТАТЬ / ESC",
                1549, 65, 274, 59, Cancel);

            topEyelid = ui.Box(root, "BedSleepUpperEyelid",
                0, 0, 1920, 0, Color.black);
            bottomEyelid = ui.Box(root, "BedSleepLowerEyelid",
                0, 1080, 1920, 0, Color.black);
            curtain = ui.Box(root, "BedSleepLaptopStyleDarkCurtain",
                0, 0, 1920, 1080,
                new Color(.008f, .013f, .021f, 0f), false);
            // Transparent end-of-frame curtain never blocks the watch.
            curtain.raycastTarget = false;
            SetEyelids(0f);

            audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.volume = .30f;
            tickClip = BuildTick();
            RefreshWatch();
        }

        private IEnumerator Play()
        {
            state = State.TransitionIn;
            // Same dark-curtain handoff grammar as the laptop; no FPS
            // transform or Camera.main. Midpoint hides the art change.
            yield return FadeCurtain(0f, 1f, .60f);
            if (cancelRequested) yield break;
            lyingPov.color = Color.white;
            yield return FadeCurtain(1f, 0f, .65f);

            state = State.LyingViewActive;
            yield return Wait(.35f);
            state = State.AlarmButtonVisible;
            yield return FadeControls(alarmControls, 0f, 1f, .38f);
            alarmControls.blocksRaycasts = true;
            alarmControls.interactable = true;
            sequence = null;
        }

        private void OpenAlarm()
        {
            if (state != State.AlarmButtonVisible || !armWatchAvailable ||
                sequence != null) return;
            alarmControls.blocksRaycasts = false;
            alarmControls.interactable = false;
            sequence = runner.StartCoroutine(RaiseWatch());
        }

        private IEnumerator RaiseWatch()
        {
            state = State.WatchRaise;
            productionRig?.ChangeState("WatchRaise");
            yield return FadeControls(alarmControls, alarmControls.alpha, 0f,
                .25f);
            yield return FadeArm(0f, 1f, .82f);
            state = State.TimeSelection;
            productionRig?.ChangeState("TimeSelection");
            watchControls.alpha = 1f;
            watchControls.blocksRaycasts = true;
            watchControls.interactable = true;
            sequence = null;
        }

        private IEnumerator FinishSleep()
        {
            state = State.ConfirmSelection;
            productionRig?.Confirm();
            productionRig?.ChangeState("ConfirmSelection");
            watchControls.interactable = false;
            watchControls.blocksRaycasts = false;
            if (alarmHint) alarmHint.text =
                "БУДИЛЬНИК УСТАНОВЛЕН НА " +
                ClockText(WakeHour(startHours, selectedHours));
            yield return Wait(.36f);
            state = State.HandLower;
            productionRig?.ChangeState("HandLower");
            watchControls.alpha = 0f;
            yield return FadeArm(1f, 0f, .76f);
            yield return Wait(.30f);
            state = State.EyesClose;
            // An intentionally heavy partial blink precedes the last close.
            yield return FadeEyelids(0f, .45f, .46f);
            yield return Wait(.16f);
            yield return FadeEyelids(.45f, .20f, .38f);
            yield return FadeEyelids(.20f, 1f, 1.12f);
            state = State.SleepCommitted;
            // ROKAS remains the ONLY owner of the durable save.
            // V3's optional event must not create a parallel save path.
            if (!committed && !cancelRequested)
            {
                committed = true;
                session.State.hubDay = WakeDay(startDay, startHours,
                    selectedHours);
                session.State.hubClockHours = WakeHour(startHours,
                    selectedHours);
                session.State.hubFatigue = Mathf.Clamp01(
                    (session.State.hubSleepInitialized ?
                     session.State.hubFatigue : .65f) -
                     selectedHours / 8f);
                session.State.hubSleepCount++;
                session.State.hubSleepInitialized = true;
                save?.Invoke();
            }
            yield return Wait(.70f);
            yield return FadeEyelids(1f, 0f, 1.0f);
            Complete();
        }

        private IEnumerator FadeArm(float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Mathf.Max(
                Time.unscaledDeltaTime, .002f))
            {
                float u = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(t / duration));
                handOpacity = Mathf.Lerp(from, to, u);
                if (productionRig != null && productionRig.Ready)
                {
                    productionRig.Sample(handOpacity);
                    if (real3DArmPov != null)
                        real3DArmPov.color = new Color(1f, 1f, 1f,
                            Mathf.Clamp01(handOpacity * 4f));
                    armPov.color = new Color(1f, 1f, 1f, 0f);
                }
                else
                    armPov.color = new Color(1f, 1f, 1f, handOpacity);
                if (blurredPov != null)
                    blurredPov.color = new Color(1f, 1f, 1f,
                        .12f * handOpacity);
                yield return null;
            }
            handOpacity = to;
            if (productionRig != null && productionRig.Ready)
            {
                productionRig.Sample(to);
                if (real3DArmPov != null)
                    real3DArmPov.color = new Color(1f, 1f, 1f,
                        Mathf.Clamp01(to * 4f));
                armPov.color = new Color(1f, 1f, 1f, 0f);
            }
            else
                armPov.color = new Color(1f, 1f, 1f, to);
            if (blurredPov != null)
                blurredPov.color = new Color(1f, 1f, 1f, .12f * to);
        }

        private IEnumerator FadeControls(CanvasGroup group, float from,
            float to, float duration)
        {
            for (float t = 0; t < duration; t += Mathf.Max(
                Time.unscaledDeltaTime, .002f))
            {
                group.alpha = Mathf.Lerp(from, to,
                    Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01(t / duration)));
                yield return null;
            }
            group.alpha = to;
        }

        private IEnumerator FadeCurtain(float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Mathf.Max(
                Time.unscaledDeltaTime, .002f))
            {
                curtain.color = new Color(.008f, .013f, .021f,
                    Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01(t / duration))));
                yield return null;
            }
            curtain.color = new Color(.008f, .013f, .021f, to);
        }

        private IEnumerator FadeEyelids(float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Mathf.Max(
                Time.unscaledDeltaTime, .002f))
            {
                SetEyelids(Mathf.Lerp(from, to,
                    Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01(t / duration))));
                yield return null;
            }
            SetEyelids(to);
        }

        private void SetEyelids(float closure)
        {
            float height = 540f * Mathf.Clamp01(closure);
            if (topEyelid != null)
                topEyelid.rectTransform.sizeDelta = new Vector2(1920, height);
            if (bottomEyelid != null)
            {
                bottomEyelid.rectTransform.sizeDelta =
                    new Vector2(1920, height);
                bottomEyelid.rectTransform.anchoredPosition =
                    new Vector2(0f, -1080f + height);
            }
        }

        private void RefreshWatch()
        {
            if (selectedDuration != null)
                selectedDuration.text = selectedHours.ToString("0.#") + "ч";
            if (wakeClock != null)
                wakeClock.text = ClockText(WakeHour(startHours,
                    selectedHours));
            if (currentClock != null)
                currentClock.text = "СЕЙЧАС  " + ClockText(startHours) +
                    "   •   ПОДЪЁМ  " +
                    ClockText(WakeHour(startHours, selectedHours));
        }

        private void PlayTick()
        {
            if (audio == null || tickClip == null) return;
            audio.pitch = UnityEngine.Random.Range(.96f, 1.07f);
            audio.PlayOneShot(tickClip, .23f);
        }

        private static AudioClip BuildTick()
        {
            const int count = 600;
            const int sampleRate = 24000;
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * 1400f * t) *
                    Mathf.Exp(-t * 220f) * .22f;
            }
            var clip = AudioClip.Create("ROKAS_WatchCrown_TactileTick",
                count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Mathf.Max(
                Time.unscaledDeltaTime, .002f))
                yield return null;
        }

        private void Complete()
        {
            if (state == State.Idle && root == null) return;
            state = State.Idle;
            sequence = null;
            productionRig?.Dispose();
            productionRig = null;
            real3DArmPov = null;
            moveHomeCamera?.Invoke(0f);
            if (root != null)
            {
                root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(root.gameObject);
                root = null;
            }
            if (tickClip != null)
                UnityEngine.Object.Destroy(tickClip);
            tickClip = null;
            alarmControls = watchControls = null;
            alarmButton = confirmButton = null;
            armPov = lyingPov = blurredPov = null;
            onFinished?.Invoke();
        }
    }

    /// <summary>Receives native UI scroll and vertical drag on the watch.</summary>
    public sealed class RokasBedWatchPointer : MonoBehaviour,
        IDragHandler, IBeginDragHandler
    {
        private Action<int> step;
        private float accumulator;

        public void Configure(Action<int> callback) { step = callback; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            accumulator = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            accumulator += eventData.delta.y;
            while (Mathf.Abs(accumulator) >= 22f)
            {
                int dir = accumulator > 0f ? 1 : -1;
                step?.Invoke(dir);
                accumulator -= dir * 22f;
            }
        }
    }
}
