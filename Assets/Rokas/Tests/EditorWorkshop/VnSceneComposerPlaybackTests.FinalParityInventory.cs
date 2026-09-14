using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void FinalParity_ComposerGlyphWarningUsesCanonicalPresentationWithoutLegacyCurrentPreset()
        {
            Type windowType = typeof(VnPresentationWorkshopWindow);
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                const string expectedSample = "Missing glyph: \u0378";
                RequireFinalParityMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireFinalParityMethod(windowType, "ComposerSetPresentationScope", typeof(bool))
                    .Invoke(window, new object[] { false });
                RequireFinalParityMethod(windowType, "ComposerSetPreviewSampleText", typeof(string))
                    .Invoke(window, new object[] { expectedSample });

                var active = (VnPresentationWorkshopPreset)RequireFinalParityMethod(
                    windowType, "ComposerGetActivePresentationPreset").Invoke(window, null);
                string storedSample = (string)RequireFinalParityMethod(windowType, "ComposerGetPreviewSampleText")
                    .Invoke(window, null);
                Assert.That(storedSample, Is.EqualTo(expectedSample),
                    "Diagnostic: canonical Composer preset identity must retain Preview Text before glyph validation.");

                VnWorkshopTypographyValues typography = VnPresentationWorkshopVn10Resolver.ResolveTypography(active);
                RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
                Assert.That(assets, Is.Not.Null,
                    "Diagnostic: Resources/RokasAssets must load in the same EditMode environment as Composer glyph validation.");
                Font dialogueFont = typography.DialogueFontPreset == VnWorkshopFontPreset.ProjectSerif
                    ? assets.serif : assets.sans;
                Font speakerFont = typography.SpeakerFontPreset == VnWorkshopFontPreset.ProjectSerif
                    ? assets.serif : assets.sans;
                Assert.That(dialogueFont, Is.Not.Null,
                    "Diagnostic: resolved canonical dialogue font must be available.");
                Assert.That(speakerFont, Is.Not.Null,
                    "Diagnostic: resolved canonical speaker font must be available.");
                Assert.That(dialogueFont.HasCharacter('\u0378'), Is.False,
                    "Diagnostic: U+0378 must be unsupported by the resolved canonical dialogue font.");
                Assert.That(speakerFont.HasCharacter('\u0378'), Is.False,
                    "Diagnostic: U+0378 must be unsupported by the resolved canonical speaker font.");

                FieldInfo legacy = windowType.GetField("currentPreset", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(legacy, Is.Not.Null, "The test must explicitly remove the legacy Workshop preset dependency.");
                legacy.SetValue(window, null);

                string warning = (string)RequireFinalParityMethod(windowType, "ComposerGetPreviewTextGlyphWarning")
                    .Invoke(window, null);

                Assert.That(warning, Does.Contain("missing glyph").IgnoreCase);
                Assert.That(RequireFinalParityMethod(windowType, "ComposerGetActivePresentationPreset").Invoke(window, null), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void FinalParity_ComposerUiFeedbackSamplesHoverPressedReleaseFromCanonicalPreset()
        {
            Type windowType = typeof(VnPresentationWorkshopWindow);
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                RequireFinalParityMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireFinalParityMethod(windowType, "ComposerSetPresentationScope", typeof(bool))
                    .Invoke(window, new object[] { false });
                var active = (VnPresentationWorkshopPreset)RequireFinalParityMethod(windowType, "ComposerGetActivePresentationPreset")
                    .Invoke(window, null);
                VnPresentationWorkshopVn10Resolver.SetUiFeedbackPreviewOverrides(
                    active,
                    1.20f,
                    .80f,
                    new Vector2(7f, -3f),
                    .25f,
                    VnWorkshopEasing.Linear,
                    1.10f,
                    .70f,
                    .90f,
                    .60f,
                    .20f,
                    .50f);

                MethodInfo sample = RequireFinalParityMethod(
                    windowType,
                    "ComposerSampleUiFeedback",
                    typeof(VnWorkshopElement),
                    typeof(VnWorkshopUiFeedbackState),
                    typeof(float));

                var hover = (VnWorkshopUiFeedbackSample)sample.Invoke(window, new object[]
                {
                    VnWorkshopElement.Back, VnWorkshopUiFeedbackState.Hover, 1f
                });
                Assert.That(hover.ScaleMultiplier, Is.EqualTo(1.20f).Within(.0001f));
                Assert.That(hover.Brightness, Is.EqualTo(1.10f).Within(.0001f));
                Assert.That(hover.Alpha, Is.EqualTo(.90f).Within(.0001f));

                var pressed = (VnWorkshopUiFeedbackSample)sample.Invoke(window, new object[]
                {
                    VnWorkshopElement.Back, VnWorkshopUiFeedbackState.Pressed, 1f
                });
                Assert.That(pressed.ScaleMultiplier, Is.EqualTo(.80f).Within(.0001f));
                Assert.That(pressed.PositionOffset, Is.EqualTo(new Vector2(7f, -3f)));
                Assert.That(pressed.Brightness, Is.EqualTo(.70f).Within(.0001f));
                Assert.That(pressed.Alpha, Is.EqualTo(.60f).Within(.0001f));

                var release = (VnWorkshopUiFeedbackSample)sample.Invoke(window, new object[]
                {
                    VnWorkshopElement.Back, VnWorkshopUiFeedbackState.Release, 1f
                });
                Assert.That(release.ScaleMultiplier, Is.EqualTo(1f).Within(.0001f));
                Assert.That(release.PositionOffset, Is.EqualTo(Vector2.zero));
                Assert.That(release.Brightness, Is.EqualTo(1f).Within(.0001f));
                Assert.That(release.Alpha, Is.EqualTo(1f).Within(.0001f));

                var bakedPressed = (VnWorkshopUiFeedbackSample)sample.Invoke(window, new object[]
                {
                    VnWorkshopElement.MuteHitRegion, VnWorkshopUiFeedbackState.Pressed, 1f
                });
                Assert.That(bakedPressed.ScaleMultiplier, Is.EqualTo(1f).Within(.0001f));
                Assert.That(bakedPressed.PositionOffset, Is.EqualTo(Vector2.zero));
                Assert.That(bakedPressed.Brightness, Is.EqualTo(.70f).Within(.0001f));
                Assert.That(bakedPressed.Alpha, Is.EqualTo(.60f).Within(.0001f));
                Assert.That(bakedPressed.OverlayHighlight, Is.EqualTo(.50f).Within(.0001f));

                FieldInfo legacy = windowType.GetField("currentPreset", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(legacy, Is.Not.Null);
                Assert.That(legacy.GetValue(window), Is.Not.SameAs(active),
                    "UI feedback preview must consume canonical Composer presentation state, not legacy currentPreset.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void FinalParity_PlayAllHonorsCanonicalAutoPreviewSequenceGapBetweenScenes()
        {
            var project = new VnSceneComposerProject();
            VnPresentationWorkshopVn10Resolver.SetTimingPreviewOverrides(project.defaultPresentation, 0f, 0f, .30f);
            project.scenes.Add(new VnSceneComposerScene
            {
                label = "A",
                timing = new VnSceneComposerTiming
                {
                    previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration,
                    previewAutoDuration = .20f
                }
            });
            project.scenes.Add(new VnSceneComposerScene
            {
                label = "B",
                timing = new VnSceneComposerTiming
                {
                    previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration,
                    previewAutoDuration = .20f
                }
            });

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayAll();
                controller.Advance(.20f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0),
                    "The authored scene must hold at its completed frame while the sequence gap elapses.");

                controller.Advance(.29f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));

                controller.Advance(.01f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1),
                    "Play All should advance only after the canonical AutoPreviewSequenceGap has elapsed.");
            }
        }

        private static MethodInfo RequireFinalParityMethod(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameters ?? Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null, "Missing required Scene Composer parity API: " + name);
            return method;
        }
    }
}
