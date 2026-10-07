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
                AssertQuaternionComponents(camera.transform.rotation, 0, 0, 0, 1);
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


        // Angle() rounds micro-rotations to zero; home detection must also reject small real pose/lens drift.
        [Test]
        public void HomeCheckRejectsSmallPoseAndProjectionDrift()
        {
            var root = new GameObject("PreciseHomeFixture");
            var camera = root.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.fieldOfView = 38f;
            try
            {
                var player = new LicensedCombatCameraPlayer(camera);
                Assert.That(player.IsHome, Is.True);
                camera.transform.rotation = new Quaternion(.000002f, 0f, 0f, 1f);
                Assert.That(player.IsHome, Is.False, "A real micro-rotation must not be hidden by float dot/acos rounding");
                player.Cancel();
                Assert.That(player.IsHome, Is.True);
                camera.transform.position = new Vector3(.00002f, 0f, 0f);
                Assert.That(player.IsHome, Is.False, "A real camera translation must not pass a millimetre tolerance");
                player.Cancel();
                camera.fieldOfView += .0001f;
                Assert.That(player.IsHome, Is.False, "Captured lens is exact at home");
                player.Cancel();
                camera.orthographicSize += .0001f;
                Assert.That(player.IsHome, Is.False, "Captured orthographic extent is exact at home");
                player.Cancel();
                Assert.That(player.IsHome, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // Numeric expectations are derived independently from source cubic coefficients and ZXY Euler composition.
        [Test]
        public void BasicDamageSourceKeysAndMixRemainPreciseUnderRotatedStage()
        {
            var stage = new GameObject("RotatedStageFixture");
            var root = new GameObject("SourceCameraFixture");
            root.transform.SetParent(stage.transform, false);
            stage.transform.position = new Vector3(7f, -4f, 3f);
            stage.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var camera = root.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 38f;
            root.transform.localPosition = new Vector3(0f, .74f, -8.3f);
            root.transform.localRotation = Quaternion.identity;
            Vector3 homePosition = root.transform.position;
            Quaternion homeRotation = root.transform.rotation;
            var data = SourceBasicDamage();
            try
            {
                Assert.That((double)data.y.Evaluate(.5f), Is.EqualTo(.6245901640504599).Within(.0000001));
                Assert.That((double)data.z.Evaluate(.5f), Is.EqualTo(-7.4161712527275085).Within(.000001));
                var player = new LicensedCombatCameraPlayer(camera);
                player.Begin(data, stage.transform.position, 1f, stage.transform.rotation);
                AssertLocalPosition(stage.transform, root.transform.position, 0, .6000000238418579, -14.10009994506836);
                AssertQuaternionComponents(root.transform.rotation, .009434674413429052, .7070438366315858, .05013163221632111, .7053274554781824);
                Assert.That(camera.fieldOfView, Is.EqualTo(18f));
                player.Tick(.5f);
                AssertLocalPosition(stage.transform, root.transform.position, 0, .6245901640504599, -13.81617125272751);
                player.Tick(.7f);
                // t=1.2: out=(1-u)^2=.25; source position curves have clamped at their final keys.
                Assert.That(camera.fieldOfView, Is.EqualTo(33f).Within(.000008f));
                AssertLocalPosition(stage.transform, root.transform.position, 0, .7174999940395355, -9.617524933815003);
                // Unity Slerp approximates small angles; independent double slerp differs by ~4.32e-12 squared here.
                AssertQuaternionComponents(root.transform.rotation, .002359629281968366, .7072638335473714, .012538012669745802, .7068345634898034, 1e-11);
                player.Tick(.2f);
                Assert.That(player.Active, Is.False);
                Assert.That(player.IsHome, Is.True);
                Assert.That(root.transform.position, Is.EqualTo(homePosition));
                AssertQuaternionComponents(root.transform.rotation, homeRotation.x, homeRotation.y, homeRotation.z, homeRotation.w);
                Assert.That(camera.fieldOfView, Is.EqualTo(38f));
            }
            finally { UnityEngine.Object.DestroyImmediate(stage); }
        }


        // Source ShortTarget Offset changes both the offset translation and its parent rotation.
        [Test]
        public void ResolvedShortTargetOffsetTransformsPositionAndRotationWithoutDoubleOffset()
        {
            var root = new GameObject("ShortTargetOffsetFixture");
            var camera = root.AddComponent<Camera>();
            var data = SourceBasicDamage();
            try
            {
                var player = new LicensedCombatCameraPlayer(camera);
                Call(player, "Begin", data, Vector3.zero, 1f, Quaternion.identity,
                    (Vector3?)new Vector3(0f, -.15f, -6.5f), (Vector3?)new Vector3(0f, 0f, -5f));
                Assert.That((double)camera.transform.position.x, Is.EqualTo(.05229344772653832).Within(.000001));
                Assert.That((double)camera.transform.position.y, Is.EqualTo(.4477168426061797).Within(.000001));
                Assert.That((double)camera.transform.position.z, Is.EqualTo(-14.20009994506836).Within(.000002));
                AssertQuaternionComponents(camera.transform.rotation, -.02869676610530747, .0024677488596581295, -.0014829144942192993, .9995840168766534);
                Assert.That(data.parentPosition, Is.EqualTo(new Vector3(0f, 0f, -6.4f)), "Resolved actor offset is runtime state, never a mutation of shared profile data");
                player.Cancel();
                player.Begin(data, Vector3.zero, 1f, Quaternion.identity);
                Assert.That(camera.transform.position.x, Is.EqualTo(0f));
                Assert.That((double)camera.transform.position.y, Is.EqualTo(.6000000238418579).Within(.000001));
                Assert.That((double)camera.transform.position.z, Is.EqualTo(-14.10009994506836).Within(.000002));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }


        [Test]
        public void CancelRestoresCapturedTacticalLocalsOnSameMovedArenaParent()
        {
            var stage = new GameObject("MovedArenaHomeFixture");
            var root = new GameObject("ParentedCameraFixture");
            root.transform.SetParent(stage.transform, false);
            var camera = root.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.5f;
            camera.fieldOfView = 38f;
            Vector3 homeLocal = new Vector3(0f, .74f, -8.3f);
            Quaternion homeLocalRotation = new Quaternion(0f, 0f, .125f, .9921567416492215f);
            root.transform.localPosition = homeLocal;
            root.transform.localRotation = homeLocalRotation;
            Quaternion capturedLocalRotation = root.transform.localRotation;
            try
            {
                var player = new LicensedCombatCameraPlayer(camera);
                player.Begin(SourceBasicDamage(), Vector3.zero, 1f, Quaternion.identity);
                stage.transform.position = new Vector3(1000f, 1000f, 1000f);
                stage.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                player.Cancel();
                Assert.That(root.transform.localPosition, Is.EqualTo(homeLocal), "Tactical home is owned in the existing arena's local frame");
                AssertQuaternionComponents(root.transform.localRotation, capturedLocalRotation.x, capturedLocalRotation.y, capturedLocalRotation.z, capturedLocalRotation.w);
                Assert.That(camera.orthographic, Is.True);
                Assert.That(camera.orthographicSize, Is.EqualTo(3.5f));
                Assert.That(camera.fieldOfView, Is.EqualTo(38f));
                Assert.That(player.IsHome, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(stage); }
        }


        // Own interruption exit follows the body's linear .08 s handoff, without continuing source dolly/shake.
        [Test]
        public void ControlledCancelFreezesDisplayedShotAndKeepsOwnershipUntilExactHome()
        {
            var root = new GameObject("ControlledCameraExitFixture");
            var camera = root.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.fieldOfView = 90f;
            root.transform.position = new Vector3(0f, .74f, -8.3f);
            var data = SourceBasicDamage();
            data.baseFov = 90f;
            try
            {
                var player = new LicensedCombatCameraPlayer(camera);
                player.Begin(data, Vector3.zero, 1f, Quaternion.identity);
                player.Tick(.5f);
                Vector3 displayedPosition = root.transform.position;
                Quaternion displayedRotation = root.transform.rotation;
                Call(player, "BeginCancel", .08f);
                Assert.That(player.Active, Is.True, "Arena reset remains gated throughout the camera exit");
                Assert.That(root.transform.position, Is.EqualTo(displayedPosition), "Cancel begins from displayed pose, not resampled source");
                AssertQuaternionComponents(root.transform.rotation, displayedRotation.x, displayedRotation.y, displayedRotation.z, displayedRotation.w);
                Assert.That(camera.fieldOfView, Is.EqualTo(18f));
                player.Tick(.02f);
                Assert.That(player.Active, Is.True);
                Assert.That(player.Clock, Is.EqualTo(.5f), "Source time remains frozen during own cancellation");
                Assert.That(camera.fieldOfView, Is.EqualTo(36f).Within(.000008f), "Exit alpha .25 must match the body's linear alpha");
                Assert.That((double)root.transform.position.y, Is.EqualTo(.6534426230378449).Within(.000001));
                Assert.That((double)root.transform.position.z, Is.EqualTo(-10.86212843954563).Within(.000004));
                Call(player, "BeginCancel", .08f); // Repeated lost-profile checks must not restart the exit.
                player.Tick(.06f);
                Assert.That(player.Active, Is.False);
                Assert.That(player.IsHome, Is.True);
                Assert.That(camera.orthographic, Is.True);
                Assert.That(camera.orthographicSize, Is.EqualTo(2f));
                Assert.That(camera.fieldOfView, Is.EqualTo(90f));
                Assert.That(root.transform.position, Is.EqualTo(new Vector3(0f, .74f, -8.3f)));
                AssertQuaternionComponents(root.transform.rotation, 0, 0, 0, 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static LicensedCameraData SourceBasicDamage() => new LicensedCameraData
        {
            parentPosition = new Vector3(0f, 0f, -6.4f),
            baseFov = 38f, shotFov = 18f, shotDuration = 1.383333333333333f, blendOut = .36666666666666664f,
            x = AnimationCurve.Constant(0f, 1.0166666507720947f, 0f),
            y = new AnimationCurve(
                new Keyframe(0f, .6000000238418579f, 0f, .0491802804172039f),
                new Keyframe(.9333333373069763f, .645901620388031f, .0491802804172039f, .0491802804172039f),
                new Keyframe(1.0166666507720947f, .6499999761581421f, .0491802804172039f, 0f)),
            z = new AnimationCurve(
                new Keyframe(0f, -7.700099945068359f, 0f, .5678573846817017f),
                new Keyframe(.9333333373069763f, -7.17009973526001f, .5678573846817017f, 0f),
                new Keyframe(1.0166666507720947f, -7.17009973526001f, 0f, 0f)),
            pitch = AnimationCurve.Constant(0f, 1.0166666507720947f, -3.3010001182556152f),
            yaw = AnimationCurve.Constant(0f, 1.0166666507720947f, 0f),
            roll = AnimationCurve.Constant(0f, 1.0166666507720947f, 4.829999923706055f),
            shakeDuration = 0f
        };

        static void AssertLocalPosition(Transform stage, Vector3 position, double x, double y, double z)
        {
            Vector3 local = stage.InverseTransformPoint(position);
            // Unity Transform round-trips use float matrices; this budget is separate from source-curve precision.
            double parentUlp = Math.Max(FloatUlp(stage.position.x), Math.Max(FloatUlp(stage.position.y), FloatUlp(stage.position.z)));
            Assert.That((double)local.x, Is.EqualTo(x).Within(Math.Max(1e-6, 8 * (FloatUlp((float)x) + parentUlp))));
            Assert.That((double)local.y, Is.EqualTo(y).Within(Math.Max(1e-6, 8 * (FloatUlp((float)y) + parentUlp))));
            Assert.That((double)local.z, Is.EqualTo(z).Within(Math.Max(1e-6, 8 * (FloatUlp((float)z) + parentUlp))));
        }

        static void AssertQuaternionComponents(Quaternion actual, double x, double y, double z, double w, double squaredBudget = 1e-12)
        {
            double direct = Square(actual.x - x) + Square(actual.y - y) + Square(actual.z - z) + Square(actual.w - w);
            double flipped = Square(actual.x + x) + Square(actual.y + y) + Square(actual.z + z) + Square(actual.w + w);
            Assert.That(Math.Min(direct, flipped), Is.LessThan(squaredBudget), "Quaternion components differ from independent double-precision source composition");
        }

        static double FloatUlp(float value)
        {
            value = Mathf.Abs(value);
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            float next = BitConverter.ToSingle(BitConverter.GetBytes(bits + 1), 0);
            return (double)next - value;
        }

        static double Square(double value) => value * value;

        static object Call(object value, string method, params object[] args)
        {
            foreach (var candidate in value.GetType().GetMethods())
                if (candidate.Name == method && candidate.GetParameters().Length == args.Length)
                    return candidate.Invoke(value, args);
            Assert.Fail("Licensed camera method is missing: " + method + " with " + args.Length + " arguments");
            return null;
        }
        static System.Collections.Generic.IEqualityComparer<Vector3> Vector3Comparer() => new ApproximateVector3();
        sealed class ApproximateVector3 : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .000001f;
            public int GetHashCode(Vector3 value) => 0;
        }
    }
}
