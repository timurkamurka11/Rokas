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

    internal sealed class VnSceneComposerVideoRequestState
    {
        private int generation;
        private string sourceIdentity = string.Empty;

        public int Generation { get { return generation; } }
        public string SourceIdentity { get { return sourceIdentity; } }
        public bool Preparing { get; private set; }
        public bool Prepared { get; private set; }
        public bool Failed { get; private set; }

        public int Begin(string requestedSourceIdentity)
        {
            generation++;
            sourceIdentity = requestedSourceIdentity ?? string.Empty;
            Preparing = true;
            Prepared = false;
            Failed = false;
            return generation;
        }

        public void Invalidate()
        {
            generation++;
            sourceIdentity = string.Empty;
            Preparing = false;
            Prepared = false;
            Failed = false;
        }

        public bool AcceptPrepared(int candidateGeneration, string candidateSourceIdentity)
        {
            if (!IsCurrent(candidateGeneration, candidateSourceIdentity) || Failed) return false;
            Preparing = false;
            Prepared = true;
            return true;
        }

        public bool AcceptFrame(int candidateGeneration, string candidateSourceIdentity)
        {
            if (!IsCurrent(candidateGeneration, candidateSourceIdentity) || Failed) return false;
            Preparing = false;
            Prepared = true;
            return true;
        }

        public bool AcceptError(int candidateGeneration, string candidateSourceIdentity)
        {
            if (!IsCurrent(candidateGeneration, candidateSourceIdentity)) return false;
            Preparing = false;
            Prepared = false;
            Failed = true;
            return true;
        }

        private bool IsCurrent(int candidateGeneration, string candidateSourceIdentity)
        {
            return candidateGeneration == generation &&
                   string.Equals(sourceIdentity, candidateSourceIdentity ?? string.Empty,
                       StringComparison.Ordinal);
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
        private readonly int requestedWidth;
        private readonly int requestedHeight;
        private readonly string desiredUrl;
        private readonly VnSceneComposerVideoRequestState requestState =
            new VnSceneComposerVideoRequestState();
        private int requestGeneration;
        private string sourceIdentity = string.Empty;
        private VideoPlayer.EventHandler prepareCompletedHandler;
        private VideoPlayer.FrameReadyEventHandler frameReadyHandler;
        private VideoPlayer.ErrorEventHandler errorReceivedHandler;

        protected internal VnSceneComposerVideoPreview(string path, int width, int height, bool shouldLoop)
        {
            loop = shouldLoop;
            warning = string.Empty;
            requestedWidth = Mathf.Max(16, width);
            requestedHeight = Mathf.Max(16, height);
            desiredUrl = string.IsNullOrEmpty(path) ? string.Empty : ToFileUrl(path);
            sourceIdentity = BuildSourceIdentity(desiredUrl, shouldLoop);

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                warning = "External preview video is missing: " + (path ?? string.Empty);
                return;
            }

            texture = CreateRenderTexture(requestedWidth, requestedHeight);

            host = new GameObject("ROKAS_VnSceneComposerVideoPreview", typeof(VideoPlayer))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            player = host.GetComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.aspectRatio = VideoAspectRatio.Stretch;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.source = VideoSource.Url;
            player.url = desiredUrl;
            player.isLooping = loop;
            player.targetTexture = texture;
            player.sendFrameReadyEvents = true;
            VnSceneComposerMotionPreviewRegistry.Register(this);
        }

        public virtual bool IsPrepared
        {
            get { return player != null && player.isPrepared && requestState.Prepared; }
        }

        public virtual bool IsPreparing
        {
            get
            {
                return player != null && prepareRequested && requestState.Preparing &&
                       !player.isPrepared;
            }
        }

        public virtual bool IsPlaying { get { return player != null && player.isPlaying; } }
        public virtual bool HasVisibleFrame { get { return hasVisibleFrame; } }
        public virtual bool HasFailed
        {
            get { return requestState.Failed || !string.IsNullOrEmpty(warning); }
        }

        public virtual bool IsRequestReusable
        {
            get
            {
                return texture != null && texture.IsCreated() &&
                       string.IsNullOrEmpty(warning);
            }
        }

        public virtual bool IsReadyForCurrentRequest
        {
            get
            {
                return IsRequestReusable && HasVisibleFrame &&
                       (IsPrepared || IsPlaying);
            }
        }

        public virtual void Prepare()
        {
            if (player == null) return;
            playRequested = false;
            if (!EnsureRenderTargetBound()) return;

            if (player.isPrepared)
            {
                BeginRequest(false, false);
                prepareRequested = false;
                requestState.AcceptPrepared(requestGeneration, sourceIdentity);
                if (!hasVisibleFrame) RequestFirstFrame();
                return;
            }

            if (prepareRequested) return;
            if (!BeginRequest(false, false)) return;
            prepareRequested = true;
            player.Prepare();
        }

        public virtual void Play()
        {
            if (player == null) return;

            // If authoring already prewarmed this exact source, Play adopts the
            // in-flight request. The existing generation/source token remains
            // authoritative and Prepare is not issued a second time.
            if (prepareRequested && !player.isPrepared && requestState.Preparing)
            {
                playRequested = true;
                previewFrameRequested = false;
                return;
            }

            if (!BeginRequest(true, false)) return;

            previewFrameRequested = false;
            if (player.isPrepared)
            {
                prepareRequested = false;
                requestState.AcceptPrepared(requestGeneration, sourceIdentity);
                player.Play();
            }
            else
            {
                prepareRequested = true;
                player.Prepare();
            }
        }

        public virtual void ResumePresentation()
        {
            if (player == null || !player.isPrepared) return;
            playRequested = true;
            player.Play();
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
            requestState.Invalidate();
            requestGeneration = requestState.Generation;
            UnbindRequestCallbacks();
            if (player != null) player.Stop();
        }

        public virtual void AbortCurrentRequest(string message)
        {
            warning = string.IsNullOrWhiteSpace(message)
                ? "External preview video request was cancelled."
                : message;
            Stop();
            Changed?.Invoke();
        }

        public virtual void Dispose()
        {
            VnSceneComposerMotionPreviewRegistry.Unregister(this);
            playRequested = false;
            prepareRequested = false;
            previewFrameRequested = false;
            hasVisibleFrame = false;
            Changed = null;
            requestState.Invalidate();
            requestGeneration = requestState.Generation;
            UnbindRequestCallbacks();

            if (player != null)
            {
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

        private bool BeginRequest(bool requestedPlay, bool supersedePreparing)
        {
            if (player == null) return false;

            if (supersedePreparing)
            {
                requestState.Invalidate();
                requestGeneration = requestState.Generation;
                UnbindRequestCallbacks();
                player.Stop();
                prepareRequested = false;
                previewFrameRequested = false;
            }

            if (!EnsureRenderTargetBound()) return false;

            warning = string.Empty;
            playRequested = requestedPlay;
            requestGeneration = requestState.Begin(sourceIdentity);
            BindRequestCallbacks(requestGeneration, sourceIdentity);
            return true;
        }

        private bool EnsureRenderTargetBound()
        {
            if (player == null) return false;

            if (texture == null)
                texture = CreateRenderTexture(requestedWidth, requestedHeight);
            else if (!texture.IsCreated())
                texture.Create();

            bool sourceChanged =
                player.source != VideoSource.Url ||
                !string.Equals(player.url ?? string.Empty, desiredUrl ?? string.Empty,
                    StringComparison.Ordinal);

            if (sourceChanged)
            {
                player.Stop();
                hasVisibleFrame = false;
                prepareRequested = false;
                previewFrameRequested = false;
                requestState.Invalidate();
                requestGeneration = requestState.Generation;
                UnbindRequestCallbacks();
                player.source = VideoSource.Url;
                player.url = desiredUrl;
            }

            player.isLooping = loop;
            player.sendFrameReadyEvents = true;
            if (player.targetTexture != texture)
                player.targetTexture = texture;

            return texture != null && texture.IsCreated() &&
                   player.targetTexture == texture;
        }

        private static RenderTexture CreateRenderTexture(int width, int height)
        {
            var created = new RenderTexture(
                Mathf.Max(16, width), Mathf.Max(16, height), 0, RenderTextureFormat.ARGB32)
            {
                name = "ROKAS_VnSceneComposerVideoPreview",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            created.Create();
            return created;
        }

        private static string BuildSourceIdentity(string url, bool shouldLoop)
        {
            return (url ?? string.Empty) + "|" + shouldLoop;
        }

        private void BindRequestCallbacks(int generation, string identity)
        {
            UnbindRequestCallbacks();
            if (player == null) return;

            prepareCompletedHandler = prepared =>
                OnPreparedForRequest(prepared, generation, identity);
            frameReadyHandler = (source, frameIndex) =>
                OnFrameReadyForRequest(source, frameIndex, generation, identity);
            errorReceivedHandler = (source, message) =>
                OnErrorForRequest(source, message, generation, identity);

            player.prepareCompleted += prepareCompletedHandler;
            player.frameReady += frameReadyHandler;
            player.errorReceived += errorReceivedHandler;
        }

        private void UnbindRequestCallbacks()
        {
            if (player != null)
            {
                if (prepareCompletedHandler != null)
                    player.prepareCompleted -= prepareCompletedHandler;
                if (frameReadyHandler != null)
                    player.frameReady -= frameReadyHandler;
                if (errorReceivedHandler != null)
                    player.errorReceived -= errorReceivedHandler;
            }

            prepareCompletedHandler = null;
            frameReadyHandler = null;
            errorReceivedHandler = null;
        }

        private void OnPreparedForRequest(
            VideoPlayer prepared, int generation, string identity)
        {
            if (prepared != player ||
                !requestState.AcceptPrepared(generation, identity))
                return;
            OnPrepared(prepared);
        }

        private void OnFrameReadyForRequest(
            VideoPlayer source, long frameIndex, int generation, string identity)
        {
            if (source != player ||
                !requestState.AcceptFrame(generation, identity))
                return;
            OnFrameReady(source, frameIndex);
        }

        private void OnErrorForRequest(
            VideoPlayer source, string message, int generation, string identity)
        {
            if (source != player ||
                !requestState.AcceptError(generation, identity))
                return;
            OnError(source, message);
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
            if (prepared == null || prepared != player) return;
            if (!EnsureRenderTargetBound()) return;

            int sourceWidth = (int)prepared.width;
            int sourceHeight = (int)prepared.height;
            if (sourceWidth <= 0 || sourceHeight <= 0) return;

            Vector2Int desired = ResolvePreparedRenderSize(sourceWidth, sourceHeight);
            if (texture.width == desired.x && texture.height == desired.y) return;

            prepared.targetTexture = null;
            texture.Release();
            texture.width = desired.x;
            texture.height = desired.y;
            texture.filterMode = FilterMode.Bilinear;
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
