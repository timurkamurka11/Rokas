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
    public sealed class VnSceneComposerTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string ExpectedSourceHead = "f58f1db08fc225c2831ff59d50d42e8c07ea15ce";
        private const string ManagedRoot = "Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets";

        [Test]
        public void ComposerContractPinsVerifiedVn10SourceAndStartsAtSchemaOne()
        {
            Type contract = RequireType("VnSceneComposerContract");
            FieldInfo schemaVersion = RequirePublicStaticField(contract, "SchemaVersion");
            FieldInfo sourceHead = RequirePublicStaticField(contract, "SourceHead");

            Assert.That(schemaVersion.GetRawConstantValue(), Is.EqualTo(1));
            Assert.That(sourceHead.GetRawConstantValue(), Is.EqualTo(ExpectedSourceHead));
        }

        [Test]
        public void ComposerProjectAndSceneModelsExposeStableIdentityAndAuthoringData()
        {
            Type project = RequireType("VnSceneComposerProject");
            RequirePublicInstanceField(project, "schemaVersion");
            RequirePublicInstanceField(project, "projectId");
            RequirePublicInstanceField(project, "title");
            RequirePublicInstanceField(project, "sourceHead");
            RequirePublicInstanceField(project, "defaultPresentation");
            RequirePublicInstanceField(project, "scenes");

            Type scene = RequireType("VnSceneComposerScene");
            RequirePublicInstanceField(scene, "sceneId");
            RequirePublicInstanceField(scene, "label");
            RequirePublicInstanceField(scene, "media");
            RequirePublicInstanceField(scene, "characters");
            RequirePublicInstanceField(scene, "speaker");
            RequirePublicInstanceField(scene, "previewText");
            RequirePublicInstanceField(scene, "narration");
            RequirePublicInstanceField(scene, "presentationOverrides");
            RequirePublicInstanceField(scene, "transition");
            RequirePublicInstanceField(scene, "timing");
        }

        [Test]
        public void ComposerMediaKindsDistinguishRepositoryAssetsFromLocalPreviewDependencies()
        {
            Type mediaKind = RequireType("VnSceneComposerMediaKind");
            Assert.That(mediaKind.IsEnum, Is.True);
            string[] names = Enum.GetNames(mediaKind);
            Assert.That(names, Does.Contain("None"));
            Assert.That(names, Does.Contain("ExistingRokasAsset"));
            Assert.That(names, Does.Contain("ExternalImage"));
            Assert.That(names, Does.Contain("ExternalVideo"));
            Assert.That(names, Does.Contain("ExternalGif"));

            Type media = RequireType("VnSceneComposerMediaReference");
            RequirePublicInstanceField(media, "kind");
            RequirePublicInstanceField(media, "reference");
            RequirePublicInstanceField(media, "displayName");
            RequirePublicInstanceField(media, "contentHash");
            RequirePublicInstanceField(media, "localPreviewDependency");
        }

        [Test]
        public void ComposerReusesExistingVn10PresetTypeForProjectDefaultsAndSceneOverrides()
        {
            Type preset = RequireType("VnPresentationWorkshopPreset");
            Type project = RequireType("VnSceneComposerProject");
            Type scene = RequireType("VnSceneComposerScene");

            Assert.That(RequirePublicInstanceField(project, "defaultPresentation").FieldType, Is.EqualTo(preset));
            Assert.That(RequirePublicInstanceField(scene, "presentationOverrides").FieldType, Is.EqualTo(preset));
        }

        [Test]
        public void ComposerCharacterModelUsesAuthoredStateIdAndExistingStageSlot()
        {
            Type character = RequireType("VnSceneComposerCharacter");
            RequirePublicInstanceField(character, "characterId");
            RequirePublicInstanceField(character, "stateId");
            FieldInfo stageSlot = RequirePublicInstanceField(character, "stageSlot");
            Assert.That(stageSlot.FieldType, Is.EqualTo(RequireType("VnWorkshopStageSlot")));
        }

        [Test]
        public void AssetLibraryContractIsPurposeAwarePortableAndDoesNotRequireManualGuids()
        {
            Type purpose = RequireType("VnSceneComposerAssetPurpose");
            CollectionAssert.IsSubsetOf(
                new[] { "CharacterState", "Background", "ImageStill", "UiOverlay", "ReferenceImage" },
                Enum.GetNames(purpose));

            Type entry = RequireType("VnSceneComposerAssetEntry");
            foreach (string field in new[]
            {
                "stableAssetId", "purpose", "displayName", "assetPath", "assetGuid", "contentHash",
                "character", "stateName", "stateId", "missing", "warning", "managedByComposer", "productionReadOnly"
            }) RequirePublicInstanceField(entry, field);

            Type catalog = RequireType("VnSceneComposerAssetCatalog");
            RequirePublicInstanceField(catalog, "schemaVersion");
            RequirePublicInstanceField(catalog, "entries");

            Type library = RequireType("VnSceneComposerAssetLibrary");
            RequirePublicStaticField(library, "ManagedRootRelative");
            RequirePublicStaticMethod(library, "Onboard", typeof(string), typeof(string), purpose,
                typeof(string), typeof(string), typeof(string));
            RequirePublicStaticMethod(library, "Refresh", typeof(string));
            RequirePublicStaticMethod(library, "SerializeCatalog", typeof(string));
            RequirePublicStaticMethod(library, "FindCharacterStates", typeof(string), typeof(string));
            RequirePublicStaticMethod(library, "RegisterExistingProjectAsset", typeof(string), typeof(string), purpose,
                typeof(string), typeof(string), typeof(string));
            RequirePublicStaticMethod(library, "Unregister", typeof(string), typeof(string));
        }

        [Test]
        public void OnboardTransparentCharacterPngCopiesExactBytesPreservesAlphaAndAppearsAfterRefresh()
        {
            Type purpose = RequireType("VnSceneComposerAssetPurpose");
            Type library = RequireType("VnSceneComposerAssetLibrary");
            string projectRoot = ProjectRoot();
            string source = CreateTransparentPng();
            object result = null;
            string assetPath = null;
            string stableId = null;
            try
            {
                result = InvokeStatic(library, "Onboard",
                    new[] { typeof(string), typeof(string), purpose, typeof(string), typeof(string), typeof(string) },
                    projectRoot, source, Enum.Parse(purpose, "CharacterState"), "TDD Soft Smile", "Mina", "tdd_soft_smile");
                Assert.That((bool)Get(result, "Success"), Is.True, Get(result, "Error") as string);
                Assert.That((bool)Get(result, "Duplicate"), Is.False);
                object entry = Get(result, "Entry");
                Assert.That(entry, Is.Not.Null);
                assetPath = (string)Get(entry, "assetPath");
                stableId = (string)Get(entry, "stableAssetId");
                Assert.That(Path.IsPathRooted(assetPath), Is.False);
                Assert.That(assetPath.Replace('\\', '/'), Does.StartWith(ManagedRoot + "/"));
                CollectionAssert.AreEqual(File.ReadAllBytes(source), File.ReadAllBytes(Path.Combine(projectRoot, assetPath)));

                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath), Is.Not.Null);

                InvokeStatic(library, "Refresh", new[] { typeof(string) }, projectRoot);
                object states = InvokeStatic(library, "FindCharacterStates",
                    new[] { typeof(string), typeof(string) }, projectRoot, "Mina");
                Assert.That(((IEnumerable)states).Cast<object>().Any(item =>
                    string.Equals((string)Get(item, "stateId"), "onboarded:mina:tdd_soft_smile", StringComparison.Ordinal)), Is.True,
                    "Refresh must make the newly onboarded authored state discoverable without GUID typing.");
            }
            finally
            {
                CleanupAsset(library, projectRoot, stableId, assetPath);
                if (File.Exists(source)) File.Delete(source);
            }
        }

        [Test]
        public void DuplicateDetectionReturnsOriginalIdentityAndNeverCreatesSuffixCopies()
        {
            Type purpose = RequireType("VnSceneComposerAssetPurpose");
            Type library = RequireType("VnSceneComposerAssetLibrary");
            string projectRoot = ProjectRoot();
            string source = CreateTransparentPng();
            string assetPath = null;
            string stableId = null;
            try
            {
                object first = InvokeStatic(library, "Onboard",
                    new[] { typeof(string), typeof(string), purpose, typeof(string), typeof(string), typeof(string) },
                    projectRoot, source, Enum.Parse(purpose, "CharacterState"), "Duplicate Test", "Mina", "tdd_duplicate");
                Assert.That((bool)Get(first, "Success"), Is.True);
                object firstEntry = Get(first, "Entry");
                assetPath = (string)Get(firstEntry, "assetPath");
                stableId = (string)Get(firstEntry, "stableAssetId");

                object second = InvokeStatic(library, "Onboard",
                    new[] { typeof(string), typeof(string), purpose, typeof(string), typeof(string), typeof(string) },
                    projectRoot, source, Enum.Parse(purpose, "CharacterState"), "Duplicate Test", "Mina", "tdd_duplicate");
                Assert.That((bool)Get(second, "Success"), Is.True);
                Assert.That((bool)Get(second, "Duplicate"), Is.True);
                Assert.That((string)Get(Get(second, "Entry"), "stableAssetId"), Is.EqualTo(stableId));
                Assert.That((string)Get(Get(second, "Entry"), "assetPath"), Is.EqualTo(assetPath));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(Path.Combine(projectRoot, assetPath)), "*.png")
                    .Count(path => File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(source))), Is.EqualTo(1));
            }
            finally
            {
                CleanupAsset(library, projectRoot, stableId, assetPath);
                if (File.Exists(source)) File.Delete(source);
            }
        }

        [Test]
        public void CatalogSerializationIsDeterministicPortableAndRefreshMarksMissingAssets()
        {
            Type purpose = RequireType("VnSceneComposerAssetPurpose");
            Type library = RequireType("VnSceneComposerAssetLibrary");
            string projectRoot = ProjectRoot();
            string source = CreateTransparentPng();
            string assetPath = null;
            string stableId = null;
            try
            {
                object result = InvokeStatic(library, "Onboard",
                    new[] { typeof(string), typeof(string), purpose, typeof(string), typeof(string), typeof(string) },
                    projectRoot, source, Enum.Parse(purpose, "CharacterState"), "Portable Test", "Mina", "tdd_portable");
                object entry = Get(result, "Entry");
                assetPath = (string)Get(entry, "assetPath");
                stableId = (string)Get(entry, "stableAssetId");

                string json1 = (string)InvokeStatic(library, "SerializeCatalog", new[] { typeof(string) }, projectRoot);
                string json2 = (string)InvokeStatic(library, "SerializeCatalog", new[] { typeof(string) }, projectRoot);
                Assert.That(json2, Is.EqualTo(json1));
                Assert.That(json1, Does.Not.Contain(projectRoot));
                Assert.That(json1, Does.Not.Contain(Path.GetTempPath()));

                Assert.That(AssetDatabase.DeleteAsset(assetPath), Is.True);
                object catalog = InvokeStatic(library, "Refresh", new[] { typeof(string) }, projectRoot);
                object missing = ((IEnumerable)Get(catalog, "entries")).Cast<object>()
                    .Single(item => string.Equals((string)Get(item, "stableAssetId"), stableId, StringComparison.Ordinal));
                Assert.That((bool)Get(missing, "missing"), Is.True);
                Assert.That((string)Get(missing, "warning"), Does.Contain("missing").IgnoreCase);
            }
            finally
            {
                CleanupAsset(library, projectRoot, stableId, assetPath);
                if (File.Exists(source)) File.Delete(source);
            }
        }

        [Test]
        public void RefreshDiscoversNewManagedImageAsReferenceWithoutUnityRestart()
        {
            Type library = RequireType("VnSceneComposerAssetLibrary");
            string projectRoot = ProjectRoot();
            string folder = ManagedRoot + "/ReferenceImage";
            string assetPath = folder + "/tdd_refresh_discovery_" + Guid.NewGuid().ToString("N") + ".png";
            string source = CreateTransparentPng();
            string stableId = null;
            try
            {
                Directory.CreateDirectory(Path.Combine(projectRoot, folder));
                File.Copy(source, Path.Combine(projectRoot, assetPath), true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                object catalog = InvokeStatic(library, "Refresh", new[] { typeof(string) }, projectRoot);
                object discovered = ((IEnumerable)Get(catalog, "entries")).Cast<object>()
                    .Single(item => string.Equals(((string)Get(item, "assetPath")).Replace('\\', '/'), assetPath, StringComparison.Ordinal));
                stableId = (string)Get(discovered, "stableAssetId");
                Assert.That(Get(discovered, "purpose").ToString(), Is.EqualTo("ReferenceImage"));
                Assert.That((bool)Get(discovered, "missing"), Is.False);
            }
            finally
            {
                CleanupAsset(library, projectRoot, stableId, assetPath);
                if (File.Exists(source)) File.Delete(source);
            }
        }

        [Test]
        public void ExistingRokasProductionAssetIsReadOnlyAndUnregisterNeverDeletesIt()
        {
            const string productionPath = "Assets/Rokas/Art/VN/Backgrounds/VN_BusStop_Rain_Night.png";
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(productionPath), Is.Not.Null,
                "Known production ROKAS background fixture is required for this read-only proof.");

            Type purpose = RequireType("VnSceneComposerAssetPurpose");
            Type library = RequireType("VnSceneComposerAssetLibrary");
            string projectRoot = ProjectRoot();
            object result = InvokeStatic(library, "RegisterExistingProjectAsset",
                new[] { typeof(string), typeof(string), purpose, typeof(string), typeof(string), typeof(string) },
                projectRoot, productionPath, Enum.Parse(purpose, "Background"), "Existing Bus Stop", string.Empty, string.Empty);
            Assert.That((bool)Get(result, "Success"), Is.True);
            object entry = Get(result, "Entry");
            string stableId = (string)Get(entry, "stableAssetId");
            try
            {
                Assert.That((bool)Get(entry, "productionReadOnly"), Is.True);
                Assert.That((bool)Get(entry, "managedByComposer"), Is.False);
                bool unregistered = (bool)InvokeStatic(library, "Unregister", new[] { typeof(string), typeof(string) }, projectRoot, stableId);
                Assert.That(unregistered, Is.True);
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(productionPath), Is.Not.Null,
                    "Unregister must remove Composer metadata only and must never delete production assets.");
            }
            finally
            {
                InvokeStatic(library, "Unregister", new[] { typeof(string), typeof(string) }, projectRoot, stableId);
            }
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string CreateTransparentPng()
        {
            string path = Path.Combine(Path.GetTempPath(), "rokas-vn-asset-tdd-" + Guid.NewGuid().ToString("N") + ".png");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[]
            {
                new Color(1f, 0f, 0f, 0f), new Color(0f, 1f, 0f, .35f),
                new Color(0f, 0f, 1f, .75f), new Color(1f, 1f, 1f, 1f)
            });
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return path;
        }

        private static void CleanupAsset(Type library, string projectRoot, string stableId, string assetPath)
        {
            if (library != null && !string.IsNullOrEmpty(stableId))
            {
                MethodInfo unregister = library.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string) }, null);
                if (unregister != null) unregister.Invoke(null, new object[] { projectRoot, stableId });
            }
            if (!string.IsNullOrEmpty(assetPath)) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.Refresh();
        }

        private static object InvokeStatic(Type type, string name, Type[] parameters, params object[] args)
        {
            MethodInfo method = RequirePublicStaticMethod(type, name, parameters);
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }

        private static object Get(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null, "Cannot read member '" + name + "' from null.");
            Type type = instance.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return field.GetValue(instance);
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, type.Name + " is missing member " + name + ".");
            return property.GetValue(instance, null);
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(candidate => candidate.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null, "Expected editor assembly '" + EditorAssembly + "'.");
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer contract type: " + Namespace + shortName + ".");
            return type;
        }

        private static FieldInfo RequirePublicStaticField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, type.Name + " must expose public static field " + name + ".");
            return field;
        }

        private static MethodInfo RequirePublicStaticMethod(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            Assert.That(method, Is.Not.Null, type.Name + " must expose public static method " + name + ".");
            return method;
        }

        private static FieldInfo RequirePublicInstanceField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, type.Name + " must expose public instance field " + name + ".");
            return field;
        }
    }
}
