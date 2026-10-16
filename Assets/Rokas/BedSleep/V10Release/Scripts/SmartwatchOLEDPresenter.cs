using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// V6 Interactive 3D OLED Watch Face Presenter.
    /// V6-04 Resolution: Directly rasterizes numeric duration ("7h 30m"), wake time ("WAKE 07:30"),
    /// current time ("NOW 23:30"), and interactive confirmation label ("CONFIRM" / "ALARM SET")
    /// onto OledDynamicTexture and OledRenderTexture using a crisp embedded 5x7 glyph font.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmartwatchOLEDPresenter : MonoBehaviour
    {
        public RenderTexture OledRenderTexture { get; private set; }
        public Texture2D OledDynamicTexture { get; private set; }
        public int CurrentHour { get; private set; } = 0;
        public int CurrentMinute { get; private set; } = 0;
        public int SelectedHalfHours { get; private set; } = 15;
        public float SelectedDurationHours => SelectedHalfHours * 0.5f;
        public int CalculatedWakeTimeMinutes => ((CurrentHour * 60 + CurrentMinute + Mathf.RoundToInt(SelectedDurationHours * 60f)) % (24 * 60));
        public bool IsConfirmed { get; private set; }
        public string LastRenderedTextSummary { get; private set; } = string.Empty;

        public string FormattedCurrentTime => $"{CurrentHour:00}:{CurrentMinute:00}";

        public string FormattedDuration
        {
            get
            {
                int h = SelectedHalfHours / 2;
                int m = (SelectedHalfHours % 2) * 30;
                return $"{h}h {m:00}m";
            }
        }

        public string FormattedWakeTime
        {
            get
            {
                int totalMins = CalculatedWakeTimeMinutes;
                if (totalMins < 0) totalMins += 24 * 60;
                return $"{(totalMins / 60) % 24:00}:{totalMins % 60:00}";
            }
        }

        private Color32[] _pixelBuffer;
        public const int Size = 256;

        // 5x7 Bitmap Font Table (each byte is a 5-bit row, top to bottom, MSB on left)
        private static readonly Dictionary<char, byte[]> GlyphTable = new Dictionary<char, byte[]>
        {
            {'0', new byte[]{0x0E,0x11,0x13,0x15,0x19,0x11,0x0E}},
            {'1', new byte[]{0x04,0x0C,0x04,0x04,0x04,0x04,0x0E}},
            {'2', new byte[]{0x0E,0x11,0x01,0x06,0x08,0x10,0x1F}},
            {'3', new byte[]{0x1F,0x02,0x04,0x02,0x01,0x11,0x0E}},
            {'4', new byte[]{0x02,0x06,0x0A,0x12,0x1F,0x02,0x02}},
            {'5', new byte[]{0x1F,0x10,0x1E,0x01,0x01,0x11,0x0E}},
            {'6', new byte[]{0x06,0x08,0x10,0x1E,0x11,0x11,0x0E}},
            {'7', new byte[]{0x1F,0x01,0x02,0x04,0x08,0x08,0x08}},
            {'8', new byte[]{0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E}},
            {'9', new byte[]{0x0E,0x11,0x11,0x0F,0x01,0x02,0x0C}},
            {':', new byte[]{0x00,0x04,0x04,0x00,0x04,0x04,0x00}},
            {' ', new byte[]{0x00,0x00,0x00,0x00,0x00,0x00,0x00}},
            {'A', new byte[]{0x0E,0x11,0x11,0x1F,0x11,0x11,0x11}},
            {'C', new byte[]{0x0E,0x11,0x10,0x10,0x10,0x11,0x0E}},
            {'E', new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F}},
            {'F', new byte[]{0x1F,0x10,0x10,0x1E,0x10,0x10,0x10}},
            {'H', new byte[]{0x11,0x11,0x11,0x1F,0x11,0x11,0x11}},
            {'I', new byte[]{0x0E,0x04,0x04,0x04,0x04,0x04,0x0E}},
            {'K', new byte[]{0x11,0x12,0x14,0x18,0x14,0x12,0x11}},
            {'L', new byte[]{0x10,0x10,0x10,0x10,0x10,0x10,0x1F}},
            {'M', new byte[]{0x11,0x1B,0x15,0x15,0x11,0x11,0x11}},
            {'N', new byte[]{0x11,0x19,0x15,0x13,0x11,0x11,0x11}},
            {'O', new byte[]{0x0E,0x11,0x11,0x11,0x11,0x11,0x0E}},
            {'R', new byte[]{0x1E,0x11,0x11,0x1E,0x14,0x12,0x11}},
            {'S', new byte[]{0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E}},
            {'T', new byte[]{0x1F,0x04,0x04,0x04,0x04,0x04,0x04}},
            {'W', new byte[]{0x11,0x11,0x11,0x15,0x15,0x1B,0x11}},
            {'h', new byte[]{0x10,0x10,0x1E,0x11,0x11,0x11,0x11}},
            {'m', new byte[]{0x00,0x00,0x1A,0x15,0x15,0x15,0x15}},
        };

        public void InitializeOled()
        {
            if (OledRenderTexture == null)
            {
                OledRenderTexture = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32)
                {
                    name = "RT_ROKAS_WatchOLED_V6",
                    antiAliasing = 1
                };
                OledRenderTexture.Create();
            }
            if (OledDynamicTexture == null)
            {
                OledDynamicTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    name = "Tex_ROKAS_WatchOLED_V6",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                _pixelBuffer = new Color32[Size * Size];
            }
            ConfigureTime(0, 0, 15);
        }

        public void ConfigureTime(int currentHour, int currentMinute, int selectedHalfHours)
        {
            CurrentHour = ((currentHour % 24) + 24) % 24;
            CurrentMinute = Mathf.Clamp(currentMinute, 0, 59);
            SelectedHalfHours = Mathf.Clamp(selectedHalfHours, 2, 24);
            RedrawOledSurface();
        }

        public void SetConfirmedState(bool confirmed)
        {
            IsConfirmed = confirmed;
            RedrawOledSurface();
        }

        public void ConfirmCurrentSelection()
        {
            SetConfirmedState(true);
        }

        private void RedrawOledSurface()
        {
            if (OledDynamicTexture == null || _pixelBuffer == null) return;

            Color32 bg = new Color32(5, 8, 14, 255);
            for (int i = 0; i < _pixelBuffer.Length; i++) _pixelBuffer[i] = bg;

            float progress = Mathf.Clamp01((SelectedHalfHours - 2f) / 22f);
            float maxSweepRad = 1.5f * Mathf.PI * progress;
            Color32 ringTrack = new Color32(24, 40, 62, 255);
            Color32 ringActive = IsConfirmed ? new Color32(85, 245, 175, 255) : new Color32(78, 196, 255, 255);
            Color32 pillCol = IsConfirmed ? new Color32(36, 168, 110, 255) : new Color32(28, 74, 118, 255);

            float cx = Size * 0.5f, cy = Size * 0.52f, rOuter = Size * 0.42f, rInner = Size * 0.37f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist >= rInner && dist <= rOuter)
                    {
                        float ang = Mathf.Atan2(-dy, dx);
                        float normAng = Mathf.Repeat(ang - (0.75f * Mathf.PI), 2f * Mathf.PI);
                        if (normAng <= 1.5f * Mathf.PI) _pixelBuffer[y * Size + x] = normAng <= maxSweepRad ? ringActive : ringTrack;
                    }
                    else if (y >= 24 && y <= 54 && x >= 52 && x <= 204)
                    {
                        _pixelBuffer[y * Size + x] = pillCol;
                    }
                }
            }

            // V6-04: Render crisp numeric duration, wake time, current time, and confirm text directly onto the OLED pixels!
            string nowLine = $"NOW {FormattedCurrentTime}";
            string durLine = FormattedDuration;
            string wakeLine = $"WAKE {FormattedWakeTime}";
            string ctaLine = IsConfirmed ? "ALARM SET" : "CONFIRM";

            DrawStringCentered(nowLine, 128, 188, 2, new Color32(145, 205, 250, 255));
            DrawStringCentered(durLine, 128, 140, 4, new Color32(255, 248, 238, 255));
            DrawStringCentered(wakeLine, 128, 98, 2, new Color32(180, 225, 255, 255));
            DrawStringCentered(ctaLine, 128, 38, 2, new Color32(245, 255, 250, 255));

            LastRenderedTextSummary = $"{nowLine} | {durLine} | {wakeLine} | {ctaLine}";

            OledDynamicTexture.SetPixels32(_pixelBuffer);
            OledDynamicTexture.Apply(false);
            if (OledRenderTexture != null) Graphics.Blit(OledDynamicTexture, OledRenderTexture);
        }

        private void DrawStringCentered(string text, int centerX, int centerY, int scale, Color32 color)
        {
            if (string.IsNullOrEmpty(text)) return;
            int charAdvance = 6 * scale;
            int totalWidth = text.Length * charAdvance - scale;
            int startX = centerX - (totalWidth / 2);
            int topY = centerY + (7 * scale) / 2;

            for (int i = 0; i < text.Length; i++)
            {
                DrawChar(text[i], startX + i * charAdvance, topY, scale, color);
            }
        }

        private void DrawChar(char c, int leftX, int topY, int scale, Color32 color)
        {
            if (!GlyphTable.TryGetValue(c, out byte[] rows))
            {
                GlyphTable.TryGetValue(char.ToUpperInvariant(c), out rows);
            }
            if (rows == null) return;

            for (int r = 0; r < 7; r++)
            {
                byte rowBits = rows[r];
                for (int col = 0; col < 5; col++)
                {
                    if (((rowBits >> (4 - col)) & 1) != 0)
                    {
                        int px0 = leftX + col * scale;
                        int py0 = topY - (r + 1) * scale;
                        for (int sy = 0; sy < scale; sy++)
                        {
                            int y = py0 + sy;
                            if (y < 0 || y >= Size) continue;
                            for (int sx = 0; sx < scale; sx++)
                            {
                                int x = px0 + sx;
                                if (x >= 0 && x < Size)
                                {
                                    _pixelBuffer[y * Size + x] = color;
                                }
                            }
                        }
                    }
                }
            }
        }

        public int CountLitTextPixelsInRegion(int xMin, int yMin, int xMax, int yMax)
        {
            if (_pixelBuffer == null) return 0;
            int count = 0;
            for (int y = Mathf.Max(0, yMin); y <= Mathf.Min(Size - 1, yMax); y++)
            {
                for (int x = Mathf.Max(0, xMin); x <= Mathf.Min(Size - 1, xMax); x++)
                {
                    Color32 c = _pixelBuffer[y * Size + x];
                    if (c.r > 120 && c.g > 180 && c.b > 200) count++;
                }
            }
            return count;
        }

        private void OnDestroy()
        {
            if (OledRenderTexture != null)
            {
                OledRenderTexture.Release();
                DestroyImmediate(OledRenderTexture);
                OledRenderTexture = null;
            }
            if (OledDynamicTexture != null)
            {
                DestroyImmediate(OledDynamicTexture);
                OledDynamicTexture = null;
            }
        }
    }
}
