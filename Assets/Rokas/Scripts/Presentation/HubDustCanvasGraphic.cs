using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum HubDustVisibilityMode { Normal, HighContrastBoost, DiagnosticTestPattern }

    /// <summary>
    /// V6_SUBTLE_WARM_ATMOSPHERE. One non-interactive ScreenSpaceOverlay
    /// graphic: 65% wide-air dust, 35% local warm-light dust, and two
    /// very soft amber radial glows. Coordinates follow the HOME photograph.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubDustCanvasGraphic : MaskableGraphic
    {
        public const int ParticleCount = 128;
        public const int GeneralAirCount = 84;        // 65.625%
        public const int LocalAirCount = ParticleCount - GeneralAirCount;
        public const int MicroCount = 105;            // 82.0%
        public const int SmallCount = 19;             // 14.8%
        public const int BokehCount = 4;              // 3.1%
        private const float MeshStep = 1f / 30f;
        private const float FullWidth = 1920f, FullHeight = 1080f;
        private static readonly Color WarmCore = new Color(1f, .82f, .52f);
        private static readonly Color WarmAmbient = new Color(.95f, .74f, .45f);
        private static readonly Color CoolWindow = new Color(.65f, .82f, .88f);
        private static readonly Color WarmGlow = new Color(1f, .72f, .35f);

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
        private bool wasShown, showRoom, roomOn, hallwayOn, didLog;
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
                    nx = Mathf.Lerp(.08f, .92f, (float)rng.NextDouble());
                    ny = Mathf.Lerp(.12f, .88f, (float)rng.NextDouble());
                }
                else
                {
                    // Uniform AREA distribution inside elliptical light
                    // pockets, not clustering at the centre of a rectangle.
                    int localIndex = i - GeneralAirCount;
                    float cx, cy, rx, ry;
                    if (localIndex < 16)
                    {
                        cx = .14f; cy = .34f; rx = .16f; ry = .22f;
                    }
                    else if (localIndex < 30)
                    {
                        cx = .63f; cy = .58f; rx = .18f; ry = .22f;
                    }
                    else if (localIndex < 38)
                    {
                        cx = .82f; cy = .52f; rx = .12f; ry = .28f;
                    }
                    else
                    {
                        cx = .52f; cy = .42f; rx = .14f; ry = .12f;
                    }
                    float r = Mathf.Sqrt((float)rng.NextDouble());
                    float phi = 6.2831853f * (float)rng.NextDouble();
                    nx = Mathf.Clamp(cx + rx * r * Mathf.Cos(phi), .08f, .92f);
                    ny = Mathf.Clamp(cy + ry * r * Mathf.Sin(phi), .12f, .88f);
                    if (localIndex < 16) ny = Mathf.Min(ny, .56f);
                }

                // Coprime permutation mixes all three sizes across
                // both spatial pools rather than leaving bokeh at one lamp.
                int order = (i * 73) % ParticleCount;
                int kind = order < MicroCount ? 0 :
                    (order < MicroCount + SmallCount ? 1 : 2);
                float size = kind == 0
                    ? Mathf.Lerp(1.8f, 4.2f, (float)rng.NextDouble())
                    : kind == 1
                        ? Mathf.Lerp(4.5f, 8f, (float)rng.NextDouble())
                        : Mathf.Lerp(12f, 22f, (float)rng.NextDouble());
                // Effective displacement 3-8 authored pixels per second,
                // with 2-6 px/s thermal rise close to the warm lamps.
                float flowSpeed = Mathf.Lerp(3f, 8f,
                    (float)rng.NextDouble());
                float direction = 6.2831853f *
                    (float)rng.NextDouble();
                float vx = flowSpeed * Mathf.Cos(direction);
                float vy = flowSpeed * Mathf.Sin(direction);
                if (local && i < GeneralAirCount + 30)
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
                    kind = kind, shape = rng.Next(0, 8)
                };
            }

            var shader = Resources.Load<Shader>("Shaders/HubDustSoftMotes");
            if (shader == null) shader = Shader.Find("ROKAS/UI/HubDustSoftMotes");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[ROKAS HubDust V6] UI shader unavailable.");
                enabled = false;
                return;
            }
            ownedAtlas = HubDustAtlasFactory.Create();
            ownedMaterial = new Material(shader)
                { name = "ROKAS V6 Subtle Warm Atmosphere UI" };
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
            bool corridorLampOn)
        {
            bool switched = roomLampOn != roomOn || corridorLampOn != hallwayOn;
            roomOn = roomLampOn;
            hallwayOn = corridorLampOn;
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
                Ellipse(nx, ny, .14f, .34f, .16f, .22f),
                Ellipse(nx, ny, .63f, .58f, .18f, .22f));
        }

        public static float HallwayContribution(float x, float y, bool on)
        {
            return on ? Ellipse(x / FullWidth, y / FullHeight,
                .82f, .52f, .12f, .28f) : 0f;
        }

        public static float WindowContribution(float x, float y)
        {
            return Ellipse(x / FullWidth, y / FullHeight,
                .385f, .65f, .135f, .20f);
        }

        public static float EvaluateDustAlpha(int kind, float intrinsic,
            float x, float y, bool roomLampOn, bool hallwayLampOn,
            float strength = 1f)
        {
            float warm = RoomLampContribution(x, y, roomLampOn);
            float hall = HallwayContribution(x, y, hallwayLampOn);
            float window = WindowContribution(x, y);
            float low, litWarm, litHall, cool;
            if (kind == 0)
            {
                low = .034f;
                litWarm = .200f;
                litHall = .085f;
                cool = .012f;
            }
            else if (kind == 1)
            {
                low = .027f;
                litWarm = .065f;
                litHall = .033f;
                cool = .005f;
            }
            else
            {
                low = .021f;
                litWarm = .022f;
                litHall = .012f;
                cool = .003f;
            }
            // The Gaussian profile itself provides smooth arrival/departure
            // through light fields: no hard emitter rectangle boundaries.
            float alpha = low + litWarm * warm + litHall * hall + cool * window;
            if (!roomLampOn && !hallwayLampOn)
                alpha = Mathf.Min(alpha, kind == 0 ? .07f : .06f);
            if (window > .45f && !roomLampOn)
                alpha = Mathf.Min(alpha, .08f);
            return Mathf.Clamp(alpha * intrinsic * strength, 0f,
                kind == 0 ? .32f : kind == 1 ? .12f : .06f);
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
                    Mathf.Clamp01(Mathf.Min(t, m.life - t) / 5f));
                // Very slow, smooth convection without frantic tremors.
                float waveX = 4f * Mathf.Sin(t * .10f + m.phase);
                float waveY = 2.4f * Mathf.Sin(t * .075f + m.phase * 1.3f);
                float x = m.x + m.vx * t + waveX;
                float y = m.y + m.vy * t + waveY;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                    Mathf.Min(Mathf.Min(x, FullWidth - x),
                        Mathf.Min(y, FullHeight - y)) / 30f));
                float shimmer = .95f + .05f * Mathf.Sin(age * .27f + m.phase);
                float alpha = EvaluateDustAlpha(m.kind, m.alpha,
                    x, y, roomOn, hallwayOn, visibility) *
                    lifetimeFade * edge * shimmer * visibilityGain;
                float boost = visibilityMode ==
                    HubDustVisibilityMode.HighContrastBoost ? 2.5f : 1f;
                alpha = Mathf.Min(alpha * boost,
                    visibilityMode == HubDustVisibilityMode.Normal ? .32f : .68f);
                if (alpha < .001f && visibilityMode !=
                    HubDustVisibilityMode.DiagnosticTestPattern) continue;

                float warm = RoomLampContribution(x, y, roomOn);
                float hall = HallwayContribution(x, y, hallwayOn);
                float window = WindowContribution(x, y);
                Color tint = m.kind == 0
                    ? Color.Lerp(WarmAmbient, WarmCore,
                        Mathf.Clamp01(warm + hall))
                    : WarmAmbient;
                // Only a faint city-blue hint immediately at the window.
                float coldShare = Mathf.Clamp01(
                    window * .36f / Mathf.Max(.01f, warm + hall + window));
                tint = Color.Lerp(tint, CoolWindow, coldShare);
                if (!roomOn && !hallwayOn && window > .5f)
                    alpha = Mathf.Min(alpha, .08f);

                // Authored pixel diameter, not oversized radius.
                float radius = m.size * particleScale * .5f;
                float ry = radius * (m.kind == 1 ? .77f : 1f);
                tint.a = alpha;
                float rotation = m.angle + age * .008f;
                AddQuad(vh, new Vector2(rc.xMin + x * sx,
                        rc.yMin + y * sy), radius * sx, ry * sy,
                    Mathf.Cos(rotation), Mathf.Sin(rotation), tint, m.shape);
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
                Debug.Log("[UIHubFxField] V6_SUBTLE_WARM_ATMOSPHERE" +
                    " active=" + LastVisibleMotes + "/" + ParticleCount +
                    " (dust=" + LastVisibleMotes +
                    " glows=" + LastShaftCount + ")" +
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
            if (!roomOn || shaftStrength <= 0f) return;
            // Only two very low-opacity, wide radial lamp glows. No
            // stretched geometry at curtain/window/corridor, no grey haze.
            AddRadialGlow(vh, rc, sx, sy,
                .14f, .34f, .16f, .22f, .05f);
            AddRadialGlow(vh, rc, sx, sy,
                .63f, .58f, .18f, .22f, .045f);
        }

        private void AddRadialGlow(VertexHelper vh, Rect rc, float sx,
            float sy, float cx, float cy, float radiusX, float radiusY,
            float glowAlpha)
        {
            Color tint = WarmGlow;
            tint.a = Mathf.Min(.055f, glowAlpha * shaftStrength * visibility);
            if (tint.a < .001f) return;
            AddQuad(vh, new Vector2(rc.xMin + cx * FullWidth * sx,
                    rc.yMin + cy * FullHeight * sy),
                radiusX * FullWidth * sx, radiusY * FullHeight * sy,
                1f, 0f, tint, 8);
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
