using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
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
            Assert.That(boot.Session.State.yen, Is.GreaterThan(previous));
            Assert.That(boot.Session.ClaimPayment(), Is.False);
            Assert.That(File.Exists(Path.Combine(directory, "save.json")), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private void Press(string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                if (button.name != name) continue;
                Assert.That(button.interactable, Is.True, name);
                button.onClick.Invoke();
                return;
            }
            Assert.Fail("Missing active interaction: " + name);
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
