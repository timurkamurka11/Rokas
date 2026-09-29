using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        private sealed class McLifecycleVideoPreview : VnSceneComposerVideoPreview
        {
            public int PrepareCalls;
            public int PlayCalls;
            public int DisposeCalls;

            private bool prepared;
            private bool playing;

            public McLifecycleVideoPreview(bool shouldLoop, int serial)
                : base(null, 16, 16, shouldLoop)
            {
                warning = string.Empty;
                texture = new RenderTexture(32, 18, 0)
                {
                    name = "ROKAS_MC_Video_" + serial
                };
                texture.Create();
            }

            public override bool IsPrepared { get { return prepared; } }
            public override bool IsPreparing { get { return false; } }
            public override bool IsPlaying { get { return playing; } }
            public override bool HasVisibleFrame { get { return prepared; } }

            public override void Prepare()
            {
                PrepareCalls++;
                prepared = true;
            }

            public override void Play()
            {
                PlayCalls++;
                if (!prepared) Prepare();
                playing = true;
            }

            public override void Pause()
            {
                playing = false;
            }

            public override void Restart()
            {
                playing = false;
            }

            public override void Dispose()
            {
                DisposeCalls++;
                playing = false;
                base.Dispose();
            }
        }

        private sealed class McLifecycleVideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly List<McLifecycleVideoPreview> Created = new List<McLifecycleVideoPreview>();

            public int TotalPrepareCalls
            {
                get
                {
                    int total = 0;
                    for (int i = 0; i < Created.Count; i++) total += Created[i].PrepareCalls;
                    return total;
                }
            }

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                var preview = new McLifecycleVideoPreview(media != null && media.loop, Created.Count);
                Created.Add(preview);
                return preview;
            }
        }

        [Test]
        public void MC_PreparedAuthoringVideoDoesNotIssueAnotherPrepareWhenPlaySceneIsClicked()
        {
            var factory = new McLifecycleVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();

            try
            {
                window.ComposerAddScene();
                FieldInfo projectField = typeof(VnPresentationWorkshopWindow).GetField(
                    "_sceneComposerProject", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(projectField, Is.Not.Null);
                var project = projectField.GetValue(window) as VnSceneComposerProject;
                Assert.That(project, Is.Not.Null);
                Assert.That(project.scenes.Count, Is.EqualTo(1));

                project.scenes[0].media = new VnSceneComposerMediaReference
                {
                    kind = VnSceneComposerMediaKind.ExternalVideo,
                    reference = "mc-first-play.mp4",
                    displayName = "mc-first-play.mp4",
                    contentHash = "mc-prepared-authoring",
                    scaleMode = VnSceneComposerMediaScaleMode.Fit,
                    loop = false
                };

                Assert.That(window.ComposerPrepareSelectedVideoForAuthoring(), Is.True);
                Assert.That(factory.Created.Count, Is.EqualTo(1),
                    "Authoring preparation should create the selected video preview once.");
                Assert.That(factory.Created[0].IsPrepared, Is.True);
                int preparesBeforePlay = factory.TotalPrepareCalls;
                Assert.That(preparesBeforePlay, Is.EqualTo(1));

                window.ComposerPlayScene();

                Assert.That(factory.TotalPrepareCalls, Is.EqualTo(preparesBeforePlay),
                    "Clicking Play Scene after the selected authoring video is already prepared must not start another expensive Prepare on a separate playback-owned video preview.");
            }
            finally
            {
                Object.DestroyImmediate(window);
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
