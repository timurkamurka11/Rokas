using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rokas.Presentation
{
    // Arena advances the aperture, filaments and particles with one paused clock.
    public sealed class ReactiveCombatPortalEffect : IDisposable
    {
        private const int Segments = 80;
        private const int FilamentCount = 5;
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private readonly GameObject root;
        private readonly Mesh discMesh;
        private readonly Material discMaterial;
        private readonly Material rimMaterial;
        private readonly Material particlesMaterial;
        private readonly Material smokeMaterial;
        private readonly LineRenderer outerRim;
        private readonly LineRenderer innerRim;
        private readonly LineRenderer[] filaments = new LineRenderer[FilamentCount];
        private readonly ParticleSystem particles;
        private readonly ParticleSystem smoke;
        private readonly Vector3[] outerPositions = new Vector3[Segments];
        private readonly Vector3[] innerPositions = new Vector3[Segments];
        private readonly Vector3[] filamentPositions = new Vector3[32];
        private float elapsed;
        private float emissionElapsed;
        private float visibility;
        private int emittedCount;
        private bool disposed;
        public Transform Transform => root == null ? null : root.transform;
        public float Visibility => visibility;
        public bool IsClosed => visibility <= .001f;
        public float PresentationElapsed => elapsed;
        public int ParticleCount => particles == null ? 0 : particles.particleCount + smoke.particleCount;
        public Vector3 FloorPosition => root.transform.position - Vector3.up * 1.7f;
        public Vector3 ApertureCenter => root.transform.position;
        public Vector3 PlaneNormal => -root.transform.forward;
        public Vector3 PlaneRight => root.transform.right;

        public ReactiveCombatPortalEffect(Transform parent, Vector3 position, int layer)
        {
            root = new GameObject("ReactiveEnemyPortal");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position + Vector3.up * 1.7f;
            root.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            root.layer = layer;
            Shader shader = Resources.Load<Shader>("Combat/ReactiveCombatPortalVolume");
            if (shader == null) throw new InvalidOperationException("Missing layered combat portal shader.");
            discMaterial = new Material(shader) { name = "Combat Moving Portal Depth" };
            rimMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Combat Portal Energy",
                new Color(.48f, .36f, .69f, 1f), 0f);
            rimMaterial.SetFloat("_SoftShape", 2f);
            particlesMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Combat Portal Sparks", Color.white, 0f);
            particlesMaterial.SetFloat("_SoftShape", 1f);
            smokeMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Combat Portal Wisps", Color.white, 0f);
            smokeMaterial.SetFloat("_SoftShape", 3f);
            var disc = new GameObject("PortalMovingDepth", typeof(MeshFilter), typeof(MeshRenderer));
            disc.transform.SetParent(root.transform, false);
            disc.layer = layer;
            discMesh = MakeDisc();
            disc.GetComponent<MeshFilter>().sharedMesh = discMesh;
            var renderer = disc.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = discMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            outerRim = MakeLine("PortalBrokenEnergyRim", layer, .055f, Segments, true, Color.white);
            innerRim = MakeLine("PortalInnerVortexRim", layer, .027f, Segments, true, new Color(.64f, .8f, .88f, .7f));
            for (int i = 0; i < FilamentCount; i++)
                filaments[i] = MakeLine("PortalSwirlFilament" + i, layer, .026f, 32, false,
                    i % 2 == 0 ? new Color(.85f, .6f, 1f, .72f) : new Color(.42f, .76f, .8f, .5f));
            particles = MakeParticles("PortalOrbitingSparks", layer, particlesMaterial, 48, .055f);
            smoke = MakeParticles("PortalResidualWisps", layer, smokeMaterial, 48, .3f);
            UpdateGeometry();
            SetProgress(0f);
        }
        public void SetProgress(float value)
        {
            if (disposed || root == null) return;
            visibility = Mathf.Clamp01(value);
            float scale = Mathf.SmoothStep(0f, 1f, visibility);
            root.transform.localScale = new Vector3(Mathf.Lerp(.1f, 1f, scale), Mathf.Lerp(.08f, 1f, scale), 1f);
            discMaterial.SetFloat(OpacityId, visibility * .99f);
            rimMaterial.SetFloat(OpacityId, visibility * .88f);
            particlesMaterial.SetFloat(OpacityId, visibility * .8f);
            smokeMaterial.SetFloat(OpacityId, visibility * .65f);
        }
        public void Tick(float deltaTime)
        {
            if (disposed || root == null) return;
            float step = Mathf.Max(0f, deltaTime);
            if (step <= 0f) return;
            elapsed += step;
            discMaterial.SetFloat(PhaseId, elapsed);
            smokeMaterial.SetFloat(PhaseId, elapsed);
            UpdateGeometry();
            if (visibility > .12f)
            {
                emissionElapsed += step;
                int count = Mathf.Min(3, Mathf.FloorToInt(emissionElapsed / .06f));
                emissionElapsed %= .06f;
                for (int i = 0; i < count; i++) EmitWisps();
            }
            else emissionElapsed = 0f;
            particles.Simulate(step, false, false, false);
            smoke.Simulate(step, false, false, false);
            particles.Pause(false);
            smoke.Pause(false);
        }
        private void EmitWisps()
        {
            float angle = emittedCount++ * 2.399963f + elapsed * .6f;
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(Mathf.Cos(angle) * .86f, Mathf.Sin(angle) * 1.9f, -.11f),
                velocity = new Vector3(-Mathf.Sin(angle) * .15f, .07f + Mathf.Cos(angle) * .14f, -.06f),
                startLifetime = .35f + .25f * Mathf.Abs(Mathf.Sin(angle)),
                startSize = .025f + .02f * Mathf.Abs(Mathf.Cos(angle)),
                startColor = new Color(.61f, .47f, .84f, .7f)
            };
            particles.Emit(emit, 1);
            if (emittedCount % 2 != 0) return;
            emit.position *= 1.08f;
            emit.velocity *= .7f;
            emit.startLifetime = .75f;
            emit.startSize = .27f + .15f * Mathf.Abs(Mathf.Sin(angle));
            emit.rotation = angle * Mathf.Rad2Deg;
            emit.startColor = new Color(.16f, .11f, .24f, .5f);
            smoke.Emit(emit, 1);
        }
        private void UpdateGeometry()
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                float wobble = 1f + .027f * Mathf.Sin(angle * 7f + elapsed * 3.1f) + .018f * Mathf.Sin(angle * 13f - elapsed * 2.3f);
                outerPositions[i] = new Vector3(Mathf.Cos(angle) * .9f * wobble, Mathf.Sin(angle) * 1.9f * wobble, -.02f);
                innerPositions[i] = new Vector3(Mathf.Cos(angle) * .84f / wobble, Mathf.Sin(angle) * 1.82f / wobble, .01f);
            }
            outerRim.SetPositions(outerPositions);
            innerRim.SetPositions(innerPositions);
            for (int f = 0; f < FilamentCount; f++)
            {
                for (int i = 0; i < filamentPositions.Length; i++)
                {
                    float t = (float)i / (filamentPositions.Length - 1);
                    float angle = f * Mathf.PI * 2f / FilamentCount + elapsed * .85f + t * 2.6f;
                    float radius = Mathf.Lerp(.3f, 1.15f, t);
                    filamentPositions[i] = new Vector3(Mathf.Cos(angle) * .9f * radius,
                        Mathf.Sin(angle) * 1.9f * radius, -.015f - Mathf.Sin(t * Mathf.PI) * .05f);
                }
                filaments[f].SetPositions(filamentPositions);
            }
        }
        private LineRenderer MakeLine(string name, int layer, float width, int count, bool loop, Color color)
        {
            var item = new GameObject(name, typeof(LineRenderer));
            item.transform.SetParent(root.transform, false);
            item.layer = layer;
            var line = item.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = count;
            line.startWidth = width;
            line.endWidth = loop ? width : .001f;
            line.startColor = loop ? color : new Color(color.r, color.g, color.b, 0f);
            line.endColor = color;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = rimMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
        private ParticleSystem MakeParticles(string name, int layer, Material material, int cap, float size)
        {
            var item = new GameObject(name, typeof(ParticleSystem));
            item.transform.SetParent(root.transform, false);
            item.layer = layer;
            var system = item.GetComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.startLifetime = .65f;
            main.startSize = size;
            main.maxParticles = cap;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.85f, .13f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Pause(false);
            return system;
        }
        private static Mesh MakeDisc()
        {
            var vertices = new Vector3[Segments + 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Segments * 3];
            uv[0] = Vector2.one * .5f;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 1.04f, Mathf.Sin(angle) * 2.19f, 0f);
                uv[i + 1] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f + Vector2.one * .5f;
                if (i < Segments)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }
            var mesh = new Mesh { name = "Reactive Portal Aperture" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReactiveCombatAshDissolve.DestroyOwned(root);
            ReactiveCombatAshDissolve.DestroyOwned(discMesh);
            ReactiveCombatAshDissolve.DestroyOwned(discMaterial);
            ReactiveCombatAshDissolve.DestroyOwned(rimMaterial);
            ReactiveCombatAshDissolve.DestroyOwned(particlesMaterial);
            ReactiveCombatAshDissolve.DestroyOwned(smokeMaterial);
        }
    }
}
