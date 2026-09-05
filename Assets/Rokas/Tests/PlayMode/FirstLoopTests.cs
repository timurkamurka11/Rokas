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
    public sealed class FirstLoopTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator IllustratedHomeCanAcceptFightReturnAndClaimExactlyOnce()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-playmode-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("FirstLoopFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Assert.That(boot.Session, Is.Not.Null);
            var art = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(art.home, Is.Not.Null);
            Assert.That(art.portal, Is.Not.Null);
            Assert.That(art.subway, Is.Not.Null);
            Assert.That(art.enemy, Is.Not.Null);
            Assert.That(art.familiar, Is.Not.Null);
            Assert.That(art.IsComplete(), Is.True, "Every shipped art, font, contract and audio binding must import.");
            Assert.That(root.GetComponentInChildren<Camera>(), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<Canvas>(), Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);
            Press("LaptopHotspot");
            Press("AcceptContract");
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Accepted));
            Press("ClosePanel");
            Press("TeaHotspot");
            Press("PrepareTea");
            Press("ClosePanel");
            Press("DoorHotspot");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Portal));
            Press("EnterPortal");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));

            // The real UI input path must not reach combat through a modal pause panel.
            Press("Settings");
            var enemyButton = FindButton("EnemyAttack");
            Assert.That(enemyButton.IsInteractable(), Is.False);
            float pausedHealth = boot.Session.State.enemyHp;
            float pausedTime = boot.Session.State.combatTime;
            Click(enemyButton);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(pausedHealth));
            Assert.That(boot.Session.State.combatTime, Is.EqualTo(pausedTime));
            Press("CloseSettings");

            float beforeAutoAttack = boot.Session.State.enemyHp;
            boot.Session.Tick(boot.Session.Contract.autoInterval + .05f);
            Assert.That(boot.Session.State.enemyHp, Is.LessThan(beforeAutoAttack));

            // Exercise the actual domain and the wired hit button, not a duplicate simulation.
            for (int i = 0; i < 200 && boot.Session.State.phase == RunPhase.Combat; i++)
            {
                boot.Session.Tick(.2f);
                if (boot.Session.State.phase == RunPhase.Combat) Press("EnemyAttack");
            }
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Sealed));
            Press("ReturnHome");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Payment));
            Press("LaptopHotspot");
            long previous = boot.Session.State.yen;
            Press("ClaimPayment");
            Assert.That(boot.Session.State.yen, Is.EqualTo(previous + boot.Session.Contract.reward));
            Assert.That(FindButton("ClaimPayment", false), Is.Null, "The payment action must disappear after claiming.");
            int paidYen = boot.Session.State.yen;
            int paidReputation = boot.Session.State.reputation;
            int paidAsh = boot.Session.State.spiritAsh;
            Assert.That(File.Exists(Path.Combine(directory, "save.json")), Is.True);

            // Recreate the presentation with its real Unity JSON codec and the same isolated profile.
            // File existence alone would also pass with a stale pre-reward save.
            UnityEngine.Object.Destroy(root);
            yield return null;
            root = new GameObject("ReloadedFirstLoopFixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            Assert.That(boot.Session.State.yen, Is.EqualTo(paidYen));
            Assert.That(boot.Session.State.reputation, Is.EqualTo(paidReputation));
            Assert.That(boot.Session.State.spiritAsh, Is.EqualTo(paidAsh));
            Assert.That(boot.Session.State.completedRuns, Is.EqualTo(1));
            Assert.That(boot.Session.State.preparedFoodId, Is.Empty);
            Press("LaptopHotspot");
            Assert.That(FindButton("ClaimPayment", false), Is.Null);
            Assert.That(FindButton("AcceptContract").IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private void Press(string name)
        {
            var button = FindButton(name);
            Assert.That(button.IsInteractable(), Is.True, name);
            Click(button);
        }

        private static void Click(Button button)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private Button FindButton(string name, bool required = true)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                if (button.name != name) continue;
                return button;
            }
            if (required) Assert.Fail("Missing active interaction: " + name);
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
