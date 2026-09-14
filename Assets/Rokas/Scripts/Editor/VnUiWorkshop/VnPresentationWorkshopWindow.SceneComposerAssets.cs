using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        public void ComposerRefreshAssets()
        {
            VnSceneComposerAssetCatalog catalog = VnSceneComposerAssetLibrary.Refresh(GetProjectRoot());
            ClearSceneComposerThumbnailCache();
            ResetSceneComposerPlayback();
            string warning = catalog.warnings != null && catalog.warnings.Count > 0
                ? string.Join("\n", catalog.warnings)
                : string.Empty;
            SetSceneComposerStatus(string.IsNullOrEmpty(warning)
                ? "Scene Composer Asset Library refreshed."
                : "Asset Library refreshed with warnings:\n" + warning,
                string.IsNullOrEmpty(warning) ? MessageType.Info : MessageType.Warning);
        }

        public string[] ComposerGetAvailableBackgroundAssetIds()
        {
            VnSceneComposerAssetCatalog catalog = VnSceneComposerAssetLibrary.LoadCatalog(GetProjectRoot());
            return catalog.assets
                .Where(entry => entry != null && entry.valid &&
                    entry.purpose == VnSceneComposerAssetPurpose.Background &&
                    !string.IsNullOrWhiteSpace(entry.assetId))
                .OrderBy(entry => entry.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.assetId, StringComparer.Ordinal)
                .Select(entry => entry.assetId)
                .ToArray();
        }

        public string[] ComposerGetAvailableBackgroundDisplayNames()
        {
            VnSceneComposerAssetCatalog catalog = VnSceneComposerAssetLibrary.LoadCatalog(GetProjectRoot());
            return catalog.assets
                .Where(entry => entry != null && entry.valid &&
                    entry.purpose == VnSceneComposerAssetPurpose.Background &&
                    !string.IsNullOrWhiteSpace(entry.assetId))
                .OrderBy(entry => entry.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.assetId, StringComparer.Ordinal)
                .Select(entry => string.IsNullOrWhiteSpace(entry.displayName) ? entry.assetId : entry.displayName)
                .ToArray();
        }

        public void ComposerSetLibraryBackground(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
                throw new ArgumentException("Background asset identity is required.", nameof(assetId));
            VnSceneComposerAssetCatalog catalog = VnSceneComposerAssetLibrary.LoadCatalog(GetProjectRoot());
            VnSceneComposerAssetEntry entry = catalog.assets.FirstOrDefault(candidate => candidate != null &&
                candidate.valid && candidate.purpose == VnSceneComposerAssetPurpose.Background &&
                string.Equals(candidate.assetId, assetId, StringComparison.Ordinal));
            if (entry == null)
                throw new ArgumentException("Unknown or invalid Scene Composer background asset: " + assetId, nameof(assetId));
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.projectPath);
            if (texture == null)
                throw new InvalidOperationException("Scene Composer background texture is missing: " + entry.projectPath);
            ComposerSetExistingRokasAsset(texture);
        }

        public Texture2D ComposerGetLibraryAssetThumbnail(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId)) return null;
            VnSceneComposerAssetCatalog catalog = VnSceneComposerAssetLibrary.LoadCatalog(GetProjectRoot());
            VnSceneComposerAssetEntry entry = catalog.assets.FirstOrDefault(candidate => candidate != null &&
                string.Equals(candidate.assetId, assetId, StringComparison.Ordinal));
            return VnSceneComposerAssetLibrary.LoadThumbnail(entry);
        }
    }
}
