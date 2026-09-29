using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public struct VnSceneComposerReplicaEffectSample
    {
        public bool Active;
        public Vector2 Offset;
        public float Scale;
        public Color Flash;
    }

    public static class VnSceneComposerReplicaEffects
    {
        public static VnSceneComposerReplicaEffectSample Sample(
            VnSceneComposerReplicaEffect effect, float elapsedSeconds)
        {
            var result = new VnSceneComposerReplicaEffectSample
            {
                Scale = 1f,
                Flash = Color.clear
            };
            if (effect == null || effect.type == VnSceneComposerReplicaEffectType.None)
                return result;

            float duration = Mathf.Clamp(effect.duration, .05f, 5f);
            float elapsed = Mathf.Max(0f, elapsedSeconds);
            if (elapsed >= duration) return result;
            float progress = Mathf.Clamp01(elapsed / duration);
            float intensity = Mathf.Clamp01(effect.intensity);
            result.Active = true;

            switch (effect.type)
            {
                case VnSceneComposerReplicaEffectType.Shake:
                    float envelope = Mathf.Pow(1f - progress, Mathf.Clamp(effect.decay, .25f, 4f));
                    float cycle = elapsed * Mathf.Clamp(effect.frequency, 1f, 30f) * Mathf.PI * 2f;
                    result.Offset = new Vector2(
                        Mathf.Sin(cycle) * 10f,
                        Mathf.Cos(cycle * 1.31f) * 6f) * intensity * envelope;
                    break;

                case VnSceneComposerReplicaEffectType.Punch:
                    float impulse = Mathf.Sin(Mathf.PI * progress) * intensity;
                    Vector2 direction = effect.direction.sqrMagnitude > .0001f
                        ? effect.direction.normalized : Vector2.right;
                    result.Offset = direction * (30f * impulse);
                    result.Scale = 1f + .035f * impulse;
                    break;

                case VnSceneComposerReplicaEffectType.Flash:
                    Color color = effect.flashColor;
                    color.a = Mathf.Clamp01(color.a * intensity * (1f - progress) * (1f - progress));
                    result.Flash = color;
                    break;

                case VnSceneComposerReplicaEffectType.Pulse:
                    result.Scale = 1f + .03f * intensity * Mathf.Sin(Mathf.PI * progress);
                    break;

                default:
                    result.Active = false;
                    break;
            }
            return result;
        }
    }
}
