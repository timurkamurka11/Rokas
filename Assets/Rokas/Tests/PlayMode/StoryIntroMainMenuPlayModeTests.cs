using System;
using System.Collections;
using System.IO;
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

        [UnityTest]
        public IEnumerator RealLaunchPreviewCompletionStartsStoryBeforeHome()
        {
            RokasBootstrap boot = CreateRealLaunch();
            yield return WaitForFirstFrame(boot.VideoPresenter, 8f, "existing startup preview");

            boot.VideoPresenter.Skip();
            yield return null;

            Assert.That(Find("HomeTitle"), Is.Null,
                "Completing the existing Startup Preview must no longer construct Home directly.");
            Assert.That(Find("StoryIntroVideoSurface"), Is.Not.Null,
                "The story intro must become the next exclusive launch gate after the existing Startup Preview.");
            Assert.That(Find("RokasMainMenu"), Is.Null,
                "The main menu must not appear before the story intro completes or is skipped.");
        }

        [UnityTest]
        public IEnumerator StoryCompletionShowsMainMenuBeforeHome()
        {
            RokasBootstrap boot = CreateRealLaunch();
            yield return AdvancePastExistingPreview(boot);
            yield return WaitForFirstFrame(boot.VideoPresenter, 8f, "story intro");

            boot.VideoPresenter.Skip();
            yield return null;

            Assert.That(Find("HomeTitle"), Is.Null,
                "Home must stay unbuilt while the main menu owns the launch presentation.");
            Assert.That(Find("RokasMainMenu"), Is.Not.Null);
            Assert.That(Find("MainMenuVideoSurface"), Is.Not.Null,
                "The approved seamless menu loop must own the full-screen menu background.");
            Assert.That(FindButton("EnterWorldButton"), Is.Not.Null);
            Assert.That(FindButton("DevelopersButton"), Is.Not.Null);
            Assert.That(FindButton("SupportDevelopmentButton"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator EnterWorldBuildsExistingHomeExactlyOnce()
        {
            RokasBootstrap boot = CreateRealLaunch();
            yield return AdvancePastExistingPreview(boot);
            yield return WaitForFirstFrame(boot.VideoPresenter, 8f, "story intro");
            boot.VideoPresenter.Skip();
            yield return null;

            Press("EnterWorldButton");
            yield return null;

            Assert.That(Find("RokasMainMenu"), Is.Null);
            Assert.That(Find("MainMenuVideoSurface"), Is.Null);
            Assert.That(Find("HomeTitle"), Is.Not.Null,
                "Entering the world must reuse the existing Home presentation rather than construct a replacement Home.");
            Assert.That(Count("HomeTitle"), Is.EqualTo(1),
                "The main-menu handoff must build the existing Home exactly once.");
            Assert.That(boot.View, Is.Not.Null);
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

        private RokasBootstrap CreateRealLaunch()
        {
            root = new GameObject("StoryIntroMainMenuFixture");
            return root.AddComponent<RokasBootstrap>();
        }

        private static IEnumerator AdvancePastExistingPreview(RokasBootstrap boot)
        {
            yield return WaitForFirstFrame(boot.VideoPresenter, 8f, "existing startup preview");
            boot.VideoPresenter.Skip();
            yield return null;
        }

        private static IEnumerator WaitForFirstFrame(VideoSequencePresenter presenter, float seconds, string label)
        {
            Assert.That(presenter, Is.Not.Null, "Missing VideoSequencePresenter for " + label);
            float deadline = Time.realtimeSinceStartup + seconds;
            while (presenter.IsPlaying && !presenter.FirstFramePresented && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(presenter.IsPlaying, Is.True, label + " ended before exposing a decodable frame.");
            Assert.That(presenter.FirstFramePresented, Is.True, "Timed out waiting for first frame of " + label + ".");
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
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
