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
            float luma = .2126f*r + .7152f*g + .0722f*b;
            // Chromatic adaptation: bright YOMI icons survive a dark
            // wallpaper without any constant blue-tinted light.
            const float desaturate = .30f;
            float cr = Mathf.Lerp(r,luma,desaturate);
            float cg = Mathf.Lerp(g,luma,desaturate);
            float cb = Mathf.Lerp(b,luma,desaturate);
            float peak = Mathf.Max(Mathf.Max(cr,cg),Mathf.Max(cb,.001f));
            float carrier = Mathf.Lerp(.24f,.88f,Mathf.Sqrt(luma));
            reflected = new Color(
                Mathf.Clamp01(Mathf.Lerp(cr/peak*carrier,
                    .06f+.82f*cr,.43f)),
                Mathf.Clamp01(Mathf.Lerp(cg/peak*carrier,
                    .06f+.82f*cg,.43f)),
                Mathf.Clamp01(Mathf.Lerp(cb/peak*carrier,
                    .06f+.82f*cb,.43f)),1f);
            // The shader expects a black-LCD baseline of exactly .10.
            amount = Mathf.Clamp(.10f+Mathf.Pow(luma,.82f)*.76f,
                .10f,.86f);
        }

        /// <summary>Emissive weight for each downsampled YOMI pixel.</summary>
        public static float EvaluateEmissiveWeight(float luma)
        {
            float y=Mathf.Clamp01(luma);
            return .14f+y*1.65f+BrightPass(y)*2.25f;
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
            float compressedEnergy = 1f - Mathf.Exp(-energy * 2.4f);
            bloomAmount = Mathf.Clamp01(compressedEnergy * .82f + luminance * luminance * .12f);
            specAmount = Mathf.Clamp01(compressedEnergy * .88f + luminance * .16f);
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
            // Wood-only footprint, mirrored exactly in the V11 shader.
            // Preserve the established V10 y855 alignment to real wood.
            float forwardT=Mathf.Clamp01((p.y-668f)/273f);
            float halfWidth=Mathf.Lerp(188f,266f,forwardT);
            float x=Mathf.Max(Mathf.Abs(p.x-824f)-halfWidth,0f)/335f;
            float y=(p.y-855f)/210f;
            float d=x*x+y*y;
            float invSq=1f/(1f+.88f*d);
            float t=Mathf.Clamp01((d-.80f)/1.30f);
            float radial=invSq*(1f-t*t*(3f-2f*t));
            float f=Mathf.Clamp01((p.y-875f)/66f);
            float front=1f-.96f*f*f*(3f-2f*f);
            return Mathf.Clamp01(radial*front);
        }

        /// <summary>Perceptual LCD exposure, zero for a black screen.
        /// Must match _ScreenBounceAmount transformation in the shader.</summary>
        public static float EvaluateVisibleEnergy(float screenAmount)
        {
            return Mathf.Clamp01((screenAmount - .10f) * 3.25f);
        }

        public void Tick(Texture source, bool powered, float deltaTime)
        {
            if (disposed) return;
            float dt = Mathf.Max(0f, deltaTime);
            if (readPending && pendingRead.done)
            {
                readPending = false;
                if (!pendingRead.hasError && powered)
                {
                    var pixels = pendingRead.GetData<Color32>();
                    if (pixels.Length > 0)
                    {
                        float r = 0f, g = 0f, b = 0f;
                        float chromaWeightSum = 0f;
                        float lumaSum = 0f;
                        float emissionSum = 0f;
                        float highlightSum = 0f;
                        float formFactorSum = 0f;
                        float leftLight = 0f, rightLight = 0f;
                        for (int i = 0; i < pixels.Length; i++)
                        {
                            int px = i % SampleWidth;
                            int py = i / SampleWidth;
                            float u = (px + .5f) / SampleWidth;
                            float v = (py + .5f) / SampleHeight;
                            // Lower LCD scanlines sit physically closer to the
                            // keyboard and wooden tabletop than the top bezel.
                            float verticalWeight = Mathf.Lerp(.72f, 1.28f, v);
                            float linearR = pixels[i].r / 255f;
                            float linearG = pixels[i].g / 255f;
                            float linearB = pixels[i].b / 255f;
                            float luma = .2126f * linearR +
                                .7152f * linearG + .0722f * linearB;
                            float hi = BrightPass(luma);
                            float chromaWeight =
                                EvaluateEmissiveWeight(luma)*verticalWeight;
                            r += linearR * chromaWeight;
                            g += linearG * chromaWeight;
                            b += linearB * chromaWeight;
                            chromaWeightSum += chromaWeight;
                            lumaSum += luma * verticalWeight;
                            emissionSum += luma*chromaWeight;
                            highlightSum += hi * verticalWeight;
                            formFactorSum += verticalWeight;
                            float lateralEnergy = (luma + hi * 1.6f) * verticalWeight *
                                (.5f + Mathf.Abs(u - .5f));
                            if (px < SampleWidth / 2)
                                leftLight += lateralEnergy;
                            else rightLight += lateralEnergy;
                        }
                        float invChroma = 1f / Mathf.Max(.0001f, chromaWeightSum);
                        float invForm = 1f / Mathf.Max(.0001f, formFactorSum);
                        float averageLuma=lumaSum*invForm;
                        float effectiveLuma=Mathf.Lerp(averageLuma,
                            emissionSum*invChroma,.57f);
                        float weightedR=r*invChroma;
                        float weightedG=g*invChroma;
                        float weightedB=b*invChroma;
                        float weightedLuma=Mathf.Max(.0001f,
                            .2126f*weightedR+.7152f*weightedG+
                            .0722f*weightedB);
                        float scale=effectiveLuma/weightedLuma;
                        EvaluateMeanScreenColor(
                            new Color(weightedR*scale,weightedG*scale,
                                weightedB*scale,1f),
                            out targetTint,out targetAmount);
                        EvaluateHighlightResponse(
                            effectiveLuma,
                            Mathf.Max(highlightSum*invForm,
                                effectiveLuma*.23f),
                            out targetBloom,out targetSpecular);
                        targetSideBias = EvaluateSideBias(leftLight, rightLight);
                    }
                }
            }

            if (!powered)
            {
                targetAmount = 0f;
                targetBloom = 0f;
                targetSpecular = 0f;
                targetSideBias = 0f;
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
                        hideFlags = HideFlags.DontSave,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    sampleTarget.Create();
                }
                // Graphics.Blit assigns RenderTexture.active = sampleTarget.
                // Always restore the prior active target immediately so
                // sampleTarget is never left bound when released or disposed.
                RenderTexture previousActive = RenderTexture.active;
                try
                {
                    Graphics.Blit(source, sampleTarget);
                }
                finally
                {
                    RenderTexture.active =
                        previousActive == sampleTarget ? null : previousActive;
                }
                pendingRead = AsyncGPUReadback.Request(
                    sampleTarget, 0, TextureFormat.RGBA32);
                readPending = true;
                sinceSample = 0f;
            }

            float ease = 1f - Mathf.Exp(-dt / (powered ? .16f : .08f));
            float required = powered ? targetAmount : 0f;
            intensity = Mathf.Lerp(intensity, required, ease);
            bloom = Mathf.Lerp(bloom, powered ? targetBloom : 0f, ease);
            specular = Mathf.Lerp(specular, powered ? targetSpecular : 0f, ease);
            sideBias = Mathf.Lerp(sideBias, powered ? targetSideBias : 0f, ease);
            tint = Color.Lerp(tint, targetTint, ease);
            if (Mathf.Abs(intensity - required) < .0005f) intensity = required;
            if (!powered)
            {
                if (bloom < .0005f) bloom = 0f;
                if (specular < .0005f) specular = 0f;
                if (Mathf.Abs(sideBias) < .0005f) sideBias = 0f;
            }
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
