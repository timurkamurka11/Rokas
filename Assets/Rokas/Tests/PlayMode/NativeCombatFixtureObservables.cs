using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Tests
{
    // Test-only observables. Never submits commands or authors combat state/poses.
    internal static class NativeCombatFixtureObservables
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        internal static ReactiveCombatArena Arena(RokasBootstrap boot)
        {
            object mission = typeof(RokasView).GetField("mission", Private).GetValue(boot.View);
            object reactive = mission.GetType().GetField("reactiveView", Private).GetValue(mission);
            var arena = reactive.GetType().GetField("arena", Private).GetValue(reactive) as ReactiveCombatArena;
            Assert.That(arena, Is.Not.Null, "Actual Bootstrap must own a live arena.");
            return arena;
        }

        internal static ReactiveCombatActorVisual Enemy(ReactiveCombatArena arena, string id)
        {
            var enemies = (Dictionary<string, ReactiveCombatActorVisual>)typeof(ReactiveCombatArena)
                .GetField("enemies", Private).GetValue(arena);
            Assert.That(enemies.TryGetValue(id, out ReactiveCombatActorVisual actor), Is.True,
                "Missing Core actor ID: " + id);
            Assert.That(actor, Is.Not.Null);
            return actor;
        }

        internal static void AssertNativeAnticipation(ReactiveCombatActorVisual actor,
            LicensedCombatMotionProfile profile, string pose)
        {
            Assert.That(profile, Is.Not.Null);
            Assert.That(actor.ActiveLicensedProfile, Is.SameAs(profile), "Action selects its own Native bank.");
            Assert.That(actor.CurrentPose, Is.EqualTo(pose));
            Assert.That(actor.AwaitingAttackContact, Is.True, "Source action cannot pass contact without its event.");
            var animation = actor.ModelRoot.GetComponent<Animation>();
            Assert.That(animation.enabled, Is.False, "Explicit samples own the pose.");
            Assert.That(animation.playAutomatically, Is.False);
            Assert.That(animation.GetClip("Licensed_Anticipation"), Is.Not.Null);
            Assert.That(animation["Licensed_Anticipation"].enabled, Is.True);
            Assert.That(animation["Licensed_Anticipation"].weight, Is.GreaterThan(0f));
        }

        internal static void AssertTacticalCamera(RokasBootstrap boot)
        {
            Assert.That(Arena(boot).CameraAtHome, Is.True,
                "The exact adaptive tactical mode, lens, position and rotation must be restored before execution.");
        }

        internal static void AssertPassiveStageEnvelope(ReactiveCombatArena arena, Transform world,
            ReactiveCombatActorVisual performer, LicensedCombatMotionProfile profile, string targetId)
        {
            Assert.That(arena.EnemyMotionPhase(targetId), Is.EqualTo("None").Or.EqualTo("StageReturn"),
                "A queued enemy may only undo passive placement; it must not begin an attack.");
            Vector3 home = arena.EnemyHome(targetId);
            Vector3 source = profile != null && profile.sourceStageOffsets
                ? new Vector3(-profile.targetTeamOffset.x, 0f, profile.targetTeamOffset.y) : Vector3.zero;
            float scale = profile == null ? 0f : performer.ModelRoot.lossyScale.y * profile.sourceToTargetScale;
            Vector3 offset = world.InverseTransformVector(world.rotation * source * scale);
            Vector3 remaining = arena.EnemyPosition(targetId) - home;
            if (offset.sqrMagnitude < .000001f)
                Assert.That(remaining.sqrMagnitude, Is.LessThan(.0001f));
            else
            {
                float t = Vector3.Dot(remaining, offset) / offset.sqrMagnitude;
                Assert.That(t, Is.InRange(-.001f, 1.001f));
                Assert.That((remaining - offset * t).sqrMagnitude, Is.LessThan(.0001f),
                    "Passive target remains on the authored placement-to-durable-home path.");
            }
        }

        internal static string Describe(RokasBootstrap boot)
        {
            var combat = boot.Session.ReactiveCombat;
            if (combat == null) return "Core combat missing";
            var arena = Arena(boot);
            return $"phase={combat.Phase}, action={combat.CurrentActionId}, actor={combat.ActiveActorId}, " +
                $"elapsedUs={combat.CurrentCombatUs - combat.CurrentActionStartUs}, held={boot.ReactivePresentationHeld}, " +
                $"paused={boot.View.Paused}, ready={boot.View.ReactivePresentationReady}, " +
                $"hunterMotion={arena.HunterMotionPhase}, enemyMotion={arena.EnemyMotionPhase(combat.ActiveActorId)}";
        }


        internal static float FirstNormalRoundBudget(RokasBootstrap boot)
        {
            const float settle = .16f, idleBlend = .08f, announcement = .85f, schedulingMargin = 2f;
            var arena = Arena(boot);
            var combat = boot.Session.ReactiveCombat;
            var definitions = (CombatDefinitions)typeof(ReactiveCombatSession)
                .GetField("_definitions", Private).GetValue(combat);
            var hunter = GameObject.Find("ReactiveCombatWorld")
                .GetComponentsInChildren<ReactiveCombatActorVisual>(true)
                .First(a => a.name == "CombatActor_Keiko");
            float SourceTail(ReactiveCombatActorVisual actor, bool heavy, float returnTravel)
            {
                var profile = actor.AttackLicensedProfile(heavy);
                Assert.That(profile, Is.Not.Null, "This source-derived budget requires the actual Native profile.");
                return Mathf.Max(profile.Duration, profile.StageRecoveryTime + returnTravel);
            }
            // This begins after accepted Basic reaches PlayerExecution. Preview restore is already done.
            float budget = arena.HunterApproachDuration + hunter.AttackContactSeconds(false) +
                arena.PlayerContactResolutionDelay + SourceTail(hunter, false, arena.HunterReturnDuration) +
                settle + idleBlend + announcement;
            foreach (string id in combat.ActiveEnemyIds)
            {
                var definition = definitions.FindActor(id);
                var sequence = definitions.FindSequence(definition.AttackSequenceIds[0]);
                var actor = Enemy(arena, id);
                budget += (float)(sequence.DurationUs / 1000000d) + .16f +
                    arena.EnemyApproachDuration + settle + idleBlend + announcement;
                // A conservative finite upper envelope: charge each real hit its complete authored tail.
                // The current combo overlaps some of these tails, so this is not predicted runtime duration.
                foreach (var hit in sequence.Hits)
                    budget += SourceTail(actor, hit.IsHeavy, arena.EnemyReturnDuration);
            }
            return Mathf.Ceil(budget + schedulingMargin);
        }

        internal static string DescribeAllPendingActors(RokasBootstrap boot)
        {
            const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object Read(object owner, string name) => owner?.GetType().GetField(name, All)?.GetValue(owner);
            var arena = Arena(boot);
            var enemies = (Dictionary<string, ReactiveCombatActorVisual>)Read(arena, "enemies");
            var motions = (System.Collections.IDictionary)Read(arena, "enemyMotions");
            var text = new System.Text.StringBuilder(1024);
            text.Append(Describe(boot));
            text.Append($"; arenaSettled={arena.PresentationReady}, introComplete={arena.IntroComplete}, " +
                $"settleRemaining={Read(arena, "settleRemaining")}, cameraHome={arena.CameraAtHome}, " +
                $"announcementActive={boot.View.ReactiveAnnouncementActive}, " +
                $"presentationHold={Read(boot, "reactivePresentationHold")}, approachHold={Read(boot, "reactiveApproachHold")}, " +
                $"announcementRequested={Read(boot, "reactiveAnnouncementRequested")}");
            var deferred = Read(boot, "reactiveDeferredEvents") as System.Collections.ICollection;
            text.Append($", deferredCount={deferred?.Count}");
            var world = GameObject.Find("ReactiveCombatWorld");
            void Actor(string id, ReactiveCombatActorVisual actor, object motion, Vector3 home)
            {
                var profile = actor.ActiveLicensedProfile;
                text.Append($" | {id}: motion={Read(motion, "Phase")}, returnRequested={Read(motion, "ReturnRequested")}, " +
                    $"stageOffsetActive={Read(motion, "StageOffsetActive")}, preserveNative={Read(motion, "PreserveNativeRecovery")}, " +
                    $"homeDistance={Vector3.Distance(actor.transform.localPosition, home):F4}, pose={actor.CurrentPose}, " +
                    $"idleSettled={actor.IdleSettled}, actionRecovery={actor.ActionRecoveryComplete}, " +
                    $"native={profile?.sourceSkill}, nativeClock={actor.LicensedMotionClock:F4}, " +
                    $"nativeDuration={profile?.Duration}, contacted={actor.LicensedContactConfirmed}, " +
                    $"awaitingContact={actor.AwaitingAttackContact}, hitStop={actor.HitStopRemaining:F4}, " +
                    $"frozenPrevious={Read(actor, "frozenPreviousPose")}, blendElapsed={Read(actor, "blendElapsed")}, " +
                    $"poseSecondsLeft={Read(actor, "poseSecondsLeft")}");
            }
            var hunter = world.GetComponentsInChildren<ReactiveCombatActorVisual>(true)
                .First(a => a.name == "CombatActor_Keiko");
            Actor("Hunter", hunter, Read(arena, "hunterMotion"), arena.HunterHome);
            foreach (var entry in enemies) Actor(entry.Key, entry.Value, motions[entry.Key], arena.EnemyHome(entry.Key));
            return text.ToString();
        }

        internal static IEnumerator WaitForFirstNormalRoundReady(RokasBootstrap boot)
        {
            float budget = FirstNormalRoundBudget(boot);
            float started = Time.realtimeSinceStartup;
            string atOldDeadline = null, twoSecondsLater = null;
            bool Ready() => boot.Session.ReactiveCombat.Phase == ReactivePhase.PlayerCommand &&
                boot.View.ReactivePresentationReady && !boot.ReactivePresentationHeld && !boot.View.Paused;
            while (!Ready() && Time.realtimeSinceStartup - started < budget)
            {
                float elapsed = Time.realtimeSinceStartup - started;
                if (atOldDeadline == null && elapsed >= 18f) atOldDeadline = DescribeAllPendingActors(boot);
                if (twoSecondsLater == null && elapsed >= 20f) twoSecondsLater = DescribeAllPendingActors(boot);
                Assert.That(boot.Session.ReactiveCombat.Phase, Is.Not.EqualTo(ReactivePhase.SafeError));
                yield return null;
            }
            // Only bounded snapshots, emitted after the wait; no per-frame disk writes/readback.
            TestContext.WriteLine($"First Normal round: elapsed={Time.realtimeSinceStartup - started:F3}s, sourceBudget={budget:F1}s");
            if (atOldDeadline != null) TestContext.WriteLine("18s snapshot: " + atOldDeadline);
            if (twoSecondsLater != null) TestContext.WriteLine("20s snapshot: " + twoSecondsLater);
            Assert.That(Ready(), Is.True, "Finite authored Normal-round gate failed. Final=" +
                DescribeAllPendingActors(boot) + " | 18s=" + atOldDeadline + " | 20s=" + twoSecondsLater);
        }

    }
}
