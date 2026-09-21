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
