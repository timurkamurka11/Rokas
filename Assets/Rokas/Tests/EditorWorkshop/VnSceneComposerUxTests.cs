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
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null,
                "The proven Workshop preview path must remain available to compatibility tests.");
            Assert.That(windowType.GetMethod("ActivateSceneComposerWorkspace",
                BindingFlags.Instance | BindingFlags.Public), Is.Not.Null,
                "Existing Scene Composer compatibility entry points must remain intact.");
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
