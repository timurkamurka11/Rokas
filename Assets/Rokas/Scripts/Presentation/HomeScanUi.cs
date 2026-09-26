using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum HomeScanGlyph
    {
        LightBulb,
        Window,
        DeskLamp,
        Swords,
        Laptop,
        Tea,
        Cat,
        Exit
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomeScanRuleGraphic : MaskableGraphic
    {
        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Color left = new Color(.96f, .72f, .35f, .98f);
            Color right = new Color(.96f, .72f, .35f, 0f);
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), left, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), left, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), right, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), right, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomeScanOutlineGraphic : MaskableGraphic
    {
        private static readonly Color Glow = new Color(.96f, .66f, .24f, .08f);
        private static readonly Color Halo = new Color(.98f, .70f, .28f, .24f);
        private static readonly Color Core = new Color(1f, .73f, .30f, .94f);

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();

            // Window / balcony: the oracle highlights the three illuminated frame bays.
            GlowRect(mesh, 398, 35, 610, 553);
            GlowRect(mesh, 626, 35, 840, 553);
            GlowRect(mesh, 856, 35, 1006, 553);

            // Left standing paper lamp.
            GlowPath(mesh, new[]
            {
                P(18, 443), P(170, 443), P(188, 718), P(14, 718)
            }, true);
            GlowPath(mesh, new[] { P(54, 443), P(54, 718) }, false);
            GlowPath(mesh, new[] { P(132, 443), P(132, 718) }, false);

            // Desk lamp.
            GlowPath(mesh, new[]
            {
                P(1037, 335), P(1050, 274), P(1077, 247), P(1085, 219),
                P(1111, 213), P(1122, 242), P(1161, 258), P(1147, 279),
                P(1112, 268), P(1096, 295), P(1065, 304)
            }, true);

            // Katana / sword display.
            GlowPath(mesh, new[]
            {
                P(1163, 335), P(1397, 338), P(1411, 349), P(1396, 357),
                P(1161, 351)
            }, true);

            // Door portal frame.
            GlowRect(mesh, 1592, 33, 1832, 610);

            // Laptop silhouette.
            GlowPath(mesh, new[]
            {
                P(886, 522), P(1169, 522), P(1194, 628), P(1144, 643),
                P(878, 646), P(853, 620)
            }, true);

            // Tea cup.
            GlowPath(mesh, new[]
            {
                P(781, 632), P(846, 632), P(852, 682), P(790, 686)
            }, true);
            GlowEllipse(mesh, 851, 656, 18, 20);

            // Cat bed / familiar.
            GlowEllipse(mesh, 289, 886, 150, 70);

            // Equipment backpack visible in the oracle.
            GlowPath(mesh, new[]
            {
                P(1380, 518), P(1401, 500), P(1442, 493), P(1477, 505),
                P(1504, 498), P(1532, 520), P(1555, 620), P(1530, 642),
                P(1395, 641), P(1367, 610)
            }, true);
        }

        private void GlowRect(VertexHelper mesh, float x0, float y0, float x1, float y1)
        {
            GlowPath(mesh, new[] { P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1) }, true);
        }

        private void GlowEllipse(VertexHelper mesh, float cx, float cy, float rx, float ry)
        {
            const int segments = 42;
            var points = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                points[i] = P(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            GlowPath(mesh, points, true);
        }

        private void GlowPath(VertexHelper mesh, Vector2[] points, bool closed)
        {
            Stroke(mesh, points, closed, 13f, Glow);
            Stroke(mesh, points, closed, 6f, Halo);
            Stroke(mesh, points, closed, 2.2f, Core);
        }

        private void Stroke(VertexHelper mesh, Vector2[] points, bool closed, float thickness, Color color)
        {
            if (points == null || points.Length < 2) return;
            for (int i = 0; i < points.Length - 1; i++)
                Segment(mesh, points[i], points[i + 1], thickness, color);
            if (closed)
                Segment(mesh, points[points.Length - 1], points[0], thickness, color);
        }

        private static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color color)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude <= .0001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * thickness * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
        }

        private Vector2 P(float x, float y)
        {
            Rect rect = GetPixelAdjustedRect();
            return new Vector2(rect.xMin + x, rect.yMax - y);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomeScanGlyphGraphic : MaskableGraphic
    {
        [SerializeField] private HomeScanGlyph glyph;
        private Rect drawingRect;
        private float scale;

        public HomeScanGlyph Glyph
        {
            get => glyph;
            set { glyph = value; SetVerticesDirty(); }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            drawingRect = GetPixelAdjustedRect();
            scale = Mathf.Min(drawingRect.width, drawingRect.height) / 100f;
            if (scale <= 0f) return;

            switch (glyph)
            {
                case HomeScanGlyph.LightBulb: DrawLightBulb(mesh); break;
                case HomeScanGlyph.Window: DrawWindow(mesh); break;
                case HomeScanGlyph.DeskLamp: DrawDeskLamp(mesh); break;
                case HomeScanGlyph.Swords: DrawSwords(mesh); break;
                case HomeScanGlyph.Laptop: DrawLaptop(mesh); break;
                case HomeScanGlyph.Tea: DrawTea(mesh); break;
                case HomeScanGlyph.Cat: DrawCat(mesh); break;
                case HomeScanGlyph.Exit: DrawExit(mesh); break;
            }
        }

        private void DrawLightBulb(VertexHelper mesh)
        {
            Ellipse(mesh, 50, 37, 20, 23, 5f, 24);
            Line(mesh, 37, 58, 43, 66, 5f);
            Line(mesh, 63, 58, 57, 66, 5f);
            Line(mesh, 41, 69, 59, 69, 6f);
            Line(mesh, 43, 77, 57, 77, 5f);
            Line(mesh, 46, 84, 54, 84, 5f);
        }

        private void DrawWindow(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(23, 22), new Vector2(77, 22), new Vector2(77, 78), new Vector2(23, 78) }, 5f, true);
            Line(mesh, 50, 23, 50, 77, 5f);
            Line(mesh, 32, 31, 32, 69, 3f);
            Line(mesh, 68, 31, 68, 69, 3f);
        }

        private void DrawDeskLamp(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(31, 25), new Vector2(67, 25), new Vector2(76, 48), new Vector2(22, 48) }, 5f, true);
            Line(mesh, 49, 48, 49, 72, 6f);
            Line(mesh, 34, 78, 66, 78, 6f);
        }

        private void DrawSwords(VertexHelper mesh)
        {
            Line(mesh, 24, 20, 72, 74, 6f);
            Line(mesh, 76, 20, 28, 74, 6f);
            Line(mesh, 18, 26, 34, 42, 5f);
            Line(mesh, 82, 26, 66, 42, 5f);
            Line(mesh, 26, 72, 18, 82, 6f);
            Line(mesh, 74, 72, 82, 82, 6f);
        }

        private void DrawLaptop(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(22, 24), new Vector2(78, 24), new Vector2(78, 64), new Vector2(22, 64) }, 5f, true);
            Line(mesh, 15, 76, 85, 76, 6f);
            Line(mesh, 29, 68, 71, 68, 4f);
        }

        private void DrawTea(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(27, 42), new Vector2(66, 42), new Vector2(62, 71), new Vector2(34, 71) }, 5f, true);
            Ellipse(mesh, 69, 55, 10, 11, 5f, 22);
            Line(mesh, 27, 79, 69, 79, 5f);
            Stroke(mesh, new[] { new Vector2(39, 35), new Vector2(36, 29), new Vector2(40, 23) }, 4f, false);
            Stroke(mesh, new[] { new Vector2(53, 35), new Vector2(50, 29), new Vector2(54, 23) }, 4f, false);
        }

        private void DrawCat(VertexHelper mesh)
        {
            Ellipse(mesh, 50, 54, 24, 21, 5f, 26);
            FilledTriangle(mesh, new Vector2(31, 42), new Vector2(35, 22), new Vector2(46, 38));
            FilledTriangle(mesh, new Vector2(54, 38), new Vector2(66, 22), new Vector2(69, 43));
            Disc(mesh, new Vector2(42, 54), 3.2f);
            Disc(mesh, new Vector2(58, 54), 3.2f);
            Line(mesh, 46, 65, 50, 68, 3f);
            Line(mesh, 50, 68, 54, 65, 3f);
        }

        private void DrawExit(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(20, 20), new Vector2(58, 20), new Vector2(58, 80), new Vector2(20, 80) }, 5f, true);
            Line(mesh, 43, 50, 84, 50, 6f);
            Line(mesh, 70, 36, 84, 50, 6f);
            Line(mesh, 84, 50, 70, 64, 6f);
            Disc(mesh, new Vector2(48, 56), 3.2f);
        }

        private void Line(VertexHelper mesh, float x1, float y1, float x2, float y2, float thickness)
        {
            Vector2 a = Map(new Vector2(x1, y1));
            Vector2 b = Map(new Vector2(x2, y2));
            Vector2 delta = b - a;
            if (delta.sqrMagnitude <= .0001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * thickness * scale * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
        }

        private void Stroke(VertexHelper mesh, Vector2[] points, float thickness, bool closed)
        {
            for (int i = 0; i < points.Length - 1; i++)
                Line(mesh, points[i].x, points[i].y, points[i + 1].x, points[i + 1].y, thickness);
            if (closed)
                Line(mesh, points[points.Length - 1].x, points[points.Length - 1].y,
                    points[0].x, points[0].y, thickness);
        }

        private void Ellipse(VertexHelper mesh, float cx, float cy, float rx, float ry, float thickness, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                Line(mesh,
                    cx + Mathf.Cos(a0) * rx, cy + Mathf.Sin(a0) * ry,
                    cx + Mathf.Cos(a1) * rx, cy + Mathf.Sin(a1) * ry,
                    thickness);
            }
        }

        private void Disc(VertexHelper mesh, Vector2 center, float radius)
        {
            const int segments = 16;
            int first = mesh.currentVertCount;
            mesh.AddVert(Map(center), color, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                mesh.AddVert(Map(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius), color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
                mesh.AddTriangle(first, first + i + 1, first + (i + 1) % segments + 1);
        }

        private void FilledTriangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(Map(a), color, Vector2.zero);
            mesh.AddVert(Map(b), color, Vector2.zero);
            mesh.AddVert(Map(c), color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
        }

        private Vector2 Map(Vector2 point)
        {
            Vector2 center = drawingRect.center;
            return new Vector2(
                center.x + (point.x - 50f) * scale,
                center.y + (50f - point.y) * scale);
        }
    }

    public sealed class HomeScanMarkerFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform root;
        private Button button;
        private LaptopSurface glow;
        private CanvasGroup glowGroup;
        private LaptopSurface border;
        private LaptopSurface face;
        private Graphic glyph;
        private CanvasGroup connector;
        private bool hovered;
        private bool selected;
        private bool pressed;
        private bool attention;

        public void Initialize(RectTransform markerRoot, Button owner, LaptopSurface markerGlow,
            CanvasGroup markerGlowGroup, LaptopSurface markerBorder, LaptopSurface markerFace,
            Graphic markerGlyph, CanvasGroup markerConnector)
        {
            root = markerRoot;
            button = owner;
            glow = markerGlow;
            glowGroup = markerGlowGroup;
            border = markerBorder;
            face = markerFace;
            glyph = markerGlyph;
            connector = markerConnector;
        }

        public void SetAttention(bool value)
        {
            attention = value;
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnSelect(BaseEventData eventData) { selected = true; }
        public void OnDeselect(BaseEventData eventData) { selected = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) pressed = true;
        }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }

        private void Update()
        {
            if (!root || !button) return;

            float focus = button.IsInteractable() && (hovered || selected) ? 1f : 0f;
            float targetScale = pressed ? .965f : Mathf.Lerp(1f, 1.055f, focus);
            float speed = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            root.localScale = Vector3.Lerp(root.localScale,
                new Vector3(targetScale, targetScale, 1f), speed);

            float idlePulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.35f);
            float attentionPulse = attention ? (.5f + .5f * Mathf.Sin(Time.unscaledTime * 4.4f)) : 0f;
            float glowAlpha = .10f + .05f * idlePulse + .16f * focus + .14f * attentionPulse;
            if (pressed) glowAlpha += .10f;
            if (glowGroup) glowGroup.alpha = Mathf.Clamp01(glowAlpha / .35f);

            Color coreGold = new Color(.92f, .70f, .36f, 1f);
            Color brightGold = new Color(1f, .82f, .47f, 1f);
            if (border) border.color = Color.Lerp(border.color, Color.Lerp(coreGold, brightGold, focus), speed);
            if (glyph) glyph.color = Color.Lerp(glyph.color,
                Color.Lerp(new Color(.98f, .80f, .43f, 1f), Color.white, focus), speed);
            if (face) face.color = Color.Lerp(face.color,
                Color.Lerp(new Color(.025f, .018f, .014f, .88f),
                    new Color(.055f, .035f, .015f, .94f), focus), speed);
            if (glow) glow.color = new Color(.96f, .70f, .34f, .13f + .08f * focus);
            if (connector) connector.alpha = Mathf.Lerp(connector.alpha, .94f + .06f * focus, speed);
        }

        private void OnDisable()
        {
            hovered = selected = pressed = false;
            if (root) root.localScale = Vector3.one;
        }
    }
}
