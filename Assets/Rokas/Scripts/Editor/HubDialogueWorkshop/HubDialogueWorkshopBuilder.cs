using System;
using System.IO;
using System.Reflection;
using Rokas.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rokas.EditorTools
{
    public static class HubDialogueWorkshopBuilder
    {
        public const string ScenePath =
            "Assets/Rokas/EditorWorkshops/HubDialogueWorkshop.unity";
        public const string DefaultConfigPath =
            "Assets/Rokas/Resources/HubDialogue/HubDialogueConfig.json";
        public const string UserConfigPath =
            "Assets/Rokas/Resources/HubDialogue/HubDialogueConfig.user.json";

        private const string HomeTexturePath =
            "Assets/Rokas/Art/Home/ApartmentNight.png";
        private const string PlaqueTexturePath =
            "Assets/Rokas/Art/Home/HubDialogue/HubDialoguePlaque.png";
        private const string PortraitTexturePath =
            "Assets/Rokas/Art/Home/HubDialogue/KeikoHubPortraitAtlas.png";
        private const string AssetsPath =
            "Assets/Rokas/Resources/RokasAssets.asset";

        [MenuItem("ROKAS/Hub Dialogue Workshop/Open or Create")]
        public static void OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (File.Exists(ToAbsoluteProjectPath(ScenePath)))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                EnsureAssetFolder("Assets/Rokas/EditorWorkshops");
                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                Populate(scene, LoadEditableConfig());
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException(
                        "Could not save " + ScenePath);
            }

            HubDialogueWorkshopWindow.ShowWindow();
            Select("HubDialoguePlaque");
        }

        [MenuItem("ROKAS/Hub Dialogue Workshop/Save Layout To Runtime")]
        public static void SaveLayoutToRuntime()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsWorkshopScene(scene))
            {
                EditorUtility.DisplayDialog(
                    "ROKAS Hub Dialogue Workshop",
                    "Open the Hub Dialogue Workshop first.",
                    "OK");
                return;
            }

            HubDialogueConfigData config = LoadEditableConfig();
            CaptureScene(scene, config);
            WriteUserConfig(config);
        }

        [MenuItem("ROKAS/Hub Dialogue Workshop/Rebuild Preview From Config")]
        public static void RebuildPreviewFromConfig()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsWorkshopScene(scene))
            {
                OpenOrCreate();
                return;
            }

            GameObject root = FindRoot(scene, "HubDialogueWorkshop");
            if (root) UnityEngine.Object.DestroyImmediate(root);
            Populate(scene, LoadEditableConfig());
            EditorSceneManager.MarkSceneDirty(scene);
            Select("HubDialoguePlaque");
        }

        public static Scene CreateUnsavedWorkshopForTests()
        {
            return CreateUnsavedWorkshopForTests(
                HubDialogueConfig.LoadFresh());
        }

        public static Scene CreateUnsavedWorkshopForTests(
            HubDialogueConfigData config)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Populate(
                scene,
                HubDialogueConfig.NormalizeForAuthoring(
                    config ?? new HubDialogueConfigData()));
            return scene;
        }

        public static HubDialogueConfigData CaptureForTests(
            Scene scene,
            HubDialogueConfigData data)
        {
            CaptureScene(scene, data);
            return data;
        }

        public static HubDialogueConfigData LoadEditableConfig()
        {
            string path = File.Exists(ToAbsoluteProjectPath(UserConfigPath))
                ? UserConfigPath
                : DefaultConfigPath;
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (!asset || string.IsNullOrWhiteSpace(asset.text))
                return HubDialogueConfig.NormalizeForAuthoring(
                    new HubDialogueConfigData());
            try
            {
                HubDialogueConfigData data =
                    JsonUtility.FromJson<HubDialogueConfigData>(asset.text);
                return HubDialogueConfig.NormalizeForAuthoring(
                    data ?? new HubDialogueConfigData());
            }
            catch
            {
                return HubDialogueConfig.NormalizeForAuthoring(
                    new HubDialogueConfigData());
            }
        }

        public static void WriteUserConfig(HubDialogueConfigData config)
        {
            config = HubDialogueConfig.NormalizeForAuthoring(
                config ?? new HubDialogueConfigData());
            string absolute = ToAbsoluteProjectPath(UserConfigPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllText(
                absolute,
                JsonUtility.ToJson(config, true),
                new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                UserConfigPath,
                ImportAssetOptions.ForceUpdate);
            HubDialogueConfig.ResetCache();
        }

        public static bool IsWorkshopScene(Scene scene)
        {
            return scene.IsValid() &&
                FindRoot(scene, "HubDialogueWorkshop") != null;
        }

        private static void Populate(
            Scene scene,
            HubDialogueConfigData config)
        {
            config = HubDialogueConfig.NormalizeForAuthoring(
                config ?? new HubDialogueConfigData());

            GameObject root =
                new GameObject("HubDialogueWorkshop");
            SceneManager.MoveGameObjectToScene(root, scene);

            GameObject canvasObject =
                new GameObject(
                    "WorkshopCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(
                root.transform,
                false);
            Canvas canvas =
                canvasObject.GetComponent<Canvas>();
            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            // Runtime uses a fixed 1920x1080 authored stage. The Workshop
            // deliberately uses the same pixel-space model rather than a
            // second ScaleWithScreenSize interpretation.
            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            RectTransform canvasRect =
                canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            RectTransform preview =
                CreateRect(
                    canvasRect,
                    "HubDialoguePreview",
                    new HubDialogueRectData(
                        0f,
                        0f,
                        1920f,
                        1080f));
            preview.anchorMin =
                preview.anchorMax =
                    new Vector2(.5f, .5f);
            preview.pivot =
                new Vector2(.5f, .5f);
            preview.anchoredPosition =
                Vector2.zero;
            preview.sizeDelta =
                new Vector2(1920f, 1080f);

            RawImage background =
                CreateRaw(
                    preview,
                    "MainRoomBackground",
                    new HubDialogueRectData(
                        0f,
                        0f,
                        1920f,
                        1080f));
            background.texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    HomeTexturePath);
            background.raycastTarget = false;

            RectTransform layout =
                CreateOptionalRect(
                    preview,
                    "HubDialogueLayoutRoot",
                    config.layoutRoot);

            RectTransform plaqueRoot =
                layout
                    ? CreateOptionalRect(
                        layout,
                        "HubDialoguePlaque",
                        config.plaque)
                    : null;

            if (plaqueRoot)
            {
                RawImage plaque =
                    CreateRaw(
                        plaqueRoot,
                        "PlaqueArt",
                        config.plaqueArt);
                if (plaque)
                {
                    plaque.texture =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            PlaqueTexturePath);
                    plaque.raycastTarget = false;
                }

                RectTransform maskRoot =
                    CreateOptionalRect(
                        plaqueRoot,
                        "PortraitMask",
                        config.portraitMask);
                if (maskRoot)
                {
                    var maskGraphic =
                        maskRoot.gameObject.AddComponent<
                            HubDialogueOctagonMaskGraphic>();
                    HubDialogueLayout.ApplyGraphic(
                        maskGraphic,
                        config.portraitMask);
                    maskGraphic.raycastTarget = false;

                    Mask mask =
                        maskRoot.gameObject.AddComponent<Mask>();
                    mask.showMaskGraphic = false;

                    RawImage portrait =
                        CreateRaw(
                            maskRoot,
                            "PortraitImage",
                            config.portrait);
                    if (portrait)
                    {
                        portrait.texture =
                            AssetDatabase.LoadAssetAtPath<Texture2D>(
                                PortraitTexturePath);
                        portrait.raycastTarget = false;
                    }
                }

                RokasAssets assets =
                    AssetDatabase.LoadAssetAtPath<RokasAssets>(
                        AssetsPath);
                Font font =
                    assets ? assets.sans : null;

                Text speaker =
                    CreateText(
                        plaqueRoot,
                        "SpeakerName",
                        config.speakerName,
                        config.speaker,
                        config.speakerFontSize,
                        config.speakerAlignment,
                        font);
                if (speaker)
                    speaker.fontStyle =
                        FontStyle.Bold;

                Text dialogue =
                    CreateText(
                        plaqueRoot,
                        "DialogueText",
                        config.guildIntroText,
                        config.dialogue,
                        config.dialogueFontSize,
                        config.dialogueAlignment,
                        font);
                if (dialogue)
                    dialogue.lineSpacing =
                        config.dialogueLineSpacing;

                RawImage cover =
                    CreateRaw(
                        plaqueRoot,
                        "BakedArrowCover",
                        config.arrowCover);
                if (cover)
                {
                    cover.texture =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            PlaqueTexturePath);
                    cover.raycastTarget = false;
                }

                RectTransform arrow =
                    CreateOptionalRect(
                        plaqueRoot,
                        "CompletionArrow",
                        config.completionArrow);
                if (arrow)
                {
                    HubDialogueTriangleGraphic triangle =
                        arrow.gameObject.AddComponent<
                            HubDialogueTriangleGraphic>();
                    HubDialogueLayout.ApplyGraphic(
                        triangle,
                        config.completionArrow);
                    triangle.raycastTarget = false;
                }

                CreateControlGuide(
                    plaqueRoot,
                    "HubMuteButton",
                    config.muteButton,
                    config.muteButtonEnabled);
                CreateControlGuide(
                    plaqueRoot,
                    "HubForwardButton",
                    config.forwardButton,
                    config.forwardButtonEnabled);
                CreateControlGuide(
                    plaqueRoot,
                    "HubMenuButton",
                    config.menuButton,
                    config.menuButtonEnabled);

                HubDialogueLayout.ApplySiblingOrder(
                    plaqueRoot,
                    new[]
                    {
                        "PlaqueArt",
                        "PortraitMask",
                        "SpeakerName",
                        "DialogueText",
                        "BakedArrowCover",
                        "CompletionArrow",
                        "HubMuteButton",
                        "HubForwardButton",
                        "HubMenuButton"
                    },
                    new[]
                    {
                        config.plaqueArt,
                        config.portraitMask,
                        config.speaker,
                        config.dialogue,
                        config.arrowCover,
                        config.completionArrow,
                        config.muteButton,
                        config.forwardButton,
                        config.menuButton
                    });
            }

            GameObject note =
                new GameObject(
                    "WorkshopHelpers",
                    typeof(RectTransform));
            note.transform.SetParent(
                canvasRect,
                false);
        }

        private static void CaptureScene(
            Scene scene,
            HubDialogueConfigData config)
        {
            config = HubDialogueConfig.NormalizeForAuthoring(
                config ?? new HubDialogueConfigData());

            GameObject root =
                FindRoot(scene, "HubDialogueWorkshop");
            if (!root)
                throw new InvalidOperationException(
                    "HubDialogueWorkshop root is missing.");

            Transform transform =
                root.transform;

            config.layoutRoot =
                CaptureOptionalRect(
                    transform,
                    "HubDialogueLayoutRoot",
                    config.layoutRoot);
            RectTransform layout =
                FindTransform(
                    transform,
                    "HubDialogueLayoutRoot")
                    as RectTransform;
            if (layout)
                config.layoutScale =
                    Mathf.Max(
                        .01f,
                        layout.localScale.x);

            config.plaque =
                CaptureOptionalRect(
                    transform,
                    "HubDialoguePlaque",
                    config.plaque);
            config.plaqueArt =
                CaptureOptionalRect(
                    transform,
                    "PlaqueArt",
                    config.plaqueArt);
            config.portraitMask =
                CaptureOptionalRect(
                    transform,
                    "PortraitMask",
                    config.portraitMask);
            config.portrait =
                CaptureOptionalRect(
                    transform,
                    "PortraitImage",
                    config.portrait);
            config.speaker =
                CaptureOptionalRect(
                    transform,
                    "SpeakerName",
                    config.speaker);
            config.dialogue =
                CaptureOptionalRect(
                    transform,
                    "DialogueText",
                    config.dialogue);
            config.arrowCover =
                CaptureOptionalRect(
                    transform,
                    "BakedArrowCover",
                    config.arrowCover);
            config.completionArrow =
                CaptureOptionalRect(
                    transform,
                    "CompletionArrow",
                    config.completionArrow);
            config.muteButton =
                CaptureOptionalRect(
                    transform,
                    "HubMuteButton",
                    config.muteButton);
            config.forwardButton =
                CaptureOptionalRect(
                    transform,
                    "HubForwardButton",
                    config.forwardButton);
            config.menuButton =
                CaptureOptionalRect(
                    transform,
                    "HubMenuButton",
                    config.menuButton);

            CaptureRawImage(
                transform,
                "PlaqueArt",
                config.plaqueArt);
            CaptureRawImage(
                transform,
                "PortraitImage",
                config.portrait);
            CaptureRawImage(
                transform,
                "BakedArrowCover",
                config.arrowCover);
            CaptureGraphic(
                transform,
                "PortraitMask",
                config.portraitMask);
            CaptureGraphic(
                transform,
                "CompletionArrow",
                config.completionArrow);

            Text speaker =
                FindComponent<Text>(
                    transform,
                    "SpeakerName");
            Text dialogue =
                FindComponent<Text>(
                    transform,
                    "DialogueText");

            if (speaker)
            {
                config.speakerName =
                    speaker.text;
                config.speakerFontSize =
                    speaker.fontSize;
                config.speakerAlignment =
                    speaker.alignment;
                HubDialogueLayout.CaptureGraphic(
                    speaker,
                    config.speaker);
            }

            if (dialogue)
            {
                config.dialogueFontSize =
                    dialogue.fontSize;
                config.dialogueAlignment =
                    dialogue.alignment;
                config.dialogueLineSpacing =
                    dialogue.lineSpacing;
                HubDialogueLayout.CaptureGraphic(
                    dialogue,
                    config.dialogue);
            }

            Transform mute =
                FindTransform(
                    transform,
                    "HubMuteButton");
            Transform forward =
                FindTransform(
                    transform,
                    "HubForwardButton");
            Transform menu =
                FindTransform(
                    transform,
                    "HubMenuButton");

            config.muteButtonEnabled =
                mute && mute.gameObject.activeSelf;
            config.forwardButtonEnabled =
                forward && forward.gameObject.activeSelf;
            config.menuButtonEnabled =
                menu && menu.gameObject.activeSelf;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            HubDialogueRectData data)
        {
            if (!parent)
                throw new InvalidOperationException(
                    "Workshop parent is missing for " + name);

            data =
                data ?? new HubDialogueRectData();

            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform));
            RectTransform rect =
                go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            HubDialogueLayout.Apply(rect, data);
            return rect;
        }

        private static RectTransform CreateOptionalRect(
            Transform parent,
            string name,
            HubDialogueRectData data)
        {
            if (!parent ||
                data == null ||
                !data.exists)
                return null;

            return CreateRect(
                parent,
                name,
                data);
        }

        private static RawImage CreateRaw(
            Transform parent,
            string name,
            HubDialogueRectData data)
        {
            RectTransform rect =
                CreateOptionalRect(
                    parent,
                    name,
                    data);
            if (!rect) return null;

            rect.gameObject.AddComponent<CanvasRenderer>();
            RawImage image =
                rect.gameObject.AddComponent<RawImage>();
            HubDialogueLayout.ApplyRawImage(
                image,
                data);
            return image;
        }

        private static RawImage CreateRaw(
            Transform parent,
            string name)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));
            RectTransform rect =
                go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return go.GetComponent<RawImage>();
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            HubDialogueRectData data,
            int fontSize,
            TextAnchor alignment,
            Font font)
        {
            RectTransform rect =
                CreateOptionalRect(
                    parent,
                    name,
                    data);
            if (!rect) return null;

            rect.gameObject.AddComponent<CanvasRenderer>();
            Text text =
                rect.gameObject.AddComponent<Text>();
            text.text =
                value ?? string.Empty;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow =
                HorizontalWrapMode.Wrap;
            text.verticalOverflow =
                VerticalWrapMode.Overflow;
            HubDialogueLayout.ApplyGraphic(
                text,
                data);
            return text;
        }

        private static void CreateControlGuide(
            Transform parent,
            string name,
            HubDialogueRectData data,
            bool active)
        {
            RectTransform rect =
                CreateOptionalRect(
                    parent,
                    name,
                    data);
            if (!rect) return;

            rect.gameObject.AddComponent<CanvasRenderer>();
            Image image =
                rect.gameObject.AddComponent<Image>();
            image.color =
                new Color(
                    1f,
                    .72f,
                    .22f,
                    .18f);
            image.raycastTarget = false;
            rect.gameObject.SetActive(
                active && data.active);
        }

        private static HubDialogueRectData ReadRect(
            RectTransform rect,
            HubDialogueRectData data = null)
        {
            if (!rect)
                throw new InvalidOperationException(
                    "Workshop RectTransform is missing.");

            data =
                data ?? new HubDialogueRectData();
            HubDialogueLayout.Capture(
                rect,
                data);
            return data;
        }

        private static HubDialogueRectData CaptureOptionalRect(
            Transform root,
            string name,
            HubDialogueRectData data)
        {
            data =
                data ?? new HubDialogueRectData();

            RectTransform rect =
                FindTransform(root, name)
                    as RectTransform;
            if (!rect)
            {
                data.exists = false;
                data.active = false;
                return data;
            }

            return ReadRect(
                rect,
                data);
        }

        private static void CaptureRawImage(
            Transform root,
            string name,
            HubDialogueRectData data)
        {
            RawImage image =
                FindComponent<RawImage>(
                    root,
                    name);
            if (image)
                HubDialogueLayout.CaptureRawImage(
                    image,
                    data);
        }

        private static void CaptureGraphic(
            Transform root,
            string name,
            HubDialogueRectData data)
        {
            Graphic graphic =
                FindComponent<Graphic>(
                    root,
                    name);
            if (graphic)
                HubDialogueLayout.CaptureGraphic(
                    graphic,
                    data);
        }

        private static T FindComponent<T>(
            Transform root,
            string name)
            where T : Component
        {
            Transform found =
                FindTransform(root, name);
            return found
                ? found.GetComponent<T>()
                : null;
        }

        private static RectTransform FindRect(
            Transform root,
            string name)
        {
            Transform found = FindTransform(root, name);
            RectTransform rect =
                found as RectTransform;
            if (!rect)
                throw new InvalidOperationException(
                    "Workshop object is missing: " + name);
            return rect;
        }

        public static Transform FindTransform(
            Transform root,
            string name)
        {
            if (!root) return null;
            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        private static GameObject FindRoot(
            Scene scene,
            string name)
        {
            if (!scene.IsValid()) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == name) return roots[i];
            return null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Select(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject root =
                FindRoot(scene, "HubDialogueWorkshop");
            Transform found =
                root
                    ? FindTransform(root.transform, name)
                    : null;
            if (found)
                Selection.activeGameObject =
                    found.gameObject;
        }

        private static void EnsureAssetFolder(
            string assetFolder)
        {
            string normalized =
                assetFolder.Replace('\\', '/').TrimEnd('/');
            string[] parts = normalized.Split('/');
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);
                current = next;
            }
        }

        private static string ToAbsoluteProjectPath(
            string assetPath)
        {
            string projectRoot =
                Directory.GetParent(
                    Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new InvalidOperationException(
                    "Could not resolve Unity project root.");
            return Path.Combine(
                projectRoot,
                assetPath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
        }
    }

    [InitializeOnLoad]
    public static class HubDialogueWorkshopAutoSave
    {
        static HubDialogueWorkshopAutoSave()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(
            Scene scene,
            string path)
        {
            if (!HubDialogueWorkshopBuilder.IsWorkshopScene(scene))
                return;
            HubDialogueConfigData data =
                HubDialogueWorkshopBuilder.LoadEditableConfig();
            HubDialogueWorkshopBuilder.CaptureForTests(
                scene,
                data);
            HubDialogueWorkshopBuilder.WriteUserConfig(data);
        }
    }

    public sealed class HubDialogueWorkshopWindow :
        EditorWindow
    {
        private HubDialogueConfigData config;
        private Vector2 scroll;

        private static bool typewriterPreview;
        private static double previewStart;
        private static string previewLine;

        [MenuItem("ROKAS/Hub Dialogue Workshop/Controls")]
        public static void ShowWindow()
        {
            HubDialogueWorkshopWindow window =
                GetWindow<HubDialogueWorkshopWindow>(
                    "Hub Dialogue");
            window.minSize = new Vector2(420f, 620f);
            window.Reload();
            window.Show();
        }

        private void OnEnable()
        {
            Reload();
        }

        private void Reload()
        {
            config =
                HubDialogueWorkshopBuilder.LoadEditableConfig();
            Repaint();
        }

        private void OnGUI()
        {
            if (config == null) Reload();
            scroll =
                EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField(
                "Hub Dialogue Workshop",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Move/resize/rotate visible objects directly in Scene View. Ctrl+S automatically saves layout to a local runtime override.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Content",
                EditorStyles.boldLabel);
            config.speakerName =
                EditorGUILayout.TextField(
                    "Speaker",
                    config.speakerName);
            EditorGUILayout.LabelField("Guild intro");
            config.guildIntroText =
                EditorGUILayout.TextArea(
                    config.guildIntroText,
                    GUILayout.MinHeight(54f));
            EditorGUILayout.LabelField("Window");
            config.windowText =
                EditorGUILayout.TextArea(
                    config.windowText,
                    GUILayout.MinHeight(44f));
            EditorGUILayout.LabelField("Lamp: turn off");
            config.floorLampTurnOffText =
                EditorGUILayout.TextArea(
                    config.floorLampTurnOffText,
                    GUILayout.MinHeight(36f));
            EditorGUILayout.LabelField("Lamp: turn on");
            config.floorLampTurnOnText =
                EditorGUILayout.TextArea(
                    config.floorLampTurnOnText,
                    GUILayout.MinHeight(36f));
            EditorGUILayout.LabelField("Mame");
            config.mameText =
                EditorGUILayout.TextArea(
                    config.mameText,
                    GUILayout.MinHeight(44f));
            EditorGUILayout.LabelField("Mame third interaction");
            config.mameThirdText =
                EditorGUILayout.TextArea(
                    config.mameThirdText,
                    GUILayout.MinHeight(44f));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Voice",
                EditorStyles.boldLabel);
            config.voiceEnabled =
                EditorGUILayout.Toggle(
                    "Enabled",
                    config.voiceEnabled);
            config.voiceVolume =
                EditorGUILayout.Slider(
                    "Volume",
                    config.voiceVolume,
                    0f,
                    1f);
            config.voicePitch =
                EditorGUILayout.Slider(
                    "Pitch",
                    config.voicePitch,
                    .5f,
                    2f);
            config.voiceLoop =
                EditorGUILayout.Toggle(
                    "Loop",
                    config.voiceLoop);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Completion Arrow",
                EditorStyles.boldLabel);
            config.completionBobScale =
                EditorGUILayout.Slider(
                    "Bob",
                    config.completionBobScale,
                    0f,
                    3f);
            config.completionPulseScale =
                EditorGUILayout.Slider(
                    "Pulse",
                    config.completionPulseScale,
                    0f,
                    3f);

            EditorGUILayout.Space(10f);
            if (GUILayout.Button(
                    "Save Content + Current Layout"))
            {
                Scene scene =
                    SceneManager.GetActiveScene();
                if (HubDialogueWorkshopBuilder.IsWorkshopScene(scene))
                    HubDialogueWorkshopBuilder.CaptureForTests(
                        scene,
                        config);
                HubDialogueWorkshopBuilder.WriteUserConfig(config);
                Reload();
            }

            if (GUILayout.Button(
                    "Rebuild Preview From Saved Config"))
            {
                HubDialogueWorkshopBuilder.RebuildPreviewFromConfig();
                Reload();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(
                "Preview",
                EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("TALK"))
                PreviewPortrait(false);
            if (GUILayout.Button("IDLE"))
                PreviewPortrait(true);
            if (GUILayout.Button("Arrow"))
                ToggleArrow();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Typewriter + Voice"))
                StartTypewriterPreview();
            if (GUILayout.Button("Voice only"))
                PlayVoice();
            if (GUILayout.Button("Stop"))
                StopPreview();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        private void StartTypewriterPreview()
        {
            StopPreview();
            Text dialogue =
                Find<Text>("DialogueText");
            if (!dialogue) return;
            previewLine =
                config.guildIntroText ?? string.Empty;
            previewStart =
                EditorApplication.timeSinceStartup;
            typewriterPreview = true;
            dialogue.text = string.Empty;
            SetArrow(false);
            PreviewPortrait(false);
            PlayVoice();
            EditorApplication.update += TickTypewriter;
        }

        private void TickTypewriter()
        {
            if (!typewriterPreview)
            {
                EditorApplication.update -= TickTypewriter;
                return;
            }

            Text dialogue =
                Find<Text>("DialogueText");
            RawImage portrait =
                Find<RawImage>("PortraitImage");
            if (!dialogue || !portrait)
            {
                StopPreview();
                return;
            }

            double elapsed =
                EditorApplication.timeSinceStartup -
                previewStart;
            int visible =
                Mathf.Clamp(
                    Mathf.FloorToInt((float)elapsed * 18f),
                    0,
                    previewLine.Length);
            dialogue.text =
                previewLine.Substring(0, visible);

            int frame =
                Mathf.FloorToInt(
                    (float)elapsed *
                    Mathf.Max(
                        .01f,
                        config.talkFramesPerSecond)) % 6;
            portrait.uvRect =
                new Rect(
                    config.portrait.uvX +
                        frame * config.portrait.uvWidth,
                    config.portrait.uvY,
                    config.portrait.uvWidth,
                    config.portrait.uvHeight);

            if (visible >= previewLine.Length)
            {
                typewriterPreview = false;
                EditorApplication.update -= TickTypewriter;
                PreviewPortrait(true);
                SetArrow(true);
                StopAudio();
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void PreviewPortrait(bool idle)
        {
            RawImage portrait =
                Find<RawImage>("PortraitImage");
            if (!portrait) return;
            portrait.uvRect =
                new Rect(
                    config.portrait.uvX,
                    config.portrait.uvY +
                        (idle
                            ? config.portrait.uvHeight
                            : 0f),
                    config.portrait.uvWidth,
                    config.portrait.uvHeight);
            SceneView.RepaintAll();
        }

        private void ToggleArrow()
        {
            RectTransform arrow =
                Find<RectTransform>("CompletionArrow");
            if (!arrow) return;
            arrow.gameObject.SetActive(
                !arrow.gameObject.activeSelf);
            SceneView.RepaintAll();
        }

        private void SetArrow(bool active)
        {
            RectTransform arrow =
                Find<RectTransform>("CompletionArrow");
            if (arrow)
                arrow.gameObject.SetActive(active);
        }

        private void PlayVoice()
        {
            if (!config.voiceEnabled) return;
            AudioClip clip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Rokas/Resources/HubDialogue/KeikoTextVoice.mp3");
            if (!clip) return;
            StopAudio();
            InvokeAudioUtil(
                "PlayPreviewClip",
                clip,
                0,
                config.voiceLoop);
        }

        private void StopPreview()
        {
            typewriterPreview = false;
            EditorApplication.update -= TickTypewriter;
            StopAudio();
        }

        private static void StopAudio()
        {
            InvokeAudioUtil("StopAllPreviewClips");
            InvokeAudioUtil("StopAllClips");
        }

        private static void InvokeAudioUtil(
            string methodName,
            params object[] args)
        {
            Type type =
                typeof(AudioImporter).Assembly.GetType(
                    "UnityEditor.AudioUtil");
            if (type == null) return;
            MethodInfo[] methods =
                type.GetMethods(
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != methodName) continue;
                ParameterInfo[] parameters =
                    method.GetParameters();
                if (parameters.Length != args.Length) continue;
                try
                {
                    method.Invoke(null, args);
                    return;
                }
                catch
                {
                }
            }
        }

        private static T Find<T>(string name)
            where T : Component
        {
            Scene scene =
                SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            GameObject[] roots =
                scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T[] components =
                    roots[i].GetComponentsInChildren<T>(true);
                for (int j = 0; j < components.Length; j++)
                    if (components[j].name == name)
                        return components[j];
            }
            return null;
        }
    }
}
