using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        private sealed class PreparingFakeVideoPreview : VnSceneComposerVideoPreview
        {
            public int PrepareCalls;
            private bool prepared;

            public PreparingFakeVideoPreview(bool shouldLoop)
                : base(null, 16, 16, shouldLoop)
            {
                warning = string.Empty;
                texture = new RenderTexture(32, 18, 0) { name = "ROKAS_AuthoringVideo" };
                texture.Create();
            }

            public override bool IsPrepared { get { return prepared; } }
            public override bool IsPreparing { get { return false; } }
            public override bool HasVisibleFrame { get { return prepared; } }

            public override void Prepare()
            {
                PrepareCalls++;
                prepared = true;
            }
        }

        private sealed class PreparingFakeVideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public PreparingFakeVideoPreview Last;

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                Last = new PreparingFakeVideoPreview(media != null && media.loop);
                return Last;
            }
        }

        [Test]
        public void P19_DirectElementDragAndNudgeWriteSelectedComposerScopeAndDrivePreview()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Type elementType = RequireType("VnWorkshopElement");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireWindowMethod(windowType, "ComposerSetPresentationScope", typeof(bool)).Invoke(window, new object[] { false });
                object panel = Enum.Parse(elementType, "DialoguePanel");
                RequireWindowMethod(windowType, "ComposerSetSelectedPresentationElement", elementType).Invoke(window, new[] { panel });
                MethodInfo build = RequireWindowMethod(windowType, "ComposerBuildSelectedPreviewFrame");
                Rect before = (Rect)Get(build.Invoke(window, null), "DialoguePanel");

                RequireWindowMethod(windowType, "ComposerDragPresentationElement", typeof(Vector2))
                    .Invoke(window, new object[] { new Vector2(25f, 10f) });
                RequireWindowMethod(windowType, "ComposerNudgePresentationElement", typeof(Vector2), typeof(bool))
                    .Invoke(window, new object[] { Vector2.right, true });

                object active = RequireWindowMethod(windowType, "ComposerGetActivePresentationPreset").Invoke(window, null);
                object project = GetPrivateField(window, "_sceneComposerProject");
                object global = Get(project, "defaultPresentation");
                object elementOverride = global.GetType().GetMethod("GetElementOverride").Invoke(global, new[] { panel });
                Assert.That((Vector2)Get(elementOverride, "positionDelta"), Is.EqualTo(new Vector2(35f, 10f)));
                object localOverride = active.GetType().GetMethod("GetElementOverride").Invoke(active, new[] { panel });
                Assert.That((bool)Get(localOverride, "hasPositionDelta"), Is.False,
                    "Dialogue plaque geometry must not fork into the selected Scene.");
                Assert.That((Rect)Get(build.Invoke(window, null), "DialoguePanel"), Is.Not.EqualTo(before));
                Assert.That(GetPrivateField(window, "currentPreset"), Is.Not.SameAs(active),
                    "Direct Composer manipulation must target canonical Composer state, not legacy currentPreset.");
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void P19_PreviewObjectSelectionCanTargetComposerCharacterBody()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                RequireWindowMethod(windowType, "ComposerAddCharacter", typeof(string), typeof(string))
                    .Invoke(window, new object[] { "Mina", "mina_neutral" });
                object frame = RequireWindowMethod(windowType, "ComposerBuildSelectedPreviewFrame").Invoke(window, null);
                Array characters = (Array)Get(frame, "ComposerCharacters");
                Assert.That(characters, Is.Not.Null);
                Assert.That(characters.Length, Is.EqualTo(1));
                Rect body = (Rect)Get(characters.GetValue(0), "Body");

                bool selected = (bool)RequireWindowMethod(windowType, "ComposerSelectPreviewObjectAt", typeof(Vector2))
                    .Invoke(window, new object[] { body.center });
                Assert.That(selected, Is.True);
                Assert.That((int)RequireWindowMethod(windowType, "ComposerGetSelectedCharacterIndex").Invoke(window, null), Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void ExternalVideoAuthoringPrepareIsOwnedByPreviewUiPathAndUsesInjectedProvider()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            var factory = new PreparingFakeVideoFactory();
            string root = Path.Combine(Path.GetTempPath(), "rokas-authoring-video-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string videoPath = Path.Combine(root, "dummy.mp4");
            File.WriteAllBytes(videoPath, new byte[] { 1, 2, 3, 4 });
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            try
            {
                RequireWindowMethod(windowType, "ComposerAddScene").Invoke(window, null);
                object project = GetPrivateField(window, "_sceneComposerProject");
                object sceneObject = ((IList)Get(project, "scenes"))[0];
                var scene = sceneObject as VnSceneComposerScene;
                Assert.That(scene, Is.Not.Null);
                VnSceneComposerMediaEditing.SetExternalVideo(scene, videoPath, VnSceneComposerMediaScaleMode.Fit, true);

                RequireWindowMethod(windowType, "ComposerBuildSelectedPreviewPlaybackFrame").Invoke(window, null);
                Assert.That(factory.Last, Is.Not.Null);
                Assert.That(factory.Last.PrepareCalls, Is.EqualTo(0),
                    "Pure build/routing must remain decode-free; preparation belongs to the authoring preview lifecycle.");

                bool owned = (bool)RequireWindowMethod(windowType, "ComposerPrepareSelectedVideoForAuthoring").Invoke(window, null);
                Assert.That(owned, Is.True);
                Assert.That(factory.Last.PrepareCalls, Is.EqualTo(1));
                Assert.That(factory.Last.IsPrepared, Is.True);
            }
            finally
            {
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                if (factory.Last != null) factory.Last.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
