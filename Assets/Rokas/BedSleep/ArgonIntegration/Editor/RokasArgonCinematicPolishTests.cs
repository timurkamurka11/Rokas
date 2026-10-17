#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        public void OrganicGestureHasNoSidewaysSlideOrAbruptHeroPoseCut()
        {
            var arrived=ArgonBedOrganicGesture.Evaluate(1f,false);
            var loweringStart=ArgonBedOrganicGesture.Evaluate(1f,true);
            Assert.That(arrived.Horizontal,Is.EqualTo(0f).Within(1e-5f));
            Assert.That(arrived.Vertical,Is.EqualTo(0f).Within(1e-5f));
            Assert.That(arrived.Away,Is.EqualTo(0f).Within(1e-5f));
            Assert.That(arrived.WristPitchDegrees,Is.EqualTo(0f).Within(1e-4f));
            Assert.That(arrived.ForearmPitchDegrees,Is.EqualTo(0f).Within(1e-4f));
            Assert.That(arrived.ProximalElbowDegrees,Is.EqualTo(0f).Within(1e-4f),
                "Approved final sleeve/watch/finger alignment is unchanged");
            Assert.That(loweringStart.ProximalElbowDegrees,
                Is.EqualTo(0f).Within(1e-4f));
            Assert.That(loweringStart.Vertical,Is.EqualTo(0f).Within(1e-5f));
            Assert.That(loweringStart.WristRollDegrees,
                Is.EqualTo(0f).Within(1e-4f));

            var start=ArgonBedOrganicGesture.Evaluate(0f,false);
            var end=ArgonBedOrganicGesture.Evaluate(0f,true);
            Assert.That(Mathf.Abs(start.Vertical),Is.GreaterThan(.30f));
            Assert.That(Mathf.Abs(start.Vertical),
                Is.GreaterThan(Mathf.Abs(start.Horizontal)*2f));
            Assert.That(Vector2.Distance(
                new Vector2(start.Vertical,start.Away),
                new Vector2(end.Vertical,end.Away)),Is.GreaterThan(.025f),
                "Exit must follow a DIFFERENT depth/height arc");

            // Wrist turns before forearm settles. During exit it relaxes
            // before the forearm is fully lowered, not a backwards replay.
            var entering=ArgonBedOrganicGesture.Evaluate(.42f,false);
            var leaving=ArgonBedOrganicGesture.Evaluate(.58f,true);
            Assert.That(Mathf.Abs(entering.WristPitchDegrees/13.2f),
                Is.LessThan(Mathf.Abs(entering.ForearmPitchDegrees/6.1f)));
            Assert.That(Mathf.Abs(entering.WristPitchDegrees),
                Is.GreaterThan(8f),
                "The wrist must still visibly turn once the hand is on screen");
            Assert.That(Mathf.Abs(entering.ForearmPitchDegrees),
                Is.GreaterThan(4f),
                "Forearm secondary rotation must not be completed offscreen");
            Assert.That(Mathf.Abs(entering.WristRollDegrees),Is.GreaterThan(8f),
                "Wrist must BEND visibly in the screen plane, not just twist around the finger axis");
            Assert.That(Mathf.Abs(entering.ProximalSwivelDegrees),
                Is.GreaterThan(5f),
                "Elbow must create a visible forearm arc during the on-screen entrance");
            Assert.That(entering.WristRollDegrees*entering.ProximalSwivelDegrees,
                Is.LessThan(0f),
                "Wrist and elbow form a curved human limb, not a parallel stiff board");
            Assert.That(Mathf.Abs(entering.ProximalElbowDegrees),
                Is.GreaterThan(8f),
                "Elbow/forearm root must articulate independently, not slide as one solid stick");
            Assert.That(Mathf.Abs(leaving.ProximalElbowDegrees),
                Is.GreaterThan(1f),
                "Lowering must include a separate proximal arm hinge");
            Assert.That(Mathf.Abs(leaving.WristPitchDegrees/15.6f),
                Is.GreaterThan(Mathf.Abs(leaving.ForearmPitchDegrees/7.8f)));

            // Small sampled boundary velocities protect against "a stick
            // suddenly arriving" and visible stop/start at hero position.
            var nearHero=ArgonBedOrganicGesture.Evaluate(.999f,false);
            var beforeLeave=ArgonBedOrganicGesture.Evaluate(.999f,true);
            Assert.That(Mathf.Abs(nearHero.Vertical),Is.LessThan(.001f));
            Assert.That(Mathf.Abs(beforeLeave.Vertical),Is.LessThan(.001f));
            Assert.That(Mathf.Abs(nearHero.WristPitchDegrees),
                Is.LessThan(.2f));
            Assert.That(Mathf.Abs(beforeLeave.WristPitchDegrees),
                Is.LessThan(.2f));

            // Position is continuous across source FBX raise -> wrist turn
            // clips (at visible=.82), despite their independent sampling.
            var left=ArgonBedOrganicGesture.Evaluate(.8199f,false);
            var right=ArgonBedOrganicGesture.Evaluate(.8201f,false);
            Assert.That(Mathf.Abs(left.Vertical-right.Vertical),
                Is.LessThan(.002f));
            Assert.That(Mathf.Abs(left.WristPitchDegrees-right.WristPitchDegrees),
                Is.LessThan(.15f));
            Assert.That(start.FingerResponse,Is.LessThan(.02f));
            Assert.That(arrived.FingerResponse,Is.GreaterThan(.98f));
            Assert.That(end.FingerResponse,Is.LessThan(.02f));
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

        [Test]
        [Timeout(900000)]
        public void CaptureFiveGenuineUnityPOVFramesOfOrganicRaiseAndWithdrawal()
        {
            const int width=1280,height=720;
            var realRoom=Resources.Load<Texture2D>("BedSleep/BedCeilingPOV");
            Assert.That(realRoom,Is.Not.Null,
                "Only the authentic ROKAS BedCeilingPOV is allowed");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(prefab,Is.Not.Null);
            GameObject instance=null,cameraObject=null,lightObject=null;
            RenderTexture handRT=null,finalRT=null;
            Texture2D capture=null;
            Material composite=null,oledMaterial=null;
            ArgonSkinFilteredPbrCandidate filteredSkin=null;
            Material originalSkinMaterial=null;
            // Preview-only anatomical candidate, never saved to source FBX or
            // production V10. Proven neutral preservation and finger isolation
            // have separate focused Unity test coverage.
            SkinnedMeshRenderer handSkin=null;
            Mesh originalHandMesh=null, compensatedMesh=null;
            Transform[] originalHandBones=null;
            RenderTexture originalTarget=RenderTexture.active;
            try
            {
                instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                foreach(var t in instance.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer=29;
                handSkin=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh");
                originalHandMesh=handSkin.sharedMesh;
                originalHandBones=handSkin.bones;
                originalSkinMaterial=handSkin.sharedMaterial;
                // Preview-only premium skin light response on the real FBX,
                // preserving all original albedo, pores, AO and source PNGs.
                // Reduce overbright, chalky specular on moving finger joints;
                // do not replace skin or synthesize a plastic flat color.
                // Source-native PBR: the real albedo, AO, packed roughness
                // and imported BC5/DXT5nm normal remain *the same texture
                // references*. No GPU RGBA conversion and no checker grid.
                // Validated on three authentic Unity closeups (run
                // 39047411924), candidate-only material.
                filteredSkin=ArgonSkinFilteredPbrCandidate.Build(
                    originalSkinMaterial);
                handSkin.sharedMaterial=filteredSkin.Material;
                var compensated=ArgonFingerBindposeRetarget.ApplyCandidate(
                    handSkin,out compensatedMesh,out var authenticBones);
                Assert.That(compensated.MaxNeutralDisplacement,
                    Is.LessThan(1e-6f),
                    "Do not deform the source neutral hand in the cinematic");
                // Actual three-way Unity comparison #39030324837:
                // compensated joints preserve neutral pose and articulate
                // individual fingers, but aggressive root socket nesting
                // created new dark seams/pinches on close-up. Keep that
                // experimental mesh out of the cinematic candidate.
                Assert.That(compensated.RetargetedVertices,Is.EqualTo(6970));
                Assert.That(authenticBones.Length,Is.EqualTo(25));
                var animator=instance.GetComponentInChildren<Animator>(true);
                AnimationClip Find(string name)=>animator.runtimeAnimatorController
                    .animationClips.Single(c=>c.name.Contains("SleepArm_"+name+"_"));
                var raise=Find("Raise");
                var inspect=Find("InspectIdle");
                var lower=Find("Lower");
                var oled=instance.GetComponentsInChildren<MeshRenderer>(true)
                    .Single(x=>x.name=="Watch_OLED_Display");
                var presenter=instance.AddComponent<ArgonRussianSleepOledPresenter>();
                presenter.InitializeOled();
                presenter.ConfigureTime(19,30,14);
                oledMaterial=new Material(oled.sharedMaterial);
                oledMaterial.mainTexture=presenter.OledRenderTexture;
                oled.sharedMaterial=oledMaterial;

                cameraObject=new GameObject("ROKAS_OriginalPOV_OrganicMotion_QA");
                var cam=cameraObject.AddComponent<Camera>();
                cam.enabled=false;
                cam.clearFlags=CameraClearFlags.SolidColor;
                cam.backgroundColor=new Color(0,0,0,0);
                cam.cullingMask=1<<29;
                cam.aspect=(float)width/height;
                var pose=new RokasArgonHorizontalHeroPose(instance);
                inspect.SampleAnimation(animator.gameObject,inspect.length*.5f);
                pose.CaptureHeroPose();
                pose.RestoreNaturalAnatomy();
                pose.ConfigureCamera(cam,Vector3.up,.41f);

                lightObject=new GameObject("ROKAS_WarmCeilingKey_OrganicQA");
                var key=lightObject.AddComponent<Light>();
                key.type=LightType.Directional;
                key.intensity=.265f;
                key.color=new Color(1f,.92f,.875f);
                key.cullingMask=1<<29;
                key.transform.rotation=Quaternion.Euler(35,-20,30);

                handRT=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
                finalRT=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32);
                handRT.Create();finalRT.Create();
                cam.targetTexture=handRT;
                composite=new Material(Shader.Find("Hidden/ROKAS/ArgonBedVideoComposite"));
                Assert.That(composite.shader,Is.Not.Null);
                composite.SetTexture("_OverlayTex",handRT);
                capture=new Texture2D(width,height,TextureFormat.RGBA32,false);
                string dir=System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(),
                    "Artifacts","ArgonOrganicMotionStills");
                System.IO.Directory.CreateDirectory(dir);
                // Isolated real 512px texture states from the SAME OLED
                // bound to the original rectangular physical watch mesh.
                // The 7-stage preview reflects live selectable states, not
                // a composited fake HUD or images pasted on a screenshot.
                void WatchStill(string stage)
                {
                    presenter.RenderAt(3.0f,true);
                    System.IO.File.WriteAllBytes(
                        System.IO.Path.Combine(dir,"square_oled_"+stage+".png"),
                        presenter.OledDynamicTexture.EncodeToPNG());
                }
                presenter.ShowDial();
                WatchStill("01_dial");
                presenter.ShowPresets();
                WatchStill("02_presets");
                presenter.ShowOverview();
                WatchStill("03_sleep_plan");
                presenter.ShowRelaxation();
                WatchStill("04_calming_waves");
                presenter.SetConfirmedState(true);
                WatchStill("05_confirmed");
                presenter.SetFallingAsleep();
                WatchStill("06_falling_asleep");
                presenter.CancelSelection();
                WatchStill("07_cancelled");
                presenter.ShowDial();
                presenter.ConfigureTime(19,30,14);

                var stages=new[]{
                    Tuple.Create("01_raise_from_below",raise,.14f,false),
                    Tuple.Create("02_arm_lift_wrist_turn",raise,.55f,false),
                    Tuple.Create("03_final_watch_inspect",inspect,1f,false),
                    Tuple.Create("04_wrist_relax_lower",lower,.55f,true),
                    Tuple.Create("05_exit_below_camera",lower,.10f,true)
                };
                foreach(var stage in stages)
                {
                    stage.Item2.SampleAnimation(animator.gameObject,
                        stage.Item2.length*Mathf.Clamp01(stage.Item3));
                    pose.RestoreNaturalAnatomy();
                    pose.SetCinematicPresentation(stage.Item3,stage.Item4);
                    presenter.RenderAt(1.0f+stage.Item3,true);
                    cam.Render();
                    Graphics.Blit(realRoom,finalRT,composite);
                    RenderTexture.active=finalRT;
                    capture.ReadPixels(new Rect(0,0,width,height),0,0);
                    capture.Apply(false);
                    string file=System.IO.Path.Combine(dir,stage.Item1+".png");
                    System.IO.File.WriteAllBytes(file,capture.EncodeToPNG());
                    Assert.That(new System.IO.FileInfo(file).Length,
                        Is.GreaterThan(100000));
                }
                // Short REAL Unity motion video, not an interpolated slideshow.
                // Preserve full 1280x720 genuine 3D skin, watch mesh and
                // authentic bedroom POV; stage ~2.42 seconds at 24fps.
                // No extra fullscreen camera movements or stock assets.
                const int videoFps=24;
                string framesDir=System.IO.Path.Combine(dir,"frames");
                System.IO.Directory.CreateDirectory(framesDir);
                int videoFrames=0;
                var living=new ArgonLivingRig(instance);
                void Frame(AnimationClip clip,float t,float visible,bool lowering)
                {
                    clip.SampleAnimation(animator.gameObject,
                        clip.length*Mathf.Clamp01(t));
                    pose.RestoreNaturalAnatomy();
                    var gesture=pose.SetCinematicPresentation(visible,lowering);
                    living.NotifyAnimationPoseReset();
                    living.Apply((float)videoFrames/videoFps,
                        Mathf.SmoothStep(0f,1f,visible),false,
                        pose.Right,pose.Up,gesture.FingerResponse,lowering);
                    presenter.RenderAt((float)videoFrames/videoFps);
                    cam.Render();
                    Graphics.Blit(realRoom,finalRT,composite);
                    RenderTexture.active=finalRT;
                    capture.ReadPixels(new Rect(0,0,width,height),0,0);
                    capture.Apply(false);
                    string file=System.IO.Path.Combine(framesDir,
                        "frame_"+videoFrames.ToString("D5")+".tga");
                    System.IO.File.WriteAllBytes(file,
                        ImageConversion.EncodeToTGA(capture));
                    videoFrames++;
                }
                void Shoot(AnimationClip clip,int count,
                    Func<float,float> visibility,bool lowering)
                {
                    for(int i=0;i<count;i++)
                    {
                        float t=count==1?1f:(float)i/(count-1);
                        Frame(clip,t,visibility(t),lowering);
                    }
                }
                // Choreographed in genuine source FBX phases, without a
                // discontinuity at their shared visibility=.82 boundary.
                // Give the physical forearm, twist and wrist distinct visible beats.
                // Preserve the same approved hero pose and original FBX.
                Shoot(raise,39,t=>.82f*t,false);
                var turn=Find("WristTurn");
                Shoot(turn,17,t=>.82f+.18f*t,false);
                Shoot(inspect,12,t=>1f,false);
                Shoot(lower,38,t=>1f-t,true);
                Assert.That(videoFrames,Is.EqualTo(106));
                var cloth=new List<ArgonFoleyEvent>{
                    new ArgonFoleyEvent(.18f,ArgonFoleyCue.Sleeve,.55f),
                    new ArgonFoleyEvent(1.05f,ArgonFoleyCue.Sleeve,.24f),
                    new ArgonFoleyEvent(2.92f,ArgonFoleyCue.Sleeve,.37f),
                    new ArgonFoleyEvent(3.75f,ArgonFoleyCue.Sleeve,.23f)
                };
                ArgonWatchFoley.WriteSoundtrack(System.IO.Path.Combine(
                    dir,"OrganicRaiseLower_44100Hz.wav"),
                    videoFrames,videoFps,cloth);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir,
                    "organic_motion_manifest.txt"),
                    "Actual FBX/Unity 6000.3.19f1, original BedCeilingPOV;"+
                    " 106 real frames 1280x720 24fps; three FBX arm bones articulate,"+
                    " entry wrist leads, forearm follows, neutral-safe finger retarget;"+
                    " approved horizontal final pose unchanged.");
                Debug.Log("[ARGON ORGANIC ARM VIDEO] "+videoFrames+
                    " REAL Unity scene/mesh frames at 24fps recorded.");
                Debug.Log("[ARGON ORGANIC ARM CINEMATIC] Saved five authentic"+
                    " Unity 1280x720 bed POV camera keyframes, no fake HUD.");
            }
            finally
            {
                RenderTexture.active=originalTarget;
                if(cameraObject!=null)
                {
                    var cam=cameraObject.GetComponent<Camera>();
                    if(cam!=null)cam.targetTexture=null;
                }
                if(capture!=null)UnityEngine.Object.DestroyImmediate(capture);
                if(composite!=null)UnityEngine.Object.DestroyImmediate(composite);
                if(oledMaterial!=null)UnityEngine.Object.DestroyImmediate(oledMaterial);
                if(handRT!=null){handRT.Release();UnityEngine.Object.DestroyImmediate(handRT);}
                if(finalRT!=null){finalRT.Release();UnityEngine.Object.DestroyImmediate(finalRT);}
                if(handSkin!=null && originalHandMesh!=null)
                {
                    if(originalSkinMaterial!=null)
                        handSkin.sharedMaterial=originalSkinMaterial;
                    handSkin.sharedMesh=originalHandMesh;
                    handSkin.bones=originalHandBones;
                }
                if(compensatedMesh!=null)UnityEngine.Object.DestroyImmediate(compensatedMesh);
                if(filteredSkin!=null)filteredSkin.Dispose();
                                if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);
                if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
                if(lightObject!=null)UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }

    }
}
#endif
