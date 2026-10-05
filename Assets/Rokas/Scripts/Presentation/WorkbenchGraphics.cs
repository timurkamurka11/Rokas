using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum WorkbenchWeaponKind
    {
        TwoHanded,
        Dagger
    }

    public enum WorkbenchTierState
    {
        Current,
        Available,
        Locked
    }

    [AddComponentMenu("ROKAS/UI/Workbench Panel")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkbenchPanelGraphic : MaskableGraphic
    {
        [SerializeField] private Color fillColor = new Color(.025f, .07f, .11f, .94f);
        [SerializeField] private Color borderColor = new Color(.28f, .72f, .93f, .92f);
        [SerializeField] private Color innerBorderColor = new Color(.18f, .48f, .65f, .34f);
        [SerializeField] private float cornerCut = 14f;
        [SerializeField] private float borderWidth = 2f;
        [SerializeField] private float innerInset = 5f;

        public Color FillColor
        {
            get => fillColor;
            set { fillColor = value; SetVerticesDirty(); }
        }

        public Color BorderColor
        {
            get => borderColor;
            set { borderColor = value; SetVerticesDirty(); }
        }

        public Color InnerBorderColor
        {
            get => innerBorderColor;
            set { innerBorderColor = value; SetVerticesDirty(); }
        }

        public float CornerCut
        {
            get => cornerCut;
            set { cornerCut = Mathf.Max(0, value); SetVerticesDirty(); }
        }

        public float BorderWidth
        {
            get => borderWidth;
            set { borderWidth = Mathf.Max(0, value); SetVerticesDirty(); }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;

            float cut = Mathf.Min(cornerCut, Mathf.Min(rect.width, rect.height) * .25f);
            Vector2[] points =
            {
                new Vector2(rect.xMin + cut, rect.yMax),
                new Vector2(rect.xMax - cut, rect.yMax),
                new Vector2(rect.xMax, rect.yMax - cut),
                new Vector2(rect.xMax, rect.yMin + cut),
                new Vector2(rect.xMax - cut, rect.yMin),
                new Vector2(rect.xMin + cut, rect.yMin),
                new Vector2(rect.xMin, rect.yMin + cut),
                new Vector2(rect.xMin, rect.yMax - cut)
            };

            if (fillColor.a > 0)
            {
                int center = mesh.currentVertCount;
                mesh.AddVert(rect.center, fillColor, Vector2.zero);
                for (int i = 0; i < points.Length; i++)
                    mesh.AddVert(points[i], fillColor, Vector2.zero);
                for (int i = 0; i < points.Length; i++)
                    mesh.AddTriangle(center, center + i + 1, center + ((i + 1) % points.Length) + 1);
            }

            if (borderWidth > 0 && borderColor.a > 0)
            {
                for (int i = 0; i < points.Length; i++)
                    AddLine(mesh, points[i], points[(i + 1) % points.Length], borderWidth, borderColor);
            }

            if (innerBorderColor.a > 0 && rect.width > innerInset * 2f && rect.height > innerInset * 2f)
            {
                Rect inner = new Rect(rect.xMin + innerInset, rect.yMin + innerInset,
                    rect.width - innerInset * 2f, rect.height - innerInset * 2f);
                float innerCut = Mathf.Max(2f, cut - innerInset * .55f);
                Vector2[] innerPoints =
                {
                    new Vector2(inner.xMin + innerCut, inner.yMax),
                    new Vector2(inner.xMax - innerCut, inner.yMax),
                    new Vector2(inner.xMax, inner.yMax - innerCut),
                    new Vector2(inner.xMax, inner.yMin + innerCut),
                    new Vector2(inner.xMax - innerCut, inner.yMin),
                    new Vector2(inner.xMin + innerCut, inner.yMin),
                    new Vector2(inner.xMin, inner.yMin + innerCut),
                    new Vector2(inner.xMin, inner.yMax - innerCut)
                };
                for (int i = 0; i < innerPoints.Length; i++)
                    AddLine(mesh, innerPoints[i], innerPoints[(i + 1) % innerPoints.Length], 1f, innerBorderColor);
            }

            Color accent = new Color(borderColor.r, borderColor.g, borderColor.b,
                Mathf.Min(1f, borderColor.a * 1.15f));
            float accentLength = Mathf.Min(44f, rect.width * .16f);
            AddLine(mesh, new Vector2(rect.xMin + cut + 5f, rect.yMax - 1f),
                new Vector2(rect.xMin + cut + 5f + accentLength, rect.yMax - 1f), 2.2f, accent);
            AddLine(mesh, new Vector2(rect.xMax - cut - 5f - accentLength, rect.yMin + 1f),
                new Vector2(rect.xMax - cut - 5f, rect.yMin + 1f), 1.8f,
                new Color(accent.r, accent.g, accent.b, accent.a * .7f));

            if (rect.width >= 120f && rect.height >= 60f)
            {
                Color corner = new Color(accent.r, accent.g, accent.b, accent.a * .55f);
                float arm = Mathf.Min(26f, Mathf.Min(rect.width, rect.height) * .16f);
                float inset = 9f;
                AddLine(mesh, new Vector2(rect.xMin + inset, rect.yMax - cut - 2f),
                    new Vector2(rect.xMin + inset, rect.yMax - cut - arm), 1.4f, corner);
                AddLine(mesh, new Vector2(rect.xMin + cut + 2f, rect.yMax - inset),
                    new Vector2(rect.xMin + cut + arm, rect.yMax - inset), 1.4f, corner);
                AddLine(mesh, new Vector2(rect.xMax - inset, rect.yMin + cut + 2f),
                    new Vector2(rect.xMax - inset, rect.yMin + cut + arm), 1.2f, corner);
                AddLine(mesh, new Vector2(rect.xMax - cut - 2f, rect.yMin + inset),
                    new Vector2(rect.xMax - cut - arm, rect.yMin + inset), 1.2f, corner);
            }
        }

        private static void AddLine(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * .5f);
            int first = mesh.currentVertCount;
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
        }
    }

    [AddComponentMenu("ROKAS/UI/Workbench HUD")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkbenchHudGraphic : MaskableGraphic
    {
        [SerializeField] private float rotationSpeed = 4.5f;

        public float RotationSpeed
        {
            get => rotationSpeed;
            set => rotationSpeed = value;
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            rectTransform.localEulerAngles = new Vector3(0, 0, Time.unscaledTime * rotationSpeed);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            if (radius <= 1) return;

            Vector2 center = rect.center;
            Color strong = color;
            Color soft = new Color(color.r, color.g, color.b, color.a * .42f);
            AddArc(mesh, center, radius * .82f, 12, 116, 2.2f, strong, 28);
            AddArc(mesh, center, radius * .82f, 150, 262, 2.2f, strong, 30);
            AddArc(mesh, center, radius * .64f, 34, 205, 1.5f, soft, 34);
            AddArc(mesh, center, radius * .46f, 222, 346, 1.8f, soft, 24);
            AddArc(mesh, center, radius * .92f, 278, 342, 1.1f,
                new Color(color.r, color.g, color.b, color.a * .28f), 18);
            AddArc(mesh, center, radius * .91f, 14, 66, 3.0f,
                new Color(color.r, color.g, color.b, color.a * .55f), 14);
            AddArc(mesh, center, radius * .74f, 268, 330, 2.4f,
                new Color(color.r, color.g, color.b, color.a * .58f), 14);
            AddCircle(mesh, center, radius * .30f, 1.2f, new Color(color.r, color.g, color.b, color.a * .24f), 44);
            AddCircle(mesh, center, radius * .56f, .9f, new Color(color.r, color.g, color.b, color.a * .16f), 52);
            AddLine(mesh, center + Vector2.left * radius * .92f, center + Vector2.right * radius * .92f, 1f, soft);
            AddLine(mesh, center + Vector2.down * radius * .92f, center + Vector2.up * radius * .92f, 1f, soft);

            for (int i = 0; i < 24; i++)
            {
                float angle = i * 15f * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                bool major = i % 2 == 0;
                AddLine(mesh,
                    center + direction * radius * (major ? .69f : .73f),
                    center + direction * radius * .80f,
                    major ? 1.55f : .85f,
                    major ? strong : soft);
            }

            AddLine(mesh, center + new Vector2(-radius * .09f, 0), center + new Vector2(radius * .09f, 0), 1.4f, strong);
            AddLine(mesh, center + new Vector2(0, -radius * .09f), center + new Vector2(0, radius * .09f), 1.4f, strong);
        }

        private static void AddCircle(VertexHelper mesh, Vector2 center, float radius, float width, Color color, int segments)
        {
            AddArc(mesh, center, radius, 0, 360, width, color, segments);
        }

        private static void AddArc(VertexHelper mesh, Vector2 center, float radius, float startDeg, float endDeg,
            float width, Color color, int segments)
        {
            float half = width * .5f;
            int first = mesh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + radial * (radius + half), color, Vector2.zero);
                mesh.AddVert(center + radial * (radius - half), color, Vector2.zero);
                if (i == segments) continue;
                int a = first + i * 2;
                mesh.AddTriangle(a, a + 1, a + 2);
                mesh.AddTriangle(a + 2, a + 1, a + 3);
            }
        }

        private static void AddLine(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * .5f);
            int first = mesh.currentVertCount;
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
        }
    }

    [AddComponentMenu("ROKAS/UI/Workbench Arrow")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkbenchArrowGraphic : MaskableGraphic
    {
        [SerializeField] private int direction = 1;

        public int Direction
        {
            get => direction;
            set
            {
                direction = value < 0 ? -1 : 1;
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            float sign = direction < 0 ? -1f : 1f;
            Vector2 center = rect.center;
            float h = rect.height * .31f;
            float w = rect.width * .27f;
            Color bright = color;
            Color soft = new Color(color.r, color.g, color.b, color.a * .38f);
            Vector2 tip = center + new Vector2(sign * w, 0);
            Vector2 top = center + new Vector2(-sign * w * .65f, h);
            Vector2 bottom = center + new Vector2(-sign * w * .65f, -h);
            AddLine(mesh, top, tip, 4.2f, bright);
            AddLine(mesh, tip, bottom, 4.2f, bright);
            AddLine(mesh, top + new Vector2(-sign * 10f, 0), tip + new Vector2(-sign * 10f, 0), 1.6f, soft);
            AddLine(mesh, tip + new Vector2(-sign * 10f, 0), bottom + new Vector2(-sign * 10f, 0), 1.6f, soft);
            AddLine(mesh, center + new Vector2(-sign * 19f, -h * .66f),
                center + new Vector2(-sign * 19f, h * .66f), 1.2f, soft);
        }

        private static void AddLine(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * .5f);
            int first = mesh.currentVertCount;
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
        }
    }

    public sealed class WorkbenchTechButtonFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        private Button button;
        private RectTransform rect;
        private WorkbenchPanelGraphic face;
        private Outline glow;
        private Vector3 baseScale;
        private bool hovered;
        private bool pressed;
        private float visual;
        private float restGlowAlpha;

        public float RestGlowAlpha
        {
            get => restGlowAlpha;
            set => restGlowAlpha = Mathf.Clamp01(value);
        }

        public void Initialize(Button button, WorkbenchPanelGraphic face, Outline glow)
        {
            this.button = button;
            this.face = face;
            this.glow = glow;
            rect = button ? button.transform as RectTransform : null;
            baseScale = rect ? rect.localScale : Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData) { if (button && button.IsInteractable()) pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }
        public void OnSelect(BaseEventData eventData) { hovered = true; }
        public void OnDeselect(BaseEventData eventData) { hovered = false; pressed = false; }

        private void Update()
        {
            if (!button || !rect || !face) return;
            bool enabled = button.IsInteractable();
            float target = enabled ? (pressed ? .55f : hovered ? 1f : 0f) : -.5f;
            visual = Mathf.Lerp(visual, target, Time.unscaledDeltaTime * 16f);
            float lift = Mathf.Max(0, visual);
            float scale = pressed ? .975f : 1f + lift * .018f;
            rect.localScale = Vector3.Lerp(rect.localScale, baseScale * scale, Time.unscaledDeltaTime * 18f);

            Color border = face.BorderColor;
            float baseAlpha = enabled ? .62f : .22f;
            border.a = Mathf.Clamp01(baseAlpha + lift * .34f);
            face.BorderColor = border;
            if (glow)
            {
                Color c = glow.effectColor;
                c.a = enabled ? Mathf.Max(restGlowAlpha, Mathf.Lerp(.02f, .38f, lift)) : 0f;
                glow.effectColor = c;
            }
        }
    }

    [AddComponentMenu("ROKAS/UI/Workbench Weapon")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkbenchWeaponGraphic : MaskableGraphic
    {
        [SerializeField] private WorkbenchWeaponKind kind;

        public WorkbenchWeaponKind Kind
        {
            get => kind;
            set
            {
                if (kind == value) return;
                kind = value;
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            if (kind == WorkbenchWeaponKind.Dagger) DrawDagger(mesh, rect);
            else DrawTwoHanded(mesh, rect);
        }

        private void DrawTwoHanded(VertexHelper mesh, Rect rect)
        {
            Color edge = new Color(.82f, .95f, 1f, color.a);
            Color blade = new Color(.54f, .72f, .82f, color.a);
            Color core = new Color(.93f, .98f, 1f, color.a);
            Color metal = new Color(.29f, .48f, .61f, color.a);
            Color grip = new Color(.15f, .11f, .09f, color.a);
            Color glow = new Color(.25f, .83f, 1f, color.a * .95f);

            Polygon(mesh, rect, blade, new Vector2(.50f, .02f), new Vector2(.61f, .58f),
                new Vector2(.54f, .68f), new Vector2(.46f, .68f), new Vector2(.39f, .58f));
            Polygon(mesh, rect, core, new Vector2(.50f, .04f), new Vector2(.54f, .57f),
                new Vector2(.50f, .64f), new Vector2(.46f, .57f));
            Line(mesh, rect, new Vector2(.50f, .06f), new Vector2(.50f, .60f), .010f, edge);
            Polygon(mesh, rect, metal, new Vector2(.26f, .64f), new Vector2(.46f, .60f),
                new Vector2(.50f, .67f), new Vector2(.54f, .60f), new Vector2(.74f, .64f),
                new Vector2(.58f, .71f), new Vector2(.42f, .71f));
            Polygon(mesh, rect, grip, new Vector2(.45f, .69f), new Vector2(.55f, .69f),
                new Vector2(.57f, .93f), new Vector2(.43f, .93f));
            for (int i = 0; i < 5; i++)
            {
                float y = .73f + i * .04f;
                Line(mesh, rect, new Vector2(.44f, y), new Vector2(.56f, y + .015f), .008f, metal);
            }
            Polygon(mesh, rect, metal, new Vector2(.42f, .92f), new Vector2(.58f, .92f),
                new Vector2(.55f, .98f), new Vector2(.45f, .98f));
            Line(mesh, rect, new Vector2(.35f, .63f), new Vector2(.65f, .63f), .010f, glow);
        }

        private void DrawDagger(VertexHelper mesh, Rect rect)
        {
            Color edge = new Color(.86f, .96f, 1f, color.a);
            Color blade = new Color(.60f, .76f, .85f, color.a);
            Color core = new Color(.95f, .99f, 1f, color.a);
            Color metal = new Color(.34f, .50f, .60f, color.a);
            Color grip = new Color(.14f, .10f, .08f, color.a);
            Color glow = new Color(.24f, .82f, 1f, color.a * .95f);

            Polygon(mesh, rect, blade, new Vector2(.50f, .12f), new Vector2(.67f, .57f),
                new Vector2(.55f, .67f), new Vector2(.45f, .67f), new Vector2(.33f, .57f));
            Polygon(mesh, rect, core, new Vector2(.50f, .15f), new Vector2(.56f, .56f),
                new Vector2(.50f, .63f), new Vector2(.44f, .56f));
            Line(mesh, rect, new Vector2(.50f, .18f), new Vector2(.50f, .58f), .012f, edge);
            Polygon(mesh, rect, metal, new Vector2(.27f, .62f), new Vector2(.45f, .58f),
                new Vector2(.50f, .66f), new Vector2(.55f, .58f), new Vector2(.73f, .62f),
                new Vector2(.57f, .69f), new Vector2(.43f, .69f));
            Polygon(mesh, rect, grip, new Vector2(.44f, .68f), new Vector2(.56f, .68f),
                new Vector2(.58f, .91f), new Vector2(.42f, .91f));
            for (int i = 0; i < 4; i++)
            {
                float y = .73f + i * .045f;
                Line(mesh, rect, new Vector2(.43f, y), new Vector2(.57f, y + .016f), .009f, metal);
            }
            Polygon(mesh, rect, metal, new Vector2(.40f, .90f), new Vector2(.60f, .90f),
                new Vector2(.55f, .97f), new Vector2(.45f, .97f));
            Line(mesh, rect, new Vector2(.34f, .61f), new Vector2(.66f, .61f), .011f, glow);
        }

        private static void Polygon(VertexHelper mesh, Rect rect, Color color, params Vector2[] points)
        {
            int first = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++)
                mesh.AddVert(Map(rect, points[i]), color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++)
                mesh.AddTriangle(first, first + i, first + i + 1);
        }

        private static void Line(VertexHelper mesh, Rect rect, Vector2 a, Vector2 b, float normalizedWidth, Color color)
        {
            Vector2 pa = Map(rect, a);
            Vector2 pb = Map(rect, b);
            Vector2 direction = (pb - pa).normalized;
            float width = normalizedWidth * Mathf.Min(rect.width, rect.height);
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * .5f);
            int first = mesh.currentVertCount;
            mesh.AddVert(pa + normal, color, Vector2.zero);
            mesh.AddVert(pa - normal, color, Vector2.zero);
            mesh.AddVert(pb + normal, color, Vector2.zero);
            mesh.AddVert(pb - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
        }

        private static Vector2 Map(Rect rect, Vector2 normalized)
        {
            return new Vector2(rect.xMin + normalized.x * rect.width, rect.yMax - normalized.y * rect.height);
        }
    }
}
