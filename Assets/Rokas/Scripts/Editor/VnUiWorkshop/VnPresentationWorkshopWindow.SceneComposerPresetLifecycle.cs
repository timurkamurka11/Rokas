using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        public void ComposerDuplicatePresentationPreset(string projectRoot, string sourceName, string duplicateName)
        {
            VnWorkshopImportResult source = VnPresentationWorkshopStorage.LoadVariant(projectRoot, sourceName);
            if (!source.Success || source.Document == null || source.Document.preset == null)
                throw new InvalidOperationException("Presentation preset duplicate failed: " + source.Error);

            string destinationPath = VnPresentationWorkshopStorage.GetVariantPath(projectRoot, duplicateName);
            if (File.Exists(destinationPath))
                throw new IOException("A Composer presentation preset already exists with that name.");

            VnPresentationWorkshopPreset duplicate = CloneComposerPresentationPreset(source.Document.preset);
            VnWorkshopPreviewSampleStore.Set(duplicate,
                source.Document.previewSampleText ?? VnWorkshopPreviewSampleStore.DefaultText);
            VnPresentationWorkshopStorage.SaveVariant(projectRoot, duplicateName, duplicate);

            variantName = VnPresentationWorkshopStorage.SanitizeVariantName(duplicateName);
            selectedVariantIndex = 0;
            SetSceneComposerStatus("Duplicated presentation preset '" +
                VnPresentationWorkshopStorage.SanitizeVariantName(sourceName) + "' as '" + variantName + "'.", MessageType.Info);
        }

        public void ComposerRenamePresentationPreset(string projectRoot, string currentName, string newName)
        {
            VnWorkshopImportResult source = VnPresentationWorkshopStorage.LoadVariant(projectRoot, currentName);
            if (!source.Success || source.Document == null || source.Document.preset == null)
                throw new InvalidOperationException("Presentation preset rename failed: " + source.Error);

            string sourcePath = VnPresentationWorkshopStorage.GetVariantPath(projectRoot, currentName);
            string destinationPath = VnPresentationWorkshopStorage.GetVariantPath(projectRoot, newName);
            bool samePath = string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase);
            if (!samePath && File.Exists(destinationPath))
                throw new IOException("A Composer presentation preset already exists with that name.");

            VnPresentationWorkshopPreset renamed = CloneComposerPresentationPreset(source.Document.preset);
            VnWorkshopPreviewSampleStore.Set(renamed,
                source.Document.previewSampleText ?? VnWorkshopPreviewSampleStore.DefaultText);
            VnPresentationWorkshopStorage.SaveVariant(projectRoot, newName, renamed);

            if (!samePath && !VnPresentationWorkshopStorage.DeleteVariant(projectRoot, currentName))
            {
                VnPresentationWorkshopStorage.DeleteVariant(projectRoot, newName);
                throw new IOException("Presentation preset rename could not remove the original preset safely.");
            }

            variantName = VnPresentationWorkshopStorage.SanitizeVariantName(newName);
            selectedVariantIndex = 0;
            SetSceneComposerStatus("Renamed presentation preset to '" + variantName + "'.", MessageType.Info);
        }

        public bool ComposerDeletePresentationPreset(string projectRoot, string name)
        {
            bool deleted = VnPresentationWorkshopStorage.DeleteVariant(projectRoot, name);
            if (!deleted) return false;

            string deletedName = VnPresentationWorkshopStorage.SanitizeVariantName(name);
            if (string.Equals(VnPresentationWorkshopStorage.SanitizeVariantName(variantName), deletedName,
                StringComparison.OrdinalIgnoreCase))
                variantName = "Variant";
            selectedVariantIndex = 0;
            SetSceneComposerStatus("Deleted presentation preset '" + deletedName + "'.", MessageType.Info);
            return true;
        }

        private static VnPresentationWorkshopPreset CloneComposerPresentationPreset(VnPresentationWorkshopPreset source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            VnPresentationWorkshopPreset clone = JsonUtility.FromJson<VnPresentationWorkshopPreset>(JsonUtility.ToJson(source));
            return clone ?? new VnPresentationWorkshopPreset();
        }
    }
}
