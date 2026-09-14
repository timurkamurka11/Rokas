using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerExternalVideoRoutingTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void ExternalVideoStaticPreviewBuildsRichRendererFacingFrameInsteadOfTexture2DFallback()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            string path = Path.Combine(Path.GetTempPath(), "rokas-vn-video-routing-" + Guid.NewGuid().ToString("N") + ".mp4");
            File.WriteAllBytes(path, new byte[] { 0, 0, 0, 0 });
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                RequireInstance(windowType, "ComposerSetExternalVideo", typeof(string)).Invoke(window, new object[] { path });

                MethodInfo build = RequireInstance(windowType, "ComposerBuildSelectedPreviewPlaybackFrame");
                object playbackFrame = build.Invoke(window, null);
                Assert.That(playbackFrame, Is.Not.Null);
                Texture target = (Texture)GetProperty(playbackFrame, "TargetBackground");
                Assert.That(target, Is.Not.Null.And.TypeOf<RenderTexture>(),
                    "ExternalVideo must reach the renderer as its RenderTexture, never disappear through a Texture2D cast.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void VideoPreviewExposesPrepareFirstFrameLifecycleForNonPlayingScenePreview()
        {
            Type previewType = RequireType("VnSceneComposerVideoPreview");
            RequireInstance(previewType, "Prepare");
            PropertyInfo prepared = previewType.GetProperty("IsPrepared", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(prepared, Is.Not.Null);
            PropertyInfo preparing = previewType.GetProperty("IsPreparing", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(preparing, Is.Not.Null,
                "Static ExternalVideo preview needs an explicit Preparing state instead of silently showing unrelated default media.");
            PropertyInfo visibleFrame = previewType.GetProperty("HasVisibleFrame", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(visibleFrame, Is.Not.Null,
                "The authoring UI must distinguish a prepared/rendered video frame from an empty RenderTexture.");
        }

        [Test]
        public void PlaybackControllerProvidesInjectableVideoFactoryForDeterministicRoutingTests()
        {
            Type controllerType = RequireType("VnSceneComposerPlaybackController");
            Type projectType = RequireType("VnSceneComposerProject");
            Type factoryType = RequireType("IVnSceneComposerVideoPreviewFactory");
            ConstructorInfo constructor = controllerType.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { projectType, factoryType },
                null);
            Assert.That(constructor, Is.Not.Null,
                "Playback must accept an editor-only fake video provider so CI can prove routing/pause/restart/loop/scene-switch without pretending a fake mp4 decoded.");
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer type: " + shortName);
            return type;
        }

        private static MethodInfo RequireInstance(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                parameters ?? Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null, "Missing method: " + type.Name + "." + name);
            return method;
        }

        private static object GetProperty(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            PropertyInfo property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property: " + instance.GetType().Name + "." + name);
            return property.GetValue(instance, null);
        }
    }
}
