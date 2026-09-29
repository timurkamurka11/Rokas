using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void UxG_PreparedVideoSizingPreservesSourceAspectWithinPreviewBudget()
        {
            MethodInfo resolve = typeof(VnSceneComposerVideoPreview).GetMethod(
                "ResolvePreparedRenderSize",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null);

            Assert.That(resolve, Is.Not.Null,
                "ExternalVideo needs one explicit prepared-source sizing policy instead of keeping the fixed placeholder RenderTexture size.");
            if (resolve == null) return;

            Vector2Int landscape = (Vector2Int)resolve.Invoke(null, new object[] { 3840, 2160 });
            Vector2Int portrait = (Vector2Int)resolve.Invoke(null, new object[] { 1080, 1920 });
            Vector2Int small = (Vector2Int)resolve.Invoke(null, new object[] { 640, 360 });

            Assert.That(Mathf.Max(landscape.x, landscape.y), Is.LessThanOrEqualTo(1920),
                "Editor video preview should be bounded to the VN reference-quality budget rather than allocating source 4K blindly.");
            Assert.That((float)landscape.x / landscape.y, Is.EqualTo(3840f / 2160f).Within(.002f));
            Assert.That((float)portrait.x / portrait.y, Is.EqualTo(1080f / 1920f).Within(.002f));
            Assert.That(Mathf.Max(portrait.x, portrait.y), Is.LessThanOrEqualTo(1920));
            Assert.That(small, Is.EqualTo(new Vector2Int(640, 360)),
                "A source already below the quality budget must not be needlessly enlarged before display.");
        }

        [Test]
        public void UxG_PreparedSourceSizingIsAppliedOnceBeforeFirstVisibleFrame()
        {
            string motion = ReadUxFEditorSource("VnSceneComposerMotionMediaEditing.cs");
            string prepared = ExtractUxFMethodBody(motion, "private void OnPrepared(VideoPlayer prepared)");
            string frameReady = ExtractUxFMethodBody(motion, "private void OnFrameReady(VideoPlayer source, long frameIndex)");

            Assert.That(prepared, Does.Contain("EnsureRenderTargetMatchesPreparedSource(prepared)"),
                "Prepared video metadata must stabilize RenderTexture dimensions before the first decoded frame is requested.");
            Assert.That(frameReady, Does.Not.Contain("EnsureRenderTargetMatchesPreparedSource"),
                "Stable playback frames must not continuously resize/recreate the RenderTexture.");
        }

        [Test]
        public void UxG_ExistingFitAndFillMappingsPreserveAspect()
        {
            Assert.That(VnSceneComposerMediaEditing.ToUnityScaleMode(VnSceneComposerMediaScaleMode.Fit),
                Is.EqualTo(ScaleMode.ScaleToFit));
            Assert.That(VnSceneComposerMediaEditing.ToUnityScaleMode(VnSceneComposerMediaScaleMode.Fill),
                Is.EqualTo(ScaleMode.ScaleAndCrop));
        }

        [Test]
        public void UxG_ScaleModeContractIncludesExplicitStretchMapping()
        {
            string types = ReadUxFEditorSource("VnSceneComposerTypes.cs");
            string media = ReadUxFEditorSource("VnSceneComposerMediaEditing.cs");

            Assert.That(types, Does.Contain("Stretch"),
                "Canonical media display mode must explicitly support intentional aspect-breaking Stretch instead of treating stretching as an implicit renderer default.");
            Assert.That(media, Does.Contain("VnSceneComposerMediaScaleMode.Stretch"));
            Assert.That(media, Does.Contain("ScaleMode.StretchToFill"));
        }

        [Test]
        public void UxG_PlaybackFrameCarriesCanonicalSourceAndTargetScaleModes()
        {
            string playback = ReadUxFEditorSource("VnSceneComposerPlaybackController.cs");

            Assert.That(playback, Does.Contain("SourceScaleMode"),
                "Renderer-facing playback frames must carry the authored source media display policy.");
            Assert.That(playback, Does.Contain("TargetScaleMode"),
                "Renderer-facing playback frames must carry the authored target media display policy.");
            Assert.That(playback, Does.Contain("targetScene.media.scaleMode"),
                "Playback must route the canonical scene media scale mode instead of inventing a parallel display state.");
        }

        [Test]
        public void UxG_CentralPlaybackRendererUsesPerSceneScaleModeInsteadOfForcedStretch()
        {
            string renderer = ReadUxFEditorSource("VnPresentationWorkshopPreviewRenderer.Playback.cs");

            Assert.That(renderer, Does.Contain("VnSceneComposerMediaEditing.ToUnityScaleMode"),
                "Central authoring/playback rendering must map each scene's canonical Fit/Fill/Stretch policy at draw time.");
            Assert.That(renderer, Does.Contain("TargetScaleMode"));
            Assert.That(renderer, Does.Contain("SourceScaleMode"));
        }

        [Test]
        public void UxG_StaticAuthoringPreviewPropagatesSelectedSceneScaleMode()
        {
            string videoUi = ReadUxFEditorSource("VnPresentationWorkshopWindow.SceneComposerVideo.cs");
            string build = ExtractUxFMethodBody(videoUi, "public VnSceneComposerPlaybackFrame ComposerBuildSelectedPreviewPlaybackFrame()");

            Assert.That(build, Does.Contain("scene.media.scaleMode"),
                "Static authoring preview and its first visible video frame must use the same canonical framing policy.");
        }

        [Test]
        public void UxG_BasicMediaUiUsesRussianDisplayModeLabels()
        {
            string window = ReadUxFEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string inspector = ExtractUxFMethodBody(window, "private void DrawSceneComposerMediaInspector(VnSceneComposerScene scene)");

            Assert.That(inspector, Does.Contain("Отображение"));
            Assert.That(inspector, Does.Contain("Вписать"));
            Assert.That(inspector, Does.Contain("Заполнить"));
            Assert.That(inspector, Does.Contain("Растянуть"));
            Assert.That(inspector, Does.Not.Contain("EnumPopup(\"Масштаб\", scene.media.scaleMode)"),
                "Ordinary Media UI must not expose raw Fit/Fill enum names.");
        }

        [Test]
        public void UxG_PreviewResolutionAndWindowDrawingDoNotRewriteCanonicalMediaData()
        {
            string presentation = ReadUxFEditorSource("VnPresentationWorkshopWindow.SceneComposerPresentation.cs");
            string setResolution = ExtractUxFMethodBody(presentation, "public void ComposerSetPreviewResolution(VnWorkshopResolution resolution)");
            string window = ReadUxFEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string drawPreview = ExtractUxFMethodBody(window, "private void DrawSceneComposerPreview()");

            Assert.That(setResolution, Does.Not.Contain("scene.media"),
                "Changing preview resolution must remain display-only and must not mutate canonical media bindings.");
            Assert.That(drawPreview, Does.Not.Contain("scene.media.scaleMode ="),
                "Resizing/redrawing the editor preview must not rewrite the scene's authored media mode.");
        }

        [Test]
        public void UxG_GenuineSameSceneSourceChangeStillRecreatesVideoResources()
        {
            var factory = new FakeVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            project.scenes.Add(VideoScene("Video", "A.mp4", false));

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                Assert.That(factory.Created.Count, Is.EqualTo(1));
                FakeVideoPreview initial = factory.Created[0];

                project.scenes[0].media.reference = "B.mp4";
                project.scenes[0].media.contentHash = "changed-source";
                controller.PlayScene(0);

                Assert.That(factory.Created.Count, Is.EqualTo(2),
                    "Changing the actual video source must invalidate same-scene preview reuse.");
                Assert.That(initial.DisposeCalls, Is.EqualTo(1));
                Assert.That(factory.Created[1].PlayCalls, Is.EqualTo(1));
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                DisposeFakePreviews(factory);
            }
        }
    }
}
