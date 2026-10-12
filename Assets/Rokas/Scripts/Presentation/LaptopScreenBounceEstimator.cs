using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rokas.Presentation
{
    /// <summary>
    /// Samples a 16x16 GPU-downscaled copy of the REAL YOMI LCD image.
    /// No blocking ReadPixels and no per-frame CPU texture copies.
    /// Every 0.25 s its mean color determines the reflected light tint,
    /// then a 0.28 s transition removes flicker from UI changes.
    /// </summary>
    public sealed class LaptopScreenBounceEstimator : IDisposable
    {
        private const int SampleSize = 16;
        private const float SampleInterval = .25f;
        private RenderTexture sampleTarget;
        private AsyncGPUReadbackRequest pendingRead;
        private bool readPending;
        private float sinceSample = SampleInterval;
        private float targetAmount = .27f;
        private Color targetTint = new Color(.18f, .21f, .24f, 1f);
        private float intensity;
        private Color tint = new Color(.18f, .21f, .24f, 1f);
        private bool disposed;

        public float Intensity => intensity;
        public Color Tint => tint;

        // Exposed as a pure mapping for focused EditMode tests.
        public static void EvaluateMeanScreenColor(Color mean,
            out Color reflected, out float amount)
        {
            float r = Mathf.Clamp01(mean.r);
            float g = Mathf.Clamp01(mean.g);
            float b = Mathf.Clamp01(mean.b);
            float luma = .2126f * r + .7152f * g + .0722f * b;
            // Secondary bounce: colors are neutralized rather than copied
            // onto the wood as an emissive screen-shaped neon rectangle.
            reflected = Color.Lerp(new Color(.20f, .21f, .22f, 1f),
                new Color(r, g, b, 1f), .36f);
            reflected.a = 1f;
            amount = Mathf.Clamp(.15f + luma * 1.20f, .16f, .88f);
        }

        public void Tick(Texture source, bool powered, float deltaTime)
        {
            if (disposed) return;
            float dt = Mathf.Max(0f, deltaTime);
            if (readPending && pendingRead.done)
            {
                readPending = false;
                if (!pendingRead.hasError)
                {
                    var pixels = pendingRead.GetData<Color32>();
                    if (pixels.Length > 0)
                    {
                        float r = 0f, g = 0f, b = 0f;
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            r += pixels[i].r;
                            g += pixels[i].g;
                            b += pixels[i].b;
                        }
                        float inv = 1f / (255f * pixels.Length);
                        EvaluateMeanScreenColor(
                            new Color(r * inv, g * inv, b * inv, 1f),
                            out targetTint, out targetAmount);
                    }
                }
            }

            sinceSample += dt;
            if (powered && source && SystemInfo.supportsAsyncGPUReadback &&
                !readPending && sinceSample >= SampleInterval)
            {
                if (!sampleTarget)
                {
                    sampleTarget = new RenderTexture(SampleSize, SampleSize, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    {
                        name = "ROKAS YOMI Screen Bounce 16x16",
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    sampleTarget.Create();
                }
                Graphics.Blit(source, sampleTarget);
                pendingRead = AsyncGPUReadback.Request(
                    sampleTarget, 0, TextureFormat.RGBA32);
                readPending = true;
                sinceSample = 0f;
            }

            float ease = 1f - Mathf.Exp(-dt / .28f);
            float required = powered ? targetAmount : 0f;
            intensity = Mathf.Lerp(intensity, required, ease);
            tint = Color.Lerp(tint, targetTint, ease);
            if (Mathf.Abs(intensity - required) < .0005f) intensity = required;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (readPending)
            {
                pendingRead.WaitForCompletion();
                readPending = false;
            }
            if (sampleTarget)
            {
                sampleTarget.Release();
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(sampleTarget);
                else
                    UnityEngine.Object.DestroyImmediate(sampleTarget);
                sampleTarget = null;
            }
            intensity = 0f;
        }
    }
}
