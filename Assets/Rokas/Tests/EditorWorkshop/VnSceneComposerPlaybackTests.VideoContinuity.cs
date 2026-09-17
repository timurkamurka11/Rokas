using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
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
    }
}
