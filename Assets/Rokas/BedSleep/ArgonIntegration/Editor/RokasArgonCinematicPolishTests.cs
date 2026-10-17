#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using Rokas.BedSleep.Argon.Candidate;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Small targeted test gate: natural original-arm choreography and
    /// high-contrast Russian OLED readability. Not a lengthy full-video run.
    /// </summary>
    public sealed class RokasArgonCinematicPolishTests
    {
        [Test]
        [Timeout(45000)]
        public void NaturalRaiseAndSeparateLowerPreserveApprovedBedFraming()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(prefab,Is.Not.Null);
            GameObject instance=null,cameraObject=null;
            try
            {
                instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var animator=instance.GetComponentInChildren<Animator>(true);
                var inspect=animator.runtimeAnimatorController.animationClips
                    .Single(c=>c.name.Contains("SleepArm_InspectIdle"));
                var wrist=instance.GetComponentsInChildren<Transform>(true)
                    .Single(t=>t.name=="Arm_Wrist");
                var pose=new RokasArgonHorizontalHeroPose(instance);
                inspect.SampleAnimation(animator.gameObject,inspect.length*.5f);
                pose.CaptureHeroPose();
                pose.RestoreNaturalAnatomy();
                cameraObject=new GameObject("Argon_OrganicPOV_QA");
                var camera=cameraObject.AddComponent<Camera>();
                camera.aspect=1280f/720f;
                pose.ConfigureCamera(camera,Vector3.up,.41f);
                pose.SetCinematicPresentation(1f,false);
                Vector3 hero=instance.transform.position;
                Quaternion finalWrist=wrist.localRotation;

                pose.RestoreNaturalAnatomy();
                pose.SetCinematicPresentation(0f,false);
                Vector3 hidden=instance.transform.position;
                float vertical=Mathf.Abs(Vector3.Dot(hero-hidden,pose.Up));
                float horizontal=Mathf.Abs(Vector3.Dot(hero-hidden,pose.Right));
                Assert.That(vertical,Is.GreaterThan(.25f),
                    "Raised hand must genuinely enter from below the eye line");
                Assert.That(vertical,Is.GreaterThan(horizontal*2f),
                    "Rigid sideways entrance has returned");
                Assert.That(Quaternion.Angle(finalWrist,wrist.localRotation),
                    Is.GreaterThan(7f),"Wrist must roll into the final pose");

                pose.RestoreNaturalAnatomy();
                pose.SetCinematicPresentation(.12f,false);
                float early=Vector3.Distance(hidden,instance.transform.position);
                pose.RestoreNaturalAnatomy();
                pose.SetCinematicPresentation(.5f,false);
                float halfway=Vector3.Distance(hidden,instance.transform.position);
                Assert.That(early,Is.LessThan(halfway*.17f),
                    "Entrancing movement must ease naturally instead of sliding");

                pose.RestoreNaturalAnatomy();
                pose.SetCinematicPresentation(1f,true);
                Assert.That(Vector3.Distance(hero,instance.transform.position),
                    Is.LessThan(.00001f));
                pose.RestoreNaturalAnatomy();
                pose.SetCinematicPresentation(0f,true);
                Assert.That(Vector3.Distance(hidden,instance.transform.position),
                    Is.GreaterThan(.03f),"Exit and entry must not share a reversed path");

                pose.RestoreNaturalAnatomy();
                pose.SetCinematicPresentation(1f,false);
                var sleeve=pose.SleeveScreen();
                var watch=pose.WatchScreen();
                var tip=pose.FingertipScreen();
                Assert.That(sleeve.x,Is.LessThan(watch.x-.09f));
                Assert.That(watch.x,Is.InRange(.24f,.58f));
                Assert.That(tip.x,Is.GreaterThan(watch.x+.09f));
                Assert.That(Mathf.Abs(tip.y-watch.y),Is.LessThan(.18f));
            }
            finally
            {
                if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);
                if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        [Timeout(45000)]
        public void RussianSleepTimerRetainsReadableDigitsAndQuietMoonConfirmation()
        {
            GameObject root=null;
            try
            {
                root=new GameObject("ROKAS_Russian_Premium_OLED_QA");
                var oled=root.AddComponent<ArgonRussianSleepOledPresenter>();
                oled.InitializeOled();
                oled.ConfigureTime(23,30,14);
                oled.RenderAt(1.1f,true);
                Assert.That(oled.DurationText,Is.EqualTo("07:00"));
                Assert.That(oled.WakeText,Is.EqualTo("06:30"));
                Color32[] dial=oled.OledDynamicTexture.GetPixels32();
                int bright=0;
                for(int y=213;y<293;y++)
                for(int x=143;x<372;x++)
                {
                    var c=dial[y*512+x];
                    if(c.r>170 && c.g>170 && c.b>195)bright++;
                }
                Assert.That(bright,Is.GreaterThan(100),
                    "7-hour time digits must remain prominent in small watch shot");

                oled.SetConfirmedState(true);
                Assert.That(oled.Screen,
                    Is.EqualTo(ArgonRussianWatchScreen.Confirmed));
                Assert.That(oled.LastRenderedTextSummary,
                    Does.Contain("УСТАНОВЛЕНО"));
                Color32[] confirmed=oled.OledDynamicTexture.GetPixels32();
                int changes=0;
                for(int i=0;i<confirmed.Length;i+=3)
                    if(Mathf.Abs(confirmed[i].r-dial[i].r)>10 ||
                       Mathf.Abs(confirmed[i].b-dial[i].b)>10)
                        changes++;
                Assert.That(changes,Is.GreaterThan(900),
                    "Confirm must be a genuine animated OLED state, not inert text");
            }
            finally
            {
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
