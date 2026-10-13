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
            public int formatVersion;
            public string coordinateSpace;
            public int sourceWidth;
            public int sourceHeight;
            public float topLeftX = 596f, topLeftY = 413f;
            public float topRightX = 1044f, topRightY = 412f;
            public float bottomRightX = 1051f, bottomRightY = 676f;
            public float bottomLeftX = 586f, bottomLeftY = 676f;
            public float powerX = 1040f, powerY = 708f;
            public bool debugPowerAnchor;
            public bool enableRoundedMask;
            public float topLeftRadius;
            public float topRightRadius;
            public float bottomRightRadius;
            public float bottomLeftRadius;
            public bool enableEdgeControlPoints;
            public float topMidX, topMidY, rightMidX, rightMidY;
            public float bottomMidX, bottomMidY, leftMidX, leftMidY;
            public bool enableCornerHelpers;
            public float tlHx, tlHy, tlVx, tlVy, trHx, trHy, trVx, trVy;
            public float brHx, brHy, brVx, brVy, blHx, blHy, blVx, blVy;
            // The optional V4 bezelReference contains traced diagnostic data
            // for the editor only. The physical screen uses fitted controls.
        }

        private static bool IsValidV2(ScreenCalibration c, Texture2D reference,
            int expectedVersion = 2)
        {
            if (c == null || !reference || c.formatVersion != expectedVersion ||
                c.coordinateSpace != "source_image_pixels_top_left" ||
                c.sourceWidth != reference.width ||
                c.sourceHeight != reference.height) return false;
            Vector2[] p =
            {
                new Vector2(c.topLeftX, c.topLeftY),
                new Vector2(c.topRightX, c.topRightY),
                new Vector2(c.bottomRightX, c.bottomRightY),
                new Vector2(c.bottomLeftX, c.bottomLeftY)
            };
            float area = 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % 4], d = p[(i + 2) % 4];
                if (float.IsNaN(a.x) || float.IsNaN(a.y) ||
                    float.IsInfinity(a.x) || float.IsInfinity(a.y) ||
                    a.x < 0f || a.y < 0f || a.x >= reference.width ||
                    a.y >= reference.height) return false;
                float cross = (b.x - a.x) * (d.y - b.y) -
                              (b.y - a.y) * (d.x - b.x);
                if (cross <= .001f) return false;
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 200f) return false;
            float width = (Vector2.Distance(p[0], p[1]) +
                           Vector2.Distance(p[3], p[2])) * .5f;
            float height = (Vector2.Distance(p[0], p[3]) +
                            Vector2.Distance(p[1], p[2])) * .5f;
            float cap = Mathf.Max(0f, Mathf.Min(120f, .45f *
                Mathf.Min(width, height)));
            foreach (float r in new[] { c.topLeftRadius, c.topRightRadius,
                                       c.bottomRightRadius, c.bottomLeftRadius })
                if (float.IsNaN(r) || float.IsInfinity(r) ||
                    r < 0f || r > cap + .01f) return false;
            return !float.IsNaN(c.powerX) && !float.IsInfinity(c.powerX) &&
                   !float.IsNaN(c.powerY) && !float.IsInfinity(c.powerY);
        }

        private static bool TryBuildV4Warp(ScreenCalibration c, Texture2D reference,
            out Vector2[] corners, out Vector2[] edgeDeltas,
            out Vector2[] helperDeltas)
        {
            corners = edgeDeltas = helperDeltas = null;
            if (c == null || !c.enableEdgeControlPoints ||
                !c.enableCornerHelpers || !IsValidV2(c, reference, 4))
                return false;

            Vector2[] bounds =
            {
                new Vector2(c.topLeftX, c.topLeftY),
                new Vector2(c.topRightX, c.topRightY),
                new Vector2(c.bottomRightX, c.bottomRightY),
                new Vector2(c.bottomLeftX, c.bottomLeftY)
            };
            Vector2[] midpoints =
            {
                new Vector2(c.topMidX, c.topMidY),
                new Vector2(c.rightMidX, c.rightMidY),
                new Vector2(c.bottomMidX, c.bottomMidY),
                new Vector2(c.leftMidX, c.leftMidY)
            };
            Vector2[] extra =
            {
                new Vector2(c.tlHx, c.tlHy), new Vector2(c.tlVx, c.tlVy),
                new Vector2(c.trHx, c.trHy), new Vector2(c.trVx, c.trVy),
                new Vector2(c.brHx, c.brHy), new Vector2(c.brVx, c.brVy),
                new Vector2(c.blHx, c.blHy), new Vector2(c.blVx, c.blVy)
            };
            Vector2[] tangentUV =
            {
                new Vector2(.18f, 0f), new Vector2(0f, .18f),
                new Vector2(.82f, 0f), new Vector2(1f, .18f),
                new Vector2(.82f, 1f), new Vector2(1f, .82f),
                new Vector2(.18f, 1f), new Vector2(0f, .82f)
            };
            Vector2[] edges = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2 midpoint = (bounds[i] + bounds[(i + 1) % 4]) * .5f;
                edges[i] = midpoints[i] - midpoint;
                if (!IsInsidePhoto(midpoints[i], reference) ||
                    edges[i].magnitude > 100f) return false;
            }
            Vector2[] helpers = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                helpers[i] = extra[i] - LaptopPerspectiveQuad.MapUV(
                    tangentUV[i], bounds, edges, new Vector2[8], true);
                if (!IsInsidePhoto(extra[i], reference) ||
                    helpers[i].magnitude > 65f) return false;
            }
            // Refuse a folded or invalid mesh. The same UV mapping is used
            // by every runtime layer and the user's calibration workshop.
            const int steps = 24;
            for (int y = 0; y < steps; y++)
                for (int x = 0; x < steps; x++)
                {
                    float u = x / (float)steps, v = y / (float)steps;
                    float increment = 1f / steps;
                    Vector2 p = LaptopPerspectiveQuad.MapUV(new Vector2(u, v),
                        bounds, edges, helpers, true);
                    Vector2 right = LaptopPerspectiveQuad.MapUV(
                        new Vector2(u + increment, v), bounds, edges, helpers, true);
                    Vector2 below = LaptopPerspectiveQuad.MapUV(
                        new Vector2(u, v + increment), bounds, edges, helpers, true);
                    float cross = (right.x - p.x) * (below.y - p.y) -
                                  (right.y - p.y) * (below.x - p.x);
                    if (float.IsNaN(cross) || float.IsInfinity(cross) ||
                        cross <= .0001f) return false;
                }
            corners = bounds;
            edgeDeltas = edges;
            helperDeltas = helpers;
            return true;
        }

        private static bool IsInsidePhoto(Vector2 value, Texture2D photo)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
                !float.IsInfinity(value.x) && !float.IsInfinity(value.y) &&
                value.x >= 0f && value.y >= 0f &&
                value.x < photo.width && value.y < photo.height;
        }

        private static void ComputeLCDBounds(Vector2[] corners,
            Vector2[] edges, Vector2[] helpers, out float minX,
            out float minY, out float maxX, out float maxY)
        {
            minX = Mathf.Min(corners[0].x, corners[3].x);
            minY = Mathf.Min(corners[0].y, corners[1].y);
            maxX = Mathf.Max(corners[1].x, corners[2].x);
            maxY = Mathf.Max(corners[2].y, corners[3].y);
            if (edges == null || helpers == null) return; // V2 unchanged
            // Edges can extend beyond the four corners. RectMask2D must NOT
            // clip even 1-3px of a user's fitted tangent near rounded glass.
            const int samples = 64;
            for (int iy = 0; iy <= samples; iy++)
                for (int ix = 0; ix <= samples; ix++)
                {
                    Vector2 p = LaptopPerspectiveQuad.MapUV(
                        new Vector2(ix / (float)samples, iy / (float)samples),
                        corners, edges, helpers, true);
                    minX = Mathf.Min(minX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxX = Mathf.Max(maxX, p.x);
                    maxY = Mathf.Max(maxY, p.y);
                }
            // Tiny conservative overscan is invisible behind the actual
            // curved stencil; it prevents reintroduced border cropping.
            minX -= 2f; minY -= 2f; maxX += 2f; maxY += 2f;
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
        private const float ShutdownHandReturn = .58f;

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
        private readonly LaptopCinematicLightPresentation lighting;
        private LaptopScreenBounceEstimator screenBounce =
            new LaptopScreenBounceEstimator();

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
        private bool shutdownRequested;
        private bool returnRequested;
        private bool returningFromLaptop;
        private bool pendingVisualHandoff;
        private bool startingFirstBoot;
        private bool standbyHeldForFirstBoot;
        private Text powerHint;
        private Text backHint;
        private RawImage powerHintArt;
        private RawImage backHintArt;
        private RawImage poweredBackHintArt;
        private Text poweredBackHint;
        private RectTransform rightGoldRailPowered;
        private RectTransform rightGoldRailStandby;
        private Button onScreenBack;
        private Button poweredStandButton;
        private Button shutdownButton;
        private RectTransform shutdownRoot;
        private RectTransform powerGlowRoot;
        private RectTransform greenPowerGlowRoot;
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
        public LaptopCinematicLightPresentation Lighting => lighting;

        /// <summary>Called by the host view even while YOMI owns the focus.
        /// Updates the same photograph instead of creating a second scene.</summary>
        public void UpdateLighting(float deltaTime)
        {
            if (!root) return;
            // This is the same YOMI screenshot used by the physical LCD,
            // not a second generated imitation of the desktop.
            Texture source = LaptopYomiMirror.Current
                ? (Texture)LaptopYomiMirror.Current
                : (desktopClock && desktopClock.mainTexture
                    ? desktopClock.mainTexture : yomiWallpaper);
            screenBounce.Tick(source, LaptopPowerSession.PoweredOn, deltaTime);
            lighting?.SetScreenBounce(screenBounce.Tint, screenBounce.Intensity,
                screenBounce.Bloom, screenBounce.Specular, screenBounce.SideBias);
            lighting?.Tick(deltaTime);
        }
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
            Action bounceSound = null, Func<bool> roomLightOn = null,
            Func<bool> hallwayLightOn = null)
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
            // Power is already latched at the calibrated physical key contact.
            // No new switch/state; screen spill tracks the real laptop power.
            lighting = new LaptopCinematicLightPresentation(
                roomLightOn, hallwayLightOn, () => LaptopPowerSession.PoweredOn);
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
                shutdownRequested = false;
                returnRequested = false;
                returningFromLaptop = true;
                startingFirstBoot = false;
                standbyHeldForFirstBoot = false;
                if (povGroup) povGroup.alpha = 1f;
                CurrentPhase = Phase.PreBootChoice;
                // Existing POV is already drawn during the YOMI close fade.
                // Show its original hints before the first resumed frame,
                // while keyboard/mouse interaction remains locked.
                SetPromptVisibility(true, false);
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
                shutdownRequested = false;
                returnRequested = false;
                returningFromLaptop = resumeFromLaptop;
                startingFirstBoot = !LaptopPowerSession.PoweredOn;
                standbyHeldForFirstBoot = false;
                BuildOverlay();
                active = true;
                CurrentPhase = Phase.Approach;
                // The whole POV (and its GOLD UI) fades in together during
                // seating; never delay the hints until HandsStart ~1.8 s.
                // Inputs are still disabled until the camera is seated.
                SetPromptVisibility(true, false);
                KeepPoweredLCDReady(); // powered LCD visible from first seating frame
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
            RawImage povPhoto = ui.Art(povRoot, "UnmodifiedScreenOffPOV", povTexture,
                imageX, imageY, imageWidth, imageHeight);
            // The original 1672x941 photo remains the source texture. The
            // unified two-zone GPU mask only changes the displayed lighting.
            // Bind samples GameSession BEFORE the first cinematic frame.
            lighting.Bind(povPhoto);

            // V11.9: use the actual owner's Workshop V2 export as FINAL inner
            // LCD source pixels. Never add the legacy 32/38/5px insets twice.
            ScreenCalibration calibration = new ScreenCalibration();
            bool userCalibratedV2 = false;
            Vector2[] warpEdges = null;
            Vector2[] warpHelpers = null;
            // Prefer complete V4 LCD shape: all sixteen owner's controls.
            // Bezel reference points are diagnostics, NOT the fitted LCD.
            TextAsset v4Text = Resources.Load<TextAsset>(
                "LaptopCinematic/screen_calibration_v4");
            if (v4Text)
            {
                try
                {
                    var candidate = JsonUtility.FromJson<ScreenCalibration>(v4Text.text);
                    if (TryBuildV4Warp(candidate, povTexture,
                        out _, out warpEdges, out warpHelpers))
                    {
                        calibration = candidate;
                        userCalibratedV2 = true;
                    }
                    else Debug.LogError(
                        "ROKAS LCD: invalid V4; falling back to approved V2 geometry.");
                }
                catch (Exception error)
                {
                    Debug.LogError("ROKAS LCD V4 JSON rejected: " + error.Message);
                }
            }
            TextAsset v2Text = Resources.Load<TextAsset>(
                "LaptopCinematic/screen_calibration_v2");
            if (!userCalibratedV2 && v2Text)
            {
                try
                {
                    var candidate = JsonUtility.FromJson<ScreenCalibration>(v2Text.text);
                    if (IsValidV2(candidate, povTexture))
                    {
                        calibration = candidate;
                        userCalibratedV2 = true;
                    }
                    else Debug.LogError(
                        "ROKAS LCD: invalid Workshop V2 source pixels/radii; using legacy calibration.");
                }
                catch (Exception error)
                {
                    Debug.LogError("ROKAS LCD V2 JSON rejected: " + error.Message);
                }
            }
            if (!userCalibratedV2)
            {
                TextAsset legacy = Resources.Load<TextAsset>("LaptopCinematic/screen_calibration");
                if (legacy)
                {
                    try { calibration = JsonUtility.FromJson<ScreenCalibration>(
                        legacy.text) ?? new ScreenCalibration(); }
                    catch (Exception error)
                    {
                        Debug.LogWarning("ROKAS legacy LCD calibration ignored: " + error.Message);
                    }
                }
            }
            Vector2[] safeCorners = userCalibratedV2
                ? new[]
                {
                    new Vector2(calibration.topLeftX, calibration.topLeftY),
                    new Vector2(calibration.topRightX, calibration.topRightY),
                    new Vector2(calibration.bottomRightX, calibration.bottomRightY),
                    new Vector2(calibration.bottomLeftX, calibration.bottomLeftY)
                }
                : new[]
                {
                    new Vector2(calibration.topLeftX + 32f, calibration.topLeftY + 5f),
                    new Vector2(calibration.topRightX - 5f, calibration.topRightY + 5f),
                    new Vector2(calibration.bottomRightX - 5f, calibration.bottomRightY - 5f),
                    new Vector2(calibration.bottomLeftX + 38f, calibration.bottomLeftY - 5f)
                };
            ComputeLCDBounds(safeCorners, warpEdges, warpHelpers,
                out float minLcdX, out float minLcdY,
                out float maxLcdX, out float maxLcdY);
            RectTransform screenRect = ui.Rect(povRoot, "LaptopWakePolygon",
                imageX, imageY, imageWidth, imageHeight);
            wake = screenRect.gameObject.AddComponent<LaptopWakeQuad>();
            wake.raycastTarget = false;
            wake.SetCorners(safeCorners, imageScale);
            ConfigureRoundedLCD(wake, userCalibratedV2, calibration,
                warpEdges, warpHelpers);
            wake.color = new Color(.16f, .34f, .48f, 0f);

            // Physical LCD is fully live (YOMI's real app catalog + unread
            // session state + real OS clock). No screenshot of a desktop is used.
            // The approved OFF-state POV and screen corners stay unchanged.
            Vector2[] lcdCorners = (Vector2[])safeCorners.Clone();
            RectTransform desktopRect = ui.Rect(povRoot, "LaptopPhysicalDesktop",
                imageX, imageY, imageWidth, imageHeight);
            poweredWallpaper = desktopRect.gameObject.AddComponent<LaptopPerspectiveQuad>();
            poweredWallpaper.SetCorners(lcdCorners, imageScale);
            ConfigureRoundedLCD(poweredWallpaper, userCalibratedV2, calibration,
                warpEdges, warpHelpers);
            poweredWallpaper.SetImage(null); // live atlas is the sole ON-state image
            poweredWallpaper.color = new Color(.08f, .16f, .27f, 1f);
            poweredWallpaper.raycastTarget = false;
            RectTransform liveClockRect = ui.Rect(povRoot, "PhysicalDesktopLiveClock",
                imageX, imageY, imageWidth, imageHeight);
            desktopClock = liveClockRect.gameObject.AddComponent<LaptopPhysicalDesktopClock>();
            desktopClock.SetCorners(lcdCorners, imageScale);
            ConfigureRoundedLCD(desktopClock, userCalibratedV2, calibration,
                warpEdges, warpHelpers);
            desktopClock.raycastTarget = false;
            desktopClock.Initialize(unreadMessages, yomiWallpaper);
            RectTransform lcdHit = ui.Rect(povRoot, "PhysicalDesktopClickTarget",
                imageX + minLcdX * imageScale,
                imageY + minLcdY * imageScale,
                (maxLcdX - minLcdX) * imageScale,
                (maxLcdY - minLcdY) * imageScale);
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
            // Exact supplied transparent blue/green status LEDs. They are
            // separated Resources sprites on the SAME calibrated power key.
            // Procedural lights are diagnostic fallbacks only.
            Texture2D powerAtlas = Resources.Load<Texture2D>(
                "LaptopCinematic/UI/power_control_atlas");
            Texture2D blueLed = powerAtlas ? powerAtlas :
                Resources.Load<Texture2D>("LaptopCinematic/UI/laptop_power_blue");
            Texture2D greenLed = powerAtlas ? powerAtlas :
                Resources.Load<Texture2D>("LaptopCinematic/UI/laptop_power_green");
            // User approved LEDs: enlarge the real physical key, with a
            // separate translucent bloom of the SAME photographic LED texture.
            // Both colours share one calibrated center and never coexist.
            const float ledHaloSize = 64f;
            const float ledFaceSize = 50f;
            powerGlowRoot = ui.Rect(povRoot, "PowerKeyBlueStandby",
                keyX - ledHaloSize * .5f, keyY - ledHaloSize * .5f,
                ledHaloSize, ledHaloSize);
            greenPowerGlowRoot = ui.Rect(povRoot, "PowerKeyGreenActive",
                keyX - ledHaloSize * .5f, keyY - ledHaloSize * .5f,
                ledHaloSize, ledHaloSize);
            if (blueLed)
            {
                var soft = ui.Art(powerGlowRoot, "BlueLedPhotographicHalo",
                    blueLed, 0f, 0f, ledHaloSize, ledHaloSize);
                soft.color = new Color(1f, 1f, 1f, .39f);
                var icon = ui.Art(powerGlowRoot, "SuppliedBluePowerLED",
                    blueLed, (ledHaloSize-ledFaceSize)*.5f,
                    (ledHaloSize-ledFaceSize)*.5f, ledFaceSize, ledFaceSize);
                icon.color = Color.white;
                if (powerAtlas)
                {
                    Rect uv = new Rect(72f / 470f, 2f / 224f, 64f / 470f, 64f / 224f);
                    soft.uvRect = uv;
                    icon.uvRect = uv;
                }
                soft.raycastTarget = false;
                icon.raycastTarget = false;
            }
            else
            {
                ui.Box(powerGlowRoot, "FallbackBlueHalo", 0f, 0f, 36f, 36f,
                    new Color(.02f,.22f,.82f,.055f));
                ui.Box(powerGlowRoot, "FallbackBlueLED", 15f, 15f, 6f, 6f,
                    new Color(.24f,.72f,1f,.85f));
            }
            if (greenLed)
            {
                var soft = ui.Art(greenPowerGlowRoot, "GreenLedPhotographicHalo",
                    greenLed, 0f, 0f, ledHaloSize, ledHaloSize);
                soft.color = new Color(1f, 1f, 1f, .39f);
                var icon = ui.Art(greenPowerGlowRoot, "SuppliedGreenPowerLED",
                    greenLed, (ledHaloSize-ledFaceSize)*.5f,
                    (ledHaloSize-ledFaceSize)*.5f, ledFaceSize, ledFaceSize);
                icon.color = Color.white;
                if (powerAtlas)
                {
                    Rect uv = new Rect(0f, 2f / 224f, 64f / 470f, 64f / 224f);
                    soft.uvRect = uv;
                    icon.uvRect = uv;
                }
                soft.raycastTarget = false;
                icon.raycastTarget = false;
            }
            else
            {
                ui.Box(greenPowerGlowRoot, "FallbackGreenHalo", 0f, 0f, 36f, 36f,
                    new Color(.05f,.62f,.20f,.06f));
                ui.Box(greenPowerGlowRoot, "FallbackGreenLED", 15f, 15f, 6f, 6f,
                    new Color(.36f,1f,.53f,.98f));
            }

            // UI hit target over the calibrated physical Power key, not a fake
            // full-screen confirmation button. E remains the primary shortcut.
            var hit = ui.Rect(povRoot, "PowerKeyClickTarget", keyX - 26f, keyY - 27f, 52f, 54f);
            var target = hit.gameObject.AddComponent<Image>();
            target.color = new Color(0f, 0f, 0f, .002f);
            var button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                if (LaptopPowerSession.PoweredOn) TryShutdownLaptop();
                else TryPressPower();
            });

            float centerX = (calibration.topLeftX + calibration.topRightX
                + calibration.bottomLeftX + calibration.bottomRightX) * .25f;
            float centerY = (calibration.topLeftY + calibration.topRightY
                + calibration.bottomLeftY + calibration.bottomRightY) * .25f;
            // Both standby chibi and the original full-UV YOMI screenshot
            // use the SAME polygon as all fallback desktop/backlight layers.
            // Right edge is unchanged; only the photographed left edge is
            // moved further inside (previous top/bottom: 22/26).
            // Extents already include all curved V4 side/tangent controls.
            // The inner bounding clip is now larger than their visible mesh.
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
                new Color(.003f, .007f, .015f, 1f),
                userCalibratedV2, calibration, warpEdges, warpHelpers);
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
                new Color(.01f, .025f, .05f, 1f),
                userCalibratedV2, calibration, warpEdges, warpHelpers);
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
            ConfigureRoundedLCD(physicalExactYomiQuad, userCalibratedV2,
                calibration, warpEdges, warpHelpers);
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
            // V8.1: both visual power states match the user's TWO
            // 1672x941 screenshot references, mapped to 1920x1080 stage.
            // OFF: E, ESC stacked on the right. ON: R, E, Q, ESC there.
            // A narrow gold ornamental rail is next to (not over) buttons.
            Texture2D onArt = Resources.Load<Texture2D>(
                "LaptopCinematic/UI/prompt_power_v8");
            if (!onArt)
                onArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_power");
            Texture2D exitArt = Resources.Load<Texture2D>(
                "LaptopCinematic/UI/prompt_back_v8");
            if (!exitArt)
                exitArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_back");
            Texture2D standArt = Resources.Load<Texture2D>(
                "LaptopCinematic/UI/prompt_stand_r_v8");
            if (!standArt)
                standArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_stand_r");
            if (!standArt)
                standArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_stand");

            if (onArt)
            {
                const float width = 396f;
                powerHintArt = ui.Art(povRoot, "PowerChoiceHint", onArt,
                    1446f, 254f, width, width * (float)onArt.height / onArt.width);
                powerHintArt.color = Color.white;
                AddGoldEmissionBoost(powerHintArt, onArt);
                powerHintArt.raycastTarget = true;
                var startButton = powerHintArt.gameObject.AddComponent<Button>();
                startButton.targetGraphic = powerHintArt;
                startButton.transition = Selectable.Transition.None;
                startButton.onClick.AddListener(() => TryPressPower());
                powerHintArt.gameObject.AddComponent<LaptopChoiceHover>();
            }
            else
                powerHint = ui.Label(povRoot, "PowerChoiceHint",
                    "E — ВКЛЮЧИТЬ НОУТБУК", 1450f, 272f, 384f, 58f,
                    23, new Color(1f, .83f, .53f, 1f));

            if (exitArt)
            {
                // Separate ESC rects avoid a positional jump on hover when
                // changing power state. Both retain the same BackToRoom path.
                const float width = 350f;
                float height = width * (float)exitArt.height / exitArt.width;
                backHintArt = ui.Art(povRoot, "BackChoiceHint", exitArt,
                    1490f, 385f, width, height);
                backHintArt.color = Color.white;
                AddGoldEmissionBoost(backHintArt, exitArt);
                backHintArt.raycastTarget = true;
                var leaveButton = backHintArt.gameObject.AddComponent<Button>();
                leaveButton.targetGraphic = backHintArt;
                leaveButton.transition = Selectable.Transition.None;
                leaveButton.onClick.AddListener(() => BackToRoom());
                backHintArt.gameObject.AddComponent<LaptopChoiceHover>();

                poweredBackHintArt = ui.Art(povRoot, "PoweredBackChoiceHint",
                    exitArt, 1490f, 531f, width, height);
                poweredBackHintArt.color = Color.white;
                AddGoldEmissionBoost(poweredBackHintArt, exitArt);
                poweredBackHintArt.raycastTarget = true;
                var leavePowered = poweredBackHintArt.gameObject.AddComponent<Button>();
                leavePowered.targetGraphic = poweredBackHintArt;
                leavePowered.transition = Selectable.Transition.None;
                leavePowered.onClick.AddListener(() => BackToRoom());
                poweredBackHintArt.gameObject.AddComponent<LaptopChoiceHover>();
            }
            else
            {
                backHint = ui.Label(povRoot, "BackChoiceHint",
                    "ESC — НАЗАД", 1500f, 402f, 320f, 54f,
                    22, new Color(1f, .83f, .53f, 1f));
                poweredBackHint = ui.Label(povRoot, "PoweredBackChoiceHint",
                    "ESC — НАЗАД", 1500f, 547f, 320f, 54f,
                    22, new Color(1f, .83f, .53f, 1f));
            }

            // Never reintroduce the obsolete standalone left-arrow.
            onScreenBack = null;

            if (standArt)
            {
                const float width = 355f;
                float height = width * (float)standArt.height / standArt.width;
                var standImage = ui.Art(povRoot, "PoweredStandOriginalPNG",
                    standArt, 1480f, 181f, width, height);
                standImage.color = Color.white;
                AddGoldEmissionBoost(standImage, standArt);
                standImage.raycastTarget = true;
                poweredStandButton = standImage.gameObject.AddComponent<Button>();
                poweredStandButton.targetGraphic = standImage;
                poweredStandButton.transition = Selectable.Transition.None;
                poweredStandButton.onClick.AddListener(() => BackToRoom());
                standImage.gameObject.AddComponent<LaptopChoiceHover>();
            }

            Texture2D openArt = Resources.Load<Texture2D>(
                "LaptopCinematic/UI/prompt_open_yomi_v8");
            if (!openArt)
                openArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_open_yomi");
            if (!openArt)
                openArt = Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_open");
            poweredOpenRoot = ui.Rect(povRoot,
                "PoweredLaptopOpenHint", 1452f, 287f, 390f, 118f);
            if (openArt)
            {
                var openImage = ui.Art(poweredOpenRoot, "ExactGoldenOpenPrompt",
                    openArt, 0f, 0f, 390f,
                    390f * (float)openArt.height / openArt.width);
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
                var openSurface = poweredOpenRoot.gameObject.AddComponent<LaptopSurface>();
                openSurface.Radius = 22f;
                openSurface.color = new Color(.65f, .31f, .08f, 1f);
                var openButton = poweredOpenRoot.gameObject.AddComponent<Button>();
                openButton.targetGraphic = openSurface;
                openButton.transition = Selectable.Transition.None;
                openButton.onClick.AddListener(() => TryPressPower());
                poweredOpenRoot.gameObject.AddComponent<LaptopChoiceHover>();
                poweredOpenHint = ui.Label(poweredOpenRoot, "OpenYomiGoldText",
                    "E — ОТКРЫТЬ НОУТБУК", 6f, 18f, 377f, 64f,
                    23, new Color(1f, .91f, .70f, 1f),
                    false, TextAnchor.MiddleCenter);
                // The fallback uses the same right-hand vertical layout.
            }

            Texture2D shutdownArt = Resources.Load<Texture2D>(
                "LaptopCinematic/UI/prompt_shutdown_v8");
            bool shutdownUsesAtlas = false;
            if (!shutdownArt)
            {
                shutdownArt = powerAtlas;
                shutdownUsesAtlas = shutdownArt != null;
            }
            if (!shutdownArt)
                shutdownArt = Resources.Load<Texture2D>(
                    "LaptopCinematic/UI/prompt_shutdown");
            shutdownRoot = ui.Rect(povRoot,
                "PoweredShutdownHint", 1490f, 419f, 353f, 105f);
            if (shutdownArt)
            {
                RawImage art = ui.Art(shutdownRoot, "GoldShutdownArtwork",
                    shutdownArt, 0f, 0f, 353f,
                    shutdownUsesAtlas ? 353f * 152f / 470f :
                        353f * (float)shutdownArt.height / shutdownArt.width);
                if (shutdownUsesAtlas)
                    art.uvRect = new Rect(0f, 72f / 224f, 1f, 152f / 224f);
                art.color = Color.white;
                art.raycastTarget = true;
                AddGoldEmissionBoost(art, shutdownArt);
                shutdownButton = art.gameObject.AddComponent<Button>();
                shutdownButton.targetGraphic = art;
                // Vertical stack; no horizontal-baseline reflow.
            }
            else
            {
                // Missing external art must not disable turning off the computer.
                var hitSurface = shutdownRoot.gameObject.AddComponent<LaptopSurface>();
                hitSurface.Radius = 23f;
                hitSurface.color = new Color(.30f, .14f, .03f, .94f);
                shutdownButton = shutdownRoot.gameObject.AddComponent<Button>();
                shutdownButton.targetGraphic = hitSurface;
                ui.Label(shutdownRoot, "GoldShutdownText",
                    "Q — ВЫКЛЮЧИТЬ НОУТБУК", 12f, 16f, 331f, 62f,
                    22, new Color(1f, .90f, .67f, 1f),
                    false, TextAnchor.MiddleCenter);
                // Match the approved right-side reference even without PNG.
            }
            shutdownButton.transition = Selectable.Transition.None;
            shutdownButton.onClick.AddListener(() => TryShutdownLaptop());
            shutdownRoot.gameObject.AddComponent<LaptopChoiceHover>();

            // Same gold ornaments as the supplied screenshots: a delicate
            // vertical gold/copper rule with two small hollow diamonds.
            // Rail has no raycast and remains independent of key hover scale.
            rightGoldRailPowered = AddGoldChoiceRail(povRoot,
                "PoweredGoldChoiceRail", 1847f, 122f, 555f);
            rightGoldRailStandby = AddGoldChoiceRail(povRoot,
                "StandbyGoldChoiceRail", 1847f, 207f, 279f);
            SetPromptVisibility(false);
        }

        private RectTransform AddGoldChoiceRail(Transform parent,
            string name, float centerX, float top, float length)
        {
            // Unity UI pixels, top-left authored; not a raycast target.
            RectTransform rail = ui.Rect(parent, name,
                centerX - 9f, top, 18f, length);
            ui.Box(rail, "AmberAtmosphere", 5f, 5f, 8f,
                length - 10f, new Color(1f, .45f, .025f, .055f));
            ui.Box(rail, "CopperFilament", 8f, 5f, 2f,
                length - 10f, new Color(.95f, .49f, .06f, .66f));
            ui.Box(rail, "GoldEdge", 9f, 6f, 1f,
                length - 12f, new Color(1f, .80f, .26f, .83f));
            AddGoldRailDiamond(rail, "TopGoldDiamond", 4f);
            AddGoldRailDiamond(rail, "BottomGoldDiamond", length - 4f);
            return rail;
        }

        private void AddGoldRailDiamond(RectTransform rail,
            string name, float centerY)
        {
            RectTransform diamond = ui.Rect(rail, name,
                4f, centerY - 5f, 10f, 10f);
            // Diamond centers are aligned to the slim filament x = 9.
            diamond.pivot = new Vector2(.5f, .5f);
            diamond.anchoredPosition = new Vector2(9f, -centerY);
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image border = diamond.gameObject.AddComponent<Image>();
            border.color = new Color(1f, .60f, .08f, .96f);
            border.raycastTarget = false;
            ui.Box(diamond, "HollowDiamond", 2f, 2f, 6f, 6f,
                new Color(.17f, .085f, .022f, .93f));
        }

        private static void ConfigureRoundedLCD(LaptopPerspectiveQuad quad,
            bool calibratedLCD, ScreenCalibration calibration,
            Vector2[] warpEdges, Vector2[] warpHelpers)
        {
            if (!quad || calibration == null) return;
            quad.SetRoundedCorners(calibratedLCD && calibration.enableRoundedMask,
                calibration.topLeftRadius, calibration.topRightRadius,
                calibration.bottomRightRadius, calibration.bottomLeftRadius);
            quad.SetWarpOffsets(warpEdges, warpHelpers);
        }

        private static void AddLcdStencil(RectTransform viewport, Vector2[] localCorners,
            float scale, Color screenBackground, bool calibratedLCD,
            ScreenCalibration calibration, Vector2[] warpEdges,
            Vector2[] warpHelpers)
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
            ConfigureRoundedLCD(quad, calibratedLCD, calibration,
                warpEdges, warpHelpers);
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
            glow.uvRect = original.uvRect;
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
            // Remain seated through shutdown; do not invoke fullscreen YOMI.
            while (active)
            {
                RenderAt(HandsStart);
                CurrentPhase = Phase.PreBootChoice;
                SetPromptVisibility(true);
                while (active && !powerConfirmed && !returnRequested &&
                    !shutdownRequested)
                {
                    RenderAt(HandsStart);
                    if (LaptopPowerSession.PoweredOn && desktopClock)
                        desktopClock.UpdateClockIfNecessary();
                    yield return null;
                }
                if (!active) yield break;
                if (shutdownRequested)
                {
                    yield return PlayShutdownHand();
                    if (!active) yield break;
                    shutdownRequested = false;
                    powerConfirmed = false;
                    CurrentPhase = Phase.PreBootChoice;
                    SetPromptVisibility(true);
                    continue;
                }
                if (returnRequested)
                {
                    CurrentPhase = Phase.CancelBack;
                    float exitElapsed = 0f;
                    while (active && exitElapsed < ReturnDuration)
                    {
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
                yield break;
            }
        }

        private IEnumerator PlayShutdownHand()
        {
            if (!LaptopPowerSession.PoweredOn) yield break;
            CurrentPhase = Phase.HandsInteraction;
            SetPromptVisibility(false, false);
            float elapsed = HandsStart;
            while (active && elapsed < LaptopPowerTimeline.ContactTime)
            {
                RenderHandFrame(elapsed, true);
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .06f);
            }
            if (!active) yield break;
            RenderHandFrame(LaptopPowerTimeline.ContactTime, true);
            powerClick?.Invoke();  // existing calibrated one-click SFX
            LaptopPowerSession.Shutdown();
            screenBounce.Tick(null, false, 0f);
            lighting?.SetScreenBounce(screenBounce.Tint, 0f, 0f, 0f, 0f);
            lighting?.Snap();
            powerTimeline = new LaptopPowerTimeline();
            startingFirstBoot = true;
            if (wake)
            {
                Color tint = wake.color;
                tint.a = .042f;
                wake.color = tint;
            }
            SetPromptVisibility(true, false); // existing jumping chibi returns
            float retreat = 0f;
            while (active && retreat < ShutdownHandReturn)
            {
                float eased = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(retreat / ShutdownHandReturn));
                RenderHandFrame(Mathf.Lerp(LaptopPowerTimeline.ContactTime,
                    HandsStart, eased), true);
                yield return null;
                retreat += Mathf.Min(Time.unscaledDeltaTime, .06f);
            }
            RenderHandFrame(HandsStart, false);
            shownFrame = -1;
        }

        private void RenderHandFrame(float t, bool visible)
        {
            if (!handsImage) return;
            handsImage.gameObject.SetActive(visible);
            if (!visible || frames == null || frames.Length == 0 ||
                manifest == null) return;
            int index = Mathf.Clamp(
                Mathf.FloorToInt((t - HandsStart) * manifest.fps), 0, frames.Length - 1);
            if (index == shownFrame) return;
            HandFrame frame = manifest.frames[index];
            handsImage.texture = frames[index];
            RectTransform r = handsImage.rectTransform;
            r.anchoredPosition = new Vector2(
                imageX + frame.x * imageScale,
                -(imageY + frame.y * imageScale));
            r.sizeDelta = new Vector2(frame.w * imageScale, frame.h * imageScale);
            shownFrame = index;
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

        private void SetPromptVisibility(bool show, bool allowInput = true)
        {
            bool powered = LaptopPowerSession.PoweredOn;
            if (powerHint) powerHint.gameObject.SetActive(show && !powered);
            if (backHint) backHint.gameObject.SetActive(show && !powered);
            if (poweredBackHint) poweredBackHint.gameObject.SetActive(show && powered);
            if (powerHintArt) powerHintArt.gameObject.SetActive(show && !powered);
            if (backHintArt) backHintArt.gameObject.SetActive(show && !powered);
            if (poweredBackHintArt) poweredBackHintArt.gameObject.SetActive(show && powered);
            if (rightGoldRailStandby) rightGoldRailStandby.gameObject.SetActive(show && !powered);
            if (rightGoldRailPowered) rightGoldRailPowered.gameObject.SetActive(show && powered);
            if (onScreenBack) onScreenBack.gameObject.SetActive(show && (!powered || !poweredStandButton));
            if (poweredStandButton) poweredStandButton.gameObject.SetActive(show && powered);
            if (powerGlowRoot) powerGlowRoot.gameObject.SetActive(!powered && !returnRequested &&
                (show || (powerConfirmed && CurrentPhase != Phase.PowerOn && CurrentPhase != Phase.UIOpen)));
            if (greenPowerGlowRoot) greenPowerGlowRoot.gameObject.SetActive(powered &&
                (show || shutdownRequested || CurrentPhase == Phase.Approach ||
                 CurrentPhase == Phase.POVTransition));
            if (shutdownRoot) shutdownRoot.gameObject.SetActive(show && powered);
            if (noSignalScreen) noSignalScreen.gameObject.SetActive(show && !powered);
            if (noSignalText && noSignalScreen && noSignalScreen.gameObject.activeSelf)
                noSignalText.gameObject.SetActive(noSignalBounce == null);
            if (physicalMiniYomiViewport)
                physicalMiniYomiViewport.gameObject.SetActive(powered &&
                    (show || returnRequested || returningFromLaptop ||
                     (!powerConfirmed && (CurrentPhase == Phase.Approach ||
                                           CurrentPhase == Phase.POVTransition)) || shutdownRequested));
            if (show && powered && LaptopYomiMirror.Current) RefreshExactYomiMirror();
            bool lcdVisible = physicalMiniYomiViewport && physicalMiniYomiViewport.gameObject.activeSelf;
            if (poweredWallpaper) poweredWallpaper.gameObject.SetActive(lcdVisible && !LaptopYomiMirror.Current);
            if (desktopClock) desktopClock.gameObject.SetActive(lcdVisible && !LaptopYomiMirror.Current);
            if (poweredDesktopClick) poweredDesktopClick.gameObject.SetActive(show && powered);
            if (poweredOpenRoot) poweredOpenRoot.gameObject.SetActive(show && powered);
            if (povGroup)
            {
                povGroup.blocksRaycasts = show && allowInput;
                povGroup.interactable = show && allowInput;
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

        public bool TryShutdownLaptop()
        {
            if (!IsAwaitingPowerChoice || !LaptopPowerSession.PoweredOn ||
                powerConfirmed || returnRequested || shutdownRequested) return false;
            shutdownRequested = true;
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
            RenderHandFrame(t, showHands);
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
            lighting?.Dispose();
            screenBounce.Dispose();
            // This sequence object survives standing up and sitting back down.
            // A fresh estimator is required after disposing its GPU readback RT.
            screenBounce = new LaptopScreenBounceEstimator();
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
            poweredBackHint = null;
            poweredBackHintArt = null;
            rightGoldRailPowered = null;
            rightGoldRailStandby = null;
            shutdownRequested = false;
            shutdownButton = null;
            shutdownRoot = null;
            greenPowerGlowRoot = null;
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

    // Wake glow shares the exact same rounded UV clipping and physical LCD
    // geometry as the YOMI texture, fallback clock and standby chibi stencil.
    public sealed class LaptopWakeQuad : LaptopPerspectiveQuad { }
}
