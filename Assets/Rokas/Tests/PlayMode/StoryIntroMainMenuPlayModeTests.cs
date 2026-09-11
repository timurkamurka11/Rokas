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
        private string storyMediaPath;
        private string hiddenStoryMediaPath;
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
        public IEnumerator StorySkipIsBlockedUntilFirstFrameThenUsesSameCompletionPath()
        {
            root = new GameObject("StoryIntroSkipFixture");
            var presenter = root.AddComponent<VideoSequencePresenter>();
            bool completed = false;
            presenter.PlayStoryIntro("__missing_story_skip_gate_test__.mp4", 1f, () => completed = true);

            Assert.That(presenter.IsPlaying, Is.True);
            presenter.Skip();
            Assert.That(completed, Is.False,
                "Story skip must be ignored before the first decoded frame is safely presented.");
            Assert.That(presenter.IsPlaying, Is.True);

            FieldInfo firstFrame = typeof(VideoSequencePresenter).GetField(
                "firstFramePresented", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(firstFrame, Is.Not.Null);
            firstFrame.SetValue(presenter, true);

            presenter.Skip();

            Assert.That(completed, Is.True,
                "After first-frame gating, story skip must use the exact same completion callback as natural completion.");
            Assert.That(presenter.IsPlaying, Is.False);
            yield return null;
            Assert.That(Find("StoryIntroVideoSurface"), Is.Null);
        }

        [UnityTest]
        public IEnumerator RealLaunchFallsThroughStoryToMainMenuBeforeHome()
        {
            HideStoryMediaForDeterministicLinuxFallback();
            RokasBootstrap boot = CreateRealLaunchIgnoringHostDecoderErrors();
            yield return WaitFor("RokasMainMenu", 1.5f);

            Assert.That(Find("HomeTitle"), Is.Null,
                "Startup Preview and Story Intro must finish into the menu, not directly into Home.");
            Assert.That(Find("RokasMainMenu"), Is.Not.Null);
            Assert.That(Find("MainMenuVideoSurface"), Is.Not.Null,
                "The approved seamless loop must own the full-screen menu background.");
            Assert.That(FindButton("EnterWorldButton"), Is.Not.Null);
            Assert.That(FindButton("DevelopersButton"), Is.Not.Null);
            Assert.That(FindButton("SupportDevelopmentButton"), Is.Not.Null);
            Assert.That(boot.View, Is.Null,
                "RokasView/Home must not be constructed behind the launch menu.");
        }

        [UnityTest]
        public IEnumerator SecondaryButtonsAreRealRaycastableNoOps()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            root = new GameObject("MainMenuInteractionFixture");
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            int entered = 0;
            MainMenuView.Create(root.transform, assets, 0f, null, () => entered++);
            yield return null;

            foreach (string name in new[] { "EnterWorldButton", "DevelopersButton", "SupportDevelopmentButton" })
            {
                Button button = FindButton(name);
                Assert.That(button, Is.Not.Null, "Missing real Unity Button: " + name);
                Assert.That(button.targetGraphic, Is.Not.Null, name + " needs a Graphic for actual UI raycasting.");
                Assert.That(button.targetGraphic.raycastTarget, Is.True,
                    name + " must expose a live raycast target; direct ExecuteEvents-only buttons are not acceptable.");
            }

            Press("DevelopersButton");
            Press("SupportDevelopmentButton");
            yield return null;

            Assert.That(entered, Is.EqualTo(0), "Secondary actions must remain safe no-ops for this milestone.");
            Assert.That(Find("RokasMainMenu"), Is.Not.Null,
                "About and Support must leave the user on the same menu.");
            Assert.That(Find("HomeTitle"), Is.Null);
        }

        [UnityTest]
        public IEnumerator EnterWorldBuildsExistingHomeExactlyOnce()
        {
            HideStoryMediaForDeterministicLinuxFallback();
            RokasBootstrap boot = CreateRealLaunchIgnoringHostDecoderErrors();
            yield return WaitFor("RokasMainMenu", 1.5f);

            Press("EnterWorldButton");
            yield return null;

            Assert.That(Find("RokasMainMenu"), Is.Not.Null,
                "Enter World must keep the launch menu beneath the fade until the full-black handoff.");
            Assert.That(Find("HomeTitle"), Is.Null,
                "Home must not be constructed during the initial partial fade-to-black frames.");

            float deadline = Time.realtimeSinceStartup + 1f;
            while (Find("HomeTitle") == null && Time.realtimeSinceStartup < deadline) yield return null;

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

        private void HideStoryMediaForDeterministicLinuxFallback()
        {
            storyMediaPath = Path.Combine(Application.streamingAssetsPath, "RokasVideo", "StoryIntro.mp4");
            Assert.That(File.Exists(storyMediaPath), Is.True,
                "Focused CI verifies the real StoryIntro.mp4 before tests; this helper only hides it from Linux VideoPlayer.");
            hiddenStoryMediaPath = Path.Combine(Path.GetTempPath(), "rokas-hidden-story-" + Guid.NewGuid().ToString("N") + ".mp4");
            File.Move(storyMediaPath, hiddenStoryMediaPath);
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
            if (!string.IsNullOrEmpty(hiddenStoryMediaPath) && File.Exists(hiddenStoryMediaPath) &&
                !string.IsNullOrEmpty(storyMediaPath) && !File.Exists(storyMediaPath))
                File.Move(hiddenStoryMediaPath, storyMediaPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
