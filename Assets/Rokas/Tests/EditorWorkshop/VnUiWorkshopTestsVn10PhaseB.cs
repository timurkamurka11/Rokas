using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseB
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void TypewriterModelHasRestrainedImmutableDefaultsAndCanBeDisabled()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveTypewriter", BindingFlags.Public | BindingFlags.Static);
            MethodInfo visible = resolver.GetMethod("CalculateTypewriterVisibleCharacters", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null, "Phase B must expose a typewriter resolver.");
            Assert.That(visible, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object values = resolve.Invoke(null, new object[] { preset });
            Type t = values.GetType();
            Assert.That((bool)t.GetField("Enabled").GetValue(values), Is.True);
            Assert.That((float)t.GetField("CharactersPerSecond").GetValue(values), Is.GreaterThan(0f));
            Assert.That((float)t.GetField("CommaPause").GetValue(values), Is.GreaterThan(0f));
            Assert.That((float)t.GetField("PeriodPause").GetValue(values), Is.GreaterThan((float)t.GetField("CommaPause").GetValue(values)));
            Assert.That(preset.HasAnyOverride, Is.False, "Resolving Phase B defaults must not mutate CurrentPreset.");

            MethodInfo set = resolver.GetMethod("SetTypewriterPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            Assert.That(set, Is.Not.Null);
            set.Invoke(null, new object[] { preset, false, 10f, 0f, .2f, .4f, .6f, .3f, .3f, 0f });
            values = resolve.Invoke(null, new object[] { preset });
            int full = (int)visible.Invoke(null, new object[] { "ROKAS", 0f, values });
            Assert.That(full, Is.EqualTo(5), "Disabled typewriter must expose the complete line without advancing story state.");
            Assert.That(preset.HasAnyOverride, Is.True);
        }

        [Test]
        public void TypewriterRevealIsDeterministicAndHonorsPunctuationPausesAndInstantComplete()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveTypewriter", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetTypewriterPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo visible = resolver.GetMethod("CalculateTypewriterVisibleCharacters", BindingFlags.Public | BindingFlags.Static);
            MethodInfo duration = resolver.GetMethod("CalculateTypewriterDuration", BindingFlags.Public | BindingFlags.Static);
            MethodInfo instant = resolver.GetMethod("InstantCompleteVisibleCharacters", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(set, Is.Not.Null);
            Assert.That(visible, Is.Not.Null);
            Assert.That(duration, Is.Not.Null);
            Assert.That(instant, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            set.Invoke(null, new object[] { preset, true, 10f, 0f, .5f, .6f, .8f, .7f, .7f, 0f });
            object values = resolve.Invoke(null, new object[] { preset });

            int beforeB = (int)visible.Invoke(null, new object[] { "A,B", .4f, values });
            Assert.That(beforeB, Is.EqualTo(2), "Comma pause must hold the reveal after the comma before B appears.");
            int later = (int)visible.Invoke(null, new object[] { "A,B", .81f, values });
            Assert.That(later, Is.EqualTo(3));

            float plain = (float)duration.Invoke(null, new object[] { "A B", values });
            float comma = (float)duration.Invoke(null, new object[] { "A,B", values });
            float period = (float)duration.Invoke(null, new object[] { "A.B", values });
            float ellipsis = (float)duration.Invoke(null, new object[] { "A…B", values });
            float question = (float)duration.Invoke(null, new object[] { "A?B", values });
            float exclamation = (float)duration.Invoke(null, new object[] { "A!B", values });
            Assert.That(comma - plain, Is.EqualTo(.5f).Within(.001f));
            Assert.That(period - plain, Is.EqualTo(.6f).Within(.001f));
            Assert.That(ellipsis - plain, Is.EqualTo(.8f).Within(.001f));
            Assert.That(question - plain, Is.EqualTo(.7f).Within(.001f));
            Assert.That(exclamation - plain, Is.EqualTo(.7f).Within(.001f));
            Assert.That((int)instant.Invoke(null, new object[] { "A,B" }), Is.EqualTo(3));
        }

        [Test]
        public void BeatPacingModelIsPreviewOnlyOverrideData()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveTiming", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetTimingPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null, "Phase B must expose pacing values for later preview sequencing.");
            Assert.That(set, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object baseline = resolve.Invoke(null, new object[] { preset });
            Type t = baseline.GetType();
            Assert.That((float)t.GetField("MinimumBeatSettleDuration").GetValue(baseline), Is.GreaterThanOrEqualTo(0f));
            Assert.That((float)t.GetField("PostTransitionBreathingRoom").GetValue(baseline), Is.GreaterThanOrEqualTo(0f));
            Assert.That((float)t.GetField("AutoPreviewSequenceGap").GetValue(baseline), Is.GreaterThanOrEqualTo(0f));
            Assert.That(preset.HasAnyOverride, Is.False);

            set.Invoke(null, new object[] { preset, .2f, .15f, .6f });
            object changed = resolve.Invoke(null, new object[] { preset });
            Assert.That((float)t.GetField("MinimumBeatSettleDuration").GetValue(changed), Is.EqualTo(.2f));
            Assert.That((float)t.GetField("PostTransitionBreathingRoom").GetValue(changed), Is.EqualTo(.15f));
            Assert.That((float)t.GetField("AutoPreviewSequenceGap").GetValue(changed), Is.EqualTo(.6f));
            Assert.That(preset.HasAnyOverride, Is.True);
        }
    }
}
