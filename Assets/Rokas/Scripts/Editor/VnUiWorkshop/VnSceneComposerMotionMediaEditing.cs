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

    public sealed class VnSceneComposerVideoPreview : IDisposable
    {
        public RenderTexture texture;
        public string warning;
        public bool loop;

        private GameObject host;
        private VideoPlayer player;
        private bool playRequested;

        internal VnSceneComposerVideoPreview(string path, int width, int height, bool shouldLoop)
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
                filterMode = FilterMode.Bilinear,
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
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.source = VideoSource.Url;
            player.url = ToFileUrl(path);
            player.isLooping = loop;
            player.targetTexture = texture;
            player.prepareCompleted += OnPrepared;
            player.errorReceived += OnError;
            VnSceneComposerMotionPreviewRegistry.Register(this);
        }

        public bool IsPrepared { get { return player != null && player.isPrepared; } }
        public bool IsPlaying { get { return player != null && player.isPlaying; } }

        public void Play()
        {
            if (player == null) return;
            playRequested = true;
            if (player.isPrepared) player.Play();
            else player.Prepare();
        }

        public void Pause()
        {
            playRequested = false;
            if (player != null && player.isPlaying) player.Pause();
        }

        public void Restart()
        {
            if (player == null) return;
            if (player.isPrepared)
            {
                player.frame = 0;
                if (playRequested) player.Play();
            }
        }

        public void Stop()
        {
            playRequested = false;
            if (player != null) player.Stop();
        }

        public void Dispose()
        {
            VnSceneComposerMotionPreviewRegistry.Unregister(this);
            playRequested = false;
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
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
            if (prepared == player && playRequested) player.Play();
        }

        private void OnError(VideoPlayer source, string message)
        {
            if (source != player) return;
            warning = "External preview video failed: " + message;
            playRequested = false;
            source.Stop();
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
            return new VnSceneComposerVideoPreview(media.reference, width, height, media.loop);
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
