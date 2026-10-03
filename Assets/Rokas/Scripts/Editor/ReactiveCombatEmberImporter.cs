using UnityEditor;

namespace Rokas.Editor
{
    public sealed class ReactiveCombatEmberImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Rokas/Resources/Combat/ReactiveTurns/Vfx/EmberGen/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false; // Source RGB is already premultiplied.
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name = "Standalone", overridden = true, maxTextureSize = 2048,
                format = TextureImporterFormat.BC7, compressionQuality = 100 });
        }
    }
}
