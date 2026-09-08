using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class RokasSansFontPlayModeTests
    {
        [UnityTest]
        public IEnumerator FoodGiftMultiplicationSignIsSupportedByRokasSans()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("RokasSans TMP");
            Assert.That(font, Is.Not.Null, "RokasSans TMP must remain the existing Messages font asset");

            bool added = font.TryAddCharacters("×", out string missingCharacters);
            Assert.That(added, Is.True,
                "RokasSans TMP must be able to populate U+00D7 so FoodGift quantity renders as ×1 without fallback warnings");
            Assert.That(string.IsNullOrEmpty(missingCharacters), Is.True,
                "U+00D7 must not remain in the missing character set");
            Assert.That(font.HasCharacter('×'), Is.True,
                "RokasSans TMP must contain U+00D7 after dynamic population");

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
