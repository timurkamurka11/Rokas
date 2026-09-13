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

        [SerializeField] private VnPresentationWorkshopPreset currentPreset = new VnPresentationWorkshopPreset();
        [SerializeField] private VnWorkshopPreviewScene previewScene = VnWorkshopPreviewScene.MinaBody;
        [SerializeField] private VnWorkshopResolution previewResolution = VnWorkshopResolution.Reference1920x1080;
        [SerializeField] private VnWorkshopComparisonView comparisonView = VnWorkshopComparisonView.Current;
        [SerializeField] private VnWorkshopElement selectedElement = VnWorkshopElement.DialoguePanel;

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

        private void OnGUI()
        {
            DrawComparisonToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawLeftColumn();
            DrawPreviewColumn();
            DrawRightColumn();
            EditorGUILayout.EndHorizontal();
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
            EditorGUILayout.LabelField("Saved Variants", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Variant controls are connected in the next Workshop slice.", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField(comparisonView == VnWorkshopComparisonView.Original ? "Original" : "Current", EditorStyles.boldLabel);
            float previewHeight = Mathf.Max(360f, position.height - 72f);
            Rect previewRect = EditorGUILayout.GetControlRect(false, previewHeight, GUILayout.ExpandWidth(true));
            VnPresentationWorkshopPreviewRenderer.Draw(previewRect, BuildPreviewFrame(), selectedElement);
            EditorGUILayout.EndVertical();
        }

        private void DrawRightColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(230f));
            EditorGUILayout.LabelField("Selected Element", EditorStyles.boldLabel);
            selectedElement = (VnWorkshopElement)EditorGUILayout.EnumPopup("Element", selectedElement);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                comparisonView == VnWorkshopComparisonView.Original
                    ? "Original is the immutable verified baseline."
                    : "Current is the Workshop override preset.",
                MessageType.Info);
            EditorGUILayout.EndVertical();
        }
    }
}
