using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerMTextStyleLayoutTests
    {
        [Test]
        public void MTextStyleLayout_RED_PerCharacterSpeakerStyleApiExists()
        {
            MethodInfo method = typeof(VnPresentationWorkshopWindow).GetMethod(
                "ComposerSetCharacterSpeakerStyle",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null,
                "Speaker style is currently shared globally; a stable-character keyed style mutation path is required.");
        }

        [Test]
        public void MTextStyleLayout_RED_SceneBodyStyleApiExists()
        {
            MethodInfo method = typeof(VnPresentationWorkshopWindow).GetMethod(
                "ComposerSetSelectedSceneDialogueBodyStyle",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null,
                "Dialogue body style needs a Scene-local visual override instead of sharing all typography globally.");
        }

        [Test]
        public void MTextStyleLayout_RED_SpeakerGeometryDoesNotDependOnBaseSpeakerScene()
        {
            var project = new VnSceneComposerProject();
            var keiko = Scene("Keiko");
            var mina = Scene("Mina");
            project.scenes.Add(keiko);
            project.scenes.Add(mina);

            VnWorkshopPreviewFrame a = VnSceneComposerComposition.BuildFrame(
                project, keiko, keiko.dialogueBeats[0],
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
            VnWorkshopPreviewFrame b = VnSceneComposerComposition.BuildFrame(
                project, mina, mina.dialogueBeats[0],
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);

            Assert.That(b.SpeakerName, Is.EqualTo(a.SpeakerName),
                "The same shared authored geometry must not be rebuilt from a speaker-specific plaque.");
            Assert.That(b.DialogueText, Is.EqualTo(a.DialogueText),
                "Dialogue geometry must be Scene/speaker independent.");
        }

        [Test]
        public void MTextStyleLayout_RED_AuthoredTextRectIsNotSilentlyClampedToPanel()
        {
            Rect panel = VnPresentationWorkshopPreviewRenderer.GetReferenceTextRect(
                VnWorkshopElement.DialoguePanel);
            Rect authored = new Rect(
                panel.xMin + 40f,
                panel.yMax + 25f,
                Mathf.Min(300f, panel.width),
                60f);

            Rect resolved = VnPresentationWorkshopPreviewRenderer.ConstrainTextRectToPanel(authored, panel);

            Assert.That(resolved, Is.EqualTo(authored),
                "The current panel clamp creates the reported hidden top barrier/jump.");
        }

        [Test]
        public void MTextStyleLayout_RED_ColorAndGeometryCommitPathsAreSeparated()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposerTextElements.cs"));

            Assert.That(source, Does.Contain("ComposerSetCharacterSpeakerStyle")
                .And.Contain("ComposerSetSelectedSceneDialogueBodyStyle")
                .And.Contain("ComposerSetSharedTextGeometry"));
            Assert.That(source, Does.Not.Contain("ApplyTypographyValues("),
                "A single style+geometry mutation path lets ColorField events commit stale X/Y/W/H.");
        }

        private static VnSceneComposerScene Scene(string speaker)
        {
            var scene = new VnSceneComposerScene();
            scene.dialogueBeats.Clear();
            scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat
            {
                speaker = speaker,
                text = "Text",
                narration = false
            });
            return scene;
        }
    }
}
