using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseD
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void IdleCharacterRemainsExactlyAtBaselineWithoutBounceEvent()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveActionBounce", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleActionBounce", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object values = resolve.Invoke(null, new object[] { preset });
            Assert.That(preset.HasAnyOverride, Is.False);

            object idle = sample.Invoke(null, new object[] { false, .5f, values });
            Type sampleType = idle.GetType();
            Assert.That((Vector2)sampleType.GetField("PositionOffset").GetValue(idle), Is.EqualTo(Vector2.zero));
            Assert.That((float)sampleType.GetField("ScaleMultiplier").GetValue(idle), Is.EqualTo(1f).Within(.0001f));
            Assert.That((bool)sampleType.GetField("Complete").GetValue(idle), Is.True);
        }

        [Test]
        public void TriggeredBounceLeavesAndReturnsExactlyToBaselineDeterministically()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveActionBounce", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetActionBouncePreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleActionBounce", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(set, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            Type easingType = Type.GetType(Namespace + "VnWorkshopEasing, " + EditorAssembly);
            object ease = Enum.Parse(easingType, "EaseInOut");
            var preset = new VnPresentationWorkshopPreset();
            set.Invoke(null, new object[] { preset, 24f, .32f, .04f, .2f, ease });
            object values = resolve.Invoke(null, new object[] { preset });

            object start = sample.Invoke(null, new object[] { true, 0f, values });
            Type sampleType = start.GetType();
            Assert.That((Vector2)sampleType.GetField("PositionOffset").GetValue(start), Is.EqualTo(Vector2.zero));
            Assert.That((float)sampleType.GetField("ScaleMultiplier").GetValue(start), Is.EqualTo(1f).Within(.0001f));

            object middle = sample.Invoke(null, new object[] { true, .5f, values });
            Vector2 middleOffset = (Vector2)sampleType.GetField("PositionOffset").GetValue(middle);
            float middleScale = (float)sampleType.GetField("ScaleMultiplier").GetValue(middle);
            Assert.That(Mathf.Abs(middleOffset.y), Is.GreaterThan(.001f));
            Assert.That(middleScale, Is.GreaterThan(1f));

            object replay = sample.Invoke(null, new object[] { true, .5f, values });
            Assert.That((Vector2)sampleType.GetField("PositionOffset").GetValue(replay), Is.EqualTo(middleOffset));
            Assert.That((float)sampleType.GetField("ScaleMultiplier").GetValue(replay), Is.EqualTo(middleScale).Within(.0001f));

            object end = sample.Invoke(null, new object[] { true, 1f, values });
            Assert.That((Vector2)sampleType.GetField("PositionOffset").GetValue(end), Is.EqualTo(Vector2.zero));
            Assert.That((float)sampleType.GetField("ScaleMultiplier").GetValue(end), Is.EqualTo(1f).Within(.0001f));
            Assert.That((bool)sampleType.GetField("Complete").GetValue(end), Is.True);
        }

        [Test]
        public void ActionBounceOverridesRemainResettableAndOverrideOnly()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo set = resolver.GetMethod("SetActionBouncePreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            Assert.That(set, Is.Not.Null);

            Type easingType = Type.GetType(Namespace + "VnWorkshopEasing, " + EditorAssembly);
            object ease = Enum.Parse(easingType, "EaseOut");
            var preset = new VnPresentationWorkshopPreset();
            set.Invoke(null, new object[] { preset, 18f, .28f, .03f, .15f, ease });
            Assert.That(preset.HasAnyOverride, Is.True);

            preset.ResetAll();
            Assert.That(preset.HasAnyOverride, Is.False);
        }
    }
}
