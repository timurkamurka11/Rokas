using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerPersistenceTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string FixedProjectId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string SceneOneId = "11111111111111111111111111111111";
        private const string SceneTwoId = "22222222222222222222222222222222";

        [Test]
        public void PortableRoundTripIsDeterministicPreservesOrderAndDoesNotLeakAbsoluteExternalPaths()
        {
            Type serializationType = RequirePersistenceType();
            Type projectType = RequireType("VnSceneComposerProject");
            object project = Project(projectType, FixedProjectId, "Portable Storyboard");
            object defaults = Get(project, "defaultPresentation");
            object typography = Get(defaults, "typography");
            Set(typography, "hasDialogueFontSize", true);
            Set(typography, "dialogueFontSize", 31f);

            object first = Scene(SceneOneId, "Intro", "Hello from Mina.", "PreviewAutoDuration", 1.75f);
            SetProperty(first, "speaker", "Mina");
            AddCharacter(first, "Mina", "mina_happy", "Left");
            Set(Get(first, "transition"), "triggerActionBounce", true);
            object firstBounce = Get(Get(first, "presentationOverrides"), "actionBounce");
            Set(firstBounce, "hasAmplitude", true);
            Set(firstBounce, "amplitude", 48f);

            object second = Scene(SceneTwoId, "Narration", "Rain crosses the window.\nThen silence.", "ManualBeat", 3f);
            SetProperty(second, "narration", true);
            object media = Get(second, "media");
            string absolute = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "machine-specific", "portable.gif"));
            SetEnum(media, "kind", "ExternalGif");
            Set(media, "reference", absolute);
            Set(media, "displayName", "portable.gif");
            Set(media, "contentHash", "0123456789abcdef");
            Set(media, "localPreviewDependency", true);
            Set(media, "loop", true);

            IList scenes = (IList)Get(project, "scenes");
            scenes.Add(first);
            scenes.Add(second);

            MethodInfo serialize = RequireStatic(serializationType, "SerializePortable", projectType);
            MethodInfo deserialize = RequireStatic(serializationType, "DeserializePortable", typeof(string));
            string json = (string)serialize.Invoke(null, new[] { project });

            Assert.That(json, Does.Contain("\"schemaVersion\""));
            Assert.That(json, Does.Contain("\"sourceHead\""));
            Assert.That(json, Does.Contain("Portable Storyboard"));
            Assert.That(json, Does.Contain("portable.gif"));
            Assert.That(json, Does.Not.Contain(absolute),
                "Portable Scene Composer JSON must not leak a machine-specific absolute external-media path.");
            Assert.That(json, Does.Not.Contain(Path.GetDirectoryName(absolute)));

            object result = deserialize.Invoke(null, new object[] { json });
            Assert.That((bool)Get(result, "Success"), Is.True, (string)Get(result, "Error"));
            object loaded = Get(result, "Project");
            Assert.That(loaded, Is.Not.Null);
            IList loadedScenes = (IList)Get(loaded, "scenes");
            Assert.That(loadedScenes.Count, Is.EqualTo(2));
            Assert.That((string)Get(loadedScenes[0], "sceneId"), Is.EqualTo(SceneOneId));
            Assert.That((string)Get(loadedScenes[1], "sceneId"), Is.EqualTo(SceneTwoId));
            Assert.That((string)Get(loadedScenes[0], "previewText"), Is.EqualTo("Hello from Mina."));
            Assert.That((string)Get(((IList)Get(loadedScenes[0], "characters"))[0], "stateId"), Is.EqualTo("mina_happy"));
            Assert.That(Get(Get(loadedScenes[0], "timing"), "previewAdvanceMode").ToString(), Is.EqualTo("PreviewAutoDuration"));
            Assert.That(Get(Get(loadedScenes[1], "timing"), "previewAdvanceMode").ToString(), Is.EqualTo("ManualBeat"));

            string secondExport = (string)serialize.Invoke(null, new[] { loaded });
            Assert.That(secondExport, Is.EqualTo(json),
                "Project -> Export -> Import -> Export must be deterministic for meaningful portable data.");
        }

        [Test]
        public void MalformedJsonAndUnsupportedSchemaAreRejectedWithClearErrors()
        {
            Type serializationType = RequirePersistenceType();
            Type projectType = RequireType("VnSceneComposerProject");
            MethodInfo serialize = RequireStatic(serializationType, "SerializePortable", projectType);
            MethodInfo deserialize = RequireStatic(serializationType, "DeserializePortable", typeof(string));

            object malformed = deserialize.Invoke(null, new object[] { "{" });
            Assert.That((bool)Get(malformed, "Success"), Is.False);
            Assert.That((string)Get(malformed, "Error"), Does.Contain("parse").IgnoreCase.Or.Contain("malformed").IgnoreCase);

            object project = Project(projectType, FixedProjectId, "Schema");
            string valid = (string)serialize.Invoke(null, new[] { project });
            string unsupported = valid.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 999");
            Assert.That(unsupported, Is.Not.EqualTo(valid), "Fixture must actually change schemaVersion.");
            object unsupportedResult = deserialize.Invoke(null, new object[] { unsupported });
            Assert.That((bool)Get(unsupportedResult, "Success"), Is.False);
            Assert.That((string)Get(unsupportedResult, "Error"), Does.Contain("unsupported").IgnoreCase.And.Contain("schema").IgnoreCase);
        }

        [Test]
        public void DuplicateMissingOrNullSceneEntriesAreRejectedInsteadOfBeingSilentlyNormalized()
        {
            Type serializationType = RequirePersistenceType();
            Type projectType = RequireType("VnSceneComposerProject");
            MethodInfo serialize = RequireStatic(serializationType, "SerializePortable", projectType);

            object duplicateProject = Project(projectType, FixedProjectId, "Duplicates");
            IList duplicateScenes = (IList)Get(duplicateProject, "scenes");
            duplicateScenes.Add(Scene(SceneOneId, "A", "A", "ManualBeat", 1f));
            duplicateScenes.Add(Scene(SceneOneId, "B", "B", "ManualBeat", 1f));
            TargetInvocationException duplicateError = Assert.Throws<TargetInvocationException>(() =>
                serialize.Invoke(null, new[] { duplicateProject }));
            Assert.That(duplicateError.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(duplicateError.InnerException.Message, Does.Contain("duplicate").IgnoreCase.And.Contain("scene").IgnoreCase);

            object missingIdProject = Project(projectType, FixedProjectId, "Missing ID");
            ((IList)Get(missingIdProject, "scenes")).Add(Scene(string.Empty, "No ID", "text", "ManualBeat", 1f));
            TargetInvocationException missingIdError = Assert.Throws<TargetInvocationException>(() =>
                serialize.Invoke(null, new[] { missingIdProject }));
            Assert.That(missingIdError.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(missingIdError.InnerException.Message, Does.Contain("scene").IgnoreCase.And.Contain("id").IgnoreCase);

            object nullSceneProject = Project(projectType, FixedProjectId, "Null Scene");
            ((IList)Get(nullSceneProject, "scenes")).Add(null);
            TargetInvocationException nullSceneError = Assert.Throws<TargetInvocationException>(() =>
                serialize.Invoke(null, new[] { nullSceneProject }));
            Assert.That(nullSceneError.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(nullSceneError.InnerException.Message, Does.Contain("scene").IgnoreCase);
        }

        [Test]
        public void MissingAuthoredCharacterStateIsRejectedDuringPersistenceValidation()
        {
            Type serializationType = RequirePersistenceType();
            Type projectType = RequireType("VnSceneComposerProject");
            MethodInfo serialize = RequireStatic(serializationType, "SerializePortable", projectType);
            object project = Project(projectType, FixedProjectId, "Bad State");
            object scene = Scene(SceneOneId, "Bad", "text", "ManualBeat", 1f);
            AddCharacter(scene, "Mina", "mina_state_that_does_not_exist", "Center");
            ((IList)Get(project, "scenes")).Add(scene);

            TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
                serialize.Invoke(null, new[] { project }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(error.InnerException.Message, Does.Contain("state").IgnoreCase.Or.Contain("authored").IgnoreCase);
        }

        [Test]
        public void InvalidExistingRokasAssetReferenceIsRejectedDuringPersistenceValidation()
        {
            Type serializationType = RequirePersistenceType();
            Type projectType = RequireType("VnSceneComposerProject");
            MethodInfo serialize = RequireStatic(serializationType, "SerializePortable", projectType);
            object project = Project(projectType, FixedProjectId, "Bad Asset");
            object scene = Scene(SceneOneId, "Bad Asset", "text", "ManualBeat", 1f);
            object media = Get(scene, "media");
            SetEnum(media, "kind", "ExistingRokasAsset");
            Set(media, "reference", "00000000000000000000000000000000");
            Set(media, "displayName", "missing-roka-asset");
            ((IList)Get(project, "scenes")).Add(scene);

            TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
                serialize.Invoke(null, new[] { project }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(error.InnerException.Message, Does.Contain("asset").IgnoreCase.Or.Contain("guid").IgnoreCase);
        }

        [Test]
        public void SourceMismatchAndMissingExternalMediaAreWarningsAndPreserveImportedProject()
        {
            Type serializationType = RequirePersistenceType();
            Type projectType = RequireType("VnSceneComposerProject");
            MethodInfo serialize = RequireStatic(serializationType, "SerializePortable", projectType);
            MethodInfo deserialize = RequireStatic(serializationType, "DeserializePortable", typeof(string));
            object project = Project(projectType, FixedProjectId, "Diagnostics");
            object scene = Scene(SceneOneId, "External", "text", "ManualBeat", 1f);
            object media = Get(scene, "media");
            string missingPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rokas-sc-h-missing-" + Guid.NewGuid().ToString("N") + ".mp4"));
            SetEnum(media, "kind", "ExternalVideo");
            Set(media, "reference", missingPath);
            Set(media, "displayName", Path.GetFileName(missingPath));
            Set(media, "contentHash", "fedcba9876543210");
            Set(media, "localPreviewDependency", true);
            ((IList)Get(project, "scenes")).Add(scene);

            string expectedHead = (string)Get(project, "sourceHead");
            string json = (string)serialize.Invoke(null, new[] { project });
            string mismatch = json.Replace(expectedHead, "different-source-head");
            Assert.That(mismatch, Is.Not.EqualTo(json));
            object result = deserialize.Invoke(null, new object[] { mismatch });

            Assert.That((bool)Get(result, "Success"), Is.True,
                "Portable project must remain importable when only local media/source diagnostics differ.");
            Assert.That((bool)Get(result, "SourceHeadMismatch"), Is.True);
            Assert.That(Get(result, "Project"), Is.Not.Null);
            string[] warnings = ToStrings(Get(result, "Warnings"));
            Assert.That(warnings.Any(w => w.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
            Assert.That(warnings.Any(w => w.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          w.IndexOf("external", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
        }

        [Test]
        public void EditorLocalStorageUsesLibraryFixedFilenameAndRoundTripsWithoutAssetsWrites()
        {
            Type serializationType = RequirePersistenceType();
            Assert.That(serializationType, Is.Not.Null);
            Type storageType = RequireType("VnSceneComposerStorage");
            Type projectType = RequireType("VnSceneComposerProject");
            object project = Project(projectType, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "Stored Project");
            ((IList)Get(project, "scenes")).Add(Scene(SceneOneId, "One", "stored", "ManualBeat", 2f));
            ((IList)Get(project, "scenes")).Add(Scene(SceneTwoId, "Two", "stored two", "PreviewAutoDuration", .75f));

            MethodInfo getPath = RequireStatic(storageType, "GetProjectPath", typeof(string), typeof(string));
            MethodInfo save = RequireStatic(storageType, "SaveProject", typeof(string), projectType);
            MethodInfo load = RequireStatic(storageType, "LoadProject", typeof(string), typeof(string));
            string root = Path.Combine(Path.GetTempPath(), "rokas-sc-h-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string path = (string)getPath.Invoke(null, new object[] { root, Get(project, "projectId") });
                string expectedSuffix = Path.Combine("Library", "ROKAS", "VnSceneComposer", "projects",
                    (string)Get(project, "projectId"), "ROKAS_VN_SCENE_COMPOSER_PROJECT.json");
                Assert.That(Path.GetFullPath(path), Is.EqualTo(Path.GetFullPath(Path.Combine(root, expectedSuffix))));

                save.Invoke(null, new[] { root, project });
                Assert.That(File.Exists(path), Is.True);
                Assert.That(Directory.Exists(Path.Combine(root, "Assets")), Is.False,
                    "Scene Composer persistence is editor-local and must never write production Assets.");

                object result = load.Invoke(null, new object[] { root, Get(project, "projectId") });
                Assert.That((bool)Get(result, "Success"), Is.True, (string)Get(result, "Error"));
                object loaded = Get(result, "Project");
                Assert.That((string)Get(loaded, "projectId"), Is.EqualTo(Get(project, "projectId")));
                IList loadedScenes = (IList)Get(loaded, "scenes");
                Assert.That(loadedScenes.Count, Is.EqualTo(2));
                Assert.That((string)Get(loadedScenes[0], "sceneId"), Is.EqualTo(SceneOneId));
                Assert.That((string)Get(loadedScenes[1], "sceneId"), Is.EqualTo(SceneTwoId));

                TargetInvocationException escape = Assert.Throws<TargetInvocationException>(() =>
                    getPath.Invoke(null, new object[] { root, "../../escape" }));
                Assert.That(escape.InnerException, Is.TypeOf<ArgumentException>().Or.TypeOf<InvalidOperationException>());
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void PortableJsonNeverSerializesPlaybackSelectionOrTransientMediaState()
        {
            Type serializationType = RequirePersistenceType();
            Type projectType = RequireType("VnSceneComposerProject");
            MethodInfo serialize = RequireStatic(serializationType, "SerializePortable", projectType);
            object project = Project(projectType, FixedProjectId, "No Transients");
            ((IList)Get(project, "scenes")).Add(Scene(SceneOneId, "Scene", "text", "ManualBeat", 2f));
            string json = (string)serialize.Invoke(null, new[] { project });

            string[] forbidden =
            {
                "SceneElapsedSeconds", "MediaTimeSeconds", "CurrentMediaTexture", "CurrentSceneIndex",
                "currentTexture", "RenderTexture", "VideoPlayer", "selection", "\"hover\"", "\"pressed\"", "foldout"
            };
            foreach (string token in forbidden)
                Assert.That(json, Does.Not.Contain(token), "Transient editor/playback token leaked into persisted JSON: " + token);
        }

        private static object Project(Type projectType, string projectId, string title)
        {
            object project = Activator.CreateInstance(projectType);
            Set(project, "projectId", projectId);
            Set(project, "title", title);
            return project;
        }

        private static object Scene(string id, string label, string text, string timingMode, float duration)
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            object scene = Activator.CreateInstance(sceneType);
            Set(scene, "sceneId", id);
            Set(scene, "label", label);
            SetProperty(scene, "previewText", text);
            object timing = Get(scene, "timing");
            SetEnum(timing, "previewAdvanceMode", timingMode);
            Set(timing, "previewAutoDuration", duration);
            return scene;
        }

        private static void AddCharacter(object scene, string id, string state, string slot)
        {
            Type characterType = RequireType("VnSceneComposerCharacter");
            object character = Activator.CreateInstance(characterType);
            Set(character, "characterId", id);
            Set(character, "stateId", state);
            SetEnum(character, "stageSlot", slot);
            ((IList)Get(scene, "characters")).Add(character);
        }

        private static Type RequirePersistenceType()
        {
            Type type = RequireTypeOrNull("VnSceneComposerSerialization");
            Assert.That(type, Is.Not.Null,
                "Missing Scene Composer persistence type: VnSceneComposerSerialization");
            return type;
        }

        private static Type RequireType(string shortName)
        {
            Type type = RequireTypeOrNull(shortName);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer type: " + shortName);
            return type;
        }

        private static Type RequireTypeOrNull(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            return assembly.GetType(Namespace + shortName, false);
        }

        private static MethodInfo RequireStatic(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing public static method: " + type.Name + "." + name);
            return method;
        }

        private static object Get(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null, "Cannot get '" + name + "' from a null object.");
            Type type = instance.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.GetValue(instance);
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing public field/property: " + type.Name + "." + name);
            return property.GetValue(instance, null);
        }

        private static void SetProperty(object instance, string name, object value)
        {
            PropertyInfo property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing public property: " + instance.GetType().Name + "." + name);
            property.SetValue(instance, value, null);
        }

        private static void Set(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, value);
        }

        private static void SetEnum(object instance, string name, string value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public enum field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, Enum.Parse(field.FieldType, value));
        }

        private static string[] ToStrings(object value)
        {
            if (value == null) return Array.Empty<string>();
            if (value is string[] strings) return strings;
            if (value is IEnumerable enumerable)
                return enumerable.Cast<object>().Select(item => item == null ? string.Empty : item.ToString()).ToArray();
            return new[] { value.ToString() };
        }
    }
}
