using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void MDREG_BounceDurationEditDoesNotMutateAmplitude()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3Invoke(window, "ComposerSetPresentationScope", false);
                object easing = Md3EnumValue("VnWorkshopEasing", "EaseInOut");
                Md3Invoke(window, "ComposerSetBounce", 42f, .5f, .03f, .15f, easing);

                Md3Invoke(window, "ComposerSetBounceDuration", 2f);

                object bounce = MdRegResolveBounce(window);
                Assert.That((float)Md3GetField(bounce, "Amplitude"), Is.EqualTo(42f).Within(.0001f),
                    "Editing Bounce duration must preserve the authored Strength exactly.");
                Assert.That((float)Md3GetField(bounce, "Duration"), Is.EqualTo(2f).Within(.0001f));
            }
            finally
            {
                UnityEditor.Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MDREG_BounceAmplitudeEditDoesNotMutateDuration()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3Invoke(window, "ComposerSetPresentationScope", false);
                object easing = Md3EnumValue("VnWorkshopEasing", "EaseInOut");
                Md3Invoke(window, "ComposerSetBounce", 18f, 1.75f, .03f, .15f, easing);

                Md3Invoke(window, "ComposerSetBounceAmplitude", 80f);

                object bounce = MdRegResolveBounce(window);
                Assert.That((float)Md3GetField(bounce, "Amplitude"), Is.EqualTo(80f).Within(.0001f));
                Assert.That((float)Md3GetField(bounce, "Duration"), Is.EqualTo(1.75f).Within(.0001f),
                    "Editing Bounce Strength must preserve the authored duration exactly.");
            }
            finally
            {
                UnityEditor.Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MDREG_BounceAmplitudeChangesRendererFacingMotion()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3Invoke(window, "ComposerSetPresentationScope", false);
                object easing = Md3EnumValue("VnWorkshopEasing", "EaseInOut");

                Md3Invoke(window, "ComposerSetBounce", 5f, 1f, .03f, .15f, easing);
                object weakSample = MdRegSampleBounce(window, .5f);

                Md3Invoke(window, "ComposerSetBounce", 80f, 1f, .03f, .15f, easing);
                object strongSample = MdRegSampleBounce(window, .5f);

                float weakY = Mathf.Abs(((Vector2)Md3GetField(weakSample, "PositionOffset")).y);
                float strongY = Mathf.Abs(((Vector2)Md3GetField(strongSample, "PositionOffset")).y);
                Assert.That(strongY, Is.GreaterThan(weakY * 8f),
                    "Strength 80 must produce materially stronger renderer-facing movement than Strength 5.");
            }
            finally
            {
                UnityEditor.Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MDREG_BounceDurationChangesFocusedPreviewTiming()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            object bounceEffect = Md3EnumValue("VnWorkshopPreviewEffect", "Bounce");
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3Invoke(window, "ActivateSceneComposerWorkspace");
                Md3Invoke(window, "ComposerSetPresentationScope", false);
                object easing = Md3EnumValue("VnWorkshopEasing", "EaseInOut");

                Md3Invoke(window, "ComposerSetBounce", 60f, .10f, .03f, .15f, easing);
                Md3Invoke(window, "ComposerPreviewFocusedEffect", bounceEffect);
                float shortDuration = (float)Md3GetField(Md3Invoke(window, "GetPreviewPlaybackSnapshot"), "Duration");

                Md3Invoke(window, "ComposerSetBounce", 60f, 2f, .03f, .15f, easing);
                Md3Invoke(window, "ComposerPreviewFocusedEffect", bounceEffect);
                float longDuration = (float)Md3GetField(Md3Invoke(window, "GetPreviewPlaybackSnapshot"), "Duration");

                Assert.That(shortDuration, Is.EqualTo(.10f).Within(.0001f));
                Assert.That(longDuration, Is.EqualTo(2f).Within(.0001f));
            }
            finally
            {
                UnityEditor.Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MDREG_CharacterTransitionDurationChangesFocusedPreviewTiming()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            object enterEffect = Md3EnumValue("VnWorkshopPreviewEffect", "CharacterEnter");
            object mode = Md3EnumValue("VnWorkshopCharacterTransitionMode", "SlideAndFade");
            object direction = Md3EnumValue("VnWorkshopSlideDirection", "Left");
            object easing = Md3EnumValue("VnWorkshopEasing", "EaseInOut");
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3Invoke(window, "ActivateSceneComposerWorkspace");
                Md3Invoke(window, "ComposerSetPresentationScope", false);

                Md3Invoke(window, "ComposerSetCharacterTransition", mode, .25f, .30f, 64f, direction, easing);
                Md3Invoke(window, "ComposerPreviewFocusedEffect", enterEffect);
                float shortDuration = (float)Md3GetField(Md3Invoke(window, "GetPreviewPlaybackSnapshot"), "Duration");

                Md3Invoke(window, "ComposerSetCharacterTransition", mode, 2f, .30f, 64f, direction, easing);
                Md3Invoke(window, "ComposerPreviewFocusedEffect", enterEffect);
                float longDuration = (float)Md3GetField(Md3Invoke(window, "GetPreviewPlaybackSnapshot"), "Duration");

                Assert.That(shortDuration, Is.EqualTo(.25f).Within(.0001f));
                Assert.That(longDuration, Is.EqualTo(2f).Within(.0001f));
            }
            finally
            {
                UnityEditor.Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MDREG_CharacterAnimationUiExposesSharedTransitionModeHonestly()
        {
            string path = Path.Combine(Application.dataPath,
                "Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposer.cs");
            string source = File.ReadAllText(path);
            string inspector = Md4ExtractMethodBody(source,
                "private void DrawSceneComposerCharacterAnimationInspector(VnSceneComposerScene scene)");

            Assert.That(inspector, Does.Contain("\"Переход персонажа\""),
                "The creator-facing UI must identify one shared character transition profile.");
            Assert.That(MdRegCount(inspector, "EditorGUILayout.Popup(\"Способ\""), Is.EqualTo(1),
                "One canonical transition Mode must be represented by one creator-facing mode control.");
            Assert.That(inspector, Does.Not.Contain("new GUIContent(\"Появление\""));
            Assert.That(inspector, Does.Not.Contain("new GUIContent(\"Исчезновение\""));
            Assert.That(inspector, Does.Contain("\"▶ Проверить появление\""));
            Assert.That(inspector, Does.Contain("\"▶ Проверить исчезновение\""));
        }

        [Test]
        public void MDREG_DurationControlUsesDeterministicNonOverlappingRects()
        {
            string path = Path.Combine(Application.dataPath,
                "Rokas/Scripts/Editor/VnUiWorkshop/VnPresentationWorkshopWindow.SceneComposer.cs");
            string source = File.ReadAllText(path);
            string duration = Md4ExtractMethodBody(source,
                "private static float DrawSceneComposerDurationControl(string label, float value, float minimum)");

            Assert.That(duration, Does.Contain("EditorGUILayout.GetControlRect"),
                "Duration editing should reserve one deterministic row before splitting slider/numeric hit areas.");
            Assert.That(duration, Does.Contain("sliderRect"));
            Assert.That(duration, Does.Contain("fieldRect"));
            Assert.That(duration, Does.Not.Contain("EditorGUILayout.BeginHorizontal"),
                "The duration helper must not rely on a compressed implicit horizontal GUILayout row.");
        }

        private static object MdRegResolveBounce(object window)
        {
            object preset = Md3Invoke(window, "ComposerGetActivePresentationPreset");
            Type resolverType = Md3RequireType("VnPresentationWorkshopVn10Resolver");
            MethodInfo resolve = Md3RequireStatic(resolverType, "ResolveActionBounce", 1);
            return Md3InvokeMethod(null, resolve, preset);
        }

        private static object MdRegSampleBounce(object window, float progress)
        {
            object values = MdRegResolveBounce(window);
            Type resolverType = Md3RequireType("VnPresentationWorkshopVn10Resolver");
            MethodInfo sample = Md3RequireStatic(resolverType, "SampleActionBounce", 3);
            return Md3InvokeMethod(null, sample, true, progress, values);
        }

        private static int MdRegCount(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }
    }
}
