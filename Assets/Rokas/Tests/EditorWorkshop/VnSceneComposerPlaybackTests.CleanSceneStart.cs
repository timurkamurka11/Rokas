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
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                ManualInvoke(windowType, window, "ComposerAddScene");
                IList scenes = (IList)Get(ManualGetPrivateField(window, "_sceneComposerProject"), "scenes");
                Texture2D previousBackground = AssignKnownBackground(scenes[0]);
                ManualInvoke(windowType, window, "ComposerAddScene");
                ManualRequireInstance(windowType, "ComposerSelectScene", typeof(int))
                    .Invoke(window, new object[] { 1 });

                ManualInvoke(windowType, window, "ComposerPlayScene");
                object controller = ManualGetPrivateField(window, "_sceneComposerPlayback");
                object frame = Get(controller, "CurrentFrame");
                Texture source = (Texture)Get(frame, "SourceBackground");

                Assert.That(source, Is.Not.SameAs(previousBackground),
                    "The user-facing isolated Play Scene command must not source scene N-1, otherwise the previous scene can flash first.");
                Assert.That(source, Is.SameAs(Texture2D.blackTexture),
                    "The user-facing isolated Play Scene command should transition from the neutral black preview baseline.");
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void CleanSceneStartUserPlaySceneFirstVisibleBackgroundDoesNotExposeNeutralBlack()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Type playbackFrameType = RequireType("VnSceneComposerPlaybackFrame");
            Type rendererType = RequireType("VnPresentationWorkshopPreviewRenderer");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                ManualInvoke(windowType, window, "ComposerAddScene");
                IList scenes = (IList)Get(ManualGetPrivateField(window, "_sceneComposerProject"), "scenes");
                AssignKnownBackground(scenes[0]);

                ManualInvoke(windowType, window, "ComposerPlayScene");
                object controller = ManualGetPrivateField(window, "_sceneComposerPlayback");
                object frame = Get(controller, "CurrentFrame");
                Assert.That(Get(frame, "SourceBackground"), Is.SameAs(Texture2D.blackTexture),
                    "Neutral source selection must remain intact; this regression is about what becomes visible.");

                Texture2D composed = (Texture2D)RequireStatic(rendererType, "ComposePlaybackBackground",
                        playbackFrameType, typeof(int), typeof(int))
                    .Invoke(null, new object[] { frame, 32, 18 });
                try
                {
                    Color32[] pixels = composed.GetPixels32();
                    bool anyVisibleTarget = Array.Exists(pixels,
                        pixel => pixel.r > 2 || pixel.g > 2 || pixel.b > 2);
                    Assert.That(anyVisibleTarget, Is.True,
                        "The first user-visible Play Scene background must already show the authored target instead of exposing the neutral black transition source.");
                }
                finally { UnityEngine.Object.DestroyImmediate(composed); }
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void CleanSceneStartUserPlayAllFirstVisibleBackgroundDoesNotExposeNeutralBlack()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Type playbackFrameType = RequireType("VnSceneComposerPlaybackFrame");
            Type rendererType = RequireType("VnPresentationWorkshopPreviewRenderer");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                ManualInvoke(windowType, window, "ComposerAddScene");
                IList scenes = (IList)Get(ManualGetPrivateField(window, "_sceneComposerProject"), "scenes");
                AssignKnownBackground(scenes[0]);
                ManualInvoke(windowType, window, "ComposerAddScene");
                scenes = (IList)Get(ManualGetPrivateField(window, "_sceneComposerProject"), "scenes");
                SetSceneBackground(scenes[1], LoadRokasTexture("vnNightSkyRain"));
                ManualRequireInstance(windowType, "ComposerSelectScene", typeof(int))
                    .Invoke(window, new object[] { 1 });

                ManualInvoke(windowType, window, "ComposerPlayAll");
                object controller = ManualGetPrivateField(window, "_sceneComposerPlayback");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(0));
                object frame = Get(controller, "CurrentFrame");
                Assert.That(Get(frame, "SourceBackground"), Is.SameAs(Texture2D.blackTexture),
                    "Play All scene zero keeps the neutral baseline source; the bug is exposing that source to the user.");

                Texture2D composed = (Texture2D)RequireStatic(rendererType, "ComposePlaybackBackground",
                        playbackFrameType, typeof(int), typeof(int))
                    .Invoke(null, new object[] { frame, 32, 18 });
                try
                {
                    Color32[] pixels = composed.GetPixels32();
                    bool anyVisibleTarget = Array.Exists(pixels,
                        pixel => pixel.r > 2 || pixel.g > 2 || pixel.b > 2);
                    Assert.That(anyVisibleTarget, Is.True,
                        "The first user-visible Play All frame must show scene one immediately instead of fading from the neutral black baseline.");
                }
                finally { UnityEngine.Object.DestroyImmediate(composed); }
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void CleanSceneStartUserPreviousToFirstSceneDoesNotExposeNeutralBlack()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Type playbackFrameType = RequireType("VnSceneComposerPlaybackFrame");
            Type rendererType = RequireType("VnPresentationWorkshopPreviewRenderer");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                ManualInvoke(windowType, window, "ComposerAddScene");
                IList scenes = (IList)Get(ManualGetPrivateField(window, "_sceneComposerProject"), "scenes");
                AssignKnownBackground(scenes[0]);
                ManualInvoke(windowType, window, "ComposerAddScene");
                scenes = (IList)Get(ManualGetPrivateField(window, "_sceneComposerProject"), "scenes");
                SetSceneBackground(scenes[1], LoadRokasTexture("vnNightSkyRain"));

                ManualInvoke(windowType, window, "ComposerPlayAll");
                ManualInvoke(windowType, window, "ComposerNext");
                object controller = ManualGetPrivateField(window, "_sceneComposerPlayback");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1));

                ManualInvoke(windowType, window, "ComposerPrevious");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(0));
                object frame = Get(controller, "CurrentFrame");
                Assert.That(Get(frame, "SourceBackground"), Is.SameAs(Texture2D.blackTexture),
                    "Previous-to-first keeps scene zero's neutral baseline source; the bug is exposing it as a transport flash.");

                Texture2D composed = (Texture2D)RequireStatic(rendererType, "ComposePlaybackBackground",
                        playbackFrameType, typeof(int), typeof(int))
                    .Invoke(null, new object[] { frame, 32, 18 });
                try
                {
                    Color32[] pixels = composed.GetPixels32();
                    bool anyVisibleTarget = Array.Exists(pixels,
                        pixel => pixel.r > 2 || pixel.g > 2 || pixel.b > 2);
                    Assert.That(anyVisibleTarget, Is.True,
                        "Previous to the first scene must show the destination background immediately instead of flashing the neutral black baseline.");
                }
                finally { UnityEngine.Object.DestroyImmediate(composed); }
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
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

        [Test]
        public void CleanSceneStartPlayFromHereStartsNeutralThenUsesRealPreviousSceneAtBoundary()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                ManualInvoke(windowType, window, "ComposerAddScene");
                object project = ManualGetPrivateField(window, "_sceneComposerProject");
                SetAutoPreviewSequenceGap(project, 0f);
                IList scenes = (IList)Get(project, "scenes");
                Texture2D firstBackground = LoadRokasTexture("vnBusStopPhoneMessageMina");
                SetSceneBackground(scenes[0], firstBackground);

                ManualInvoke(windowType, window, "ComposerAddScene");
                scenes = (IList)Get(project, "scenes");
                object second = scenes[1];
                Texture2D secondBackground = LoadRokasTexture("vnNightSkyRain");
                SetSceneBackground(second, secondBackground);
                object secondTiming = Get(second, "timing");
                SetEnum(secondTiming, "previewAdvanceMode", "PreviewAutoDuration");
                Set(secondTiming, "previewAutoDuration", .1f);

                ManualInvoke(windowType, window, "ComposerAddScene");
                ManualRequireInstance(windowType, "ComposerSelectScene", typeof(int))
                    .Invoke(window, new object[] { 1 });
                ManualInvoke(windowType, window, "ComposerPlayFromHere");

                object controller = ManualGetPrivateField(window, "_sceneComposerPlayback");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1));
                object initialFrame = Get(controller, "CurrentFrame");
                Assert.That(Get(initialFrame, "SourceBackground"), Is.SameAs(Texture2D.blackTexture),
                    "The user-facing Play From Here command must start selected scene N from a neutral baseline instead of flashing scene N-1.");

                Type controllerType = RequirePlaybackType();
                Invoke(controllerType, controller, "Advance", typeof(float), .2f);
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(2));
                object boundaryFrame = Get(controller, "CurrentFrame");
                Assert.That(Get(boundaryFrame, "SourceBackground"), Is.SameAs(secondBackground),
                    "After the isolated Play From Here start, the real scene N -> scene N+1 boundary must restore the authored previous-scene source.");
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
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
