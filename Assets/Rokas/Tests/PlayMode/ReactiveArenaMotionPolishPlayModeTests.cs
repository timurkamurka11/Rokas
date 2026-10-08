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
        public IEnumerator CancelledPendingSuffixKeepsItsSampleThenRecoversAndSettlesWithoutFakeContact()
        {
            var parent = new GameObject("CancelledPendingContactFixture");
            try
            {
                var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Yokai, parent.transform);
                actor.PlayAttack(); actor.AwaitAttackContact(false);
                actor.TickPresentation(2f);
                float heldSample = actor.CurrentPoseSeconds;
                bool native = actor.ActiveLicensedProfile != null;
                Transform[] sampledBones = actor.ModelRoot.GetComponentsInChildren<Transform>(true);
                var positions = new Vector3[sampledBones.Length];
                var rotations = new Quaternion[sampledBones.Length];
                for (int i = 0; i < sampledBones.Length; i++)
                { positions[i] = sampledBones[i].localPosition; rotations[i] = sampledBones[i].localRotation; }
                Assert.That(actor.AwaitingAttackContact, Is.True);
                actor.CancelPendingAttackContact();
                Assert.That(actor.AwaitingAttackContact, Is.False);
                if (native)
                {
                    Assert.That(actor.CurrentPose, Is.EqualTo("Idle"));
                    Assert.That(actor.ActiveLicensedProfile, Is.Null);
                    Assert.That(actor.LicensedContactConfirmed, Is.False);
                    Assert.That(actor.IdleSettled, Is.False, "The .08s exit starts from the displayed source pose.");
                    for (int i = 0; i < sampledBones.Length; i++)
                    {
                        Assert.That(sampledBones[i].localPosition, Is.EqualTo(positions[i]));
                        Assert.That(PreciseAngle(sampledBones[i].localRotation, rotations[i]), Is.LessThan(.0001f),
                            "Cancellation preserves actual orientation despite quaternion normalization.");
                    }
                }
                else Assert.That(actor.CurrentPoseSeconds, Is.EqualTo(heldSample).Within(.0001f),
                    "Legacy cancellation releases its contact sample without inventing contact.");
                Assert.That(actor.HitStopRemaining, Is.Zero);
                if (!native) Assert.That(actor.ActionRecoveryComplete, Is.False,
                    "Cancelled Legacy contact still owns its follow-through and recovery tail.");
                actor.TickPresentation(.04f);
                if (native) Assert.That(actor.IdleSettled, Is.False);
                actor.TickPresentation(.06f);
                if (native) Assert.That(actor.LicensedContactConfirmed, Is.False);
                else Assert.That(actor.CurrentPoseSeconds, Is.GreaterThan(heldSample));
                for (int tick = 0; !actor.IdleSettled && tick < 100; tick++) actor.TickPresentation(.02f);
                Assert.That(actor.ActionRecoveryComplete, Is.True);
                Assert.That(actor.IdleSettled, Is.True);
                yield return null;
            }
            finally { Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator ResizingExistingRigsAndWaveSlotsPreservesDistanceBasedFootCadence()
        {
            var parent = new GameObject("ScaledCadenceFixture");
            try
            {
                foreach (CombatActorKind kind in new[] { CombatActorKind.Keiko, CombatActorKind.Yokai })
                {
                    ReactiveCombatActorVisual actor = ReactiveCombatActorVisual.Spawn(kind, parent.transform);
                    actor.PlayApproach(5f);
                    actor.SetLocomotionSpeed(2f);
                    actor.TickPresentation(.1f);
                    float referenceTime = actor.CurrentPoseSeconds;
                    Assert.That(referenceTime, Is.GreaterThan(0f));
                    actor.PlayIdle();
                    actor.ModelRoot.localScale *= 2f;
                    actor.PlayApproach(5f);
                    actor.SetLocomotionSpeed(2f);
                    actor.TickPresentation(.1f);
                    Assert.That(actor.CurrentPoseSeconds, Is.EqualTo(referenceTime / 2f).Within(.0001f),
                        "Doubling the same rig doubles its physical stride at the same travel speed.");
                    actor.PlayIdle();
                    actor.transform.localScale = Vector3.one * .83f;
                    actor.PlayApproach(5f);
                    actor.SetLocomotionSpeed(2f);
                    actor.TickPresentation(.1f);
                    Assert.That(actor.CurrentPoseSeconds, Is.EqualTo(referenceTime / (2f * .83f)).Within(.0001f),
                        "Wave slot scale must also affect cadence; it must not use only model-local scale.");
                    float stoppedTime = actor.CurrentPoseSeconds;
                    actor.SetLocomotionSpeed(0f);
                    actor.TickPresentation(.1f);
                    Assert.That(actor.CurrentPoseSeconds, Is.EqualTo(stoppedTime).Within(.0001f));
                }
                yield return null;
            }
            finally { Object.Destroy(parent); }
        }

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
                GameObject equippedSword = hunter.WeaponAttachment.CurrentWeapon;
                Transform hips = hunter.ModelRoot.Find("mixamorig:Hips");
                Transform left = null;
                foreach (Transform bone in hunter.ModelRoot.GetComponentsInChildren<Transform>(true))
                    if (bone.name == "mixamorig:LeftHand") left = bone;
                Assert.That(left, Is.Not.Null);
                var idleReference = new GameObject("NativeIdleCurveReference");
                idleReference.transform.SetParent(root.transform, false);
                var referenceHips = new GameObject(hips.name).transform;
                referenceHips.SetParent(idleReference.transform, false);
                float maxSourceIdlePositionError = 0f, maxSourceIdleRotationError = 0f;
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
                    while (!arena.HunterApproachComplete && ticks++ < 160) arena.Tick(.025f);
                    Assert.That(arena.HunterApproachComplete, Is.True);
                    string alias = heavy ? "Heavy" : "Attack";
                    Assert.That(hunter.CurrentPose, Is.EqualTo(alias));
                    LicensedCombatMotionProfile source = hunter.ActiveLicensedProfile;
                    float pausedTime = source == null ? animation[alias].time : hunter.LicensedMotionClock;
                    yield return new WaitForSecondsRealtime(.12f);
                    Assert.That(source == null ? animation[alias].time : hunter.LicensedMotionClock,
                        Is.EqualTo(pausedTime).Within(.0001f),
                        "The arena clock owns animation; a paused view must not keep playing it.");
                    arena.Present(new CombatEvent(CombatEventKind.CommandCommitted,
                        actorId: ReactiveDuelDefinitions.HunterId, actionId: "polish-" + action,
                        detail: heavy ? "heavy" : "Basic"));
                    Assert.That(source == null ? animation[alias].time : hunter.LicensedMotionClock,
                        Is.EqualTo(pausedTime).Within(.0001f),
                        "The Core commit must not restart the already playing sword windup.");
                    arena.Tick(.2f);
                    arena.Present(new CombatEvent(CombatEventKind.HitResolved,
                        actorId: ReactiveDuelDefinitions.HunterId, targetId: target,
                        actionId: "polish-" + action, amount: 8));
                    float contactTime = source == null ? animation[alias].time : hunter.LicensedMotionClock;
                    if (source != null)
                    {
                        Assert.That(hunter.LicensedContactConfirmed, Is.True);
                        Assert.That(contactTime, Is.Zero);
                        Assert.That(hunter.HitStopRemaining, Is.Zero);
                    }
                    arena.Tick(.04f);
                    Assert.That(source == null ? animation[alias].time : hunter.LicensedMotionClock,
                        Is.EqualTo(source == null ? contactTime : .04f).Within(.0001f));
                    arena.Tick(.05f);
                    arena.Present(new CombatEvent(CombatEventKind.ActionSettled,
                        actorId: ReactiveDuelDefinitions.HunterId, actionId: "polish-" + action));
                    ticks = 0;
                    int recoveryBudget = source == null ? 80 : Mathf.CeilToInt((source.Duration + 2f) / .025f);
                    while (!arena.PresentationReady && ticks++ < recoveryBudget) arena.Tick(.025f);
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
                        if (source == null)
                        {
                            Assert.That(Vector3.Distance(hips.localPosition, idleHipPosition), Is.LessThan(.006f));
                            Assert.That(Quaternion.Angle(hips.localRotation, idleHipRotation), Is.LessThan(3f));
                        }
                        else
                        {
                            // Preserve the actual saved source idle excursion instead of imposing the Legacy breathing cap.
                            source.baseIdle.SampleAnimation(idleReference, animation["Idle"].time);
                            float positionError = Vector3.Distance(hips.localPosition, referenceHips.localPosition);
                            float rotationError = PreciseAngle(hips.localRotation, referenceHips.localRotation);
                            maxSourceIdlePositionError = Mathf.Max(maxSourceIdlePositionError, positionError);
                            maxSourceIdleRotationError = Mathf.Max(maxSourceIdleRotationError, rotationError);
                            Assert.That(positionError, Is.LessThan(.00001f), "Idle must follow its imported source hip curve.");
                            Assert.That(rotationError, Is.LessThan(.0001f), "Idle must retain its imported source hip rotation.");
                        }
                        Transform grip = hunter.WeaponAttachment.CurrentWeapon.transform.Find("LeftHandGrip");
                        if (source == null)
                            Assert.That(Vector3.Distance(left.TransformPoint(new Vector3(0f, .033f, 0f)),
                                grip.position), Is.LessThan(.025f));
                        else AssertNativePalmGrip(hunter, source.weapon == LicensedWeaponKind.TwoHandedSword);
                    }
                    Assert.That(hunter.WeaponAttachment.CurrentWeapon, Is.SameAs(equippedSword),
                        "Repeated actions must reuse the actual owned sword, including helper parenting.");
                    Assert.That(equippedSword.transform.IsChildOf(hunter.ModelRoot), Is.True);
                    if (source == null) Assert.That(hunter.WeaponAttachment.Socket.childCount, Is.EqualTo(1));
                    yield return null;
                }
                TestContext.WriteLine("Native saved-idle hip max position error=" + maxSourceIdlePositionError +
                    " rotation error degrees=" + maxSourceIdleRotationError);
                for (int action = 0; action < 4; action++)
                {
                    string enemy = action % 2 == 0 ? "E1" : "E2";
                    Vector3 enemyHome = arena.EnemyHome(enemy);
                    arena.Present(new CombatEvent(CombatEventKind.AttackStarted,
                        actorId: enemy, actionId: "long-enemy-" + action));
                    for (int tick = 0; !arena.EnemyApproachComplete(enemy) && tick < 100; tick++) arena.Tick(.02f);
                    arena.Tick(.35f);
                    arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: enemy,
                        targetId: ReactiveDuelDefinitions.HunterId, actionId: "long-enemy-" + action, amount: 8));
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
                GameObject world = GameObject.Find("ReactiveCombatWorld");
                ReactiveCombatActorVisual attacker = null;
                foreach (var actor in world.GetComponentsInChildren<ReactiveCombatActorVisual>())
                    if (actor.name == "CombatActor_Yokai" && actor.transform.localPosition == home) attacker = actor;
                Assert.That(attacker, Is.Not.Null);
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
                // Resolve at the Core watermark, then allow the next authored
                // strike to begin after the first contact's hit-stop ends.
                for (int tick = 0; tick < 29; tick++) arena.Tick(.01f);
                Assert.That(arena.EnemyApproachComplete("E1"), Is.True,
                    "A multi-hit attacker stays at the defender until its whole action settles.");
                Assert.That(attacker.AwaitingAttackContact, Is.True);
                arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "E1",
                    targetId: ReactiveDuelDefinitions.HunterId, actionId: "enemy-polish", hitId: "hit-1", amount: 8));
                Assert.That(attacker.AwaitingAttackContact, Is.False);
                for (int tick = 0; tick < 65; tick++) arena.Tick(.01f);
                Assert.That(attacker.AwaitingAttackContact, Is.True,
                    "The second hit must have its own pending swing before its resolution arrives.");
                arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "E1",
                    targetId: ReactiveDuelDefinitions.HunterId, actionId: "enemy-polish", hitId: "hit-2", amount: 8));
                arena.Present(new CombatEvent(CombatEventKind.ActionSettled,
                    actorId: "E1", actionId: "enemy-polish"));
                arena.Tick(.1f);
                Assert.That(arena.EnemyAtHome("E1"), Is.False);
                Assert.That(arena.StartHunterApproach("E1"), Is.False,
                    "Player presentation cannot begin before the enemy completes its return and settle.");
                float completeTail = attacker.ActiveLicensedProfile == null ? 5f : attacker.ActiveLicensedProfile.Duration + 2f;
                for (int tick = 0; !arena.PresentationReady && tick < Mathf.CeilToInt(completeTail / .025f); tick++) arena.Tick(.025f);
                Assert.That(arena.PresentationReady, Is.True,
                    "Both resolved contacts must release their holds and permit return completion.");
                Assert.That(arena.EnemyAtHome("E1"), Is.True);
                Assert.That(arena.EnemyPosition("E1"), Is.EqualTo(home));
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

        private static float PreciseAngle(Quaternion a, Quaternion b)
        {
            Quaternion delta = a.normalized * Quaternion.Inverse(b.normalized);
            double vector = System.Math.Sqrt((double)delta.x * delta.x + (double)delta.y * delta.y + (double)delta.z * delta.z);
            return (float)(2 * System.Math.Atan2(vector, System.Math.Abs(delta.w)) * 180 / System.Math.PI);
        }

        private static void AssertNativePalmGrip(ReactiveCombatActorVisual actor, bool twoHands)
        {
            Transform right = null, left = null;
            foreach (Transform bone in actor.ModelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "mixamorig:RightHand") right = bone;
                if (bone.name == "mixamorig:LeftHand") left = bone;
            }
            Assert.That(right, Is.Not.Null); Assert.That(left, Is.Not.Null);
            Transform sword = actor.WeaponAttachment.CurrentWeapon.transform;
            Vector3 primary = actor.ModelRoot.InverseTransformPoint(sword.position);
            Vector3 rightPalm = actor.ModelRoot.InverseTransformPoint(right.TransformPoint(Vector3.up * .0324945897f));
            Vector3 leftPalm = actor.ModelRoot.InverseTransformPoint(left.TransformPoint(Vector3.up * .0329871997f));
            Assert.That(Mathf.Min(Vector3.Distance(rightPalm, primary), Vector3.Distance(leftPalm, primary)),
                Is.LessThan(.001f), "Source primary palm must hold the real owned hilt.");
            if (twoHands) Assert.That(Vector3.Distance(leftPalm,
                actor.ModelRoot.InverseTransformPoint(sword.Find("LeftHandGrip").position)), Is.LessThan(.001f));
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
