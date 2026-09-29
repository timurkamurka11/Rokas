using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerProjectSpeakerColorTests
    {
        private const string Keiko = "Keiko";
        private const string Mina = "Mina";

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
        }

        [Test] public void GlobalSpeakerColors_01_ProjectOwnsPalette()
        {
            Assert.That(typeof(VnSceneComposerProject).GetField(
                "projectSpeakerPalette", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(new VnSceneComposerProject().projectSpeakerPalette, Is.Not.Null);
        }

        [Test] public void GlobalSpeakerColors_02_OneGlobalProfilePerSpeaker()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            string key = Key(p.scenes[0], 0);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, key, Color.red);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerDialogueBodyColor(p, key, Color.yellow);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, key, Color.blue);
            Assert.That(p.projectSpeakerPalette.Count, Is.EqualTo(1));
            Assert.That(p.projectSpeakerPalette[0].speakerKey, Is.EqualTo(key));
        }

        [Test] public void GlobalSpeakerColors_03_KeikoSharedAcrossScenes()
        {
            VnSceneComposerProject p = Project(Scene(Keiko), Scene(Keiko), Scene(Keiko));
            SetGlobal(p, p.scenes[0], 0, Color.white, Color.gray);
            for (int i = 0; i < p.scenes.Count; i++)
            {
                Assert.That(Resolve(p, p.scenes[i], 0).SpeakerColor, Is.EqualTo(Color.white));
                Assert.That(Resolve(p, p.scenes[i], 0).DialogueColor, Is.EqualTo(Color.gray));
            }
        }

        [Test] public void GlobalSpeakerColors_04_MinaSharedAcrossScenes()
        {
            VnSceneComposerProject p = Project(Scene(Mina), Scene(Mina));
            Color orange = new Color(1f, .45f, .1f, 1f);
            Color pale = new Color(1f, .72f, .45f, 1f);
            SetGlobal(p, p.scenes[0], 0, orange, pale);
            Assert.That(Resolve(p, p.scenes[1], 0).SpeakerColor, Is.EqualTo(orange));
            Assert.That(Resolve(p, p.scenes[1], 0).DialogueColor, Is.EqualTo(pale));
        }

        [Test] public void GlobalSpeakerColors_05_KeikoNameIndependentFromMinaName()
        {
            VnSceneComposerProject p = Project(Scene(Keiko), Scene(Mina));
            VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, Key(p.scenes[0], 0), Color.blue);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, Key(p.scenes[1], 0), Color.red);
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(Resolve(p, p.scenes[1], 0).SpeakerColor, Is.EqualTo(Color.red));
        }

        [Test] public void GlobalSpeakerColors_06_KeikoBodyIndependentFromMinaBody()
        {
            VnSceneComposerProject p = Project(Scene(Keiko), Scene(Mina));
            VnSceneComposerTextStyleResolver.SetProjectSpeakerDialogueBodyColor(p, Key(p.scenes[0], 0), Color.green);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerDialogueBodyColor(p, Key(p.scenes[1], 0), Color.yellow);
            Assert.That(Resolve(p, p.scenes[0], 0).DialogueColor, Is.EqualTo(Color.green));
            Assert.That(Resolve(p, p.scenes[1], 0).DialogueColor, Is.EqualTo(Color.yellow));
        }

        [Test] public void GlobalSpeakerColors_07_NameAndBodyIndependentForSameSpeaker()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            string key = Key(p.scenes[0], 0);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, key, Color.blue);
            Color beforeBody = Resolve(p, p.scenes[0], 0).DialogueColor;
            VnSceneComposerTextStyleResolver.SetProjectSpeakerDialogueBodyColor(p, key, Color.green);
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(Resolve(p, p.scenes[0], 0).DialogueColor, Is.EqualTo(Color.green));
            Assert.That(beforeBody, Is.Not.EqualTo(Color.green));
        }

        [Test] public void GlobalSpeakerColors_08_SpeakerButtonApiEditsNameColor()
        {
            VnPresentationWorkshopWindow w = WindowWithSpeaker(Keiko);
            try
            {
                w.ComposerSetProjectSpeakerNameColor("speaker:Keiko", Color.cyan);
                Assert.That(Resolve(Project(w), Project(w).scenes[0], 0).SpeakerColor, Is.EqualTo(Color.cyan));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void GlobalSpeakerColors_09_DialogueButtonApiEditsBodyColor()
        {
            VnPresentationWorkshopWindow w = WindowWithSpeaker(Keiko);
            try
            {
                w.ComposerSetProjectSpeakerDialogueColor("speaker:Keiko", Color.magenta);
                Assert.That(Resolve(Project(w), Project(w).scenes[0], 0).DialogueColor, Is.EqualTo(Color.magenta));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void GlobalSpeakerColors_10_BothButtonsTargetSameProfile()
        {
            VnPresentationWorkshopWindow w = WindowWithSpeaker(Keiko);
            try
            {
                w.ComposerSetProjectSpeakerNameColor("speaker:Keiko", Color.blue);
                w.ComposerSetProjectSpeakerDialogueColor("speaker:Keiko", Color.green);
                Assert.That(Project(w).projectSpeakerPalette.Count, Is.EqualTo(1));
                Assert.That(Project(w).projectSpeakerPalette[0].hasSpeakerNameColor, Is.True);
                Assert.That(Project(w).projectSpeakerPalette[0].hasDialogueBodyColor, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void GlobalSpeakerColors_11_SelectedBeatAutoFillIsWired()
        {
            string source = TextUiSource();
            string selector = ExtractMethodBody(source,
                "private void SyncProjectSpeakerColorSelection(VnSceneComposerScene scene)");
            Assert.That(selector, Does.Contain("ComposerGetSelectedDialogueBeat()")
                .And.Contain("ResolveSpeakerKey(scene, beat)")
                .And.Contain("_sceneComposerColorProfileName = ResolveBeatSpeakerDisplayName(beat)"));
        }

        [Test] public void GlobalSpeakerColors_12_KnownSpeakerDropdownScansWholeProject()
        {
            string source = TextUiSource();
            string body = ExtractMethodBody(source,
                "private void BuildProjectSpeakerChoices(");
            Assert.That(body, Does.Contain("_sceneComposerProject.scenes")
                .And.Contain("scene.dialogueBeats")
                .And.Contain("ResolveSpeakerKey(scene, beat)"));
            Assert.That(source, Does.Contain("\"Известные в проекте\""));
        }

        [Test] public void GlobalSpeakerColors_13_ManualDialogueOnlyNameCreatesTextProfile()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            string key = VnSceneComposerTextStyleResolver.ResolveSpeakerKeyForName("  Miko  ");
            Assert.That(key, Is.EqualTo("speaker:Miko"));
            VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, key, Color.red);
            Assert.That(p.projectSpeakerPalette.Count, Is.EqualTo(1));
            Assert.That(p.projectSpeakerPalette[0].speakerKey, Is.EqualTo("speaker:Miko"));
        }

        [Test] public void GlobalSpeakerColors_14_TextOnlySpeakerNeedsNoCharacter()
        {
            VnSceneComposerProject p = Project(Scene(Mina));
            Assert.That(p.scenes[0].characters, Is.Empty);
            SetGlobal(p, p.scenes[0], 0, Color.red, Color.yellow);
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.red));
            Assert.That(Resolve(p, p.scenes[0], 0).DialogueColor, Is.EqualTo(Color.yellow));
        }

        [Test] public void GlobalSpeakerColors_15_NoSceneColorScopeInNormalInspector()
        {
            string body = ExtractMethodBody(TextUiSource(),
                "private void DrawSceneComposerDialogueTypographyInspector(VnSceneComposerScene scene)");
            Assert.That(body, Does.Not.Contain("speakerColorScope")
                .And.Not.Contain("ComposerSetSelectedSceneSpeakerColorScope"));
        }

        [Test] public void GlobalSpeakerColors_16_NoPermanentSpeakerColorsPanel()
        {
            string body = ExtractMethodBody(TextUiSource(),
                "private void DrawSceneComposerDialogueTypographyInspector(VnSceneComposerScene scene)");
            Assert.That(body, Does.Not.Contain("DrawCurrentSceneSpeakerPaletteControls(scene)")
                .And.Not.Contain("\"Цвета говорящего в этой сцене\""));
        }

        [Test] public void GlobalSpeakerColors_17_NoDuplicateColorFieldsInMainDialogueStyling()
        {
            string body = ExtractMethodBody(TextUiSource(),
                "private void DrawSceneComposerDialogueTypographyInspector(VnSceneComposerScene scene)");
            Assert.That(body, Does.Not.Contain("ColorField"));
            Assert.That(body, Does.Contain("\"Изменить текст говорящего\"")
                .And.Contain("\"Изменить текст реплики\""));
        }

        [Test] public void GlobalSpeakerColors_18_SpeakerEditorHasExactlySemanticNameColor()
        {
            string body = ExtractMethodBody(TextUiSource(),
                "private void DrawSpeakerTypographyControls(VnSceneComposerScene scene)");
            Assert.That(body, Does.Contain("\"Говорящий\"").And.Contain("\"Цвет имени\"").And.Contain("\"Сбросить цвет\""));
            Assert.That(body, Does.Not.Contain("\"Общий цвет имени\""));
        }

        [Test] public void GlobalSpeakerColors_19_DialogueEditorHasExactlySemanticBodyColor()
        {
            string body = ExtractMethodBody(TextUiSource(),
                "private void DrawScopedTypographyControls(bool speaker, VnSceneComposerScene scene)");
            Assert.That(body, Does.Contain("\"Говорящий\"").And.Contain("\"Цвет текста реплики\"").And.Contain("\"Сбросить цвет\""));
            Assert.That(body, Does.Not.Contain("\"Цвет реплики по умолчанию\""));
        }

        [Test] public void GlobalSpeakerColors_20_ChangingKeikoInSceneOneUpdatesSceneTwo()
        {
            VnSceneComposerProject p = Project(Scene(Keiko), Scene(Keiko));
            VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, Key(p.scenes[0], 0), Color.blue);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerDialogueBodyColor(p, Key(p.scenes[0], 0), Color.green);
            Assert.That(Resolve(p, p.scenes[1], 0).SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(Resolve(p, p.scenes[1], 0).DialogueColor, Is.EqualTo(Color.green));
        }

        [Test] public void GlobalSpeakerColors_21_MinaRemainsUnchangedWhenKeikoChanges()
        {
            VnSceneComposerProject p = Project(Scene(Keiko), Scene(Mina));
            VnWorkshopTypographyValues before = Resolve(p, p.scenes[1], 0);
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            VnWorkshopTypographyValues after = Resolve(p, p.scenes[1], 0);
            Assert.That(after.SpeakerColor, Is.EqualTo(before.SpeakerColor));
            Assert.That(after.DialogueColor, Is.EqualTo(before.DialogueColor));
        }

        [Test] public void GlobalSpeakerColors_22_GlobalFontSizesRemainSeparate()
        {
            VnSceneComposerProject p = Project(Scene(Keiko), Scene(Mina));
            p.defaultPresentation.typography.hasSpeakerFontSize = true;
            p.defaultPresentation.typography.speakerFontSize = 41f;
            p.defaultPresentation.typography.hasDialogueFontSize = true;
            p.defaultPresentation.typography.dialogueFontSize = 33f;
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            SetGlobal(p, p.scenes[1], 0, Color.red, Color.yellow);
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerFontSize, Is.EqualTo(41f));
            Assert.That(Resolve(p, p.scenes[1], 0).SpeakerFontSize, Is.EqualTo(41f));
            Assert.That(Resolve(p, p.scenes[0], 0).DialogueFontSize, Is.EqualTo(33f));
            Assert.That(Resolve(p, p.scenes[1], 0).DialogueFontSize, Is.EqualTo(33f));
            Assert.That(typeof(VnSceneComposerProjectSpeakerProfile).GetField("fontSize"), Is.Null);
        }

        [Test] public void GlobalSpeakerColors_23_TextGeometryUnchanged()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            string before = JsonUtility.ToJson(p.defaultPresentation.speakerName) +
                            JsonUtility.ToJson(p.defaultPresentation.dialogueText);
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            string after = JsonUtility.ToJson(p.defaultPresentation.speakerName) +
                           JsonUtility.ToJson(p.defaultPresentation.dialogueText);
            Assert.That(after, Is.EqualTo(before));
        }

        [Test] public void GlobalSpeakerColors_24_PlaqueUnchanged()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            string before = JsonUtility.ToJson(p.defaultPresentation.dialoguePanel);
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            Assert.That(JsonUtility.ToJson(p.defaultPresentation.dialoguePanel), Is.EqualTo(before));
        }

        [Test] public void GlobalSpeakerColors_25_CharacterStagingUnchanged()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            p.scenes[0].dialogueBeats[0].characterStaging.Add(new VnSceneComposerBeatCharacterStaging
            {
                characterId = Keiko,
                visibility = VnSceneComposerBeatCharacterVisibility.Show
            });
            string before = JsonUtility.ToJson(p.scenes[0].dialogueBeats[0].characterStaging[0]);
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            Assert.That(JsonUtility.ToJson(p.scenes[0].dialogueBeats[0].characterStaging[0]), Is.EqualTo(before));
        }

        [Test] public void GlobalSpeakerColors_26_PreviewParity()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            VnWorkshopPreviewFrame f = Frame(p, p.scenes[0], p.scenes[0].dialogueBeats[0]);
            Assert.That(f.Typography.SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(f.Typography.DialogueColor, Is.EqualTo(Color.green));
        }

        [Test] public void GlobalSpeakerColors_27_PlaySceneParity()
        {
            VnSceneComposerScene s = Scene(Keiko);
            s.dialogueBeats[0].text = "Keiko";
            s.dialogueBeats.Add(Beat(Mina, "Mina"));
            VnSceneComposerProject p = Project(s);
            SetGlobal(p, s, 0, Color.blue, Color.green);
            SetGlobal(p, s, 1, Color.red, Color.yellow);
            using (var playback = new VnSceneComposerPlaybackController(p))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentFrame.WorkshopFrame.Typography.SpeakerColor, Is.EqualTo(Color.blue));
                playback.AdvanceDialogue();
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentFrame.WorkshopFrame.Typography.DialogueColor, Is.EqualTo(Color.yellow));
            }
        }

        [Test] public void GlobalSpeakerColors_28_PlayAllParity()
        {
            VnSceneComposerScene s = Scene(Keiko);
            s.dialogueBeats[0].text = "Keiko";
            s.dialogueBeats.Add(Beat(Mina, "Mina"));
            VnSceneComposerProject p = Project(s);
            SetGlobal(p, s, 0, Color.blue, Color.green);
            SetGlobal(p, s, 1, Color.red, Color.yellow);
            using (var playback = new VnSceneComposerPlaybackController(p))
            {
                playback.PlayAll();
                playback.AdvanceDialogue();
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(playback.CurrentFrame.WorkshopFrame.Typography.SpeakerColor, Is.EqualTo(Color.red));
            }
        }

        [Test] public void GlobalSpeakerColors_29_PlayFromHereParity()
        {
            VnSceneComposerScene s = Scene(Keiko);
            s.dialogueBeats.Add(Beat(Mina, "Mina"));
            VnSceneComposerProject p = Project(s);
            SetGlobal(p, s, 0, Color.blue, Color.green);
            SetGlobal(p, s, 1, Color.red, Color.yellow);
            using (var playback = new VnSceneComposerPlaybackController(p))
            {
                playback.PlayFromHere(0, 1);
                Assert.That(playback.CurrentFrame.WorkshopFrame.Typography.SpeakerColor, Is.EqualTo(Color.red));
                Assert.That(playback.CurrentFrame.WorkshopFrame.Typography.DialogueColor, Is.EqualTo(Color.yellow));
            }
        }

        [Test] public void GlobalSpeakerColors_30_FirstVisibleGlyphHasCorrectBodyColor()
        {
            VnSceneComposerScene s = Scene(Keiko);
            s.dialogueBeats.Add(Beat(Mina, "Mina"));
            VnSceneComposerProject p = Project(s);
            SetGlobal(p, s, 1, Color.red, Color.yellow);
            VnPresentationWorkshopVn10Resolver.SetTypewriterPreviewOverrides(
                p.defaultPresentation, true, 10f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            using (var playback = new VnSceneComposerPlaybackController(p))
            {
                playback.PlayFromHere(0, 1);
                Assert.That(playback.CurrentFrame.WorkshopFrame.Typography.DialogueColor, Is.EqualTo(Color.yellow));
                playback.Advance(.11f);
                Assert.That(playback.CurrentFrame.DialogueReveal.VisibleGlyphCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(playback.CurrentFrame.WorkshopFrame.Typography.DialogueColor, Is.EqualTo(Color.yellow));
            }
        }

        [Test] public void GlobalSpeakerColors_31_SaveReopenPersistsProjectPalette()
        {
            VnSceneComposerProject p = Project(Scene(Keiko), Scene(Mina));
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            SetGlobal(p, p.scenes[1], 0, Color.red, Color.yellow);
            VnSceneComposerImportResult loaded =
                VnSceneComposerSerialization.DeserializePortable(
                    VnSceneComposerSerialization.SerializePortable(p));
            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Project.projectSpeakerPalette.Count, Is.EqualTo(2));
            Assert.That(Resolve(loaded.Project, loaded.Project.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(Resolve(loaded.Project, loaded.Project.scenes[1], 0).DialogueColor, Is.EqualTo(Color.yellow));
        }

        [Test] public void GlobalSpeakerColors_32_UndoRestoresProjectGlobalValue()
        {
            VnPresentationWorkshopWindow w = WindowWithSpeaker(Keiko);
            try
            {
                w.ComposerSetProjectSpeakerNameColor("speaker:Keiko", Color.white);
                Undo.ClearAll();
                w.ComposerSetProjectSpeakerNameColor("speaker:Keiko", Color.blue);
                Undo.FlushUndoRecordObjects();
                Assert.That(Resolve(Project(w), Project(w).scenes[0], 0).SpeakerColor, Is.EqualTo(Color.blue));
                Undo.PerformUndo();
                Assert.That(Resolve(Project(w), Project(w).scenes[0], 0).SpeakerColor, Is.EqualTo(Color.white));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void GlobalSpeakerColors_33_DuplicateSceneSharesSameGlobalProfile()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(p, p.scenes[0].sceneId);
            Assert.That(p.projectSpeakerPalette.Count, Is.EqualTo(1));
            Assert.That(Resolve(p, copy, 0).SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(Resolve(p, copy, 0).DialogueColor, Is.EqualTo(Color.green));
        }

        [Test] public void GlobalSpeakerColors_34_ResetNameOnlyKeepsBody()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            string key = Key(p.scenes[0], 0);
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            Assert.That(VnSceneComposerTextStyleResolver.ClearProjectSpeakerNameColor(p, key), Is.True);
            VnSceneComposerProjectSpeakerProfile profile =
                VnSceneComposerTextStyleResolver.FindProjectSpeakerProfile(p, key);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.hasSpeakerNameColor, Is.False);
            Assert.That(profile.hasDialogueBodyColor, Is.True);
            Assert.That(Resolve(p, p.scenes[0], 0).DialogueColor, Is.EqualTo(Color.green));
        }

        [Test] public void GlobalSpeakerColors_35_ResetBodyOnlyKeepsName()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            string key = Key(p.scenes[0], 0);
            SetGlobal(p, p.scenes[0], 0, Color.blue, Color.green);
            Assert.That(VnSceneComposerTextStyleResolver.ClearProjectSpeakerDialogueBodyColor(p, key), Is.True);
            VnSceneComposerProjectSpeakerProfile profile =
                VnSceneComposerTextStyleResolver.FindProjectSpeakerProfile(p, key);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.hasSpeakerNameColor, Is.True);
            Assert.That(profile.hasDialogueBodyColor, Is.False);
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.blue));
        }

        [Test] public void GlobalSpeakerColors_36_LegacySceneLocalPaletteMigrates()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            SetLegacy(p.scenes[0], p.scenes[0].dialogueBeats[0], Color.red, Color.yellow);
            VnSceneComposerProject migrated = LoadAsSchemaThree(p);
            Assert.That(migrated.schemaVersion, Is.EqualTo(VnSceneComposerContract.SchemaVersion));
            Assert.That(migrated.projectSpeakerPalette.Count, Is.EqualTo(1));
            Assert.That(Resolve(migrated, migrated.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.red));
            Assert.That(Resolve(migrated, migrated.scenes[0], 0).DialogueColor, Is.EqualTo(Color.yellow));
        }

        [Test] public void GlobalSpeakerColors_37_ConflictingLegacyValuesMigrateDeterministically()
        {
            VnSceneComposerProject p = Project(Scene(Keiko), Scene(Keiko));
            SetLegacy(p.scenes[0], p.scenes[0].dialogueBeats[0], Color.blue, Color.green);
            SetLegacy(p.scenes[1], p.scenes[1].dialogueBeats[0], Color.red, Color.yellow);
            VnSceneComposerProject migrated = LoadAsSchemaThree(p);
            Assert.That(migrated.projectSpeakerPalette.Count, Is.EqualTo(1));
            Assert.That(Resolve(migrated, migrated.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.blue),
                "First authored Scene/Beat occurrence is the documented deterministic conflict winner.");
            Assert.That(Resolve(migrated, migrated.scenes[1], 0).DialogueColor, Is.EqualTo(Color.green));
            Assert.That(migrated.scenes[0].speakerColorOverrides[0].color, Is.EqualTo(Color.blue));
            Assert.That(migrated.scenes[1].speakerColorOverrides[0].color, Is.EqualTo(Color.red),
                "Legacy values remain preserved instead of being destructively rewritten.");
        }

        [Test] public void GlobalSpeakerColors_38_OldProjectOpensWithoutNarrativeOrStagingLoss()
        {
            VnSceneComposerScene s = Scene(Keiko);
            s.characters.Add(new VnSceneComposerCharacter { characterId = Keiko, stateId = "keiko_serious" });
            s.dialogueBeats[0].text = "Preserve this dialogue";
            s.dialogueBeats[0].characterStaging.Add(new VnSceneComposerBeatCharacterStaging
            {
                characterId = Keiko,
                visibility = VnSceneComposerBeatCharacterVisibility.Show,
                stateId = "keiko_serious",
                hasStateOverride = true
            });
            SetLegacy(s, s.dialogueBeats[0], Color.white, Color.gray);
            VnSceneComposerProject migrated = LoadAsSchemaThree(Project(s));
            Assert.That(migrated.scenes.Count, Is.EqualTo(1));
            Assert.That(migrated.scenes[0].dialogueBeats[0].text, Is.EqualTo("Preserve this dialogue"));
            Assert.That(migrated.scenes[0].characters.Count, Is.EqualTo(1));
            Assert.That(migrated.scenes[0].dialogueBeats[0].characterStaging.Count, Is.EqualTo(1));
            Assert.That(migrated.scenes[0].speakerColorOverrides.Count, Is.EqualTo(1));
        }

        [Test] public void GlobalSpeakerColors_39_StableCharacterIdentityWorksAcrossScenes()
        {
            VnSceneComposerScene a = SceneWithCharacter(Keiko);
            VnSceneComposerScene b = SceneWithCharacter(Keiko);
            VnSceneComposerProject p = Project(a, b);
            Assert.That(Key(a, 0), Is.EqualTo("character:Keiko"));
            Assert.That(Key(b, 0), Is.EqualTo("character:Keiko"));
            SetGlobal(p, a, 0, Color.blue, Color.green);
            Assert.That(Resolve(p, b, 0).SpeakerColor, Is.EqualTo(Color.blue));
        }

        [Test] public void GlobalSpeakerColors_40_WhitespaceNormalizationDoesNotCreateDuplicateProfile()
        {
            VnSceneComposerProject p = Project(Scene(Keiko));
            string a = VnSceneComposerTextStyleResolver.ResolveSpeakerKeyForName("Keiko");
            string b = VnSceneComposerTextStyleResolver.ResolveSpeakerKeyForName("  Keiko  ");
            Assert.That(a, Is.EqualTo(b));
            VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, a, Color.blue);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerDialogueBodyColor(p, b, Color.green);
            Assert.That(p.projectSpeakerPalette.Count, Is.EqualTo(1));
        }

        [Test] public void GlobalSpeakerColors_41_EmptyAndNarratorCreateNoProfile()
        {
            VnSceneComposerScene s = Scene(string.Empty);
            s.dialogueBeats[0].narration = true;
            VnSceneComposerProject p = Project(s);
            Assert.That(Key(s, 0), Is.Empty);
            Assert.That(p.projectSpeakerPalette, Is.Empty);
            Assert.Throws<InvalidOperationException>(() =>
                VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, string.Empty, Color.red));
            Assert.That(p.projectSpeakerPalette, Is.Empty);
        }

        [Test] public void GlobalSpeakerColors_42_SpeakerRenameRetargetsWithoutMutatingOldProfile()
        {
            VnSceneComposerScene s = Scene(Keiko);
            VnSceneComposerProject p = Project(s);
            SetGlobal(p, s, 0, Color.blue, Color.green);
            s.dialogueBeats[0].speaker = "Miko";
            VnWorkshopTypographyValues renamed = Resolve(p, s, 0);
            Assert.That(renamed.SpeakerColor, Is.Not.EqualTo(Color.blue));
            Assert.That(VnSceneComposerTextStyleResolver.FindProjectSpeakerProfile(p, "speaker:Keiko"), Is.Not.Null);
            Assert.That(VnSceneComposerTextStyleResolver.FindProjectSpeakerProfile(p, "speaker:Miko"), Is.Null);
        }

        private static VnSceneComposerProject Project(params VnSceneComposerScene[] scenes)
        {
            var p = new VnSceneComposerProject();
            for (int i = 0; i < scenes.Length; i++) p.scenes.Add(scenes[i]);
            return p;
        }

        private static VnSceneComposerScene Scene(string speaker)
        {
            var s = new VnSceneComposerScene();
            s.dialogueBeats.Clear();
            s.dialogueBeats.Add(Beat(speaker, "Text"));
            return s;
        }

        private static VnSceneComposerScene SceneWithCharacter(string id)
        {
            VnSceneComposerScene s = Scene(id);
            s.characters.Add(new VnSceneComposerCharacter { characterId = id, stateId = "state" });
            s.dialogueBeats[0].targetCharacterId = id;
            return s;
        }

        private static VnSceneComposerDialogueBeat Beat(string speaker, string text)
        {
            return new VnSceneComposerDialogueBeat { speaker = speaker, text = text, narration = false };
        }

        private static string Key(VnSceneComposerScene scene, int beatIndex)
        {
            return VnSceneComposerTextStyleResolver.ResolveSpeakerKey(
                scene, scene.dialogueBeats[beatIndex]);
        }

        private static VnWorkshopTypographyValues Resolve(
            VnSceneComposerProject p, VnSceneComposerScene s, int beatIndex)
        {
            return VnSceneComposerTextStyleResolver.Resolve(p, s, s.dialogueBeats[beatIndex]);
        }

        private static void SetGlobal(
            VnSceneComposerProject p, VnSceneComposerScene s, int beatIndex,
            Color name, Color body)
        {
            string key = Key(s, beatIndex);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerNameColor(p, key, name);
            VnSceneComposerTextStyleResolver.SetProjectSpeakerDialogueBodyColor(p, key, body);
        }

        private static void SetLegacy(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat,
            Color name, Color body)
        {
            string key = VnSceneComposerTextStyleResolver.ResolveSpeakerKey(scene, beat);
            scene.speakerColorOverrides.Add(new VnSceneComposerSpeakerColorOverride
            {
                speakerKey = key,
                color = name,
                hasDialogueBodyColor = true,
                dialogueBodyColor = body
            });
        }

        private static VnSceneComposerProject LoadAsSchemaThree(VnSceneComposerProject p)
        {
            string current = VnSceneComposerSerialization.SerializePortable(p);
            string old = current.Replace(
                "\"schemaVersion\": " + VnSceneComposerContract.SchemaVersion,
                "\"schemaVersion\": 3");
            Assert.That(old, Is.Not.EqualTo(current));
            VnSceneComposerImportResult loaded =
                VnSceneComposerSerialization.DeserializePortable(old);
            Assert.That(loaded.Success, Is.True, loaded.Error);
            return loaded.Project;
        }

        private static VnWorkshopPreviewFrame Frame(
            VnSceneComposerProject p, VnSceneComposerScene s, VnSceneComposerDialogueBeat b)
        {
            return VnSceneComposerComposition.BuildFrame(
                p, s, b, VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
        }

        private static VnPresentationWorkshopWindow WindowWithSpeaker(string speaker)
        {
            VnPresentationWorkshopWindow w =
                ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            w.ComposerAddScene();
            VnSceneComposerProject p = Project(w);
            p.scenes[0].dialogueBeats[0].speaker = speaker;
            return w;
        }

        private static VnSceneComposerProject Project(VnPresentationWorkshopWindow w)
        {
            FieldInfo field = typeof(VnPresentationWorkshopWindow).GetField(
                "_sceneComposerProject", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            return (VnSceneComposerProject)field.GetValue(w);
        }

        private static string TextUiSource()
        {
            string path = Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposerTextElements.cs");
            Assert.That(File.Exists(path), Is.True, "Missing editor source: " + path);
            return File.ReadAllText(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "Missing method: " + signature);
            int open = source.IndexOf('{', start);
            Assert.That(open, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(open, i - open + 1);
                }
            }
            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
