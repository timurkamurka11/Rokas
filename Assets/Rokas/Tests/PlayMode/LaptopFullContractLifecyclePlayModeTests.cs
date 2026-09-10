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
    public sealed class LaptopFullContractLifecyclePlayModeTests
    {
        private GameObject root;
        private string directory;
        private string hiddenLaptopMediaPath;
        private string hiddenLaptopBackupPath;

        [UnityTest]
        public IEnumerator FullContractLifecycleConsumesReturnVisitBootOnlyOnceThroughPayment()
        {
            HideLaptopMedia();
            RokasBootstrap boot = CreateSynchronousBoot(true);
            yield return null;

            // HOME VISIT #1: first open boots, every later open during this visit is READY.
            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Not.Null,
                "Home Visit #1 must begin with exactly one Laptop boot.");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(boot.View.LaptopOpen, Is.False);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "The second Laptop open in Home Visit #1 must be READY.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
            Press("LaptopContracts");
            Press("AcceptContract");
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Accepted));
            yield return null; // allow the rebuilt Home hierarchy to replace destroy-frame objects
            Press("ClosePanel");
            yield return new WaitForSecondsRealtime(.3f);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Accepting the contract while still physically Home must not reset boot eligibility.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
            Press("ClosePanel");
            yield return new WaitForSecondsRealtime(.3f);

            // Real gameplay departure: Home -> Portal -> Combat -> Sealed -> Payment/Home.
            Press("DoorHotspot");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Portal));

            Press("EnterPortal");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));

            for (int i = 0; i < 200 && boot.Session.State.phase == RunPhase.Combat; i++)
            {
                boot.Session.Tick(.48f);
                if (boot.Session.State.phase != RunPhase.Combat) break;
                if (boot.Session.Combat.Stage == CombatStage.Ritual) TraceRitual();
                else if (boot.Session.Combat.Stage == CombatStage.Fighting) TapEnemy();
            }
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Sealed));

            Press("ReturnHome");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Payment));

            // HOME VISIT #2: return creates one fresh boot.
            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Not.Null,
                "The first Laptop open after a real outside -> Home return must boot once.");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(boot.View.LaptopOpen, Is.False);

            // Consume the visit boot, then use the real laptop payment presentation.
            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "After consuming the return boot, Laptop must already be READY before payment.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
            Press("LaptopContracts");
            Press("ClaimPayment");
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            yield return null; // synchronize the post-claim presentation rebuild
            Press("ClosePanel");
            yield return new WaitForSecondsRealtime(.3f);

            // Critical regression target: payment must not manufacture another visit/boot.
            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "ClaimPayment must not cause a SECOND boot in the same physical Home Visit #2.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null,
                "Laptop must reopen directly READY after payment in the same Home visit.");
            Press("ClosePanel");
            yield return new WaitForSecondsRealtime(.3f);

            Press("LaptopHotspot");
            Assert.That(Find("LaptopBootSurface"), Is.Null,
                "Repeated reopen after payment must remain READY until the player really leaves Home again.");
            Assert.That(Find("LaptopContracts"), Is.Not.Null);
        }

        private void TapEnemy()
        {
            Button button = FindButton("EnemyAttack");
            Assert.That(button, Is.Not.Null, "Combat must expose EnemyAttack.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler), Is.True);
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler), Is.True);
        }

        private void TraceRitual()
        {
            Button button = FindButton("EnemyAttack");
            Assert.That(button, Is.Not.Null, "Combat must expose EnemyAttack during ritual tracing.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            for (int i = 1; i <= 3; i++)
            {
                RectTransform point = null;
                foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>())
                    if (rect.name == "RitualPoint" + i) point = rect;
                Assert.That(point, Is.Not.Null, "Missing active ritual point " + i);
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, point.TransformPoint(point.rect.center));
                if (i == 1)
                    Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler), Is.True);
                else
                    Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.dragHandler), Is.True);
            }
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler), Is.True);
        }

        private RokasBootstrap CreateSynchronousBoot(bool enableVideoTransitions)
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-full-contract-home-visit-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("FullContractHomeVisitFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory, enableVideoTransitions);
            return boot;
        }

        private void HideLaptopMedia()
        {
            hiddenLaptopMediaPath = Path.Combine(Application.streamingAssetsPath, "RokasVideo", "LaptopBoot.mp4");
            hiddenLaptopBackupPath = hiddenLaptopMediaPath + ".full-lifecycle-test-hidden";
            Assert.That(File.Exists(hiddenLaptopMediaPath), Is.True, "Committed LaptopBoot.mp4 is required by the fixture.");
            Assert.That(File.Exists(hiddenLaptopBackupPath), Is.False, "Unexpected stale LaptopBoot.mp4 test backup.");
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
            foreach (Button button in root.GetComponentsInChildren<Button>())
                if (button.name == name) return button;
            return null;
        }

        private GameObject Find(string name)
        {
            if (root == null) return null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>())
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
