using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerSceneEntryPolishTests
    {
        private sealed class PrewarmVideoPreview : VnSceneComposerVideoPreview
        {
            public int PrepareCalls;
            public int PlayCalls;
            public bool Prepared;
            public bool Preparing;
            public bool Visible;

            public PrewarmVideoPreview(string reference, int serial)
                : base(null, 16, 16, false)
            {
                warning = string.Empty;
                texture = new RenderTexture(64, 36, 0)
                {
                    name = "ROKAS_SceneEntryPolish_" + serial
                };
                texture.Create();
            }

            public override bool IsPrepared => Prepared;
            public override bool IsPreparing => Preparing;
            public override bool IsPlaying => false;
            public override bool HasVisibleFrame => Visible;

            public override void Prepare()
            {
                PrepareCalls++;
                Preparing = !Prepared;
            }

            public override void Play()
            {
                PlayCalls++;
                if (!Prepared)
                {
                    PrepareCalls++;
                    Preparing = true;
                }
            }

            public override void Dispose()
            {
                base.Dispose();
            }
        }

        private sealed class PrewarmFactory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly List<PrewarmVideoPreview> Created = new List<PrewarmVideoPreview>();

            public VnSceneComposerVideoPreview Open(
                VnSceneComposerMediaReference media, int width, int height)
            {
                var preview = new PrewarmVideoPreview(
                    media != null ? media.reference : string.Empty, Created.Count);
                Created.Add(preview);
                return preview;
            }
        }

        [Test]
        public void Polish_RED_A_ActivateWorkspacePrewarmsSelectedVideoBeforePreviewDraw()
        {
            var factory = new PrewarmFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            VnPresentationWorkshopWindow window =
                ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                VnSceneComposerScene scene = Project(window).scenes[0];
                scene.media = Video("cold-start.mp4");

                window.ActivateSceneComposerWorkspace();

                Assert.That(factory.Created.Count, Is.EqualTo(1),
                    "Opening/restoring Scene Composer must create the selected video request before Preview GUI draw.");
                Assert.That(factory.Created[0].PrepareCalls, Is.EqualTo(1),
                    "Selected video must begin Prepare immediately on workspace activation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                foreach (PrewarmVideoPreview preview in factory.Created) preview.Dispose();
            }
        }

        [Test]
        public void Polish_RED_A_SelectingVideoScenePrewarmsImmediately()
        {
            var factory = new PrewarmFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            VnPresentationWorkshopWindow window =
                ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerAddScene();
                VnSceneComposerProject project = Project(window);
                project.scenes[1].media = Video("scene-b.mp4");

                window.ComposerSelectScene(1);

                Assert.That(factory.Created.Count, Is.EqualTo(1));
                Assert.That(factory.Created[0].PrepareCalls, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                foreach (PrewarmVideoPreview preview in factory.Created) preview.Dispose();
            }
        }

        [Test]
        public void Polish_RED_B_PlaybackFrameExposesIndependentEntryPresentationGates()
        {
            Type frame = typeof(VnSceneComposerPlaybackFrame);
            Assert.That(frame.GetProperty("ShowDialoguePanel"), Is.Not.Null,
                "Scene entry needs a plaque gate independent from character/text visibility.");
            Assert.That(frame.GetProperty("ShowCharacters"), Is.Not.Null,
                "Scene entry needs a character gate after the plaque.");
            Assert.That(frame.GetProperty("ShowDialogueText"), Is.Not.Null,
                "Typewriter/text needs its own gate after character presentation starts.");
        }

        [Test]
        public void Polish_RED_C_HopIsARealSerializedBeatEffect()
        {
            Assert.That(Enum.GetNames(typeof(VnSceneComposerBeatEffect)), Does.Contain("Hop"));
        }

        [Test]
        public void Polish_RED_D_LongTextEditorPinsHorizontalWidth()
        {
            string source = ReadSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            Assert.That(source, Does.Contain("GUILayout.Width(availableWidth)"),
                "The dialogue TextArea must use the bounded inspector width, not content-driven horizontal expansion.");
            Assert.That(source, Does.Contain("_sceneComposerInspectorScroll.x = 0f;"),
                "The right inspector must stay horizontally pinned when dialogue text becomes long.");
        }

        [Test]
        public void Polish_RED_E_SceneBodySizeCannotForkSharedSequenceSize()
        {
            var project = new VnSceneComposerProject();
            project.defaultPresentation.typography.hasDialogueFontSize = true;
            project.defaultPresentation.typography.dialogueFontSize = 40f;
            var scene = new VnSceneComposerScene();
            scene.dialogueBodyStyleOverride.hasFontSize = true;
            scene.dialogueBodyStyleOverride.fontSize = 72f;
            project.scenes.Add(scene);

            VnWorkshopTypographyValues resolved =
                VnSceneComposerTextStyleResolver.Resolve(project, scene, scene.dialogueBeats[0]);

            Assert.That(resolved.DialogueFontSize, Is.EqualTo(40f).Within(.001f),
                "Dialogue Body Size is sequence-global; a Scene override must not fork it.");
        }

        [Test]
        public void Polish_RED_F_DirectPosePngShortcutExistsInDialogueAuthoring()
        {
            MethodInfo import = typeof(VnPresentationWorkshopWindow).GetMethod(
                "ComposerImportSelectedDialogueBeatPosePng",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(import, Is.Not.Null,
                "Dialogue authoring needs a direct PNG -> existing M-F1 Character State shortcut.");

            string source = ReadSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            Assert.That(source, Does.Contain("Выбрать PNG позы / эмоции"));
            Assert.That(source, Does.Contain("По умолчанию / Базовая"),
                "Keep Previous and explicit base pose must remain distinct choices.");
        }


        [Test]
        public void Polish_B_SceneEntryOrdersPlaqueThenCharactersThenDialogueText()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            scene.dialogueBeats[0].speaker = "Mina";
            scene.dialogueBeats[0].text = "Scene entry";
            project.scenes.Add(scene);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentFrame.ShowDialoguePanel, Is.True);
                Assert.That(playback.CurrentFrame.ShowCharacters, Is.False);
                Assert.That(playback.CurrentFrame.ShowDialogueText, Is.False);

                playback.Advance(.09f);
                Assert.That(playback.CurrentFrame.ShowDialoguePanel, Is.True);
                Assert.That(playback.CurrentFrame.ShowCharacters, Is.True);
                Assert.That(playback.CurrentFrame.ShowDialogueText, Is.False);

                playback.Advance(.08f);
                Assert.That(playback.CurrentFrame.ShowDialoguePanel, Is.True);
                Assert.That(playback.CurrentFrame.ShowCharacters, Is.True);
                Assert.That(playback.CurrentFrame.ShowDialogueText, Is.True);
            }
        }

        [Test]
        public void Polish_B_VideoEntryWaitsForRequestedVisibleFrameBeforePlaque()
        {
            var factory = new PrewarmFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            try
            {
                var project = new VnSceneComposerProject();
                var scene = new VnSceneComposerScene { media = Video("entry-video.mp4") };
                scene.dialogueBeats[0].text = "Video entry";
                project.scenes.Add(scene);

                using (var playback = new VnSceneComposerPlaybackController(project))
                {
                    playback.PlayScene(0);
                    Assert.That(factory.Created.Count, Is.EqualTo(1));
                    Assert.That(playback.CurrentFrame.ShowDialoguePanel, Is.False);
                    Assert.That(playback.CurrentFrame.ShowCharacters, Is.False);
                    Assert.That(playback.CurrentFrame.ShowDialogueText, Is.False);

                    PrewarmVideoPreview preview = factory.Created[0];
                    preview.Prepared = true;
                    preview.Preparing = false;
                    preview.Visible = true;
                    playback.Advance(.01f);

                    Assert.That(playback.CurrentFrame.ShowDialoguePanel, Is.True);
                    Assert.That(playback.CurrentFrame.ShowCharacters, Is.False);
                    Assert.That(playback.CurrentFrame.ShowDialogueText, Is.False);
                }
            }
            finally
            {
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                foreach (PrewarmVideoPreview preview in factory.Created)
                    preview.Dispose();
            }
        }

        [Test]
        public void Polish_B_OrdinaryBeatChangeDoesNotReplaySceneEntryGates()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            scene.dialogueBeats[0].text = "One";
            scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat { text = "Two" });
            project.scenes.Add(scene);
            VnPresentationWorkshopVn10Resolver.SetTypewriterPreviewOverrides(
                project.defaultPresentation, false, 36f, 0f, .10f, .24f,
                .36f, .20f, .20f, .04f);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                playback.Advance(.20f);
                playback.AdvanceDialogue();

                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(playback.CurrentFrame.ShowDialoguePanel, Is.True);
                Assert.That(playback.CurrentFrame.ShowCharacters, Is.True);
                Assert.That(playback.CurrentFrame.ShowDialogueText, Is.True);
            }
        }

        [Test]
        public void Polish_C_HopIsOneTemporaryVerticalOffsetWithExactReturn()
        {
            Type sampler = typeof(VnSceneComposerPlaybackController).Assembly.GetType(
                "Rokas.EditorTools.VnUiWorkshop.VnSceneComposerElapsedTransitionSampler");
            Assert.That(sampler, Is.Not.Null);
            MethodInfo sampleHop = sampler.GetMethod(
                "SampleHop",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(sampleHop, Is.Not.Null);

            var start = (VnWorkshopActionBounceSample)sampleHop.Invoke(
                null, new object[] { 0f, 50f, 1f });
            var middle = (VnWorkshopActionBounceSample)sampleHop.Invoke(
                null, new object[] { .5f, 50f, 1f });
            var end = (VnWorkshopActionBounceSample)sampleHop.Invoke(
                null, new object[] { 1f, 50f, 1f });

            Assert.That(start.PositionOffset, Is.EqualTo(Vector2.zero));
            Assert.That(middle.PositionOffset.x, Is.EqualTo(0f).Within(.0001f));
            Assert.That(middle.PositionOffset.y, Is.LessThan(0f));
            Assert.That(end.PositionOffset, Is.EqualTo(Vector2.zero));
            Assert.That(start.ScaleMultiplier, Is.EqualTo(1f).Within(.0001f));
            Assert.That(middle.ScaleMultiplier, Is.EqualTo(1f).Within(.0001f));
            Assert.That(end.ScaleMultiplier, Is.EqualTo(1f).Within(.0001f));
            Assert.That(end.Complete, Is.True);
        }

        [Test]
        public void Polish_D_LongTextEditorIsWrappedHeightBoundedAndVerticallyScrolled()
        {
            string source = ReadSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            Assert.That(source, Does.Contain("wordWrap = true"));
            Assert.That(source, Does.Contain("Mathf.Clamp(measuredHeight, minHeight, maxHeight)"));
            Assert.That(source, Does.Contain("needsVerticalScroll"));
            Assert.That(source, Does.Contain("scroll.x = 0f;"));
        }

        [Test]
        public void Polish_E_BothFontSizesStaySequenceGlobalWhilePaletteColorsStayLocal()
        {
            var project = new VnSceneComposerProject();
            project.defaultPresentation.typography.hasSpeakerFontSize = true;
            project.defaultPresentation.typography.speakerFontSize = 55f;
            project.defaultPresentation.typography.hasDialogueFontSize = true;
            project.defaultPresentation.typography.dialogueFontSize = 42f;

            var first = new VnSceneComposerScene();
            first.dialogueBeats[0].speaker = "Mina";
            first.presentationOverrides.typography.hasSpeakerFontSize = true;
            first.presentationOverrides.typography.speakerFontSize = 99f;
            first.dialogueBodyStyleOverride.hasFontSize = true;
            first.dialogueBodyStyleOverride.fontSize = 88f;
            first.speakerColorOverrides.Add(new VnSceneComposerSpeakerColorOverride
            {
                speakerKey = "speaker:Mina",
                color = Color.red,
                hasDialogueBodyColor = true,
                dialogueBodyColor = Color.yellow
            });

            var second = new VnSceneComposerScene();
            second.dialogueBeats[0].speaker = "Keiko";
            second.presentationOverrides.typography.hasSpeakerFontSize = true;
            second.presentationOverrides.typography.speakerFontSize = 77f;
            second.dialogueBodyStyleOverride.hasFontSize = true;
            second.dialogueBodyStyleOverride.fontSize = 66f;
            second.speakerColorOverrides.Add(new VnSceneComposerSpeakerColorOverride
            {
                speakerKey = "speaker:Keiko",
                color = Color.blue,
                hasDialogueBodyColor = true,
                dialogueBodyColor = Color.green
            });

            project.scenes.Add(first);
            project.scenes.Add(second);

            VnWorkshopTypographyValues a =
                VnSceneComposerTextStyleResolver.Resolve(project, first, first.dialogueBeats[0]);
            VnWorkshopTypographyValues b =
                VnSceneComposerTextStyleResolver.Resolve(project, second, second.dialogueBeats[0]);

            Assert.That(a.SpeakerFontSize, Is.EqualTo(55f).Within(.001f));
            Assert.That(b.SpeakerFontSize, Is.EqualTo(55f).Within(.001f));
            Assert.That(a.DialogueFontSize, Is.EqualTo(42f).Within(.001f));
            Assert.That(b.DialogueFontSize, Is.EqualTo(42f).Within(.001f));
            Assert.That(a.SpeakerColor, Is.EqualTo(Color.red));
            Assert.That(a.DialogueColor, Is.EqualTo(Color.yellow));
            Assert.That(b.SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(b.DialogueColor, Is.EqualTo(Color.green));
        }

        [Test]
        public void Polish_F_DirectPngShortcutFeedsExistingM_F1StateSystem()
        {
            string source = ReadSource(
                "VnPresentationWorkshopWindow.SceneComposerPoseImport.cs");
            Assert.That(source, Does.Contain(
                "VnSceneComposerAssetPurpose.CharacterState"));
            Assert.That(source, Does.Contain(
                "ComposerSetSelectedDialogueBeatCharacterState"));
            Assert.That(source, Does.Not.Contain("externalPath")
                .And.Not.Contain("externalSpritePath"));
        }

        private static VnSceneComposerProject Project(VnPresentationWorkshopWindow window)
        {
            FieldInfo field = typeof(VnPresentationWorkshopWindow).GetField(
                "_sceneComposerProject",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            return (VnSceneComposerProject)field.GetValue(window);
        }

        private static VnSceneComposerMediaReference Video(string reference)
        {
            return new VnSceneComposerMediaReference
            {
                kind = VnSceneComposerMediaKind.ExternalVideo,
                reference = reference,
                displayName = reference,
                contentHash = "scene-entry-polish-" + reference,
                scaleMode = VnSceneComposerMediaScaleMode.Fit,
                loop = true
            };
        }

        private static string ReadSource(string file)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop", file));
        }
    }
}
