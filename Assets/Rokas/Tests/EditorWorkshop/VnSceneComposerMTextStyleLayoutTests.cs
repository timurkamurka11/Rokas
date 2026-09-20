using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerMTextStyleLayoutTests
    {
        private const string Keiko = "Keiko";
        private const string Mina = "Mina";
        private const string UserProjectId = "3cc2bc9ec7974ea7a5f4d074683bed61";
        private const string OtherProjectId = "11111111111111111111111111111111";
        private const string FontA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string FontB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
        }

        [Test] public void MTextStyleLayout_01_OldProjectUsesDefaultTypography()
        {
            var p = Project(Scene("Speaker"));
            VnWorkshopTypographyValues v = Resolve(p, p.scenes[0], 0);
            Assert.That(v.SpeakerColor, Is.EqualTo(Color.white));
            Assert.That(v.DialogueColor, Is.EqualTo(Color.white));
        }

        [Test] public void MTextStyleLayout_02_DefaultSpeakerColorScopeIsAllScenes()
        {
            Assert.That(new VnSceneComposerScene().speakerColorScope,
                Is.EqualTo(VnSceneComposerSpeakerColorScope.AllScenes));
        }

        [Test] public void MTextStyleLayout_03_GlobalSpeakerColorResolvesAcrossScenes()
        {
            var p = Project(Scene("A"), Scene("B"), Scene("C"));
            p.defaultPresentation.typography.hasSpeakerColor = true;
            p.defaultPresentation.typography.speakerColor = Color.cyan;
            for (int i = 0; i < p.scenes.Count; i++)
                Assert.That(Resolve(p, p.scenes[i], 0).SpeakerColor, Is.EqualTo(Color.cyan));
        }

        [Test] public void MTextStyleLayout_04_SceneLocalSpeakerColorAffectsOnlyThatScene()
        {
            var p = Project(Scene("Same"), Scene("Same"));
            p.defaultPresentation.typography.hasSpeakerColor = true;
            p.defaultPresentation.typography.speakerColor = Color.white;
            p.scenes[1].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[1].speakerColor = new Color(1f, .45f, .1f, 1f);
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.white));
            Assert.That(Resolve(p, p.scenes[1], 0).SpeakerColor,
                Is.EqualTo(new Color(1f, .45f, .1f, 1f)));
        }

        [Test] public void MTextStyleLayout_05_GlobalEditDoesNotOverwriteSceneLocalSpeakerColor()
        {
            var p = Project(Scene("A"), Scene("B"), Scene("C"));
            p.defaultPresentation.typography.hasSpeakerColor = true;
            p.defaultPresentation.typography.speakerColor = Color.white;
            p.scenes[1].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[1].speakerColor = Color.red;
            p.defaultPresentation.typography.speakerColor = Color.blue;
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(Resolve(p, p.scenes[1], 0).SpeakerColor, Is.EqualTo(Color.red));
            Assert.That(Resolve(p, p.scenes[2], 0).SpeakerColor, Is.EqualTo(Color.blue));
        }

        [Test] public void MTextStyleLayout_06_ReturnToGlobalResolvesCurrentGlobalColor()
        {
            var p = Project(Scene("A"));
            p.defaultPresentation.typography.hasSpeakerColor = true;
            p.defaultPresentation.typography.speakerColor = Color.blue;
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.red;
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.AllScenes;
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.blue));
        }

        [Test] public void MTextStyleLayout_07_SpeakerColorNeedsNoCharacterId()
        {
            var p = Project(Scene(string.Empty));
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.green;
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.green));
        }

        [Test] public void MTextStyleLayout_08_SpeakerTextIdentityDoesNotControlColorStorage()
        {
            var p = Project(Scene("Keiko"));
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.magenta;
            Color before = Resolve(p, p.scenes[0], 0).SpeakerColor;
            p.scenes[0].dialogueBeats[0].speaker = "Mina";
            p.scenes[0].dialogueBeats[0].targetCharacterId = "anything";
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerColor, Is.EqualTo(before));
        }

        [Test] public void MTextStyleLayout_09_SpeakerFontIsSharedAcrossScenes()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.defaultPresentation.typography.hasSpeakerFontAssetGuid = true;
            p.defaultPresentation.typography.speakerFontAssetGuid = FontA;
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerFontAssetGuid, Is.EqualTo(FontA));
            Assert.That(Resolve(p, p.scenes[1], 0).SpeakerFontAssetGuid, Is.EqualTo(FontA));
        }

        [Test] public void MTextStyleLayout_10_SpeakerSizeIsSharedAcrossScenes()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.defaultPresentation.typography.hasSpeakerFontSize = true;
            p.defaultPresentation.typography.speakerFontSize = 50f;
            Assert.That(Resolve(p, p.scenes[0], 0).SpeakerFontSize, Is.EqualTo(50f));
            Assert.That(Resolve(p, p.scenes[1], 0).SpeakerFontSize, Is.EqualTo(50f));
        }

        [Test] public void MTextStyleLayout_11_LocalSpeakerColorPersistsAcrossBeats()
        {
            VnSceneComposerScene scene = Scene("A");
            scene.dialogueBeats.Add(Beat("B", "Two"));
            scene.speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            scene.speakerColor = Color.cyan;
            var p = Project(scene);
            Assert.That(Resolve(p, scene, 0).SpeakerColor,
                Is.EqualTo(Resolve(p, scene, 1).SpeakerColor));
        }

        [Test] public void MTextStyleLayout_12_DuplicateGlobalColorSceneUsesGlobal()
        {
            var p = Project(Scene("A"));
            p.defaultPresentation.typography.hasSpeakerColor = true;
            p.defaultPresentation.typography.speakerColor = Color.blue;
            VnSceneComposerScene copy =
                VnSceneComposerEditing.DuplicateScene(p, p.scenes[0].sceneId);
            Assert.That(copy.speakerColorScope,
                Is.EqualTo(VnSceneComposerSpeakerColorScope.AllScenes));
            Assert.That(
                VnSceneComposerTextStyleResolver.Resolve(
                    p, copy, copy.dialogueBeats[0]).SpeakerColor,
                Is.EqualTo(Color.blue));
        }

        [Test] public void MTextStyleLayout_13_DuplicateLocalColorSceneCopiesIndependentLocalColor()
        {
            var p = Project(Scene("A"));
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.red;
            VnSceneComposerScene copy =
                VnSceneComposerEditing.DuplicateScene(p, p.scenes[0].sceneId);
            Assert.That(copy.speakerColorScope,
                Is.EqualTo(VnSceneComposerSpeakerColorScope.ThisScene));
            Assert.That(copy.speakerColor, Is.EqualTo(Color.red));
            copy.speakerColor = Color.green;
            Assert.That(p.scenes[0].speakerColor, Is.EqualTo(Color.red));
        }

        [Test] public void MTextStyleLayout_14_SceneBodyOverrideResolves()
        {
            var p = Project(Scene("A"));
            SetBodyStyle(p.scenes[0], FontA, 35f, Color.cyan);
            Assert.That(Resolve(p, p.scenes[0], 0).DialogueColor, Is.EqualTo(Color.cyan));
        }

        [Test] public void MTextStyleLayout_15_SceneBBodyEditDoesNotMutateSceneA()
        {
            var p = Project(Scene("A"), Scene("B"));
            SetBodyStyle(p.scenes[0], FontA, 33f, Color.white);
            SetBodyStyle(p.scenes[1], FontB, 38f, Color.cyan);
            Color before = Resolve(p, p.scenes[0], 0).DialogueColor;
            p.scenes[1].dialogueBodyStyleOverride.color = Color.yellow;
            Assert.That(Resolve(p, p.scenes[0], 0).DialogueColor, Is.EqualTo(before));
        }

        [Test] public void MTextStyleLayout_16_SpeakerGeometryIsSharedAcrossScenes()
        {
            var p = Project(Scene("A"), Scene("B"));
            SetProjectGeometry(p, true, new Rect(40f, 50f, 500f, 80f));
            Assert.That(Frame(p, 0).SpeakerName, Is.EqualTo(Frame(p, 1).SpeakerName));
        }

        [Test] public void MTextStyleLayout_17_BodyGeometryIsSharedAcrossScenes()
        {
            var p = Project(Scene("A"), Scene("B"));
            SetProjectGeometry(p, false, new Rect(80f, 160f, 1400f, 240f));
            Assert.That(Frame(p, 0).DialogueText, Is.EqualTo(Frame(p, 1).DialogueText));
        }

        [Test] public void MTextStyleLayout_18_SceneOverrideCannotReplaceSpeakerGeometry()
        {
            var p = Project(Scene("A"));
            SetProjectGeometry(p, true, new Rect(40f, 50f, 500f, 80f));
            p.scenes[0].presentationOverrides.speakerName.hasPositionDelta = true;
            p.scenes[0].presentationOverrides.speakerName.positionDelta = new Vector2(300f, 300f);
            AssertRect(Frame(p, 0).SpeakerName, new Rect(40f, 50f, 500f, 80f));
        }

        [Test] public void MTextStyleLayout_19_SceneOverrideCannotReplaceBodyGeometry()
        {
            var p = Project(Scene("A"));
            SetProjectGeometry(p, false, new Rect(80f, 160f, 1200f, 220f));
            p.scenes[0].presentationOverrides.dialogueText.hasPositionDelta = true;
            p.scenes[0].presentationOverrides.dialogueText.positionDelta = new Vector2(200f, -200f);
            AssertRect(Frame(p, 0).DialogueText, new Rect(80f, 160f, 1200f, 220f));
        }

        [Test] public void MTextStyleLayout_20_BeatSwitchDoesNotChangeGeometry()
        {
            VnSceneComposerScene s = Scene("A");
            s.dialogueBeats.Add(Beat("B", "Longer body text"));
            var p = Project(s);
            Rect a = Frame(p, s, s.dialogueBeats[0]).DialogueText;
            Rect b = Frame(p, s, s.dialogueBeats[1]).DialogueText;
            Assert.That(b, Is.EqualTo(a));
        }

        [Test] public void MTextStyleLayout_21_ImageBackgroundDoesNotChangeGeometry()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.scenes[1].media.kind = VnSceneComposerMediaKind.ExternalImage;
            p.scenes[1].media.reference = "missing-image.png";
            Assert.That(Frame(p, 1).DialogueText, Is.EqualTo(Frame(p, 0).DialogueText));
        }

        [Test] public void MTextStyleLayout_22_VideoBackgroundDoesNotChangeGeometry()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.scenes[1].media.kind = VnSceneComposerMediaKind.ExternalVideo;
            p.scenes[1].media.reference = "missing-video.mp4";
            Assert.That(Frame(p, 1).SpeakerName, Is.EqualTo(Frame(p, 0).SpeakerName));
        }

        [Test] public void MTextStyleLayout_23_WindowResizeDoesNotMutateStoredGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                w.ComposerSetSharedTextGeometry(false, new Vector2(80f, 160f), new Vector2(900f, 220f));
                VnWorkshopElementOverride g = Project(w).defaultPresentation.dialogueText;
                Vector2 pos = g.positionDelta; Vector2 size = g.sizeDelta;
                w.position = new Rect(20f, 20f, 700f, 500f);
                w.position = new Rect(20f, 20f, 1400f, 900f);
                Assert.That(g.positionDelta, Is.EqualTo(pos));
                Assert.That(g.sizeDelta, Is.EqualTo(size));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_24_PlaqueAssetChangeDoesNotMutateGeometry()
        {
            var p = Project(Scene("A"));
            SetProjectGeometry(p, false, new Rect(80f, 160f, 900f, 220f));
            Rect before = Frame(p, 0).DialogueText;
            p.defaultPresentation.dialoguePanelVisual.hasAssetGuid = true;
            p.defaultPresentation.dialoguePanelVisual.assetGuid = FontB;
            Assert.That(Frame(p, 0).DialogueText, Is.EqualTo(before));
        }

        [Test] public void MTextStyleLayout_25_DialogueLengthDoesNotMutateGeometry()
        {
            var p = Project(Scene("A"));
            Rect before = Frame(p, 0).DialogueText;
            p.scenes[0].dialogueBeats[0].text = new string('X', 1000);
            Assert.That(Frame(p, 0).DialogueText, Is.EqualTo(before));
        }

        [Test] public void MTextStyleLayout_26_SpeakerColorSetterDoesNotMutateSpeakerGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                VnWorkshopElementOverride g = Project(w).defaultPresentation.speakerName;
                Vector2 pos = g.positionDelta, size = g.sizeDelta;
                w.ComposerSetSpeakerColor(Color.red);
                Assert.That(g.positionDelta, Is.EqualTo(pos));
                Assert.That(g.sizeDelta, Is.EqualTo(size));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_27_BodyStyleSetterDoesNotMutateBodyGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                VnWorkshopElementOverride g = Project(w).defaultPresentation.dialogueText;
                Vector2 pos=g.positionDelta, size=g.sizeDelta;
                w.ComposerSetSelectedSceneDialogueBodyStyle(string.Empty, 39f, Color.cyan,
                    VnWorkshopTextAlignment.Left);
                Assert.That(g.positionDelta, Is.EqualTo(pos));
                Assert.That(g.sizeDelta, Is.EqualTo(size));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_28_ColorPickerUiHasIndependentStyleChangeScope()
        {
            string s = TextUiSource();
            Assert.That(s, Does.Contain("// STYLE SCOPE: ColorField/font changes never read or write geometry."));
            Assert.That(s, Does.Contain("EditorGUILayout.ColorField"));
            Assert.That(s, Does.Not.Contain("ApplyTypographyValues("));
        }

        [Test] public void MTextStyleLayout_29_ColorStyleMutationCancelsActivePreviewDrag()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                SetPrivate(w, "_sceneComposerDraggingPreviewObject", true);
                w.ComposerSetSpeakerColor(Color.red);
                Assert.That((bool)GetPrivate(w, "_sceneComposerDraggingPreviewObject"), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_30_FontStyleMutationLeavesGeometryUnchanged()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                Rect before = Frame(Project(w), 0).SpeakerName;
                w.ComposerSetSharedSpeakerStyle(
                    FontA, 48f, Color.white, VnWorkshopTextAlignment.Left);
                Assert.That(Frame(Project(w), 0).SpeakerName, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_31_FontSizeMutationLeavesGeometryUnchanged()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                Rect before = Frame(Project(w), 0).DialogueText;
                w.ComposerSetSelectedSceneDialogueBodyStyle(string.Empty, 72f, Color.white,
                    VnWorkshopTextAlignment.Left);
                Assert.That(Frame(Project(w), 0).DialogueText, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_32_DirectUpwardDragIsLinear()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                w.ComposerSetSelectedPresentationElement(VnWorkshopElement.SpeakerName);
                w.ComposerDragPresentationElement(new Vector2(0f, -25f));
                Assert.That(Project(w).defaultPresentation.speakerName.positionDelta.y,
                    Is.EqualTo(-25f).Within(.001f));
                w.ComposerDragPresentationElement(new Vector2(0f, -25f));
                Assert.That(Project(w).defaultPresentation.speakerName.positionDelta.y,
                    Is.EqualTo(-50f).Within(.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_33_NumericYAndDirectDragYAreEquivalent()
        {
            Rect baseline = VnPresentationWorkshopPreviewRenderer.GetReferenceTextRect(
                VnWorkshopElement.SpeakerName);
            VnPresentationWorkshopWindow a = WindowWithScene();
            VnPresentationWorkshopWindow b = WindowWithScene();
            try
            {
                a.ComposerSetSharedTextGeometry(true,
                    baseline.position + new Vector2(0f, -30f), baseline.size);
                b.ComposerSetSelectedPresentationElement(VnWorkshopElement.SpeakerName);
                b.ComposerDragPresentationElement(new Vector2(0f, -30f));
                Assert.That(Frame(Project(a), 0).SpeakerName,
                    Is.EqualTo(Frame(Project(b), 0).SpeakerName));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test] public void MTextStyleLayout_34_NoHiddenTopClamp()
        {
            Rect panel = VnPresentationWorkshopPreviewRenderer.GetReferenceTextRect(
                VnWorkshopElement.DialoguePanel);
            Rect authored = new Rect(panel.xMin, panel.yMax + 50f, 300f, 60f);
            Assert.That(VnPresentationWorkshopPreviewRenderer.ConstrainTextRectToPanel(authored, panel),
                Is.EqualTo(authored));
        }

        [Test] public void MTextStyleLayout_35_SpeakerDragDoesNotMoveBody()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                Rect body = Frame(Project(w), 0).DialogueText;
                w.ComposerSetSelectedPresentationElement(VnWorkshopElement.SpeakerName);
                w.ComposerDragPresentationElement(new Vector2(15f, -20f));
                Assert.That(Frame(Project(w), 0).DialogueText, Is.EqualTo(body));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_36_BodyDragDoesNotMoveSpeaker()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                Rect speaker = Frame(Project(w), 0).SpeakerName;
                w.ComposerSetSelectedPresentationElement(VnWorkshopElement.DialogueText);
                w.ComposerDragPresentationElement(new Vector2(15f, -20f));
                Assert.That(Frame(Project(w), 0).SpeakerName, Is.EqualTo(speaker));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_37_UndoSpeakerColorIsStyleOnly()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                w.ComposerSetSharedTextGeometry(
                    true, new Vector2(40f, 50f), new Vector2(500f, 80f));
                Rect geometry = Frame(Project(w), 0).SpeakerName;
                Color before = Resolve(Project(w), Project(w).scenes[0], 0).SpeakerColor;
                Undo.ClearAll();
                w.ComposerSetSpeakerColor(Color.red);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(Frame(Project(w), 0).SpeakerName, Is.EqualTo(geometry));
                Assert.That(Resolve(Project(w), Project(w).scenes[0], 0).SpeakerColor,
                    Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_38_UndoGeometryIsGeometryOnly()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                w.ComposerSetSelectedSceneSpeakerColorScope(
                    VnSceneComposerSpeakerColorScope.ThisScene);
                w.ComposerSetSpeakerColor(Color.red);
                Color style = Resolve(Project(w), Project(w).scenes[0], 0).SpeakerColor;
                Rect before = Frame(Project(w), 0).SpeakerName;
                Undo.ClearAll();
                w.ComposerSetSharedTextGeometry(
                    true, new Vector2(60f,70f), new Vector2(500f,80f));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(Frame(Project(w), 0).SpeakerName, Is.EqualTo(before));
                Assert.That(Resolve(Project(w), Project(w).scenes[0], 0).SpeakerColor,
                    Is.EqualTo(style));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextStyleLayout_39_SaveReopenPreservesScopedStyles()
        {
            var p = Project(Scene("A"));
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.red;
            SetBodyStyle(p.scenes[0], FontB, 38f, Color.cyan);
            VnSceneComposerProject q = RoundTrip(p);
            Assert.That(q.scenes[0].speakerColorScope,
                Is.EqualTo(VnSceneComposerSpeakerColorScope.ThisScene));
            Assert.That(Resolve(q, q.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.red));
            Assert.That(Resolve(q, q.scenes[0], 0).DialogueColor, Is.EqualTo(Color.cyan));
        }

        [Test] public void MTextStyleLayout_40_SaveReopenPreservesSharedGeometry()
        {
            var p = Project(Scene("A"));
            Rect expected = new Rect(75f, 145f, 1000f, 210f);
            SetProjectGeometry(p, false, expected);
            AssertRect(Frame(RoundTrip(p), 0).DialogueText, expected);
        }

        [Test] public void MTextStyleLayout_41_DuplicateSceneCopiesBodyStyle()
        {
            var p = Project(Scene("A"));
            SetBodyStyle(p.scenes[0], FontB, 38f, Color.cyan);
            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(p, p.scenes[0].sceneId);
            Assert.That(copy.dialogueBodyStyleOverride.color, Is.EqualTo(Color.cyan));
            Assert.That(copy.dialogueBodyStyleOverride.fontAssetGuid, Is.EqualTo(FontB));
        }

        [Test] public void MTextStyleLayout_42_PreviewAndPlayResolveSameScopedTypography()
        {
            var p = Project(Scene("A"));
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.red;
            SetBodyStyle(p.scenes[0], FontB, 38f, Color.cyan);
            VnWorkshopPreviewFrame preview = Frame(p, 0);
            using (var playback = new VnSceneComposerPlaybackController(p))
            {
                playback.PlayScene(0);
                VnWorkshopPreviewFrame play = playback.CurrentFrame.WorkshopFrame;
                Assert.That(play.Typography.SpeakerColor, Is.EqualTo(preview.Typography.SpeakerColor));
                Assert.That(play.Typography.DialogueColor, Is.EqualTo(preview.Typography.DialogueColor));
                Assert.That(play.SpeakerName, Is.EqualTo(preview.SpeakerName));
                Assert.That(play.DialogueText, Is.EqualTo(preview.DialogueText));
            }
        }

        [Test] public void MTextStyleLayout_43_OldProjectRoundTripPreservesIdsAndDefaultAppearance()
        {
            var p = Project(Scene("A"));
            string projectId=p.projectId, sceneId=p.scenes[0].sceneId, beatId=p.scenes[0].dialogueBeats[0].beatId;
            VnWorkshopPreviewFrame before=Frame(p,0);
            VnSceneComposerProject q=RoundTrip(p);
            Assert.That(q.projectId, Is.EqualTo(projectId));
            Assert.That(q.scenes[0].sceneId, Is.EqualTo(sceneId));
            Assert.That(q.scenes[0].dialogueBeats[0].beatId, Is.EqualTo(beatId));
            Assert.That(Frame(q,0).SpeakerName, Is.EqualTo(before.SpeakerName));
            Assert.That(Frame(q,0).DialogueText, Is.EqualTo(before.DialogueText));
        }

        [Test] public void MTextStyleLayout_44_LegacySceneDialogueStyleStillResolves()
        {
            var p=Project(Scene("A"));
            p.scenes[0].presentationOverrides.typography.hasDialogueColor=true;
            p.scenes[0].presentationOverrides.typography.dialogueColor=Color.green;
            Assert.That(Resolve(p,p.scenes[0],0).DialogueColor, Is.EqualTo(Color.green));
        }

        [Test] public void MTextStyleLayout_45_LegacySceneSpeakerStylePromotesToSharedOnRoundTrip()
        {
            var p=Project(SceneWithCharacter(Keiko));
            p.scenes[0].presentationOverrides.typography.hasSpeakerColor=true;
            p.scenes[0].presentationOverrides.typography.speakerColor=Color.green;
            VnSceneComposerProject q=RoundTrip(p);
            Assert.That(q.defaultPresentation.typography.hasSpeakerColor, Is.True);
            Assert.That(Resolve(q,q.scenes[0],0).SpeakerColor, Is.EqualTo(Color.green));
        }

        [Test] public void MTextStyleLayout_46_ExplicitSceneColorWinsPromotedLegacyDefault()
        {
            var p = Project(Scene("A"));
            p.scenes[0].presentationOverrides.typography.hasSpeakerColor = true;
            p.scenes[0].presentationOverrides.typography.speakerColor = Color.green;
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.red;
            VnSceneComposerProject q = RoundTrip(p);
            Assert.That(Resolve(q, q.scenes[0], 0).SpeakerColor, Is.EqualTo(Color.red));
        }

        [Test] public void MTextStyleLayout_47_ExplicitSceneBodyOverrideWinsLegacyBodyStyle()
        {
            var p=Project(Scene("A"));
            p.scenes[0].presentationOverrides.typography.hasDialogueColor=true;
            p.scenes[0].presentationOverrides.typography.dialogueColor=Color.green;
            SetBodyStyle(p.scenes[0],FontA,35f,Color.cyan);
            Assert.That(Resolve(p,p.scenes[0],0).DialogueColor, Is.EqualTo(Color.cyan));
        }

        [Test] public void MTextStyleLayout_48_MigrationFindsNewestExactProjectId()
        {
            string root=TempRoot();
            try
            {
                string old=CreateSavedProject(root,"old",UserProjectId,"Same",DateTime.UtcNow.AddMinutes(-5));
                string latest=CreateSavedProject(root,"latest",UserProjectId,"Same",DateTime.UtcNow);
                Assert.That(VnSceneComposerReviewProjectMigration.FindLatestProjectSource(
                    root,UserProjectId,string.Empty), Is.EqualTo(latest));
                Assert.That(File.Exists(VnSceneComposerStorage.GetProjectPath(old,UserProjectId)), Is.True);
            }
            finally { Delete(root); }
        }

        [Test] public void MTextStyleLayout_49_MigrationUsesIdentityNotVisibleTitle()
        {
            string root=TempRoot();
            try
            {
                string correct=CreateSavedProject(root,"correct",UserProjectId,"Untitled VN Sequence",DateTime.UtcNow);
                CreateSavedProject(root,"other",OtherProjectId,"Untitled VN Sequence",DateTime.UtcNow.AddMinutes(1));
                Assert.That(VnSceneComposerReviewProjectMigration.FindLatestProjectSource(
                    root,UserProjectId,string.Empty), Is.EqualTo(correct));
            }
            finally { Delete(root); }
        }

        [Test] public void MTextStyleLayout_50_MigrationRejectsMismatchedJsonIdentity()
        {
            string root=TempRoot();
            try
            {
                string fake=Path.Combine(root,"fake");
                string path=VnSceneComposerStorage.GetProjectPath(fake,UserProjectId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path,JsonUtility.ToJson(new VnSceneComposerProject
                { projectId=OtherProjectId, title="Untitled VN Sequence" },true));
                Assert.That(VnSceneComposerReviewProjectMigration.FindLatestProjectSource(
                    root,UserProjectId,string.Empty), Is.Empty);
            }
            finally { Delete(root); }
        }

        [Test] public void MTextStyleLayout_51_MigrationExcludesDestinationClone()
        {
            string root=TempRoot();
            try
            {
                string a=CreateSavedProject(root,"source",UserProjectId,"A",DateTime.UtcNow.AddMinutes(-2));
                string b=CreateSavedProject(root,"destination",UserProjectId,"B",DateTime.UtcNow);
                Assert.That(VnSceneComposerReviewProjectMigration.FindLatestProjectSource(
                    root,UserProjectId,b), Is.EqualTo(a));
            }
            finally { Delete(root); }
        }

        [Test] public void MTextStyleLayout_52_MigrationContractCopiesMetaAndRefusesConflicts()
        {
            string s=MigrationSource();
            Assert.That(s, Does.Contain(".meta").And.Contain("CopyFileConflictSafe")
                .And.Contain("Migration conflict; refusing to overwrite"));
        }

        [Test] public void MTextStyleLayout_53_MigrationContractUsesOnlyKnownUserRootsPlusReferencedGuids()
        {
            string s=MigrationSource();
            Assert.That(s, Does.Contain("OnboardedAssets")
                .And.Contain("Art/UI/Fonts/Imported")
                .And.Contain("Resources/Fonts/TMP")
                .And.Contain("BuildSourceGuidMap"));
            Assert.That(s, Does.Not.Contain("CopyDirectoryConflictSafe(sourceRoot, destinationProjectRoot)"));
        }

        [Test] public void MTextStyleLayout_54_MigrationCreatesDurableBackupAndManifest()
        {
            string s=MigrationSource();
            Assert.That(s, Does.Contain("VNProjects").And.Contain("Snapshots")
                .And.Contain("user-asset-manifest.json"));
        }

        [Test] public void MTextStyleLayout_55_MigrationCommandLineIsGenericAndProjectIdDriven()
        {
            string s=MigrationSource();
            Assert.That(s, Does.Contain("-vnMigrationRoot").And.Contain("-vnProjectId")
                .And.Contain("MigrateFromCommandLine"));
        }

        [Test] public void MTextStyleLayout_56_MTextWrappingRegressionSuiteRemainsPresent()
        {
            string s=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Tests","EditorWorkshop",
                "VnSceneComposerMTextTests.cs"));
            Assert.That(s, Does.Contain("MText_LongDialogueWrapsWithinAuthoredWidth"));
        }

        [Test] public void MTextStyleLayout_57_MultilineAuthoringTextareaRegressionRemainsPresent()
        {
            string s=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Tests","EditorWorkshop",
                "VnSceneComposerMTextTests.cs"));
            Assert.That(s, Does.Contain("MText_AuthoringDialogueEditorUsesAdaptiveWrappedScrollableMultilineControl"));
        }

        [Test] public void MTextStyleLayout_58_WindowsFontImportRegressionRemainsPresent()
        {
            string s=File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnSceneComposerTextFontResolver.cs"));
            Assert.That(s, Does.Contain("ImportWindowsFont").And.Contain("ImportedTmpFolder"));
        }

        [Test] public void MTextStyleLayout_59_CharacterStagingDataIsUntouchedByStyleResolution()
        {
            var p=Project(SceneWithCharacter(Keiko));
            var staging=new VnSceneComposerBeatCharacterStaging { characterId=Keiko };
            p.scenes[0].dialogueBeats[0].characterStaging.Add(staging);
            VnSceneComposerTextStyleResolver.Resolve(p,p.scenes[0],p.scenes[0].dialogueBeats[0]);
            Assert.That(p.scenes[0].dialogueBeats[0].characterStaging[0], Is.SameAs(staging));
        }

        [Test] public void MTextStyleLayout_60_MediaMusicAndSfxReferencesAreUntouchedByStyleResolution()
        {
            var p=Project(Scene("A"));
            VnSceneComposerScene s=p.scenes[0];
            s.media.reference="video-ref";
            s.music.assetGuid=FontA;
            s.additionalAudioCues.Add(new VnSceneComposerAdditionalAudioCue { assetGuid=FontB });
            VnSceneComposerTextStyleResolver.Resolve(p,s,s.dialogueBeats[0]);
            Assert.That(s.media.reference, Is.EqualTo("video-ref"));
            Assert.That(s.music.assetGuid, Is.EqualTo(FontA));
            Assert.That(s.additionalAudioCues[0].assetGuid, Is.EqualTo(FontB));
        }

        [Test] public void MTextStyleLayout_61_MsfxContinuityFlagIsUntouchedByStyleResolution()
        {
            var p=Project(Scene("A"));
            p.scenes[0].keepPreviousAdditionalAudio=true;
            VnSceneComposerTextStyleResolver.Resolve(p,p.scenes[0],p.scenes[0].dialogueBeats[0]);
            Assert.That(p.scenes[0].keepPreviousAdditionalAudio, Is.True);
        }

        [Test] public void MTextStyleLayout_62_TransitionIsUntouchedByStyleResolution()
        {
            var p=Project(Scene("A"));
            p.scenes[0].transition.sceneTransitionType=VnSceneComposerSceneTransitionType.DarkCurtain;
            p.scenes[0].transition.sceneTransitionDuration=.7f;
            VnSceneComposerTextStyleResolver.Resolve(p,p.scenes[0],p.scenes[0].dialogueBeats[0]);
            Assert.That(p.scenes[0].transition.sceneTransitionType,
                Is.EqualTo(VnSceneComposerSceneTransitionType.DarkCurtain));
            Assert.That(p.scenes[0].transition.sceneTransitionDuration, Is.EqualTo(.7f));
        }


        [Test] public void MTextSpeakerColorSceneScope_63_SceneSpeakerColorScopeExistsAndDefaultsAllScenes()
        {
            FieldInfo field = typeof(VnSceneComposerScene).GetField(
                "speakerColorScope", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                "Speaker color scope must be stored on Scene, not on Character ID.");
            Assert.That(field.FieldType.IsEnum, Is.True);
            Assert.That(field.GetValue(new VnSceneComposerScene()).ToString(), Is.EqualTo("AllScenes"));
        }

        [Test] public void MTextColorGeometry_RED_64_TextGeometryScopeModelExistsAndDefaultsGlobal()
        {
            FieldInfo field = typeof(VnSceneComposerScene).GetField(
                "textGeometryScope", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                "Scene needs explicit All Scenes / This Scene text geometry scope.");
            Assert.That(field.FieldType.IsEnum, Is.True);
            object scene = new VnSceneComposerScene();
            Assert.That(field.GetValue(scene).ToString(), Is.EqualTo("AllScenes"));
        }

        [Test] public void MTextColorGeometry_RED_65_TextGeometryScopeAuthoringApiExists()
        {
            MethodInfo method = typeof(VnPresentationWorkshopWindow).GetMethod(
                "ComposerSetSelectedSceneTextGeometryScope",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null,
                "Authoring needs a dedicated scope switch instead of silently writing Scene/shared geometry.");
        }

        [Test] public void MTextColorGeometry_RED_66_SceneCannotOverrideDialoguePlaqueGeometry()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.defaultPresentation.dialoguePanel.hasPositionDelta = true;
            p.defaultPresentation.dialoguePanel.positionDelta = new Vector2(12f, 34f);
            p.scenes[1].presentationOverrides.dialoguePanel.hasPositionDelta = true;
            p.scenes[1].presentationOverrides.dialoguePanel.positionDelta = new Vector2(333f, -222f);

            VnPresentationWorkshopPreset a = VnSceneComposerComposition.ResolvePresentation(p, p.scenes[0]);
            VnPresentationWorkshopPreset b = VnSceneComposerComposition.ResolvePresentation(p, p.scenes[1]);

            Assert.That(a.dialoguePanel.positionDelta, Is.EqualTo(new Vector2(12f, 34f)));
            Assert.That(b.dialoguePanel.positionDelta, Is.EqualTo(new Vector2(12f, 34f)),
                "Dialogue plaque physical geometry is always project-global.");
        }

        [Test] public void MTextColorGeometry_RED_67_PlaqueLayoutEditWritesProjectGlobalGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                VnSceneComposerProject p = Project(w);
                w.ComposerSetPresentationScope(false);
                w.ComposerSetElementLayout(
                    VnWorkshopElement.DialoguePanel,
                    new Vector2(80f, -40f),
                    new Vector2(160f, 60f),
                    1.15f);

                Assert.That(p.defaultPresentation.dialoguePanel.hasPositionDelta, Is.True,
                    "Plaque layout edit must write project-global geometry even while Scene scope is selected.");
                Assert.That(p.defaultPresentation.dialoguePanel.positionDelta,
                    Is.EqualTo(new Vector2(80f, -40f)));
                Assert.That(p.scenes[0].presentationOverrides.dialoguePanel.hasPositionDelta, Is.False,
                    "Scene-local plaque geometry must not exist.");
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextSpeakerColorSceneScope_68_SceneColorApisExistWithoutCharacterKey()
        {
            MethodInfo setScope = typeof(VnPresentationWorkshopWindow).GetMethod(
                "ComposerSetSelectedSceneSpeakerColorScope",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo setColor = typeof(VnPresentationWorkshopWindow).GetMethod(
                "ComposerSetSpeakerColor",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(setScope, Is.Not.Null);
            Assert.That(setColor, Is.Not.Null);
            Assert.That(setColor.GetParameters().Length, Is.EqualTo(1),
                "Speaker color mutation must not accept Character ID or speaker text.");
        }

        [Test] public void MTextColorGeometry_RED_69_TextUiExposesScopeAndGlobalPlaqueLanguage()
        {
            string source = TextUiSource();
            Assert.That(source, Does.Contain("Ко всем сценам")
                .And.Contain("Только к этой сцене")
                .And.Contain("Общее для всех сцен"));
        }


        [Test] public void MTextColorGeometry_70_DefaultGeometryScopeIsAllScenes()
        {
            Assert.That(new VnSceneComposerScene().textGeometryScope,
                Is.EqualTo(VnSceneComposerTextGeometryScope.AllScenes));
        }

        [Test] public void MTextColorGeometry_71_LocalScopeSnapshotsSharedGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                w.ComposerSetSharedTextGeometry(true, new Vector2(70f, 80f), new Vector2(520f, 90f));
                Rect before = Frame(Project(w), 0).SpeakerName;
                w.ComposerSetSelectedSceneTextGeometryScope(VnSceneComposerTextGeometryScope.ThisScene);
                VnSceneComposerScene scene = Project(w).scenes[0];
                Assert.That(scene.presentationOverrides.speakerName.hasPositionDelta, Is.True);
                Assert.That(scene.presentationOverrides.speakerName.hasSizeDelta, Is.True);
                Assert.That(Frame(Project(w), 0).SpeakerName, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextColorGeometry_72_LocalSpeakerGeometryAffectsOnlyCurrentScene()
        {
            var p = Project(Scene("A"), Scene("B"));
            SetProjectGeometry(p, true, new Rect(40f, 50f, 500f, 80f));
            p.scenes[1].textGeometryScope = VnSceneComposerTextGeometryScope.ThisScene;
            SetLocalGeometry(p.scenes[1], true, new Rect(140f, 150f, 550f, 90f));
            AssertRect(Frame(p, 0).SpeakerName, new Rect(40f, 50f, 500f, 80f));
            AssertRect(Frame(p, 1).SpeakerName, new Rect(140f, 150f, 550f, 90f));
        }

        [Test] public void MTextColorGeometry_73_LocalBodyGeometryAffectsOnlyCurrentScene()
        {
            var p = Project(Scene("A"), Scene("B"));
            SetProjectGeometry(p, false, new Rect(80f, 160f, 1200f, 220f));
            p.scenes[1].textGeometryScope = VnSceneComposerTextGeometryScope.ThisScene;
            SetLocalGeometry(p.scenes[1], false, new Rect(180f, 260f, 900f, 260f));
            AssertRect(Frame(p, 0).DialogueText, new Rect(80f, 160f, 1200f, 220f));
            AssertRect(Frame(p, 1).DialogueText, new Rect(180f, 260f, 900f, 260f));
        }

        [Test] public void MTextColorGeometry_74_GlobalEditSkipsLocalException()
        {
            var p = Project(Scene("A"), Scene("B"), Scene("C"));
            SetProjectGeometry(p, false, new Rect(80f, 160f, 1000f, 220f));
            p.scenes[1].textGeometryScope = VnSceneComposerTextGeometryScope.ThisScene;
            SetLocalGeometry(p.scenes[1], false, new Rect(200f, 240f, 800f, 180f));
            SetProjectGeometry(p, false, new Rect(100f, 120f, 1100f, 250f));
            AssertRect(Frame(p, 0).DialogueText, new Rect(100f, 120f, 1100f, 250f));
            AssertRect(Frame(p, 1).DialogueText, new Rect(200f, 240f, 800f, 180f));
            AssertRect(Frame(p, 2).DialogueText, new Rect(100f, 120f, 1100f, 250f));
        }

        [Test] public void MTextColorGeometry_75_ReturnToGlobalClearsLocalGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                w.ComposerSetSharedTextGeometry(false, new Vector2(80f, 160f), new Vector2(1000f, 220f));
                w.ComposerSetSelectedSceneTextGeometryScope(VnSceneComposerTextGeometryScope.ThisScene);
                w.ComposerSetTextGeometry(false, new Vector2(180f, 260f), new Vector2(800f, 180f));
                w.ComposerSetSelectedSceneTextGeometryScope(VnSceneComposerTextGeometryScope.AllScenes);
                VnSceneComposerScene scene = Project(w).scenes[0];
                Assert.That(scene.presentationOverrides.dialogueText.hasPositionDelta, Is.False);
                Assert.That(scene.presentationOverrides.dialogueText.hasSizeDelta, Is.False);
                AssertRect(Frame(Project(w), 0).DialogueText, new Rect(80f, 160f, 1000f, 220f));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextColorGeometry_76_SpeakerColorDoesNotChangeGeometryScope()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                w.ComposerSetSelectedSceneTextGeometryScope(
                    VnSceneComposerTextGeometryScope.ThisScene);
                Rect before = Frame(Project(w), 0).SpeakerName;
                w.ComposerSetSelectedSceneSpeakerColorScope(
                    VnSceneComposerSpeakerColorScope.ThisScene);
                w.ComposerSetSpeakerColor(Color.red);
                Assert.That(Project(w).scenes[0].textGeometryScope,
                    Is.EqualTo(VnSceneComposerTextGeometryScope.ThisScene));
                Assert.That(Frame(Project(w), 0).SpeakerName, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextColorGeometry_77_SharedSpeakerFontDoesNotChangeGeometryScope()
        {
            VnPresentationWorkshopWindow w = WindowWithCharacter(Keiko);
            try
            {
                w.ComposerSetSelectedSceneTextGeometryScope(VnSceneComposerTextGeometryScope.ThisScene);
                Rect before = Frame(Project(w), 0).SpeakerName;
                w.ComposerSetSharedSpeakerStyle(
                    string.Empty, 61f, Color.white, VnWorkshopTextAlignment.Center);
                Assert.That(Project(w).scenes[0].textGeometryScope,
                    Is.EqualTo(VnSceneComposerTextGeometryScope.ThisScene));
                Assert.That(Frame(Project(w), 0).SpeakerName, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextColorGeometry_78_SaveReopenPreservesLocalScope()
        {
            var p = Project(Scene("A"));
            p.scenes[0].textGeometryScope = VnSceneComposerTextGeometryScope.ThisScene;
            SetLocalGeometry(p.scenes[0], false, new Rect(180f, 260f, 800f, 180f));
            VnSceneComposerProject q = RoundTrip(p);
            Assert.That(q.scenes[0].textGeometryScope,
                Is.EqualTo(VnSceneComposerTextGeometryScope.ThisScene));
            AssertRect(Frame(q, 0).DialogueText, new Rect(180f, 260f, 800f, 180f));
        }

        [Test] public void MTextColorGeometry_79_DuplicateScenePreservesLocalScopeSemantics()
        {
            var p = Project(Scene("A"));
            p.scenes[0].textGeometryScope = VnSceneComposerTextGeometryScope.ThisScene;
            SetLocalGeometry(p.scenes[0], true, new Rect(140f, 150f, 550f, 90f));
            VnSceneComposerScene copy =
                VnSceneComposerEditing.DuplicateScene(p, p.scenes[0].sceneId);
            Assert.That(copy.textGeometryScope,
                Is.EqualTo(VnSceneComposerTextGeometryScope.ThisScene));
            AssertRect(Frame(p, copy, copy.dialogueBeats[0]).SpeakerName,
                new Rect(140f, 150f, 550f, 90f));
        }

        [Test] public void MTextColorGeometry_80_UndoRestoresGeometryScope()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                Undo.ClearAll();
                w.ComposerSetSelectedSceneTextGeometryScope(VnSceneComposerTextGeometryScope.ThisScene);
                Undo.FlushUndoRecordObjects();
                Assert.That(Project(w).scenes[0].textGeometryScope,
                    Is.EqualTo(VnSceneComposerTextGeometryScope.ThisScene));
                Undo.PerformUndo();
                Assert.That(Project(w).scenes[0].textGeometryScope,
                    Is.EqualTo(VnSceneComposerTextGeometryScope.AllScenes));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextColorGeometry_81_PlaqueGeometryIsSharedAcrossScenes()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.defaultPresentation.dialoguePanel.hasPositionDelta = true;
            p.defaultPresentation.dialoguePanel.positionDelta = new Vector2(80f, -40f);
            p.defaultPresentation.dialoguePanel.hasSizeDelta = true;
            p.defaultPresentation.dialoguePanel.sizeDelta = new Vector2(160f, 60f);
            p.scenes[1].presentationOverrides.dialoguePanel.hasPositionDelta = true;
            p.scenes[1].presentationOverrides.dialoguePanel.positionDelta = new Vector2(999f, 999f);
            VnPresentationWorkshopPreset a =
                VnSceneComposerComposition.ResolvePresentation(p, p.scenes[0]);
            VnPresentationWorkshopPreset b =
                VnSceneComposerComposition.ResolvePresentation(p, p.scenes[1]);
            Assert.That(b.dialoguePanel.positionDelta, Is.EqualTo(a.dialoguePanel.positionDelta));
            Assert.That(b.dialoguePanel.sizeDelta, Is.EqualTo(a.dialoguePanel.sizeDelta));
        }

        [Test] public void MTextColorGeometry_82_PlaqueVisualMayDifferWhileGeometryStaysShared()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.defaultPresentation.dialoguePanel.hasPositionDelta = true;
            p.defaultPresentation.dialoguePanel.positionDelta = new Vector2(20f, 30f);
            p.scenes[0].presentationOverrides.dialoguePanelVisual.hasAssetGuid = true;
            p.scenes[0].presentationOverrides.dialoguePanelVisual.assetGuid = FontA;
            p.scenes[1].presentationOverrides.dialoguePanelVisual.hasAssetGuid = true;
            p.scenes[1].presentationOverrides.dialoguePanelVisual.assetGuid = FontB;
            VnPresentationWorkshopPreset a =
                VnSceneComposerComposition.ResolvePresentation(p, p.scenes[0]);
            VnPresentationWorkshopPreset b =
                VnSceneComposerComposition.ResolvePresentation(p, p.scenes[1]);
            Assert.That(a.dialoguePanelVisual.assetGuid, Is.EqualTo(FontA));
            Assert.That(b.dialoguePanelVisual.assetGuid, Is.EqualTo(FontB));
            Assert.That(a.dialoguePanel.positionDelta, Is.EqualTo(b.dialoguePanel.positionDelta));
        }

        [Test] public void MTextColorGeometry_83_PlaqueGlobalGeometryPersistsSaveReopen()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.defaultPresentation.dialoguePanel.hasPositionDelta = true;
            p.defaultPresentation.dialoguePanel.positionDelta = new Vector2(45f, -25f);
            p.scenes[1].presentationOverrides.dialoguePanel.hasPositionDelta = true;
            p.scenes[1].presentationOverrides.dialoguePanel.positionDelta = new Vector2(800f, 800f);
            VnSceneComposerProject q = RoundTrip(p);
            VnPresentationWorkshopPreset a =
                VnSceneComposerComposition.ResolvePresentation(q, q.scenes[0]);
            VnPresentationWorkshopPreset b =
                VnSceneComposerComposition.ResolvePresentation(q, q.scenes[1]);
            Assert.That(a.dialoguePanel.positionDelta, Is.EqualTo(new Vector2(45f, -25f)));
            Assert.That(b.dialoguePanel.positionDelta, Is.EqualTo(new Vector2(45f, -25f)));
        }

        [Test] public void MTextColorGeometry_84_UndoPlaqueMovementRestoresGlobalRect()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                VnSceneComposerProject p = Project(w);
                Vector2 before = p.defaultPresentation.dialoguePanel.positionDelta;
                Undo.ClearAll();
                w.ComposerSetElementLayout(
                    VnWorkshopElement.DialoguePanel,
                    new Vector2(90f, -30f), new Vector2(50f, 20f), 1f);
                Undo.FlushUndoRecordObjects();
                Assert.That(p.defaultPresentation.dialoguePanel.positionDelta,
                    Is.EqualTo(new Vector2(90f, -30f)));
                Undo.PerformUndo();
                Assert.That(p.defaultPresentation.dialoguePanel.positionDelta,
                    Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextColorGeometry_85_PlaqueEditDoesNotCreateSceneLocalGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                VnSceneComposerProject p = Project(w);
                w.ComposerSetPresentationScope(false);
                w.ComposerSetElementLayout(
                    VnWorkshopElement.DialoguePanel,
                    new Vector2(25f, 15f), new Vector2(30f, 10f), 1f);
                Assert.That(p.scenes[0].presentationOverrides.dialoguePanel.hasPositionDelta, Is.False);
                Assert.That(p.scenes[0].presentationOverrides.dialoguePanel.hasSizeDelta, Is.False);
                Assert.That(p.defaultPresentation.dialoguePanel.hasPositionDelta, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextSpeakerColorSceneScope_86_UiUsesSceneColorScopeNotCharacterColor()
        {
            string source = TextUiSource();
            Assert.That(source, Does.Contain("Применить цвет:")
                .And.Contain("Ко всем сценам")
                .And.Contain("Только к этой сцене"));
            Assert.That(source, Does.Not.Contain("Свой цвет персонажа")
                .And.Not.Contain("Цвет текущего персонажа"));
        }

        [Test] public void MTextSpeakerColorSceneScope_87_ResolverDoesNotUseCharacterIdentityForSpeakerColor()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnSceneComposerTextStyleResolver.cs"));
            Assert.That(source, Does.Not.Contain("ResolveSpeakerCharacterId")
                .And.Not.Contain("FindSpeakerStyle")
                .And.Not.Contain("CanonicalCharacterId"));
        }

        [Test] public void MTextColorGeometry_88_MigrationCollectsOnlyAssetGuidFields()
        {
            MethodInfo method = typeof(VnSceneComposerReviewProjectMigration).GetMethod(
                "CollectReferencedAssetGuids",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            string json =
                "{\"projectId\":\"11111111111111111111111111111111\"," +
                "\"sceneId\":\"22222222222222222222222222222222\"," +
                "\"assetGuid\":\"33333333333333333333333333333333\"," +
                "\"speakerFontAssetGuid\":\"44444444444444444444444444444444\"}";
            object value = method.Invoke(null, new object[] { json });
            var guids = value as System.Collections.Generic.HashSet<string>;
            Assert.That(guids, Is.Not.Null);
            Assert.That(guids, Does.Contain("33333333333333333333333333333333"));
            Assert.That(guids, Does.Contain("44444444444444444444444444444444"));
            Assert.That(guids, Does.Not.Contain("11111111111111111111111111111111"));
            Assert.That(guids, Does.Not.Contain("22222222222222222222222222222222"));
        }

        [Test] public void MTextColorGeometry_89_MigrationEnumeratesMultipleAssetRoots()
        {
            string root = TempRoot();
            try
            {
                string a = Path.Combine(root, "A");
                string b = Path.Combine(root, "B");
                string dest = Path.Combine(root, "Dest");
                Directory.CreateDirectory(Path.Combine(a, "Assets"));
                Directory.CreateDirectory(Path.Combine(b, "Assets"));
                Directory.CreateDirectory(Path.Combine(dest, "Assets"));

                MethodInfo method = typeof(VnSceneComposerReviewProjectMigration).GetMethod(
                    "EnumerateAssetSourceRoots",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method, Is.Not.Null);
                string[] roots = (string[])method.Invoke(
                    null, new object[] { root, dest, UserProjectId, a });

                Assert.That(roots, Does.Contain(Path.GetFullPath(a)));
                Assert.That(roots, Does.Contain(Path.GetFullPath(b)));
                Assert.That(roots, Does.Not.Contain(Path.GetFullPath(dest)));
            }
            finally { Delete(root); }
        }

        [Test] public void MTextColorGeometry_90_MigrationFailsClosedOnUnresolvedGuid()
        {
            string source = MigrationSource();
            Assert.That(source, Does.Contain("Referenced VN user assets could not be recovered by GUID")
                .And.Contain("Referenced VN project asset GUID is unresolved after migration")
                .And.Contain("EnumerateAssetSourceRoots"));
        }


        [Test] public void MTextColorGeometry_91_LegacyScenePlaqueGeometryPromotesToSharedAndClearsLocal()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.scenes[0].presentationOverrides.dialoguePanel.hasPositionDelta = true;
            p.scenes[0].presentationOverrides.dialoguePanel.positionDelta =
                new Vector2(44f, -33f);
            p.scenes[0].presentationOverrides.dialoguePanel.hasSizeDelta = true;
            p.scenes[0].presentationOverrides.dialoguePanel.sizeDelta =
                new Vector2(100f, 40f);
            p.scenes[0].presentationOverrides.dialoguePanelVisual.hasAssetGuid = true;
            p.scenes[0].presentationOverrides.dialoguePanelVisual.assetGuid = FontA;

            VnSceneComposerProject q = RoundTrip(p);

            Assert.That(q.defaultPresentation.dialoguePanel.hasPositionDelta, Is.True);
            Assert.That(q.defaultPresentation.dialoguePanel.positionDelta,
                Is.EqualTo(new Vector2(44f, -33f)));
            Assert.That(q.defaultPresentation.dialoguePanel.hasSizeDelta, Is.True);
            Assert.That(q.defaultPresentation.dialoguePanel.sizeDelta,
                Is.EqualTo(new Vector2(100f, 40f)));
            Assert.That(q.scenes[0].presentationOverrides.dialoguePanel.hasPositionDelta, Is.False);
            Assert.That(q.scenes[0].presentationOverrides.dialoguePanel.hasSizeDelta, Is.False);
            Assert.That(q.scenes[0].presentationOverrides.dialoguePanelVisual.hasAssetGuid, Is.True);
            Assert.That(q.scenes[0].presentationOverrides.dialoguePanelVisual.assetGuid, Is.EqualTo(FontA));
        }


        [Test] public void MTextSpeakerColorSceneScope_92_ProjectModelDoesNotOwnCharacterSpeakerColorProfiles()
        {
            FieldInfo field = typeof(VnSceneComposerProject).GetField(
                "speakerStyleOverrides", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Null,
                "Per-character speaker color profiles are cancelled; color authority is global + Scene-local.");
        }

        [Test] public void MTextColorGeometry_93_OldProjectRoundTripDefaultsGeometryScopeToAllScenes()
        {
            var p = Project(Scene("A"));
            VnSceneComposerProject q = RoundTrip(p);
            Assert.That(q.scenes[0].textGeometryScope,
                Is.EqualTo(VnSceneComposerTextGeometryScope.AllScenes));
        }

        [Test] public void MTextColorGeometry_94_LocalGeometryPreviewPlayParity()
        {
            var p = Project(Scene("A"));
            p.scenes[0].textGeometryScope = VnSceneComposerTextGeometryScope.ThisScene;
            SetLocalGeometry(p.scenes[0], true, new Rect(140f, 150f, 550f, 90f));
            SetLocalGeometry(p.scenes[0], false, new Rect(180f, 260f, 800f, 180f));
            VnWorkshopPreviewFrame preview = Frame(p, 0);
            using (var playback = new VnSceneComposerPlaybackController(p))
            {
                playback.PlayScene(0);
                VnWorkshopPreviewFrame play = playback.CurrentFrame.WorkshopFrame;
                Assert.That(play.SpeakerName, Is.EqualTo(preview.SpeakerName));
                Assert.That(play.DialogueText, Is.EqualTo(preview.DialogueText));
            }
        }

        [Test] public void MTextColorGeometry_95_DuplicateSceneDoesNotForkPlaqueGeometry()
        {
            var p = Project(Scene("A"));
            p.defaultPresentation.dialoguePanel.hasPositionDelta = true;
            p.defaultPresentation.dialoguePanel.positionDelta = new Vector2(35f, -15f);
            VnSceneComposerScene copy =
                VnSceneComposerEditing.DuplicateScene(p, p.scenes[0].sceneId);
            VnPresentationWorkshopPreset original =
                VnSceneComposerComposition.ResolvePresentation(p, p.scenes[0]);
            VnPresentationWorkshopPreset duplicate =
                VnSceneComposerComposition.ResolvePresentation(p, copy);
            Assert.That(duplicate.dialoguePanel.positionDelta,
                Is.EqualTo(original.dialoguePanel.positionDelta));
            Assert.That(copy.presentationOverrides.dialoguePanel.hasPositionDelta, Is.False);
        }

        [Test] public void MTextColorGeometry_96_SpeakerColorDoesNotMutatePlaqueGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                VnSceneComposerProject p = Project(w);
                p.defaultPresentation.dialoguePanel.hasPositionDelta = true;
                p.defaultPresentation.dialoguePanel.positionDelta = new Vector2(22f, 11f);
                Vector2 before = p.defaultPresentation.dialoguePanel.positionDelta;
                w.ComposerSetSelectedSceneSpeakerColorScope(
                    VnSceneComposerSpeakerColorScope.ThisScene);
                w.ComposerSetSpeakerColor(Color.red);
                Assert.That(p.defaultPresentation.dialoguePanel.positionDelta, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextColorGeometry_97_DialogueColorDoesNotMutatePlaqueGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                VnSceneComposerProject p = Project(w);
                p.defaultPresentation.dialoguePanel.hasPositionDelta = true;
                p.defaultPresentation.dialoguePanel.positionDelta = new Vector2(22f, 11f);
                Vector2 before = p.defaultPresentation.dialoguePanel.positionDelta;
                w.ComposerSetSelectedSceneDialogueBodyStyle(
                    string.Empty, 38f, Color.cyan, VnWorkshopTextAlignment.Left);
                Assert.That(p.defaultPresentation.dialoguePanel.positionDelta, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextColorGeometry_98_SharedSpeakerFontDoesNotMutatePlaqueGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                VnSceneComposerProject p = Project(w);
                p.defaultPresentation.dialoguePanel.hasPositionDelta = true;
                p.defaultPresentation.dialoguePanel.positionDelta = new Vector2(22f, 11f);
                Vector2 before = p.defaultPresentation.dialoguePanel.positionDelta;
                w.ComposerSetSharedSpeakerStyle(
                    string.Empty, 61f, Color.white, VnWorkshopTextAlignment.Center);
                Assert.That(p.defaultPresentation.dialoguePanel.positionDelta, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextColorGeometry_99_MigrationPreservesProjectSceneBeatCountsAndSource()
        {
            string root = TempRoot();
            try
            {
                string source = Path.Combine(root, "source");
                string destination = Path.Combine(root, "destination");
                Directory.CreateDirectory(Path.Combine(source, "Assets"));
                Directory.CreateDirectory(Path.Combine(destination, "Assets"));

                var p = new VnSceneComposerProject
                {
                    projectId = UserProjectId,
                    title = "Real Shape"
                };
                VnSceneComposerScene a = Scene("A");
                a.dialogueBeats.Add(Beat("A", "Second beat"));
                VnSceneComposerScene b = Scene("B");
                p.scenes.Add(a);
                p.scenes.Add(b);

                string sourceFile = VnSceneComposerStorage.GetProjectPath(source, UserProjectId);
                Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));
                string json = VnSceneComposerSerialization.SerializePortable(p);
                File.WriteAllText(sourceFile, json);
                string sourceBefore = File.ReadAllText(sourceFile);

                VnSceneComposerReviewProjectMigration.Migrate(
                    root, destination, UserProjectId);

                VnSceneComposerImportResult loaded =
                    VnSceneComposerStorage.LoadProject(destination, UserProjectId);
                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(loaded.Project.projectId, Is.EqualTo(UserProjectId));
                Assert.That(loaded.Project.scenes.Count, Is.EqualTo(2));
                Assert.That(loaded.Project.scenes[0].dialogueBeats.Count, Is.EqualTo(2));
                Assert.That(loaded.Project.scenes[1].dialogueBeats.Count, Is.EqualTo(1));
                Assert.That(File.ReadAllText(sourceFile), Is.EqualTo(sourceBefore),
                    "Migration must copy the project and leave the source untouched.");
            }
            finally { Delete(root); }
        }

        [Test] public void MTextSpeakerColorSceneScope_100_SceneColorStorageIsIndependentFromSpeakerAndTargetCharacter()
        {
            FieldInfo scope = typeof(VnSceneComposerScene).GetField(
                "speakerColorScope", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo color = typeof(VnSceneComposerScene).GetField(
                "speakerColor", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(scope, Is.Not.Null);
            Assert.That(color, Is.Not.Null);
        }

        [Test] public void MTextSpeakerColorSceneScope_101_DefaultScopeIsAllScenes()
        {
            Assert.That(new VnSceneComposerScene().speakerColorScope,
                Is.EqualTo(VnSceneComposerSpeakerColorScope.AllScenes));
        }

        [Test] public void MTextSpeakerColorSceneScope_102_GlobalColorAffectsAllNonOverriddenScenes()
        {
            var p = Project(Scene("A"), Scene("B"), Scene("C"));
            p.defaultPresentation.typography.hasSpeakerColor = true;
            p.defaultPresentation.typography.speakerColor = Color.blue;
            p.scenes[1].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[1].speakerColor = Color.red;
            Assert.That(Resolve(p,p.scenes[0],0).SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(Resolve(p,p.scenes[1],0).SpeakerColor, Is.EqualTo(Color.red));
            Assert.That(Resolve(p,p.scenes[2],0).SpeakerColor, Is.EqualTo(Color.blue));
        }

        [Test] public void MTextSpeakerColorSceneScope_103_LocalColorAffectsOnlyCurrentScene()
        {
            var p = Project(Scene("Same"), Scene("Same"));
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.green;
            Assert.That(Resolve(p,p.scenes[0],0).SpeakerColor, Is.EqualTo(Color.green));
            Assert.That(Resolve(p,p.scenes[1],0).SpeakerColor, Is.EqualTo(Color.white));
        }

        [Test] public void MTextSpeakerColorSceneScope_104_GlobalEditDoesNotOverwriteLocal()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.scenes[1].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[1].speakerColor = Color.red;
            p.defaultPresentation.typography.hasSpeakerColor = true;
            p.defaultPresentation.typography.speakerColor = Color.blue;
            Assert.That(Resolve(p,p.scenes[1],0).SpeakerColor, Is.EqualTo(Color.red));
        }

        [Test] public void MTextSpeakerColorSceneScope_105_LocalToGlobalResolvesCurrentGlobal()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                w.ComposerSetSpeakerColor(Color.blue);
                w.ComposerSetSelectedSceneSpeakerColorScope(
                    VnSceneComposerSpeakerColorScope.ThisScene);
                w.ComposerSetSpeakerColor(Color.red);
                w.ComposerSetSelectedSceneSpeakerColorScope(
                    VnSceneComposerSpeakerColorScope.AllScenes);
                Assert.That(Resolve(Project(w),Project(w).scenes[0],0).SpeakerColor,
                    Is.EqualTo(Color.blue));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextSpeakerColorSceneScope_106_SaveReopenPersistsGlobalAndLocal()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.defaultPresentation.typography.hasSpeakerColor = true;
            p.defaultPresentation.typography.speakerColor = Color.blue;
            p.scenes[1].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[1].speakerColor = Color.red;
            VnSceneComposerProject q = RoundTrip(p);
            Assert.That(Resolve(q,q.scenes[0],0).SpeakerColor, Is.EqualTo(Color.blue));
            Assert.That(q.scenes[1].speakerColorScope,
                Is.EqualTo(VnSceneComposerSpeakerColorScope.ThisScene));
            Assert.That(Resolve(q,q.scenes[1],0).SpeakerColor, Is.EqualTo(Color.red));
        }

        [Test] public void MTextSpeakerColorSceneScope_107_DuplicateSceneCopiesScopeSemantics()
        {
            var p = Project(Scene("A"), Scene("B"));
            p.scenes[1].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[1].speakerColor = Color.red;
            VnSceneComposerScene globalCopy =
                VnSceneComposerEditing.DuplicateScene(p,p.scenes[0].sceneId);
            VnSceneComposerScene localCopy =
                VnSceneComposerEditing.DuplicateScene(p,p.scenes[1].sceneId);
            Assert.That(globalCopy.speakerColorScope,
                Is.EqualTo(VnSceneComposerSpeakerColorScope.AllScenes));
            Assert.That(localCopy.speakerColorScope,
                Is.EqualTo(VnSceneComposerSpeakerColorScope.ThisScene));
            Assert.That(localCopy.speakerColor, Is.EqualTo(Color.red));
        }

        [Test] public void MTextSpeakerColorSceneScope_108_UndoRestoresGlobalLocalAndScope()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                Color globalBefore = Resolve(Project(w),Project(w).scenes[0],0).SpeakerColor;
                Undo.ClearAll();
                w.ComposerSetSpeakerColor(Color.blue);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(Resolve(Project(w),Project(w).scenes[0],0).SpeakerColor,
                    Is.EqualTo(globalBefore));

                Undo.ClearAll();
                w.ComposerSetSelectedSceneSpeakerColorScope(
                    VnSceneComposerSpeakerColorScope.ThisScene);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(Project(w).scenes[0].speakerColorScope,
                    Is.EqualTo(VnSceneComposerSpeakerColorScope.AllScenes));

                w.ComposerSetSelectedSceneSpeakerColorScope(
                    VnSceneComposerSpeakerColorScope.ThisScene);
                Color localBefore = Project(w).scenes[0].speakerColor;
                Undo.ClearAll();
                w.ComposerSetSpeakerColor(Color.red);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(Project(w).scenes[0].speakerColor, Is.EqualTo(localBefore));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextSpeakerColorSceneScope_109_NoCharacterIdRequired()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                Project(w).scenes[0].dialogueBeats[0].targetCharacterId = string.Empty;
                Project(w).scenes[0].dialogueBeats[0].speaker = string.Empty;
                w.ComposerSetSelectedSceneSpeakerColorScope(
                    VnSceneComposerSpeakerColorScope.ThisScene);
                w.ComposerSetSpeakerColor(Color.cyan);
                Assert.That(Resolve(Project(w),Project(w).scenes[0],0).SpeakerColor,
                    Is.EqualTo(Color.cyan));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        [Test] public void MTextSpeakerColorSceneScope_110_WorksWithNoDialogueCharacter()
        {
            var p = Project(Scene("Без персонажа"));
            p.scenes[0].characters.Clear();
            p.scenes[0].dialogueBeats[0].targetCharacterId = string.Empty;
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.yellow;
            Assert.That(Resolve(p,p.scenes[0],0).SpeakerColor, Is.EqualTo(Color.yellow));
        }

        [Test] public void MTextSpeakerColorSceneScope_111_SpeakerTextIdentityDoesNotControlStorage()
        {
            var p = Project(Scene("Keiko"));
            p.scenes[0].speakerColorScope = VnSceneComposerSpeakerColorScope.ThisScene;
            p.scenes[0].speakerColor = Color.magenta;
            p.scenes[0].dialogueBeats[0].speaker = "Mina";
            Assert.That(Resolve(p,p.scenes[0],0).SpeakerColor, Is.EqualTo(Color.magenta));
        }

        [Test] public void MTextSpeakerColorSceneScope_112_ColorChangeDoesNotMutateGeometry()
        {
            VnPresentationWorkshopWindow w = WindowWithScene();
            try
            {
                w.ComposerSetSelectedSceneTextGeometryScope(
                    VnSceneComposerTextGeometryScope.ThisScene);
                Rect speaker = Frame(Project(w),0).SpeakerName;
                Rect body = Frame(Project(w),0).DialogueText;
                Vector2 plaque = Project(w).defaultPresentation.dialoguePanel.positionDelta;
                VnSceneComposerTextGeometryScope geometryScope =
                    Project(w).scenes[0].textGeometryScope;

                w.ComposerSetSelectedSceneSpeakerColorScope(
                    VnSceneComposerSpeakerColorScope.ThisScene);
                w.ComposerSetSpeakerColor(Color.red);

                Assert.That(Frame(Project(w),0).SpeakerName, Is.EqualTo(speaker));
                Assert.That(Frame(Project(w),0).DialogueText, Is.EqualTo(body));
                Assert.That(Project(w).defaultPresentation.dialoguePanel.positionDelta,
                    Is.EqualTo(plaque));
                Assert.That(Project(w).scenes[0].textGeometryScope, Is.EqualTo(geometryScope));
            }
            finally { UnityEngine.Object.DestroyImmediate(w); }
        }

        private static VnSceneComposerProject Project(params VnSceneComposerScene[] scenes)
        {
            var p=new VnSceneComposerProject();
            for(int i=0;i<scenes.Length;i++)p.scenes.Add(scenes[i]);
            return p;
        }

        private static VnSceneComposerScene Scene(string speaker)
        {
            var s=new VnSceneComposerScene();
            s.dialogueBeats.Clear();
            s.dialogueBeats.Add(Beat(speaker,"Text"));
            return s;
        }

        private static VnSceneComposerScene SceneWithCharacter(string id)
        {
            VnSceneComposerScene s=Scene(id);
            s.characters.Add(new VnSceneComposerCharacter
            {
                characterId=id,
                stateId=StateForCharacter(id)
            });
            return s;
        }

        private static VnSceneComposerScene SceneWithCharacters(params string[] ids)
        {
            VnSceneComposerScene s=Scene(ids.Length>0?ids[0]:string.Empty);
            for(int i=0;i<ids.Length;i++)
            {
                s.characters.Add(new VnSceneComposerCharacter
                {
                    characterId=ids[i],
                    stateId=StateForCharacter(ids[i])
                });
            }
            return s;
        }

        private static string StateForCharacter(string id)
        {
            if (string.Equals(id, Keiko, StringComparison.OrdinalIgnoreCase)) return "keiko_serious";
            if (string.Equals(id, Mina, StringComparison.OrdinalIgnoreCase)) return "mina_happy";
            return string.Empty;
        }

        private static VnSceneComposerDialogueBeat Beat(string speaker,string text)
        {
            return new VnSceneComposerDialogueBeat { speaker=speaker,text=text,narration=false };
        }

        private static void SetBodyStyle(VnSceneComposerScene s,string guid,float size,Color color)
        {
            VnSceneComposerTextStyleResolver.SetStyle(
                s.dialogueBodyStyleOverride,guid,size,color,VnWorkshopTextAlignment.Left);
        }

        private static VnWorkshopTypographyValues Resolve(
            VnSceneComposerProject p,VnSceneComposerScene s,int beat)
        {
            return VnSceneComposerTextStyleResolver.Resolve(p,s,s.dialogueBeats[beat]);
        }

        private static VnWorkshopPreviewFrame Frame(VnSceneComposerProject p,int scene)
        {
            return Frame(p,p.scenes[scene],p.scenes[scene].dialogueBeats[0]);
        }

        private static VnWorkshopPreviewFrame Frame(
            VnSceneComposerProject p,VnSceneComposerScene s,VnSceneComposerDialogueBeat b)
        {
            return VnSceneComposerComposition.BuildFrame(
                p,s,b,VnWorkshopResolution.Reference1920x1080,Texture2D.blackTexture);
        }

        private static void SetProjectGeometry(VnSceneComposerProject p,bool speaker,Rect rect)
        {
            Rect baseline=VnPresentationWorkshopPreviewRenderer.GetReferenceTextRect(
                speaker?VnWorkshopElement.SpeakerName:VnWorkshopElement.DialogueText);
            VnWorkshopElementOverride g=speaker?p.defaultPresentation.speakerName:p.defaultPresentation.dialogueText;
            g.hasPositionDelta=rect.center!=baseline.center;
            g.positionDelta=rect.center-baseline.center;
            g.hasSizeDelta=rect.size!=baseline.size;
            g.sizeDelta=rect.size-baseline.size;
        }

        private static void SetLocalGeometry(
            VnSceneComposerScene scene, bool speaker, Rect rect)
        {
            Rect baseline=VnPresentationWorkshopPreviewRenderer.GetReferenceTextRect(
                speaker?VnWorkshopElement.SpeakerName:VnWorkshopElement.DialogueText);
            VnWorkshopElementOverride g=speaker
                ? scene.presentationOverrides.speakerName
                : scene.presentationOverrides.dialogueText;
            g.hasPositionDelta=true;
            g.positionDelta=rect.center-baseline.center;
            g.hasSizeDelta=true;
            g.sizeDelta=rect.size-baseline.size;
            g.hasScaleMultiplier=true;
            g.scaleMultiplier=1f;
        }

        private static VnSceneComposerProject RoundTrip(VnSceneComposerProject p)
        {
            string json=VnSceneComposerSerialization.SerializePortable(p);
            VnSceneComposerImportResult r=VnSceneComposerSerialization.DeserializePortable(json);
            Assert.That(r.Success,Is.True,r.Error);
            return r.Project;
        }

        private static VnPresentationWorkshopWindow WindowWithScene()
        {
            var w=ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            w.ComposerAddScene();
            return w;
        }

        private static VnPresentationWorkshopWindow WindowWithCharacter(string id)
        {
            VnPresentationWorkshopWindow w=WindowWithScene();
            VnSceneComposerProject p=Project(w);
            p.scenes[0].characters.Add(new VnSceneComposerCharacter
            {
                characterId=id,
                stateId=StateForCharacter(id)
            });
            p.scenes[0].dialogueBeats[0].speaker=id;
            return w;
        }

        private static VnSceneComposerProject Project(VnPresentationWorkshopWindow w)
        {
            return (VnSceneComposerProject)GetPrivate(w,"_sceneComposerProject");
        }

        private static object GetPrivate(object instance,string name)
        {
            FieldInfo f=instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(f,Is.Not.Null,"Missing field "+name);
            return f.GetValue(instance);
        }

        private static void SetPrivate(object instance,string name,object value)
        {
            FieldInfo f=instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(f,Is.Not.Null,"Missing field "+name);
            f.SetValue(instance,value);
        }

        private static void AssertRect(Rect actual,Rect expected)
        {
            Assert.That(actual.x,Is.EqualTo(expected.x).Within(.01f));
            Assert.That(actual.y,Is.EqualTo(expected.y).Within(.01f));
            Assert.That(actual.width,Is.EqualTo(expected.width).Within(.01f));
            Assert.That(actual.height,Is.EqualTo(expected.height).Within(.01f));
        }

        private static string TextUiSource()
        {
            return File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposerTextElements.cs"));
        }

        private static string MigrationSource()
        {
            return File.ReadAllText(Path.Combine(Application.dataPath,"Rokas","Scripts","Editor","VnUiWorkshop",
                "VnSceneComposerReviewProjectMigration.cs"));
        }

        private static string TempRoot()
        {
            string p=Path.Combine(Path.GetTempPath(),"rokas-mtext-style-layout-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(p);
            return p;
        }

        private static string CreateSavedProject(
            string root,string folder,string id,string title,DateTime timestamp)
        {
            string projectRoot=Path.Combine(root,folder);
            string path=VnSceneComposerStorage.GetProjectPath(projectRoot,id);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,JsonUtility.ToJson(new VnSceneComposerProject
            { projectId=id,title=title },true));
            File.SetLastWriteTimeUtc(path,timestamp);
            return Path.GetFullPath(projectRoot);
        }

        private static void Delete(string path)
        {
            if(Directory.Exists(path))Directory.Delete(path,true);
        }
    }
}
