#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.BedSleep.Argon.Candidate;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Focused regression: no full 720p render, no new FBX imports or game build.
    /// Physical Argon bones and original V10 input events only.
    /// </summary>
    public sealed class RokasArgonLivingHandRegressionTests
    {
        [Test]
        [Timeout(45000)]
        public void TwoThousandIdleFramesHaveNoRootDriftOrWristAccumulation()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(source, Is.Not.Null);
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                var rig = new ArgonLivingRig(instance);
                var wrist = instance.GetComponentsInChildren<Transform>(true)
                    .Single(x => x.name == "Arm_Wrist");
                Vector3 basePosition = instance.transform.position;
                Quaternion baseWrist = wrist.localRotation;
                float minBend = float.PositiveInfinity, maxBend = 0f;
                for (int i = 0; i < 2000; ++i)
                {
                    rig.Apply(i / 60f, 1f, i % 53 < 11,
                        Vector3.right, Vector3.up);
                    float bend = rig.FingerBendDegrees("Middle_PIP");
                    minBend = Mathf.Min(minBend, bend);
                    maxBend = Mathf.Max(maxBend, bend);
                    Assert.That((instance.transform.position - basePosition).magnitude,
                        Is.LessThan(.005f), "Idle root drift exceeds natural 5mm envelope");
                    Assert.That(Quaternion.Angle(wrist.localRotation, baseWrist),
                        Is.LessThan(1.8f), "Wrist rotation accumulated instead of breathing");
                }
                Assert.That(minBend, Is.GreaterThan(4f));
                Assert.That(maxBend - minBend, Is.GreaterThan(.15f),
                    "Source PIP joint is static rather than subtly moving");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        [Timeout(45000)]
        public void PhysicalOledHasV10DurationBoundsAndExistingRouterCanDriveIt()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(source, Is.Not.Null);
            GameObject instance = null, cameraObject = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                cameraObject = new GameObject("Argon_Router_QA_Camera");
                var cam = cameraObject.AddComponent<Camera>();
                var oled = instance.GetComponentsInChildren<MeshRenderer>(true)
                    .Single(x => x.name == "Watch_OLED_Display");
                var crown = instance.GetComponentsInChildren<MeshRenderer>(true)
                    .Single(x => x.name == "Watch_DigitalCrown_Rotary");
                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes()).ToArray();
                var presenterType = types.Single(t =>
                    t.FullName == "Rokas.BedSleep.V10Release.SmartwatchOLEDPresenter");
                var routerType = types.Single(t =>
                    t.FullName == "Rokas.BedSleep.V10Release.SmartwatchInputRouter");
                var presenter = instance.AddComponent(presenterType);
                presenterType.GetMethod("InitializeOled").Invoke(presenter, null);
                var router = instance.AddComponent(routerType);
                var controls = instance.AddComponent<ArgonWatchControls>();
                controls.Initialize(cam, presenter, oled, crown);

                controls.SetDurationSteps(-1000);
                Assert.That(controls.SelectedHalfHours, Is.EqualTo(2));
                controls.SetDurationSteps(1000);
                Assert.That(controls.SelectedHalfHours, Is.EqualTo(24));
                controls.SetDurationSteps(14);
                controls.BindExistingV10InputRouter(router);
                routerType.GetMethod("TryApplyStepOncePerFrame")
                    .Invoke(router, new object[] { 1 });
                Assert.That(controls.SelectedHalfHours, Is.EqualTo(15),
                    "Existing V10 user input did not reach original Argon watch");
                controls.Confirm();
                Assert.That(controls.IsConfirmed, Is.True);
                controls.UnbindV10InputRouter();
                controls.ResetConfirmation();
                Assert.That(controls.IsConfirmed, Is.False);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        [Timeout(90000)]
        public void IsolateOriginalPinkyMeshSeamWithoutMutatingSourceFbx()
        {
            // One focused, real Unity rendering diagnostic. The rectangular
            // finger-base seam exists in older pre-living-motion screenshots.
            // Isolate 3 authentic renderer layers to find actual source of it
            // BEFORE sculpting or changing skin weights/materials blindly.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(source, Is.Not.Null);
            GameObject instance = null, cameraObj = null, lightObj = null;
            RenderTexture rt = null;
            Texture2D result = null;
            RenderTexture originalRt = RenderTexture.active;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = 29;
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var hand = skins.Single(x => x.name == "Keiko_Hand_Skin_Mesh");
                var nails = skins.Single(x => x.name == "Keiko_Fingernails_Mesh");
                var sleeve = skins.Single(x => x.name == "Keiko_Homewear_Sleeve_Mesh");
                var watches = instance.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(watches.Length, Is.EqualTo(7));
                Assert.That(hand.sharedMesh.vertexCount, Is.GreaterThan(100));
                Assert.That(nails.sharedMesh.vertexCount, Is.GreaterThan(10));
                var animator = instance.GetComponentInChildren<Animator>(true);
                var clip = animator.runtimeAnimatorController.animationClips.Single(
                    a => a.name.Contains("SleepArm_InspectIdle"));
                var pose = new RokasArgonHorizontalHeroPose(instance);
                clip.SampleAnimation(animator.gameObject, clip.length * .5f);
                pose.CaptureHeroPose();
                pose.RestoreNaturalAnatomy();
                cameraObj = new GameObject("Argon_SourceSeam_RealUnity_InspectionCam");
                var cam = cameraObj.AddComponent<Camera>();
                cam.enabled = false;
                cam.cullingMask = 1 << 29;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.15f,.19f,.23f,1f);
                cam.aspect = 1280f / 720f;
                cam.nearClipPlane = .01f;
                cam.farClipPlane = 10f;
                pose.ConfigureCamera(cam, Vector3.up, .41f);
                lightObj = new GameObject("Argon_SoftBedroomLight_SeamCheck");
                var l = lightObj.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = .3f;
                l.color = new Color(1f,.86f,.78f);
                l.cullingMask = 1 << 29;
                lightObj.transform.rotation = Quaternion.Euler(35,-20,30);
                rt = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
                rt.Create();
                cam.targetTexture = rt;
                result = new Texture2D(1280,720,TextureFormat.RGBA32,false);
                string outDir = System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(), "Artifacts", "ArgonSeamLayers");
                System.IO.Directory.CreateDirectory(outDir);
                void Capture(string name)
                {
                    cam.Render();
                    RenderTexture.active = rt;
                    result.ReadPixels(new Rect(0,0,1280,720),0,0);
                    result.Apply(false);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(
                        outDir,name+".png"),result.EncodeToPNG());
                }

                Capture("01_all_authentic_original_meshes");
                // Candidate geometry has 5 distinct disconnected finger
                // islands. One-time taper of *cloned* root collars only.
                // This comparison must be reviewed before using in video.
                Mesh sourceHandMesh = hand.sharedMesh;
                Mesh repairedHandMesh = null;
                try
                {
                    var report = ArgonFingerBaseSeamRepair.ApplyToCandidate(
                        hand, out repairedHandMesh);
                    Assert.That(report.Components,Is.EqualTo(6));
                    Assert.That(report.FingersPolished,Is.EqualTo(5));
                    Assert.That(report.VerticesModified,Is.GreaterThan(150));
                    Capture("04_cloned_candidate_base_seam_smoothed");
                }
                finally
                {
                    hand.sharedMesh = sourceHandMesh;
                    if(repairedHandMesh!=null)
                        UnityEngine.Object.DestroyImmediate(repairedHandMesh);
                }
                // This test replaces only misassigned bone INDICES in a
                // disposable mesh clone, leaving FBX geometry and bindposes
                // untouched. Visual comparison with the ORIGINAL reference
                // determines whether it actually solves broken finger poses.
                // The neutral bind pose masks bad source influence
                // assignments. Compare original versus corrected finger
                // skins under the SAME real relaxed PIP/DIP bend.
                var living = new ArgonLivingRig(instance);
                living.NotifyAnimationPoseReset();
                living.Apply(1.35f,1f,true,cam.transform.right,cam.transform.up);
                Capture("06_original_fingers_animating_wrong_bones");
                Mesh correctedBoneMesh = null;
                try
                {
                    var reassigned=ArgonFingerWeightRepair.ApplyToCandidate(
                        hand,out correctedBoneMesh);
                    Assert.That(reassigned.Islands,Is.EqualTo(6));
                    Assert.That(reassigned.ReassociatedVertices,Is.EqualTo(5*1394));
                    Debug.Log("[ROKAS ARGON FINGER WEIGHT MAP]\n"+reassigned.Mapping);
                    Capture("05_original_fingers_corrected_bone_bindings");
                    Capture("07_corrected_fingers_in_same_animated_pose");
                }
                finally
                {
                    hand.sharedMesh=sourceHandMesh;
                    if(correctedBoneMesh!=null)
                        UnityEngine.Object.DestroyImmediate(correctedBoneMesh);
                }
                nails.enabled = false;
                Capture("02_same_pose_nails_disabled");
                nails.enabled = true;
                hand.enabled = false;
                sleeve.enabled = false;
                foreach (var w in watches) w.enabled = false;
                Capture("03_only_original_nails_mesh");
                // The isolated diagnostic MUST NOT make a quality PASS claim.
                // It only identifies source of the pre-existing rectangular seam.
                Debug.Log("[ROKAS ARGON FINGER SEAM DIAGNOSTIC] handVerts=" +
                    hand.sharedMesh.vertexCount + " nailsVerts=" +
                    nails.sharedMesh.vertexCount + " saved 3 real Unity screenshots");
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(
                    outDir,"02_same_pose_nails_disabled.png")),Is.True);
            }
            finally
            {
                RenderTexture.active = originalRt;
                if (cameraObj != null)
                {
                    var c = cameraObj.GetComponent<Camera>();
                    if (c != null) c.targetTexture = null;
                }
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (cameraObj != null) UnityEngine.Object.DestroyImmediate(cameraObj);
                if (lightObj != null) UnityEngine.Object.DestroyImmediate(lightObj);
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            }
        }
    }
}
#endif
