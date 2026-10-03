using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Presentation
{
    // One entering actor owns its temporary surface materials. The fixed world
    // plane clips the actual skinned geometry; movement remains authoritative in Arena.
    public sealed class ReactiveCombatPortalEmergence : IDisposable
    {
        private sealed class Binding
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Owned;
        }
        private readonly ReactiveCombatActorVisual actor;
        private readonly ReactiveCombatPortalEffect portal;
        private readonly List<Binding> bindings = new List<Binding>();
        private bool begun;
        private bool disposed;
        public bool Begun => begun;

        public ReactiveCombatPortalEmergence(ReactiveCombatActorVisual actor, ReactiveCombatPortalEffect portal)
        {
            this.actor = actor;
            this.portal = portal;
        }

        public void Begin()
        {
            if (begun || disposed || actor == null || actor.ModelRoot == null || portal == null) return;
            Shader shader = Resources.Load<Shader>("Combat/ReactiveCombatPortalEmergence");
            if (shader == null) throw new InvalidOperationException("Missing combat portal emergence shader.");
            foreach (Renderer renderer in actor.ModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                var original = renderer.sharedMaterials;
                var owned = new Material[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;
                    var material = new Material(shader) { name = original[i].name + " (Portal Emergence)" };
                    material.CopyPropertiesFromMaterial(original[i]);
                    // The dark depth surface has no depth write. Draw the clipped
                    // body after it so an inside silhouette remains readable; once
                    // crossing, its opaque surface correctly covers the portal rim.
                    material.renderQueue = 3001;
                    SetPortalFrame(material);
                    owned[i] = material;
                }
                renderer.sharedMaterials = owned;
                bindings.Add(new Binding { Renderer = renderer, Original = original, Owned = owned });
            }
            begun = true;
        }

        // Movement crosses a fixed plane; only the living boundary changes.
        // Both the depth surface and actor mask use the same presentation clock.
        public void Tick()
        {
            if (!begun || disposed) return;
            foreach (Binding binding in bindings)
                foreach (Material material in binding.Owned)
                    if (material != null) SetPortalFrame(material);
        }

        private void SetPortalFrame(Material material)
        {
            material.SetVector("_PortalCenter", portal.ApertureCenter);
            material.SetVector("_PortalNormal", portal.PlaneNormal);
            material.SetVector("_PortalRight", portal.PlaneRight);
            Vector2 radius = portal.ApertureRadii;
            material.SetVector("_PortalRadius", new Vector4(Mathf.Max(.001f, radius.x),
                Mathf.Max(.001f, radius.y), portal.MaskDepth, 0f));
            material.SetFloat("_PortalPhase", portal.PresentationElapsed);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Binding binding in bindings)
            {
                if (binding.Renderer != null)
                {
                    Material[] current = binding.Renderer.sharedMaterials;
                    for (int i = 0; i < current.Length && i < binding.Owned.Length; i++)
                        if (current[i] == binding.Owned[i]) current[i] = binding.Original[i];
                    binding.Renderer.sharedMaterials = current;
                }
                foreach (Material material in binding.Owned) ReactiveCombatAshDissolve.DestroyOwned(material);
            }
            bindings.Clear();
        }
    }
}
