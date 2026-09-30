using System;
using UnityEngine;

namespace Rokas.Presentation
{
    // A small vertical tear behind an entering enemy. Progress is visibility
    // (zero closed, one open); Arena supplies both opening and animation time.
    public sealed class ReactiveCombatPortalEffect : IDisposable
    {
        private const int Segments = 64;
        private readonly GameObject root;
        private readonly Mesh discMesh;
        private readonly Material discMaterial;
        private readonly Material rimMaterial;
        private readonly Material particlesMaterial;
        private readonly LineRenderer outerRim;
        private readonly LineRenderer innerRim;
        private readonly ParticleSystem particles;
        private readonly Vector3[] outerPositions = new Vector3[Segments];
        private readonly Vector3[] innerPositions = new Vector3[Segments];
        private float elapsed;
        private float emissionElapsed;
        private float visibility;
        private int emittedCount;
        private bool disposed;

        public Transform Transform => root == null ? null : root.transform;
        public float Visibility => visibility;
        public bool IsClosed => visibility <= .001f;
        public float PresentationElapsed => elapsed;
        public int ParticleCount => particles == null ? 0 : particles.particleCount;

        // position is the local floor point where the enemy steps out.
        public ReactiveCombatPortalEffect(Transform parent, Vector3 position, int layer)
        {
            root = new GameObject("ReactiveEnemyPortal");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position + Vector3.up * 1.7f;
            root.layer = layer;
            discMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Combat Portal Interior",
                new Color(.025f, .012f, .036f, 1f), 0f);
            discMaterial.SetFloat("_PortalCore", 1f);
            rimMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Combat Portal Rim",
                new Color(.42f, .19f, .32f, 1f), 0f);
            rimMaterial.SetFloat("_SoftShape", 2f);
            particlesMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Combat Portal Dust",
                Color.white, 0f);
            particlesMaterial.SetFloat("_SoftShape", 1f);

            var disc = new GameObject("PortalDarkInterior", typeof(MeshFilter), typeof(MeshRenderer));
            disc.transform.SetParent(root.transform, false);
            disc.layer = layer;
            discMesh = MakeDisc();
            disc.GetComponent<MeshFilter>().sharedMesh = discMesh;
            var discRenderer = disc.GetComponent<MeshRenderer>();
            discRenderer.sharedMaterial = discMaterial;
            discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            discRenderer.receiveShadows = false;
            outerRim = MakeRim("PortalOuterRim", layer, .065f, Color.white);
            innerRim = MakeRim("PortalInnerRim", layer, .03f,
                new Color(.72f, .7f, .82f, .55f));

            var dust = new GameObject("PortalDriftingDust", typeof(ParticleSystem));
            dust.transform.SetParent(root.transform, false);
            dust.layer = layer;
            particles = dust.GetComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.startLifetime = .65f;
            main.startSize = .045f;
            main.maxParticles = 32;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.6f, .2f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = particlesMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Pause(false);
            UpdateRim();
            SetProgress(0f);
        }

        public void SetProgress(float value)
        {
            if (disposed || root == null) return;
            visibility = Mathf.Clamp01(value);
            root.transform.localScale = new Vector3(Mathf.Lerp(.45f, 1f, visibility),
                Mathf.Lerp(.2f, 1f, visibility), 1f);
            discMaterial.SetFloat("_Opacity", visibility * .95f);
            rimMaterial.SetFloat("_Opacity", visibility * .8f);
            particlesMaterial.SetFloat("_Opacity", visibility * .7f);
        }

        public void Tick(float deltaTime)
        {
            if (disposed || root == null) return;
            float step = Mathf.Max(0f, deltaTime);
            if (step <= 0f) return;
            elapsed += step;
            discMaterial.SetFloat("_Phase", elapsed * .7f);
            UpdateRim();
            if (visibility > .35f)
            {
                emissionElapsed += step;
                // Cap catch-up emissions after a long frame; this is a quiet entrance,
                // not a burst that covers the actor or the battlefield.
                int count = Mathf.Min(3, Mathf.FloorToInt(emissionElapsed / .075f));
                emissionElapsed %= .075f;
                for (int i = 0; i < count; i++) EmitDust();
            }
            else emissionElapsed = 0f;
            particles.Simulate(step, false, false, false);
            particles.Pause(false);
        }

        private void EmitDust()
        {
            float angle = emittedCount++ * 2.399963f + elapsed * .25f;
            float fraction = .8f + .13f * Mathf.Sin(emittedCount * 1.7f);
            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(Mathf.Cos(angle) * .78f * fraction,
                    Mathf.Sin(angle) * 1.9f * fraction, -.025f),
                velocity = new Vector3(Mathf.Cos(angle) * .04f, .06f, -.01f),
                startLifetime = .45f + .2f * Mathf.Abs(Mathf.Sin(angle)),
                startSize = .025f + .035f * Mathf.Abs(Mathf.Cos(angle)),
                startColor = new Color(.18f, .13f, .19f, .6f)
            };
            particles.Emit(emit, 1);
        }

        private void UpdateRim()
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                float wobble = 1f + .02f * Mathf.Sin(angle * 4f + elapsed * 1.3f);
                outerPositions[i] = new Vector3(Mathf.Cos(angle) * .78f * wobble,
                    Mathf.Sin(angle) * 1.9f * wobble, .12f);
                innerPositions[i] = new Vector3(Mathf.Cos(angle) * .735f / wobble,
                    Mathf.Sin(angle) * 1.82f / wobble, .11f);
            }
            outerRim.SetPositions(outerPositions);
            innerRim.SetPositions(innerPositions);
        }

        private LineRenderer MakeRim(string name, int layer, float width, Color color)
        {
            var item = new GameObject(name, typeof(LineRenderer));
            item.transform.SetParent(root.transform, false);
            item.layer = layer;
            var line = item.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = rimMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static Mesh MakeDisc()
        {
            var vertices = new Vector3[Segments + 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[Segments * 3];
            vertices[0] = new Vector3(0f, 0f, .16f);
            uv[0] = Vector2.one * .5f;
            colors[0] = Color.white;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * .78f, Mathf.Sin(angle) * 1.9f, .16f);
                uv[i + 1] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f + Vector2.one * .5f;
                colors[i + 1] = Color.white;
                if (i < Segments)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }
            var mesh = new Mesh { name = "Reactive Portal Ellipse" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
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
        }
    }
}
