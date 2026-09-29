using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerAnimationScopeTests
    {
        [Test]
        public void IncomingBoundaryScopeKeepsLocalScenesSeparateAndPropagatesGlobalEdits()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerAddScene();
                VnSceneComposerProject project = Project(window);

                window.ComposerSelectScene(0);
                window.ComposerSetPresentationScope(false);
                project.scenes[0].transition.triggerActionBounce = true;
                window.ComposerSetSelectedSceneBoundaryTransition(
                    VnSceneComposerSceneTransitionType.Fade,
                    VnSceneComposerSceneTransitionDirection.LeftToRight, .65f);
                Assert.That(project.scenes[1].transition.sceneTransitionType,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.None));

                window.ComposerSelectScene(1);
                window.ComposerSetSelectedSceneBoundaryTransition(
                    VnSceneComposerSceneTransitionType.DarkCurtain,
                    VnSceneComposerSceneTransitionDirection.RightToLeft, .75f);
                Assert.That(project.scenes[0].transition.sceneTransitionType,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.Fade));
                Assert.That(project.scenes[0].transition.sceneTransitionDuration,
                    Is.EqualTo(.65f).Within(.001f));

                window.ComposerSetPresentationScope(true);
                window.ComposerSetSelectedSceneBoundaryTransition(
                    VnSceneComposerSceneTransitionType.Fade,
                    VnSceneComposerSceneTransitionDirection.RightToLeft, 1.25f);
                Assert.That(project.defaultSceneTransition.sceneTransitionType,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.Fade));
                foreach (VnSceneComposerScene scene in project.scenes)
                {
                    Assert.That(scene.transition.sceneTransitionType,
                        Is.EqualTo(VnSceneComposerSceneTransitionType.Fade));
                    Assert.That(scene.transition.sceneTransitionDuration,
                        Is.EqualTo(1.25f).Within(.001f));
                }
                Assert.That(project.scenes[0].transition.triggerActionBounce, Is.True,
                    "Global boundary editing must leave the independent accent trigger alone.");

                window.ComposerAddScene();
                Assert.That(project.scenes[2].transition.sceneTransitionType,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.Fade),
                    "New scenes inherit the authoritative global boundary setting.");

                window.ComposerSelectScene(1);
                window.ComposerSetPresentationScope(false);
                window.ComposerSetSelectedSceneBoundaryTransition(
                    VnSceneComposerSceneTransitionType.None,
                    VnSceneComposerSceneTransitionDirection.LeftToRight, 0f);
                Assert.That(project.scenes[0].transition.sceneTransitionType,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.Fade));
                Assert.That(project.scenes[1].transition.sceneTransitionType,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.None));
                Assert.That(project.defaultSceneTransition.sceneTransitionType,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.Fade));

                VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(
                    VnSceneComposerSerialization.SerializePortable(project));
                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(loaded.Project.defaultSceneTransition.sceneTransitionType,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.Fade));
                Assert.That(loaded.Project.scenes[1].transition.sceneTransitionType,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.None));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void GlobalSceneAnimationEditsReplaceConflictingPresentationOverrides()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerAddScene();
                VnSceneComposerProject project = Project(window);
                window.ComposerSelectScene(0);
                window.ComposerSetPresentationScope(false);

                VnPresentationWorkshopPreset local = VnSceneComposerComposition.ResolvePresentation(
                    project, project.scenes[0]);
                VnWorkshopBackgroundTransitionValues background =
                    VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(local);
                window.ComposerSetBackgroundTransition(VnWorkshopBackgroundTransitionMode.Curtain,
                    2f, background.CurtainDarkness, background.Direction, background.Easing);
                VnWorkshopStageLayoutValues stage = VnPresentationWorkshopVn10Resolver.ResolveStageLayout(local);
                window.ComposerSetStageLayout(stage.LeftX, stage.CenterX, stage.RightX, stage.SlotY,
                    stage.LeftScale, stage.CenterScale, stage.RightScale, stage.Spacing, 2f, stage.Easing);
                VnWorkshopSpeakerFocusValues focus = VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(local);
                window.ComposerSetSpeakerFocus(focus.ActiveScale, focus.ActiveBrightness,
                    focus.ActiveForwardOffset, focus.InactiveScale, .3f, focus.InactiveAlpha,
                    focus.TransitionDuration, focus.Easing);
                VnWorkshopTimingValues timing = VnPresentationWorkshopVn10Resolver.ResolveTiming(local);
                window.ComposerSetTiming(timing.MinimumBeatSettleDuration,
                    timing.PostTransitionBreathingRoom, 2f);

                VnPresentationWorkshopPreset other = VnSceneComposerComposition.ResolvePresentation(
                    project, project.scenes[1]);
                Assert.That(VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(other).Mode,
                    Is.Not.EqualTo(VnWorkshopBackgroundTransitionMode.Curtain));
                Assert.That(VnPresentationWorkshopVn10Resolver.ResolveStageLayout(other).RepositionDuration,
                    Is.Not.EqualTo(2f));
                Assert.That(VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(other).InactiveBrightness,
                    Is.Not.EqualTo(.3f));
                Assert.That(VnPresentationWorkshopVn10Resolver.ResolveTiming(other).AutoPreviewSequenceGap,
                    Is.Not.EqualTo(2f));

                window.ComposerSetPresentationScope(true);
                window.ComposerSetBackgroundTransition(VnWorkshopBackgroundTransitionMode.Instant,
                    .8f, background.CurtainDarkness, background.Direction, background.Easing);
                window.ComposerSetStageLayout(stage.LeftX, stage.CenterX, stage.RightX, stage.SlotY,
                    stage.LeftScale, stage.CenterScale, stage.RightScale, stage.Spacing, .8f, stage.Easing);
                window.ComposerSetSpeakerFocus(focus.ActiveScale, focus.ActiveBrightness,
                    focus.ActiveForwardOffset, focus.InactiveScale, .5f, focus.InactiveAlpha,
                    focus.TransitionDuration, focus.Easing);
                window.ComposerSetTiming(timing.MinimumBeatSettleDuration,
                    timing.PostTransitionBreathingRoom, .8f);

                foreach (VnSceneComposerScene scene in project.scenes)
                {
                    VnPresentationWorkshopPreset resolved = VnSceneComposerComposition.ResolvePresentation(
                        project, scene);
                    Assert.That(VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(resolved).Mode,
                        Is.EqualTo(VnWorkshopBackgroundTransitionMode.Instant));
                    Assert.That(VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(resolved).Duration,
                        Is.EqualTo(.8f).Within(.001f));
                    Assert.That(VnPresentationWorkshopVn10Resolver.ResolveStageLayout(resolved).RepositionDuration,
                        Is.EqualTo(.8f).Within(.001f));
                    Assert.That(VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(resolved).InactiveBrightness,
                        Is.EqualTo(.5f).Within(.001f));
                    Assert.That(VnPresentationWorkshopVn10Resolver.ResolveTiming(resolved).AutoPreviewSequenceGap,
                        Is.EqualTo(.8f).Within(.001f));
                }
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SceneAdvanceTimingUsesTheSameLocalAndGlobalScope()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerAddScene();
                VnSceneComposerProject project = Project(window);
                window.ComposerSetPresentationScope(false);
                window.ComposerSelectScene(0);
                window.ComposerSetSelectedSceneAdvanceTiming(
                    VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration, 4f);
                Assert.That(project.scenes[1].timing.previewAdvanceMode,
                    Is.EqualTo(VnSceneComposerPreviewAdvanceMode.ManualBeat));

                window.ComposerSelectScene(1);
                window.ComposerSetSelectedSceneAdvanceTiming(
                    VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration, 7f);
                Assert.That(project.scenes[0].timing.previewAutoDuration,
                    Is.EqualTo(4f).Within(.001f));

                window.ComposerSetPresentationScope(true);
                window.ComposerSetSelectedSceneAdvanceTiming(
                    VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration, 3f);
                Assert.That(project.defaultSceneTiming.previewAutoDuration,
                    Is.EqualTo(3f).Within(.001f));
                foreach (VnSceneComposerScene scene in project.scenes)
                    Assert.That(scene.timing.previewAutoDuration, Is.EqualTo(3f).Within(.001f));

                window.ComposerAddScene();
                Assert.That(project.scenes[2].timing.previewAutoDuration,
                    Is.EqualTo(3f).Within(.001f));
                VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(
                    VnSceneComposerSerialization.SerializePortable(project));
                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(loaded.Project.defaultSceneTiming.previewAutoDuration,
                    Is.EqualTo(3f).Within(.001f));
            }
            finally
            {
                Undo.ClearAll();
                Object.DestroyImmediate(window);
            }
        }

        private static VnSceneComposerProject Project(VnPresentationWorkshopWindow window)
        {
            FieldInfo field = typeof(VnPresentationWorkshopWindow).GetField(
                "_sceneComposerProject", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (VnSceneComposerProject)field.GetValue(window);
        }
    }
}
