using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class MainRoomOverlayWorkshopAssetTests
    {
        private static readonly string[] Names =
        {
            "LightBulb", "Cat", "Window", "Tea",
            "Laptop", "DeskLamp", "Swords", "Exit"
        };

        private string sourceDirectory;
        private string destinationRoot;

        [SetUp]
        public void SetUp()
        {
            sourceDirectory = Path.Combine(Path.GetTempPath(), "rokas-workshop-icons-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sourceDirectory);
            destinationRoot = "Assets/__RokasMainRoomOverlayWorkshopTests/" + Guid.NewGuid().ToString("N") + "/Icons";

            foreach (string name in Names)
            {
                var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(sourceDirectory, name + ".png"), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(sourceDirectory) && Directory.Exists(sourceDirectory))
                Directory.Delete(sourceDirectory, true);
            if (!string.IsNullOrEmpty(destinationRoot))
            {
                string folder = destinationRoot.Substring(0, destinationRoot.LastIndexOf('/'));
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ImporterCopiesCanonicalIconsAsUncompressedSingleSprites()
        {
            MainRoomOverlayWorkshopIconImporter.ImportFromDirectory(sourceDirectory, destinationRoot);

            foreach (string name in Names)
            {
                string path = destinationRoot + "/" + name + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(sprite, Is.Not.Null, path);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
            }
        }
    }
}
