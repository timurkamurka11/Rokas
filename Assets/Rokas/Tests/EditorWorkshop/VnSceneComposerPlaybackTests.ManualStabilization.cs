using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void NormalAuthoringClickSelectsDialogueTextWithoutAdvancedLayoutMode()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                ManualInvoke(windowType, window, "ComposerAddScene");
                ManualSetPrivateField(window, "comparisonView", Enum.Parse(
                    ManualGetPrivateField(window, "comparisonView").GetType(), "Current"));
                ManualSetPrivateField(window, "_sceneComposerInspectorSection", 2);

                Type elementType = RequireType("VnWorkshopElement");
                object back = Enum.Parse(elementType, "Back");
                object dialogueText = Enum.Parse(elementType, "DialogueText");
                ManualRequireInstance(windowType, "ComposerSetSelectedPresentationElement", elementType)
                    .Invoke(window, new[] { back });

                object frame = ManualInvoke(windowType, window, "ComposerBuildSelectedPreviewFrame");
                MethodInfo getElementRect = frame.GetType().GetMethod("GetElementRect",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { elementType }, null);
                Assert.That(getElementRect, Is.Not.Null);
                Rect dialogueRect = (Rect)getElementRect.Invoke(frame, new[] { dialogueText });
                Vector2 screenSize = (Vector2)Get(frame, "ScreenSize");
                Rect previewRect = new Rect(0f, 0f, screenSize.x, screenSize.y);
                Type rendererType = RequireType("VnPresentationWorkshopPreviewRenderer");
                MethodInfo logicalToPreview = rendererType.GetMethod("LogicalToPreview",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Rect), typeof(Rect), frame.GetType() }, null);
                Assert.That(logicalToPreview, Is.Not.Null);
                Rect dialoguePreviewRect = (Rect)logicalToPreview.Invoke(null, new object[]
                {
                    previewRect, dialogueRect, frame
                });

                Assert.That(previewRect.Contains(dialoguePreviewRect.center), Is.True,
                    "Synthetic click center must be inside visible preview. dialogueLogical=" + dialogueRect +
                    ", dialoguePreview=" + dialoguePreviewRect + ", preview=" + previewRect);

                MethodInfo previewToLogical = rendererType.GetMethod("PreviewToLogical",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Rect), typeof(Vector2), frame.GetType() }, null);
                Assert.That(previewToLogical, Is.Not.Null);
                Vector2 roundTripPoint = (Vector2)previewToLogical.Invoke(null, new object[]
                {
                    previewRect, dialoguePreviewRect.center, frame
                });
                Assert.That(dialogueRect.Contains(roundTripPoint), Is.True,
                    "Logical/preview round trip must remain inside DialogueText. point=" + roundTripPoint +
                    ", dialogueLogical=" + dialogueRect);

                MethodInfo hitTestUi = windowType.GetMethod("HitTestSceneComposerUi",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, new[] { frame.GetType(), typeof(Vector2) }, null);
                Assert.That(hitTestUi, Is.Not.Null,
                    "Missing static Scene Composer UI hit-test diagnostic helper.");
                object directHit = hitTestUi.Invoke(null, new object[] { frame, roundTripPoint });
                Assert.That(directHit, Is.Not.Null,
                    "UI hit-test must resolve the synthetic DialogueText point before Event dispatch.");
                Assert.That(directHit.ToString(), Is.EqualTo("DialogueText"),
                    "UI hit-test must resolve DialogueText before Event dispatch.");

                var click = new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    mousePosition = dialoguePreviewRect.center
                };

                ManualRequireInstance(windowType, "HandleSceneComposerPreviewInput",
                        typeof(Rect), frame.GetType(), typeof(Event))
                    .Invoke(window, new object[] { previewRect, frame, click });

                object selected = ManualInvoke(windowType, window, "ComposerGetSelectedPresentationElement");
                Assert.That(selected.ToString(), Is.EqualTo("DialogueText"),
                    "Normal authoring must select canonical UI objects directly; Advanced layout mode must not be required.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void NormalAuthoringArrowNudgesSelectedDialoguePanelWithoutAdvancedLayoutMode()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                ManualInvoke(windowType, window, "ComposerAddScene");
                ManualSetPrivateField(window, "comparisonView", Enum.Parse(
                    ManualGetPrivateField(window, "comparisonView").GetType(), "Current"));
                ManualSetPrivateField(window, "_sceneComposerInspectorSection", 2);

                Type elementType = RequireType("VnWorkshopElement");
                object dialoguePanel = Enum.Parse(elementType, "DialoguePanel");
                ManualRequireInstance(windowType, "ComposerSetSelectedPresentationElement", elementType)
                    .Invoke(window, new[] { dialoguePanel });

                object frame = ManualInvoke(windowType, window, "ComposerBuildSelectedPreviewFrame");
                var key = new Event { type = EventType.KeyDown, keyCode = KeyCode.RightArrow };
                ManualRequireInstance(windowType, "HandleSceneComposerPreviewInput",
                        typeof(Rect), frame.GetType(), typeof(Event))
                    .Invoke(window, new object[] { new Rect(0f, 0f, 1920f, 1080f), frame, key });

                object preset = ManualInvoke(windowType, window, "ComposerGetActivePresentationPreset");
                MethodInfo getOverride = preset.GetType().GetMethod("GetElementOverride",
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { elementType }, null);
                Assert.That(getOverride, Is.Not.Null);
                object elementOverride = getOverride.Invoke(preset, new[] { dialoguePanel });
                Assert.That((bool)Get(elementOverride, "hasPositionDelta"), Is.True,
                    "Arrow-key direct manipulation must work in the ordinary editor, not only under Advanced layout tools.");
                Assert.That((Vector2)Get(elementOverride, "positionDelta"), Is.EqualTo(Vector2.right));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void BackspaceRemovesSelectedCharacterAndUnityUndoRestoresIt()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                ManualInvoke(windowType, window, "ComposerAddScene");
                ManualSetPrivateField(window, "comparisonView", Enum.Parse(
                    ManualGetPrivateField(window, "comparisonView").GetType(), "Current"));

                string[] states = (string[])ManualRequireInstance(windowType,
                        "ComposerGetAuthoredStateIds", typeof(string))
                    .Invoke(window, new object[] { "Mina" });
                Assert.That(states, Is.Not.Null.And.Not.Empty);
                ManualRequireInstance(windowType, "ComposerAddCharacter", typeof(string), typeof(string))
                    .Invoke(window, new object[] { "Mina", states[0] });
                IList characters = (IList)ManualGetPublicOrPrivateMember(((IList)ManualGetPublicOrPrivateMember(
                    ManualGetPrivateField(window, "_sceneComposerProject"), "scenes"))[0], "characters");
                Assert.That(characters.Count, Is.EqualTo(1));
                Undo.ClearAll();

                object frame = ManualInvoke(windowType, window, "ComposerBuildSelectedPreviewFrame");
                var key = new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace };
                ManualRequireInstance(windowType, "HandleSceneComposerPreviewInput",
                        typeof(Rect), frame.GetType(), typeof(Event))
                    .Invoke(window, new object[] { new Rect(0f, 0f, 1920f, 1080f), frame, key });

                Undo.FlushUndoRecordObjects();
                Assert.That(characters.Count, Is.EqualTo(0),
                    "Backspace must remove the selected scene character in normal authoring mode.");
                Undo.PerformUndo();
                characters = (IList)ManualGetPublicOrPrivateMember(((IList)ManualGetPublicOrPrivateMember(
                    ManualGetPrivateField(window, "_sceneComposerProject"), "scenes"))[0], "characters");
                Assert.That(characters.Count, Is.EqualTo(1), "Ctrl+Z/Unity Undo must restore the removed character.");
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static object ManualInvoke(Type type, object instance, string name)
        {
            MethodInfo method = ManualRequireInstance(type, name);
            return method.Invoke(instance, null);
        }

        private static MethodInfo ManualRequireInstance(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameters ?? Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing manual-stabilization method: " + type.Name + "." + name);
            return method;
        }

        private static object ManualGetPrivateField(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            return field.GetValue(instance);
        }

        private static void ManualSetPrivateField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, value);
        }

        private static object ManualGetPublicOrPrivateMember(object instance, string name)
        {
            Type type = instance.GetType();
            FieldInfo field = type.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(instance);
            PropertyInfo property = type.GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing member: " + type.Name + "." + name);
            return property.GetValue(instance, null);
        }
    }
}
