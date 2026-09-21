using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerMeTests
    {
        [Test]
        public void ME_AdditionalGroupsAdvancedCapabilitiesBehindCollapsedCreatorFacingRoutes()
        {
            string scene = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string assets = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAssets.cs");
            string additional = ExtractMethodBody(scene, "private void DrawSceneComposerAdditionalInspector(VnSceneComposerScene scene)");

            Assert.That(additional, Does.Contain("\"Расширенная анимация\""),
                "Rare exact timing must move behind a deliberate Advanced animation group.");
            Assert.That(additional, Does.Contain("\"Расширенное оформление\""),
                "Advanced presentation controls must sit behind a deliberate creator-facing group instead of opening as an immediate wall.");
            Assert.That(additional, Does.Contain("\"Технические параметры\""),
                "Project IDs/paths and other technical metadata must sit behind a deliberate technical group.");
            Assert.That(scene, Does.Contain("_sceneComposerAdvancedAnimationGroupExpanded;")
                .And.Contain("_sceneComposerAdvancedPresentationGroupExpanded;")
                .And.Contain("_sceneComposerAdvancedTechnicalGroupExpanded;"),
                "Advanced top-level groups must have durable editor state.");
            Assert.That(scene, Does.Not.Contain("_sceneComposerAdvancedAnimationGroupExpanded = true")
                .And.Not.Contain("_sceneComposerAdvancedPresentationGroupExpanded = true")
                .And.Not.Contain("_sceneComposerAdvancedTechnicalGroupExpanded = true"),
                "Advanced top-level groups must be collapsed by default.");
            Assert.That(assets, Does.Not.Contain("_sceneComposerAssetLibraryExpanded = true"),
                "Asset Library must not open as a large default-expanded power-user panel.");
        }

        [Test]
        public void ME_BasicSceneAnimationDoesNotExposeLowLevelSequenceGap()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string animation = ExtractMethodBody(source,
                "private void DrawSceneComposerSceneAnimationInspector(VnSceneComposerScene scene)");
            string advancedTiming = ExtractMethodBody(source,
                "private void DrawSceneComposerAdvancedTimingControls(VnSceneComposerScene scene)");

            Assert.That(animation, Does.Not.Contain("\"Пауза между сценами, с\""),
                "Low-level sequence-gap tuning belongs in Advanced, not the normal Scene Animation flow.");
            Assert.That(animation, Does.Not.Contain("ComposerSetTiming("),
                "Basic Scene Animation should edit creator-facing scene progression without exposing the exact presentation timing tree.");
            Assert.That(advancedTiming, Does.Contain("\"Пауза между сценами, с\"")
                .And.Contain("ComposerSetTiming("),
                "The exact sequence-gap capability must remain reachable in Advanced instead of being deleted.");
        }

        [Test]
        public void ME_AdvancedDebugAndImportToolsAreCollapsedInsteadOfDominatingPresentation()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string presentation = ExtractMethodBody(source,
                "private void DrawSceneComposerPresentationControls(VnSceneComposerScene scene)");

            Assert.That(presentation, Does.Contain("\"Диагностика / отладка\""),
                "Original/current comparison and reference-motion tools must be demoted into a diagnostics group.");
            Assert.That(presentation, Does.Contain("\"Шаблоны и импорт / экспорт\""),
                "Preset lifecycle and raw JSON tools must be grouped as power-user import/export functionality.");
            Assert.That(source, Does.Contain("_sceneComposerDiagnosticsExpanded;")
                .And.Contain("_sceneComposerPresetToolsExpanded;"));
            Assert.That(source, Does.Not.Contain("_sceneComposerDiagnosticsExpanded = true")
                .And.Not.Contain("_sceneComposerPresetToolsExpanded = true"),
                "Diagnostics and import/export groups must be collapsed by default.");
            Assert.That(source, Does.Not.Contain("_sceneComposerPresentationLayoutExpanded = true"),
                "Exact interface geometry must not start expanded.");
        }

        [Test]
        public void ME_CollapsedAdvancedPresentationDoesNotEnableHiddenPreviewDebugOverlays()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string layoutGate = ExtractMethodBody(source,
                "private bool IsSceneComposerAdvancedLayoutEditingVisible()");
            string feedbackGate = ExtractMethodBody(source,
                "private bool IsSceneComposerAdvancedUiFeedbackPreviewVisible()");

            Assert.That(layoutGate, Does.Contain("_sceneComposerAdvancedPresentationGroupExpanded")
                .And.Contain("_sceneComposerPresentationLayoutExpanded"),
                "Hidden layout/hit-region overlays must require both the parent Advanced group and the layout group.");
            Assert.That(feedbackGate, Does.Contain("_sceneComposerAdvancedPresentationGroupExpanded")
                .And.Contain("_sceneComposerUiFeedbackExpanded"),
                "Hidden UI-feedback simulation must require the parent Advanced group and its detailed effects group.");
        }

        [Test]
        public void ME_SceneSettingsRemainConcise()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string settings = ExtractMethodBody(source,
                "private void DrawSceneComposerSceneSettings(VnSceneComposerScene scene)");

            Assert.That(settings, Does.Contain("\"Название сцены\""));
            Assert.That(settings, Does.Not.Contain("MinimumBeatSettleDuration")
                .And.Not.Contain("PostTransitionBreathingRoom")
                .And.Not.Contain("AutoPreviewSequenceGap")
                .And.Not.Contain("Library/ROKAS")
                .And.Not.Contain("\"ID проекта\""),
                "Ordinary Scene Settings must remain creator-facing and concise.");
        }

        [Test]
        public void ME_DialogueBeatEditorRemainsCanonicalTextSurface()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string text = ExtractMethodBody(
                source,
                "private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)");
            string dialogue = ExtractMethodBody(
                source,
                "private void DrawSceneComposerDialogueAuthoringSection(VnSceneComposerScene scene)");

            Assert.That(text, Does.Contain("\"Реплика\"")
                .And.Contain("DrawSceneComposerDialogueAuthoringSection(scene)"),
                "Normal Text authoring must expose the canonical dialogue workflow through the Реплика foldout.");
            Assert.That(dialogue, Does.Contain("\"Реплики\"")
                .And.Contain("\"+ Реплика\"")
                .And.Contain("\"Дублировать\"")
                .And.Contain("\"Удалить\"")
                .And.Contain("\"Говорящий\"")
                .And.Contain("\"Текст без персонажа\"")
                .And.Contain("\"Текст реплики\""),
                "The Реплика section must remain centered on real ordered dialogue Beats.");
            Assert.That(text + dialogue, Does.Not.Contain("Тест оформления текста"),
                "Sample typography text must never compete with real dialogue Beats in the normal Text section.");
        }

        [Test]
        public void ME_AdvancedScopeUsesCreatorFriendlyTerminology()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string presentation = ExtractMethodBody(source,
                "private void DrawSceneComposerPresentationControls(VnSceneComposerScene scene)");

            Assert.That(presentation, Does.Contain("\"Применить:\"")
                .And.Contain("\"Только к этой сцене\"")
                .And.Contain("\"Ко всем сценам\""));
            Assert.That(presentation, Does.Not.Contain("Project Defaults")
                .And.Not.Contain("Scene Overrides"),
                "Creator-facing scope language must not expose implementation terminology.");
        }

        [Test]
        public void ME_ExistingAdvancedFunctionalityRemainsReachable()
        {
            string authoring = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string presentation = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerPresentation.cs");
            string scene = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");

            Assert.That(authoring, Does.Contain("DrawSceneComposerSerializedPresentationSections")
                .And.Contain("DrawSceneComposerPresetControls")
                .And.Contain("DrawSceneComposerPreviewTextControls"),
                "Progressive disclosure must move advanced parity, not delete it.");
            Assert.That(presentation, Does.Contain("ComposerExportPresentationPresetJson")
                .And.Contain("ComposerImportPresentationPresetJson")
                .And.Contain("ComposerSetTiming")
                .And.Contain("ComposerApplyReferenceMotionProfile"));
            Assert.That(scene, Does.Contain("DrawSceneComposerProjectTechnicalInfo")
                .And.Contain("DrawSceneComposerAssetLibraryControls"));
        }

        [Test]
        public void ME_PlaybackPrimaryActionsRemainProminentAndControllerSemanticsStayUntouched()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string preview = ExtractMethodBody(source, "private void DrawSceneComposerPreview()");

            Assert.That(preview, Does.Contain("\"Проиграть сцену\"")
                .And.Contain("\"Проиграть всё\"")
                .And.Contain("SceneComposerPrimaryTransportButtonStyle"));
            Assert.That(preview, Does.Contain("\"Предыдущая\"")
                .And.Contain("\"Следующая\"")
                .And.Contain("\"Пауза\"")
                .And.Contain("\"Сначала\"")
                .And.Contain("\"Проиграть отсюда\""));
        }

        private static string ReadEditorSource(string fileName)
        {
            string path = Path.Combine(
                Application.dataPath,
                "Rokas",
                "Scripts",
                "Editor",
                "VnUiWorkshop",
                fileName);
            Assert.That(File.Exists(path), Is.True, "Missing inspected editor source: " + path);
            return File.ReadAllText(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), "Missing method signature: " + signature);
            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.That(openBrace, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(openBrace, i - openBrace + 1);
                }
            }
            Assert.Fail("Unterminated method body: " + signature);
            return string.Empty;
        }
    }
}
