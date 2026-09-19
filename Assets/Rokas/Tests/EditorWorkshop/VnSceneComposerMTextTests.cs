using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Rokas.EditorTools.VnUiWorkshop;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerMTextTests
    {
        private const string DefaultTmpPath = "Assets/Rokas/Resources/RokasSans TMP.asset";
        private const string MissingGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Test]
        public void MText_ZeroTextSceneRemainsCompatibleAndDialogueUntouched()
        {
            var project = ProjectWithScene();
            project.scenes[0].dialogueBeats[0].speaker = "Mina";
            project.scenes[0].dialogueBeats[0].text = "Dialogue remains authoritative.";

            string json = VnSceneComposerSerialization.SerializePortable(project);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(json);

            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Project.scenes[0].textElements, Is.Not.Null);
            Assert.That(loaded.Project.scenes[0].textElements, Is.Empty);
            Assert.That(loaded.Project.scenes[0].dialogueBeats[0].text,
                Is.EqualTo("Dialogue remains authoritative."));
        }

        [Test]
        public void MText_MultipleIndependentUnicodeElementsRoundTripAllCoreProperties()
        {
            var project = ProjectWithScene();
            string fontGuid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            Assert.That(fontGuid, Is.Not.Empty);

            project.scenes[0].textElements.Add(new VnSceneComposerTextElement
            {
                textElementId = "11111111111111111111111111111111",
                text = "Санкт-Петербург\n23:40",
                fontAssetGuid = fontGuid,
                fontDisplayName = "RokasSans TMP",
                fontSize = 61f,
                position = new Vector2(321f, 222f),
                size = new Vector2(777f, 155f),
                color = new Color(.2f, .4f, .7f, 1f),
                opacity = .63f,
                alignment = VnSceneComposerTextAlignment.Right,
                visible = true,
                layer = VnSceneComposerTextLayer.FrontCharacters
            });
            project.scenes[0].textElements.Add(new VnSceneComposerTextElement
            {
                textElementId = "22222222222222222222222222222222",
                text = "上海 · 第三章",
                fontAssetGuid = fontGuid,
                fontDisplayName = "RokasSans TMP",
                fontSize = 42f,
                position = new Vector2(1100f, 160f),
                size = new Vector2(520f, 120f),
                color = Color.white,
                opacity = 1f,
                alignment = VnSceneComposerTextAlignment.Center,
                visible = false,
                layer = VnSceneComposerTextLayer.BehindCharacters
            });

            string json = VnSceneComposerSerialization.SerializePortable(project);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(json);

            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Project.scenes[0].textElements, Has.Count.EqualTo(2));
            VnSceneComposerTextElement first = loaded.Project.scenes[0].textElements[0];
            VnSceneComposerTextElement second = loaded.Project.scenes[0].textElements[1];
            Assert.That(first.text, Is.EqualTo("Санкт-Петербург\n23:40"));
            Assert.That(second.text, Is.EqualTo("上海 · 第三章"));
            Assert.That(first.fontAssetGuid, Is.EqualTo(fontGuid));
            Assert.That(first.fontSize, Is.EqualTo(61f).Within(.001f));
            Assert.That(first.position, Is.EqualTo(new Vector2(321f, 222f)));
            Assert.That(first.size, Is.EqualTo(new Vector2(777f, 155f)));
            Assert.That(first.color.r, Is.EqualTo(.2f).Within(.001f));
            Assert.That(first.opacity, Is.EqualTo(.63f).Within(.001f));
            Assert.That(first.alignment, Is.EqualTo(VnSceneComposerTextAlignment.Right));
            Assert.That(second.visible, Is.False);
            Assert.That(second.layer, Is.EqualTo(VnSceneComposerTextLayer.BehindCharacters));
        }

        [Test]
        public void MText_MissingFontIsSafeVisibleWarningNotValidationFailure()
        {
            var project = ProjectWithScene();
            project.scenes[0].textElements.Add(new VnSceneComposerTextElement
            {
                textElementId = "33333333333333333333333333333333",
                text = "Missing font",
                fontAssetGuid = MissingGuid,
                fontDisplayName = "Missing Font"
            });

            bool valid = VnSceneComposerSerialization.ValidateProject(project, out string error, out string[] warnings);

            Assert.That(valid, Is.True, error);
            Assert.That(warnings.Any(w => w.IndexOf("font", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
            Assert.That(frame.ComposerTexts, Is.Empty);
            Assert.That(frame.ComposerTextWarnings, Is.Not.Empty);
        }

        [Test]
        public void MText_DuplicateSceneCopiesTextButRegeneratesIndependentIds()
        {
            var project = ProjectWithScene();
            string fontGuid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            project.scenes[0].textElements.Add(Text("44444444444444444444444444444444", "Original", fontGuid));

            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(project, project.scenes[0].sceneId);

            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.textElements, Has.Count.EqualTo(1));
            Assert.That(copy.textElements[0].text, Is.EqualTo("Original"));
            Assert.That(copy.textElements[0].textElementId,
                Is.Not.EqualTo(project.scenes[0].textElements[0].textElementId));
            copy.textElements[0].text = "Copy";
            Assert.That(project.scenes[0].textElements[0].text, Is.EqualTo("Original"));
        }

        [Test]
        public void MText_WindowCrudEditsIndependentSceneTextWithoutRecreatingPlayback()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerPlayScene();
                object playback = Field(window, "_sceneComposerPlayback");
                string firstId = window.ComposerAddText();
                Assert.That(firstId, Is.Not.Empty);
                Assert.That(Field(window, "_sceneComposerPlayback"), Is.SameAs(playback));

                window.ComposerSetSelectedTextContent("Глава 1\n上海");
                window.ComposerSetSelectedTextPosition(new Vector2(410f, 130f));
                window.ComposerSetSelectedTextSize(new Vector2(900f, 190f));
                window.ComposerSetSelectedTextFontSize(72f);
                window.ComposerSetSelectedTextColor(new Color(.8f, .6f, .4f, 1f));
                window.ComposerSetSelectedTextOpacity(.55f);
                window.ComposerSetSelectedTextAlignment(VnSceneComposerTextAlignment.Right);
                window.ComposerSetSelectedTextLayer(VnSceneComposerTextLayer.BehindCharacters);
                window.ComposerSetSelectedTextVisible(false);

                VnSceneComposerTextElement edited = Texts(Scene(window))[0];
                Assert.That(edited.text, Is.EqualTo("Глава 1\n上海"));
                Assert.That(edited.position, Is.EqualTo(new Vector2(410f, 130f)));
                Assert.That(edited.size, Is.EqualTo(new Vector2(900f, 190f)));
                Assert.That(edited.fontSize, Is.EqualTo(72f));
                Assert.That(edited.opacity, Is.EqualTo(.55f).Within(.001f));
                Assert.That(edited.alignment, Is.EqualTo(VnSceneComposerTextAlignment.Right));
                Assert.That(edited.layer, Is.EqualTo(VnSceneComposerTextLayer.BehindCharacters));
                Assert.That(edited.visible, Is.False);
                Assert.That(Field(window, "_sceneComposerPlayback"), Is.SameAs(playback));

                string secondId = window.ComposerDuplicateSelectedText();
                Assert.That(secondId, Is.Not.EqualTo(firstId));
                Assert.That(Texts(Scene(window)), Has.Count.EqualTo(2));
                Assert.That(window.ComposerDeleteSelectedText(), Is.True);
                Assert.That(Texts(Scene(window)), Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MText_AddEditAndDeleteParticipateInExistingUnityUndo()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                Undo.ClearAll();

                window.ComposerAddText();
                Undo.FlushUndoRecordObjects();
                Assert.That(Texts(Scene(window)), Has.Count.EqualTo(1));
                Undo.PerformUndo();
                Assert.That(Texts(Scene(window)), Is.Empty);

                window.ComposerAddText();
                Undo.FlushUndoRecordObjects();
                Undo.ClearAll();
                string id = window.ComposerGetSelectedTextId();

                window.ComposerSetSelectedTextContent("Undo me");
                Undo.FlushUndoRecordObjects();
                Assert.That(Texts(Scene(window))[0].text, Is.EqualTo("Undo me"));
                Undo.PerformUndo();
                Assert.That(Texts(Scene(window))[0].text, Is.EqualTo("Новый текст"));

                Undo.ClearAll();
                window.ComposerSelectText(id);
                window.ComposerDeleteSelectedText();
                Undo.FlushUndoRecordObjects();
                Assert.That(Texts(Scene(window)), Is.Empty);
                Undo.PerformUndo();
                Assert.That(Texts(Scene(window)), Has.Count.EqualTo(1));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MText_PreviewAndPlaySceneUseSameAuthoritativeTextFrame()
        {
            var project = ProjectWithScene();
            string fontGuid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            project.scenes[0].textElements.Add(Text("55555555555555555555555555555555", "LOCATION", fontGuid));

            VnWorkshopPreviewFrame preview = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
            Assert.That(preview.ComposerTexts, Has.Length.EqualTo(1));

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts, Has.Length.EqualTo(1));
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts[0].TextElementId,
                    Is.EqualTo(preview.ComposerTexts[0].TextElementId));
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts[0].Text,
                    Is.EqualTo(preview.ComposerTexts[0].Text));
            }
        }

        [Test]
        public void MText_DialogueBeatAdvanceKeepsStaticSceneText()
        {
            var project = ProjectWithScene();
            string fontGuid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            project.scenes[0].textElements.Add(Text("66666666666666666666666666666666", "STATIC", fontGuid));
            project.scenes[0].dialogueBeats.Add(new VnSceneComposerDialogueBeat { speaker = "Mina", text = "Second" });

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                string id = playback.CurrentFrame.WorkshopFrame.ComposerTexts[0].TextElementId;
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts, Has.Length.EqualTo(1));
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts[0].TextElementId, Is.EqualTo(id));
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts[0].Text, Is.EqualTo("STATIC"));
            }
        }

        [Test]
        public void MText_SceneTransitionKeepsOutgoingTextUntilCoveredSwapThenUsesIncomingOnly()
        {
            var project = new VnSceneComposerProject();
            string fontGuid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            var outgoing = new VnSceneComposerScene { label = "Outgoing" };
            var incoming = new VnSceneComposerScene { label = "Incoming" };
            outgoing.textElements.Add(Text("77777777777777777777777777777777", "OUT", fontGuid));
            incoming.textElements.Add(Text("88888888888888888888888888888888", "IN", fontGuid));
            incoming.transition.sceneTransitionType = VnSceneComposerSceneTransitionType.DarkCurtain;
            incoming.transition.sceneTransitionDirection = VnSceneComposerSceneTransitionDirection.LeftToRight;
            incoming.transition.sceneTransitionDuration = 1f;
            project.scenes.Add(outgoing);
            project.scenes.Add(incoming);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayFromHere(0);
                playback.Next();
                playback.Advance(.49f);
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts[0].Text, Is.EqualTo("OUT"));

                playback.Advance(.02f);
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts, Has.Length.EqualTo(1));
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts[0].Text, Is.EqualTo("IN"));
                Assert.That(playback.CurrentFrame.WorkshopFrame.ComposerTexts.Any(t => t.Text == "OUT"), Is.False);
                Assert.That(playback.CurrentFrame.SceneTransitionOverlay.Coverage, Is.GreaterThan(.95f));
            }
        }

        [Test]
        public void MText_AuthoringUiExposesAddTextWithoutReplacingDialogueAuthoring()
        {
            string root = Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop");
            string window = File.ReadAllText(Path.Combine(root, "VnPresentationWorkshopWindow.SceneComposer.cs"));
            string textUi = File.ReadAllText(Path.Combine(root, "VnPresentationWorkshopWindow.SceneComposerTextElements.cs"));

            Assert.That(window, Does.Contain("DrawSceneComposerArbitraryTextInspector(scene)")
                .And.Contain("\"Реплики\""));
            Assert.That(textUi, Does.Contain("\"+ Добавить текст\"")
                .And.Contain("\"Текст сцены\"")
                .And.Contain("TextArea"));
        }

        [Test]
        public void MText_DefaultFontIsProjectTmpAssetAndNoWindowsFontDependencyIsIntroduced()
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(DefaultTmpPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.GetType().Name, Is.EqualTo("TMP_FontAsset"));

            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerAddText();
                VnSceneComposerTextElement element = Texts(Scene(window))[0];
                Assert.That(element.fontAssetGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(DefaultTmpPath)));
                Assert.That(element.fontAssetGuid, Is.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static VnSceneComposerProject ProjectWithScene()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(new VnSceneComposerScene());
            return project;
        }

        private static VnSceneComposerTextElement Text(string id, string value, string fontGuid)
        {
            return new VnSceneComposerTextElement
            {
                textElementId = id,
                text = value,
                fontAssetGuid = fontGuid,
                fontDisplayName = "RokasSans TMP",
                fontSize = 48f,
                position = new Vector2(960f, 360f),
                size = new Vector2(720f, 160f),
                color = Color.white,
                opacity = 1f,
                alignment = VnSceneComposerTextAlignment.Center,
                visible = true,
                layer = VnSceneComposerTextLayer.FrontCharacters
            };
        }

        private static VnSceneComposerScene Scene(VnPresentationWorkshopWindow window)
        {
            object project = Field(window, "_sceneComposerProject");
            IList scenes = (IList)project.GetType().GetField("scenes").GetValue(project);
            return (VnSceneComposerScene)scenes[0];
        }

        private static System.Collections.Generic.List<VnSceneComposerTextElement> Texts(VnSceneComposerScene scene)
        {
            return scene.textElements;
        }

        private static object Field(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + name);
            return field.GetValue(instance);
        }
    }
}
