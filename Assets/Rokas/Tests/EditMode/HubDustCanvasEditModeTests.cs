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
        public void V9Exactly145MotesAndNoLargeBokeh()
        {
            Assert.AreEqual(145, HubDustCanvasGraphic.ParticleCount);
            Assert.AreEqual(85, HubDustCanvasGraphic.GeneralAirCount);
            Assert.AreEqual(60, HubDustCanvasGraphic.LocalAirCount);
            Assert.AreEqual(120, HubDustCanvasGraphic.MicroCount);
            Assert.AreEqual(25, HubDustCanvasGraphic.SmallCount);
            Assert.AreEqual(0, HubDustCanvasGraphic.BokehCount);
            Assert.AreEqual(HubDustCanvasGraphic.ParticleCount,
                HubDustCanvasGraphic.MicroCount +
                HubDustCanvasGraphic.SmallCount +
                HubDustCanvasGraphic.BokehCount);
        }

        [Test]
        public void V9NoQuadEverExceeds10Point5Pixels()
        {
            Assert.That(HubDustCanvasGraphic.MaximumQuadSize,
                Is.EqualTo(10.5f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(0,4.8f,1f),
                Is.EqualTo(4.8f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(0,8f,1.6f),
                Is.EqualTo(8f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(1,8f,1f),
                Is.EqualTo(8f));
            Assert.That(HubDustCanvasGraphic.NormalPixelDiameter(1,10.5f,1.6f),
                Is.EqualTo(10.5f));
        }

        [Test]
        public void V9RetainsPhotographicAtlasAndBuiltInShader()
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
        public void V9WarmMicroSparklesAndSoftMoteCeilings()
        {
            float x=.13f*1920f,y=.34f*1080f;
            float micro=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,true,false);
            float small=HubDustCanvasGraphic.EvaluateDustAlpha(
                1,1f,x,y,true,false);
            Assert.That(micro,Is.InRange(.46f,.54f));
            Assert.That(small,Is.InRange(.12f,.24f));
            Assert.AreEqual(.54f,HubDustCanvasGraphic.NormalAlphaCap(0));
            Assert.AreEqual(.24f,HubDustCanvasGraphic.NormalAlphaCap(1));
            Assert.That(HubDustCanvasGraphic.EvaluateDustAlpha(
                1,1f,x,y,true,false,2f),Is.LessThanOrEqualTo(.24f));
        }

        [Test]
        public void V9RoomAirShadowFloorAndWindowRemainReadable()
        {
            float x=.70f*1920f,y=.23f*1080f;
            float shadow=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,true,false);
            Assert.That(shadow,Is.InRange(.14f,.27f));
            float wx=.385f*1920f,wy=.65f*1080f;
            float window=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,wx,wy,true,false);
            Assert.That(window,Is.GreaterThanOrEqualTo(.18f));
        }

        [Test]
        public void V9LampPoolsAndCorridorKeepOriginalSwitches()
        {
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                .13f*1920f,.34f*1080f,true),
                Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                .63f*1920f,.58f*1080f,true),
                Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.RoomLampContribution(
                .13f*1920f,.34f*1080f,false),Is.Zero);
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                .85f*1920f,.50f*1080f,true),
                Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.HallwayContribution(
                .85f*1920f,.50f*1080f,false),Is.Zero);
            float x=.52f*1920f,y=.43f*1080f;
            Assert.That(HubDustCanvasGraphic.EvaluateDustAlpha(
                    0,1f,x,y,true,true,1f,false),
                Is.EqualTo(HubDustCanvasGraphic.EvaluateDustAlpha(
                    0,1f,x,y,true,true,1f,true)).Within(.00001f));
        }

        [Test]
        public void V9NightBlueWindowBrighterButDarkRoomStaysQuiet()
        {
            float x=.385f*1920f,y=.65f*1080f;
            float night=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,false,false);
            float shadow=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,130f,100f,false,false);
            Assert.That(night,Is.InRange(.24f,.30f));
            Assert.That(shadow,Is.LessThanOrEqualTo(.03f));
            Assert.That(night,Is.GreaterThan(shadow*7f));
        }

        [Test]
        public void V9LightFieldsAreSmoothAndNotRectangularEmitters()
        {
            float x=.13f*1920f,y=.34f*1080f;
            float a=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,true,false);
            float b=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x+1f,y,true,false);
            Assert.That(Mathf.Abs(a-b),Is.LessThan(.005f));
        }

        [Test]
        public void V9RealUnityMeshTelemetryMeetsLitAndNightTargets()
        {
            var root=new GameObject("V9 Mesh Telemetry",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var rect=root.GetComponent<RectTransform>();
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
                rect.sizeDelta=new Vector2(1920f,1080f);
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                var populate=typeof(HubDustCanvasGraphic).GetMethod(
                    "OnPopulateMesh",BindingFlags.NonPublic|
                        BindingFlags.Instance);
                Assert.IsNotNull(populate);
                dust.Tick(.1f,true,true,true,true);
                using(var vh=new VertexHelper())
                {
                    populate.Invoke(dust,new object[] { vh });
                    Assert.AreEqual((dust.LastVisibleMotes+4)*4,
                        vh.currentVertCount);
                }
                Assert.That(dust.LastVisibleMotes,Is.InRange(130,150));
                Assert.That(dust.LastMeanVertexAlpha,Is.InRange(.22f,.29f));
                Assert.That(dust.LastMaxVertexAlpha,Is.InRange(.46f,.58f));
                Assert.That(dust.LastMaxParticleQuadSize,
                    Is.LessThanOrEqualTo(10.5001f));
                Assert.AreEqual(4,dust.LastShaftCount);

                dust.Tick(.1f,true,false,false,false);
                using(var vh=new VertexHelper())
                    populate.Invoke(dust,new object[] { vh });
                Assert.That(dust.LastVisibleMotes,Is.InRange(45,60));
                Assert.That(dust.LastMeanVertexAlpha,Is.InRange(.11f,.15f));
                Assert.That(dust.LastMaxVertexAlpha,Is.InRange(.24f,.30f));
                Assert.That(dust.LastMaxParticleQuadSize,
                    Is.LessThanOrEqualTo(10.5001f));
                Assert.AreEqual(4,dust.LastShaftCount);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V9OnlyShowsDiagnosticWhenExplicitlySelected()
        {
            var root=new GameObject("V9 Diagnostics",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                Assert.AreEqual(145,dust.ParticleBudget);
                Assert.IsFalse(dust.raycastTarget);
                Assert.AreEqual(HubDustVisibilityMode.Normal,dust.VisibilityMode);
                Assert.IsFalse(dust.DebugVisibility);
                dust.VisibilityMode=HubDustVisibilityMode.DiagnosticTestPattern;
                Assert.IsTrue(dust.DebugVisibility);
                dust.DebugVisibility=false;
                Assert.AreEqual(HubDustVisibilityMode.Normal,dust.VisibilityMode);
                Assert.AreEqual(1024,dust.mainTexture.width);
                Assert.AreEqual(512,dust.mainTexture.height);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V9FirstFrameAndRoomLightTogglesRetainVisibility()
        {
            var root=new GameObject("V9 Light Toggle",
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
