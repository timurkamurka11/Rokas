using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Rokas.Presentation
{
    public static class RokasTmpFontSupport
    {
        private const string PrimaryResource = "RokasSans TMP";
        private const string AssetsResource = "RokasAssets";
        private const char MultiplicationSign = '\u00D7';
        private static TMP_FontAsset multiplicationFallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureMultiplicationFallback();
        }

        public static TMP_FontAsset EnsureMultiplicationFallback()
        {
            TMP_FontAsset primary = Resources.Load<TMP_FontAsset>(PrimaryResource);
            if (primary == null)
            {
                throw new InvalidOperationException(
                    "ROKAS Messages requires the persistent TMP font asset at Assets/Rokas/Resources/RokasSans TMP.asset.");
            }

            if (multiplicationFallback != null)
            {
                AttachFallback(primary, multiplicationFallback);
                return multiplicationFallback;
            }

            List<TMP_FontAsset> existingFallbacks = primary.fallbackFontAssetTable;
            if (existingFallbacks != null)
            {
                for (int index = 0; index < existingFallbacks.Count; index++)
                {
                    TMP_FontAsset existing = existingFallbacks[index];
                    if (existing != null && existing.HasCharacter(MultiplicationSign))
                    {
                        multiplicationFallback = existing;
                        return multiplicationFallback;
                    }
                }
            }

            RokasAssets assets = Resources.Load<RokasAssets>(AssetsResource);
            if (assets == null || assets.sans == null)
            {
                throw new InvalidOperationException(
                    "ROKAS Messages requires RokasAssets.sans to reference the existing RokasSans.ttf source font.");
            }

            TMP_FontAsset fallback = TMP_FontAsset.CreateFontAsset(
                assets.sans,
                32,
                4,
                GlyphRenderMode.SDFAA,
                128,
                128,
                AtlasPopulationMode.Dynamic,
                false);
            if (fallback == null)
            {
                throw new InvalidOperationException("TextMesh Pro could not create the RokasSans dynamic fallback font asset.");
            }

            fallback.name = "RokasSans Dynamic Fallback";
            if (!fallback.TryAddCharacters(MultiplicationSign.ToString(), out string missingCharacters, true) ||
                !string.IsNullOrEmpty(missingCharacters) ||
                !fallback.HasCharacter(MultiplicationSign))
            {
                UnityEngine.Object.Destroy(fallback);
                throw new InvalidOperationException(
                    "RokasSans dynamic fallback could not populate U+00D7 from the existing RokasSans.ttf source font.");
            }

            multiplicationFallback = fallback;
            AttachFallback(primary, multiplicationFallback);
            return multiplicationFallback;
        }

        private static void AttachFallback(TMP_FontAsset primary, TMP_FontAsset fallback)
        {
            List<TMP_FontAsset> fallbacks = primary.fallbackFontAssetTable;
            if (fallbacks == null)
            {
                fallbacks = new List<TMP_FontAsset>();
                primary.fallbackFontAssetTable = fallbacks;
            }
            if (!fallbacks.Contains(fallback))
            {
                fallbacks.Add(fallback);
            }
        }
    }
}
