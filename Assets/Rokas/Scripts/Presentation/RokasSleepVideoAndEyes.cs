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
    /// Reference-matched binocular POV: a black presentation mask with TWO
    /// continuously narrowing horizontal oval windows. This is not a rectangular
    /// blackout or a pair of sliding bars. Only the cached texture changes.
    /// </summary>
    internal sealed class RokasSleepEyeAperture : IDisposable
    {
        // 72 cached states at 320x180 give smoother 30/60fps transitions
        // without generating thousands of pixels synchronously on bed click.
        // Incremental warmup during alarm selection avoids a first-use stall.
        private const int Steps = 72;
        private readonly GameObject gameObject;
        private readonly GameObject overlayCanvas;
        private readonly RawImage image;
        private readonly Texture2D[] frames = new Texture2D[Steps + 1];
        private readonly Texture2D black;
        private float lastOpen = -1f;
        private int nextWarmup = 1;
        private bool midpointReported;
        public RokasSleepEyeAperture(RectTransform root)
        {
            // A dedicated high-sorting Canvas fixes V7/V8 masks being
            // visually covered by the room/game transitional curtains.
            overlayCanvas = new GameObject("ROKAS_Eyelids_ScreenOverlay_30000",
                typeof(RectTransform), typeof(Canvas));
            overlayCanvas.transform.SetParent(root, false);
            var canvasRect = (RectTransform)overlayCanvas.transform;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;
            Canvas canvas = overlayCanvas.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000; // Above the room, curtain and dream, but not the Keiko cursor.
            gameObject = new GameObject("Keiko_Real_Eyelid_Curved_Apertures",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            gameObject.transform.SetParent(overlayCanvas.transform, false);
            RectTransform r = (RectTransform)gameObject.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            image = gameObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.maskable = false;
            image.color = Color.white;

            black = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            black.SetPixel(0, 0, Color.black);
            black.Apply(false, true);
            frames[0] = black;
            // Completely open = transparent, and completely shut = opaque.
            // Neither end state is created via a fullscreen curtain fade.
            Texture2D clear = new Texture2D(1, 1,
                TextureFormat.RGBA32, false);
            clear.SetPixel(0, 0, Color.clear);
            clear.Apply(false, true);
            frames[Steps] = clear;
            SetOpen(1f);
        }

        // Transparent inside the two ovals; opaque black outside. Lens width
        // stays almost constant as height collapses into narrow eye-slits.
        // Normalized values are calibrated to the user's 24-Oct reference.
        // Pure CPU rasterization so PlayMode tests can inspect actual pixel
        // alpha without requiring GPU readback / a graphical CI runner.
        private static Color32[] RasterizeMask(float open)
        {
            const int w = 320, h = 180;
            var pixels = new Color32[w*h];
            float openness = Mathf.Clamp01(open);
            float perimeterReveal = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((1f - openness) / .14f));

            if (openness <= .0001f)
            {
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(0, 0, 0, 255);
            }
            else
            {
                float halfWidth = Mathf.Lerp(.218f, .241f,
                    Mathf.Clamp01(openness * 3f));
                float halfHeight = .535f * Mathf.Pow(openness, 1.10f);
                float invWidthSquared = 1f / (halfWidth * halfWidth);
                float invHeightSquared = 1f / (halfHeight * halfHeight);
                float feather = .022f + .010f * (1f - openness);

                for (int y=0; y<h; ++y)
                {
                    float dy = (y+.5f)/(float)h - .5f;
                    float vertical = dy*dy*invHeightSquared;
                    for (int x=0; x<w; ++x)
                    {
                        float u=(x+.5f)/(float)w;
                        float leftDx = u-.25f;
                        float rightDx = u-.75f;
                        float leftMetric = leftDx*leftDx*invWidthSquared + vertical;
                        float rightMetric = rightDx*rightDx*invWidthSquared + vertical;
                        float lensMetric = Mathf.Sqrt(Mathf.Min(leftMetric,rightMetric));
                        float outside = Mathf.SmoothStep(1f-feather, 1f+feather,
                            lensMetric);
                        float opacity = perimeterReveal * outside;
                        pixels[y*w+x] = new Color32(0,0,0,
                            (byte)Mathf.RoundToInt(opacity*255f));
                    }
                }
            }

            return pixels;
        }

        private static Texture2D RenderMask(float open)
        {
            const int w = 320, h = 180;
            Color32[] pixels = RasterizeMask(open);
            var mask = new Texture2D(w,h,TextureFormat.RGBA32,false);
            mask.name="ROKAS_DualOvalVision_"+Mathf.RoundToInt(Mathf.Clamp01(open)*1000f);
            mask.wrapMode=TextureWrapMode.Clamp;
            mask.filterMode=FilterMode.Bilinear;
            mask.SetPixels32(pixels);
            mask.Apply(false,true);
            return mask;
        }

        // Precompute only ONE frame per Update while the watch is being
        // configured. This eliminates expensive synchronous image-generation
        // spikes on the first transition from the Hub.
        public void WarmupOneFrame()
        {
            if (nextWarmup >= Steps) return;
            if (frames[nextWarmup] == null)
                frames[nextWarmup] =
                    RenderMask(nextWarmup / (float)Steps);
            nextWarmup++;
        }

        // Make the two half-oval lids render OVER the ceiling while closing
        // and reopening; don't let the authored dream video occlude them.
        public void BringToFront()
        {
            if (overlayCanvas != null) overlayCanvas.transform.SetAsLastSibling();
        }

        public void SetVisible(bool visible)
        {
            if (image != null) image.enabled = visible;
        }

        public bool IsVisible
        {
            get {
                return overlayCanvas != null && overlayCanvas.activeInHierarchy &&
                    image != null && image.isActiveAndEnabled &&
                    overlayCanvas.GetComponent<Canvas>() != null &&
                    overlayCanvas.GetComponent<Canvas>().sortingOrder >= 30000;
            }
        }

        public void SetOpen(float openness)
        {
            float v = Mathf.Clamp01(openness);
            if (Mathf.Abs(lastOpen-v) < .008f) return;
            lastOpen = v;
            int idx = Mathf.Clamp(Mathf.RoundToInt(v*Steps),0,Steps);
            if (frames[idx] == null)
                frames[idx] = RenderMask(idx / (float)Steps);
            if (image != null) image.texture = frames[idx];
            if (!midpointReported && v > .35f && v < .65f)
            {
                midpointReported = true;
                Debug.Log("[ROKAS BINOCULAR V10] Mid-transition: visible=" +
                    IsVisible + " eyeOpen=" + v.ToString("0.00") +
                    " frame=" + idx + "/" + Steps +
                    " screen=" + Screen.width + "x" + Screen.height);
            }
        }
        public void Dispose()
        {
            foreach (Texture2D t in frames)
                if (t != null) UnityEngine.Object.Destroy(t);
            if (overlayCanvas != null) UnityEngine.Object.Destroy(overlayCanvas);
        }
    }
}
