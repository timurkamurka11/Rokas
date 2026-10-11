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
        }

        private enum ViewMode { YomiPreview, BlackScreen, Mask, Outline }
        private static readonly string[] PointLabels = { "TL", "TR", "BR", "BL", "TM", "RM", "BM", "LM" };
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
        [SerializeField] private bool showEdgeGuides = true;
        [SerializeField] private bool showContactTickMarks = true;
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
                        enableEdgeControlPoints = false;
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
            enableEdgeControlPoints = false;
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
            showHandles = EditorGUILayout.Toggle("Show corner handles", showHandles);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fit full photo"))
                {
                    zoom = 1f; pan = Vector2.zero;
                }
                if (GUILayout.Button("Zoom selected (5x)"))
                    ZoomToSelected(5f);
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
            enableEdgeControlPoints = EditorGUILayout.Toggle(
                "Enable 4 edge pins", enableEdgeControlPoints);
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
                            GetEdgePoint(i));
                        if (EditorGUI.EndChangeCheck())
                        {
                            SetEdgePoint(i, edited);
                            Repaint();
                        }
                    }
                }
                if (GUILayout.Button("Straighten all four edges"))
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
                if (selected >= 4) selected = -1;
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
                        var updated = EditorGUILayout.Vector2Field("", corners[i],
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        if (EditorGUI.EndChangeCheck())
                        {
                            corners[i] = updated;
                            Repaint();
                        }
                    }
                }
            }

            bool valid = ValidateCorners(out string error);
            if (!valid) EditorGUILayout.HelpBox(error, MessageType.Error);
            else EditorGUILayout.HelpBox(
                enableEdgeControlPoints
                    ? "Valid 8-point curve — export V3 for review. NOT applied to the game."
                    : "Valid 4-corner LCD polygon — export V2 for the game.",
                MessageType.Info);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to V11.8")) ResetToCurrent();
                if (GUILayout.Button("Load JSON...")) LoadJson();
            }
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button(enableEdgeControlPoints
                    ? "COPY 8-POINT V3 JSON" : "COPY 4-CORNER V2 JSON",
                    GUILayout.Height(27)))
                {
                    GUIUtility.systemCopyBuffer = JsonUtility.ToJson(MakePreset(), true);
                    ShowNotification(new GUIContent("Calibration JSON copied"));
                }
                if (GUILayout.Button(enableEdgeControlPoints
                    ? "SAVE / EXPORT V3 JSON..." : "SAVE / EXPORT V2 JSON...",
                    GUILayout.Height(30)))
                    ExportJson();
            }
            EditorGUILayout.HelpBox(
                "Save never overwrites the live game. V3 edge pins are experimental " +
                "until their exact geometry is approved and separately integrated.",
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

        private Vector2 GetHandle(int index)
        {
            return index < 4 ? corners[index] : GetEdgePoint(index - 4);
        }

        private void SetHandle(int index, Vector2 newPoint)
        {
            if (index < 4) corners[index] = newPoint;
            else SetEdgePoint(index - 4, newPoint);
        }

        private void ZoomToSelected(float magnification)
        {
            if (!source || corners == null || selected < 0 ||
                selected >= (enableEdgeControlPoints ? 8 : 4)) return;
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
                    Handles.DrawAAPolyLine(1f, Vector2.Lerp(p[0], p[1], t),
                        Vector2.Lerp(p[3], p[2], t));
                    Handles.DrawAAPolyLine(1f, Vector2.Lerp(p[0], p[3], t),
                        Vector2.Lerp(p[1], p[2], t));
                }
            }
            if (showOutline)
            {
                Handles.color = new Color(1f, .86f, .18f, 1f);
                Handles.DrawAAPolyLine(2.5f, p[0], p[1], p[2], p[3], p[0]);
            }
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
                for (int i = 0; i < 4; i++)
                {
                    Handles.color = PointColors[i];
                    Handles.DrawSolidDisc(p[i], Vector3.forward, i == selected ? 8f : 6f);
                    var labelRect = new Rect(p[i].x + 9, p[i].y - 13, 28, 18);
                    GUI.Label(labelRect, PointLabels[i], EditorStyles.whiteBoldLabel);
                }
            }
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
                        // UV mask is applied AFTER mapping to the quadrilateral:
                        // vertices follow rounded arcs in normalized (u,v)
                        // space, and are mapped onto the identical LCD corners.
                        // The solid interior remains a UV triangle fan.
                        List<Vector2> outline = BuildRoundedBoundaryUV();
                        Vector2 centerUV = new Vector2(.5f, .5f);
                        Vector2 centerPx = MapCornerUV(centerUV);
                        GL.Begin(GL.TRIANGLES);
                        GL.Color(Color.white);
                        for (int i = 0; i < outline.Count; i++)
                        {
                            Vector2 a = outline[i];
                            Vector2 b = outline[(i + 1) % outline.Count];
                            Emit(centerPx, centerUV.x, 1f - centerUV.y);
                            Emit(MapCornerUV(a), a.x, 1f - a.y);
                            Emit(MapCornerUV(b), b.x, 1f - b.y);
                        }
                        GL.End();
                    }
                }
                finally { GL.PopMatrix(); }
            }
            finally { RenderTexture.active = previous; }
            return previewTarget;
        }

        // UV (u,v) starts at TOP LEFT and ends at BOTTOM RIGHT.
        // Exactly the same bilinear projection is used for image pixels,
        // outline and handles. No later screen-specific rectangle offset.
        private Vector2 MapCornerUV(Vector2 uv)
        {
            Vector2 top = Vector2.Lerp(corners[0], corners[1], uv.x);
            Vector2 bottom = Vector2.Lerp(corners[3], corners[2], uv.x);
            return Vector2.Lerp(top, bottom, uv.y);
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
                zoom = Mathf.Clamp(zoom * (e.delta.y > 0 ? .87f : 1.15f), 1f, 8f);
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
                    for (int i = 0; i < 4; ++i)
                    {
                        Vector2 handle = imageRect.position + corners[i] * scale;
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
                    corners[selected] = new Vector2(
                        Mathf.Clamp((mouse.x - imageRect.x) / scale, 0f, source.width - 1f),
                        Mathf.Clamp((mouse.y - imageRect.y) / scale, 0f, source.height - 1f));
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
            if (e.type == EventType.KeyDown && selected >= 0 && selected < 4)
            {
                float delta = e.shift ? 10f : 1f;
                Vector2 move = Vector2.zero;
                if (e.keyCode == KeyCode.LeftArrow) move.x = -delta;
                if (e.keyCode == KeyCode.RightArrow) move.x = delta;
                if (e.keyCode == KeyCode.UpArrow) move.y = -delta;
                if (e.keyCode == KeyCode.DownArrow) move.y = delta;
                if (move != Vector2.zero)
                {
                    corners[selected] += move;
                    corners[selected] = new Vector2(
                        Mathf.Clamp(corners[selected].x, 0f, source.width - 1f),
                        Mathf.Clamp(corners[selected].y, 0f, source.height - 1f));
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
                formatVersion = 2,
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
                bottomLeftRadius = bottomLeftRadius
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
            ShowNotification(new GUIContent("4 corners exported"));
            EditorUtility.RevealInFinder(path);
        }

        private void LoadJson()
        {
            string path = EditorUtility.OpenFilePanel("Load ROKAS LCD preset",
                Path.GetDirectoryName(Application.dataPath), "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                Preset data = JsonUtility.FromJson<Preset>(File.ReadAllText(path));
                if (data == null || data.coordinateSpace != "source_image_pixels_top_left" ||
                    (source && (data.sourceWidth != source.width ||
                        data.sourceHeight != source.height)))
                    throw new InvalidDataException("The preset must use matching original-photo pixels.");
                var proposed = new[]
                {
                    new Vector2(data.topLeftX, data.topLeftY),
                    new Vector2(data.topRightX, data.topRightY),
                    new Vector2(data.bottomRightX, data.bottomRightY),
                    new Vector2(data.bottomLeftX, data.bottomLeftY)
                };
                Vector2[] previous = corners;
                bool oldEnabled = enableRoundedMask;
                float oldTL = topLeftRadius, oldTR = topRightRadius;
                float oldBR = bottomRightRadius, oldBL = bottomLeftRadius;
                corners = proposed;
                // Older V1 presets had no rounded parameters. Honor their
                // rectangle until explicitly switched to rounded preview.
                bool legacy = data.formatVersion < 2;
                enableRoundedMask = !legacy && data.enableRoundedMask;
                topLeftRadius = legacy ? 0f : data.topLeftRadius;
                topRightRadius = legacy ? 0f : data.topRightRadius;
                bottomRightRadius = legacy ? 0f : data.bottomRightRadius;
                bottomLeftRadius = legacy ? 0f : data.bottomLeftRadius;
                if (!ValidateCorners(out string radiusError))
                {
                    // Loading invalid JSON must never destroy a good
                    // in-progress manual calibration (corners AND radii).
                    corners = previous;
                    enableRoundedMask = oldEnabled;
                    topLeftRadius = oldTL;
                    topRightRadius = oldTR;
                    bottomRightRadius = oldBR;
                    bottomLeftRadius = oldBL;
                    throw new InvalidDataException(radiusError);
                }
                originalPowerX = data.powerX;
                originalPowerY = data.powerY;
                selected = -1; Repaint();
            }
            catch (Exception e)
            {
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
