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
        public void UxC_BasicTextUsesOneCanonicalRussianSceneTextSurface()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string textInspector = ExtractMethodBody(source, "private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)");

            Assert.That(textInspector, Does.Contain("\"Текст\""));
            Assert.That(textInspector, Does.Contain("\"Текст без персонажа\""),
                "Narration must remain the existing scene.narration mode with a human Russian label.");
            Assert.That(textInspector, Does.Contain("\"Говорящий\""),
                "Speaker authoring must remain visible beside the canonical scene text.");
            Assert.That(textInspector, Does.Contain("\"Текст сцены\""),
                "The one real multiline content field must be identified as Текст сцены.");
            Assert.That(textInspector, Does.Contain("scene.previewText"),
                "The real scene-text editor must stay bound to the existing canonical scene.previewText path.");
            Assert.That(CountOccurrences(textInspector, "EditorGUILayout.TextArea("), Is.EqualTo(1),
                "Basic text authoring must expose exactly one multiline scene-content field.");
            Assert.That(textInspector, Does.Not.Contain("Preview Text"));
            Assert.That(textInspector, Does.Not.Contain("ComposerGetPreviewSampleText"));
            Assert.That(textInspector, Does.Not.Contain("Тестовый текст оформления"));
        }

        [Test]
        public void UxC_BasicTextExposesCanonicalFontSizeAndSpeedControls()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string textInspector = ExtractMethodBody(source, "private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)");

            Assert.That(textInspector, Does.Contain("\"Шрифт\""),
                "Basic text authoring must expose Шрифт without opening Дополнительно.");
            Assert.That(textInspector, Does.Contain("\"Размер текста\""),
                "Basic text authoring must expose Размер текста without opening raw Typography controls.");
            Assert.That(textInspector, Does.Contain("\"Скорость текста\""),
                "Basic text authoring must expose Скорость текста without opening raw Typewriter controls.");
            Assert.That(textInspector, Does.Contain("VnSceneComposerComposition.ResolvePresentation"),
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
