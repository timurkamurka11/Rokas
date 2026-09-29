using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static partial class VnPresentationWorkshopPreviewRenderer
    {
        private static readonly ConditionalWeakTable<VnWorkshopPreviewFrame, VnSceneComposerPlaybackFrame> PlaybackFrames =
            new ConditionalWeakTable<VnWorkshopPreviewFrame, VnSceneComposerPlaybackFrame>();

        internal static void RegisterPlaybackFrame(VnSceneComposerPlaybackFrame playbackFrame)
        {
            if (playbackFrame == null || playbackFrame.WorkshopFrame == null) return;
            PlaybackFrames.Remove(playbackFrame.WorkshopFrame);
            PlaybackFrames.Add(playbackFrame.WorkshopFrame, playbackFrame);
        }

        public static void Draw(Rect previewRect, VnSceneComposerPlaybackFrame playbackFrame,
            VnWorkshopElement? selected = null, bool showHitRegions = true)
        {
            if (playbackFrame == null) throw new ArgumentNullException(nameof(playbackFrame));
            if (playbackFrame.WorkshopFrame == null) throw new ArgumentException("Playback frame has no Workshop frame.", nameof(playbackFrame));
            RegisterPlaybackFrame(playbackFrame);
            Draw(previewRect, playbackFrame.WorkshopFrame, selected, showHitRegions);
        }

        private static bool TryDrawRegisteredPlaybackBackground(Rect localCanvas, VnWorkshopPreviewFrame frame)
        {
            if (frame == null || !PlaybackFrames.TryGetValue(frame, out VnSceneComposerPlaybackFrame playbackFrame))
                return false;
            DrawPlaybackBackground(localCanvas, playbackFrame);
            return true;
        }

        private static bool ShouldDrawRegisteredPlaybackDialogue(VnWorkshopPreviewFrame frame)
        {
            return ShouldDrawRegisteredPlaybackDialoguePanel(frame);
        }

        private static bool ShouldDrawRegisteredPlaybackDialoguePanel(
            VnWorkshopPreviewFrame frame)
        {
            return frame == null ||
                   !PlaybackFrames.TryGetValue(
                       frame, out VnSceneComposerPlaybackFrame playbackFrame) ||
                   playbackFrame.ShowDialoguePanel;
        }

        private static bool ShouldDrawRegisteredPlaybackDialogueText(
            VnWorkshopPreviewFrame frame)
        {
            return frame == null ||
                   !PlaybackFrames.TryGetValue(
                       frame, out VnSceneComposerPlaybackFrame playbackFrame) ||
                   playbackFrame.ShowDialogueText;
        }

        private static bool ShouldDrawRegisteredPlaybackCharacters(
            VnWorkshopPreviewFrame frame)
        {
            return frame == null ||
                   !PlaybackFrames.TryGetValue(
                       frame, out VnSceneComposerPlaybackFrame playbackFrame) ||
                   playbackFrame.ShowCharacters;
        }

        private static void TryDrawRegisteredSceneTransitionOverlay(Rect localCanvas, VnWorkshopPreviewFrame frame)
        {
            if (frame == null ||
                !PlaybackFrames.TryGetValue(frame, out VnSceneComposerPlaybackFrame playbackFrame) ||
                playbackFrame.SceneTransitionOverlay == null ||
                !playbackFrame.SceneTransitionOverlay.Active)
                return;
            DrawSceneTransitionOverlay(localCanvas, playbackFrame.SceneTransitionOverlay);
        }

        private static void TryDrawRegisteredTerminalFadeOverlay(
            Rect localCanvas, VnWorkshopPreviewFrame frame)
        {
            if (frame == null ||
                !PlaybackFrames.TryGetValue(
                    frame, out VnSceneComposerPlaybackFrame playbackFrame))
                return;

            float alpha = Mathf.Clamp01(playbackFrame.TerminalFadeAlpha);
            if (alpha <= .0001f) return;

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(
                localCanvas, Texture2D.whiteTexture,
                ScaleMode.StretchToFill, false);
            GUI.color = previous;
        }

        private static VnSceneComposerReplicaEffectSample GetRegisteredReplicaEffectSample(
            VnWorkshopPreviewFrame frame)
        {
            return frame != null && PlaybackFrames.TryGetValue(frame, out VnSceneComposerPlaybackFrame playbackFrame)
                ? playbackFrame.ReplicaEffect
                : new VnSceneComposerReplicaEffectSample { Scale = 1f };
        }

        private static float GetRegisteredForegroundAlpha(VnWorkshopPreviewFrame frame)
        {
            return frame != null && PlaybackFrames.TryGetValue(frame, out VnSceneComposerPlaybackFrame playbackFrame)
                ? playbackFrame.ForegroundAlpha : 1f;
        }

        private static void DrawSceneTransitionOverlay(
            Rect rect, VnSceneComposerSceneTransitionOverlaySample sample)
        {
            float coverage = Mathf.Clamp01(sample.Coverage);
            if (coverage <= .0001f) return;
            Color previous = GUI.color;
            if (sample.Mode == VnSceneComposerSceneTransitionType.Fade)
            {
                GUI.color = new Color(.015f, .015f, .02f, coverage);
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
                GUI.color = previous;
                return;
            }

            float width = rect.width * coverage;
            bool reveal = sample.Phase == VnSceneComposerSceneTransitionPhase.Reveal;
            bool anchorRight =
                (!reveal && sample.Direction == VnSceneComposerSceneTransitionDirection.RightToLeft) ||
                (reveal && sample.Direction == VnSceneComposerSceneTransitionDirection.LeftToRight);
            Rect curtain = anchorRight
                ? new Rect(rect.xMax - width, rect.y, width, rect.height)
                : new Rect(rect.x, rect.y, width, rect.height);

            GUI.color = new Color(.015f, .015f, .02f, 1f);
            GUI.DrawTexture(curtain, Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
            GUI.color = previous;
        }

        private static void DrawPlaybackBackground(Rect rect, VnSceneComposerPlaybackFrame frame)
        {
            VnWorkshopBackgroundTransitionSample sample = frame.ComposerBackgroundTransition;
            Texture source = frame.SourceBackground ?? frame.TargetBackground ?? frame.WorkshopFrame.BackgroundTexture;
            Texture target = frame.TargetBackground ?? frame.SourceBackground ?? frame.WorkshopFrame.BackgroundTexture;
            if (source == null && target == null) return;

            if (sample.CurtainCoverage > .0001f)
            {
                bool useTarget = sample.TargetAlpha >= sample.SourceAlpha;
                Texture baseTexture = useTarget ? target : source;
                VnSceneComposerMediaScaleMode baseScaleMode = useTarget ? frame.TargetScaleMode : frame.SourceScaleMode;
                GUI.DrawTexture(rect, baseTexture, VnSceneComposerMediaEditing.ToUnityScaleMode(baseScaleMode), false);
                float coverage = Mathf.Clamp01(sample.CurtainCoverage);
                float width = rect.width * coverage;
                Rect curtain = sample.CurtainDirection == VnWorkshopCurtainDirection.RightToLeft
                    ? new Rect(rect.xMax - width, rect.y, width, rect.height)
                    : new Rect(rect.x, rect.y, width, rect.height);
                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(sample.CurtainDarkness));
                GUI.DrawTexture(curtain, Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
                GUI.color = previous;
                return;
            }

            if (frame.SceneTransitionOverlay.Active &&
                frame.SceneTransitionOverlay.Mode == VnSceneComposerSceneTransitionType.Fade)
            {
                // ComposePlaybackBackground uses a normalized two-image blend. Drawing
                // both textures at half opacity would leave a quarter of the backdrop.
                float total = sample.SourceAlpha + sample.TargetAlpha;
                DrawTextureAlpha(rect, source, frame.SourceScaleMode, 1f);
                DrawTextureAlpha(rect, target, frame.TargetScaleMode,
                    total <= .0001f ? 1f : sample.TargetAlpha / total);
            }
            else
            {
                DrawTextureAlpha(rect, source, frame.SourceScaleMode, sample.SourceAlpha);
                DrawTextureAlpha(rect, target, frame.TargetScaleMode, sample.TargetAlpha);
            }
        }

        private static void DrawTextureAlpha(Rect rect, Texture texture,
            VnSceneComposerMediaScaleMode scaleMode, float alpha)
        {
            if (texture == null || alpha <= .0001f) return;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(rect, texture, VnSceneComposerMediaEditing.ToUnityScaleMode(scaleMode), false);
            GUI.color = previous;
        }

        public static Texture2D ComposePlaybackBackground(VnSceneComposerPlaybackFrame playbackFrame, int width, int height)
        {
            if (playbackFrame == null) throw new ArgumentNullException(nameof(playbackFrame));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Texture sourceTexture = playbackFrame.SourceBackground ?? playbackFrame.TargetBackground ?? playbackFrame.WorkshopFrame?.BackgroundTexture;
            Texture targetTexture = playbackFrame.TargetBackground ?? playbackFrame.SourceBackground ?? playbackFrame.WorkshopFrame?.BackgroundTexture;
            Texture2D source = CreateReadableScaledCopy(sourceTexture, width, height);
            Texture2D target = CreateReadableScaledCopy(targetTexture, width, height);
            try
            {
                var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color32[] sourcePixels = source.GetPixels32();
                Color32[] targetPixels = target.GetPixels32();
                Color32[] outputPixels = new Color32[sourcePixels.Length];
                VnWorkshopBackgroundTransitionSample sample = playbackFrame.ComposerBackgroundTransition;
                bool curtain = sample.CurtainCoverage > .0001f;
                float sourceAlpha = Mathf.Clamp01(sample.SourceAlpha);
                float targetAlpha = Mathf.Clamp01(sample.TargetAlpha);
                float total = sourceAlpha + targetAlpha;
                float targetWeight = total <= .0001f ? 1f : targetAlpha / total;
                float coverage = Mathf.Clamp01(sample.CurtainCoverage);
                float darkness = Mathf.Clamp01(sample.CurtainDarkness);

                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color color;
                    if (curtain)
                    {
                        color = sample.TargetAlpha >= sample.SourceAlpha ? targetPixels[index] : sourcePixels[index];
                        float normalizedX = (x + .5f) / width;
                        bool covered = sample.CurtainDirection == VnWorkshopCurtainDirection.RightToLeft
                            ? normalizedX >= 1f - coverage
                            : normalizedX <= coverage;
                        if (covered) color = Color.Lerp(color, Color.black, darkness);
                    }
                    else if (targetAlpha <= .0001f) color = sourcePixels[index];
                    else if (sourceAlpha <= .0001f) color = targetPixels[index];
                    else color = Color.Lerp(sourcePixels[index], targetPixels[index], targetWeight);
                    outputPixels[index] = color;
                }
                output.SetPixels32(outputPixels);
                output.Apply(false, false);
                return output;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Texture2D CreateReadableScaledCopy(Texture texture, int width, int height)
        {
            if (texture == null)
            {
                var empty = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color32[] clear = new Color32[width * height];
                empty.SetPixels32(clear); empty.Apply();
                return empty;
            }

            Texture2D readable = null;
            bool ownsReadable = false;
            if (texture is Texture2D sourceTexture)
            {
                string path = AssetDatabase.GetAssetPath(sourceTexture);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    ownsReadable = true;
                    if (!ImageConversion.LoadImage(readable, File.ReadAllBytes(path), false))
                    {
                        UnityEngine.Object.DestroyImmediate(readable);
                        readable = null;
                        ownsReadable = false;
                    }
                }
                if (readable == null)
                {
                    try
                    {
                        sourceTexture.GetPixelBilinear(.5f, .5f);
                        readable = sourceTexture;
                    }
                    catch (UnityException) { }
                }
            }

            if (readable == null)
            {
                RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    Graphics.Blit(texture, temporary);
                    RenderTexture.active = temporary;
                    readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    ownsReadable = true;
                    readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                    readable.Apply(false, false);
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(temporary);
                }
            }

            var scaled = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                scaled.SetPixel(x, y, readable.GetPixelBilinear((x + .5f) / width, (y + .5f) / height));
            scaled.Apply(false, false);
            if (ownsReadable) UnityEngine.Object.DestroyImmediate(readable);
            return scaled;
        }
    }
}
