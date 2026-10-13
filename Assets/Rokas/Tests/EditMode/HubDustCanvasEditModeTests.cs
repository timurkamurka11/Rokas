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
        public void V2DustIsVisibleNearActualHubLightSources()
        {
            // These 1920x1080 hotspot coordinates are from HomeView.Build().
            float floor=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,138f,458f,true,false);
            float desk=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,1151f,310f,true,false);
            float window=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,697f,345f,false,false);
            float corridor=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,1748f,345f,false,true);
            float darkCorner=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,1300f,1020f,false,false);
            Assert.That(floor,Is.GreaterThan(.20f));
            Assert.That(desk,Is.GreaterThan(.17f));
            Assert.That(window,Is.GreaterThan(.11f));
            Assert.That(corridor,Is.GreaterThan(.25f));
            Assert.That(darkCorner,Is.LessThan(.02f));
            Assert.That(HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,1748f,345f,false,true),
                Is.GreaterThan(HubDustCanvasGraphic.EvaluateDustAlpha(
                    0,1f,1748f,345f,false,false)*5f));
        }

        [Test]
        public void V2DustPreservesParticleLayerHierarchyAndSubtleBokeh()
        {
            float micro=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,138f,458f,true,false);
            float ambient=HubDustCanvasGraphic.EvaluateDustAlpha(
                1,1f,138f,458f,true,false);
            float bokeh=HubDustCanvasGraphic.EvaluateDustAlpha(
                2,1f,138f,458f,true,false);
            Assert.That(micro,Is.GreaterThan(ambient));
            Assert.That(ambient,Is.GreaterThan(bokeh));
            Assert.That(micro,Is.LessThanOrEqualTo(.34f));
            Assert.That(bokeh,Is.LessThanOrEqualTo(.34f));
            Assert.That(HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,138f,458f,true,false,0f),Is.EqualTo(0f));
        }

        [Test]
        public void DustLayerAllocatesBoundedNonInteractiveGraphics()
        {
            var root = new GameObject("TestDust", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                var dust = root.AddComponent<HubDustCanvasGraphic>();
                Assert.AreEqual(320, dust.ParticleBudget);
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
