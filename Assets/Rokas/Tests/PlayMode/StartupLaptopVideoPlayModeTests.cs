using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rokas.Tests
{
    public sealed class StartupLaptopVideoPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator RealStartDoesNotBuildHomeBeforeStartupVideoGate()
        {
            root = new GameObject("StartupVideoFixture");
            root.AddComponent<RokasBootstrap>();

            yield return null;

            Assert.That(Find("HomeTitle"), Is.Null,
                "The real application Start path must not construct Home before the startup preview finishes, skips, or falls back.");
            Assert.That(Find("StartupVideoSurface"), Is.Not.Null,
                "Startup must expose a black-backed video surface while the preview owns presentation startup.");
        }

        [UnityTest]
        public IEnumerator ExplicitFixtureInitializeStaysSynchronousAndVideoFree()
        {
            RokasBootstrap boot = CreateSynchronousBoot(false);
            yield return null;

            Assert.That(Find("HomeTitle"), Is.Not.Null);
            Assert.That(Find("StartupVideoSurface"), Is.Null);
            Press("LaptopHotspot");
            Assert.That(boot.View.LaptopOpen, Is.True);
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Legacy fixtures must not wait for real media unless a focused video test explicitly opts in.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null,
                "The explicit Initialize seam must preserve the existing synchronous laptop fixtures.");
        }

        [UnityTest]
        public IEnumerator LaptopOpeningShowsBootGateBeforeDesktop()
        {
            CreateSynchronousBoot(true);
            yield return null;

            Press("LaptopHotspot");
            yield return null;

            Assert.That(Find("LaptopBootSurface"), Is.Not.Null,
                "Every real Home to Laptop opening must enter the transient boot gate first.");
            Assert.That(Find("LaptopContracts"), Is.Null,
                "Laptop desktop tiles must not be constructed while the boot animation owns the screen.");
        }

        [UnityTest]
        public IEnumerator ClosingLaptopDuringBootCancelsWithoutDesktopAndReopenIsReady()
        {
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            Assert.That(root.GetComponents<VideoPlayer>().Length, Is.EqualTo(1));
            Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(1),
                "Video playback must keep one dedicated reusable AudioSource on the bootstrap root.");

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Not.Null);
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);

            Assert.That(boot.View.LaptopOpen, Is.False);
            Assert.That(Find("LaptopContracts"), Is.Null,
                "Cancelling the boot must return to Home rather than construct desktop behind the closing panel.");
            Assert.That(boot.VideoPresenter.IsPlaying, Is.False);
            Assert.That(boot.VideoPresenter.TemporaryRenderTexture, Is.Null);

            Press("LaptopHotspot");
            yield return null;
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Reopening in the same physical Home visit must not start a second boot.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null,
                "Reopening in the same physical Home visit must enter READY immediately.");
            Assert.That(root.GetComponents<VideoPlayer>().Length, Is.EqualTo(1));
            Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CommittedStartupAndStoryMediaProduceFramesThenMenuAndHomeOnce()
        {
            root = new GameObject("StartupActualMediaFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            yield return null;

            yield return WaitForFirstFrame(boot.VideoPresenter, 8f, "startup preview");
            Assert.That(boot.VideoPresenter.FirstFramePresented, Is.True,
                "The committed StartupPreview.mp4 must decode to a real first frame, not only exercise fallback.");

            boot.VideoPresenter.Skip();
            yield return null;

            Assert.That(boot.View, Is.Null,
                "Startup skip must enter the approved story intro instead of bypassing the new launch flow to Home.");
            Assert.That(Find("StoryIntroVideoSurface"), Is.Not.Null);
            Assert.That(Find("RokasMainMenu"), Is.Null);

            yield return WaitForFirstFrame(boot.VideoPresenter, 8f, "story intro");
            Assert.That(boot.VideoPresenter.FirstFramePresented, Is.True,
                "The committed StoryIntro.mp4 must decode to a real first frame with its embedded audio track available.");

            boot.VideoPresenter.Skip();
            yield return null;

            Assert.That(boot.View, Is.Null,
                "Story skip must stop at the main menu and must not carry the skip input into Enter World.");
            Assert.That(Find("StoryIntroVideoSurface"), Is.Null);
            Assert.That(Find("RokasMainMenu"), Is.Not.Null);
            Assert.That(Find("HomeTitle"), Is.Null);

            Press("EnterWorldButton");
            yield return null;

            Assert.That(boot.View, Is.Not.Null);
            Assert.That(Find("HomeTitle"), Is.Not.Null);
            Assert.That(Count("HomeTitle"), Is.EqualTo(1));
            Assert.That(Find("RokasMainMenu"), Is.Null);
            Assert.That(boot.VideoPresenter.IsPlaying, Is.False);
            Assert.That(boot.VideoPresenter.TemporaryRenderTexture, Is.Null);
        }

        [UnityTest]
        public IEnumerator CommittedLaptopMediaProducesAFrameThenDesktopAndCleansUp()
        {
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            Press("LaptopHotspot");

            yield return WaitForFirstFrame(boot.VideoPresenter, 8f, "laptop boot");
            Assert.That(boot.VideoPresenter.FirstFramePresented, Is.True,
                "The committed LaptopBoot.mp4 must decode to a real first frame, not only exercise fallback.");
            Assert.That(Find("LaptopContracts"), Is.Null);

            float deadline = Time.realtimeSinceStartup + 8f;
            while (boot.VideoPresenter.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(boot.VideoPresenter.IsPlaying, Is.False, "Laptop boot must complete through VideoPlayer lifecycle events.");
            yield return null;

            Assert.That(Find("LaptopBootSurface"), Is.Null);
            Assert.That(Find("LaptopContracts"), Is.Not.Null,
                "Desktop must be constructed only after the boot video completes or safely falls back.");
            Assert.That(boot.VideoPresenter.TemporaryRenderTexture, Is.Null);
            Assert.That(root.GetComponents<VideoPlayer>().Length, Is.EqualTo(1));
            Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(1));
        }

        private RokasBootstrap CreateSynchronousBoot(bool enableVideoTransitions)
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-video-gate-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("LaptopVideoFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory, enableVideoTransitions);
            return boot;
        }

        private static IEnumerator WaitForFirstFrame(VideoSequencePresenter presenter, float seconds, string label)
        {
            Assert.That(presenter, Is.Not.Null, "Missing video presenter for " + label);
            float deadline = Time.realtimeSinceStartup + seconds;
            while (presenter.IsPlaying && !presenter.FirstFramePresented && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private void Press(string name)
        {
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, "Missing active interaction: " + name);
            Assert.That(button.IsInteractable(), Is.True, name);
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
            if (root == null) return 0;
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
