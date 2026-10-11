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
    /// Live, independently rendered PRESENTATION of the SAME YOMI app catalog,
    /// without any static "desktop screenshot". This RGBA runtime texture is
    /// perspective-mapped into the calibrated four corners of the physical LCD.
    /// Data comes from the existing LaptopView catalog, session unread counter,
    /// and real OS clock; the actual interactions open the SAME fullscreen YOMI.
    /// </summary>
    public sealed class LaptopPhysicalDesktopClock : LaptopPerspectiveQuad
    {
        private const int Width = 640, Height = 360;
        private Texture2D live;
        private Color32[] working;
        private Func<int> unreadProvider;
        private Color32[] wallpaperPixels;
        private long lastSecond = -1;
        private int lastUnread = -1;

        public void Initialize(Func<int> getUnread = null, Texture wallpaper = null)
        {
            unreadProvider = getUnread;
            wallpaperPixels = null;
            // Capture the SAME Tokyo/blue YOMI wallpaper used by fullscreen
            // LaptopView, once. GPU copy also works when texture Read/Write is off.
            if (wallpaper)
            {
                RenderTexture target = null;
                RenderTexture previous = RenderTexture.active;
                Texture2D snapshot = null;
                try
                {
                    target = RenderTexture.GetTemporary(Width, Height, 0,
                        RenderTextureFormat.ARGB32);
                    Graphics.Blit(wallpaper, target);
                    RenderTexture.active = target;
                    snapshot = new Texture2D(Width, Height,
                        TextureFormat.RGBA32, false);
                    snapshot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    snapshot.Apply(false, false);
                    wallpaperPixels = snapshot.GetPixels32();
                    for (int i = 0; i < wallpaperPixels.Length; i++)
                        wallpaperPixels[i].a = 255;
                }
                catch (Exception error)
                {
                    wallpaperPixels = null;
                    Debug.LogWarning("ROKAS Mini YOMI: wallpaper snapshot unavailable: "
                        + error.Message);
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (snapshot) Destroy(snapshot);
                    if (target) RenderTexture.ReleaseTemporary(target);
                }
            }
            live = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            live.name = "ROKAS_YOMI_LivePhysicalDesktop";
            live.filterMode = FilterMode.Bilinear;
            live.wrapMode = TextureWrapMode.Clamp;
            working = new Color32[Width * Height];
            SetImage(live);
            UpdateClockIfNecessary(force: true);
        }

        public void UpdateClockIfNecessary(bool force = false)
        {
            if (!live) return;
            DateTime now = DateTime.Now; // Matches fullscreen YOMI's OS clock.
            long second = now.Ticks / TimeSpan.TicksPerSecond;
            int unread = Mathf.Clamp(unreadProvider != null ? unreadProvider() : 0, 0, 99);
            if (!force && second == lastSecond && unread == lastUnread) return;
            lastSecond = second;
            lastUnread = unread;
            DrawLiveDesktop(now, unread);
            live.SetPixels32(working);
            live.Apply(false, false);
        }

        private void DrawLiveDesktop(DateTime now, int unread)
        {
            // Fill every pixel with an OPAQUE live-rendered desktop; the approved
            // screen-OFF photo remains untouched and is revealed while powered off.
            if (wallpaperPixels != null && wallpaperPixels.Length == working.Length)
                Array.Copy(wallpaperPixels, working, working.Length);
            else
            {
                for (int y = 0; y < Height; y++)
                {
                    byte r = (byte)(12 + y * 10 / Height);
                    byte g = (byte)(25 + y * 15 / Height);
                    byte b = (byte)(42 + y * 19 / Height);
                    Rect(0, y, Width, 1, new Color32(r, g, b, 255));
                }
            }
            Rect(0, 0, Width, 43, new Color32(11, 20, 34, 255));
            Rect(0, 43, Width, 1, new Color32(83, 126, 158, 150));
            DrawText("R O K A S", 25, 13, 2, new Color32(228, 227, 219, 255));
            DrawText("YOMI", 495, 13, 2, new Color32(143, 197, 231, 255));

            // Nine buttons are mirrored from LaptopView's canonical catalog.
            // No separate data model, reset, fake clickable app, or second boot.
            for (int i = 0; i < LaptopView.DesktopAppCount; i++)
            {
                // Physical LCD mirrors the fullscreen YOMI desktop's 4-column
                // arrangement, with nine real app names/colors (not fake apps).
                int x = 18 + (i % 4) * 152;
                int y = 58 + (i / 4) * 86;
                Rect(x, y, 142, 76, new Color32(20, 34, 52, 255));
                Rect(x + 1, y + 1, 140, 74, new Color32(28, 46, 67, 255));
                Color tint = LaptopView.DesktopAppColor(i);
                Color32 tile = new Color32(
                    (byte)Mathf.RoundToInt(tint.r * 210f),
                    (byte)Mathf.RoundToInt(tint.g * 210f),
                    (byte)Mathf.RoundToInt(tint.b * 210f), 255);
                Rect(x + 7, y + 18, 34, 38, tile);
                Rect(x + 13, y + 22, 21, 2, new Color32(234, 231, 222, 230));
                Rect(x + 16, y + 29, 16, 19, new Color32(26, 39, 55, 140));
                DrawText(LaptopView.DesktopAppTitle(i), x + 44, y + 35, 1,
                    new Color32(222, 234, 247, 255));
                if (i == 6 && unread > 0)
                {
                    Rect(x + 23, y + 5, 26, 19, new Color32(163, 49, 55, 255));
                    DrawText(unread.ToString(), x + 27, y + 10, 1,
                        new Color32(255, 245, 233, 255));
                }
            }
            Rect(0, 324, Width, 36, new Color32(8, 16, 29, 255));
            Rect(0, 324, Width, 1, new Color32(90, 132, 161, 255));
            DrawText("YOMI ONLINE", 22, 339, 1, new Color32(136, 201, 210, 255));
            DrawText(now.ToString("HH:mm:ss"), 504, 330, 2,
                new Color32(223, 236, 255, 255));
            DrawText(now.ToString("yyyy-MM-dd"), 529, 351, 1,
                new Color32(164, 190, 214, 255));
        }

        private void Rect(int x, int y, int w, int h, Color32 color)
        {
            // The physical LCD is an OPAQUE powered panel. Decorative alpha
            // values are visual blending weights, NEVER texture transparency:
            // transparency here exposed the OFF-state photo underneath YOMI.
            int a = color.a, inv = 255 - a;
            for (int py = Mathf.Max(0, y); py < Mathf.Min(Height, y + h); py++)
                for (int px = Mathf.Max(0, x); px < Mathf.Min(Width, x + w); px++)
                {
                    int index = (Height - 1 - py) * Width + px;
                    if (a == 255)
                    {
                        working[index] = color;
                        continue;
                    }
                    Color32 baseColor = working[index];
                    working[index] = new Color32(
                        (byte)((color.r * a + baseColor.r * inv + 127) / 255),
                        (byte)((color.g * a + baseColor.g * inv + 127) / 255),
                        (byte)((color.b * a + baseColor.b * inv + 127) / 255),
                        255);
                }
        }

        private void DrawText(string text, int x, int y, int size, Color32 color)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < text.Length; i++)
            {
                string[] rows = Glyph(char.ToUpperInvariant(text[i]));
                for (int ry = 0; ry < rows.Length; ry++)
                    for (int rx = 0; rx < rows[ry].Length; rx++)
                        if (rows[ry][rx] == '1')
                            Rect(x + i * 6 * size + rx * size,
                                y + ry * size, size, size, color);
            }
        }

        private static string[] Glyph(char c)
        {
            switch (c)
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
                case 'A':return new[]{"01110","10001","10001","11111","10001","10001","10001"};
                case 'B':return new[]{"11110","10001","10001","11110","10001","10001","11110"};
                case 'C':return new[]{"01111","10000","10000","10000","10000","10000","01111"};
                case 'D':return new[]{"11110","10001","10001","10001","10001","10001","11110"};
                case 'E':return new[]{"11111","10000","10000","11110","10000","10000","11111"};
                case 'F':return new[]{"11111","10000","10000","11110","10000","10000","10000"};
                case 'G':return new[]{"01111","10000","10000","10111","10001","10001","01111"};
                case 'H':return new[]{"10001","10001","10001","11111","10001","10001","10001"};
                case 'I':return new[]{"01110","00100","00100","00100","00100","00100","01110"};
                case 'J':return new[]{"00001","00001","00001","00001","10001","10001","01110"};
                case 'K':return new[]{"10001","10010","10100","11000","10100","10010","10001"};
                case 'L':return new[]{"10000","10000","10000","10000","10000","10000","11111"};
                case 'M':return new[]{"10001","11011","10101","10101","10001","10001","10001"};
                case 'N':return new[]{"10001","11001","10101","10011","10001","10001","10001"};
                case 'O':return new[]{"01110","10001","10001","10001","10001","10001","01110"};
                case 'P':return new[]{"11110","10001","10001","11110","10000","10000","10000"};
                case 'Q':return new[]{"01110","10001","10001","10001","10101","10010","01101"};
                case 'R':return new[]{"11110","10001","10001","11110","10100","10010","10001"};
                case 'S':return new[]{"01111","10000","10000","01110","00001","00001","11110"};
                case 'T':return new[]{"11111","00100","00100","00100","00100","00100","00100"};
                case 'U':return new[]{"10001","10001","10001","10001","10001","10001","01110"};
                case 'V':return new[]{"10001","10001","10001","10001","10001","01010","00100"};
                case 'W':return new[]{"10001","10001","10001","10101","10101","10101","01010"};
                case 'X':return new[]{"10001","10001","01010","00100","01010","10001","10001"};
                case 'Y':return new[]{"10001","10001","01010","00100","00100","00100","00100"};
                case 'Z':return new[]{"11111","00001","00010","00100","01000","10000","11111"};
                case 'А':return new[]{"01110","10001","10001","11111","10001","10001","10001"};
                case 'Б':return new[]{"11111","10000","10000","11110","10001","10001","11110"};
                case 'В':return new[]{"11110","10001","10001","11110","10001","10001","11110"};
                case 'Г':return new[]{"11111","10000","10000","10000","10000","10000","10000"};
                case 'Д':return new[]{"00110","01010","01010","01010","10010","11111","10001"};
                case 'Е':return new[]{"11111","10000","10000","11110","10000","10000","11111"};
                case 'И':return new[]{"10001","10011","10101","10101","11001","10001","10001"};
                case 'Й':return new[]{"01010","00100","10011","10101","10101","11001","10001"};
                case 'К':return new[]{"10001","10010","10100","11000","10100","10010","10001"};
                case 'Л':return new[]{"00111","01001","01001","01001","10001","10001","10001"};
                case 'Н':return new[]{"10001","10001","10001","11111","10001","10001","10001"};
                case 'О':return new[]{"01110","10001","10001","10001","10001","10001","01110"};
                case 'П':return new[]{"11111","10001","10001","10001","10001","10001","10001"};
                case 'Р':return new[]{"11110","10001","10001","11110","10000","10000","10000"};
                case 'С':return new[]{"01111","10000","10000","10000","10000","10000","01111"};
                case 'Т':return new[]{"11111","00100","00100","00100","00100","00100","00100"};
                case 'У':return new[]{"10001","10001","01010","00100","00100","01000","10000"};
                case 'Ф':return new[]{"00100","01110","10101","10101","01110","00100","00100"};
                case 'Ы':return new[]{"10001","10001","11101","10101","11101","10001","10001"};
                case 'Ь':return new[]{"10000","10000","11110","10001","10001","10001","11110"};
                case 'Я':return new[]{"01111","10001","10001","01111","00101","01001","10001"};
                case 'Щ':return new[]{"10101","10101","10101","10101","10101","11111","00001"};
                case 'М':return new[]{"10001","11011","10101","10101","10001","10001","10001"};
                case 'Ж':return new[]{"10101","10101","01110","00100","01110","10101","10101"};
                case 'З':return new[]{"11110","00001","00001","00110","00001","00001","11110"};
                case 'Ч':return new[]{"10001","10001","10001","01111","00001","00001","00001"};
                case 'Ш':return new[]{"10101","10101","10101","10101","10101","10101","11111"};
                case ':':return new[]{"00000","00100","00100","00000","00100","00100","00000"};
                case '-':return new[]{"00000","00000","00000","11111","00000","00000","00000"};
                default:return new[]{"00000","00000","00000","00000","00000","00000","00000"};
            }
        }

        protected override void OnDestroy()
        {
            if (live) Destroy(live);
            live = null;
            wallpaperPixels = null;
            base.OnDestroy();
        }
    }

    /// <summary>
    /// A scaled, transparent user chibi sticker moves inside the masked physical
    /// laptop LCD like an old DVD screensaver. This changes no photos or frames.
    /// </summary>
    // A pixel crop of the ACTUAL fullscreen YOMI uGUI, not a separately
    // drawn copy of its icons, wallpaper or typography. Captured at end of
    // frame while the desktop is on screen and before its close fade begins.
    public static class LaptopYomiMirror
    {
        public static Texture2D Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void NewProcess() { Current = null; }

        public static bool Capture(RectTransform originalScreen)
        {
            if (!originalScreen || Screen.width < 1 || Screen.height < 1) return false;
            Texture2D screenshot = null;
            try
            {
                var corners = new Vector3[4];
                originalScreen.GetWorldCorners(corners);
                Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                int left = Mathf.Clamp(Mathf.RoundToInt(bottomLeft.x), 0, Screen.width - 1);
                int bottom = Mathf.Clamp(Mathf.RoundToInt(bottomLeft.y), 0, Screen.height - 1);
                int right = Mathf.Clamp(Mathf.RoundToInt(topRight.x), left + 1, Screen.width);
                int top = Mathf.Clamp(Mathf.RoundToInt(topRight.y), bottom + 1, Screen.height);
                if (right - left < 16 || top - bottom < 16) return false;
                screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (!screenshot) return false;
                var exact = new Texture2D(right - left, top - bottom, TextureFormat.RGBA32, false);
                exact.name = "ROKAS_Exact_Fullscreen_YOMI_Capture";
                exact.wrapMode = TextureWrapMode.Clamp;
                exact.filterMode = FilterMode.Bilinear;
                exact.SetPixels(screenshot.GetPixels(left, bottom, exact.width, exact.height));
                exact.Apply(false, false);
                if (Current) UnityEngine.Object.Destroy(Current);
                Current = exact;
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("ROKAS exact YOMI screen capture postponed: " + error.Message);
                return false;
            }
            finally
            {
                if (screenshot) UnityEngine.Object.Destroy(screenshot);
            }
        }
    }

    public sealed class LaptopNoSignalBounce : MonoBehaviour
    {
        private RectTransform viewport;
        private RectTransform sticker;
        private Vector2 point = new Vector2(8f, 14f);
        private Vector2 velocity = new Vector2(42f, 29f);
        private Action onWallContact;
        public int CollisionCount { get; private set; }

        public void Initialize(RectTransform area, RectTransform target, Action bounceSound = null)
        {
            viewport = area;
            sticker = target;
            onWallContact = bounceSound;
            CollisionCount = 0;
            point = new Vector2(8f, 14f);
            Apply();
        }

        private static float Bounce(ref float direction, float value, float low, float high)
        {
            if (high <= low) return low;
            while (value < low || value > high)
            {
                if (value < low) { value = low + low - value; direction = Mathf.Abs(direction); }
                if (value > high) { value = high + high - value; direction = -Mathf.Abs(direction); }
            }
            return value;
        }

        private void Update()
        {
            if (!viewport || !sticker) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .12f);
            float vx = velocity.x, vy = velocity.y;
            point.x = Bounce(ref vx, point.x + vx * dt,
                5f, viewport.rect.width - sticker.rect.width - 5f);
            point.y = Bounce(ref vy, point.y + vy * dt,
                5f, viewport.rect.height - sticker.rect.height - 5f);
            bool hit = vx != velocity.x || vy != velocity.y;
            velocity = new Vector2(vx, vy);
            Apply();
            if (hit)
            {
                CollisionCount++;
                onWallContact?.Invoke(); // one SFX per real wall contact, never per frame
            }
        }

        private void Apply()
        {
            if (sticker) sticker.anchoredPosition = new Vector2(point.x, -point.y);
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
                // Never dim or retint the approved PNG in its resting state.
                float gain = 1f + hoverWeight * (.075f + .02f * Mathf.Sin(time * 5.2f));
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
