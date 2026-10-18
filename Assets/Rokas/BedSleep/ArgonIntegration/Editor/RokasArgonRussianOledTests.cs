#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>Fast candidate-only Cyrillic OLED state and clock tests.</summary>
    public sealed class RokasArgonRussianOledTests
    {
        // Independent of the candidate import workshop. The normal local
        // game installer does not install RokasArgonCandidateImporter.cs.
        private const string CandidatePrefabPath =
            "Assets/Rokas/BedSleep/ArgonCandidate/PF_Argon_Keiko_Hand_Watch_Preview.prefab";
        [Test]
        [Timeout(90000)]
        public void RussianMoonFaceHasRealClockProgressAndAllReferenceStates()
        {
            GameObject root=null;
            try
            {
                root=new GameObject("Argon_RussianMoon_OLED_EditModeTest");
                var oled=root.AddComponent<ArgonRussianSleepOledPresenter>();
                oled.InitializeOled();
                Assert.That(oled.OledDynamicTexture,Is.Not.Null);
                Assert.That(oled.OledRenderTexture.width,Is.EqualTo(ArgonRussianSleepOledPresenter.DisplaySize));
                oled.ConfigureTime(23,30,16);
                Assert.That(oled.WakeText,Is.EqualTo("07:30"));
                Assert.That(oled.DurationText,Is.EqualTo("08:00"));
                Assert.That(oled.LastRenderedTextSummary,Does.Contain("ВРЕМЯ СНА"));
                Assert.That(oled.LastRenderedTextSummary,Does.Contain("ПОДЪЁМ"));
                oled.RenderAt(0f,true);
                var before=oled.OledDynamicTexture.GetPixels32();
                oled.RenderAt(2f,true);
                var after=oled.OledDynamicTexture.GetPixels32();
                int changed=0,lit=0;
                for(int i=0;i<before.Length;i++)
                {
                    if(before[i].r!=after[i].r ||
                       before[i].g!=after[i].g ||
                       before[i].b!=after[i].b)changed++;
                    if(after[i].b>120 && after[i].b>after[i].r*1.22f)lit++;
                }
                Assert.That(changed,Is.GreaterThan(80),
                    "Night sky, clouds and ring must animate in real time");
                Assert.That(lit,Is.GreaterThan(500),
                    "Real cyan-violet circular selection ring not rendered");
                int rendersBefore=oled.TotalFaceDraws;
                for(int frame=0;frame<48;frame++)
                    oled.RenderAt(10f+frame/24f,false);
                int redraws=oled.TotalFaceDraws-rendersBefore;
                Assert.That(redraws,Is.InRange(8,22),
                    "Dynamic Russian OLED must animate at ~10Hz, not repeat 24fps CPU raster");
                oled.ShowPresets();
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.Presets));
                int countBeforeReturn=oled.TotalFaceDraws;
                oled.SelectPreset(16);
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.Dial));
                Assert.That(oled.TotalFaceDraws,Is.GreaterThan(countBeforeReturn),
                    "Selecting unchanged duration must exit preset visual immediately");
                oled.ShowPresets();
                oled.SelectPreset(1);
                Assert.That(oled.SelectedHalfHours,Is.EqualTo(1));
                Assert.That(oled.DurationText,Is.EqualTo("00:30"));
                Assert.That(oled.WakeText,Is.EqualTo("00:00"));
                oled.SelectPreset(8);
                Assert.That(oled.SelectedHalfHours,Is.EqualTo(8));
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.Dial));
                oled.ShowOverview();
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.Overview));
                Assert.That(oled.LastRenderedTextSummary,Does.Contain("ПЛАН СНА"));
                var plan=oled.OledDynamicTexture.GetPixels32();
                oled.ShowRelaxation();
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.Relaxation));
                Assert.That(oled.LastRenderedTextSummary,Does.Contain("РЕЖИМ ОТДЫХА"));
                var relax=oled.OledDynamicTexture.GetPixels32();
                int visualDifferences=0;
                for(int p=0;p<plan.Length;p+=8)
                    if(Mathf.Abs(plan[p].b-relax[p].b)>12 ||
                       Mathf.Abs(plan[p].r-relax[p].r)>12)
                        visualDifferences++;
                Assert.That(visualDifferences,Is.GreaterThan(100),
                    "Plan and animated rest screens must not be identical placeholders");
                oled.ShowDial();
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.Dial));
                oled.SetConfirmedState(true);
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.Confirmed));
                Assert.That(oled.LastRenderedTextSummary,Does.Contain("УСТАНОВЛЕНО"));
                oled.SetFallingAsleep();
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.FallingAsleep));
                oled.CancelSelection();
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.Cancelled));
                Assert.That(oled.IsConfirmed,Is.False);
                Assert.That(oled.OledDynamicTexture.EncodeToPNG().Length,
                    Is.GreaterThan(4000));
                // Native render samples for VISUAL acceptance of actual
                // Cyrillic letter spacing, seven states and sapphire border.
                // Pure isolated RT output; no fabricated UI screenshot.
                string dir=System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(),
                    "Artifacts","ArgonSquareOLEDStills");
                System.IO.Directory.CreateDirectory(dir);
                void Snapshot(string name)
                {
                    oled.RenderAt(4f,true);
                    Assert.That(oled.LastTextRows,Is.GreaterThan(0),
                        name+" must paint real Cyrillic glyphs");
                    Assert.That(oled.LastTextLayoutSafe,Is.True,
                        name+" has intersecting/clipped text on the 1024px OLED");
                    System.IO.File.WriteAllBytes(
                        System.IO.Path.Combine(dir,name+".png"),
                        oled.OledDynamicTexture.EncodeToPNG());
                }
                oled.ShowDial(); Snapshot("01_square_dial");
                oled.ShowPresets(); Snapshot("02_square_presets");
                oled.ShowOverview(); Snapshot("03_square_sleep_plan");
                oled.ShowRelaxation(); Snapshot("04_square_relaxation");
                oled.SetConfirmedState(true); Snapshot("05_confirmed");
                oled.SetFallingAsleep(); Snapshot("06_hourglass");
                oled.CancelSelection(); Snapshot("07_cancelled");
            }
            finally
            {
                if(root!=null)Object.DestroyImmediate(root);
            }
        }
        [Test]
        [Timeout(90000)]
        public void OriginalPhysicalWatchOledUvTapSelectsRussianPresetsAndStartsSleep()
        {
            GameObject instance=null, cameraObject=null;
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(
                    CandidatePrefabPath);
                Assert.That(source,Is.Not.Null);
                instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
                cameraObject=new GameObject("Argon_Russian_OriginalPhysicalWatch_QA");
                var camera=cameraObject.AddComponent<Camera>();
                var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);
                var oledMesh=renderers.Single(x=>x.name=="Watch_OLED_Display");
                var crown=renderers.Single(x=>x.name=="Watch_DigitalCrown_Rotary");
                var face=instance.AddComponent<ArgonRussianSleepOledPresenter>();
                face.InitializeOled();
                var input=instance.AddComponent<ArgonWatchControls>();
                input.Initialize(camera,face,oledMesh,crown);
                input.SetCurrentGameTime(23,30);
                input.ProcessWatchFaceTap(new Vector2(.5f,.90f));
                Assert.That(face.Screen,Is.EqualTo(ArgonRussianWatchScreen.Presets));
                input.ProcessWatchFaceTap(new Vector2(.5f,.675f));
                Assert.That(face.Screen,Is.EqualTo(ArgonRussianWatchScreen.Dial));
                Assert.That(face.SelectedHalfHours,Is.EqualTo(1));
                Assert.That(face.WakeText,Is.EqualTo("00:00"));
                input.ProcessWatchFaceTap(new Vector2(.5f,.5f));
                Assert.That(face.Screen,Is.EqualTo(ArgonRussianWatchScreen.Overview));
                input.ProcessWatchFaceTap(new Vector2(.5f,.5f));
                Assert.That(face.Screen,Is.EqualTo(ArgonRussianWatchScreen.Relaxation));
                input.ProcessWatchFaceTap(new Vector2(.5f,.90f));
                Assert.That(face.Screen,Is.EqualTo(ArgonRussianWatchScreen.Overview));
                input.ProcessWatchFaceTap(new Vector2(.5f,.90f));
                Assert.That(face.Screen,Is.EqualTo(ArgonRussianWatchScreen.Dial));
                input.ProcessWatchFaceTap(new Vector2(.5f,.10f));
                Assert.That(face.Screen,Is.EqualTo(ArgonRussianWatchScreen.Confirmed));
                input.ProcessWatchFaceTap(new Vector2(.5f,.5f));
                Assert.That(face.Screen,Is.EqualTo(ArgonRussianWatchScreen.FallingAsleep));
                input.CancelTimer();
                Assert.That(face.Screen,Is.EqualTo(ArgonRussianWatchScreen.Cancelled));
            }
            finally
            {
                if(instance!=null)Object.DestroyImmediate(instance);
                if(cameraObject!=null)Object.DestroyImmediate(cameraObject);
            }
        }
        [Test]
        public void OLEDShaderIsOpaqueAndActuallyDepthTestedAgainstWatchCase()
        {
            var shader=Shader.Find("ROKAS/BedSleep/ArgonOLEDLiveSurface");
            Assert.That(shader,Is.Not.Null);
            var material=new Material(shader);
            try
            {
                Assert.That(material.renderQueue,Is.LessThan(2500),
                    "Physical OLED must render before sapphire glass transparency");
                Assert.That(material.GetTag("RenderType",false),
                    Is.EqualTo("Opaque"));
                Assert.That(material.HasProperty("_MainTex"),Is.True);
            }
            finally{Object.DestroyImmediate(material);}
        }

        [Test]
        [Timeout(10000)]
        public void LivelyFingersHaveIndependentLimitedRingPinkyBend()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(
                CandidatePrefabPath);
            Assert.That(source,Is.Not.Null);
            GameObject hand=null;
            try
            {
                hand=(GameObject)PrefabUtility.InstantiatePrefab(source);
                var living=new ArgonLivingRig(hand);
                float first=0f,last=0f;
                for(int frame=0;frame<240;frame++)
                {
                    living.NotifyAnimationPoseReset();
                    living.Apply(frame/24f,1f,false,Vector3.right,Vector3.up,1f,false);
                    float ring=living.FingerBendDegrees("Ring_PIP");
                    float little=living.FingerBendDegrees("Little_PIP");
                    Assert.That(ring,Is.InRange(0f,4.8f));
                    Assert.That(little,Is.InRange(0f,4.8f));
                    Assert.That(living.FingerBendDegrees("Index_PIP"),
                        Is.LessThan(9.2f));
                    if(frame==20)first=little;
                    if(frame==70)last=little;
                }
                Assert.That(Mathf.Abs(last-first),Is.GreaterThan(.1f),
                    "Pinky motion should be alive, not completely locked");
            }
            finally{if(hand!=null)Object.DestroyImmediate(hand);}
        }

        [Test]
        [Timeout(10000)]
        public void LosslessTgaFrameExportPreservesOriginal720pPixels()
        {
            // Fast focused probe: verify Unity's uncompressed export on
            // the exact RGBA32 format used by the 1280x720 Game View capture.
            const int width = 1280, height = 720;
            var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                var samples=new Color32[width*height];
                for(int i=0;i<samples.Length;i++)
                    samples[i]=new Color32(
                        (byte)(i&255),(byte)((i>>7)&255),
                        (byte)((i>>13)&255),255);
                tex.SetPixels32(samples);
                tex.Apply(false);
                byte[] tga=ImageConversion.EncodeToTGA(tex);
                Assert.That(tga,Is.Not.Null);
                Assert.That(tga.Length,Is.GreaterThan(width*height*3));
                Assert.That(tga[12]+256*tga[13],Is.EqualTo(width));
                Assert.That(tga[14]+256*tga[15],Is.EqualTo(height));
                Assert.That(tga[16],Is.EqualTo(32));
                // TGA stores B,G,R,A; check raw uncompressed pixel bytes.
                Assert.That(tga[2],Is.EqualTo(2),
                    "Video encoder expects lossless non-RLE true-color TGA");
                Assert.That(tga[18],Is.EqualTo(samples[0].b));
                Assert.That(tga[19],Is.EqualTo(samples[0].g));
                Assert.That(tga[20],Is.EqualTo(samples[0].r));
                Assert.That(tga[21],Is.EqualTo(samples[0].a));
            }
            finally { Object.DestroyImmediate(tex); }
        }

    }
}
#endif
