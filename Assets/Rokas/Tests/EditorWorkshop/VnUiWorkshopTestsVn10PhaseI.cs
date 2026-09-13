using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseI
    {
        private static readonly BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        private static readonly BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        [Test]
        public void WorkshopExposesAllPresentationMotionSectionsInExistingWindow()
        {
            Type windowType = typeof(VnPresentationWorkshopWindow);
            MethodInfo getSections = windowType.GetMethod("GetPresentationSectionLabels", PublicStatic);
            Assert.That(getSections, Is.Not.Null);

            string[] labels = (string[])getSections.Invoke(null, null);
            string[] required =
            {
                "Typography",
                "Preview Text",
                "Text Reveal",
                "Character Expression",
                "Character Enter / Exit",
                "Action Bounce",
                "Background Transition",
                "Stage Layout",
                "Speaker Focus",
                "UI Feedback",
                "Timing / Pacing"
            };

            CollectionAssert.AreEqual(required, labels);
            Assert.That(VnPresentationWorkshopWindow.MenuPath, Is.EqualTo("ROKAS/VN UI Workshop"));
        }

        [Test]
        public void TypographyControlsDriveExistingModelAndPreviewFrameIndependently()
        {
            var window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                MethodInfo setTypography = typeof(VnPresentationWorkshopWindow).GetMethod("SetTypographyPreviewValues", PublicInstance);
                Assert.That(setTypography, Is.Not.Null);

                setTypography.Invoke(window, new object[]
                {
                    VnWorkshopFontPreset.ProjectSerif,
                    31f,
                    2f,
                    4f,
                    6f,
                    VnWorkshopTextAlignment.Center,
                    VnWorkshopFontPreset.ProjectSans,
                    27f,
                    1f
                });

                VnWorkshopTypographyValues resolved = VnPresentationWorkshopVn10Resolver.ResolveTypography(window.CurrentPreset);
                Assert.That(resolved.DialogueFontPreset, Is.EqualTo(VnWorkshopFontPreset.ProjectSerif));
                Assert.That(resolved.DialogueFontSize, Is.EqualTo(31f).Within(.0001f));
                Assert.That(resolved.SpeakerFontSize, Is.EqualTo(27f).Within(.0001f));

                object frame = window.BuildPreviewFrame();
                PropertyInfo typographyProperty = frame.GetType().GetProperty("Typography", PublicInstance);
                Assert.That(typographyProperty, Is.Not.Null);
                object previewTypography = typographyProperty.GetValue(frame);
                Assert.That(FloatField(previewTypography, "DialogueFontSize"), Is.EqualTo(31f).Within(.0001f));
                Assert.That(FloatField(previewTypography, "SpeakerFontSize"), Is.EqualTo(27f).Within(.0001f));

                window.ComparisonView = VnWorkshopComparisonView.Original;
                object originalFrame = window.BuildPreviewFrame();
                object originalTypography = typographyProperty.GetValue(originalFrame);
                Assert.That(FloatField(originalTypography, "DialogueFontSize"), Is.EqualTo(22f).Within(.0001f));
                Assert.That(FloatField(originalTypography, "SpeakerFontSize"), Is.EqualTo(26f).Within(.0001f));

                VnWorkshopTypographyValues currentStillResolved = VnPresentationWorkshopVn10Resolver.ResolveTypography(window.CurrentPreset);
                Assert.That(currentStillResolved.DialogueFontSize, Is.EqualTo(31f).Within(.0001f));
                Assert.That(currentStillResolved.SpeakerFontSize, Is.EqualTo(27f).Within(.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void PreviewSampleTextSupportsManualUtf8TxtClearResetAndFeedsPreview()
        {
            var window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            string path = Path.Combine(Path.GetTempPath(), "rokas-vn10-preview-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                Type windowType = typeof(VnPresentationWorkshopWindow);
                MethodInfo set = windowType.GetMethod("SetPreviewSampleText", PublicInstance);
                MethodInfo load = windowType.GetMethod("LoadPreviewSampleTextFromFile", PublicInstance);
                MethodInfo clear = windowType.GetMethod("ClearPreviewSampleText", PublicInstance);
                MethodInfo reset = windowType.GetMethod("ResetPreviewSampleText", PublicInstance);
                PropertyInfo value = windowType.GetProperty("PreviewSampleText", PublicInstance);
                Assert.That(set, Is.Not.Null);
                Assert.That(load, Is.Not.Null);
                Assert.That(clear, Is.Not.Null);
                Assert.That(reset, Is.Not.Null);
                Assert.That(value, Is.Not.Null);

                set.Invoke(window, new object[] { "Manual preview line" });
                Assert.That(value.GetValue(window), Is.EqualTo("Manual preview line"));

                const string russian = "Привет, Мина! Это русский UTF-8 текст.\nВторая строка.";
                File.WriteAllText(path, russian, new UTF8Encoding(false));
                load.Invoke(window, new object[] { path });
                Assert.That(value.GetValue(window), Is.EqualTo(russian));

                VnWorkshopPreviewFrame frame = window.BuildPreviewFrame();
                Assert.That(frame.Dialogue, Is.EqualTo(russian));

                clear.Invoke(window, null);
                Assert.That(value.GetValue(window), Is.EqualTo(string.Empty));
                reset.Invoke(window, null);
                Assert.That((string)value.GetValue(window), Is.Not.Empty);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MotionLabPreviewCommandsAreEphemeralAndDoNotMutatePresetOrProduction()
        {
            var window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                Type windowType = typeof(VnPresentationWorkshopWindow);
                string[] commands =
                {
                    "PreviewTypewriter",
                    "PausePreview",
                    "RestartPreview",
                    "CompletePreviewLine",
                    "PreviewExpression",
                    "PreviewCharacterEnter",
                    "PreviewCharacterExit",
                    "PreviewBounce",
                    "PreviewBackgroundTransition",
                    "PreviewSpeakerSwitch",
                    "PreviewButtonPress"
                };
                foreach (string command in commands)
                    Assert.That(windowType.GetMethod(command, PublicInstance), Is.Not.Null, command);

                Assert.That(windowType.GetMethod("ApplyToProduction", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static), Is.Null);

                string before = JsonUtility.ToJson(window.CurrentPreset);
                windowType.GetMethod("PreviewTypewriter", PublicInstance).Invoke(window, null);
                windowType.GetMethod("PausePreview", PublicInstance).Invoke(window, null);
                windowType.GetMethod("RestartPreview", PublicInstance).Invoke(window, null);
                windowType.GetMethod("CompletePreviewLine", PublicInstance).Invoke(window, null);
                windowType.GetMethod("PreviewBounce", PublicInstance).Invoke(window, null);
                string after = JsonUtility.ToJson(window.CurrentPreset);

                Assert.That(after, Is.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static float FloatField(object value, string name)
        {
            return (float)value.GetType().GetField(name).GetValue(value);
        }
    }
}
