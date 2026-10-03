using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rokas.Presentation
{
    // Sample the real equipped blade, only while its authored acceleration window
    // is active. Old positions form the ribbon; no independent weapon trajectory.
    public sealed class ReactiveCombatSwordEffect : IDisposable
    {
        private const int Samples = 32;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private readonly Transform blade;
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material trailMaterial;
        private readonly Material chargeMaterial;
        private readonly LineRenderer charge;
        private readonly ReactiveCombatEmberLayer lightVapor;
        private readonly ReactiveCombatEmberLayer heavyVapor;
        private readonly Vector3[] bases = new Vector3[Samples];
        private readonly Vector3[] tips = new Vector3[Samples];
        private readonly float[] ages = new float[Samples];
        private readonly Vector3[] vertices = new Vector3[Samples * 2];
        private readonly Color[] colors = new Color[Samples * 2];
        private readonly Vector3[] chargePoints = new Vector3[40];
        private float tipLocalZ = 1.25f;
        private int count;
        private float elapsed;
        private float contactGlow;
        private bool wasActive;
        private bool disposed;
        public int ActiveSamples => count;
        public bool Emitting => wasActive;
        public float PresentationElapsed => elapsed;
        public Vector3 BladeTip => blade == null ? Vector3.zero : blade.TransformPoint(new Vector3(0, 0, tipLocalZ));
        public Vector3 BladePointNearest(Vector3 target)
        {
            if (blade == null) return target;
            Vector3 start = blade.TransformPoint(new Vector3(0, 0, tipLocalZ * .18f));
            Vector3 arc = BladeTip - start;
            float fraction = arc.sqrMagnitude > .00001f ? Vector3.Dot(target - start, arc) / arc.sqrMagnitude : 0f;
            return start + arc * Mathf.Clamp01(fraction);
        }

        public ReactiveCombatSwordEffect(ReactiveCombatActorVisual actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            var attachment = actor.GetComponentInChildren<ReactiveCombatWeaponAttachment>(true);
            blade = attachment == null || attachment.CurrentWeapon == null ? null : attachment.CurrentWeapon.transform;
            root = new GameObject("ReactiveSwordEnergy", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(actor.transform.parent, false);
            root.layer = actor.gameObject.layer;
            Shader shader = Resources.Load<Shader>("Combat/ReactiveCombatBladeTrail");
            if (shader == null) throw new InvalidOperationException("Missing combat blade trail shader.");
            trailMaterial = new Material(shader) { name = "Actual Blade Motion Ribbon" };
            chargeMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Heavy Circular Sword Charge", Color.white, 0f);
            chargeMaterial.SetFloat("_SoftShape", 2f);
            mesh = new Mesh { name = "Reactive Blade History" };
            mesh.MarkDynamic();
            var uv = new Vector2[Samples * 2];
            var triangles = new int[(Samples - 1) * 6];
            for (int i = 0; i < Samples; i++)
            {
                uv[i * 2] = new Vector2((float)i / (Samples - 1), 0);
                uv[i * 2 + 1] = new Vector2((float)i / (Samples - 1), 1);
                if (i == Samples - 1) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uv;
            mesh.triangles = triangles;
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = trailMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            var item = new GameObject("HeavyChargeSweep", typeof(LineRenderer));
            item.transform.SetParent(root.transform, false);
            item.layer = root.layer;
            charge = item.GetComponent<LineRenderer>();
            charge.useWorldSpace = true;
            charge.positionCount = chargePoints.Length;
            charge.startWidth = .032f;
            charge.endWidth = .005f;
            charge.startColor = new Color(.56f, .69f, 1f, .65f);
            charge.endColor = new Color(.87f, .95f, 1f, 0f);
            charge.sharedMaterial = chargeMaterial;
            charge.shadowCastingMode = ShadowCastingMode.Off;
            charge.receiveShadows = false;
            charge.enabled = false;
            lightVapor = new ReactiveCombatEmberLayer(root.transform.parent, root.layer, "NormalMist");
            // Held preparation needs continuous wisps. The one-shot HeavyBurst
            // belongs to the actual contact, where its smoke can grow and retire.
            heavyVapor = new ReactiveCombatEmberLayer(root.transform.parent, root.layer, "NormalMist");
            MeasureBlade();
        }

        private void MeasureBlade()
        {
            if (blade == null) return;
            float maximum = 0f;
            foreach (var filter in blade.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    maximum = Mathf.Max(maximum, blade.InverseTransformPoint(filter.transform.TransformPoint(corner)).z);
                }
            }
            if (maximum > .1f) tipLocalZ = maximum * .94f;
        }

        public void Contact(Vector3 worldPoint, bool heavy)
        {
            // Body impact is owned by ReactiveCombatImpactEffect, dispatched once.
            if (!disposed)
            {
                contactGlow = heavy ? .12f : .08f;
                // Tick pauses during hit-stop; emphasize the same contact frame now.
                trailMaterial.SetFloat(OpacityId, 1f);
            }
        }

        public void Tick(float deltaTime, bool swingActive, bool heavy, float charge01 = 0f)
        {
            if (disposed) return;
            float step = Mathf.Max(0f, deltaTime);
            if (step <= 0f) return;
            elapsed += step;
            contactGlow = Mathf.Max(0f, contactGlow - step);
            float lifetime = heavy ? .22f : .14f;
            for (int i = 0; i < count; i++) ages[i] += step;
            while (count > 0 && ages[count - 1] >= lifetime) count--;
            if (swingActive && blade != null)
            {
                Vector3 tip = root.transform.InverseTransformPoint(BladeTip);
                Vector3 start = root.transform.InverseTransformPoint(blade.TransformPoint(new Vector3(0, 0,
                    tipLocalZ * (heavy ? .18f : .58f))));
                if (!wasActive) count = 0;
                if (count == 0 || (tips[0] - tip).sqrMagnitude > .00002f) AddSample(start, tip);
            }
            wasActive = swingActive;
            Vector3 bladeCenter = blade == null ? Vector3.zero : Vector3.Lerp(blade.position, BladeTip, .55f);
            Vector3 bladeDirection = blade == null ? Vector3.right : BladeTip - blade.position;
            float bladeAngle = Mathf.Atan2(bladeDirection.y, bladeDirection.x) * Mathf.Rad2Deg;
            lightVapor.Follow(bladeCenter + Vector3.back * .07f, new Vector2(.9f, .45f),
                new Color(.52f, .86f, 1f, 1f), swingActive && !heavy ? .45f : 0f, bladeAngle);
            heavyVapor.Follow(bladeCenter + Vector3.back * .09f, new Vector2(1.35f, .8f),
                new Color(.65f, .49f, 1f, 1f), heavy && (swingActive || charge01 > .001f) ? .38f : 0f, bladeAngle);
            lightVapor.Tick(step); heavyVapor.Tick(step);
            for (int i = 0; i < Samples; i++)
            {
                int source = Mathf.Min(i, Mathf.Max(0, count - 1));
                vertices[i * 2] = bases[source];
                vertices[i * 2 + 1] = tips[source];
                float opacity = i < count ? Mathf.Pow(1f - Mathf.Clamp01(ages[i] / lifetime), 1.4f) * (heavy ? .8f : .65f) : 0f;
                colors[i * 2] = colors[i * 2 + 1] = new Color(1f, 1f, 1f, opacity);
            }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
            renderer.enabled = count > 1;
            trailMaterial.SetColor(ColorId, heavy ? new Color(.63f, .66f, 1f, 1f) : new Color(.66f, .9f, 1f, 1f));
            trailMaterial.SetFloat(OpacityId, contactGlow > 0 ? 1f : .9f);
            charge.enabled = heavy && charge01 > .001f && blade != null;
            if (charge.enabled)
            {
                Vector3 center = blade.position;
                float radius = Mathf.Lerp(.32f, .75f, Mathf.Clamp01(charge01));
                for (int i = 0; i < chargePoints.Length; i++)
                {
                    float t = (float)i / (chargePoints.Length - 1);
                    float angle = elapsed * 5.5f - t * Mathf.PI * 1.7f;
                    chargePoints[i] = center + new Vector3(Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius, -.08f);
                }
                charge.SetPositions(chargePoints);
                chargeMaterial.SetFloat(OpacityId, Mathf.Sin(Mathf.Clamp01(charge01) * Mathf.PI) * .7f);
            }
        }
        private void AddSample(Vector3 start, Vector3 tip)
        {
            count = Mathf.Min(count + 1, Samples);
            for (int i = count - 1; i > 0; i--)
            {
                bases[i] = bases[i - 1]; tips[i] = tips[i - 1]; ages[i] = ages[i - 1];
            }
            bases[0] = start; tips[0] = tip; ages[0] = 0f;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReactiveCombatAshDissolve.DestroyOwned(root);
            ReactiveCombatAshDissolve.DestroyOwned(mesh);
            ReactiveCombatAshDissolve.DestroyOwned(trailMaterial);
            ReactiveCombatAshDissolve.DestroyOwned(chargeMaterial);
            lightVapor.Dispose(); heavyVapor.Dispose();
        }
    }
}
