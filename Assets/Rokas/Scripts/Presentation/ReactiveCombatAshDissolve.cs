using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Presentation
{
    // Arena owns this effect's progress. Neither shader nor particles use an
    // independent clock, and every material is owned by this one dying actor.
    public sealed class ReactiveCombatAshDissolve : IDisposable
    {
        private sealed class MaterialBinding
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Owned;
        }

        private sealed class AshPoint
        {
            public Vector3 Position;
            public float Threshold;
            public bool Emitted;
        }

        private readonly ReactiveCombatActorVisual actor;
        private readonly List<MaterialBinding> bindings = new List<MaterialBinding>();
        private readonly List<AshPoint> ashPoints = new List<AshPoint>();
        private GameObject particlesObject;
        private ParticleSystem particles;
        private Material particlesMaterial;
        private Vector3 noiseOrigin;
        private bool captured;
        private bool begun;
        private bool disposed;
        private float progress;
        private const float NoiseScale = 14f;

        public float Duration { get; set; } = 1.2f;
        public float Progress => progress;
        public bool Begun => begun;
        public int ParticleCount => particles == null ? 0 : particles.particleCount;

        public ReactiveCombatAshDissolve(ReactiveCombatActorVisual actor)
        {
            this.actor = actor;
            CaptureMaterials();
        }

        public void CaptureMaterials()
        {
            if (captured || disposed || actor == null || actor.ModelRoot == null) return;
            foreach (Renderer renderer in actor.ModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                bindings.Add(new MaterialBinding { Renderer = renderer, Original = renderer.sharedMaterials });
            }
            captured = true;
        }

        public void Begin()
        {
            if (begun || disposed || actor == null) return;
            CaptureMaterials();
            Shader shader = Resources.Load<Shader>("Combat/ReactiveCombatAshDissolve");
            if (shader == null) shader = Shader.Find("Rokas/ReactiveCombat/AshDissolve");
            if (shader == null) throw new InvalidOperationException("Missing combat ash dissolve shader.");
            noiseOrigin = actor.transform.position;
            foreach (MaterialBinding binding in bindings)
            {
                if (binding.Renderer == null) continue;
                binding.Owned = new Material[binding.Original.Length];
                for (int i = 0; i < binding.Original.Length; i++)
                {
                    Material original = binding.Original[i];
                    if (original == null) continue;
                    var material = new Material(shader) { name = original.name + " (Combat Ash)" };
                    material.CopyPropertiesFromMaterial(original);
                    material.enableInstancing = original.enableInstancing;
                    material.globalIlluminationFlags = original.globalIlluminationFlags;
                    material.SetFloat("_Dissolve", 0f);
                    material.SetFloat("_NoiseScale", NoiseScale);
                    material.SetVector("_DissolveOrigin", noiseOrigin);
                    binding.Owned[i] = material;
                }
                binding.Renderer.sharedMaterials = binding.Owned;
            }
            CaptureAshPoints();
            CreateParticles();
            begun = true;
            SetProgress(0f);
        }

        public void SetProgress(float value)
        {
            if (!begun || disposed) return;
            float next = Mathf.Clamp01(value);
            float elapsed = Mathf.Max(0f, next - progress) * Mathf.Max(.01f, Duration);
            foreach (MaterialBinding binding in bindings)
                if (binding.Owned != null)
                    foreach (Material material in binding.Owned)
                        if (material != null) material.SetFloat("_Dissolve", next);
            if (particles != null)
            {
                for (int i = 0; i < ashPoints.Count; i++)
                {
                    AshPoint point = ashPoints[i];
                    if (point.Emitted || next < point.Threshold) continue;
                    point.Emitted = true;
                    float seed = Fraction(Mathf.Sin((i + 1) * 12.345f) * 4375.23f);
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = point.Position,
                        velocity = new Vector3((seed - .5f) * .15f, .16f + seed * .24f,
                            Mathf.Sin(i * 2.399f) * .055f),
                        startLifetime = .42f + seed * .28f,
                        startSize = .025f + seed * .035f,
                        rotation = seed * 360f,
                        startColor = new Color(.17f, .155f, .19f, .65f)
                    };
                    particles.Emit(emit, 1);
                }
                if (elapsed > 0f) particles.Simulate(elapsed, false, false, false);
                particles.Pause(false);
                particlesMaterial.SetFloat("_Opacity", 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(.65f, 1f, next)));
            }
            progress = next;
        }

        private void CaptureAshPoints()
        {
            const int maximum = 128;
            int perRenderer = Mathf.Max(16, maximum / Mathf.Max(1, bindings.Count));
            foreach (MaterialBinding binding in bindings)
            {
                Renderer renderer = binding.Renderer;
                if (renderer == null || !renderer.enabled) continue;
                Mesh mesh = null;
                bool ownsMesh = false;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh();
                    skinned.BakeMesh(mesh);
                    ownsMesh = true;
                }
                else
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                }
                if (mesh != null && mesh.isReadable)
                {
                    Vector3[] vertices = mesh.vertices;
                    int stride = Mathf.Max(1, vertices.Length / perRenderer);
                    for (int i = stride / 2; i < vertices.Length && ashPoints.Count < maximum; i += stride)
                    {
                        Vector3 world = renderer.transform.TransformPoint(vertices[i]);
                        Vector3 point = (world - noiseOrigin) * NoiseScale;
                        float noise = Noise(point) * .65f + Noise(point * 1.97f + Vector3.one * 11.3f) * .35f;
                        ashPoints.Add(new AshPoint { Position = world,
                            Threshold = Mathf.Clamp((noise + .001f) / 1.02f, .03f, .94f) });
                    }
                }
                if (ownsMesh) DestroyOwned(mesh);
            }
        }

        private void CreateParticles()
        {
            particlesObject = new GameObject("ReactiveDeathAsh");
            particlesObject.transform.SetParent(actor.transform, false);
            particlesObject.layer = actor.gameObject.layer;
            particles = particlesObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.startLifetime = .6f;
            main.startSpeed = 0f;
            main.startSize = .04f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.8f, 0f), new GradientAlphaKey(.6f, .45f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            particlesMaterial = CreateQuietMaterial("Reactive Ash Particles", Color.white, 1f);
            particlesMaterial.SetFloat("_SoftShape", 1f);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = particlesMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Pause(false);
        }

        internal static Material CreateQuietMaterial(string name, Color color, float opacity)
        {
            Shader shader = Resources.Load<Shader>("Combat/ReactiveCombatQuietFx");
            if (shader == null) shader = Shader.Find("Rokas/ReactiveCombat/QuietFx");
            if (shader == null) throw new InvalidOperationException("Missing restrained combat FX shader.");
            var material = new Material(shader) { name = name };
            material.SetColor("_Color", color);
            material.SetFloat("_Opacity", opacity);
            return material;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (MaterialBinding binding in bindings)
            {
                if (binding.Owned == null) continue;
                if (binding.Renderer != null)
                {
                    Material[] current = binding.Renderer.sharedMaterials;
                    for (int i = 0; i < current.Length && i < binding.Owned.Length; i++)
                        if (binding.Owned[i] != null && current[i] == binding.Owned[i])
                            current[i] = binding.Original[i];
                    binding.Renderer.sharedMaterials = current;
                }
                foreach (Material material in binding.Owned) DestroyOwned(material);
            }
            DestroyOwned(particlesObject);
            DestroyOwned(particlesMaterial);
            bindings.Clear();
            ashPoints.Clear();
            particles = null;
            particlesObject = null;
            particlesMaterial = null;
        }

        internal static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private static float Fraction(float value) => value - Mathf.Floor(value);

        private static float Hash(Vector3 cell) => Fraction(Mathf.Sin(Vector3.Dot(cell,
            new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f);

        private static float Noise(Vector3 p)
        {
            Vector3 cell = new Vector3(Mathf.Floor(p.x), Mathf.Floor(p.y), Mathf.Floor(p.z));
            Vector3 f = p - cell;
            f = new Vector3(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y),
                f.z * f.z * (3f - 2f * f.z));
            float x00 = Mathf.Lerp(Hash(cell), Hash(cell + Vector3.right), f.x);
            float x10 = Mathf.Lerp(Hash(cell + Vector3.up), Hash(cell + Vector3.right + Vector3.up), f.x);
            float x01 = Mathf.Lerp(Hash(cell + Vector3.forward), Hash(cell + Vector3.right + Vector3.forward), f.x);
            float x11 = Mathf.Lerp(Hash(cell + Vector3.up + Vector3.forward),
                Hash(cell + Vector3.one), f.x);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, f.y), Mathf.Lerp(x01, x11, f.y), f.z);
        }
    }
}
