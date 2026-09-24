using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseF
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void StageLayoutsPlaceOneTwoThreeCharactersInDistinctSlots()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveStageLayout", BindingFlags.Public | BindingFlags.Static);
            MethodInfo targets = resolver.GetMethod("ResolveStageTargets", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(targets, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object values = resolve.Invoke(null, new object[] { preset });
            Assert.That(preset.HasAnyOverride, Is.False);

            Array one = (Array)targets.Invoke(null, new object[] { 1, values });
            Array two = (Array)targets.Invoke(null, new object[] { 2, values });
            Array three = (Array)targets.Invoke(null, new object[] { 3, values });
            Assert.That(one.Length, Is.EqualTo(1));
            Assert.That(two.Length, Is.EqualTo(2));
            Assert.That(three.Length, Is.EqualTo(3));
            Assert.That(Slot(one.GetValue(0)), Is.EqualTo("Center"));
            Assert.That(Slot(two.GetValue(0)), Is.EqualTo("Left"));
            Assert.That(Slot(two.GetValue(1)), Is.EqualTo("Right"));
            Assert.That(Slot(three.GetValue(0)), Is.EqualTo("Left"));
            Assert.That(Slot(three.GetValue(1)), Is.EqualTo("Center"));
            Assert.That(Slot(three.GetValue(2)), Is.EqualTo("Right"));

            float spacing = FloatField(values, "Spacing");
            float left = Position(three.GetValue(0)).x;
            float center = Position(three.GetValue(1)).x;
            float right = Position(three.GetValue(2)).x;
            Assert.That(center - left, Is.GreaterThanOrEqualTo(spacing));
            Assert.That(right - center, Is.GreaterThanOrEqualTo(spacing));
            Assert.That(Scale(one.GetValue(0)), Is.GreaterThan(0f));
        }

        [Test]
        public void StageTransitionsSmoothlyRepositionExistingCharactersAndZeroDurationSnaps()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveStageLayout", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetStageLayoutPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo targets = resolver.GetMethod("ResolveStageTargets", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleStageTransition", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(set, Is.Not.Null);
            Assert.That(targets, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object values = resolve.Invoke(null, new object[] { preset });
            Array oneTargets = (Array)targets.Invoke(null, new object[] { 1, values });
            Array twoTargets = (Array)targets.Invoke(null, new object[] { 2, values });
            Array threeTargets = (Array)targets.Invoke(null, new object[] { 3, values });

            Array start12 = (Array)sample.Invoke(null, new object[] { 1, 2, 0f, values });
            Array mid12 = (Array)sample.Invoke(null, new object[] { 1, 2, .5f, values });
            Array replay12 = (Array)sample.Invoke(null, new object[] { 1, 2, .5f, values });
            Array end12 = (Array)sample.Invoke(null, new object[] { 1, 2, 1f, values });
            float centerX = Position(oneTargets.GetValue(0)).x;
            float leftX = Position(twoTargets.GetValue(0)).x;
            float midX = Position(mid12.GetValue(0)).x;
            Assert.That(Position(start12.GetValue(0)).x, Is.EqualTo(centerX).Within(.0001f));
            Assert.That(midX, Is.InRange(Mathf.Min(centerX, leftX), Mathf.Max(centerX, leftX)));
            Assert.That(midX, Is.Not.EqualTo(centerX).Within(.0001f));
            Assert.That(midX, Is.Not.EqualTo(leftX).Within(.0001f));
            Assert.That(Position(replay12.GetValue(0)).x, Is.EqualTo(midX).Within(.0001f));
            Assert.That(Position(end12.GetValue(0)).x, Is.EqualTo(leftX).Within(.0001f));

            Array mid23 = (Array)sample.Invoke(null, new object[] { 2, 3, .5f, values });
            AssertBetween(Position(mid23.GetValue(1)).x, Position(twoTargets.GetValue(1)).x, Position(threeTargets.GetValue(1)).x);
            Array mid32 = (Array)sample.Invoke(null, new object[] { 3, 2, .5f, values });
            AssertBetween(Position(mid32.GetValue(1)).x, Position(threeTargets.GetValue(1)).x, Position(twoTargets.GetValue(1)).x);
            Array mid21 = (Array)sample.Invoke(null, new object[] { 2, 1, .5f, values });
            AssertBetween(Position(mid21.GetValue(0)).x, Position(twoTargets.GetValue(0)).x, Position(oneTargets.GetValue(0)).x);

            Type easingType = Type.GetType(Namespace + "VnWorkshopEasing, " + EditorAssembly);
            object easing = Enum.Parse(easingType, "EaseInOut");
            set.Invoke(null, new object[]
            {
                preset,
                FloatField(values, "LeftX"), FloatField(values, "CenterX"), FloatField(values, "RightX"),
                FloatField(values, "SlotY"), FloatField(values, "LeftScale"), FloatField(values, "CenterScale"),
                FloatField(values, "RightScale"), FloatField(values, "Spacing"), 0f, easing
            });
            values = resolve.Invoke(null, new object[] { preset });
            Array snapped = (Array)sample.Invoke(null, new object[] { 1, 2, .25f, values });
            Assert.That(Position(snapped.GetValue(0)).x, Is.EqualTo(Position(twoTargets.GetValue(0)).x).Within(.0001f));
            Assert.That(BoolField(snapped.GetValue(0), "Complete"), Is.True);
        }

        [Test]
        public void StageLayoutOverridesArePreviewOnlyResettableAndRejectOverlap()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveStageLayout", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetStageLayoutPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo reset = resolver.GetMethod("ResetStageLayoutPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(set, Is.Not.Null);
            Assert.That(reset, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object values = resolve.Invoke(null, new object[] { preset });
            Type easingType = Type.GetType(Namespace + "VnWorkshopEasing, " + EditorAssembly);
            object easing = Enum.Parse(easingType, "Linear");
            set.Invoke(null, new object[] { preset, -420f, 0f, 420f, 12f, .95f, 1.05f, .95f, 300f, .45f, easing });
            Assert.That(preset.HasAnyOverride, Is.True);
            values = resolve.Invoke(null, new object[] { preset });
            Assert.That(FloatField(values, "LeftX"), Is.EqualTo(-420f).Within(.0001f));
            Assert.That(FloatField(values, "SlotY"), Is.EqualTo(12f).Within(.0001f));
            Assert.That(FloatField(values, "RepositionDuration"), Is.EqualTo(.45f).Within(.0001f));

            reset.Invoke(null, new object[] { preset });
            Assert.That(preset.HasAnyOverride, Is.False);

            TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() =>
                set.Invoke(null, new object[] { preset, -100f, 0f, 100f, 0f, 1f, 1f, 1f, 150f, .3f, easing }));
            Assert.That(ex.InnerException, Is.TypeOf<ArgumentException>());
        }

        private static string Slot(object target)
        {
            return target.GetType().GetField("Slot").GetValue(target).ToString();
        }

        private static Vector2 Position(object target)
        {
            return (Vector2)target.GetType().GetField("Position").GetValue(target);
        }

        private static float Scale(object target)
        {
            return (float)target.GetType().GetField("Scale").GetValue(target);
        }

        private static float FloatField(object value, string name)
        {
            return (float)value.GetType().GetField(name).GetValue(value);
        }

        private static bool BoolField(object value, string name)
        {
            return (bool)value.GetType().GetField(name).GetValue(value);
        }

        private static void AssertBetween(float actual, float a, float b)
        {
            float min = Mathf.Min(a, b);
            float max = Mathf.Max(a, b);
            Assert.That(actual, Is.InRange(min, max));
            Assert.That(actual, Is.Not.EqualTo(a).Within(.0001f));
            Assert.That(actual, Is.Not.EqualTo(b).Within(.0001f));
        }
    }
}
