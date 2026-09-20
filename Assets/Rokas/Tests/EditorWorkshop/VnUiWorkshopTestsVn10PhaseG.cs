using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseG
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void SingleCharacterFocusIsStableWithoutPulsingOrBobbing()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveSpeakerFocus", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleSpeakerFocus", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object values = resolve.Invoke(null, new object[] { preset });
            Assert.That(preset.HasAnyOverride, Is.False);

            Array samples = Array.CreateInstance(sample.ReturnType, 3);
            samples.SetValue(sample.Invoke(null, new object[] { 1, 0, 0, 0, 0f, values }), 0);
            samples.SetValue(sample.Invoke(null, new object[] { 1, 0, 0, 0, .5f, values }), 1);
            samples.SetValue(sample.Invoke(null, new object[] { 1, 0, 0, 0, 1f, values }), 2);

            for (int i = 0; i < samples.Length; i++)
            {
                object value = samples.GetValue(i);
                Assert.That(FloatField(value, "Scale"), Is.EqualTo(1f).Within(.0001f));
                Assert.That(FloatField(value, "Brightness"), Is.EqualTo(1f).Within(.0001f));
                Assert.That(FloatField(value, "Alpha"), Is.EqualTo(1f).Within(.0001f));
                Assert.That(VectorField(value, "PositionOffset"), Is.EqualTo(Vector2.zero));
                Assert.That(BoolField(value, "Complete"), Is.True);
            }
        }

        [Test]
        public void SpeakerSwitchInterpolatesActiveAndInactiveFocusDeterministically()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveSpeakerFocus", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleSpeakerFocus", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            object values = resolve.Invoke(null, new object[] { new VnPresentationWorkshopPreset() });
            float activeScale = FloatField(values, "ActiveScale");
            float activeBrightness = FloatField(values, "ActiveBrightness");
            float activeForwardOffset = FloatField(values, "ActiveForwardOffset");
            float inactiveScale = FloatField(values, "InactiveScale");
            float inactiveBrightness = FloatField(values, "InactiveBrightness");
            float inactiveAlpha = FloatField(values, "InactiveAlpha");

            object oldStart = sample.Invoke(null, new object[] { 2, 0, 1, 0, 0f, values });
            object oldMid = sample.Invoke(null, new object[] { 2, 0, 1, 0, .5f, values });
            object oldReplay = sample.Invoke(null, new object[] { 2, 0, 1, 0, .5f, values });
            object oldEnd = sample.Invoke(null, new object[] { 2, 0, 1, 0, 1f, values });
            object newStart = sample.Invoke(null, new object[] { 2, 0, 1, 1, 0f, values });
            object newEnd = sample.Invoke(null, new object[] { 2, 0, 1, 1, 1f, values });

            Assert.That(FloatField(oldStart, "Scale"), Is.EqualTo(activeScale).Within(.0001f));
            Assert.That(FloatField(oldStart, "Brightness"), Is.EqualTo(activeBrightness).Within(.0001f));
            Assert.That(FloatField(oldStart, "Alpha"), Is.EqualTo(1f).Within(.0001f));
            Assert.That(VectorField(oldStart, "PositionOffset").y, Is.EqualTo(activeForwardOffset).Within(.0001f));
            Assert.That(FloatField(oldEnd, "Scale"), Is.EqualTo(inactiveScale).Within(.0001f));
            Assert.That(FloatField(oldEnd, "Brightness"), Is.EqualTo(inactiveBrightness).Within(.0001f));
            Assert.That(FloatField(oldEnd, "Alpha"), Is.EqualTo(inactiveAlpha).Within(.0001f));
            Assert.That(VectorField(oldEnd, "PositionOffset"), Is.EqualTo(Vector2.zero));

            Assert.That(FloatField(newStart, "Scale"), Is.EqualTo(inactiveScale).Within(.0001f));
            Assert.That(FloatField(newEnd, "Scale"), Is.EqualTo(activeScale).Within(.0001f));
            AssertBetween(FloatField(oldMid, "Scale"), activeScale, inactiveScale);
            Assert.That(FloatField(oldReplay, "Scale"), Is.EqualTo(FloatField(oldMid, "Scale")).Within(.0001f));
            Assert.That(FloatField(oldReplay, "Brightness"), Is.EqualTo(FloatField(oldMid, "Brightness")).Within(.0001f));

            object threeMid = sample.Invoke(null, new object[] { 3, 2, 1, 2, .5f, values });
            AssertBetween(FloatField(threeMid, "Scale"), activeScale, inactiveScale);
        }

        [Test]
        public void SpeakerFocusOverridesResolveResetAndZeroDurationSnaps()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveSpeakerFocus", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetSpeakerFocusPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo reset = resolver.GetMethod("ResetSpeakerFocusPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleSpeakerFocus", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(set, Is.Not.Null);
            Assert.That(reset, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            Type easingType = Type.GetType(Namespace + "VnWorkshopEasing, " + EditorAssembly);
            object linear = Enum.Parse(easingType, "Linear");
            set.Invoke(null, new object[] { preset, 1.08f, 1.10f, 14f, .90f, .65f, .70f, .50f, linear });
            Assert.That(preset.HasAnyOverride, Is.True);

            object values = resolve.Invoke(null, new object[] { preset });
            Assert.That(FloatField(values, "ActiveScale"), Is.EqualTo(1.08f).Within(.0001f));
            Assert.That(FloatField(values, "ActiveBrightness"), Is.EqualTo(1.10f).Within(.0001f));
            Assert.That(FloatField(values, "ActiveForwardOffset"), Is.EqualTo(14f).Within(.0001f));
            Assert.That(FloatField(values, "InactiveScale"), Is.EqualTo(.90f).Within(.0001f));
            Assert.That(FloatField(values, "InactiveBrightness"), Is.EqualTo(.65f).Within(.0001f));
            Assert.That(FloatField(values, "InactiveAlpha"), Is.EqualTo(.70f).Within(.0001f));
            Assert.That(FloatField(values, "TransitionDuration"), Is.EqualTo(.50f).Within(.0001f));
            Assert.That(values.GetType().GetField("Easing").GetValue(values).ToString(), Is.EqualTo("Linear"));

            set.Invoke(null, new object[] { preset, 1.08f, 1.10f, 14f, .90f, .65f, .70f, 0f, linear });
            values = resolve.Invoke(null, new object[] { preset });
            object snapped = sample.Invoke(null, new object[] { 2, 0, 1, 0, .25f, values });
            Assert.That(FloatField(snapped, "Scale"), Is.EqualTo(.90f).Within(.0001f));
            Assert.That(BoolField(snapped, "Complete"), Is.True);

            reset.Invoke(null, new object[] { preset });
            Assert.That(preset.HasAnyOverride, Is.False);
        }

        private static float FloatField(object value, string name)
        {
            return (float)value.GetType().GetField(name).GetValue(value);
        }

        private static Vector2 VectorField(object value, string name)
        {
            return (Vector2)value.GetType().GetField(name).GetValue(value);
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
