#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.Presentation.Editor
{
    /// <summary>
    /// Cursor.SetCursor in Unity 6000 needs readable, RGBA, transparent,
    /// non-mipmapped pixels. Restrict changes to six authentic PNG assets.
    /// Does not touch imported hand textures or any existing game art.
    /// </summary>
    public sealed class RokasCursorPackImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath.IndexOf("/Resources/Rokas/Cursors/",
                    StringComparison.OrdinalIgnoreCase) < 0 ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 128;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
        }
    }
}
#endif
