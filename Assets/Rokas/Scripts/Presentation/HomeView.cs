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
                new Color(1f, .95f, .82f, 1f), 30f, 17f, .30f, .82f, false);

            Hotspot(parent, "LaptopHotspot", 985, 505, 225, 170,
                new[]
                {
                    new Vector2(.10f, .10f), new Vector2(.92f, .02f), new Vector2(.91f, .70f),
                    new Vector2(.78f, .84f), new Vector2(.12f, .92f), new Vector2(.02f, .80f)
                },
                () => open("laptop"),
                new Color(1f, .985f, .95f, 1f), 42f, 18f, .40f, 1f, false);

            Hotspot(parent, "TeaHotspot", 795, 600, 86, 92,
                new[]
                {
                    new Vector2(.18f, .05f), new Vector2(.70f, .03f), new Vector2(.94f, .26f),
                    new Vector2(.92f, .68f), new Vector2(.72f, .94f), new Vector2(.24f, .94f),
                    new Vector2(.05f, .70f), new Vector2(.04f, .24f)
                },
                () => open("tea"),
                new Color(1f, .97f, .86f, 1f), 22f, 11f, .32f, .86f, false);

            Hotspot(parent, "WorkbenchHotspot", 1160, 325, 300, 112,
                new[]
                {
                    new Vector2(.02f, .38f), new Vector2(.18f, .20f), new Vector2(.78f, .12f),
                    new Vector2(.98f, .31f), new Vector2(.91f, .64f), new Vector2(.22f, .72f),
                    new Vector2(.04f, .60f)
                },
                () => open("workbench"),
                new Color(.99f, .97f, .91f, 1f), 30f, 14f, .23f, .68f, false);

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
                new Color(.93f, .98f, 1f, 1f), 38f, 17f, .42f, 1f, false);

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
                new Color(1f, .97f, .91f, 1f), 24f, 15f, .14f, .46f, false);

            Hotspot(parent, "WindowHotspot", 330, 105, 615, 455,
                new[]
                {
                    new Vector2(.02f, .02f), new Vector2(.98f, .02f), new Vector2(.96f, .96f),
                    new Vector2(.03f, .99f)
                },
                () => toast("Поезд проходит без остановки. На этот раз — настоящий."),
                new Color(.91f, .96f, 1f, 1f), 0f, 0f, .18f, .52f, true);

            Refresh();
        }

        private Button Hotspot(RectTransform parent, string name, float x, float y, float w, float h,
            Vector2[] shape, Action action, Color tint, float padding, float blurRadius,
            float idleIntensity, float hoverIntensity, bool glassMist)
        {
            var rect = ui.Rect(parent, name, x, y, w, h);

            var hitMask = rect.gameObject.AddComponent<HomeObjectMask>();
            hitMask.SetShape(shape);
            hitMask.raycastTarget = true;

            var glowRect = ui.Rect(rect, name + "Glow", -padding, -padding, w + padding * 2f, h + padding * 2f);
            var glow = glowRect.gameObject.AddComponent<HomeSoftGlowGraphic>();
            glow.raycastTarget = false;
            glow.Initialize(shape, new Vector2(w, h), padding, blurRadius, tint, glassMist);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hitMask;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.onClick.AddListener(() =>
            {
                audio.Play(assets.click);
                action?.Invoke();
            });

            rect.gameObject.AddComponent<HomeObjectHighlight>()
                .Initialize(glow, button, idleIntensity, hoverIntensity);
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

        public void SetShape(Vector2[] value)
        {
            points = value ?? Array.Empty<Vector2>();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Color32 transparent = new Color32(255, 255, 255, 0);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), transparent, new Vector2(0, 0));
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), transparent, new Vector2(0, 1));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), transparent, new Vector2(1, 1));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), transparent, new Vector2(1, 0));
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 3);
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

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomeSoftGlowGraphic : MaskableGraphic
    {
        private Texture2D softMask;
        private Color tint = Color.white;
        private float intensity;

        public override Texture mainTexture => softMask ? softMask : s_WhiteTexture;

        public float Intensity
        {
            get { return intensity; }
            set
            {
                float next = Mathf.Clamp01(value);
                if (Mathf.Approximately(intensity, next)) return;
                intensity = next;
                ApplyTint();
            }
        }

        public void Initialize(Vector2[] sourceShape, Vector2 sourceSize, float padding, float blurRadius,
            Color glowTint, bool glassMist)
        {
            tint = glowTint;
            BuildTexture(sourceShape ?? Array.Empty<Vector2>(), sourceSize, padding, blurRadius, glassMist);
            intensity = 0f;
            ApplyTint();
            SetMaterialDirty();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Color32 vertexColor = color;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), vertexColor, new Vector2(0, 0));
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), vertexColor, new Vector2(0, 1));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), vertexColor, new Vector2(1, 1));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), vertexColor, new Vector2(1, 0));
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 3);
        }

        private void ApplyTint()
        {
            var c = tint;
            c.a = intensity;
            color = c;
        }

        private void BuildTexture(Vector2[] sourceShape, Vector2 sourceSize, float padding, float blurRadius, bool glassMist)
        {
            if (softMask)
                Destroy(softMask);

            float expandedW = Mathf.Max(1f, sourceSize.x + padding * 2f);
            float expandedH = Mathf.Max(1f, sourceSize.y + padding * 2f);
            int width = Mathf.Clamp(Mathf.RoundToInt(expandedW * .55f), 64, 256);
            int height = Mathf.Clamp(Mathf.RoundToInt(expandedH * .55f), 64, 256);

            softMask = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = name + "_SoftMist",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            float[] baseMask = new float[width * height];
            RasterizeShape(baseMask, width, height, sourceShape, sourceSize, padding);

            float[] alpha = glassMist
                ? BuildGlassMist(baseMask, width, height)
                : BuildObjectMist(baseMask, width, height, expandedW, expandedH, blurRadius);

            var pixels = new Color32[alpha.Length];
            for (int i = 0; i < alpha.Length; i++)
            {
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[i]) * 255f);
                pixels[i] = new Color32(255, 255, 255, a);
            }

            softMask.SetPixels32(pixels);
            softMask.Apply(false, true);
        }

        private static void RasterizeShape(float[] mask, int width, int height, Vector2[] sourceShape,
            Vector2 sourceSize, float padding)
        {
            if (sourceShape.Length < 3 || sourceSize.x <= 0 || sourceSize.y <= 0) return;

            float expandedW = sourceSize.x + padding * 2f;
            float expandedH = sourceSize.y + padding * 2f;

            for (int y = 0; y < height; y++)
            {
                float topY = expandedH - (y + .5f) / height * expandedH - padding;
                float normalizedY = topY / sourceSize.y;

                for (int x = 0; x < width; x++)
                {
                    float leftX = (x + .5f) / width * expandedW - padding;
                    float normalizedX = leftX / sourceSize.x;
                    mask[y * width + x] = Contains(sourceShape, new Vector2(normalizedX, normalizedY)) ? 1f : 0f;
                }
            }
        }

        private static float[] BuildObjectMist(float[] source, int width, int height,
            float expandedW, float expandedH, float blurRadius)
        {
            float scale = .5f * (width / expandedW + height / expandedH);
            int radius = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(3f, blurRadius) * scale), 2, 18);
            float[] blurred = (float[])source.Clone();

            // Three box-blur passes approximate a broad Photoshop-style Gaussian feather,
            // avoiding the hard polygon edge that made the previous version look like a mask.
            for (int pass = 0; pass < 3; pass++)
                blurred = BoxBlur(blurred, width, height, radius);

            var result = new float[source.Length];
            for (int i = 0; i < result.Length; i++)
            {
                // The feather carries nearly all of the visibility. The original shape contributes
                // only a tiny amount so the object reads as gently lit instead of covered by a veil.
                float broadMist = blurred[i] * .145f;
                float innerLift = source[i] * .012f;
                result[i] = Mathf.Clamp01(broadMist + innerLift);
            }
            return result;
        }

        private static float[] BuildGlassMist(float[] source, int width, int height)
        {
            var result = new float[source.Length];

            for (int y = 0; y < height; y++)
            {
                float v = (y + .5f) / height;
                float edgeY = SmoothEdge(v);

                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (source[index] <= 0f) continue;

                    float u = (x + .5f) / width;
                    float edge = edgeY * SmoothEdge(u);

                    // Broad overlapping clouds keep the window alive without tracing its rectangle.
                    float cloudA = Gaussian(u, v, .26f, .64f, .34f, .30f);
                    float cloudB = Gaussian(u, v, .58f, .42f, .42f, .38f);
                    float cloudC = Gaussian(u, v, .82f, .72f, .30f, .26f);
                    float verticalHaze = Gaussian(u, v, .53f, .50f, .65f, .80f) * .35f;

                    float mist = Mathf.Clamp01(cloudA * .55f + cloudB * .50f + cloudC * .42f + verticalHaze);
                    result[index] = mist * edge * .075f;
                }
            }

            return result;
        }

        private static float SmoothEdge(float value)
        {
            float nearest = Mathf.Min(value, 1f - value);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(nearest / .16f));
        }

        private static float Gaussian(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / Mathf.Max(.001f, rx);
            float dy = (v - cy) / Mathf.Max(.001f, ry);
            return Mathf.Exp(-(dx * dx + dy * dy) * 2f);
        }

        private static float[] BoxBlur(float[] source, int width, int height, int radius)
        {
            if (radius <= 0) return (float[])source.Clone();

            var horizontal = new float[source.Length];
            var output = new float[source.Length];
            int diameter = radius * 2 + 1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                    sum += source[row + Mathf.Clamp(k, 0, width - 1)];

                for (int x = 0; x < width; x++)
                {
                    horizontal[row + x] = sum / diameter;
                    int remove = Mathf.Clamp(x - radius, 0, width - 1);
                    int add = Mathf.Clamp(x + radius + 1, 0, width - 1);
                    sum += source[row + add] - source[row + remove];
                }
            }

            for (int x = 0; x < width; x++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                    sum += horizontal[Mathf.Clamp(k, 0, height - 1) * width + x];

                for (int y = 0; y < height; y++)
                {
                    output[y * width + x] = sum / diameter;
                    int remove = Mathf.Clamp(y - radius, 0, height - 1);
                    int add = Mathf.Clamp(y + radius + 1, 0, height - 1);
                    sum += horizontal[add * width + x] - horizontal[remove * width + x];
                }
            }

            return output;
        }

        private static bool Contains(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool crosses = (a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        protected override void OnDestroy()
        {
            if (softMask)
                Destroy(softMask);
            base.OnDestroy();
        }
    }

    public sealed class HomeObjectHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private HomeSoftGlowGraphic glow;
        private Button button;
        private bool hovered;
        private bool selected;
        private bool pressed;
        private float idleIntensity;
        private float hoverIntensity;

        public void Initialize(HomeSoftGlowGraphic target, Button owner, float idle, float hover)
        {
            glow = target;
            button = owner;
            idleIntensity = Mathf.Clamp01(idle);
            hoverIntensity = Mathf.Clamp(Mathf.Max(idleIntensity, hover), idleIntensity, 1f);
            if (glow) glow.Intensity = idleIntensity;
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
            if (!glow || !button) return;

            bool active = button.IsInteractable() && (hovered || selected);
            float target = active ? hoverIntensity : idleIntensity;

            if (active && pressed)
                target = Mathf.Min(1f, hoverIntensity * 1.08f);
            else if (active)
                target *= .996f + Mathf.Sin(Time.unscaledTime * 1.7f) * .004f;

            float factor = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            glow.Intensity = Mathf.Lerp(glow.Intensity, target, factor);
        }

        private void OnDisable()
        {
            hovered = selected = pressed = false;
            if (glow) glow.Intensity = idleIntensity;
        }
    }
}
