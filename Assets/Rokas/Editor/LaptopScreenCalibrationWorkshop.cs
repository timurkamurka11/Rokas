using System;
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
            public int formatVersion = 1;
            public string coordinateSpace = "source_image_pixels_top_left";
            public int sourceWidth = 1672;
            public int sourceHeight = 941;
            public float topLeftX, topLeftY;
            public float topRightX, topRightY;
            public float bottomRightX, bottomRightY;
            public float bottomLeftX, bottomLeftY;
            public float powerX = 1040f, powerY = 708f;
            public bool debugPowerAnchor;
        }

        private enum ViewMode { YomiPreview, BlackScreen, Mask, Outline }
        private static readonly string[] PointLabels = { "TL", "TR", "BR", "BL" };
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
        [SerializeField] private float zoom = 1f;
        [SerializeField] private Vector2 pan;
        [SerializeField] private int selected = -1;
        [SerializeField] private bool showGrid = true;
        [SerializeField] private bool showOutline = true;
        [SerializeField] private bool showHandles = true;
        [SerializeField] private float overlayAlpha = .88f;
        [SerializeField] private ViewMode mode = ViewMode.YomiPreview;
        private Texture2D loadedExternalYomi;
        private Texture2D guide;
        private Material previewMaterial;
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
            if (!guide) guide = MakeGuide();
            wantsMouseMove = true;
        }

        private void OnDisable()
        {
            if (loadedExternalYomi) DestroyImmediate(loadedExternalYomi);
            if (guide) DestroyImmediate(guide);
            if (previewMaterial) DestroyImmediate(previewMaterial);
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
            // Current V11.8 defaults are RAW calibration + the actual safe inset.
            // Exported preset contains the FINAL inside-the-bezel corners.
            var raw = new Preset
            {
                topLeftX = 596, topLeftY = 413, topRightX = 1044,
                topRightY = 412, bottomRightX = 1051, bottomRightY = 676,
                bottomLeftX = 586, bottomLeftY = 676
            };
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(LegacyJson);
            if (json)
            {
                try
                {
                    var parsed = JsonUtility.FromJson<Preset>(json.text);
                    if (parsed != null && parsed.topRightX > parsed.topLeftX &&
                        parsed.bottomRightY > parsed.topRightY)
                        raw = parsed;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("ROKAS LCD Workshop: ignoring bad legacy JSON: " + e.Message);
                }
            }
            originalPowerX = raw.powerX;
            originalPowerY = raw.powerY;
            corners = new[]
            {
                new Vector2(raw.topLeftX + 32f, raw.topLeftY + 5f),
                new Vector2(raw.topRightX - 5f, raw.topRightY + 5f),
                new Vector2(raw.bottomRightX - 5f, raw.bottomRightY - 5f),
                new Vector2(raw.bottomLeftX + 38f, raw.bottomLeftY - 5f)
            };
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
                "Move TL / TR / BR / BL on the ORIGINAL POV image. These are FINAL LCD pixels. " +
                "Save exports a standalone JSON; nothing changes in the game until the preset is reviewed and applied.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    var area = GUILayoutUtility.GetRect(250, 10000, 320, 10000,
                        GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    DrawCanvas(area);
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(275)))
                {
                    DrawTools();
                }
            }
            GUILayout.Space(4);
            GUILayout.Label("Drag colored handles • Middle mouse / Space+drag pans • Wheel zooms • Arrow keys nudge selected point • Shift = 10 px",
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
            zoom = EditorGUILayout.Slider("Zoom", zoom, 1f, 8f);
            showGrid = EditorGUILayout.Toggle("UV / perspective grid", showGrid);
            showOutline = EditorGUILayout.Toggle("LCD outline", showOutline);
            showHandles = EditorGUILayout.Toggle("Show corner handles", showHandles);
            if (GUILayout.Button("Center / fit view"))
            {
                zoom = 1f;
                pan = Vector2.zero;
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
            else EditorGUILayout.HelpBox("Valid convex LCD polygon — safe to export.",
                    MessageType.Info);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to V11.8")) ResetToCurrent();
                if (GUILayout.Button("Load JSON...")) LoadJson();
            }
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("COPY CORNERS JSON", GUILayout.Height(27)))
                {
                    GUIUtility.systemCopyBuffer = JsonUtility.ToJson(MakePreset(), true);
                    ShowNotification(new GUIContent("Calibration JSON copied"));
                }
                if (GUILayout.Button("SAVE / EXPORT JSON...", GUILayout.Height(30)))
                    ExportJson();
            }
            EditorGUILayout.HelpBox(
                "Export only. Does not replace screen_calibration.json or apply " +
                "the old runtime insets a second time. Send the saved JSON for integration.",
                MessageType.None);
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
            DrawPreview(points, area);
            GUI.EndGroup();
            ProcessInput(area, imageRect, scale);
        }

        private void DrawPreview(Vector2[] p, Rect area)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (mode == ViewMode.YomiPreview)
                DrawTexturedQuad(p, yomiProjectTexture ? yomiProjectTexture :
                    loadedExternalYomi ? loadedExternalYomi : guide, area);
            else if (mode == ViewMode.BlackScreen || mode == ViewMode.Mask)
            {
                Handles.color = mode == ViewMode.BlackScreen ?
                    new Color(0, 0, 0, overlayAlpha) :
                    new Color(.15f, .85f, 1f, overlayAlpha * .65f);
                Handles.DrawAAConvexPolygon(p[0], p[1], p[2], p[3]);
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

        private void DrawTexturedQuad(Vector2[] p, Texture image, Rect area)
        {
            if (!image || overlayAlpha <= 0f) return;
            if (!previewMaterial)
            {
                Shader shader = Shader.Find("Unlit/Transparent");
                if (!shader) shader = Shader.Find("Sprites/Default");
                if (!shader) return;
                previewMaterial = new Material(shader)
                    { hideFlags = HideFlags.HideAndDontSave };
            }
            previewMaterial.mainTexture = image;
            previewMaterial.color = new Color(1f, 1f, 1f, overlayAlpha);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, area.width, area.height, 0f);
            if (previewMaterial.SetPass(0))
            {
                GL.Begin(GL.TRIANGLES);
                GL.Color(Color.white); // material tint controls opacity for Unlit/Transparent
                Emit(p[0], 0f, 1f); Emit(p[1], 1f, 1f); Emit(p[2], 1f, 0f);
                Emit(p[0], 0f, 1f); Emit(p[2], 1f, 0f); Emit(p[3], 0f, 0f);
                GL.End();
            }
            GL.PopMatrix();
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
            return true;
        }

        private Preset MakePreset()
        {
            return new Preset
            {
                formatVersion = 1,
                coordinateSpace = "source_image_pixels_top_left",
                sourceWidth = source.width, sourceHeight = source.height,
                topLeftX = corners[0].x, topLeftY = corners[0].y,
                topRightX = corners[1].x, topRightY = corners[1].y,
                bottomRightX = corners[2].x, bottomRightY = corners[2].y,
                bottomLeftX = corners[3].x, bottomLeftY = corners[3].y,
                powerX = originalPowerX, powerY = originalPowerY
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
                corners = proposed;
                if (!ValidateCorners(out string reason))
                {
                    corners = previous;
                    throw new InvalidDataException(reason);
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
