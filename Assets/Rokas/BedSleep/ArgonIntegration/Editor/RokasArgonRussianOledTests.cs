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
                Assert.That(oled.OledRenderTexture.width,Is.EqualTo(512));
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
                    RokasArgonCandidateImporter.CandidatePrefab);
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
    }
}
#endif
