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
        public IEnumerator FiveNormalFiveHeavyAndTenAlternatingAttacksStaySettledWithoutDrift()
        {
            var root = new GameObject("ReactiveMotionPolishFixture", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                arena.SetEnemies(new[] { "E1", "E2" }, null, new[] { "E1", "E2" });
                for (int warmup = 0; !arena.PresentationReady && warmup < 100; warmup++) arena.Tick(.02f);
                ReactiveCombatActorVisual hunter = FindHunter();
                Assert.That(hunter, Is.Not.Null);
                Animation animation = hunter.ModelRoot.GetComponent<Animation>();
                Assert.That(animation.GetClip("Attack"), Is.Not.SameAs(animation.GetClip("Heavy")));
                Vector3 home = hunter.transform.localPosition;
                Quaternion rotation = hunter.transform.localRotation;
                Quaternion modelFacing = hunter.ModelRoot.localRotation;
                Transform hips = hunter.ModelRoot.Find("mixamorig:Hips");
                Transform left = null;
                foreach (Transform bone in hunter.ModelRoot.GetComponentsInChildren<Transform>(true))
                    if (bone.name == "mixamorig:LeftHand") left = bone;
                Assert.That(left, Is.Not.Null);
                for (int action = 0; action < 20; action++)
                {
                    bool heavy = action < 5 ? false : action < 10 ? true : action % 2 != 0;
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
                    while (!arena.PresentationReady && ticks++ < 80) arena.Tick(.025f);
                    Assert.That(arena.HunterAtHome, Is.True);
                    Assert.That(hunter.transform.localPosition, Is.EqualTo(home));
                    Assert.That(hunter.transform.localRotation, Is.EqualTo(rotation));
                    Assert.That(hunter.CurrentPose, Is.EqualTo("Idle"));
                    Assert.That(hunter.IdleSettled, Is.True);
                    Vector3 idleHipPosition = hips.localPosition;
                    Quaternion idleHipRotation = hips.localRotation;
                    for (int idle = 0; idle < 20; idle++)
                    {
                        arena.Tick(.1f);
                        Assert.That(hunter.transform.localPosition, Is.EqualTo(home));
                        Assert.That(hunter.ModelRoot.localRotation, Is.EqualTo(modelFacing));
                        Assert.That(Vector3.Distance(hips.localPosition, idleHipPosition), Is.LessThan(.006f));
                        Assert.That(Quaternion.Angle(hips.localRotation, idleHipRotation), Is.LessThan(3f));
                        Transform grip = hunter.WeaponAttachment.CurrentWeapon.transform.Find("LeftHandGrip");
                        Assert.That(Vector3.Distance(left.TransformPoint(new Vector3(0f, .033f, 0f)),
                            grip.position), Is.LessThan(.025f));
                    }
                    if (hunter.WeaponAttachment != null)
                        Assert.That(hunter.WeaponAttachment.Socket.childCount, Is.EqualTo(1),
                            "Repeated actions must not spawn another equipped sword.");
                    yield return null;
                }
                for (int action = 0; action < 4; action++)
                {
                    string enemy = action % 2 == 0 ? "E1" : "E2";
                    Vector3 enemyHome = arena.EnemyHome(enemy);
                    arena.Present(new CombatEvent(CombatEventKind.AttackStarted,
                        actorId: enemy, actionId: "long-enemy-" + action));
                    for (int tick = 0; !arena.EnemyApproachComplete(enemy) && tick < 100; tick++) arena.Tick(.02f);
                    arena.Present(new CombatEvent(CombatEventKind.ActionSettled,
                        actorId: enemy, actionId: "long-enemy-" + action));
                    for (int tick = 0; !arena.PresentationReady && tick < 300; tick++) arena.Tick(.02f);
                    Assert.That(arena.PresentationReady, Is.True);
                    Assert.That(arena.EnemyPosition(enemy), Is.EqualTo(enemyHome));
                    Assert.That(hunter.transform.localPosition, Is.EqualTo(home));
                    Assert.That(hunter.ModelRoot.localRotation, Is.EqualTo(modelFacing));
                }
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator EnemyKeepsOtherSlotsAndCorpseFallsHoldsThenDissolvesWithoutMoving()
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
                arena.Tick(.51f);
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
                Assert.That(arena.StartHunterApproach("E1"), Is.False,
                    "Player presentation cannot begin before the enemy completes its return and settle.");
                while (!arena.PresentationReady) arena.Tick(.025f);
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
                Assert.That(corpse.transform.localPosition, Is.EqualTo(home));
                Assert.That(corpse.GetComponentInChildren<SkinnedMeshRenderer>().material.shader.name,
                    Is.EqualTo("Rokas/ReactiveCombat/AshDissolve"));
                Assert.That(corpse.transform.localScale, Is.EqualTo(originalScale),
                    "Ash breakup preserves the corpse transform and scale until cleanup.");
                arena.Tick(arena.CorpseDissolveDuration);
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
