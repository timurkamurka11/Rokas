using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void P18_ComposerPresentationPresetDuplicateHasIndependentIdentityAndDeepCopy()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            string root = Path.Combine(Path.GetTempPath(), "rokas-composer-preset-duplicate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireWindowMethod(windowType, "ComposerSetPresentationScope", typeof(bool)).Invoke(window, new object[] { false });
                object project = GetPrivateField(window, "_sceneComposerProject");
                string projectId = (string)Get(project, "projectId");
                SetPrivateField(window, "currentPreset", null);

                Type easing = RequireType("VnWorkshopEasing");
                RequireWindowMethod(windowType, "ComposerSetBounce", typeof(float), typeof(float), typeof(float), typeof(float), easing)
                    .Invoke(window, new object[] { 23f, .31f, .04f, .12f, Enum.Parse(easing, "EaseInOut") });
                RequireWindowMethod(windowType, "ComposerSetPreviewSampleText", typeof(string))
                    .Invoke(window, new object[] { "Composer duplicate sample" });
                RequireWindowMethod(windowType, "ComposerSavePresentationPreset", typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, "OriginalPreset" });

                RequireWindowMethod(windowType, "ComposerDuplicatePresentationPreset", typeof(string), typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, "OriginalPreset", "DuplicatePreset" });

                VnWorkshopImportResult original = VnPresentationWorkshopStorage.LoadVariant(root, "OriginalPreset");
                VnWorkshopImportResult duplicate = VnPresentationWorkshopStorage.LoadVariant(root, "DuplicatePreset");
                Assert.That(original.Success, Is.True);
                Assert.That(duplicate.Success, Is.True);
                Assert.That(original.Document.variantName, Is.EqualTo("OriginalPreset"));
                Assert.That(duplicate.Document.variantName, Is.EqualTo("DuplicatePreset"),
                    "Duplicate must serialize a new Composer preset identity, not byte-copy the original metadata.");
                Assert.That(duplicate.Document.preset, Is.Not.SameAs(original.Document.preset));
                Assert.That(JsonUtility.ToJson(duplicate.Document.preset), Is.EqualTo(JsonUtility.ToJson(original.Document.preset)));
                Assert.That(duplicate.Document.previewSampleText, Is.EqualTo("Composer duplicate sample"));

                object duplicateBounce = Get(duplicate.Document.preset, "actionBounce");
                FieldInfo amplitude = duplicateBounce.GetType().GetField("amplitude", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(amplitude, Is.Not.Null);
                amplitude.SetValue(duplicateBounce, 91f);
                VnPresentationWorkshopStorage.SaveVariant(root, "DuplicatePreset", duplicate.Document.preset);
                original = VnPresentationWorkshopStorage.LoadVariant(root, "OriginalPreset");
                Assert.That((float)Get(Get(original.Document.preset, "actionBounce"), "amplitude"), Is.EqualTo(23f),
                    "Mutating the duplicate must not alter the original preset data.");

                Assert.That((string)Get(GetPrivateField(window, "_sceneComposerProject"), "projectId"), Is.EqualTo(projectId));
                Assert.That(GetPrivateField(window, "currentPreset"), Is.Null,
                    "Composer preset lifecycle must not depend on legacy currentPreset.");
                Assert.That(Directory.Exists(Path.Combine(root, "Assets")), Is.False,
                    "Composer preset operations must remain editor-local and must not create production Assets content.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void P18_ComposerPresentationPresetRenameUpdatesIdentityAndPreservesContent()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            string root = Path.Combine(Path.GetTempPath(), "rokas-composer-preset-rename-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireWindowMethod(windowType, "ComposerSetPresentationScope", typeof(bool)).Invoke(window, new object[] { false });
                object project = GetPrivateField(window, "_sceneComposerProject");
                string projectId = (string)Get(project, "projectId");
                SetPrivateField(window, "currentPreset", null);

                Type easing = RequireType("VnWorkshopEasing");
                RequireWindowMethod(windowType, "ComposerSetBounce", typeof(float), typeof(float), typeof(float), typeof(float), easing)
                    .Invoke(window, new object[] { 31f, .42f, .05f, .14f, Enum.Parse(easing, "EaseOut") });
                RequireWindowMethod(windowType, "ComposerSetPreviewSampleText", typeof(string))
                    .Invoke(window, new object[] { "Composer rename sample" });
                RequireWindowMethod(windowType, "ComposerSavePresentationPreset", typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, "BeforeRename" });

                RequireWindowMethod(windowType, "ComposerRenamePresentationPreset", typeof(string), typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, "BeforeRename", "AfterRename" });

                Assert.That(File.Exists(VnPresentationWorkshopStorage.GetVariantPath(root, "BeforeRename")), Is.False);
                VnWorkshopImportResult renamed = VnPresentationWorkshopStorage.LoadVariant(root, "AfterRename");
                Assert.That(renamed.Success, Is.True);
                Assert.That(renamed.Document.variantName, Is.EqualTo("AfterRename"));
                Assert.That((float)Get(Get(renamed.Document.preset, "actionBounce"), "amplitude"), Is.EqualTo(31f));
                Assert.That(renamed.Document.previewSampleText, Is.EqualTo("Composer rename sample"));
                Assert.That((string)Get(GetPrivateField(window, "_sceneComposerProject"), "projectId"), Is.EqualTo(projectId));
                Assert.That(((IList)Get(GetPrivateField(window, "_sceneComposerProject"), "scenes")).Count, Is.EqualTo(1));
                Assert.That(GetPrivateField(window, "currentPreset"), Is.Null);
                Assert.That(Directory.Exists(Path.Combine(root, "Assets")), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void P18_ComposerPresentationPresetDeleteIsSafeAndLeavesProjectValid()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            string root = Path.Combine(Path.GetTempPath(), "rokas-composer-preset-delete-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireWindowMethod(windowType, "ComposerSetPresentationScope", typeof(bool)).Invoke(window, new object[] { false });
                object project = GetPrivateField(window, "_sceneComposerProject");
                string projectId = (string)Get(project, "projectId");
                SetPrivateField(window, "currentPreset", null);
                SetPrivateField(window, "variantName", "DeleteMe");
                SetPrivateField(window, "selectedVariantIndex", 0);

                RequireWindowMethod(windowType, "ComposerSavePresentationPreset", typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, "DeleteMe" });
                MethodInfo delete = RequireWindowMethod(windowType, "ComposerDeletePresentationPreset", typeof(string), typeof(string));
                bool deleted = (bool)delete.Invoke(window, new object[] { root, "DeleteMe" });
                bool deletedAgain = (bool)delete.Invoke(window, new object[] { root, "DeleteMe" });

                Assert.That(deleted, Is.True);
                Assert.That(deletedAgain, Is.False, "Deleting an already-removed Composer preset must be safe.");
                Assert.That(File.Exists(VnPresentationWorkshopStorage.GetVariantPath(root, "DeleteMe")), Is.False);
                Assert.That((string)Get(GetPrivateField(window, "_sceneComposerProject"), "projectId"), Is.EqualTo(projectId));
                Assert.That(((IList)Get(GetPrivateField(window, "_sceneComposerProject"), "scenes")).Count, Is.EqualTo(1));
                Assert.That(RequireWindowMethod(windowType, "ComposerGetActivePresentationPreset").Invoke(window, null), Is.Not.Null);
                Assert.That(GetPrivateField(window, "currentPreset"), Is.Null);
                Assert.That(Directory.Exists(Path.Combine(root, "Assets")), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
