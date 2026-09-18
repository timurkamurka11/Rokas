using System;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;
using UnityEngine.Video;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        private sealed class Mc3InspectableVideoPreview : VnSceneComposerVideoPreview
        {
            internal Mc3InspectableVideoPreview(string path, int width, int height, bool loop)
                : base(path, width, height, loop) { }
        }

        [Test]
        public void MC3_LiveVideoPreviewUsesFilteredSamplingForScaledDisplay()
        {
            string path = CreateMc3ExistingDummyVideoPath();
            VnSceneComposerVideoPreview preview = null;
            try
            {
                preview = new Mc3InspectableVideoPreview(path, 640, 360, false);
                Assert.That(preview.texture, Is.Not.Null);
                Assert.That(preview.texture.filterMode, Is.EqualTo(FilterMode.Bilinear),
                    "Live video is routinely displayed at a different size from its prepared RenderTexture. " +
                    "Nearest-neighbour Point sampling creates avoidable blocky/aliased degradation while scaling.");
            }
            finally
            {
                preview?.Dispose();
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void MC3_OuterRendererIsTheSinglePresentationAspectAuthority()
        {
            string path = CreateMc3ExistingDummyVideoPath();
            VnSceneComposerVideoPreview preview = null;
            try
            {
                preview = new Mc3InspectableVideoPreview(path, 640, 360, false);
                FieldInfo playerField = typeof(VnSceneComposerVideoPreview).GetField(
                    "player", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(playerField, Is.Not.Null);
                VideoPlayer player = playerField.GetValue(preview) as VideoPlayer;
                Assert.That(player, Is.Not.Null);
                Assert.That(player.renderMode, Is.EqualTo(VideoRenderMode.RenderTexture));
                Assert.That(player.aspectRatio, Is.EqualTo(VideoAspectRatio.Stretch),
                    "The prepared RenderTexture already owns source-aspect geometry. " +
                    "VideoPlayer must fill that target; creator-facing Fit/Fill/Stretch belongs only to the outer renderer.");
            }
            finally
            {
                preview?.Dispose();
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void MC3_FitFillAndStretchProduceExpectedFinalDrawGeometry()
        {
            Rect landscapeTarget = new Rect(0f, 0f, 1000f, 1000f);
            Rect fitScreen = Rect.zero;
            Rect fitSource = Rect.zero;
            GUI.CalculateScaledTextureRects(
                landscapeTarget,
                VnSceneComposerMediaEditing.ToUnityScaleMode(VnSceneComposerMediaScaleMode.Fit),
                16f / 9f,
                ref fitScreen,
                ref fitSource);

            Assert.That(fitScreen.width / fitScreen.height, Is.EqualTo(16f / 9f).Within(.002f),
                "Fit must preserve a 16:9 source inside a square viewport.");
            Assert.That(fitSource, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                "Fit must show the complete source rather than crop it.");

            Rect portraitTarget = new Rect(0f, 0f, 1600f, 900f);
            Rect fillScreen = Rect.zero;
            Rect fillSource = Rect.zero;
            GUI.CalculateScaledTextureRects(
                portraitTarget,
                VnSceneComposerMediaEditing.ToUnityScaleMode(VnSceneComposerMediaScaleMode.Fill),
                9f / 16f,
                ref fillScreen,
                ref fillSource);

            Assert.That(fillScreen, Is.EqualTo(portraitTarget),
                "Fill must cover the whole viewport.");
            Assert.That(fillSource.width, Is.EqualTo(1f).Within(.002f));
            Assert.That(fillSource.height, Is.LessThan(1f),
                "Portrait Fill in a landscape viewport must crop source height instead of stretching.");

            Rect ultrawideTarget = new Rect(0f, 0f, 1000f, 700f);
            Rect ultrawideFitScreen = Rect.zero;
            Rect ultrawideFitSource = Rect.zero;
            GUI.CalculateScaledTextureRects(
                ultrawideTarget,
                VnSceneComposerMediaEditing.ToUnityScaleMode(VnSceneComposerMediaScaleMode.Fit),
                2560f / 1080f,
                ref ultrawideFitScreen,
                ref ultrawideFitSource);
            Assert.That(ultrawideFitScreen.width / ultrawideFitScreen.height,
                Is.EqualTo(2560f / 1080f).Within(.002f),
                "Non-16:9 Fit must preserve the original source aspect.");

            Rect stretchScreen = Rect.zero;
            Rect stretchSource = Rect.zero;
            GUI.CalculateScaledTextureRects(
                landscapeTarget,
                VnSceneComposerMediaEditing.ToUnityScaleMode(VnSceneComposerMediaScaleMode.Stretch),
                16f / 9f,
                ref stretchScreen,
                ref stretchSource);
            Assert.That(stretchScreen, Is.EqualTo(landscapeTarget));
            Assert.That(stretchScreen.width / stretchScreen.height, Is.Not.EqualTo(16f / 9f).Within(.002f),
                "Stretch is the explicit mode allowed to break source aspect.");
        }

        [Test]
        public void MC3_PreparedTargetsDoNotNeedlesslyResample1080p720pOrUltrawideGeometry()
        {
            MethodInfo resolve = typeof(VnSceneComposerVideoPreview).GetMethod(
                "ResolvePreparedRenderSize",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            Assert.That(resolve, Is.Not.Null);

            Vector2Int hd1080 = (Vector2Int)resolve.Invoke(null, new object[] { 1920, 1080 });
            Vector2Int hd720 = (Vector2Int)resolve.Invoke(null, new object[] { 1280, 720 });
            Vector2Int portrait = (Vector2Int)resolve.Invoke(null, new object[] { 1080, 1920 });
            Vector2Int ultrawide = (Vector2Int)resolve.Invoke(null, new object[] { 2560, 1080 });

            Assert.That(hd1080, Is.EqualTo(new Vector2Int(1920, 1080)),
                "A 1080p source must not pass through a lower-resolution prepared target.");
            Assert.That(hd720, Is.EqualTo(new Vector2Int(1280, 720)),
                "A 720p source must not be needlessly upscaled or downscaled before display.");
            Assert.That(portrait, Is.EqualTo(new Vector2Int(1080, 1920)),
                "Portrait 1080x1920 must retain its full prepared geometry.");
            Assert.That(ultrawide, Is.EqualTo(new Vector2Int(1920, 810)),
                "Ultrawide preview may be bounded, but the bound must preserve source aspect.");
            Assert.That((float)ultrawide.x / ultrawide.y, Is.EqualTo(2560f / 1080f).Within(.002f));
        }

        [Test]
        public void MC3_RepositoryReferenceMp4ProvidesRealSourceGeometryEvidence()
        {
            string path = Path.Combine(Application.dataPath, "StreamingAssets", "RokasVideo", "StartupPreview.mp4");
            Assert.That(File.Exists(path), Is.True, "Representative repository MP4 is required for M-C3 source-geometry evidence.");

            byte[] bytes = File.ReadAllBytes(path);
            Vector2Int geometry = ReadMp4TrackHeaderGeometry(bytes);
            string codec = FindMp4CodecTag(bytes);

            Assert.That(geometry.x, Is.GreaterThan(0), "Could not resolve MP4 video width from tkhd metadata.");
            Assert.That(geometry.y, Is.GreaterThan(0), "Could not resolve MP4 video height from tkhd metadata.");
            TestContext.WriteLine("MC3_SOURCE path=StartupPreview.mp4 container=mp4 width=" + geometry.x +
                                  " height=" + geometry.y + " aspect=" +
                                  (geometry.x / (double)geometry.y).ToString("0.0000") +
                                  " codec=" + codec + " bytes=" + bytes.Length);
        }

        private static string CreateMc3ExistingDummyVideoPath()
        {
            string path = Path.Combine(Path.GetTempPath(), "ROKAS_MC3_" + Guid.NewGuid().ToString("N") + ".mp4");
            File.WriteAllBytes(path, new byte[] { 0, 0, 0, 0 });
            return path;
        }

        private static Vector2Int ReadMp4TrackHeaderGeometry(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 16) return Vector2Int.zero;
            byte[] marker = Encoding.ASCII.GetBytes("tkhd");
            for (int i = 4; i <= bytes.Length - marker.Length; i++)
            {
                if (!Matches(bytes, i, marker)) continue;
                int boxStart = i - 4;
                uint boxSize = ReadUInt32BigEndian(bytes, boxStart);
                if (boxSize < 16 || boxSize > int.MaxValue) continue;
                int boxEnd = boxStart + (int)boxSize;
                if (boxEnd > bytes.Length || boxEnd - 8 < 0) continue;
                uint widthFixed = ReadUInt32BigEndian(bytes, boxEnd - 8);
                uint heightFixed = ReadUInt32BigEndian(bytes, boxEnd - 4);
                int width = (int)(widthFixed >> 16);
                int height = (int)(heightFixed >> 16);
                if (width > 0 && height > 0) return new Vector2Int(width, height);
            }
            return Vector2Int.zero;
        }

        private static string FindMp4CodecTag(byte[] bytes)
        {
            string[] tags = { "avc1", "hvc1", "hev1", "av01", "vp09", "mp4v" };
            foreach (string tag in tags)
            {
                if (IndexOfAscii(bytes, tag) >= 0) return tag;
            }
            return "unknown";
        }

        private static int IndexOfAscii(byte[] bytes, string value)
        {
            if (bytes == null || string.IsNullOrEmpty(value)) return -1;
            byte[] marker = Encoding.ASCII.GetBytes(value);
            for (int i = 0; i <= bytes.Length - marker.Length; i++)
                if (Matches(bytes, i, marker)) return i;
            return -1;
        }

        private static bool Matches(byte[] bytes, int offset, byte[] marker)
        {
            if (bytes == null || marker == null || offset < 0 || offset + marker.Length > bytes.Length) return false;
            for (int i = 0; i < marker.Length; i++)
                if (bytes[offset + i] != marker[i]) return false;
            return true;
        }

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            if (bytes == null || offset < 0 || offset + 4 > bytes.Length) return 0;
            return ((uint)bytes[offset] << 24) |
                   ((uint)bytes[offset + 1] << 16) |
                   ((uint)bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }
    }
}
