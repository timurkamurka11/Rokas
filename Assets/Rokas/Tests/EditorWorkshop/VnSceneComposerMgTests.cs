using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerMgTests
    {
        [Test]
        public void MG_PracticalWindowKeepsPreviewWiderThanEitherSidePanel()
        {
            VnPresentationWorkshopWindow window = CreateWindow(900f, 700f);
            try
            {
                float storyboard = InvokeFloat(window, "GetSceneComposerStoryboardWidth");
                float inspector = InvokeFloat(window, "GetSceneComposerInspectorWidth");
                float preview = InvokeFloat(window, "GetSceneComposerPreviewAvailableWidth");

                Assert.That(storyboard, Is.InRange(176f, 224f));
                Assert.That(inspector, Is.InRange(288f, 360f));
                Assert.That(preview, Is.GreaterThan(storyboard));
                Assert.That(preview, Is.GreaterThan(inspector),
                    "The central Scene preview must remain the dominant single panel at a practical compact width.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MG_WideWindowPreservesEstablishedMaximumSidePanelWidths()
        {
            VnPresentationWorkshopWindow window = CreateWindow(1400f, 800f);
            try
            {
                Assert.That(InvokeFloat(window, "GetSceneComposerStoryboardWidth"), Is.EqualTo(224f).Within(.001f));
                Assert.That(InvokeFloat(window, "GetSceneComposerInspectorWidth"), Is.EqualTo(360f).Within(.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MG_TransportUsesCompactModeOnlyWhenPreviewNeedsIt()
        {
            VnPresentationWorkshopWindow window = CreateWindow(900f, 700f);
            try
            {
                Assert.That(InvokeBool(window, "UseCompactSceneComposerTransport"), Is.True);
                window.position = new Rect(0f, 0f, 1400f, 800f);
                Assert.That(InvokeBool(window, "UseCompactSceneComposerTransport"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MG_HeaderUsesCompactModeAtNarrowWidthAndFullModeAtWideWidth()
        {
            VnPresentationWorkshopWindow window = CreateWindow(900f, 700f);
            try
            {
                Assert.That(InvokeBool(window, "UseCompactSceneComposerHeader"), Is.True);
                window.position = new Rect(0f, 0f, 1400f, 800f);
                Assert.That(InvokeBool(window, "UseCompactSceneComposerHeader"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MG_InspectorKeepsRecognizableSectionsAndDedicatedSelector()
        {
            Type type = typeof(VnPresentationWorkshopWindow);
            FieldInfo sectionsField = type.GetField("SceneComposerInspectorSections",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(sectionsField, Is.Not.Null);
            string[] sections = (string[])sectionsField.GetValue(null);
            Assert.That(sections, Has.Length.EqualTo(11));
            Assert.That(sections, Does.Contain("Фон").And.Contain("Персонажи").And.Contain("Текст"));
            Assert.That(sections, Does.Contain("Анимация персонажа").And.Contain("Анимация сцены"));
            Assert.That(sections, Does.Contain("Медиа").And.Contain("Настройки сцены").And.Contain("Дополнительно"));
            Assert.That(sections, Does.Contain("Декорации").And.Contain("Звуки").And.Contain("Музыка"));
            Assert.That(type.GetMethod("DrawSceneComposerInspectorSelector",
                BindingFlags.NonPublic | BindingFlags.Instance), Is.Not.Null);
        }

        private static VnPresentationWorkshopWindow CreateWindow(float width, float height)
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            window.position = new Rect(0f, 0f, width, height);
            return window;
        }

        private static float InvokeFloat(object target, string method)
        {
            return (float)RequireMethod(target.GetType(), method).Invoke(target, null);
        }

        private static bool InvokeBool(object target, string method)
        {
            return (bool)RequireMethod(target.GetType(), method).Invoke(target, null);
        }

        private static MethodInfo RequireMethod(Type type, string method)
        {
            MethodInfo info = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, "Missing M-G layout helper: " + method);
            return info;
        }
    }
}
