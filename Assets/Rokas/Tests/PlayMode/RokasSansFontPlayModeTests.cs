using System.Collections;
using NUnit.Framework;
using Rokas.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TextCore.LowLevel;

namespace Rokas.Tests
{
    public sealed class RokasSansFontPlayModeTests
    {
        [UnityTest]
        public IEnumerator FoodGiftMultiplicationSignIsSupportedByRokasSans()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("RokasSans TMP");
            Assert.That(font, Is.Not.Null, "RokasSans TMP must remain the existing Messages font asset");

            TMP_FontAsset fallback = RokasTmpFontSupport.EnsureMultiplicationFallback();
            Assert.That(fallback, Is.Not.Null,
                "RokasSans TMP must have a runtime fallback for FoodGift quantity rendering");
            Assert.That(fallback.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic),
                "the cached RokasSans fallback must remain Dynamic so U+00D7 is sourced from RokasSans.ttf");
            Assert.That(font.fallbackFontAssetTable, Does.Contain(fallback),
                "the existing RokasSans TMP asset must route missing glyphs through the cached fallback");
            Assert.That(fallback.HasCharacter('×'), Is.True,
                "the cached RokasSans fallback must contain U+00D7 so FoodGift quantity renders as ×1 without warnings");
            Assert.That(RokasTmpFontSupport.EnsureMultiplicationFallback(), Is.SameAs(fallback),
                "the Dynamic fallback must be cached and reused rather than recreated per label or render");

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
