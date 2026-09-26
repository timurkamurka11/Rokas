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
        public const int SchemaVersion = 1;
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
                document.rects.Add(new RectState
                {
                    path = RelativePath(root.transform, rect.transform),
                    anchoredPosition = rect.anchoredPosition,
                    sizeDelta = rect.sizeDelta,
                    anchorMin = rect.anchorMin,
                    anchorMax = rect.anchorMax,
                    pivot = rect.pivot,
                    localScale = rect.localScale,
                    rotationZ = rect.localEulerAngles.z,
                    active = rect.gameObject.activeSelf
                });
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

            return JsonUtility.ToJson(document, true);
        }

        public static void ApplyJson(GameObject root, string json)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Layout JSON is empty.", nameof(json));

            Document document = JsonUtility.FromJson<Document>(json);
            if (document == null) throw new InvalidDataException("Layout JSON could not be parsed.");
            if (document.schemaVersion != SchemaVersion)
                throw new InvalidDataException("Unsupported Main Room workshop layout schema: " + document.schemaVersion);

            Undo.RegisterFullObjectHierarchyUndo(root, "Import Main Room workshop layout");

            foreach (RectState state in document.rects ?? new List<RectState>())
            {
                Transform target = FindRelative(root.transform, state.path);
                if (!(target is RectTransform rect)) continue;
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

            EditorSceneManager.MarkSceneDirty(root.scene);
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
