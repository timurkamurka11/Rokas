using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        private const string CleanSceneBackgroundPath =
            "Assets/Rokas/Art/VN/Backgrounds/VN_BusStop_Rain_Night.png";

        [Test]
        public void CleanSceneStartEmptySceneUsesBlackInsteadOfLegacyWorkshopBackground()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type resolutionType = RequireType("VnWorkshopResolution");
            Type compositionType = RequireType("VnSceneComposerComposition");
            object project = Activator.CreateInstance(projectType);
            object scene = Activator.CreateInstance(sceneType);
            MethodInfo build = RequireStatic(compositionType, "BuildFrame",
                projectType, sceneType, resolutionType, typeof(Texture2D));

            object frame = build.Invoke(null, new object[]
            {
                project,
                scene,
                Enum.Parse(resolutionType, "Reference1920x1080"),
                null
            });

            Assert.That(Get(frame, "BackgroundTexture"), Is.SameAs(Texture2D.blackTexture),
                "A scene with no authored background/media must render black, not a hidden Workshop/BusStopKeiko background.");
        }

        [Test]
        public void CleanSceneStartPlaySceneDoesNotUsePreviousSceneAsTransitionSource()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object project = Activator.CreateInstance(projectType);
            IList scenes = (IList)Get(project, "scenes");
            object previous = Scene("Previous", "previous", "ManualBeat", 1f);
            Texture2D previousBackground = AssignKnownBackground(previous);
            object target = Scene("Target", "target", "ManualBeat", 1f);
            scenes.Add(previous);
            scenes.Add(target);

            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                object frame = Get(controller, "CurrentFrame");
                Texture source = (Texture)Get(frame, "SourceBackground");

                Assert.That(source, Is.Not.SameAs(previousBackground),
                    "Isolated Play Scene must not source scene N-1, otherwise the previous scene can flash first.");
                Assert.That(source, Is.SameAs(Texture2D.blackTexture),
                    "Isolated Play Scene should transition from the neutral black preview baseline.");
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void CleanSceneStartPlayAllPreservesRealPreviousSceneTransitionSource()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object project = Activator.CreateInstance(projectType);
            SetAutoPreviewSequenceGap(project, 0f);
            IList scenes = (IList)Get(project, "scenes");
            object first = Scene("First", "first", "PreviewAutoDuration", .1f);
            Texture2D firstBackground = AssignKnownBackground(first);
            object second = Scene("Second", "second", "ManualBeat", 1f);
            scenes.Add(first);
            scenes.Add(second);

            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayAll");
                Invoke(controllerType, controller, "Advance", typeof(float), .2f);

                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1));
                object frame = Get(controller, "CurrentFrame");
                Assert.That(Get(frame, "SourceBackground"), Is.SameAs(firstBackground),
                    "Ordered Play All must preserve the genuine scene 1 -> scene 2 transition source.");
            }
            finally { Dispose(controller); }
        }

        private static Texture2D AssignKnownBackground(object scene)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CleanSceneBackgroundPath);
            Assert.That(texture, Is.Not.Null, "Known VN background must exist for M-B playback-source proof.");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type editingType = RequireType("VnSceneComposerMediaEditing");
            Type scaleModeType = RequireType("VnSceneComposerMediaScaleMode");
            RequireStatic(editingType, "SetExistingRokasAsset",
                    sceneType, typeof(UnityEngine.Object), scaleModeType)
                .Invoke(null, new object[] { scene, texture, Enum.Parse(scaleModeType, "Fill") });
            return texture;
        }
    }
}
