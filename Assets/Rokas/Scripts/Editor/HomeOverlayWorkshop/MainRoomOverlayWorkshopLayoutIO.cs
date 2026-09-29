using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rokas.EditorTools
{
    public static class MainRoomOverlayWorkshopLayoutIO
    {
        public const int SchemaVersion = 2;
        public const int LegacySchemaVersion = 1;
        public const string DefaultFileName = "ROKAS_MAIN_ROOM_OVERLAY_LAYOUT.json";

        [Serializable]
        private sealed class Document
        {
            public int schemaVersion = SchemaVersion;
            public List<RectState> rects = new List<RectState>();
            public List<ComponentState> outlines = new List<ComponentState>();
            public List<ComponentState> connectors = new List<ComponentState>();
            public List<ImageState> images = new List<ImageState>();
            public List<TextState> texts = new List<TextState>();
            public List<GlowState> glow = new List<GlowState>();
        }

        [Serializable]
        private sealed class RectState
        {
            public string path;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector3 localScale;
            public float rotationZ;
            public bool active;
        }

        [Serializable]
        private sealed class ComponentState
        {
            public string path;
            public string json;
        }

        [Serializable]
        private sealed class ImageState
        {
            public string path;
            public string spriteAssetPath;
            public Color color;
            public bool preserveAspect;
        }

        [Serializable]
        private sealed class TextState
        {
            public string path;
            public string text;
            public float fontSize;
            public Color color;
        }

        [Serializable]
        private sealed class GlowState
        {
            public string stableId;
            public string path;
            public string parentPath;
            public string objectName;
            public string spriteAssetPath;
            public RectState rect = new RectState();
            public Color color = Color.white;
            public bool preserveAspect;
            public int imageType;
            public bool fillCenter;
            public bool hazeElement;
            public float brightness = 1f;
            public bool pulseEnabled;
            public float pulseSpeed = .32f;
            public float pulseAmount = .07f;
            public bool shimmerEnabled;
            public float shimmerSpeed = .18f;
            public float shimmerAmount = .22f;
            public float shimmerWidth = .10f;
            public bool hazeEnabled;
            public float hazeOpacity = .16f;
            public float phaseOffset;
        }

        [MenuItem("ROKAS/Main Room Overlay Workshop/Export Layout JSON...")]
        public static void ExportActiveWorkshop()
        {
            GameObject root = FindWorkshopRoot(SceneManager.GetActiveScene());
            if (!root)
            {
                EditorUtility.DisplayDialog("ROKAS Main Room Overlay Workshop",
                    "Сначала откройте workshop-сцену.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export ROKAS Main Room Overlay Layout",
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                DefaultFileName,
                "json");
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, CaptureJson(root), new UTF8Encoding(false));
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("ROKAS/Main Room Overlay Workshop/Import Layout JSON...")]
        public static void ImportIntoActiveWorkshop()
        {
            GameObject root = FindWorkshopRoot(SceneManager.GetActiveScene());
            if (!root)
            {
                EditorUtility.DisplayDialog("ROKAS Main Room Overlay Workshop",
                    "Сначала откройте workshop-сцену.", "OK");
                return;
            }

            string path = EditorUtility.OpenFilePanel(
                "Import ROKAS Main Room Overlay Layout",
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "json");
            if (string.IsNullOrEmpty(path)) return;

            ApplyJson(root, File.ReadAllText(path, Encoding.UTF8));
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        public static string CaptureJson(GameObject root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            var document = new Document();

            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.GetComponent<MainRoomOverlayCustomGlowElement>()) continue;
                document.rects.Add(CaptureRect(root.transform, rect));
            }

            foreach (MainRoomOverlayEditablePath path in root.GetComponentsInChildren<MainRoomOverlayEditablePath>(true))
            {
                document.outlines.Add(new ComponentState
                {
                    path = RelativePath(root.transform, path.transform),
                    json = EditorJsonUtility.ToJson(path)
                });
            }

            foreach (MainRoomOverlayDottedConnector connector in root.GetComponentsInChildren<MainRoomOverlayDottedConnector>(true))
            {
                document.connectors.Add(new ComponentState
                {
                    path = RelativePath(root.transform, connector.transform),
                    json = EditorJsonUtility.ToJson(connector)
                });
            }

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.GetComponent<MainRoomOverlayCustomGlowElement>()) continue;
                document.images.Add(new ImageState
                {
                    path = RelativePath(root.transform, image.transform),
                    spriteAssetPath = image.sprite ? AssetDatabase.GetAssetPath(image.sprite) : string.Empty,
                    color = image.color,
                    preserveAspect = image.preserveAspect
                });
            }

            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                document.texts.Add(new TextState
                {
                    path = RelativePath(root.transform, text.transform),
                    text = text.text,
                    fontSize = text.fontSize,
                    color = text.color
                });
            }

            var seenGlowIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MainRoomOverlayCustomGlowElement marker in root.GetComponentsInChildren<MainRoomOverlayCustomGlowElement>(true))
            {
                if (string.IsNullOrWhiteSpace(marker.StableId) || !seenGlowIds.Add(marker.StableId))
                {
                    marker.StableId = "authored-" + Guid.NewGuid().ToString("N");
                    seenGlowIds.Add(marker.StableId);
                    EditorUtility.SetDirty(marker);
                    EditorSceneManager.MarkSceneDirty(root.scene);
                }

                RectTransform rect = marker.transform as RectTransform;
                Image image = marker.GetComponent<Image>();
                MainRoomOverlayGlowFx fx = marker.GetComponent<MainRoomOverlayGlowFx>();
                if (!rect || !image || !fx) continue;

                document.glow.Add(new GlowState
                {
                    stableId = marker.StableId,
                    path = RelativePath(root.transform, marker.transform),
                    parentPath = RelativePath(root.transform, marker.transform.parent),
                    objectName = marker.name,
                    spriteAssetPath = image.sprite ? AssetDatabase.GetAssetPath(image.sprite) : string.Empty,
                    rect = CaptureRect(root.transform, rect),
                    color = image.color,
                    preserveAspect = image.preserveAspect,
                    imageType = (int)image.type,
                    fillCenter = image.fillCenter,
                    hazeElement = marker.HazeElement,
                    brightness = fx.Brightness,
                    pulseEnabled = fx.PulseEnabled,
                    pulseSpeed = fx.PulseSpeed,
                    pulseAmount = fx.PulseAmount,
                    shimmerEnabled = fx.ShimmerEnabled,
                    shimmerSpeed = fx.ShimmerSpeed,
                    shimmerAmount = fx.ShimmerAmount,
                    shimmerWidth = fx.ShimmerWidth,
                    hazeEnabled = fx.HazeEnabled,
                    hazeOpacity = fx.HazeOpacity,
                    phaseOffset = fx.PhaseOffset
                });
            }

            return JsonUtility.ToJson(document, true);
        }

        public static void ApplyJson(GameObject root, string json)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Layout JSON is empty.", nameof(json));

            Document document = JsonUtility.FromJson<Document>(json);
            if (document == null) throw new InvalidDataException("Layout JSON could not be parsed.");
            if (document.schemaVersion != LegacySchemaVersion && document.schemaVersion != SchemaVersion)
                throw new InvalidDataException("Unsupported Main Room workshop layout schema: " + document.schemaVersion);

            Undo.RegisterFullObjectHierarchyUndo(root, "Import Main Room workshop layout");

            foreach (RectState state in document.rects ?? new List<RectState>())
            {
                Transform target = FindRelative(root.transform, state.path);
                if (target is RectTransform rect) ApplyRect(rect, state);
            }

            foreach (ComponentState state in document.outlines ?? new List<ComponentState>())
            {
                Transform target = FindRelative(root.transform, state.path);
                if (!target || !target.TryGetComponent(out MainRoomOverlayEditablePath path)) continue;
                EditorJsonUtility.FromJsonOverwrite(state.json, path);
                path.SetVerticesDirty();
                EditorUtility.SetDirty(path);
            }

            foreach (ComponentState state in document.connectors ?? new List<ComponentState>())
            {
                Transform target = FindRelative(root.transform, state.path);
                if (!target || !target.TryGetComponent(out MainRoomOverlayDottedConnector connector)) continue;
                EditorJsonUtility.FromJsonOverwrite(state.json, connector);
                connector.SetVerticesDirty();
                EditorUtility.SetDirty(connector);
            }

            foreach (ImageState state in document.images ?? new List<ImageState>())
            {
                Transform target = FindRelative(root.transform, state.path);
                if (!target || !target.TryGetComponent(out Image image)) continue;
                image.sprite = string.IsNullOrEmpty(state.spriteAssetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Sprite>(state.spriteAssetPath);
                image.color = state.color;
                image.preserveAspect = state.preserveAspect;
                EditorUtility.SetDirty(image);
            }

            foreach (TextState state in document.texts ?? new List<TextState>())
            {
                Transform target = FindRelative(root.transform, state.path);
                if (!target || !target.TryGetComponent(out TextMeshProUGUI text)) continue;
                text.text = state.text;
                text.fontSize = state.fontSize;
                text.color = state.color;
                EditorUtility.SetDirty(text);
            }

            if (document.schemaVersion >= SchemaVersion)
                ApplyGlowStates(root, document.glow ?? new List<GlowState>());

            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static void ApplyGlowStates(GameObject root, List<GlowState> states)
        {
            MainRoomOverlayCustomGlowAuthoring.PrepareCustomGlowLayer(root, false);

            var desiredIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GlowState state in states)
                if (!string.IsNullOrWhiteSpace(state.stableId)) desiredIds.Add(state.stableId);

            MainRoomOverlayCustomGlowElement[] existing = root.GetComponentsInChildren<MainRoomOverlayCustomGlowElement>(true);
            foreach (MainRoomOverlayCustomGlowElement marker in existing)
            {
                if (desiredIds.Contains(marker.StableId)) continue;
                Undo.DestroyObjectImmediate(marker.gameObject);
            }

            foreach (GlowState state in states)
            {
                if (string.IsNullOrWhiteSpace(state.stableId)) continue;
                MainRoomOverlayCustomGlowElement marker = FindGlowById(root, state.stableId);
                if (!marker)
                {
                    GameObject created = MainRoomOverlayCustomGlowAuthoring.CreateFromLayout(root, state.parentPath,
                        state.objectName, state.stableId, state.spriteAssetPath, state.hazeElement);
                    marker = created.GetComponent<MainRoomOverlayCustomGlowElement>();
                }

                marker.StableId = state.stableId;
                marker.HazeElement = state.hazeElement;
                marker.gameObject.name = string.IsNullOrWhiteSpace(state.objectName) ? marker.gameObject.name : state.objectName;

                RectTransform rect = marker.transform as RectTransform;
                if (rect != null) ApplyRect(rect, state.rect);

                Image image = marker.GetComponent<Image>();
                if (image)
                {
                    image.sprite = string.IsNullOrEmpty(state.spriteAssetPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<Sprite>(state.spriteAssetPath);
                    image.color = state.color;
                    image.preserveAspect = state.preserveAspect;
                    image.type = Enum.IsDefined(typeof(Image.Type), state.imageType)
                        ? (Image.Type)state.imageType
                        : Image.Type.Simple;
                    image.fillCenter = state.fillCenter;
                    EditorUtility.SetDirty(image);
                }

                MainRoomOverlayGlowFx fx = marker.GetComponent<MainRoomOverlayGlowFx>();
                if (fx)
                {
                    fx.Brightness = state.brightness;
                    fx.PulseEnabled = state.pulseEnabled;
                    fx.PulseSpeed = state.pulseSpeed;
                    fx.PulseAmount = state.pulseAmount;
                    fx.ShimmerEnabled = state.shimmerEnabled;
                    fx.ShimmerSpeed = state.shimmerSpeed;
                    fx.ShimmerAmount = state.shimmerAmount;
                    fx.ShimmerWidth = state.shimmerWidth;
                    fx.HazeEnabled = state.hazeEnabled;
                    fx.HazeOpacity = state.hazeOpacity;
                    fx.PhaseOffset = state.phaseOffset;
                    EditorUtility.SetDirty(fx);
                }

                EditorUtility.SetDirty(marker);
            }
        }

        private static RectState CaptureRect(Transform root, RectTransform rect)
        {
            return new RectState
            {
                path = RelativePath(root, rect.transform),
                anchoredPosition = rect.anchoredPosition,
                sizeDelta = rect.sizeDelta,
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                localScale = rect.localScale,
                rotationZ = rect.localEulerAngles.z,
                active = rect.gameObject.activeSelf
            };
        }

        private static void ApplyRect(RectTransform rect, RectState state)
        {
            if (rect == null || state == null) return;
            rect.anchorMin = state.anchorMin;
            rect.anchorMax = state.anchorMax;
            rect.pivot = state.pivot;
            rect.anchoredPosition = state.anchoredPosition;
            rect.sizeDelta = state.sizeDelta;
            rect.localScale = state.localScale;
            Vector3 euler = rect.localEulerAngles;
            euler.z = state.rotationZ;
            rect.localEulerAngles = euler;
            rect.gameObject.SetActive(state.active);
            EditorUtility.SetDirty(rect);
        }

        private static MainRoomOverlayCustomGlowElement FindGlowById(GameObject root, string stableId)
        {
            foreach (MainRoomOverlayCustomGlowElement marker in root.GetComponentsInChildren<MainRoomOverlayCustomGlowElement>(true))
                if (marker.StableId == stableId) return marker;
            return null;
        }

        private static string RelativePath(Transform root, Transform target)
        {
            return AnimationUtility.CalculateTransformPath(target, root);
        }

        private static Transform FindRelative(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            return root.Find(path);
        }

        private static GameObject FindWorkshopRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.name == "MainRoomOverlayWorkshop") return candidate;
            return null;
        }
    }
}
