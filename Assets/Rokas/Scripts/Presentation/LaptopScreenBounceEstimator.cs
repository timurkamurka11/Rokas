using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rokas.Presentation
{
    /// <summary>
    /// Samples 48x27 GPU-downscaled pixels of the REAL YOMI LCD image.
    /// No blocking ReadPixels or repeated CPU image copies. Colour, intensity
    /// and highlight energy are derived from displayed pixels.
    /// </summary>
    public sealed class LaptopScreenBounceEstimator : IDisposable
    {
        private const int SampleWidth = 48;
        private const int SampleHeight = 27;
        private const float SampleInterval = .12f;
        public const float BloomThreshold = .58f;
        public const float BloomSoftKnee = .22f;
        private RenderTexture sampleTarget;
        private AsyncGPUReadbackRequest pendingRead;
        private bool readPending;
        private float sinceSample = SampleInterval;
        private float targetAmount;
        private float targetBloom;
        private float targetSpecular;
        private float targetSideBias;
        private Color targetTint = new Color(.18f, .21f, .24f, 1f);
        private float intensity;
        private float bloom;
        private float specular;
        private float sideBias;
        private Color tint = new Color(.18f, .21f, .24f, 1f);
        private bool disposed;

        public float Intensity => intensity;
        public float Bloom => bloom;
        public float Specular => specular;
        public float SideBias => sideBias;
        public Color Tint => tint;

        // Exposed as a pure mapping for focused EditMode tests.
        public static void EvaluateMeanScreenColor(Color mean,
            out Color reflected, out float amount)
        {
            float r = Mathf.Clamp01(mean.r);
            float g = Mathf.Clamp01(mean.g);
            float b = Mathf.Clamp01(mean.b);
            float luma = .2126f * r + .7152f * g + .0722f * b;
            // Neutral content must stay neutral; YOMI blue remains cool
            // without a constant blue floor or excessive brightness.
            reflected = new Color(
                Mathf.Clamp01(.15f + .66f * r),
                Mathf.Clamp01(.16f + .64f * g),
                Mathf.Clamp01(.17f + .63f * b), 1f);
            amount = Mathf.Clamp(.10f + luma, .10f, .70f);
        }


        /// <summary>
        /// A soft-knee bright-pass using luminance of sampled YOMI pixels.
        /// Dark content yields zero bloom; sharp white UI highlights are
        /// compressed so the desktop does not flare like a flashlight.
        /// </summary>
        public static float BrightPass(float luma)
        {
            luma = Mathf.Clamp01(luma);
            float lo = BloomThreshold - BloomSoftKnee * .5f;
            float hi = BloomThreshold + BloomSoftKnee * .5f;
            float t = Mathf.Clamp01((luma - lo) / (hi - lo));
            float soft = t * t * (3f - 2f * t);
            float hard = Mathf.Max(0f,
                (luma - BloomThreshold) / (1f - BloomThreshold));
            return Mathf.Clamp01(soft * .45f + hard * .55f);
        }

        public static void EvaluateHighlightResponse(float averageBrightness,
            float highlightEnergy, out float bloomAmount, out float specAmount)
        {
            float energy = Mathf.Clamp01(highlightEnergy);
            float luminance = Mathf.Clamp01(averageBrightness);
            bloomAmount = Mathf.Clamp01(energy * 2.30f + luminance * .09f);
            specAmount = Mathf.Clamp01(energy * 1.85f + luminance * .22f);
        }


        /// <summary>Screen left/right lighting contrast, limited to 30%.
        /// A moving bright YOMI panel now moves the desk reflection.</summary>
        public static float EvaluateSideBias(float leftEnergy, float rightEnergy)
        {
            float total = Mathf.Max(.00001f,
                Mathf.Max(0f,leftEnergy) + Mathf.Max(0f,rightEnergy));
            return Mathf.Clamp((rightEnergy-leftEnergy)/total*1.2f,
                -.30f, .30f);
        }

        /// <summary>Photo-space falloff mirrored by the room material.
        /// Center is just in front of the laptop, not at the table edge.</summary>
        public static float EvaluateSurfaceAttenuation(Vector2 p)
        {
            float x = (p.x - 830f) / 505f;
            float y = (p.y - 746f) / 216f;
            float d = x * x + y * y;
            float t = Mathf.Clamp01((d - .10f) / 1.13f);
            float radial = 1f - t * t * (3f - 2f * t);
            float v = Mathf.Clamp01((p.y - 793f) / 142f);
            float front = 1f - .73f * v * v * (3f - 2f * v);
            return Mathf.Clamp01(radial * front);
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
                        float lumaSum = 0f;
                        float highlightSum = 0f;
                        float leftLight = 0f, rightLight = 0f;
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            r += pixels[i].r;
                            g += pixels[i].g;
                            b += pixels[i].b;
                            float linearR = pixels[i].r / 255f;
                            float linearG = pixels[i].g / 255f;
                            float linearB = pixels[i].b / 255f;
                            float luma = .2126f * linearR +
                                .7152f * linearG + .0722f * linearB;
                            lumaSum += luma;
                            highlightSum += BrightPass(luma);
                            if ((i % SampleWidth) < SampleWidth / 2)
                                leftLight += luma;
                            else rightLight += luma;
                        }
                        float inv = 1f / (255f * pixels.Length);
                        EvaluateMeanScreenColor(
                            new Color(r * inv, g * inv, b * inv, 1f),
                            out targetTint, out targetAmount);
                        EvaluateHighlightResponse(
                            lumaSum / pixels.Length,
                            highlightSum / pixels.Length,
                            out targetBloom, out targetSpecular);
                        targetSideBias = EvaluateSideBias(leftLight, rightLight);
                    }
                }
            }

            sinceSample += dt;
            if (powered && source && SystemInfo.supportsAsyncGPUReadback &&
                !readPending && sinceSample >= SampleInterval)
            {
                if (!sampleTarget)
                {
                    sampleTarget = new RenderTexture(SampleWidth, SampleHeight, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    {
                        name = "ROKAS YOMI Screen Readback 48x27",
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

            float ease = 1f - Mathf.Exp(-dt / .23f);
            float required = powered ? targetAmount : 0f;
            intensity = Mathf.Lerp(intensity, required, ease);
            bloom = Mathf.Lerp(bloom, powered ? targetBloom : 0f, ease);
            specular = Mathf.Lerp(specular, powered ? targetSpecular : 0f, ease);
            sideBias = Mathf.Lerp(sideBias, powered ? targetSideBias : 0f, ease);
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
                // Detach this owned target before releasing GPU backing.
                // Releasing RenderTexture.active triggers a Unity warning.
                if (RenderTexture.active == sampleTarget)
                    RenderTexture.active = null;
                if (sampleTarget.IsCreated())
                    sampleTarget.Release();
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(sampleTarget);
                else
                    UnityEngine.Object.DestroyImmediate(sampleTarget);
                sampleTarget = null;
            }
            intensity = 0f;
            bloom = 0f;
            specular = 0f;
            sideBias = 0f;
        }
    }
}
