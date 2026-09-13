using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsImportExport
    {
        [Test]
        public void EditorWindowImportExportIsPortableValidatedAndNeverAppliesProduction()
        {
            Type windowType = Type.GetType("Rokas.EditorTools.VnUiWorkshop.VnPresentationWorkshopWindow, Rokas.Editor");
            Assert.That(windowType, Is.Not.Null);

            FieldInfo exportFileName = windowType.GetField("ExportFileName", BindingFlags.Public | BindingFlags.Static);
            MethodInfo exportJson = windowType.GetMethod("ExportCurrentPresetJson", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo importJson = windowType.GetMethod("ImportPresetJson", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo exportFile = windowType.GetMethod("ExportCurrentPresetToFile", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo importFile = windowType.GetMethod("ImportPresetFromFile", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo currentPreset = windowType.GetProperty("CurrentPreset", BindingFlags.Public | BindingFlags.Instance);

            Assert.That(exportFileName, Is.Not.Null, "The export UI must expose the approved portable filename.");
            Assert.That(exportFileName.GetRawConstantValue(), Is.EqualTo("ROKAS_VN_WORKSHOP_PRESET.json"));
            Assert.That(exportJson, Is.Not.Null, "The window must export through the existing schema-v1 serializer.");
            Assert.That(importJson, Is.Not.Null, "The window must import through the existing validated deserializer.");
            Assert.That(exportFile, Is.Not.Null, "The window must support an explicit user-selected export file.");
            Assert.That(importFile, Is.Not.Null, "The window must support an explicit user-selected import file.");
            Assert.That(currentPreset, Is.Not.Null);

            EditorWindow window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);

            string tempDirectory = Path.Combine(Path.GetTempPath(), "RokasVnWorkshopTests", Guid.NewGuid().ToString("N"));
            string portablePath = Path.Combine(tempDirectory, "ROKAS_VN_WORKSHOP_PRESET.json");

            try
            {
                var preset = (VnPresentationWorkshopPreset)currentPreset.GetValue(window);
                VnPresentationWorkshopEditing.SetPositionDelta(preset, VnWorkshopElement.DialoguePanel, new Vector2(18f, -4f));
                VnPresentationWorkshopEditing.SetSizeDelta(preset, VnWorkshopElement.DialogueText, new Vector2(32f, 14f));

                string json1 = (string)exportJson.Invoke(window, new object[] { "Portable" });
                string json2 = (string)exportJson.Invoke(window, new object[] { "Portable" });
                Assert.That(json1, Is.EqualTo(json2), "Export must be deterministic for an unchanged preset.");
                Assert.That(json1, Does.Contain("\"schemaVersion\""));
                Assert.That(json1, Does.Contain(VnPresentationWorkshopBaseline.SourceHead));
                Assert.That(json1, Does.Contain("\"preset\""));
                Assert.That(json1, Does.Not.Contain(Application.dataPath),
                    "Portable Workshop JSON must not contain machine-specific project paths.");

                exportFile.Invoke(window, new object[] { portablePath });
                Assert.That(File.Exists(portablePath), Is.True);
                Assert.That(File.ReadAllText(portablePath), Is.EqualTo(json1));

                preset.ResetAll();
                var importResult = (VnWorkshopImportResult)importFile.Invoke(window, new object[] { portablePath });
                Assert.That(importResult.Success, Is.True, importResult.Error);
                Assert.That(importResult.SourceHeadMismatch, Is.False);

                var restored = (VnPresentationWorkshopPreset)currentPreset.GetValue(window);
                Assert.That(restored.dialoguePanel.positionDelta, Is.EqualTo(new Vector2(18f, -4f)));
                Assert.That(restored.dialoguePanel.hasPositionDelta, Is.True);
                Assert.That(restored.dialogueText.sizeDelta, Is.EqualTo(new Vector2(32f, 14f)));
                Assert.That(restored.dialogueText.hasSizeDelta, Is.True);

                string mismatched = json1.Replace(VnPresentationWorkshopBaseline.SourceHead,
                    "0000000000000000000000000000000000000000");
                var mismatchResult = (VnWorkshopImportResult)importJson.Invoke(window, new object[] { mismatched });
                Assert.That(mismatchResult.Success, Is.True,
                    "A structurally valid preset from another source HEAD should remain importable with an explicit warning.");
                Assert.That(mismatchResult.SourceHeadMismatch, Is.True,
                    "Source HEAD incompatibility must be surfaced explicitly.");

                string invalidSchema = json1.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 999");
                var invalidResult = (VnWorkshopImportResult)importJson.Invoke(window, new object[] { invalidSchema });
                Assert.That(invalidResult.Success, Is.False, "Unsupported schema versions must be rejected.");

                bool hasProductionApply = windowType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                    .Any(method => method.Name.IndexOf("ApplyToProduction", StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.That(hasProductionApply, Is.False);
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }
    }
}
