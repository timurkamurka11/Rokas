using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVariants
    {
        [Test]
        public void EditorWindowVariantUiUsesLocalStorageCrudAndRestoresCurrentPreset()
        {
            Type windowType = Type.GetType("Rokas.EditorTools.VnUiWorkshop.VnPresentationWorkshopWindow, Rokas.Editor");
            Assert.That(windowType, Is.Not.Null);

            MethodInfo list = windowType.GetMethod("ListSavedVariants", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo save = windowType.GetMethod("SaveCurrentVariant", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo load = windowType.GetMethod("LoadSavedVariant", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo duplicate = windowType.GetMethod("DuplicateSavedVariant", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo rename = windowType.GetMethod("RenameSavedVariant", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo delete = windowType.GetMethod("DeleteSavedVariant", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo currentPreset = windowType.GetProperty("CurrentPreset", BindingFlags.Public | BindingFlags.Instance);

            Assert.That(list, Is.Not.Null, "The window must list editor-local saved variants.");
            Assert.That(save, Is.Not.Null, "The window must wire Save Variant to the existing storage backend.");
            Assert.That(load, Is.Not.Null, "The window must wire Load Variant to the existing storage backend.");
            Assert.That(duplicate, Is.Not.Null, "The window must wire Duplicate to the existing storage backend.");
            Assert.That(rename, Is.Not.Null, "The window must wire Rename to the existing storage backend.");
            Assert.That(delete, Is.Not.Null, "The window must wire Delete to the existing storage backend.");
            Assert.That(currentPreset, Is.Not.Null);

            EditorWindow window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);

            string token = Guid.NewGuid().ToString("N");
            string originalName = "CI_Variant_" + token;
            string duplicateName = originalName + "_Copy";
            string renamedName = originalName + "_Renamed";

            try
            {
                var preset = (VnPresentationWorkshopPreset)currentPreset.GetValue(window);
                VnPresentationWorkshopEditing.SetPositionDelta(preset, VnWorkshopElement.MinaBody, new Vector2(12f, 3f));
                save.Invoke(window, new object[] { originalName });

                string[] saved = (string[])list.Invoke(window, null);
                Assert.That(saved, Does.Contain(originalName));

                VnPresentationWorkshopEditing.SetPositionDelta(preset, VnWorkshopElement.MinaBody, new Vector2(50f, 9f));
                var loadResult = (VnWorkshopImportResult)load.Invoke(window, new object[] { originalName });
                Assert.That(loadResult.Success, Is.True, loadResult.Error);
                Assert.That(loadResult.SourceHeadMismatch, Is.False);

                var restored = (VnPresentationWorkshopPreset)currentPreset.GetValue(window);
                Assert.That(restored.minaBody.positionDelta, Is.EqualTo(new Vector2(12f, 3f)));
                Assert.That(restored.minaBody.hasPositionDelta, Is.True);

                duplicate.Invoke(window, new object[] { originalName, duplicateName });
                Assert.That((string[])list.Invoke(window, null), Does.Contain(duplicateName));

                rename.Invoke(window, new object[] { duplicateName, renamedName });
                string[] renamed = (string[])list.Invoke(window, null);
                Assert.That(renamed, Does.Contain(renamedName));
                Assert.That(renamed, Does.Not.Contain(duplicateName));

                Assert.That((bool)delete.Invoke(window, new object[] { renamedName }), Is.True);
                Assert.That((string[])list.Invoke(window, null), Does.Not.Contain(renamedName));
            }
            finally
            {
                if (delete != null && window != null)
                {
                    foreach (string name in new[] { originalName, duplicateName, renamedName })
                    {
                        try { delete.Invoke(window, new object[] { name }); }
                        catch { }
                    }
                }
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}
