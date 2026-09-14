using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerMotionMediaTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string TwoFrameGifBase64 =
            "R0lGODlhAgABAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQICgAAACwAAAAAAgABAAAIBQABAAgIACH5BAgUAAAALAAAAAACAAEAgQD/AAAAAAAAAAAAAAgFAAEACAgAOw==";

        [Test]
        public void ExternalVideoIsEditorLocalAndPreviewControllerOwnsDeterministicRenderTarget()
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            Type mediaType = RequireType("VnSceneComposerMediaReference");
            Type scaleModeType = RequireType("VnSceneComposerMediaScaleMode");
            Type editingType = RequireType("VnSceneComposerMediaEditing");
            Type previewType = RequireType("VnSceneComposerVideoPreview");
            object scene = Activator.CreateInstance(sceneType);
            string path = Path.Combine(Path.GetTempPath(), "rokas-vn-composer-sc-d-" + Guid.NewGuid().ToString("N") + ".mp4");
            File.WriteAllBytes(path, new byte[] { 0, 0, 0, 0 });

            try
            {
                object fit = Enum.Parse(scaleModeType, "Fit");
                MethodInfo select = RequireStatic(editingType, "SetExternalVideo",
                    sceneType, typeof(string), scaleModeType, typeof(bool));
                select.Invoke(null, new object[] { scene, path, fit, true });
                object media = Get(scene, "media");

                Assert.That(Get(media, "kind").ToString(), Is.EqualTo("ExternalVideo"));
                Assert.That(GetString(media, "reference"), Is.EqualTo(Path.GetFullPath(path)));
                Assert.That((bool)Get(media, "localPreviewDependency"), Is.True);
                Assert.That((bool)Get(media, "loop"), Is.True);
                Assert.That(GetString(media, "contentHash"), Is.Not.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Empty);

                MethodInfo open = RequireStatic(editingType, "OpenVideoPreview", mediaType, typeof(int), typeof(int));
                object preview = open.Invoke(null, new object[] { media, 320, 180 });
                Assert.That(preview, Is.Not.Null);
                RenderTexture texture = (RenderTexture)Get(preview, "texture");
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.width, Is.EqualTo(320));
                Assert.That(texture.height, Is.EqualTo(180));
                Assert.That(GetString(preview, "warning"), Is.Empty);
                Assert.That((bool)Get(preview, "loop"), Is.True);

                RequireInstance(previewType, "Play");
                RequireInstance(previewType, "Pause").Invoke(preview, null);
                RequireInstance(previewType, "Restart").Invoke(preview, null);
                RequireInstance(previewType, "Stop").Invoke(preview, null);
                RequireInstance(previewType, "Dispose").Invoke(preview, null);
                Assert.That(Get(preview, "texture"), Is.Null);

                File.Delete(path);
                object missing = open.Invoke(null, new object[] { media, 320, 180 });
                Assert.That(Get(missing, "texture"), Is.Null);
                Assert.That(GetString(missing, "warning"), Does.Contain("missing").IgnoreCase);
                RequireInstance(previewType, "Dispose").Invoke(missing, null);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void AnimatedGifDecodesCachedFramesAndRespectsFrameTiming()
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            Type mediaType = RequireType("VnSceneComposerMediaReference");
            Type scaleModeType = RequireType("VnSceneComposerMediaScaleMode");
            Type editingType = RequireType("VnSceneComposerMediaEditing");
            Type previewType = RequireType("VnSceneComposerGifPreview");
            object scene = Activator.CreateInstance(sceneType);
            string path = Path.Combine(Path.GetTempPath(), "rokas-vn-composer-sc-d-" + Guid.NewGuid().ToString("N") + ".gif");
            File.WriteAllBytes(path, Convert.FromBase64String(TwoFrameGifBase64));

            try
            {
                object fill = Enum.Parse(scaleModeType, "Fill");
                MethodInfo select = RequireStatic(editingType, "SetExternalGif",
                    sceneType, typeof(string), scaleModeType, typeof(bool));
                select.Invoke(null, new object[] { scene, path, fill, true });
                object media = Get(scene, "media");

                Assert.That(Get(media, "kind").ToString(), Is.EqualTo("ExternalGif"));
                Assert.That((bool)Get(media, "localPreviewDependency"), Is.True);
                Assert.That((bool)Get(media, "loop"), Is.True);
                Assert.That(GetString(media, "contentHash"), Is.Not.Empty);

                MethodInfo open = RequireStatic(editingType, "OpenGifPreview", mediaType);
                object preview = open.Invoke(null, new[] { media });
                Assert.That(GetString(preview, "warning"), Is.Empty);
                Assert.That((int)Get(preview, "frameCount"), Is.EqualTo(2));
                Assert.That((float)Get(preview, "totalDuration"), Is.EqualTo(.3f).Within(.03f));

                MethodInfo frameAt = RequireInstance(previewType, "GetFrameAtTime", typeof(float));
                Texture2D first = (Texture2D)frameAt.Invoke(preview, new object[] { .05f });
                Texture2D firstAgain = (Texture2D)frameAt.Invoke(preview, new object[] { .08f });
                Texture2D second = (Texture2D)frameAt.Invoke(preview, new object[] { .15f });
                Texture2D looped = (Texture2D)frameAt.Invoke(preview, new object[] { .35f });

                Assert.That(first, Is.Not.Null);
                Assert.That(firstAgain, Is.SameAs(first), "Frame lookup must reuse decoded cache, not decode every preview draw.");
                Assert.That(second, Is.Not.Null.And.Not.SameAs(first));
                Assert.That(looped, Is.SameAs(first));
                Assert.That(first.width, Is.EqualTo(2));
                Assert.That(first.height, Is.EqualTo(1));
                Color firstColor = first.GetPixel(0, 0);
                Color secondColor = second.GetPixel(0, 0);
                Assert.That(firstColor.r, Is.GreaterThan(.8f));
                Assert.That(firstColor.g, Is.LessThan(.2f));
                Assert.That(secondColor.g, Is.GreaterThan(.8f));
                Assert.That(secondColor.r, Is.LessThan(.2f));

                RequireInstance(previewType, "Restart").Invoke(preview, null);
                RequireInstance(previewType, "Advance", typeof(float)).Invoke(preview, new object[] { .15f });
                Assert.That(Get(preview, "currentTexture"), Is.SameAs(second));
                RequireInstance(previewType, "Dispose").Invoke(preview, null);
                Assert.That((int)Get(preview, "frameCount"), Is.EqualTo(0));
                Assert.That(Get(preview, "currentTexture"), Is.Null);

                File.Delete(path);
                object missing = open.Invoke(null, new[] { media });
                Assert.That(GetString(missing, "warning"), Does.Contain("missing").IgnoreCase);
                RequireInstance(previewType, "Dispose").Invoke(missing, null);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

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
        public void PlaybackUsesInjectableVideoFactoryForDeterministicRoutingTests()
        {
            Type editingType = RequireType("VnSceneComposerMediaEditing");
            Type factoryType = RequireType("IVnSceneComposerVideoPreviewFactory");
            PropertyInfo factory = editingType.GetProperty("VideoPreviewFactory", BindingFlags.Public | BindingFlags.Static);
            Assert.That(factory, Is.Not.Null,
                "Playback and static preview must share an editor-only injectable video factory so CI can prove routing without pretending a fake mp4 decoded.");
            Assert.That(factory.PropertyType, Is.EqualTo(factoryType));
            RequireStatic(editingType, "ResetVideoPreviewFactory");
        }

        [Test]
        public void MotionMediaContractExposesPlaybackControlsWithoutProductionImport()
        {
            Type mediaType = RequireType("VnSceneComposerMediaReference");
            Assert.That(RequirePublicField(mediaType, "loop").FieldType, Is.EqualTo(typeof(bool)));

            Type editingType = RequireType("VnSceneComposerMediaEditing");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type scaleModeType = RequireType("VnSceneComposerMediaScaleMode");
            RequireStatic(editingType, "SetExternalVideo", sceneType, typeof(string), scaleModeType, typeof(bool));
            RequireStatic(editingType, "SetExternalGif", sceneType, typeof(string), scaleModeType, typeof(bool));
            RequireStatic(editingType, "OpenVideoPreview", mediaType, typeof(int), typeof(int));
            RequireStatic(editingType, "OpenGifPreview", mediaType);

            Type video = RequireType("VnSceneComposerVideoPreview");
            RequireInstance(video, "Play");
            RequireInstance(video, "Pause");
            RequireInstance(video, "Restart");
            RequireInstance(video, "Stop");
            RequireInstance(video, "Dispose");

            Type gif = RequireType("VnSceneComposerGifPreview");
            RequireInstance(gif, "GetFrameAtTime", typeof(float));
            RequireInstance(gif, "Advance", typeof(float));
            RequireInstance(gif, "Restart");
            RequireInstance(gif, "Dispose");
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer type: " + shortName);
            return type;
        }

        private static FieldInfo RequirePublicField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + type.Name + "." + name);
            return field;
        }

        private static MethodInfo RequireStatic(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing public static method: " + type.Name + "." + name);
            return method;
        }

        private static MethodInfo RequireInstance(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                parameters,
                null);
            Assert.That(method, Is.Not.Null, "Missing instance method: " + type.Name + "." + name);
            return method;
        }

        private static FieldInfo Field(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + instance.GetType().Name + "." + name);
            return field;
        }

        private static object Get(object instance, string name) { return Field(instance, name).GetValue(instance); }
        private static string GetString(object instance, string name) { return (string)Get(instance, name); }

        private static object GetProperty(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            PropertyInfo property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property: " + instance.GetType().Name + "." + name);
            return property.GetValue(instance, null);
        }
    }
}
