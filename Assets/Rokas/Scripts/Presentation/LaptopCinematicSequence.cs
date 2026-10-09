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
        public enum Phase { Idle, Approach, POVTransition, HandsInteraction, PowerOn, UIOpen }

        [Serializable]
        private sealed class HandManifest
        {
            public float fps = 24f;
            public HandFrame[] frames;
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
        private const float HandsStart = 2.0f;
        private const float PressTime = 3.7f;
        private const float HandsEnd = 5.45f;
        private const float FinishTime = 5.7f;

        private readonly UiKit ui;
        private readonly MonoBehaviour owner;
        private readonly RectTransform transitionLayer;
        private readonly RawImage mainBackground;
        private readonly Action openExistingLaptop;
        private readonly Action cancelled;

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

        public Phase CurrentPhase { get; private set; }
        public bool IsPlaying => active;

        public LaptopCinematicSequence(UiKit ui, MonoBehaviour owner, RectTransform transitionLayer,
            RawImage mainBackground, Action openExistingLaptop, Action cancelled)
        {
            this.ui = ui;
            this.owner = owner;
            this.transitionLayer = transitionLayer;
            this.mainBackground = mainBackground;
            this.openExistingLaptop = openExistingLaptop;
            this.cancelled = cancelled;
        }

        // False means the caller MUST invoke the existing OpenPanel("laptop") immediately.
        public bool TryStart()
        {
            if (active) return true;
            if (!owner || !transitionLayer || !mainBackground || !mainBackground.texture) return false;

            povTexture = Resources.Load<Texture2D>("LaptopCinematic/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF");
            var json = Resources.Load<TextAsset>("LaptopCinematic/hands_manifest");
            if (!povTexture || !json) return false;
            if (povTexture.width != 1672 || povTexture.height != 941) return false;

            try { manifest = JsonUtility.FromJson<HandManifest>(json.text); }
            catch (Exception error)
            {
                Debug.LogWarning("ROKAS laptop cinematic: invalid hand manifest: " + error.Message);
                return false;
            }

            if (manifest == null || manifest.frames == null || manifest.frames.Length < 2 ||
                manifest.fps < 1f || manifest.fps > 120f) return false;

            frames = new Texture2D[manifest.frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                HandFrame frame = manifest.frames[i];
                if (frame == null || string.IsNullOrEmpty(frame.resource)) return false;
                frames[i] = Resources.Load<Texture2D>(frame.resource);
                if (!frames[i] || frame.w <= 0f || frame.h <= 0f ||
                    frames[i].width != Mathf.RoundToInt(frame.w) ||
                    frames[i].height != Mathf.RoundToInt(frame.h)) return false;
            }

            try
            {
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

            // These four corners are STARTING CALIBRATION VALUES on the approved PNG.
            // They must be tuned against a rendered hand-press proof in Unity.
            RectTransform screenRect = ui.Rect(povRoot, "LaptopWakePolygon",
                imageX, imageY, imageWidth, imageHeight);
            wake = screenRect.gameObject.AddComponent<LaptopWakeQuad>();
            wake.raycastTarget = false;
            wake.SetCorners(new[]
            {
                new Vector2(596f, 413f), new Vector2(1044f, 412f),
                new Vector2(1051f, 676f), new Vector2(586f, 676f)
            }, imageScale);
            wake.color = new Color(.16f, .34f, .48f, 0f);

            handsImage = ui.Art(povRoot, "HandAnimationAlphaFrame",
                frames[0], 0f, 0f, 1f, 1f);
            handsImage.gameObject.SetActive(false);
            handsImage.raycastTarget = false;
        }

        private IEnumerator Play()
        {
            float elapsed = 0f;
            while (active && elapsed < FinishTime)
            {
                RenderAt(elapsed);
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, .06f);
            }
            if (active) Finish(true);
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
            else if (t < PressTime) CurrentPhase = Phase.HandsInteraction;
            else CurrentPhase = Phase.PowerOn;

            bool showHands = t >= HandsStart && t <= HandsEnd;
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
                tint.a = t < PressTime ? 0f : .62f *
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PressTime, 4.55f, t));
                wake.color = tint;
            }
        }

        // Escape / skip goes to the real Laptop UI, even during approach or finger press.
        public void SkipToLaptop()
        {
            if (active) Finish(true);
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
