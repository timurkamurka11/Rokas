using System;
using System.Collections;
using System.Reflection;
using Rokas.Core;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rokas.Presentation
{
    // Main-room bridge: ROKAS is a 1920x1080 ScreenSpaceOverlay game,
    // not a first-person walkable scene with a Camera.main/bed collider.
    // Render a separately lit three-dimensional watch + arm into a
    // transparent RenderTexture while the existing room remains a UI plane.
    // The detailed supplied SleepInteraction package may replace the watch
    // mesh at runtime if it has been imported into Unity.
    public sealed class RokasBedSleepCinematic : IDisposable
    {
        public enum State
        {
            Idle, LieDown, RaiseArm, ChooseDuration, LowerArm,
            FallingAsleep, DeepSleep, WakeUp
        }

        public const float MinimumHours = 1f;
        public const float MaximumHours = 12f;
        public const float StepHours = .5f;

        private readonly MonoBehaviour runner;
        private readonly UiKit ui;
        private readonly Transform canvasLayer;
        private readonly GameSession session;
        private readonly Action save;
        private readonly Action onFinished;
        private readonly Texture roomIllustration;
        private readonly Action<float> moveRoomCamera;
        private Coroutine routine;
        private RectTransform root;
        private RawImage povRoom;
        private CanvasGroup watchUi;
        private Text durationLabel, wakeLabel, clockLabel, phaseLabel;
        private Image upperEyelid, lowerEyelid;
        private RenderTexture renderTarget;
        private GameObject rigRoot;
        private GameObject armRoot;
        private Transform crown;
        private Camera renderCamera;
        private float armRaise, eyelid, selectedHours = 7f, startHours;
        private int startDay;
        private bool confirmed, cancelled, committed;
        private State state = State.Idle;

        public State CurrentState => state;
        public bool Active => state != State.Idle;
        public float SelectedHours => selectedHours;
        public float ArmRaise01 => armRaise;

        public RokasBedSleepCinematic(MonoBehaviour runner, UiKit ui,
            Transform canvasLayer, Texture roomIllustration,
            GameSession session, Action save, Action onFinished,
            Action<float> moveRoomCamera)
        {
            this.runner = runner;
            this.ui = ui;
            this.canvasLayer = canvasLayer;
            this.roomIllustration = roomIllustration;
            this.session = session;
            this.save = save;
            this.onFinished = onFinished;
            this.moveRoomCamera = moveRoomCamera;
        }

        public static float QuantizeDuration(float value)
        {
            return Mathf.Clamp(Mathf.Round(value / StepHours) * StepHours,
                MinimumHours, MaximumHours);
        }

        public static float WakeHour(float currentHour, float duration)
        {
            return Mathf.Repeat(currentHour + QuantizeDuration(duration), 24f);
        }

        public static int WakeDay(int day, float currentHour, float duration)
        {
            return Mathf.Max(1, day) +
                Mathf.FloorToInt((currentHour +
                    QuantizeDuration(duration)) / 24f);
        }

        public void Begin()
        {
            if (Active || runner == null || session == null) return;
            startHours = session.State.hubSleepInitialized
                ? Mathf.Repeat(session.State.hubClockHours, 24f) : 23.5f;
            startDay = session.State.hubSleepInitialized
                ? Mathf.Max(1, session.State.hubDay) : 1;
            selectedHours = 7f;
            committed = confirmed = cancelled = false;
            armRaise = eyelid = 0f;
            BuildOverlay();
            state = State.LieDown;
            routine = runner.StartCoroutine(Sequence());
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            if (state != State.ChooseDuration) return;
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
            if (state != State.ChooseDuration || direction == 0) return;
            float before = selectedHours;
            selectedHours = QuantizeDuration(
                selectedHours + Mathf.Sign(direction) * StepHours);
            if (Mathf.Approximately(before, selectedHours)) return;
            if (crown != null)
                crown.localRotation *= Quaternion.Euler(0f, 24f *
                    Mathf.Sign(direction), 0f);
            if (armRoot != null)
                armRoot.transform.localRotation = Quaternion.Euler(
                    -3f, 0f, Mathf.Sign(direction) * 1.8f);
            RefreshTexts();
        }

        public void Confirm()
        {
            if (state == State.ChooseDuration)
                confirmed = true;
        }

        public void Cancel()
        {
            if (!Active) return;
            // No game-time mutation occurs unless the sleep blackout is
            // committed. Cancel during a non-cancellable fade restores UI.
            cancelled = true;
            if (routine != null && runner != null) runner.StopCoroutine(routine);
            Complete();
        }

        public void Dispose() => Cancel();

        private void BuildOverlay()
        {
            root = ui.Rect(canvasLayer, "ROKAS_BedSleep_FPS_Cinematic",
                0, 0, 1920, 1080);
            ui.Box(root, "BedSleep_InputBlocker", 0, 0, 1920, 1080,
                new Color(.015f, .024f, .034f, .94f), true);

            // Draw actual room photo, not a stale hardcoded mock-up.
            povRoom = ui.Art(root, "LyingPOV_OriginalRoom",
                roomIllustration, 0, 0, 1920, 1080);
            povRoom.uvRect = new Rect(.08f, .38f, .84f, .62f);
            povRoom.color = new Color(.64f, .68f, .74f, .78f);
            ui.Box(root, "BedDarkTop", 0, 0, 1920, 120,
                new Color(.01f, .016f, .023f, .45f));
            ui.Box(root, "BedDarkBottom", 0, 880, 1920, 200,
                new Color(.01f, .016f, .023f, .68f));
            ui.Label(root, "POV_SleepCaption",
                "КЕЙКИ  /  ЛЕЧЬ СПАТЬ", 96, 65, 850, 60, 25, UiKit.Paper);

            Create3DArm();
            ui.Art(root, "KeikoArm3D_Render", renderTarget,
                410, 175, 1080, 730);
            watchUi = ui.Rect(root, "SleepWatchControls", 0, 0,
                1920, 1080).gameObject.AddComponent<CanvasGroup>();
            watchUi.alpha = 0f;
            watchUi.blocksRaycasts = false;
            ui.Box(watchUi.transform, "WatchInformationPanel",
                679, 713, 558, 202, new Color(.014f, .025f, .034f, .92f));
            clockLabel = ui.Label(watchUi.transform, "Now",
                "", 702, 727, 500, 32, 17, UiKit.Muted);
            durationLabel = ui.Label(watchUi.transform, "Duration",
                "", 699, 757, 500, 65, 44, UiKit.Paper);
            wakeLabel = ui.Label(watchUi.transform, "WakeTime",
                "", 702, 822, 500, 42, 22, UiKit.Jade);
            phaseLabel = ui.Label(watchUi.transform, "Help",
                "КОЛЕСО МЫШИ  /  СТРЕЛКИ  /  30 МИН",
                670, 918, 740, 42, 19, UiKit.Muted);
            ui.Button(watchUi.transform, "CrownDown", "− 30 МИН",
                520, 755, 160, 70, () => Step(-1));
            ui.Button(watchUi.transform, "CrownUp", "+ 30 МИН",
                1240, 755, 160, 70, () => Step(1));
            ui.Button(watchUi.transform, "SleepConfirm", "ЗАСНУТЬ",
                751, 966, 420, 66, Confirm, true);
            ui.Button(root, "SleepCancel", "ВСТАТЬ / НАЗАД",
                1490, 71, 334, 62, Cancel);

            upperEyelid = ui.Box(root, "UpperEyelid",
                0, 0, 1920, 540, Color.black);
            lowerEyelid = ui.Box(root, "LowerEyelid",
                0, 540, 1920, 540, Color.black);
            SetEyelids(0f);
            RefreshTexts();
        }

        private void Create3DArm()
        {
            const int visualLayer = 30; // isolated from existing UI/Combat
            rigRoot = new GameObject("ROKAS_Sleep_3D_FPSArm_Rig");
            GameObject cameraGo = new GameObject("SleepArm_RenderCamera");
            cameraGo.transform.SetParent(rigRoot.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -3.4f);
            renderCamera = cameraGo.AddComponent<Camera>();
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            renderCamera.cullingMask = 1 << visualLayer;
            renderCamera.orthographic = true;
            renderCamera.orthographicSize = 1.1f;
            renderCamera.nearClipPlane = .03f;
            renderCamera.farClipPlane = 20f;
            renderCamera.allowHDR = false;
            renderCamera.allowMSAA = true;
            renderTarget = new RenderTexture(1024, 768, 16,
                RenderTextureFormat.ARGB32);
            renderTarget.name = "ROKAS_Sleep_Transparent3DHand";
            renderTarget.Create();
            renderCamera.targetTexture = renderTarget;

            GameObject lightGo = new GameObject("WatchWarmSoftLight");
            lightGo.transform.SetParent(rigRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(-.9f, 1.3f, -1.4f);
            Light lamp = lightGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, .82f, .60f);
            lamp.range = 10f;
            lamp.intensity = 2f;
            lamp.cullingMask = 1 << visualLayer;

            armRoot = new GameObject("KeikoLeftArm_Animated");
            armRoot.transform.SetParent(rigRoot.transform, false);
            armRoot.transform.localPosition = new Vector3(-.1f, -1.7f, .65f);
            Color skin = new Color(.82f, .64f, .53f);
            Color cloth = new Color(.11f, .12f, .16f);
            Color titanium = new Color(.31f, .36f, .40f);
            Color black = new Color(.025f, .038f, .05f);
            CreatePart("LooseHomeSleeve", PrimitiveType.Capsule, cloth,
                new Vector3(0f, -.48f, .12f), new Vector3(.31f, .52f, .28f),
                armRoot.transform, visualLayer);
            CreatePart("Forearm", PrimitiveType.Capsule, skin,
                new Vector3(0f, -.17f, .03f), new Vector3(.22f, .30f, .20f),
                armRoot.transform, visualLayer);
            CreatePart("Palm", PrimitiveType.Capsule, skin,
                new Vector3(.025f, .16f, .02f), new Vector3(.25f, .22f, .17f),
                armRoot.transform, visualLayer);
            for (int i = 0; i < 4; i++)
                CreatePart("Finger" + i, PrimitiveType.Capsule, skin,
                    new Vector3(-.083f + .067f * i, .305f, -.013f),
                    new Vector3(.051f, .125f, .058f),
                    armRoot.transform, visualLayer);

            GameObject watch = CreatePart("ROKAS_Smartwatch_Housing",
                PrimitiveType.Cube, titanium,
                new Vector3(0f, -.035f, -.165f),
                new Vector3(.40f, .48f, .115f),
                armRoot.transform, visualLayer);
            CreatePart("TitaniumBezel", PrimitiveType.Cube,
                new Color(.54f, .55f, .55f), new Vector3(0f, -.035f, -.230f),
                new Vector3(.355f, .421f, .034f), watch.transform,
                visualLayer, true);
            CreatePart("OLEDScreen", PrimitiveType.Cube, black,
                new Vector3(0f, -.035f, -.257f),
                new Vector3(.327f, .398f, .014f), armRoot.transform,
                visualLayer);
            crown = CreatePart("Watch_DigitalCrown_Rotary",
                PrimitiveType.Cylinder, titanium,
                new Vector3(.235f, .07f, -.175f),
                new Vector3(.06f, .050f, .06f),
                armRoot.transform, visualLayer).transform;

            // If the supplied engineering archive is imported, use its
            // production procedural smartwatch mesh instead of this
            // zero-dependency preview. No hard assembly dependency.
            TryUseImportedHighDetailWatch();
        }

        private GameObject CreatePart(string name, PrimitiveType primitive,
            Color color, Vector3 position, Vector3 size, Transform parent,
            int layer, bool localToParent = false)
        {
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            var renderer = go.GetComponent<Renderer>();
            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Unlit/Color");
            if (shader)
            {
                var material = new Material(shader);
                material.color = color;
                if (material.HasProperty("_Glossiness"))
                    material.SetFloat("_Glossiness", .64f);
                renderer.material = material;
            }
            return go;
        }

        private void TryUseImportedHighDetailWatch()
        {
            // Runtime component is optional until the supplied package
            // has been installed locally in Assets.
            Type builder = Type.GetType(
                "ROKAS.Hub.SleepInteraction.ProceduralSmartwatchMeshBuilder, Assembly-CSharp");
            if (builder == null) return;
            try
            {
                var host = new GameObject("ImportedHighDetailSmartwatch");
                host.layer = 30;
                host.transform.SetParent(armRoot.transform, false);
                host.transform.localPosition = new Vector3(0f, -.02f, -.19f);
                host.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                host.transform.localScale = Vector3.one * 8f;
                Component instance = host.AddComponent(builder);
                MethodInfo method = builder.GetMethod(
                    "EnsureSmartwatchHierarchyBuilt");
                if (method == null) throw new MissingMethodException(
                    builder.FullName, "EnsureSmartwatchHierarchyBuilt");
                object[] args = { null, null, null };
                method.Invoke(instance, args);
                foreach (Transform t in host.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = 30;
                // Hide only the default watch geometry, never the arm.
                foreach (Renderer r in armRoot.GetComponentsInChildren<Renderer>())
                    if (r.transform != host.transform &&
                        !r.transform.IsChildOf(host.transform) &&
                        (r.name.StartsWith("ROKAS_Smartwatch_") ||
                         r.name.StartsWith("TitaniumBezel") ||
                         r.name.StartsWith("OLEDScreen") ||
                         r.name.StartsWith("Watch_DigitalCrown")))
                        r.enabled = false;
                if (args[0] is Transform generatedCrown)
                    crown = generatedCrown;
            }
            catch (Exception error)
            {
                Debug.LogWarning("ROKAS sleep: optional 3D smartwatch mesh " +
                    "not available; keep procedural preview. " + error.Message);
            }
        }

        private IEnumerator Sequence()
        {
            state = State.LieDown;
            yield return Animate(1.85f, t =>
            {
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                moveRoomCamera?.Invoke(smooth * .50f);
                if (povRoom) povRoom.uvRect = new Rect(
                    .0f + .08f * smooth, .0f + .38f * smooth,
                    1f - .16f * smooth, 1f - .38f * smooth);
                SetEyelids(Mathf.Sin(t * Mathf.PI) * .09f);
            });
            state = State.RaiseArm;
            yield return Animate(.95f, t =>
            {
                armRaise = Mathf.SmoothStep(0f, 1f, t);
                PoseArm();
                if (watchUi) watchUi.alpha = Mathf.Clamp01(
                    (armRaise - .70f) / .30f);
            });
            armRaise = 1f;
            PoseArm();
            state = State.ChooseDuration;
            watchUi.alpha = 1f;
            watchUi.blocksRaycasts = true;
            while (!confirmed && !cancelled)
                yield return null;
            if (cancelled) yield break;
            watchUi.blocksRaycasts = false;
            watchUi.alpha = 0f;
            state = State.LowerArm;
            yield return Animate(.85f, t =>
            {
                armRaise = 1f - Mathf.SmoothStep(0f, 1f, t);
                PoseArm();
            });
            state = State.FallingAsleep;
            yield return Animate(1.9f, t =>
            {
                float blink = Mathf.Sin(Mathf.Clamp01(t * 1.5f) *
                    Mathf.PI) * .13f;
                SetEyelids(Mathf.Max(t * t, blink));
            });
            SetEyelids(1f);
            state = State.DeepSleep;
            if (!committed)
            {
                committed = true;
                session.State.hubDay = WakeDay(startDay, startHours,
                    selectedHours);
                session.State.hubClockHours = WakeHour(startHours,
                    selectedHours);
                session.State.hubFatigue = Mathf.Clamp01(
                    (session.State.hubSleepInitialized
                        ? session.State.hubFatigue : .65f) -
                        selectedHours / 8f);
                session.State.hubSleepCount++;
                session.State.hubSleepInitialized = true;
                save?.Invoke();
            }
            yield return WaitRealtime(.85f);
            state = State.WakeUp;
            yield return Animate(1.65f, t =>
            {
                SetEyelids(1f - Mathf.SmoothStep(0f, 1f, t));
                moveRoomCamera?.Invoke(.50f * (1f-t));
            });
            Complete();
        }

        private IEnumerator Animate(float seconds, Action<float> evaluate)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .05f);
                evaluate?.Invoke(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            evaluate?.Invoke(1f);
        }

        private IEnumerator WaitRealtime(float duration)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Mathf.Min(Time.unscaledDeltaTime, .05f);
                yield return null;
            }
        }

        private void PoseArm()
        {
            if (!armRoot) return;
            float smooth = Mathf.SmoothStep(0f, 1f, armRaise);
            armRoot.transform.localPosition = Vector3.Lerp(
                new Vector3(-.1f, -1.7f, .65f),
                new Vector3(0f, -.12f, .65f), smooth);
            armRoot.transform.localRotation = Quaternion.Euler(
                5f * smooth, 0f, 0f);
        }

        private void SetEyelids(float value)
        {
            eyelid = Mathf.Clamp01(value);
            if (upperEyelid)
            {
                upperEyelid.rectTransform.sizeDelta =
                    new Vector2(1920, 540f * eyelid);
                upperEyelid.color = Color.black;
            }
            if (lowerEyelid)
            {
                lowerEyelid.rectTransform.sizeDelta =
                    new Vector2(1920, 540f * eyelid);
                lowerEyelid.rectTransform.anchoredPosition =
                    new Vector2(0, -(1080 - 540f * eyelid));
                lowerEyelid.color = Color.black;
            }
        }

        private void RefreshTexts()
        {
            if (durationLabel)
                durationLabel.text = selectedHours.ToString("00.0") + " ЧАСА";
            if (clockLabel)
                clockLabel.text = "СЕЙЧАС  " +
                    ClockText(startHours) + "  /  ДЕНЬ " + startDay;
            if (wakeLabel)
                wakeLabel.text = "ПРОБУЖДЕНИЕ  " +
                    ClockText(WakeHour(startHours, selectedHours)) +
                    "  /  ДЕНЬ " +
                    WakeDay(startDay, startHours, selectedHours);
        }

        public static string ClockText(float hours)
        {
            int minutes = Mathf.RoundToInt(Mathf.Repeat(hours,24f) * 60f)
                % 1440;
            return (minutes / 60).ToString("00") + ":" +
                (minutes % 60).ToString("00");
        }

        private void Complete()
        {
            if (!Active && root == null && rigRoot == null) return;
            state = State.Idle;
            routine = null;
            moveRoomCamera?.Invoke(0f);
            if (root != null)
            {
                root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(root.gameObject);
                root = null;
            }
            if (rigRoot != null)
            {
                foreach (Renderer renderer in
                    rigRoot.GetComponentsInChildren<Renderer>())
                    if (renderer.sharedMaterial != null)
                        UnityEngine.Object.Destroy(renderer.sharedMaterial);
                renderCamera = null;
                UnityEngine.Object.Destroy(rigRoot);
                rigRoot = null;
                armRoot = null;
            }
            if (renderTarget != null)
            {
                renderTarget.Release();
                UnityEngine.Object.Destroy(renderTarget);
                renderTarget = null;
            }
            onFinished?.Invoke();
        }
    }
}
