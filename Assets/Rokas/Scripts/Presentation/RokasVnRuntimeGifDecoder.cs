using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rokas.Presentation
{
    internal static class RokasVnRuntimeGifDecoder
    {
        private sealed class FrameState
        {
            public int left;
            public int top;
            public int width;
            public int height;
            public int disposal;
            public Color32[] restore;
        }

        public static void Decode(byte[] bytes, List<Texture2D> frames, List<float> delays)
        {
            if (bytes == null || bytes.Length < 13) throw new InvalidDataException("GIF data is truncated.");
            int offset = 0;
            string signature = ReadAscii(bytes, ref offset, 6);
            if (signature != "GIF87a" && signature != "GIF89a")
                throw new InvalidDataException("GIF header is invalid.");

            int canvasWidth = ReadU16(bytes, ref offset);
            int canvasHeight = ReadU16(bytes, ref offset);
            if (canvasWidth <= 0 || canvasHeight <= 0)
                throw new InvalidDataException("GIF dimensions are invalid.");

            byte packed = ReadByte(bytes, ref offset);
            int backgroundIndex = ReadByte(bytes, ref offset);
            ReadByte(bytes, ref offset);
            Color32[] globalPalette = (packed & 0x80) != 0
                ? ReadPalette(bytes, ref offset, 1 << ((packed & 0x07) + 1))
                : null;

            Color32 background = globalPalette != null && backgroundIndex < globalPalette.Length
                ? globalPalette[backgroundIndex]
                : new Color32(0, 0, 0, 0);
            Color32[] canvas = new Color32[canvasWidth * canvasHeight];
            for (int i = 0; i < canvas.Length; i++) canvas[i] = background;

            int gceDelay = 10;
            int gceDisposal = 0;
            bool gceTransparent = false;
            int gceTransparentIndex = 0;
            FrameState previous = null;

            while (offset < bytes.Length)
            {
                byte marker = ReadByte(bytes, ref offset);
                if (marker == 0x3B) break;
                if (marker == 0x21)
                {
                    byte label = ReadByte(bytes, ref offset);
                    if (label == 0xF9)
                    {
                        int blockSize = ReadByte(bytes, ref offset);
                        if (blockSize != 4)
                            throw new InvalidDataException("GIF graphics-control block is invalid.");
                        byte control = ReadByte(bytes, ref offset);
                        gceDisposal = (control >> 2) & 0x07;
                        gceTransparent = (control & 0x01) != 0;
                        gceDelay = ReadU16(bytes, ref offset);
                        gceTransparentIndex = ReadByte(bytes, ref offset);
                        if (ReadByte(bytes, ref offset) != 0)
                            throw new InvalidDataException("GIF graphics-control terminator is invalid.");
                    }
                    else
                    {
                        SkipExtension(bytes, ref offset);
                    }
                    continue;
                }

                if (marker != 0x2C)
                    throw new InvalidDataException("Unsupported GIF block marker: 0x" + marker.ToString("X2") + ".");

                ApplyPreviousDisposal(canvas, canvasWidth, canvasHeight, background, previous);

                int left = ReadU16(bytes, ref offset);
                int top = ReadU16(bytes, ref offset);
                int width = ReadU16(bytes, ref offset);
                int height = ReadU16(bytes, ref offset);
                byte imagePacked = ReadByte(bytes, ref offset);
                bool hasLocalPalette = (imagePacked & 0x80) != 0;
                bool interlaced = (imagePacked & 0x40) != 0;
                Color32[] palette = hasLocalPalette
                    ? ReadPalette(bytes, ref offset, 1 << ((imagePacked & 0x07) + 1))
                    : globalPalette;
                if (palette == null) throw new InvalidDataException("GIF frame has no color table.");

                int minCodeSize = ReadByte(bytes, ref offset);
                byte[] compressed = ReadSubBlocks(bytes, ref offset);
                byte[] indices = DecodeLzw(compressed, minCodeSize, width * height);
                if (interlaced) indices = Deinterlace(indices, width, height);

                Color32[] restore = gceDisposal == 3 ? (Color32[])canvas.Clone() : null;
                DrawFrame(canvas, canvasWidth, canvasHeight, left, top, width, height,
                    indices, palette, gceTransparent, gceTransparentIndex);
                frames.Add(CreateTexture(canvas, canvasWidth, canvasHeight, frames.Count));
                delays.Add(Mathf.Max(.01f, gceDelay > 0 ? gceDelay / 100f : .1f));

                previous = new FrameState
                {
                    left = left,
                    top = top,
                    width = width,
                    height = height,
                    disposal = gceDisposal,
                    restore = restore
                };
                gceDelay = 10;
                gceDisposal = 0;
                gceTransparent = false;
                gceTransparentIndex = 0;
            }
        }

        private static void ApplyPreviousDisposal(Color32[] canvas, int canvasWidth, int canvasHeight,
            Color32 background, FrameState previous)
        {
            if (previous == null) return;
            if (previous.disposal == 2)
            {
                int maxY = Mathf.Min(canvasHeight, previous.top + previous.height);
                int maxX = Mathf.Min(canvasWidth, previous.left + previous.width);
                for (int y = Mathf.Max(0, previous.top); y < maxY; y++)
                {
                    for (int x = Mathf.Max(0, previous.left); x < maxX; x++)
                        canvas[y * canvasWidth + x] = background;
                }
            }
            else if (previous.disposal == 3 && previous.restore != null && previous.restore.Length == canvas.Length)
            {
                Array.Copy(previous.restore, canvas, canvas.Length);
            }
        }

        private static void DrawFrame(Color32[] canvas, int canvasWidth, int canvasHeight,
            int left, int top, int width, int height, byte[] indices, Color32[] palette,
            bool transparent, int transparentIndex)
        {
            int source = 0;
            for (int y = 0; y < height; y++)
            {
                int canvasY = top + y;
                for (int x = 0; x < width; x++, source++)
                {
                    if (source >= indices.Length) return;
                    int canvasX = left + x;
                    int colorIndex = indices[source];
                    if (transparent && colorIndex == transparentIndex) continue;
                    if (colorIndex < 0 || colorIndex >= palette.Length) continue;
                    if (canvasX < 0 || canvasX >= canvasWidth || canvasY < 0 || canvasY >= canvasHeight) continue;
                    canvas[canvasY * canvasWidth + canvasX] = palette[colorIndex];
                }
            }
        }

        private static Texture2D CreateTexture(Color32[] topDownPixels, int width, int height, int frameIndex)
        {
            Color32[] unityPixels = new Color32[topDownPixels.Length];
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * width;
                int destinationRow = (height - 1 - y) * width;
                Array.Copy(topDownPixels, sourceRow, unityPixels, destinationRow, width);
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "ROKAS_RuntimeVnGifFrame_" + frameIndex,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(unityPixels);
            texture.Apply(false, false);
            return texture;
        }

        private static byte[] Deinterlace(byte[] source, int width, int height)
        {
            byte[] output = new byte[source.Length];
            int read = 0;
            int[] starts = { 0, 4, 2, 1 };
            int[] steps = { 8, 8, 4, 2 };
            for (int pass = 0; pass < 4; pass++)
            {
                for (int y = starts[pass]; y < height; y += steps[pass])
                {
                    int count = Math.Min(width, source.Length - read);
                    if (count <= 0) return output;
                    Array.Copy(source, read, output, y * width, count);
                    read += count;
                }
            }
            return output;
        }

        private static byte[] DecodeLzw(byte[] data, int minCodeSize, int expectedCount)
        {
            if (minCodeSize < 2 || minCodeSize > 8)
                throw new InvalidDataException("GIF LZW code size is invalid.");

            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;
            int codeSize = minCodeSize + 1;
            List<byte[]> dictionary = CreateDictionary(clearCode, endCode);
            var output = new List<byte>(Math.Max(0, expectedCount));
            byte[] previous = null;
            int bitPosition = 0;

            while (true)
            {
                int code = ReadCode(data, ref bitPosition, codeSize);
                if (code < 0) break;
                if (code == clearCode)
                {
                    dictionary = CreateDictionary(clearCode, endCode);
                    codeSize = minCodeSize + 1;
                    previous = null;
                    continue;
                }
                if (code == endCode) break;

                byte[] entry;
                if (code < dictionary.Count && dictionary[code] != null)
                {
                    entry = dictionary[code];
                }
                else if (code == dictionary.Count && previous != null && previous.Length > 0)
                {
                    entry = Append(previous, previous[0]);
                }
                else
                {
                    throw new InvalidDataException("GIF LZW stream contains an invalid code.");
                }

                output.AddRange(entry);
                if (previous != null && previous.Length > 0 && dictionary.Count < 4096)
                {
                    dictionary.Add(Append(previous, entry[0]));
                    if (dictionary.Count == (1 << codeSize) && codeSize < 12) codeSize++;
                }
                previous = entry;

                if (expectedCount > 0 && output.Count >= expectedCount) break;
            }

            if (expectedCount > 0 && output.Count < expectedCount)
                throw new InvalidDataException("GIF frame pixel data is truncated.");
            if (expectedCount > 0 && output.Count > expectedCount)
                output.RemoveRange(expectedCount, output.Count - expectedCount);
            return output.ToArray();
        }

        private static List<byte[]> CreateDictionary(int clearCode, int endCode)
        {
            var dictionary = new List<byte[]>(4096);
            for (int i = 0; i < clearCode; i++) dictionary.Add(new[] { (byte)i });
            dictionary.Add(null);
            dictionary.Add(null);
            while (dictionary.Count <= endCode) dictionary.Add(null);
            return dictionary;
        }

        private static byte[] Append(byte[] prefix, byte value)
        {
            byte[] result = new byte[prefix.Length + 1];
            Array.Copy(prefix, result, prefix.Length);
            result[result.Length - 1] = value;
            return result;
        }

        private static int ReadCode(byte[] data, ref int bitPosition, int codeSize)
        {
            if (bitPosition + codeSize > data.Length * 8) return -1;
            int code = 0;
            for (int bit = 0; bit < codeSize; bit++)
            {
                int absolute = bitPosition + bit;
                if ((data[absolute >> 3] & (1 << (absolute & 7))) != 0) code |= 1 << bit;
            }
            bitPosition += codeSize;
            return code;
        }

        private static Color32[] ReadPalette(byte[] bytes, ref int offset, int count)
        {
            Color32[] palette = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                byte r = ReadByte(bytes, ref offset);
                byte g = ReadByte(bytes, ref offset);
                byte b = ReadByte(bytes, ref offset);
                palette[i] = new Color32(r, g, b, 255);
            }
            return palette;
        }

        private static void SkipExtension(byte[] bytes, ref int offset)
        {
            int initialSize = ReadByte(bytes, ref offset);
            Ensure(bytes, offset, initialSize);
            offset += initialSize;
            while (true)
            {
                int size = ReadByte(bytes, ref offset);
                if (size == 0) break;
                Ensure(bytes, offset, size);
                offset += size;
            }
        }

        private static byte[] ReadSubBlocks(byte[] bytes, ref int offset)
        {
            var data = new List<byte>();
            while (true)
            {
                int size = ReadByte(bytes, ref offset);
                if (size == 0) break;
                Ensure(bytes, offset, size);
                for (int i = 0; i < size; i++) data.Add(bytes[offset + i]);
                offset += size;
            }
            return data.ToArray();
        }

        private static string ReadAscii(byte[] bytes, ref int offset, int count)
        {
            Ensure(bytes, offset, count);
            string value = System.Text.Encoding.ASCII.GetString(bytes, offset, count);
            offset += count;
            return value;
        }

        private static int ReadU16(byte[] bytes, ref int offset)
        {
            int low = ReadByte(bytes, ref offset);
            int high = ReadByte(bytes, ref offset);
            return low | (high << 8);
        }

        private static byte ReadByte(byte[] bytes, ref int offset)
        {
            Ensure(bytes, offset, 1);
            return bytes[offset++];
        }

        private static void Ensure(byte[] bytes, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > bytes.Length)
                throw new InvalidDataException("GIF data is truncated.");
        }
    }
}
