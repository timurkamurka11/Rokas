using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
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

                string storedSample = (string)RequireFinalParityMethod(windowType, "ComposerGetPreviewSampleText")
                    .Invoke(window, null);
                Assert.That(storedSample, Is.EqualTo(expectedSample),
                    "Canonical Composer preset identity must retain Preview Text before glyph validation.");

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
        public void FinalParity_ComposerPreviewTextUiSurfacesLiveGlyphWarning()
        {
            Type windowType = typeof(VnPresentationWorkshopWindow);
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                RequireFinalParityMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireFinalParityMethod(windowType, "ComposerSetPresentationScope", typeof(bool))
                    .Invoke(window, new object[] { false });

                RequireFinalParityMethod(windowType, "ComposerSetPreviewSampleText", typeof(string))
                    .Invoke(window, new object[] { "Привет" });
                string supported = (string)RequireFinalParityMethod(windowType, "ComposerGetPreviewTextGlyphWarning")
                    .Invoke(window, null);
                Assert.That(supported, Is.Empty, "Supported Cyrillic preview text should not show a false warning.");

                RequireFinalParityMethod(windowType, "ComposerSetPreviewSampleText", typeof(string))
                    .Invoke(window, new object[] { "Missing glyph: \u0378" });
                string missing = (string)RequireFinalParityMethod(windowType, "ComposerGetPreviewTextGlyphWarning")
                    .Invoke(window, null);
                Assert.That(missing, Does.Contain("missing glyph").IgnoreCase,
                    "Changing Preview Text must immediately re-evaluate canonical glyph coverage.");

                string source = ReadProjectSource(
                    "Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
                Assert.That(source, Does.Contain("ComposerGetPreviewTextGlyphWarning()"),
                    "#44 requires the actual Scene Composer Preview Text UI to consume the canonical glyph validator.");
                Assert.That(source, Does.Contain("EditorGUILayout.HelpBox(glyphWarning, MessageType.Warning)"),
                    "#44 requires the user to actually see the missing-glyph diagnostic in Scene Composer.");
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
        public void FinalParity_ComposerUiFeedbackPreviewUsesEffectiveDefaultsOverridesAndCentralRenderer()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerSetPresentationScope(true);
                VnPresentationWorkshopPreset defaults = window.ComposerGetActivePresentationPreset();
                VnPresentationWorkshopVn10Resolver.SetUiFeedbackPreviewOverrides(
                    defaults,
                    1.20f,
                    .94f,
                    new Vector2(0f, -5f),
                    .12f,
                    VnWorkshopEasing.EaseOut,
                    1.10f,
                    .88f,
                    .95f,
                    .90f,
                    .18f,
                    .48f);

                window.ComposerSetPresentationScope(false);
                VnPresentationWorkshopPreset sceneOverrides = window.ComposerGetActivePresentationPreset();
                VnPresentationWorkshopVn10Resolver.SetUiFeedbackPreviewOverrides(
                    sceneOverrides,
                    1.06f,
                    .80f,
                    new Vector2(7f, -3f),
                    .12f,
                    VnWorkshopEasing.EaseOut,
                    1.08f,
                    .70f,
                    1f,
                    .60f,
                    .18f,
                    .50f);

                MethodInfo sampleMethod = RequireFinalParityMethod(
                    typeof(VnPresentationWorkshopWindow),
                    "ComposerSampleUiFeedback",
                    typeof(VnWorkshopElement),
                    typeof(VnWorkshopUiFeedbackState),
                    typeof(float));
                var inheritedHover = (VnWorkshopUiFeedbackSample)sampleMethod.Invoke(window, new object[]
                {
                    VnWorkshopElement.Back, VnWorkshopUiFeedbackState.Hover, 1f
                });
                Assert.That(inheritedHover.ScaleMultiplier, Is.EqualTo(1.20f).Within(.0001f),
                    "Scene UI feedback preview must inherit Project Defaults when the Scene Override leaves that field unset.");
                Assert.That(inheritedHover.Brightness, Is.EqualTo(1.10f).Within(.0001f));
                Assert.That(inheritedHover.Alpha, Is.EqualTo(.95f).Within(.0001f));

                var scenePressed = (VnWorkshopUiFeedbackSample)sampleMethod.Invoke(window, new object[]
                {
                    VnWorkshopElement.Back, VnWorkshopUiFeedbackState.Pressed, 1f
                });
                Assert.That(scenePressed.ScaleMultiplier, Is.EqualTo(.80f).Within(.0001f));
                Assert.That(scenePressed.PositionOffset, Is.EqualTo(new Vector2(7f, -3f)));
                Assert.That(scenePressed.Brightness, Is.EqualTo(.70f).Within(.0001f));
                Assert.That(scenePressed.Alpha, Is.EqualTo(.60f).Within(.0001f));

                VnWorkshopPreviewFrame frame = window.ComposerBuildSelectedPreviewFrame();
                MethodInfo resolveRect = typeof(VnPresentationWorkshopPreviewRenderer).GetMethod(
                    "ResolveUiFeedbackLogicalRect",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(VnWorkshopPreviewFrame), typeof(VnWorkshopElement), typeof(VnWorkshopUiFeedbackSample) },
                    null);
                Assert.That(resolveRect, Is.Not.Null,
                    "#90-93 require a renderer-facing UI feedback transform path, not sampler-only API coverage.");
                Rect pressedRect = (Rect)resolveRect.Invoke(null, new object[]
                {
                    frame, VnWorkshopElement.Back, scenePressed
                });
                Assert.That(pressedRect.size, Is.EqualTo(frame.Back.size * .80f));
                Assert.That(pressedRect.center, Is.EqualTo(frame.Back.center + new Vector2(7f, -3f)));

                var bakedPressed = (VnWorkshopUiFeedbackSample)sampleMethod.Invoke(window, new object[]
                {
                    VnWorkshopElement.MuteHitRegion, VnWorkshopUiFeedbackState.Pressed, 1f
                });
                Rect bakedRect = (Rect)resolveRect.Invoke(null, new object[]
                {
                    frame, VnWorkshopElement.MuteHitRegion, bakedPressed
                });
                Assert.That(bakedRect, Is.EqualTo(frame.MuteHitRegion),
                    "Baked Mute/Pause/Skip controls must not receive independent transform feedback.");

                string authoring = ReadProjectSource(
                    "Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
                Assert.That(authoring, Does.Contain("ComposerSetUiFeedbackPreview"),
                    "Scene Composer must expose authoring controls for Normal/Hover/Pressed/Release preview phases.");
                Assert.That(authoring, Does.Contain("\"Normal\"").And.Contain("\"Hover\"").And.Contain("\"Pressed\"").And.Contain("\"Release\""));
                Assert.That(authoring, Does.Not.Contain("DrawSceneComposerUiFeedbackPreview(previewRect, frame);"),
                    "UI feedback must be integrated into the existing renderer pass instead of drawn as an additive overlay.");

                string workspace = ReadProjectSource(
                    "Assets/Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposer.cs");
                Assert.That(workspace,
                    Does.Contain("VnPresentationWorkshopPreviewRenderer.Draw(previewRect, frame, selectedUi, staticAuthoringPreview,"),
                    "The central Scene Preview must pass canonical UI feedback into the existing renderer before Back/Next are drawn.");
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

        private static string ReadProjectSource(string relativePath)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
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
