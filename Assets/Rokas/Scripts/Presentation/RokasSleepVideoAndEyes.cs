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
    /// V12 first-person human eyelids. Independent upper/lower asymmetric
    /// cubic-Bezier contours. Black geometry is ONLY outside the visible
    /// eye field, initially continuous and separating into tapered slits
    /// late in closure. No textures, inverted alpha or binocular circles.
    /// </summary>
    internal sealed class RokasAnatomicEyelidGraphic : MaskableGraphic
    {
        private const int Columns = 512;
        private float openness = 1f;
        private bool waking;

        public float OpenAmount
        {
            get { return openness; }
            set {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Abs(clamped - openness) < .0001f) return;
                openness = clamped;
                SetVerticesDirty();
            }
        }

        public void SetWaking(bool value)
        {
            if (waking == value) return;
            waking = value;
            SetVerticesDirty();
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Bezier(float t, float c1, float c2)
        {
            float q = 1f - t;
            return 3f * q * q * t * c1 + 3f * q * t * t * c2;
        }

        internal static float Darkness(float open)
        {
            return Ease((1f - Mathf.Clamp01(open)) / .17f);
        }

        private static bool EyeContour(float x, float closure, bool right,
            bool waking, out float upper, out float lower)
        {
            upper = lower = .415f;
            float lag = right ? (waking ? .022f : .030f) : 0f;
            float c = Mathf.Clamp01((closure - lag) / (1f - lag));
            float halfWidth = Mathf.Lerp(.222f, .316f,
                Ease(Mathf.Clamp01((1f - c) / .76f)));
            float centerX = right ? .750f : .250f;
            float t = (x - (centerX - halfWidth)) / (2f * halfWidth);
            if (t <= 0f || t >= 1f) return false;

            // Different independent Bezier upper/lower arches, both tapered
            // to a real canthus rather than semicircle ends.
            float topArch = Bezier(t, right ? 1.20f : 1.30f,
                right ? 1.02f : .92f) / .83f;
            float bottomArch = Bezier(t, right ? .85f : .72f,
                right ? .83f : 1.02f) / .69f;
            // Upper lid carries ~75% of the movement, lower follows gently.
            float upperMotion = Mathf.Pow(c, waking ? .93f : .79f);
            float lowerMotion = Mathf.Pow(c, waking ? 1.19f : 1.36f);
            float tilt = (right ? -.006f : .008f) * (2f * t - 1f) *
                Mathf.Sin(Mathf.PI * t) * (1f - c);
            upper = .415f + .555f * (1f - upperMotion) * topArch + tilt;
            lower = .415f - .205f * (1f - lowerMotion) * bottomArch + tilt;
            return upper > lower;
        }

        internal static bool GetContourAt(float x, float open, bool waking,
            out float upper, out float lower)
        {
            float closure = 1f - Mathf.Clamp01(open);
            bool left = EyeContour(x, closure, false, waking,
                out float lu, out float ll);
            bool right = EyeContour(x, closure, true, waking,
                out float ru, out float rl);
            if (!left && !right) {
                upper = lower = .415f;
                return false;
            }
            upper = left ? lu : ru;
            lower = left ? ll : rl;
            if (right) {
                upper = Mathf.Max(upper, ru);
                lower = Mathf.Min(lower, rl);
            }
            return upper - lower > .0001f;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float darkness = Darkness(openness);
            if (darkness < .0001f) return;
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;
            byte opaque = (byte)Mathf.RoundToInt(darkness * 255f);
            float feather = Mathf.Max(.0015f, 2.5f / Mathf.Max(480f, r.height)) * r.height;
            // 512 columns and four quad strips: smooth 1080p contours.
            // The clear region has NO geometry, so negative-space cannot invert.
            for (int i = 0; i < 512; ++i)
            {
                float u0 = i / 512f;
                float u1 = (i + 1f) / 512f;
                bool open0 = GetContourAt(u0, openness, waking, out float a0, out float b0);
                bool open1 = GetContourAt(u1, openness, waking, out float a1, out float b1);
                float x0 = r.xMin + u0 * r.width;
                float x1 = r.xMin + u1 * r.width;
                if (!open0 && !open1) {
                    AddQuad(vh, x0, x1, r.yMin, r.yMin, r.yMax, r.yMax,
                        opaque, opaque);
                    continue;
                }
                if (!open0) { a0 = b0 = .415f; }
                if (!open1) { a1 = b1 = .415f; }
                float upper0 = r.yMin + a0 * r.height;
                float upper1 = r.yMin + a1 * r.height;
                float lower0 = r.yMin + b0 * r.height;
                float lower1 = r.yMin + b1 * r.height;
                float edge0 = Mathf.Min(feather, Mathf.Max(0f,
                    (upper0 - lower0) * .22f));
                float edge1 = Mathf.Min(feather, Mathf.Max(0f,
                    (upper1 - lower1) * .22f));
                AddQuad(vh, x0, x1, r.yMin, r.yMin,
                    lower0 - edge0, lower1 - edge1, opaque, opaque);
                AddQuad(vh, x0, x1, lower0 - edge0, lower1 - edge1,
                    lower0 + edge0, lower1 + edge1, opaque, 0);
                AddQuad(vh, x0, x1, upper0 - edge0, upper1 - edge1,
                    upper0 + edge0, upper1 + edge1, 0, opaque);
                AddQuad(vh, x0, x1, upper0 + edge0, upper1 + edge1,
                    r.yMax, r.yMax, opaque, opaque);
            }
        }

        private static void AddQuad(VertexHelper vh, float x0, float x1,
            float yBottom0, float yBottom1, float yTop0, float yTop1,
            byte bottomAlpha, byte topAlpha)
        {
            if (x1 <= x0 || (yTop0 <= yBottom0 && yTop1 <= yBottom1)) return;
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = new Color32(0, 0, 0, bottomAlpha);
            v.position = new Vector3(x0, yBottom0, 0f); vh.AddVert(v);
            v.position = new Vector3(x1, yBottom1, 0f); vh.AddVert(v);
            v.color = new Color32(0, 0, 0, topAlpha);
            v.position = new Vector3(x1, yTop1, 0f); vh.AddVert(v);
            v.position = new Vector3(x0, yTop0, 0f); vh.AddVert(v);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    internal sealed class RokasSleepEyeAperture : IDisposable
    {
        private readonly GameObject overlay;
        private readonly RokasAnatomicEyelidGraphic graphic;
        private bool reported;

        public RokasSleepEyeAperture(RectTransform root)
        {
            overlay = new GameObject("ROKAS_V12_Natural_FirstPerson_Eyelids",
                typeof(RectTransform), typeof(Canvas));
            overlay.transform.SetParent(root, false);
            RectTransform panel = (RectTransform)overlay.transform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            var surface = new GameObject("ROKAS_V12_Bezier_UpperLower_Lids",
                typeof(RectTransform), typeof(CanvasRenderer));
            surface.transform.SetParent(overlay.transform, false);
            var rect = (RectTransform)surface.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            graphic = surface.AddComponent<RokasAnatomicEyelidGraphic>();
            graphic.raycastTarget = false;
            graphic.maskable = false;
            graphic.color = Color.white;
            graphic.OpenAmount = 1f;
        }

        public void WarmupOneFrame() { }
        public void BringToFront() { if (overlay != null) overlay.transform.SetAsLastSibling(); }
        public void SetVisible(bool visible) { if (graphic != null) graphic.enabled = visible; }
        public void SetWaking(bool waking) { if (graphic != null) graphic.SetWaking(waking); }
        public bool IsVisible {
            get {
                return overlay != null && overlay.activeInHierarchy && graphic != null &&
                    graphic.isActiveAndEnabled && overlay.GetComponent<Canvas>().sortingOrder >= 30000;
            }
        }
        public void SetOpen(float open)
        {
            if (graphic == null) return;
            graphic.OpenAmount = open;
            if (!reported && open > .35f && open < .65f) {
                reported = true;
                Debug.Log("[ROKAS V12 EYES] Organic contours active; visible=" + IsVisible);
            }
        }
        public void Dispose() { if (overlay != null) UnityEngine.Object.Destroy(overlay); }
    }
}
