using System;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class HomeView
    {
        private static readonly Color ActionGold = new Color(.86f, .67f, .34f, 1f);
        private static readonly Color ActionFace = new Color(.018f, .035f, .038f, .93f);
        private static readonly Color ActionPaper = new Color(.94f, .92f, .86f, 1f);
        private static readonly Color ActionCyan = new Color(.12f, .78f, .82f, 1f);

        private readonly UiKit ui;
        private readonly RokasAssets assets;
        private readonly GameSession session;
        private readonly RokasAudio audio;
        private readonly Action<string> open;
        private readonly Action<Func<bool>, string> act;
        private readonly Action<Func<bool>, string> travel;
        private readonly Action<string> toast;
        private RawImage mame;
        private Image weaponWard;
        private Text objective;
        private Text prepared;
        private CanvasGroup laptopUnreadIndicator;
        private float time;
        private float mameReaction;

        public HomeView(UiKit ui, RokasAssets assets, GameSession session, RokasAudio audio, Action<string> open,
            Action<Func<bool>, string> act, Action<Func<bool>, string> travel, Action<string> toast)
        {
            this.ui = ui; this.assets = assets; this.session = session; this.audio = audio;
            this.open = open; this.act = act; this.travel = travel; this.toast = toast;
        }

        public void Build(RectTransform parent)
        {
            ui.Label(parent, "HomeChapter", "МЕЖДУ ДВУМЯ МИРАМИ", 65, 138, 680, 37, 18, UiKit.Gold);
            ui.Label(parent, "HomeTitle", "С возвращением.", 61, 177, 820, 90, 52, UiKit.Paper, true);
            objective = ui.Label(parent, "HomeObjective", "", 65, 267, 820, 80, 23, UiKit.Paper);
            prepared = ui.Label(parent, "PreparationStatus", "", 1360, 137, 480, 60, 19, UiKit.Jade, false, TextAnchor.MiddleRight);
            mame = ui.Art(parent, "Mame", assets.familiar, 150, 772, 218, 218);
            weaponWard = ui.Box(parent, "WardedBlade", 1202, 344, 13, 71, UiKit.Paper);
            weaponWard.rectTransform.localRotation = Quaternion.Euler(0, 0, -12);
            ui.Box(weaponWard.transform, "WardInk", 5, 12, 3, 40, UiKit.Red);

            ActionButton(parent, "LampHotspot", "Свет", HomeActionGlyph.Lamp, 62, 620, 252,
                () => act(() => { session.SetLamp(!session.State.lampOn); return true; },
                    session.State.lampOn ? "За окном кто-то есть?.." : "Комната снова наполнилась теплом."));

            ActionButton(parent, "WindowHotspot", "За окном", HomeActionGlyph.Torii, 470, 410, 315,
                () => toast("Поезд проходит без остановки. На этот раз — настоящий."));

            ActionButton(parent, "WorkbenchHotspot", "Снаряжение", HomeActionGlyph.Equipment, 1110, 392, 356,
                () => open("workbench"));

            ActionButton(parent, "LaptopHotspot", "YOMI  /  Ноутбук", HomeActionGlyph.Laptop, 950, 638, 390,
                () => open("laptop"));
            RectTransform unread = ui.Rect(parent, "HomeLaptopUnreadIndicator", 1260, 628, 34, 34);
            laptopUnreadIndicator = unread.gameObject.AddComponent<CanvasGroup>();
            Surface(unread, "UnreadGlow", 0, 0, 34, 34, 17, new Color(.18f, .86f, .90f, .86f));
            Surface(unread, "UnreadCore", 10, 10, 14, 14, 7, new Color(.90f, .98f, 1f, .95f));
            UpdateLaptopUnreadIndicator();

            ActionButton(parent, "TeaHotspot", "Заварить чай", HomeActionGlyph.Tea, 638, 727, 318,
                () => open("tea"));

            ActionButton(parent, "MameHotspot", "Мамэ", HomeActionGlyph.Mame, 88, 895, 260,
                () =>
                {
                    act(() => { session.PetMame(); return true; }, "");
                    mameReaction = 1;
                    audio.Play(assets.mame);
                    toast(session.State.mameInteractions % 3 == 0
                        ? "Мамэ внимательно смотрит в пустой угол."
                        : "Мамэ довольно щурится. Почти как обычный питомец.");
                });

            ActionButton(parent, "DoorHotspot", "Выйти из дома", HomeActionGlyph.Exit, 1582, 648, 310,
                () =>
                {
                    if (session.State.phase == RunPhase.Accepted)
                        travel(session.LeaveHome, "Дождь. Последний переход.\nСвятилище между домами.");
                    else if (session.State.phase == RunPhase.Payment)
                        toast("На ноутбук пришло подтверждение оплаты.");
                    else { open("laptop"); toast("Сначала выберите контракт в YOMI."); }
                });

            Refresh();
        }

        private Button ActionButton(RectTransform parent, string name, string title, HomeActionGlyph glyph,
            float x, float y, float width, Action action)
        {
            const float rootHeight = 82f;
            const float pillY = 8f;
            const float pillHeight = 62f;
            const float iconSize = 82f;
            const float pillX = 43f;

            var root = ui.Rect(parent, name, x, y, width, rootHeight);

            Surface(root, "PillShadow", pillX + 5, pillY + 6, width - pillX, pillHeight,
                pillHeight * .5f, new Color(0, 0, 0, .48f));
            var border = Surface(root, "PillGold", pillX, pillY, width - pillX, pillHeight,
                pillHeight * .5f, ActionGold);
            var face = Surface(root, "PillFace", pillX + 2, pillY + 2, width - pillX - 4, pillHeight - 4,
                (pillHeight - 4) * .5f, ActionFace, true);

            Surface(root, "IconShadow", 4, 6, iconSize, iconSize, iconSize * .5f, new Color(0, 0, 0, .48f));
            var iconBorder = Surface(root, "IconGold", 0, 0, iconSize, iconSize, iconSize * .5f, ActionGold);
            var iconFace = Surface(root, "IconFace", 3, 3, iconSize - 6, iconSize - 6,
                (iconSize - 6) * .5f, new Color(.018f, .045f, .048f, .98f), true);
            ActionIcon(root, "ActionGlyph", glyph, 18, 18, 46, new Color(.91f, .73f, .40f, 1f));

            var label = ui.Label(root, "Title", title, 100, pillY, width - 152, pillHeight,
                22, ActionPaper, false, TextAnchor.MiddleLeft);
            ActionIcon(root, "Chevron", HomeActionGlyph.Chevron, width - 42, 27, 26, ActionGold);

            var accent = BuildYokaiAccent(root, 25, 68, 118, 14);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.onClick.AddListener(() =>
            {
                audio.Play(assets.click);
                action?.Invoke();
            });

            root.gameObject.AddComponent<HomeActionFeedback>()
                .Initialize(root, button, face, border, iconFace, iconBorder, label, accent);
            return button;
        }

        private CanvasGroup BuildYokaiAccent(Transform parent, float x, float y, float w, float h)
        {
            var root = ui.Rect(parent, "YokaiAccent", x, y, w, h);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = .42f;
            var a = ui.Box(root, "MistA", 0, 4, w * .80f, 2, new Color(ActionCyan.r, ActionCyan.g, ActionCyan.b, .42f));
            var b = ui.Box(root, "MistB", 13, 8, w * .64f, 2, new Color(ActionCyan.r, ActionCyan.g, ActionCyan.b, .26f));
            var c = ui.Box(root, "MistC", 38, 1, w * .44f, 1, new Color(.54f, .94f, .94f, .24f));
            a.rectTransform.localRotation = Quaternion.Euler(0, 0, 1.2f);
            b.rectTransform.localRotation = Quaternion.Euler(0, 0, -1.8f);
            c.rectTransform.localRotation = Quaternion.Euler(0, 0, .6f);
            ui.Box(root, "SparkA", 8, 1, 3, 3, new Color(.42f, .95f, .95f, .38f));
            ui.Box(root, "SparkB", 91, 7, 2, 2, new Color(.42f, .95f, .95f, .30f));
            return group;
        }

        private LaptopSurface Surface(Transform parent, string name, float x, float y, float w, float h,
            float radius, Color color, bool blocks = false)
        {
            var go = ui.Rect(parent, name, x, y, w, h).gameObject;
            if (!go.TryGetComponent<CanvasRenderer>(out _)) go.AddComponent<CanvasRenderer>();
            var surface = go.AddComponent<LaptopSurface>();
            surface.Radius = radius;
            surface.color = color;
            surface.raycastTarget = blocks;
            return surface;
        }

        private void ActionIcon(Transform parent, string name, HomeActionGlyph glyph,
            float x, float y, float size, Color color)
        {
            var go = ui.Rect(parent, name, x, y, size, size).gameObject;
            if (!go.TryGetComponent<CanvasRenderer>(out _)) go.AddComponent<CanvasRenderer>();
            var icon = go.AddComponent<HomeActionIcon>();
            icon.Glyph = glyph;
            icon.color = color;
            icon.raycastTarget = false;
        }

        public void Refresh()
        {
            if (!objective) return;
            objective.text = session.State.phase == RunPhase.Accepted
                ? "Контракт принят. Чашка чая — и можно выходить."
                : session.State.phase == RunPhase.Payment
                    ? "Вы снова дома. В YOMI вас ждёт оплата контракта."
                    : session.State.completedRuns > 0
                        ? "За окном всё ещё дождь. Может, ещё один контракт?"
                        : "Ночной город спит. В YOMI появился первый заказ.";
            prepared.text = string.IsNullOrEmpty(session.State.preparedFoodId) ? "" : "ЗЕЛЁНЫЙ ЧАЙ  /  +5% АВТОАТАКА";
            if (weaponWard) weaponWard.gameObject.SetActive(session.State.weaponLevel >= 2);
            UpdateLaptopUnreadIndicator();
        }

        private void UpdateLaptopUnreadIndicator()
        {
            if (laptopUnreadIndicator == null) return;
            if (session.Messages.TotalUnread <= 0)
            {
                laptopUnreadIndicator.alpha = 0f;
                return;
            }
            laptopUnreadIndicator.alpha = .68f + .22f * (.5f + .5f * Mathf.Sin(time * 3.2f));
        }

        public void Tick(float dt)
        {
            time += dt;
            mameReaction = Mathf.Max(0, mameReaction - dt);
            UpdateLaptopUnreadIndicator();
            if (mame)
            {
                mame.rectTransform.localScale = new Vector3(1, 1 + Mathf.Sin(time * 2) * .018f, 1);
                mame.rectTransform.anchoredPosition = new Vector2(150, -772 + Mathf.Sin(mameReaction * Mathf.PI * 4) * mameReaction * 12);
            }
        }

        public void ClearReferences()
        {
            mame = null; weaponWard = null; objective = null; prepared = null; laptopUnreadIndicator = null;
        }
    }

    public enum HomeActionGlyph
    {
        Lamp, Torii, Equipment, Laptop, Tea, Mame, Exit, Chevron
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomeActionIcon : MaskableGraphic
    {
        [SerializeField] private HomeActionGlyph glyph;
        private Rect drawingRect;
        private float drawingScale;

        public HomeActionGlyph Glyph
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
            drawingScale = Mathf.Min(drawingRect.width, drawingRect.height) / 100f;
            if (drawingScale <= 0) return;

            switch (glyph)
            {
                case HomeActionGlyph.Lamp: DrawLamp(mesh); break;
                case HomeActionGlyph.Torii: DrawTorii(mesh); break;
                case HomeActionGlyph.Equipment: DrawEquipment(mesh); break;
                case HomeActionGlyph.Laptop: DrawLaptop(mesh); break;
                case HomeActionGlyph.Tea: DrawTea(mesh); break;
                case HomeActionGlyph.Mame: DrawMame(mesh); break;
                case HomeActionGlyph.Exit: DrawExit(mesh); break;
                case HomeActionGlyph.Chevron:
                    Line(mesh, 38, 28, 64, 50, 7);
                    Line(mesh, 64, 50, 38, 72, 7);
                    break;
            }
        }

        private void DrawLamp(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(30, 22), new Vector2(70, 22), new Vector2(80, 49), new Vector2(20, 49) }, 5, true);
            Line(mesh, 50, 49, 50, 78, 6);
            Line(mesh, 38, 80, 62, 80, 6);
            Line(mesh, 43, 68, 57, 68, 4);
        }

        private void DrawTorii(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(15, 25), new Vector2(31, 30), new Vector2(50, 32), new Vector2(69, 30), new Vector2(85, 25) }, 7);
            Line(mesh, 24, 43, 76, 43, 6);
            Line(mesh, 34, 33, 30, 79, 7);
            Line(mesh, 66, 33, 70, 79, 7);
            Line(mesh, 50, 33, 50, 43, 5);
            Line(mesh, 23, 80, 37, 80, 5);
            Line(mesh, 63, 80, 77, 80, 5);
        }

        private void DrawEquipment(VertexHelper mesh)
        {
            Line(mesh, 24, 20, 72, 74, 6);
            Line(mesh, 76, 20, 28, 74, 6);
            Line(mesh, 20, 24, 33, 37, 5);
            Line(mesh, 80, 24, 67, 37, 5);
            Line(mesh, 25, 72, 18, 80, 6);
            Line(mesh, 75, 72, 82, 80, 6);
        }

        private void DrawLaptop(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(22, 24), new Vector2(78, 24), new Vector2(78, 66), new Vector2(22, 66) }, 5, true);
            Line(mesh, 16, 77, 84, 77, 6);
            Line(mesh, 29, 68, 71, 68, 4);
        }

        private void DrawTea(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(28, 42), new Vector2(67, 42), new Vector2(63, 72), new Vector2(34, 72) }, 5, true);
            Ellipse(mesh, 69, 55, 10, 11, 5, 22);
            Line(mesh, 27, 79, 69, 79, 5);
            Stroke(mesh, new[] { new Vector2(38, 35), new Vector2(35, 29), new Vector2(39, 23) }, 4);
            Stroke(mesh, new[] { new Vector2(52, 35), new Vector2(49, 29), new Vector2(53, 23) }, 4);
        }

        private void DrawMame(VertexHelper mesh)
        {
            Ellipse(mesh, 49, 43, 18, 16, 5, 24);
            Polygon(mesh, new[] { new Vector2(34, 33), new Vector2(38, 17), new Vector2(47, 31) });
            Polygon(mesh, new[] { new Vector2(52, 31), new Vector2(62, 17), new Vector2(65, 34) });
            Ellipse(mesh, 49, 68, 17, 23, 5, 26);
            Stroke(mesh, new[] { new Vector2(64, 73), new Vector2(77, 67), new Vector2(81, 54), new Vector2(75, 47) }, 5);
        }

        private void DrawExit(VertexHelper mesh)
        {
            Stroke(mesh, new[] { new Vector2(20, 20), new Vector2(58, 20), new Vector2(58, 80), new Vector2(20, 80) }, 5, true);
            Line(mesh, 43, 50, 84, 50, 6);
            Line(mesh, 70, 36, 84, 50, 6);
            Line(mesh, 84, 50, 70, 64, 6);
            Disc(mesh, new Vector2(49, 54), 3.5f);
        }

        private void AddVertex(VertexHelper mesh, Vector2 point)
        {
            var center = drawingRect.center;
            mesh.AddVert(new Vector3(center.x + (point.x - 50) * drawingScale,
                center.y + (50 - point.y) * drawingScale, 0), color, Vector2.zero);
        }

        private void Line(VertexHelper mesh, float x1, float y1, float x2, float y2, float thickness)
        {
            var a = new Vector2(x1, y1);
            var b = new Vector2(x2, y2);
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * (thickness * .5f);
            int first = mesh.currentVertCount;
            AddVertex(mesh, a + normal);
            AddVertex(mesh, a - normal);
            AddVertex(mesh, b + normal);
            AddVertex(mesh, b - normal);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
        }

        private void Stroke(VertexHelper mesh, Vector2[] points, float thickness, bool closed = false)
        {
            int count = points.Length;
            if (count < 2) return;
            for (int i = 0; i < count - 1; i++)
                Line(mesh, points[i].x, points[i].y, points[i + 1].x, points[i + 1].y, thickness);
            if (closed) Line(mesh, points[count - 1].x, points[count - 1].y, points[0].x, points[0].y, thickness);
        }

        private void Polygon(VertexHelper mesh, Vector2[] points)
        {
            int first = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++) AddVertex(mesh, points[i]);
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(first, first + i, first + i + 1);
        }

        private void Disc(VertexHelper mesh, Vector2 center, float radius)
        {
            const int segments = 18;
            int first = mesh.currentVertCount;
            AddVertex(mesh, center);
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                AddVertex(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            for (int i = 0; i < segments; i++)
                mesh.AddTriangle(first, first + i + 1, first + (i + 1) % segments + 1);
        }

        private void Ellipse(VertexHelper mesh, float x, float y, float rx, float ry, float thickness, int segments)
        {
            float half = thickness * .5f;
            int first = mesh.currentVertCount;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var p = new Vector2(x + radial.x * rx, y + radial.y * ry);
                var normal = new Vector2(radial.x / Mathf.Max(.001f, rx), radial.y / Mathf.Max(.001f, ry)).normalized * half;
                AddVertex(mesh, p + normal);
                AddVertex(mesh, p - normal);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = first + i * 2;
                int b = first + ((i + 1) % segments) * 2;
                mesh.AddTriangle(a, a + 1, b);
                mesh.AddTriangle(b, a + 1, b + 1);
            }
        }
    }

    public sealed class HomeActionFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform rect;
        private Button button;
        private LaptopSurface face;
        private LaptopSurface border;
        private LaptopSurface iconFace;
        private LaptopSurface iconBorder;
        private Text title;
        private CanvasGroup accent;
        private Vector2 origin;
        private bool hovered;
        private bool selected;
        private bool pressed;

        public void Initialize(RectTransform root, Button owner, LaptopSurface pillFace, LaptopSurface pillBorder,
            LaptopSurface circleFace, LaptopSurface circleBorder, Text label, CanvasGroup yokaiAccent)
        {
            rect = root; button = owner; face = pillFace; border = pillBorder;
            iconFace = circleFace; iconBorder = circleBorder; title = label; accent = yokaiAccent;
            origin = root.anchoredPosition;
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }

        private void Update()
        {
            if (!rect || !button) return;
            bool active = button.IsInteractable() && (hovered || selected);
            float speed = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            float press = pressed ? 1f : 0f;
            float focus = active ? 1f : 0f;

            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition,
                origin + new Vector2(active ? 3f : 0f, pressed ? 1f : 0f), speed);

            Color gold = Color.Lerp(new Color(.86f, .67f, .34f, 1f), new Color(1f, .82f, .45f, 1f), focus);
            if (pressed) gold = new Color(1f, .90f, .58f, 1f);
            border.color = Color.Lerp(border.color, gold, speed);
            iconBorder.color = Color.Lerp(iconBorder.color, gold, speed);

            Color faceTarget = Color.Lerp(new Color(.018f, .035f, .038f, .93f),
                new Color(.025f, .075f, .075f, .97f), focus);
            if (pressed) faceTarget = new Color(.035f, .095f, .09f, .99f);
            face.color = Color.Lerp(face.color, faceTarget, speed);
            iconFace.color = Color.Lerp(iconFace.color,
                Color.Lerp(new Color(.018f, .045f, .048f, .98f), new Color(.025f, .09f, .088f, 1f), focus), speed);

            if (title)
                title.color = Color.Lerp(title.color,
                    Color.Lerp(new Color(.94f, .92f, .86f, 1f), Color.white, focus), speed);
            if (accent)
                accent.alpha = Mathf.Lerp(accent.alpha, active ? (pressed ? 1f : .92f) : .42f, speed);
        }

        private void OnDisable()
        {
            hovered = selected = pressed = false;
            if (rect) rect.anchoredPosition = origin;
        }
    }
}
