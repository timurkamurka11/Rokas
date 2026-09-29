using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsVn10PhaseJ
    {
        private static readonly BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

        [Test]
        public void SchemaV2AcceptsLegacyV1AndNormalizesDocumentInMemory()
        {
            Assert.That(VnPresentationWorkshopSerialization.SchemaVersion, Is.EqualTo(2));

            string current = VnPresentationWorkshopSerialization.Serialize(new VnPresentationWorkshopPreset(), "Legacy");
            string legacy = Regex.Replace(current, "\\\"schemaVersion\\\"\\s*:\\s*\\d+", "\"schemaVersion\": 1");
            VnWorkshopImportResult imported = VnPresentationWorkshopSerialization.Deserialize(legacy);

            Assert.That(imported.Success, Is.True, imported.Error);
            Assert.That(imported.Document, Is.Not.Null);
            Assert.That(imported.Document.schemaVersion, Is.EqualTo(2), "Legacy schema v1 must normalize to current v2 in memory.");
            FieldInfo sample = typeof(VnPresentationWorkshopDocument).GetField("previewSampleText");
            Assert.That(sample, Is.Not.Null, "Schema v2 must use the explicit previewSampleText field name.");
            Assert.That(sample.GetValue(imported.Document), Is.Not.Null);
            Assert.That(imported.Document.preset, Is.Not.Null);
            Assert.That(imported.Document.preset.typography, Is.Not.Null);
            Assert.That(imported.Document.preset.typewriter, Is.Not.Null);
            Assert.That(imported.Document.preset.stageLayout, Is.Not.Null);
            Assert.That(imported.Document.preset.uiFeedback, Is.Not.Null);
        }

        [Test]
        public void SchemaV2RoundTripPersistsVn10TunablesAndPreviewSampleText()
        {
            var preset = new VnPresentationWorkshopPreset();
            VnPresentationWorkshopVn10Resolver.SetTypographyPreviewOverrides(
                preset, VnWorkshopFontPreset.ProjectSerif, 30f, 1.5f, 3f, 5f,
                VnWorkshopTextAlignment.Center, VnWorkshopFontPreset.ProjectSans, 25f, .5f);
            VnPresentationWorkshopVn10Resolver.SetTypewriterPreviewOverrides(
                preset, true, 31f, .01f, .13f, .29f, .43f, .23f, .24f, .06f);
            VnPresentationWorkshopVn10Resolver.SetExpressionTransitionPreviewOverrides(preset, .29f, VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetCharacterTransitionPreviewOverrides(
                preset, VnWorkshopCharacterTransitionMode.SlideAndFade, .38f, .28f, 76f,
                VnWorkshopSlideDirection.Left, VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetActionBouncePreviewOverrides(
                preset, 16f, .31f, .025f, .12f, VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetBackgroundTransitionPreviewOverrides(
                preset, VnWorkshopBackgroundTransitionMode.Curtain, .54f, .82f,
                VnWorkshopCurtainDirection.RightToLeft, VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetStageLayoutPreviewOverrides(
                preset, -390f, 0f, 390f, 10f, .98f, 1.02f, .98f, 300f, .39f, VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetSpeakerFocusPreviewOverrides(
                preset, 1.06f, 1f, 10f, .93f, .72f, .81f, .25f, VnWorkshopEasing.EaseInOut);
            VnPresentationWorkshopVn10Resolver.SetUiFeedbackPreviewOverrides(
                preset, 1.05f, .95f, new Vector2(0f, -4f), .11f, VnWorkshopEasing.EaseOut,
                1.07f, .89f, 1f, .91f, .17f, .46f);
            VnPresentationWorkshopVn10Resolver.SetTimingPreviewOverrides(preset, .14f, .10f, .45f);

            MethodInfo serialize = typeof(VnPresentationWorkshopSerialization).GetMethod("Serialize", PublicStatic);
            Assert.That(serialize, Is.Not.Null);
            Assert.That(serialize.GetParameters().Length, Is.EqualTo(3),
                "Schema v2 Serialize must accept previewSampleText without introducing an ambiguous overload.");

            const string sample = "Русский preview sample — не канонический диалог.";
            string json = (string)serialize.Invoke(null, new object[] { preset, "VN10 Roundtrip", sample });
            Assert.That(json, Does.Contain("\"schemaVersion\": 2"));
            Assert.That(json, Does.Contain("\"previewSampleText\""));
            Assert.That(json, Does.Not.Contain("previewEffect"));
            Assert.That(json, Does.Not.Contain("previewPlaying"));

            VnWorkshopImportResult imported = VnPresentationWorkshopSerialization.Deserialize(json);
            Assert.That(imported.Success, Is.True, imported.Error);
            Assert.That(imported.Document.schemaVersion, Is.EqualTo(2));
            FieldInfo previewField = typeof(VnPresentationWorkshopDocument).GetField("previewSampleText");
            Assert.That(previewField.GetValue(imported.Document), Is.EqualTo(sample));

            VnPresentationWorkshopPreset restored = imported.Document.preset;
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveTypography(restored).DialogueFontSize, Is.EqualTo(30f).Within(.0001f));
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveTypewriter(restored).CharactersPerSecond, Is.EqualTo(31f).Within(.0001f));
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(restored).Mode, Is.EqualTo(VnWorkshopCharacterTransitionMode.SlideAndFade));
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(restored).Mode, Is.EqualTo(VnWorkshopBackgroundTransitionMode.Curtain));
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveStageLayout(restored).LeftX, Is.EqualTo(-390f).Within(.0001f));
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(restored).InactiveAlpha, Is.EqualTo(.81f).Within(.0001f));
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveUiFeedback(restored).PressedScale, Is.EqualTo(.95f).Within(.0001f));
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveTiming(restored).PostTransitionBreathingRoom, Is.EqualTo(.10f).Within(.0001f));
        }

        [Test]
        public void ReferenceMotionPreviewIsNonFinalOverrideProfileAndNeverOriginal()
        {
            Type profiles = Type.GetType("Rokas.EditorTools.VnUiWorkshop.VnPresentationWorkshopVn10Profiles, Rokas.Editor");
            Assert.That(profiles, Is.Not.Null);
            MethodInfo apply = profiles.GetMethod("ApplyReferenceMotionPreview", PublicStatic);
            Assert.That(apply, Is.Not.Null);

            var original = new VnPresentationWorkshopPreset();
            var reference = new VnPresentationWorkshopPreset();
            Assert.That(original.HasAnyOverride, Is.False);
            apply.Invoke(null, new object[] { reference });

            Assert.That(reference.HasAnyOverride, Is.True, "Reference Motion Preview must be an override-only starting profile, not Original.");
            Assert.That(original.HasAnyOverride, Is.False, "Applying the reference profile must not mutate a separate Original baseline.");
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(reference).Mode,
                Is.EqualTo(VnWorkshopCharacterTransitionMode.SlideAndFade));
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(reference).Mode,
                Is.EqualTo(VnWorkshopBackgroundTransitionMode.Curtain));
            Assert.That(VnPresentationWorkshopVn10Resolver.ResolveTypewriter(reference).CharactersPerSecond,
                Is.LessThan(36f));
        }

        [Test]
        public void WindowExportAndVariantRoundTripPreservePreviewSampleButNeverPlaybackState()
        {
            var window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            var restored = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            string variant = "VN10_PhaseJ_" + Guid.NewGuid().ToString("N");
            const string sample = "Тест варианта: previewSampleText UTF-8.";
            try
            {
                window.SetPreviewSampleText(sample);
                window.PreviewBounce();
                string json = window.ExportCurrentPresetJson("Portable VN10");
                Assert.That(VnPresentationWorkshopWindow.ExportFileName, Is.EqualTo("ROKAS_VN_WORKSHOP_PRESET.json"));
                Assert.That(json, Does.Contain("\"previewSampleText\""));
                Assert.That(json, Does.Contain(sample));
                Assert.That(json, Does.Not.Contain("previewEffect"));
                Assert.That(json, Does.Not.Contain("previewPlaying"));
                Assert.That(json, Does.Not.Contain(Application.dataPath));

                window.SaveCurrentVariant(variant);
                VnWorkshopImportResult loaded = restored.LoadSavedVariant(variant);
                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(restored.PreviewSampleText, Is.EqualTo(sample));
            }
            finally
            {
                try { restored.DeleteSavedVariant(variant); } catch { }
                UnityEngine.Object.DestroyImmediate(restored);
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
    }
}
