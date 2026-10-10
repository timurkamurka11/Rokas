using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Runs over the existing 1920x1080 UiKit stage; never mutates the Home background.
    // Missing POV / hand frames deliberately cause a safe fallback to the old laptop route.
    public sealed class LaptopCinematicSequence
    {
        public enum Phase { Idle, Approach, POVTransition, PreBootChoice, HandsInteraction, PowerOn, CancelBack, UIOpen }

        [Serializable]
        private sealed class HandManifest
        {
            public float fps = 24f;
            public string handedness;
            public HandFrame[] frames;
        }

        [Serializable]
        private sealed class ScreenCalibration
        {
            public float topLeftX = 596f, topLeftY = 413f;
            public float topRightX = 1044f, topRightY = 412f;
            public float bottomRightX = 1051f, bottomRightY = 676f;
            public float bottomLeftX = 586f, bottomLeftY = 676f;
            public float powerX = 1040f, powerY = 708f;
            public bool debugPowerAnchor;
        }

        [Serializable]
        private sealed class HandFrame
        {
            public string resource;
            public float x;
            public float y;
            public float w;
            public float h;
        }

        private const float StageWidth = 1920f;
        private const float StageHeight = 1080f;
        private const float ApproachEnd = 1.2f;
        private const float POVStart = 1.15f;
        private const float POVEnd = 1.8f;
        private const float HandsStart = 1.8f;
        private const float HandsEnd = 3.84f;
        private const float FinishTime = 3.92f;
        private const float ReturnDuration = .72f;

        private readonly UiKit ui;
        private readonly MonoBehaviour owner;
        private readonly RectTransform transitionLayer;
        private readonly RawImage mainBackground;
        private readonly Action openExistingLaptop;
        private readonly Action cancelled;
        private readonly Action powerClick;
        private readonly Action returningToRoom;

        private HandManifest manifest;
        private Texture2D[] frames;
        private Texture2D povTexture;
        private RectTransform root;
        private RawImage wideImage;
        private RawImage handsImage;
        private CanvasGroup povGroup;
        private LaptopWakeQuad wake;
        private Coroutine running;
        private float imageScale;
        private float imageX;
        private float imageY;
        private int shownFrame = -1;
        private bool active;
        private bool powerConfirmed;
        private bool returnRequested;
        private Text powerHint;
        private Text backHint;
        private RawImage powerHintArt;
        private RawImage backHintArt;
        private Button onScreenBack;
        private RectTransform powerGlowRoot;
        private Text noSignalText;
        private LaptopPowerTimeline powerTimeline;

        public Phase CurrentPhase { get; private set; }
        public bool IsPlaying => active;
        public bool IsAwaitingPowerChoice => active && CurrentPhase == Phase.PreBootChoice;

        public LaptopCinematicSequence(UiKit ui, MonoBehaviour owner, RectTransform transitionLayer,
            RawImage mainBackground, Action openExistingLaptop, Action cancelled,
            Action powerClick, Action returningToRoom)
        {
            this.ui = ui;
            this.owner = owner;
            this.transitionLayer = transitionLayer;
            this.mainBackground = mainBackground;
            this.openExistingLaptop = openExistingLaptop;
            this.cancelled = cancelled;
            this.powerClick = powerClick;
            this.returningToRoom = returningToRoom;
        }

        // False means the caller MUST invoke the existing OpenPanel("laptop") immediately.
        public bool TryStart()
        {
            if (active) return true;
            if (!owner || !transitionLayer || !mainBackground || !mainBackground.texture) return false;

            povTexture = Resources.Load<Texture2D>("LaptopCinematic/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF");
            var json = Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest");
            if (!povTexture || !json)
            {
                Debug.LogWarning("ROKAS-LAPTOP-CINEMATIC: realistic right-hand art unavailable; using existing laptop route.");
                return false;
            }
            if (povTexture.width != 1672 || povTexture.height != 941)
            {
                Debug.LogWarning("ROKAS-LAPTOP-CINEMATIC: POV NPOT texture dimensions changed; use NPOT None.");
                return false;
            }

            try { manifest = JsonUtility.FromJson<HandManifest>(json.text); }
            catch (Exception error)
            {
                Debug.LogWarning("ROKAS laptop cinematic: invalid hand manifest: " + error.Message);
                return false;
            }

            if (manifest == null || manifest.frames == null || manifest.frames.Length < 36 ||
                manifest.handedness != "right" || manifest.fps < 1f || manifest.fps > 120f)
            {
                Debug.LogWarning("ROKAS-LAPTOP-CINEMATIC: invalid right-only art manifest.");
                return false;
            }

            frames = new Texture2D[manifest.frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                HandFrame frame = manifest.frames[i];
                if (frame == null || string.IsNullOrEmpty(frame.resource))
                {
                    Debug.LogWarning("ROKAS-LAPTOP-CINEMATIC: invalid frame definition #" + i);
                    return false;
                }
                frames[i] = Resources.Load<Texture2D>(frame.resource);
                if (!frames[i] || frame.w <= 0f || frame.h <= 0f ||
                    frames[i].width != Mathf.RoundToInt(frame.w) ||
                    frames[i].height != Mathf.RoundToInt(frame.h))
                {
                    Debug.LogWarning("ROKAS-LAPTOP-CINEMATIC: frame missing or resized by importer: " + frame.resource);
                    return false;
                }
            }

            try
            {
                powerTimeline = new LaptopPowerTimeline();
                powerConfirmed = false;
                returnRequested = false;
                BuildOverlay();
                active = true;
                CurrentPhase = Phase.Approach;
                running = owner.StartCoroutine(Play());
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError("ROKAS laptop cinematic: failed to initialize overlay: " + error);
                Cleanup();
                return false;
            }
        }

        private void BuildOverlay()
        {
            root = ui.Rect(transitionLayer, "LaptopCinematicOverlay", 0f, 0f, StageWidth, StageHeight);
            // The overlay is also the input blocker, including during the crossfade.
            ui.Box(root, "OpaqueUnderlay", 0f, 0f, StageWidth, StageHeight, Color.black, true);
            wideImage = ui.Art(root, "OriginalHomeApproach", mainBackground.texture,
                0f, 0f, StageWidth, StageHeight);
            wideImage.uvRect = mainBackground.uvRect;

            RectTransform zoom = wideImage.rectTransform;
            zoom.pivot = new Vector2(.59f, .53f);
            zoom.anchoredPosition = new Vector2(StageWidth * .59f, -StageHeight * .47f);

            var povRoot = ui.Rect(root, "ApprovedPOV", 0f, 0f, StageWidth, StageHeight);
            povGroup = povRoot.gameObject.AddComponent<CanvasGroup>();
            povGroup.alpha = 0f;
            povGroup.blocksRaycasts = false;
            povGroup.interactable = false;

            imageScale = Mathf.Min(StageWidth / povTexture.width, StageHeight / povTexture.height);
            float imageWidth = povTexture.width * imageScale;
            float imageHeight = povTexture.height * imageScale;
            imageX = (StageWidth - imageWidth) * .5f;
            imageY = (StageHeight - imageHeight) * .5f;
            ui.Art(povRoot, "UnmodifiedScreenOffPOV", povTexture,
                imageX, imageY, imageWidth, imageHeight);

            // Calibration is separate from the approved art and can be edited as JSON.
            ScreenCalibration calibration = new ScreenCalibration();
            TextAsset calibrationText = Resources.Load<TextAsset>("LaptopCinematic/screen_calibration");
            if (calibrationText)
            {
                try { calibration = JsonUtility.FromJson<ScreenCalibration>(calibrationText.text)
                    ?? new ScreenCalibration(); }
                catch (Exception error)
                {
                    Debug.LogWarning("ROKAS laptop calibration ignored: " + error.Message);
                }
            }
            RectTransform screenRect = ui.Rect(povRoot, "LaptopWakePolygon",
                imageX, imageY, imageWidth, imageHeight);
            wake = screenRect.gameObject.AddComponent<LaptopWakeQuad>();
            wake.raycastTarget = false;
            wake.SetCorners(new[]
            {
                new Vector2(calibration.topLeftX, calibration.topLeftY),
                new Vector2(calibration.topRightX, calibration.topRightY),
                new Vector2(calibration.bottomRightX, calibration.bottomRightY),
                new Vector2(calibration.bottomLeftX, calibration.bottomLeftY)
            }, imageScale);
            wake.color = new Color(.16f, .34f, .48f, 0f);
            if (calibration.debugPowerAnchor && Debug.isDebugBuild)
                ui.Box(povRoot, "PowerAnchorDebug",
                    imageX + calibration.powerX * imageScale - 5f,
                    imageY + calibration.powerY * imageScale - 5f,
                    10f, 10f, Color.red);

            handsImage = ui.Art(povRoot, "HandAnimationAlphaFrame",
                frames[0], 0f, 0f, 1f, 1f);
            handsImage.gameObject.SetActive(false);
            handsImage.raycastTarget = false;

            // One physical backlit Power key. A small concentric halo keeps the
            // keyboard readable without coloring the bezel or entire desk.
            float keyX = imageX + calibration.powerX * imageScale;
            float keyY = imageY + calibration.powerY * imageScale;
            powerGlowRoot = ui.Rect(povRoot, "PowerKeyBlueStandby", keyX - 18f, keyY - 18f, 36f, 36f);
            ui.Box(powerGlowRoot, "KeyOuterHalo", 0f, 0f, 36f, 36f,
                new Color(.02f, .22f, .82f, .055f));
            ui.Box(powerGlowRoot, "KeyInnerHalo", 12f, 12f, 12f, 12f,
                new Color(.13f, .58f, 1f, .24f));
            ui.Box(powerGlowRoot, "KeyLED", 15f, 15f, 6f, 6f,
                new Color(.24f, .72f, 1f, .85f));

            // UI hit target over the calibrated physical Power key, not a fake
            // full-screen confirmation button. E remains the primary shortcut.
            var hit = ui.Rect(povRoot, "PowerKeyClickTarget", keyX - 26f, keyY - 27f, 52f, 54f);
            var target = hit.gameObject.AddComponent<Image>();
            target.color = new Color(0f, 0f, 0f, .002f);
            var button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => TryPressPower());

            float centerX = (calibration.topLeftX + calibration.topRightX
                + calibration.bottomLeftX + calibration.bottomRightX) * .25f;
            float centerY = (calibration.topLeftY + calibration.topRightY
                + calibration.bottomLeftY + calibration.bottomRightY) * .25f;
            noSignalText = ui.Label(povRoot, "StandbyNoSignal", "NO SIGNAL",
                imageX + (centerX - 130f) * imageScale,
                imageY + (centerY - 11f) * imageScale,
                260f * imageScale, 24f * imageScale, 14,
                new Color(.37f, .59f, .77f, .33f), false, TextAnchor.MiddleCenter);
            // Load the three EXACT approved transparent user PNGs when installed.
            // If V3.2 art was not installed, preserve readable V3.1 text fallbacks.
            Texture2D onArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_power");
            Texture2D exitArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_back");
            Texture2D arrowArt = Resources.Load<Texture2D>("LaptopCinematic/UI/back_arrow");
            if (onArt)
            {
                float w = 390f;
                powerHintArt = ui.Art(povRoot, "PowerChoiceHint", onArt, 570f, 933f,
                    w, w * (float)onArt.height / onArt.width);
                powerHintArt.raycastTarget = false;
            }
            else
                powerHint = ui.Label(povRoot, "PowerChoiceHint", "E  —  ВКЛЮЧИТЬ НОУТБУК",
                    575f, 955f, 475f, 44f, 20, new Color(.8f, .85f, .9f, .88f));
            if (exitArt)
            {
                float w = 390f;
                backHintArt = ui.Art(povRoot, "BackChoiceHint", exitArt, 1045f, 933f,
                    w, w * (float)exitArt.height / exitArt.width);
                backHintArt.raycastTarget = false;
            }
            else
                backHint = ui.Label(povRoot, "BackChoiceHint", "ESC  —  НАЗАД",
                    1080f, 955f, 320f, 44f, 20, new Color(.8f, .85f, .9f, .88f));
            if (arrowArt)
            {
                float iconSize = 82f;
                RawImage arrow = ui.Art(povRoot, "LaptopBackChoice", arrowArt,
                    80f, 916f, iconSize, iconSize * (float)arrowArt.height / arrowArt.width);
                arrow.raycastTarget = true;
                onScreenBack = arrow.gameObject.AddComponent<Button>();
                onScreenBack.targetGraphic = arrow;
                onScreenBack.transition = Selectable.Transition.ColorTint;
                onScreenBack.onClick.AddListener(() => BackToRoom());
            }
            else
                onScreenBack = ui.Button(povRoot, "LaptopBackChoice", "←",
                    92f, 915f, 72f, 58f, () => BackToRoom(), playClickSound: false);
            SetPromptVisibility(false);
            SetPromptVisibility(false);
        }

        // The initial camera movement is followed by an INDEFINITE player choice.
        // No finger motion or power audio may happen until confirmed.
        private IEnumerator Play()
        {
            float elapsed = 0f;
            while (active && elapsed < HandsStart)
            {
                RenderAt(elapsed);
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .06f);
            }
            if (!active) yield break;
            RenderAt(HandsStart);
            CurrentPhase = Phase.PreBootChoice;
            SetPromptVisibility(true);
            while (active && !powerConfirmed && !returnRequested)
            {
                RenderAt(HandsStart);
                yield return null;
            }
            if (!active) yield break;
            SetPromptVisibility(false);
            if (returnRequested)
            {
                CurrentPhase = Phase.CancelBack;
                float exitElapsed = 0f;
                while (active && exitElapsed < ReturnDuration)
                {
                    // Gently reverse the camera blend; never start LaptopBoot.
                    RenderAt(Mathf.Lerp(HandsStart, 0f,
                        Mathf.SmoothStep(0f, 1f, exitElapsed / ReturnDuration)));
                    CurrentPhase = Phase.CancelBack;
                    yield return null;
                    exitElapsed += Mathf.Min(Time.unscaledDeltaTime, .06f);
                }
                running = null;
                if (active) Finish(false);
                yield break;
            }

            // User confirmed E/Power. Timeline keeps contact and one click
            // frame-rate-independent; press animation begins only now.
            elapsed = HandsStart;
            while (active && elapsed < FinishTime)
            {
                RenderAt(elapsed);
                if (powerTimeline != null && powerTimeline.Advance(elapsed))
                    powerClick?.Invoke();
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .06f);
            }
            running = null;
            if (active) Finish(true);
        }

        private void SetPromptVisibility(bool show)
        {
            if (powerHint) powerHint.gameObject.SetActive(show);
            if (backHint) backHint.gameObject.SetActive(show);
            if (powerHintArt) powerHintArt.gameObject.SetActive(show);
            if (backHintArt) backHintArt.gameObject.SetActive(show);
            if (onScreenBack) onScreenBack.gameObject.SetActive(show);
            if (powerGlowRoot) powerGlowRoot.gameObject.SetActive(show);
            if (noSignalText) noSignalText.gameObject.SetActive(show);
        }

        public bool TryPressPower()
        {
            if (!IsAwaitingPowerChoice || powerConfirmed || returnRequested) return false;
            powerConfirmed = true;
            SetPromptVisibility(false);
            return true;
        }

        public bool BackToRoom()
        {
            if (!IsAwaitingPowerChoice || returnRequested || powerConfirmed) return false;
            returnRequested = true;
            returningToRoom?.Invoke();
            SetPromptVisibility(false);
            return true;
        }

        private void RenderAt(float t)
        {
            if (!root) { Finish(true); return; }

            float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / ApproachEnd));
            wideImage.rectTransform.localScale = Vector3.one * (1f + .065f * ease);
            povGroup.alpha = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(POVStart, POVEnd, t));

            if (t < POVStart) CurrentPhase = Phase.Approach;
            else if (t < POVEnd) CurrentPhase = Phase.POVTransition;
            else if (returnRequested) CurrentPhase = Phase.CancelBack;
            else if (!powerConfirmed) CurrentPhase = Phase.PreBootChoice;
            else if (t < LaptopPowerTimeline.ContactTime) CurrentPhase = Phase.HandsInteraction;
            else CurrentPhase = Phase.PowerOn;

            bool showHands = powerConfirmed && t >= HandsStart && t <= HandsEnd;
            handsImage.gameObject.SetActive(showHands);
            if (showHands)
            {
                int index = Mathf.Clamp(
                    Mathf.FloorToInt((t - HandsStart) * manifest.fps),
                    0, frames.Length - 1);
                if (index != shownFrame)
                {
                    HandFrame frame = manifest.frames[index];
                    handsImage.texture = frames[index];
                    RectTransform r = handsImage.rectTransform;
                    r.anchoredPosition = new Vector2(
                        imageX + frame.x * imageScale, -(imageY + frame.y * imageScale));
                    r.sizeDelta = new Vector2(frame.w * imageScale, frame.h * imageScale);
                    shownFrame = index;
                }
            }
            if (wake)
            {
                Color tint = wake.color;
                var wakeStage = LaptopPowerTimeline.StageAt(t);
                if (wakeStage == LaptopPowerTimeline.WakeStage.Off)
                    tint.a = powerConfirmed ? 0f : .042f; // subtle standby LCD in NO SIGNAL state
                else if (wakeStage == LaptopPowerTimeline.WakeStage.Glow)
                    tint.a = Mathf.Lerp(.12f, .31f,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LaptopPowerTimeline.ContactTime, LaptopPowerTimeline.GlowEnd, t)));
                else tint.a = .17f; // subtle powered-on LCD backlight, not dead black
                wake.color = tint;
            }
        }

        // Never open boot/YOMI before the real calibrated fingertip contact.
        // Esc while the hand is reaching cannot silently bypass the Power press.
        public void SkipToLaptop()
        {
            if (active && CurrentPhase == Phase.PowerOn && powerTimeline != null)
                Finish(true);
        }

        // Called on scene disposal, game phase changes, or a protected-save overlay.
        public void Cancel()
        {
            if (active) Finish(false);
        }

        private void Finish(bool shouldOpenLaptop)
        {
            if (!active) return;
            active = false; // Guard callbacks and out-of-order coroutine completion.
            powerTimeline?.Cancel();
            CurrentPhase = shouldOpenLaptop ? Phase.UIOpen : Phase.Idle;
            if (running != null && owner) owner.StopCoroutine(running);
            running = null;
            Cleanup();
            if (shouldOpenLaptop) openExistingLaptop?.Invoke();
            else cancelled?.Invoke();
        }

        private void Cleanup()
        {
            if (root)
            {
                root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(root.gameObject);
                root = null;
            }
            manifest = null;
            frames = null;
            povTexture = null;
            wideImage = null;
            handsImage = null;
            wake = null;
            povGroup = null;
            shownFrame = -1;
            powerConfirmed = false;
            returnRequested = false;
            powerHint = null;
            backHint = null;
            powerHintArt = null;
            backHintArt = null;
            onScreenBack = null;
            powerGlowRoot = null;
            noSignalText = null;
            powerTimeline = null;
        }
    }

    // Screen wake is drawn on a four-corner mesh; the approved bitmap is never modified.
    public sealed class LaptopWakeQuad : MaskableGraphic
    {
        private Vector2[] corners;
        private float factor = 1f;

        public void SetCorners(Vector2[] imagePixels, float scale)
        {
            if (imagePixels == null || imagePixels.Length != 4)
                throw new ArgumentException("Exactly four screen corners are required.");
            corners = (Vector2[])imagePixels.Clone();
            factor = scale;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (corners == null || corners.Length != 4) return;
            for (int i = 0; i < 4; i++)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(corners[i].x * factor,
                    -corners[i].y * factor, 0f);
                vertex.color = color;
                vertex.uv0 = new Vector2(i == 1 || i == 2 ? 1f : 0f,
                    i >= 2 ? 0f : 1f);
                vh.AddVert(vertex);
            }
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }
    }
}
