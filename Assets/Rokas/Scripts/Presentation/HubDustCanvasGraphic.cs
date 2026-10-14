using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum HubDustVisibilityMode { Normal, HighContrastBoost, DiagnosticTestPattern }

    /// <summary>
    /// V7_CINEMATIC_GOLD_BALANCE. One non-interactive ScreenSpaceOverlay
    /// graphic: 60% broad-air dust, 40% elliptical localized dust, and
    /// four switch-controlled additive light volumes on the HOME photograph.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubDustCanvasGraphic : MaskableGraphic
    {
        public const int ParticleCount = 200;
        public const int GeneralAirCount = 120;       // 60%
        public const int LocalAirCount = ParticleCount - GeneralAirCount;
        public const int MicroCount = 140;            // 70%
        public const int SmallCount = 46;             // 23%
        public const int BokehCount = 14;             // 7%
        private const float MeshStep = 1f / 30f;
        private const float FullWidth = 1920f, FullHeight = 1080f;
        private static readonly Color WarmCore = new Color(1f, .95f, .82f);
        private static readonly Color WarmAmbient = new Color(1f, .78f, .42f);
        private static readonly Color WarmWindow = new Color(1f, .88f, .68f);
        private static readonly Color CoolWindow = new Color(.82f, .94f, 1f);
        private static readonly Color NightAir = new Color(.72f, .90f, 1f);

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
                    ny = Mathf.Lerp(.10f, .90f, (float)rng.NextDouble());
                }
                else
                {
                    // Uniform AREA distribution inside elliptical light
                    // pockets, not clustering at the centre of a rectangle.
                    int localIndex = i - GeneralAirCount;
                    float cx, cy, rx, ry;
                    if (localIndex < 28)
                    {
                        cx = .13f; cy = .33f; rx = .20f; ry = .28f;
                    }
                    else if (localIndex < 52)
                    {
                        cx = .63f; cy = .57f; rx = .22f; ry = .26f;
                    }
                    else if (localIndex < 68)
                    {
                        cx = .85f; cy = .48f; rx = .18f; ry = .34f;
                    }
                    else
                    {
                        cx = .52f; cy = .43f; rx = .15f; ry = .13f;
                    }
                    float r = Mathf.Sqrt((float)rng.NextDouble());
                    float phi = 6.2831853f * (float)rng.NextDouble();
                    nx = Mathf.Clamp(cx + rx * r * Mathf.Cos(phi), .06f, .94f);
                    ny = Mathf.Clamp(cy + ry * r * Mathf.Sin(phi), .10f, .90f);
                    if (localIndex < 28) ny = Mathf.Min(ny, .61f);
                }

                // Coprime permutation mixes all three sizes across
                // both spatial pools rather than leaving bokeh at one lamp.
                int order = (i * 73) % ParticleCount;
                int kind = order < MicroCount ? 0 :
                    (order < MicroCount + SmallCount ? 1 : 2);
                float size = kind == 0
                    ? Mathf.Lerp(6.5f, 13.5f, (float)rng.NextDouble())
                    : kind == 1
                        ? Mathf.Lerp(14f, 24f, (float)rng.NextDouble())
                        : Mathf.Lerp(28f, 48f, (float)rng.NextDouble());
                // Effective displacement 3-8 authored pixels per second,
                // with 2-6 px/s thermal rise close to the warm lamps.
                float flowSpeed = Mathf.Lerp(3f, 8f,
                    (float)rng.NextDouble());
                float direction = 6.2831853f *
                    (float)rng.NextDouble();
                float vx = flowSpeed * Mathf.Cos(direction);
                float vy = flowSpeed * Mathf.Sin(direction);
                if (local && i < GeneralAirCount + 52)
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
                    // Bokeh uses the ring/disc cells in the source 4x2 atlas.
                    shape = kind == 2 ? (rng.Next(0, 2) == 0 ? 1 : 5)
                        : rng.Next(0, 8)
                };
            }

            var shader = Resources.Load<Shader>("Shaders/HubDustSoftMotes");
            if (shader == null) shader = Shader.Find("ROKAS/UI/HubDustSoftMotes");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[ROKAS HubDust V7] UI shader unavailable.");
                enabled = false;
                return;
            }
            ownedAtlas = HubDustAtlasFactory.Create();
            ownedMaterial = new Material(shader)
                { name = "ROKAS V7 Cinematic Gold Balance UI" };
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
            return kind == 0 ? .68f : kind == 1 ? .42f : .22f;
        }

        public static float NormalPixelDiameter(int kind, float diameter,
            float scale)
        {
            return kind == 0
                ? Mathf.Clamp(diameter * scale, 6.5f, 13.5f)
                : kind == 1
                    ? Mathf.Clamp(diameter * scale, 14f, 24f)
                    : Mathf.Clamp(diameter * scale, 28f, 48f);
        }

        // Alpha is per-vertex and intentionally higher than V6.
        // The atlas/shader still reduce per-fragment opacity at the edges.
        public static float EvaluateDustAlpha(int kind, float intrinsic,
            float x, float y, bool roomLampOn, bool hallwayLampOn,
            float strength = 1f, bool laptopPowered = false)
        {
            float warm = RoomLampContribution(x, y, roomLampOn);
            float hall = HallwayContribution(x, y, hallwayLampOn);
            float window = WindowContribution(x, y);
            float laptop = laptopPowered ? LaptopContribution(x, y) : 0f;
            float alpha;
            if (kind == 0)
                alpha = (roomLampOn ? .260f : .225f)
                    + .385f * warm + .095f * hall
                    + .095f * laptop + (roomLampOn ? .075f : .205f) * window;
            else if (kind == 1)
                alpha = (roomLampOn ? .205f : .175f)
                    + .180f * warm + .058f * hall
                    + .040f * laptop + (roomLampOn ? .035f : .100f) * window;
            else
                alpha = (roomLampOn ? .132f : .115f)
                    + .082f * warm + .030f * hall
                    + .022f * laptop + .024f * window;

            return Mathf.Clamp(alpha * intrinsic * strength, 0f,
                NormalAlphaCap(kind));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            LastVisibleMotes = 0;
            LastMeanVertexAlpha = 0f;
            LastMaxVertexAlpha = 0f;
            LastShaftCount = 0;
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
                // Seamless edge wrapping, with a narrow fade to prevent
                // teleportation while keeping 185-200 visible particles.
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
                        ? NormalAlphaCap(m.kind) : .90f);
                if (alpha < .001f && visibilityMode !=
                    HubDustVisibilityMode.DiagnosticTestPattern) continue;

                float window = WindowContribution(x, y);
                // On: cream-to-amber with warm pearl at the window.
                // Off: subdued blue night air, intensified only at the
                // city window; corridor/laptop keep their own glow state.
                Color tint = roomOn ? Color.Lerp(WarmAmbient, WarmCore,
                    Mathf.Clamp01(RoomLampContribution(x, y, true) * .38f))
                    : NightAir;
                if (roomOn)
                    tint = Color.Lerp(tint, WarmWindow,
                        Mathf.Clamp01(window * .75f));
                else
                    tint = Color.Lerp(tint, CoolWindow,
                        Mathf.Clamp01(window * .85f));
                // Powered laptop / lit corridor lend a warm accent even
                // in the night scene without switching room lamps on.
                if (!roomOn)
                {
                    float reflectedWarm = Mathf.Max(
                        HallwayContribution(x, y, hallwayOn) * .55f,
                        laptopOn ? LaptopContribution(x, y) * .45f : 0f);
                    tint = Color.Lerp(tint, WarmWindow, reflectedWarm);
                }

                // Authored pixel diameter, not oversized radius.
                float diameter = visibilityMode == HubDustVisibilityMode.Normal
                    ? NormalPixelDiameter(m.kind, m.size, particleScale)
                    : m.size * particleScale;
                float radius = diameter * .5f;
                float ry = radius * (m.kind == 1 ? .77f : 1f);
                tint.a = alpha;
                float rotation = m.angle + age * .008f;
                AddQuad(vh, new Vector2(rc.xMin + x * sx,
                        rc.yMin + y * sy), radius * sx, ry * sy,
                    Mathf.Cos(rotation), Mathf.Sin(rotation), tint,
                    m.shape + m.kind * 10);
                LastVisibleMotes++;
                vertexAlphaTotal += alpha;
                vertexAlphaPeak = Mathf.Max(vertexAlphaPeak, alpha);
            }
            LastMeanVertexAlpha = LastVisibleMotes > 0
                ? vertexAlphaTotal / LastVisibleMotes : 0f;
            LastMaxVertexAlpha = vertexAlphaPeak;
            if (Application.isPlaying && !didLog && visibility >= .99f)
            {
                didLog = true;
                Debug.Log("[UIHubFxField] V7_CINEMATIC_GOLD_BALANCE" +
                    " active=" + LastVisibleMotes + "/" + ParticleCount +
                    " quads=" + (LastVisibleMotes + LastShaftCount) +
                    "/" + (ParticleCount + 4) +
                    " (dust=" + LastVisibleMotes +
                    " beams=" + LastShaftCount + ")" +
                    " meanAlpha=" + LastMeanVertexAlpha.ToString("F3") +
                    " maxAlpha=" + LastMaxVertexAlpha.ToString("F3") +
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
                .13f, .33f, .20f, .28f, .15f,
                new Color(1f, .76f, .38f), roomOn);
            AddRadialGlow(vh, rc, sx, sy,
                .63f, .57f, .22f, .26f, .16f,
                new Color(1f, .80f, .44f), roomOn);
            AddRadialGlow(vh, rc, sx, sy,
                .85f, .48f, .18f, .34f, .12f,
                new Color(1f, .74f, .36f), hallwayOn);
            AddRadialGlow(vh, rc, sx, sy,
                .52f, .43f, .15f, .13f, .10f,
                new Color(1f, .84f, .50f), laptopOn);
        }

        private void AddRadialGlow(VertexHelper vh, Rect rc, float sx,
            float sy, float cx, float cy, float radiusX, float radiusY,
            float peakAlpha, Color tint, bool lightIsOn)
        {
            tint.a = lightIsOn
                ? Mathf.Clamp(peakAlpha * shaftStrength * visibility, 0f, .18f)
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
