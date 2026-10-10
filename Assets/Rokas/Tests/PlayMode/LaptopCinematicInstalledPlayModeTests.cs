using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class LaptopCinematicInstalledPlayModeTests
    {
        private GameObject root;
        private string directory;

        private RokasBootstrap Initialize(string prefix)
        {
            LaptopPowerSession.ResetForTests(); // tests simulate separate launches
            if (!Resources.Load<Texture2D>(
                    "LaptopCinematic/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF") ||
                !Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest"))
                Assert.Ignore("First install new right-hand Blender art resources; old PSX frames are not acceptable.");

            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("LaptopInstalledCinematicFixture");
            RokasBootstrap boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            return boot;
        }

        private T Find<T>(string name) where T : Component
        {
            if (!root) return null;
            foreach (T element in root.GetComponentsInChildren<T>(true))
                if (element.name == name) return element;
            return null;
        }

        private void PressLaptop()
        {
            Button button = Find<Button>("LaptopHotspot");
            Assert.That(button, Is.Not.Null, "The original hotspot must survive.");
            button.onClick.Invoke();
        }

        // V11 deliberately preserves the outgoing POV until YOMI is visible.
        // This is a compositing assertion, not an extra black-delay tolerance.
        private IEnumerator AssertAtomicYomiHandoff()
        {
            RectTransform pov = Find<RectTransform>("LaptopCinematicOverlay");
            if (pov)
            {
                RectTransform modal = Find<RectTransform>("Panels");
                RectTransform overlay = Find<RectTransform>("Transitions");
                Assert.That(modal, Is.Not.Null);
                Assert.That(overlay, Is.Not.Null);
                Assert.That(modal.GetSiblingIndex(), Is.GreaterThan(overlay.GetSiblingIndex()),
                    "YOMI MUST render above the outgoing seated POV, never exposing Hub.");
            }
            // V11.1 intentional change: keep the SAME original seated POV
            // under the YOMI modal for the entire fullscreen session.
            yield return null;
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Not.Null,
                "Underlying POV should remain behind YOMI, never expose a Hub screenshot.");
        }

        [UnityTest]
        public IEnumerator ConfirmPowerThenCloseReturnsToSeatedChoiceWithoutStanding()
        {
            RokasBootstrap boot = Initialize("rokas-laptop-seat-cycle-");
            yield return null;
            PressLaptop();
            PressLaptop(); // repeated room click must not open the laptop
            yield return new WaitForSecondsRealtime(2.05f);
            Assert.That(boot.View.LaptopOpen, Is.False);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Not.Null);
            Assert.That(Find<Button>("PowerKeyClickTarget"), Is.Not.Null);
            Find<Button>("PowerKeyClickTarget").onClick.Invoke();
            Find<Button>("PowerKeyClickTarget").onClick.Invoke(); // one Power event
            yield return new WaitForSecondsRealtime(2.55f);
            Assert.That(boot.View.LaptopOpen, Is.True);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Not.Null,
                "The original seated image MUST remain behind the YOMI window.");
            boot.View.Escape(); // Close YOMI, but DO NOT stand
            yield return new WaitForSecondsRealtime(.85f);
            Assert.That(boot.View.LaptopOpen, Is.False);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Not.Null,
                "Closing physical laptop must return to seated Power/Stand choice.");
            Assert.That(Find<Button>("PowerKeyClickTarget"), Is.Not.Null);
            Assert.That(Find<Button>("LaptopHotspot").IsInteractable(), Is.False);
            Assert.That(Find<RectTransform>("PowerKeyBlueStandby"), Is.Not.Null);
            boot.View.Escape(); // This Escape stands up and returns to hub
            yield return new WaitForSecondsRealtime(1.05f);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"), Is.Null);
            Assert.That(Find<Button>("LaptopHotspot").IsInteractable(), Is.True);
        }

        [UnityTest]
        public IEnumerator RepeatedAccessFromSeatedChoiceSkipsFurtherBootCycles()
        {
            RokasBootstrap boot = Initialize("rokas-laptop-reboot-cycle-");
            yield return null;
            PressLaptop();
            yield return new WaitForSecondsRealtime(2.05f);
            Find<Button>("PowerKeyClickTarget").onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.55f);
            Assert.That(boot.View.LaptopOpen, Is.True);
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.85f);
            Assert.That(boot.View.LaptopOpen, Is.False);
            LaptopPowerSession.CompleteFirstBoot();
            Assert.That(Find<Button>("PhysicalDesktopClickTarget"), Is.Not.Null);
            Assert.That(Find<Button>("PhysicalDesktopClickTarget").gameObject.activeInHierarchy, Is.True);
            Find<Button>("PhysicalDesktopClickTarget").onClick.Invoke();
            yield return null;
            Assert.That(boot.View.LaptopOpen, Is.True);
            Assert.That(LaptopPowerSession.CompletedBoots, Is.EqualTo(1));
            yield return AssertAtomicYomiHandoff();
        }

        [UnityTest]
        public IEnumerator PoweredLaptopShowsLiveClockAndReopensWithoutHandOrBoot()
        {
            RokasBootstrap boot = Initialize("rokas-laptop-powered-desktop-");
            yield return null;
            PressLaptop();
            yield return new WaitForSecondsRealtime(2.05f);
            Assert.That(Find<Button>("PowerKeyClickTarget"), Is.Not.Null);
            Find<Button>("PowerKeyClickTarget").onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.55f);
            Assert.That(boot.View.LaptopOpen, Is.True);
            // Simulate the VideoPlayer completing the FIRST and ONLY boot.
            // Real playback completion is tested via the normal VideoPresenter.
            LaptopPowerSession.CompleteFirstBoot();
            Assert.That(LaptopPowerSession.CompletedBoots, Is.EqualTo(1));
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.9f);
            var live = Find<LaptopPhysicalDesktopClock>("PhysicalDesktopLiveClock");
            Assert.That(live, Is.Not.Null);
            Assert.That(live.gameObject.activeInHierarchy, Is.True);
            Assert.That(Find<RectTransform>("PowerKeyBlueStandby").gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Text>("StandbyNoSignal").gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Button>("PhysicalDesktopClickTarget"), Is.Not.Null);
            Assert.That(Find<Button>("PhysicalDesktopClickTarget").gameObject.activeInHierarchy, Is.True);
            Find<Button>("PhysicalDesktopClickTarget").onClick.Invoke();
            yield return null;
            Assert.That(boot.View.LaptopOpen, Is.True,
                "Clicking the powered physical LCD should directly reopen YOMI.");
            yield return AssertAtomicYomiHandoff();
            Assert.That(LaptopPowerSession.CompletedBoots, Is.EqualTo(1));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            root = null;
            LaptopPowerSession.ResetForTests();
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
