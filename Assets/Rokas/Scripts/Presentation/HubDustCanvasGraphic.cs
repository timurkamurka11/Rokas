using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// Atmospheric dust for the actual ROKAS ScreenSpaceOverlay 2.5D hub.
    /// A world-space Shuriken particle system cannot render between the
    /// photographed room and the authored UI in this project's UI pipeline.
    /// Three depth layers are baked into one Canvas mesh (one draw call).
    /// Room/hall visibility uses the SAME persistent lamp switches as YOMI.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubDustCanvasGraphic : MaskableGraphic
    {
        private const int ParticleCount = 192;
        private const float MeshStep = 1f / 24f;
        private struct Mote
        {
            public float x, y, z, phase, drift, alpha, rotation, scale;
            public int kind;
        }
        private readonly Mote[] motes = new Mote[ParticleCount];
        private float age, accum, visualStrength;
        private bool roomOn, hallwayOn, showRoom;
        private Material ownedMaterial;
        public int ParticleBudget => ParticleCount;
        public float Visibility => visualStrength;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            var rng = new System.Random(0x524F4B41);
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i] = new Mote
                {
                    x = (float)rng.NextDouble() * 1920f,
                    y = (float)rng.NextDouble() * 1080f,
                    z = (float)rng.NextDouble(),
                    phase = (float)rng.NextDouble() * 6.28318f,
                    drift = .32f + (float)rng.NextDouble() * 1.10f,
                    alpha = .55f + (float)rng.NextDouble() * .45f,
                    rotation = (float)rng.NextDouble() * 6.28318f,
                    scale = .65f + (float)rng.NextDouble() * .75f,
                    kind = i < 125 ? 0 : i < 173 ? 1 : 2 // 65/25/10
                };
            }
            var shader = Resources.Load<Shader>("Shaders/HubDustSoftMotes");
            if (shader == null) shader = Shader.Find("ROKAS/UI/HubDustSoftMotes");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[ROKAS Hub Dust] UI dust shader unavailable; suppressing squares.");
                enabled = false;
                return;
            }
            ownedMaterial = new Material(shader) { name = "ROKAS Hub Dust UI (Runtime)" };
            material = ownedMaterial;
        }

        /// <summary>No new lighting state: references GameSession's own switches.</summary>
        public void Tick(float dt, bool visible, bool roomLampOn, bool corridorLampOn)
        {
            roomOn = roomLampOn;
            hallwayOn = corridorLampOn;
            showRoom = visible;
            float target = showRoom ? 1f : 0f;
            visualStrength = Mathf.MoveTowards(visualStrength, target, Mathf.Max(0f, dt) * 2.8f);
            bool display = visualStrength > .0001f;
            if (gameObject.activeSelf != display)
                gameObject.SetActive(display);
            if (!display) return;
            age += Mathf.Max(0f, dt);
            accum += Mathf.Max(0f, dt);
            if (accum >= MeshStep)
            {
                accum = 0f;
                SetVerticesDirty();
            }
        }

        public override Texture mainTexture => Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!showRoom || visualStrength < .0001f) return;
            var rc = rectTransform.rect;
            float toX = rc.width / 1920f;
            float toY = rc.height / 1080f;
            for (int i = 0; i < motes.Length; i++)
            {
                Mote m = motes[i];
                float speed = m.drift * (m.kind == 2 ? .58f : .40f);
                float x = Mathf.Repeat(m.x + age * speed * 9.0f +
                    Mathf.Sin(age * .32f + m.phase) * 7.5f, 1920f);
                float y = Mathf.Repeat(m.y - age * speed * 4.5f +
                    Mathf.Sin(age * .23f + m.phase * 1.7f) * 8.0f, 1080f);
                float warm = RoomLampContribution(x, y, roomOn);
                float doorway = HallwayContribution(x, y, hallwayOn);
                // In dark areas particles nearly vanish instead of snowing.
                float light = .035f + warm + doorway;
                float alpha = Mathf.Min(.25f,
                    m.alpha * (m.kind == 0 ? .090f : m.kind == 1 ? .058f : .047f) *
                    light * visualStrength);
                if (alpha < .0013f) continue;
                float size = (m.kind == 0 ? 1.6f : m.kind == 1 ? 4.4f : 13.5f) *
                    m.scale * (m.kind == 2 ? 1f + .25f * m.z : 1f);
                // Bright warm lamp and cool doorway produce different motes.
                float blend = doorway / Mathf.Max(.001f, doorway + warm);
                var tint = Color.Lerp(new Color(1f, .86f, .69f), 
                    new Color(.64f, .78f, .99f), blend);
                tint.a = alpha;
                float cs = Mathf.Cos(m.rotation + age * .025f);
                float sn = Mathf.Sin(m.rotation + age * .025f);
                float sx = size * toX;
                float sy = size * toY * (m.kind == 1 ? .65f : 1f);
                float cx = rc.xMin + x * toX;
                float cy = rc.yMax - y * toY;
                AddQuad(vh, new Vector2(cx, cy), sx, sy, cs, sn, tint, m.kind);
            }
        }

        private static float Gaussian(float x, float y, float cx, float cy,
            float radiusX, float radiusY)
        {
            float dx = (x - cx) / radiusX;
            float dy = (y - cy) / radiusY;
            return Mathf.Exp(-2f * (dx * dx + dy * dy));
        }

        public static float RoomLampContribution(float x, float y, bool on)
        {
            if (!on) return 0f;
            // Hand-authored to the existing 1920x1080 HOME photograph;
            // never reads/repaints the laptop's approved V11 photo.
            return .75f * Gaussian(x,y,1225f,480f,330f,450f) +
                .46f * Gaussian(x,y,595f,430f,260f,380f);
        }

        public static float HallwayContribution(float x, float y, bool on)
        {
            // The doorway is on the LEFT of the approved room art.
            return (on ? .58f : .105f) *
                Gaussian(x,y,355f,365f,185f,390f);
        }

        private static void AddQuad(VertexHelper vh, Vector2 c, float rx,
            float ry, float cs, float sn, Color tint, int kind)
        {
            int offset = vh.currentVertCount;
            for (int j = 0; j < 4; j++)
            {
                float xx = (j == 0 || j == 3) ? -1f : 1f;
                float yy = (j < 2) ? -1f : 1f;
                var v = UIVertex.simpleVert;
                v.position = new Vector3(c.x + (xx * cs - yy * sn) * rx,
                    c.y + (xx * sn + yy * cs) * ry, 0f);
                v.color = tint;
                v.uv0 = new Vector2(kind * 2f + (xx + 1f) * .5f, (yy + 1f) * .5f);
                vh.AddVert(v);
            }
            vh.AddTriangle(offset, offset + 1, offset + 2);
            vh.AddTriangle(offset, offset + 2, offset + 3);
        }

        protected override void OnDestroy()
        {
            material = null;
            if (ownedMaterial != null)
            {
                if (Application.isPlaying) Destroy(ownedMaterial);
                else DestroyImmediate(ownedMaterial);
            }
            base.OnDestroy();
        }
    }
}
