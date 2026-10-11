using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Rokas.Editor
{
    /// <summary>
    /// Non-destructive four-corner LCD calibration of the ORIGINAL source photo.
    /// This is an Editor-only tool: saving a preset NEVER changes runtime art,
    /// the live laptop, or the existing screen_calibration.json.
    /// Coordinates are source-image pixels (origin TOP LEFT), not GameView pixels.
    /// </summary>
    public sealed class LaptopScreenCalibrationWorkshop : EditorWindow
    {
        [Serializable]
        private sealed class Preset
        {
            public int formatVersion = 2;
            public string coordinateSpace = "source_image_pixels_top_left";
            public int sourceWidth = 1672;
            public int sourceHeight = 941;
            public float topLeftX, topLeftY;
            public float topRightX, topRightY;
            public float bottomRightX, bottomRightY;
            public float bottomLeftX, bottomLeftY;
            public float powerX = 1040f, powerY = 708f;
            public bool debugPowerAnchor;
            public bool enableRoundedMask = true;
            public float topLeftRadius = 18f;
            public float topRightRadius = 18f;
            public float bottomRightRadius = 6f;
            public float bottomLeftRadius = 6f;
            // Experimental V3: additional contact points on physical LCD
            // edges. V2 remains fully supported as the production format.
            public bool enableEdgeControlPoints;
            public float topMidX, topMidY;
            public float rightMidX, rightMidY;
            public float bottomMidX, bottomMidY;
            public float leftMidX, leftMidY;
            // Experimental V4: local tangent helpers close to each rounded
            // corner; exports never modify the production V2 LCD mesh.
            public bool enableCornerHelpers;
            public float tlHx, tlHy, tlVx, tlVy;
            public float trHx, trHy, trVx, trVy;
            public float brHx, brHy, brVx, brVy;
            public float blHx, blHy, blVx, blVy;
            public BezelReference bezelReference;
        }

        [Serializable]
        private sealed class BezelReference
        {
            public Vector2[] corners;
            public Vector2[] midpointOffsets;
            public Vector2[] tangentOffsets;
            public bool useMidpoints;
            public bool useTangents;
        }

        private enum ViewMode { YomiPreview, BlackScreen, Mask, Outline }
        private static readonly string[] PointLabels =
        {
            "TL", "TR", "BR", "BL", "TM", "RM", "BM", "LM",
            "TL-H", "TL-V", "TR-H", "TR-V",
            "BR-H", "BR-V", "BL-H", "BL-V"
        };
        // UV locations on physical glass: one tangent helper 18% from
        // each corner along each of the two incident LCD edges.
        private static readonly Vector2[] CornerHelperUV =
        {
            new Vector2(.18f, 0f), new Vector2(0f, .18f),
            new Vector2(.82f, 0f), new Vector2(1f, .18f),
            new Vector2(.82f, 1f), new Vector2(1f, .82f),
            new Vector2(.18f, 1f), new Vector2(0f, .82f)
        };
        private static readonly Color[] PointColors =
        {
            new Color(.35f, .95f, 1f), new Color(1f, .78f, .31f),
            new Color(1f, .48f, .55f), new Color(.51f, 1f, .54f)
        };

        private const string ArtName = "LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF";
        private const string LegacyJson =
            "Assets/Rokas/Resources/LaptopCinematic/screen_calibration.json";

        [SerializeField] private Texture2D source;
        [SerializeField] private Texture2D yomiProjectTexture;
        [SerializeField] private Vector2[] corners;
        // Offsets relative to each straight-edge midpoint: when a CORNER
        // moves, the corresponding extra contact points follow naturally.
        [SerializeField] private Vector2[] edgeOffsets;
        [SerializeField] private bool enableEdgeControlPoints;
        // V4 tangent handles store LOCAL displacements relative to the V3
        // mapping. The four corners and four edge midpoints are unmodified.
        [SerializeField] private bool enableCornerHelpers;
        [SerializeField] private Vector2[] cornerHelperOffsets;
        [SerializeField] private bool showEdgeGuides = true;
        [SerializeField] private bool showContactTickMarks = true;
        [SerializeField] private bool showSafeBorder = true;
        [SerializeField] private float safeBorderPixels = 3f;
        [SerializeField] private bool showErrorHeatmap;
        [SerializeField] private float errorTolerancePixels = 1.5f;
        [SerializeField] private bool showMagnifier = true;
        [SerializeField] private int magnifierZoom = 8;
        [SerializeField] private bool traceReference;
        [SerializeField] private bool referenceDefined;
        [SerializeField] private Vector2[] referenceCorners;
        [SerializeField] private Vector2[] referenceEdgeOffsets;
        [SerializeField] private Vector2[] referenceHelperOffsets;
        [SerializeField] private bool referenceHasEdges;
        [SerializeField] private bool referenceHasHelpers;
        [SerializeField] private Vector2 lastCanvasSize;
        [SerializeField] private float zoom = 1f;
        [SerializeField] private Vector2 pan;
        [SerializeField] private Vector2 toolsScroll;
        [SerializeField] private int selected = -1;
        [SerializeField] private bool showGrid = true;
        [SerializeField] private bool showOutline = true; // outer four-sided polygon
        [SerializeField] private bool showRoundedOutline = true;
        [SerializeField] private bool enableRoundedMask = true;
        [SerializeField] private float topLeftRadius = 18f;
        [SerializeField] private float topRightRadius = 18f;
        [SerializeField] private float bottomRightRadius = 6f;
        [SerializeField] private float bottomLeftRadius = 6f;
        [SerializeField] private bool showHandles = true;
        [SerializeField] private float overlayAlpha = .88f;
        [SerializeField] private ViewMode mode = ViewMode.YomiPreview;
        private Texture2D loadedExternalYomi;
        private Texture2D guide;
        private Material previewMaterial;
        private RenderTexture previewTarget;
        private bool draggingPoint;
        private bool panning;
        private Vector2 lastMouse;
        private float originalPowerX = 1040f, originalPowerY = 708f;

        [MenuItem("ROKAS/Laptop/Screen Calibration Workshop", false, 110)]
        public static void Open()
        {
            var window = GetWindow<LaptopScreenCalibrationWorkshop>(
                "Laptop LCD Workshop");
            window.minSize = new Vector2(880f, 610f);
            window.Show();
        }

        private void OnEnable()
        {
            if (!source) FindOriginalPOV();
            if (corners == null || corners.Length != 4) ResetToCurrent();
            if (edgeOffsets == null || edgeOffsets.Length != 4)
                edgeOffsets = new Vector2[4];
            if (cornerHelperOffsets == null || cornerHelperOffsets.Length != 8)
                cornerHelperOffsets = new Vector2[8];
            if (!guide) guide = MakeGuide();
            wantsMouseMove = true;
        }

        private void OnDisable()
        {
            if (loadedExternalYomi) DestroyImmediate(loadedExternalYomi);
            if (guide) DestroyImmediate(guide);
            if (previewMaterial) DestroyImmediate(previewMaterial);
            if (previewTarget)
            {
                previewTarget.Release();
                DestroyImmediate(previewTarget);
            }
        }

        private void FindOriginalPOV()
        {
            string[] matches = AssetDatabase.FindAssets(ArtName + " t:Texture2D");
            foreach (string guid in matches)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != ArtName) continue;
                source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (source) return;
            }
            source = Resources.Load<Texture2D>("LaptopCinematic/" + ArtName);
        }

        private void ResetToCurrent()
        {
            // IMPORTANT: prefer the currently APPROVED user V2 Resources asset,
            // not the obsolete V11.8 32/38/5px fallback. The Workshop starts
            // from exactly the same FINAL inner pixels as the runtime LCD.
            TextAsset current = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Rokas/Resources/LaptopCinematic/screen_calibration_v2.json");
            if (current)
            {
                try
                {
                    Preset saved = JsonUtility.FromJson<Preset>(current.text);
                    if (saved != null && saved.formatVersion >= 2 &&
                        saved.coordinateSpace == "source_image_pixels_top_left" &&
                        saved.sourceWidth == (source ? source.width : 1672) &&
                        saved.sourceHeight == (source ? source.height : 941))
                    {
                        corners = new[]
                        {
                            new Vector2(saved.topLeftX, saved.topLeftY),
                            new Vector2(saved.topRightX, saved.topRightY),
                            new Vector2(saved.bottomRightX, saved.bottomRightY),
                            new Vector2(saved.bottomLeftX, saved.bottomLeftY)
                        };
                        topLeftRadius = saved.topLeftRadius;
                        topRightRadius = saved.topRightRadius;
                        bottomRightRadius = saved.bottomRightRadius;
                        bottomLeftRadius = saved.bottomLeftRadius;
                        enableRoundedMask = saved.enableRoundedMask;
                        originalPowerX = saved.powerX;
                        originalPowerY = saved.powerY;
                        edgeOffsets = new Vector2[4];
                        cornerHelperOffsets = new Vector2[8];
                        enableEdgeControlPoints = false;
                        enableCornerHelpers = false;
                        traceReference = referenceDefined = false;
                        selected = -1;
                        pan = Vector2.zero;
                        Repaint();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("ROKAS LCD Workshop: bad V2 preset: " + e.Message);
                }
            }
            // Use the previous V11.8 geometry only when no valid V2 exists.
            var raw = new Preset
            {
                topLeftX = 596, topLeftY = 413, topRightX = 1044,
                topRightY = 412, bottomRightX = 1051, bottomRightY = 676
            };
            TextAsset legacy = AssetDatabase.LoadAssetAtPath<TextAsset>(LegacyJson);
            if (legacy)
            {
                try
                {
                    Preset parsed = JsonUtility.FromJson<Preset>(legacy.text);
                    if (parsed != null && parsed.topRightX > parsed.topLeftX)
                        raw = parsed;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("ROKAS LCD Workshop: bad legacy JSON: " + e.Message);
                }
            }
            corners = new[]
            {
                new Vector2(raw.topLeftX + 32f, raw.topLeftY + 5f),
                new Vector2(raw.topRightX - 5f, raw.topRightY + 5f),
                new Vector2(raw.bottomRightX - 5f, raw.bottomRightY - 5f),
                new Vector2(raw.bottomLeftX + 38f, raw.bottomLeftY - 5f)
            };
            originalPowerX = raw.powerX;
            originalPowerY = raw.powerY;
            enableRoundedMask = true;
            topLeftRadius = topRightRadius = 18f;
            bottomRightRadius = bottomLeftRadius = 6f;
            edgeOffsets = new Vector2[4];
            cornerHelperOffsets = new Vector2[8];
            enableEdgeControlPoints = false;
            enableCornerHelpers = false;
            traceReference = referenceDefined = false;
            selected = -1;
            pan = Vector2.zero;
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(
                "ROKAS  /  LAPTOP LCD CALIBRATION WORKSHOP",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adjust TL/TR/BR/BL, then optionally enable 4 extra edge pins " +
                "(TM/RM/BM/LM). V2 is live in the game; experimental V3 is EXPORT ONLY.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    var area = GUILayoutUtility.GetRect(250, 10000, 320, 10000,
                        GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawCanvas(area);
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(305)))
                {
                    toolsScroll = EditorGUILayout.BeginScrollView(toolsScroll,
                        GUILayout.ExpandHeight(true));
                    DrawTools();
                    EditorGUILayout.EndScrollView();
                }
            }
            GUILayout.Space(4);
            GUILayout.Label("4 corners + optional 4 edge pins • Middle mouse / Alt+drag pan • Wheel zoom • Arrow keys ±1 px (Shift ±10)",
                EditorStyles.miniLabel);
        }

        private void DrawTools()
        {
            EditorGUILayout.LabelField("REFERENCE PHOTO", EditorStyles.boldLabel);
            source = (Texture2D)EditorGUILayout.ObjectField(
                source, typeof(Texture2D), false);
            if (!source)
            {
                EditorGUILayout.HelpBox(
                    "Missing source photo. Import the approved POV into Unity Resources " +
                    "or choose it with the object field above. No synthetic room image will be substituted.",
                    MessageType.Warning);
                if (GUILayout.Button("Find original POV")) FindOriginalPOV();
            }
            else
                EditorGUILayout.LabelField(source.width + " x " + source.height
                    + " px · source-image coordinates", EditorStyles.miniLabel);

            GUILayout.Space(6);
            EditorGUILayout.LabelField("LIVE VIEW", EditorStyles.boldLabel);
            mode = (ViewMode)EditorGUILayout.EnumPopup("Preview", mode);
            overlayAlpha = EditorGUILayout.Slider("Overlay opacity", overlayAlpha, 0f, 1f);
            zoom = EditorGUILayout.Slider("Zoom", zoom, 1f, 12f);
            showGrid = EditorGUILayout.Toggle("UV / perspective grid", showGrid);
            showOutline = EditorGUILayout.Toggle("Show outer polygon", showOutline);
            showRoundedOutline = EditorGUILayout.Toggle("Show rounded LCD", showRoundedOutline);
            showHandles = EditorGUILayout.Toggle("Show corner / edge pins", showHandles);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fit full photo"))
                {
                    zoom = 1f; pan = Vector2.zero;
                }
                if (GUILayout.Button("Zoom selected (5x)"))
                    ZoomToSelected(5f);
                if (GUILayout.Button("Zoom 12x"))
                    ZoomToSelected(12f);
            }

            yomiProjectTexture = (Texture2D)EditorGUILayout.ObjectField(
                "YOMI in project", yomiProjectTexture, typeof(Texture2D), false);
            if (GUILayout.Button("Choose captured YOMI PNG...")) ChooseYomiPNG();
            if (mode == ViewMode.YomiPreview &&
                !yomiProjectTexture && !loadedExternalYomi)
                EditorGUILayout.HelpBox("Showing a CHECKER GRID, not actual YOMI. " +
                    "Select an existing YOMI screenshot PNG for exact preview.",
                    MessageType.None);

            GUILayout.Space(7);
            EditorGUILayout.LabelField("ROUNDED LCD (source pixels)",
                EditorStyles.boldLabel);
            enableRoundedMask = EditorGUILayout.Toggle("Enable rounded mask",
                enableRoundedMask);
            using (new EditorGUI.DisabledScope(!enableRoundedMask))
            {
                float maxRadius = MaxCornerRadius();
                topLeftRadius = EditorGUILayout.Slider("Top left radius", topLeftRadius, 0f, maxRadius);
                topRightRadius = EditorGUILayout.Slider("Top right radius", topRightRadius, 0f, maxRadius);
                bottomRightRadius = EditorGUILayout.Slider("Bottom right radius", bottomRightRadius, 0f, maxRadius);
                bottomLeftRadius = EditorGUILayout.Slider("Bottom left radius", bottomLeftRadius, 0f, maxRadius);
            }
            EditorGUILayout.LabelField("Radius rounds the VISIBLE pixels inside each",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField("corner. The four handles stay on outer LCD bounds.",
                EditorStyles.miniLabel);

            GUILayout.Space(7);
            EditorGUILayout.LabelField("EDGE CONTACT POINTS (optional V3)",
                EditorStyles.boldLabel);
            if (!traceReference)
            {
                enableEdgeControlPoints = EditorGUILayout.Toggle(
                    "Enable 4 edge pins", enableEdgeControlPoints);
                if (enableCornerHelpers) enableEdgeControlPoints = true;
            }
            else
                EditorGUILayout.LabelField("Tracing reference, not moving mask",
                    EditorStyles.miniLabel);
            showEdgeGuides = EditorGUILayout.Toggle(
                "Show curved edge lines", showEdgeGuides);
            showContactTickMarks = EditorGUILayout.Toggle(
                "25 / 50 / 75% markers", showContactTickMarks);
            if (enableEdgeControlPoints)
            {
                EditorGUILayout.HelpBox(
                    "Drag TM / RM / BM / LM to match any slightly curved glass edge. " +
                    "They deform the UV preview AND the rounded boundary. V3 exports " +
                    "these four contacts; the game continues using approved V2 until reviewed.",
                    MessageType.Info);
                if (edgeOffsets == null || edgeOffsets.Length != 4)
                    edgeOffsets = new Vector2[4];
                for (int i = 0; i < 4; ++i)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        int handleId = i + 4;
                        if (GUILayout.Toggle(selected == handleId,
                            PointLabels[handleId], "Button", GUILayout.Width(38)))
                            selected = handleId;
                        EditorGUI.BeginChangeCheck();
                        Vector2 edited = EditorGUILayout.Vector2Field("",
                            GetHandle(handleId));
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetHandle(handleId, edited);
                            Repaint();
                        }
                    }
                }
                if (!traceReference &&
                    GUILayout.Button("Straighten all four edges"))
                {
                    edgeOffsets = new Vector2[4];
                    Repaint();
                }
            }
            else
            {
                EditorGUILayout.LabelField(
                    "Off = exact V2 4-corner mask; no extra deformation.",
                    EditorStyles.miniLabel);
                if (!traceReference && selected >= 4) selected = -1;
            }

            GUILayout.Space(7);
            EditorGUILayout.LabelField("CORNER TANGENT HELPERS (V4)",
                EditorStyles.boldLabel);
            if (!traceReference)
                enableCornerHelpers = EditorGUILayout.Toggle(
                    "Enable 8 corner helpers", enableCornerHelpers);
            if (enableCornerHelpers) enableEdgeControlPoints = true;
            if (enableCornerHelpers || (traceReference && referenceHasHelpers))
            {
                EditorGUILayout.HelpBox(
                    "8 small local points near the rounded corners. " +
                    "H adjusts top/bottom; V adjusts left/right without " +
                    "dragging the entire side.", MessageType.Info);
                for (int i = 0; i < 8; i++)
                {
                    int id = i + 8;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(selected == id, PointLabels[id],
                            "Button", GUILayout.Width(53))) selected = id;
                        EditorGUI.BeginChangeCheck();
                        Vector2 value = EditorGUILayout.Vector2Field("",
                            GetHandle(id));
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetHandle(id, value); Repaint();
                        }
                    }
                }
                if (!traceReference && GUILayout.Button("Reset corner helpers"))
                {
                    cornerHelperOffsets = new Vector2[8];
                    Repaint();
                }
            }
            GUILayout.Space(7);
            EditorGUILayout.LabelField("BEZEL REFERENCE / ERROR FIT",
                EditorStyles.boldLabel);
            if (!referenceDefined)
            {
                EditorGUILayout.HelpBox(
                    "NO measured bezel reference yet. First capture current " +
                    "shape, then TRACE the real glass border with the handles. " +
                    "Colors compare to THAT traced line, not computer vision.",
                    MessageType.Warning);
                if (GUILayout.Button("Start tracing bezel reference"))
                    BeginReferenceTracing();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    traceReference
                        ? "TRACING REFERENCE: handles edit GOLD bezel line. " +
                          "The BLUE screen mask is unchanged."
                        : "FITTING MASK: handles edit screen mask; GOLD " +
                          "reference remains fixed.",
                    MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(traceReference
                        ? "Back to FIT MASK" : "EDIT REFERENCE"))
                    {
                        traceReference = !traceReference; selected = -1; Repaint();
                    }
                    if (GUILayout.Button("Re-capture reference"))
                    {
                        if (EditorUtility.DisplayDialog("Replace reference?",
                            "Replace the old manually traced bezel reference " +
                            "with the CURRENT screen mask? The old reference " +
                            "cannot be recovered.", "Replace", "Cancel"))
                            BeginReferenceTracing();
                    }
                }
                showErrorHeatmap = EditorGUILayout.Toggle(
                    "Difference / heatmap", showErrorHeatmap);
                errorTolerancePixels = EditorGUILayout.Slider(
                    "Green tolerance (px)", errorTolerancePixels, .25f, 8f);
            }
            showSafeBorder = EditorGUILayout.Toggle(
                "Inner safe contour", showSafeBorder);
            safeBorderPixels = EditorGUILayout.Slider(
                "Safe inset (px)", safeBorderPixels, 0f, 12f);
            EditorGUILayout.LabelField("Red: outside | Cyan: gap | Green: within tolerance",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Gold: traced reference | White: fitted mask",
                EditorStyles.miniLabel);

            GUILayout.Space(7);
            EditorGUILayout.LabelField("CORNER MAGNIFIER",
                EditorStyles.boldLabel);
            showMagnifier = EditorGUILayout.Toggle(
                "Show selected detail", showMagnifier);
            if (showMagnifier)
            {
                magnifierZoom = EditorGUILayout.IntPopup(
                    "Magnification", magnifierZoom,
                    new[] { "4x", "8x", "12x", "20x" },
                    new[] { 4, 8, 12, 20 });
                DrawMagnifier();
            }

            GUILayout.Space(7);
            EditorGUILayout.LabelField("FOUR CORNERS (source pixels)",
                EditorStyles.boldLabel);
            if (corners != null && corners.Length == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(selected == i, PointLabels[i],
                                "Button", GUILayout.Width(34))) selected = i;
                        EditorGUI.BeginChangeCheck();
                        var updated = EditorGUILayout.Vector2Field("",
                            GetHandle(i),
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetHandle(i, updated);
                            Repaint();
                        }
                    }
                }
            }

            bool valid = ValidateCorners(out string error);
            if (!valid) EditorGUILayout.HelpBox(error, MessageType.Error);
            else EditorGUILayout.HelpBox(
                enableCornerHelpers
                    ? "V4 16-point mask — export for review (NOT in game)."
                    : enableEdgeControlPoints
                        ? "V3 8-point mask — export for review (NOT in game)."
                        : "V2 4-corner mask — export for review.",
                MessageType.Info);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to approved V2")) ResetToCurrent();
                if (GUILayout.Button("Load JSON...")) LoadJson();
            }
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button(enableCornerHelpers
                    ? "COPY 16-POINT V4 JSON" : enableEdgeControlPoints
                        ? "COPY 8-POINT V3 JSON" : "COPY 4-CORNER V2 JSON",
                    GUILayout.Height(27)))
                {
                    GUIUtility.systemCopyBuffer = JsonUtility.ToJson(MakePreset(), true);
                    ShowNotification(new GUIContent("Calibration JSON copied"));
                }
                if (GUILayout.Button(enableCornerHelpers
                    ? "SAVE / EXPORT V4 JSON..." : enableEdgeControlPoints
                        ? "SAVE / EXPORT V3 JSON..." : "SAVE / EXPORT V2 JSON...",
                    GUILayout.Height(30)))
                    ExportJson();
            }
            EditorGUILayout.HelpBox(
                "V4 exports are REVIEW-ONLY. They do not change the approved " +
                "V2 in-game screen or its original textures.",
                MessageType.None);
        }

        private Vector2 EdgeBase(int index)
        {
            if (corners == null || corners.Length != 4) return Vector2.zero;
            switch (index)
            {
                case 0: return (corners[0] + corners[1]) * .5f; // top
                case 1: return (corners[1] + corners[2]) * .5f; // right
                case 2: return (corners[2] + corners[3]) * .5f; // bottom
                default: return (corners[3] + corners[0]) * .5f; // left
            }
        }

        private Vector2 GetEdgePoint(int index)
        {
            return EdgeBase(index) +
                (edgeOffsets != null && edgeOffsets.Length == 4
                    ? edgeOffsets[index] : Vector2.zero);
        }

        private void SetEdgePoint(int index, Vector2 value)
        {
            if (edgeOffsets == null || edgeOffsets.Length != 4)
                edgeOffsets = new Vector2[4];
            edgeOffsets[index] = value - EdgeBase(index);
        }

        private Vector2 GetCornerHelper(int index)
        {
            return MapBaseUV(CornerHelperUV[index]) +
                (cornerHelperOffsets != null && cornerHelperOffsets.Length == 8 ?
                    cornerHelperOffsets[index] : Vector2.zero);
        }

        private void SetCornerHelper(int index, Vector2 point)
        {
            if (cornerHelperOffsets == null || cornerHelperOffsets.Length != 8)
                cornerHelperOffsets = new Vector2[8];
            cornerHelperOffsets[index] = point - MapBaseUV(CornerHelperUV[index]);
        }

        private int HandleCount => traceReference && referenceDefined
            ? (referenceHasHelpers ? 16 : referenceHasEdges ? 8 : 4)
            : (enableCornerHelpers ? 16 : enableEdgeControlPoints ? 8 : 4);

        private Vector2 GetHandle(int index)
        {
            if (traceReference && referenceDefined)
                return GetReferenceHandle(index);
            return index < 4 ? corners[index] :
                index < 8 ? GetEdgePoint(index - 4) : GetCornerHelper(index - 8);
        }

        private void SetHandle(int index, Vector2 point)
        {
            if (traceReference && referenceDefined)
            {
                SetReferenceHandle(index, point);
                return;
            }
            if (index < 4) corners[index] = point;
            else if (index < 8) SetEdgePoint(index - 4, point);
            else SetCornerHelper(index - 8, point);
        }

        private void BeginReferenceTracing()
        {
            referenceCorners = (Vector2[])corners.Clone();
            referenceEdgeOffsets = edgeOffsets == null ?
                new Vector2[4] : (Vector2[])edgeOffsets.Clone();
            referenceHelperOffsets = cornerHelperOffsets == null ?
                new Vector2[8] : (Vector2[])cornerHelperOffsets.Clone();
            referenceHasEdges = enableEdgeControlPoints;
            referenceHasHelpers = enableCornerHelpers;
            referenceDefined = traceReference = true;
            showErrorHeatmap = true;
            // Expose the actual photographic bezel while reference points
            // are being traced; tinted preview must not hide its edge.
            mode = ViewMode.Outline;
            selected = -1;
            Repaint();
        }

        private Vector2 MapReferenceUV(Vector2 uv)
        {
            return MapUV(uv, referenceCorners, referenceEdgeOffsets,
                referenceHelperOffsets, referenceHasEdges, referenceHasHelpers);
        }

        private Vector2 GetReferenceHandle(int index)
        {
            if (index < 4) return referenceCorners[index];
            if (index < 8)
            {
                int edge = index - 4;
                Vector2 midpoint = (referenceCorners[edge] +
                    referenceCorners[(edge + 1) % 4]) * .5f;
                return midpoint + referenceEdgeOffsets[edge];
            }
            int helper = index - 8;
            return MapUV(CornerHelperUV[helper], referenceCorners,
                referenceEdgeOffsets, null, referenceHasEdges, false) +
                referenceHelperOffsets[helper];
        }

        private void SetReferenceHandle(int index, Vector2 point)
        {
            if (index < 4)
            {
                referenceCorners[index] = point;
                return;
            }
            if (index < 8)
            {
                int edge = index - 4;
                Vector2 midpoint = (referenceCorners[edge] +
                    referenceCorners[(edge + 1) % 4]) * .5f;
                referenceEdgeOffsets[edge] = point - midpoint;
                return;
            }
            int helper = index - 8;
            referenceHelperOffsets[helper] = point -
                MapUV(CornerHelperUV[helper], referenceCorners,
                    referenceEdgeOffsets, null, referenceHasEdges, false);
        }

        private void ZoomToSelected(float magnification)
        {
            if (!source || corners == null || selected < 0 ||
                selected >= HandleCount) return;
            zoom = Mathf.Clamp(magnification, 1f, 12f);
            Vector2 focus = new Vector2(.5f, .56f);
            float w = Mathf.Max(50f, lastCanvasSize.x - 16f);
            float h = Mathf.Max(50f, lastCanvasSize.y - 16f);
            float scale = Mathf.Min(w / source.width,
                h / source.height) * zoom;
            Vector2 wanted = GetHandle(selected);
            pan = new Vector2(focus.x * source.width - wanted.x,
                focus.y * source.height - wanted.y) * scale;
            Repaint();
        }

        private void DrawCanvas(Rect area)
        {
            GUI.Box(area, GUIContent.none, EditorStyles.helpBox);
            if (!source || corners == null || corners.Length != 4)
            {
                GUI.Label(area, "Select the approved original LaptopPOV image.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }
            lastCanvasSize = area.size;
            var inner = new Rect(area.x + 8, area.y + 8,
                Mathf.Max(50, area.width - 16), Mathf.Max(50, area.height - 16));
            float scale = Mathf.Min(inner.width / source.width,
                inner.height / source.height) * zoom;
            // Keep focus FIXED while handles are dragged. Moving the camera
            // according to the moving polygon centroid caused feedback/jitter.
            // At zoom 1 this is the exact image center, at zoom > 1 the
            // factory LCD center is comfortably in view.
            Vector2 focus = zoom > 1.001f
                ? new Vector2(.5f, .56f) : new Vector2(.5f, .5f);
            var imageRect = new Rect(inner.center.x - focus.x * source.width * scale + pan.x,
                inner.center.y - focus.y * source.height * scale + pan.y,
                source.width * scale, source.height * scale);

            GUI.BeginGroup(area);
            Rect localImage = new Rect(imageRect.x - area.x, imageRect.y - area.y,
                imageRect.width, imageRect.height);
            GUI.DrawTexture(localImage, source, ScaleMode.StretchToFill, false);
            Vector2[] points = new Vector2[4];
            for (int i = 0; i < 4; i++)
                points[i] = new Vector2(localImage.x + corners[i].x * scale,
                    localImage.y + corners[i].y * scale);
            DrawPreview(points, localImage);
            GUI.EndGroup();
            ProcessInput(area, imageRect, scale);
        }

        private void DrawPreview(Vector2[] p, Rect localImage)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (mode != ViewMode.Outline)
            {
                // All previews use ONE UV-clipped polygon in original source
                // pixels; no separate Handles polygon leaking beyond the LCD.
                Texture preview = mode == ViewMode.YomiPreview ?
                    (yomiProjectTexture ? yomiProjectTexture :
                     loadedExternalYomi ? loadedExternalYomi : guide) :
                    Texture2D.whiteTexture;
                Color tint = mode == ViewMode.BlackScreen ? Color.black :
                    mode == ViewMode.Mask ? new Color(.15f, .85f, 1f, 1f) :
                    Color.white;
                RenderTexture aligned = RenderTexturedLCD(preview, tint);
                if (aligned)
                    GUI.DrawTexture(localImage, aligned, ScaleMode.StretchToFill, true);
            }
            if (showGrid)
            {
                Handles.color = new Color(.75f, .98f, 1f, .65f);
                for (int step = 1; step <= 7; step++)
                {
                    float t = step / 8f;
                    DrawUVLine(localImage, new Vector2(t, 0f),
                        new Vector2(t, 1f), 1f);
                    DrawUVLine(localImage, new Vector2(0f, t),
                        new Vector2(1f, t), 1f);
                }
            }
            if (enableEdgeControlPoints && showEdgeGuides)
            {
                Handles.color = new Color(1f, .3f, .9f, .95f);
                for (int edge = 0; edge < 4; ++edge)
                    DrawUVLine(localImage, EdgeUV(edge, 0f),
                        EdgeUV(edge, 1f), 2f);
            }
            if (showContactTickMarks)
            {
                Handles.color = new Color(1f, 1f, 1f, .9f);
                for (int edge = 0; edge < 4; ++edge)
                    foreach (float fraction in new[] { .25f, .5f, .75f })
                    {
                        Vector2 px = MapCornerUV(EdgeUV(edge, fraction));
                        Vector2 spot = DrawPosition(localImage, px);
                        Handles.DrawSolidDisc(spot, Vector3.forward, 2.2f);
                    }
            }
            if (showOutline)
            {
                Handles.color = new Color(1f, .86f, .18f, 1f);
                Handles.DrawAAPolyLine(1.5f, p[0], p[1], p[2], p[3], p[0]);
            }
            DrawReferenceAndMargins(localImage);
            if (showRoundedOutline && enableRoundedMask)
            {
                List<Vector2> boundary = BuildRoundedBoundaryUV();
                Vector3[] outline = new Vector3[boundary.Count + 1];
                for (int i = 0; i < boundary.Count; i++)
                {
                    Vector2 photoPx = MapCornerUV(boundary[i]);
                    outline[i] = new Vector3(
                        localImage.x + photoPx.x * localImage.width / source.width,
                        localImage.y + photoPx.y * localImage.height / source.height, 0f);
                }
                outline[boundary.Count] = outline[0];
                Handles.color = new Color(.2f, 1f, .75f, .96f);
                Handles.DrawAAPolyLine(2.25f, outline);
            }
            if (showHandles)
            {
                int count = HandleCount;
                for (int i = 0; i < count; i++)
                {
                    Vector2 position = DrawPosition(localImage, GetHandle(i));
                    Handles.color = i < 4 ? PointColors[i] :
                        i < 8 ? new Color(.97f, .31f, .93f, 1f) :
                        new Color(.99f, .94f, .35f);
                    float radius = i == selected ? 10f :
                        i < 4 ? 7f : i < 8 ? 8f : 6f;
                    // High-contrast ring keeps pins visible on the bright
                    // lantern, YOMI, or dim photographic bezel.
                    Handles.color = Color.black;
                    Handles.DrawSolidDisc(position, Vector3.forward, radius + 2f);
                    Handles.color = i < 4 ? PointColors[i] :
                        i < 8 ? new Color(.97f, .31f, .93f, 1f) :
                        new Color(.99f, .94f, .35f);
                    Handles.DrawSolidDisc(position, Vector3.forward, radius);
                    Rect label = new Rect(position.x + 10, position.y - 14, 36, 18);
                    GUI.Label(label, PointLabels[i], EditorStyles.whiteBoldLabel);
                }
            }
        }

        private List<Vector2> DenseBoundaryUV()
        {
            List<Vector2> original = BuildRoundedBoundaryUV();
            var dense = new List<Vector2>(original.Count * 3);
            for (int i = 0; i < original.Count; ++i)
            {
                Vector2 a = original[i];
                Vector2 b = original[(i + 1) % original.Count];
                int count = Mathf.Max(1, Mathf.CeilToInt(
                    Vector2.Distance(a, b) * 90f));
                for (int step = 0; step < count; ++step)
                    dense.Add(Vector2.Lerp(a, b, step / (float)count));
            }
            return dense;
        }

        private static Vector2 OutwardNormal(Vector2 before, Vector2 after)
        {
            Vector2 tangent = after - before;
            Vector2 outward = new Vector2(tangent.y, -tangent.x);
            return outward.sqrMagnitude > 1e-10f
                ? outward.normalized : Vector2.zero;
        }

        private void DrawReferenceAndMargins(Rect localImage)
        {
            if (!showSafeBorder && !referenceDefined) return;
            List<Vector2> uv = DenseBoundaryUV();
            int count = uv.Count;
            if (count < 4) return;

            if (showSafeBorder)
            {
                var inset = new Vector3[count + 1];
                for (int i = 0; i < count; ++i)
                {
                    Vector2 before = MapCornerUV(uv[(i + count - 1) % count]);
                    Vector2 point = MapCornerUV(uv[i]);
                    Vector2 after = MapCornerUV(uv[(i + 1) % count]);
                    Vector2 normal = OutwardNormal(before, after);
                    inset[i] = DrawPosition(localImage,
                        point - safeBorderPixels * normal);
                }
                inset[count] = inset[0];
                Handles.color = new Color(.64f, .90f, 1f, .85f);
                Handles.DrawAAPolyLine(1.2f, inset);
            }

            if (!referenceDefined || referenceCorners == null ||
                referenceCorners.Length != 4) return;
            var reference = new Vector3[count + 1];
            for (int i = 0; i < count; ++i)
                reference[i] = DrawPosition(localImage, MapReferenceUV(uv[i]));
            reference[count] = reference[0];
            Handles.color = new Color(1f, .69f, .08f, .95f);
            Handles.DrawAAPolyLine(2.5f, reference);
            if (!showErrorHeatmap) return;

            for (int i = 0; i < count; ++i)
            {
                int j = (i + 1) % count;
                Vector2 refPrevious = MapReferenceUV(uv[(i + count - 1) % count]);
                Vector2 refHere = MapReferenceUV(uv[i]);
                Vector2 refNext = MapReferenceUV(uv[j]);
                Vector2 normal = OutwardNormal(refPrevious, refNext);
                Vector2 fitted = MapCornerUV(uv[i]);
                float signedError = Vector2.Dot(fitted - refHere, normal);
                Color shade = signedError > errorTolerancePixels
                    ? new Color(1f, .13f, .16f, .9f)
                    : signedError < -errorTolerancePixels
                        ? new Color(.12f, .85f, 1f, .9f)
                        : new Color(.2f, 1f, .40f, .9f);
                Vector3 a = DrawPosition(localImage, fitted);
                Vector3 b = DrawPosition(localImage, MapCornerUV(uv[j]));
                Handles.color = shade;
                Handles.DrawAAPolyLine(2.5f, a, b);
                // Sampled cross-ticks show actual distance to independently
                // traced reference, instead of relying on pixel color guesses.
                if ((i % 5) == 0 &&
                    Mathf.Abs(signedError) > errorTolerancePixels)
                {
                    Handles.color = new Color(shade.r, shade.g, shade.b, .55f);
                    Handles.DrawAAPolyLine(1f,
                        DrawPosition(localImage, refHere), a);
                }
            }
        }

        private void DrawMagnifier()
        {
            Rect area = GUILayoutUtility.GetRect(240f, 182f,
                GUILayout.ExpandWidth(true));
            GUI.Box(area, GUIContent.none, EditorStyles.helpBox);
            if (!source || selected < 0 || selected >= HandleCount)
            {
                GUI.Label(area, "Select TL/TR/BR/BL or a helper point",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }
            Vector2 origin = GetHandle(selected);
            float visibleWidth = area.width / Mathf.Max(1, magnifierZoom);
            float visibleHeight = area.height / Mathf.Max(1, magnifierZoom);
            Rect texCoords = new Rect(
                (origin.x - visibleWidth * .5f) / source.width,
                1f - (origin.y + visibleHeight * .5f) / source.height,
                visibleWidth / source.width, visibleHeight / source.height);
            GUI.BeginGroup(area);
            try
            {
                Rect local = new Rect(0f, 0f, area.width, area.height);
                GUI.DrawTextureWithTexCoords(local, source, texCoords, false);
                List<Vector2> uv = DenseBoundaryUV();
                int n = uv.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = MapCornerUV(uv[i]);
                    Vector2 b = MapCornerUV(uv[(i + 1) % n]);
                    Vector3 aScreen = new Vector3(
                        (a.x - origin.x) * magnifierZoom + area.width * .5f,
                        (a.y - origin.y) * magnifierZoom + area.height * .5f);
                    Vector3 bScreen = new Vector3(
                        (b.x - origin.x) * magnifierZoom + area.width * .5f,
                        (b.y - origin.y) * magnifierZoom + area.height * .5f);
                    Handles.color = new Color(.1f, 1f, .9f, 1f);
                    Handles.DrawAAPolyLine(1.5f, aScreen, bScreen);
                    if (referenceDefined && referenceCorners != null)
                    {
                        Vector2 ra = MapReferenceUV(uv[i]);
                        Vector2 rb = MapReferenceUV(uv[(i + 1) % n]);
                        Handles.color = new Color(1f, .75f, .15f, .9f);
                        Handles.DrawAAPolyLine(1.5f,
                            new Vector3((ra.x - origin.x) * magnifierZoom +
                                area.width * .5f,
                                (ra.y - origin.y) * magnifierZoom + area.height * .5f),
                            new Vector3((rb.x - origin.x) * magnifierZoom +
                                area.width * .5f,
                                (rb.y - origin.y) * magnifierZoom + area.height * .5f));
                    }
                }
                Vector2 middle = local.center;
                Handles.color = Color.white;
                Handles.DrawAAPolyLine(1f,
                    new Vector3(middle.x - 9f, middle.y),
                    new Vector3(middle.x + 9f, middle.y));
                Handles.DrawAAPolyLine(1f,
                    new Vector3(middle.x, middle.y - 9f),
                    new Vector3(middle.x, middle.y + 9f));
            }
            finally { GUI.EndGroup(); }
            EditorGUILayout.LabelField(PointLabels[selected] +
                " · " + origin.x.ToString("F2") + ", " +
                origin.y.ToString("F2") + " px", EditorStyles.miniLabel);
        }

        private Vector2 DrawPosition(Rect localImage, Vector2 sourcePoint)
        {
            return new Vector2(localImage.x + sourcePoint.x * localImage.width / source.width,
                localImage.y + sourcePoint.y * localImage.height / source.height);
        }

        // Clockwise side order: Top, Right, Bottom, Left. Guides are auxiliary
        // physical-glass touchpoints, not additional imaginary corners.
        private static Vector2 EdgeUV(int edge, float t)
        {
            switch (edge)
            {
                case 0: return new Vector2(t, 0f);
                case 1: return new Vector2(1f, t);
                case 2: return new Vector2(1f - t, 1f);
                default: return new Vector2(0f, 1f - t);
            }
        }

        private void DrawUVLine(Rect rect, Vector2 startUV, Vector2 endUV,
            float thickness)
        {
            const int subdivisions = 24;
            Vector3[] path = new Vector3[subdivisions + 1];
            for (int i = 0; i <= subdivisions; i++)
            {
                Vector2 uv = Vector2.Lerp(startUV, endUV,
                    i / (float)subdivisions);
                Vector2 pt = DrawPosition(rect, MapCornerUV(uv));
                path[i] = new Vector3(pt.x, pt.y, 0f);
            }
            Handles.DrawAAPolyLine(thickness, path);
        }

        private RenderTexture RenderTexturedLCD(Texture image, Color tint)
        {
            if (!image || !source || overlayAlpha <= 0f) return null;
            if (!previewMaterial)
            {
                Shader shader = Shader.Find("Unlit/Transparent");
                if (!shader) shader = Shader.Find("Sprites/Default");
                if (!shader) return null;
                previewMaterial = new Material(shader)
                    { hideFlags = HideFlags.HideAndDontSave };
            }
            // A separate transparent RT avoids IMGUI GUI.BeginGroup / GL
            // projection mismatch. Its geometry uses unscaled ORIGINAL PHOTO
            // pixels, exactly the same four values displayed in the sidebar.
            if (!previewTarget || previewTarget.width != source.width ||
                previewTarget.height != source.height)
            {
                if (previewTarget)
                {
                    previewTarget.Release();
                    DestroyImmediate(previewTarget);
                }
                previewTarget = new RenderTexture(source.width, source.height,
                    0, RenderTextureFormat.ARGB32)
                {
                    name = "ROKAS_Laptop_CalibrationPreview_EditorOnly",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                previewTarget.Create();
            }
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = previewTarget;
                GL.Clear(true, true, Color.clear);
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0f, source.width, source.height, 0f);
                    previewMaterial.mainTexture = image;
                    previewMaterial.color = new Color(tint.r, tint.g, tint.b,
                        overlayAlpha * tint.a * (mode == ViewMode.Mask ? .65f : 1f));
                    if (previewMaterial.SetPass(0))
                    {
                        // Dense rings preserve texture UVs when mid-edge
                        // controls bend the glass. A center-only fan would
                        // visually shear the checkerboard vs the guides.
                        List<Vector2> border = BuildRoundedBoundaryUV();
                        Vector2 center = new Vector2(.5f, .5f);
                        GL.Begin(GL.TRIANGLES);
                        GL.Color(Color.white);
                        const int radialSteps = 10;
                        for (int ring = 1; ring <= radialSteps; ring++)
                        {
                            float inner = (ring - 1f) / radialSteps;
                            float outer = ring / (float)radialSteps;
                            for (int i = 0; i < border.Count; i++)
                            {
                                Vector2 a = border[i];
                                Vector2 b = border[(i + 1) % border.Count];
                                Vector2 ua = Vector2.Lerp(center, a, inner);
                                Vector2 ub = Vector2.Lerp(center, b, inner);
                                Vector2 va = Vector2.Lerp(center, a, outer);
                                Vector2 vb = Vector2.Lerp(center, b, outer);
                                EmitWarped(ua); EmitWarped(va); EmitWarped(vb);
                                if (ring > 1)
                                {
                                    EmitWarped(ua); EmitWarped(vb); EmitWarped(ub);
                                }
                            }
                        }
                        GL.End();
                    }
                }
                finally { GL.PopMatrix(); }
            }
            finally { RenderTexture.active = previous; }
            return previewTarget;
        }

        private void EmitWarped(Vector2 uv)
        {
            Vector2 pixel = MapCornerUV(uv);
            Emit(pixel, uv.x, 1f - uv.y);
        }

        // V2 bilinear, V3 midpoint, V4 corner-local tangent deformation.
        // All modes share one UV->photo-pixel mapping so mask, YOMI and
        // colored outlines never drift apart.
        private Vector2 MapCornerUV(Vector2 uv)
        {
            return MapUV(uv, corners, edgeOffsets, cornerHelperOffsets,
                enableEdgeControlPoints, enableCornerHelpers);
        }

        private Vector2 MapBaseUV(Vector2 uv)
        {
            return MapUV(uv, corners, edgeOffsets, null,
                enableEdgeControlPoints, false);
        }

        private static float NearStart(float t)
        {
            const float k = .18f;
            if (t <= 0f || t >= .5f) return 0f;
            return t <= k ? Mathf.SmoothStep(0f, 1f, t / k) :
                Mathf.SmoothStep(1f, 0f, (t - k) / (.5f - k));
        }

        private static Vector2 MapUV(Vector2 uv, Vector2[] bounds,
            Vector2[] edgeDeltas, Vector2[] helperDeltas, bool useEdges,
            bool useHelpers)
        {
            Vector2 top = Vector2.Lerp(bounds[0], bounds[1], uv.x);
            Vector2 bottom = Vector2.Lerp(bounds[3], bounds[2], uv.x);
            Vector2 result = Vector2.Lerp(top, bottom, uv.y);
            if (useEdges && edgeDeltas != null && edgeDeltas.Length == 4)
            {
                float h = 4f * uv.x * (1f - uv.x);
                float v = 4f * uv.y * (1f - uv.y);
                result += h * Vector2.Lerp(edgeDeltas[0], edgeDeltas[2], uv.y);
                result += v * Vector2.Lerp(edgeDeltas[3], edgeDeltas[1], uv.x);
            }
            if (useHelpers && helperDeltas != null && helperDeltas.Length == 8)
            {
                float u = uv.x, v = uv.y;
                float l = NearStart(u), r = NearStart(1f - u);
                float t = NearStart(v), b = NearStart(1f - v);
                float topFalloff = (1f - v) * (1f - v);
                float bottomFalloff = v * v;
                float leftFalloff = (1f - u) * (1f - u);
                float rightFalloff = u * u;
                result += helperDeltas[0] * (l * topFalloff);
                result += helperDeltas[1] * (t * leftFalloff);
                result += helperDeltas[2] * (r * topFalloff);
                result += helperDeltas[3] * (t * rightFalloff);
                result += helperDeltas[4] * (r * bottomFalloff);
                result += helperDeltas[5] * (b * rightFalloff);
                result += helperDeltas[6] * (l * bottomFalloff);
                result += helperDeltas[7] * (b * leftFalloff);
            }
            return result;
        }

        private float MaxCornerRadius()
        {
            if (corners == null || corners.Length != 4) return 80f;
            float top = Vector2.Distance(corners[0], corners[1]);
            float bottom = Vector2.Distance(corners[3], corners[2]);
            float left = Vector2.Distance(corners[0], corners[3]);
            float right = Vector2.Distance(corners[1], corners[2]);
            return Mathf.Max(0f, Mathf.Min(120f,
                .45f * Mathf.Min((top + bottom) * .5f, (left + right) * .5f)));
        }

        private List<Vector2> BuildRoundedBoundaryUV()
        {
            var outline = new List<Vector2>(48);
            if (!enableRoundedMask)
            {
                outline.Add(Vector2.zero);
                outline.Add(Vector2.right);
                outline.Add(Vector2.one);
                outline.Add(Vector2.up);
                return outline;
            }
            float width = Mathf.Max(1f, (Vector2.Distance(corners[0], corners[1])
                + Vector2.Distance(corners[3], corners[2])) * .5f);
            float height = Mathf.Max(1f, (Vector2.Distance(corners[0], corners[3])
                + Vector2.Distance(corners[1], corners[2])) * .5f);
            float cap = MaxCornerRadius();
            float tl = Mathf.Clamp(topLeftRadius, 0f, cap);
            float tr = Mathf.Clamp(topRightRadius, 0f, cap);
            float br = Mathf.Clamp(bottomRightRadius, 0f, cap);
            float bl = Mathf.Clamp(bottomLeftRadius, 0f, cap);
            float tlx = tl / width, tly = tl / height;
            float trx = tr / width, tryy = tr / height;
            float brx = br / width, bry = br / height;
            float blx = bl / width, bly = bl / height;
            // Clockwise, starting at the left edge near TL. Each arc's
            // terminal tangent joins the next arc with a straight edge.
            AddRoundedArc(outline, new Vector2(tlx, tly), tlx, tly,
                180f, 270f, Vector2.zero);
            AddRoundedArc(outline, new Vector2(1f - trx, tryy), trx, tryy,
                270f, 360f, Vector2.right);
            AddRoundedArc(outline, new Vector2(1f - brx, 1f - bry), brx, bry,
                0f, 90f, Vector2.one);
            AddRoundedArc(outline, new Vector2(blx, 1f - bly), blx, bly,
                90f, 180f, Vector2.up);
            return outline;
        }

        private static void AddRoundedArc(List<Vector2> outline,
            Vector2 center, float radiusX, float radiusY,
            float startAngle, float endAngle, Vector2 squareCorner)
        {
            if (radiusX <= .00001f || radiusY <= .00001f)
            {
                outline.Add(squareCorner);
                return;
            }
            const int subdivisions = 12;
            for (int i = 0; i <= subdivisions; i++)
            {
                float angle = Mathf.Lerp(startAngle, endAngle,
                    i / (float)subdivisions) * Mathf.Deg2Rad;
                outline.Add(new Vector2(center.x + Mathf.Cos(angle) * radiusX,
                    center.y + Mathf.Sin(angle) * radiusY));
            }
        }

        private static void Emit(Vector2 p, float u, float v)
        {
            GL.TexCoord2(u, v);
            GL.Vertex3(p.x, p.y, 0f);
        }

        private void ProcessInput(Rect area, Rect imageRect, float scale)
        {
            Event e = Event.current;
            if (!area.Contains(e.mousePosition) && !draggingPoint && !panning &&
                e.type != EventType.KeyDown)
                return;
            if (e.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom * (e.delta.y > 0 ? .87f : 1.15f), 1f, 12f);
                e.Use(); Repaint(); return;
            }
            Vector2 mouse = e.mousePosition;
            if (e.type == EventType.MouseDown)
            {
                if (e.button == 2 || (e.button == 0 && e.modifiers == EventModifiers.Alt))
                {
                    panning = true; lastMouse = mouse; e.Use(); return;
                }
                if (e.button == 0)
                {
                    int nearest = -1;
                    float distance = 17f;
                    int count = HandleCount;
                    for (int i = 0; i < count; ++i)
                    {
                        Vector2 handle = imageRect.position + GetHandle(i) * scale;
                        float d = Vector2.Distance(handle, mouse);
                        if (d < distance) { nearest = i; distance = d; }
                    }
                    if (nearest >= 0)
                    {
                        selected = nearest; draggingPoint = true;
                        Focus(); e.Use(); return;
                    }
                }
            }
            if (e.type == EventType.MouseDrag)
            {
                if (draggingPoint && selected >= 0)
                {
                    SetHandle(selected, new Vector2(
                        Mathf.Clamp((mouse.x - imageRect.x) / scale, 0f, source.width - 1f),
                        Mathf.Clamp((mouse.y - imageRect.y) / scale, 0f, source.height - 1f)));
                    e.Use(); Repaint(); return;
                }
                if (panning)
                {
                    pan += mouse - lastMouse;
                    lastMouse = mouse; e.Use(); Repaint(); return;
                }
            }
            if (e.type == EventType.MouseUp && (draggingPoint || panning))
            {
                draggingPoint = panning = false; e.Use(); Repaint(); return;
            }
            if (e.type == EventType.KeyDown && selected >= 0 &&
                selected < HandleCount)
            {
                float delta = e.shift ? 10f : 1f;
                Vector2 move = Vector2.zero;
                if (e.keyCode == KeyCode.LeftArrow) move.x = -delta;
                if (e.keyCode == KeyCode.RightArrow) move.x = delta;
                if (e.keyCode == KeyCode.UpArrow) move.y = -delta;
                if (e.keyCode == KeyCode.DownArrow) move.y = delta;
                if (move != Vector2.zero)
                {
                    Vector2 current = GetHandle(selected) + move;
                    SetHandle(selected, new Vector2(
                        Mathf.Clamp(current.x, 0f, source.width - 1f),
                        Mathf.Clamp(current.y, 0f, source.height - 1f)));
                    e.Use(); Repaint();
                }
            }
        }

        private bool ValidateCorners(out string message)
        {
            message = "";
            if (!source || corners == null || corners.Length != 4)
            {
                message = "Original POV image / four corners are required.";
                return false;
            }
            float area = 0;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4],
                    c = corners[(i + 2) % 4];
                if (float.IsNaN(a.x) || float.IsNaN(a.y) ||
                    a.x < 0 || a.y < 0 || a.x >= source.width || a.y >= source.height)
                {
                    message = "A corner is outside the original source image.";
                    return false;
                }
                float cross = (b.x - a.x) * (c.y - b.y)
                    - (b.y - a.y) * (c.x - b.x);
                if (cross <= .001f)
                {
                    message = "Corners must be TL → TR → BR → BL, convex and not crossing.";
                    return false;
                }
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 200f)
            {
                message = "LCD polygon has almost no visible area.";
                return false;
            }
            if (enableEdgeControlPoints || enableCornerHelpers)
            {
                if (edgeOffsets == null || edgeOffsets.Length != 4)
                {
                    message = "Four edge contact pins have not been initialized.";
                    return false;
                }
                float edgeAllowance = Mathf.Min(100f,
                    .34f * Mathf.Min(
                        Vector2.Distance(corners[0], corners[3]),
                        Vector2.Distance(corners[1], corners[2])));
                for (int edge = 0; edge < 4; ++edge)
                {
                    Vector2 pin = GetEdgePoint(edge);
                    if (float.IsNaN(pin.x) || float.IsNaN(pin.y) ||
                        float.IsInfinity(pin.x) || float.IsInfinity(pin.y) ||
                        pin.x < 0f || pin.y < 0f ||
                        pin.x >= source.width || pin.y >= source.height ||
                        edgeOffsets[edge].magnitude > edgeAllowance)
                    {
                        message = "Edge pin outside the source photo or too far from its side.";
                        return false;
                    }
                }
                if (enableCornerHelpers)
                {
                    if (cornerHelperOffsets == null ||
                        cornerHelperOffsets.Length != 8)
                    {
                        message = "V4 requires all eight corner helpers.";
                        return false;
                    }
                    for (int i = 0; i < 8; ++i)
                    {
                        Vector2 pt = GetCornerHelper(i);
                        Vector2 delta = cornerHelperOffsets[i];
                        if (float.IsNaN(pt.x) || float.IsNaN(pt.y) ||
                            float.IsInfinity(pt.x) || float.IsInfinity(pt.y) ||
                            pt.x < 0f || pt.y < 0f ||
                            pt.x >= source.width || pt.y >= source.height ||
                            delta.magnitude > 65f)
                        {
                            message = "Corner helper " + PointLabels[i + 8] +
                                " is too far from its original bezel arc.";
                            return false;
                        }
                    }
                }
                // Reject folded/crossing warped LCDs: UV must retain positive
                // orientation at all dense grid sample cells.
                const int cells = 24;
                for (int u = 0; u < cells; ++u)
                    for (int v = 0; v < cells; ++v)
                    {
                        float x = u / (float)cells, y = v / (float)cells;
                        float h = 1f / cells;
                        Vector2 a = MapCornerUV(new Vector2(x, y));
                        Vector2 b = MapCornerUV(new Vector2(x + h, y));
                        Vector2 d = MapCornerUV(new Vector2(x, y + h));
                        float determinant = (b.x - a.x) * (d.y - a.y) -
                            (b.y - a.y) * (d.x - a.x);
                        if (determinant <= .0001f)
                        {
                            message = "Warped LCD folds over itself; reduce edge pin displacement.";
                            return false;
                        }
                    }
            }
            if (referenceDefined)
            {
                if (referenceCorners == null || referenceCorners.Length != 4 ||
                    referenceEdgeOffsets == null || referenceEdgeOffsets.Length != 4 ||
                    referenceHelperOffsets == null ||
                    referenceHelperOffsets.Length != 8)
                {
                    message = "Incomplete independent bezel reference.";
                    return false;
                }
                for (int i = 0; i < 4; ++i)
                {
                    Vector2 rp = referenceCorners[i];
                    if (float.IsNaN(rp.x) || float.IsNaN(rp.y) ||
                        rp.x < 0 || rp.y < 0 ||
                        rp.x >= source.width || rp.y >= source.height)
                    {
                        message = "Reference bezel corner outside photo.";
                        return false;
                    }
                }
                for (int u = 0; u < 24; ++u)
                    for (int v = 0; v < 24; ++v)
                    {
                        float x = u / 24f, y = v / 24f, h = 1f / 24f;
                        Vector2 a = MapReferenceUV(new Vector2(x, y));
                        Vector2 b = MapReferenceUV(new Vector2(x + h, y));
                        Vector2 d = MapReferenceUV(new Vector2(x, y + h));
                        float cross = (b.x - a.x) * (d.y - a.y) -
                            (b.y - a.y) * (d.x - a.x);
                        if (float.IsNaN(cross) || cross <= .0001f)
                        {
                            message = "Traced reference crosses/folds itself.";
                            return false;
                        }
                    }
            }
            if (enableRoundedMask)
            {
                float cap = MaxCornerRadius();
                foreach (float radius in new[] {
                    topLeftRadius, topRightRadius, bottomRightRadius, bottomLeftRadius })
                {
                    if (float.IsNaN(radius) || float.IsInfinity(radius) ||
                        radius < 0f || radius > cap + .01f)
                    {
                        message = "LCD corner radii must be finite and fit inside the current polygon.";
                        return false;
                    }
                }
            }
            return true;
        }

        private Preset MakePreset()
        {
            return new Preset
            {
                formatVersion = enableCornerHelpers ? 4 :
                    enableEdgeControlPoints ? 3 : 2,
                coordinateSpace = "source_image_pixels_top_left",
                sourceWidth = source.width, sourceHeight = source.height,
                topLeftX = corners[0].x, topLeftY = corners[0].y,
                topRightX = corners[1].x, topRightY = corners[1].y,
                bottomRightX = corners[2].x, bottomRightY = corners[2].y,
                bottomLeftX = corners[3].x, bottomLeftY = corners[3].y,
                powerX = originalPowerX, powerY = originalPowerY,
                enableRoundedMask = enableRoundedMask,
                topLeftRadius = topLeftRadius,
                topRightRadius = topRightRadius,
                bottomRightRadius = bottomRightRadius,
                bottomLeftRadius = bottomLeftRadius,
                enableEdgeControlPoints = enableEdgeControlPoints,
                topMidX = GetEdgePoint(0).x, topMidY = GetEdgePoint(0).y,
                rightMidX = GetEdgePoint(1).x, rightMidY = GetEdgePoint(1).y,
                bottomMidX = GetEdgePoint(2).x, bottomMidY = GetEdgePoint(2).y,
                leftMidX = GetEdgePoint(3).x, leftMidY = GetEdgePoint(3).y,
                enableCornerHelpers = enableCornerHelpers,
                tlHx = GetCornerHelper(0).x, tlHy = GetCornerHelper(0).y,
                tlVx = GetCornerHelper(1).x, tlVy = GetCornerHelper(1).y,
                trHx = GetCornerHelper(2).x, trHy = GetCornerHelper(2).y,
                trVx = GetCornerHelper(3).x, trVy = GetCornerHelper(3).y,
                brHx = GetCornerHelper(4).x, brHy = GetCornerHelper(4).y,
                brVx = GetCornerHelper(5).x, brVy = GetCornerHelper(5).y,
                blHx = GetCornerHelper(6).x, blHy = GetCornerHelper(6).y,
                blVx = GetCornerHelper(7).x, blVy = GetCornerHelper(7).y,
                bezelReference = referenceDefined ? new BezelReference
                {
                    corners = (Vector2[])referenceCorners.Clone(),
                    midpointOffsets = (Vector2[])referenceEdgeOffsets.Clone(),
                    tangentOffsets = (Vector2[])referenceHelperOffsets.Clone(),
                    useMidpoints = referenceHasEdges,
                    useTangents = referenceHasHelpers
                } : null
            };
        }

        private void ExportJson()
        {
            if (!ValidateCorners(out string error))
            {
                EditorUtility.DisplayDialog("Invalid LCD", error, "OK");
                return;
            }
            string directory = Path.GetDirectoryName(Application.dataPath);
            string path = EditorUtility.SaveFilePanel("Export ROKAS LCD corners",
                directory, "ROKAS_Laptop_LCD_Calibration", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, JsonUtility.ToJson(MakePreset(), true) + "\n",
                new UTF8Encoding(false));
            ShowNotification(new GUIContent(enableCornerHelpers
                ? "V4 16-point LCD exported" : enableEdgeControlPoints
                    ? "V3 8-point LCD exported" : "V2 4-corner LCD exported"));
            EditorUtility.RevealInFinder(path);
        }

        private void ApplyPreset(Preset data)
        {
            if (data == null)
                throw new InvalidDataException("Calibration JSON is null.");
            corners = new[]
            {
                new Vector2(data.topLeftX, data.topLeftY),
                new Vector2(data.topRightX, data.topRightY),
                new Vector2(data.bottomRightX, data.bottomRightY),
                new Vector2(data.bottomLeftX, data.bottomLeftY)
            };
            bool legacy = data.formatVersion < 2;
            enableRoundedMask = !legacy && data.enableRoundedMask;
            topLeftRadius = legacy ? 0f : data.topLeftRadius;
            topRightRadius = legacy ? 0f : data.topRightRadius;
            bottomRightRadius = legacy ? 0f : data.bottomRightRadius;
            bottomLeftRadius = legacy ? 0f : data.bottomLeftRadius;
            edgeOffsets = new Vector2[4];
            enableEdgeControlPoints = data.formatVersion >= 3 &&
                data.enableEdgeControlPoints;
            if (enableEdgeControlPoints)
            {
                SetEdgePoint(0, new Vector2(data.topMidX, data.topMidY));
                SetEdgePoint(1, new Vector2(data.rightMidX, data.rightMidY));
                SetEdgePoint(2, new Vector2(data.bottomMidX, data.bottomMidY));
                SetEdgePoint(3, new Vector2(data.leftMidX, data.leftMidY));
            }
            cornerHelperOffsets = new Vector2[8];
            enableCornerHelpers = data.formatVersion >= 4 &&
                data.enableCornerHelpers;
            if (enableCornerHelpers)
            {
                enableEdgeControlPoints = true;
                SetCornerHelper(0, new Vector2(data.tlHx, data.tlHy));
                SetCornerHelper(1, new Vector2(data.tlVx, data.tlVy));
                SetCornerHelper(2, new Vector2(data.trHx, data.trHy));
                SetCornerHelper(3, new Vector2(data.trVx, data.trVy));
                SetCornerHelper(4, new Vector2(data.brHx, data.brHy));
                SetCornerHelper(5, new Vector2(data.brVx, data.brVy));
                SetCornerHelper(6, new Vector2(data.blHx, data.blHy));
                SetCornerHelper(7, new Vector2(data.blVx, data.blVy));
            }
            var reference = data.bezelReference;
            referenceDefined = reference != null &&
                reference.corners != null && reference.corners.Length == 4 &&
                reference.midpointOffsets != null &&
                reference.midpointOffsets.Length == 4 &&
                reference.tangentOffsets != null &&
                reference.tangentOffsets.Length == 8;
            if (referenceDefined)
            {
                referenceCorners = (Vector2[])reference.corners.Clone();
                referenceEdgeOffsets = (Vector2[])reference.midpointOffsets.Clone();
                referenceHelperOffsets = (Vector2[])reference.tangentOffsets.Clone();
                referenceHasEdges = reference.useMidpoints;
                referenceHasHelpers = reference.useTangents;
            }
            else
            {
                referenceCorners = null;
                referenceEdgeOffsets = new Vector2[4];
                referenceHelperOffsets = new Vector2[8];
                referenceHasEdges = referenceHasHelpers = false;
            }
            originalPowerX = data.powerX;
            originalPowerY = data.powerY;
            traceReference = false;
            selected = -1;
        }

        private void LoadJson()
        {
            string path = EditorUtility.OpenFilePanel("Load ROKAS LCD preset",
                Path.GetDirectoryName(Application.dataPath), "json");
            if (string.IsNullOrEmpty(path)) return;
            Preset previous = null;
            try
            {
                Preset data = JsonUtility.FromJson<Preset>(File.ReadAllText(path));
                if (data == null ||
                    data.coordinateSpace != "source_image_pixels_top_left" ||
                    (source && (data.sourceWidth != source.width ||
                        data.sourceHeight != source.height)))
                    throw new InvalidDataException(
                        "Calibration must use the original source-photo pixels.");
                previous = MakePreset();
                ApplyPreset(data);
                if (!ValidateCorners(out string reason))
                    throw new InvalidDataException(reason);
                Repaint();
                ShowNotification(new GUIContent("LCD V" +
                    data.formatVersion + " calibration loaded"));
            }
            catch (Exception e)
            {
                if (previous != null)
                    ApplyPreset(previous); // atomic undo incl. tangents/reference
                EditorUtility.DisplayDialog("Unable to load LCD calibration",
                    e.Message, "OK");
            }
        }

        private void ChooseYomiPNG()
        {
            string path = EditorUtility.OpenFilePanel("Choose original YOMI screenshot",
                Path.GetDirectoryName(Application.dataPath), "png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, false))
                    throw new InvalidDataException("Invalid PNG contents");
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                if (loadedExternalYomi) DestroyImmediate(loadedExternalYomi);
                loadedExternalYomi = texture;
                mode = ViewMode.YomiPreview;
                Repaint();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Could not load PNG", e.Message, "OK");
            }
        }

        private static Texture2D MakeGuide()
        {
            const int w = 512, h = 288;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; ++y)
                for (int x = 0; x < w; ++x)
                {
                    bool edge = x < 6 || x >= w - 6 || y < 6 || y >= h - 6;
                    bool grid = x % 64 < 2 || y % 48 < 2;
                    Color32 blue = ((x / 64 + y / 48) % 2 == 0)
                        ? new Color32(26, 87, 145, 255)
                        : new Color32(13, 55, 105, 255);
                    pixels[y * w + x] = edge ? new Color32(255, 184, 38, 255) :
                        grid ? new Color32(128, 231, 248, 255) : blue;
                }
            t.SetPixels32(pixels);
            t.Apply(false, true);
            return t;
        }
    }
}
