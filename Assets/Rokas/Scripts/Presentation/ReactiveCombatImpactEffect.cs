using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rokas.Presentation
{
    // Reusable bounded contact particles. Call only at the resolved visual contact;
    // Dodge emits dust at feet and cannot dispatch a false body impact.
    public sealed class ReactiveCombatImpactEffect : IDisposable
    {
        private readonly GameObject root;
        private readonly Material sparkMaterial;
        private readonly Material smokeMaterial;
        private readonly ParticleSystem sparks;
        private readonly ParticleSystem smoke;
        private readonly ReactiveCombatEmberLayer normalVapor;
        private readonly ReactiveCombatEmberLayer heavyVapor;
        private readonly ReactiveCombatEmberLayer throwVapor;
        private readonly ReactiveCombatEmberLayer guardVapor;
        private readonly ReactiveCombatEmberLayer clawVapor;
        private readonly LineRenderer[] rays = new LineRenderer[10];
        private readonly Vector3[] directions = new Vector3[10];
        private readonly float[] rayLengths = new float[10];
        private Vector3 contact;
        private Color contactColor;
        private float burstElapsed = 1f;
        private float burstDuration;
        private bool disposed;
        public int FleshDispatches { get; private set; }
        public int GuardDispatches { get; private set; }
        public int DodgeDispatches { get; private set; }
        public int ParticleCount => sparks == null ? 0 : sparks.particleCount + smoke.particleCount;

        public ReactiveCombatImpactEffect(Transform parent, int layer)
        {
            root = new GameObject("ReactiveContactEffects");
            root.transform.SetParent(parent, false);
            root.layer = layer;
            sparkMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Reactive Contact Sparks", Color.white, 1f);
            sparkMaterial.SetFloat("_SoftShape", 1f);
            smokeMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Reactive Contact Vapor", Color.white, 1f);
            smokeMaterial.SetFloat("_SoftShape", 3f);
            sparks = CreateParticles("ContactSparks", layer, sparkMaterial, 96, true);
            smoke = CreateParticles("ContactVapor", layer, smokeMaterial, 40, false);
            normalVapor = new ReactiveCombatEmberLayer(parent, layer, "NormalMist");
            heavyVapor = new ReactiveCombatEmberLayer(parent, layer, "HeavyBurst");
            throwVapor = new ReactiveCombatEmberLayer(parent, layer, "ThrowImpact");
            guardVapor = new ReactiveCombatEmberLayer(parent, layer, "BlockBurst");
            clawVapor = new ReactiveCombatEmberLayer(parent, layer, "ClawImpact");
            for (int i = 0; i < rays.Length; i++)
            {
                var item = new GameObject("ContactStreak" + i, typeof(LineRenderer));
                item.transform.SetParent(root.transform, false);
                item.layer = layer;
                var line = item.GetComponent<LineRenderer>();
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.startWidth = .04f;
                line.endWidth = .001f;
                line.sharedMaterial = sparkMaterial;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                rays[i] = line;
            }
        }
        public void Flesh(Vector3 point, bool heavy, Vector3 normal = default(Vector3))
        {
            if (disposed) return;
            FleshDispatches++;
            Burst(point, heavy ? new Color(.76f, .7f, 1f, 1f) : new Color(.69f, .9f, 1f, 1f), heavy ? 1.35f : .8f, false, normal);
            (heavy ? heavyVapor : normalVapor).Play(contact,
                Vector2.one * (heavy ? 1.35f : .65f), contactColor, heavy ? .52f : .26f,
                ContactAngle(normal));
        }
        public void Claw(Vector3 point, bool heavy, Vector3 normal)
        {
            if (disposed) return;
            FleshDispatches++;
            Burst(point, new Color(.94f, .61f, .73f, 1f), heavy ? 1.1f : .75f, false, normal);
            clawVapor.Play(contact, Vector2.one * (heavy ? 1.05f : .75f), contactColor,
                heavy ? .38f : .26f, ContactAngle(normal));
        }
        public void Guard(Vector3 point, Vector3 normal = default(Vector3))
        {
            if (disposed) return;
            GuardDispatches++;
            Burst(point, new Color(1f, .81f, .4f, 1f), .85f, true, normal);
            guardVapor.Play(contact, new Vector2(.7f, .5f), contactColor, .24f, ContactAngle(normal));
        }
        public void Throw(Vector3 point, Vector3 direction)
        {
            if (disposed) return;
            FleshDispatches++;
            Burst(point, new Color(.57f, .67f, 1f, 1f), .65f, false, direction);
            throwVapor.Play(contact, Vector2.one * .7f, contactColor, .28f, ContactAngle(direction));
        }
        private static float ContactAngle(Vector3 direction) =>
            direction.sqrMagnitude < .0001f ? 0f : Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        public void DodgeDust(Vector3 feet)
        {
            if (disposed) return;
            DodgeDispatches++;
            for (int i = 0; i < 6; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = feet + new Vector3(i * .035f, .06f, -.05f),
                    velocity = new Vector3(-.16f - i * .025f, .08f, 0f),
                    startSize = .13f + i * .015f,
                    startLifetime = .3f,
                    startColor = new Color(.3f, .31f, .4f, .26f)
                };
                smoke.Emit(emit, 1);
            }
        }
        private void Burst(Vector3 point, Color color, float strength, bool guard, Vector3 normal)
        {
            // Camera looks +Z; keep the tiny burst just in front of contact geometry.
            contact = point + Vector3.back * .08f;
            contactColor = color;
            burstElapsed = 0f;
            burstDuration = guard ? .16f : .2f;
            for (int i = 0; i < rays.Length; i++)
            {
                float angle = i * 2.399963f;
                Vector3 spread = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .72f, -.05f);
                directions[i] = (spread + normal.normalized * (guard ? .85f : .35f)).normalized;
                rayLengths[i] = strength * (.25f + .2f * Mathf.Abs(Mathf.Sin(i * 1.71f)));
                rays[i].startWidth = strength * .028f;
                rays[i].SetPosition(0, contact);
                rays[i].SetPosition(1, contact + directions[i] * rayLengths[i] * .35f);
                rays[i].startColor = color;
                rays[i].endColor = new Color(color.r, color.g, color.b, 0f);
                rays[i].enabled = true;
            }
            int count = guard ? 18 : strength > 1f ? 24 : 13;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.399963f;
                var emit = new ParticleSystem.EmitParams
                {
                    position = contact,
                    velocity = (new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .8f, -.08f) +
                        normal.normalized * (guard ? 1.2f : .5f)) * strength * (guard ? 2.4f : 1.5f),
                    startSize = guard ? .027f : .035f,
                    startLifetime = .16f + .2f * Mathf.Abs(Mathf.Cos(angle)),
                    startColor = color
                };
                sparks.Emit(emit, 1);
            }
            if (guard) return;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 2.399963f;
                var emit = new ParticleSystem.EmitParams
                {
                    position = contact,
                    velocity = new Vector3(Mathf.Cos(angle) * .28f, .18f + i * .02f, 0f),
                    startSize = strength * (.22f + i * .06f),
                    startLifetime = .36f,
                    rotation = i * 37f,
                    startColor = new Color(.19f, .17f, .24f, .35f)
                };
                smoke.Emit(emit, 1);
            }
        }
        public void Tick(float deltaTime)
        {
            if (disposed) return;
            float step = Mathf.Max(0f, deltaTime);
            if (step <= 0f) return;
            normalVapor.Tick(step); heavyVapor.Tick(step); throwVapor.Tick(step);
            guardVapor.Tick(step); clawVapor.Tick(step);
            burstElapsed += step;
            float t = Mathf.Clamp01(burstElapsed / Mathf.Max(.01f, burstDuration));
            for (int i = 0; i < rays.Length; i++)
            {
                rays[i].enabled = t < 1f;
                if (t >= 1f) continue;
                rays[i].SetPosition(0, contact + directions[i] * rayLengths[i] * t * .28f);
                rays[i].SetPosition(1, contact + directions[i] * rayLengths[i] * (.35f + t * .65f));
                rays[i].startColor = new Color(contactColor.r, contactColor.g, contactColor.b, 1f - t);
            }
            sparks.Simulate(step, false, false, false);
            smoke.Simulate(step, false, false, false);
            sparks.Pause(false);
            smoke.Pause(false);
        }
        private ParticleSystem CreateParticles(string name, int layer, Material material, int cap, bool stretch)
        {
            var item = new GameObject(name, typeof(ParticleSystem));
            item.transform.SetParent(root.transform, false);
            item.layer = layer;
            var system = item.GetComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false; main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = cap; main.startSpeed = 0f;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var fade = system.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.75f, .3f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = stretch ? 2f : 1f;
            renderer.velocityScale = stretch ? .035f : 0f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Pause(false);
            return system;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReactiveCombatAshDissolve.DestroyOwned(root);
            ReactiveCombatAshDissolve.DestroyOwned(sparkMaterial);
            ReactiveCombatAshDissolve.DestroyOwned(smokeMaterial);
            normalVapor.Dispose(); heavyVapor.Dispose(); throwVapor.Dispose();
            guardVapor.Dispose(); clawVapor.Dispose();
        }
    }
}
