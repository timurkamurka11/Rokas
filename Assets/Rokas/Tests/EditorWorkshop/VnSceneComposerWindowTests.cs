using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerWindowTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string KnownBackgroundPath = "Assets/Rokas/Art/VN/Backgrounds/VN_BusStop_Rain_Night.png";
        private const string KnownBackgroundGuid = "88761c34d7af479ea57efce7073ff8d4";

        [Test]
        public void SceneComposerWorkspaceIsHostedByExistingWorkshopWindow()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Assert.That(typeof(EditorWindow).IsAssignableFrom(windowType), Is.True);
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ActivateSceneComposerWorkspace").Invoke(window, null);
                Assert.That((bool)GetField(window, "_sceneComposerWorkspaceActive"), Is.True);
                RequireInstance(windowType, "DrawSceneComposerWorkspace");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SceneCrudWiringPreservesStableIdsAndDuplicateGetsNewId()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                MethodInfo add = RequireInstance(windowType, "ComposerAddScene");
                MethodInfo rename = RequireInstance(windowType, "ComposerRenameSelectedScene", typeof(string));
                MethodInfo duplicate = RequireInstance(windowType, "ComposerDuplicateSelectedScene");
                MethodInfo move = RequireInstance(windowType, "ComposerMoveSelectedScene", typeof(int));
                MethodInfo delete = RequireInstance(windowType, "ComposerDeleteSelectedScene");

                add.Invoke(window, null);
                rename.Invoke(window, new object[] { "Intro" });
                IList scenes = Scenes(window);
                Assert.That(scenes.Count, Is.EqualTo(1));
                string originalId = GetString(scenes[0], "sceneId");
                Assert.That(GetString(scenes[0], "label"), Is.EqualTo("Intro"));

                duplicate.Invoke(window, null);
                scenes = Scenes(window);
                Assert.That(scenes.Count, Is.EqualTo(2));
                string duplicateId = GetString(scenes[1], "sceneId");
                Assert.That(duplicateId, Is.Not.EqualTo(originalId));

                move.Invoke(window, new object[] { -1 });
                scenes = Scenes(window);
                Assert.That(GetString(scenes[0], "sceneId"), Is.EqualTo(duplicateId));
                Assert.That(GetString(scenes[1], "sceneId"), Is.EqualTo(originalId));

                delete.Invoke(window, null);
                scenes = Scenes(window);
                Assert.That(scenes.Count, Is.EqualTo(1));
                Assert.That(GetString(scenes[0], "sceneId"), Is.EqualTo(originalId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ExistingMediaSelectionUpdatesSelectedSceneWithoutGuidTyping()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KnownBackgroundPath);
                Assert.That(texture, Is.Not.Null);
                Assert.That(AssetDatabase.AssetPathToGUID(KnownBackgroundPath), Is.EqualTo(KnownBackgroundGuid));

                RequireInstance(windowType, "ComposerSetExistingRokasAsset", typeof(UnityEngine.Object))
                    .Invoke(window, new object[] { texture });

                object media = GetField(Scenes(window)[0], "media");
                Assert.That(GetField(media, "kind").ToString(), Is.EqualTo("ExistingRokasAsset"));
                Assert.That(GetString(media, "reference"), Is.EqualTo(KnownBackgroundGuid));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SelectedSceneDrivesExistingRendererPreviewFrame()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                object scene = Scenes(window)[0];
                SetField(scene, "speaker", "Mina");
                SetField(scene, "previewText", "Composer selected scene preview");

                object frame = RequireInstance(windowType, "ComposerBuildSelectedPreviewFrame").Invoke(window, null);
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.GetType().Name, Is.EqualTo("VnWorkshopPreviewFrame"));
                Assert.That(GetProperty(frame, "Speaker"), Is.EqualTo("Mina"));
                Assert.That(GetProperty(frame, "Dialogue"), Is.EqualTo("Composer selected scene preview"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TransportRoutesToRealSceneComposerPlaybackController()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Type playbackType = RequireType("VnSceneComposerPlaybackController");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                RequireInstance(windowType, "ComposerSelectScene", typeof(int)).Invoke(window, new object[] { 1 });

                RequireInstance(windowType, "ComposerPlayScene").Invoke(window, null);
                object playback = GetField(window, "_sceneComposerPlayback");
                Assert.That(playback, Is.Not.Null);
                Assert.That(playback.GetType(), Is.EqualTo(playbackType));
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(1));
                Assert.That(GetProperty(playback, "IsPlaying"), Is.True);

                RequireInstance(windowType, "ComposerPause").Invoke(window, null);
                Assert.That(GetProperty(playback, "IsPaused"), Is.True);
                RequireInstance(windowType, "ComposerRestart").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(1));

                RequireInstance(windowType, "ComposerPlayAll").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(0));
                RequireInstance(windowType, "ComposerNext").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(1));
                RequireInstance(windowType, "ComposerPrevious").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(0));

                RequireInstance(windowType, "ComposerSelectScene", typeof(int)).Invoke(window, new object[] { 1 });
                RequireInstance(windowType, "ComposerPlayFromHere").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(1));
                Assert.That(GetProperty(playback, "IsPlaying"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SaveAndLoadRouteThroughSceneComposerStorage()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            string root = Path.Combine(Path.GetTempPath(), "rokas-sc-i-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                RequireInstance(windowType, "ComposerRenameSelectedScene", typeof(string)).Invoke(window, new object[] { "Stored Scene" });
                object project = GetField(window, "_sceneComposerProject");
                string projectId = GetString(project, "projectId");

                RequireInstance(windowType, "ComposerSaveProject", typeof(string)).Invoke(window, new object[] { root });
                string expected = Path.Combine(root, "Library", "ROKAS", "VnSceneComposer", "projects", projectId,
                    "ROKAS_VN_SCENE_COMPOSER_PROJECT.json");
                Assert.That(File.Exists(expected), Is.True);
                Assert.That(Directory.Exists(Path.Combine(root, "Assets")), Is.False);

                RequireInstance(windowType, "ComposerDeleteSelectedScene").Invoke(window, null);
                Assert.That(Scenes(window).Count, Is.EqualTo(0));

                object result = RequireInstance(windowType, "ComposerLoadProject", typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, projectId });
                Assert.That(GetProperty(result, "Success"), Is.True, GetProperty(result, "Error") as string);
                Assert.That(GetString(GetField(window, "_sceneComposerProject"), "projectId"), Is.EqualTo(projectId));
                Assert.That(Scenes(window).Count, Is.EqualTo(1));
                Assert.That(GetString(Scenes(window)[0], "label"), Is.EqualTo("Stored Scene"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void AuthoredCharacterPickerOnlyReturnsCatalogStatesAndAddCharacterUsesSelectedState()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                string[] stateIds = (string[])RequireInstance(windowType, "ComposerGetAuthoredStateIds", typeof(string))
                    .Invoke(window, new object[] { "Mina" });
                Assert.That(stateIds, Is.Not.Null.And.Not.Empty);
                foreach (string stateId in stateIds)
                {
                    Assert.That(VnCharacterVisualCatalog.TryResolve(stateId, out VnCharacterVisualState state), Is.True);
                    Assert.That(state.Character, Is.EqualTo("Mina").IgnoreCase);
                }

                RequireInstance(windowType, "ComposerAddCharacter", typeof(string), typeof(string))
                    .Invoke(window, new object[] { "Mina", stateIds[0] });
                IList characters = (IList)GetField(Scenes(window)[0], "characters");
                Assert.That(characters.Count, Is.EqualTo(1));
                Assert.That(GetString(characters[0], "characterId"), Is.EqualTo("Mina"));
                Assert.That(GetString(characters[0], "stateId"), Is.EqualTo(stateIds[0]));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SceneThumbnailUsesCachedRepresentativeTextureForExistingAsset()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KnownBackgroundPath);
                RequireInstance(windowType, "ComposerSetExistingRokasAsset", typeof(UnityEngine.Object))
                    .Invoke(window, new object[] { texture });
                MethodInfo thumbnail = RequireInstance(windowType, "ComposerGetSceneThumbnail", typeof(int));
                object first = thumbnail.Invoke(window, new object[] { 0 });
                object second = thumbnail.Invoke(window, new object[] { 0 });
                Assert.That(first, Is.SameAs(second));
                Assert.That(first, Is.SameAs(texture));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DeleteReorderAndMajorEditAreRecoverableWithUnityUndo()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                MethodInfo add = RequireInstance(windowType, "ComposerAddScene");
                MethodInfo select = RequireInstance(windowType, "ComposerSelectScene", typeof(int));
                MethodInfo rename = RequireInstance(windowType, "ComposerRenameSelectedScene", typeof(string));
                MethodInfo move = RequireInstance(windowType, "ComposerMoveSelectedScene", typeof(int));
                MethodInfo delete = RequireInstance(windowType, "ComposerDeleteSelectedScene");
                add.Invoke(window, null);
                add.Invoke(window, null);
                add.Invoke(window, null);
                Undo.ClearAll();

                select.Invoke(window, new object[] { 1 });
                string beforeRename = GetString(Scenes(window)[1], "label");
                rename.Invoke(window, new object[] { "Renamed" });
                Undo.FlushUndoRecordObjects();
                Assert.That(GetString(Scenes(window)[1], "label"), Is.EqualTo("Renamed"));
                Undo.PerformUndo();
                Assert.That(GetString(Scenes(window)[1], "label"), Is.EqualTo(beforeRename));

                string[] orderBefore = SceneIds(window);
                select.Invoke(window, new object[] { 1 });
                move.Invoke(window, new object[] { -1 });
                Undo.FlushUndoRecordObjects();
                Assert.That(SceneIds(window), Is.Not.EqualTo(orderBefore));
                Undo.PerformUndo();
                Assert.That(SceneIds(window), Is.EqualTo(orderBefore));

                select.Invoke(window, new object[] { 1 });
                delete.Invoke(window, null);
                Undo.FlushUndoRecordObjects();
                Assert.That(Scenes(window).Count, Is.EqualTo(2));
                Undo.PerformUndo();
                Assert.That(Scenes(window).Count, Is.EqualTo(3));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ComposerEditorSurfaceHasNoProductionApplyOrYarnWritePath()
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == EditorAssembly);
            string[] forbiddenMethodNames = assembly.GetTypes()
                .Where(type => type.Namespace != null && type.Namespace.StartsWith(Namespace.TrimEnd('.'), StringComparison.Ordinal))
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                .Select(method => method.Name)
                .Where(name => name.IndexOf("ApplyToProduction", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("GenerateYarn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("WriteYarn", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            Assert.That(forbiddenMethodNames, Is.Empty);
        }

        private static UnityEngine.Object CreateWindow(Type windowType)
        {
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            Assert.That(window, Is.Not.Null);
            return window;
        }

        private static IList Scenes(object window)
        {
            object project = GetField(window, "_sceneComposerProject");
            Assert.That(project, Is.Not.Null);
            IList scenes = (IList)GetField(project, "scenes");
            Assert.That(scenes, Is.Not.Null);
            return scenes;
        }

        private static string[] SceneIds(object window)
        {
            return Scenes(window).Cast<object>().Select(scene => GetString(scene, "sceneId")).ToArray();
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer/Workshop type: " + shortName);
            return type;
        }

        private static MethodInfo RequireInstance(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameters ?? Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing Scene Composer window method: " + type.Name + "." + name);
            return method;
        }

        private static object GetField(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            return field.GetValue(instance);
        }

        private static void SetField(object instance, string name, object value)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, value);
        }

        private static object GetProperty(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            PropertyInfo property = instance.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property: " + instance.GetType().Name + "." + name);
            return property.GetValue(instance, null);
        }

        private static string GetString(object instance, string name)
        {
            return (string)GetField(instance, name);
        }
    }
}
