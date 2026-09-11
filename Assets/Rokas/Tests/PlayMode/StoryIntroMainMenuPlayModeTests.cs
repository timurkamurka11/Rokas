using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class StoryIntroMainMenuPlayModeTests
    {
        private GameObject root;
        private string directory;
        private bool previousIgnoreFailingMessages;

        [UnityTest]
        public IEnumerator StoryPlaybackOwnsExclusiveSurfaceUntilSafeFallback()
        {
            root = new GameObject("StoryIntroPresenterFixture");
            var presenter = root.AddComponent<VideoSequencePresenter>();
            MethodInfo playStory = typeof(VideoSequencePresenter).GetMethod(
                "PlayStoryIntro", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(playStory, Is.Not.Null,
                "VideoSequencePresenter needs a dedicated story-intro entry without changing PlayStartup semantics.");

            bool completed = false;
            playStory.Invoke(presenter, new object[]
            {
                "__missing_story_intro_for_fallback_test__.mp4",
                1f,
                (Action)(() => completed = true)
            });

            Assert.That(presenter.IsPlaying, Is.True);
            Assert.That(presenter.FirstFramePresented, Is.False);
            Assert.That(Find("StoryIntroVideoSurface"), Is.Not.Null,
                "The story intro must own its own full-screen surface before the menu exists.");
            Assert.That(completed, Is.False);

            yield return new WaitForSecondsRealtime(.12f);

            Assert.That(completed, Is.True,
                "Missing/corrupt story media must fail safely into the next launch stage.");
            Assert.That(Find("StoryIntroVideoSurface"), Is.Null,
                "The story surface must be cleaned by the presenter's terminal path.");
        }

        [UnityTest]
        public IEnumerator RealLaunchFallsThroughStoryToMainMenuBeforeHome()
        {
            RokasBootstrap boot = CreateRealLaunchIgnoringHostDecoderErrors();
            yield return WaitFor("RokasMainMenu", 1.5f);

            Assert.That(Find("HomeTitle"), Is.Null,
                "Startup Preview and Story Intro must finish into the menu, not directly into Home.");
            Assert.That(Find("RokasMainMenu"), Is.Not.Null);
            Assert.That(Find("MainMenuVideoSurface"), Is.Not.Null,
                "The approved seamless loop must own the menu background even when this Linux host cannot decode it.");
            Assert.That(FindButton("EnterWorldButton"), Is.Not.Null);
            Assert.That(FindButton("DevelopersButton"), Is.Not.Null);
            Assert.That(FindButton("SupportDevelopmentButton"), Is.Not.Null);
            Assert.That(boot.View, Is.Null,
                "RokasView/Home must not be constructed behind the launch menu.");
        }

        [UnityTest]
        public IEnumerator EnterWorldBuildsExistingHomeExactlyOnce()
        {
            RokasBootstrap boot = CreateRealLaunchIgnoringHostDecoderErrors();
            yield return WaitFor("RokasMainMenu", 1.5f);

            Press("EnterWorldButton");
            yield return null;

            Assert.That(Find("RokasMainMenu"), Is.Null);
            Assert.That(Find("MainMenuVideoSurface"), Is.Null);
            Assert.That(Find("HomeTitle"), Is.Not.Null,
                "Entering the world must reuse the existing Home presentation rather than construct a replacement Home.");
            Assert.That(Count("HomeTitle"), Is.EqualTo(1),
                "The main-menu handoff must build the existing Home exactly once.");
            Assert.That(boot.View, Is.Not.Null);

            Button staleButton = FindButton("EnterWorldButton");
            Assert.That(staleButton, Is.Null,
                "The consumed main menu must not leave a second active entry path to Home.");
        }

        [UnityTest]
        public IEnumerator ExplicitFixtureInitializeRemainsSynchronousAndLaunchMenuFree()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-story-menu-fixture-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("StoryMenuSynchronousFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory, false);
            yield return null;

            Assert.That(Find("HomeTitle"), Is.Not.Null,
                "Existing synchronous fixtures must continue to build Home directly.");
            Assert.That(Find("StoryIntroVideoSurface"), Is.Null);
            Assert.That(Find("RokasMainMenu"), Is.Null);
            Assert.That(Find("MainMenuVideoSurface"), Is.Null);
        }

        private RokasBootstrap CreateRealLaunchIgnoringHostDecoderErrors()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            root = new GameObject("StoryIntroMainMenuFixture");
            return root.AddComponent<RokasBootstrap>();
        }

        private IEnumerator WaitFor(string objectName, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Find(objectName) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Find(objectName), Is.Not.Null,
                "Timed out waiting for launch object: " + objectName + ".");
        }

        private void Press(string name)
        {
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, "Missing active interaction: " + name);
            Assert.That(button.IsInteractable(), Is.True, name + " must be interactable.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private Button FindButton(string name)
        {
            if (root == null) return null;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            return null;
        }

        private GameObject Find(string name)
        {
            if (root == null) return null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        private int Count(string name)
        {
            int count = 0;
            if (root == null) return count;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) count++;
            return count;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
