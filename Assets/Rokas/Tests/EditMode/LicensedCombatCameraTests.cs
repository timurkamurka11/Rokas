using System;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Tests
{
    public sealed class LicensedCombatCameraTests
    {
        // Catches a camera that eases into contact, ignores movement curves, or retains the shot after its deadline.
        [Test]
        public void ContactCutsToAuthoredCameraThenRestoresCapturedTacticalState()
        {
            var type = typeof(ReactiveCombatActorVisual).Assembly.GetType("Rokas.Presentation.LicensedCombatCameraPlayer");
            Assert.That(type, Is.Not.Null, "Licensed camera runtime is missing");
            var root = new GameObject("CameraFixture");
            var camera = root.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.fieldOfView = 90f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            var data = new LicensedCameraData
            {
                parentPosition = new Vector3(0f, 0f, -6f),
                baseFov = 90f, shotFov = 18f, shotDuration = 2f, blendOut = 1f,
                x = AnimationCurve.Constant(0f, 2f, 0f),
                y = AnimationCurve.Linear(0f, .6f, 1f, .65f),
                z = AnimationCurve.Linear(0f, -7.7f, 1f, -7.17f),
                pitch = AnimationCurve.Constant(0f, 2f, 0f),
                yaw = AnimationCurve.Constant(0f, 2f, 0f),
                roll = AnimationCurve.Constant(0f, 2f, 5f)
            };
            try
            {
                var player = Activator.CreateInstance(type, new object[] { camera });
                Call(player, "Begin", data, new Vector3(3f, 0f, 0f), 2f, Quaternion.identity);
                Assert.That(camera.orthographic, Is.False);
                Assert.That(camera.fieldOfView, Is.EqualTo(18f).Within(.001f), "Contact is a cut, never a zoom-in tween");
                Assert.That(camera.transform.position, Is.EqualTo(new Vector3(3f, 1.2f, -27.4f)).Using(Vector3Comparer()));
                Call(player, "Tick", .5f);
                Assert.That(camera.transform.position, Is.EqualTo(new Vector3(3f, 1.25f, -26.87f)).Using(Vector3Comparer()), "Authored camera dolly must keep moving during the action hold");
                Call(player, "Tick", 1f);
                // At t=1.5, source mixOut gives .25. Tactical perspective equivalent is z=-2.
                Assert.That(camera.fieldOfView, Is.EqualTo(72f).Within(.001f));
                Assert.That(camera.transform.position.z, Is.EqualTo(-8.085f).Within(.001f));
                Call(player, "Tick", .5f);
                Assert.That(camera.orthographic, Is.True);
                Assert.That(camera.orthographicSize, Is.EqualTo(2f));
                Assert.That(camera.fieldOfView, Is.EqualTo(90f));
                Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0f, 0f, -10f)).Using(Vector3Comparer()));
                Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.identity), Is.LessThan(.001f));
                Assert.That(type.GetProperty("IsHome").GetValue(player), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // Catches shake tracks summed together, a zero high-priority track suppressing useful shake, or a muted track being played.
        [Test]
        public void ShakeSelectsHighestPriorityNonzeroActiveTrack()
        {
            var type = typeof(ReactiveCombatActorVisual).Assembly.GetType("Rokas.Presentation.LicensedCombatCameraPlayer");
            Assert.That(type, Is.Not.Null);
            var root = new GameObject("ShakeFixture");
            var camera = root.AddComponent<Camera>();
            var data = new LicensedCameraData
            {
                parentPosition = Vector3.zero, shotDuration = 2f, blendOut = 0f,
                x = AnimationCurve.Constant(0f, 2f, 0f),
                y = AnimationCurve.Constant(0f, 2f, 0f),
                z = AnimationCurve.Constant(0f, 2f, 0f),
                shakeDuration = 0f,
                shakes = new[]
                {
                    new LicensedCameraShakeSegment { duration = 1f, priority = 1, x = AnimationCurve.Constant(0f, 1f, 50f) },
                    new LicensedCameraShakeSegment { duration = 1f, priority = 10, x = AnimationCurve.Constant(0f, 1f, 2f) },
                    new LicensedCameraShakeSegment { duration = .5f, priority = 20, x = AnimationCurve.Linear(0f, 0f, 1f, 4f) },
                    new LicensedCameraShakeSegment { duration = 1f, priority = 30, muted = true, x = AnimationCurve.Constant(0f, 1f, 1000f) }
                }
            };
            try
            {
                var player = Activator.CreateInstance(type, new object[] { camera });
                Call(player, "Begin", data, Vector3.zero, 1f, Quaternion.identity);
                Assert.That(camera.transform.position.x, Is.EqualTo(2f).Within(.001f), "Zero high-priority track must allow the next nonzero track");
                Call(player, "Tick", .25f);
                Assert.That(camera.transform.position.x, Is.EqualTo(1f).Within(.001f), "Priority selects one offset; it never adds all tracks");
                Call(player, "Tick", .25f);
                Assert.That(camera.transform.position.x, Is.EqualTo(2f).Within(.001f), "Expired high-priority track must release its camera offset");
                Call(player, "Tick", .5f);
                Assert.That(camera.transform.position.x, Is.EqualTo(0f).Within(.001f), "No active track means no residual shake");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method).Invoke(value, args);
        static System.Collections.Generic.IEqualityComparer<Vector3> Vector3Comparer() => new ApproximateVector3();
        sealed class ApproximateVector3 : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .000001f;
            public int GetHashCode(Vector3 value) => 0;
        }
    }
}
