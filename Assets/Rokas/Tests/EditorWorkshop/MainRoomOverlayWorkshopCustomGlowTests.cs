using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rokas.EditorTools.Tests
{
    public sealed class MainRoomOverlayWorkshopCustomGlowTests
    {
        private string sourceDirectory;
        private string destinationRoot;
        private Scene scene;
        private bool createdCustomGlowTestAssets;

        [SetUp]
        public void SetUp()
        {
            sourceDirectory = Path.Combine(Path.GetTempPath(), "rokas-workshop-glow-icons-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sourceDirectory);
            destinationRoot = "Assets/__RokasMainRoomOverlayWorkshopTests/" + Guid.NewGuid().ToString("N") + "/Icons";

            foreach (string name in MainRoomOverlayWorkshopIconImporter.CanonicalNames)
            {
                var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(sourceDirectory, name + ".png"), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }

            MainRoomOverlayWorkshopIconImporter.ImportFromDirectory(sourceDirectory, destinationRoot);
            EnsureCustomGlowTestAssets();
            MainRoomOverlayCustomGlowImporter.ConfigureProjectAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
            if (!string.IsNullOrEmpty(sourceDirectory) && Directory.Exists(sourceDirectory))
                Directory.Delete(sourceDirectory, true);
            if (!string.IsNullOrEmpty(destinationRoot))
            {
                string folder = destinationRoot.Substring(0, destinationRoot.LastIndexOf('/'));
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.Refresh();
            }
            if (createdCustomGlowTestAssets)
            {
                AssetDatabase.DeleteAsset(MainRoomOverlayCustomGlowImporter.Root);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void CustomGlowAssetsImportAsUncompressedTransparentSprites()
        {
            foreach (string name in MainRoomOverlayCustomGlowImporter.SpriteNames)
                AssertGlowSprite(MainRoomOverlayCustomGlowImporter.SpriteRoot + "/" + name + ".png");
            foreach (string name in MainRoomOverlayCustomGlowImporter.HazeNames)
                AssertGlowSprite(MainRoomOverlayCustomGlowImporter.HazeRoot + "/" + name + ".png");

            Sprite longHorizontal = AssetDatabase.LoadAssetAtPath<Sprite>(
                MainRoomOverlayCustomGlowImporter.SpriteRoot + "/LongHorizontal.png");
            Assert.That(longHorizontal, Is.Not.Null);
            Assert.That(longHorizontal.border.x, Is.GreaterThan(0f));
            Assert.That(longHorizontal.border.z, Is.GreaterThan(0f));
        }

        [Test]
        public void CustomGlowRoundTripRecreatesPiecePreservesFxAndDoesNotResurrectDeletedDefaults()
        {
            scene = MainRoomOverlayWorkshopBuilder.CreateUnsavedWorkshopForTests(destinationRoot);
            GameObject root = FindRoot(scene, "MainRoomOverlayWorkshop");
            Assert.That(root, Is.Not.Null);

            Remove(root, "Hotspot_Cup");
            Remove(root, "Hotspot_DeskLamp");
            Remove(root, "Connector_Cup");
            Remove(root, "Connector_DeskLamp");
            foreach (string obsolete in new[] { "Window", "DeskLamp", "Swords", "Cup", "FloorLamp" })
                Remove(root, "Outline_" + obsolete);

            MainRoomOverlayCustomGlowAuthoring.PrepareCustomGlowLayer(root, false);
            GameObject glow = MainRoomOverlayCustomGlowAuthoring.AddPiece(root, "LongHorizontal");
            MainRoomOverlayCustomGlowElement marker = glow.GetComponent<MainRoomOverlayCustomGlowElement>();
            marker.StableId = "test-glow-01";

            RectTransform rect = glow.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(777f, -444f);
            rect.sizeDelta = new Vector2(512f, 77f);
            rect.localEulerAngles = new Vector3(0f, 0f, 17f);

            Image image = glow.GetComponent<Image>();
            image.color = new Color(.93f, .88f, .77f, .42f);

            MainRoomOverlayGlowFx fx = glow.GetComponent<MainRoomOverlayGlowFx>();
            fx.Brightness = 1.22f;
            fx.PulseEnabled = true;
            fx.PulseSpeed = .44f;
            fx.PulseAmount = .09f;
            fx.ShimmerEnabled = true;
            fx.ShimmerSpeed = .27f;
            fx.ShimmerAmount = .33f;
            fx.ShimmerWidth = .12f;
            fx.HazeEnabled = true;
            fx.HazeOpacity = .20f;
            fx.PhaseOffset = .63f;

            string json = MainRoomOverlayWorkshopLayoutIO.CaptureJson(root);
            Assert.That(json, Does.Contain("\"schemaVersion\": 2"));
            Assert.That(json, Does.Contain("test-glow-01"));

            UnityEngine.Object.DestroyImmediate(glow);
            Assert.That(FindGlow(root, "test-glow-01"), Is.Null);

            MainRoomOverlayWorkshopLayoutIO.ApplyJson(root, json);

            MainRoomOverlayCustomGlowElement restored = FindGlow(root, "test-glow-01");
            Assert.That(restored, Is.Not.Null);
            RectTransform restoredRect = restored.GetComponent<RectTransform>();
            Assert.That(restoredRect.anchoredPosition.x, Is.EqualTo(777f).Within(.01f));
            Assert.That(restoredRect.anchoredPosition.y, Is.EqualTo(-444f).Within(.01f));
            Assert.That(restoredRect.sizeDelta.x, Is.EqualTo(512f).Within(.01f));
            Assert.That(restoredRect.sizeDelta.y, Is.EqualTo(77f).Within(.01f));
            Assert.That(restoredRect.localEulerAngles.z, Is.EqualTo(17f).Within(.05f));

            Image restoredImage = restored.GetComponent<Image>();
            Assert.That(restoredImage.sprite, Is.Not.Null);
            Assert.That(restoredImage.color.a, Is.EqualTo(.42f).Within(.001f));

            MainRoomOverlayGlowFx restoredFx = restored.GetComponent<MainRoomOverlayGlowFx>();
            Assert.That(restoredFx.Brightness, Is.EqualTo(1.22f).Within(.001f));
            Assert.That(restoredFx.PulseEnabled, Is.True);
            Assert.That(restoredFx.PulseSpeed, Is.EqualTo(.44f).Within(.001f));
            Assert.That(restoredFx.PulseAmount, Is.EqualTo(.09f).Within(.001f));
            Assert.That(restoredFx.ShimmerEnabled, Is.True);
            Assert.That(restoredFx.ShimmerSpeed, Is.EqualTo(.27f).Within(.001f));
            Assert.That(restoredFx.ShimmerAmount, Is.EqualTo(.33f).Within(.001f));
            Assert.That(restoredFx.ShimmerWidth, Is.EqualTo(.12f).Within(.001f));
            Assert.That(restoredFx.HazeEnabled, Is.True);
            Assert.That(restoredFx.HazeOpacity, Is.EqualTo(.20f).Within(.001f));
            Assert.That(restoredFx.PhaseOffset, Is.EqualTo(.63f).Within(.001f));

            Assert.That(FindTransform(root, "Hotspot_Cup"), Is.Null);
            Assert.That(FindTransform(root, "Hotspot_DeskLamp"), Is.Null);
            Assert.That(FindTransform(root, "Outline_Window"), Is.Null);
            Assert.That(FindTransform(root, "Outline_Door"), Is.Not.Null);
            Assert.That(FindTransform(root, "Outline_Laptop"), Is.Not.Null);
            Assert.That(FindTransform(root, "Outline_Cat"), Is.Not.Null);

            const string legacyV1 = "{\"schemaVersion\":1,\"rects\":[],\"outlines\":[],\"connectors\":[],\"images\":[],\"texts\":[]}";
            MainRoomOverlayWorkshopLayoutIO.ApplyJson(root, legacyV1);
            Assert.That(FindGlow(root, "test-glow-01"), Is.Not.Null, "v1 import must not delete the new glow layer.");
            Assert.That(FindTransform(root, "Hotspot_Cup"), Is.Null, "v1 import must not rebuild deleted defaults.");
            Assert.That(FindTransform(root, "Hotspot_DeskLamp"), Is.Null, "v1 import must not rebuild deleted defaults.");
        }

        [Test]
        public void PrepareCustomGlowLayerPreservesManualDeletionAndKeepsHotspotsAboveGlow()
        {
            scene = MainRoomOverlayWorkshopBuilder.CreateUnsavedWorkshopForTests(destinationRoot);
            GameObject root = FindRoot(scene, "MainRoomOverlayWorkshop");
            Remove(root, "Hotspot_Cup");
            Remove(root, "Hotspot_DeskLamp");

            MainRoomOverlayCustomGlowAuthoring.PrepareCustomGlowLayer(root, true);

            Transform canvas = FindTransform(root, "WorkshopCanvas");
            Transform customGlow = canvas.Find("CustomGlow");
            Transform hotspots = canvas.Find("Hotspots");
            Assert.That(customGlow, Is.Not.Null);
            Assert.That(customGlow.Find("GlowLines"), Is.Not.Null);
            Assert.That(customGlow.Find("GlowConnectors"), Is.Not.Null);
            Assert.That(customGlow.Find("GlowFrames"), Is.Not.Null);
            Assert.That(customGlow.Find("GlowFX"), Is.Not.Null);
            Assert.That(hotspots.GetSiblingIndex(), Is.GreaterThan(customGlow.GetSiblingIndex()));
            Assert.That(FindTransform(root, "Hotspot_Cup"), Is.Null);
            Assert.That(FindTransform(root, "Hotspot_DeskLamp"), Is.Null);
        }

        private void EnsureCustomGlowTestAssets()
        {
            if (AssetDatabase.IsValidFolder(MainRoomOverlayCustomGlowImporter.Root)) return;
            createdCustomGlowTestAssets = true;

            Directory.CreateDirectory(ToAbsoluteProjectPath(MainRoomOverlayCustomGlowImporter.SpriteRoot));
            Directory.CreateDirectory(ToAbsoluteProjectPath(MainRoomOverlayCustomGlowImporter.HazeRoot));
            Directory.CreateDirectory(ToAbsoluteProjectPath(MainRoomOverlayCustomGlowImporter.SourceRoot));

            foreach (string name in MainRoomOverlayCustomGlowImporter.SpriteNames)
                WriteTinyPng(MainRoomOverlayCustomGlowImporter.SpriteRoot + "/" + name + ".png");
            foreach (string name in MainRoomOverlayCustomGlowImporter.HazeNames)
                WriteTinyPng(MainRoomOverlayCustomGlowImporter.HazeRoot + "/" + name + ".png");
            WriteTinyPng(MainRoomOverlayCustomGlowImporter.SourceRoot + "/CustomGlowLines.png");
            WriteTinyPng(MainRoomOverlayCustomGlowImporter.SourceRoot + "/CustomGlowHaze.png");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void WriteTinyPng(string assetPath)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, new Color(1f, .65f, .2f, (x > 8 && x < size - 9 && y > 8 && y < size - 9) ? 1f : 0f));
            texture.Apply();
            File.WriteAllBytes(ToAbsoluteProjectPath(assetPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new InvalidOperationException("Could not resolve project root.");
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void AssertGlowSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), path);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
            Assert.That(importer.alphaIsTransparency, Is.True, path);
            Assert.That(importer.mipmapEnabled, Is.False, path);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
        }

        private static void Remove(GameObject root, string name)
        {
            Transform target = FindTransform(root, name);
            if (target) UnityEngine.Object.DestroyImmediate(target.gameObject);
        }

        private static MainRoomOverlayCustomGlowElement FindGlow(GameObject root, string stableId)
        {
            foreach (MainRoomOverlayCustomGlowElement marker in root.GetComponentsInChildren<MainRoomOverlayCustomGlowElement>(true))
                if (marker.StableId == stableId) return marker;
            return null;
        }

        private static GameObject FindRoot(Scene targetScene, string name)
        {
            foreach (GameObject root in targetScene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static Transform FindTransform(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}
