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
    /// Real eye-shaped upper+lower eyelid silhouettes, not two black bars.
    /// Precomputed antialiased two-eye aperture masks. 25 fixed textures
    /// rather than rebuilding textures each frame, keeping GC silent.
    /// </summary>
    internal sealed class RokasSleepEyeAperture : IDisposable
    {
        // Old 33 x 640x360 precomputed masks took millions of expensive
        // per-pixel Mathf.Exp/Pow calls on the FIRST bed click and visibly
        // stalled the room-to-ceiling fade. Cache lightweight masks once.
        private const int Steps = 16;
        private readonly GameObject gameObject;
        private readonly RawImage image;
        private readonly Texture2D[] frames = new Texture2D[Steps + 1];
        private readonly Texture2D black;
        private float lastOpen = -1f;
        public RokasSleepEyeAperture(RectTransform root)
        {
            gameObject = new GameObject("Keiko_Real_Eyelid_Curved_Apertures",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            gameObject.transform.SetParent(root, false);
            RectTransform r = (RectTransform)gameObject.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            image = gameObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.white;

            black = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            black.SetPixel(0, 0, Color.black);
            black.Apply(false, true);
            frames[0] = black;
            // Build only 17 small textures (~900k pixels), never 33 large
            // textures (~7.6m pixels) during the first room transition.
            for (int i = 1; i <= Steps; i++)
                frames[i] = RenderMask(i / (float)Steps);
            SetOpen(1f);
        }

        private static Texture2D RenderMask(float open)
        {
            const int w = 320, h = 180;
            var data = new Color32[w * h];
            // 0 = closed; 1 = fully open. Semioval upper+lower lids.
            float openShape = Mathf.Sin(Mathf.PI * .5f * open);
            // The old 0.345 radii caused both eye apertures to overlap
            // into a single oversized opening. These independent, wide
            // half-ovals resemble real upper/lower eyelids in first person.
            float wide = .222f;
            float tall = .255f * openShape;
            for (int y = 0; y < h; ++y)
            for (int x = 0; x < w; ++x)
            {
                float u = (x+.5f) / w, v = (y+.5f) / h;
                float transparency = 0f;
                for (int eye = 0; eye < 2; ++eye)
                {
                    float cx = eye == 0 ? .26f : .74f;
                    float dx = (u-cx)/wide;
                    if (Mathf.Abs(dx) > 1.12f) continue;
                    // Asymmetric organic eye curve with drooping outer edge.
                    float lidCurve = Mathf.Sqrt(Mathf.Max(0f, 1f-dx*dx));
                    float top = .515f + tall*lidCurve*.96f - .012f*dx;
                    float bottom = .515f - tall*lidCurve*.69f - .010f*dx;
                    float feather = .012f + .015f*(1f-open);
                    float a = Mathf.SmoothStep(bottom-feather,
                        bottom+feather, v) *
                        (1f-Mathf.SmoothStep(top-feather, top+feather, v));
                    transparency = Mathf.Max(transparency,a);
                }
                // A subtle dark bridge between both pupils adds a believable
                // first-person brow/nose silhouette at the intermediate state.
                float noseX = Mathf.Abs(u-.5f)/.028f;
                float nose = .42f * Mathf.Max(0f,
                    1f - noseX * noseX);
                float opacity = Mathf.Clamp01(1f-transparency*(1f-nose));
                // Fade residual peripheral darkness completely at the end.
                opacity *= 1f-Mathf.SmoothStep(.86f, 1f, open);
                data[y*w+x] = new Color32(0,0,0,
                    (byte)Mathf.RoundToInt(opacity*255f));
            }
            Texture2D tex = new Texture2D(w,h,TextureFormat.RGBA32,false);
            tex.name = "ROKAS_Eyes_Curved_Open_" +
                Mathf.RoundToInt(open*100f);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(data);
            tex.Apply(false,true);
            return tex;
        }

        // Make the two half-oval lids render OVER the ceiling while closing
        // and reopening; don't let the authored dream video occlude them.
        public void BringToFront()
        {
            if (gameObject != null) gameObject.transform.SetAsLastSibling();
        }

        public void SetOpen(float openness)
        {
            float v = Mathf.Clamp01(openness);
            if (Mathf.Abs(lastOpen-v) < .008f) return;
            lastOpen = v;
            int idx = Mathf.Clamp(Mathf.RoundToInt(v*Steps),0,Steps);
            if (image != null) image.texture = frames[idx];
        }
        public void Dispose()
        {
            foreach (Texture2D t in frames)
                if (t != null) UnityEngine.Object.Destroy(t);
            if (gameObject != null) UnityEngine.Object.Destroy(gameObject);
        }
    }
}
