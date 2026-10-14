using UnityEngine;

namespace Rokas.Presentation
{
    /// <summary>
    /// Result of projecting the current actor / weapon world bounds into
    /// the licensed camera shot. RequiredUniformScale is in (0,1] and is
    /// applied by the existing projection owner, never by this helper.
    /// </summary>
    public readonly struct CombatShotBoundsProjection
    {
        public bool IsValid { get; }
        public bool UsesConservativeFallback { get; }
        public float RequiredUniformScale { get; }

        public CombatShotBoundsProjection(bool isValid,
            bool usesConservativeFallback, float requiredUniformScale)
        {
            IsValid = isValid;
            UsesConservativeFallback = usesConservativeFallback;
            RequiredUniformScale =
                Mathf.Clamp(requiredUniformScale, .15f, 1f);
        }
    }

    /// <summary>
    /// Live world-space actor bounds projected through the existing combat
    /// camera matrix. This restores the missing framing type referenced by
    /// ReactiveCombatArena without modifying its camera choreography.
    /// Renderers are queried each Project call because animated actor
    /// skeletons and thrown projectiles move between frames.
    /// </summary>
    public sealed class CombatShotBounds
    {
        private readonly Transform root;

        public CombatShotBounds(Transform root)
        {
            this.root = root;
        }

        public CombatShotBoundsProjection Project(Matrix4x4 worldToClip,
            Vector2 safeMargin)
        {
            if (root == null)
                return new CombatShotBoundsProjection(false, true, 1f);

            bool hasBounds = false;
            Bounds combined = new Bounds(root.position, Vector3.zero);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy) continue;
                Bounds bounds = renderer.bounds;
                if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                    continue;
                if (!hasBounds) { combined = bounds; hasBounds = true; }
                else combined.Encapsulate(bounds);
            }

            // Projectile effects may temporarily have no live renderer;
            // conservatively frame the root rather than return a NaN.
            if (!hasBounds)
            {
                float size = Mathf.Max(.15f, Mathf.Min(1.5f,
                    root.lossyScale.magnitude * .22f));
                combined = new Bounds(root.position,
                    new Vector3(size, size, size));
            }

            Vector3 c = combined.center, e = combined.extents;
            float maxX = 0f, maxY = 0f;
            int projectedCount = 0;
            bool clippedByNearPlane = false;
            for (int j = 0; j < 8; ++j)
            {
                Vector3 p = new Vector3(
                    c.x + ((j & 1) == 0 ? -e.x : e.x),
                    c.y + ((j & 2) == 0 ? -e.y : e.y),
                    c.z + ((j & 4) == 0 ? -e.z : e.z));
                Vector4 clip = worldToClip * new Vector4(p.x, p.y, p.z, 1f);
                if (!IsFinite(clip.x) || !IsFinite(clip.y) ||
                    !IsFinite(clip.w) || clip.w <= .0001f)
                {
                    clippedByNearPlane = true;
                    continue;
                }
                float nx = Mathf.Abs(clip.x / clip.w);
                float ny = Mathf.Abs(clip.y / clip.w);
                if (!IsFinite(nx) || !IsFinite(ny))
                {
                    clippedByNearPlane = true;
                    continue;
                }
                maxX = Mathf.Max(maxX, nx);
                maxY = Mathf.Max(maxY, ny);
                projectedCount++;
            }

            if (projectedCount == 0)
                return new CombatShotBoundsProjection(false, true, 1f);

            float safeX = Mathf.Clamp(1f - 2f * safeMargin.x, .15f, 1f);
            float safeY = Mathf.Clamp(1f - 2f * safeMargin.y, .15f, 1f);
            float fitX = maxX > .0001f ? safeX / maxX : 1f;
            float fitY = maxY > .0001f ? safeY / maxY : 1f;
            float fit = Mathf.Min(1f, Mathf.Min(fitX, fitY));
            return new CombatShotBoundsProjection(
                true, !hasBounds || clippedByNearPlane, fit);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }
    }
}
