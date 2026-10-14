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
        public void V10Exactly145MotesAndNoLargeBokeh()
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
        public void V10NoQuadEverExceeds10Point5Pixels()
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
        public void V10RetainsPhotographicAtlasAndBuiltInShader()
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
        public void V10WarmMicroSparklesAndSoftMoteCeilings()
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
        public void V10RoomAirShadowFloorAndWindowRemainReadable()
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
        public void V10LampPoolsAndCorridorKeepOriginalSwitches()
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
        public void V10WindowGlassFloorSpillAndCrtAreBroadAndSmooth()
        {
            float centerX=.38f*1920f, centerY=.64f*1080f;
            float floorX=.40f*1920f, floorY=.36f*1080f;
            float crtX=.94f*1920f, crtY=.35f*1080f;
            Assert.That(HubDustCanvasGraphic.WindowGlassContribution(
                centerX,centerY),Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.WindowSpillContribution(
                floorX,floorY),Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.CrtContribution(
                crtX,crtY),Is.EqualTo(1f).Within(.001f));
            Assert.That(HubDustCanvasGraphic.NocturneField(
                floorX,floorY),Is.GreaterThan(.90f));
            float lit=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,centerX,centerY,false,true);
            Assert.That(lit,Is.InRange(.42f,.52f));
            float nearby=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,centerX+1f,centerY,false,true);
            Assert.That(Mathf.Abs(lit-nearby),Is.LessThan(.01f));
        }

        [Test]
        public void V10LightFieldsAreSmoothAndNotRectangularEmitters()
        {
            float x=.13f*1920f,y=.34f*1080f;
            float a=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x,y,true,false);
            float b=HubDustCanvasGraphic.EvaluateDustAlpha(
                0,1f,x+1f,y,true,false);
            Assert.That(Mathf.Abs(a-b),Is.LessThan(.005f));
        }

        [Test]
        public void V10RealUnityMeshTelemetryAndNightWindowColorPass()
        {
            var root=new GameObject("V10 Mesh Telemetry",
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
                    Assert.AreEqual((dust.LastVisibleMotes+6)*4,
                        vh.currentVertCount);
                }
                Assert.AreEqual(1f,dust.LightBlendFactor);
                Assert.That(dust.LastVisibleMotes,Is.InRange(135,150));
                Assert.That(dust.LastMeanVertexAlpha,Is.InRange(.23f,.30f));
                Assert.That(dust.LastMaxVertexAlpha,Is.InRange(.48f,.62f));
                Assert.That(dust.LastMaxParticleQuadSize,
                    Is.LessThanOrEqualTo(10.5001f));
                Assert.AreEqual(6,dust.LastShaftCount);

                dust.Tick(.40f,true,false,true,true);
                Assert.That(dust.LightBlendFactor,Is.EqualTo(0f).Within(.001f));
                using(var vh=new VertexHelper())
                {
                    populate.Invoke(dust,new object[] { vh });
                    Assert.AreEqual((dust.LastVisibleMotes+6)*4,
                        vh.currentVertCount);
                    int windowMotes=0;
                    // The six glow quads come before the 145 dust quads.
                    for(int vert=24;vert<vh.currentVertCount;vert+=4)
                    {
                        UIVertex a=new UIVertex(),b=new UIVertex();
                        vh.PopulateUIVertex(ref a,vert);
                        vh.PopulateUIVertex(ref b,vert+2);
                        float cx=(a.position.x+b.position.x)*.5f+960f;
                        float cy=(a.position.y+b.position.y)*.5f+540f;
                        if(cx<.26f*1920f||cx>.52f*1920f||
                           cy<.45f*1080f||cy>.85f*1080f)continue;
                        windowMotes++;
                        Assert.That(a.color.b,Is.GreaterThan(a.color.r),
                            "Night window should stay distinctly cyan.");
                    }
                    Assert.That(windowMotes,Is.GreaterThan(0));
                }
                Assert.That(dust.LastVisibleMotes,Is.InRange(105,135));
                Assert.That(dust.LastMeanVertexAlpha,Is.InRange(.19f,.26f));
                Assert.That(dust.LastMaxVertexAlpha,Is.InRange(.42f,.54f));
                Assert.That(dust.LastMaxParticleQuadSize,
                    Is.LessThanOrEqualTo(10.5001f));
                Assert.AreEqual(6,dust.LastShaftCount);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V10LightTransitionIsUnscaledAndRunsForPointFourSeconds()
        {
            var root=new GameObject("V10 Transition",
                typeof(RectTransform),typeof(CanvasRenderer));
            try
            {
                var dust=root.AddComponent<HubDustCanvasGraphic>();
                dust.Tick(.1f,true,true,true,false);
                Assert.That(dust.LightBlendFactor,Is.EqualTo(1f).Within(.001f));
                dust.Tick(.20f,true,false,true,false);
                Assert.That(dust.LightBlendFactor,Is.EqualTo(.5f).Within(.001f));
                dust.Tick(.20f,true,false,true,false);
                Assert.That(dust.LightBlendFactor,Is.EqualTo(0f).Within(.001f));
                dust.Tick(.40f,true,true,true,false);
                Assert.That(dust.LightBlendFactor,Is.EqualTo(1f).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void V10IndependentSeededMicroGlintsDoNotChangeParticleCount()
        {
            float low=1f,high=0f;
            for(int seed=0;seed<145;++seed)
            {
                float glint=HubDustCanvasGraphic.SpecularGlint(1.5f,seed);
                Assert.That(glint,Is.InRange(0f,1f));
                low=Mathf.Min(low,glint);high=Mathf.Max(high,glint);
            }
            Assert.That(low,Is.LessThan(.001f));
            Assert.That(high,Is.GreaterThan(.75f));
            Assert.AreEqual(145,HubDustCanvasGraphic.ParticleCount);
            Assert.AreEqual(0,HubDustCanvasGraphic.BokehCount);
        }

        [Test]
        public void V10WindowTealMixStaysWithinFortyFivePercent()
        {
            float x=.385f*1920f,y=.66f*1080f;
            float mix=HubDustCanvasGraphic.WindowTealMix(x,y);
            Assert.That(mix,Is.InRange(.30f,.45f));
            float far=HubDustCanvasGraphic.WindowTealMix(
                .13f*1920f,.34f*1080f);
            Assert.That(far,Is.LessThan(.08f));
        }

        [Test]
        public void V10OnlyShowsDiagnosticWhenExplicitlySelected()
        {
            var root=new GameObject("V10 Diagnostics",
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
        public void V10FirstFrameAndRoomLightTogglesRetainVisibility()
        {
            var root=new GameObject("V10 Light Toggle",
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
