using System;
using System.Collections;
using System.Collections.Generic;
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
        private const string DefaultSourcePath = "Assets/Rokas/Art/UI/Fonts/RokasSans.ttf";
        private const string MissingGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private readonly List<string> _createdAssets = new List<string>();

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                string path = _createdAssets[i];
                if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadMainAssetAtPath(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }
            _createdAssets.Clear();
            AssetDatabase.Refresh();
        }

        [Test]
        public void MText_OldProjectResolvesToRokasSansAndDefaultDialogueLayout()
        {
            var project = ProjectWithScene();
            VnWorkshopTypographyValues values =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(project.defaultPresentation);

            Assert.That(values.DialogueFontAssetGuid, Is.Empty);
            Assert.That(values.SpeakerFontAssetGuid, Is.Empty);
            Assert.That(values.DialogueFontSize, Is.EqualTo(22f).Within(.001f));
            Assert.That(values.SpeakerFontSize, Is.EqualTo(26f).Within(.001f));
            Assert.That(values.DialogueColor, Is.EqualTo(Color.white));
            Assert.That(values.SpeakerColor, Is.EqualTo(Color.white));

            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
            Assert.That(frame.DialogueFont, Is.Not.Null);
            Assert.That(frame.SpeakerFont, Is.Not.Null);
        }

        [Test]
        public void MText_SpeakerFontSelectionPersists()
        {
            var project = ProjectWithScene();
            string guid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            project.defaultPresentation.typography.hasSpeakerFontAssetGuid = true;
            project.defaultPresentation.typography.speakerFontAssetGuid = guid;

            VnSceneComposerProject loaded = RoundTrip(project);
            Assert.That(loaded.defaultPresentation.typography.speakerFontAssetGuid, Is.EqualTo(guid));
        }

        [Test]
        public void MText_DialogueFontSelectionPersists()
        {
            var project = ProjectWithScene();
            string guid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            project.defaultPresentation.typography.hasDialogueFontAssetGuid = true;
            project.defaultPresentation.typography.dialogueFontAssetGuid = guid;

            VnSceneComposerProject loaded = RoundTrip(project);
            Assert.That(loaded.defaultPresentation.typography.dialogueFontAssetGuid, Is.EqualTo(guid));
        }

        [Test]
        public void MText_SpeakerAndDialogueMayUseDifferentFonts()
        {
            VnSceneComposerFontImportResult imported = ImportTestFont("MText Dialogue Face");
            var project = ProjectWithScene();
            string defaultGuid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            project.defaultPresentation.typography.hasSpeakerFontAssetGuid = true;
            project.defaultPresentation.typography.speakerFontAssetGuid = defaultGuid;
            project.defaultPresentation.typography.hasDialogueFontAssetGuid = true;
            project.defaultPresentation.typography.dialogueFontAssetGuid = imported.TmpFontAssetGuid;

            VnWorkshopTypographyValues values =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(project.defaultPresentation);
            Assert.That(values.SpeakerFontAssetGuid, Is.EqualTo(defaultGuid));
            Assert.That(values.DialogueFontAssetGuid, Is.EqualTo(imported.TmpFontAssetGuid));
            Assert.That(values.DialogueFontAssetGuid, Is.Not.EqualTo(values.SpeakerFontAssetGuid));
        }

        [Test]
        public void MText_FontImportCreatesProjectOwnedSourceAndTmpAsset()
        {
            VnSceneComposerFontImportResult imported = ImportTestFont("MText Import Face");

            Assert.That(imported.Success, Is.True, imported.Error);
            Assert.That(imported.SourceAssetPath, Does.StartWith("Assets/"));
            Assert.That(imported.TmpFontAssetPath, Does.StartWith("Assets/"));
            Assert.That(imported.TmpFontAssetGuid, Has.Length.EqualTo(32));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(imported.SourceAssetPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(imported.TmpFontAssetPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(imported.TmpFontAssetPath).GetType().Name,
                Is.EqualTo("TMP_FontAsset"));
        }

        [Test]
        public void MText_RuntimeDataNeverStoresMachineLocalFontPath()
        {
            VnSceneComposerFontImportResult imported = ImportTestFont("MText Runtime Face");
            var project = ProjectWithScene();
            project.defaultPresentation.typography.hasDialogueFontAssetGuid = true;
            project.defaultPresentation.typography.dialogueFontAssetGuid = imported.TmpFontAssetGuid;

            string json = VnSceneComposerSerialization.SerializePortable(project);

            Assert.That(json, Does.Contain(imported.TmpFontAssetGuid));
            Assert.That(json, Does.Not.Contain("C:\\Windows\\Fonts"));
            Assert.That(json, Does.Not.Contain(Path.GetFullPath(
                Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultSourcePath))));
        }

        [Test]
        public void MText_SelectingSameFontAgainReusesProjectAssets()
        {
            VnSceneComposerFontImportResult first = ImportTestFont("MText Reuse Face");
            VnSceneComposerFontImportResult second =
                VnSceneComposerTextFontResolver.ImportProjectFont(DefaultSourcePath, "MText Reuse Face");

            Assert.That(second.Success, Is.True, second.Error);
            Assert.That(second.SourceAssetPath, Is.EqualTo(first.SourceAssetPath));
            Assert.That(second.TmpFontAssetPath, Is.EqualTo(first.TmpFontAssetPath));
            Assert.That(second.TmpFontAssetGuid, Is.EqualTo(first.TmpFontAssetGuid));
        }

        [Test]
        public void MText_DifferentFontVariantIdentityRemainsDistinct()
        {
            string regular = VnSceneComposerTextFontResolver.BuildStableIdentity(
                "Golos Text Regular", "0123456789abcdef");
            string semibold = VnSceneComposerTextFontResolver.BuildStableIdentity(
                "Golos Text SemiBold", "fedcba9876543210");

            Assert.That(regular, Is.Not.EqualTo(semibold));
            Assert.That(regular, Does.Contain("golos text regular"));
            Assert.That(semibold, Does.Contain("golos text semibold"));
        }

        [Test]
        public void MText_SpeakerPositionIsSharedAcrossDialogueBeats()
        {
            var project = ProjectWithTwoBeats();
            project.defaultPresentation.speakerName.hasPositionDelta = true;
            project.defaultPresentation.speakerName.positionDelta = new Vector2(47f, -21f);

            VnWorkshopPreviewFrame first = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], project.scenes[0].dialogueBeats[0],
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
            VnWorkshopPreviewFrame second = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], project.scenes[0].dialogueBeats[1],
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);

            Assert.That(second.SpeakerName, Is.EqualTo(first.SpeakerName));
        }

        [Test]
        public void MText_DialoguePositionIsSharedAcrossDialogueBeats()
        {
            var project = ProjectWithTwoBeats();
            project.defaultPresentation.dialogueText.hasPositionDelta = true;
            project.defaultPresentation.dialogueText.positionDelta = new Vector2(-36f, 18f);
            project.defaultPresentation.dialogueText.hasSizeDelta = true;
            project.defaultPresentation.dialogueText.sizeDelta = new Vector2(140f, 30f);

            VnWorkshopPreviewFrame first = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], project.scenes[0].dialogueBeats[0],
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
            VnWorkshopPreviewFrame second = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], project.scenes[0].dialogueBeats[1],
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);

            Assert.That(second.DialogueText, Is.EqualTo(first.DialogueText));
        }

        [Test]
        public void MText_BeatNavigationDoesNotResetTypography()
        {
            var project = ProjectWithTwoBeats();
            project.defaultPresentation.typography.hasDialogueFontSize = true;
            project.defaultPresentation.typography.dialogueFontSize = 39f;
            project.defaultPresentation.typography.hasSpeakerFontSize = true;
            project.defaultPresentation.typography.speakerFontSize = 31f;

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                VnWorkshopTypographyValues before = playback.CurrentFrame.WorkshopFrame.Typography;
                playback.AdvanceDialogue();
                VnWorkshopTypographyValues after = playback.CurrentFrame.WorkshopFrame.Typography;
                Assert.That(after.DialogueFontSize, Is.EqualTo(before.DialogueFontSize));
                Assert.That(after.SpeakerFontSize, Is.EqualTo(before.SpeakerFontSize));
            }
        }

        [Test]
        public void MText_ContentChangesDoNotResetTypography()
        {
            var project = ProjectWithScene();
            project.defaultPresentation.typography.hasDialogueFontSize = true;
            project.defaultPresentation.typography.dialogueFontSize = 44f;
            project.scenes[0].dialogueBeats[0].text = "До";
            project.scenes[0].dialogueBeats[0].text = "После";

            VnWorkshopTypographyValues values =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(project.defaultPresentation);
            Assert.That(values.DialogueFontSize, Is.EqualTo(44f));
        }

        [Test]
        public void MText_CustomPlaqueChangeDoesNotResetTypography()
        {
            var project = ProjectWithScene();
            project.defaultPresentation.typography.hasSpeakerFontSize = true;
            project.defaultPresentation.typography.speakerFontSize = 34f;
            project.defaultPresentation.dialoguePanelVisual.hasAssetGuid = true;
            project.defaultPresentation.dialoguePanelVisual.assetGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            Assert.That(
                VnPresentationWorkshopVn10Resolver.ResolveTypography(project.defaultPresentation).SpeakerFontSize,
                Is.EqualTo(34f));
        }

        [Test]
        public void MText_SaveReopenPreservesTypographyAndLayout()
        {
            var project = ProjectWithScene();
            string guid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            project.defaultPresentation.typography.hasDialogueFontAssetGuid = true;
            project.defaultPresentation.typography.dialogueFontAssetGuid = guid;
            project.defaultPresentation.typography.hasDialogueFontSize = true;
            project.defaultPresentation.typography.dialogueFontSize = 37f;
            project.defaultPresentation.typography.hasDialogueColor = true;
            project.defaultPresentation.typography.dialogueColor = new Color(.2f, .7f, .9f, .65f);
            project.defaultPresentation.dialogueText.hasPositionDelta = true;
            project.defaultPresentation.dialogueText.positionDelta = new Vector2(25f, -14f);

            VnSceneComposerProject loaded = RoundTrip(project);

            Assert.That(loaded.defaultPresentation.typography.dialogueFontAssetGuid, Is.EqualTo(guid));
            Assert.That(loaded.defaultPresentation.typography.dialogueFontSize, Is.EqualTo(37f));
            Assert.That(loaded.defaultPresentation.typography.dialogueColor.a, Is.EqualTo(.65f).Within(.001f));
            Assert.That(loaded.defaultPresentation.dialogueText.positionDelta, Is.EqualTo(new Vector2(25f, -14f)));
        }

        [Test]
        public void MText_UndoRestoresSharedTypographyLayout()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                Undo.ClearAll();
                window.ComposerSetSharedDialogueTypography(
                    string.Empty, 48f, new Color(.7f, .4f, .2f, .8f),
                    VnWorkshopTextAlignment.Center, new Vector2(33f, 12f), new Vector2(80f, 20f));
                Undo.FlushUndoRecordObjects();

                Assert.That(Project(window).defaultPresentation.typography.dialogueFontSize, Is.EqualTo(48f));
                Undo.PerformUndo();

                VnWorkshopTypographyValues restored =
                    VnPresentationWorkshopVn10Resolver.ResolveTypography(Project(window).defaultPresentation);
                Assert.That(restored.DialogueFontSize, Is.EqualTo(22f).Within(.001f));
                Assert.That(Project(window).defaultPresentation.dialogueText.hasPositionDelta, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MText_PreviewAndPlaySceneResolveIdenticalTypography()
        {
            var project = ProjectWithScene();
            string guid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            project.defaultPresentation.typography.hasSpeakerFontAssetGuid = true;
            project.defaultPresentation.typography.speakerFontAssetGuid = guid;
            project.defaultPresentation.typography.hasDialogueFontAssetGuid = true;
            project.defaultPresentation.typography.dialogueFontAssetGuid = guid;
            project.defaultPresentation.typography.hasDialogueFontSize = true;
            project.defaultPresentation.typography.dialogueFontSize = 41f;
            project.defaultPresentation.typography.hasDialogueAlignment = true;
            project.defaultPresentation.typography.dialogueAlignment = VnWorkshopTextAlignment.Center;
            project.defaultPresentation.dialogueText.hasPositionDelta = true;
            project.defaultPresentation.dialogueText.positionDelta = new Vector2(20f, 11f);

            VnWorkshopPreviewFrame preview = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                VnWorkshopPreviewFrame play = playback.CurrentFrame.WorkshopFrame;
                Assert.That(play.DialogueFont.name, Is.EqualTo(preview.DialogueFont.name));
                Assert.That(play.SpeakerFont.name, Is.EqualTo(preview.SpeakerFont.name));
                Assert.That(play.Typography.DialogueFontSize, Is.EqualTo(preview.Typography.DialogueFontSize));
                Assert.That(play.Typography.DialogueAlignment, Is.EqualTo(preview.Typography.DialogueAlignment));
                Assert.That(play.DialogueText, Is.EqualTo(preview.DialogueText));
            }
        }

        [Test]
        public void MText_MissingProjectFontWarnsButDialogueRemainsVisibleAndReferenceSurvives()
        {
            var project = ProjectWithScene();
            project.defaultPresentation.typography.hasDialogueFontAssetGuid = true;
            project.defaultPresentation.typography.dialogueFontAssetGuid = MissingGuid;

            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
            Assert.That(frame.DialogueFont, Is.Not.Null);
            Assert.That(frame.Dialogue, Is.EqualTo("Первая реплика"));
            Assert.That(project.defaultPresentation.typography.dialogueFontAssetGuid, Is.EqualTo(MissingGuid));

            bool valid = VnSceneComposerSerialization.ValidateProject(project, out string error, out string[] warnings);
            Assert.That(valid, Is.True, error);
            Assert.That(warnings, Has.Some.Contains("font"));
        }

        [Test]
        public void MText_ExistingRokasSansTmpPathStillResolves()
        {
            string guid = AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            Assert.That(guid, Is.Not.Empty);
            Assert.That(VnSceneComposerTextFontResolver.TryResolvePreviewFont(
                guid, out Font font, out string warning), Is.True, warning);
            Assert.That(font, Is.Not.Null);
        }

        [Test]
        public void MText_TransitionRevealUsesAuthoredTypographyWithoutDefaultFlash()
        {
            VnSceneComposerFontImportResult imported = ImportTestFont("MText Transition Face");
            var project = new VnSceneComposerProject();
            project.defaultPresentation.typography.hasDialogueFontAssetGuid = true;
            project.defaultPresentation.typography.dialogueFontAssetGuid = imported.TmpFontAssetGuid;
            project.defaultPresentation.typography.hasDialogueFontSize = true;
            project.defaultPresentation.typography.dialogueFontSize = 43f;

            var first = new VnSceneComposerScene { label = "First" };
            first.dialogueBeats[0].text = "One";
            var second = new VnSceneComposerScene { label = "Second" };
            second.dialogueBeats[0].text = "Two";
            second.transition.sceneTransitionType = VnSceneComposerSceneTransitionType.DarkCurtain;
            second.transition.sceneTransitionDuration = 1f;
            project.scenes.Add(first);
            project.scenes.Add(second);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayFromHere(0);
                playback.Next();
                playback.Advance(.51f);
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(playback.CurrentFrame.WorkshopFrame.Typography.DialogueFontAssetGuid,
                    Is.EqualTo(imported.TmpFontAssetGuid));
                Assert.That(playback.CurrentFrame.WorkshopFrame.Typography.DialogueFontSize, Is.EqualTo(43f));
            }
        }

        [Test]
        public void MText_DuplicateSceneUsesSameSharedTypographyProfile()
        {
            var project = ProjectWithScene();
            project.defaultPresentation.typography.hasSpeakerFontSize = true;
            project.defaultPresentation.typography.speakerFontSize = 33f;
            project.defaultPresentation.speakerName.hasPositionDelta = true;
            project.defaultPresentation.speakerName.positionDelta = new Vector2(17f, 9f);

            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(project, project.scenes[0].sceneId);
            VnWorkshopPreviewFrame originalFrame = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
            VnWorkshopPreviewFrame copyFrame = VnSceneComposerComposition.BuildFrame(
                project, copy, VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);

            Assert.That(copyFrame.Typography.SpeakerFontSize, Is.EqualTo(originalFrame.Typography.SpeakerFontSize));
            Assert.That(copyFrame.SpeakerName, Is.EqualTo(originalFrame.SpeakerName));
        }

        [Test]
        public void MText_AuthoringUiUsesExistingSpeakerAndDialogueInsteadOfStandaloneTextObjects()
        {
            string root = Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop");
            string window = File.ReadAllText(Path.Combine(root, "VnPresentationWorkshopWindow.SceneComposer.cs"));
            string typographyUi = File.ReadAllText(
                Path.Combine(root, "VnPresentationWorkshopWindow.SceneComposerTextElements.cs"));

            Assert.That(window, Does.Not.Contain("DrawSceneComposerArbitraryTextInspector(scene)"));
            Assert.That(typographyUi, Does.Not.Contain("+ Добавить текст"));
            Assert.That(window, Does.Contain("\"Говорящий\"").And.Contain("\"Текст реплики\""));
            Assert.That(typographyUi, Does.Contain("Изменить текст говорящего")
                .And.Contain("Изменить текст реплики")
                .And.Contain("GetInstalledWindowsFonts"));
        }

        [Test]
        public void MText_TmpFallbackCanBeConfiguredWithoutDuplicateEntries()
        {
            VnSceneComposerFontImportResult primary = ImportTestFont("MText Primary Face");
            VnSceneComposerFontImportResult fallback = ImportTestFont("MText Fallback Face");

            Assert.That(VnSceneComposerTextFontResolver.ConfigureFallback(
                primary.TmpFontAssetGuid, fallback.TmpFontAssetGuid, out string error), Is.True, error);
            Assert.That(VnSceneComposerTextFontResolver.ConfigureFallback(
                primary.TmpFontAssetGuid, fallback.TmpFontAssetGuid, out error), Is.True, error);

            string description = VnSceneComposerTextFontResolver.DescribeFallbacks(
                VnSceneComposerTextFontResolver.ResolveAsset(primary.TmpFontAssetGuid));
            Assert.That(description, Does.Contain("1"));
        }

        [Test]
        public void MText_InstalledFontEnumerationIsSafeAndVariantAware()
        {
            VnSceneComposerInstalledFontFace[] faces = VnSceneComposerTextFontResolver.GetInstalledWindowsFonts();
            Assert.That(faces, Is.Not.Null);
#if UNITY_EDITOR_WIN
            Assert.That(faces, Is.Not.Empty);
            Assert.That(faces, Has.All.Matches<VnSceneComposerInstalledFontFace>(
                f => f != null && !string.IsNullOrWhiteSpace(f.DisplayName) &&
                     (f.SourcePath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                      f.SourcePath.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))));
#endif
        }


        [Test]
        public void MText_ShortDialogueRemainsSingleWrappedLineInsideAuthoredRect()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                SetDialogueRect(window, new Rect(260f, 30f, 820f, 160f), 28f);
                VnSceneComposerProject project = Project(window);
                project.scenes[0].dialogueBeats[0].text = "Короткая реплика.";

                VnWorkshopPreviewFrame frame = Frame(project, 0);
                float oneLine = MeasureWrappedHeight(frame, "A");
                float actual = MeasureWrappedHeight(frame, frame.Dialogue);

                Assert.That(frame.DialogueText.width, Is.EqualTo(820f).Within(.01f));
                Assert.That(actual, Is.EqualTo(oneLine).Within(.5f));
                Assert.That(actual, Is.LessThanOrEqualTo(frame.DialogueText.height));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MText_LongDialogueWrapsWithinAuthoredWidth()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                SetDialogueRect(window, new Rect(260f, 30f, 820f, 160f), 28f);
                VnSceneComposerProject project = Project(window);
                project.scenes[0].dialogueBeats[0].text =
                    "Это длинная реплика, которая должна естественно переноситься по словам на несколько строк " +
                    "и при этом никогда не расширять прямоугольник текста вправо за пределы заданной ширины.";

                VnWorkshopPreviewFrame frame = Frame(project, 0);
                float oneLine = MeasureWrappedHeight(frame, "A");
                float wrapped = MeasureWrappedHeight(frame, frame.Dialogue);

                Assert.That(frame.DialogueText.width, Is.EqualTo(820f).Within(.01f));
                Assert.That(wrapped, Is.GreaterThan(oneLine + .5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MText_DialogueRectWidthDoesNotChangeWhenTextGetsLonger()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                SetDialogueRect(window, new Rect(275f, 25f, 780f, 170f), 30f);
                VnSceneComposerProject project = Project(window);

                project.scenes[0].dialogueBeats[0].text = "Коротко.";
                Rect shortRect = Frame(project, 0).DialogueText;
                project.scenes[0].dialogueBeats[0].text = new string('Д', 220) +
                    " длинная фраза с дополнительными словами для проверки ограничения.";
                Rect longRect = Frame(project, 0).DialogueText;

                Assert.That(longRect.width, Is.EqualTo(shortRect.width).Within(.001f));
                Assert.That(longRect.height, Is.EqualTo(shortRect.height).Within(.001f));
                Assert.That(longRect.width, Is.EqualTo(780f).Within(.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MText_VeryLongDialogueNeverIncreasesHorizontalBounds()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                SetDialogueRect(window, new Rect(300f, 20f, 760f, 180f), 32f);
                VnSceneComposerProject project = Project(window);
                project.scenes[0].dialogueBeats[0].text =
                    string.Join(" ", new string[120].Select((_, i) => "слово" + i));

                VnWorkshopPreviewFrame frame = Frame(project, 0);

                Assert.That(frame.DialogueText.width, Is.EqualTo(760f).Within(.01f));
                Assert.That(frame.DialogueText.xMin, Is.GreaterThanOrEqualTo(frame.DialoguePanel.xMin - .01f));
                Assert.That(frame.DialogueText.xMax, Is.LessThanOrEqualTo(frame.DialoguePanel.xMax + .01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MText_BeatNavigationDoesNotMutateDialogueRect()
        {
            VnSceneComposerProject project = ProjectWithTwoBeats();
            SetDialogueRect(project, new Rect(255f, 35f, 800f, 150f));

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Rect first = playback.CurrentFrame.WorkshopFrame.DialogueText;
                playback.AdvanceDialogue();
                Rect second = playback.CurrentFrame.WorkshopFrame.DialogueText;

                Assert.That(second, Is.EqualTo(first));
            }
        }

        [Test]
        public void MText_SaveReopenPreservesResolvedDialogueRect()
        {
            VnSceneComposerProject project = ProjectWithScene();
            Rect expected = new Rect(245f, 28f, 810f, 165f);
            SetDialogueRect(project, expected);

            VnSceneComposerProject loaded = RoundTrip(project);
            Rect actual = Frame(loaded, 0).DialogueText;

            AssertRect(actual, expected);
        }

        [Test]
        public void MText_SharedLayoutUsesSameDialogueRectAcrossBeats()
        {
            VnSceneComposerProject project = ProjectWithTwoBeats();
            Rect expected = new Rect(280f, 22f, 790f, 175f);
            SetDialogueRect(project, expected);
            project.scenes[0].dialogueBeats[0].text = "Short.";
            project.scenes[0].dialogueBeats[1].text =
                "A much longer dialogue body that must use precisely the same shared rectangle on the next beat.";

            Rect first = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], project.scenes[0].dialogueBeats[0],
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture).DialogueText;
            Rect second = VnSceneComposerComposition.BuildFrame(
                project, project.scenes[0], project.scenes[0].dialogueBeats[1],
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture).DialogueText;

            AssertRect(first, expected);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void MText_CustomPlaqueChangeDoesNotResetDialogueRect()
        {
            VnSceneComposerProject project = ProjectWithScene();
            Rect expected = new Rect(270f, 25f, 805f, 160f);
            SetDialogueRect(project, expected);
            Rect before = Frame(project, 0).DialogueText;

            project.defaultPresentation.dialoguePanelVisual.hasAssetGuid = true;
            project.defaultPresentation.dialoguePanelVisual.assetGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            Rect after = Frame(project, 0).DialogueText;

            Assert.That(after, Is.EqualTo(before));
            AssertRect(after, expected);
        }

        [Test]
        public void MText_PreviewAndPlayResolveIdenticalDialogueBounds()
        {
            VnSceneComposerProject project = ProjectWithScene();
            Rect expected = new Rect(265f, 32f, 815f, 155f);
            SetDialogueRect(project, expected);
            VnWorkshopPreviewFrame preview = Frame(project, 0);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentFrame.WorkshopFrame.DialogueText, Is.EqualTo(preview.DialogueText));
                AssertRect(preview.DialogueText, expected);
            }
        }

        [Test]
        public void MText_ChangingFontPreservesAuthoredDialogueRect()
        {
            VnSceneComposerProject project = ProjectWithScene();
            Rect expected = new Rect(250f, 24f, 825f, 168f);
            SetDialogueRect(project, expected);
            Rect before = Frame(project, 0).DialogueText;

            project.defaultPresentation.typography.hasDialogueFontAssetGuid = true;
            project.defaultPresentation.typography.dialogueFontAssetGuid =
                AssetDatabase.AssetPathToGUID(DefaultTmpPath);
            Rect after = Frame(project, 0).DialogueText;

            Assert.That(after, Is.EqualTo(before));
            AssertRect(after, expected);
        }

        [Test]
        public void MText_LargeFontLongDialogueRemainsHorizontallyBounded()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                SetDialogueRect(window, new Rect(290f, 18f, 740f, 190f), 72f);
                VnSceneComposerProject project = Project(window);
                project.scenes[0].dialogueBeats[0].text =
                    "Очень крупный текст всё равно обязан переноситься внутри заданной ширины, " +
                    "а не расширять контейнер вправо.";

                VnWorkshopPreviewFrame frame = Frame(project, 0);

                Assert.That(frame.DialogueText.width, Is.EqualTo(740f).Within(.01f));
                Assert.That(frame.DialogueText.xMax, Is.LessThanOrEqualTo(frame.DialoguePanel.xMax + .01f));
                Assert.That(MeasureWrappedHeight(frame, frame.Dialogue), Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MText_ExistingShortDialogueDefaultRectRemainsCompatible()
        {
            VnSceneComposerProject project = ProjectWithScene();
            VnWorkshopPreviewFrame before = Frame(project, 0);
            Rect defaultRect = before.DialogueText;

            project.scenes[0].dialogueBeats[0].text = "OK";
            VnWorkshopPreviewFrame after = Frame(project, 0);

            Assert.That(after.DialogueText, Is.EqualTo(defaultRect));
            Assert.That(after.DialogueText.width, Is.GreaterThan(1f));
            Assert.That(after.DialogueText.height, Is.GreaterThan(1f));
        }

        private static VnWorkshopPreviewFrame Frame(VnSceneComposerProject project, int sceneIndex)
        {
            return VnSceneComposerComposition.BuildFrame(
                project, project.scenes[sceneIndex],
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
        }

        private static void SetDialogueRect(
            VnPresentationWorkshopWindow window, Rect rect, float fontSize)
        {
            window.ComposerSetSharedDialogueTypography(
                string.Empty, fontSize, Color.white, VnWorkshopTextAlignment.Left,
                rect.position, rect.size);
        }

        private static void SetDialogueRect(VnSceneComposerProject project, Rect rect)
        {
            Rect baseline = Frame(new VnSceneComposerProject
            {
                scenes = { new VnSceneComposerScene() }
            }, 0).DialogueText;
            VnWorkshopElementOverride layout = project.defaultPresentation.dialogueText;
            layout.hasPositionDelta = rect.center != baseline.center;
            layout.positionDelta = rect.center - baseline.center;
            layout.hasSizeDelta = rect.size != baseline.size;
            layout.sizeDelta = rect.size - baseline.size;
        }

        private static float MeasureWrappedHeight(VnWorkshopPreviewFrame frame, string text)
        {
            MethodInfo method = typeof(VnPresentationWorkshopPreviewRenderer).GetMethod(
                "MeasureWrappedTextHeight",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                "Renderer must expose its actual bounded wrapping measurement helper.");
            return (float)method.Invoke(null, new object[]
            {
                frame.DialogueText, text, frame.DialogueFont,
                Mathf.RoundToInt(frame.Typography.DialogueFontSize), FontStyle.Normal,
                VnWorkshopTextAlignment.Left
            });
        }

        private static void AssertRect(Rect actual, Rect expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(.01f));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(.01f));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(.01f));
        }


        [Test]
        public void MText_AuthoringDialogueEditorUsesAdaptiveWrappedScrollableMultilineControl()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposer.cs"));

            Assert.That(source, Does.Contain("DrawSceneComposerDialogueBodyEditor"));
            Assert.That(source, Does.Contain("wordWrap = true"));
            Assert.That(source, Does.Contain("EditorGUILayout.BeginScrollView"));
            Assert.That(source, Does.Contain("SceneComposerDialogueEditorMinVisibleLines"));
            Assert.That(source, Does.Contain("SceneComposerDialogueEditorMaxVisibleLines"));
            Assert.That(source, Does.Not.Contain(
                "EditorGUILayout.TextArea(selectedBeat.text ?? string.Empty, GUILayout.MinHeight(70f))"));
        }

        [Test]
        public void MText_LongDialogueAuthoringRoundTripPreservesCompleteUnicodeContent()
        {
            VnSceneComposerProject project = ProjectWithScene();
            string longText = string.Join(" ", Enumerable.Range(0, 180)
                .Select(i => "длинная_реплика_" + i)) +
                "\nКонец большого русского текста — 中文 — Unicode ✓.";
            project.scenes[0].dialogueBeats[0].text = longText;

            VnSceneComposerProject loaded = RoundTrip(project);

            Assert.That(loaded.scenes[0].dialogueBeats[0].text, Is.EqualTo(longText));
            Assert.That(loaded.scenes[0].dialogueBeats[0].text.Length, Is.EqualTo(longText.Length));
        }

        [Test]
        public void MText_ManualLineBreaksInDialogueSurviveRoundTrip()
        {
            VnSceneComposerProject project = ProjectWithScene();
            const string text =
                "Первая строка.\nВторая строка после ручного переноса.\n\nЧетвертая строка после пустой.";
            project.scenes[0].dialogueBeats[0].text = text;

            VnSceneComposerProject loaded = RoundTrip(project);

            Assert.That(loaded.scenes[0].dialogueBeats[0].text, Is.EqualTo(text));
            Assert.That(loaded.scenes[0].dialogueBeats[0].text.Count(c => c == '\n'), Is.EqualTo(3));
        }

        [Test]
        public void MText_SwitchingBetweenShortAndLongBeatsDoesNotTruncateDialogue()
        {
            VnPresentationWorkshopWindow window =
                ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                VnSceneComposerProject project = Project(window);
                VnSceneComposerScene scene = project.scenes[0];
                scene.dialogueBeats.Clear();

                string longTwo = string.Join(" ", Enumerable.Range(0, 120)
                    .Select(i => "второй_" + i));
                string longFour = string.Join(" ", Enumerable.Range(0, 160)
                    .Select(i => "четвертый_" + i));

                var beat1 = new VnSceneComposerDialogueBeat { text = "Коротко." };
                var beat2 = new VnSceneComposerDialogueBeat { text = longTwo };
                var beat3 = new VnSceneComposerDialogueBeat { text = "Средняя реплика из нескольких слов." };
                var beat4 = new VnSceneComposerDialogueBeat { text = longFour };
                scene.dialogueBeats.Add(beat1);
                scene.dialogueBeats.Add(beat2);
                scene.dialogueBeats.Add(beat3);
                scene.dialogueBeats.Add(beat4);

                window.ComposerSelectDialogueBeat(beat1.beatId);
                window.ComposerSelectDialogueBeat(beat2.beatId);
                window.ComposerSelectDialogueBeat(beat3.beatId);
                window.ComposerSelectDialogueBeat(beat4.beatId);
                window.ComposerSelectDialogueBeat(beat2.beatId);

                Assert.That(scene.dialogueBeats[1].text, Is.EqualTo(longTwo));
                Assert.That(scene.dialogueBeats[3].text, Is.EqualTo(longFour));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MText_AuthoringTextareaStateDoesNotAffectSharedTypographyOrRuntimeRect()
        {
            VnSceneComposerProject project = ProjectWithScene();
            Rect expectedRect = new Rect(270f, 26f, 800f, 164f);
            SetDialogueRect(project, expectedRect);
            project.defaultPresentation.typography.hasDialogueFontSize = true;
            project.defaultPresentation.typography.dialogueFontSize = 38f;
            VnWorkshopPreviewFrame before = Frame(project, 0);

            project.scenes[0].dialogueBeats[0].text = string.Join(" ",
                Enumerable.Range(0, 150).Select(i => "редактор_" + i));
            VnWorkshopPreviewFrame after = Frame(project, 0);

            Assert.That(after.DialogueText, Is.EqualTo(before.DialogueText));
            Assert.That(after.Typography.DialogueFontSize, Is.EqualTo(38f));
            AssertRect(after.DialogueText, expectedRect);
        }

        [Test]
        public void MText_ShortDialogueEditingContractRemainsPresent()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposer.cs"));

            Assert.That(source, Does.Contain("EditorGUILayout.LabelField(\"Текст реплики\")"));
            Assert.That(source, Does.Contain("selectedBeat.text"));
            Assert.That(source, Does.Contain("Edit VN Dialogue Beat"));
        }

        [Test]
        public void MTextReveal_RED_01_FullAuthoredTextExistsBeforeProgressiveVisibility()
        {
            VnWorkshopTypewriterValues values = TypewriterValues(10f);
            object sample = SampleRevealReflect("Это длинная строка", 0f, values, false);
            Assert.That(RevealString(sample, "FullText"), Is.EqualTo("Это длинная строка"));
            Assert.That(RevealInt(sample, "VisibleGlyphCount"), Is.EqualTo(0));
            Assert.That(RevealBool(sample, "Complete"), Is.False);
        }

        [Test]
        public void MTextReveal_RED_02_VisibleGlyphCountIsMonotonic()
        {
            VnWorkshopTypewriterValues values = TypewriterValues(12f);
            int a = RevealInt(SampleRevealReflect("abcdef", .10f, values, false), "VisibleGlyphCount");
            int b = RevealInt(SampleRevealReflect("abcdef", .30f, values, false), "VisibleGlyphCount");
            int c = RevealInt(SampleRevealReflect("abcdef", .60f, values, false), "VisibleGlyphCount");
            Assert.That(b, Is.GreaterThanOrEqualTo(a));
            Assert.That(c, Is.GreaterThanOrEqualTo(b));
        }

        [Test]
        public void MTextReveal_RED_03_FinalStateShowsEveryVisibleGlyph()
        {
            VnWorkshopTypewriterValues values = TypewriterValues(20f);
            object sample = SampleRevealReflect("Привет 世界", 10f, values, false);
            Assert.That(RevealBool(sample, "Complete"), Is.True);
            Assert.That(RevealInt(sample, "VisibleGlyphCount"),
                Is.EqualTo(RevealInt(sample, "TotalGlyphCount")));
            Assert.That(RevealString(sample, "PlainVisibleText"), Is.EqualTo("Привет 世界"));
        }

        [Test]
        public void MTextReveal_RED_04_SpeedControlsRevealDuration()
        {
            object slow = SampleRevealReflect("abcdefghij", 0f, TypewriterValues(5f), false);
            object fast = SampleRevealReflect("abcdefghij", 0f, TypewriterValues(40f), false);
            Assert.That(RevealFloat(slow, "DurationSeconds"),
                Is.GreaterThan(RevealFloat(fast, "DurationSeconds")));
        }

        [Test]
        public void MTextReveal_RED_05_ElapsedSamplingIsFrameRateIndependent()
        {
            VnWorkshopTypewriterValues values = TypewriterValues(17f);
            object atThirty = SampleRevealReflect("frame independent", 30f / 60f, values, false);
            object atOneTwenty = SampleRevealReflect("frame independent", 120f / 240f, values, false);
            Assert.That(RevealInt(atThirty, "VisibleGlyphCount"),
                Is.EqualTo(RevealInt(atOneTwenty, "VisibleGlyphCount")));
            Assert.That(RevealFloat(atThirty, "NewestGlyphAlpha"),
                Is.EqualTo(RevealFloat(atOneTwenty, "NewestGlyphAlpha")).Within(.001f));
        }

        [Test]
        public void MTextReveal_RED_06_NewlyExposedGlyphHasShortSmoothAlphaRamp()
        {
            VnWorkshopTypewriterValues values = TypewriterValues(10f);
            object sample = SampleRevealReflect("AB", .12f, values, false);
            float alpha = RevealFloat(sample, "NewestGlyphAlpha");
            Assert.That(RevealInt(sample, "VisibleGlyphCount"), Is.EqualTo(1));
            Assert.That(alpha, Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void MTextReveal_RED_07_RichTextMarkupDoesNotCountAsVisibleGlyphs()
        {
            VnWorkshopTypewriterValues values = TypewriterValues(20f);
            object sample = SampleRevealReflect("<b>A</b><color=#ff0000>B</color>", 10f, values, false);
            Assert.That(RevealInt(sample, "TotalGlyphCount"), Is.EqualTo(2));
            Assert.That(RevealString(sample, "PlainVisibleText"), Is.EqualTo("AB"));
            Assert.That(RevealString(sample, "RenderText"), Does.Contain("<b>").And.Contain("<color=#ff0000>"));
        }

        [Test]
        public void MTextReveal_RED_08_CyrillicUsesVisibleTextElements()
        {
            object sample = SampleRevealReflect("Привет", 10f, TypewriterValues(20f), false);
            Assert.That(RevealInt(sample, "TotalGlyphCount"), Is.EqualTo(6));
            Assert.That(RevealString(sample, "PlainVisibleText"), Is.EqualTo("Привет"));
        }

        [Test]
        public void MTextReveal_RED_09_ChineseUsesVisibleGlyphs()
        {
            object sample = SampleRevealReflect("你好世界", 10f, TypewriterValues(20f), false);
            Assert.That(RevealInt(sample, "TotalGlyphCount"), Is.EqualTo(4));
            Assert.That(RevealString(sample, "PlainVisibleText"), Is.EqualTo("你好世界"));
        }

        [Test]
        public void MTextReveal_RED_10_SurrogateAndCombiningSequencesAreNotSplit()
        {
            const string text = "A\U0001F642e\u0301";
            object sample = SampleRevealReflect(text, 10f, TypewriterValues(20f), false);
            Assert.That(RevealInt(sample, "TotalGlyphCount"), Is.EqualTo(3));
            Assert.That(RevealString(sample, "PlainVisibleText"), Is.EqualTo(text));
        }

        [Test]
        public void MTextReveal_RED_11_LongDialogueKeepsFinalRectStableDuringReveal()
        {
            VnSceneComposerProject project = ProjectWithScene();
            project.scenes[0].dialogueBeats[0].text =
                string.Join(" ", Enumerable.Range(0, 90).Select(i => "слово" + i));
            Rect expected = new Rect(250f, 35f, 820f, 180f);
            SetDialogueRect(project, expected);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Rect before = playback.CurrentFrame.WorkshopFrame.DialogueText;
                playback.Advance(.15f);
                Rect during = playback.CurrentFrame.WorkshopFrame.DialogueText;
                playback.Advance(10f);
                Rect after = playback.CurrentFrame.WorkshopFrame.DialogueText;
                Assert.That(during, Is.EqualTo(before));
                Assert.That(after, Is.EqualTo(before));
            }
        }

        [Test]
        public void MTextReveal_RED_12_FirstNextWhileRevealingCompletesCurrentBeatOnly()
        {
            VnSceneComposerProject project = ProjectWithTwoBeats();
            EnableTypewriter(project, 4f);
            project.scenes[0].dialogueBeats[0].text = "Очень длинная первая реплика";

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(0));
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(0),
                    "First Next while revealing must complete the current line, not advance Beat.");
                Assert.That(playback.CurrentFrame.Dialogue,
                    Is.EqualTo(project.scenes[0].dialogueBeats[0].text));
            }
        }

        [Test]
        public void MTextReveal_RED_13_SecondNextAfterForcedCompletionAdvancesBeat()
        {
            VnSceneComposerProject project = ProjectWithTwoBeats();
            EnableTypewriter(project, 4f);
            project.scenes[0].dialogueBeats[0].text = "Очень длинная первая реплика";

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                playback.AdvanceDialogue();
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(1));
            }
        }

        [Test]
        public void MTextReveal_RED_14_NewBeatCancelsForcedCompletionState()
        {
            VnSceneComposerProject project = ProjectWithTwoBeats();
            EnableTypewriter(project, 5f);
            project.scenes[0].dialogueBeats[0].text = "Первая длинная реплика";
            project.scenes[0].dialogueBeats[1].text = "Вторая длинная реплика";

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                playback.AdvanceDialogue();
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(playback.CurrentFrame.Dialogue,
                    Is.Not.EqualTo(project.scenes[0].dialogueBeats[1].text),
                    "New Beat must start a fresh reveal instead of inheriting force-complete state.");
            }
        }

        [Test]
        public void MTextReveal_RED_15_RestartAndPlayFromHereStartFreshReveal()
        {
            VnSceneComposerProject project = ProjectWithTwoBeats();
            EnableTypewriter(project, 5f);
            project.scenes[0].dialogueBeats[0].text = "Первая длинная реплика";

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentFrame.Dialogue,
                    Is.EqualTo(project.scenes[0].dialogueBeats[0].text));
                playback.Restart();
                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(0));
                Assert.That(playback.CurrentFrame.Dialogue,
                    Is.Not.EqualTo(project.scenes[0].dialogueBeats[0].text));
                playback.PlayFromHere(0);
                Assert.That(playback.CurrentFrame.Dialogue,
                    Is.Not.EqualTo(project.scenes[0].dialogueBeats[0].text));
            }
        }

        [Test]
        public void MTextReveal_RED_16_PreviousStartsPreviousBeatRevealCleanly()
        {
            VnSceneComposerProject project = ProjectWithTwoBeats();
            EnableTypewriter(project, 5f);
            project.scenes[0].dialogueBeats[0].text = "Первая длинная реплика";
            project.scenes[0].dialogueBeats[1].text = "Вторая длинная реплика";

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                playback.AdvanceDialogue();
                playback.AdvanceDialogue();
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(1));
                playback.PreviousDialogue();
                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(0));
                Assert.That(playback.CurrentFrame.Dialogue,
                    Is.Not.EqualTo(project.scenes[0].dialogueBeats[0].text));
            }
        }

        [Test]
        public void MTextReveal_RED_17_RevealNeverMutatesDialogueGeometryOrPlaque()
        {
            VnSceneComposerProject project = ProjectWithScene();
            EnableTypewriter(project, 4f);
            project.scenes[0].dialogueBeats[0].text = "Геометрия должна оставаться полностью неподвижной.";
            Rect expected = new Rect(260f, 30f, 800f, 170f);
            SetDialogueRect(project, expected);
            VnWorkshopPreviewFrame authored = Frame(project, 0);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Rect plaque = playback.CurrentFrame.WorkshopFrame.DialoguePanel;
                playback.Advance(.5f);
                Assert.That(playback.CurrentFrame.WorkshopFrame.DialogueText, Is.EqualTo(authored.DialogueText));
                Assert.That(playback.CurrentFrame.WorkshopFrame.DialoguePanel, Is.EqualTo(plaque));
            }
        }

        [Test]
        public void MTextReveal_RED_18_SpeakerNameIsImmediateWhileDialogueBodyReveals()
        {
            VnSceneComposerProject project = ProjectWithScene();
            EnableTypewriter(project, 4f);
            project.scenes[0].dialogueBeats[0].speaker = "Mina";
            project.scenes[0].dialogueBeats[0].text = "Медленно раскрываемый текст";

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentFrame.WorkshopFrame.Speaker, Is.EqualTo("Mina"));
                Assert.That(playback.CurrentFrame.Dialogue,
                    Is.Not.EqualTo(project.scenes[0].dialogueBeats[0].text));
            }
        }

        [Test]
        public void MTextReveal_19_PlaybackRendererReceivesFullLayoutTextBeforeVisibility()
        {
            VnSceneComposerProject project = ProjectWithScene();
            EnableTypewriter(project, 4f);
            const string authored = "Полный текст уже участвует в раскладке";
            project.scenes[0].dialogueBeats[0].text = authored;

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentFrame.Dialogue, Is.Empty,
                    "Compatibility visible text should still begin hidden.");
                string renderText = playback.CurrentFrame.WorkshopFrame.Dialogue;
                Assert.That(renderText, Is.Not.Empty);
                Assert.That(renderText, Does.Contain("<color=#FFFFFF00>"),
                    "Renderer-facing text must keep hidden glyphs in the layout from frame zero.");
                Assert.That(renderText.Length, Is.GreaterThan(authored.Length),
                    "Hidden alpha markup must coexist with the full authored layout text.");
                Assert.That(renderText, Does.Contain("П").And.Contain("е"),
                    "The renderer-facing string must still contain authored glyphs while hidden.");
            }
        }

        private static VnWorkshopTypewriterValues TypewriterValues(float cps)
        {
            return new VnWorkshopTypewriterValues
            {
                Enabled = true,
                CharactersPerSecond = cps,
                BaseCharacterDelay = 0f,
                CommaPause = 0f,
                PeriodPause = 0f,
                EllipsisPause = 0f,
                QuestionPause = 0f,
                ExclamationPause = 0f,
                LineStartDelay = 0f
            };
        }

        private static void EnableTypewriter(VnSceneComposerProject project, float cps)
        {
            VnPresentationWorkshopVn10Resolver.SetTypewriterPreviewOverrides(
                project.defaultPresentation, true, cps, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        }

        private static object SampleRevealReflect(
            string text, float elapsed, VnWorkshopTypewriterValues values, bool forceComplete)
        {
            Type type = typeof(VnSceneComposerTransitionSampler).Assembly.GetType(
                "Rokas.EditorTools.VnUiWorkshop.VnSceneComposerDialogueReveal");
            Assert.That(type, Is.Not.Null,
                "Smooth reveal requires one layout-stable reveal sampler.");
            MethodInfo method = type.GetMethod(
                "Sample", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new object[] { text, elapsed, values, forceComplete });
        }

        private static object RevealMember(object sample, string name)
        {
            Assert.That(sample, Is.Not.Null);
            Type type = sample.GetType();
            PropertyInfo property = type.GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return property.GetValue(sample, null);
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing reveal member: " + name);
            return field.GetValue(sample);
        }

        private static string RevealString(object sample, string name)
        {
            return (string)RevealMember(sample, name);
        }

        private static int RevealInt(object sample, string name)
        {
            return Convert.ToInt32(RevealMember(sample, name));
        }

        private static float RevealFloat(object sample, string name)
        {
            return Convert.ToSingle(RevealMember(sample, name));
        }

        private static bool RevealBool(object sample, string name)
        {
            return Convert.ToBoolean(RevealMember(sample, name));
        }


        [Test]
        public void MTextSpeakerPalette_RED_ProfileContractOwnsTwoIndependentColors()
        {
            Type entryType = typeof(VnSceneComposerSpeakerColorOverride);
            Assert.That(entryType.GetField("dialogueBodyColor"), Is.Not.Null,
                "A Scene speaker palette entry must own a dialogue-body color as well as the speaker-name color.");
            Assert.That(entryType.GetField("hasDialogueBodyColor"), Is.Not.Null,
                "Legacy one-color entries need an explicit presence bit so body color can fall back safely.");
        }

        [Test]
        public void MTextSpeakerPalette_RED_SelectedMinaBodyColorDoesNotBleedIntoKeiko()
        {
            VnPresentationWorkshopWindow window =
                ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                VnSceneComposerProject project = Project(window);
                VnSceneComposerScene scene = project.scenes[0];
                scene.dialogueBeats.Clear();
                var keiko = new VnSceneComposerDialogueBeat
                {
                    speaker = "Keiko",
                    text = "Keiko line"
                };
                var mina = new VnSceneComposerDialogueBeat
                {
                    speaker = "Mina",
                    text = "Mina line"
                };
                scene.dialogueBeats.Add(keiko);
                scene.dialogueBeats.Add(mina);

                VnWorkshopTypographyValues minaBefore =
                    VnSceneComposerTextStyleResolver.Resolve(project, scene, mina);
                window.ComposerSelectDialogueBeat(mina.beatId);
                window.ComposerSetCurrentSceneSpeakerDialogueColor(
                    new Color(1f, .45f, .1f, minaBefore.DialogueColor.a));

                VnWorkshopTypographyValues keikoAfter =
                    VnSceneComposerTextStyleResolver.Resolve(project, scene, keiko);
                VnWorkshopTypographyValues minaAfter =
                    VnSceneComposerTextStyleResolver.Resolve(project, scene, mina);

                Assert.That(keikoAfter.DialogueColor.r, Is.EqualTo(1f).Within(.001f));
                Assert.That(keikoAfter.DialogueColor.g, Is.EqualTo(1f).Within(.001f),
                    "Editing Mina's current-speaker body color must not recolor Keiko.");
                Assert.That(minaAfter.DialogueColor.g, Is.EqualTo(.45f).Within(.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private VnSceneComposerFontImportResult ImportTestFont(string displayName)
        {
            VnSceneComposerFontImportResult result =
                VnSceneComposerTextFontResolver.ImportProjectFont(DefaultSourcePath, displayName);
            Assert.That(result.Success, Is.True, result.Error);
            Remember(result.SourceAssetPath);
            Remember(result.TmpFontAssetPath);
            return result;
        }

        private void Remember(string path)
        {
            if (!string.IsNullOrEmpty(path) && !_createdAssets.Contains(path))
                _createdAssets.Add(path);
        }

        private static VnSceneComposerProject ProjectWithScene()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            scene.dialogueBeats[0].speaker = "KEIKO";
            scene.dialogueBeats[0].text = "Первая реплика";
            project.scenes.Add(scene);
            return project;
        }

        private static VnSceneComposerProject ProjectWithTwoBeats()
        {
            VnSceneComposerProject project = ProjectWithScene();
            project.scenes[0].dialogueBeats.Add(new VnSceneComposerDialogueBeat
            {
                speaker = "MINA",
                text = "Вторая реплика"
            });
            return project;
        }

        private static VnSceneComposerProject RoundTrip(VnSceneComposerProject project)
        {
            string json = VnSceneComposerSerialization.SerializePortable(project);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(json);
            Assert.That(loaded.Success, Is.True, loaded.Error);
            return loaded.Project;
        }

        private static VnSceneComposerProject Project(VnPresentationWorkshopWindow window)
        {
            return (VnSceneComposerProject)Field(window, "_sceneComposerProject");
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
