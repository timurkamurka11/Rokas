using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
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
        private string hiddenLaptopMediaPath;
        private string hiddenLaptopBackupPath;

        [UnityTest]
        public IEnumerator StartupPreviewBuildsSkipHintOverlayBeforeHome()
        {
            root = new GameObject("StartupSkipHintFixture");
            var presenter = root.AddComponent<VideoSequencePresenter>();
            presenter.PlayStartup("__missing_startup_hint_test__.mp4", 0f, () => { });

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
            HideLaptopMedia();
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Not.Null);
            Assert.That(Find("LaptopContracts"), Is.Null);

            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(boot.View.LaptopOpen, Is.False);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Closing during the first boot still consumes that Home visit's one boot.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null,
                "Reopening in the same Home visit must enter READY immediately.");
        }

        [UnityTest]
        public IEnumerator HomeInternalPanelRoundTripDoesNotResetLaptopBootEligibility()
        {
            HideLaptopMedia();
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            Press("LaptopHotspot");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);

            Press("TeaHotspot");
            Assert.That(boot.View.LaptopOpen, Is.False);
            boot.View.Escape();
            yield return null;

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Opening and closing an internal Home panel must not start a new Home visit.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LeavingAndReturningHomeRestoresOneLaptopBoot()
        {
            HideLaptopMedia();
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            Press("LaptopHotspot");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "The current Home visit must already be consumed before testing the reset boundary.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Portal));
            Assert.That(boot.Session.ReturnHome(), Is.True);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            yield return null;

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Not.Null,
                "Returning from a non-Home runtime phase must start a new Home visit with one fresh boot.");
            Assert.That(Find("LaptopContracts"), Is.Null);
        }

        [UnityTest]
        public IEnumerator AcceptingContractInsideConsumedHomeVisitKeepsLaptopReady()
        {
            HideLaptopMedia();
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            yield return ConsumeInitialLaptopBoot(boot);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null);
            Press("LaptopContracts");
            Press("AcceptContract");
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Accepted));
            CloseLaptopFromSection(boot);
            yield return new WaitForSecondsRealtime(.3f);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Accepting a contract while physically at Home must not create a new Home visit.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null,
                "Laptop must reopen directly READY after Home -> Accepted.");
        }

        [UnityTest]
        public IEnumerator AcceptedToHomeContractStateChangeInsideVisitKeepsLaptopReady()
        {
            HideLaptopMedia();
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            yield return ConsumeInitialLaptopBoot(boot);

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Accepted));
            yield return null;
            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null);
            Press("LaptopContracts");
            Press("CancelContract");
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            CloseLaptopFromSection(boot);
            yield return new WaitForSecondsRealtime(.3f);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Accepted -> Home contract state change at the same physical location must preserve consumed boot.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PhysicalLeaveAndReturnCreatesExactlyOneFreshLaptopBoot()
        {
            HideLaptopMedia();
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;
            yield return ConsumeInitialLaptopBoot(boot);

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Portal));
            Assert.That(boot.Session.ReturnHome(), Is.True);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            yield return null;

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Not.Null,
                "A real outside -> Home return must restore exactly one Laptop boot.");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "After consuming the return visit boot, the second open must be READY.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ClaimingPaymentInsideConsumedHomeVisitDoesNotCreateThirdVisit()
        {
            HideLaptopMedia();
            RokasBootstrap boot = CreatePaymentHomeVisitBoot(true);
            yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Payment));
            yield return ConsumeInitialLaptopBoot(boot);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null);
            Press("LaptopContracts");
            Press("ClaimPayment");
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            CloseLaptopFromSection(boot);
            yield return new WaitForSecondsRealtime(.3f);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Payment -> Home reward claim must not manufacture another physical Home visit.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null,
                "Laptop must remain READY after payment within the same physical Home visit.");
        }

        private IEnumerator ConsumeInitialLaptopBoot(RokasBootstrap boot)
        {
            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Not.Null,
                "The first Laptop open in this physical Home visit must consume the one boot.");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(boot.View.LaptopOpen, Is.False);
        }

        private void CloseLaptopFromSection(RokasBootstrap boot)
        {
            boot.View.Escape();
            boot.View.Escape();
        }

        private void HideLaptopMedia()
        {
            hiddenLaptopMediaPath = Path.Combine(Application.streamingAssetsPath, "RokasVideo", "LaptopBoot.mp4");
            hiddenLaptopBackupPath = hiddenLaptopMediaPath + ".home-visit-test-hidden";
            Assert.That(File.Exists(hiddenLaptopMediaPath), Is.True, "Committed LaptopBoot.mp4 is required by the fixture.");
            Assert.That(File.Exists(hiddenLaptopBackupPath), Is.False, "Unexpected stale laptop media test backup.");
            File.Move(hiddenLaptopMediaPath, hiddenLaptopBackupPath);
        }

        private void RestoreLaptopMedia()
        {
            if (string.IsNullOrEmpty(hiddenLaptopBackupPath) || !File.Exists(hiddenLaptopBackupPath)) return;
            if (!File.Exists(hiddenLaptopMediaPath)) File.Move(hiddenLaptopBackupPath, hiddenLaptopMediaPath);
            else File.Delete(hiddenLaptopBackupPath);
            hiddenLaptopMediaPath = null;
            hiddenLaptopBackupPath = null;
        }

        private RokasBootstrap CreateSynchronousBoot(bool enableVideoTransitions)
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-home-visit-video-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("HomeVisitVideoFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory, enableVideoTransitions);
            return boot;
        }

        private RokasBootstrap CreatePaymentHomeVisitBoot(bool enableVideoTransitions)
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-payment-home-visit-" + Guid.NewGuid().ToString("N"));
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            ContractDefinition contract = JsonUtility.FromJson<ContractDefinition>(assets.contract.text);
            var state = new SaveData
            {
                phase = RunPhase.Payment,
                activeContractId = contract.id,
                contractRunSequence = 1
            };
            SaveWriteResult saved = new SaveStore(directory, new UnitySaveCodec()).Save(state);
            Assert.That(saved.Succeeded, Is.True, saved.Message);

            root = new GameObject("PaymentHomeVisitFixture");
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
            RestoreLaptopMedia();
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
