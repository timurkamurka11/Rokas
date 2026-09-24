using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseA
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void TypographyWorkbenchResolvesImmutableDefaultsAndProjectFontPresets()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            Assert.That(resolver, Is.Not.Null, "VN10 must extend the existing Workshop with a presentation resolver.");

            Type fontPreset = Type.GetType(Namespace + "VnWorkshopFontPreset, " + EditorAssembly);
            Assert.That(fontPreset, Is.Not.Null);
            CollectionAssert.AreEquivalent(new[] { "ProjectSans", "ProjectSerif" }, Enum.GetNames(fontPreset));

            MethodInfo resolveTypography = resolver.GetMethod("ResolveTypography", BindingFlags.Public | BindingFlags.Static);
            Assert.That(resolveTypography, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            object baseline = resolveTypography.Invoke(null, new object[] { preset });
            Type valuesType = baseline.GetType();
            Assert.That((float)valuesType.GetField("DialogueFontSize").GetValue(baseline), Is.EqualTo(22f));
            Assert.That((float)valuesType.GetField("SpeakerFontSize").GetValue(baseline), Is.EqualTo(26f));
            Assert.That(valuesType.GetField("DialogueFontPreset").GetValue(baseline).ToString(), Is.EqualTo("ProjectSans"));
            Assert.That(valuesType.GetField("SpeakerFontPreset").GetValue(baseline).ToString(), Is.EqualTo("ProjectSans"));
            Assert.That(preset.HasAnyOverride, Is.False, "Resolving typography must not copy baseline values into CurrentPreset.");

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            Assert.That(assets.sans, Is.Not.Null);
            Assert.That(assets.serif, Is.Not.Null, "Typography comparison must use only already-authored project fonts.");
        }

        [Test]
        public void TypographyOverridesResolveWithoutMutatingOriginalBaseline()
        {
            Type resolver = Type.GetType(Namespace + "VnPresentationWorkshopVn10Resolver, " + EditorAssembly);
            Assert.That(resolver, Is.Not.Null);
            MethodInfo apply = resolver.GetMethod("SetTypographyPreviewOverrides", BindingFlags.Public | BindingFlags.Static);
            MethodInfo resolve = resolver.GetMethod("ResolveTypography", BindingFlags.Public | BindingFlags.Static);
            Assert.That(apply, Is.Not.Null);
            Assert.That(resolve, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            Type fontPreset = Type.GetType(Namespace + "VnWorkshopFontPreset, " + EditorAssembly);
            Type alignment = Type.GetType(Namespace + "VnWorkshopTextAlignment, " + EditorAssembly);
            object serif = Enum.Parse(fontPreset, "ProjectSerif");
            object centered = Enum.Parse(alignment, "Center");

            apply.Invoke(null, new object[] { preset, serif, 28f, 1.5f, 5f, 7f, centered, serif, 31f, .75f });
            object resolved = resolve.Invoke(null, new object[] { preset });
            Type t = resolved.GetType();
            Assert.That(t.GetField("DialogueFontPreset").GetValue(resolved).ToString(), Is.EqualTo("ProjectSerif"));
            Assert.That((float)t.GetField("DialogueFontSize").GetValue(resolved), Is.EqualTo(28f));
            Assert.That((float)t.GetField("DialogueCharacterSpacing").GetValue(resolved), Is.EqualTo(1.5f));
            Assert.That((float)t.GetField("DialogueLineSpacing").GetValue(resolved), Is.EqualTo(5f));
            Assert.That((float)t.GetField("DialogueParagraphSpacing").GetValue(resolved), Is.EqualTo(7f));
            Assert.That(t.GetField("DialogueAlignment").GetValue(resolved).ToString(), Is.EqualTo("Center"));
            Assert.That((float)t.GetField("SpeakerFontSize").GetValue(resolved), Is.EqualTo(31f));
            Assert.That((float)t.GetField("SpeakerCharacterSpacing").GetValue(resolved), Is.EqualTo(.75f));
            Assert.That(preset.HasAnyOverride, Is.True);

            var original = new VnPresentationWorkshopPreset();
            object originalValues = resolve.Invoke(null, new object[] { original });
            Assert.That((float)originalValues.GetType().GetField("DialogueFontSize").GetValue(originalValues), Is.EqualTo(22f));
            Assert.That(original.HasAnyOverride, Is.False);
        }

        [Test]
        public void CharacterSafeFrameClipsBodyGeometryToGameViewportAtBothRequiredResolutions()
        {
            Type renderer = Type.GetType(Namespace + "VnPresentationWorkshopPreviewRenderer, " + EditorAssembly);
            Assert.That(renderer, Is.Not.Null);
            MethodInfo clip = renderer.GetMethod("ClipLogicalRectToViewport", BindingFlags.Public | BindingFlags.Static);
            Assert.That(clip, Is.Not.Null, "VN10 preview must expose deterministic safe-frame clipping geometry.");

            foreach (VnWorkshopResolution resolution in new[]
                     {
                         VnWorkshopResolution.Wide1280x720,
                         VnWorkshopResolution.FourThree1024x768
                     })
            {
                VnWorkshopPreviewFrame frame = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                    new VnPresentationWorkshopPreset(), resolution, VnWorkshopPreviewScene.MinaBody);
                Rect visible = (Rect)clip.Invoke(null, new object[] { frame.MinaBody, frame.VirtualCanvasSize });
                Assert.That(visible.yMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(visible.xMin, Is.GreaterThanOrEqualTo(0f));
                Assert.That(visible.yMax, Is.LessThanOrEqualTo(frame.VirtualCanvasSize.y));
                Assert.That(visible.xMax, Is.LessThanOrEqualTo(frame.VirtualCanvasSize.x));
                Assert.That(frame.DialoguePanel.yMax, Is.GreaterThan(visible.yMin),
                    "Dialogue UI must remain capable of occluding the lower body after viewport clipping.");
            }
        }
    }
}
