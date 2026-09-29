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
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Populate(scene, HubDialogueConfig.LoadFresh());
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
                return new HubDialogueConfigData();
            try
            {
                HubDialogueConfigData data =
                    JsonUtility.FromJson<HubDialogueConfigData>(asset.text);
                return data ?? new HubDialogueConfigData();
            }
            catch
            {
                return new HubDialogueConfigData();
            }
        }

        public static void WriteUserConfig(HubDialogueConfigData config)
        {
            if (config == null) config = new HubDialogueConfigData();
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
            GameObject root = new GameObject("HubDialogueWorkshop");
            SceneManager.MoveGameObjectToScene(root, scene);

            GameObject canvasObject = new GameObject(
                "WorkshopCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            RectTransform canvasRect =
                canvasObject.GetComponent<RectTransform>();
            Stretch(canvasRect);

            RawImage background =
                CreateRaw(canvasRect, "MainRoomBackground");
            background.texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    HomeTexturePath);
            background.raycastTarget = false;
            Stretch(background.rectTransform);

            RectTransform preview =
                CreateRect(canvasRect, "HubDialoguePreview",
                    new HubDialogueRectData(
                        0f, 0f, 1920f, 1080f));

            RectTransform layout =
                CreateRect(
                    preview,
                    "HubDialogueLayoutRoot",
                    config.layoutRoot);
            layout.localScale = new Vector3(
                config.layoutScale,
                config.layoutScale,
                1f);

            RectTransform plaqueRoot =
                CreateRect(
                    layout,
                    "HubDialoguePlaque",
                    config.plaque);

            RawImage plaque =
                CreateRaw(
                    plaqueRoot,
                    "PlaqueArt",
                    config.plaqueArt);
            plaque.texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    PlaqueTexturePath);
            plaque.raycastTarget = false;

            RectTransform maskRoot =
                CreateRect(
                    plaqueRoot,
                    "PortraitMask",
                    config.portraitMask);
            var maskGraphic =
                maskRoot.gameObject.AddComponent<
                    HubDialogueOctagonMaskGraphic>();
            maskGraphic.color = Color.white;
            maskGraphic.raycastTarget = false;
            Mask mask =
                maskRoot.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RawImage portrait =
                CreateRaw(
                    maskRoot,
                    "PortraitImage",
                    config.portrait);
            portrait.texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    PortraitTexturePath);
            portrait.uvRect =
                new Rect(0f, 0f, 1f / 6f, .5f);
            portrait.raycastTarget = false;

            RokasAssets assets =
                AssetDatabase.LoadAssetAtPath<RokasAssets>(
                    AssetsPath);
            Font font = assets ? assets.sans : null;

            Text speaker =
                CreateText(
                    plaqueRoot,
                    "SpeakerName",
                    config.speakerName,
                    config.speaker,
                    config.speakerFontSize,
                    config.speakerAlignment,
                    font);
            speaker.fontStyle = FontStyle.Bold;
            speaker.color =
                new Color(.90f, .96f, 1f, 1f);

            Text dialogue =
                CreateText(
                    plaqueRoot,
                    "DialogueText",
                    config.guildIntroText,
                    config.dialogue,
                    config.dialogueFontSize,
                    config.dialogueAlignment,
                    font);
            dialogue.lineSpacing =
                config.dialogueLineSpacing;

            RawImage cover =
                CreateRaw(
                    plaqueRoot,
                    "BakedArrowCover",
                    config.arrowCover);
            cover.texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    PlaqueTexturePath);
            cover.uvRect = new Rect(
                1680f / 2048f,
                155f / 682f,
                90f / 2048f,
                90f / 682f);
            cover.raycastTarget = false;

            RectTransform arrow =
                CreateRect(
                    plaqueRoot,
                    "CompletionArrow",
                    config.completionArrow);
            HubDialogueTriangleGraphic triangle =
                arrow.gameObject.AddComponent<
                    HubDialogueTriangleGraphic>();
            triangle.color = Color.white;
            triangle.raycastTarget = false;

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

            GameObject note = new GameObject(
                "WorkshopHelpers",
                typeof(RectTransform));
            note.transform.SetParent(canvasRect, false);
        }

        private static void CaptureScene(
            Scene scene,
            HubDialogueConfigData config)
        {
            GameObject root =
                FindRoot(scene, "HubDialogueWorkshop");
            if (!root)
                throw new InvalidOperationException(
                    "HubDialogueWorkshop root is missing.");

            Transform transform = root.transform;
            config.layoutRoot =
                ReadRect(FindRect(
                    transform,
                    "HubDialogueLayoutRoot"));
            RectTransform layout =
                FindRect(transform, "HubDialogueLayoutRoot");
            config.layoutScale =
                Mathf.Max(.01f, layout.localScale.x);
            config.plaque =
                ReadRect(FindRect(transform, "HubDialoguePlaque"));
            config.plaqueArt =
                ReadRect(FindRect(transform, "PlaqueArt"));
            config.portraitMask =
                ReadRect(FindRect(transform, "PortraitMask"));
            config.portrait =
                ReadRect(FindRect(transform, "PortraitImage"));
            config.speaker =
                ReadRect(FindRect(transform, "SpeakerName"));
            config.dialogue =
                ReadRect(FindRect(transform, "DialogueText"));
            config.arrowCover =
                ReadRect(FindRect(transform, "BakedArrowCover"));
            config.completionArrow =
                ReadRect(FindRect(transform, "CompletionArrow"));
            config.muteButton =
                ReadRect(FindRect(transform, "HubMuteButton"));
            config.forwardButton =
                ReadRect(FindRect(transform, "HubForwardButton"));
            config.menuButton =
                ReadRect(FindRect(transform, "HubMenuButton"));

            Text speaker =
                FindRect(transform, "SpeakerName")
                    .GetComponent<Text>();
            Text dialogue =
                FindRect(transform, "DialogueText")
                    .GetComponent<Text>();
            config.speakerName = speaker.text;
            config.speakerFontSize = speaker.fontSize;
            config.speakerAlignment = speaker.alignment;
            config.dialogueFontSize = dialogue.fontSize;
            config.dialogueAlignment = dialogue.alignment;
            config.dialogueLineSpacing = dialogue.lineSpacing;

            config.muteButtonEnabled =
                FindRect(transform, "HubMuteButton")
                    .gameObject.activeSelf;
            config.forwardButtonEnabled =
                FindRect(transform, "HubForwardButton")
                    .gameObject.activeSelf;
            config.menuButtonEnabled =
                FindRect(transform, "HubMenuButton")
                    .gameObject.activeSelf;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            HubDialogueRectData data)
        {
            GameObject go =
                new GameObject(name, typeof(RectTransform));
            RectTransform rect =
                go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin =
                rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition =
                new Vector2(data.x, -data.y);
            rect.sizeDelta =
                new Vector2(data.width, data.height);
            rect.localEulerAngles =
                new Vector3(0f, 0f, data.rotationZ);
            return rect;
        }

        private static RawImage CreateRaw(
            Transform parent,
            string name,
            HubDialogueRectData data)
        {
            RectTransform rect =
                CreateRect(parent, name, data);
            rect.gameObject.AddComponent<CanvasRenderer>();
            return rect.gameObject.AddComponent<RawImage>();
        }

        private static RawImage CreateRaw(
            Transform parent,
            string name)
        {
            GameObject go = new GameObject(
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
                CreateRect(parent, name, data);
            rect.gameObject.AddComponent<CanvasRenderer>();
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value ?? string.Empty;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow =
                HorizontalWrapMode.Wrap;
            text.verticalOverflow =
                VerticalWrapMode.Overflow;
            return text;
        }

        private static void CreateControlGuide(
            Transform parent,
            string name,
            HubDialogueRectData data,
            bool active)
        {
            RectTransform rect =
                CreateRect(parent, name, data);
            rect.gameObject.AddComponent<CanvasRenderer>();
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, .72f, .22f, .18f);
            image.raycastTarget = false;
            rect.gameObject.SetActive(active);
        }

        private static HubDialogueRectData ReadRect(
            RectTransform rect)
        {
            if (!rect)
                throw new InvalidOperationException(
                    "Workshop RectTransform is missing.");
            float z =
                Mathf.DeltaAngle(
                    0f,
                    rect.localEulerAngles.z);
            return new HubDialogueRectData(
                rect.anchoredPosition.x,
                -rect.anchoredPosition.y,
                rect.sizeDelta.x,
                rect.sizeDelta.y,
                z);
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
                assetFolder.Replace('\\\\', '/').TrimEnd('/');
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
                new Rect(frame / 6f, 0f, 1f / 6f, .5f);

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
                    0f,
                    idle ? .5f : 0f,
                    1f / 6f,
                    .5f);
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
