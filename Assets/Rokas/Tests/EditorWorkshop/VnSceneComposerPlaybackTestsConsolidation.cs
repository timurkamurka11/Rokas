using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void P13_ConsolidationInventoryExposesEveryUsefulWorkshopCapabilityInComposer()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            string[] expected =
            {
                "preview-modes", "element-layout", "character-layout", "variants", "portable-preset", "profiles",
                "typography", "preview-text", "typewriter", "expression", "character-enter-exit", "bounce",
                "background-transition", "stage-layout", "speaker-focus", "ui-feedback", "timing", "resolution-preview",
                "dialogue-panel-visual"
            };
            MethodInfo inventory = RequireWindowMethod(windowType, "ComposerGetPresentationCapabilityIds");
            string[] actual = (string[])inventory.Invoke(null, null);
            CollectionAssert.AreEquivalent(expected, actual);
        }

        [Test]
        public void P15_ProjectDefaultsAndSceneOverridesAreTheOnlyEditablePresentationSources()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            Type easing = RequireType("VnWorkshopEasing");
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                object project = GetPrivateField(window, "_sceneComposerProject");
                object scene = ((IList)Get(project, "scenes"))[0];
                MethodInfo scope = RequireWindowMethod(windowType, "ComposerSetPresentationScope", typeof(bool));
                MethodInfo active = RequireWindowMethod(windowType, "ComposerGetActivePresentationPreset");
                MethodInfo bounce = RequireWindowMethod(windowType, "ComposerSetBounce", typeof(float), typeof(float), typeof(float), typeof(float), easing);
                object ease = Enum.Parse(easing, "EaseInOut");

                scope.Invoke(window, new object[] { true });
                bounce.Invoke(window, new object[] { 27f, .42f, .08f, .2f, ease });
                object projectPreset = active.Invoke(window, null);
                Assert.That(projectPreset, Is.SameAs(Get(project, "defaultPresentation")));
                Assert.That((float)Get(Get(projectPreset, "actionBounce"), "amplitude"), Is.EqualTo(27f));

                scope.Invoke(window, new object[] { false });
                bounce.Invoke(window, new object[] { 41f, .33f, .05f, .11f, ease });
                object scenePreset = active.Invoke(window, null);
                Assert.That(scenePreset, Is.SameAs(Get(scene, "presentationOverrides")));
                Assert.That((float)Get(Get(scenePreset, "actionBounce"), "amplitude"), Is.EqualTo(41f));
                Assert.That((float)Get(Get(projectPreset, "actionBounce"), "amplitude"), Is.EqualTo(27f),
                    "Editing a Scene Override must not mutate Project Defaults.");

                SetPrivateField(window, "currentPreset", null);
                Type fontPreset = RequireType("VnWorkshopFontPreset");
                Type alignment = RequireType("VnWorkshopTextAlignment");
                MethodInfo typography = RequireWindowMethod(windowType, "ComposerSetTypography",
                    fontPreset, typeof(float), typeof(float), typeof(float), typeof(float), alignment,
                    fontPreset, typeof(float), typeof(float));
                typography.Invoke(window, new object[]
                {
                    Enum.Parse(fontPreset, "ProjectSans"), 30f, 1f, 2f, 3f, Enum.Parse(alignment, "Center"),
                    Enum.Parse(fontPreset, "ProjectSerif"), 28f, 1f
                });
                Assert.That((float)Get(Get(scenePreset, "typography"), "dialogueFontSize"), Is.EqualTo(30f),
                    "Normal Composer editing must not depend on the legacy currentPreset field.");
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void P19_ElementAndCharacterDirectEditingChangeTheActualSelectedScenePreviewAndResetExactly()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Type elementType = RequireType("VnWorkshopElement");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireWindowMethod(windowType, "ComposerAddCharacter", typeof(string), typeof(string))
                    .Invoke(window, new object[] { "Mina", "mina_neutral" });
                RequireWindowMethod(windowType, "ComposerSetPresentationScope", typeof(bool)).Invoke(window, new object[] { false });
                object dialoguePanel = Enum.Parse(elementType, "DialoguePanel");
                MethodInfo build = RequireWindowMethod(windowType, "ComposerBuildSelectedPreviewFrame");
                Rect panelBefore = (Rect)Get(build.Invoke(window, null), "DialoguePanel");
                Rect bodyBefore = FirstComposerBody(build.Invoke(window, null));

                RequireWindowMethod(windowType, "ComposerSetElementLayout", elementType, typeof(Vector2), typeof(Vector2), typeof(float))
                    .Invoke(window, new object[] { dialoguePanel, new Vector2(55f, 30f), new Vector2(80f, 40f), 1.1f });
                RequireWindowMethod(windowType, "ComposerSetCharacterTransform", typeof(int), typeof(Vector2), typeof(float))
                    .Invoke(window, new object[] { 0, new Vector2(120f, 25f), 1.2f });
                object changedFrame = build.Invoke(window, null);
                Assert.That((Rect)Get(changedFrame, "DialoguePanel"), Is.Not.EqualTo(panelBefore));
                Assert.That(FirstComposerBody(changedFrame), Is.Not.EqualTo(bodyBefore));

                RequireWindowMethod(windowType, "ComposerResetElement", elementType).Invoke(window, new[] { dialoguePanel });
                RequireWindowMethod(windowType, "ComposerResetCharacterTransform", typeof(int)).Invoke(window, new object[] { 0 });
                object resetFrame = build.Invoke(window, null);
                Assert.That((Rect)Get(resetFrame, "DialoguePanel"), Is.EqualTo(panelBefore));
                Assert.That(FirstComposerBody(resetFrame), Is.EqualTo(bodyBefore));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void P19_AllAdvancedPresentationControlsWriteComposerPresetData()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireWindowMethod(windowType, "ComposerSetPresentationScope", typeof(bool)).Invoke(window, new object[] { false });
                object preset = RequireWindowMethod(windowType, "ComposerGetActivePresentationPreset").Invoke(window, null);
                Type easing = RequireType("VnWorkshopEasing");
                object ease = Enum.Parse(easing, "EaseInOut");

                RequireWindowMethod(windowType, "ComposerSetTypewriter", typeof(bool), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float))
                    .Invoke(window, new object[] { true, 44f, .01f, .12f, .25f, .4f, .2f, .21f, .05f });
                RequireWindowMethod(windowType, "ComposerSetExpressionTransition", typeof(float), easing)
                    .Invoke(window, new object[] { .37f, ease });
                Type transitionMode = RequireType("VnWorkshopCharacterTransitionMode");
                Type slideDirection = RequireType("VnWorkshopSlideDirection");
                RequireWindowMethod(windowType, "ComposerSetCharacterTransition", transitionMode, typeof(float), typeof(float), typeof(float), slideDirection, easing)
                    .Invoke(window, new object[] { Enum.Parse(transitionMode, "SlideAndFade"), .48f, .31f, 96f, Enum.Parse(slideDirection, "Right"), ease });
                Type backgroundMode = RequireType("VnWorkshopBackgroundTransitionMode");
                Type curtainDirection = RequireType("VnWorkshopCurtainDirection");
                RequireWindowMethod(windowType, "ComposerSetBackgroundTransition", backgroundMode, typeof(float), typeof(float), curtainDirection, easing)
                    .Invoke(window, new object[] { Enum.Parse(backgroundMode, "Curtain"), .61f, .73f, Enum.Parse(curtainDirection, "LeftToRight"), ease });
                RequireWindowMethod(windowType, "ComposerSetStageLayout", typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), easing)
                    .Invoke(window, new object[] { -420f, 0f, 420f, 12f, .95f, 1.05f, .95f, 300f, .44f, ease });
                RequireWindowMethod(windowType, "ComposerSetSpeakerFocus", typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), easing)
                    .Invoke(window, new object[] { 1.08f, 1f, 14f, .91f, .68f, .79f, .29f, ease });
                Type uiEasing = RequireType("VnWorkshopEasing");
                RequireWindowMethod(windowType, "ComposerSetUiFeedback", typeof(float), typeof(float), typeof(Vector2), typeof(float), uiEasing, typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float))
                    .Invoke(window, new object[] { 1.07f, .93f, new Vector2(0f, -6f), .15f, Enum.Parse(uiEasing, "EaseOut"), 1.1f, .85f, 1f, .88f, .2f, .5f });
                RequireWindowMethod(windowType, "ComposerSetTiming", typeof(float), typeof(float), typeof(float))
                    .Invoke(window, new object[] { .18f, .13f, .52f });

                Assert.That((float)Get(Get(preset, "typewriter"), "charactersPerSecond"), Is.EqualTo(44f));
                Assert.That((float)Get(Get(preset, "expressionTransition"), "duration"), Is.EqualTo(.37f));
                Assert.That(Get(Get(preset, "characterTransition"), "mode").ToString(), Is.EqualTo("SlideAndFade"));
                Assert.That(Get(Get(preset, "backgroundTransition"), "mode").ToString(), Is.EqualTo("Curtain"));
                Assert.That((float)Get(Get(preset, "stageLayout"), "rightX"), Is.EqualTo(420f));
                Assert.That((float)Get(Get(preset, "focus"), "activeForwardOffset"), Is.EqualTo(14f));
                Assert.That((float)Get(Get(preset, "uiFeedback"), "pressedScale"), Is.EqualTo(.93f));
                Assert.That((float)Get(Get(preset, "timing"), "autoPreviewSequenceGap"), Is.EqualTo(.52f));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void P18_PresentationPresetTemplatesRoundTripWithoutReplacingComposerProjectState()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Type easing = RequireType("VnWorkshopEasing");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            string root = Path.Combine(Path.GetTempPath(), "rokas-composer-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireWindowMethod(windowType, "ComposerSetPresentationScope", typeof(bool)).Invoke(window, new object[] { false });
                object projectBefore = GetPrivateField(window, "_sceneComposerProject");
                string projectId = (string)Get(projectBefore, "projectId");
                MethodInfo setBounce = RequireWindowMethod(windowType, "ComposerSetBounce", typeof(float), typeof(float), typeof(float), typeof(float), easing);
                setBounce.Invoke(window, new object[] { 23f, .31f, .04f, .12f, Enum.Parse(easing, "EaseInOut") });
                RequireWindowMethod(windowType, "ComposerSavePresentationPreset", typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, "ParityTemplate" });

                setBounce.Invoke(window, new object[] { 49f, .5f, .1f, .2f, Enum.Parse(easing, "EaseOut") });
                RequireWindowMethod(windowType, "ComposerLoadPresentationPreset", typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, "ParityTemplate" });
                object active = RequireWindowMethod(windowType, "ComposerGetActivePresentationPreset").Invoke(window, null);
                Assert.That((float)Get(Get(active, "actionBounce"), "amplitude"), Is.EqualTo(23f));
                Assert.That((string)Get(GetPrivateField(window, "_sceneComposerProject"), "projectId"), Is.EqualTo(projectId));

                string json = (string)RequireWindowMethod(windowType, "ComposerExportPresentationPresetJson", typeof(string))
                    .Invoke(window, new object[] { "PortableParity" });
                setBounce.Invoke(window, new object[] { 61f, .6f, .12f, .25f, Enum.Parse(easing, "Linear") });
                object import = RequireWindowMethod(windowType, "ComposerImportPresentationPresetJson", typeof(string)).Invoke(window, new object[] { json });
                Assert.That((bool)Get(import, "Success"), Is.True);
                active = RequireWindowMethod(windowType, "ComposerGetActivePresentationPreset").Invoke(window, null);
                Assert.That((float)Get(Get(active, "actionBounce"), "amplitude"), Is.EqualTo(23f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void P19_ResolutionProfileAndUndoOperateOnComposerState()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Type resolutionType = RequireType("VnWorkshopResolution");
            Type elementType = RequireType("VnWorkshopElement");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireWindowMethod(windowType, "ComposerSetPresentationScope", typeof(bool)).Invoke(window, new object[] { false });
                RequireWindowMethod(windowType, "ComposerSetPreviewResolution", resolutionType)
                    .Invoke(window, new object[] { Enum.Parse(resolutionType, "FourThree1024x768") });
                object frame = RequireWindowMethod(windowType, "ComposerBuildSelectedPreviewFrame").Invoke(window, null);
                Vector2 screen = (Vector2)Get(frame, "ScreenSize");
                Assert.That(screen.x, Is.EqualTo(1024f));
                Assert.That(screen.y, Is.EqualTo(768f));

                RequireWindowMethod(windowType, "ComposerApplyReferenceMotionProfile").Invoke(window, null);
                object active = RequireWindowMethod(windowType, "ComposerGetActivePresentationPreset").Invoke(window, null);
                Assert.That((float)Get(Get(active, "typewriter"), "charactersPerSecond"), Is.EqualTo(31f));

                object panel = Enum.Parse(elementType, "DialoguePanel");
                Undo.ClearAll();
                RequireWindowMethod(windowType, "ComposerSetElementLayout", elementType, typeof(Vector2), typeof(Vector2), typeof(float))
                    .Invoke(window, new object[] { panel, new Vector2(77f, 0f), Vector2.zero, 1f });
                Undo.FlushUndoRecordObjects();
                active = RequireWindowMethod(windowType, "ComposerGetActivePresentationPreset").Invoke(window, null);
                Assert.That((Vector2)Get(Get(active, "dialoguePanel"), "positionDelta"), Is.EqualTo(new Vector2(77f, 0f)));
                Undo.PerformUndo();
                active = RequireWindowMethod(windowType, "ComposerGetActivePresentationPreset").Invoke(window, null);
                Assert.That((bool)Get(Get(active, "dialoguePanel"), "hasPositionDelta"), Is.False);
            }
            finally { Undo.ClearAll(); UnityEngine.Object.DestroyImmediate(window); }
        }

        private static MethodInfo RequireWindowMethod(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                null, parameters ?? Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing consolidated Scene Composer API: " + type.Name + "." + name);
            return method;
        }

        private static object GetPrivateField(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            return field.GetValue(instance);
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, value);
        }

        private static Rect FirstComposerBody(object frame)
        {
            IList characters = (IList)Get(frame, "ComposerCharacters");
            Assert.That(characters, Is.Not.Null.And.Not.Empty);
            return (Rect)Get(characters[0], "Body");
        }
    }
}
