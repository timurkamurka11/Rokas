using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rokas.Presentation
{
    // Plays genuine EmberGen pixels at the live weapon/contact/portal transform.
    // This layer cannot move an actor, dispatch audio, or resolve combat damage.
    public sealed class ReactiveCombatEmberLayer : IDisposable
    {
        public const string ResourceFolder = "Combat/ReactiveTurns/Vfx/EmberGen/";
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Material material;
        private float age, duration = 1f, opacity;
        private Color tint;
        private bool looping, active, disposed;
        public Texture2D SourceAtlas { get; }
        public bool Visible => active && renderer.enabled;

        public ReactiveCombatEmberLayer(Transform parent, int layer, string source)
        {
            SourceAtlas = Resources.Load<Texture2D>(ResourceFolder + source);
            root = new GameObject("EmberGen " + source, typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false);
            root.layer = layer;
            mesh = new Mesh { name = "EmberGen carrier " + source };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
                new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = root.GetComponent<MeshRenderer>();
            var shader = Resources.Load<Shader>("Combat/ReactiveCombatEmberFlipbook");
            if (shader == null) throw new InvalidOperationException("Missing EmberGen flipbook shader.");
            material = new Material(shader) { name = "EmberGen " + source + " premultiplied RGBA" };
            material.SetTexture("_MainTex", SourceAtlas);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }

        public void Play(Vector3 point, Vector2 size, Color color, float seconds, float angle = 0f)
        {
            if (disposed || SourceAtlas == null) return;
            age = 0f; duration = Mathf.Max(.05f, seconds);
            looping = false; active = true; opacity = 1f; tint = color;
            root.transform.position = point;
            root.transform.rotation = Quaternion.Euler(0, 0, angle);
            root.transform.localScale = new Vector3(size.x, size.y, 1f);
            UpdateFrame();
        }

        public void Follow(Vector3 point, Vector2 size, Color color, float alpha, float angle = 0f)
        {
            if (disposed) return;
            if (!active && alpha > .001f) age = 0f;
            looping = true; duration = 1.2f; active = alpha > .001f;
            tint = color; opacity = Mathf.Clamp01(alpha);
            root.transform.position = point;
            root.transform.rotation = Quaternion.Euler(0, 0, angle);
            root.transform.localScale = new Vector3(size.x, size.y, 1f);
            UpdateFrame();
        }

        public void FollowLocal(Vector3 point, Vector2 size, Color color, float alpha, float phaseOffset = 0f)
        {
            if (disposed) return;
            if (!active && alpha > .001f) age = 0f;
            looping = true; duration = 2.3f; active = alpha > .001f;
            tint = color; opacity = Mathf.Clamp01(alpha);
            root.transform.localPosition = point;
            root.transform.localRotation = Quaternion.Euler(0, 0, phaseOffset);
            root.transform.localScale = new Vector3(size.x, size.y, 1f);
            UpdateFrame();
        }

        public void Tick(float delta)
        {
            if (disposed) return;
            age += Mathf.Max(0f, delta);
            if (!looping && age >= duration) active = false;
            UpdateFrame();
        }

        private void UpdateFrame()
        {
            renderer.enabled = active && SourceAtlas != null;
            if (!renderer.enabled) return;
            float progress = looping ? Mathf.PingPong(age / duration, 1f) : Mathf.Clamp01(age / duration);
            float fade = looping ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.65f, 1f, progress));
            material.SetFloat("_Frame", progress * 63f);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Opacity", opacity * fade);
        }

        public void Stop()
        {
            if (disposed) return;
            active = false; renderer.enabled = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReactiveCombatAshDissolve.DestroyOwned(root);
            ReactiveCombatAshDissolve.DestroyOwned(mesh);
            ReactiveCombatAshDissolve.DestroyOwned(material);
        }
    }
}
