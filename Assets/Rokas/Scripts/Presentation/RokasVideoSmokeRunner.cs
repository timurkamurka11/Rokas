using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rokas.Presentation
{
    /// <summary>
    /// Development-player evidence seam. It is dormant unless the exact CI command-line switch is present,
    /// and exercises the same VideoSequencePresenter used by normal startup and laptop boot playback.
    /// </summary>
    public sealed class RokasVideoSmokeRunner : MonoBehaviour
    {
        private const string SmokeArgument = "-rokasVideoSmoke";
        private const float MediaTimeoutSeconds = 25f;

        [Serializable]
        private sealed class MediaEvidence
        {
            public string fileName;
            public bool fileFound;
            public bool prepared;
            public bool firstFrame;
            public bool playbackProgressed;
            public bool naturalCompletion;
            public bool completionCallback;
            public bool audioConfigured;
            public bool earlyRuntimeCapture;
            public bool middleRuntimeCapture;
            public bool cleanup;
            public string error;
            public double firstFrameTime;
            public double maximumPlaybackTime;
            public float elapsedSeconds;
        }

        [Serializable]
        private sealed class SmokeEvidence
        {
            public string mode = "ROKAS_BUILT_WINDOWS_PLAYER_VIDEO_SMOKE";
            public string unityVersion;
            public string platform;
            public string streamingAssetsPath;
            public MediaEvidence startup = new MediaEvidence();
            public MediaEvidence laptop = new MediaEvidence();
            public bool videoPlayerErrorReceived;
            public int videoPlayerCount;
            public int audioSourceCount;
            public bool resourceLifetimeClean;
            public bool finalPass;
        }

        private VideoSequencePresenter presenter;
        private VideoPlayer player;
        private AudioSource videoAudio;
        private SmokeEvidence evidence;
        private MediaEvidence activeEvidence;
        private bool activeCompletion;
        private long firstFrameIndex;
        private double firstFrameTime;
        private string outputDirectory;

        public static bool IsRequested(string[] args)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], SmokeArgument, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private IEnumerator Start()
        {
            outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "rokas-video-smoke");
            Directory.CreateDirectory(outputDirectory);

            evidence = new SmokeEvidence
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                streamingAssetsPath = Application.streamingAssetsPath
            };

            presenter = GetComponent<VideoSequencePresenter>();
            if (!presenter) presenter = gameObject.AddComponent<VideoSequencePresenter>();
            player = GetComponent<VideoPlayer>();
            videoAudio = GetComponent<AudioSource>();

            if (!player || !videoAudio)
            {
                evidence.startup.error = "Production VideoSequencePresenter did not create its VideoPlayer/AudioSource dependencies.";
                FinishRun(false);
                yield break;
            }

            player.prepareCompleted += OnPrepared;
            player.frameReady += OnFrameReady;
            player.loopPointReached += OnLoopPointReached;
            player.errorReceived += OnError;

            yield return RunMedia(evidence.startup, "StartupPreview.mp4", true, 1280, 720, "startup");
            if (evidence.startup.error.Length == 0)
                yield return RunMedia(evidence.laptop, "LaptopBoot.mp4", false, 1792, 1008, "laptop");
            else
                evidence.laptop.error = "Skipped because startup runtime proof failed.";

            yield return null;
            evidence.videoPlayerCount = GetComponents<VideoPlayer>().Length;
            evidence.audioSourceCount = GetComponents<AudioSource>().Length;
            evidence.resourceLifetimeClean = evidence.videoPlayerCount == 1 && evidence.audioSourceCount == 1 &&
                                             !presenter.IsPlaying && presenter.TemporaryRenderTexture == null &&
                                             player.targetTexture == null && !videoAudio.isPlaying &&
                                             GetComponentsInChildren<RawImage>(true).Length == 0;

            bool pass = MediaPassed(evidence.startup) && MediaPassed(evidence.laptop) &&
                        !evidence.videoPlayerErrorReceived && evidence.resourceLifetimeClean;
            FinishRun(pass);
        }

        private IEnumerator RunMedia(MediaEvidence media, string fileName, bool startup,
            int width, int height, string capturePrefix)
        {
            activeEvidence = media;
            activeCompletion = false;
            firstFrameIndex = -1;
            firstFrameTime = -1;
            media.fileName = fileName;
            media.error = string.Empty;
            string mediaPath = Path.Combine(Application.streamingAssetsPath, "RokasVideo", fileName);
            media.fileFound = File.Exists(mediaPath);
            if (!media.fileFound)
            {
                media.error = "Packaged media missing: " + fileName;
                yield break;
            }

            RectTransform laptopHost = null;
            float startedAt = Time.realtimeSinceStartup;
            if (startup)
            {
                presenter.PlayStartup(fileName, 0f, () => { activeCompletion = true; media.completionCallback = true; });
            }
            else
            {
                GameObject hostObject = new GameObject("VideoSmokeLaptopHost", typeof(RectTransform), typeof(Canvas));
                laptopHost = (RectTransform)hostObject.transform;
                Canvas canvas = hostObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                presenter.PlayInHost(laptopHost, fileName, "LaptopBootSurface", width, height, 0f,
                    () => { activeCompletion = true; media.completionCallback = true; });
            }

            media.audioConfigured = player.audioOutputMode == VideoAudioOutputMode.AudioSource &&
                                    player.controlledAudioTrackCount == 1 &&
                                    player.GetTargetAudioSource(0) == videoAudio;

            bool earlyCaptured = false;
            bool middleCaptured = false;
            float deadline = startedAt + MediaTimeoutSeconds;
            while (!activeCompletion && Time.realtimeSinceStartup < deadline)
            {
                if (media.firstFrame && presenter.TemporaryRenderTexture != null)
                {
                    if (!earlyCaptured)
                    {
                        earlyCaptured = CaptureRenderTexture(presenter.TemporaryRenderTexture,
                            capturePrefix + "-unity-player-runtime-early.png", ref media.error);
                        media.earlyRuntimeCapture = earlyCaptured;
                    }

                    double currentTime = player.time;
                    if (currentTime > media.maximumPlaybackTime) media.maximumPlaybackTime = currentTime;
                    if ((player.frame > firstFrameIndex + 2) || currentTime > firstFrameTime + 0.20)
                        media.playbackProgressed = true;

                    double length = player.length;
                    double middleThreshold = length > 0.5 ? length * 0.45 : firstFrameTime + 0.75;
                    if (!middleCaptured && currentTime >= middleThreshold)
                    {
                        middleCaptured = CaptureRenderTexture(presenter.TemporaryRenderTexture,
                            capturePrefix + "-unity-player-runtime-middle.png", ref media.error);
                        media.middleRuntimeCapture = middleCaptured;
                    }
                }
                yield return null;
            }

            if (!activeCompletion && string.IsNullOrEmpty(media.error))
            {
                media.error = "Smoke safety timeout before production completion callback: " + fileName;
                presenter.Cancel();
            }

            media.elapsedSeconds = Time.realtimeSinceStartup - startedAt;
            yield return null;
            media.cleanup = !presenter.IsPlaying && presenter.TemporaryRenderTexture == null &&
                            player.targetTexture == null && !videoAudio.isPlaying;

            if (laptopHost)
            {
                Destroy(laptopHost.gameObject);
                yield return null;
            }

            if (string.IsNullOrEmpty(media.error))
            {
                if (!media.prepared) media.error = "prepareCompleted was not observed: " + fileName;
                else if (!media.firstFrame) media.error = "No decoded frame was observed: " + fileName;
                else if (!media.playbackProgressed) media.error = "Playback did not advance after first frame: " + fileName;
                else if (!media.naturalCompletion) media.error = "loopPointReached was not observed: " + fileName;
                else if (!media.completionCallback) media.error = "Production completion callback was not observed: " + fileName;
                else if (!media.audioConfigured) media.error = "Production VideoPlayer audio routing was not configured: " + fileName;
                else if (!media.earlyRuntimeCapture || !media.middleRuntimeCapture) media.error = "Runtime frame capture was incomplete: " + fileName;
                else if (!media.cleanup) media.error = "Production video resources were not cleaned after completion: " + fileName;
            }

            activeEvidence = null;
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (source == player && activeEvidence != null) activeEvidence.prepared = true;
        }

        private void OnFrameReady(VideoPlayer source, long frameIndex)
        {
            if (source != player || activeEvidence == null) return;
            if (!activeEvidence.firstFrame)
            {
                activeEvidence.firstFrame = true;
                firstFrameIndex = frameIndex;
                firstFrameTime = source.time;
                activeEvidence.firstFrameTime = firstFrameTime;
            }
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            if (source == player && activeEvidence != null) activeEvidence.naturalCompletion = true;
        }

        private void OnError(VideoPlayer source, string message)
        {
            if (source != player || activeEvidence == null) return;
            evidence.videoPlayerErrorReceived = true;
            activeEvidence.error = "VideoPlayer errorReceived: " + message;
        }

        private static bool MediaPassed(MediaEvidence media)
        {
            return media.fileFound && media.prepared && media.firstFrame && media.playbackProgressed &&
                   media.naturalCompletion && media.completionCallback && media.audioConfigured &&
                   media.earlyRuntimeCapture && media.middleRuntimeCapture && media.cleanup &&
                   string.IsNullOrEmpty(media.error);
        }

        private bool CaptureRenderTexture(RenderTexture source, string fileName, ref string error)
        {
            try
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = source;
                Texture2D capture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                capture.Apply(false, false);
                byte[] bytes = capture.EncodeToPNG();
                Destroy(capture);
                RenderTexture.active = previous;
                string path = Path.Combine(outputDirectory, fileName);
                File.WriteAllBytes(path, bytes);
                return bytes != null && bytes.Length > 0 && File.Exists(path);
            }
            catch (Exception exception)
            {
                error = "Runtime capture failed: " + exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private void FinishRun(bool pass)
        {
            if (evidence == null) evidence = new SmokeEvidence();
            evidence.finalPass = pass;
            try
            {
                Directory.CreateDirectory(outputDirectory ?? Directory.GetCurrentDirectory());
                string path = Path.Combine(outputDirectory ?? Directory.GetCurrentDirectory(), "rokas-video-smoke-result.json");
                File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("ROKAS video smoke could not write result JSON: " + exception);
                pass = false;
            }

            Debug.Log(pass ? "ROKAS_VIDEO_SMOKE_PASS" : "ROKAS_VIDEO_SMOKE_FAIL");
            Application.Quit(pass ? 0 : 1);
        }

        private void OnDestroy()
        {
            if (!player) return;
            player.prepareCompleted -= OnPrepared;
            player.frameReady -= OnFrameReady;
            player.loopPointReached -= OnLoopPointReached;
            player.errorReceived -= OnError;
        }
    }
}
