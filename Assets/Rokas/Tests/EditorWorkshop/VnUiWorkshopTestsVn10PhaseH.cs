using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseH
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void IndependentBackNextFeedbackSupportsNormalHoverPressedAndRelease()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveUiFeedback", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleUiFeedback", BindingFlags.Public | BindingFlags.Static);
            Type stateType = Type.GetType(Namespace + "VnWorkshopUiFeedbackState, " + EditorAssembly);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);
            Assert.That(stateType, Is.Not.Null);

            object values = resolve.Invoke(null, new object[] { new VnPresentationWorkshopPreset() });
            object normalState = Enum.Parse(stateType, "Normal");
            object hoverState = Enum.Parse(stateType, "Hover");
            object pressedState = Enum.Parse(stateType, "Pressed");
            object releaseState = Enum.Parse(stateType, "Release");

            foreach (VnWorkshopElement element in new[] { VnWorkshopElement.Back, VnWorkshopElement.Next })
            {
                object normal = sample.Invoke(null, new object[] { element, normalState, 1f, values });
                object hover = sample.Invoke(null, new object[] { element, hoverState, 1f, values });
                object pressed = sample.Invoke(null, new object[] { element, pressedState, 1f, values });
                object releaseStart = sample.Invoke(null, new object[] { element, releaseState, 0f, values });
                object releaseEnd = sample.Invoke(null, new object[] { element, releaseState, 1f, values });

                Assert.That(BoolField(normal, "UsesIndependentTransform"), Is.True);
                Assert.That(FloatField(normal, "ScaleMultiplier"), Is.EqualTo(1f).Within(.0001f));
                Assert.That(VectorField(normal, "PositionOffset"), Is.EqualTo(Vector2.zero));
                Assert.That(FloatField(hover, "ScaleMultiplier"), Is.GreaterThan(1f));
                Assert.That(FloatField(pressed, "ScaleMultiplier"), Is.Not.EqualTo(1f).Within(.0001f));
                Assert.That(VectorField(pressed, "PositionOffset"), Is.Not.EqualTo(Vector2.zero));
                Assert.That(FloatField(releaseStart, "ScaleMultiplier"), Is.EqualTo(FloatField(pressed, "ScaleMultiplier")).Within(.0001f));
                Assert.That(FloatField(releaseEnd, "ScaleMultiplier"), Is.EqualTo(1f).Within(.0001f));
                Assert.That(VectorField(releaseEnd, "PositionOffset"), Is.EqualTo(Vector2.zero));
            }
        }

        [Test]
        public void BakedPanelControlsUseOverlayFeedbackWithoutIndependentMovement()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveUiFeedback", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleUiFeedback", BindingFlags.Public | BindingFlags.Static);
            Type stateType = Type.GetType(Namespace + "VnWorkshopUiFeedbackState, " + EditorAssembly);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);
            Assert.That(stateType, Is.Not.Null);

            object values = resolve.Invoke(null, new object[] { new VnPresentationWorkshopPreset() });
            object hoverState = Enum.Parse(stateType, "Hover");
            object pressedState = Enum.Parse(stateType, "Pressed");

            foreach (VnWorkshopElement element in new[]
                     {
                         VnWorkshopElement.MuteHitRegion,
                         VnWorkshopElement.PauseHitRegion,
                         VnWorkshopElement.SkipHitRegion
                     })
            {
                object hover = sample.Invoke(null, new object[] { element, hoverState, 1f, values });
                object pressed = sample.Invoke(null, new object[] { element, pressedState, 1f, values });
                Assert.That(BoolField(hover, "UsesIndependentTransform"), Is.False);
                Assert.That(BoolField(pressed, "UsesIndependentTransform"), Is.False);
                Assert.That(FloatField(hover, "ScaleMultiplier"), Is.EqualTo(1f).Within(.0001f));
                Assert.That(FloatField(pressed, "ScaleMultiplier"), Is.EqualTo(1f).Within(.0001f));
                Assert.That(VectorField(hover, "PositionOffset"), Is.EqualTo(Vector2.zero));
                Assert.That(VectorField(pressed, "PositionOffset"), Is.EqualTo(Vector2.zero));
                Assert.That(FloatField(hover, "OverlayHighlight"), Is.GreaterThan(0f));
                Assert.That(FloatField(pressed, "OverlayHighlight"), Is.GreaterThan(FloatField(hover, "OverlayHighlight")));
                Assert.That(FloatField(pressed, "Brightness"), Is.Not.EqualTo(1f).Within(.0001f));
            }
        }

        [Test]
        public void UiFeedbackOverridesResolveResetAndZeroDurationSnaps()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            MethodInfo resolve = resolver.GetMethod("ResolveUiFeedback", BindingFlags.Public | BindingFlags.Static);
            MethodInfo set = resolver.GetMethod("SetUiFeedbackPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo reset = resolver.GetMethod("ResetUiFeedbackPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo sample = resolver.GetMethod("SampleUiFeedback", BindingFlags.Public | BindingFlags.Static);
            Type stateType = Type.GetType(Namespace + "VnWorkshopUiFeedbackState, " + EditorAssembly);
            Type easingType = Type.GetType(Namespace + "VnWorkshopEasing, " + EditorAssembly);
            Assert.That(resolve, Is.Not.Null);
            Assert.That(set, Is.Not.Null);
            Assert.That(reset, Is.Not.Null);
            Assert.That(sample, Is.Not.Null);
            Assert.That(stateType, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object linear = Enum.Parse(easingType, "Linear");
            set.Invoke(null, new object[]
            {
                preset, 1.12f, .92f, new Vector2(0f, -7f), 0f, linear,
                1.08f, .86f, .97f, .80f, .20f, .55f
            });
            Assert.That(preset.HasAnyOverride, Is.True);
            object values = resolve.Invoke(null, new object[] { preset });
            Assert.That(FloatField(values, "HoverScale"), Is.EqualTo(1.12f).Within(.0001f));
            Assert.That(FloatField(values, "PressedScale"), Is.EqualTo(.92f).Within(.0001f));
            Assert.That(VectorField(values, "PressedOffset"), Is.EqualTo(new Vector2(0f, -7f)));
            Assert.That(FloatField(values, "Duration"), Is.EqualTo(0f).Within(.0001f));
            Assert.That(FloatField(values, "PressedOverlayHighlight"), Is.EqualTo(.55f).Within(.0001f));

            object hoverState = Enum.Parse(stateType, "Hover");
            object snapped = sample.Invoke(null, new object[] { VnWorkshopElement.Back, hoverState, .1f, values });
            Assert.That(FloatField(snapped, "ScaleMultiplier"), Is.EqualTo(1.12f).Within(.0001f));
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
    }
}
