using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rokas.Presentation
{
    /// <summary>
    /// Original user-supplied chibi Keiko dreaming clip with its original
    /// audio crossfaded for seamless looping. StreamingAssets works
    /// in Unity Editor and Windows builds; no Resources video import needed.
    /// </summary>
    internal sealed class RokasKeikoDreamLoop : IDisposable
    {
        private readonly GameObject gameObject;
        private readonly VideoPlayer player;
        private readonly AudioSource dreamAudio;
        private float audioMix = .50f;
        private readonly RenderTexture target;
        private readonly RawImage image;
        private readonly CanvasGroup group;
        private bool prepared;
        private bool failed;
        public bool Available { get; private set; }
        public float Alpha { get { return group != null ? group.alpha : 0f; }
                             set {
                                 if(group != null) group.alpha = Mathf.Clamp01(value);
                                 RefreshVolume();
                             } }
        public void SetAudioMix(float mix)
        {
            audioMix = Mathf.Clamp01(mix);
            RefreshVolume();
        }
        private void RefreshVolume()
        {
            if (dreamAudio != null)
                dreamAudio.volume = (group != null ? group.alpha : 0f) * audioMix;
        }

        public RokasKeikoDreamLoop(RectTransform parent)
        {
            var uri = Path.Combine(Application.streamingAssetsPath,
                "BedSleep", "KeikoSleepLoop.mp4");
            if (!File.Exists(uri))
            {
                Debug.LogWarning("[ROKAS DREAM] Missing authored Keiko sleep loop at " + uri);
                Available = false;
                return;
            }
            gameObject = new GameObject("Keiko_Original_Dreams_Seamless_Loop",
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage), typeof(CanvasGroup), typeof(VideoPlayer),
                typeof(AudioSource));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            target = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
            target.name = "Keiko_Dream_Loop_RenderTexture";
            target.wrapMode = TextureWrapMode.Clamp;
            target.filterMode = FilterMode.Bilinear;
            target.Create();
            image = gameObject.GetComponent<RawImage>();
            image.texture = target;
            image.color = Color.white;
            image.raycastTarget = false;
            group = gameObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            dreamAudio = gameObject.GetComponent<AudioSource>();
            dreamAudio.playOnAwake = false;
            dreamAudio.loop = false;
            dreamAudio.spatialBlend = 0f;
            dreamAudio.volume = 0f;
            player = gameObject.GetComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = uri;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            player.isLooping = true;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.prepareCompleted += OnPrepared;
            player.errorReceived += OnError;
            Available = true;
            player.Prepare();
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (source != player) return;
            // Original audio is part of the prepared loop asset; only this
            // dedicated AudioSource carries it, so home music/rain ducking
            // does not accidentally mute the dream recording.
            if (player.audioTrackCount > 0)
            {
                player.EnableAudioTrack(0, true);
                player.SetTargetAudioSource(0, dreamAudio);
            }
            prepared = true;
        }
        private void OnError(VideoPlayer source, string error)
        {
            failed = true;
            Available = false;
            Debug.LogWarning("[ROKAS DREAM] Video decoder failed: " + error);
        }
        public bool Begin()
        {
            if (!Available || failed || !prepared || player == null) return false;
            player.time = 0;
            RefreshVolume();
            player.Play();
            return true;
        }
        public IEnumerator AwaitPrepared(float timeout)
        {
            float elapsed = 0f;
            while (Available && !prepared && !failed && elapsed < timeout)
            {
                elapsed += Mathf.Max(.002f, Time.unscaledDeltaTime);
                yield return null;
            }
        }
        // ScreenSpaceOverlay draw order: the dream MUST be above the
        // closed eyelids during sleep, but BELOW the opening-eyes layer
        // after the video fades out.
        public void BringToFront()
        {
            if (gameObject != null) gameObject.transform.SetAsLastSibling();
        }
        public void End()
        {
            if (group != null) group.alpha = 0f;
            RefreshVolume();
            if (player != null && player.isPlaying) player.Stop();
        }
        public void Dispose()
        {
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
                player.errorReceived -= OnError;
                player.Stop();
                player.targetTexture = null;
            }
            if (image != null) image.texture = null;
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
        }
    }

    /// <summary>
    /// V11: Real negative-space binocular eyelids. Black geometry is drawn
    /// ONLY OUTSIDE two transparent eye windows; nothing can be painted into
    /// the openings. Replaces V7-V10 texture-alpha masks, avoiding the
    /// inverted black ellipses visible in the player's 25-Oct recording.
    /// </summary>
    internal sealed class RokasDualOvalLidGraphic : MaskableGraphic
    {
        private const int HorizontalBands = 288;
        private float openAmount = 1f;

        public float OpenAmount
        {
            get { return openAmount; }
            set
            {
                float next = Mathf.Clamp01(value);
                if (Mathf.Abs(openAmount - next) < .0005f) return;
                openAmount = next;
                SetVerticesDirty();
            }
        }

        // Dark contour fades in, rather than abruptly appearing at open=0.99.
        internal static float Darkness(float open)
        {
            float t = Mathf.Clamp01((1f - Mathf.Clamp01(open)) / .28f);
            return t * t * (3f - 2f * t);
        }

        internal static float RelativeHalfHeight(float open)
        {
            return .53f * Mathf.Pow(Mathf.Clamp01(open), 1.16f);
        }

        internal static float RelativeHalfWidth(float open)
        {
            return Mathf.Lerp(.212f, .242f, Mathf.Clamp01(open * 3f));
        }

        internal static bool InsideWindow(float u, float v, float open)
        {
            float ry = RelativeHalfHeight(open);
            if (ry < .0001f) return false;
            float y = (v - .5f) / ry;
            if (Mathf.Abs(y) >= 1f) return false;
            float rx = RelativeHalfWidth(open);
            float left = (u - .25f) / rx;
            float right = (u - .75f) / rx;
            return left * left + y * y < 1f ||
                   right * right + y * y < 1f;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float darkness = Darkness(openAmount);
            if (darkness <= .0001f) return;

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            float halfWidth = RelativeHalfWidth(openAmount) * rect.width;
            float halfHeight = RelativeHalfHeight(openAmount) * rect.height;
            float centerY = rect.center.y;
            float cxL = rect.xMin + rect.width * .25f;
            float cxR = rect.xMin + rect.width * .75f;
            float feather = Mathf.Max(1.5f, rect.width * .00125f);
            byte dark = (byte)Mathf.RoundToInt(darkness * 255f);

            // ONLY opaque strips outside the apertures are triangulated.
            for (int row = 0; row < HorizontalBands; row++)
            {
                float y0 = Mathf.Lerp(rect.yMin, rect.yMax,
                    row / (float)HorizontalBands);
                float y1 = Mathf.Lerp(rect.yMin, rect.yMax,
                    (row + 1f) / HorizontalBands);
                float ym = .5f * (y0 + y1);
                float yNorm = halfHeight > .0001f ?
                    (ym - centerY) / halfHeight : 99f;
                if (Mathf.Abs(yNorm) >= 1f)
                {
                    AddQuad(vh, rect.xMin, rect.xMax, y0, y1,
                        dark, dark);
                    continue;
                }

                float xRadius = halfWidth * Mathf.Sqrt(Mathf.Max(0f,
                    1f - yNorm * yNorm));
                float l0 = cxL - xRadius;
                float l1 = cxL + xRadius;
                float r0 = cxR - xRadius;
                float r1 = cxR + xRadius;
                // Feather alpha out at each of the four curved boundaries.
                AddQuad(vh, rect.xMin, l0 - feather, y0, y1, dark, dark);
                AddQuad(vh, l0 - feather, l0 + feather, y0, y1, dark, 0);
                AddQuad(vh, l1 - feather, l1 + feather, y0, y1, 0, dark);
                AddQuad(vh, l1 + feather, r0 - feather, y0, y1, dark, dark);
                AddQuad(vh, r0 - feather, r0 + feather, y0, y1, dark, 0);
                AddQuad(vh, r1 - feather, r1 + feather, y0, y1, 0, dark);
                AddQuad(vh, r1 + feather, rect.xMax, y0, y1, dark, dark);
            }
        }

        private static void AddQuad(VertexHelper vh, float left, float right,
            float bottom, float top, byte alphaLeft, byte alphaRight)
        {
            if (right <= left) return;
            int start = vh.currentVertCount;
            var vl = UIVertex.simpleVert;
            vl.color = new Color32(0, 0, 0, alphaLeft);
            vl.position = new Vector3(left, bottom, 0f);
            vh.AddVert(vl);
            vl.position = new Vector3(left, top, 0f);
            vh.AddVert(vl);
            vl.color = new Color32(0, 0, 0, alphaRight);
            vl.position = new Vector3(right, top, 0f);
            vh.AddVert(vl);
            vl.position = new Vector3(right, bottom, 0f);
            vh.AddVert(vl);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    internal sealed class RokasSleepEyeAperture : IDisposable
    {
        private readonly GameObject overlayCanvas;
        private readonly RokasDualOvalLidGraphic graphic;
        private bool midpointReported;

        public RokasSleepEyeAperture(RectTransform root)
        {
            overlayCanvas = new GameObject("ROKAS_V11_Real_Black_Eyelid_Cutout_Canvas",
                typeof(RectTransform), typeof(Canvas));
            overlayCanvas.transform.SetParent(root, false);
            var canvasRect = (RectTransform)overlayCanvas.transform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            Canvas canvas = overlayCanvas.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;

            var image = new GameObject("ROKAS_V11_Two_Transparent_Eye_Windows",
                typeof(RectTransform), typeof(CanvasRenderer));
            image.transform.SetParent(overlayCanvas.transform, false);
            var r = (RectTransform)image.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            graphic = image.AddComponent<RokasDualOvalLidGraphic>();
            graphic.color = Color.white;
            graphic.maskable = false;
            graphic.raycastTarget = false;
            graphic.OpenAmount = 1f;
            Debug.Log("[ROKAS V11 EYES] Mesh-based negative-space " +
                "binocular eyelids enabled; no alpha texture or rectangle fallback.");
        }

        public void WarmupOneFrame() { } // No cached textures to build.
        public void BringToFront()
        {
            if (overlayCanvas != null) overlayCanvas.transform.SetAsLastSibling();
        }
        public void SetVisible(bool visible)
        {
            if (graphic != null) graphic.enabled = visible;
        }
        public bool IsVisible
        {
            get
            {
                if (overlayCanvas == null || graphic == null) return false;
                Canvas canvas = overlayCanvas.GetComponent<Canvas>();
                return canvas != null && canvas.enabled && canvas.sortingOrder >= 30000 &&
                    graphic.isActiveAndEnabled && overlayCanvas.activeInHierarchy;
            }
        }
        public void SetOpen(float openness)
        {
            if (graphic == null) return;
            graphic.OpenAmount = openness;
            if (!midpointReported && openness > .35f && openness < .65f)
            {
                midpointReported = true;
                Debug.Log("[ROKAS V11 EYES] Two clear eye windows: " +
                    "open=" + openness.ToString("0.00") +
                    " canvasVisible=" + IsVisible);
            }
        }
        public void Dispose()
        {
            if (overlayCanvas != null)
                UnityEngine.Object.Destroy(overlayCanvas);
        }
    }
}
