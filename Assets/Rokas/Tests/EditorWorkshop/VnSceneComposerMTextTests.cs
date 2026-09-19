using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
            Assert.That(window, Does.Contain(""Говорящий"").And.Contain(""Текст реплики""));
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
