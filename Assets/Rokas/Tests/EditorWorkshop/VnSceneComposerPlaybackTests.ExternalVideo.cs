using System.Collections.Generic;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        private sealed class FakeVideoPreview : VnSceneComposerVideoPreview
        {
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
            public override void Prepare() { }

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
                FakeVideoPreview playing = factory.Created[factory.Created.Count - 1];
                Assert.That(initial.DisposeCalls, Is.EqualTo(1));
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
