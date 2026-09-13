using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string ExpectedHead = "04a53a955286bb926365459ee11c6836ae490282";

        [Test]
        public void WorkshopEditorApiExistsAndPinsVerifiedSourceHead()
        {
            Type baseline = RequireType("VnPresentationWorkshopBaseline");
            FieldInfo sourceHead = baseline.GetField("SourceHead", BindingFlags.Public | BindingFlags.Static);
            Assert.That(sourceHead, Is.Not.Null, "Baseline must expose immutable SourceHead.");
            Assert.That(sourceHead.GetRawConstantValue(), Is.EqualTo(ExpectedHead));

            PropertyInfo referenceResolution = baseline.GetProperty("ReferenceResolution", BindingFlags.Public | BindingFlags.Static);
            Assert.That(referenceResolution, Is.Not.Null);
            Assert.That((Vector2)referenceResolution.GetValue(null), Is.EqualTo(new Vector2(1920f, 1080f)));

            RequireType("VnPresentationWorkshopPreset");
            RequireType("VnPresentationWorkshopResolver");
            RequireType("VnPresentationWorkshopStorage");
        }

        [Test]
        public void ResolverExposesProductionCanvasScalerMapping()
        {
            Type resolver = RequireType("VnPresentationWorkshopResolver");
            MethodInfo method = resolver.GetMethod("CalculateVirtualCanvasSize", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            Vector2 wide = (Vector2)method.Invoke(null, new object[] { 1280, 720 });
            Assert.That(wide.x, Is.EqualTo(1920f).Within(.01f));
            Assert.That(wide.y, Is.EqualTo(1080f).Within(.01f));

            Vector2 fourThree = (Vector2)method.Invoke(null, new object[] { 1024, 768 });
            float logWidth = Mathf.Log(1024f / 1920f, 2f);
            float logHeight = Mathf.Log(768f / 1080f, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, .5f));
            Assert.That(fourThree.x, Is.EqualTo(1024f / scale).Within(.01f));
            Assert.That(fourThree.y, Is.EqualTo(768f / scale).Within(.01f));
        }

        [Test]
        public void PresetStoresOnlyEnabledOverridesAndResetNeverCopiesBaseline()
        {
            var preset = new VnPresentationWorkshopPreset();
            Assert.That(preset.HasAnyOverride, Is.False);

            preset.minaBody.hasPositionDelta = true;
            preset.minaBody.positionDelta = new Vector2(23f, -11f);
            preset.dialogueText.hasSizeDelta = true;
            preset.dialogueText.sizeDelta = new Vector2(40f, 12f);
            preset.focus.hasActiveScale = true;
            preset.focus.activeScale = 1.12f;
            Assert.That(preset.HasAnyOverride, Is.True);

            preset.ResetElement(VnWorkshopElement.MinaBody);
            Assert.That(preset.minaBody.HasAnyOverride, Is.False);
            Assert.That(preset.minaBody.positionDelta, Is.EqualTo(Vector2.zero));
            Assert.That(preset.dialogueText.HasAnyOverride, Is.True);
            Assert.That(preset.focus.HasAnyOverride, Is.True);

            preset.ResetAll();
            Assert.That(preset.HasAnyOverride, Is.False);
            Assert.That(preset.focus.activeScale, Is.EqualTo(0f),
                "Reset must clear the override rather than copying the production baseline into the preset.");
        }

        [Test]
        public void FocusResolverUsesBaselineUntilAnOverrideIsExplicitlyEnabled()
        {
            var preset = new VnPresentationWorkshopPreset();
            VnWorkshopFocusValues baseline = VnPresentationWorkshopResolver.ResolveFocus(preset);
            Assert.That(baseline.TwoCharacterOffset, Is.EqualTo(310f));
            Assert.That(baseline.ActiveScale, Is.EqualTo(1.05f));
            Assert.That(baseline.InactiveScale, Is.EqualTo(.94f));
            Assert.That(baseline.InactiveBrightness, Is.EqualTo(.76f));
            Assert.That(baseline.InactiveAlpha, Is.EqualTo(.84f));

            preset.focus.hasTwoCharacterOffset = true;
            preset.focus.twoCharacterOffset = 345f;
            preset.focus.hasInactiveAlpha = true;
            preset.focus.inactiveAlpha = .65f;
            VnWorkshopFocusValues changed = VnPresentationWorkshopResolver.ResolveFocus(preset);
            Assert.That(changed.TwoCharacterOffset, Is.EqualTo(345f));
            Assert.That(changed.InactiveAlpha, Is.EqualTo(.65f));
            Assert.That(changed.ActiveScale, Is.EqualTo(1.05f),
                "Unspecified focus values must keep resolving from the immutable baseline.");
        }

        [Test]
        public void DirectEditingApiSupportsDragNudgeAndElementReset()
        {
            Type editing = RequireType("VnPresentationWorkshopEditing");
            Assert.That(editing.GetMethod("ApplyDrag", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(editing.GetMethod("Nudge", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(editing.GetMethod("SetPositionDelta", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(editing.GetMethod("SetSizeDelta", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(editing.GetMethod("SetScaleMultiplier", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
        }

        [Test]
        public void PortableSerializationApiRequiresValidationAndSourceMismatchReporting()
        {
            Type serialization = RequireType("VnPresentationWorkshopSerialization");
            Assert.That(serialization.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(serialization.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(serialization.GetMethod("ValidatePreset", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            RequireType("VnPresentationWorkshopDocument");
            RequireType("VnWorkshopImportResult");
        }

        [Test]
        public void BakedControlMetadataIsExplicitAndIndependentControlsStayIndependent()
        {
            Type baseline = RequireType("VnPresentationWorkshopBaseline");
            MethodInfo method = baseline.GetMethod("GetControlPresentationKind", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(null, new object[] { "Mute" }), Is.EqualTo("Baked into panel"));
            Assert.That(method.Invoke(null, new object[] { "Pause" }), Is.EqualTo("Baked into panel"));
            Assert.That(method.Invoke(null, new object[] { "Skip" }), Is.EqualTo("Baked into panel"));
            Assert.That(method.Invoke(null, new object[] { "Back" }), Is.EqualTo("Independent"));
            Assert.That(method.Invoke(null, new object[] { "Next" }), Is.EqualTo("Independent"));
        }

        [Test]
        public void VariantsDirectoryIsProjectLocalLibraryPathAndStorageExposesSafeCrudContract()
        {
            Type storage = RequireType("VnPresentationWorkshopStorage");
            MethodInfo directoryMethod = storage.GetMethod("GetVariantsDirectory", BindingFlags.Public | BindingFlags.Static);
            Assert.That(directoryMethod, Is.Not.Null);
            string path = (string)directoryMethod.Invoke(null, new object[] { "/project/root" });
            string normalized = path.Replace('\\', '/');
            Assert.That(normalized, Does.EndWith("/project/root/Library/ROKAS/VnUiWorkshop/variants"));
            Assert.That(normalized, Does.Not.Contain("/Assets/"));

            Assert.That(storage.GetMethod("SanitizeVariantName", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(storage.GetMethod("GetVariantPath", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(storage.GetMethod("SaveVariant", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(storage.GetMethod("LoadVariant", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(storage.GetMethod("DeleteVariant", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(storage.GetMethod("RenameVariant", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(storage.GetMethod("DuplicateVariant", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
        }

        private static Type RequireType(string shortName)
        {
            Type type = Type.GetType(Namespace + shortName + ", " + EditorAssembly);
            Assert.That(type, Is.Not.Null, shortName + " is required by the approved Workshop design.");
            return type;
        }
    }
}
