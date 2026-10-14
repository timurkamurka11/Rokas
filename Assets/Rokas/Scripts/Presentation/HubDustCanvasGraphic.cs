using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum HubDustVisibilityMode { Normal, HighContrastBoost, DiagnosticTestPattern }

    /// <summary>
    /// V8_CRISP_MICRO_DUST. 85 tiny particles, no large bokeh, 70% broad
    /// air and 30% gentle lamp-light dust. Four independently controlled
    /// soft additive light volumes preserve the original HOME photograph.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubDustCanvasGraphic : MaskableGraphic
    {
        public const int ParticleCount = 85;
        public const int GeneralAirCount = 60;         // 70.6%
        public const int LocalAirCount = ParticleCount - GeneralAirCount; // 25
        public const int MicroCount = 75;              // 88.2%
        public const int SmallCount = 10;              // 11.8%
        public const int BokehCount = 0;               // zero large flakes
        public const float MaximumQuadSize = 9f;
        private const float MeshStep = 1f / 30f;
        private const float FullWidth = 1920f, FullHeight = 1080f;
        private static readonly Color WarmCore = new Color(.98f, .80f, .46f);
        private static readonly Color WarmAmbient = new Color(.92f, .68f, .35f);
        private static readonly Color NightAir = new Color(.60f, .78f, .88f);
        private static readonly Color NightShadow = new Color(.37f, .48f, .55f);

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
            public int kind, shape;
        }
        private readonly Mote[] motes = new Mote[ParticleCount];
        private float age, accumulated, visibility;
        private bool wasShown, showRoom, roomOn, hallwayOn, laptopOn, didLog;
        private float vertexAlphaTotal;
        private float vertexAlphaPeak;
        public int ParticleBudget => ParticleCount;
        public float Visibility => visibility;
        public int LastVisibleMotes { get; private set; }
        public float LastMeanVertexAlpha { get; private set; }
        public float LastMaxVertexAlpha { get; private set; }
        public int LastShaftCount { get; private set; }
        public float LastMaxParticleQuadSize { get; private set; }

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
                    // 25 localized motes: 11 floor, 9 desk, 5 corridor.
                    // NO independent laptop emitter above the main table.
                    if (localIndex < 11)
                    {
                        cx = .13f; cy = .33f; rx = .22f; ry = .28f;
                    }
                    else if (localIndex < 20)
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
                    if (localIndex < 11) ny = Mathf.Min(ny, .61f);
                }

                // Coprime permutation evenly mixes the two small classes.
                int order = (i * 73) % ParticleCount;
                int kind = order < MicroCount ? 0 :
                    (order < MicroCount + SmallCount ? 1 : 2);
                float size = kind == 0
                    ? Mathf.Lerp(3.2f, 6.2f, (float)rng.NextDouble())
                    : Mathf.Lerp(6.5f, 9f, (float)rng.NextDouble());
                // Effective displacement 3-8 authored pixels per second,
                // with 2-6 px/s thermal rise close to the warm lamps.
                float flowSpeed = Mathf.Lerp(3f, 8f,
                    (float)rng.NextDouble());
                float direction = 6.2831853f *
                    (float)rng.NextDouble();
                float vx = flowSpeed * Mathf.Cos(direction);
                float vy = flowSpeed * Mathf.Sin(direction);
                if (local && i < GeneralAirCount + 20)
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
                    shape = rng.Next(0, 8)
                };
            }

            var shader = Resources.Load<Shader>("Shaders/HubDustSoftMotes");
            if (shader == null) shader = Shader.Find("ROKAS/UI/HubDustSoftMotes");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[ROKAS HubDust V8] UI shader unavailable.");
                enabled = false;
                return;
            }
            ownedAtlas = HubDustAtlasFactory.Create();
            ownedMaterial = new Material(shader)
                { name = "ROKAS V8 Crisp Micro Dust UI" };
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
                ownedMaterial.SetFloat("_DustMode", (int)visibilityMode);
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
                Ellipse(nx, ny, .13f, .33f, .20f, .28f),
                Ellipse(nx, ny, .63f, .57f, .22f, .26f));
        }

        public static float HallwayContribution(float x, float y, bool on)
        {
            return on ? Ellipse(x / FullWidth, y / FullHeight,
                .85f, .48f, .18f, .34f) : 0f;
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

        public static float NormalAlphaCap(int kind)
        {
            return kind == 0 ? .42f : .14f;
        }

        public static float NormalPixelDiameter(int kind, float diameter,
            float scale)
        {
            return kind == 0
                ? Mathf.Clamp(diameter * scale, 3.2f, 6.2f)
                : Mathf.Clamp(diameter * scale, 6.5f, MaximumQuadSize);
        }

        // Stable, smooth Gaussian-response alpha; this represents
        // VERTEX alpha, not the final atlas/fragment contribution.
        // In darkness, only the city window appreciably illuminates dust.
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
                    alpha = .072f + .175f * warm +
                        .024f * hall + .017f * window +
                        .165f * Mathf.Pow(warm, 7f);
                else
                    alpha = .060f + .070f * warm + .010f * hall;
            }
            else
            {
                // Window peak ~.20, dark interior <.03. Do not keep
                // the previous bright blue full-room nighttime field.
                if (kind == 0)
                    alpha = .012f + .180f * Mathf.Pow(window, 1.5f) +
                        .010f * hall;
                else
                    alpha = .009f + .080f * Mathf.Pow(window, 1.5f) +
                        .004f * hall;
            }

            // Laptop's optional soft additive reflection is controlled by
            // LaptopPowerSession in RokasView, never a new dust spawn pool.
            // The laptop does not brighten night particle alphas.
            return Mathf.Clamp(alpha * intrinsic * strength, 0f,
                roomLampOn ? NormalAlphaCap(kind) :
                    (kind == 0 ? .22f : .10f));
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
                float alpha = EvaluateDustAlpha(m.kind, m.alpha,
                    x, y, roomOn, hallwayOn, visibility, laptopOn) *
                    lifetimeFade * edge * shimmer * visibilityGain;
                float boost = visibilityMode ==
                    HubDustVisibilityMode.HighContrastBoost ? 2.5f : 1f;
                alpha = Mathf.Min(alpha * boost,
                    visibilityMode == HubDustVisibilityMode.Normal
                        ? (roomOn ? NormalAlphaCap(m.kind) :
                            (m.kind == 0 ? .22f : .10f)) : .90f);
                // Night: ignore dim motes in dark corners; preserve
                // only a small scattering around the city window.
                if (alpha < (roomOn ? .008f : .030f) &&
                    visibilityMode != HubDustVisibilityMode.DiagnosticTestPattern)
                    continue;

                float window = WindowContribution(x, y);
                // Amber pinpoints near lamps; never pure white.
                // Night interior is almost colorless; only particles
                // crossing the photographic city window become cool blue.
                Color tint = roomOn
                    ? Color.Lerp(WarmAmbient, WarmCore, Mathf.Clamp01(
                        RoomLampContribution(x, y, true) * .85f))
                    : Color.Lerp(NightShadow, NightAir,
                        Mathf.Clamp01(window * 1.5f));

                // Authored pixel diameter, not oversized radius.
                float diameter = visibilityMode == HubDustVisibilityMode.Normal
                    ? NormalPixelDiameter(m.kind, m.size, particleScale)
                    : m.size * particleScale;
                // No rotating large AABB: both mesh axes stay below
                // 9 authored pixels, including at the Inspector max.
                diameter = Mathf.Min(diameter, MaximumQuadSize);
                float radius = diameter * .5f;
                float ry = radius * (m.kind == 1 ? .77f : 1f);
                tint.a = alpha;
                float rotation = 0f;
                AddQuad(vh, new Vector2(rc.xMin + x * sx,
                        rc.yMin + y * sy), radius * sx, ry * sy,
                    Mathf.Cos(rotation), Mathf.Sin(rotation), tint,
                    m.shape + m.kind * 10);
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
                Debug.Log("[UIHubFxField] V8_CRISP_MICRO_DUST" +
                    " active=" + LastVisibleMotes + "/" + ParticleCount +
                    " quads=" + (LastVisibleMotes + LastShaftCount) +
                    "/" + (ParticleCount + 4) +
                    " (dust=" + LastVisibleMotes +
                    " beams=" + LastShaftCount + ")" +
                    " meanAlpha=" + LastMeanVertexAlpha.ToString("F3") +
                    " maxAlpha=" + LastMaxVertexAlpha.ToString("F3") +
                    " maxParticleQuadSize=" +
                        LastMaxParticleQuadSize.ToString("F2") +
                    " mode=" + visibilityMode +
                    " strength=" + visibility.ToString("F2") +
                    " rect=" + rc.width.ToString("F0") + "x" +
                    rc.height.ToString("F0") +
                    " shader=" + (material != null && material.shader != null
                        ? material.shader.name : "MISSING"));
            }
        }

        private void AddSoftWarmGlows(VertexHelper vh, Rect rc, float sx,
            float sy)
        {
            // Exactly four geometry volumes, even when some light sources
            // are off. Inactive sources have alpha=0, not fake grey fog.
            AddRadialGlow(vh, rc, sx, sy,
                .13f, .33f, .22f, .28f, .065f,
                new Color(.98f, .77f, .41f), roomOn);
            AddRadialGlow(vh, rc, sx, sy,
                .63f, .58f, .24f, .26f, .070f,
                new Color(.98f, .80f, .45f), roomOn);
            AddRadialGlow(vh, rc, sx, sy,
                .85f, .50f, .16f, .30f, .045f,
                new Color(.98f, .74f, .39f), hallwayOn);
            AddRadialGlow(vh, rc, sx, sy,
                .52f, .43f, .15f, .13f, .025f,
                new Color(.98f, .80f, .49f), laptopOn);
        }

        private void AddRadialGlow(VertexHelper vh, Rect rc, float sx,
            float sy, float cx, float cy, float radiusX, float radiusY,
            float peakAlpha, Color tint, bool lightIsOn)
        {
            tint.a = lightIsOn
                ? Mathf.Clamp(peakAlpha * shaftStrength * visibility, 0f, .075f)
                : 0f;
            AddQuad(vh, new Vector2(rc.xMin + cx * FullWidth * sx,
                    rc.yMin + cy * FullHeight * sy),
                radiusX * FullWidth * sx, radiusY * FullHeight * sy,
                1f, 0f, tint, 30);
            LastShaftCount++;
        }

        private static void AddQuad(VertexHelper vh, Vector2 center,
            float rx, float ry, float cs, float sn, Color tint, int shape)
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
                v.uv0 = new Vector2(shape * 2f + (xx + 1f) * .5f,
                    (yy + 1f) * .5f);
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
