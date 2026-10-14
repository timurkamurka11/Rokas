using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace Rokas.Tests
{
    public sealed class HubDustCanvasEditModeTests
    {
        [Test]
        public void V8HasExactly85ParticlesAndZeroLargeBokeh()
        {
            Assert.AreEqual(85, HubDustCanvasGraphic.ParticleCount);
            Assert.AreEqual(60, HubDustCanvasGraphic.GeneralAirCount);
            Assert.AreEqual(25, HubDustCanvasGraphic.LocalAirCount);
            Assert.AreEqual(75, HubDustCanvasGraphic.MicroCount);
            Assert.AreEqual(10, HubDustCanvasGraphic.SmallCount);
            Assert.AreEqual(0, HubDustCanvasGraphic.BokehCount);
            Assert.AreEqual(HubDustCanvasGraphic.ParticleCount,
                HubDustCanvasGraphic.MicroCount +
                HubDustCanvasGraphic.SmallCount +
                HubDustCanvasGraphic.BokehCount);
        }

        [Test]
        public void V8NoQuadEverExceeds9PxEvenAtMaxInspectorScale()
        {
            Assert.That(HubDustCanvasGraphic.MaximumQuadSize,Is.EqualTo(9f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(0,3.2f,1f),
                Is.EqualTo(3.2f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(0,6.2f,1.6f),
                Is.EqualTo(6.2f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(1,6.5f,1f),
                Is.EqualTo(6.5f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(1,9f,1.6f),
                Is.EqualTo(9f));
        }

        [Test]
        public void V8UsesSourceAtlasAndScreenSpaceShader()
        {
            var shader=Resources.Load<Shader>("Shaders/HubDustSoftMotes");
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported);
            var tex=HubDustAtlasFactory.Create();
            try
            {
                Assert.AreEqual(1024,tex.width);
                Assert.AreEqual(512,tex.height);
                Assert.AreEqual(TextureFormat.RGBA32,tex.format);
                Assert.AreEqual(TextureWrapMode.Clamp,tex.wrapMode);
                Assert.AreEqual(FilterMode.Bilinear,tex.filterMode);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void V8WarmMicroAlphaPeaksButSmallMotesRemainQuiet()
        {
            float lampX=.13f*1920f,lampY=.33f*1080f;
            float micro=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,lampX,lampY,true,false);
            float small=HubDustCanvasGraphic.EvaluateDustAlpha(
                1,1f,lampX,lampY,true,false);
            Assert.That(micro,Is.InRange(.35f,.43f));
            Assert.That(small,Is.InRange(.07f,.14f));
            Assert.AreEqual(.42f,HubDustCanvasGraphic.NormalAlphaCap(0));
            Assert.AreEqual(.14f,HubDustCanvasGraphic.NormalAlphaCap(1));
            Assert.That(HubDustCanvasGraphic.EvaluateDustAlpha(
                1,1f,lampX,lampY,true,false,2f),
                Is.LessThanOrEqualTo(.14f));
        }

        [Test]
        public void V8NoLaptopEmitterAndIndependentRoomCorridorSwitches()
        {
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                .13f*1920f,.33f*1080f,true),Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                .13f*1920f,.33f*1080f,false),Is.Zero);
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                .85f*1920f,.48f*1080f,true),Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                .85f*1920f,.48f*1080f,false),Is.Zero);
            // Powering the laptop does NOT spawn or brighten center dust.
            float centerX=.52f*1920f,centerY=.43f*1080f;
            float off=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,centerX,centerY,true,true,1f,false);
            float on=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,centerX,centerY,true,true,1f,true);
            Assert.That(on,Is.EqualTo(off).Within(.00001f));
        }

        [Test]
        public void V8NightIsDarkExceptSoftBlueCityWindow()
        {
            float winX=.385f*1920f,winY=.65f*1080f;
            float darkX=130f,darkY=100f;
            float night=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,winX,winY,false,false);
            float dark=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,darkX,darkY,false,false);
            Assert.That(night,Is.InRange(.16f,.22f));
            Assert.That(dark,Is.LessThanOrEqualTo(.03f));
            Assert.That(night,Is.GreaterThan(dark*5f));
        }

        [Test]
        public void V8LampFieldFadesSmoothlyWithoutEmitterRectangles()
        {
            float x=.13f*1920f,y=.33f*1080f;
            float a=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,true,false);
            float b=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x+1f,y,true,false);
            Assert.That(Mathf.Abs(a-b),Is.LessThan(.005f));
        }

        [Test]
        public void V8RealUnityMeshTelemetryFitsGoldenQuietTargets()
        {
            var root=new GameObject("V8 Mesh Telemetry",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var rect=root.GetComponent<RectTransform>();
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
                rect.sizeDelta=new Vector2(1920f,1080f);
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                var populate=typeof(HubDustCanvasGraphic).GetMethod(
                    "OnPopulateMesh", BindingFlags.NonPublic|
                        BindingFlags.Instance);
                Assert.IsNotNull(populate);
                dust.Tick(.1f,true,true,true,true);
                using(var vh=new VertexHelper())
                {
                    populate.Invoke(dust,new object[] { vh });
                    Assert.AreEqual((dust.LastVisibleMotes+4)*4,
                        vh.currentVertCount);
                }
                Assert.That(dust.LastVisibleMotes,Is.InRange(70,90));
                Assert.That(dust.LastMeanVertexAlpha,Is.InRange(.14f,.20f));
                Assert.That(dust.LastMaxVertexAlpha,Is.InRange(.32f,.44f));
                Assert.That(dust.LastMaxParticleQuadSize,
                    Is.LessThanOrEqualTo(9.0001f));
                Assert.AreEqual(4,dust.LastShaftCount);

                dust.Tick(.1f,true,false,false,false);
                using(var vh=new VertexHelper())
                    populate.Invoke(dust,new object[] { vh });
                Assert.That(dust.LastVisibleMotes,Is.LessThan(70));
                Assert.That(dust.LastMeanVertexAlpha,Is.InRange(.06f,.09f));
                Assert.That(dust.LastMaxVertexAlpha,Is.InRange(.16f,.22f));
                Assert.AreEqual(4,dust.LastShaftCount);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V8NormalModeOnlyUsesDiagnosticsWhenExplicitlyChosen()
        {
            var root=new GameObject("V8 Diagnostic",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                Assert.AreEqual(85,dust.ParticleBudget);
                Assert.IsFalse(dust.raycastTarget);
                Assert.AreEqual(HubDustVisibilityMode.Normal,
                    dust.VisibilityMode);
                Assert.IsFalse(dust.DebugVisibility);
                dust.VisibilityMode=HubDustVisibilityMode.DiagnosticTestPattern;
                Assert.IsTrue(dust.DebugVisibility);
                dust.DebugVisibility=false;
                Assert.AreEqual(HubDustVisibilityMode.Normal,
                    dust.VisibilityMode);
                Assert.AreEqual(1024,dust.mainTexture.width);
                Assert.AreEqual(512,dust.mainTexture.height);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V8FirstRoomFrameAndLightTogglePreserveRuntimeVisibility()
        {
            var root=new GameObject("V8 First Frame",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                dust.Tick(.1f,true,true,false,false);
                Assert.That(dust.Visibility,Is.EqualTo(1f).Within(.001f));
                dust.Tick(.1f,true,false,false,false);
                Assert.That(dust.Visibility,Is.EqualTo(1f).Within(.001f));
                dust.Tick(.1f,false,false,false,false);
                Assert.That(dust.Visibility,Is.InRange(.01f,.99f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
