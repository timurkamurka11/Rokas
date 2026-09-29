using System.Collections;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests.PlayMode
{
    public sealed class ReactiveArenaMotionPolishPlayModeTests
    {
        [UnityTest]
        public IEnumerator TenAlternatingAttacksReturnExactlyAndPresentationPauseHoldsPose()
        {
            var root = new GameObject("ReactiveMotionPolishFixture", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                arena.SetEnemies(new[] { "E1", "E2" }, null, new[] { "E1", "E2" });
                ReactiveCombatActorVisual hunter = FindHunter();
                Assert.That(hunter, Is.Not.Null);
                Animation animation = hunter.ModelRoot.GetComponent<Animation>();
                Assert.That(animation.GetClip("Attack"), Is.Not.SameAs(animation.GetClip("Heavy")));
                Vector3 home = hunter.transform.localPosition;
                Quaternion rotation = hunter.transform.localRotation;
                for (int action = 0; action < 10; action++)
                {
                    bool heavy = action % 2 != 0;
                    string target = heavy ? "E2" : "E1";
                    Assert.That(arena.StartHunterApproach(target, heavy), Is.True);
                    arena.Tick(.3f);
                    Vector3 approaching = hunter.transform.localPosition;
                    arena.SetEnemies(new[] { "E1", "E2" }, null, new[] { "E1", "E2" });
                    Assert.That(hunter.transform.localPosition, Is.EqualTo(approaching));
                    int ticks = 0;
                    while (!arena.HunterApproachComplete && ticks++ < 100) arena.Tick(.025f);
                    Assert.That(arena.HunterApproachComplete, Is.True);
                    string alias = heavy ? "Heavy" : "Attack";
                    Assert.That(hunter.CurrentPose, Is.EqualTo(alias));
                    float pausedTime = animation[alias].time;
                    yield return new WaitForSecondsRealtime(.12f);
                    Assert.That(animation[alias].time, Is.EqualTo(pausedTime).Within(.0001f),
                        "The arena clock owns animation; a paused view must not keep playing it.");
                    arena.Present(new CombatEvent(CombatEventKind.CommandCommitted,
                        actorId: ReactiveDuelDefinitions.HunterId, actionId: "polish-" + action,
                        detail: heavy ? "heavy" : "Basic"));
                    Assert.That(animation[alias].time, Is.EqualTo(pausedTime).Within(.0001f),
                        "The Core commit must not restart the already playing sword windup.");
                    arena.Tick(.2f);
                    arena.Present(new CombatEvent(CombatEventKind.HitResolved,
                        actorId: ReactiveDuelDefinitions.HunterId, targetId: target,
                        actionId: "polish-" + action, amount: 8));
                    float contactTime = animation[alias].time;
                    arena.Tick(.04f);
                    Assert.That(animation[alias].time, Is.EqualTo(contactTime).Within(.0001f));
                    arena.Tick(.05f);
                    arena.Present(new CombatEvent(CombatEventKind.ActionSettled,
                        actorId: ReactiveDuelDefinitions.HunterId, actionId: "polish-" + action));
                    ticks = 0;
                    while (!arena.HunterAtHome && ticks++ < 60) arena.Tick(.025f);
                    Assert.That(arena.HunterAtHome, Is.True);
                    Assert.That(hunter.transform.localPosition, Is.EqualTo(home));
                    Assert.That(hunter.transform.localRotation, Is.EqualTo(rotation));
                    if (hunter.WeaponAttachment != null)
                        Assert.That(hunter.WeaponAttachment.Socket.childCount, Is.EqualTo(1),
                            "Repeated actions must not spawn another equipped sword.");
                    yield return null;
                }
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator EnemyKeepsOtherSlotsAndCorpseFallsHoldsThenSinksWithoutShrinking()
        {
            var root = new GameObject("ReactiveEnemyMotionPolishFixture", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                string[] ids = { "E1", "E2", "E3" };
                arena.SetEnemies(ids, null, ids);
                Vector3 home = arena.EnemyHome("E1");
                Vector3 other2 = arena.EnemyPosition("E2");
                Vector3 other3 = arena.EnemyPosition("E3");
                var sequence = new AttackSequenceDefinition("polish-sequence", 3000000,
                    new[] { new HitDefinition("hit-1", 1000000, 10, DefenseResponseMask.Dodge | DefenseResponseMask.Parry),
                        new HitDefinition("hit-2", 1650000, 10, DefenseResponseMask.Dodge | DefenseResponseMask.Parry) });
                arena.Present(new CombatEvent(CombatEventKind.AttackStarted,
                    actorId: "E1", actionId: "enemy-polish", detail: sequence.Id), sequence);
                arena.Tick(.3f);
                Vector3 approach = arena.EnemyPosition("E1");
                Assert.That(approach.x, Is.LessThan(home.x));
                arena.SetEnemies(ids, "E1", ids);
                Assert.That(arena.EnemyPosition("E1"), Is.EqualTo(approach));
                arena.Tick(.35f);
                Assert.That(arena.EnemyApproachComplete("E1"), Is.True);
                Assert.That(arena.EnemyPosition("E2"), Is.EqualTo(other2));
                Assert.That(arena.EnemyPosition("E3"), Is.EqualTo(other3));
                arena.Tick(1f);
                Assert.That(arena.EnemyApproachComplete("E1"), Is.True,
                    "A multi-hit attacker stays at the defender until its whole action settles.");
                arena.Present(new CombatEvent(CombatEventKind.ActionSettled,
                    actorId: "E1", actionId: "enemy-polish"));
                arena.Tick(.1f);
                Assert.That(arena.EnemyAtHome("E1"), Is.False);
                Assert.That(arena.StartHunterApproach("E1"), Is.True);
                arena.Tick(arena.HunterApproachDuration);
                Assert.That(arena.HunterPosition, Is.EqualTo(new Vector3(
                    home.x - arena.HunterAttackDistance, arena.HunterHome.y, home.z - .45f)),
                    "A selected enemy's returning pose must not move the next attack's destination.");
                arena.CancelHunterMotion();
                Assert.That(arena.EnemyAtHome("E1"), Is.True);
                Assert.That(arena.EnemyPosition("E1"), Is.EqualTo(home));
                GameObject world = GameObject.Find("ReactiveCombatWorld");
                ReactiveCombatActorVisual corpse = null;
                foreach (var actor in world.GetComponentsInChildren<ReactiveCombatActorVisual>())
                    if (actor.name == "CombatActor_Yokai" && actor.transform.localPosition == home) corpse = actor;
                Assert.That(corpse, Is.Not.Null);
                Vector3 originalScale = corpse.transform.localScale;
                arena.SetEnemies(new[] { "E2", "E3" }, null, ids);
                Assert.That(arena.IsCorpse("E1"), Is.True);
                Assert.That(corpse.IsDead, Is.True);
                Assert.That(arena.StartHunterApproach("E1"), Is.False,
                    "A retained visual corpse must immediately reject targeting.");
                arena.Tick(arena.DeathFallDuration + arena.CorpseHoldDuration - .01f);
                Assert.That(corpse.transform.localPosition, Is.EqualTo(home));
                Assert.That(corpse.transform.localScale, Is.EqualTo(originalScale));
                arena.Tick(.21f);
                Assert.That(corpse.transform.localPosition.y, Is.LessThan(home.y));
                Assert.That(corpse.transform.localScale, Is.EqualTo(originalScale),
                    "Corpse cleanup is a vertical sink, not a scale collapse.");
                arena.Tick(arena.CorpseSinkDuration);
                Assert.That(arena.CorpseCount, Is.Zero);
                Assert.That(arena.VisibleEnemyCount, Is.EqualTo(2));
                yield return null;
                Assert.That(world.GetComponentsInChildren<ReactiveCombatActorVisual>().Length, Is.EqualTo(3));
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(root);
            }
        }

        private static ReactiveCombatActorVisual FindHunter()
        {
            GameObject world = GameObject.Find("ReactiveCombatWorld");
            foreach (var actor in world.GetComponentsInChildren<ReactiveCombatActorVisual>())
                if (actor.name == "CombatActor_Keiko") return actor;
            return null;
        }
    }
}
