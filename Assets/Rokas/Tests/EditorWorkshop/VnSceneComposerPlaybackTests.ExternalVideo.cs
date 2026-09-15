using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        private sealed class FakeVideoPreview : VnSceneComposerVideoPreview
        {
            public int PrepareCalls;
            public int PlayCalls;
            public int PauseCalls;
            public int RestartCalls;
            public int DisposeCalls;
            private bool playing;

            public FakeVideoPreview(bool shouldLoop, int serial)
                : base(null, 16, 16, shouldLoop)
            {
                warning = string.Empty;
                texture = new RenderTexture(32, 18, 0)
                {
                    name = "ROKAS_TestVideo_" + serial
                };
                texture.Create();
            }

            public override bool IsPrepared { get { return true; } }
            public override bool IsPreparing { get { return false; } }
            public override bool IsPlaying { get { return playing; } }
            public override bool HasVisibleFrame { get { return true; } }
            public override void Prepare() { PrepareCalls++; }

            public override void Play()
            {
                PlayCalls++;
                playing = true;
            }

            public override void Pause()
            {
                PauseCalls++;
                playing = false;
            }

            public override void Restart()
            {
                RestartCalls++;
                playing = false;
            }

            public override void Dispose()
            {
                DisposeCalls++;
                playing = false;
                base.Dispose();
            }
        }

        private sealed class FakeVideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly List<FakeVideoPreview> Created = new List<FakeVideoPreview>();

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                var preview = new FakeVideoPreview(media != null && media.loop, Created.Count);
                Created.Add(preview);
                return preview;
            }
        }

        [Test]
        public void ExternalVideoPlaybackRoutesRendererTextureAndPreservesPauseRestartLoopAndSceneSwitchCleanup()
        {
            var factory = new FakeVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            project.scenes.Add(VideoScene("Video A", "A.mp4", true));
            project.scenes.Add(VideoScene("Video B", "B.mp4", false));

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                Assert.That(factory.Created.Count, Is.EqualTo(1));
                FakeVideoPreview initial = factory.Created[0];
                Assert.That(initial.loop, Is.True);
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(initial.texture));
                Assert.That(controller.CurrentFrame.TargetBackground, Is.SameAs(initial.texture),
                    "The renderer-facing frame must use ExternalVideo media instead of the default static VN background.");

                controller.PlayScene(0);
                Assert.That(factory.Created.Count, Is.EqualTo(1),
                    "Playing the already-open current video scene must reuse the existing compatible preview.");
                FakeVideoPreview playing = initial;
                Assert.That(initial.DisposeCalls, Is.EqualTo(0),
                    "Same-scene playback must not dispose the prepared preview before it is used.");
                Assert.That(playing.PlayCalls, Is.EqualTo(1));
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(playing.texture));
                Assert.That(controller.CurrentFrame.TargetBackground, Is.SameAs(playing.texture));

                Texture pausedTexture = controller.CurrentMediaTexture;
                controller.Pause();
                Assert.That(playing.PauseCalls, Is.EqualTo(1));
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(pausedTexture),
                    "Pause must keep the currently displayed video texture stable.");

                controller.Restart();
                Assert.That(playing.RestartCalls, Is.EqualTo(1));
                Assert.That(playing.PlayCalls, Is.EqualTo(2));
                Assert.That(controller.MediaTimeSeconds, Is.EqualTo(0f).Within(.0001f));
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(pausedTexture));

                controller.Next();
                Assert.That(playing.DisposeCalls, Is.EqualTo(1),
                    "Switching Scene must dispose the previous target video preview.");
                FakeVideoPreview next = factory.Created[factory.Created.Count - 1];
                Assert.That(next.loop, Is.False, "Loop must propagate from the selected Scene media binding.");
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(next.texture));
                Assert.That(controller.CurrentFrame.TargetBackground, Is.SameAs(next.texture));
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                for (int i = 0; i < factory.Created.Count; i++)
                {
                    FakeVideoPreview preview = factory.Created[i];
                    if (preview != null && preview.texture != null) preview.Dispose();
                }
            }
        }

        [Test]
        public void UxF_PrepareRequestsAreCoalescedForSameAuthoringPreview()
        {
            string motion = ReadUxFEditorSource("VnSceneComposerMotionMediaEditing.cs");
            string prepare = ExtractUxFMethodBody(motion, "public virtual void Prepare()");
            string authoring = ReadUxFEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string request = ExtractUxFMethodBody(authoring, "public bool ComposerPrepareSelectedVideoForAuthoring()");

            Assert.That(prepare, Does.Contain("if (player.isPrepared)"));
            Assert.That(prepare, Does.Contain("if (prepareRequested) return;"),
                "Repeated Prepare requests for the same video preview must be coalesced.");
            Assert.That(request, Does.Contain("!video.IsPrepared && !video.IsPreparing"),
                "OnGUI/repaint authoring must not restart Prepare while the same preview is preparing or prepared.");
        }

        [Test]
        public void UxF_IdlePreparedAuthoringIsPausedAndDoesNotContinuouslyRepaint()
        {
            string motion = ReadUxFEditorSource("VnSceneComposerMotionMediaEditing.cs");
            string frameReady = ExtractUxFMethodBody(motion, "private void OnFrameReady(VideoPlayer source, long frameIndex)");
            string window = ReadUxFEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string update = ExtractUxFMethodBody(window, "private void SceneComposerEditorUpdate()");

            Assert.That(frameReady, Does.Contain("previewFrameRequested && !playRequested && source.isPlaying"));
            Assert.That(frameReady, Does.Contain("source.Pause();"),
                "Authoring first-frame decoding must settle into a paused stable preview.");
            Assert.That(update, Does.Contain("!_sceneComposerPlayback.IsPlaying"),
                "An idle paused authoring preview must not drive the continuous playback repaint loop.");
        }

        [Test]
        public void UxF_RestartRetainsVisiblePreparedFrame()
        {
            string motion = ReadUxFEditorSource("VnSceneComposerMotionMediaEditing.cs");
            string restart = ExtractUxFMethodBody(motion, "public virtual void Restart()");

            Assert.That(restart, Does.Not.Contain("hasVisibleFrame = false;"),
                "Restart must keep the current prepared frame visible while the decoder seeks back to frame zero.");
        }

        [Test]
        public void UxF_PlaySceneReusesAlreadyOpenedVideoPreview()
        {
            var factory = new FakeVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            project.scenes.Add(VideoScene("Video A", "A.mp4", true));

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                Assert.That(factory.Created.Count, Is.EqualTo(1));
                FakeVideoPreview initial = factory.Created[0];
                Texture initialTexture = initial.texture;

                controller.PlayScene(0);

                Assert.That(factory.Created.Count, Is.EqualTo(1),
                    "Playing the already-open current video scene must reuse its prepared preview instead of creating a second VideoPlayer/RenderTexture pair.");
                Assert.That(initial.DisposeCalls, Is.EqualTo(0));
                Assert.That(initial.PlayCalls, Is.EqualTo(1));
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(initialTexture));
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                DisposeFakePreviews(factory);
            }
        }

        [Test]
        public void UxF_PauseRetainsVisibleFrameAndResources()
        {
            var factory = new FakeVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            project.scenes.Add(VideoScene("Video A", "A.mp4", false));

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                controller.PlayScene(0);
                FakeVideoPreview active = factory.Created[factory.Created.Count - 1];
                Texture visible = controller.CurrentMediaTexture;
                int created = factory.Created.Count;

                controller.Pause();

                Assert.That(active.PauseCalls, Is.EqualTo(1));
                Assert.That(factory.Created.Count, Is.EqualTo(created));
                Assert.That(active.HasVisibleFrame, Is.True);
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(visible));
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                DisposeFakePreviews(factory);
            }
        }

        [Test]
        public void UxF_RestartDoesNotRecreateOrPrepareResources()
        {
            var factory = new FakeVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            project.scenes.Add(VideoScene("Video A", "A.mp4", false));

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                controller.PlayScene(0);
                FakeVideoPreview active = factory.Created[factory.Created.Count - 1];
                int created = factory.Created.Count;
                int prepared = active.PrepareCalls;
                Texture texture = controller.CurrentMediaTexture;

                controller.Restart();

                Assert.That(factory.Created.Count, Is.EqualTo(created));
                Assert.That(active.PrepareCalls, Is.EqualTo(prepared));
                Assert.That(active.RestartCalls, Is.EqualTo(1));
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(texture));
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                DisposeFakePreviews(factory);
            }
        }

        [Test]
        public void UxF_LoopDoesNotReinitializeAuthoringPipeline()
        {
            var factory = new FakeVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            project.scenes.Add(VideoScene("Loop", "loop.mp4", true));

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                controller.PlayScene(0);
                int created = factory.Created.Count;
                FakeVideoPreview active = factory.Created[factory.Created.Count - 1];

                controller.Advance(.10f);
                controller.Advance(.10f);
                controller.Advance(.10f);

                Assert.That(active.loop, Is.True);
                Assert.That(factory.Created.Count, Is.EqualTo(created),
                    "Looping playback must remain on the same video preview instead of rebuilding the authoring pipeline each cycle/update.");
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                DisposeFakePreviews(factory);
            }
        }

        [Test]
        public void UxF_SceneSwitchDisposesOldTargetExactlyOnce()
        {
            var factory = new FakeVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            project.scenes.Add(VideoScene("A", "A.mp4", false));
            project.scenes.Add(VideoScene("B", "B.mp4", false));

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                controller.PlayScene(0);
                FakeVideoPreview oldTarget = factory.Created[factory.Created.Count - 1];

                controller.Next();

                Assert.That(oldTarget.DisposeCalls, Is.EqualTo(1),
                    "Switching to another scene/source must dispose the previous target preview exactly once.");
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                DisposeFakePreviews(factory);
            }
        }

        [Test]
        public void UxF_VideoLifecycleStatusesAreRussian()
        {
            string authoring = ReadUxFEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string state = ExtractUxFMethodBody(authoring, "private void DrawSceneComposerVideoPreparationState(Rect previewRect)");

            Assert.That(state, Does.Contain("Подготовка видео…"),
                "Preparing state must be user-facing Russian UI.");
            Assert.That(state, Does.Contain("Не удалось открыть видео"),
                "Video failure must use a clear Russian user-facing diagnostic.");
            Assert.That(state, Does.Not.Contain("Preparing video"));
            Assert.That(state, Does.Not.Contain("Missing / failed External Video"));
        }

        [Test]
        public void UxF_PreparingAuthoringPreviewUsesPosterInsteadOfEmptyVideoTexture()
        {
            string videoUi = ReadUxFEditorSource("VnPresentationWorkshopWindow.SceneComposerVideo.cs");
            string build = ExtractUxFMethodBody(videoUi, "public VnSceneComposerPlaybackFrame ComposerBuildSelectedPreviewPlaybackFrame()");

            Assert.That(build, Does.Contain("HasVisibleFrame"),
                "The static authoring renderer must gate the video RenderTexture until it contains a decoded visible frame.");
            Assert.That(build, Does.Contain("frame.BackgroundTexture"),
                "While video is preparing, the authoring preview must retain a poster/background representation instead of routing an empty RenderTexture.");
        }

        private static string ReadUxFEditorSource(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop", fileName);
            Assert.That(File.Exists(path), Is.True, "Missing inspected editor source: " + path);
            return File.ReadAllText(path);
        }

        private static string ExtractUxFMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), "Missing method signature: " + signature);
            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.That(openBrace, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(openBrace, i - openBrace + 1);
                }
            }
            Assert.Fail("Unterminated method body: " + signature);
            return string.Empty;
        }

        private static void DisposeFakePreviews(FakeVideoFactory factory)
        {
            if (factory == null) return;
            for (int i = 0; i < factory.Created.Count; i++)
            {
                FakeVideoPreview preview = factory.Created[i];
                if (preview != null && preview.texture != null) preview.Dispose();
            }
        }

        private static VnSceneComposerScene VideoScene(string label, string reference, bool loop)
        {
            return new VnSceneComposerScene
            {
                label = label,
                narration = true,
                previewText = string.Empty,
                media = new VnSceneComposerMediaReference
                {
                    kind = VnSceneComposerMediaKind.ExternalVideo,
                    reference = reference,
                    displayName = reference,
                    localPreviewDependency = true,
                    loop = loop,
                    scaleMode = VnSceneComposerMediaScaleMode.Fit
                },
                timing = new VnSceneComposerTiming
                {
                    previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.ManualBeat,
                    previewAutoDuration = 2f
                }
            };
        }
    }
}
