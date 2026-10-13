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
        private const int ParticleCount = 432;
        private const float MeshStep = 1f / 30f;
        [SerializeField, Range(.5f, 2.0f)] private float visibilityGain = 1.38f;
        [SerializeField, Range(.5f, 1.6f)] private float particleScale = 1.12f;
        public float VisibilityGain { get => visibilityGain; set => visibilityGain = Mathf.Clamp(value,.5f,2.0f); }
        public float ParticleScale { get => particleScale; set => particleScale = Mathf.Clamp(value,.5f,1.6f); }
        private struct Mote
        {
            public float x, y, z, phase, drift, alpha, rotation, scale;
            public int kind, shape;
        }
        private readonly Mote[] motes = new Mote[ParticleCount];
        private float age, accum, visualStrength;
        private bool roomOn, hallwayOn, showRoom;
        private Material ownedMaterial;
        public int ParticleBudget => ParticleCount;
        public float Visibility => visualStrength;
        public int LastVisibleMotes { get; private set; }
        private bool reportedFirstRender;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            var rng = new System.Random(0x524F4B41);
            for (int i = 0; i < motes.Length; i++)
            {
                // Photo-registered light pockets in the actual 1920x1080 hub.
                // 76% of motes are deliberately seeded where they have a
                // believable chance to scatter window / lamp / doorway light.
                float x = (float)rng.NextDouble() * 1920f;
                float y = (float)rng.NextDouble() * 1080f;
                float selector = (float)rng.NextDouble();
                if (selector < .27f)
                {
                    x = 670f + Triangular(rng)*520f;
                    y = 350f + Triangular(rng)*410f;
                }
                else if (selector < .48f)
                {
                    x = 180f + Triangular(rng)*310f;
                    y = 480f + Triangular(rng)*400f;
                }
                else if (selector < .69f)
                {
                    x = 1140f + Triangular(rng)*365f;
                    y = 390f + Triangular(rng)*380f;
                }
                else if (selector < .83f)
                {
                    x = 1710f + Triangular(rng)*260f;
                    y = 400f + Triangular(rng)*455f;
                }
                motes[i] = new Mote
                {
                    x = Mathf.Clamp(x, 8f, 1912f),
                    y = Mathf.Clamp(y, 8f, 1072f),
                    z = (float)rng.NextDouble(),
                    phase = (float)rng.NextDouble() * 6.28318f,
                    drift = .4f + (float)rng.NextDouble() * .8f,
                    alpha = .71f + (float)rng.NextDouble() * .29f,
                    rotation = (float)rng.NextDouble() * 6.28318f,
                    scale = .68f + (float)rng.NextDouble() * .76f,
                    kind = i < 281 ? 0 : i < 389 ? 1 : 2, // 65/25/10
                    shape = rng.Next(0,8) // all 8 shapes of Argon's 4x2 atlas
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
            LastVisibleMotes = 0;
            if (!showRoom || visualStrength < .0001f) return;
            var rc = rectTransform.rect;
            float toX = rc.width / 1920f;
            float toY = rc.height / 1080f;
            for (int i = 0; i < motes.Length; i++)
            {
                Mote m = motes[i];
                // Slowly rising thermal drift + 3 octave-like smooth waves;
                // no high frequency twitching, flickering or snowfall.
                float speed = m.drift * (m.kind == 2 ? .67f : .48f);
                float t = age * speed;
                float flutterX = Mathf.Sin(t*.47f+m.phase)*7.5f
                    + Mathf.Sin(t*.21f+m.phase*2.2f)*5.4f
                    + Mathf.Sin(t*.09f+m.phase*4.1f)*3f;
                float flutterY = Mathf.Cos(t*.31f+m.phase*1.7f)*6.2f
                    + Mathf.Sin(t*.16f+m.phase*.8f)*3.6f;
                float x = Mathf.Repeat(m.x + t*7.8f + flutterX, 1920f);
                float y = Mathf.Repeat(m.y - t*4.6f + flutterY, 1080f);
                // Avoid a popping particle as it wraps around a frame edge.
                float edgeFade = Mathf.SmoothStep(0f,1f,
                    Mathf.Clamp01(Mathf.Min(Mathf.Min(x,1920f-x),
                        Mathf.Min(y,1080f-y))/45f));
                float breath = .82f + .18f*Mathf.Sin(age*.42f+m.phase);
                float warm = RoomLampContribution(x, y, roomOn);
                float doorway = HallwayContribution(x, y, hallwayOn);
                float window = WindowContribution(x,y);
                // V1 emitted nearly invisible 1px dots at 0.5%-1% alpha.
                // V2 bounds the opacity while making actual lamplit dust
                // clearly visible even at 0.7x Game View scale.
                float alpha = EvaluateDustAlpha(m.kind, m.alpha, x, y,
                    roomOn, hallwayOn, visualStrength) * visibilityGain *
                    edgeFade * breath;
                if (alpha < .006f) continue;
                // Rare larger, defocused motes near the camera, not all equal.
                float size = (m.kind == 0 ? 4.2f : m.kind == 1 ? 10.8f : 25f)
                    * m.scale * particleScale *
                    (m.kind == 2 ? 1f+.27f*m.z : 1f);
                // Cool city/window and corridor scatter; warm practicals.
                float cool = doorway + window;
                float blend = cool / Mathf.Max(.001f, cool + warm);
                var tint = Color.Lerp(new Color(1f, .86f, .69f), 
                    new Color(.64f, .78f, .99f), blend);
                tint.a = alpha;
                float cs = Mathf.Cos(m.rotation + age * .025f);
                float sn = Mathf.Sin(m.rotation + age * .025f);
                float sx = size * toX;
                float sy = size * toY * (m.kind == 1 ? .76f : 1f);
                float cx = rc.xMin + x * toX;
                float cy = rc.yMax - y * toY;
                AddQuad(vh, new Vector2(cx, cy), sx, sy, cs, sn, tint, m.shape);
                LastVisibleMotes++;
            }
            if (!reportedFirstRender && Application.isPlaying && LastVisibleMotes > 0)
            {
                reportedFirstRender = true;
                Debug.Log("[ROKAS HubDust V3] rendering " +
                    LastVisibleMotes + " motes; room=" + roomOn +
                    " hallway=" + hallwayOn + " strength=" +
                    visualStrength.ToString("F2"));
            }
        }

        private static float Triangular(System.Random rng)
        {
            return (float)(rng.NextDouble()+rng.NextDouble()-1.0);
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
            // Verified against the real 1920x1080 HomeView hotspots:
            // floor lamp (138,400), desk lamp (1151,195).
            return 1.08f*Gaussian(x,y,138f,458f,250f,390f) +
                .78f*Gaussian(x,y,1151f,310f,340f,340f) +
                .30f*Gaussian(x,y,1020f,740f,410f,320f);
        }

        public static float HallwayContribution(float x, float y, bool on)
        {
            // The actual corridor door is on the RIGHT at X1748,
            // unlike the incorrect V1 X355 left-wall light.
            return (on ? .98f : .065f)*
                Gaussian(x,y,1748f,345f,265f,470f);
        }

        public static float WindowContribution(float x, float y)
        {
            // City reflections exist with BOTH room lamp states;
            // they are cold and noticeably weaker than a practical.
            return .43f*Gaussian(x,y,697f,345f,460f,430f);
        }

        public static float EvaluateDustAlpha(int kind, float intrinsic,
            float x,float y,bool roomOn,bool hallwayOn,float visibility=1f)
        {
            float warm=RoomLampContribution(x,y,roomOn);
            float corridor=HallwayContribution(x,y,hallwayOn);
            float window=WindowContribution(x,y);
            float light=.042f+warm+corridor+window;
            float baseAlpha=kind==0 ? .35f : kind==1 ? .29f : .185f;
            // Photo-registered shafts are bright, shade falls nearly to zero.
            // The 4x2 atlas soft profile further attenuates these values.
            return Mathf.Clamp(intrinsic*baseAlpha*light*visibility,0f,.37f);
        }

        private static void AddQuad(VertexHelper vh, Vector2 c, float rx,
            float ry, float cs, float sn, Color tint, int shape)
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
                v.uv0 = new Vector2(shape * 2f + (xx + 1f) * .5f, (yy + 1f) * .5f);
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
