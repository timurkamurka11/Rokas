using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rokas.EditorTools
{
    public static class MainRoomOverlayWorkshopBuilder
    {
        public const string ScenePath = "Assets/Rokas/EditorWorkshops/MainRoomOverlayWorkshop.unity";
        private const string HomeTexturePath = "Assets/Rokas/Art/Home/ApartmentNight.png";
        private const string ReferencePath = "Assets/Rokas/Art/Home/OverlayWorkshop/Reference/ReferenceOracle.png";

        private static readonly string[] HotspotNames =
        {
            "Window", "DeskLamp", "Swords", "Door",
            "Laptop", "Cup", "FloorLamp", "Cat"
        };

        private static readonly Dictionary<string, string> IconByHotspot =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Window", "Window" },
                { "DeskLamp", "DeskLamp" },
                { "Swords", "Swords" },
                { "Door", "Exit" },
                { "Laptop", "Laptop" },
                { "Cup", "Tea" },
                { "FloorLamp", "LightBulb" },
                { "Cat", "Cat" }
            };

        [MenuItem("ROKAS/Main Room Overlay Workshop/Open or Create")]
        public static void OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ToAbsoluteProjectPath(ScenePath)))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return;
            }

            EnsureAssetFolder("Assets/Rokas/EditorWorkshops");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Populate(scene, MainRoomOverlayWorkshopIconImporter.DefaultDestinationRoot);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save " + ScenePath);
            Selection.activeGameObject = FindRoot(scene, "MainRoomOverlayWorkshop");
        }

        [MenuItem("ROKAS/Main Room Overlay Workshop/Refresh Imported Assets")]
        public static void RefreshImportedAssets()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = FindRoot(scene, "MainRoomOverlayWorkshop");
            if (!root)
            {
                EditorUtility.DisplayDialog("ROKAS Main Room Overlay Workshop",
                    "Open the workshop scene first.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Refresh workshop icon assets");
            ApplyImportedAssets(root, MainRoomOverlayWorkshopIconImporter.DefaultDestinationRoot);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static Scene CreateUnsavedWorkshopForTests(string iconRoot)
        {
            // Test-only helper: the Test Runner may start with an untitled unsaved scene,
            // which Unity refuses to keep while creating an additive scene.
            // A Single test scene is safe here because the test owns the editor scene state.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Populate(scene, iconRoot);
            return scene;
        }

        private static void Populate(Scene scene, string iconRoot)
        {
            GameObject root = new GameObject("MainRoomOverlayWorkshop");
            SceneManager.MoveGameObjectToScene(root, scene);

            GameObject canvasObject = new GameObject(
                "WorkshopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            RawImage background = CreateRawImage(canvasRect, "Background");
            background.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(HomeTexturePath);
            background.raycastTarget = false;

            RectTransform referenceHelpers = CreateRect(canvasRect, "ReferenceHelpers");
            RawImage reference = CreateRawImage(referenceHelpers, "ReferenceOracle");
            reference.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferencePath);
            reference.color = new Color(1f, 1f, 1f, .32f);
            reference.raycastTarget = false;
            reference.gameObject.SetActive(false);

            RectTransform header = CreateRect(canvasRect, "Header");
            TextMeshProUGUI title = CreateText(header, "TitleText", "Variant 4 — scan mode reveal");
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(44f, -24f);
            titleRect.sizeDelta = new Vector2(560f, 48f);
            title.fontSize = 30f;
            title.color = new Color(.96f, .93f, .84f, 1f);
            title.alignment = TextAlignmentOptions.MidlineLeft;

            Image accent = CreateImage(header, "AccentLine");
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 1f);
            accentRect.anchoredPosition = new Vector2(44f, -73f);
            accentRect.sizeDelta = new Vector2(360f, 3f);
            accent.color = new Color(1f, .69f, .25f, .96f);
            accent.raycastTarget = false;

            RectTransform outlines = CreateRect(canvasRect, "Outlines");
            RectTransform connectors = CreateRect(canvasRect, "Connectors");
            RectTransform hotspots = CreateRect(canvasRect, "Hotspots");

            foreach (string name in HotspotNames)
            {
                MainRoomOverlayEditablePath path = CreateOutline(outlines, name);
                path.SetPoints(DefaultOutline(name));

                MainRoomOverlayDottedConnector connector = CreateConnector(connectors, name);
                ConfigureConnectorRect(connector.rectTransform, name);

                Image hotspot = CreateImage(hotspots, "Hotspot_" + name);
                hotspot.preserveAspect = true;
                hotspot.raycastTarget = false;
                ConfigureHotspotRect(hotspot.rectTransform, name);
            }

            ApplyImportedAssets(root, iconRoot);
        }

        private static void ApplyImportedAssets(GameObject root, string iconRoot)
        {
            Transform hotspots = FindTransform(root.transform, "Hotspots");
            if (!hotspots) return;

            foreach (string name in HotspotNames)
            {
                Transform child = hotspots.Find("Hotspot_" + name);
                if (!child) continue;
                Image image = child.GetComponent<Image>();
                if (!image) continue;

                string iconName = IconByHotspot[name];
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    iconRoot.TrimEnd('/') + "/" + iconName + ".png");
            }

            Transform reference = FindTransform(root.transform, "ReferenceOracle");
            if (reference && reference.TryGetComponent(out RawImage raw))
                raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferencePath);
        }

        private static MainRoomOverlayEditablePath CreateOutline(RectTransform parent, string name)
        {
            GameObject go = new GameObject("Outline_" + name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(MainRoomOverlayEditablePath));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            MainRoomOverlayEditablePath path = go.GetComponent<MainRoomOverlayEditablePath>();
            path.raycastTarget = false;
            return path;
        }

        private static MainRoomOverlayDottedConnector CreateConnector(RectTransform parent, string name)
        {
            GameObject go = new GameObject("Connector_" + name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(MainRoomOverlayDottedConnector));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            MainRoomOverlayDottedConnector connector = go.GetComponent<MainRoomOverlayDottedConnector>();
            connector.raycastTarget = false;
            return connector;
        }

        private static Image CreateImage(RectTransform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static RawImage CreateRawImage(RectTransform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            return go.GetComponent<RawImage>();
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, string value)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(RectTransform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ConfigureHotspotRect(RectTransform rect, string name)
        {
            Vector2 center = name switch
            {
                "Window" => new Vector2(697f, 129f),
                "DeskLamp" => new Vector2(1151f, 195f),
                "Swords" => new Vector2(1404f, 297f),
                "Door" => new Vector2(1748f, 133f),
                "Laptop" => new Vector2(1093f, 490f),
                "Cup" => new Vector2(831f, 593f),
                "FloorLamp" => new Vector2(139f, 400f),
                "Cat" => new Vector2(297f, 782f),
                _ => new Vector2(960f, 540f)
            };
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(center.x, -center.y);
            rect.sizeDelta = new Vector2(96f, 96f);
        }

        private static void ConfigureConnectorRect(RectTransform rect, string name)
        {
            Vector2 center = name switch
            {
                "Window" => new Vector2(697f, 196f),
                "DeskLamp" => new Vector2(1151f, 258f),
                "Swords" => new Vector2(1404f, 360f),
                "Door" => new Vector2(1748f, 200f),
                "Laptop" => new Vector2(1093f, 555f),
                "Cup" => new Vector2(831f, 654f),
                "FloorLamp" => new Vector2(139f, 463f),
                "Cat" => new Vector2(297f, 845f),
                _ => new Vector2(960f, 540f)
            };
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(center.x, -center.y);
            rect.sizeDelta = new Vector2(22f, 70f);
        }

        private static IEnumerable<Vector2> DefaultOutline(string name)
        {
            switch (name)
            {
                case "Window":
                    return new[] { P(398, 35), P(1006, 35), P(1006, 553), P(398, 553) };
                case "DeskLamp":
                    return new[] { P(1037, 335), P(1050, 274), P(1077, 247), P(1085, 219), P(1111, 213), P(1122, 242), P(1161, 258), P(1147, 279), P(1112, 268), P(1096, 295), P(1065, 304) };
                case "Swords":
                    return new[] { P(1163, 335), P(1397, 338), P(1411, 349), P(1396, 357), P(1161, 351) };
                case "Door":
                    return new[] { P(1592, 33), P(1832, 33), P(1832, 610), P(1592, 610) };
                case "Laptop":
                    return new[] { P(886, 522), P(1169, 522), P(1194, 628), P(1144, 643), P(878, 646), P(853, 620) };
                case "Cup":
                    return new[] { P(781, 632), P(846, 632), P(852, 682), P(790, 686) };
                case "FloorLamp":
                    return new[] { P(18, 443), P(170, 443), P(188, 718), P(14, 718) };
                case "Cat":
                    return new[] { P(160, 866), P(200, 823), P(289, 810), P(377, 826), P(420, 875), P(399, 927), P(289, 955), P(183, 932) };
                default:
                    return Array.Empty<Vector2>();
            }
        }

        private static Vector2 P(float x, float y) => new Vector2(x, y);

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static Transform FindTransform(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
            string[] parts = normalized.Split('/');
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
