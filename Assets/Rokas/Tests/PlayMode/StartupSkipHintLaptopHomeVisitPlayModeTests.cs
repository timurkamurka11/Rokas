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
    public sealed class StartupSkipHintLaptopHomeVisitPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator StartupPreviewBuildsSkipHintOverlayBeforeHome()
        {
            root = new GameObject("StartupSkipHintFixture");
            var presenter = root.AddComponent<VideoSequencePresenter>();
            presenter.PlayStartup("StartupPreview.mp4", 0f, () => { });

            GameObject startupCanvas = Find("StartupVideoCanvas");
            GameObject hint = Find("StartupSkipHintOverlay");
            Assert.That(startupCanvas, Is.Not.Null);
            Assert.That(hint, Is.Not.Null,
                "Startup preview must build the approved skip-hint overlay on its transient startup canvas.");
            Assert.That(hint.transform.IsChildOf(startupCanvas.transform), Is.True);
            var hintImage = hint.GetComponent<RawImage>();
            Assert.That(hintImage, Is.Not.Null);
            Assert.That(hintImage.texture, Is.Not.Null,
                "The approved attached PNG must be bound to the startup skip hint.");
            Assert.That(hintImage.raycastTarget, Is.False);
            Assert.That(hintImage.color.a, Is.EqualTo(0f).Within(.001f),
                "The hint must remain hidden until the first real video frame is presented.");

            presenter.Cancel();
            yield return null;
            Assert.That(Find("StartupSkipHintOverlay"), Is.Null,
                "The startup overlay must be destroyed through the existing video cleanup path.");
        }

        [UnityTest]
        public IEnumerator ClosingFirstLaptopBootConsumesCurrentHomeVisitAndReopenIsReady()
        {
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Not.Null);
            Assert.That(Find("LaptopContracts"), Is.Null);

            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(boot.View.LaptopOpen, Is.False);

            Press("LaptopHotspot");
            yield return null;
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Closing during the first boot still consumes that Home visit's one boot.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null,
                "Reopening in the same Home visit must enter READY immediately.");
        }

        [UnityTest]
        public IEnumerator HomeInternalPhaseChangeDoesNotResetLaptopBootEligibility()
        {
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            Press("LaptopHotspot");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.State.phase, Is.EqualTo(Rokas.Core.RunPhase.Accepted));

            Press("LaptopHotspot");
            yield return null;
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Accepting a contract while still physically at Home must not start a new Home visit.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LeavingAndReturningHomeRestoresOneLaptopBoot()
        {
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            Press("LaptopHotspot");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);

            Press("LaptopHotspot");
            yield return null;
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "The current Home visit must already be consumed before testing the reset boundary.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            Assert.That(boot.Session.State.phase, Is.EqualTo(Rokas.Core.RunPhase.Portal));
            Assert.That(boot.Session.ReturnHome(), Is.True);
            Assert.That(boot.Session.State.phase, Is.EqualTo(Rokas.Core.RunPhase.Home));
            yield return null;

            Press("LaptopHotspot");
            yield return null;
            Assert.That(Find("LaptopBootSurface"), Is.Not.Null,
                "Returning from a non-Home runtime phase must start a new Home visit with one fresh boot.");
            Assert.That(Find("LaptopContracts"), Is.Null);
        }

        private RokasBootstrap CreateSynchronousBoot(bool enableVideoTransitions)
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-home-visit-video-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("HomeVisitVideoFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory, enableVideoTransitions);
            return boot;
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

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
