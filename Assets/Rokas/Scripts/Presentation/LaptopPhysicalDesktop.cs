using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>Perspective-correct-enough four-corner UI quad aligned to the
    /// approved POV image. Never alters the source photo.</summary>
    public class LaptopPerspectiveQuad : MaskableGraphic
    {
        private Vector2[] corners;
        private float factor = 1f;
        private Texture image;

        public override Texture mainTexture => image ? image : Texture2D.whiteTexture;

        public void SetImage(Texture value)
        {
            image = value;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void SetCorners(Vector2[] imagePixels, float scale)
        {
            if (imagePixels == null || imagePixels.Length != 4)
                throw new ArgumentException("Exactly four screen corners required.");
            corners = (Vector2[])imagePixels.Clone();
            factor = scale;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (corners == null || corners.Length != 4) return;
            for (int i = 0; i < 4; i++)
            {
                UIVertex v = UIVertex.simpleVert;
                v.position = new Vector3(corners[i].x * factor,
                    -corners[i].y * factor, 0f);
                v.uv0 = new Vector2(i == 1 || i == 2 ? 1f : 0f,
                    i >= 2 ? 0f : 1f);
                v.color = color;
                vh.AddVert(v);
            }
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
        }
    }

    /// <summary>
    /// Actual changing desktop pixels, not a still of a clock. Time is read
    /// from the local OS. Rendered once per second on a transparent RGBA atlas
    /// which is perspective-mapped onto the physical LCD.
    /// </summary>
    public sealed class LaptopPhysicalDesktopClock : LaptopPerspectiveQuad
    {
        private const int Width = 640, Height = 360;
        private Texture2D live;
        private Color32[] clearPixels;
        private Color32[] working;
        private long lastSecond = -1;

        public void Initialize()
        {
            live = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            live.name = "ROKAS_LaptopLiveDesktopClock";
            live.filterMode = FilterMode.Bilinear;
            clearPixels = new Color32[Width * Height];
            working = new Color32[clearPixels.Length];
            SetImage(live);
            UpdateClockIfNecessary(force: true);
        }

        public void UpdateClockIfNecessary(bool force = false)
        {
            if (!live) return;
            DateTime now = DateTime.Now; // User explicitly selected real system time.
            long second = now.Ticks / TimeSpan.TicksPerSecond;
            if (!force && second == lastSecond) return;
            lastSecond = second;
            Array.Copy(clearPixels, working, working.Length);
            // On-screen desktop taskbar and indicators animate independently
            // of YOMI while its physical display stays powered.
            Rect(0, 326, Width, 34, new Color32(8, 17, 33, 218));
            Rect(455, 333, 174, 22, new Color32(16, 31, 51, 228));
            Rect(470, 339, 8, 8, new Color32(55, 163, 231, 210));
            Rect(486, 335, 4, 12, new Color32(98, 188, 230, 240));
            Rect(493, 332, 4, 15, new Color32(98, 188, 230, 240));
            Rect(500, 329, 4, 18, new Color32(98, 188, 230, 240));
            DrawText(now.ToString("HH:mm:ss"), 511, 335, 2,
                new Color32(214, 231, 251, 255));
            DrawText(now.ToString("yyyy-MM-dd"), 511, 350, 1,
                new Color32(164, 188, 214, 255));
            live.SetPixels32(working);
            live.Apply(false, false);
        }

        private void Rect(int x, int y, int w, int h, Color32 color)
        {
            for (int py = Mathf.Max(0, y); py < Mathf.Min(Height, y+h); py++)
                for (int px = Mathf.Max(0,x); px < Mathf.Min(Width,x+w); px++)
                    working[(Height-1-py)*Width+px] = color;
        }

        private void DrawText(string text, int x, int y, int size, Color32 color)
        {
            for (int i = 0; i < text.Length; i++)
            {
                string[] rows = Glyph(text[i]);
                for (int ry = 0; ry < rows.Length; ry++)
                    for (int rx = 0; rx < rows[ry].Length; rx++)
                        if (rows[ry][rx] == '1')
                            Rect(x+i*6*size+rx*size,y+ry*size,size,size,color);
            }
        }

        private static string[] Glyph(char c)
        {
            switch(c)
            {
                case '0':return new[]{"01110","10001","10011","10101","11001","10001","01110"};
                case '1':return new[]{"00100","01100","00100","00100","00100","00100","01110"};
                case '2':return new[]{"01110","10001","00001","00110","01000","10000","11111"};
                case '3':return new[]{"11110","00001","00001","01110","00001","00001","11110"};
                case '4':return new[]{"00010","00110","01010","10010","11111","00010","00010"};
                case '5':return new[]{"11111","10000","10000","11110","00001","00001","11110"};
                case '6':return new[]{"01110","10000","10000","11110","10001","10001","01110"};
                case '7':return new[]{"11111","00001","00010","00100","01000","01000","01000"};
                case '8':return new[]{"01110","10001","10001","01110","10001","10001","01110"};
                case '9':return new[]{"01110","10001","10001","01111","00001","00001","01110"};
                case ':':return new[]{"00000","00100","00100","00000","00100","00100","00000"};
                case '-':return new[]{"00000","00000","00000","11111","00000","00000","00000"};
                default:return new[]{"00000","00000","00000","00000","00000","00000","00000"};
            }
        }

        protected override void OnDestroy()
        {
            if (live) Destroy(live);
            live = null;
            base.OnDestroy();
        }
    }

    /// <summary>
    /// Subtle idle pulse + responsive hover/press on the ORIGINAL approved PNG.
    /// The source art is never modified and disable/reopen resets all state.
    /// </summary>
    public sealed class LaptopChoiceHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler
    {
        private RectTransform rect;
        private Graphic graphic;
        private bool hovering;
        private float time;
        private float hoverWeight;
        private float pressRemaining;
        private Color initialTint;
        private Vector3 initialScale;

        private void Awake()
        {
            rect = transform as RectTransform;
            graphic = GetComponent<Graphic>();
            initialScale = rect ? rect.localScale : Vector3.one;
            if (graphic) initialTint = graphic.color;
        }

        public void OnPointerEnter(PointerEventData data) { hovering = true; }
        public void OnPointerExit(PointerEventData data) { hovering = false; }
        public void OnPointerDown(PointerEventData data) { pressRemaining = .14f; }
        public void OnPointerUp(PointerEventData data) { pressRemaining = Mathf.Min(pressRemaining, .06f); }

        private void Update()
        {
            if (!rect) return;
            float dt = Time.unscaledDeltaTime;
            time += dt;
            hoverWeight = Mathf.MoveTowards(hoverWeight, hovering ? 1f : 0f, dt * 6.5f);
            pressRemaining = Mathf.Max(0f, pressRemaining - dt);

            float idlePulse = .005f * Mathf.Sin(time * 2.1f);
            float hoverPulse = .014f * Mathf.Sin(time * 5.2f);
            float press = -.055f * Mathf.Clamp01(pressRemaining / .14f);
            float targetScale = 1f + idlePulse +
                hoverWeight * (.035f + hoverPulse) + press;
            float smooth = 1f - Mathf.Exp(-15f * dt);
            rect.localScale = Vector3.Lerp(rect.localScale,
                initialScale * targetScale, smooth);
            if (graphic)
            {
                float gain = 1f + .018f * Mathf.Sin(time * 2.1f) +
                    hoverWeight * (.10f + .045f * Mathf.Sin(time * 5.2f));
                Color target = new Color(
                    Mathf.Clamp01(initialTint.r * gain),
                    Mathf.Clamp01(initialTint.g * gain),
                    Mathf.Clamp01(initialTint.b * gain), initialTint.a);
                graphic.color = Color.Lerp(graphic.color, target, smooth);
            }
        }

        private void OnDisable()
        {
            hovering = false;
            time = hoverWeight = pressRemaining = 0f;
            if (rect) rect.localScale = initialScale;
            if (graphic) graphic.color = initialTint;
        }
    }

}
