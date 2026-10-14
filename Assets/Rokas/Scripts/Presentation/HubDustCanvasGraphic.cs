using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum HubDustVisibilityMode { Normal, HighContrastBoost, DiagnosticTestPattern }

    /// <summary>
    /// V10_NOCTURNE_WINDOW_AND_GLINT. Preserve V9's warm/ON camera
    /// composition, 145-mote layout and <=10.5px dust size. Add window/CRT
    /// nocturne light, asynchronous seed glints and 0.40s light crossfade.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubDustCanvasGraphic : MaskableGraphic
    {
        public const int ParticleCount = 145;
        public const int GeneralAirCount = 85;        // 58.6%
        public const int LocalAirCount = ParticleCount - GeneralAirCount; // 60
        public const int MicroCount = 120;            // 82.8%
        public const int SmallCount = 25;             // 17.2%
        public const int BokehCount = 0;              // no large flakes
        public const float MaximumQuadSize = 10.5f;
        private const float MeshStep = 1f / 30f;
        private const float FullWidth = 1920f, FullHeight = 1080f;
        private static readonly Color WarmCore = new Color(1f, .89f, .62f);
        private static readonly Color WarmAmbient = new Color(.96f, .74f, .38f);
        private static readonly Color NocturneCore = new Color(.62f, .92f, 1f);
        private static readonly Color NocturneHalo = new Color(.28f, .68f, .96f);
        private static readonly Color WindowSilver = new Color(.75f, .92f, 1f);
        private static readonly Color CtvBlue = new Color(.40f, .77f, 1f);
        private static readonly Color HallAmber = new Color(.98f, .74f, .39f);
        private const float LightTransitionSeconds = .40f;
        private const float NightVisibilityThreshold = .135f;
        private const float LitGlintAmplitude = .12f;
        private const float NightGlintAmplitude = .06f;

        [SerializeField, Range(.5f, 2f)] private float visibilityGain = 1f;
        [SerializeField, Range(.5f, 1.6f)] private float particleScale = 1f;
        [SerializeField, Range(0f, 1.5f)] private float shaftStrength = 1f;
        [SerializeField] private HubDustVisibilityMode visibilityMode =
            HubDustVisibilityMode.Normal;
        private Texture2D ownedAtlas;
        private Material ownedMaterial;

        public HubDustVisibilityMode VisibilityMode
        {
            get => visibilityMode;
            set { visibilityMode = value; SetVerticesDirty(); SetMaterialDirty(); }
        }
        public bool DebugVisibility
        {
            get => visibilityMode == HubDustVisibilityMode.DiagnosticTestPattern;
            set => VisibilityMode = value
                ? HubDustVisibilityMode.DiagnosticTestPattern
                : HubDustVisibilityMode.Normal;
        }
        public float VisibilityGain
        {
            get => visibilityGain;
            set { visibilityGain = Mathf.Clamp(value, .5f, 2f); SetVerticesDirty(); }
        }
        public float ParticleScale
        {
            get => particleScale;
            set { particleScale = Mathf.Clamp(value, .5f, 1.6f); SetVerticesDirty(); }
        }
        public float ShaftStrength
        {
            get => shaftStrength;
            set { shaftStrength = Mathf.Clamp(value, 0f, 1.5f); SetVerticesDirty(); }
        }

        private struct Mote
        {
            public float x, y, vx, vy, phase, life, timeOffset, alpha, size, angle;
            public int kind, shape, glintSeed;
        }
        private readonly Mote[] motes = new Mote[ParticleCount];
        private float age, accumulated, visibility;
        private bool wasShown, showRoom, roomOn, hallwayOn, laptopOn, didLog;
        private bool lightBlendInitialized;
        private float lightBlendFactor;
        private float vertexAlphaTotal;
        private float vertexAlphaPeak;
        public int ParticleBudget => ParticleCount;
        public float Visibility => visibility;
        public int LastVisibleMotes { get; private set; }
        public float LastMeanVertexAlpha { get; private set; }
        public float LastMaxVertexAlpha { get; private set; }
        public int LastShaftCount { get; private set; }
        public float LastMaxParticleQuadSize { get; private set; }
        public float LightBlendFactor => lightBlendFactor;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            var rng = new System.Random(0x524F4B41);
            for (int i = 0; i < ParticleCount; ++i)
            {
                bool local = i >= GeneralAirCount;
                float nx, ny;
                if (!local)
                {
                    // Evenly spread through the main photographic room;
                    // no curtain/doorway emitter columns.
                    nx = Mathf.Lerp(.06f, .94f, (float)rng.NextDouble());
                    ny = Mathf.Lerp(.10f, .88f, (float)rng.NextDouble());
                }
                else
                {
                    // Uniform AREA distribution inside elliptical light
                    // pockets, not clustering at the centre of a rectangle.
                    int localIndex = i - GeneralAirCount;
                    float cx, cy, rx, ry;
                    // 60 soft light-pool motes: 24 floor, 24 desk,
                    // 12 corridor. No isolated laptop spawner.
                    if (localIndex < 24)
                    {
                        cx = .13f; cy = .34f; rx = .22f; ry = .28f;
                    }
                    else if (localIndex < 48)
                    {
                        cx = .63f; cy = .58f; rx = .24f; ry = .26f;
                    }
                    else
                    {
                        cx = .85f; cy = .50f; rx = .16f; ry = .30f;
                    }
                    float r = Mathf.Sqrt((float)rng.NextDouble());
                    float phi = 6.2831853f * (float)rng.NextDouble();
                    nx = Mathf.Clamp(cx + rx * r * Mathf.Cos(phi), .06f, .94f);
                    ny = Mathf.Clamp(cy + ry * r * Mathf.Sin(phi), .10f, .88f);
                    if (localIndex < 24) ny = Mathf.Min(ny, .62f);
                }

                // Coprime permutation evenly mixes the two small classes.
                int order = (i * 73) % ParticleCount;
                int kind = order < MicroCount ? 0 :
                    (order < MicroCount + SmallCount ? 1 : 2);
                float size = kind == 0
                    ? Mathf.Lerp(4.8f, 8f, (float)rng.NextDouble())
                    : Mathf.Lerp(8f, 10.5f, (float)rng.NextDouble());
                // Effective displacement 3-8 authored pixels per second,
                // with 2-6 px/s thermal rise close to the warm lamps.
                float flowSpeed = Mathf.Lerp(3f, 8f,
                    (float)rng.NextDouble());
                float direction = 6.2831853f *
                    (float)rng.NextDouble();
                float vx = flowSpeed * Mathf.Cos(direction);
                float vy = flowSpeed * Mathf.Sin(direction);
                if (local && i < GeneralAirCount + 48)
                {
                    // Exact thermal rise 2-6 px/s while full drift
                    // magnitude remains 3-8 px/s.
                    vy = Mathf.Min(flowSpeed * .85f, Mathf.Lerp(
                        2f, 6f, (float)rng.NextDouble()));
                    vx = Mathf.Sqrt(Mathf.Max(0f,
                        flowSpeed * flowSpeed - vy * vy)) *
                        (rng.NextDouble() < .5 ? -1f : 1f);
                }
                motes[i] = new Mote
                {
                    x = nx * FullWidth, y = ny * FullHeight,
                    vx = vx, vy = vy, size = size,
                    phase = 6.2831853f * (float)rng.NextDouble(),
                    life = Mathf.Lerp(55f, 105f, (float)rng.NextDouble()),
                    timeOffset = (float)rng.NextDouble() * 90f,
                    alpha = Mathf.Lerp(.91f, 1f, (float)rng.NextDouble()),
                    angle = (float)rng.NextDouble() * 6.2831853f,
                    kind = kind,
                    shape = rng.Next(0, 8),
                    // Stable seed without extra RNG calls: positions, sizes
                    // and motion remain IDENTICAL to the V9 warm scene.
                    glintSeed = (i * 89 + 31) % 251
                };
            }

            var shader = Resources.Load<Shader>("Shaders/HubDustSoftMotes");
            if (shader == null) shader = Shader.Find("ROKAS/UI/HubDustSoftMotes");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[ROKAS HubDust V10] UI shader unavailable.");
                enabled = false;
                return;
            }
            ownedAtlas = HubDustAtlasFactory.Create();
            ownedMaterial = new Material(shader)
                { name = "ROKAS V10 Nocturne Window and Glint UI" };
            ownedMaterial.SetTexture("_DustAtlasTex", ownedAtlas);
            ownedMaterial.SetFloat("_UseAtlas", 1f);
            ownedMaterial.SetFloat("_DustMode", (int)visibilityMode);
            material = ownedMaterial;
            SetMaterialDirty();
        }

        public override Texture mainTexture =>
            ownedAtlas != null ? ownedAtlas : Texture2D.whiteTexture;

        protected override void UpdateMaterial()
        {
            if (!IsActive()) return;
            base.UpdateMaterial();
            if (ownedAtlas == null || ownedMaterial == null) return;
            ownedMaterial.SetTexture("_DustAtlasTex", ownedAtlas);
            canvasRenderer.SetTexture(ownedAtlas);
            canvasRenderer.SetMaterial(materialForRendering, 0);
        }

        public void Tick(float dt, bool visible, bool roomLampOn,
            bool corridorLampOn, bool laptopPowered = false)
        {
            bool switched = roomLampOn != roomOn ||
                corridorLampOn != hallwayOn || laptopPowered != laptopOn;
            roomOn = roomLampOn;
            hallwayOn = corridorLampOn;
            laptopOn = laptopPowered;
            if (!lightBlendInitialized)
            {
                // Cold-start directly in the correct lighting state;
                // subsequent switch events crossfade over 0.40 seconds.
                lightBlendInitialized = true;
                lightBlendFactor = roomLampOn ? 1f : 0f;
            }
            else
            {
                float unscaledDt = Application.isPlaying
                    ? Time.unscaledDeltaTime : Mathf.Max(0f, dt);
                lightBlendFactor = Mathf.MoveTowards(lightBlendFactor,
                    roomLampOn ? 1f : 0f,
                    unscaledDt / LightTransitionSeconds);
            }
            showRoom = visible;
            if (visible && !wasShown)
            {
                // No first-frame partial-alpha drop.
                wasShown = true;
                visibility = 1f;
            }
            else
                visibility = Mathf.MoveTowards(visibility,
                    visible ? 1f : 0f, Mathf.Max(0f, dt) * 2.8f);

            bool display = visibility > .0001f;
            if (gameObject.activeSelf != display)
                gameObject.SetActive(display);
            if (!display) return;
            if (ownedMaterial != null)
            {
                ownedMaterial.SetFloat("_DustMode", (int)visibilityMode);
                ownedMaterial.SetFloat("_DustAge", age);
            }
            age += Mathf.Max(0f, dt);
            accumulated += Mathf.Max(0f, dt);
            if (switched || accumulated >= MeshStep)
            {
                accumulated = 0f;
                SetVerticesDirty();
            }
        }

        // All positions below use normalized image coordinates with
        // (0,0) at BOTTOM LEFT and (1,1) at TOP RIGHT.
        private static float Ellipse(float nx, float ny,
            float cx, float cy, float rx, float ry)
        {
            float dx = (nx - cx) / rx, dy = (ny - cy) / ry;
            return Mathf.Exp(-.5f * (dx * dx + dy * dy));
        }

        public static float RoomLampContribution(float x, float y, bool on)
        {
            if (!on) return 0f;
            float nx = x / FullWidth, ny = y / FullHeight;
            return Mathf.Max(
                Ellipse(nx, ny, .13f, .34f, .22f, .28f),
                Ellipse(nx, ny, .63f, .58f, .24f, .26f));
        }

        public static float HallwayContribution(float x, float y, bool on)
        {
            return on ? Ellipse(x / FullWidth, y / FullHeight,
                .85f, .50f, .16f, .30f) : 0f;
        }

        public static float WindowContribution(float x, float y)
        {
            return Ellipse(x / FullWidth, y / FullHeight,
                .385f, .65f, .15f, .22f);
        }

        public static float LaptopContribution(float x, float y)
        {
            return Ellipse(x / FullWidth, y / FullHeight,
                .52f, .43f, .15f, .13f);
        }

        public static float WindowGlassContribution(float x, float y)
        {
            return Ellipse(x / FullWidth, y / FullHeight,
                .38f, .64f, .24f, .28f);
        }

        public static float WindowSpillContribution(float x, float y)
        {
            return Ellipse(x / FullWidth, y / FullHeight,
                .40f, .36f, .28f, .22f);
        }

        public static float CrtContribution(float x, float y)
        {
            return Ellipse(x / FullWidth, y / FullHeight,
                .94f, .35f, .10f, .14f);
        }

        // Smooth night illumination encompasses both glass and room
        // depth, rather than dimming all motes outside the old small V9
        // window ellipse.
        public static float NocturneField(float x, float y)
        {
            return Mathf.Max(WindowGlassContribution(x, y),
                WindowSpillContribution(x, y) * .95f);
        }

        // Crossfade from V9 warm color to window silver only where the
        // wet glass is exposed; never recolor dust near active lamps.
        public static float WindowTealMix(float x, float y)
        {
            float nx = x / FullWidth, ny = y / FullHeight;
            float wetGlass = Ellipse(nx, ny, .385f, .66f, .13f, .17f);
            float warm = RoomLampContribution(x, y, true);
            float awayFromLamp = 1f - Mathf.SmoothStep(.62f, .95f, warm);
            return .42f * wetGlass * awayFromLamp;
        }

        public static float SpecularGlint(float seconds, int seed)
        {
            return Mathf.Pow(Mathf.Clamp01(Mathf.Sin(
                seconds * 1.8f + seed * 43.7f)), 14f);
        }

        public static float NormalAlphaCap(int kind)
        {
            return kind == 0 ? .54f : .24f;
        }

        public static float NormalPixelDiameter(int kind, float diameter,
            float scale)
        {
            return kind == 0
                ? Mathf.Clamp(diameter * scale, 4.8f, 8f)
                : Mathf.Clamp(diameter * scale, 8f, MaximumQuadSize);
        }

        // Authoring-space vertex alpha. Atlas sampling modulates the final
        // pixel in the GPU shader. A gentle ambient floor makes even
        // wall/window/bedside motes readable without bright snowflakes.
        public static float EvaluateDustAlpha(int kind, float intrinsic,
            float x, float y, bool roomLampOn, bool hallwayLampOn,
            float strength = 1f, bool laptopPowered = false)
        {
            float warm = RoomLampContribution(x, y, roomLampOn);
            float hall = HallwayContribution(x, y, hallwayLampOn);
            float window = WindowContribution(x, y);

            float alpha;
            if (roomLampOn)
            {
                if (kind == 0)
                    alpha = .145f + .155f * warm + .026f * hall +
                        .045f * window + .195f * Mathf.Pow(warm, 7f);
                else
                    alpha = .135f + .090f * warm + .018f * hall;
            }
            else
            {
                // V10 nocturne uses the window and its wide floor spill.
                // Independent hall/CRT light remains active as needed.
                float glass = WindowGlassContribution(x, y);
                float field = NocturneField(x, y);
                float crt = CrtContribution(x, y);
                float nightHall = hallwayLampOn ? Ellipse(
                    x / FullWidth, y / FullHeight,
                    .85f, .50f, .15f, .28f) : 0f;
                if (kind == 0)
                    alpha = .070f + .250f * field +
                        .210f * Mathf.Pow(glass, 6f) +
                        .045f * crt + .060f * nightHall;
                else
                    alpha = .067f + .180f * field +
                        .020f * crt + .035f * nightHall;
            }
            // Laptop reflection stays independent, but never attracts
            // particles or creates a new spawn pool at the central desk.
            return Mathf.Clamp(alpha * intrinsic * strength, 0f,
                roomLampOn ? NormalAlphaCap(kind) :
                    (kind == 0 ? .52f : .24f));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            LastVisibleMotes = 0;
            LastMeanVertexAlpha = 0f;
            LastMaxVertexAlpha = 0f;
            LastShaftCount = 0;
            LastMaxParticleQuadSize = 0f;
            vertexAlphaTotal = 0f;
            vertexAlphaPeak = 0f;
            if (!showRoom || visibility < .0001f) return;

            Rect rc = rectTransform.rect;
            float sx = rc.width / FullWidth, sy = rc.height / FullHeight;
            if (sx <= 0f || sy <= 0f) return;

            // Glow goes below motes, never an opaque fog strip.
            AddSoftWarmGlows(vh, rc, sx, sy);

            for (int i = 0; i < ParticleCount; ++i)
            {
                Mote m = motes[i];
                float t = Mathf.Repeat(age + m.timeOffset, m.life);
                float lifetimeFade = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(Mathf.Min(t, m.life - t) / 1.4f));
                // Very slow, smooth convection without frantic tremors.
                float waveX = 4f * Mathf.Sin(t * .10f + m.phase);
                float waveY = 2.4f * Mathf.Sin(t * .075f + m.phase * 1.3f);
                // Slow seamless drift with a narrow edge fade, maintaining
                // calm distribution without sudden popping or swarms.
                float x = Mathf.Repeat(m.x + m.vx * t + waveX, FullWidth);
                float y = Mathf.Repeat(m.y + m.vy * t + waveY, FullHeight);
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                    Mathf.Min(Mathf.Min(x, FullWidth - x),
                        Mathf.Min(y, FullHeight - y)) / 14f));
                float shimmer = .95f + .05f * Mathf.Sin(age * .27f + m.phase);
                float warmAlpha = EvaluateDustAlpha(m.kind, m.alpha,
                    x, y, true, hallwayOn, visibility, laptopOn);
                float nightAlpha = EvaluateDustAlpha(m.kind, m.alpha,
                    x, y, false, hallwayOn, visibility, laptopOn);
                float shimmerAndFade =
                    lifetimeFade * edge * shimmer * visibilityGain;
                // V9 warm baseline survives unchanged before glint.
                float baseAlpha = Mathf.Lerp(nightAlpha, warmAlpha,
                    lightBlendFactor) * shimmerAndFade;
                float boost = visibilityMode ==
                    HubDustVisibilityMode.HighContrastBoost ? 2.5f : 1f;
                // Per-seed specular glints occupy only a short fraction
                // of the cycle; CPU alpha telemetry includes their peak.
                float glint = SpecularGlint(age, m.glintSeed);
                float glintAmplitude = Mathf.Lerp(NightGlintAmplitude,
                    LitGlintAmplitude, lightBlendFactor);
                float alpha = baseAlpha * boost +
                    glint * glintAmplitude * lifetimeFade * edge;
                float cap = Mathf.Lerp(
                    m.kind == 0 ? .52f : .24f,
                    m.kind == 0 ? .62f : .36f, lightBlendFactor);
                alpha = Mathf.Min(alpha,
                    visibilityMode == HubDustVisibilityMode.Normal
                        ? cap : .90f);
                // Night stays populated in the window/spill/CRT light
                // while truly dark corners remain background.
                float threshold = Mathf.Lerp(
                    NightVisibilityThreshold, .008f, lightBlendFactor);
                if (alpha < threshold &&
                    visibilityMode != HubDustVisibilityMode.DiagnosticTestPattern)
                    continue;

                float window = WindowGlassContribution(x, y);
                float hall = HallwayContribution(x, y, hallwayOn);
                Color warmTint = Color.Lerp(WarmAmbient, WarmCore,
                    Mathf.Clamp01(RoomLampContribution(x, y, true) * .85f));
                warmTint = Color.Lerp(warmTint, WindowSilver,
                    WindowTealMix(x, y));
                warmTint = Color.Lerp(warmTint, CtvBlue,
                    CrtContribution(x, y) * .15f);
                // Cyan core over the glass and in its deep room spill;
                // only the lit corridor remains amber on room-light OFF.
                Color nocturneTint = Color.Lerp(NocturneHalo, NocturneCore,
                    Mathf.Clamp01(.42f + NocturneField(x, y) * .55f));
                nocturneTint = Color.Lerp(nocturneTint, CtvBlue,
                    CrtContribution(x, y) * .20f);
                nocturneTint = Color.Lerp(nocturneTint, HallAmber,
                    Mathf.Clamp01(hall * .90f));
                Color tint = Color.Lerp(nocturneTint, warmTint,
                    lightBlendFactor);

                // Authored pixel diameter, not oversized radius.
                float diameter = visibilityMode == HubDustVisibilityMode.Normal
                    ? NormalPixelDiameter(m.kind, m.size, particleScale)
                    : m.size * particleScale;
                // No rotated AABB: authored mesh quads stay at or below
                // 10.5 pixels even at maximum Inspector scale.
                diameter = Mathf.Min(diameter, MaximumQuadSize);
                float radius = diameter * .5f;
                float ry = radius * (m.kind == 1 ? .77f : 1f);
                tint.a = alpha;
                float rotation = 0f;
                AddQuad(vh, new Vector2(rc.xMin + x * sx,
                        rc.yMin + y * sy), radius * sx, ry * sy,
                    Mathf.Cos(rotation), Mathf.Sin(rotation), tint,
                    m.shape + m.kind * 10, m.glintSeed);
                LastVisibleMotes++;
                LastMaxParticleQuadSize = Mathf.Max(
                    LastMaxParticleQuadSize, diameter);
                vertexAlphaTotal += alpha;
                vertexAlphaPeak = Mathf.Max(vertexAlphaPeak, alpha);
            }
            LastMeanVertexAlpha = LastVisibleMotes > 0
                ? vertexAlphaTotal / LastVisibleMotes : 0f;
            LastMaxVertexAlpha = vertexAlphaPeak;
            if (Application.isPlaying && !didLog && visibility >= .99f)
            {
                didLog = true;
                Debug.Log("[UIHubFxField] V10_NOCTURNE_WINDOW_AND_GLINT" +
                    " active=" + LastVisibleMotes + "/" + ParticleCount +
                    " quads=" + (LastVisibleMotes + LastShaftCount) +
                    "/" + (ParticleCount + 6) +
                    " (dust=" + LastVisibleMotes +
                    " beams=" + LastShaftCount + ")" +
                    " meanAlpha=" + LastMeanVertexAlpha.ToString("F3") +
                    " maxAlpha=" + LastMaxVertexAlpha.ToString("F3") +
                    " maxParticleQuadSize=" +
                        LastMaxParticleQuadSize.ToString("F2") +
                    " mode=" + visibilityMode +
                    " strength=" + visibility.ToString("F2") +
                    " lightBlendFactor=" + lightBlendFactor.ToString("F2") +
                    " rect=" + rc.width.ToString("F0") + "x" +
                    rc.height.ToString("F0") +
                    " shader=" + (material != null && material.shader != null
                        ? material.shader.name : "MISSING"));
            }
        }

        private void AddSoftWarmGlows(VertexHelper vh, Rect rc, float sx,
            float sy)
        {
            // Existing V9 lamps are unchanged when lightBlendFactor=1.
            // Glow vertices have zero alpha when their source is OFF.
            AddRadialGlow(vh, rc, sx, sy,
                .13f, .34f, .22f, .28f, .100f,
                new Color(1f, .78f, .43f), lightBlendFactor);
            AddRadialGlow(vh, rc, sx, sy,
                .63f, .58f, .24f, .26f, .105f,
                new Color(1f, .82f, .47f), lightBlendFactor);
            AddRadialGlow(vh, rc, sx, sy,
                .85f, .50f, .16f, .30f, .045f,
                HallAmber, hallwayOn ? 1f : 0f);
            AddRadialGlow(vh, rc, sx, sy,
                .52f, .43f, .15f, .13f, .045f,
                new Color(1f, .84f, .52f), laptopOn ? 1f : 0f);
            // Broad nocturne spill from the glass toward bed/desk,
            // never an opaque polygonal white shaft.
            AddRadialGlow(vh, rc, sx, sy,
                .40f, .49f, .32f, .36f, .085f,
                new Color(.25f, .65f, .95f), 1f-lightBlendFactor);
            // Very subtle CRT bounce that does not generate extra motes.
            AddRadialGlow(vh, rc, sx, sy,
                .94f, .35f, .10f, .14f, .030f,
                CtvBlue, 1f);
        }

        private void AddRadialGlow(VertexHelper vh, Rect rc, float sx,
            float sy, float cx, float cy, float radiusX, float radiusY,
            float peakAlpha, Color tint, float sourceIntensity)
        {
            tint.a = Mathf.Clamp(peakAlpha * shaftStrength * visibility *
                Mathf.Clamp01(sourceIntensity), 0f, .110f);
            AddQuad(vh, new Vector2(rc.xMin + cx * FullWidth * sx,
                    rc.yMin + cy * FullHeight * sy),
                radiusX * FullWidth * sx, radiusY * FullHeight * sy,
                1f, 0f, tint, 30);
            LastShaftCount++;
        }

        private static void AddQuad(VertexHelper vh, Vector2 center,
            float rx, float ry, float cs, float sn, Color tint, int shape,
            int glintSeed = 0)
        {
            int offset = vh.currentVertCount;
            for (int j = 0; j < 4; ++j)
            {
                float xx = (j == 0 || j == 3) ? -1f : 1f;
                float yy = j < 2 ? -1f : 1f;
                var v = UIVertex.simpleVert;
                v.position = new Vector3(
                    center.x + (xx * cs - yy * sn) * rx,
                    center.y + (xx * sn + yy * cs) * ry, 0f);
                v.color = tint;
                // Encode a constant per-particle glint seed in UV0.y,
                // preserving local 0..1 sampling of the existing atlas.
                v.uv0 = new Vector2(shape * 2f + (xx + 1f) * .5f,
                    glintSeed * 2f + (yy + 1f) * .5f);
                vh.AddVert(v);
            }
            vh.AddTriangle(offset, offset + 1, offset + 2);
            vh.AddTriangle(offset, offset + 2, offset + 3);
        }

        protected override void OnDestroy()
        {
            material = null;
            if (ownedAtlas != null)
            {
                if (Application.isPlaying) Destroy(ownedAtlas);
                else DestroyImmediate(ownedAtlas);
                ownedAtlas = null;
            }
            if (ownedMaterial != null)
            {
                if (Application.isPlaying) Destroy(ownedMaterial);
                else DestroyImmediate(ownedMaterial);
                ownedMaterial = null;
            }
            base.OnDestroy();
        }
    }
}
