using System;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class HomeView
    {
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

            Hotspot(parent, "LampHotspot", 30, 450, 150, 270,
                new[]
                {
                    new Vector2(.18f, .02f), new Vector2(.83f, .02f), new Vector2(.95f, .14f),
                    new Vector2(.93f, .92f), new Vector2(.78f, .99f), new Vector2(.20f, .99f),
                    new Vector2(.06f, .90f), new Vector2(.05f, .14f)
                },
                () => act(() => { session.SetLamp(!session.State.lampOn); return true; },
                    session.State.lampOn ? "За окном кто-то есть?.." : "Комната снова наполнилась теплом."),
                .78f, .10f, .76f, 1.0f);

            Hotspot(parent, "LaptopHotspot", 985, 505, 225, 170,
                new[]
                {
                    new Vector2(.10f, .10f), new Vector2(.92f, .02f), new Vector2(.91f, .70f),
                    new Vector2(.78f, .84f), new Vector2(.12f, .92f), new Vector2(.02f, .80f)
                },
                () => open("laptop"),
                .95f, .16f, .98f, 1.1f);

            Hotspot(parent, "TeaHotspot", 795, 600, 86, 92,
                new[]
                {
                    new Vector2(.18f, .05f), new Vector2(.70f, .03f), new Vector2(.94f, .26f),
                    new Vector2(.92f, .68f), new Vector2(.72f, .94f), new Vector2(.24f, .94f),
                    new Vector2(.05f, .70f), new Vector2(.04f, .24f)
                },
                () => open("tea"),
                .82f, .13f, .86f, .9f);

            Hotspot(parent, "WorkbenchHotspot", 1160, 325, 300, 112,
                new[]
                {
                    new Vector2(.02f, .38f), new Vector2(.18f, .20f), new Vector2(.78f, .12f),
                    new Vector2(.98f, .31f), new Vector2(.91f, .64f), new Vector2(.22f, .72f),
                    new Vector2(.04f, .60f)
                },
                () => open("workbench"),
                .65f, .08f, .64f, .75f);

            Hotspot(parent, "MameHotspot", 150, 772, 218, 218,
                new[]
                {
                    new Vector2(.34f, .02f), new Vector2(.55f, .07f), new Vector2(.71f, .20f),
                    new Vector2(.80f, .42f), new Vector2(.78f, .72f), new Vector2(.65f, .91f),
                    new Vector2(.45f, .99f), new Vector2(.25f, .91f), new Vector2(.13f, .70f),
                    new Vector2(.12f, .42f), new Vector2(.20f, .18f)
                },
                () =>
                {
                    act(() => { session.PetMame(); return true; }, "");
                    mameReaction = 1;
                    audio.Play(assets.mame);
                    toast(session.State.mameInteractions % 3 == 0
                        ? "Мамэ внимательно смотрит в пустой угол."
                        : "Мамэ довольно щурится. Почти как обычный питомец.");
                },
                .92f, .18f, .96f, 1.2f);

            Hotspot(parent, "DoorHotspot", 1625, 100, 150, 505,
                new[]
                {
                    new Vector2(.13f, .01f), new Vector2(.88f, .04f), new Vector2(.82f, .96f),
                    new Vector2(.07f, .99f)
                },
                () =>
                {
                    if (session.State.phase == RunPhase.Accepted)
                        travel(session.LeaveHome, "Дождь. Последний переход.\nСвятилище между домами.");
                    else if (session.State.phase == RunPhase.Payment)
                        toast("На ноутбук пришло подтверждение оплаты.");
                    else { open("laptop"); toast("Сначала выберите контракт в YOMI."); }
                },
                .42f, .045f, .38f, .55f);

            Hotspot(parent, "WindowHotspot", 330, 105, 615, 455,
                new[]
                {
                    new Vector2(.02f, .02f), new Vector2(.98f, .02f), new Vector2(.96f, .96f),
                    new Vector2(.03f, .99f)
                },
                () => toast("Поезд проходит без остановки. На этот раз — настоящий."),
                .42f, .025f, .24f, .22f);

            Refresh();
        }

        private Button Hotspot(RectTransform parent, string name, float x, float y, float w, float h,
            Vector2[] shape, Action action, float strength = 1f, float idleIntensity = .10f,
            float hoverIntensity = .82f, float haloScale = 1f)
        {
            var rect = ui.Rect(parent, name, x, y, w, h);
            var mask = rect.gameObject.AddComponent<HomeObjectMask>();
            mask.SetShape(shape);
            mask.Strength = strength;
            mask.HaloScale = haloScale;
            mask.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = mask;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.onClick.AddListener(() =>
            {
                audio.Play(assets.click);
                action?.Invoke();
            });

            rect.gameObject.AddComponent<HomeObjectHighlight>()
                .Initialize(mask, button, idleIntensity, hoverIntensity);
            return button;
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
        }

        public void Tick(float dt)
        {
            time += dt;
            mameReaction = Mathf.Max(0, mameReaction - dt);
            if (mame)
            {
                mame.rectTransform.localScale = new Vector3(1, 1 + Mathf.Sin(time * 2) * .018f, 1);
                mame.rectTransform.anchoredPosition = new Vector2(150, -772 + Mathf.Sin(mameReaction * Mathf.PI * 4) * mameReaction * 12);
            }
        }

        public void ClearReferences() { mame = null; weaponWard = null; objective = null; prepared = null; }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomeObjectMask : MaskableGraphic, ICanvasRaycastFilter
    {
        private Vector2[] points = Array.Empty<Vector2>();
        private float intensity;
        private float strength = 1f;
        private float haloScale = 1f;

        private static readonly Color MistWhite = new Color(.97f, .96f, .91f, 1f);

        public float Intensity
        {
            get { return intensity; }
            set
            {
                float next = Mathf.Max(0, value);
                if (Mathf.Approximately(intensity, next)) return;
                intensity = next;
                SetVerticesDirty();
            }
        }

        public float Strength
        {
            get { return strength; }
            set
            {
                strength = Mathf.Clamp01(value);
                SetVerticesDirty();
            }
        }

        public float HaloScale
        {
            get { return haloScale; }
            set
            {
                haloScale = Mathf.Max(0, value);
                SetVerticesDirty();
            }
        }

        public void SetShape(Vector2[] value)
        {
            points = value ?? Array.Empty<Vector2>();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (points.Length < 3) return;

            Rect rect = GetPixelAdjustedRect();
            var verts = new Vector2[points.Length];
            Vector2 center = Vector2.zero;
            for (int i = 0; i < points.Length; i++)
            {
                verts[i] = new Vector2(rect.xMin + points[i].x * rect.width, rect.yMax - points[i].y * rect.height);
                center += verts[i];
            }
            center /= points.Length;

            float amount = Mathf.Clamp01(intensity) * strength;
            if (amount <= .0001f) return;

            AddInteriorMist(mesh, verts, center, amount);
            if (haloScale > .001f)
                AddSoftHalo(mesh, verts, center, 18f * haloScale, amount);
        }

        private static void AddInteriorMist(VertexHelper mesh, Vector2[] verts, Vector2 center, float amount)
        {
            Color centerColor = WithAlpha(MistWhite, .10f * amount);
            Color edgeColor = WithAlpha(MistWhite, .028f * amount);

            int centerIndex = mesh.currentVertCount;
            mesh.AddVert(center, centerColor, Vector2.zero);
            int firstEdge = mesh.currentVertCount;
            for (int i = 0; i < verts.Length; i++)
                mesh.AddVert(verts[i], edgeColor, Vector2.zero);

            for (int i = 0; i < verts.Length; i++)
            {
                int next = (i + 1) % verts.Length;
                mesh.AddTriangle(centerIndex, firstEdge + i, firstEdge + next);
            }
        }

        private static void AddSoftHalo(VertexHelper mesh, Vector2[] verts, Vector2 center, float expansion, float amount)
        {
            Color inner = WithAlpha(MistWhite, .11f * amount);
            Color outer = WithAlpha(MistWhite, 0f);
            int start = mesh.currentVertCount;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector2 radial = verts[i] - center;
                Vector2 expanded = radial.sqrMagnitude > .001f
                    ? verts[i] + radial.normalized * expansion
                    : verts[i];
                mesh.AddVert(verts[i], inner, Vector2.zero);
                mesh.AddVert(expanded, outer, Vector2.zero);
            }

            for (int i = 0; i < verts.Length; i++)
            {
                int next = (i + 1) % verts.Length;
                int innerA = start + i * 2;
                int outerA = innerA + 1;
                int innerB = start + next * 2;
                int outerB = innerB + 1;
                mesh.AddTriangle(innerA, outerA, outerB);
                mesh.AddTriangle(innerA, outerB, innerB);
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (points.Length < 3) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local))
                return false;

            Rect rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return false;
            var point = new Vector2((local.x - rect.xMin) / rect.width, (rect.yMax - local.y) / rect.height);
            return Contains(point);
        }

        private bool Contains(Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[j];
                bool crosses = (a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }
    }

    public sealed class HomeObjectHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private HomeObjectMask mask;
        private Button button;
        private bool hovered;
        private bool selected;
        private bool pressed;
        private float idleIntensity;
        private float hoverIntensity;

        public void Initialize(HomeObjectMask target, Button owner, float idle, float hover)
        {
            mask = target;
            button = owner;
            idleIntensity = Mathf.Max(0, idle);
            hoverIntensity = Mathf.Max(idleIntensity, hover);
            if (mask) mask.Intensity = idleIntensity;
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; pressed = false; }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) pressed = true;
        }
        public void OnPointerUp(PointerEventData e) { pressed = false; }

        private void Update()
        {
            if (!mask || !button) return;

            bool active = button.IsInteractable() && (hovered || selected);
            float target = active ? hoverIntensity : idleIntensity;
            if (active && pressed)
                target = hoverIntensity * 1.14f;
            else if (active)
                target *= .985f + Mathf.Sin(Time.unscaledTime * 2.35f) * .015f;

            float factor = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            mask.Intensity = Mathf.Lerp(mask.Intensity, target, factor);
        }

        private void OnDisable()
        {
            hovered = selected = pressed = false;
            if (mask) mask.Intensity = idleIntensity;
        }
    }
}
