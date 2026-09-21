using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerProjectSpeakerColorTests
    {
        [Test]
        public void GlobalSpeakerColors_RED_01_ProjectOwnsPalette()
        {
            FieldInfo field = typeof(VnSceneComposerProject).GetField(
                "projectSpeakerPalette", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                "Speaker color profiles must be owned once by the project, not by individual Scenes.");
        }

        [Test]
        public void GlobalSpeakerColors_RED_02_ResolverExposesProjectProfileLookup()
        {
            MethodInfo method = typeof(VnSceneComposerTextStyleResolver).GetMethod(
                "FindProjectSpeakerProfile",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null,
                "The resolver needs a project-wide speaker profile lookup.");
        }

        [Test]
        public void GlobalSpeakerColors_RED_03_WindowExposesProjectNameColorAuthoring()
        {
            MethodInfo method = typeof(VnPresentationWorkshopWindow).GetMethod(
                "ComposerSetProjectSpeakerNameColor",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
        }

        [Test]
        public void GlobalSpeakerColors_RED_04_WindowExposesProjectDialogueColorAuthoring()
        {
            MethodInfo method = typeof(VnPresentationWorkshopWindow).GetMethod(
                "ComposerSetProjectSpeakerDialogueColor",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
        }

        [Test]
        public void GlobalSpeakerColors_RED_05_PermanentScenePaletteBlockIsGone()
        {
            string source = ReadEditorSource(
                "VnPresentationWorkshopWindow.SceneComposerTextElements.cs");
            string body = ExtractMethodBody(
                source,
                "private void DrawSceneComposerDialogueTypographyInspector(VnSceneComposerScene scene)");
            Assert.That(body, Does.Not.Contain("DrawCurrentSceneSpeakerPaletteControls(scene)")
                .And.Not.Contain("\"Цвета говорящего в этой сцене\""));
        }

        [Test]
        public void GlobalSpeakerColors_RED_06_NameColorLivesInsideSpeakerButtonEditor()
        {
            string source = ReadEditorSource(
                "VnPresentationWorkshopWindow.SceneComposerTextElements.cs");
            string body = ExtractMethodBody(
                source,
                "private void DrawSpeakerTypographyControls(VnSceneComposerScene scene)");
            Assert.That(body, Does.Contain("\"Говорящий\"")
                .And.Contain("\"Цвет имени\"")
                .And.Contain("\"Сбросить цвет\""));
        }

        [Test]
        public void GlobalSpeakerColors_RED_07_BodyColorLivesInsideDialogueButtonEditor()
        {
            string source = ReadEditorSource(
                "VnPresentationWorkshopWindow.SceneComposerTextElements.cs");
            string body = ExtractMethodBody(
                source,
                "private void DrawScopedTypographyControls(bool speaker, VnSceneComposerScene scene)");
            Assert.That(body, Does.Contain("\"Говорящий\"")
                .And.Contain("\"Цвет текста реплики\"")
                .And.Contain("\"Сбросить цвет\""));
        }

        [Test]
        public void GlobalSpeakerColors_RED_08_NoDuplicateDefaultColorRowsInNormalEditors()
        {
            string source = ReadEditorSource(
                "VnPresentationWorkshopWindow.SceneComposerTextElements.cs");
            Assert.That(source, Does.Not.Contain("\"Общий цвет имени\"")
                .And.Not.Contain("\"Цвет реплики по умолчанию\""));
        }

        [Test]
        public void GlobalSpeakerColors_RED_09_SceneColorScopeIsNotNormalAuthoring()
        {
            string source = ReadEditorSource(
                "VnPresentationWorkshopWindow.SceneComposerTextElements.cs");
            string body = ExtractMethodBody(
                source,
                "private void DrawSceneComposerDialogueTypographyInspector(VnSceneComposerScene scene)");
            Assert.That(body, Does.Not.Contain("speakerColorScope")
                .And.Not.Contain("ComposerSetSelectedSceneSpeakerColorScope"));
        }

        private static string ReadEditorSource(string fileName)
        {
            string path = Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop", fileName);
            Assert.That(File.Exists(path), Is.True, "Missing editor source: " + path);
            return File.ReadAllText(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "Missing method: " + signature);
            int open = source.IndexOf('{', start);
            Assert.That(open, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(open, i - open + 1);
                }
            }
            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
