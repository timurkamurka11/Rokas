using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rokas.Presentation
{
    // A single-use blade. Release and contact are separate, exactly-once presentation
    // events; neither this projectile nor its trail can apply combat damage.
    public sealed class ReactiveCombatThrowEffect : IDisposable
    {
        private readonly GameObject root;
        private readonly GameObject projectile;
        private readonly Material energy;
        private readonly Material moteMaterial;
        private readonly ParticleSystem motes;
        private readonly LineRenderer trail;
        private readonly LineRenderer spiral;
        private readonly LineRenderer charge;
        private readonly ReactiveCombatEmberLayer chargeVapor;
        private readonly ReactiveCombatEmberLayer releaseVapor;
        private readonly ReactiveCombatEmberLayer flightVapor;
        private Vector3 start, goal;
        private float elapsed, duration, resolvedAge, chargeAge, emissionAge;
        private Vector3 chargePoint;
        private bool charging;
        private int emitted;
        private string action;
        private bool released, resolved;
        public int ReleaseCount { get; private set; }
        public int ContactCount { get; private set; }
        public Vector3 ContactPosition => goal;
        public bool InFlight => released && !resolved && elapsed < duration;
        public int ParticleCount => motes.particleCount;

        public ReactiveCombatThrowEffect(Transform parent, int layer, GameObject dagger)
        {
            root = new GameObject("ReactiveThrowPresentation");
            root.transform.SetParent(parent, false);
            projectile = UnityEngine.Object.Instantiate(dagger, root.transform, false);
            projectile.name = "KeikoThrownDagger";
            foreach (Transform child in projectile.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            projectile.SetActive(false);
            energy = ReactiveCombatAshDissolve.CreateQuietMaterial("Throw blade energy", Color.white, 1f);
            trail = Line("ThrowTrail", layer, 18, .11f);
            spiral = Line("ThrowEnergyHelix", layer, 24, .023f);
            charge = Line("ThrowCharge", layer, 25, .015f);
            moteMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Throw energy motes", Color.white, 1f);
            moteMaterial.SetFloat("_SoftShape", 1f);
            var motesObject = new GameObject("ThrowReleaseAndFlightMotes", typeof(ParticleSystem));
            motesObject.transform.SetParent(root.transform, false);
            motesObject.layer = layer;
            motes = motesObject.GetComponent<ParticleSystem>();
            var main = motes.main;
            main.playOnAwake = false; main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48; main.startSpeed = 0f;
            var emission = motes.emission; emission.enabled = false;
            var shape = motes.shape; shape.enabled = false;
            var color = motes.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.7f, .25f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var size = motes.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var renderer = motes.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = moteMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f; renderer.velocityScale = .025f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            motes.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            motes.Pause(false);
            chargeVapor = new ReactiveCombatEmberLayer(parent, layer, "ThrowEnergy");
            releaseVapor = new ReactiveCombatEmberLayer(parent, layer, "ThrowEnergy");
            flightVapor = new ReactiveCombatEmberLayer(parent, layer, "ThrowEnergy");
        }

        private LineRenderer Line(string name, int layer, int count, float width)
        {
            var obj = new GameObject(name, typeof(LineRenderer));
            obj.transform.SetParent(root.transform, false);
            obj.layer = layer;
            var line = obj.GetComponent<LineRenderer>();
            line.sharedMaterial = energy;
            line.useWorldSpace = true;
            line.positionCount = count;
            line.startWidth = width;
            line.endWidth = .001f;
            line.startColor = new Color(.55f, .78f, 1f, .9f);
            line.endColor = new Color(.53f, .4f, 1f, 0f);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        public bool Release(string id, Vector3 origin, Vector3 contact, Vector3 scale, float flightSeconds)
        {
            if (string.IsNullOrEmpty(id) || action == id) return false;
            action = id;
            start = origin;
            goal = contact;
            elapsed = resolvedAge = 0f;
            duration = Mathf.Max(.08f, flightSeconds);
            released = true;
            resolved = false;
            ReleaseCount++;
            projectile.transform.position = start;
            projectile.transform.localScale = scale;
            projectile.transform.rotation = Quaternion.LookRotation((goal - start).normalized, Vector3.up);
            projectile.SetActive(true);
            charge.enabled = false;
            charging = false;
            emissionAge = 0f;
            Vector3 direction = (goal - start).normalized;
            releaseVapor.Play(start + Vector3.back * .07f, new Vector2(.85f, .6f),
                new Color(.57f, .8f, 1f, 1f), .22f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            // A brief fan from the release hand, followed by a sparse wake. The
            // blade stays readable between these layers; the burst is not contact.
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 2.399963f;
                EmitMote(start, direction * (1.1f + i * .035f) +
                    new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle)) * .65f,
                    .025f + (i % 3) * .007f, .18f + (i % 4) * .035f);
            }
            return true;
        }

        public bool ResolveContact()
        {
            if (!released || resolved) return false;
            resolved = true;
            elapsed = duration;
            projectile.transform.position = goal;
            ContactCount++;
            return true;
        }

        public void Cancel()
        {
            released = charging = false;
            resolved = true;
            projectile.SetActive(false);
            charge.enabled = trail.enabled = spiral.enabled = false;
            motes.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            motes.Pause(false);
            chargeVapor.Stop(); releaseVapor.Stop(); flightVapor.Stop();
        }

        private Vector3 Trajectory(float progress)
        {
            float t = Mathf.Clamp01(progress);
            return Vector3.Lerp(start, goal, t) + Vector3.up * (.12f * Mathf.Sin(t * Mathf.PI));
        }

        public void PreviewCharge(Vector3 point, bool visible)
        {
            charge.enabled = visible;
            charging = visible;
            chargePoint = point;
            chargeVapor.Follow(point + Vector3.back * .07f, Vector2.one * .55f,
                new Color(.58f, .72f, 1f, 1f), visible ? .65f : 0f);
            if (!visible) return;
            for (int i = 0; i < charge.positionCount; i++)
            {
                float a = i / 24f * Mathf.PI * 2f + chargeAge * 4f;
                float radius = .17f + .02f * Mathf.Sin(chargeAge * 5f);
                charge.SetPosition(i, point + new Vector3(Mathf.Cos(a), Mathf.Sin(a), -.12f) * radius);
            }
        }

        public void Tick(float delta)
        {
            float step = Mathf.Max(0f, delta);
            chargeAge += step;
            chargeVapor.Tick(step); releaseVapor.Tick(step); flightVapor.Tick(step);
            emissionAge += step;
            if (charging || InFlight)
            {
                float interval = charging ? .09f : .025f;
                int count = Mathf.Min(3, Mathf.FloorToInt(emissionAge / interval));
                emissionAge %= interval;
                for (int i = 0; i < count; i++)
                {
                    float angle = emitted++ * 2.399963f + chargeAge * 3f;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -.3f);
                    Vector3 point = charging ? chargePoint + radial * .19f : Trajectory(elapsed / duration);
                    Vector3 velocity = charging ? -radial * .28f :
                        -(goal - start).normalized * .4f + radial * .22f;
                    EmitMote(point, velocity, charging ? .022f : .031f, charging ? .28f : .22f);
                }
            }
            else emissionAge = 0f;
            motes.Simulate(step, false, false, false);
            motes.Pause(false);
            if (!released) return;
            elapsed = Mathf.Min(duration, elapsed + step);
            if (resolved) resolvedAge += step;
            if (resolved && resolvedAge >= .16f)
            {
                projectile.SetActive(false);
                trail.enabled = spiral.enabled = false;
                flightVapor.Stop();
                released = false;
                return;
            }
            float t = elapsed / duration;
            projectile.transform.position = Trajectory(t);
            float fade = resolved ? Mathf.Clamp01(1f - resolvedAge / .16f) : 1f;
            projectile.transform.localScale *= resolved ? Mathf.Pow(.001f, step / .16f) : 1f;
            trail.enabled = spiral.enabled = fade > 0f;
            Vector3 direction = (goal - start).normalized;
            flightVapor.Follow(projectile.transform.position - direction * .2f + Vector3.back * .06f,
                new Vector2(.65f, .3f), new Color(.53f, .76f, 1f, 1f), InFlight ? .75f : 0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Vector3 side = Vector3.Cross(direction, Vector3.forward).normalized;
            for (int i = 0; i < trail.positionCount; i++)
                trail.SetPosition(i, Trajectory(t - i / 17f * .4f));
            for (int i = 0; i < spiral.positionCount; i++)
            {
                float f = i / 23f;
                float phase = f * Mathf.PI * 4f - elapsed * 24f;
                spiral.SetPosition(i, Trajectory(t - f * .19f) +
                    (side * Mathf.Sin(phase) + Vector3.forward * Mathf.Cos(phase)) * (.065f * Mathf.Sin(f * Mathf.PI)));
            }
            trail.startColor = new Color(.72f, .92f, 1f, fade);
            spiral.startColor = new Color(.58f, .42f, 1f, fade * .8f);
        }

        private void EmitMote(Vector3 point, Vector3 velocity, float size, float lifetime)
        {
            motes.Emit(new ParticleSystem.EmitParams { position = point, velocity = velocity,
                startSize = size, startLifetime = lifetime,
                startColor = new Color(.55f, .73f, 1f, .85f) }, 1);
        }

        public void Dispose()
        {
            ReactiveCombatAshDissolve.DestroyOwned(root);
            ReactiveCombatAshDissolve.DestroyOwned(energy);
            ReactiveCombatAshDissolve.DestroyOwned(moteMaterial);
            chargeVapor.Dispose(); releaseVapor.Dispose(); flightVapor.Dispose();
        }
    }
}
