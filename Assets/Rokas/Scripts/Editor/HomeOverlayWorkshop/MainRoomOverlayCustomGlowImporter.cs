using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools
{
    public static class MainRoomOverlayCustomGlowImporter
    {
        public const string Root = "Assets/Rokas/Art/Home/OverlayWorkshop/CustomGlow";
        public const string SpriteRoot = Root + "/Sprites";
        public const string HazeRoot = Root + "/Haze";
        public const string SourceRoot = Root + "/Source";

        public static readonly string[] SpriteNames =
        {
            "LongHorizontal", "MediumHorizontal", "LongVertical", "ShortVertical",
            "CornerLeft", "CornerRight", "RoundedFrame", "SingleDot",
            "DotStackLong", "DotStackShort", "BentLineA", "BentLineB", "CurvedLine"
        };

        public static readonly string[] HazeNames =
        {
            "HazeLong_01", "HazeLong_02", "HazeLong_03", "HazeLong_04",
            "HazeLong_05", "HazeLong_06", "HazeShort_01", "HazeShort_02"
        };

        [MenuItem("ROKAS/Main Room Overlay Workshop/Refresh Custom Glow Assets")]
        public static void ConfigureProjectAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureSource(SourceRoot + "/CustomGlowLines.png");
            ConfigureSource(SourceRoot + "/CustomGlowHaze.png");

            foreach (string name in SpriteNames)
                ConfigureSprite(SpriteRoot + "/" + name + ".png", BorderFor(name));
            foreach (string name in HazeNames)
                ConfigureSprite(HazeRoot + "/" + name + ".png", Vector4.zero);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureSource(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        private static void ConfigureSprite(string path, Vector4 border)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException("Custom glow TextureImporter missing: " + path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private static Vector4 BorderFor(string name)
        {
            return name switch
            {
                "LongHorizontal" => new Vector4(72f, 0f, 72f, 0f),
                "MediumHorizontal" => new Vector4(62f, 0f, 62f, 0f),
                "LongVertical" => new Vector4(0f, 72f, 0f, 72f),
                "ShortVertical" => new Vector4(0f, 58f, 0f, 58f),
                _ => Vector4.zero
            };
        }
    }
}
