#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
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
                oled.ShowPresets();
                Assert.That(oled.Screen,Is.EqualTo(ArgonRussianWatchScreen.Presets));
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
    }
}
#endif
