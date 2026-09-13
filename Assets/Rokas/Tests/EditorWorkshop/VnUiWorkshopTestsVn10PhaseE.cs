using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseE
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void BackgroundTransitionDefaultsAreOverrideOnlyAndReachExactEndpoints()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveBackgroundTransition", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleBackgroundTransition", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object values = resolve.Invoke(null, new object[] { preset });
            Assert.That(preset.HasAnyOverride, Is.False);

            object start = sample.Invoke(null, new object[] { 0f, values });
            Type sampleType = start.GetType();
            Assert.That((float)sampleType.GetField("SourceAlpha").GetValue(start), Is.EqualTo(1f).Within(.0001f));
            Assert.That((float)sampleType.GetField("TargetAlpha").GetValue(start), Is.EqualTo(0f).Within(.0001f));

            object end = sample.Invoke(null, new object[] { 1f, values });
            Assert.That((float)sampleType.GetField("SourceAlpha").GetValue(end), Is.EqualTo(0f).Within(.0001f));
            Assert.That((float)sampleType.GetField("TargetAlpha").GetValue(end), Is.EqualTo(1f).Within(.0001f));
            Assert.That((float)sampleType.GetField("CurtainCoverage").GetValue(end), Is.EqualTo(0f).Within(.0001f));
            Assert.That((bool)sampleType.GetField("Complete").GetValue(end), Is.True);
        }

        [Test]
        public void CurtainDirectionsAreExplicitAndSamplingIsDeterministic()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveBackgroundTransition", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetBackgroundTransitionPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleBackgroundTransition", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(set, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            Type modeType = Type.GetType(Namespace + "VnWorkshopBackgroundTransitionMode, " + EditorAssembly);
            Type directionType = Type.GetType(Namespace + "VnWorkshopCurtainDirection, " + EditorAssembly);
            Type easingType = Type.GetType(Namespace + "VnWorkshopEasing, " + EditorAssembly);
            object curtain = Enum.Parse(modeType, "Curtain");
            object rightToLeft = Enum.Parse(directionType, "RightToLeft");
            object leftToRight = Enum.Parse(directionType, "LeftToRight");
            object ease = Enum.Parse(easingType, "EaseInOut");

            var preset = new VnPresentationWorkshopPreset();
            set.Invoke(null, new object[] { preset, curtain, .6f, .85f, rightToLeft, ease });
            object values = resolve.Invoke(null, new object[] { preset });
            object first = sample.Invoke(null, new object[] { .25f, values });
            object replay = sample.Invoke(null, new object[] { .25f, values });
            Type sampleType = first.GetType();
            Assert.That((float)sampleType.GetField("CurtainCoverage").GetValue(first), Is.GreaterThan(0f));
            Assert.That((float)sampleType.GetField("CurtainCoverage").GetValue(replay),
                Is.EqualTo((float)sampleType.GetField("CurtainCoverage").GetValue(first)).Within(.0001f));
            Assert.That(sampleType.GetField("CurtainDirection").GetValue(first).ToString(), Is.EqualTo("RightToLeft"));

            set.Invoke(null, new object[] { preset, curtain, .6f, .85f, leftToRight, ease });
            values = resolve.Invoke(null, new object[] { preset });
            object opposite = sample.Invoke(null, new object[] { .25f, values });
            Assert.That(sampleType.GetField("CurtainDirection").GetValue(opposite).ToString(), Is.EqualTo("LeftToRight"));
        }

        [Test]
        public void BackgroundTransitionPreviewUsesDistinctRealRokasBackgrounds()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveBackgroundTransition", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetBackgroundTransitionPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleBackgroundTransition", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(set, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnWorkshopPreviewFrame bus = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                new VnPresentationWorkshopPreset(), VnWorkshopResolution.Wide1280x720, VnWorkshopPreviewScene.BusStopKeiko);
            VnWorkshopPreviewFrame night = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                new VnPresentationWorkshopPreset(), VnWorkshopResolution.Wide1280x720, VnWorkshopPreviewScene.NightSkyKeiko);
            Assert.That(bus.BackgroundTexture, Is.SameAs(assets.vnBusStopRainNight));
            Assert.That(night.BackgroundTexture, Is.SameAs(assets.vnNightSkyRain));
            Assert.That(bus.BackgroundTexture, Is.Not.SameAs(night.BackgroundTexture));

            Type modeType = Type.GetType(Namespace + "VnWorkshopBackgroundTransitionMode, " + EditorAssembly);
            Type directionType = Type.GetType(Namespace + "VnWorkshopCurtainDirection, " + EditorAssembly);
            Type easingType = Type.GetType(Namespace + "VnWorkshopEasing, " + EditorAssembly);
            var preset = new VnPresentationWorkshopPreset();
            set.Invoke(null, new object[]
            {
                preset,
                Enum.Parse(modeType, "Fade"),
                .4f,
                .35f,
                Enum.Parse(directionType, "RightToLeft"),
                Enum.Parse(easingType, "EaseInOut")
            });
            object values = resolve.Invoke(null, new object[] { preset });
            object mid = sample.Invoke(null, new object[] { .5f, values });
            Type sampleType = mid.GetType();
            Assert.That((float)sampleType.GetField("SourceAlpha").GetValue(mid), Is.InRange(0f, 1f));
            Assert.That((float)sampleType.GetField("TargetAlpha").GetValue(mid), Is.InRange(0f, 1f));
        }
    }
}
