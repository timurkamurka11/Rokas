using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        private const float SceneComposerNudgeStep = 1f;
        private const float SceneComposerLargeNudgeStep = 10f;

        [SerializeField] private int _sceneComposerSelectedCharacterIndex = -1;
        [SerializeField] private bool _sceneComposerPresentationLayoutExpanded = true;
        [SerializeField] private bool _sceneComposerTypographyExpanded;
        [SerializeField] private bool _sceneComposerTextRevealExpanded;
        [SerializeField] private bool _sceneComposerExpressionExpanded;
        [SerializeField] private bool _sceneComposerCharacterMotionExpanded;
        [SerializeField] private bool _sceneComposerBounceExpanded;
        [SerializeField] private bool _sceneComposerBackgroundTransitionExpanded;
        [SerializeField] private bool _sceneComposerStageExpanded;
        [SerializeField] private bool _sceneComposerFocusExpanded;
        [SerializeField] private bool _sceneComposerUiFeedbackExpanded;
        [SerializeField] private bool _sceneComposerTimingExpanded;
        [SerializeField] private bool _sceneComposerAdvancedExpanded;
        [SerializeField] private bool _sceneComposerPreviewTextExpanded;
        [SerializeField] private bool _sceneComposerExactCharacterTransformExpanded;

        [NonSerialized] private bool _sceneComposerDraggingPreviewObject;
        [NonSerialized] private bool _sceneComposerDraggingCharacter;
        [NonSerialized] private Vector2 _sceneComposerLastDragLogicalPoint;

        private bool IsSceneComposerAdvancedLayoutEditingVisible()
        {
            return _sceneComposerInspectorSection == 7 && _sceneComposerPresentationLayoutExpanded;
        }

        private bool IsSceneComposerAdvancedUiFeedbackPreviewVisible()
        {
            return _sceneComposerInspectorSection == 7 && _sceneComposerUiFeedbackExpanded;
        }

        public VnWorkshopElement ComposerGetSelectedPresentationElement()
        {
            return selectedElement;
        }

        public void ComposerSetSelectedPresentationElement(VnWorkshopElement element)
        {
            if (!Enum.IsDefined(typeof(VnWorkshopElement), element))
                throw new ArgumentOutOfRangeException(nameof(element));
            selectedElement = element;
            _sceneComposerSelectedCharacterIndex = -1;
            Repaint();
        }

        public int ComposerGetSelectedCharacterIndex()
        {
            return _sceneComposerSelectedCharacterIndex;
        }

        public void ComposerDragPresentationElement(Vector2 logicalDelta)
        {
            RecordSceneComposerUndo("Move VN Scene UI Element");
            ApplySceneComposerElementDrag(logicalDelta);
        }

        public void ComposerNudgePresentationElement(Vector2 direction, bool largeStep)
        {
            if (!IsFinite(direction)) throw new ArgumentException("Nudge direction must be finite.", nameof(direction));
            float step = largeStep ? SceneComposerLargeNudgeStep : SceneComposerNudgeStep;
            ComposerDragPresentationElement(direction * step);
        }

        public void ComposerSetSelectedPresentationPosition(Vector2 positionDelta)
        {
            VnWorkshopElementOverride current = ComposerGetActivePresentationPreset().GetElementOverride(selectedElement);
            Vector2 size = current.hasSizeDelta ? current.sizeDelta : Vector2.zero;
            float scale = current.hasScaleMultiplier ? current.scaleMultiplier : 1f;
            ComposerSetElementLayout(selectedElement, positionDelta, size, scale);
        }

        public void ComposerSetSelectedPresentationSize(Vector2 sizeDelta)
        {
            VnWorkshopElementOverride current = ComposerGetActivePresentationPreset().GetElementOverride(selectedElement);
            Vector2 position = current.hasPositionDelta ? current.positionDelta : Vector2.zero;
            float scale = current.hasScaleMultiplier ? current.scaleMultiplier : 1f;
            ComposerSetElementLayout(selectedElement, position, sizeDelta, scale);
        }

        public void ComposerSetSelectedPresentationScale(float scaleMultiplier)
        {
            VnWorkshopElementOverride current = ComposerGetActivePresentationPreset().GetElementOverride(selectedElement);
            Vector2 position = current.hasPositionDelta ? current.positionDelta : Vector2.zero;
            Vector2 size = current.hasSizeDelta ? current.sizeDelta : Vector2.zero;
            ComposerSetElementLayout(selectedElement, position, size, scaleMultiplier);
        }

        public bool ComposerSelectPreviewObjectAt(Vector2 logicalPoint)
        {
            if (!IsFinite(logicalPoint)) throw new ArgumentException("Preview point must be finite.", nameof(logicalPoint));
            VnWorkshopPreviewFrame frame = ComposerBuildSelectedPreviewFrame();

            VnWorkshopElement? hit = HitTestSceneComposerUi(frame, logicalPoint);
            if (hit.HasValue)
            {
                selectedElement = hit.Value;
                _sceneComposerSelectedCharacterIndex = -1;
                Repaint();
                return true;
            }

            if (ComposerSelectPreviewCharacterAt(frame, logicalPoint)) return true;

            if (frame.DialoguePanel.Contains(logicalPoint))
            {
                selectedElement = VnWorkshopElement.DialoguePanel;
                _sceneComposerSelectedCharacterIndex = -1;
                Repaint();
                return true;
            }
            return false;
        }

        private bool ComposerSelectPreviewCharacterAt(VnWorkshopPreviewFrame frame, Vector2 logicalPoint)
        {
            if (frame == null || frame.ComposerCharacters == null) return false;
            for (int i = frame.ComposerCharacters.Length - 1; i >= 0; i--)
            {
                VnWorkshopPreviewCharacter character = frame.ComposerCharacters[i];
                if (character != null && character.Body.Contains(logicalPoint))
                {
                    _sceneComposerSelectedCharacterIndex = i;
                    Repaint();
                    return true;
                }
            }
            return false;
        }

        public bool ComposerPrepareSelectedVideoForAuthoring()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null || scene.media == null || scene.media.kind != VnSceneComposerMediaKind.ExternalVideo)
                return false;

            int index = FindSceneIndex(scene.sceneId);
            if (index < 0) return false;
            ComposerGetSceneThumbnail(index);
            if (_sceneComposerThumbnailCache == null) return false;
            if (!_sceneComposerThumbnailCache.TryGetValue(scene.sceneId, out SceneComposerThumbnailCacheEntry entry) || entry == null)
                return false;

            VnSceneComposerVideoPreview video = entry.owner as VnSceneComposerVideoPreview;
            if (video == null) return false;
            video.Changed -= OnSceneComposerVideoPreviewChanged;
            video.Changed += OnSceneComposerVideoPreviewChanged;
            if (string.IsNullOrEmpty(video.warning) && !video.IsPrepared && !video.IsPreparing)
                video.Prepare();
            return true;
        }

        private void OnSceneComposerVideoPreviewChanged()
        {
            if (this != null) Repaint();
        }

        private VnSceneComposerVideoPreview GetSelectedComposerVideoPreview()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null || _sceneComposerThumbnailCache == null) return null;
            if (!_sceneComposerThumbnailCache.TryGetValue(scene.sceneId, out SceneComposerThumbnailCacheEntry entry) || entry == null)
                return null;
            return entry.owner as VnSceneComposerVideoPreview;
        }

        private VnWorkshopPreviewFrame ComposerBuildSelectedPreviewFrameForDisplay()
        {
            if (comparisonView == VnWorkshopComparisonView.Current)
                return ComposerBuildSelectedPreviewPlaybackFrame().WorkshopFrame;

            VnSceneComposerScene scene = RequireSelectedScene();
            int index = FindSceneIndex(scene.sceneId);
            Texture media = ComposerGetSceneThumbnail(index);
            VnSceneComposerScene baselineScene = JsonUtility.FromJson<VnSceneComposerScene>(JsonUtility.ToJson(scene));
            if (baselineScene == null) baselineScene = scene;
            else baselineScene.presentationOverrides = new VnPresentationWorkshopPreset();
            var baselineProject = new VnSceneComposerProject
            {
                projectId = _sceneComposerProject.projectId,
                title = _sceneComposerProject.title,
                defaultPresentation = new VnPresentationWorkshopPreset()
            };
            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                baselineProject, baselineScene, previewResolution, media as Texture2D);
            Texture visual = media != null ? media : frame.BackgroundTexture;
            var sample = new VnWorkshopBackgroundTransitionSample
            {
                SourceAlpha = 0f,
                TargetAlpha = 1f,
                CurtainCoverage = 0f,
                CurtainPosition = 1f,
                CurtainDarkness = 0f,
                CurtainDirection = VnWorkshopCurtainDirection.LeftToRight,
                Complete = true
            };
            new VnSceneComposerPlaybackFrame(frame, sample, visual, visual);
            return frame;
        }

        private void HandleSceneComposerPreviewInput(Rect previewRect, VnWorkshopPreviewFrame frame, Event currentEvent)
        {
            if (currentEvent == null || frame == null || comparisonView != VnWorkshopComparisonView.Current) return;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && previewRect.Contains(currentEvent.mousePosition))
            {
                Vector2 logicalPoint = VnPresentationWorkshopPreviewRenderer.PreviewToLogical(previewRect, currentEvent.mousePosition, frame);
                bool selected = IsSceneComposerAdvancedLayoutEditingVisible()
                    ? ComposerSelectPreviewObjectAt(logicalPoint)
                    : ComposerSelectPreviewCharacterAt(frame, logicalPoint);
                if (selected)
                {
                    _sceneComposerDraggingPreviewObject = true;
                    _sceneComposerDraggingCharacter = _sceneComposerSelectedCharacterIndex >= 0;
                    _sceneComposerLastDragLogicalPoint = logicalPoint;
                    RecordSceneComposerUndo(_sceneComposerDraggingCharacter ? "Move VN Scene Character" : "Move VN Scene UI Element");
                    Focus();
                    GUI.FocusControl(null);
                    currentEvent.Use();
                }
                return;
            }

            if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && _sceneComposerDraggingPreviewObject)
            {
                Vector2 logicalPoint = VnPresentationWorkshopPreviewRenderer.PreviewToLogical(previewRect, currentEvent.mousePosition, frame);
                Vector2 logicalDelta = logicalPoint - _sceneComposerLastDragLogicalPoint;
                _sceneComposerLastDragLogicalPoint = logicalPoint;
                if (_sceneComposerDraggingCharacter) ApplySceneComposerCharacterDrag(logicalDelta);
                else ApplySceneComposerElementDrag(logicalDelta);
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && _sceneComposerDraggingPreviewObject)
            {
                _sceneComposerDraggingPreviewObject = false;
                _sceneComposerDraggingCharacter = false;
                currentEvent.Use();
                return;
            }

            if (currentEvent.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;
            Vector2 direction;
            switch (currentEvent.keyCode)
            {
                case KeyCode.LeftArrow: direction = Vector2.left; break;
                case KeyCode.RightArrow: direction = Vector2.right; break;
                case KeyCode.UpArrow: direction = Vector2.up; break;
                case KeyCode.DownArrow: direction = Vector2.down; break;
                default: return;
            }

            bool characterSelected = _sceneComposerSelectedCharacterIndex >= 0;
            if (!characterSelected && !IsSceneComposerAdvancedLayoutEditingVisible()) return;
            RecordSceneComposerUndo(characterSelected ? "Nudge VN Scene Character" : "Nudge VN Scene UI Element");
            float step = currentEvent.shift ? SceneComposerLargeNudgeStep : SceneComposerNudgeStep;
            if (characterSelected) ApplySceneComposerCharacterDrag(direction * step);
            else ApplySceneComposerElementDrag(direction * step);
            currentEvent.Use();
        }

        private void ApplySceneComposerElementDrag(Vector2 logicalDelta)
        {
            if (!IsFinite(logicalDelta)) return;
            VnPresentationWorkshopEditing.ApplyDrag(ComposerGetActivePresentationPreset(), selectedElement, logicalDelta);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        private void ApplySceneComposerCharacterDrag(Vector2 logicalDelta)
        {
            if (!IsFinite(logicalDelta)) return;
            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.characters == null || _sceneComposerSelectedCharacterIndex < 0 ||
                _sceneComposerSelectedCharacterIndex >= scene.characters.Count) return;
            VnSceneComposerCharacter character = scene.characters[_sceneComposerSelectedCharacterIndex];
            if (character == null) return;
            Vector2 current = character.hasPositionOffset ? character.positionOffset : Vector2.zero;
            Vector2 next = current + logicalDelta;
            character.hasPositionOffset = next != Vector2.zero;
            character.positionOffset = next;
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        private void DrawSceneComposerSelectionOverlay(Rect previewRect, VnWorkshopPreviewFrame frame)
        {
            if (frame == null) return;
            if (_sceneComposerSelectedCharacterIndex < 0 || frame.ComposerCharacters == null ||
                _sceneComposerSelectedCharacterIndex >= frame.ComposerCharacters.Length) return;
            VnWorkshopPreviewCharacter character = frame.ComposerCharacters[_sceneComposerSelectedCharacterIndex];
            if (character == null) return;
            Rect canvasRect = FitSceneComposerPreviewRect(previewRect, frame.ScreenSize.x / Mathf.Max(1f, frame.ScreenSize.y));
            Rect rect = VnPresentationWorkshopPreviewRenderer.LogicalToPreview(canvasRect, character.Body, frame);
            DrawSceneComposerOutline(rect, 2f);
        }

        private static Rect FitSceneComposerPreviewRect(Rect available, float aspect)
        {
            float availableAspect = available.width / Mathf.Max(1f, available.height);
            if (availableAspect > aspect)
            {
                float width = available.height * aspect;
                return new Rect(available.center.x - width * .5f, available.y, width, available.height);
            }
            float height = available.width / aspect;
            return new Rect(available.x, available.center.y - height * .5f, available.width, height);
        }

        private static void DrawSceneComposerOutline(Rect rect, float thickness)
        {
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static VnWorkshopElement? HitTestSceneComposerUi(VnWorkshopPreviewFrame frame, Vector2 logicalPoint)
        {
            VnWorkshopElement[] order =
            {
                VnWorkshopElement.Back, VnWorkshopElement.Next, VnWorkshopElement.MuteHitRegion,
                VnWorkshopElement.PauseHitRegion, VnWorkshopElement.SkipHitRegion, VnWorkshopElement.SpeakerName,
                VnWorkshopElement.DialogueText
            };
            for (int i = 0; i < order.Length; i++)
            {
                VnWorkshopElement element = order[i];
                if (frame.GetElementRect(element).Contains(logicalPoint)) return element;
            }
            return null;
        }

        private void DrawSceneComposerCharacterTransformControls(int characterIndex, VnSceneComposerCharacter character)
        {
            _sceneComposerExactCharacterTransformExpanded = EditorGUILayout.Foldout(
                _sceneComposerExactCharacterTransformExpanded,
                new GUIContent("Точное положение", "Точная настройка координат персонажа."),
                true);
            if (!_sceneComposerExactCharacterTransformExpanded) return;

            EditorGUI.indentLevel++;
            Vector2 position = character.hasPositionOffset ? character.positionOffset : Vector2.zero;
            float scale = character.hasScaleMultiplier ? character.scaleMultiplier : 1f;
            EditorGUI.BeginChangeCheck();
            float x = EditorGUILayout.FloatField("X", position.x);
            float y = EditorGUILayout.FloatField("Y", position.y);
            float nextScale = EditorGUILayout.FloatField("Масштаб", scale);
            if (EditorGUI.EndChangeCheck())
                ComposerSetCharacterTransform(characterIndex, new Vector2(x, y), Mathf.Clamp(nextScale, .05f, 5f));
            if (GUILayout.Button("Сбросить положение и размер"))
                ComposerResetCharacterTransform(characterIndex);
            EditorGUI.indentLevel--;
        }

        private void DrawSceneComposerPresentationControls(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Расширенные параметры", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Точная настройка оформления и поведения предпросмотра.", EditorStyles.miniLabel);

            EditorGUILayout.LabelField("Применить:", EditorStyles.miniBoldLabel);
            int scope = _sceneComposerPresentationProjectDefaults ? 1 : 0;
            int nextScope = GUILayout.Toolbar(scope, new[] { "Только к этой сцене", "Ко всем сценам" });
            if (nextScope != scope) ComposerSetPresentationScope(nextScope == 1);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(comparisonView == VnWorkshopComparisonView.Original, "Исходный", EditorStyles.miniButtonLeft))
                comparisonView = VnWorkshopComparisonView.Original;
            if (GUILayout.Toggle(comparisonView == VnWorkshopComparisonView.Current, "Текущий", EditorStyles.miniButtonMid))
                comparisonView = VnWorkshopComparisonView.Current;
            if (GUILayout.Button("До / после", EditorStyles.miniButtonRight))
                comparisonView = comparisonView == VnWorkshopComparisonView.Original
                    ? VnWorkshopComparisonView.Current : VnWorkshopComparisonView.Original;
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            VnWorkshopResolution nextResolution = (VnWorkshopResolution)EditorGUILayout.EnumPopup("Разрешение предпросмотра", previewResolution);
            if (EditorGUI.EndChangeCheck()) ComposerSetPreviewResolution(nextResolution);

            _sceneComposerPresentationLayoutExpanded = EditorGUILayout.Foldout(
                _sceneComposerPresentationLayoutExpanded, "Разметка интерфейса", true);
            if (_sceneComposerPresentationLayoutExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                VnWorkshopElement nextElement = (VnWorkshopElement)EditorGUILayout.EnumPopup("Элемент", selectedElement);
                if (EditorGUI.EndChangeCheck()) ComposerSetSelectedPresentationElement(nextElement);
                VnWorkshopElementOverride elementOverride = ComposerGetActivePresentationPreset().GetElementOverride(selectedElement);
                Vector2 position = elementOverride.hasPositionDelta ? elementOverride.positionDelta : Vector2.zero;
                EditorGUI.BeginChangeCheck();
                float x = EditorGUILayout.FloatField("Позиция X", position.x);
                float y = EditorGUILayout.FloatField("Позиция Y", position.y);
                if (EditorGUI.EndChangeCheck()) ComposerSetSelectedPresentationPosition(new Vector2(x, y));
                if (SupportsSize(selectedElement))
                {
                    Vector2 size = elementOverride.hasSizeDelta ? elementOverride.sizeDelta : Vector2.zero;
                    EditorGUI.BeginChangeCheck();
                    float width = EditorGUILayout.FloatField("Ширина", size.x);
                    float height = EditorGUILayout.FloatField("Высота", size.y);
                    if (EditorGUI.EndChangeCheck()) ComposerSetSelectedPresentationSize(new Vector2(width, height));
                }
                if (SupportsScale(selectedElement))
                {
                    float scale = elementOverride.hasScaleMultiplier ? elementOverride.scaleMultiplier : 1f;
                    EditorGUI.BeginChangeCheck();
                    float nextScale = EditorGUILayout.FloatField("Масштаб", scale);
                    if (EditorGUI.EndChangeCheck()) ComposerSetSelectedPresentationScale(Mathf.Clamp(nextScale, .05f, 5f));
                }
                if (IsBakedControlHitRegion(selectedElement))
                    EditorGUILayout.HelpBox("Изображение элемента не меняется — редактируется только его область нажатия.", MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Сбросить элемент")) ComposerResetElement(selectedElement);
                if (GUILayout.Button("Сбросить всё")) ComposerResetAllPresentation();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("Щёлкните элемент или персонажа в предпросмотре. Перетаскивание меняет положение; стрелки сдвигают на 1, Shift + стрелки — на 10.", MessageType.None);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Применить эталонное движение"))
                ComposerApplyReferenceMotionProfile();

            DrawSceneComposerPresetControls();
            DrawSceneComposerPreviewTextControls();
            DrawSceneComposerSerializedPresentationSections(scene);
        }

        private void DrawSceneComposerPresetControls()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Шаблоны оформления", EditorStyles.miniBoldLabel);
            string[] variants;
            try { variants = ListSavedVariants(); }
            catch { variants = Array.Empty<string>(); }
            if (variants.Length > 0)
            {
                selectedVariantIndex = Mathf.Clamp(selectedVariantIndex, 0, variants.Length - 1);
                selectedVariantIndex = EditorGUILayout.Popup("Сохранённые", selectedVariantIndex, variants);
            }
            variantName = EditorGUILayout.TextField("Название", variantName ?? string.Empty);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Сохранить шаблон"))
                TrySceneComposerPresentationAction(() => ComposerSavePresentationPreset(GetProjectRoot(), variantName));
            using (new EditorGUI.DisabledScope(variants.Length == 0))
            {
                if (GUILayout.Button("Загрузить шаблон"))
                    TrySceneComposerPresentationAction(() => ComposerLoadPresentationPreset(GetProjectRoot(), variants[selectedVariantIndex]));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(variants.Length == 0))
            {
                string selectedName = variants.Length > 0 ? variants[selectedVariantIndex] : string.Empty;
                if (GUILayout.Button("Дублировать"))
                    TrySceneComposerPresentationAction(() => ComposerDuplicatePresentationPreset(GetProjectRoot(), selectedName, variantName));
                if (GUILayout.Button("Переименовать"))
                    TrySceneComposerPresentationAction(() => ComposerRenamePresentationPreset(GetProjectRoot(), selectedName, variantName));
                if (GUILayout.Button("Удалить"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Удалить шаблон оформления",
                        "Удалить шаблон «" + selectedName + "»? Это удалит только локальный шаблон редактора.",
                        "Удалить", "Отмена");
                    if (confirmed)
                        TrySceneComposerPresentationAction(() => ComposerDeletePresentationPreset(GetProjectRoot(), selectedName));
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Экспорт JSON"))
            {
                string path = EditorUtility.SaveFilePanel("Экспорт шаблона оформления", string.Empty, ExportFileName, "json");
                if (!string.IsNullOrEmpty(path))
                    TrySceneComposerPresentationAction(() => File.WriteAllText(path, ComposerExportPresentationPresetJson(variantName)));
            }
            if (GUILayout.Button("Импорт JSON"))
            {
                string path = EditorUtility.OpenFilePanel("Импорт шаблона оформления", string.Empty, "json");
                if (!string.IsNullOrEmpty(path))
                    TrySceneComposerPresentationAction(() => ComposerImportPresentationPresetJson(File.ReadAllText(path)));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSceneComposerPreviewTextControls()
        {
            EditorGUILayout.Space();
            _sceneComposerPreviewTextExpanded = EditorGUILayout.Foldout(
                _sceneComposerPreviewTextExpanded, "Тест оформления текста", true);
            if (!_sceneComposerPreviewTextExpanded) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Тестовый текст оформления", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Используется только для проверки внешнего вида текста.\nНе является текстом сцены.",
                MessageType.Info);
            string sample = ComposerGetPreviewSampleText();
            EditorGUI.BeginChangeCheck();
            string next = EditorGUILayout.TextArea(sample ?? string.Empty, GUILayout.MinHeight(48f));
            if (EditorGUI.EndChangeCheck()) ComposerSetPreviewSampleText(next);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Загрузить TXT"))
            {
                string path = EditorUtility.OpenFilePanel("Загрузить тестовый текст UTF-8", string.Empty, "txt");
                if (!string.IsNullOrEmpty(path))
                    TrySceneComposerPresentationAction(() => ComposerSetPreviewSampleText(File.ReadAllText(path)));
            }
            if (GUILayout.Button("Очистить")) ComposerSetPreviewSampleText(string.Empty);
            if (GUILayout.Button("Сбросить пример")) ComposerResetPreviewSampleText();
            EditorGUILayout.EndHorizontal();
            string glyphWarning = ComposerGetPreviewTextGlyphWarning();
            if (!string.IsNullOrEmpty(glyphWarning))
                EditorGUILayout.HelpBox(glyphWarning, MessageType.Warning);
            EditorGUI.indentLevel--;
        }

        private void DrawSceneComposerSerializedPresentationSections(VnSceneComposerScene scene)
        {
            SerializedObject serialized = new SerializedObject(this);
            serialized.Update();
            SerializedProperty project = serialized.FindProperty("_sceneComposerProject");
            if (project == null) return;
            SerializedProperty active;
            if (_sceneComposerPresentationProjectDefaults)
            {
                active = project.FindPropertyRelative("defaultPresentation");
            }
            else
            {
                int sceneIndex = FindSceneIndex(scene.sceneId);
                SerializedProperty scenes = project.FindPropertyRelative("scenes");
                SerializedProperty sceneProperty = scenes != null && sceneIndex >= 0 && sceneIndex < scenes.arraySize
                    ? scenes.GetArrayElementAtIndex(sceneIndex) : null;
                active = sceneProperty != null ? sceneProperty.FindPropertyRelative("presentationOverrides") : null;
            }
            if (active == null) return;

            DrawSceneComposerSerializedSection(ref _sceneComposerTypographyExpanded, "Расширенная типографика", active, "typography");
            DrawSceneComposerSerializedSection(ref _sceneComposerTextRevealExpanded, "Появление текста — точные параметры", active, "typewriter");
            DrawSceneComposerSerializedSection(ref _sceneComposerExpressionExpanded, "Смена позы / эмоции — точные параметры", active, "expressionTransition");
            DrawSceneComposerSerializedSection(ref _sceneComposerCharacterMotionExpanded, "Появление и исчезновение — точные параметры", active, "characterTransition");
            DrawSceneComposerSerializedSection(ref _sceneComposerBounceExpanded, "Акцент / движение — точные параметры", active, "actionBounce");
            DrawSceneComposerSerializedSection(ref _sceneComposerBackgroundTransitionExpanded, "Переход фона — точные параметры", active, "backgroundTransition");
            DrawSceneComposerSerializedSection(ref _sceneComposerStageExpanded, "Расположение персонажей — точные параметры", active, "stageLayout");
            DrawSceneComposerSerializedSection(ref _sceneComposerFocusExpanded, "Фокус говорящего — точные параметры", active, "focus");
            DrawSceneComposerSerializedSection(ref _sceneComposerUiFeedbackExpanded, "Эффекты интерфейса", active, "uiFeedback");
            if (_sceneComposerUiFeedbackExpanded) DrawSceneComposerUiFeedbackPreviewControls();
            DrawSceneComposerSerializedSection(ref _sceneComposerTimingExpanded, "Тайминг сцены — точные параметры", active, "timing");
            _sceneComposerAdvancedExpanded = EditorGUILayout.Foldout(_sceneComposerAdvancedExpanded, "Полный набор параметров", true);
            if (_sceneComposerAdvancedExpanded) EditorGUILayout.PropertyField(active, true);

            if (serialized.ApplyModifiedProperties())
            {
                ResetSceneComposerPlayback();
                MarkSceneComposerChanged();
            }
        }

        private void DrawSceneComposerUiFeedbackPreviewControls()
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Предпросмотр эффектов интерфейса", EditorStyles.miniBoldLabel);
            VnWorkshopElement[] targets =
            {
                VnWorkshopElement.Back,
                VnWorkshopElement.Next,
                VnWorkshopElement.MuteHitRegion,
                VnWorkshopElement.PauseHitRegion,
                VnWorkshopElement.SkipHitRegion
            };
            string[] targetNames = { "Назад", "Далее", "Без звука", "Пауза", "Пропустить" };
            int currentTarget = Array.IndexOf(targets, _sceneComposerUiFeedbackPreviewElement);
            if (currentTarget < 0) currentTarget = 0;
            int nextTarget = EditorGUILayout.Popup("Элемент", currentTarget, targetNames);
            if (nextTarget != currentTarget)
                ComposerSetUiFeedbackPreview(targets[nextTarget], _sceneComposerUiFeedbackPreviewState, _sceneComposerUiFeedbackPreviewProgress);

            string[] phaseNames = { "Обычное", "Наведение", "Нажатие", "Отпускание" };
            int currentPhase = (int)_sceneComposerUiFeedbackPreviewState;
            int nextPhase = GUILayout.Toolbar(currentPhase, phaseNames);
            if (nextPhase != currentPhase)
            {
                VnWorkshopUiFeedbackState nextState = (VnWorkshopUiFeedbackState)nextPhase;
                float phaseProgress = nextState == VnWorkshopUiFeedbackState.Release ? .5f : 1f;
                ComposerSetUiFeedbackPreview(_sceneComposerUiFeedbackPreviewElement, nextState, phaseProgress);
            }

            EditorGUI.BeginChangeCheck();
            float progress = EditorGUILayout.Slider("Прогресс фазы", _sceneComposerUiFeedbackPreviewProgress, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                ComposerSetUiFeedbackPreview(_sceneComposerUiFeedbackPreviewElement, _sceneComposerUiFeedbackPreviewState, progress);
            EditorGUILayout.LabelField("Предпросмотр использует итоговые настройки выбранной области применения.", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        private static void DrawSceneComposerSerializedSection(ref bool expanded, string label,
            SerializedProperty active, string propertyName)
        {
            SerializedProperty property = active.FindPropertyRelative(propertyName);
            if (property == null) return;
            expanded = EditorGUILayout.Foldout(expanded, label, true);
            if (expanded) EditorGUILayout.PropertyField(property, true);
        }

        private void DrawSceneComposerVideoPreparationState(Rect previewRect)
        {
            VnSceneComposerVideoPreview video = GetSelectedComposerVideoPreview();
            if (video == null) return;
            if (!string.IsNullOrEmpty(video.warning))
            {
                GUI.Box(new Rect(previewRect.x + 12f, previewRect.y + 12f, Mathf.Max(180f, previewRect.width - 24f), 44f),
                    "Не удалось открыть видео\n" + video.warning);
                return;
            }
            if (video.IsPreparing || !video.HasVisibleFrame)
            {
                GUI.Box(new Rect(previewRect.center.x - 90f, previewRect.center.y - 20f, 180f, 40f), "Подготовка видео…");
            }
        }

        private void TrySceneComposerPresentationAction(Action action)
        {
            try
            {
                action();
                SetSceneComposerStatus("Настройки оформления обновлены.", MessageType.Info);
            }
            catch (Exception exception)
            {
                SetSceneComposerStatus(exception.Message, MessageType.Error);
            }
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }
    }
}
