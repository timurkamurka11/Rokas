using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerImageMediaTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string KnownBackgroundPath = "Assets/Rokas/Art/VN/Backgrounds/VN_BusStop_Rain_Night.png";
        private const string KnownBackgroundGuid = "88761c34d7af479ea57efce7073ff8d4";

        [Test]
        public void ExistingRokasAssetSelectionStoresGuidAndResolvesOriginalTexture()
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            Type mediaType = RequireType("VnSceneComposerMediaReference");
            Type scaleModeType = RequireType("VnSceneComposerMediaScaleMode");
            Type editingType = RequireType("VnSceneComposerMediaEditing");
            Type previewType = RequireType("VnSceneComposerImagePreview");
            object scene = Activator.CreateInstance(sceneType);
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(KnownBackgroundPath);
            Assert.That(source, Is.Not.Null, "Known ROKAS VN background must exist for Composer media tests.");
            Assert.That(AssetDatabase.AssetPathToGUID(KnownBackgroundPath), Is.EqualTo(KnownBackgroundGuid));

            object fit = Enum.Parse(scaleModeType, "Fit");
            MethodInfo select = RequireStatic(editingType, "SetExistingRokasAsset", sceneType, typeof(UnityEngine.Object), scaleModeType);
            select.Invoke(null, new[] { scene, source, fit });

            object media = Get(scene, "media");
            Assert.That(Get(media, "kind").ToString(), Is.EqualTo("ExistingRokasAsset"));
            Assert.That(GetString(media, "reference"), Is.EqualTo(KnownBackgroundGuid));
            Assert.That(GetString(media, "displayName"), Is.EqualTo(source.name));
            Assert.That((bool)Get(media, "localPreviewDependency"), Is.False);
            Assert.That(Get(media, "scaleMode").ToString(), Is.EqualTo("Fit"));
            Assert.That(AssetDatabase.GUIDToAssetPath(KnownBackgroundGuid), Is.EqualTo(KnownBackgroundPath));

            MethodInfo open = RequireStatic(editingType, "OpenImagePreview", mediaType);
            object preview = open.Invoke(null, new[] { media });
            Assert.That(preview, Is.Not.Null);
            Assert.That(Get(preview, "texture"), Is.SameAs(source));
            Assert.That(GetString(preview, "warning"), Is.Empty);
            Assert.That((bool)Get(preview, "ownsTexture"), Is.False);
            RequireInstance(previewType, "Dispose").Invoke(preview, null);
            Assert.That(AssetDatabase.AssetPathToGUID(KnownBackgroundPath), Is.EqualTo(KnownBackgroundGuid));
        }

        [Test]
        public void ExternalPngSelectionIsLocalPreviewDependencyAndMissingFileWarns()
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            Type mediaType = RequireType("VnSceneComposerMediaReference");
            Type scaleModeType = RequireType("VnSceneComposerMediaScaleMode");
            Type editingType = RequireType("VnSceneComposerMediaEditing");
            Type previewType = RequireType("VnSceneComposerImagePreview");
            object scene = Activator.CreateInstance(sceneType);
            string path = Path.Combine(Path.GetTempPath(), "rokas-vn-composer-sc-c-" + Guid.NewGuid().ToString("N") + ".png");
            Texture2D generated = new Texture2D(4, 2, TextureFormat.RGBA32, false);
            try
            {
                generated.SetPixels(Enumerable.Repeat(Color.magenta, 8).ToArray());
                generated.Apply();
                File.WriteAllBytes(path, generated.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(generated);
                generated = null;

                object fill = Enum.Parse(scaleModeType, "Fill");
                MethodInfo select = RequireStatic(editingType, "SetExternalImage", sceneType, typeof(string), scaleModeType);
                select.Invoke(null, new[] { scene, path, fill });
                object media = Get(scene, "media");

                Assert.That(Get(media, "kind").ToString(), Is.EqualTo("ExternalImage"));
                Assert.That(GetString(media, "reference"), Is.EqualTo(Path.GetFullPath(path)));
                Assert.That((bool)Get(media, "localPreviewDependency"), Is.True);
                Assert.That(GetString(media, "contentHash"), Is.Not.Empty);
                Assert.That(Get(media, "scaleMode").ToString(), Is.EqualTo("Fill"));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Empty);

                MethodInfo open = RequireStatic(editingType, "OpenImagePreview", mediaType);
                object preview = open.Invoke(null, new[] { media });
                Texture2D texture = (Texture2D)Get(preview, "texture");
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.width, Is.EqualTo(4));
                Assert.That(texture.height, Is.EqualTo(2));
                Assert.That(GetString(preview, "warning"), Is.Empty);
                Assert.That((bool)Get(preview, "ownsTexture"), Is.True);
                RequireInstance(previewType, "Dispose").Invoke(preview, null);

                File.Delete(path);
                object missing = open.Invoke(null, new[] { media });
                Assert.That(Get(missing, "texture"), Is.Null);
                Assert.That(GetString(missing, "warning"), Does.Contain("missing").IgnoreCase);
                RequireInstance(previewType, "Dispose").Invoke(missing, null);
            }
            finally
            {
                if (generated != null) UnityEngine.Object.DestroyImmediate(generated);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void ImageMediaContractSupportsFitAndFillWithoutProductionImport()
        {
            Type scaleModeType = RequireType("VnSceneComposerMediaScaleMode");
            Assert.That(scaleModeType.IsEnum, Is.True);
            string[] names = Enum.GetNames(scaleModeType);
            Assert.That(names, Does.Contain("Fit"));
            Assert.That(names, Does.Contain("Fill"));

            Type mediaType = RequireType("VnSceneComposerMediaReference");
            FieldInfo scaleMode = RequirePublicField(mediaType, "scaleMode");
            Assert.That(scaleMode.FieldType, Is.EqualTo(scaleModeType));
            Type editingType = RequireType("VnSceneComposerMediaEditing");
            RequireStatic(editingType, "SetExistingRokasAsset", RequireType("VnSceneComposerScene"), typeof(UnityEngine.Object), scaleModeType);
            RequireStatic(editingType, "SetExternalImage", RequireType("VnSceneComposerScene"), typeof(string), scaleModeType);
            RequireStatic(editingType, "OpenImagePreview", mediaType);
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer type: " + shortName);
            return type;
        }

        private static FieldInfo RequirePublicField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + type.Name + "." + name);
            return field;
        }

        private static MethodInfo RequireStatic(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing public static method: " + type.Name + "." + name);
            return method;
        }

        private static MethodInfo RequireInstance(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing public instance method: " + type.Name + "." + name);
            return method;
        }

        private static FieldInfo Field(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + instance.GetType().Name + "." + name);
            return field;
        }

        private static object Get(object instance, string name) { return Field(instance, name).GetValue(instance); }
        private static string GetString(object instance, string name) { return (string)Get(instance, name); }
    }
}
