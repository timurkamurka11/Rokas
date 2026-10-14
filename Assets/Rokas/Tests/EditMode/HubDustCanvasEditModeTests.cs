using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class HubDustCanvasEditModeTests
    {
        [Test]
        public void V7MatchesGoldenBalanceBudgetAndThreeOpticalClasses()
        {
            Assert.AreEqual(200, HubDustCanvasGraphic.ParticleCount);
            Assert.AreEqual(120, HubDustCanvasGraphic.GeneralAirCount);
            Assert.AreEqual(80, HubDustCanvasGraphic.LocalAirCount);
            Assert.AreEqual(140, HubDustCanvasGraphic.MicroCount);
            Assert.AreEqual(46, HubDustCanvasGraphic.SmallCount);
            Assert.AreEqual(14, HubDustCanvasGraphic.BokehCount);
            Assert.AreEqual(HubDustCanvasGraphic.ParticleCount,
                HubDustCanvasGraphic.MicroCount +
                HubDustCanvasGraphic.SmallCount +
                HubDustCanvasGraphic.BokehCount);
        }

        [Test]
        public void V7PhysicalQuadDiametersUseArgonThreeSizeBands()
        {
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(0, 6.5f, 1f),
                Is.EqualTo(6.5f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(0, 13.5f, 1.6f),
                Is.EqualTo(13.5f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(1, 14f, 1f),
                Is.EqualTo(14f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(1, 24f, 1.6f),
                Is.EqualTo(24f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(2, 28f, 1f),
                Is.EqualTo(28f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(2, 48f, 1.6f),
                Is.EqualTo(48f));
        }

        [Test]
        public void V7KeepsSourceAtlasPremultipliedShader()
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
                Assert.That(HubDustAtlasFactory.EvaluateAtlasAlpha(0,0f,0f),
                    Is.GreaterThan(.97f));
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void V7WarmLampsUseRealSpotsAndExistingRoomSwitch()
        {
            float floorX=.13f*1920f, floorY=.33f*1080f;
            float deskX=.63f*1920f, deskY=.57f*1080f;
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                floorX,floorY,true),Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                deskX,deskY,true),Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                floorX,floorY,false),Is.Zero);
            float lit=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,floorX,floorY,true,false);
            Assert.That(lit,Is.InRange(.63f,.68f));
        }

        [Test]
        public void V7NightCorridorAndLaptopSourcesStayIndependent()
        {
            float corridorX=.85f*1920f,corridorY=.48f*1080f;
            float laptopX=.52f*1920f,laptopY=.43f*1080f;
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                corridorX,corridorY,true),Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                corridorX,corridorY,false),Is.Zero);
            Assert.That(HubDustCanvasGraphic.LaptopContribution(
                laptopX,laptopY),Is.EqualTo(1f).Within(.001f));
            float off=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,laptopX,laptopY,false,false,1f,false);
            float on=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,laptopX,laptopY,false,false,1f,true);
            Assert.That(on,Is.GreaterThan(off));
            float hallOff=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,corridorX,corridorY,false,false);
            float hallOn=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,corridorX,corridorY,false,true);
            Assert.That(hallOn,Is.GreaterThan(hallOff));
        }

        [Test]
        public void V7AlphaCapsPerClassAreHigherButBounded()
        {
            Assert.That(HubDustCanvasGraphic.NormalAlphaCap(0),Is.EqualTo(.68f));
            Assert.That(HubDustCanvasGraphic.NormalAlphaCap(1),Is.EqualTo(.42f));
            Assert.That(HubDustCanvasGraphic.NormalAlphaCap(2),Is.EqualTo(.22f));
            float x=.63f*1920f,y=.57f*1080f;
            for (int kind=0;kind<3;++kind)
            {
                float value=HubDustCanvasGraphic.EvaluateDustAlpha(
                    kind,1f,x,y,true,true,2f,true);
                Assert.That(value,Is.GreaterThan(0f));
                Assert.That(value,Is.LessThanOrEqualTo(
                    HubDustCanvasGraphic.NormalAlphaCap(kind)));
            }
        }

        [Test]
        public void V7NightCityWindowRemainsVisibleWithoutRoomLamps()
        {
            float x=.385f*1920f,y=.65f*1080f;
            float window=HubDustCanvasGraphic.WindowContribution(x,y);
            Assert.That(window,Is.EqualTo(1f).Within(.001f));
            float night=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,false,false);
            Assert.That(night,Is.InRange(.25f,.45f));
            Assert.That(night,Is.GreaterThan(
                HubDustCanvasGraphic.EvaluateDustAlpha(
                    0,1f,130f,100f,false,false)));
        }

        [Test]
        public void V7AirDoesNotAbruptlyJumpAtLampOrWindowBoundary()
        {
            float x=.13f*1920f,y=.33f*1080f;
            float at=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,true,false);
            float near=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x+1f,y,true,false);
            Assert.That(Mathf.Abs(near-at),Is.LessThan(.005f));
        }

        [Test]
        public void V7NormalDiagnosticAndCanvasInputRemainSeparated()
        {
            var root=new GameObject("V7 Dust",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                Assert.AreEqual(200,dust.ParticleBudget);
                Assert.IsFalse(dust.raycastTarget);
                Assert.AreEqual(HubDustVisibilityMode.Normal,dust.VisibilityMode);
                Assert.IsFalse(dust.DebugVisibility);
                dust.VisibilityMode=HubDustVisibilityMode.DiagnosticTestPattern;
                Assert.IsTrue(dust.DebugVisibility);
                dust.DebugVisibility=false;
                Assert.AreEqual(HubDustVisibilityMode.Normal,dust.VisibilityMode);
                dust.VisibilityMode=HubDustVisibilityMode.HighContrastBoost;
                Assert.IsFalse(dust.DebugVisibility);
                Assert.AreEqual(1024,dust.mainTexture.width);
                Assert.AreEqual(512,dust.mainTexture.height);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V7FirstFrameAndLightToggleKeepStablePresentation()
        {
            var root=new GameObject("V7 First Frame",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                dust.Tick(.1f,true,true,true,true);
                Assert.That(dust.Visibility,Is.EqualTo(1f).Within(.001f));
                dust.Tick(.1f,true,false,false,false);
                Assert.That(dust.Visibility,Is.EqualTo(1f).Within(.001f));
                dust.Tick(.1f,false,false,false,false);
                Assert.That(dust.Visibility,Is.InRange(.01f,.99f));
                Assert.IsFalse(dust.raycastTarget);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
