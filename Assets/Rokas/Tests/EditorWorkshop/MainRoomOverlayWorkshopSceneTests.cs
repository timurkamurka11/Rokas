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
    public sealed class MainRoomOverlayWorkshopSceneTests
    {
        private static readonly string[] HotspotNames =
        {
            "Window", "DeskLamp", "Swords", "Door",
            "Laptop", "Cup", "FloorLamp", "Cat"
        };

        private string sourceDirectory;
        private string destinationRoot;
        private Scene scene;

        [SetUp]
        public void SetUp()
        {
            sourceDirectory = Path.Combine(Path.GetTempPath(), "rokas-workshop-scene-icons-" + Guid.NewGuid().ToString("N"));
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
        }

        [Test]
        public void LayoutJsonRoundTripPreservesManualWorkshopEdits()
        {
            scene = MainRoomOverlayWorkshopBuilder.CreateUnsavedWorkshopForTests(destinationRoot);
            GameObject root = FindRoot(scene, "MainRoomOverlayWorkshop");
            Assert.That(root, Is.Not.Null);

            RectTransform laptop = FindTransform(root, "Hotspot_Laptop") as RectTransform;
            MainRoomOverlayEditablePath outline = Find<MainRoomOverlayEditablePath>(root, "Outline_Laptop");
            MainRoomOverlayDottedConnector connector = Find<MainRoomOverlayDottedConnector>(root, "Connector_Laptop");
            Assert.That(laptop, Is.Not.Null);
            Assert.That(outline, Is.Not.Null);
            Assert.That(connector, Is.Not.Null);

            laptop.anchoredPosition = new Vector2(777f, -333f);
            laptop.sizeDelta = new Vector2(123f, 117f);
            outline.SetPoint(0, new Vector2(812f, 499f));

            string json = MainRoomOverlayWorkshopLayoutIO.CaptureJson(root);
            Assert.That(json, Does.Contain("Hotspot_Laptop"));
            Assert.That(json, Does.Contain("Outline_Laptop"));

            laptop.anchoredPosition = new Vector2(10f, -10f);
            laptop.sizeDelta = new Vector2(10f, 10f);
            outline.SetPoint(0, Vector2.zero);

            MainRoomOverlayWorkshopLayoutIO.ApplyJson(root, json);

            Assert.That(laptop.anchoredPosition.x, Is.EqualTo(777f).Within(.01f));
            Assert.That(laptop.anchoredPosition.y, Is.EqualTo(-333f).Within(.01f));
            Assert.That(laptop.sizeDelta.x, Is.EqualTo(123f).Within(.01f));
            Assert.That(laptop.sizeDelta.y, Is.EqualTo(117f).Within(.01f));
            Assert.That(outline.GetPoint(0).x, Is.EqualTo(812f).Within(.01f));
            Assert.That(outline.GetPoint(0).y, Is.EqualTo(499f).Within(.01f));
        }

        [Test]
        public void BuilderCreatesEditableWorkshopLayersWithoutTouchingBuildSettings()
        {
            scene = MainRoomOverlayWorkshopBuilder.CreateUnsavedWorkshopForTests(destinationRoot);

            GameObject root = FindRoot(scene, "MainRoomOverlayWorkshop");
            Assert.That(root, Is.Not.Null);

            Canvas canvas = Find<Canvas>(root, "WorkshopCanvas");
            Assert.That(canvas, Is.Not.Null);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

            RawImage background = Find<RawImage>(root, "Background");
            Assert.That(background, Is.Not.Null);
            Assert.That(background.texture, Is.Not.Null);

            Assert.That(FindTransform(root, "Header"), Is.Not.Null);
            Assert.That(FindTransform(root, "TitleText"), Is.Not.Null);
            Assert.That(FindTransform(root, "AccentLine"), Is.Not.Null);
            Assert.That(FindTransform(root, "ReferenceHelpers"), Is.Not.Null);
            Assert.That(FindTransform(root, "ReferenceOracle"), Is.Not.Null);

            Transform hotspots = FindTransform(root, "Hotspots");
            Transform connectors = FindTransform(root, "Connectors");
            Transform outlines = FindTransform(root, "Outlines");
            Assert.That(hotspots, Is.Not.Null);
            Assert.That(connectors, Is.Not.Null);
            Assert.That(outlines, Is.Not.Null);

            foreach (string name in HotspotNames)
            {
                Transform hotspot = hotspots.Find("Hotspot_" + name);
                Assert.That(hotspot, Is.Not.Null, name);
                Image image = hotspot.GetComponent<Image>();
                Assert.That(image, Is.Not.Null, name);
                Assert.That(image.sprite, Is.Not.Null, name);
                Assert.That(image.preserveAspect, Is.True, name);

                Transform connector = connectors.Find("Connector_" + name);
                Assert.That(connector, Is.Not.Null, name);
                Assert.That(connector.GetComponent<MainRoomOverlayDottedConnector>(), Is.Not.Null, name);

                Transform outline = outlines.Find("Outline_" + name);
                Assert.That(outline, Is.Not.Null, name);
                Assert.That(outline.GetComponent<MainRoomOverlayEditablePath>(), Is.Not.Null, name);
            }

            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
                Assert.That(buildScene.path, Is.Not.EqualTo(MainRoomOverlayWorkshopBuilder.ScenePath),
                    "Workshop scene must remain editor-only and outside Build Settings.");
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

        private static T Find<T>(GameObject root, string name) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == name) return component;
            return null;
        }
    }
}
