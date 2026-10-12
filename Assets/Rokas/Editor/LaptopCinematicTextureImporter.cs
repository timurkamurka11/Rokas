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
            // Packed lighting masks encode four linear *data* channels,
            // not photographic RGB. Importing them as sRGB destroys the
            // calibrated response and makes the weak bounce even weaker.
            bool isLightMask = assetPath.IndexOf("/LightMasks/",
                StringComparison.OrdinalIgnoreCase) >= 0;
            importer.sRGBTexture = !isLightMask;
            if (isLightMask) importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            if (assetPath.IndexOf("/HandsRight/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetPath.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0)
                importer.alphaIsTransparency = true; // preserve original PNG glow and chibi edges
        }
    }
}
