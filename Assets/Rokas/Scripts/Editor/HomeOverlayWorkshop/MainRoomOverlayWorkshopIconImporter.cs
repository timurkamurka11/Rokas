using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools
{
    public static class MainRoomOverlayWorkshopIconImporter
    {
        public const string DefaultDestinationRoot = "Assets/Rokas/Art/Home/OverlayWorkshop/Icons";
        public const string ReferenceDestinationRoot = "Assets/Rokas/Art/Home/OverlayWorkshop/Reference";
        public const string ReferenceFileName = "ReferenceOracle.png";

        public static readonly string[] CanonicalNames =
        {
            "LightBulb", "Cat", "Window", "Tea",
            "Laptop", "DeskLamp", "Swords", "Exit"
        };

        [MenuItem("ROKAS/Main Room Overlay Workshop/Import Provided Icons...")]
        public static void ImportProvidedIconsFromFolder()
        {
            string sourceDirectory = EditorUtility.OpenFolderPanel(
                "Select extracted ROKAS_MAIN_ROOM_WORKSHOP_ASSETS folder", "", "");
            if (string.IsNullOrWhiteSpace(sourceDirectory)) return;

            ImportFromDirectory(sourceDirectory, DefaultDestinationRoot);
            ImportReferenceIfPresent(sourceDirectory);
            EditorUtility.DisplayDialog(
                "ROKAS Main Room Overlay Workshop",
                "Provided icon assets imported. Open/Create the workshop scene to place them manually.",
                "OK");
        }

        public static void ImportFromDirectory(string sourceDirectory, string destinationRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory))
                throw new ArgumentException("Source directory is required.", nameof(sourceDirectory));
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException(sourceDirectory);
            if (string.IsNullOrWhiteSpace(destinationRoot) ||
                !destinationRoot.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Destination must be under Assets/.", nameof(destinationRoot));

            EnsureAssetFolder(destinationRoot);

            foreach (string name in CanonicalNames)
            {
                string source = Path.Combine(sourceDirectory, name + ".png");
                if (!File.Exists(source))
                    throw new FileNotFoundException("Missing provided workshop icon: " + name + ".png", source);

                string destination = destinationRoot.TrimEnd('/') + "/" + name + ".png";
                File.Copy(source, ToAbsoluteProjectPath(destination), true);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                ConfigureSprite(destination);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ImportReferenceIfPresent(string sourceDirectory)
        {
            string source = Path.Combine(sourceDirectory, ReferenceFileName);
            if (!File.Exists(source)) return;

            EnsureAssetFolder(ReferenceDestinationRoot);
            string destination = ReferenceDestinationRoot + "/" + ReferenceFileName;
            File.Copy(source, ToAbsoluteProjectPath(destination), true);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(destination) is TextureImporter importer)
            {
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
        }

        private static void ConfigureSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("TextureImporter was not created for " + assetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new ArgumentException("Asset folder must start with Assets/.", nameof(assetFolder));

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new InvalidOperationException("Could not resolve Unity project root.");
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
