using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rokas.Presentation
{
    /// <summary>
    /// Owns the single transient video playback path used by application startup and laptop boot.
    /// Source media stays in StreamingAssets and is played by URL so the original MP4 bytes ship unchanged.
    /// </summary>
    public sealed class VideoSequencePresenter : MonoBehaviour
    {
        private const float PrepareTimeoutSeconds = 8f;
        private const float PlaybackTimeoutSeconds = 30f;
        private const float MissingMediaFallbackDelay = .05f;

        private VideoPlayer player;
        private AudioSource videoAudio;
        private RawImage image;
        private CanvasGroup startupHintGroup;
        private RectTransform surface;
        private GameObject ownedRoot;
        private RenderTexture target;
        private Action completed;
        private bool active;
        private bool firstFramePresented;
        private bool allowSkip;
        private bool fallbackPending;
        private float volume;
        private float prepareDeadline;
        private float playbackDeadline;

        public bool IsPlaying { get { return active; } }
        public bool FirstFramePresented { get { return firstFramePresented; } }
        public RenderTexture TemporaryRenderTexture { get { return target; } }

        private void Awake()
        {
            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (!player)
            {
                player = gameObject.GetComponent<VideoPlayer>();
                if (!player) player = gameObject.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.isLooping = false;
                player.waitForFirstFrame = true;
                player.skipOnDrop = true;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.audioOutputMode = VideoAudioOutputMode.AudioSource;
                player.sendFrameReadyEvents = true;
            }

            if (!videoAudio)
            {
                // RokasAudio creates its own sources after the startup preview. This source belongs only to video.
                videoAudio = gameObject.AddComponent<AudioSource>();
                videoAudio.playOnAwake = false;
                videoAudio.loop = false;
                videoAudio.spatialBlend = 0f;
                videoAudio.volume = 0f;
            }
        }

        public void PlayStartup(string fileName, float requestedVolume, Action onComplete)
        {
            Cancel();
            RectTransform host = CreateStartupHost();
            Begin(host, fileName, "StartupVideoSurface", 1280, 720, requestedVolume, true, onComplete);
        }

        public void PlayInHost(RectTransform host, string fileName, string surfaceName,
            int targetWidth, int targetHeight, float requestedVolume, Action onComplete)
        {
            if (!host) throw new ArgumentNullException("host");
            Cancel();
            Begin(host, fileName, surfaceName, targetWidth, targetHeight, requestedVolume, false, onComplete);
        }

        public void Skip()
        {
            if (!active || !allowSkip || !firstFramePresented) return;
            Finish(true);
        }

        /// <summary>
        /// Cancels playback without invoking the completion callback. Laptop close uses this path so cancellation
        /// returns to Home instead of constructing the desktop behind the closing panel.
        /// </summary>
        public void Cancel()
        {
            if (!active && !surface && !ownedRoot && !target) return;
            Finish(false);
        }

        private void Begin(RectTransform host, string fileName, string surfaceName,
            int targetWidth, int targetHeight, float requestedVolume, bool canSkip, Action onComplete)
        {
            EnsureComponents();
            active = true;
            firstFramePresented = false;
            allowSkip = canSkip;
            fallbackPending = false;
            completed = onComplete;
            volume = Mathf.Clamp01(requestedVolume);
            prepareDeadline = Time.realtimeSinceStartup + PrepareTimeoutSeconds;
            playbackDeadline = float.PositiveInfinity;

            targetWidth = Mathf.Max(16, targetWidth);
            targetHeight = Mathf.Max(16, targetHeight);
            target = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "RokasVideoSequenceRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();
            ClearTargetToBlack(target);

            var surfaceObject = new GameObject(surfaceName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            surface = (RectTransform)surfaceObject.transform;
            surface.SetParent(host, false);
            surface.anchorMin = Vector2.zero;
            surface.anchorMax = Vector2.one;
            surface.offsetMin = Vector2.zero;
            surface.offsetMax = Vector2.zero;
            surface.SetAsLastSibling();
            image = surfaceObject.GetComponent<RawImage>();
            image.texture = target;
            image.color = Color.black;
            image.raycastTarget = false;
            if (canSkip) CreateStartupHint(host);

            player.Stop();
            videoAudio.Stop();
            player.targetTexture = target;
            player.source = VideoSource.Url;
            player.url = ResolveStreamingAssetsUrl(fileName);
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, videoAudio);
            videoAudio.volume = Application.isFocused ? volume : 0f;

            AttachHandlers();
            string localPath = ResolveStreamingAssetsPath(fileName);
            if (!File.Exists(localPath))
            {
                Debug.LogWarning("ROKAS video media missing; continuing through safe fallback: " + localPath);
                fallbackPending = true;
                prepareDeadline = Time.realtimeSinceStartup + MissingMediaFallbackDelay;
                return;
            }

            player.Prepare();
        }

        private void CreateStartupHint(RectTransform host)
        {
            Texture2D hintTexture = Resources.Load<Texture2D>("StartupSkipHint");
            if (!hintTexture)
            {
                Debug.LogWarning("ROKAS approved StartupSkipHint texture is missing; startup video remains usable without the hint.");
                return;
            }

            var hintObject = new GameObject("StartupSkipHintOverlay", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage), typeof(CanvasGroup));
            var hint = (RectTransform)hintObject.transform;
            hint.SetParent(host, false);
            hint.anchorMin = hint.anchorMax = new Vector2(1f, 0f);
            hint.pivot = new Vector2(1f, 0f);
            const float hintWidth = 520f;
            hint.sizeDelta = new Vector2(hintWidth, hintWidth * hintTexture.height / hintTexture.width);
            hint.anchoredPosition = new Vector2(-48f, 40f);

            var hintImage = hintObject.GetComponent<RawImage>();
            hintImage.texture = hintTexture;
            hintImage.color = new Color(1f, 1f, 1f, 0f);
            hintImage.raycastTarget = false;
            startupHintGroup = hintObject.GetComponent<CanvasGroup>();
            startupHintGroup.alpha = 0f;
            startupHintGroup.interactable = false;
            startupHintGroup.blocksRaycasts = false;
            hint.SetAsLastSibling();
        }

        private RectTransform CreateStartupHost()
        {
            ownedRoot = new GameObject("StartupVideoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            ownedRoot.transform.SetParent(transform, false);
            var canvas = ownedRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = ownedRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            var host = (RectTransform)ownedRoot.transform;
            host.anchorMin = Vector2.zero;
            host.anchorMax = Vector2.one;
            host.offsetMin = Vector2.zero;
            host.offsetMax = Vector2.zero;
            return host;
        }

        private void AttachHandlers()
        {
            player.prepareCompleted += OnPrepared;
            player.frameReady += OnFrameReady;
            player.loopPointReached += OnLoopPointReached;
            player.errorReceived += OnError;
        }

        private void DetachHandlers()
        {
            if (!player) return;
            player.prepareCompleted -= OnPrepared;
            player.frameReady -= OnFrameReady;
            player.loopPointReached -= OnLoopPointReached;
            player.errorReceived -= OnError;
        }

        private void OnPrepared(VideoPlayer preparedPlayer)
        {
            if (!active || preparedPlayer != player) return;
            fallbackPending = false;
            playbackDeadline = Time.realtimeSinceStartup + PlaybackTimeoutSeconds;
            player.Play();
        }

        private void OnFrameReady(VideoPlayer source, long frameIndex)
        {
            if (!active || source != player) return;
            if (firstFramePresented) return;
            firstFramePresented = true;
            if (image) image.color = Color.white;
            if (startupHintGroup)
            {
                var hintImage = startupHintGroup.GetComponent<RawImage>();
                if (hintImage) hintImage.color = Color.white;
                startupHintGroup.alpha = 1f;
            }
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            if (!active || source != player) return;
            Finish(true);
        }

        private void OnError(VideoPlayer source, string message)
        {
            if (!active || source != player) return;
            Debug.LogWarning("ROKAS video playback failed; continuing through safe fallback: " + message);
            Finish(true);
        }

        private void Update()
        {
            if (!active) return;
            if (videoAudio) videoAudio.volume = Application.isFocused ? volume : 0f;

            float now = Time.realtimeSinceStartup;
            if (!firstFramePresented && now >= prepareDeadline)
            {
                if (!fallbackPending)
                    Debug.LogWarning("ROKAS video prepare/first-frame timeout; continuing through safe fallback.");
                Finish(true);
                return;
            }
            if (firstFramePresented && now >= playbackDeadline)
            {
                Debug.LogWarning("ROKAS video playback timeout; continuing through safe fallback.");
                Finish(true);
                return;
            }

            if (allowSkip && firstFramePresented && player && player.isPlaying && Input.anyKeyDown)
                Finish(true);
        }

        private void Finish(bool invokeCompletion)
        {
            if (!active && !surface && !ownedRoot && !target) return;

            // Clear the guard before callbacks so every terminal path is idempotent, including callback re-entry.
            active = false;
            allowSkip = false;
            fallbackPending = false;
            firstFramePresented = false;
            DetachHandlers();

            if (player)
            {
                player.Stop();
                player.targetTexture = null;
                player.url = string.Empty;
            }
            if (videoAudio)
            {
                videoAudio.Stop();
                videoAudio.clip = null;
                videoAudio.volume = 0f;
            }

            if (image) image.texture = null;
            startupHintGroup = null;
            if (ownedRoot)
            {
                Destroy(ownedRoot);
                ownedRoot = null;
                surface = null;
            }
            else if (surface)
            {
                Destroy(surface.gameObject);
                surface = null;
            }
            image = null;

            if (target)
            {
                target.Release();
                Destroy(target);
                target = null;
            }

            Action callback = completed;
            completed = null;
            if (invokeCompletion) callback?.Invoke();
        }

        private static void ClearTargetToBlack(RenderTexture renderTexture)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        private static string ResolveStreamingAssetsPath(string fileName)
        {
            return Path.Combine(Application.streamingAssetsPath, "RokasVideo", fileName);
        }

        private static string ResolveStreamingAssetsUrl(string fileName)
        {
            string path = ResolveStreamingAssetsPath(fileName);
            try { return new Uri(path).AbsoluteUri; }
            catch (UriFormatException) { return path; }
        }

        private void OnDestroy()
        {
            Cancel();
        }
    }
}
