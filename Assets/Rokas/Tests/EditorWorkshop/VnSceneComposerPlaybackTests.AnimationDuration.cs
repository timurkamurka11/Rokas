using System;
using System.Collections;
using NUnit.Framework;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void AnimationDurationTwoSecondsReachesRendererMidpointAtOneSecond()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object project = Activator.CreateInstance(projectType);
            IList scenes = (IList)Get(project, "scenes");
            scenes.Add(Scene("Empty", string.Empty, "PreviewAutoDuration", 10f));
            object target = Scene("Timed", string.Empty, "PreviewAutoDuration", 10f);
            AddCharacter(target, "Mina", "mina_neutral", "Center");
            ConfigureCharacterTransition(target, "SlideAndFade", 2f, 2f, 180f);
            scenes.Add(target);

            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                Invoke(controllerType, controller, "Advance", typeof(float), 1f);

                object frame = Get(controller, "CurrentFrame");
                object character = ((IList)Get(frame, "ComposerCharacters"))[0];
                float alpha = (float)Get(character, "Alpha");

                Assert.That(alpha, Is.EqualTo(.5f).Within(.03f),
                    "A 2-second character transition must be at its renderer midpoint after 1 elapsed second, independent of the 10-second scene preview duration.");
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void AnimationDurationTwoSecondsCompletesBeforeTenSecondSceneEnds()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object project = Activator.CreateInstance(projectType);
            IList scenes = (IList)Get(project, "scenes");
            scenes.Add(Scene("From", string.Empty, "PreviewAutoDuration", 10f));
            object target = Scene("Target", string.Empty, "PreviewAutoDuration", 10f);
            ConfigureBackgroundTransition(target, "Fade", 2f);
            scenes.Add(target);

            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                Invoke(controllerType, controller, "Advance", typeof(float), 2f);

                object frame = Get(controller, "CurrentFrame");
                object background = Get(frame, "ComposerBackgroundTransition");

                Assert.That((bool)Get(background, "Complete"), Is.True,
                    "A 2-second background transition must complete after 2 elapsed seconds even while the 10-second scene is still playing.");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1));
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void ZeroDurationBackgroundTransitionStillSnapsImmediately()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object project = Activator.CreateInstance(projectType);
            IList scenes = (IList)Get(project, "scenes");
            scenes.Add(Scene("From", string.Empty, "PreviewAutoDuration", 10f));
            object target = Scene("Instant", string.Empty, "PreviewAutoDuration", 10f);
            ConfigureBackgroundTransition(target, "Fade", 0f);
            scenes.Add(target);

            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                object frame = Get(controller, "CurrentFrame");
                object background = Get(frame, "ComposerBackgroundTransition");

                Assert.That((bool)Get(background, "Complete"), Is.True,
                    "Zero duration must preserve explicit instant/snap semantics.");
            }
            finally { Dispose(controller); }
        }
    }
}
