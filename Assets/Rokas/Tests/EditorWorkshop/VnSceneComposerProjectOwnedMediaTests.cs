using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerProjectOwnedMediaTests
    {
        private static readonly string ProjectRoot = VnSceneComposerAssetLibrary.GetDefaultProjectRoot();

        [Test]
        public void ImportedBackgroundSurvivesSourceDeletionSaveReopenAndSceneDuplication()
        {
            string source = CreatePng(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png"), Color.cyan);
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            string importedPath = null, stableId = null;
            try
            {
                VnSceneComposerMediaEditing.SetExternalImage(scene, source, VnSceneComposerMediaScaleMode.Fill);
                Assert.That(scene.media.kind, Is.EqualTo(VnSceneComposerMediaKind.ExistingRokasAsset));
                string guid = scene.media.reference;
                importedPath = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(importedPath, Does.StartWith(VnSceneComposerAssetLibrary.ManagedRootRelative + "/Background/"));
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(importedPath), Is.Not.Null);
                Assert.That(File.Exists(Path.Combine(ProjectRoot, importedPath + ".meta")), Is.True);
                Assert.That(File.ReadAllBytes(Path.Combine(ProjectRoot, importedPath)), Is.EqualTo(File.ReadAllBytes(source)));
                stableId = VnSceneComposerAssetLibrary.FindByPurpose(ProjectRoot, VnSceneComposerAssetPurpose.Background)
                    .Single(entry => entry.assetGuid == guid).stableAssetId;

                File.Delete(source);
                VnSceneComposerStorage.SaveProject(ProjectRoot, project);
                VnSceneComposerImportResult loaded = VnSceneComposerStorage.LoadProject(ProjectRoot, project.projectId);
                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(loaded.Project.scenes[0].media.reference, Is.EqualTo(guid));
                Assert.That(loaded.Project.scenes[0].media.kind, Is.EqualTo(VnSceneComposerMediaKind.ExistingRokasAsset));
                VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(loaded.Project, loaded.Project.scenes[0].sceneId);
                Assert.That(copy.media.reference, Is.EqualTo(guid));
                using (VnSceneComposerImagePreview preview = VnSceneComposerMediaEditing.OpenImagePreview(copy.media))
                {
                    Assert.That(preview.texture, Is.Not.Null);
                    Assert.That(preview.warning, Is.Empty);
                }
            }
            finally
            {
                if (File.Exists(source)) File.Delete(source);
                string folder = Path.GetDirectoryName(VnSceneComposerStorage.GetProjectPath(ProjectRoot, project.projectId));
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                if (!string.IsNullOrEmpty(stableId)) VnSceneComposerAssetLibrary.Unregister(ProjectRoot, stableId);
                if (!string.IsNullOrEmpty(importedPath)) AssetDatabase.DeleteAsset(importedPath);
            }
        }

        [Test]
        public void SameContentReusesGuidAndFilenameCollisionKeepsBothAssets()
        {
            string firstDir = Path.Combine(Path.GetTempPath(), "vn-media-" + Guid.NewGuid().ToString("N"));
            string secondDir = Path.Combine(Path.GetTempPath(), "vn-media-" + Guid.NewGuid().ToString("N"));
            string first = CreatePng(Path.Combine(firstDir, "background.png"), Color.red);
            string second = CreatePng(Path.Combine(secondDir, "background.png"), Color.green);
            var a = new VnSceneComposerScene();
            var b = new VnSceneComposerScene();
            var repeated = new VnSceneComposerScene();
            string[] paths = new string[2];
            string[] ids = new string[2];
            try
            {
                VnSceneComposerMediaEditing.SetExternalImage(a, first, VnSceneComposerMediaScaleMode.Fit);
                VnSceneComposerMediaEditing.SetExternalImage(repeated, first, VnSceneComposerMediaScaleMode.Fit);
                VnSceneComposerMediaEditing.SetExternalImage(b, second, VnSceneComposerMediaScaleMode.Fit);
                Assert.That(repeated.media.reference, Is.EqualTo(a.media.reference));
                Assert.That(b.media.reference, Is.Not.EqualTo(a.media.reference));
                paths[0] = AssetDatabase.GUIDToAssetPath(a.media.reference);
                paths[1] = AssetDatabase.GUIDToAssetPath(b.media.reference);
                Assert.That(paths[0], Is.Not.EqualTo(paths[1]));
                Assert.That(File.ReadAllBytes(Path.Combine(ProjectRoot, paths[0])), Is.EqualTo(File.ReadAllBytes(first)));
                Assert.That(File.ReadAllBytes(Path.Combine(ProjectRoot, paths[1])), Is.EqualTo(File.ReadAllBytes(second)));
                foreach (VnSceneComposerAssetEntry entry in VnSceneComposerAssetLibrary.FindByPurpose(ProjectRoot, VnSceneComposerAssetPurpose.Background))
                {
                    if (entry.assetGuid == a.media.reference) ids[0] = entry.stableAssetId;
                    if (entry.assetGuid == b.media.reference) ids[1] = entry.stableAssetId;
                }
            }
            finally
            {
                for (int i = 0; i < 2; i++)
                {
                    if (!string.IsNullOrEmpty(ids[i])) VnSceneComposerAssetLibrary.Unregister(ProjectRoot, ids[i]);
                    if (!string.IsNullOrEmpty(paths[i])) AssetDatabase.DeleteAsset(paths[i]);
                }
                if (Directory.Exists(firstDir)) Directory.Delete(firstDir, true);
                if (Directory.Exists(secondDir)) Directory.Delete(secondDir, true);
            }
        }

        [Test]
        public void WindowUndoRestoresPreviousReferenceWithoutRemovingImportedAsset()
        {
            string source = CreatePng(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png"), Color.blue);
            var window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            string importedPath = null, stableId = null;
            try
            {
                window.ComposerAddScene();
                Undo.ClearAll();
                MethodInfo setImage = typeof(VnPresentationWorkshopWindow).GetMethod(
                    "ComposerSetExternalImage", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(setImage, Is.Not.Null);
                setImage.Invoke(window, new object[] { source });
                Undo.FlushUndoRecordObjects();
                FieldInfo projectField = typeof(VnPresentationWorkshopWindow).GetField(
                    "_sceneComposerProject", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(projectField, Is.Not.Null);
                var project = (VnSceneComposerProject)projectField.GetValue(window);
                string guid = project.scenes[0].media.reference;
                importedPath = AssetDatabase.GUIDToAssetPath(guid);
                stableId = VnSceneComposerAssetLibrary.FindByPurpose(ProjectRoot, VnSceneComposerAssetPurpose.Background)
                    .Single(entry => entry.assetGuid == guid).stableAssetId;

                Undo.PerformUndo();
                project = (VnSceneComposerProject)projectField.GetValue(window);
                Assert.That(project.scenes[0].media.kind, Is.EqualTo(VnSceneComposerMediaKind.None));
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(importedPath));
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(importedPath), Is.Not.Null);
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
                if (!string.IsNullOrEmpty(stableId)) VnSceneComposerAssetLibrary.Unregister(ProjectRoot, stableId);
                if (!string.IsNullOrEmpty(importedPath)) AssetDatabase.DeleteAsset(importedPath);
                if (File.Exists(source)) File.Delete(source);
            }
        }

        [Test]
        public void MigrationCopiesImportedBackgroundAndMetaWithItsSceneReference()
        {
            string sourceImage = CreatePng(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png"), Color.yellow);
            string temp = Path.Combine(Path.GetTempPath(), "vn-migration-" + Guid.NewGuid().ToString("N"));
            string sourceRoot = Path.Combine(temp, "source");
            string destinationRoot = Path.Combine(temp, "destination");
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            string importedPath = null, stableId = null;
            try
            {
                VnSceneComposerMediaEditing.SetExternalImage(scene, sourceImage, VnSceneComposerMediaScaleMode.Fit);
                string guid = scene.media.reference;
                importedPath = AssetDatabase.GUIDToAssetPath(guid);
                stableId = VnSceneComposerAssetLibrary.FindByPurpose(ProjectRoot, VnSceneComposerAssetPurpose.Background)
                    .Single(entry => entry.assetGuid == guid).stableAssetId;
                string relative = importedPath.Replace('/', Path.DirectorySeparatorChar);
                string sourceCopy = Path.Combine(sourceRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(sourceCopy));
                Directory.CreateDirectory(Path.Combine(destinationRoot, "Assets"));
                File.Copy(Path.Combine(ProjectRoot, relative), sourceCopy);
                File.Copy(Path.Combine(ProjectRoot, relative + ".meta"), sourceCopy + ".meta");
                VnSceneComposerStorage.SaveProject(sourceRoot, project);
                File.Delete(sourceImage);

                VnSceneComposerReviewProjectMigration.Migrate(temp, destinationRoot, project.projectId);
                string destination = Path.Combine(destinationRoot, relative);
                Assert.That(File.ReadAllBytes(destination), Is.EqualTo(File.ReadAllBytes(sourceCopy)));
                Assert.That(File.ReadAllText(destination + ".meta"), Is.EqualTo(File.ReadAllText(sourceCopy + ".meta")));
                VnSceneComposerImportResult loaded = VnSceneComposerStorage.LoadProject(destinationRoot, project.projectId);
                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(loaded.Project.scenes.Count, Is.EqualTo(1));
                Assert.That(loaded.Project.scenes[0].media.reference, Is.EqualTo(guid));
            }
            finally
            {
                if (!string.IsNullOrEmpty(stableId)) VnSceneComposerAssetLibrary.Unregister(ProjectRoot, stableId);
                if (!string.IsNullOrEmpty(importedPath)) AssetDatabase.DeleteAsset(importedPath);
                if (File.Exists(sourceImage)) File.Delete(sourceImage);
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        private static string CreatePng(string path, Color color)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(4, 3, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < 3; y++) for (int x = 0; x < 4; x++) texture.SetPixel(x, y, color);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                return path;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
