using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Tests.EditMode
{
    public sealed class LaptopV8BlueReflectionEditModeTests
    {
        [Test]
        public void RealScreenColourProducesBlueDominantSecondaryBounce()
        {
            LaptopScreenBounceEstimator.EvaluateMeanScreenColor(
                new Color(.07f, .14f, .28f, 1f),
                out Color dark, out float darkAmount);
            LaptopScreenBounceEstimator.EvaluateMeanScreenColor(
                new Color(.16f, .35f, .64f, 1f),
                out Color bright, out float brightAmount);
            Assert.That(dark.b, Is.GreaterThan(dark.g));
            Assert.That(dark.g, Is.GreaterThan(dark.r));
            Assert.That(bright.b, Is.GreaterThan(dark.b));
            Assert.That(brightAmount, Is.GreaterThan(darkAmount));
            Assert.That(brightAmount, Is.LessThanOrEqualTo(.96f));
        }

        [Test]
        public void V9DiffuseBounceMatchesScreenHueAndFallsOffBeforeDeskEdge()
        {
            LaptopScreenBounceEstimator.EvaluateMeanScreenColor(
                Color.black, out Color blackTint, out float blackAmount);
            LaptopScreenBounceEstimator.EvaluateMeanScreenColor(
                new Color(.07f, .14f, .28f), out Color blueTint, out float blueAmount);
            LaptopScreenBounceEstimator.EvaluateMeanScreenColor(
                new Color(.70f, .70f, .70f), out Color greyTint, out float greyAmount);
            Assert.That(blackTint.b, Is.LessThan(.24f),
                "A black LCD must not generate a constant blue floodlight.");
            Assert.That(blackAmount, Is.LessThan(.15f));
            Assert.That(blueTint.b, Is.GreaterThan(blackTint.b));
            Assert.That(blueAmount, Is.GreaterThan(blackAmount));
            Assert.That(Mathf.Abs(greyTint.b - greyTint.r), Is.LessThan(.06f),
                "A neutral LCD must not make the desk cyan.");
            Assert.That(greyAmount, Is.GreaterThan(blueAmount));

            float near = LaptopScreenBounceEstimator.EvaluateSurfaceAttenuation(
                new Vector2(824f, 865f));
            float frontEdge = LaptopScreenBounceEstimator.EvaluateSurfaceAttenuation(
                new Vector2(824f, 940f));
            float distant = LaptopScreenBounceEstimator.EvaluateSurfaceAttenuation(
                new Vector2(1570f, 940f));
            Assert.That(near, Is.GreaterThan(.93f));
            Assert.That(frontEdge, Is.LessThan(near * .06f));
            Assert.That(distant, Is.EqualTo(0f).Within(.001f));
        }

        [Test]
        public void HighlightResponseIsContentDrivenNotRandomFlicker()
        {
            LaptopScreenBounceEstimator.EvaluateHighlightResponse(
                .04f, .01f, out float lowHalo, out float lowSpec);
            LaptopScreenBounceEstimator.EvaluateHighlightResponse(
                .50f, .20f, out float highHalo, out float highSpec);
            Assert.That(highHalo, Is.GreaterThan(lowHalo));
            Assert.That(highSpec, Is.GreaterThan(lowSpec));
        }
    }
}
