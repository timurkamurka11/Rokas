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
    }
}
#endif
