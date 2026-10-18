#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Preserve the user's five sharp OLED PNGs exactly: Unity's default
    /// block texture compression adds halos to Russian text and graph lines.
    /// This import hook runs only for the dedicated WatchFaces folder.
    /// </summary>
    internal sealed class ROKASWatchFaceImportSettings : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/BedSleep/WatchFaces/"))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
#endif
