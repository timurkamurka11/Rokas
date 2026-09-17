using System.Collections.Generic;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        private sealed class Mc2DelayedVideoPreview : VnSceneComposerVideoPreview
        {
            public readonly string Reference;
            public int PrepareCalls;
            public int PlayCalls;
            public int PauseCalls;
            public int DisposeCalls;

            private readonly bool delayed;
            private bool prepared;
            private bool playing;
            private bool visible;

            public Mc2DelayedVideoPreview(VnSceneComposerMediaReference media, int serial)
                : base(null, 16, 16, media != null && media.loop)
            {
                Reference = media != null ? media.reference : string.Empty;
                delayed = Reference == "B-delayed.mp4";
                warning = string.Empty;
                texture = new RenderTexture(32, 18, 0)
                {
                    name = "ROKAS_MC2_Video_" + serial
                };
                texture.Create();
            }

            public override bool IsPrepared { get { return prepared; } }
            public override bool IsPreparing { get { return PrepareCalls > 0 && !prepared; } }
            public override bool IsPlaying { get { return playing; } }
            public override bool HasVisibleFrame { get { return visible; } }

            public override void Prepare()
            {
                PrepareCalls++;
                if (!delayed)
                {
                    prepared = true;
                    visible = true;
                }
            }

            public override void Play()
            {
                PlayCalls++;
                if (!prepared) Prepare();
                playing = true;
            }

            public override void Pause()
            {
                PauseCalls++;
                playing = false;
            }

            public override void Restart()
            {
                playing = false;
            }

            public void CompletePreparation()
            {
                prepared = true;
                visible = true;
            }

            public override void Dispose()
            {
                DisposeCalls++;
                playing = false;
                base.Dispose();
            }
        }

        private sealed class Mc2DelayedVideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly List<Mc2DelayedVideoPreview> Created = new List<Mc2DelayedVideoPreview>();

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                var preview = new Mc2DelayedVideoPreview(media, Created.Count);
                Created.Add(preview);
                return preview;
            }
        }

        [Test]
        public void MC2_SameVideoOrderedBoundaryKeepsPreparedPlayerTextureAndTimeline()
        {
            var factory = new McLifecycleVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            VnSceneComposerScene sceneA = VideoScene("A", "shared-continuous.mp4", true);
            VnSceneComposerScene sceneB = VideoScene("B", "shared-continuous.mp4", true);
            sceneA.media.contentHash = "same-video-content";
            sceneB.media.contentHash = "same-video-content";
            sceneA.timing.previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration;
            sceneA.timing.previewAutoDuration = .01f;
            sceneB.timing.previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration;
            sceneB.timing.previewAutoDuration = .01f;
            project.scenes.Add(sceneA);
            project.scenes.Add(sceneB);

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                controller.PlayAll();

                Assert.That(factory.Created.Count, Is.EqualTo(1));
                McLifecycleVideoPreview continuous = factory.Created[0];
                Texture continuousTexture = continuous.texture;
                Assert.That(factory.TotalPrepareCalls, Is.EqualTo(1),
                    "The first scene should prepare the active video once before the ordered boundary.");

                controller.Advance(100f);

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1),
                    "The probe must cross the real ordered Scene A -> Scene B boundary.");
                Assert.That(factory.Created.Count, Is.EqualTo(1),
                    "Adjacent scenes with the same effective video must keep the active VideoPlayer/RenderTexture instead of creating source/target replacements.");
                Assert.That(factory.TotalPrepareCalls, Is.EqualTo(1),
                    "Crossing a same-video scene boundary must not issue another expensive Prepare.");
                Assert.That(continuous.DisposeCalls, Is.EqualTo(0),
                    "The valid outgoing player must stay alive across a compatible same-video boundary.");
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(continuousTexture),
                    "The same visible RenderTexture must remain routed after the logical scene changes.");
                Assert.That(controller.CurrentFrame.SourceBackground, Is.SameAs(continuousTexture));
                Assert.That(controller.CurrentFrame.TargetBackground, Is.SameAs(continuousTexture));
                Assert.That(controller.MediaTimeSeconds, Is.GreaterThan(0f),
                    "The video timeline must continue across the logical scene boundary instead of seeking/restarting at zero.");
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                for (int i = 0; i < factory.Created.Count; i++)
                {
                    McLifecycleVideoPreview preview = factory.Created[i];
                    if (preview != null && preview.texture != null) preview.Dispose();
                }
            }
        }

        [Test]
        public void MC2_DifferentVideoOrderedBoundaryKeepsOutgoingFrameUntilTargetIsVisible()
        {
            var factory = new Mc2DelayedVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            VnSceneComposerScene sceneA = VideoScene("A", "A-visible.mp4", false);
            VnSceneComposerScene sceneB = VideoScene("B", "B-delayed.mp4", false);
            sceneA.media.contentHash = "video-a";
            sceneB.media.contentHash = "video-b";
            sceneA.timing.previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration;
            sceneA.timing.previewAutoDuration = .01f;
            sceneB.timing.previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration;
            sceneB.timing.previewAutoDuration = 10f;
            project.scenes.Add(sceneA);
            project.scenes.Add(sceneB);

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                controller.PlayAll();

                Assert.That(factory.Created.Count, Is.EqualTo(1));
                Mc2DelayedVideoPreview outgoing = factory.Created[0];
                Texture outgoingTexture = outgoing.texture;
                Assert.That(outgoing.HasVisibleFrame, Is.True,
                    "Scene A must have a real visible outgoing frame before the boundary probe starts.");

                controller.Advance(100f);

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(factory.Created.Count, Is.EqualTo(2),
                    "Different-video handoff should retain outgoing A and open only target B, not recreate A as a source player.");
                Mc2DelayedVideoPreview target = factory.Created[1];
                Assert.That(target.Reference, Is.EqualTo("B-delayed.mp4"));
                Assert.That(target.PrepareCalls, Is.EqualTo(1),
                    "The new target should begin preparation once at the ordered boundary.");
                Assert.That(target.HasVisibleFrame, Is.False,
                    "This probe intentionally holds B before its first decoded visible frame.");
                Assert.That(outgoing.DisposeCalls, Is.EqualTo(0),
                    "Outgoing A must remain alive while B prepares so its last valid frame can stay visible.");
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(outgoingTexture),
                    "Until B has a visible frame, playback must keep routing A's valid outgoing texture instead of an empty target RenderTexture.");
                Assert.That(controller.CurrentFrame.SourceBackground, Is.SameAs(outgoingTexture));
                Assert.That(controller.CurrentFrame.TargetBackground, Is.SameAs(outgoingTexture),
                    "The renderer-facing target must fall back to the retained outgoing frame while B is not visible.");

                target.CompletePreparation();
                controller.Advance(0f);

                Assert.That(controller.CurrentMediaTexture, Is.SameAs(target.texture),
                    "Once B exposes a valid frame, the target texture should replace the retained outgoing fallback.");
                Assert.That(controller.CurrentFrame.SourceBackground, Is.SameAs(outgoingTexture));
                Assert.That(controller.CurrentFrame.TargetBackground, Is.SameAs(target.texture));
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                for (int i = 0; i < factory.Created.Count; i++)
                {
                    Mc2DelayedVideoPreview preview = factory.Created[i];
                    if (preview != null && preview.texture != null) preview.Dispose();
                }
            }
        }
    }
}
