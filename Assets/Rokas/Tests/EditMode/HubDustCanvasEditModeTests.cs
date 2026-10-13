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
            var atlas=Resources.Load<Texture2D>(
                "HubDust/T_RokasDustAtlas_4x2_Alpha");
            Assert.IsNotNull(atlas,"Actual source ZIP atlas is required.");
            Assert.AreEqual(128,atlas.width);
            Assert.AreEqual(64,atlas.height);
        }

        [Test]
        public void BothLightsFollowExistingHubSwitchesAtRealPositions()
        {
            // Coordinates from HomeView.Build(): room floor x138, desk x1151,
            // hallway RIGHT at x1748, cool city window x697.
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                138f,458f,false),Is.EqualTo(0f).Within(.00001f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                138f,458f,true),Is.GreaterThan(1f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                1151f,310f,true),Is.GreaterThan(.70f));
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                1748f,345f,true),Is.GreaterThan(.90f));
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                1748f,345f,false),Is.LessThan(.07f));
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                355f,365f,true),Is.LessThan(.001f));
            Assert.That(HubDustCanvasGraphic.WindowContribution(
                697f,345f),Is.GreaterThan(.42f));
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
            Assert.That(micro,Is.LessThanOrEqualTo(.85f));
            Assert.That(bokeh,Is.LessThanOrEqualTo(.45f));
            Assert.That(HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,138f,458f,true,false,0f),Is.EqualTo(0f));
        }

        [Test]
        public void V3StudioVisibilityControlsRemainBounded()
        {
            var root = new GameObject("Dust V3",typeof(RectTransform),
                typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                dust.VisibilityGain=3f;
                dust.ParticleScale=.01f;
                Assert.That(dust.VisibilityGain,Is.EqualTo(2f));
                Assert.That(dust.ParticleScale,Is.EqualTo(.5f));
                dust.VisibilityGain=1.38f;
                dust.ParticleScale=1.12f;
                Assert.That(dust.VisibilityGain,Is.EqualTo(1.38f).Within(.001f));
                Assert.That(dust.ParticleScale,Is.EqualTo(1.12f).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V4FirstRoomFrameMustNotSuppressDustToTwentyEightPercent()
        {
            var root=new GameObject("V4 Dust First Frame",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                dust.Tick(.1f,true,true,true);
                // V3's first-frame count logged 418 particles at .28
                // opacity. A static home scene should start fully settled.
                Assert.That(dust.Visibility,Is.EqualTo(1f).Within(.0001f));
                Assert.AreEqual(432,dust.ParticleBudget);
                Assert.IsFalse(dust.raycastTarget);
                dust.Tick(.1f,false,false,false);
                Assert.That(dust.Visibility,Is.LessThan(1f));
                Assert.That(dust.Visibility,Is.GreaterThan(0f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V4HasDeterministicOpacityTelemetryForGpuDiagnosis()
        {
            var root=new GameObject("V4 Dust Telemetry",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                Assert.IsTrue(dust.LastMeanVertexAlpha>=0f);
                Assert.IsTrue(dust.LastMaxVertexAlpha>=0f);
                Assert.That(HubDustCanvasGraphic.EvaluateDustAlpha(
                    0,1f,138f,458f,true,false),Is.GreaterThan(.2f));
                Assert.That(HubDustCanvasGraphic.EvaluateDustAlpha(
                    0,1f,1300f,1020f,false,false),Is.LessThan(.03f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V5UsesRealSourceAtlasAndHasExplicitVisibilityDiagnostic()
        {
            var root=new GameObject("Dust V5 Atlas",typeof(RectTransform),
                typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                Assert.IsTrue(dust.UsingSourceAtlas);
                Assert.IsFalse(dust.DebugVisibility);
                Assert.That(dust.ShaftStrength,Is.EqualTo(.9f).Within(.0001f));
                dust.DebugVisibility=true;
                Assert.IsTrue(dust.DebugVisibility);
                dust.DebugVisibility=false;
                dust.ShaftStrength=20f;
                Assert.That(dust.ShaftStrength,Is.EqualTo(1.5f));
                dust.ShaftStrength=-2f;
                Assert.That(dust.ShaftStrength,Is.EqualTo(0f));
                Assert.AreEqual(432,dust.ParticleBudget);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V5TrueEightBitHighResolutionAtlasIsGeneratedOnlyOnce()
        {
            var texture=HubDustAtlasFactory.Create();
            try
            {
                Assert.AreEqual(1024,texture.width);
                Assert.AreEqual(512,texture.height);
                Assert.AreEqual(TextureFormat.RGBA32,texture.format);
                Assert.AreEqual(TextureWrapMode.Clamp,texture.wrapMode);
                Assert.AreEqual(FilterMode.Bilinear,texture.filterMode);
                Assert.AreEqual(0f,
                    HubDustAtlasFactory.EvaluateAtlasAlpha(0,.89f,0f));
                Assert.That(HubDustAtlasFactory.EvaluateAtlasAlpha(0,0f,0f),
                    Is.GreaterThan(.97f));
                int unique=0;
                var seen=new bool[256];
                for (int j=0;j<=240;++j)
                {
                    float u=-.87f+j*1.74f/240f;
                    int quant=Mathf.RoundToInt(
                        255f*HubDustAtlasFactory.EvaluateAtlasAlpha(0,u,0f));
                    if (!seen[quant]) { seen[quant]=true; unique++; }
                }
                Assert.That(unique,Is.GreaterThan(32),
                    "V5 must retain more than the old 32 alpha levels.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void V5RealCanvasModesAndScreenIntensityDoNotBreakGameplay()
        {
            var root=new GameObject("Dust V5",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                dust.VisibilityMode=HubDustVisibilityMode.DiagnosticTestPattern;
                Assert.IsTrue(dust.DebugVisibility);
                dust.VisibilityMode=HubDustVisibilityMode.HighContrastBoost;
                Assert.IsFalse(dust.DebugVisibility);
                dust.VisibilityMode=HubDustVisibilityMode.Normal;
                dust.ShaftStrength=2f;
                Assert.That(dust.ShaftStrength,Is.EqualTo(1.5f));
                Assert.AreEqual(432,dust.ParticleBudget);
                Assert.IsFalse(dust.raycastTarget);
                Assert.AreEqual(1024,dust.mainTexture.width);
                Assert.AreEqual(512,dust.mainTexture.height);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V5SingleLightingAttenuationPreservesLitDustAndDarkShadow()
        {
            float light=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,138f,458f,true,true);
            float shade=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,1300f,1020f,false,false);
            float mid=HubDustCanvasGraphic.EvaluateDustAlpha(
                1,1f,138f,458f,true,true);
            float bokeh=HubDustCanvasGraphic.EvaluateDustAlpha(
                2,1f,138f,458f,true,true);
            Assert.That(light,Is.InRange(.55f,.85f));
            Assert.That(mid,Is.InRange(.30f,.45f));
            Assert.That(bokeh,Is.InRange(.30f,.45f));
            Assert.That(shade,Is.LessThan(.02f));
            Assert.That(light,Is.GreaterThan(shade*15f));
        }

        [Test]
        public void DustLayerAllocatesBoundedNonInteractiveGraphics()
        {
            var root = new GameObject("TestDust", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                var dust = root.AddComponent<HubDustCanvasGraphic>();
                Assert.AreEqual(432, dust.ParticleBudget);
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
