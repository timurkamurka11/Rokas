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
