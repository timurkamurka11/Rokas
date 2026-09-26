using NUnit.Framework;
using UnityEditor;

namespace Rokas.EditorTools.Tests
{
    public sealed class MainRoomOverlayWorkshopAssetTests
    {
        private static readonly string[] IconPaths =
        {
            "Assets/Rokas/Art/Home/OverlayWorkshop/Icons/LightBulb.png",
            "Assets/Rokas/Art/Home/OverlayWorkshop/Icons/Cat.png",
            "Assets/Rokas/Art/Home/OverlayWorkshop/Icons/Window.png",
            "Assets/Rokas/Art/Home/OverlayWorkshop/Icons/Tea.png",
            "Assets/Rokas/Art/Home/OverlayWorkshop/Icons/Laptop.png",
            "Assets/Rokas/Art/Home/OverlayWorkshop/Icons/DeskLamp.png",
            "Assets/Rokas/Art/Home/OverlayWorkshop/Icons/Swords.png",
            "Assets/Rokas/Art/Home/OverlayWorkshop/Icons/Exit.png"
        };

        [Test]
        public void ProvidedIconsExistAsUncompressedSingleSprites()
        {
            foreach (string path in IconPaths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
                Assert.That(sprite, Is.Not.Null, path + " must use the provided project-owned PNG.");

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
            }
        }
    }
}
