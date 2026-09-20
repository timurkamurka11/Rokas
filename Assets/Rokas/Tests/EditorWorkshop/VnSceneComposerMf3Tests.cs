using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerMf3Tests
    {
        private const string DecorationPath = "Assets/Rokas/Art/VN/UI/Dialogue/VN_DialoguePanel_Mina_Light.png";
        private const string DecorationGuid = "db6ce06f7a32417097fc2b2b6376c2d3";
        private const string MissingGuid = "11111111111111111111111111111111";
        private const string DecorationId = "abababababababababababababababab";

        [Test]
        public void MF3_ModelIsSceneLocalCollectionAndKeepsSchemaThree()
        {
            Assert.That(VnSceneComposerContract.SchemaVersion, Is.EqualTo(3),
                "M-F3 is additive editor data and should not require a schema bump.");

            Type decorationType = RequireType("VnSceneComposerDecoration");
            Type layerType = RequireType("VnSceneComposerDecorationLayer");
            Assert.That(layerType.IsEnum, Is.True);

            AssertField(decorationType, "decorationId");
            AssertField(decorationType, "assetGuid");
            AssertField(decorationType, "displayName");
            AssertField(decorationType, "position");
            AssertField(decorationType, "scale");
            AssertField(decorationType, "opacity");
            AssertField(decorationType, "visible");
            AssertField(decorationType, "layer");

            FieldInfo decorations = typeof(VnSceneComposerScene).GetField("decorations", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(decorations, Is.Not.Null, "Decorations must belong directly to each Scene.");
            var scene = new VnSceneComposerScene();
            IList list = decorations.GetValue(scene) as IList;
            Assert.That(list, Is.Not.Null);
            Assert.That(list.Count, Is.EqualTo(0));
        }

        [Test]
        public void MF3_DecorationRoundTripsAllAuthoredProperties()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);

            object decoration = CreateDecoration(DecorationGuid, DecorationId);
            SetField(decoration, "displayName", "Poster");
            SetField(decoration, "position", new Vector2(321f, 222f));
            SetField(decoration, "scale", .67f);
            SetField(decoration, "opacity", .43f);
            SetField(decoration, "visible", false);
            SetEnumField(decoration, "layer", "FrontCharacters");
            GetDecorations(scene).Add(decoration);

            string json = VnSceneComposerSerialization.SerializePortable(project);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(json);

            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Project.schemaVersion, Is.EqualTo(3));
            IList loadedDecorations = GetDecorations(loaded.Project.scenes[0]);
            Assert.That(loadedDecorations.Count, Is.EqualTo(1));
            object restored = loadedDecorations[0];
            Assert.That((string)GetField(restored, "decorationId"), Is.EqualTo(DecorationId));
            Assert.That((string)GetField(restored, "assetGuid"), Is.EqualTo(DecorationGuid));
            Assert.That((string)GetField(restored, "displayName"), Is.EqualTo("Poster"));
            Assert.That((Vector2)GetField(restored, "position"), Is.EqualTo(new Vector2(321f, 222f)));
            Assert.That((float)GetField(restored, "scale"), Is.EqualTo(.67f).Within(.0001f));
            Assert.That((float)GetField(restored, "opacity"), Is.EqualTo(.43f).Within(.0001f));
            Assert.That((bool)GetField(restored, "visible"), Is.False);
            Assert.That(GetField(restored, "layer").ToString(), Is.EqualTo("FrontCharacters"));
        }

        [Test]
        public void MF3_DuplicateScenePreservesDecorationDataButRegeneratesIdentity()
        {
            var project = new VnSceneComposerProject();
            var source = new VnSceneComposerScene();
            project.scenes.Add(source);
            object decoration = CreateDecoration(DecorationGuid, DecorationId);
            SetField(decoration, "position", new Vector2(140f, 90f));
            SetField(decoration, "opacity", .5f);
            GetDecorations(source).Add(decoration);

            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(project, source.sceneId);

            IList copied = GetDecorations(copy);
            Assert.That(copied.Count, Is.EqualTo(1));
            Assert.That((string)GetField(copied[0], "decorationId"), Is.Not.EqualTo(DecorationId));
            Assert.That(IsStableId((string)GetField(copied[0], "decorationId")), Is.True);
            Assert.That((string)GetField(copied[0], "assetGuid"), Is.EqualTo(DecorationGuid));
            Assert.That((Vector2)GetField(copied[0], "position"), Is.EqualTo(new Vector2(140f, 90f)));
            Assert.That((float)GetField(copied[0], "opacity"), Is.EqualTo(.5f).Within(.0001f));
        }

        [Test]
        public void MF3_CompositionResolvesDecorationIntoCanonicalFrame()
        {
            Texture2D expected = AssetDatabase.LoadAssetAtPath<Texture2D>(DecorationPath);
            Assert.That(expected, Is.Not.Null);

            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            object decoration = CreateDecoration(DecorationGuid, DecorationId);
            SetField(decoration, "position", new Vector2(600f, 360f));
            SetField(decoration, "scale", .8f);
            SetField(decoration, "opacity", .65f);
            GetDecorations(scene).Add(decoration);

            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                project, scene, VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);

            IList rendered = GetFrameCollection(frame, "ComposerDecorations");
            Assert.That(rendered.Count, Is.EqualTo(1));
            object visual = rendered[0];
            Assert.That((string)GetProperty(visual, "DecorationId"), Is.EqualTo(DecorationId));
            Assert.That(GetProperty(visual, "Texture"), Is.SameAs(expected));
            Assert.That((float)GetProperty(visual, "Alpha"), Is.EqualTo(.65f).Within(.0001f));
            Rect body = (Rect)GetProperty(visual, "Body");
            Assert.That(body.center.x, Is.EqualTo(600f).Within(.1f));
            Assert.That(body.center.y, Is.EqualTo(360f).Within(.1f));
        }

        [Test]
        public void MF3_MissingAssetIsSafeWarnsAndPreservesAuthoredData()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            object decoration = CreateDecoration(MissingGuid, DecorationId);
            SetField(decoration, "displayName", "Missing Poster");
            GetDecorations(scene).Add(decoration);

            VnWorkshopPreviewFrame frame = null;
            Assert.DoesNotThrow(() => frame = VnSceneComposerComposition.BuildFrame(
                project, scene, VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture));

            Assert.That(GetFrameCollection(frame, "ComposerDecorations").Count, Is.EqualTo(0));
            IList warnings = GetFrameCollection(frame, "ComposerDecorationWarnings");
            Assert.That(warnings.Count, Is.GreaterThan(0));
            Assert.That(warnings[0].ToString(), Does.Contain("Missing Poster").Or.Contain("не найден").IgnoreCase);
            Assert.That((string)GetField(GetDecorations(scene)[0], "assetGuid"), Is.EqualTo(MissingGuid));
        }

        [Test]
        public void MF3_PlaySceneUsesSameDecorationFrameData()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            object decoration = CreateDecoration(DecorationGuid, DecorationId);
            GetDecorations(scene).Add(decoration);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentFrame, Is.Not.Null);
                IList rendered = GetFrameCollection(playback.CurrentFrame.WorkshopFrame, "ComposerDecorations");
                Assert.That(rendered.Count, Is.EqualTo(1));
                Assert.That((string)GetProperty(rendered[0], "DecorationId"), Is.EqualTo(DecorationId));
            }
        }

        [Test]
        public void MF3_WindowActionsSelectEditHideAndDeleteSceneDecoration()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DecorationPath);
            Assert.That(texture, Is.Not.Null);
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                string id = (string)RequireWindowMethod("ComposerAddDecorationAsset", typeof(Texture2D))
                    .Invoke(window, new object[] { texture });
                Assert.That(id, Is.Not.Empty);
                Assert.That((string)RequireWindowMethod("ComposerGetSelectedDecorationId").Invoke(window, null), Is.EqualTo(id));

                RequireWindowMethod("ComposerSetSelectedDecorationPosition", typeof(Vector2))
                    .Invoke(window, new object[] { new Vector2(444f, 333f) });
                RequireWindowMethod("ComposerSetSelectedDecorationScale", typeof(float))
                    .Invoke(window, new object[] { 1.4f });
                RequireWindowMethod("ComposerSetSelectedDecorationOpacity", typeof(float))
                    .Invoke(window, new object[] { .35f });
                RequireWindowMethod("ComposerSetSelectedDecorationVisible", typeof(bool))
                    .Invoke(window, new object[] { false });

                VnSceneComposerScene scene = GetProject(window).scenes[0];
                IList list = GetDecorations(scene);
                Assert.That(list.Count, Is.EqualTo(1));
                object edited = list[0];
                Assert.That((Vector2)GetField(edited, "position"), Is.EqualTo(new Vector2(444f, 333f)));
                Assert.That((float)GetField(edited, "scale"), Is.EqualTo(1.4f).Within(.0001f));
                Assert.That((float)GetField(edited, "opacity"), Is.EqualTo(.35f).Within(.0001f));
                Assert.That((bool)GetField(edited, "visible"), Is.False);

                Assert.That((bool)RequireWindowMethod("ComposerDeleteSelectedDecoration").Invoke(window, null), Is.True);
                Assert.That(GetDecorations(scene).Count, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MF3_UndoRestoresAddEditVisibilityAndDelete()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DecorationPath);
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                Undo.ClearAll();

                string id = (string)RequireWindowMethod("ComposerAddDecorationAsset", typeof(Texture2D))
                    .Invoke(window, new object[] { texture });
                Undo.FlushUndoRecordObjects();
                Assert.That(GetDecorations(GetProject(window).scenes[0]).Count, Is.EqualTo(1));

                // Add is independently undoable.
                Undo.PerformUndo();
                Assert.That(GetDecorations(GetProject(window).scenes[0]).Count, Is.EqualTo(0));

                // Recreate setup, then isolate each following mutation exactly like the established
                // Scene Composer Undo fixtures do after setup.
                id = (string)RequireWindowMethod("ComposerAddDecorationAsset", typeof(Texture2D))
                    .Invoke(window, new object[] { texture });
                Undo.FlushUndoRecordObjects();
                Undo.ClearAll();

                RequireWindowMethod("ComposerSelectDecoration", typeof(string)).Invoke(window, new object[] { id });
                RequireWindowMethod("ComposerSetSelectedDecorationOpacity", typeof(float))
                    .Invoke(window, new object[] { .25f });
                Undo.FlushUndoRecordObjects();
                Assert.That((float)GetField(GetDecorations(GetProject(window).scenes[0])[0], "opacity"), Is.EqualTo(.25f).Within(.0001f));
                Undo.PerformUndo();
                Assert.That((float)GetField(GetDecorations(GetProject(window).scenes[0])[0], "opacity"), Is.EqualTo(1f).Within(.0001f));

                Undo.ClearAll();
                RequireWindowMethod("ComposerSelectDecoration", typeof(string)).Invoke(window, new object[] { id });
                RequireWindowMethod("ComposerSetSelectedDecorationVisible", typeof(bool)).Invoke(window, new object[] { false });
                Undo.FlushUndoRecordObjects();
                Assert.That((bool)GetField(GetDecorations(GetProject(window).scenes[0])[0], "visible"), Is.False);
                Undo.PerformUndo();
                Assert.That((bool)GetField(GetDecorations(GetProject(window).scenes[0])[0], "visible"), Is.True);

                Undo.ClearAll();
                RequireWindowMethod("ComposerSelectDecoration", typeof(string)).Invoke(window, new object[] { id });
                RequireWindowMethod("ComposerDeleteSelectedDecoration").Invoke(window, null);
                Undo.FlushUndoRecordObjects();
                Assert.That(GetDecorations(GetProject(window).scenes[0]).Count, Is.EqualTo(0));
                Undo.PerformUndo();
                Assert.That(GetDecorations(GetProject(window).scenes[0]).Count, Is.EqualTo(1));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MF3_DecorationMutationDoesNotResetActivePlayback()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DecorationPath);
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerPlayScene();
                object before = GetPrivateField(window, "_sceneComposerPlayback");
                Assert.That(before, Is.Not.Null);

                RequireWindowMethod("ComposerAddDecorationAsset", typeof(Texture2D)).Invoke(window, new object[] { texture });
                object afterAdd = GetPrivateField(window, "_sceneComposerPlayback");
                Assert.That(afterAdd, Is.SameAs(before), "Adding a static decoration must not recreate playback/media state.");

                RequireWindowMethod("ComposerSetSelectedDecorationOpacity", typeof(float)).Invoke(window, new object[] { .7f });
                object afterEdit = GetPrivateField(window, "_sceneComposerPlayback");
                Assert.That(afterEdit, Is.SameAs(before), "Decoration property edits must not reset active playback.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MF3_CustomPlaqueAndCharacterCompositionRemainIntact()
        {
            Texture2D panel = AssetDatabase.LoadAssetAtPath<Texture2D>(DecorationPath);
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            scene.characters.Add(new VnSceneComposerCharacter
            {
                characterId = "Mina",
                stateId = "mina_neutral",
                stageSlot = VnWorkshopStageSlot.Center
            });
            SetPanelOverride(scene.presentationOverrides, DecorationGuid);
            object decoration = CreateDecoration(DecorationGuid, DecorationId);
            GetDecorations(scene).Add(decoration);

            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                project, scene, VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);

            Assert.That(frame.DialoguePanelTexture, Is.SameAs(panel));
            Assert.That(frame.ComposerCharacters, Is.Not.Null);
            Assert.That(frame.ComposerCharacters.Length, Is.EqualTo(1));
            Assert.That(frame.ComposerCharacters[0].StateId, Is.EqualTo("mina_neutral"));
            Assert.That(GetFrameCollection(frame, "ComposerDecorations").Count, Is.EqualTo(1));
        }

        [Test]
        public void MF3_BasicUiUsesFriendlyDecorationControlsAndExistingUiOverlayOnboarding()
        {
            string sourcePath = Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposer.cs");
            string source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Contain("\"Декорации\"")
                .And.Contain("\"Добавить PNG\"")
                .And.Contain("\"Прозрачность\"")
                .And.Contain("\"Показывать\""));
            Assert.That(source, Does.Not.Contain("\"assetGuid\""));

            string authoringPath = Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string authoring = File.ReadAllText(authoringPath);
            Assert.That(authoring, Does.Contain("VnSceneComposerAssetPurpose.UiOverlay"),
                "M-F3 must reuse the established UiOverlay onboarding path instead of creating another importer.");
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                bool hex = (ch >= '0' && ch <= '9') ||
                           (ch >= 'a' && ch <= 'f') ||
                           (ch >= 'A' && ch <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        private static Type RequireType(string simpleName)
        {
            Type type = typeof(VnSceneComposerScene).Assembly.GetType(
                "Rokas.EditorTools.VnUiWorkshop." + simpleName, false);
            Assert.That(type, Is.Not.Null, "Missing M-F3 type: " + simpleName);
            return type;
        }

        private static object CreateDecoration(string guid, string id)
        {
            Type type = RequireType("VnSceneComposerDecoration");
            object value = Activator.CreateInstance(type);
            SetField(value, "decorationId", id);
            SetField(value, "assetGuid", guid);
            SetField(value, "displayName", "Decoration");
            SetField(value, "position", new Vector2(960f, 540f));
            SetField(value, "scale", 1f);
            SetField(value, "opacity", 1f);
            SetField(value, "visible", true);
            SetEnumField(value, "layer", "BehindCharacters");
            return value;
        }

        private static IList GetDecorations(VnSceneComposerScene scene)
        {
            FieldInfo field = typeof(VnSceneComposerScene).GetField(
                "decorations", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Scene must own a decorations collection.");
            IList list = field.GetValue(scene) as IList;
            Assert.That(list, Is.Not.Null, "Scene decorations collection must be initialized.");
            return list;
        }

        private static IList GetFrameCollection(VnWorkshopPreviewFrame frame, string propertyName)
        {
            Assert.That(frame, Is.Not.Null);
            PropertyInfo property = typeof(VnWorkshopPreviewFrame).GetProperty(
                propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing frame property: " + propertyName);
            object value = property.GetValue(frame, null);
            Assert.That(value, Is.Not.Null);
            if (value is IList list) return list;
            Array array = value as Array;
            Assert.That(array, Is.Not.Null, propertyName + " must be an array/list.");
            return array;
        }

        private static void SetPanelOverride(VnPresentationWorkshopPreset preset, string guid)
        {
            object visual = typeof(VnPresentationWorkshopPreset)
                .GetField("dialoguePanelVisual", BindingFlags.Public | BindingFlags.Instance)
                .GetValue(preset);
            SetField(visual, "hasAssetGuid", true);
            SetField(visual, "assetGuid", guid);
        }

        private static MethodInfo RequireWindowMethod(string name, params Type[] parameters)
        {
            MethodInfo method = typeof(VnPresentationWorkshopWindow).GetMethod(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameters ?? Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing M-F3 authoring method: " + name);
            return method;
        }

        private static VnSceneComposerProject GetProject(VnPresentationWorkshopWindow window)
        {
            return (VnSceneComposerProject)GetPrivateField(window, "_sceneComposerProject");
        }

        private static object GetPrivateField(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(
                name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing private field: " + name);
            return field.GetValue(instance);
        }

        private static object GetField(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            return field.GetValue(instance);
        }

        private static object GetProperty(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            PropertyInfo property = instance.GetType().GetProperty(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property: " + instance.GetType().Name + "." + name);
            return property.GetValue(instance, null);
        }

        private static void SetField(object instance, string name, object value)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, value);
        }

        private static void SetEnumField(object instance, string name, string enumName)
        {
            FieldInfo field = instance.GetType().GetField(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing enum field: " + name);
            field.SetValue(instance, Enum.Parse(field.FieldType, enumName));
        }

        private static void AssertField(Type type, string name)
        {
            Assert.That(type.GetField(name, BindingFlags.Public | BindingFlags.Instance), Is.Not.Null,
                "Missing M-F3 field: " + type.Name + "." + name);
        }
    }
}
