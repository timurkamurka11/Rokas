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
        [Range(0f, 1.5f)] public float rainIntensity = .88f;
        [Range(.35f, 2f)] public float rainSpeed = 1f;
        public bool farRainEnabled = true;
        public bool midRainEnabled = true;
        public bool nearRainEnabled = true;

        [Header("Storm")]
        [Min(5f)] public float lightningMinInterval = 16f;
        [Min(8f)] public float lightningMaxInterval = 34f;
        [Range(0f, 1.5f)] public float lightningIntensity = .82f;
        [Range(0f, 1f)] public float thunderVolume = .56f;

        [Header("Atmosphere Mix")]
        [Range(0f, 1f)] public float ambientRainVolume = .72f;
        [Range(0f, 1.5f)] public float glowIntensity = .62f;
        [Range(0f, 1.5f)] public float hazeIntensity = .56f;
        [Range(0f, 1.5f)] public float wetGlassIntensity = .58f;
    }
}
