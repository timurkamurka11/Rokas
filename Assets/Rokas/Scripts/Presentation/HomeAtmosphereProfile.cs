using UnityEngine;

namespace Rokas.Presentation
{
    [CreateAssetMenu(fileName = "HomeAtmosphereProfile", menuName = "ROKAS/Home Atmosphere Profile")]
    public sealed class HomeAtmosphereProfile : ScriptableObject
    {
        [Header("Master")]
        public bool enabled = true;
        public bool rainEnabled = true;
        public bool lightningEnabled = true;

        [Header("Rain")]
        [Range(0f, 1.5f)] public float rainIntensity = 1.18f;
        [Range(.35f, 2f)] public float rainSpeed = 1.12f;
        public bool farRainEnabled = true;
        public bool midRainEnabled = true;
        public bool nearRainEnabled = true;

        [Header("Storm")]
        [Min(5f)] public float lightningMinInterval = 16f;
        [Min(8f)] public float lightningMaxInterval = 34f;
        [Range(0f, 1.5f)] public float lightningIntensity = 1.02f;
        [Range(0f, 1f)] public float thunderVolume = .56f;

        [Header("Atmosphere Mix")]
        [Range(0f, 1f)] public float ambientRainVolume = .72f;
        [Range(0f, 1.5f)] public float glowIntensity = .82f;
        [Range(0f, 1.5f)] public float hazeIntensity = .88f;
        [Range(0f, 1.5f)] public float wetGlassIntensity = .92f;

        [Header("2.5D Layered Motion")]
        [Range(0f, 1.5f)] public float parallaxIntensity = .85f;
        [Range(0f, .45f)] public float cursorParallaxStrength = .16f;
        [Range(.5f, 10f)] public float parallaxSmoothing = 4.2f;
        [Range(0f, 2.25f)] public float farParallaxPixels = .72f;
        [Range(0f, 2.25f)] public float roomParallaxPixels = 1.45f;
        [Range(0f, 4.25f)] public float nearParallaxPixels = 3.85f;
        [Range(0f, 1.5f)] public float outsideMotionIntensity = .92f;
        [Range(.5f, 1.5f)] public float weatherReadability = 1.16f;
    }
}
