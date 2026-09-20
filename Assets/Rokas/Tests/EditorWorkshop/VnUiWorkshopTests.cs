using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using Rokas.Presentation;
using UnityEditor;
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

        [Test]
        public void RealAssetMirrorRendererRequiresAuthoredRokasAssetsAndMinaBodyUv()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null, "The Workshop must mirror the real Resources/RokasAssets asset.");
            Assert.That(assets.vnBusStopRainNight, Is.Not.Null);
            Assert.That(assets.vnNightSkyRain, Is.Not.Null);
            Assert.That(assets.vnBusStopPhoneMessageMina, Is.Not.Null);
            Assert.That(assets.vnDialoguePanelKeikoDark, Is.Not.Null);
            Assert.That(assets.vnDialoguePanelMinaLight, Is.Not.Null);
            Assert.That(assets.vnMinaCharacterSheet, Is.Not.Null);
            Assert.That(assets.sans, Is.Not.Null);

            VnCharacterVisualState mina = VnCharacterVisualCatalog.ResolveOrNeutral("mina_neutral", "Mina");
            Assert.That(mina.Character, Is.EqualTo("Mina"));
            Assert.That(mina.BodyUv.x, Is.EqualTo(.000921f).Within(.000001f));
            Assert.That(mina.BodyUv.y, Is.EqualTo(.008287f).Within(.000001f));
            Assert.That(mina.BodyUv.width, Is.EqualTo(.361878f).Within(.000001f));
            Assert.That(mina.BodyUv.height, Is.EqualTo(.986878f).Within(.000001f));

            Type renderer = RequireType("VnPresentationWorkshopPreviewRenderer");
            Assert.That(renderer.GetMethod("LoadAssets", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(renderer.GetMethod("BuildFrame", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
        }

        [Test]
        public void RealAssetMirrorRendererExposesPureResolvedFrameWithoutProductionViewMutationApi()
        {
            Type renderer = RequireType("VnPresentationWorkshopPreviewRenderer");
            Type frame = RequireType("VnWorkshopPreviewFrame");
            Type scene = RequireType("VnWorkshopPreviewScene");

            Assert.That(renderer.GetMethod("BuildFrame", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
            Assert.That(frame.GetProperty("VirtualCanvasSize", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(frame.GetProperty("DialoguePanel", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(frame.GetProperty("MinaBody", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(frame.GetProperty("SpeakerName", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(frame.GetProperty("DialogueText", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(frame.GetProperty("MuteHitRegion", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(frame.GetProperty("PauseHitRegion", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(frame.GetProperty("SkipHitRegion", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(frame.GetProperty("Back", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(frame.GetProperty("Next", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);

            Assert.That(renderer.GetMethod("CreateProductionView", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance), Is.Null,
                "The isolated mirror renderer must not expose an API that creates or mutates VnIntroView.");
            Assert.That(scene.IsEnum, Is.True);
        }

        [Test]
        public void EditorWindowContractUsesApprovedMenuAndHasNoProductionApplyAction()
        {
            Type window = RequireType("VnPresentationWorkshopWindow");
            Assert.That(typeof(EditorWindow).IsAssignableFrom(window), Is.True,
                "The Workshop entry point must be an editor-only EditorWindow.");

            FieldInfo menuPath = window.GetField("MenuPath", BindingFlags.Public | BindingFlags.Static);
            Assert.That(menuPath, Is.Not.Null, "The window must expose the approved menu path as an immutable constant.");
            Assert.That(menuPath.GetRawConstantValue(), Is.EqualTo("ROKAS/Редактор новеллы"));

            MethodInfo open = window.GetMethod("Open", BindingFlags.Public | BindingFlags.Static);
            Assert.That(open, Is.Not.Null, "The Workshop must expose a static menu entry point.");
            MenuItem menuItem = open.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>().SingleOrDefault();
            Assert.That(menuItem, Is.Not.Null, "The Workshop Open method must be registered with Unity's MenuItem attribute.");
            Assert.That(menuItem.menuItem, Is.EqualTo("ROKAS/Редактор новеллы"));

            bool hasProductionApply = window
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Any(method => method.Name.IndexOf("ApplyToProduction", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(hasProductionApply, Is.False,
                "The Workshop must never expose an ApplyToProduction action.");
        }

        [Test]
        public void EditorWindowPreviewComparisonKeepsOriginalImmutable()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            EditorWindow window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);

            try
            {
                PropertyInfo presetProperty = windowType.GetProperty("CurrentPreset", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo sceneProperty = windowType.GetProperty("PreviewScene", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo resolutionProperty = windowType.GetProperty("PreviewResolution", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo comparisonProperty = windowType.GetProperty("ComparisonView", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo buildPreview = windowType.GetMethod("BuildPreviewFrame", BindingFlags.Public | BindingFlags.Instance);

                Assert.That(presetProperty, Is.Not.Null, "The window must own the current override-only preset.");
                Assert.That(sceneProperty, Is.Not.Null, "The window must expose the preview-state selector state.");
                Assert.That(resolutionProperty, Is.Not.Null, "The window must expose the preview-resolution selector state.");
                Assert.That(comparisonProperty, Is.Not.Null, "The window must expose immutable Original/Current comparison state.");
                Assert.That(buildPreview, Is.Not.Null, "The window must build its preview through the approved mirror renderer.");

                var preset = (VnPresentationWorkshopPreset)presetProperty.GetValue(window);
                preset.minaBody.hasPositionDelta = true;
                preset.minaBody.positionDelta = new Vector2(37f, -12f);

                sceneProperty.SetValue(window, VnWorkshopPreviewScene.MinaBody);
                resolutionProperty.SetValue(window, VnWorkshopResolution.Wide1280x720);

                object currentMode = Enum.Parse(comparisonProperty.PropertyType, "Current");
                object originalMode = Enum.Parse(comparisonProperty.PropertyType, "Original");

                comparisonProperty.SetValue(window, currentMode);
                var current = (VnWorkshopPreviewFrame)buildPreview.Invoke(window, null);

                comparisonProperty.SetValue(window, originalMode);
                var original = (VnWorkshopPreviewFrame)buildPreview.Invoke(window, null);
                VnWorkshopPreviewFrame baseline = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                    new VnPresentationWorkshopPreset(), VnWorkshopResolution.Wide1280x720, VnWorkshopPreviewScene.MinaBody);

                Assert.That(original.MinaBody.center.x, Is.EqualTo(baseline.MinaBody.center.x).Within(.01f));
                Assert.That(original.MinaBody.center.y, Is.EqualTo(baseline.MinaBody.center.y).Within(.01f));
                Assert.That(current.MinaBody.center.x, Is.EqualTo(baseline.MinaBody.center.x + 37f).Within(.01f));
                Assert.That(current.MinaBody.center.y, Is.EqualTo(baseline.MinaBody.center.y - 12f).Within(.01f));
                Assert.That(preset.minaBody.hasPositionDelta, Is.True,
                    "Viewing Original must never overwrite or reset the Current Workshop preset.");
                Assert.That(preset.minaBody.positionDelta, Is.EqualTo(new Vector2(37f, -12f)));
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void EditorWindowDirectManipulationAndInspectorEditOnlyCurrentOverrides()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            EditorWindow window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);

            try
            {
                PropertyInfo selectedElement = windowType.GetProperty("SelectedElement", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo selectElementAt = windowType.GetMethod("SelectElementAt", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo dragSelected = windowType.GetMethod("DragSelectedElement", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo nudgeSelected = windowType.GetMethod("NudgeSelectedElement", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo setPosition = windowType.GetMethod("SetSelectedPosition", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo setSize = windowType.GetMethod("SetSelectedSize", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo setScale = windowType.GetMethod("SetSelectedScale", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo resetSelected = windowType.GetMethod("ResetSelectedElement", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo resetAll = windowType.GetMethod("ResetAll", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo setFocus = windowType.GetMethod("SetFocusValues", BindingFlags.Public | BindingFlags.Instance);

                Assert.That(selectedElement, Is.Not.Null, "The real inspector must expose the selected editable element.");
                Assert.That(selectElementAt, Is.Not.Null, "Preview selection must use the mirror renderer hit-test path.");
                Assert.That(dragSelected, Is.Not.Null, "Selected elements must support mouse-drag deltas.");
                Assert.That(nudgeSelected, Is.Not.Null, "Selected elements must support arrow-key nudging.");
                Assert.That(setPosition, Is.Not.Null, "The inspector must support exact Position X/Y editing.");
                Assert.That(setSize, Is.Not.Null, "The inspector must support exact Width/Height editing where the model supports it.");
                Assert.That(setScale, Is.Not.Null, "The inspector must support exact Scale editing where the model supports it.");
                Assert.That(resetSelected, Is.Not.Null, "The inspector must support Reset Element.");
                Assert.That(resetAll, Is.Not.Null, "The Workshop must support Reset All.");
                Assert.That(setFocus, Is.Not.Null, "The approved focus override fields must be editable without a second model.");

                var preset = (VnPresentationWorkshopPreset)windowType.GetProperty("CurrentPreset").GetValue(window);
                windowType.GetProperty("ComparisonView").SetValue(window,
                    Enum.Parse(windowType.GetProperty("ComparisonView").PropertyType, "Current"));
                windowType.GetProperty("PreviewScene").SetValue(window, VnWorkshopPreviewScene.MinaBody);

                VnWorkshopPreviewFrame frame = (VnWorkshopPreviewFrame)windowType.GetMethod("BuildPreviewFrame").Invoke(window, null);
                bool selected = (bool)selectElementAt.Invoke(window, new object[] { frame.MinaBody.center });
                Assert.That(selected, Is.True);
                Assert.That((VnWorkshopElement)selectedElement.GetValue(window), Is.EqualTo(VnWorkshopElement.MinaBody));

                dragSelected.Invoke(window, new object[] { new Vector2(20f, -10f) });
                nudgeSelected.Invoke(window, new object[] { Vector2.right, false });
                nudgeSelected.Invoke(window, new object[] { Vector2.up, true });
                setScale.Invoke(window, new object[] { 1.2f });
                Assert.That(preset.minaBody.positionDelta, Is.EqualTo(new Vector2(21f, 0f)));
                Assert.That(preset.minaBody.hasPositionDelta, Is.True);
                Assert.That(preset.minaBody.scaleMultiplier, Is.EqualTo(1.2f));
                Assert.That(preset.minaBody.hasScaleMultiplier, Is.True);

                selectedElement.SetValue(window, VnWorkshopElement.DialogueText);
                setPosition.Invoke(window, new object[] { new Vector2(7f, 8f) });
                setSize.Invoke(window, new object[] { new Vector2(30f, 40f) });
                Assert.That(preset.dialogueText.positionDelta, Is.EqualTo(new Vector2(7f, 8f)));
                Assert.That(preset.dialogueText.sizeDelta, Is.EqualTo(new Vector2(30f, 40f)));
                resetSelected.Invoke(window, null);
                Assert.That(preset.dialogueText.HasAnyOverride, Is.False);
                Assert.That(preset.minaBody.HasAnyOverride, Is.True,
                    "Reset Element must not clear unrelated CurrentPreset overrides.");

                setFocus.Invoke(window, new object[] { 325f, 1.08f, .91f, .72f, .80f });
                Assert.That(preset.focus.hasTwoCharacterOffset, Is.True);
                Assert.That(preset.focus.twoCharacterOffset, Is.EqualTo(325f));
                Assert.That(preset.focus.hasActiveScale, Is.True);
                Assert.That(preset.focus.activeScale, Is.EqualTo(1.08f));
                Assert.That(preset.focus.hasInactiveScale, Is.True);
                Assert.That(preset.focus.inactiveScale, Is.EqualTo(.91f));
                Assert.That(preset.focus.hasInactiveBrightness, Is.True);
                Assert.That(preset.focus.inactiveBrightness, Is.EqualTo(.72f));
                Assert.That(preset.focus.hasInactiveAlpha, Is.True);
                Assert.That(preset.focus.inactiveAlpha, Is.EqualTo(.80f));

                resetAll.Invoke(window, null);
                Assert.That(preset.HasAnyOverride, Is.False,
                    "Reset All must clear CurrentPreset overrides without copying or mutating the Original baseline.");
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static Type RequireType(string shortName)
        {
            Type type = Type.GetType(Namespace + shortName + ", " + EditorAssembly);
            Assert.That(type, Is.Not.Null, shortName + " is required by the approved Workshop design.");
            return type;
        }
    }
}
