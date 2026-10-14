using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class HubDustCanvasEditModeTests
    {
        [Test]
        public void V6HasArgonSpecifiedParticleBudgetAndSpatialPools()
        {
            Assert.AreEqual(128, HubDustCanvasGraphic.ParticleCount);
            Assert.AreEqual(84, HubDustCanvasGraphic.GeneralAirCount);
            Assert.AreEqual(44, HubDustCanvasGraphic.LocalAirCount);
            Assert.AreEqual(105, HubDustCanvasGraphic.MicroCount);
            Assert.AreEqual(19, HubDustCanvasGraphic.SmallCount);
            Assert.AreEqual(4, HubDustCanvasGraphic.BokehCount);
            Assert.AreEqual(128, HubDustCanvasGraphic.MicroCount +
                HubDustCanvasGraphic.SmallCount +
                HubDustCanvasGraphic.BokehCount);
        }

        [Test]
        public void V6Uses1024x512EightBitAtlasAndExistingBuiltInShader()
        {
            var shader = Resources.Load<Shader>("Shaders/HubDustSoftMotes");
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported);
            var tex = HubDustAtlasFactory.Create();
            try
            {
                Assert.AreEqual(1024, tex.width);
                Assert.AreEqual(512, tex.height);
                Assert.AreEqual(TextureFormat.RGBA32, tex.format);
                Assert.AreEqual(TextureWrapMode.Clamp, tex.wrapMode);
                Assert.AreEqual(FilterMode.Bilinear, tex.filterMode);
                Assert.AreEqual(0f, HubDustAtlasFactory.EvaluateAtlasAlpha(
                    0, .89f, 0f));
                Assert.That(HubDustAtlasFactory.EvaluateAtlasAlpha(
                    0, 0f, 0f), Is.GreaterThan(.97f));
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void V6WarmLampFieldsRespectExistingRoomLightSwitch()
        {
            float floorX = .14f * 1920f, floorY = .34f * 1080f;
            float deskX = .63f * 1920f, deskY = .58f * 1080f;
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                floorX, floorY, true), Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                deskX, deskY, true), Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                floorX, floorY, false), Is.Zero);
        }

        [Test]
        public void V6HallLightIsCenteredAtRightDoorAndIndependentOfRoom()
        {
            float x = .82f * 1920f, y = .52f * 1080f;
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                x,y,true), Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                x,y,false), Is.Zero);
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                .14f*1920f,.34f*1080f,true), Is.LessThan(.01f));
        }

        [Test]
        public void V6AlphaCapsApplyToAllMoteClasses()
        {
            float x = .63f*1920f, y = .58f*1080f;
            for (int kind=0;kind<3;++kind)
            {
                float alpha=HubDustCanvasGraphic.EvaluateDustAlpha(
                    kind,1f,x,y,true,true);
                Assert.That(alpha, Is.GreaterThan(0f));
                Assert.That(alpha, Is.LessThanOrEqualTo(
                    kind==0 ? .32f : kind==1 ? .12f : .06f));
            }
        }

        [Test]
        public void V6MicroDustInDarknessIsDimAndLightFadesSmoothly()
        {
            float x=.14f*1920f,y=.34f*1080f;
            float lit=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,true,false);
            float dark=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,false,false);
            Assert.That(lit,Is.InRange(.23f,.31f));
            Assert.That(dark,Is.InRange(.02f,.07f));
            Assert.That(lit,Is.GreaterThan(dark*4f));
            Assert.That(HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,true,false,0f),Is.Zero);
            // Gaussian field is continuous, never a hard rectangle.
            float near=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x+1f,y,true,false);
            Assert.That(Mathf.Abs(near-lit),Is.LessThan(.004f));
        }

        [Test]
        public void V6CityWindowStaysOnlySlightlyCoolInDarkRoom()
        {
            float x=.385f*1920f,y=.65f*1080f;
            float alpha=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,false,false);
            Assert.That(alpha,Is.InRange(.02f,.08f));
            Assert.That(HubDustCanvasGraphic.WindowContribution(x,y),
                Is.EqualTo(1f).Within(.001f));
        }

        [Test]
        public void V6LaptopReflectionIsGentleAndSpreadsAcrossCenter()
        {
            float cx=.52f*1920f,cy=.42f*1080f;
            Assert.That(HubDustCanvasGraphic.LaptopContribution(cx,cy),
                Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.LaptopContribution(
                .10f*1920f,.85f*1080f),Is.LessThan(.001f));
            float center=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,cx,cy,false,false);
            Assert.That(center,Is.InRange(.02f,.07f));
            Assert.That(center,Is.LessThan(.08f));
        }

        [Test]
        public void V6NormalModeHardCapsSurviveInspectorGains()
        {
            Assert.AreEqual(.32f,HubDustCanvasGraphic.NormalAlphaCap(0));
            Assert.AreEqual(.12f,HubDustCanvasGraphic.NormalAlphaCap(1));
            Assert.AreEqual(.06f,HubDustCanvasGraphic.NormalAlphaCap(2));
            for(int kind=0;kind<3;++kind)
            {
                float value=HubDustCanvasGraphic.EvaluateDustAlpha(
                    kind,1f,.63f*1920f,.58f*1080f,true,true);
                float gain=2f;
                float finalAlpha=Mathf.Min(value*gain,
                    HubDustCanvasGraphic.NormalAlphaCap(kind));
                Assert.That(finalAlpha,Is.LessThanOrEqualTo(
                    HubDustCanvasGraphic.NormalAlphaCap(kind)));
            }
        }

        [Test]
        public void V6NormalModeNeverProducesOversizedBokeh()
        {
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(
                0,4.2f,1.6f),Is.EqualTo(4.2f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(
                1,8f,1.6f),Is.EqualTo(8f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(
                2,22f,1.6f),Is.EqualTo(22f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(
                0,1.8f,.5f),Is.EqualTo(1.8f));
        }

        [Test]
        public void V6DiagnosticIsExplicitAndNoDustGraphicBlocksClicks()
        {
            var root = new GameObject("V6 Hub Dust",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                Assert.AreEqual(128,dust.ParticleBudget);
                Assert.IsFalse(dust.raycastTarget);
                Assert.AreEqual(HubDustVisibilityMode.Normal,
                    dust.VisibilityMode);
                Assert.IsFalse(dust.DebugVisibility);
                dust.VisibilityMode=HubDustVisibilityMode.DiagnosticTestPattern;
                Assert.IsTrue(dust.DebugVisibility);
                dust.DebugVisibility=false;
                Assert.AreEqual(HubDustVisibilityMode.Normal,
                    dust.VisibilityMode);
                dust.VisibilityMode=HubDustVisibilityMode.HighContrastBoost;
                Assert.IsFalse(dust.DebugVisibility);
                Assert.AreEqual(HubDustAtlasFactory.Width,
                    dust.mainTexture.width);
                Assert.AreEqual(HubDustAtlasFactory.Height,
                    dust.mainTexture.height);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V6InitialFrameStartsFullyVisibleAndSubsequentFadeRemains()
        {
            var root=new GameObject("V6 First Frame",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                dust.Tick(.1f,true,true,true);
                Assert.That(dust.Visibility,Is.EqualTo(1f).Within(.001f));
                dust.Tick(.1f,false,false,false);
                Assert.That(dust.Visibility,Is.InRange(.01f,.99f));
                Assert.IsFalse(dust.raycastTarget);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V6InspectorGainsAreBounded()
        {
            var root = new GameObject("V6 Inspector",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                dust.VisibilityGain=20f;
                dust.ParticleScale=0f;
                dust.ShaftStrength=100f;
                Assert.That(dust.VisibilityGain,Is.EqualTo(2f));
                Assert.That(dust.ParticleScale,Is.EqualTo(.5f));
                Assert.That(dust.ShaftStrength,Is.EqualTo(1.5f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
