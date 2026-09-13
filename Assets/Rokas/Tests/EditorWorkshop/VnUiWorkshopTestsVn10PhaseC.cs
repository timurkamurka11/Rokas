using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseC
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void ExpressionCrossfadeUsesOnlyAuthoredStatesAndCompletesExactly()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveExpressionTransition", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleExpressionTransition", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object values = resolve.Invoke(null, new object[] { preset });
            Assert.That(preset.HasAnyOverride, Is.False);

            object start = sample.Invoke(null, new object[] { "mina_neutral", "mina_happy", 0f, values });
            Type t = start.GetType();
            Assert.That((string)t.GetField("StartStateId").GetValue(start), Is.EqualTo("mina_neutral"));
            Assert.That((string)t.GetField("EndStateId").GetValue(start), Is.EqualTo("mina_happy"));
            Assert.That((float)t.GetField("StartAlpha").GetValue(start), Is.EqualTo(1f).Within(.0001f));
            Assert.That((float)t.GetField("EndAlpha").GetValue(start), Is.EqualTo(0f).Within(.0001f));

            object middle = sample.Invoke(null, new object[] { "mina_neutral", "mina_happy", .5f, values });
            Assert.That((float)t.GetField("StartAlpha").GetValue(middle), Is.InRange(0f, 1f));
            Assert.That((float)t.GetField("EndAlpha").GetValue(middle), Is.InRange(0f, 1f));

            object end = sample.Invoke(null, new object[] { "mina_neutral", "mina_happy", 1f, values });
            Assert.That((float)t.GetField("StartAlpha").GetValue(end), Is.EqualTo(0f).Within(.0001f));
            Assert.That((float)t.GetField("EndAlpha").GetValue(end), Is.EqualTo(1f).Within(.0001f));
            Assert.That((bool)t.GetField("Complete").GetValue(end), Is.True);
        }

        [Test]
        public void ExpressionCrossfadeRejectsUnsupportedOrCrossCharacterFakeStates()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveExpressionTransition", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleExpressionTransition", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);
            object values = resolve.Invoke(null, new object[] { new VnPresentationWorkshopPreset() });

            TargetInvocationException missing = Assert.Throws<TargetInvocationException>(() =>
                sample.Invoke(null, new object[] { "mina_neutral", "mina_blink", .5f, values }));
            Assert.That(missing.InnerException, Is.TypeOf<ArgumentException>());

            TargetInvocationException wrongCharacter = Assert.Throws<TargetInvocationException>(() =>
                sample.Invoke(null, new object[] { "mina_neutral", "keiko_surprised", .5f, values }));
            Assert.That(wrongCharacter.InnerException, Is.TypeOf<ArgumentException>());
        }

        [Test]
        public void SlideFadeEnterExitInterpolatesAndResetsDeterministically()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveCharacterTransition", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetCharacterTransitionPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo enter = resolver.GetMethod("SampleCharacterEnter", BindingFlags.Public | BindingFlags.Static);
            MethodInfo exit = resolver.GetMethod("SampleCharacterExit", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(set, Is.Not.Null);
            Assert.That(enter, Is.Not.Null);
            Assert.That(exit, Is.Not.Null);

            Type modeType = Type.GetType(Namespace + "VnWorkshopCharacterTransitionMode, " + EditorAssembly);
            Type directionType = Type.GetType(Namespace + "VnWorkshopSlideDirection, " + EditorAssembly);
            Type easingType = Type.GetType(Namespace + "VnWorkshopEasing, " + EditorAssembly);
            object slideFade = Enum.Parse(modeType, "SlideAndFade");
            object fromLeft = Enum.Parse(directionType, "Left");
            object ease = Enum.Parse(easingType, "EaseInOut");

            var preset = new VnPresentationWorkshopPreset();
            set.Invoke(null, new object[] { preset, slideFade, .4f, .3f, 80f, fromLeft, ease });
            object values = resolve.Invoke(null, new object[] { preset });

            object enterStart = enter.Invoke(null, new object[] { 0f, values });
            Type sampleType = enterStart.GetType();
            Assert.That((float)sampleType.GetField("Alpha").GetValue(enterStart), Is.EqualTo(0f).Within(.0001f));
            Assert.That(((Vector2)sampleType.GetField("PositionOffset").GetValue(enterStart)).x, Is.LessThan(0f));
            object enterEnd = enter.Invoke(null, new object[] { 1f, values });
            Assert.That((float)sampleType.GetField("Alpha").GetValue(enterEnd), Is.EqualTo(1f).Within(.0001f));
            Assert.That((Vector2)sampleType.GetField("PositionOffset").GetValue(enterEnd), Is.EqualTo(Vector2.zero));

            object exitStart = exit.Invoke(null, new object[] { 0f, values });
            Assert.That((float)sampleType.GetField("Alpha").GetValue(exitStart), Is.EqualTo(1f).Within(.0001f));
            Assert.That((Vector2)sampleType.GetField("PositionOffset").GetValue(exitStart), Is.EqualTo(Vector2.zero));
            object exitEnd = exit.Invoke(null, new object[] { 1f, values });
            Assert.That((float)sampleType.GetField("Alpha").GetValue(exitEnd), Is.EqualTo(0f).Within(.0001f));
            Assert.That(((Vector2)sampleType.GetField("PositionOffset").GetValue(exitEnd)).x, Is.LessThan(0f));
        }
    }
}
