using System;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Tests
{
    public sealed class CriticalResourceSmokeTests
    {
        private static readonly string[] FoodDetails =
        {
            "Food/Details/TravelerOnigiri",
            "Food/Details/SpicyMiso",
            "Food/Details/HunterTempura",
            "Food/Details/MoonMochi",
            "Food/Details/GreenTea"
        };

        [Test]
        public void MissingTextureProbeProvesGuardCanFail()
        {
            Assert.Throws<AssertionException>(() =>
                RequireResource("Food/Details/__ROKAS_CI_MISSING_PROBE__", Resources.Load<Texture2D>));
        }

        [Test]
        public void ProductionFoodResourcesLoadAsTexture2D()
        {
            RequireResource("Food/ROKAS_FoodAtlas", Resources.Load<Texture2D>);
            foreach (string path in FoodDetails)
                RequireResource(path, Resources.Load<Texture2D>);
        }

        [Test]
        public void CriticalLaptopAudioBindingsArePresent()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null, "Missing Resources/RokasAssets asset.");
            Assert.That(assets.laptopMouseClick, Is.Not.Null, "Missing production LaptopMouseClick binding.");
            Assert.That(assets.lampOn, Is.Not.Null, "Missing production LampOn binding.");
            Assert.That(assets.lampOff, Is.Not.Null, "Missing production LampOff binding.");
        }

        private static void RequireResource<T>(string path, Func<string, T> load) where T : UnityEngine.Object
        {
            Assert.That(load(path), Is.Not.Null, "Missing production Unity resource: " + path);
        }
    }
}
