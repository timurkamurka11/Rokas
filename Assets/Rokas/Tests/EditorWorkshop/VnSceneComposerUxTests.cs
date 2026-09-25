using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerUxTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void UxA_NormalEditorUsesRussianVisualNovelIdentity()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.cs");

            Assert.That(source, Does.Contain("ROKAS/Редактор новеллы"),
                "The normal menu entry must present one Russian visual-novel editor instead of the old VN UI Workshop identity.");
            Assert.That(source, Does.Contain("ROKAS — Редактор новеллы"),
                "The editor window title must identify the canonical Russian VN authoring workspace.");
        }

        [Test]
        public void UxA_NormalOnGuiDoesNotExposePresentationWorkshopAsPeerEditor()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.cs");
            string onGui = ExtractMethodBody(source, "private void OnGUI()");

            Assert.That(onGui, Does.Contain("DrawSceneComposerWorkspace();"),
                "Scene Composer must remain the normal authoring surface.");
            Assert.That(onGui, Does.Not.Contain("DrawWorkspaceModeToolbar();"),
                "Ordinary authoring must not show Presentation Workshop and Scene Composer as two peer editors.");
        }

        [Test]
        public void UxA_LegacyWorkshopCompatibilityRemainsAvailableInternally()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");

            Assert.That(windowType.GetField("currentPreset",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null,
                "Legacy preset state may remain for compatibility even though it is hidden from ordinary authoring.");
            Assert.That(windowType.GetMethod("BuildPreviewFrame",
                BindingFlags.Instance | BindingFlags.Public), Is.Not.Null,
                "The proven Workshop preview path must remain available to compatibility tests.");
            Assert.That(windowType.GetMethod("ActivateSceneComposerWorkspace",
                BindingFlags.Instance | BindingFlags.Public), Is.Not.Null,
                "Existing Scene Composer compatibility entry points must remain intact.");
        }

        [Test]
        public void UxB_CoreWorkspaceUsesRussianSceneListAndPreview()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string storyboard = ExtractMethodBody(source, "private void DrawSceneComposerStoryboard()");
            string preview = ExtractMethodBody(source, "private void DrawSceneComposerPreview()");

            Assert.That(storyboard, Does.Contain("\"Сцены\""),
                "The left authoring column must be presented as Сцены.");
            Assert.That(storyboard, Does.Contain("\"+ Добавить сцену\""),
                "The primary scene action must be the Russian Добавить сцену action.");
            Assert.That(storyboard, Does.Not.Contain("\"Storyboard\""),
                "The ordinary scene list must not retain the engineering Storyboard heading.");
            Assert.That(preview, Does.Contain("\"Предпросмотр сцены\""),
                "The dominant center column must be presented as Предпросмотр сцены.");
            Assert.That(preview, Does.Not.Contain("\"Scene Preview\""),
                "The ordinary center column must not retain the English Scene Preview heading.");
        }

        [Test]
        public void UxB_RightInspectorExposesRussianCoreAuthoringSections()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs") + "\n" +
                ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs") + "\n" +
                ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAssets.cs");

            string[] requiredSections =
            {
                "Фон",
                "Персонажи",
                "Текст",
                "Анимация сцены",
                "Анимация персонажа",
                "Медиа",
                "Настройки сцены",
                "Дополнительно"
            };
            foreach (string section in requiredSections)
            {
                Assert.That(source, Does.Contain("\"" + section + "\""),
                    "The ordinary inspector must expose the Russian core section: " + section + ".");
            }
        }

        [Test]
        public void UxB_BasicPlaybackUsesRussianTransportLabels()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string preview = ExtractMethodBody(source, "private void DrawSceneComposerPreview()");

            string[] requiredLabels =
            {
                "Предыдущая",
                "Проиграть сцену",
                "Проиграть всё",
                "Пауза",
                "Сначала",
                "Следующая"
            };
            foreach (string label in requiredLabels)
            {
                Assert.That(preview, Does.Contain("\"" + label + "\""),
                    "Basic playback must expose the Russian transport label: " + label + ".");
            }

            Assert.That(preview, Does.Not.Contain("\"Play Scene\""));
            Assert.That(preview, Does.Not.Contain("\"Play All\""));
            Assert.That(preview, Does.Not.Contain("\"Restart\""));
        }

        [Test]
        public void SceneAnimationHidesAutomaticRearrangementButKeepsSpeakerFocus()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string animation = ExtractMethodBody(source,
                "private void DrawSceneComposerSceneAnimationInspector(VnSceneComposerScene scene)");

            Assert.That(animation, Does.Not.Contain("Расположение персонажей"));
            Assert.That(animation, Does.Not.Contain("Длительность перестановки"));
            Assert.That(animation, Does.Not.Contain("ComposerSetStageLayout"));
            Assert.That(animation, Does.Contain("Фокус говорящего"));
            Assert.That(animation, Does.Contain("Яркость остальных"));
            Assert.That(animation, Does.Contain("Длительность фокуса"));
            Assert.That(animation, Does.Contain("ComposerSetSpeakerFocus"));

            string text = ExtractMethodBody(source,
                "private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)");
            Assert.That(text, Does.Contain("DrawSceneComposerBeatCharacterStagingInspector"),
                "Static Beat staging remains available in Text authoring.");
        }

        [Test]
        public void StoryboardExposesCompactTerminalSceneMarkerAndAction()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string storyboard = ExtractMethodBody(
                source, "private void DrawSceneComposerStoryboard()");
            Assert.That(source, Does.Contain("[ФИНАЛ]"));
            Assert.That(storyboard, Does.Contain("Конечный кадр"));
            Assert.That(source, Does.Contain("terminalFadeDuration"));
        }

        [Test]
        public void MovementInspectorUsesSelectedReplicaAndCompactRussianControls()
        {
            string main = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string movement = ReadEditorSource(
                "VnPresentationWorkshopWindow.SceneComposerCharacterStaging.cs");

            Assert.That(main, Does.Contain("\"Движение\""));
            Assert.That(movement, Does.Contain("ДВИЖЕНИЕ ТЕКУЩЕЙ РЕПЛИКИ"));
            Assert.That(movement, Does.Contain("Основное действие"));
            Assert.That(movement, Does.Contain("\"Персонаж\""));
            Assert.That(movement, Does.Contain("\"Действие\""));
            Assert.That(movement, Does.Contain("\"Длительность\""));
            Assert.That(movement, Does.Contain("После основного"));
            Assert.That(movement, Does.Contain("Одновременно"));
            Assert.That(movement, Does.Contain("ComposerGetSelectedDialogueBeat"));
            Assert.That(movement, Does.Contain("ComposerPreviewSelectedMovement"));
        }

        [Test]
        public void MD_BasicTextUsesSelectedCanonicalDialogueBeatSurface()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string textInspector = ExtractMethodBody(
                source, "private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)");
            string authoringSection = ExtractMethodBody(
                source, "private void DrawSceneComposerDialogueAuthoringSection(VnSceneComposerScene scene)");

            Assert.That(textInspector, Does.Contain("DrawSceneComposerDialogueAuthoringSection(scene)"),
                "The consolidated Text inspector must route ordinary Beat editing through its dedicated Реплика foldout helper.");

            string[] requiredLabels =
            {
                "Реплики",
                "+ Реплика",
                "Дублировать",
                "Удалить",
                "↑",
                "↓",
                "Говорящий"
            };
            foreach (string label in requiredLabels)
            {
                Assert.That(authoringSection, Does.Contain("\"" + label + "\""),
                    "M-DIALOGUE basic text authoring must expose the creator-facing control: " + label + ".");
            }

            Assert.That(authoringSection, Does.Contain("ComposerGetSelectedDialogueBeat"),
                "The Реплика section must edit the selected canonical dialogue beat.");
            Assert.That(authoringSection, Does.Not.Contain("scene.previewText"),
                "Ordinary authoring must not bind permanently to the first-beat compatibility proxy.");
            Assert.That(authoringSection, Does.Not.Contain("scene.speaker"),
                "Ordinary authoring must resolve the selected canonical Beat instead of Scene speaker proxy state.");
            Assert.That(CountOccurrences(
                    authoringSection, "DrawSceneComposerDialogueBodyEditor(selectedBeat)"), Is.EqualTo(1),
                "The selected canonical dialogue Beat must route through exactly one multiline authoring editor.");

            string dialogueEditor = ExtractMethodBody(
                source, "private string DrawSceneComposerDialogueBodyEditor(VnSceneComposerDialogueBeat beat)");
            Assert.That(CountOccurrences(dialogueEditor, "EditorGUILayout.TextArea("), Is.EqualTo(1),
                "The dialogue authoring helper must own exactly one multiline text editor.");
            Assert.That(dialogueEditor, Does.Contain("wordWrap = true")
                .Or.Contain("SceneComposerDialogueTextAreaStyle"),
                "The dialogue authoring helper must use the wrapped Scene Composer textarea style.");
        }

        [Test]
        public void UxC_BasicTextExposesCanonicalFontSizeAndSpeedControls()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string typographySource = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerTextElements.cs");
            string textInspector = ExtractMethodBody(
                source, "private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)");
            string presentationSection = ExtractMethodBody(
                source, "private void DrawSceneComposerDialoguePresentationSection(");

            Assert.That(textInspector, Does.Contain("DrawSceneComposerDialoguePresentationSection(scene)"),
                "The consolidated Text inspector must expose its Оформление диалога foldout.");
            Assert.That(presentationSection, Does.Contain("DrawSceneComposerDialogueTypographyInspector(scene)"),
                "Оформление диалога must expose the corrected existing-dialogue typography surface.");
            Assert.That(typographySource, Does.Contain("\"Шрифт Windows\"")
                .And.Contain("\"Размер\"")
                .And.Contain("\"Изменить текст говорящего\"")
                .And.Contain("\"Изменить текст реплики\""),
                "Basic text authoring must expose Windows font selection, size, and both existing dialogue-text controls.");
            Assert.That(presentationSection, Does.Contain("\"Скорость текста\""),
                "Оформление диалога must expose Скорость текста without opening raw Typewriter controls.");
            Assert.That(presentationSection, Does.Contain("VnSceneComposerComposition.ResolvePresentation"),
                "Basic formatting must read the canonical effective Project Defaults + Scene Overrides presentation.");
        }

        [Test]
        public void UxC_PreviewSampleIsCollapsedAdvancedTypographyTestOnly()
        {
            string sceneSource = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string authoringSource = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string additional = ExtractMethodBody(sceneSource, "private void DrawSceneComposerAdditionalInspector(VnSceneComposerScene scene)");
            string presentation = ExtractMethodBody(authoringSource, "private void DrawSceneComposerPresentationControls(VnSceneComposerScene scene)");
            string sample = ExtractMethodBody(authoringSource, "private void DrawSceneComposerPreviewTextControls()");

            Assert.That(additional, Does.Contain("\"Дополнительно\""),
                "Preview/sample text must remain reachable only through the Дополнительно workflow.");
            Assert.That(presentation, Does.Contain("DrawSceneComposerPreviewTextControls();"));
            Assert.That(authoringSource, Does.Contain("[SerializeField] private bool _sceneComposerPreviewTextExpanded;"),
                "Typography test text must default to a collapsed advanced disclosure state.");
            Assert.That(sample, Does.Contain("EditorGUILayout.Foldout"));
            Assert.That(sample, Does.Contain("\"Тест оформления текста\""),
                "Advanced sample text must be presented as a typography test, not as Preview Text.");
            Assert.That(sample, Does.Contain("\"Тестовый текст оформления\""));
            Assert.That(sample, Does.Contain("Используется только для проверки внешнего вида текста."));
            Assert.That(sample, Does.Contain("Не является текстом сцены."),
                "The sample editor must explicitly say that it is not scene content.");
            Assert.That(sample, Does.Not.Contain("EditorGUILayout.LabelField(\"Preview Text\""));
        }

        [Test]
        public void UxC_TypographyTestPreservesTextCoreGlyphWarning()
        {
            string authoring = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string sample = ExtractMethodBody(authoring, "private void DrawSceneComposerPreviewTextControls()");
            string parity = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerFinalParity.cs");

            Assert.That(sample, Does.Contain("ComposerGetPreviewTextGlyphWarning()"),
                "Typography test text must keep automatic missing-glyph feedback.");
            Assert.That(parity, Does.Contain("FontEngine.LoadFontFace"));
            Assert.That(parity, Does.Contain("FontEngine.TryGetGlyphWithUnicodeValue"));
            Assert.That(parity, Does.Contain("FontEngine.UnloadFontFace"),
                "TextCore glyph validation must continue to unload the font face in the proven path.");
        }

        [Test]
        public void MD_PlaybackPreviewForwardRoutesDialogueWithoutChangingToolbarSceneNext()
        {
            string sceneSource = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string authoringSource = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string rendererSource = ReadEditorSource("VnPresentationWorkshopPreviewRenderer.RuntimeUi.cs");
            string preview = ExtractMethodBody(sceneSource, "private void DrawSceneComposerPreview()");
            string playbackInput = ExtractMethodBody(authoringSource,
                "private void HandleSceneComposerPlaybackInput(");
            string forwardButton = ExtractMethodBody(rendererSource,
                "private static void DrawPlaqueButton(");

            Assert.That(preview, Does.Contain("HandleSceneComposerPlaybackInput(previewRect, frame, Event.current);"),
                "During active playback, ordinary plaque clicks must still route dialogue progression.");
            Assert.That(preview, Does.Contain("previewRect, _sceneComposerPlayback.CurrentFrame, selectedUi, advancedLayout"),
                "Playback must register the controller with the renderer before drawing native controls.");
            Assert.That(forwardButton, Does.Contain("GUI.Button(hit,").And.Contain("index == 1) owner.RequestAdvance(owner.InputTick);"),
                "The visible Forward button must use its own hitbox and authoritative dialogue command.");
            Assert.That(playbackInput, Does.Contain("_sceneComposerPlayback.RequestAdvance(_sceneComposerPlayback.InputTick);"));
            Assert.That(playbackInput, Does.Not.Contain("VnWorkshopElement.Next"),
                "The removed invisible Next rectangle must not handle playback clicks.");
            Assert.That(playbackInput, Does.Not.Contain("ComposerSelectPreviewObjectAt"),
                "Playback clicking must not enable authoring selection/drag behavior.");
            Assert.That(playbackInput, Does.Not.Contain("RecordSceneComposerUndo"),
                "Transient playback dialogue progression must not create Undo records.");
            Assert.That(preview, Does.Contain("ComposerNext();"),
                "Toolbar Следующая must remain real Scene navigation.");
        }

        [Test]
        public void MDREG_PlaybackDialoguePanelAndForwardBothAdvanceDialogue()
        {
            string authoringSource = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string rendererSource = ReadEditorSource("VnPresentationWorkshopPreviewRenderer.RuntimeUi.cs");
            string playbackInput = ExtractMethodBody(authoringSource,
                "private void HandleSceneComposerPlaybackInput(");
            string controls = ExtractMethodBody(rendererSource,
                "private static void DrawComposerRuntimeControls(");
            string button = ExtractMethodBody(rendererSource,
                "private static void DrawPlaqueButton(");

            Assert.That(playbackInput, Does.Contain("frame.DialoguePanel"),
                "Normal playback must treat the visible dialogue panel as a dialogue-advance hit region.");
            Assert.That(controls, Does.Contain("layout.Forward, 1"),
                "The visible round Forward control must be drawn within the plaque layout.");
            Assert.That(button, Does.Contain("GUI.Button(hit,").And.Contain("owner.RequestAdvance(owner.InputTick);"),
                "The Forward hitbox must invoke the same authoritative command as the dialogue panel.");
            Assert.That(playbackInput, Does.Contain("_sceneComposerPlayback.RequestAdvance(_sceneComposerPlayback.InputTick);"));
            Assert.That(playbackInput, Does.Contain("currentEvent.Use();"),
                "A handled playback click must be consumed exactly as playback input.");
            Assert.That(playbackInput, Does.Not.Contain("ComposerNext();"),
                "Preview clicks must advance dialogue beats, not invoke scene-level toolbar navigation.");
        }

        [Test]
        public void UxD_BasicCharacterInspectorUsesContextualRussianControls()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string inspector = ExtractMethodBody(source, "private void DrawSceneComposerCharacterInspector(VnSceneComposerScene scene)");

            Assert.That(inspector, Does.Contain("\"+ Добавить персонажа\""));
            Assert.That(inspector, Does.Contain("\"Персонаж\""));
            Assert.That(inspector, Does.Contain("\"Поза / эмоция\""),
                "The ordinary pose picker must use the visual-novel concept Поза / эмоция.");
            Assert.That(inspector, Does.Contain("\"Положение\""));
            Assert.That(inspector, Does.Contain("\"Размер\""),
                "Character size must be an ordinary control rather than raw Scale engineering UI.");
            Assert.That(inspector, Does.Contain("\"Слева\""));
            Assert.That(inspector, Does.Contain("\"Центр\""));
            Assert.That(inspector, Does.Contain("\"Справа\""));
            Assert.That(inspector, Does.Contain("\"Свободно\""),
                "Dragged characters must have a clear free-position state in the basic inspector.");
            Assert.That(inspector, Does.Contain("character.stageSlot"),
                "Position presets must keep using the canonical stage slot.");
            Assert.That(inspector, Does.Contain("character.hasPositionOffset"),
                "Free positioning must remain the existing canonical position-offset path.");
            Assert.That(inspector, Does.Not.Contain("\"Поза / состояние\""));
        }

        [Test]
        public void UxD_CharacterListHasFriendlyEmptyAndSelectionStates()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string inspector = ExtractMethodBody(source, "private void DrawSceneComposerCharacterInspector(VnSceneComposerScene scene)");

            Assert.That(inspector, Does.Contain("В сцене пока нет персонажей."),
                "An empty scene must explain that there are no characters yet.");
            Assert.That(inspector, Does.Contain("Выберите персонажа в списке или на сцене."),
                "When characters exist but none is selected, the inspector must explain the natural list/preview selection workflow.");
            Assert.That(inspector, Does.Contain("_sceneComposerSelectedCharacterIndex = i"),
                "Selecting a character in the list must use the same active-character state as preview selection.");
            Assert.That(inspector, Does.Not.Contain("Select for Preview"));
            Assert.That(inspector, Does.Not.Contain("Selected in Preview"));
        }

        [Test]
        public void UxD_PreviewSelectionAndDirectDragRemainCanonical()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string select = ExtractMethodBody(source, "public bool ComposerSelectPreviewObjectAt(Vector2 logicalPoint)");
            string selectCharacter = ExtractMethodBody(source,
                "private bool ComposerSelectPreviewCharacterAt(VnWorkshopPreviewFrame frame, Vector2 logicalPoint)");
            string selectCharacterById = ExtractMethodBody(source,
                "private bool TrySelectPreviewCharacterById(");
            string input = ExtractMethodBody(source,
                "private void HandleSceneComposerPreviewInput(Rect previewRect, VnWorkshopPreviewFrame frame, Event currentEvent)");
            string drag = ExtractMethodBody(source, "private void ApplySceneComposerCharacterDrag(Vector2 logicalDelta)");

            Assert.That(select, Does.Contain("ComposerSelectPreviewCharacterAt(frame, logicalPoint)"),
                "The public preview selector must continue delegating character hit-testing to the canonical character-selection path.");
            Assert.That(selectCharacter, Does.Contain("TrySelectPreviewCharacterById("),
                "Preview hit-testing must delegate authored-character selection through the stable character-id path.");
            Assert.That(selectCharacterById, Does.Contain("_sceneComposerSelectedCharacterIndex = sceneCharacterIndex"),
                "Clicking a character in the central preview must select the matching authored Scene character by stable id.");
            Assert.That(selectCharacterById, Does.Contain("FindCharacterStagingByCharacter(beat, characterId)"),
                "Preview selection must preserve the current Beat staging identity for the selected character.");
            Assert.That(input, Does.Contain("ComposerSelectPreviewObjectAt(logicalPoint)"),
                "Basic preview interaction must delegate click selection to the canonical preview selector.");
            Assert.That(select, Does.Contain("HitTestSceneComposerUi(frame, logicalPoint)"),
                "Ordinary authoring must select canonical UI objects through the same preview selector before character fallback.");
            Assert.That(select.IndexOf("HitTestSceneComposerUi(frame, logicalPoint)", StringComparison.Ordinal),
                Is.LessThan(select.IndexOf("ComposerSelectPreviewCharacterAt(frame, logicalPoint)", StringComparison.Ordinal)),
                "Canonical UI-object hit-testing must run before the character fallback in ordinary authoring.");
            Assert.That(input, Does.Contain("ApplySceneComposerCharacterDrag(logicalDelta);"),
                "Mouse drag in the preview must continue to route to the character drag path.");
            Assert.That(drag, Does.Contain("staging.position = VnSceneComposerBeatCharacterPosition.Custom;"));
            Assert.That(drag, Does.Contain("staging.customPositionOffset ="),
                "Direct drag must remain owned by the current Beat staging row instead of mutating Scene-level character base geometry.");
            Assert.That(drag, Does.Not.Contain("character.positionOffset ="),
                "Beat-owned Preview drag must not fall back to Scene-level character positionOffset.");
        }

        [Test]
        public void UxD_ExactTransformIsSecondaryRussianDisclosure()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string exact = ExtractMethodBody(source,
                "private void DrawSceneComposerCharacterTransformControls(int characterIndex, VnSceneComposerCharacter character)");

            Assert.That(exact, Does.Contain("\"Точное положение\""),
                "Exact coordinates must remain available behind a secondary disclosure.");
            Assert.That(exact, Does.Contain("\"X\""));
            Assert.That(exact, Does.Contain("\"Y\""));
            Assert.That(exact, Does.Contain("\"Масштаб\""));
            Assert.That(exact, Does.Contain("\"Сбросить положение и размер\""),
                "The reset label must describe the existing reset semantics accurately.");
            Assert.That(exact, Does.Contain("ComposerSetCharacterTransform"));
            Assert.That(exact, Does.Contain("ComposerResetCharacterTransform"));
            Assert.That(exact, Does.Not.Contain("\"Transform\""));
            Assert.That(exact, Does.Not.Contain("\"Position X\""));
            Assert.That(exact, Does.Not.Contain("\"Position Y\""));
            Assert.That(exact, Does.Not.Contain("\"Scale\""));
            Assert.That(exact, Does.Not.Contain("Select for Preview"));
            Assert.That(exact, Does.Not.Contain("Selected in Preview"));
            Assert.That(exact, Does.Not.Contain("Reset Transform"));
        }

        [Test]
        public void UxE_CharacterAnimationUsesRussianVnConcepts()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string inspector = ExtractMethodBody(source, "private void DrawSceneComposerInspector()");

            Assert.That(inspector, Does.Contain("DrawSceneComposerCharacterAnimationInspector(scene);"),
                "Анимация персонажа must be a real ordinary authoring surface instead of a shortcut to Дополнительно.");
            string[] requiredLabels =
            {
                "Анимация персонажа",
                "Переход персонажа",
                "Смена позы / эмоции",
                "Акцент / движение",
                "Без анимации",
                "Плавный переход",
                "Переход со сдвигом",
                "▶ Проверить появление",
                "▶ Проверить исчезновение"
            };
            foreach (string label in requiredLabels)
                Assert.That(source, Does.Contain("\"" + label + "\""),
                    "Basic character animation must expose the Russian VN concept: " + label + ".");

            Assert.That(source, Does.Contain("ComposerSetCharacterTransition("),
                "Shared enter/exit authoring must edit the proven canonical characterTransition profile.");
            Assert.That(source, Does.Contain("ComposerSetExpressionTransition("),
                "Pose/emotion transition authoring must reuse the canonical expressionTransition profile.");
            Assert.That(source, Does.Contain("scene.transition.triggerActionBounce"),
                "The one basic accent trigger must remain the canonical scene transition flag.");
            Assert.That(source, Does.Contain("ComposerSetBounceAmplitude("),
                "Accent Strength must use the targeted canonical actionBounce mutation path.");
            Assert.That(source, Does.Contain("ComposerSetBounceDuration("),
                "Accent Duration must use the targeted canonical actionBounce mutation path.");
        }

        [Test]
        public void UxE_SceneAnimationUsesRussianVnConcepts()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string inspector = ExtractMethodBody(source, "private void DrawSceneComposerInspector()");

            Assert.That(inspector, Does.Contain("DrawSceneComposerSceneAnimationInspector(scene);"),
                "Анимация сцены must be a real ordinary authoring surface instead of a shortcut to Дополнительно.");
            string[] requiredLabels =
            {
                "Анимация сцены",
                "Переход между сценами",
                "Фокус говорящего",
                "Тайминг сцены",
                "Без перехода",
                "Плавный переход",
                "Тёмная шторка",
                "Слева направо",
                "Справа налево",
                "Переход к следующей сцене",
                "Вручную",
                "Автоматически"
            };
            foreach (string label in requiredLabels)
                Assert.That(source, Does.Contain("\"" + label + "\""),
                    "Basic scene animation must expose the Russian VN concept: " + label + ".");

            Assert.That(source, Does.Not.Contain("\"Переход фона\""),
                "Scene Animation must offer one scene-boundary transition control.");
            Assert.That(source, Does.Not.Contain("\"Расположение персонажей\""),
                "Automatic Character Rearrangement must remain removed from ordinary authoring.");
            Assert.That(source, Does.Not.Contain("ComposerSetStageLayout("),
                "Scene Animation must not reintroduce the removed automatic rearrangement control.");
            Assert.That(source, Does.Contain("ComposerSetSpeakerFocus("));
            Assert.That(source, Does.Contain("ComposerSetTiming("));
            Assert.That(source, Does.Contain("GetSceneComposerAdvanceTimingForAuthoring(scene)"));
            Assert.That(source, Does.Contain("advanceTiming.previewAdvanceMode"));
            Assert.That(source, Does.Contain("ComposerSetSelectedSceneAdvanceTiming("),
                "Manual/automatic scene advance must keep using the scoped canonical scene timing model.");
        }

        [Test]
        public void UxE_AdvancedKeepsParityButHidesEngineeringAnimationHierarchy()
        {
            string sceneSource = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string authoringSource = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string additional = ExtractMethodBody(sceneSource, "private void DrawSceneComposerAdditionalInspector(VnSceneComposerScene scene)");
            string playback = ExtractMethodBody(sceneSource, "private void DrawSceneComposerPresentationInspector(VnSceneComposerScene scene)");
            string sections = ExtractMethodBody(authoringSource, "private void DrawSceneComposerSerializedPresentationSections(VnSceneComposerScene scene)");

            Assert.That(additional, Does.Contain("DrawSceneComposerPresentationInspector(scene);"),
                "Advanced presentation functionality must remain reachable from Дополнительно.");
            string[] canonicalProperties =
            {
                "expressionTransition", "characterTransition", "actionBounce", "backgroundTransition",
                "stageLayout", "focus", "uiFeedback", "timing"
            };
            foreach (string property in canonicalProperties)
                Assert.That(sections, Does.Contain("\"" + property + "\""),
                    "Advanced parity must retain the canonical presentation property: " + property + ".");

            Assert.That(sections, Does.Contain("\"Эффекты интерфейса\""),
                "Detailed UI Feedback must remain available under a Russian advanced concept.");
            Assert.That(playback, Does.Not.Contain("Trigger Action Bounce"),
                "The scene-level bounce trigger must not duplicate Action Bounce outside the one Акцент / движение workflow.");
            Assert.That(playback, Does.Not.Contain("Scene Playback"),
                "Basic scene timing now belongs to Анимация сцены, not a second engineering playback block.");

            string[] oldEngineeringHeadings =
            {
                "Authored State / Expression",
                "Character Enter / Exit",
                "Action Bounce",
                "Background Transition",
                "Stage Layout",
                "Speaker Focus",
                "UI Feedback",
                "Timing / Pacing"
            };
            foreach (string heading in oldEngineeringHeadings)
                Assert.That(sections, Does.Not.Contain("\"" + heading + "\""),
                    "The advanced disclosure must not repeat the old engineering animation heading: " + heading + ".");
        }

        [Test]
        public void UxE_AnimationEngineParityStillConsumesCanonicalPresentationState()
        {
            string presentation = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerPresentation.cs");
            string sampler = ReadEditorSource("VnSceneComposerTransitionSampler.cs");
            string presetLifecycle = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerPresetLifecycle.cs");

            string[] setters =
            {
                "ComposerSetExpressionTransition", "ComposerSetCharacterTransition", "ComposerSetBounce",
                "ComposerSetBackgroundTransition", "ComposerSetStageLayout", "ComposerSetSpeakerFocus",
                "ComposerSetUiFeedback", "ComposerSetTiming"
            };
            foreach (string setter in setters)
                Assert.That(presentation, Does.Contain(setter),
                    "UX-E must preserve the proven presentation mutation API: " + setter + ".");

            Assert.That(sampler, Does.Contain("ResolveBackgroundTransition(preset)"));
            Assert.That(sampler, Does.Contain("ResolveActionBounce(preset)"));
            Assert.That(sampler, Does.Contain("ResolveStageLayout(preset)"));
            Assert.That(sampler, Does.Contain("ResolveCharacterTransition(preset)"));
            Assert.That(sampler, Does.Contain("ResolveExpressionTransition(preset)"));
            Assert.That(sampler, Does.Contain("ResolveSpeakerFocus(preset)"));
            Assert.That(sampler, Does.Contain("SampleCharacterEnter"));
            Assert.That(sampler, Does.Contain("SampleCharacterExit"));
            Assert.That(sampler, Does.Contain("toScene.transition.triggerActionBounce"));
            Assert.That(sampler, Does.Contain("timing.AutoPreviewSequenceGap"),
                "AutoPreviewSequenceGap must continue feeding ordered playback timing.");

            Assert.That(presentation, Does.Contain("ComposerSavePresentationPreset"));
            Assert.That(presentation, Does.Contain("ComposerLoadPresentationPreset"));
            Assert.That(presentation, Does.Contain("ComposerExportPresentationPresetJson"));
            Assert.That(presentation, Does.Contain("ComposerImportPresentationPresetJson"));
            Assert.That(presetLifecycle, Does.Contain("ComposerDuplicatePresentationPreset"));
            Assert.That(presetLifecycle, Does.Contain("ComposerRenamePresentationPreset"));
            Assert.That(presetLifecycle, Does.Contain("ComposerDeletePresentationPreset"));
        }

        [Test]
        public void UxH_BasicPreviewHidesEngineeringHitRegionsAndUiFeedbackPreview()
        {
            string scene = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string preview = ExtractMethodBody(scene, "private void DrawSceneComposerPreview()");

            Assert.That(preview, Does.Contain("IsSceneComposerAdvancedLayoutEditingVisible()"),
                "Hit-region/layout overlays must be explicitly gated by the advanced layout disclosure.");
            Assert.That(preview, Does.Contain("IsSceneComposerAdvancedUiFeedbackPreviewVisible()"),
                "UI-feedback simulation must be explicitly gated by the advanced effects disclosure.");
            Assert.That(preview, Does.Not.Contain(
                "VnPresentationWorkshopPreviewRenderer.Draw(previewRect, frame, selectedUi, staticAuthoringPreview,"),
                "A normal static authoring preview must not automatically turn on hit-region debug rendering.");
        }

        [Test]
        public void UxH_BasicProjectAndMediaHideTechnicalEditorInternals()
        {
            string scene = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string project = ExtractMethodBody(scene, "private void DrawSceneComposerProjectStorage()");
            string media = ExtractMethodBody(scene, "private void DrawSceneComposerMediaInspector(VnSceneComposerScene scene)");
            string additional = ExtractMethodBody(scene, "private void DrawSceneComposerAdditionalInspector(VnSceneComposerScene scene)");

            Assert.That(project, Does.Not.Contain("\"ID проекта\""),
                "Stable project IDs are engineering metadata and must not occupy the normal project header.");
            Assert.That(project, Does.Not.Contain("Library/ROKAS/VnSceneComposer"),
                "The editor storage path must not be exposed in basic authoring.");
            Assert.That(media, Does.Not.Contain("\"Ресурс ROKAS\""),
                "Basic media authoring should use image/GIF/video concepts rather than a raw project-asset field.");
            Assert.That(media, Does.Not.Contain("production Assets"),
                "Ordinary Russian media help must not leak internal English production terminology.");
            Assert.That(additional, Does.Contain("DrawSceneComposerProjectTechnicalInfo();"),
                "Project ID/storage diagnostics must remain reachable from Дополнительно instead of being deleted.");
        }

        [Test]
        public void UxH_AdvancedScopeUsesUserIntentInsteadOfDefaultsOverridesJargon()
        {
            string authoring = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string presentation = ExtractMethodBody(authoring,
                "private void DrawSceneComposerPresentationControls(VnSceneComposerScene scene)");

            Assert.That(presentation, Does.Contain("\"Применить:\""));
            Assert.That(presentation, Does.Contain("\"Только к этой сцене\""));
            Assert.That(presentation, Does.Contain("\"Ко всем сценам\""));
            Assert.That(presentation, Does.Contain("ComposerSetPresentationScope"),
                "Friendly scope wording must continue to mutate the canonical defaults/overrides model.");
            Assert.That(presentation, Does.Not.Contain("\"Project Defaults\""));
            Assert.That(presentation, Does.Not.Contain("\"Scene Overrides\""));
        }

        [Test]
        public void UxH_AdvancedPresentationUsesRussianIntentHeadings()
        {
            string authoring = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string presentation = ExtractMethodBody(authoring,
                "private void DrawSceneComposerPresentationControls(VnSceneComposerScene scene)");
            string sections = ExtractMethodBody(authoring,
                "private void DrawSceneComposerSerializedPresentationSections(VnSceneComposerScene scene)");

            Assert.That(presentation, Does.Contain("\"Разметка интерфейса\""),
                "Exact layout and hit-region editing must live under a clear Russian advanced heading.");
            Assert.That(sections, Does.Contain("\"Расширенная типографика\""));
            Assert.That(sections, Does.Contain("\"Эффекты интерфейса\""));
            Assert.That(presentation, Does.Not.Contain("\"UI Layout / Hit Regions\""));
            Assert.That(presentation, Does.Not.Contain("\"Presentation\""));
            Assert.That(sections, Does.Not.Contain("\"Advanced / Full Preset\""));
        }

        [Test]
        public void UxH_PreviewTextGlyphAndPresetJsonRemainAdvancedAndPreserved()
        {
            string scene = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string authoring = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string additional = ExtractMethodBody(scene, "private void DrawSceneComposerAdditionalInspector(VnSceneComposerScene scene)");
            string presentation = ExtractMethodBody(authoring,
                "private void DrawSceneComposerPresentationControls(VnSceneComposerScene scene)");
            string sample = ExtractMethodBody(authoring, "private void DrawSceneComposerPreviewTextControls()");
            string presets = ExtractMethodBody(authoring, "private void DrawSceneComposerPresetControls()");

            Assert.That(additional, Does.Contain("DrawSceneComposerPresentationInspector(scene);"));
            Assert.That(presentation, Does.Contain("DrawSceneComposerPreviewTextControls();"));
            Assert.That(presentation, Does.Contain("DrawSceneComposerPresetControls();"));
            Assert.That(sample, Does.Contain("ComposerGetPreviewTextGlyphWarning()"),
                "Glyph diagnostics must remain available inside the advanced typography-test path.");
            Assert.That(presets, Does.Contain("ComposerExportPresentationPresetJson"));
            Assert.That(presets, Does.Contain("ComposerImportPresentationPresetJson"),
                "JSON import/export must remain available in advanced presentation templates.");
        }

        [Test]
        public void UxH_AssetLibraryHasFriendlyRussianAdvancedEntryPoint()
        {
            string assets = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAssets.cs");
            string controls = ExtractMethodBody(assets,
                "private void DrawSceneComposerAssetLibraryControls(VnSceneComposerScene scene)");

            Assert.That(controls, Does.Contain("\"Библиотека ресурсов\""));
            Assert.That(controls, Does.Contain("\"Обновить\""));
            Assert.That(controls, Does.Contain("\"Открыть папку\""));
            Assert.That(controls, Does.Contain("ComposerSetExistingRokasAsset"),
                "Moving the raw project asset picker out of basic Media must not remove that capability.");
            Assert.That(controls, Does.Not.Contain("\"Asset Library / Onboard Asset\""));
            Assert.That(controls, Does.Not.Contain("\"Refresh Assets\""));
            Assert.That(controls, Does.Not.Contain("\"Open Managed Folder\""));
        }

        [Test]
        public void UxH_BasicPlaybackStatusUsesRussianTimeUnit()
        {
            string scene = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string preview = ExtractMethodBody(scene, "private void DrawSceneComposerPreview()");

            Assert.That(preview, Does.Contain("+ \" с\""),
                "The ordinary playback status must use the Russian seconds abbreviation.");
            Assert.That(preview, Does.Not.Contain("+ \"s\""));
        }

        [Test]
        public void VisualPolish_ProjectControlsAreAHeaderAboveTheThreeColumnWorkspace()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string workspace = ExtractMethodBody(source, "public void DrawSceneComposerWorkspace()");
            string inspector = ExtractMethodBody(source, "private void DrawSceneComposerInspector()");

            Assert.That(workspace, Does.Contain("DrawSceneComposerProjectStorage();"),
                "Project name/save/load controls should read as a top tool header rather than consume contextual inspector space.");
            Assert.That(workspace, Does.Contain("DrawSceneComposerStoryboard();")
                .And.Contain("DrawSceneComposerPreview();")
                .And.Contain("DrawSceneComposerInspector();"),
                "The proven left / center / right Scene Composer regions must remain the core workspace.");
            Assert.That(inspector, Does.Not.Contain("DrawSceneComposerProjectStorage();"),
                "The contextual inspector should be reserved for scene authoring after the project controls move to the header.");
        }

        [Test]
        public void VisualPolish_SceneCardsAreCompactAndKeepManagementSecondary()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string storyboard = ExtractMethodBody(source, "private void DrawSceneComposerStoryboard()");

            Assert.That(storyboard, Does.Contain("Создайте первую сцену."),
                "The empty storyboard should use the concise approved empty state.");
            Assert.That(storyboard, Does.Contain("SceneComposerSceneThumbnailWidth"),
                "Scene thumbnails should use a compact card-sized width rather than the previous full-column 220x100 block.");
            Assert.That(storyboard, Does.Contain("EditorStyles.miniButtonLeft")
                .And.Contain("EditorStyles.miniButtonMid")
                .And.Contain("EditorStyles.miniButtonRight"),
                "Duplicate/delete/reorder actions should form a compact secondary action row on the selected scene.");
        }

        [Test]
        public void VisualPolish_PreviewTransportReadsAsOneBarWithPrimaryPlayActions()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string preview = ExtractMethodBody(source, "private void DrawSceneComposerPreview()");

            Assert.That(preview, Does.Contain("EditorGUILayout.BeginHorizontal(EditorStyles.toolbar)"),
                "Playback should read as one coherent transport strip beneath the preview.");
            Assert.That(preview, Does.Contain("SceneComposerPrimaryTransportButtonStyle"),
                "Play Scene and Play All should receive stronger visual priority than navigation and utility transport actions.");
            Assert.That(preview, Does.Contain("\"Проиграть отсюда\""),
                "Play From Here remains available as a secondary/contextual transport action.");
            Assert.That(preview, Does.Contain("GUILayout.FlexibleSpace();"),
                "The contextual Play From Here action should be visually separated from the core transport cluster.");
        }

        [Test]
        public void VisualPolish_RightInspectorIsContextualAndVisuallyGrouped()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string inspector = ExtractMethodBody(source, "private void DrawSceneComposerInspector()");

            Assert.That(inspector, Does.Contain("\"Свойства сцены\""),
                "The right column should identify itself as contextual scene properties.");
            Assert.That(inspector, Does.Contain("EditorGUILayout.BeginVertical(EditorStyles.helpBox)"),
                "The active inspector section should be visually grouped instead of reading as a flat control dump.");
            Assert.That(inspector, Does.Not.Contain("DrawSceneComposerProjectStorage();"),
                "Project-wide controls do not belong inside the contextual scene inspector after visual polish.");
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

        private static int CountOccurrences(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value)) return 0;
            int count = 0;
            int offset = 0;
            while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing editor type: " + shortName);
            return type;
        }
    }
}
