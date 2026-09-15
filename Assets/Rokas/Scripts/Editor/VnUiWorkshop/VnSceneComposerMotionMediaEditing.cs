using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Rokas.EditorTools.VnUiWorkshop
{
    [InitializeOnLoad]
    internal static class VnSceneComposerMotionPreviewRegistry
    {
        private static readonly HashSet<IDisposable> Active = new HashSet<IDisposable>();

        static VnSceneComposerMotionPreviewRegistry()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
        }

        internal static void Register(IDisposable preview)
        {
            if (preview != null) Active.Add(preview);
        }

        internal static void Unregister(IDisposable preview)
        {
            if (preview != null) Active.Remove(preview);
        }

        private static void DisposeAll()
        {
            IDisposable[] previews = new IDisposable[Active.Count];
            Active.CopyTo(previews);
            Active.Clear();
            for (int i = 0; i < previews.Length; i++)
            {
                try { previews[i]?.Dispose(); }
                catch (Exception exception)
                {
                    Debug.LogWarning("Scene Composer preview cleanup failed: " + exception.Message);
                }
            }
        }
    }

    public interface IVnSceneComposerVideoPreviewFactory
    {
        VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height);
    }

    internal sealed class VnSceneComposerDefaultVideoPreviewFactory : IVnSceneComposerVideoPreviewFactory
    {
        public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
        {
            return new VnSceneComposerVideoPreview(media.reference, width, height, media.loop);
        }
    }

    public class VnSceneComposerVideoPreview : IDisposable
    {
        private const int MaxVideoPreviewDimension = 1920;

        public RenderTexture texture;
        public string warning;
        public bool loop;
        public event Action Changed;

        private GameObject host;
        private VideoPlayer player;
        private bool playRequested;
        private bool prepareRequested;
        private bool previewFrameRequested;
        private bool hasVisibleFrame;

        protected internal VnSceneComposerVideoPreview(string path, int width, int height, bool shouldLoop)
        {
            loop = shouldLoop;
            warning = string.Empty;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                warning = "External preview video is missing: " + (path ?? string.Empty);
                return;
            }

            width = Mathf.Max(16, width);
            height = Mathf.Max(16, height);
            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "ROKAS_VnSceneComposerVideoPreview",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();

            host = new GameObject("ROKAS_VnSceneComposerVideoPreview", typeof(VideoPlayer))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            player = host.GetComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.aspectRatio = VideoAspectRatio.FitInside;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.source = VideoSource.Url;
            player.url = ToFileUrl(path);
            player.isLooping = loop;
            player.targetTexture = texture;
            player.sendFrameReadyEvents = true;
            player.prepareCompleted += OnPrepared;
            player.frameReady += OnFrameReady;
            player.errorReceived += OnError;
            VnSceneComposerMotionPreviewRegistry.Register(this);
        }

        public virtual bool IsPrepared { get { return player != null && player.isPrepared; } }
        public virtual bool IsPreparing { get { return player != null && prepareRequested && !player.isPrepared; } }
        public virtual bool IsPlaying { get { return player != null && player.isPlaying; } }
        public virtual bool HasVisibleFrame { get { return hasVisibleFrame; } }

        public virtual void Prepare()
        {
            if (player == null) return;
            playRequested = false;
            if (player.isPrepared)
            {
                prepareRequested = false;
                if (!hasVisibleFrame) RequestFirstFrame();
                return;
            }
            if (prepareRequested) return;
            prepareRequested = true;
            player.Prepare();
        }

        public virtual void Play()
        {
            if (player == null) return;
            playRequested = true;
            previewFrameRequested = false;
            if (player.isPrepared)
            {
                prepareRequested = false;
                player.Play();
            }
            else if (!prepareRequested)
            {
                prepareRequested = true;
                player.Prepare();
            }
        }

        public virtual void Pause()
        {
            playRequested = false;
            previewFrameRequested = false;
            if (player != null && player.isPlaying) player.Pause();
        }

        public virtual void Restart()
        {
            if (player == null) return;
            if (!player.isPrepared)
            {
                // Restart is a transport action, not a decode/probe action. The real authoring
                // preview owns explicit Prepare(), while deterministic routing tests can safely
                // exercise Restart against an unprepared/dummy media binding.
                return;
            }

            player.frame = 0;
            if (playRequested) player.Play();
            else RequestFirstFrame();
        }

        public virtual void Stop()
        {
            playRequested = false;
            prepareRequested = false;
            previewFrameRequested = false;
            hasVisibleFrame = false;
            if (player != null) player.Stop();
        }

        public virtual void Dispose()
        {
            VnSceneComposerMotionPreviewRegistry.Unregister(this);
            playRequested = false;
            prepareRequested = false;
            previewFrameRequested = false;
            hasVisibleFrame = false;
            Changed = null;
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
                player.frameReady -= OnFrameReady;
                player.errorReceived -= OnError;
                player.Stop();
                player.targetTexture = null;
                player.url = string.Empty;
                player = null;
            }

            if (host != null)
            {
                UnityEngine.Object.DestroyImmediate(host);
                host = null;
            }

            if (texture != null)
            {
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                texture = null;
            }
        }

        private void OnPrepared(VideoPlayer prepared)
        {
            if (prepared != player) return;
            prepareRequested = false;
            EnsureRenderTargetMatchesPreparedSource(prepared);
            if (playRequested) player.Play();
            else RequestFirstFrame();
            Changed?.Invoke();
        }

        private void EnsureRenderTargetMatchesPreparedSource(VideoPlayer prepared)
        {
            if (prepared == null || prepared != player || texture == null) return;
            int sourceWidth = (int)prepared.width;
            int sourceHeight = (int)prepared.height;
            if (sourceWidth <= 0 || sourceHeight <= 0) return;

            Vector2Int desired = ResolvePreparedRenderSize(sourceWidth, sourceHeight);
            if (texture.width == desired.x && texture.height == desired.y) return;

            prepared.targetTexture = null;
            texture.Release();
            texture.width = desired.x;
            texture.height = desired.y;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Create();
            prepared.targetTexture = texture;
        }

        private static Vector2Int ResolvePreparedRenderSize(int sourceWidth, int sourceHeight)
        {
            sourceWidth = Mathf.Max(16, sourceWidth);
            sourceHeight = Mathf.Max(16, sourceHeight);
            int longest = Mathf.Max(sourceWidth, sourceHeight);
            if (longest <= MaxVideoPreviewDimension)
                return new Vector2Int(sourceWidth, sourceHeight);

            float scale = MaxVideoPreviewDimension / (float)longest;
            return new Vector2Int(
                Mathf.Max(16, Mathf.RoundToInt(sourceWidth * scale)),
                Mathf.Max(16, Mathf.RoundToInt(sourceHeight * scale)));
        }

        private void RequestFirstFrame()
        {
            if (player == null || !player.isPrepared) return;
            previewFrameRequested = true;
            player.frame = 0;
            player.Play();
        }

        private void OnFrameReady(VideoPlayer source, long frameIndex)
        {
            if (source != player) return;
            hasVisibleFrame = true;
            if (previewFrameRequested && !playRequested && source.isPlaying) source.Pause();
            previewFrameRequested = false;
            EditorApplication.QueuePlayerLoopUpdate();
            Changed?.Invoke();
        }

        private void OnError(VideoPlayer source, string message)
        {
            if (source != player) return;
            warning = "External preview video failed: " + message;
            playRequested = false;
            prepareRequested = false;
            previewFrameRequested = false;
            hasVisibleFrame = false;
            source.Stop();
            Changed?.Invoke();
        }

        private static string ToFileUrl(string path)
        {
            try { return new Uri(Path.GetFullPath(path)).AbsoluteUri; }
            catch (UriFormatException) { return Path.GetFullPath(path); }
        }
    }

    public sealed class VnSceneComposerGifPreview : IDisposable
    {
        public string warning;
        public int frameCount;
        public float totalDuration;
        public Texture2D currentTexture;

        private readonly List<Texture2D> frames = new List<Texture2D>();
        private readonly List<float> delays = new List<float>();
        private readonly bool loop;
        private float elapsed;

        internal VnSceneComposerGifPreview(string path, bool shouldLoop)
        {
            loop = shouldLoop;
            warning = string.Empty;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                warning = "External preview GIF is missing: " + (path ?? string.Empty);
                return;
            }

            try
            {
                VnSceneComposerGifDecoder.Decode(File.ReadAllBytes(path), frames, delays);
                frameCount = frames.Count;
                for (int i = 0; i < delays.Count; i++) totalDuration += delays[i];
                if (frameCount == 0)
                {
                    warning = "External preview GIF contains no decodable frames: " + path;
                    return;
                }
                currentTexture = frames[0];
                VnSceneComposerMotionPreviewRegistry.Register(this);
            }
            catch (Exception exception)
            {
                warning = "External preview GIF failed: " + exception.Message;
                Dispose();
            }
        }

        public Texture2D GetFrameAtTime(float seconds)
        {
            if (frames.Count == 0) return null;
            if (frames.Count == 1 || totalDuration <= 0f) return frames[0];

            float time = Mathf.Max(0f, seconds);
            if (loop) time = Mathf.Repeat(time, totalDuration);
            else time = Mathf.Min(time, Mathf.Max(0f, totalDuration - .0001f));

            float cursor = 0f;
            for (int i = 0; i < frames.Count; i++)
            {
                cursor += delays[i];
                if (time < cursor) return frames[i];
            }
            return frames[frames.Count - 1];
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds),
                    "GIF preview delta must be finite and non-negative.");

            elapsed += deltaSeconds;
            if (!loop && totalDuration > 0f) elapsed = Mathf.Min(elapsed, totalDuration);
            currentTexture = GetFrameAtTime(elapsed);
        }

        public void Restart()
        {
            elapsed = 0f;
            currentTexture = frames.Count > 0 ? frames[0] : null;
        }

        public void Dispose()
        {
            VnSceneComposerMotionPreviewRegistry.Unregister(this);
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] != null) UnityEngine.Object.DestroyImmediate(frames[i]);
            }
            frames.Clear();
            delays.Clear();
            frameCount = 0;
            totalDuration = 0f;
            elapsed = 0f;
            currentTexture = null;
        }
    }

    public static partial class VnSceneComposerMediaEditing
    {
        private static readonly IVnSceneComposerVideoPreviewFactory DefaultVideoPreviewFactory =
            new VnSceneComposerDefaultVideoPreviewFactory();
        private static IVnSceneComposerVideoPreviewFactory videoPreviewFactory = DefaultVideoPreviewFactory;

        public static IVnSceneComposerVideoPreviewFactory VideoPreviewFactory
        {
            get { return videoPreviewFactory ?? DefaultVideoPreviewFactory; }
            set { videoPreviewFactory = value ?? DefaultVideoPreviewFactory; }
        }

        public static void ResetVideoPreviewFactory()
        {
            videoPreviewFactory = DefaultVideoPreviewFactory;
        }

        public static void SetExternalVideo(VnSceneComposerScene scene, string path,
            VnSceneComposerMediaScaleMode scaleMode, bool loop)
        {
            SetExternalMotionMedia(scene, path, scaleMode, loop, VnSceneComposerMediaKind.ExternalVideo,
                ".mp4", ".mov", ".m4v", ".webm");
        }

        public static void SetExternalGif(VnSceneComposerScene scene, string path,
            VnSceneComposerMediaScaleMode scaleMode, bool loop)
        {
            SetExternalMotionMedia(scene, path, scaleMode, loop, VnSceneComposerMediaKind.ExternalGif, ".gif");
        }

        public static VnSceneComposerVideoPreview OpenVideoPreview(VnSceneComposerMediaReference media,
            int width, int height)
        {
            if (media == null)
                return new VnSceneComposerVideoPreview(null, width, height, false)
                {
                    warning = "Video media reference is missing."
                };
            if (media.kind != VnSceneComposerMediaKind.ExternalVideo)
                return new VnSceneComposerVideoPreview(null, width, height, media.loop)
                {
                    warning = "Selected media is not a video."
                };
            return VideoPreviewFactory.Open(media, width, height);
        }

        public static VnSceneComposerGifPreview OpenGifPreview(VnSceneComposerMediaReference media)
        {
            if (media == null)
                return new VnSceneComposerGifPreview(null, false)
                {
                    warning = "GIF media reference is missing."
                };
            if (media.kind != VnSceneComposerMediaKind.ExternalGif)
                return new VnSceneComposerGifPreview(null, media.loop)
                {
                    warning = "Selected media is not a GIF."
                };
            return new VnSceneComposerGifPreview(media.reference, media.loop);
        }

        private static void SetExternalMotionMedia(VnSceneComposerScene scene, string path,
            VnSceneComposerMediaScaleMode scaleMode, bool loop, VnSceneComposerMediaKind kind,
            params string[] allowedExtensions)
        {
            RequireScene(scene);
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Media path is required.", nameof(path));

            string fullPath = Path.GetFullPath(path);
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            bool allowed = false;
            for (int i = 0; i < allowedExtensions.Length; i++)
            {
                if (extension == allowedExtensions[i])
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed)
                throw new ArgumentException("Unsupported Scene Composer preview media extension: " + extension,
                    nameof(path));
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("External preview media is missing.", fullPath);

            scene.media = new VnSceneComposerMediaReference
            {
                kind = kind,
                reference = fullPath,
                displayName = Path.GetFileName(fullPath),
                contentHash = ComputeSha256(fullPath),
                localPreviewDependency = true,
                scaleMode = scaleMode,
                loop = loop
            };
        }
    }
}
