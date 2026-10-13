using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class HubDustCanvasEditModeTests
    {
        [Test]
        public void DustShaderExistsInResourcesWithoutExternalPackages()
        {
            var shader = Resources.Load<Shader>("Shaders/HubDustSoftMotes");
            Assert.IsNotNull(shader, "2D dust must ship with the Built-in Unity build.");
            Assert.IsTrue(shader.isSupported);
        }

        [Test]
        public void BothLightsFollowOneSetOfSavedSwitches()
        {
            var x = 1225f;
            var y = 480f;
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(x,y,false),
                Is.EqualTo(0f).Within(.00001f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(x,y,true),
                Is.GreaterThan(.70f));
            Assert.That(HubDustCanvasGraphic.HallwayContribution(355f,365f,true),
                Is.GreaterThan(HubDustCanvasGraphic.HallwayContribution(355f,365f,false)*4f));
            Assert.That(HubDustCanvasGraphic.HallwayContribution(1550f,800f,true),
                Is.LessThan(.001f));
        }

        [Test]
        public void DustLayerAllocatesBoundedNonInteractiveGraphics()
        {
            var root = new GameObject("TestDust", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                var dust = root.AddComponent<HubDustCanvasGraphic>();
                Assert.AreEqual(192, dust.ParticleBudget);
                Assert.IsFalse(dust.raycastTarget);
                dust.Tick(.50f, true, true, false);
                Assert.IsTrue(root.activeSelf);
                Assert.That(dust.Visibility,Is.GreaterThan(.99f));
                dust.Tick(.50f, false, false, false);
                Assert.IsFalse(root.activeSelf);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
