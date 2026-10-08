using System.Collections;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class ReactivePolishIIPlayModeTests
    {
        private GameObject root;
        private ReactiveCombatArena arena;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("PolishIIFixture", typeof(RectTransform));
            arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            arena.Dispose(); Object.Destroy(root); yield return null;
        }
        [UnityTest]
        public IEnumerator InterruptedSuffixSettlesWithoutAwaitingCancelledContactOrEmittingLaterSwing()
        {
            arena.SetEnemies(new[] { "E1" }, null); FinishPresentation();
            Vector3 home = arena.EnemyHome("E1");
            ReactiveCombatActorVisual enemy = null;
            foreach (var actor in GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<ReactiveCombatActorVisual>())
                if (actor.name == "CombatActor_Yokai") enemy = actor;
            Assert.That(enemy, Is.Not.Null);
            var sequence = new AttackSequenceDefinition("interruptible-test", 3000000,
                new[] { new HitDefinition("first", 1000000, 10, DefenseResponseMask.Parry),
                    new HitDefinition("cancelled", 1650000, 10, DefenseResponseMask.Parry) },
                interruptibleOnBreak: true);
            int swings = 0;
            arena.SwingStarted += (id, actor, heavy, hit) => { if (actor == "E1") swings++; };
            arena.Present(new CombatEvent(CombatEventKind.AttackStarted, actorId: "E1",
                actionId: "interrupted-suffix", detail: sequence.Id), sequence);
            for (int tick = 0; tick < 110; tick++) arena.Tick(.01f);
            Assert.That(enemy.AwaitingAttackContact, Is.True);
            arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "E1",
                targetId: ReactiveDuelDefinitions.HunterId, actionId: "interrupted-suffix",
                hitId: "first", detail: "Perfect"));
            for (int tick = 0; tick < 65; tick++) arena.Tick(.01f);
            Assert.That(enemy.AwaitingAttackContact, Is.True, "Reproduce a pending suffix before its authoritative cancellation.");
            arena.Present(new CombatEvent(CombatEventKind.ActionSettled, actorId: "E1",
                actionId: "interrupted-suffix"));
            int swingsAtSettlement = swings;
            Assert.That(enemy.AwaitingAttackContact, Is.False);
            Assert.That(arena.StartHunterApproach("E1"), Is.False, "Cancellation must still finish recovery and return.");
            for (int tick = 0; !arena.PresentationReady && tick < 300; tick++) arena.Tick(.02f);
            Assert.That(arena.PresentationReady, Is.True);
            Assert.That(arena.EnemyPosition("E1"), Is.EqualTo(home));
            Assert.That(enemy.IdleSettled, Is.True);
            Assert.That(swings, Is.EqualTo(swingsAtSettlement));
            yield return null;
        }
        [UnityTest]
        public IEnumerator CounterAfterHeavyEnemyUsesHuntersNormalContactWindow()
        {
            arena.SetEnemies(new[] { "E1" }, null); FinishPresentation();
            arena.Present(new CombatEvent(CombatEventKind.AttackStarted, actorId: "E1",
                actionId: "heavy-enemy-counter", detail: "heavy"));
            arena.Present(new CombatEvent(CombatEventKind.HitResolved,
                actorId: ReactiveDuelDefinitions.HunterId, targetId: "E1",
                actionId: "heavy-enemy-counter", detail: "Counter", amount: 20));
            Assert.That(Hunter().HitStopRemaining, Is.EqualTo(arena.HitStopDuration));
            Assert.That(Hunter().HitStopRemaining, Is.LessThan(arena.HeavyHitStopDuration));
            yield return null;
        }
        [UnityTest]
        public IEnumerator EntryCompletesAtExactSlotAndIdleBeforeMusicAndSequentialEnemies()
        {
            var assets = Resources.Load<RokasAssets>("RokasAssets");
            var audio = new RokasAudio(root, assets, new SettingsData());
            audio.SetLocation(true, true, true);
            arena.BeginEncounterIntro();
            arena.SetEnemies(new[] { "E1", "E2", "E3" }, null);
            Assert.That(arena.VisibleEnemyCount, Is.Zero);
            Assert.That(audio.CombatMusicPlaying, Is.False);
            var hunter = Hunter();
            var cam = GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
            foreach (Renderer renderer in hunter.GetComponentsInChildren<Renderer>())
                Assert.That(renderer.bounds.max.x, Is.LessThan(cam.transform.position.x - cam.orthographicSize * cam.aspect));
            int priorCount = 0;
            for (int tick = 0; tick < 1600 && !arena.PresentationReady; tick++)
            {
                arena.Tick(.02f);
                if (!arena.HunterEntryComplete) Assert.That(audio.CombatMusicPlaying, Is.False);
                audio.SetCombatEntryReady(arena.HunterEntryComplete);
                if (arena.HunterEntryComplete && priorCount == 0)
                {
                    Assert.That(hunter.transform.localPosition, Is.EqualTo(arena.HunterHome));
                    Assert.That(hunter.IdleSettled, Is.True);
                }
                int count = arena.VisibleEnemyCount;
                Assert.That(count - priorCount, Is.InRange(0, 1));
                priorCount = count;
            }
            Assert.That(arena.PresentationReady, Is.True);
            Assert.That(audio.CombatMusicPlaying, Is.True);
            Assert.That(arena.VisibleEnemyCount, Is.EqualTo(3));
            foreach (string id in new[] { "E1", "E2", "E3" }) Assert.That(arena.EnemyAtHome(id), Is.True);
            audio.Dispose(); yield return null;
        }
        [UnityTest]
        public IEnumerator SelectionStancesReadAtHomeAndCameraRestoresBeforeTravelForBothActions()
        {
            arena.SetEnemies(new[] { "E1" }, null);
            FinishPresentation();
            foreach (bool heavy in new[] { false, true })
            {
                Assert.That(arena.StartHunterApproach("E1", heavy), Is.True);
                for (int i = 0; i < 12; i++) arena.Tick(.025f);
                Assert.That(arena.SelectionVisible, Is.True);
                Assert.That(arena.HunterPosition, Is.EqualTo(arena.HunterHome));
                Assert.That(arena.CameraAtHome, Hunter().DefaultLicensedProfile == null ? Is.False : Is.True,
                    "Native selection keeps source tactical framing; Legacy selection owns its preview zoom.");
                string prepare = Hunter().CurrentPose;
                Assert.That(prepare, Is.EqualTo(heavy ? "HeavyPreparation" : "Preparation"));
                int ticks = 0;
                while (arena.SelectionVisible && ticks++ < 100) arena.Tick(.025f);
                Assert.That(arena.CameraAtHome, Is.True);
                Assert.That(Hunter().CurrentPose, Is.EqualTo("Approach"));
                while (!arena.HunterApproachComplete && ticks++ < 200) arena.Tick(.025f);
                Assert.That(arena.HunterApproachComplete, Is.True);
                Assert.That(arena.CameraAtHome, Is.True);
                arena.CancelHunterMotion(); FinishPresentation();
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator AcceptedGuardAndDodgeKeepAuthoredPosesThroughContactThenRecoverWithoutRootDrift()
        {
            arena.SetEnemies(new[] { "E1" }, null); FinishPresentation();
            var hunter = Hunter(); Vector3 slot = arena.HunterHome;
            foreach (bool dodge in new[] { false, true })
            {
                arena.BeginDefense(dodge, .2f);
                arena.Tick(.18f);
                Assert.That(hunter.DefenseActive, Is.True);
                Assert.That(hunter.CurrentPose, Is.EqualTo(dodge ? "Dodge" : "Guard"));
                if (dodge) Assert.That(hunter.DodgeDisplacement, Is.GreaterThan(.5f));
                arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "E1",
                    targetId: ReactiveDuelDefinitions.HunterId, actionId: "defense-" + dodge,
                    detail: dodge ? "Dodge" : "Parry"));
                Assert.That(hunter.CurrentPose, Is.EqualTo(dodge ? "Dodge" : "Guard"));
                Assert.That(arena.HitStopRemaining, dodge ? Is.Zero : Is.GreaterThan(0f));
                FinishPresentation();
                Assert.That(hunter.DefenseRecoveryComplete, Is.True);
                Assert.That(hunter.transform.localPosition, Is.EqualTo(slot));
                Assert.That(hunter.DodgeDisplacement, Is.Zero);
            }
            yield return null;
        }
        private void FinishPresentation()
        {
            for (int i = 0; !arena.PresentationReady && i < 1000; i++) arena.Tick(.02f);
            Assert.That(arena.PresentationReady, Is.True);
        }
        [UnityTest]
        public IEnumerator PendingContactPreservesContactPoseAndRecoveryUntilAuthoritativeResult()
        {
            var hunter = Hunter();
            hunter.PlayAttack(); hunter.AwaitAttackContact(false);
            hunter.TickPresentation(2f);
            Assert.That(hunter.CurrentPose, Is.EqualTo("Attack"));
            if (hunter.ActiveLicensedProfile != null)
            {
                LicensedCombatMotionProfile source = hunter.ActiveLicensedProfile;
                Assert.That(hunter.AwaitingAttackContact, Is.True);
                Assert.That(hunter.LicensedContactConfirmed, Is.False, "Time alone never authorizes source contact.");
                Assert.That(hunter.LicensedMotionClock, Is.EqualTo(2f).Within(.001f));
                var anticipation = hunter.ModelRoot.GetComponent<Animation>()["Licensed_Anticipation"];
                Assert.That(anticipation.time, Is.EqualTo(Mathf.Min(2f * source.anticipationSpeed,
                    source.anticipation.length)).Within(.001f));
                Assert.That(hunter.ActionRecoveryComplete, Is.False);
                hunter.BindLicensedContact("pending-contact-source");
                Assert.That(hunter.ConfirmLicensedContact("pending-contact-source"), Is.True);
                Assert.That(hunter.ConfirmLicensedContact("pending-contact-source"), Is.False);
                Assert.That(hunter.LicensedMotionClock, Is.Zero);
                Assert.That(hunter.HitStopRemaining, Is.Zero, "Source playback adds no Legacy hit-stop.");
                hunter.TickPresentation(.04f);
                Assert.That(hunter.LicensedMotionClock, Is.EqualTo(.04f).Within(.0001f));
                hunter.TickPresentation(source.Duration - .05f);
                Assert.That(hunter.ActionRecoveryComplete, Is.False, "Keep the complete source recovery tail.");
                hunter.TickPresentation(.02f);
                hunter.TickPresentation(.09f);
                Assert.That(hunter.ActionRecoveryComplete && hunter.IdleSettled, Is.True);
                yield return null;
                yield break;
            }
            Assert.That(hunter.CurrentPoseSeconds, Is.EqualTo(hunter.AttackContactSeconds(false)).Within(.001f));
            Assert.That(hunter.ActionRecoveryComplete, Is.False);
            hunter.HoldAttackAtContact(false, .08f);
            hunter.TickPresentation(.04f);
            Assert.That(hunter.CurrentPoseSeconds, Is.EqualTo(hunter.AttackContactSeconds(false)).Within(.001f));
            hunter.TickPresentation(.8f);
            hunter.TickPresentation(.1f);
            Assert.That(hunter.ActionRecoveryComplete, Is.True);
            Assert.That(hunter.IdleSettled, Is.True);
            yield return null;
        }
        private static ReactiveCombatActorVisual Hunter()
        {
            foreach (var actor in GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                if (actor.name == "CombatActor_Keiko") return actor;
            Assert.Fail("Keiko missing"); return null;
        }
    }
}
