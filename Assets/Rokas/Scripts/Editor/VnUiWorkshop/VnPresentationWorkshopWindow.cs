using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnWorkshopComparisonView
    {
        Original,
        Current
    }

    public sealed class VnPresentationWorkshopWindow : EditorWindow
    {
        public const string MenuPath = "ROKAS/VN UI Workshop";
        public const string ExportFileName = "ROKAS_VN_WORKSHOP_PRESET.json";

        private const float NudgeStep = 1f;
        private const float LargeNudgeStep = 10f;

        [SerializeField] private VnPresentationWorkshopPreset currentPreset = new VnPresentationWorkshopPreset();
        [SerializeField] private VnWorkshopPreviewScene previewScene = VnWorkshopPreviewScene.MinaBody;
        [SerializeField] private VnWorkshopResolution previewResolution = VnWorkshopResolution.Reference1920x1080;
        [SerializeField] private VnWorkshopComparisonView comparisonView = VnWorkshopComparisonView.Current;
        [SerializeField] private VnWorkshopElement selectedElement = VnWorkshopElement.DialoguePanel;
        [SerializeField] private string variantName = "Variant";
        [SerializeField] private int selectedVariantIndex;

        [NonSerialized] private bool draggingSelected;
        [NonSerialized] private Vector2 lastDragLogicalPoint;
        [NonSerialized] private string variantStatus;
        [NonSerialized] private MessageType variantStatusType = MessageType.None;

        public VnPresentationWorkshopPreset CurrentPreset
        {
            get
            {
                if (currentPreset == null) currentPreset = new VnPresentationWorkshopPreset();
                return currentPreset;
            }
        }

        public VnWorkshopPreviewScene PreviewScene
        {
            get => previewScene;
            set => previewScene = value;
        }

        public VnWorkshopResolution PreviewResolution
        {
            get => previewResolution;
            set => previewResolution = value;
        }

        public VnWorkshopComparisonView ComparisonView
        {
            get => comparisonView;
            set => comparisonView = value;
        }

        public VnWorkshopElement SelectedElement
        {
            get => selectedElement;
            set => selectedElement = value;
        }

        [MenuItem(MenuPath)]
        public static void Open()
        {
            VnPresentationWorkshopWindow window = GetWindow<VnPresentationWorkshopWindow>();
            window.titleContent = new GUIContent("VN UI Workshop");
            window.minSize = new Vector2(960f, 600f);
            window.Show();
        }

        public VnWorkshopPreviewFrame BuildPreviewFrame()
        {
            VnPresentationWorkshopPreset previewPreset = comparisonView == VnWorkshopComparisonView.Original
                ? new VnPresentationWorkshopPreset()
                : CurrentPreset;
            return VnPresentationWorkshopPreviewRenderer.BuildFrame(previewPreset, previewResolution, previewScene);
        }

        public bool SelectElementAt(Vector2 logicalPoint)
        {
            VnWorkshopElement? hit = VnPresentationWorkshopPreviewRenderer.HitTest(BuildPreviewFrame(), logicalPoint);
            if (!hit.HasValue) return false;
            selectedElement = hit.Value;
            Repaint();
            return true;
        }

        public void DragSelectedElement(Vector2 logicalDelta)
        {
            VnPresentationWorkshopEditing.ApplyDrag(CurrentPreset, selectedElement, logicalDelta);
            Repaint();
        }

        public void NudgeSelectedElement(Vector2 direction, bool largeStep)
        {
            float step = largeStep ? LargeNudgeStep : NudgeStep;
            VnPresentationWorkshopEditing.Nudge(CurrentPreset, selectedElement, direction * step);
            Repaint();
        }

        public void SetSelectedPosition(Vector2 delta)
        {
            VnPresentationWorkshopEditing.SetPositionDelta(CurrentPreset, selectedElement, delta);
            Repaint();
        }

        public void SetSelectedSize(Vector2 delta)
        {
            VnPresentationWorkshopEditing.SetSizeDelta(CurrentPreset, selectedElement, delta);
            Repaint();
        }

        public void SetSelectedScale(float multiplier)
        {
            VnPresentationWorkshopEditing.SetScaleMultiplier(CurrentPreset, selectedElement, multiplier);
            Repaint();
        }

        public void ResetSelectedElement()
        {
            CurrentPreset.ResetElement(selectedElement);
            Repaint();
        }

        public void ResetAll()
        {
            CurrentPreset.ResetAll();
            Repaint();
        }

        public void SetFocusValues(float twoCharacterOffset, float activeScale, float inactiveScale,
            float inactiveBrightness, float inactiveAlpha)
        {
            RequireFinite(twoCharacterOffset, nameof(twoCharacterOffset));
            RequireRange(activeScale, .05f, 5f, nameof(activeScale));
            RequireRange(inactiveScale, .05f, 5f, nameof(inactiveScale));
            RequireRange(inactiveBrightness, 0f, 1f, nameof(inactiveBrightness));
            RequireRange(inactiveAlpha, 0f, 1f, nameof(inactiveAlpha));
            if (twoCharacterOffset < 0f)
                throw new ArgumentOutOfRangeException(nameof(twoCharacterOffset), "Character offset cannot be negative.");

            VnWorkshopFocusValues baseline = VnPresentationWorkshopResolver.ResolveFocus(new VnPresentationWorkshopPreset());
            SetFocusOverride(ref CurrentPreset.focus.hasTwoCharacterOffset, ref CurrentPreset.focus.twoCharacterOffset,
                twoCharacterOffset, baseline.TwoCharacterOffset);
            SetFocusOverride(ref CurrentPreset.focus.hasActiveScale, ref CurrentPreset.focus.activeScale,
                activeScale, baseline.ActiveScale);
            SetFocusOverride(ref CurrentPreset.focus.hasInactiveScale, ref CurrentPreset.focus.inactiveScale,
                inactiveScale, baseline.InactiveScale);
            SetFocusOverride(ref CurrentPreset.focus.hasInactiveBrightness, ref CurrentPreset.focus.inactiveBrightness,
                inactiveBrightness, baseline.InactiveBrightness);
            SetFocusOverride(ref CurrentPreset.focus.hasInactiveAlpha, ref CurrentPreset.focus.inactiveAlpha,
                inactiveAlpha, baseline.InactiveAlpha);
            Repaint();
        }

        public string[] ListSavedVariants()
        {
            return VnPresentationWorkshopStorage.ListVariants(GetProjectRoot());
        }

        public void SaveCurrentVariant(string name)
        {
            VnPresentationWorkshopStorage.SaveVariant(GetProjectRoot(), name, CurrentPreset);
            Repaint();
        }

        public VnWorkshopImportResult LoadSavedVariant(string name)
        {
            VnWorkshopImportResult result = VnPresentationWorkshopStorage.LoadVariant(GetProjectRoot(), name);
            if (result.Success && result.Document != null && result.Document.preset != null)
            {
                currentPreset = result.Document.preset;
                comparisonView = VnWorkshopComparisonView.Current;
            }
            Repaint();
            return result;
        }

        public void DuplicateSavedVariant(string sourceName, string duplicateName)
        {
            VnPresentationWorkshopStorage.DuplicateVariant(GetProjectRoot(), sourceName, duplicateName);
            Repaint();
        }

        public void RenameSavedVariant(string currentName, string newName)
        {
            VnPresentationWorkshopStorage.RenameVariant(GetProjectRoot(), currentName, newName);
            Repaint();
        }

        public bool DeleteSavedVariant(string name)
        {
            bool deleted = VnPresentationWorkshopStorage.DeleteVariant(GetProjectRoot(), name);
            Repaint();
            return deleted;
        }

        public string ExportCurrentPresetJson(string name)
        {
            variantName = name ?? string.Empty;
            return VnPresentationWorkshopSerialization.Serialize(CurrentPreset, variantName);
        }

        public void ExportCurrentPresetToFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Export path is required.", nameof(path));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, ExportCurrentPresetJson(variantName));
            SetVariantStatus("Exported " + ExportFileName + ".", MessageType.Info);
        }

        public VnWorkshopImportResult ImportPresetJson(string json)
        {
            VnWorkshopImportResult result = VnPresentationWorkshopSerialization.Deserialize(json);
            if (!result.Success || result.Document == null || result.Document.preset == null)
            {
                SetVariantStatus("Import failed: " + result.Error, MessageType.Error);
                return result;
            }

            currentPreset = result.Document.preset;
            variantName = result.Document.variantName ?? string.Empty;
            comparisonView = VnWorkshopComparisonView.Current;

            if (result.SourceHeadMismatch)
            {
                SetVariantStatus(
                    "Imported with source HEAD mismatch. " + result.Error +
                    " Production remains unchanged.",
                    MessageType.Warning);
            }
            else
            {
                SetVariantStatus("Imported Workshop preset '" + variantName + "'.", MessageType.Info);
            }

            Repaint();
            return result;
        }

        public VnWorkshopImportResult ImportPresetFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Import path is required.", nameof(path));

            return ImportPresetJson(File.ReadAllText(path));
        }

        private void OnGUI()
        {
            DrawComparisonToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawLeftColumn();
            DrawPreviewColumn();
            DrawRightColumn();
            EditorGUILayout.EndHorizontal();

            HandleKeyboardNudge(Event.current);
        }

        private void DrawComparisonToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(comparisonView == VnWorkshopComparisonView.Original, "Original", EditorStyles.toolbarButton))
                comparisonView = VnWorkshopComparisonView.Original;
            if (GUILayout.Toggle(comparisonView == VnWorkshopComparisonView.Current, "Current", EditorStyles.toolbarButton))
                comparisonView = VnWorkshopComparisonView.Current;
            if (GUILayout.Button("Before/After", EditorStyles.toolbarButton))
                comparisonView = comparisonView == VnWorkshopComparisonView.Original
                    ? VnWorkshopComparisonView.Current
                    : VnWorkshopComparisonView.Original;
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(VnPresentationWorkshopBaseline.SourceHead, EditorStyles.miniLabel, GUILayout.Width(285f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(210f));
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            previewScene = (VnWorkshopPreviewScene)EditorGUILayout.EnumPopup("State", previewScene);
            previewResolution = (VnWorkshopResolution)EditorGUILayout.EnumPopup("Resolution", previewResolution);
            EditorGUILayout.Space();
            DrawVariantsPanel();
            EditorGUILayout.EndVertical();
        }

        private void DrawVariantsPanel()
        {
            EditorGUILayout.LabelField("Saved Variants", EditorStyles.boldLabel);

            string[] variants;
            try
            {
                variants = ListSavedVariants();
            }
            catch (Exception exception)
            {
                variants = new string[0];
                SetVariantStatus("Could not read variants: " + exception.Message, MessageType.Error);
            }

            if (variants.Length > 0)
            {
                selectedVariantIndex = Mathf.Clamp(selectedVariantIndex, 0, variants.Length - 1);
                int nextIndex = EditorGUILayout.Popup("Saved", selectedVariantIndex, variants);
                if (nextIndex != selectedVariantIndex)
                {
                    selectedVariantIndex = nextIndex;
                    variantName = variants[selectedVariantIndex];
                }
            }
            else
            {
                selectedVariantIndex = 0;
                EditorGUILayout.LabelField("Saved", "(none)");
            }

            variantName = EditorGUILayout.TextField("Name", variantName ?? string.Empty);

            if (GUILayout.Button("Save Variant"))
            {
                TryVariantAction(
                    () => SaveCurrentVariant(variantName),
                    "Saved variant '" + VnPresentationWorkshopStorage.SanitizeVariantName(variantName) + "'.");
            }

            using (new EditorGUI.DisabledScope(variants.Length == 0))
            {
                string selectedName = variants.Length > 0 ? variants[selectedVariantIndex] : string.Empty;

                if (GUILayout.Button("Load Variant"))
                {
                    try
                    {
                        VnWorkshopImportResult result = LoadSavedVariant(selectedName);
                        if (!result.Success)
                        {
                            SetVariantStatus("Load failed: " + result.Error, MessageType.Error);
                        }
                        else if (result.SourceHeadMismatch)
                        {
                            SetVariantStatus("Loaded with source HEAD mismatch warning.", MessageType.Warning);
                        }
                        else
                        {
                            variantName = selectedName;
                            SetVariantStatus("Loaded variant '" + selectedName + "'.", MessageType.Info);
                        }
                    }
                    catch (Exception exception)
                    {
                        SetVariantStatus("Load failed: " + exception.Message, MessageType.Error);
                    }
                }

                if (GUILayout.Button("Duplicate"))
                {
                    TryVariantAction(
                        () => DuplicateSavedVariant(selectedName, variantName),
                        "Duplicated '" + selectedName + "'.");
                }

                if (GUILayout.Button("Rename"))
                {
                    TryVariantAction(
                        () => RenameSavedVariant(selectedName, variantName),
                        "Renamed '" + selectedName + "'.");
                }

                if (GUILayout.Button("Delete"))
                {
                    TryVariantAction(
                        () =>
                        {
                            if (!DeleteSavedVariant(selectedName))
                                throw new IOException("Variant no longer exists: " + selectedName + ".");
                        },
                        "Deleted variant '" + selectedName + "'.");
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Portable Preset", EditorStyles.boldLabel);

            if (GUILayout.Button("Export JSON"))
            {
                string path = EditorUtility.SaveFilePanel(
                    "Export VN UI Workshop Preset",
                    string.Empty,
                    ExportFileName,
                    "json");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        ExportCurrentPresetToFile(path);
                    }
                    catch (Exception exception)
                    {
                        SetVariantStatus("Export failed: " + exception.Message, MessageType.Error);
                    }
                }
            }

            if (GUILayout.Button("Import JSON"))
            {
                string path = EditorUtility.OpenFilePanel(
                    "Import VN UI Workshop Preset",
                    string.Empty,
                    "json");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        ImportPresetFromFile(path);
                    }
                    catch (Exception exception)
                    {
                        SetVariantStatus("Import failed: " + exception.Message, MessageType.Error);
                    }
                }
            }

            if (!string.IsNullOrEmpty(variantStatus))
                EditorGUILayout.HelpBox(variantStatus, variantStatusType);
        }

        private void DrawPreviewColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField(comparisonView == VnWorkshopComparisonView.Original ? "Original" : "Current", EditorStyles.boldLabel);
            float previewHeight = Mathf.Max(360f, position.height - 72f);
            Rect previewRect = EditorGUILayout.GetControlRect(false, previewHeight, GUILayout.ExpandWidth(true));
            VnWorkshopPreviewFrame frame = BuildPreviewFrame();
            VnPresentationWorkshopPreviewRenderer.Draw(previewRect, frame, selectedElement);
            HandlePreviewInput(previewRect, frame, Event.current);
            EditorGUILayout.EndVertical();
        }

        private void DrawRightColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(245f));
            EditorGUILayout.LabelField("Selected Element", EditorStyles.boldLabel);
            selectedElement = (VnWorkshopElement)EditorGUILayout.EnumPopup("Element", selectedElement);
            EditorGUILayout.LabelField(GetFriendlyElementName(selectedElement), EditorStyles.miniBoldLabel);
            EditorGUILayout.Space();

            if (comparisonView == VnWorkshopComparisonView.Original)
            {
                EditorGUILayout.HelpBox("Original is the immutable verified baseline. Switch to Current to edit.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(comparisonView == VnWorkshopComparisonView.Original))
            {
                DrawSelectedElementInspector();
                EditorGUILayout.Space();
                DrawFocusInspector();
                EditorGUILayout.Space();

                if (GUILayout.Button("Reset Element"))
                    ResetSelectedElement();
                if (GUILayout.Button("Reset All"))
                    ResetAll();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedElementInspector()
        {
            VnWorkshopElementOverride elementOverride = CurrentPreset.GetElementOverride(selectedElement);
            Vector2 positionDelta = elementOverride.hasPositionDelta ? elementOverride.positionDelta : Vector2.zero;

            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Offsets from Original", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            float positionX = EditorGUILayout.FloatField("Position X", positionDelta.x);
            float positionY = EditorGUILayout.FloatField("Position Y", positionDelta.y);
            if (EditorGUI.EndChangeCheck())
                SetSelectedPosition(new Vector2(positionX, positionY));

            if (SupportsSize(selectedElement))
            {
                Vector2 sizeDelta = elementOverride.hasSizeDelta ? elementOverride.sizeDelta : Vector2.zero;
                EditorGUI.BeginChangeCheck();
                float width = EditorGUILayout.FloatField("Width", sizeDelta.x);
                float height = EditorGUILayout.FloatField("Height", sizeDelta.y);
                if (EditorGUI.EndChangeCheck())
                    SetSelectedSize(new Vector2(width, height));
            }

            if (SupportsScale(selectedElement))
            {
                float scale = elementOverride.hasScaleMultiplier ? elementOverride.scaleMultiplier : 1f;
                EditorGUI.BeginChangeCheck();
                scale = EditorGUILayout.FloatField("Scale", scale);
                if (EditorGUI.EndChangeCheck())
                    SetSelectedScale(scale);
            }

            if (IsBakedControlHitRegion(selectedElement))
            {
                EditorGUILayout.HelpBox("Baked into panel — only the hit region is editable here.", MessageType.Info);
            }
        }

        private void DrawFocusInspector()
        {
            VnWorkshopFocusValues focus = VnPresentationWorkshopResolver.ResolveFocus(CurrentPreset);
            EditorGUILayout.LabelField("Focus", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float offset = EditorGUILayout.FloatField("Character Offset", focus.TwoCharacterOffset);
            float activeScale = EditorGUILayout.FloatField("Active Scale", focus.ActiveScale);
            float inactiveScale = EditorGUILayout.FloatField("Inactive Scale", focus.InactiveScale);
            float brightness = EditorGUILayout.Slider("Inactive Brightness", focus.InactiveBrightness, 0f, 1f);
            float alpha = EditorGUILayout.Slider("Inactive Alpha", focus.InactiveAlpha, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                SetFocusValues(offset, activeScale, inactiveScale, brightness, alpha);
        }

        private void HandlePreviewInput(Rect previewRect, VnWorkshopPreviewFrame frame, Event currentEvent)
        {
            if (currentEvent == null || comparisonView != VnWorkshopComparisonView.Current) return;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && previewRect.Contains(currentEvent.mousePosition))
            {
                Vector2 logicalPoint = VnPresentationWorkshopPreviewRenderer.PreviewToLogical(
                    previewRect, currentEvent.mousePosition, frame);
                if (SelectElementAt(logicalPoint))
                {
                    draggingSelected = true;
                    lastDragLogicalPoint = logicalPoint;
                    Focus();
                    GUI.FocusControl(null);
                    currentEvent.Use();
                }
                return;
            }

            if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && draggingSelected)
            {
                Vector2 logicalPoint = VnPresentationWorkshopPreviewRenderer.PreviewToLogical(
                    previewRect, currentEvent.mousePosition, frame);
                Vector2 logicalDelta = logicalPoint - lastDragLogicalPoint;
                lastDragLogicalPoint = logicalPoint;
                DragSelectedElement(logicalDelta);
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && draggingSelected)
            {
                draggingSelected = false;
                currentEvent.Use();
            }
        }

        private void HandleKeyboardNudge(Event currentEvent)
        {
            if (currentEvent == null || comparisonView != VnWorkshopComparisonView.Current ||
                currentEvent.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
                return;

            Vector2 direction;
            switch (currentEvent.keyCode)
            {
                case KeyCode.LeftArrow:
                    direction = Vector2.left;
                    break;
                case KeyCode.RightArrow:
                    direction = Vector2.right;
                    break;
                case KeyCode.UpArrow:
                    direction = Vector2.up;
                    break;
                case KeyCode.DownArrow:
                    direction = Vector2.down;
                    break;
                default:
                    return;
            }

            NudgeSelectedElement(direction, currentEvent.shift);
            currentEvent.Use();
        }

        private void TryVariantAction(Action action, string successMessage)
        {
            try
            {
                action();
                SetVariantStatus(successMessage, MessageType.Info);
            }
            catch (Exception exception)
            {
                SetVariantStatus(exception.Message, MessageType.Error);
            }
        }

        private void SetVariantStatus(string message, MessageType type)
        {
            variantStatus = message;
            variantStatusType = type;
            Repaint();
        }

        private static string GetProjectRoot()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Could not resolve the Unity project root for Workshop variants.");
            return projectRoot;
        }

        private static bool SupportsSize(VnWorkshopElement element)
        {
            switch (element)
            {
                case VnWorkshopElement.DialoguePanel:
                case VnWorkshopElement.SpeakerName:
                case VnWorkshopElement.DialogueText:
                case VnWorkshopElement.Back:
                case VnWorkshopElement.Next:
                case VnWorkshopElement.MuteHitRegion:
                case VnWorkshopElement.PauseHitRegion:
                case VnWorkshopElement.SkipHitRegion:
                    return true;
                default:
                    return false;
            }
        }

        private static bool SupportsScale(VnWorkshopElement element)
        {
            switch (element)
            {
                case VnWorkshopElement.DialoguePanel:
                case VnWorkshopElement.MinaBody:
                case VnWorkshopElement.Back:
                case VnWorkshopElement.Next:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsBakedControlHitRegion(VnWorkshopElement element)
        {
            return element == VnWorkshopElement.MuteHitRegion ||
                   element == VnWorkshopElement.PauseHitRegion ||
                   element == VnWorkshopElement.SkipHitRegion;
        }

        private static string GetFriendlyElementName(VnWorkshopElement element)
        {
            switch (element)
            {
                case VnWorkshopElement.DialoguePanel: return "Dialogue Panel";
                case VnWorkshopElement.MinaBody: return "Mina / Visible Character";
                case VnWorkshopElement.SpeakerName: return "Speaker Name";
                case VnWorkshopElement.DialogueText: return "Dialogue Text";
                case VnWorkshopElement.Back: return "Back";
                case VnWorkshopElement.Next: return "Next";
                case VnWorkshopElement.MuteHitRegion: return "Mute Hit Region";
                case VnWorkshopElement.PauseHitRegion: return "Pause Hit Region";
                case VnWorkshopElement.SkipHitRegion: return "Skip Hit Region";
                default: return element.ToString();
            }
        }

        private static void SetFocusOverride(ref bool enabled, ref float storage, float value, float baseline)
        {
            enabled = !Mathf.Approximately(value, baseline);
            storage = enabled ? value : 0f;
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Workshop value must be finite: " + name, name);
        }

        private static void RequireRange(float value, float minimum, float maximum, string name)
        {
            RequireFinite(value, name);
            if (value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(name,
                    "Workshop value must stay between " + minimum + " and " + maximum + ".");
        }
    }
}
