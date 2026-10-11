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
        // Soften both sit-down and stand-up fade. Preserve camera/hand timeline.
        private const float POVStart = .78f;
        private const float POVEnd = 1.8f;
        private const float HandsStart = 1.8f;
        private const float HandsEnd = 3.84f;
        private const float FinishTime = 3.92f;
        private const float ReturnDuration = .84f;

        // Move every gold UI action 50 stage pixels down from V11.6:
        // OFF E / ESC / arrow and ON R / E / ESC share this single offset.
        // Original PNG scale, texture bytes and optical spacing remain unchanged.
        private const float GoldPromptDrop = 50f;
        private const float PreviouslyApprovedStandBottom = 1045f;

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
        private readonly Action bounceSound;

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
        private bool startingFirstBoot;
        private bool standbyHeldForFirstBoot;
        private Text powerHint;
        private Text backHint;
        private RawImage powerHintArt;
        private RawImage backHintArt;
        private Button onScreenBack;
        private Button poweredStandButton;
        private RectTransform powerGlowRoot;
        private Text noSignalText;
        private RectTransform noSignalScreen;
        private RawImage physicalMiniYomiImage;
        private LaptopPerspectiveQuad physicalExactYomiQuad;
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
            if (pendingVisualHandoff && standbyHeldForFirstBoot)
            {
                standbyHeldForFirstBoot = false;
                // Switch standby -> visible original boot frame in SAME update.
                SetPromptVisibility(false);
            }
        }
        public bool IsAwaitingPowerChoice => active && CurrentPhase == Phase.PreBootChoice;

        // Physical screen uses the identical pixels actually presented by
        // fullscreen YOMI, captured before its close animation. Never generate
        // a second imitation desktop when those pixels are available.
        public void RefreshExactYomiMirror()
        {
            if (!physicalMiniYomiImage || !physicalMiniYomiViewport) return;
            Texture2D exact = LaptopYomiMirror.Current;
            if (!exact) return;
            // UV-map the ENTIRE original YOMI screenshot directly onto the
            // calibrated LCD trapezoid; no rectangular cover/crop on its edges.
            physicalMiniYomiImage.texture = exact; // inspection source
            physicalMiniYomiImage.gameObject.SetActive(false);
            if (physicalExactYomiQuad)
            {
                physicalExactYomiQuad.SetImage(exact);
                physicalExactYomiQuad.gameObject.SetActive(true);
            }
            // Remove ALL synthetic 640x360 glyph overlays once the original
            // screenshot source is ready; they must never contaminate 1:1 UI.
            foreach (var glyph in physicalMiniYomiViewport.GetComponentsInChildren<LaptopIcon>(true))
                glyph.gameObject.SetActive(false);
            if (desktopClock) desktopClock.gameObject.SetActive(false);
            if (poweredWallpaper) poweredWallpaper.gameObject.SetActive(false);
        }

        public LaptopCinematicSequence(UiKit ui, MonoBehaviour owner, RectTransform transitionLayer,
            RawImage mainBackground, Action openExistingLaptop, Action cancelled,
            Action powerClick, Action returningToRoom, Func<int> unreadMessages = null,
            Action<float> updateRoomCamera = null, Texture yomiWallpaper = null,
            Action bounceSound = null)
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
            this.bounceSound = bounceSound;
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
                startingFirstBoot = false;
                standbyHeldForFirstBoot = false;
                if (povGroup) povGroup.alpha = 1f;
                CurrentPhase = Phase.PreBootChoice;
                KeepPoweredLCDReady();
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
                startingFirstBoot = !LaptopPowerSession.PoweredOn;
                standbyHeldForFirstBoot = false;
                BuildOverlay();
                active = true;
                CurrentPhase = Phase.Approach;
                KeepPoweredLCDReady(); // powered LCD visible from the first seating frame
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
            // The actual LCD is a trapezoid, NOT a rectangle. A rectangular
            // raw-image viewport leaked out of its top-left edge and failed
            // to reach the lower-right corner in the user's 2026-10-11 frame.
            // Clip both OFF and ON content by the SAME four-corner stencil,
            // with a small inset measured *inside* the original bezel.
            // The real bezel is asymmetrical in the approved POV photo:
            // after V11.5 the right edge fits, but the upper/lower LEFT
            // stencil pixels extend onto the lid. Trim only the left edge.
            // These values are original-source pixels (scaled by imageScale).
            const float leftTopInset = 22f;
            const float leftBottomInset = 26f;
            const float rightInset = 5f;
            const float verticalInset = 5f;
            Vector2[] safeCorners =
            {
                new Vector2(calibration.topLeftX + leftTopInset, calibration.topLeftY + verticalInset),
                new Vector2(calibration.topRightX - rightInset, calibration.topRightY + verticalInset),
                new Vector2(calibration.bottomRightX - rightInset, calibration.bottomRightY - verticalInset),
                new Vector2(calibration.bottomLeftX + leftBottomInset, calibration.bottomLeftY - verticalInset)
            };
            float minLcdX = Mathf.Min(safeCorners[0].x, safeCorners[3].x);
            float maxLcdX = Mathf.Max(safeCorners[1].x, safeCorners[2].x);
            float minLcdY = Mathf.Min(safeCorners[0].y, safeCorners[1].y);
            float maxLcdY = Mathf.Max(safeCorners[2].y, safeCorners[3].y);
            float lcdX = imageX + minLcdX * imageScale;
            float lcdY = imageY + minLcdY * imageScale;
            float lcdW = Mathf.Max(80f, (maxLcdX - minLcdX) * imageScale);
            float lcdH = Mathf.Max(48f, (maxLcdY - minLcdY) * imageScale);
            Vector2[] localCorners = new Vector2[4];
            for (int i = 0; i < localCorners.Length; i++)
                localCorners[i] = new Vector2(
                    safeCorners[i].x - minLcdX, safeCorners[i].y - minLcdY);

            noSignalScreen = ui.Rect(povRoot, "ROKASNoSignalScreen", lcdX, lcdY, lcdW, lcdH);
            AddLcdStencil(noSignalScreen, localCorners, imageScale,
                new Color(.003f, .007f, .015f, 1f));
            Texture2D chibi = Resources.Load<Texture2D>("LaptopCinematic/UI/rokas_no_signal_chibi");
            if (chibi)
            {
                float w = Mathf.Min(lcdW * .34f, lcdH * .55f);
                var sticker = ui.Art(noSignalScreen, "ROKASNoSignalChibiDVD",
                    chibi, 10f, 10f, w, w * chibi.height / chibi.width);
                sticker.color = Color.white;
                sticker.raycastTarget = false;
                noSignalBounce = noSignalScreen.gameObject.AddComponent<LaptopNoSignalBounce>();
                noSignalBounce.Initialize(noSignalScreen, sticker.rectTransform, bounceSound);
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
            AddLcdStencil(physicalMiniYomiViewport, localCorners, imageScale,
                new Color(.01f, .025f, .05f, 1f));
            physicalMiniYomiImage = ui.Art(physicalMiniYomiViewport,
                "PhysicalMiniYomiVisible", desktopClock.mainTexture,
                0f, 0f, lcdW, lcdH);
            physicalMiniYomiImage.color = Color.white;
            physicalMiniYomiImage.raycastTarget = false;
            RectTransform exactQuadRect = ui.Rect(physicalMiniYomiViewport,
                "PhysicalExactYomiPerspective", 0f, 0f, lcdW, lcdH);
            exactQuadRect.gameObject.AddComponent<CanvasRenderer>();
            physicalExactYomiQuad = exactQuadRect.gameObject.AddComponent<LaptopPerspectiveQuad>();
            physicalExactYomiQuad.SetCorners(localCorners, imageScale);
            physicalExactYomiQuad.color = Color.white;
            physicalExactYomiQuad.raycastTarget = false;
            physicalExactYomiQuad.gameObject.SetActive(false);
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
            RefreshExactYomiMirror();
            // Load the three EXACT approved transparent user PNGs when installed.
            // If V3.2 art was not installed, preserve readable V3.1 text fallbacks.
            Texture2D onArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_power");
            Texture2D exitArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_back");
            Texture2D arrowArt = Resources.Load<Texture2D>("LaptopCinematic/UI/back_arrow");
            Texture2D standArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_stand_r");
            if (!standArt) standArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_stand");
            if (onArt)
            {
                float w = 535f;
                powerHintArt = ui.Art(povRoot, "PowerChoiceHint", onArt, 514f, 903f,
                    w, w * (float)onArt.height / onArt.width);
                powerHintArt.color = Color.white; // original RGB/alpha, no tint
                AddGoldEmissionBoost(powerHintArt, onArt);
                powerHintArt.raycastTarget = true;
                var startButton = powerHintArt.gameObject.AddComponent<Button>();
                startButton.transition = Selectable.Transition.None;
                startButton.onClick.AddListener(() => TryPressPower());
                powerHintArt.gameObject.AddComponent<LaptopChoiceHover>();
            }
            else
                powerHint = ui.Label(povRoot, "PowerChoiceHint", "E  —  ВКЛЮЧИТЬ НОУТБУК",
                    575f, 955f + GoldPromptDrop, 475f, 44f, 20,
                    new Color(.8f, .85f, .9f, .88f));
            if (exitArt)
            {
                float w = 460f;
                backHintArt = ui.Art(povRoot, "BackChoiceHint", exitArt, 1110f, 920f,
                    w, w * (float)exitArt.height / exitArt.width);
                backHintArt.color = Color.white;
                AddGoldEmissionBoost(backHintArt, exitArt);
                backHintArt.raycastTarget = true;
                var leaveButton = backHintArt.gameObject.AddComponent<Button>();
                leaveButton.transition = Selectable.Transition.None;
                leaveButton.onClick.AddListener(() => BackToRoom());
                backHintArt.gameObject.AddComponent<LaptopChoiceHover>();
            }
            else
                backHint = ui.Label(povRoot, "BackChoiceHint", "ESC  —  НАЗАД",
                    1080f, 955f + GoldPromptDrop, 320f, 44f, 20,
                    new Color(.8f, .85f, .9f, .88f));
            if (arrowArt)
            {
                float iconSize = 82f;
                RawImage arrow = ui.Art(povRoot, "LaptopBackChoice", arrowArt,
                    80f, 942f + GoldPromptDrop, iconSize,
                    iconSize * (float)arrowArt.height / arrowArt.width);
                arrow.color = Color.white;
                AddGoldEmissionBoost(arrow, arrowArt);
                arrow.raycastTarget = true;
                onScreenBack = arrow.gameObject.AddComponent<Button>();
                onScreenBack.targetGraphic = arrow;
                onScreenBack.transition = Selectable.Transition.None;
                onScreenBack.onClick.AddListener(() => BackToRoom());
                arrow.gameObject.AddComponent<LaptopChoiceHover>();
            }
            else
                onScreenBack = ui.Button(povRoot, "LaptopBackChoice", "←",
                    92f, 915f + GoldPromptDrop, 72f, 58f,
                    () => BackToRoom(), playClickSound: false);
            // When the LCD is already powered, replace ONLY the little arrow
            // with the user's exact E — ВСТАТЬ gold PNG. Mouse click calls the
            // existing stand-up/back-to-room animation; keyboard E continues
            // to open YOMI as agreed (ESC also stands up).
            if (standArt)
            {
                const float standWidth = 438f;
                // The prior approved V11.6 R lower edge was 1045.
                // User explicitly requested ALL gold controls 50px lower,
                // so R moves by the SAME amount as E and ESC.
                float standHeight = standWidth * (float)standArt.height / standArt.width;
                float approvedStandLowerEdge = PreviouslyApprovedStandBottom + GoldPromptDrop;
                var standImage = ui.Art(povRoot, "PoweredStandOriginalPNG",
                    standArt, 92f, approvedStandLowerEdge - standHeight,
                    standWidth, standHeight);
                standImage.color = Color.white;
                AddGoldEmissionBoost(standImage, standArt);
                standImage.raycastTarget = true;
                poweredStandButton = standImage.gameObject.AddComponent<Button>();
                poweredStandButton.targetGraphic = standImage;
                poweredStandButton.transition = Selectable.Transition.None;
                poweredStandButton.onClick.AddListener(() => BackToRoom());
                standImage.gameObject.AddComponent<LaptopChoiceHover>();
            }
            // The original user's golden "ОТКРЫТЬ НОУТБУК" PNG is not a
            // made-up flat UI. An optional exact "ОТКРЫТЬ YOMI" PNG has
            // first priority if installed; either gets the same hover.
            Texture2D openArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_open_yomi");
            if (!openArt)
                openArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_open");
            poweredOpenRoot = ui.Rect(povRoot, "PoweredLaptopOpenHint", 540f, 900f, 560f, 190f);
            if (openArt)
            {
                var openImage = ui.Art(poweredOpenRoot, "ExactGoldenOpenPrompt",
                    openArt, 0f, 0f, 560f, 560f * openArt.height / openArt.width);
                openImage.color = Color.white;
                AddGoldEmissionBoost(openImage, openArt);
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
            // R — ВСТАТЬ defines the alignment after the shared 50px
            // drop. Unlike uniform graphic
            // rectangles, the supplied transparent PNGs have unequal alpha
            // padding, so matching RectTransform bottoms was visibly wrong.
            // Align other prompts to the R button's optical lower edge, preserving
            // V11.6 optical corrections of +19 (E) / +13 (ESC).
            RectTransform standRect = poweredStandButton
                ? poweredStandButton.GetComponent<RectTransform>() : null;
            float referenceBottom = standRect
                ? -standRect.anchoredPosition.y + standRect.rect.height
                : PreviouslyApprovedStandBottom + GoldPromptDrop; // same 50px shift in fallback
            const float openOpticalDown = 19f; // measured in approved Game View
            const float backOpticalDown = 13f;
            if (powerHintArt) PlaceGoldPromptOnBaseline(powerHintArt.rectTransform,
                referenceBottom + openOpticalDown);
            if (backHintArt) PlaceGoldPromptOnBaseline(backHintArt.rectTransform,
                referenceBottom + backOpticalDown);
            if (poweredOpenRoot) PlaceGoldPromptOnBaseline(poweredOpenRoot,
                referenceBottom + openOpticalDown,
                poweredOpenRoot.GetComponentInChildren<RawImage>(true));
            // Keep the existing R position unchanged; do NOT re-baseline it.
            SetPromptVisibility(false);
        }

        private static void PlaceGoldPromptOnBaseline(RectTransform root,
            float bottomLine, RawImage measuredImage = null)
        {
            if (!root) return;
            RectTransform source = measuredImage ? measuredImage.rectTransform : root;
            float height = source.rect.height;
            root.anchoredPosition = new Vector2(root.anchoredPosition.x,
                -(bottomLine - height));
        }

        private static void AddLcdStencil(RectTransform viewport, Vector2[] localCorners,
            float scale, Color screenBackground)
        {
            // A visible Graphic supplies the stencil shape to Unity UI Mask.
            // RectMask2D is retained as a second, conservative bounding clip.
            // Both the bouncing chibi and the real YOMI photo use this shape.
            // MaskableGraphic needs a CanvasRenderer even on code-built objects.
            // Without it Unity throws MissingComponentException in PlayMode.
            if (!viewport.GetComponent<CanvasRenderer>())
                viewport.gameObject.AddComponent<CanvasRenderer>();
            var quad = viewport.gameObject.AddComponent<LaptopPerspectiveQuad>();
            quad.SetCorners(localCorners, scale);
            quad.color = screenBackground;
            quad.raycastTarget = false;
            var polygonMask = viewport.gameObject.AddComponent<Mask>();
            polygonMask.showMaskGraphic = true;
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        private void AddGoldEmissionBoost(RawImage original, Texture2D approved)
        {
            // The PNGs were mastered over black. Alpha compositing their
            // delicate gold glow over a busy lit desk made them look grey.
            // Draw the very SAME source art a second time (no new artwork,
            // hue change, dark tint, or material substitution).
            RectTransform rect = original.rectTransform;
            var glow = ui.Art(rect, "OriginalGoldLuminance", approved,
                0f, 0f, rect.rect.width, rect.rect.height);
            glow.color = new Color(1f, 1f, 1f, .8f);
            glow.raycastTarget = false;
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
                    // Gentle seated-to-standing return; keep the 49-frame hand timing unchanged.
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
            if (returnRequested)
            {
                // Keep the original buttons and powered screen rendered during
                // the return animation; they inherit the easing POV fade,
                // instead of abruptly disappearing on the R/Esc input.
                CurrentPhase = Phase.CancelBack;
                float exitElapsed = 0f;
                while (active && exitElapsed < ReturnDuration)
                {
                    // Gently reverse the camera blend; never start LaptopBoot.
                    float progress = Mathf.Clamp01(exitElapsed / ReturnDuration);
                    float smooth = progress * progress * progress *
                        (progress * (progress * 6f - 15f) + 10f);
                    RenderAt(Mathf.Lerp(HandsStart, 0f, smooth));
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

        private void KeepPoweredLCDReady()
        {
            if (!LaptopPowerSession.PoweredOn || !physicalMiniYomiViewport) return;
            physicalMiniYomiViewport.gameObject.SetActive(true);
            if (LaptopYomiMirror.Current) RefreshExactYomiMirror();
            bool fallback = !LaptopYomiMirror.Current;
            if (poweredWallpaper) poweredWallpaper.gameObject.SetActive(fallback);
            if (desktopClock) desktopClock.gameObject.SetActive(fallback);
        }

        private void SetPromptVisibility(bool show)
        {
            bool powered = LaptopPowerSession.PoweredOn;
            if (powerHint) powerHint.gameObject.SetActive(show && !powered);
            if (backHint) backHint.gameObject.SetActive(show);
            if (powerHintArt) powerHintArt.gameObject.SetActive(show && !powered);
            if (backHintArt) backHintArt.gameObject.SetActive(show);
            if (onScreenBack) onScreenBack.gameObject.SetActive(show && (!powered || !poweredStandButton));
            if (poweredStandButton) poweredStandButton.gameObject.SetActive(show && powered);
            if (powerGlowRoot) powerGlowRoot.gameObject.SetActive(!powered && !returnRequested &&
                (show || (powerConfirmed && CurrentPhase != Phase.PowerOn && CurrentPhase != Phase.UIOpen)));
            if (noSignalScreen) noSignalScreen.gameObject.SetActive(show && !powered);
            if (noSignalText && noSignalScreen && noSignalScreen.gameObject.activeSelf)
                noSignalText.gameObject.SetActive(noSignalBounce == null);
            if (physicalMiniYomiViewport)
                physicalMiniYomiViewport.gameObject.SetActive(powered &&
                    (show || returnRequested || returningFromLaptop ||
                     (!powerConfirmed && (CurrentPhase == Phase.Approach ||
                                           CurrentPhase == Phase.POVTransition))));
            if (show && powered && LaptopYomiMirror.Current) RefreshExactYomiMirror();
            bool lcdVisible = physicalMiniYomiViewport && physicalMiniYomiViewport.gameObject.activeSelf;
            if (poweredWallpaper) poweredWallpaper.gameObject.SetActive(lcdVisible && !LaptopYomiMirror.Current);
            if (desktopClock) desktopClock.gameObject.SetActive(lcdVisible && !LaptopYomiMirror.Current);
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
            // Do NOT remove chibi or gold prompts on keyboard/mouse input.
            // They remain through the 49-frame hand press until actual boot.
            if (LaptopPowerSession.PoweredOn)
            {
                // Already powered: the click explicitly opens fullscreen YOMI.
                SetPromptVisibility(false);
                Finish(true);
            }
            return true;
        }

        public bool BackToRoom()
        {
            if (!IsAwaitingPowerChoice || returnRequested || powerConfirmed) return false;
            returnRequested = true;
            returningToRoom?.Invoke();
            // Fade with the approved POV, not an instant SetActive(false).
            // Disallow clicks while exiting, but keep visual children alive.
            if (povGroup)
            {
                povGroup.blocksRaycasts = false;
                povGroup.interactable = false;
            }
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
            float visibility = Mathf.InverseLerp(POVStart, POVEnd, t);
            // Quintic smootherstep avoids sharp alpha acceleration when the
            // photographed laptop enters/leaves the frame.
            povGroup.alpha = visibility * visibility * visibility *
                (visibility * (visibility * 6f - 15f) + 10f);
            // Chibi is already present in the final ~0.1s of seating,
            // instead of popping in only after the camera finishes settling.
            // This does not change the original POV art or the hand timeline.
            if (noSignalScreen && !powerConfirmed && !returnRequested &&
                !LaptopPowerSession.PoweredOn && t >= HandsStart - .12f &&
                t < HandsStart)
                noSignalScreen.gameObject.SetActive(true);

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
                // Atomic handoff: standby prompts disappear only as we create
                // the real boot/YOMI surface, never at the E/Power input.
                // Hold the ORIGINAL chibi/prompt artwork until VideoPlayer has
                // a real first boot frame and its modal is raised to front.
                // Hiding now caused the blank gap visible in user recording.
                standbyHeldForFirstBoot = startingFirstBoot;
                if (!standbyHeldForFirstBoot) SetPromptVisibility(false);
                if (povGroup)
                {
                    povGroup.blocksRaycasts = false;
                    povGroup.interactable = false;
                }
                // Preserve chibi and standby artwork until the first boot
                // frame is presented, not on the Power confirmation input.
                if (handsImage) handsImage.gameObject.SetActive(false);
                // The already powered LCD stays drawn behind fullscreen YOMI.
                // NEVER flash a loaded physical desktop before LaptopBoot.mp4:
                // first boot is already electrically latched at fingertip
                // contact, but visual startup is independently gated by video.
                if (LaptopPowerSession.CompletedBoots > 0 && !LaptopYomiMirror.Current)
                    desktopClock?.UpdateClockIfNecessary(true);
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
            standbyHeldForFirstBoot = false;
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
            poweredStandButton = null;
            powerGlowRoot = null;
            noSignalText = null;
            noSignalScreen = null;
            noSignalBounce = null;
            physicalMiniYomiImage = null;
            physicalExactYomiQuad = null;
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
