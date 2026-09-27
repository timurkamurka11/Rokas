using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rokas.EditorTools
{
    public static class MainRoomOverlayCustomGlowAuthoring
    {
        public const string GlowRootAssetPath = MainRoomOverlayCustomGlowImporter.SpriteRoot;
        public const string HazeRootAssetPath = MainRoomOverlayCustomGlowImporter.HazeRoot;

        private sealed class Definition
        {
            public string Id;
            public string SpriteName;
            public string Group;
            public Vector2 Size;
            public Vector2 Position;
            public float Phase;
        }

        private static readonly Definition[] StarterDefinitions =
        {
            D("starter-long-horizontal", "LongHorizontal", "GlowLines", new Vector2(430f, 68f), new Vector2(430f, -1005f), .00f),
            D("starter-long-vertical", "LongVertical", "GlowLines", new Vector2(72f, 330f), new Vector2(1760f, -830f), .13f),
            D("starter-corner-left", "CornerLeft", "GlowLines", new Vector2(150f, 150f), new Vector2(1220f, -995f), .26f),
            D("starter-corner-right", "CornerRight", "GlowLines", new Vector2(150f, 150f), new Vector2(1390f, -995f), .39f),
            D("starter-rounded-frame", "RoundedFrame", "GlowFrames", new Vector2(280f, 185f), new Vector2(970f, -970f), .52f),
            D("starter-dot-stack", "DotStackLong", "GlowConnectors", new Vector2(58f, 170f), new Vector2(1830f, -865f), .65f),
            D("starter-bent-line", "BentLineA", "GlowConnectors", new Vector2(300f, 86f), new Vector2(690f, -990f), .78f)
        };

        public static readonly string[] AvailableSpriteNames = MainRoomOverlayCustomGlowImporter.SpriteNames;
        public static readonly string[] AvailableHazeNames = MainRoomOverlayCustomGlowImporter.HazeNames;

        [MenuItem("ROKAS/Main Room Overlay Workshop/Prepare Custom Glow Layer")]
        public static void PrepareActiveWorkshop()
        {
            GameObject root = FindWorkshopRoot(SceneManager.GetActiveScene());
            if (!root)
            {
                EditorUtility.DisplayDialog("ROKAS Main Room Overlay Workshop",
                    "Сначала откройте workshop-сцену.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Prepare Main Room custom glow layer");
            MainRoomOverlayCustomGlowImporter.ConfigureProjectAssets();
            PrepareCustomGlowLayer(root, true);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        [MenuItem("ROKAS/Main Room Overlay Workshop/Open Custom Glow Palette")]
        public static void OpenPalette()
        {
            MainRoomOverlayGlowPaletteWindow.ShowWindow();
        }

        public static void PrepareForReviewBatch()
        {
            MainRoomOverlayCustomGlowImporter.ConfigureProjectAssets();
            Scene scene = EditorSceneManager.OpenScene(MainRoomOverlayWorkshopBuilder.ScenePath, OpenSceneMode.Single);
            GameObject root = FindWorkshopRoot(scene);
            if (!root) throw new InvalidOperationException("MainRoomOverlayWorkshop root was not found.");

            MainRoomOverlayWorkshopBuilder.RefreshImportedAssets();
            PrepareCustomGlowLayer(root, true);
            if (!EditorSceneManager.SaveScene(scene, MainRoomOverlayWorkshopBuilder.ScenePath))
                throw new InvalidOperationException("Could not save prepared Main Room workshop scene.");
            AssetDatabase.SaveAssets();
        }

        public static void PrepareCustomGlowLayer(GameObject root, bool createStarters)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            Transform canvas = FindTransform(root.transform, "WorkshopCanvas");
            if (!canvas) throw new InvalidOperationException("WorkshopCanvas was not found.");

            RectTransform customGlow = EnsureStretchRect(canvas, "CustomGlow");
            EnsureStretchRect(customGlow, "GlowLines");
            EnsureStretchRect(customGlow, "GlowConnectors");
            EnsureStretchRect(customGlow, "GlowFrames");
            RectTransform fxRoot = EnsureStretchRect(customGlow, "GlowFX");
            PlaceCustomGlowBeforeHotspots(canvas, customGlow);

            if (createStarters)
            {
                foreach (Definition definition in StarterDefinitions)
                    EnsurePiece(root, definition);
            }

            EnsureHazePreview(fxRoot, "HazeLong_01", "haze-preview-01",
                new Vector2(510f, -900f), new Vector2(430f, 72f));
        }

        public static GameObject AddPiece(GameObject root, string spriteName)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (Array.IndexOf(AvailableSpriteNames, spriteName) < 0)
                throw new ArgumentOutOfRangeException(nameof(spriteName), spriteName, "Unknown glow sprite.");

            PrepareCustomGlowLayer(root, false);
            Transform canvas = FindTransform(root.transform, "WorkshopCanvas");
            RectTransform customGlow = canvas.Find("CustomGlow") as RectTransform;
            string groupName = GroupForSprite(spriteName);
            RectTransform group = EnsureStretchRect(customGlow, groupName);

            string stableId = "manual-" + Guid.NewGuid().ToString("N");
            GameObject go = CreateGlowImage(group, "Glow_" + spriteName, stableId,
                GlowRootAssetPath + "/" + spriteName + ".png", new Vector2(960f, -540f), DefaultSize(spriteName), .11f, false);
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(root.scene);
            return go;
        }

        public static GameObject AddHaze(GameObject root, string spriteName)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (Array.IndexOf(AvailableHazeNames, spriteName) < 0)
                throw new ArgumentOutOfRangeException(nameof(spriteName), spriteName, "Unknown haze sprite.");

            PrepareCustomGlowLayer(root, false);
            Transform canvas = FindTransform(root.transform, "WorkshopCanvas");
            RectTransform customGlow = canvas.Find("CustomGlow") as RectTransform;
            RectTransform group = EnsureStretchRect(customGlow, "GlowFX");

            string stableId = "manual-haze-" + Guid.NewGuid().ToString("N");
            GameObject go = CreateGlowImage(group, "Glow_" + spriteName, stableId,
                HazeRootAssetPath + "/" + spriteName + ".png", new Vector2(960f, -540f), new Vector2(420f, 90f), .37f, true);
            MainRoomOverlayGlowFx fx = go.GetComponent<MainRoomOverlayGlowFx>();
            fx.HazeEnabled = true;
            fx.HazeOpacity = .16f;
            fx.PulseEnabled = false;
            fx.ShimmerEnabled = false;
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(root.scene);
            return go;
        }

        public static GameObject CreateFromLayout(GameObject root, string parentPath, string objectName,
            string stableId, string spriteAssetPath, bool hazeElement)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            PrepareCustomGlowLayer(root, false);

            Transform parent = FindRelative(root.transform, parentPath);
            if (!(parent is RectTransform parentRect))
            {
                Transform canvas = FindTransform(root.transform, "WorkshopCanvas");
                RectTransform customGlow = canvas.Find("CustomGlow") as RectTransform;
                string fallbackGroup = hazeElement ? "GlowFX" : "GlowLines";
                parentRect = EnsureStretchRect(customGlow, fallbackGroup);
            }

            return CreateGlowImage(parentRect,
                string.IsNullOrWhiteSpace(objectName) ? "Glow_Restored" : objectName,
                stableId, spriteAssetPath, new Vector2(960f, -540f), new Vector2(180f, 90f), .0f, hazeElement);
        }

        private static void EnsurePiece(GameObject root, Definition definition)
        {
            MainRoomOverlayCustomGlowElement existing = FindGlowById(root, definition.Id);
            if (existing) return;

            Transform canvas = FindTransform(root.transform, "WorkshopCanvas");
            RectTransform customGlow = canvas.Find("CustomGlow") as RectTransform;
            RectTransform group = EnsureStretchRect(customGlow, definition.Group);
            CreateGlowImage(group, "Glow_" + definition.SpriteName, definition.Id,
                GlowRootAssetPath + "/" + definition.SpriteName + ".png",
                definition.Position, definition.Size, definition.Phase, false);
        }

        private static GameObject CreateGlowImage(RectTransform parent, string name, string stableId,
            string spritePath, Vector2 position, Vector2 size, float phase, bool hazeElement)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(MainRoomOverlayCustomGlowElement), typeof(MainRoomOverlayGlowFx));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            image.raycastTarget = false;
            ApplyImageType(image);

            MainRoomOverlayCustomGlowElement marker = go.GetComponent<MainRoomOverlayCustomGlowElement>();
            marker.StableId = stableId;
            marker.HazeElement = hazeElement;

            MainRoomOverlayGlowFx fx = go.GetComponent<MainRoomOverlayGlowFx>();
            fx.Brightness = 1f;
            fx.PulseEnabled = !hazeElement;
            fx.PulseAmount = hazeElement ? .02f : .055f;
            fx.PulseSpeed = .28f;
            fx.ShimmerEnabled = false;
            fx.HazeEnabled = hazeElement;
            fx.HazeOpacity = hazeElement ? .16f : 1f;
            fx.PhaseOffset = phase;
            return go;
        }

        private static void ApplyImageType(Image image)
        {
            if (!image || !image.sprite) return;
            if (image.sprite.border.sqrMagnitude > 0.001f)
            {
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                image.preserveAspect = false;
            }
            else
            {
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
        }

        private static void EnsureHazePreview(RectTransform fxRoot, string spriteName, string stableId,
            Vector2 position, Vector2 size)
        {
            foreach (MainRoomOverlayCustomGlowElement marker in fxRoot.GetComponentsInChildren<MainRoomOverlayCustomGlowElement>(true))
                if (marker.StableId == stableId) return;

            GameObject go = CreateGlowImage(fxRoot, "GlowHaze_Preview", stableId,
                HazeRootAssetPath + "/" + spriteName + ".png", position, size, .41f, true);
            MainRoomOverlayGlowFx fx = go.GetComponent<MainRoomOverlayGlowFx>();
            fx.HazeEnabled = true;
            fx.HazeOpacity = .16f;
            go.SetActive(false);
        }

        private static string GroupForSprite(string spriteName)
        {
            if (spriteName.IndexOf("Frame", StringComparison.Ordinal) >= 0) return "GlowFrames";
            if (spriteName.IndexOf("Dot", StringComparison.Ordinal) >= 0 ||
                spriteName.IndexOf("Bent", StringComparison.Ordinal) >= 0 ||
                spriteName.IndexOf("Curved", StringComparison.Ordinal) >= 0)
                return "GlowConnectors";
            return "GlowLines";
        }

        private static Vector2 DefaultSize(string spriteName)
        {
            return spriteName switch
            {
                "LongHorizontal" => new Vector2(430f, 68f),
                "MediumHorizontal" => new Vector2(280f, 84f),
                "LongVertical" => new Vector2(72f, 330f),
                "ShortVertical" => new Vector2(70f, 180f),
                "CornerLeft" => new Vector2(150f, 150f),
                "CornerRight" => new Vector2(150f, 150f),
                "RoundedFrame" => new Vector2(280f, 185f),
                "SingleDot" => new Vector2(64f, 64f),
                "DotStackLong" => new Vector2(58f, 170f),
                "DotStackShort" => new Vector2(58f, 105f),
                "BentLineA" => new Vector2(300f, 86f),
                "BentLineB" => new Vector2(300f, 86f),
                "CurvedLine" => new Vector2(320f, 108f),
                _ => new Vector2(180f, 90f)
            };
        }

        private static Definition D(string id, string spriteName, string group, Vector2 size, Vector2 position, float phase)
        {
            return new Definition { Id = id, SpriteName = spriteName, Group = group, Size = size, Position = position, Phase = phase };
        }

        private static RectTransform EnsureStretchRect(Transform parent, string name)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            Transform existing = parent.Find(name);
            if (existing is RectTransform existingRect) return existingRect;

            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void PlaceCustomGlowBeforeHotspots(Transform canvas, RectTransform customGlow)
        {
            Transform hotspots = canvas.Find("Hotspots");
            if (!hotspots) return;
            int hotspotIndex = hotspots.GetSiblingIndex();
            customGlow.SetSiblingIndex(Mathf.Max(0, hotspotIndex));
            hotspots.SetAsLastSibling();
        }

        private static MainRoomOverlayCustomGlowElement FindGlowById(GameObject root, string stableId)
        {
            foreach (MainRoomOverlayCustomGlowElement marker in root.GetComponentsInChildren<MainRoomOverlayCustomGlowElement>(true))
                if (marker.StableId == stableId) return marker;
            return null;
        }

        private static Transform FindRelative(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            return root.Find(path);
        }

        private static Transform FindTransform(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static GameObject FindWorkshopRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.name == "MainRoomOverlayWorkshop") return candidate;
            return null;
        }
    }

    public sealed class MainRoomOverlayGlowPaletteWindow : EditorWindow
    {
        [MenuItem("ROKAS/Main Room Overlay Workshop/Glow Palette")]
        public static void ShowWindow()
        {
            GetWindow<MainRoomOverlayGlowPaletteWindow>("ROKAS Glow Palette");
        }

        private Vector2 scroll;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Custom Glow Pieces", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Нажмите Add — элемент появится в центре workshop. Затем двигайте, масштабируйте и вращайте его обычным Rect Tool.",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Lines / Frames / Connectors", EditorStyles.boldLabel);
            foreach (string spriteName in MainRoomOverlayCustomGlowAuthoring.AvailableSpriteNames)
                DrawAddRow(spriteName, false);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Optional Haze", EditorStyles.boldLabel);
            foreach (string spriteName in MainRoomOverlayCustomGlowAuthoring.AvailableHazeNames)
                DrawAddRow(spriteName, true);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawAddRow(string spriteName, bool haze)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(spriteName);
                if (GUILayout.Button("Add", GUILayout.Width(70f))) Add(spriteName, haze);
            }
        }

        private static void Add(string spriteName, bool haze)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root = null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.name == "MainRoomOverlayWorkshop") { root = candidate; break; }
            if (!root)
            {
                EditorUtility.DisplayDialog("ROKAS Glow Palette", "Сначала откройте Main Room workshop.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Add custom glow piece");
            MainRoomOverlayCustomGlowAuthoring.PrepareCustomGlowLayer(root, false);
            if (haze) MainRoomOverlayCustomGlowAuthoring.AddHaze(root, spriteName);
            else MainRoomOverlayCustomGlowAuthoring.AddPiece(root, spriteName);
        }
    }
}
