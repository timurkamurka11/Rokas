using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.Editor
{
    // Reapply these settings on every import so new non-power-of-two RGBA
    // hand frames and the user-approved POV never become resized textures.
    public sealed class LaptopCinematicTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            const string root = "Assets/Rokas/Resources/LaptopCinematic/";
            if (!assetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
            if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            if (assetPath.IndexOf("/HandsRight/", StringComparison.OrdinalIgnoreCase) >= 0)
                importer.alphaIsTransparency = true;
        }
    }
}
