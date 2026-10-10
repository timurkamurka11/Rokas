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
        private readonly Func<int> unreadMessages;
        private readonly Action<float> updateRoomCamera;
        private readonly Texture yomiWallpaper;

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
        private bool returningFromLaptop;
        private bool pendingVisualHandoff;
        private Text powerHint;
        private Text backHint;
        private RawImage powerHintArt;
        private RawImage backHintArt;
        private Button onScreenBack;
        private RectTransform powerGlowRoot;
        private Text noSignalText;
        private RectTransform noSignalScreen;
        private RawImage physicalMiniYomiImage;
        private RectTransform physicalMiniYomiViewport;
        private LaptopNoSignalBounce noSignalBounce;
        private LaptopPowerTimeline powerTimeline;
        private LaptopPerspectiveQuad poweredWallpaper;
        private LaptopPhysicalDesktopClock desktopClock;
        private Button poweredDesktopClick;
        private RectTransform poweredOpenRoot;
        private Text poweredOpenHint;

        public Phase CurrentPhase { get; private set; }
        public bool IsPlaying => active;
        public bool HasPendingVisualHandoff => pendingVisualHandoff;
        // The previous approved POV stays drawn until the first REAL YOMI
        // frame has been presented, never exposing the underlying Hub.
        public void CompleteVisualHandoff()
        {
            // V11.1: preserve the actual seated photo UNDER the YOMI window
            // for its entire lifetime (including exterior margins).
            if (pendingVisualHandoff && povGroup) povGroup.alpha = 1f;
        }
        public bool IsAwaitingPowerChoice => active && CurrentPhase == Phase.PreBootChoice;

        public LaptopCinematicSequence(UiKit ui, MonoBehaviour owner, RectTransform transitionLayer,
            RawImage mainBackground, Action openExistingLaptop, Action cancelled,
            Action powerClick, Action returningToRoom, Func<int> unreadMessages = null,
            Action<float> updateRoomCamera = null, Texture yomiWallpaper = null)
        {
            this.ui = ui;
            this.owner = owner;
            this.transitionLayer = transitionLayer;
            this.mainBackground = mainBackground;
            this.openExistingLaptop = openExistingLaptop;
            this.cancelled = cancelled;
            this.powerClick = powerClick;
            this.returningToRoom = returningToRoom;
            this.unreadMessages = unreadMessages;
            this.updateRoomCamera = updateRoomCamera;
            this.yomiWallpaper = yomiWallpaper;
        }

        // False means the caller MUST invoke the existing OpenPanel("laptop") immediately.
        public bool TryStart(bool resumeFromLaptop = false)
        {
            if (active) return false;
            if (resumeFromLaptop && pendingVisualHandoff && root && owner)
            {
                // Re-use the exact same approved POV; no hub or new image.
                pendingVisualHandoff = false;
                active = true;
                powerConfirmed = false;
                returnRequested = false;
                returningFromLaptop = true;
                if (povGroup) povGroup.alpha = 1f;
                CurrentPhase = Phase.PreBootChoice;
                running = owner.StartCoroutine(Play());
                return true;
            }
            if (pendingVisualHandoff) return false;
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
                returningFromLaptop = resumeFromLaptop;
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
            // Transparent blocker: the ACTUAL animated Home view remains alive
            // under the camera settle. Never replace CustomGlow/idle lines with
            // a stale background-only screenshot (reported in user gameplay).
            ui.Box(root, "TransparentInputBlocker", 0f, 0f,
                StageWidth, StageHeight, Color.clear, true);
            wideImage = ui.Art(root, "OriginalHomeApproach", mainBackground.texture,
                0f, 0f, StageWidth, StageHeight);
            wideImage.gameObject.SetActive(false); // not a render source
            wideImage.uvRect = mainBackground.uvRect;

            RectTransform zoom = wideImage.rectTransform;
            zoom.pivot = new Vector2(.59f, .53f);
            zoom.anchoredPosition = new Vector2(StageWidth * .59f, -StageHeight * .47f);

            var povRoot = ui.Rect(root, "ApprovedPOV", 0f, 0f, StageWidth, StageHeight);
            povGroup = povRoot.gameObject.AddComponent<CanvasGroup>();
            povGroup.alpha = 0f;
            povGroup.blocksRaycasts = false;
            povGroup.interactable = false;

            // Cover the entire frame, plus slight overscan to eliminate edge/door seams.
            imageScale = Mathf.Max(StageWidth / povTexture.width, StageHeight / povTexture.height) * 1.012f;
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

            // Physical LCD is fully live (YOMI's real app catalog + unread
            // session state + real OS clock). No screenshot of a desktop is used.
            // The approved OFF-state POV and screen corners stay unchanged.
            Vector2[] lcdCorners =
            {
                new Vector2(calibration.topLeftX, calibration.topLeftY),
                new Vector2(calibration.topRightX, calibration.topRightY),
                new Vector2(calibration.bottomRightX, calibration.bottomRightY),
                new Vector2(calibration.bottomLeftX, calibration.bottomLeftY)
            };
            RectTransform desktopRect = ui.Rect(povRoot, "LaptopPhysicalDesktop",
                imageX, imageY, imageWidth, imageHeight);
            poweredWallpaper = desktopRect.gameObject.AddComponent<LaptopPerspectiveQuad>();
            poweredWallpaper.SetCorners(lcdCorners, imageScale);
            poweredWallpaper.SetImage(null); // live atlas is the sole ON-state image
            poweredWallpaper.color = new Color(.08f, .16f, .27f, 1f);
            poweredWallpaper.raycastTarget = false;
            RectTransform liveClockRect = ui.Rect(povRoot, "PhysicalDesktopLiveClock",
                imageX, imageY, imageWidth, imageHeight);
            desktopClock = liveClockRect.gameObject.AddComponent<LaptopPhysicalDesktopClock>();
            desktopClock.SetCorners(lcdCorners, imageScale);
            desktopClock.raycastTarget = false;
            desktopClock.Initialize(unreadMessages, yomiWallpaper);
            RectTransform lcdHit = ui.Rect(povRoot, "PhysicalDesktopClickTarget",
                imageX + calibration.topLeftX * imageScale,
                imageY + calibration.topLeftY * imageScale,
                (calibration.topRightX - calibration.topLeftX) * imageScale,
                (calibration.bottomLeftY - calibration.topLeftY) * imageScale);
            var lcdTouch = lcdHit.gameObject.AddComponent<Image>();
            lcdTouch.color = new Color(0f, 0f, 0f, .002f);
            poweredDesktopClick = lcdHit.gameObject.AddComponent<Button>();
            poweredDesktopClick.targetGraphic = lcdTouch;
            poweredDesktopClick.transition = Selectable.Transition.None;
            poweredDesktopClick.onClick.AddListener(() => TryPressPower());

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
            // The underlying custom four-corner LCD mesh is retained, but
            // uGUI RawImage + RectMask2D supplies a dependable *visible* screen
            // compositor even on graphics configurations where Mesh UI vanished.
            // Inset from the calibrated polygon so no edge leaks onto the bezel.
            float lcdX = imageX + (calibration.topLeftX + 10f) * imageScale;
            float lcdY = imageY + (calibration.topLeftY + 10f) * imageScale;
            float lcdW = (calibration.topRightX - calibration.topLeftX - 20f) * imageScale;
            float lcdH = (calibration.bottomLeftY - calibration.topLeftY - 20f) * imageScale;
            lcdW = Mathf.Max(80f, lcdW);
            lcdH = Mathf.Max(48f, lcdH);
            noSignalScreen = ui.Rect(povRoot, "ROKASNoSignalScreen", lcdX, lcdY, lcdW, lcdH);
            var unpoweredPanel = noSignalScreen.gameObject.AddComponent<Image>();
            unpoweredPanel.color = new Color(.003f, .007f, .015f, 1f);
            unpoweredPanel.raycastTarget = false;
            noSignalScreen.gameObject.AddComponent<RectMask2D>();
            Texture2D chibi = Resources.Load<Texture2D>("LaptopCinematic/UI/rokas_no_signal_chibi");
            if (chibi)
            {
                float w = Mathf.Min(lcdW * .34f, lcdH * .55f);
                var sticker = ui.Art(noSignalScreen, "ROKASNoSignalChibiDVD",
                    chibi, 10f, 10f, w, w * chibi.height / chibi.width);
                sticker.color = Color.white;
                sticker.raycastTarget = false;
                noSignalBounce = noSignalScreen.gameObject.AddComponent<LaptopNoSignalBounce>();
                noSignalBounce.Initialize(noSignalScreen, sticker.rectTransform);
            }
            else
            {
                Debug.LogWarning("ROKAS V11.2: chibi sticker resource missing; use fallback NO SIGNAL until art is installed.");
            }
            // Retain a readable fallback for incomplete resource installations.
            noSignalText = ui.Label(noSignalScreen, "StandbyNoSignal", "NO SIGNAL",
                10f, lcdH * .42f, lcdW - 20f, 30f, 18,
                new Color(.70f, .82f, 1f, .92f), false, TextAnchor.MiddleCenter);
            noSignalText.gameObject.SetActive(!chibi);

            // Display the SAME runtime YOMI atlas as a passive miniature.
            // It stays within an inset calibrated screen mask and never
            // opens fullscreen YOMI without an explicit E/click action.
            physicalMiniYomiViewport = ui.Rect(povRoot, "PhysicalMiniYomiViewport",
                lcdX, lcdY, lcdW, lcdH);
            var miniMaskBackground = physicalMiniYomiViewport.gameObject.AddComponent<Image>();
            miniMaskBackground.color = new Color(.01f, .025f, .05f, 1f);
            miniMaskBackground.raycastTarget = false;
            physicalMiniYomiViewport.gameObject.AddComponent<RectMask2D>();
            physicalMiniYomiImage = ui.Art(physicalMiniYomiViewport,
                "PhysicalMiniYomiVisible", desktopClock.mainTexture,
                0f, 0f, lcdW, lcdH);
            physicalMiniYomiImage.color = Color.white;
            physicalMiniYomiImage.raycastTarget = false;
            // Exact same nine vector glyphs as fullscreen LaptopView, drawn
            // directly on the mini LCD. Not generic placeholder symbols.
            float miniScaleX = lcdW / 640f;
            float miniScaleY = lcdH / 360f;
            for (int i = 0; i < LaptopView.DesktopAppCount; i++)
            {
                int tileX = 18 + (i % 4) * 152;
                int tileY = 58 + (i / 4) * 86;
                RectTransform iconRect = ui.Rect(physicalMiniYomiViewport,
                    "PhysicalMiniYomiAppIcon_" + i,
                    (tileX + 7) * miniScaleX, (tileY + 18) * miniScaleY,
                    34f * miniScaleX, 38f * miniScaleY);
                iconRect.gameObject.AddComponent<CanvasRenderer>();
                var glyph = iconRect.gameObject.AddComponent<LaptopIcon>();
                glyph.Glyph = (LaptopGlyph)i;
                glyph.color = Color.white;
                glyph.raycastTarget = false;
            }
            noSignalScreen.gameObject.SetActive(false);
            physicalMiniYomiViewport.gameObject.SetActive(false);
            // Load the three EXACT approved transparent user PNGs when installed.
            // If V3.2 art was not installed, preserve readable V3.1 text fallbacks.
            Texture2D onArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_power");
            Texture2D exitArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_back");
            Texture2D arrowArt = Resources.Load<Texture2D>("LaptopCinematic/UI/back_arrow");
            if (onArt)
            {
                float w = 535f;
                powerHintArt = ui.Art(povRoot, "PowerChoiceHint", onArt, 514f, 870f,
                    w, w * (float)onArt.height / onArt.width);
                powerHintArt.color = Color.white; // full original PNG intensity
                powerHintArt.raycastTarget = true;
                var startButton = powerHintArt.gameObject.AddComponent<Button>();
                startButton.transition = Selectable.Transition.None;
                startButton.onClick.AddListener(() => TryPressPower());
                powerHintArt.gameObject.AddComponent<LaptopChoiceHover>();
            }
            else
                powerHint = ui.Label(povRoot, "PowerChoiceHint", "E  —  ВКЛЮЧИТЬ НОУТБУК",
                    575f, 955f, 475f, 44f, 20, new Color(.8f, .85f, .9f, .88f));
            if (exitArt)
            {
                float w = 460f;
                backHintArt = ui.Art(povRoot, "BackChoiceHint", exitArt, 1110f, 887f,
                    w, w * (float)exitArt.height / exitArt.width);
                backHintArt.color = Color.white;
                backHintArt.raycastTarget = true;
                var leaveButton = backHintArt.gameObject.AddComponent<Button>();
                leaveButton.transition = Selectable.Transition.None;
                leaveButton.onClick.AddListener(() => BackToRoom());
                backHintArt.gameObject.AddComponent<LaptopChoiceHover>();
            }
            else
                backHint = ui.Label(povRoot, "BackChoiceHint", "ESC  —  НАЗАД",
                    1080f, 955f, 320f, 44f, 20, new Color(.8f, .85f, .9f, .88f));
            if (arrowArt)
            {
                float iconSize = 82f;
                RawImage arrow = ui.Art(povRoot, "LaptopBackChoice", arrowArt,
                    80f, 916f, iconSize, iconSize * (float)arrowArt.height / arrowArt.width);
                arrow.color = Color.white;
                arrow.raycastTarget = true;
                onScreenBack = arrow.gameObject.AddComponent<Button>();
                onScreenBack.targetGraphic = arrow;
                onScreenBack.transition = Selectable.Transition.None;
                onScreenBack.onClick.AddListener(() => BackToRoom());
                arrow.gameObject.AddComponent<LaptopChoiceHover>();
            }
            else
                onScreenBack = ui.Button(povRoot, "LaptopBackChoice", "←",
                    92f, 915f, 72f, 58f, () => BackToRoom(), playClickSound: false);
            // The original user's golden "ОТКРЫТЬ НОУТБУК" PNG is not a
            // made-up flat UI. An optional exact "ОТКРЫТЬ YOMI" PNG has
            // first priority if installed; either gets the same hover.
            Texture2D openArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_open_yomi");
            if (!openArt)
                openArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_open");
            poweredOpenRoot = ui.Rect(povRoot, "PoweredLaptopOpenHint", 540f, 868f, 560f, 190f);
            if (openArt)
            {
                var openImage = ui.Art(poweredOpenRoot, "ExactGoldenOpenPrompt",
                    openArt, 0f, 0f, 560f, 560f * openArt.height / openArt.width);
                openImage.color = Color.white;
                openImage.raycastTarget = true;
                var openButton = openImage.gameObject.AddComponent<Button>();
                openButton.targetGraphic = openImage;
                openButton.transition = Selectable.Transition.None;
                openButton.onClick.AddListener(() => TryPressPower());
                openImage.gameObject.AddComponent<LaptopChoiceHover>();
            }
            else
            {
                // Only used if the user PNG was not installed.
                var openSurface = poweredOpenRoot.gameObject.AddComponent<LaptopSurface>();
                openSurface.Radius = 22f;
                openSurface.color = new Color(.65f, .31f, .08f, 1f);
                var openButton = poweredOpenRoot.gameObject.AddComponent<Button>();
                openButton.targetGraphic = openSurface;
                openButton.transition = Selectable.Transition.None;
                openButton.onClick.AddListener(() => TryPressPower());
                poweredOpenRoot.gameObject.AddComponent<LaptopChoiceHover>();
                poweredOpenHint = ui.Label(poweredOpenRoot, "OpenYomiGoldText",
                    "E — ОТКРЫТЬ YOMI", 18f, 25f, 520f, 72f, 26,
                    new Color(1f, .91f, .70f, 1f), false, TextAnchor.MiddleCenter);
            }
            SetPromptVisibility(false);
        }

        // Enter from the room with a grounded settle; closing YOMI instead
        // returns to the already seated POV without replaying sit audio.
        private IEnumerator Play()
        {
            float elapsed = 0f;
            if (returningFromLaptop)
            {
                // The YOMI panel fades away over an ALREADY-READY seated POV.
                // No alpha=0 frame, so neither Hub nor empty canvas can flash.
                RenderAt(HandsStart);
                povGroup.alpha = 1f;
                elapsed = HandsStart;
            }
            else
            {
                while (active && elapsed < HandsStart)
                {
                    RenderAt(elapsed);
                    yield return null;
                    // 0.72s approach = seated-to-standing return; keep 49-frame hand timing unchanged.
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, .06f) * (HandsStart / ReturnDuration);
                }
            }
            if (!active) yield break;
            RenderAt(HandsStart);
            CurrentPhase = Phase.PreBootChoice;
            SetPromptVisibility(true);
            while (active && !powerConfirmed && !returnRequested)
            {
                RenderAt(HandsStart);
                if (LaptopPowerSession.PoweredOn && desktopClock)
                    desktopClock.UpdateClockIfNecessary();
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
            bool powered = LaptopPowerSession.PoweredOn;
            if (powerHint) powerHint.gameObject.SetActive(show && !powered);
            if (backHint) backHint.gameObject.SetActive(show);
            if (powerHintArt) powerHintArt.gameObject.SetActive(show && !powered);
            if (backHintArt) backHintArt.gameObject.SetActive(show);
            if (onScreenBack) onScreenBack.gameObject.SetActive(show);
            if (powerGlowRoot) powerGlowRoot.gameObject.SetActive(!powered && !returnRequested &&
                (show || (powerConfirmed && CurrentPhase != Phase.PowerOn && CurrentPhase != Phase.UIOpen)));
            if (noSignalScreen) noSignalScreen.gameObject.SetActive(show && !powered);
            if (noSignalText && noSignalScreen && noSignalScreen.gameObject.activeSelf)
                noSignalText.gameObject.SetActive(noSignalBounce == null);
            if (physicalMiniYomiViewport) physicalMiniYomiViewport.gameObject.SetActive(show && powered);
            if (poweredWallpaper) poweredWallpaper.gameObject.SetActive(show && powered);
            if (desktopClock) desktopClock.gameObject.SetActive(show && powered);
            if (poweredDesktopClick) poweredDesktopClick.gameObject.SetActive(show && powered);
            if (poweredOpenRoot) poweredOpenRoot.gameObject.SetActive(show && powered);
            if (povGroup)
            {
                povGroup.blocksRaycasts = show;
                povGroup.interactable = show;
            }
        }

        public bool TryPressPower()
        {
            if (!IsAwaitingPowerChoice || powerConfirmed || returnRequested) return false;
            powerConfirmed = true;
            SetPromptVisibility(false);
            if (LaptopPowerSession.PoweredOn)
            {
                // Already running: clicking the live LCD or pressing E opens
                // existing YOMI directly, with no hand animation or boot.
                Finish(true);
            }
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

            // Focus the existing LIVE home scene and glowing hotspots together;
            // never animate a disconnected or stale home screenshot.
            updateRoomCamera?.Invoke(Mathf.Clamp01(t / HandsStart));
            float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / ApproachEnd));
            wideImage.rectTransform.localScale = Vector3.one * (1f + .065f * ease);
            // Grounded body settle: weight drops with a damped rebound as Keiki
            // sits at the low table. The reverse follows the same motion.
            float settled = Mathf.Clamp01(t / HandsStart);
            float sitDrop = -14f * Mathf.SmoothStep(0f, 1f, settled) +
                2.5f * Mathf.Sin(settled * Mathf.PI * 2.3f) * Mathf.Exp(-4f * settled);
            wideImage.rectTransform.anchoredPosition =
                new Vector2(StageWidth * .59f,
                    -StageHeight * .47f + sitDrop);
            povGroup.alpha = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(POVStart, POVEnd, t));

            if (t < POVStart) CurrentPhase = Phase.Approach;
            else if (t < POVEnd) CurrentPhase = Phase.POVTransition;
            else if (returnRequested) CurrentPhase = Phase.CancelBack;
            else if (!powerConfirmed) CurrentPhase = Phase.PreBootChoice;
            else if (t < LaptopPowerTimeline.ContactTime) CurrentPhase = Phase.HandsInteraction;
            else CurrentPhase = Phase.PowerOn;

            // Physical blue LED stays illuminated during finger approach and
            // is extinguished only at the actual calibrated contact timestamp.
            if (powerGlowRoot)
                powerGlowRoot.gameObject.SetActive(!LaptopPowerSession.PoweredOn && !returnRequested &&
                    ((!powerConfirmed && CurrentPhase == Phase.PreBootChoice) ||
                     (powerConfirmed && t < LaptopPowerTimeline.ContactTime)));

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
            else if (root) Cleanup();
        }

        private void Finish(bool shouldOpenLaptop)
        {
            if (!active) return;
            active = false; // Guard callbacks and out-of-order coroutine completion.
            powerTimeline?.Cancel();
            CurrentPhase = shouldOpenLaptop ? Phase.UIOpen : Phase.Idle;
            if (running != null && owner) owner.StopCoroutine(running);
            running = null;
            if (shouldOpenLaptop)
            {
                // Keep the original approved POV covering Home until YOMI
                // reports its first visible video/desktop frame.
                SetPromptVisibility(false);
                if (handsImage) handsImage.gameObject.SetActive(false);
                // The already powered LCD stays drawn behind fullscreen YOMI.
                if (desktopClock && LaptopPowerSession.PoweredOn)
                {
                    desktopClock.gameObject.SetActive(true);
                    desktopClock.UpdateClockIfNecessary(true);
                }
                if (physicalMiniYomiViewport && LaptopPowerSession.PoweredOn)
                    physicalMiniYomiViewport.gameObject.SetActive(true);
                if (poweredWallpaper && LaptopPowerSession.PoweredOn)
                    poweredWallpaper.gameObject.SetActive(true);
                if (povGroup) povGroup.alpha = 1f;
                pendingVisualHandoff = true;
                try { openExistingLaptop?.Invoke(); }
                catch (Exception e)
                {
                    Debug.LogError("ROKAS laptop handoff failed: " + e);
                    Cleanup();
                    cancelled?.Invoke();
                }
            }
            else
            {
                Cleanup();
                cancelled?.Invoke();
            }
        }

        private void Cleanup()
        {
            updateRoomCamera?.Invoke(0f);
            pendingVisualHandoff = false;
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
            returningFromLaptop = false;
            powerHint = null;
            backHint = null;
            poweredWallpaper = null;
            desktopClock = null;
            poweredDesktopClick = null;
            poweredOpenHint = null;
            poweredOpenRoot = null;
            powerHintArt = null;
            backHintArt = null;
            onScreenBack = null;
            powerGlowRoot = null;
            noSignalText = null;
            noSignalScreen = null;
            noSignalBounce = null;
            physicalMiniYomiImage = null;
            physicalMiniYomiViewport = null;
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
