using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class LicensedCombatPresentationPlayModeTests
    {
        private const string CaptureRoot = "D:/DD2-Research/AnimationStudy/RuntimeValidation";
        private GameObject root;
        private ReactiveCombatArena arena;
        private RokasBootstrap boot;
        private string profileDirectory;

        [UnityTest]
        public IEnumerator PoolResetCancelsSourceMotionAndRetiresItsSampleWeights()
        {
            root = new GameObject("LicensedPoolResetFixture");
            var hunter = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
            hunter.PlayPreparation(false);
            hunter.TickPresentation(.25f);
            LicensedCombatMotionProfile profile = ActiveProfile(hunter);
            Assert.That(profile, Is.Not.Null, "The real actor must use its configured licensed preparation.");
            Assert.That(ContactConfirmed(hunter), Is.False, "Elapsed presentation time cannot authorize contact.");
            hunter.ResetForPool();
            Assert.That(ActiveProfile(hunter), Is.Null, "Pool reset must cancel the prior sequence.");
            Assert.That(ContactConfirmed(hunter), Is.False);
            Assert.That(hunter.AwaitingAttackContact, Is.False);
            var animation = hunter.ModelRoot.GetComponent<Animation>();
            foreach (AnimationState state in animation)
                if (state.name.StartsWith("Licensed_", StringComparison.Ordinal))
                    Assert.That(state.enabled && state.weight > 0f, Is.False,
                        "Cancelled sequence still samples the pooled rig: " + state.name);
            hunter.PlayPreparation(true);
            hunter.TickPresentation(.25f);
            Assert.That(ActiveProfile(hunter), Is.Not.Null, "The reset actor must accept a new source motion.");
            Assert.That(ContactConfirmed(hunter), Is.False);
            foreach (Animator animator in hunter.ModelRoot.GetComponentsInChildren<Animator>(true))
            { Assert.That(animator.enabled, Is.False); Assert.That(animator.applyRootMotion, Is.False); }
            Assert.That(animation.enabled, Is.False, "Native autoplay cannot write over the presentation sample.");
            yield return null;
        }



        [UnityTest]
        public IEnumerator CancellingUnconfirmedNativePreparationPreservesTheSampleBeforeBlendingToIdle()
        {
            root = new GameObject("LicensedCancelContinuityFixture", typeof(RectTransform));
            arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            arena.SetEnemies(new[] { "E1" }, null);
            StepUntil(() => arena.PresentationReady, 30f, "cancel fixture readiness");
            ReactiveCombatActorVisual hunter = Hunter();
            Assert.That(arena.SelectHunterPreview("normal"), Is.True);
            for (int tick = 0; tick < 24; tick++) arena.Tick(1f / 60f);
            Assert.That(ActiveProfile(hunter), Is.Not.Null);
            Assert.That(ContactConfirmed(hunter), Is.False);
            var sampledPositions = new Dictionary<Transform, Vector3>();
            var sampledRotations = new Dictionary<Transform, Quaternion>();
            foreach (SkinnedMeshRenderer skin in hunter.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (Transform bone in skin.bones)
                {
                    if (bone == null || sampledPositions.ContainsKey(bone)) continue;
                    sampledPositions.Add(bone, bone.localPosition);
                    sampledRotations.Add(bone, bone.localRotation);
                }
            Assert.That(sampledPositions.Count, Is.GreaterThan(10), "Inspect the real character skeleton.");
            arena.CancelHunterPreview();
            Assert.That(ActiveProfile(hunter), Is.Null);
            Assert.That(ContactConfirmed(hunter), Is.False, "Cancel is not an authorized source contact.");
            Assert.That(hunter.AwaitingAttackContact, Is.False);
            Assert.That(hunter.HitStopRemaining, Is.Zero);
            Assert.That(hunter.IdleSettled, Is.False, "Cancel must finish the actual pose transition before settlement.");
            foreach (var sample in sampledPositions)
            {
                Assert.That(Vector3.Distance(sample.Key.localPosition, sample.Value), Is.LessThan(.00001f),
                    "Cancellation snapped the sampled bone position: " + sample.Key.name);
                Assert.That(PreciseAngle(sample.Key.localRotation, sampledRotations[sample.Key]), Is.LessThan(.01f),
                    "Cancellation snapped the sampled bone rotation: " + sample.Key.name);
            }
            Assert.That(arena.CameraAtHome, Is.True, "Unconfirmed preview cannot activate the source contact shot.");
            Assert.That(ActorCamera().fieldOfView, Is.EqualTo(hunter.DefaultLicensedProfile.camera.baseFov));
            Assert.That(arena.ThrowReleaseCount, Is.Zero);
            Assert.That(arena.ThrowContactCount, Is.Zero);
            Assert.That(GameObject.Find("KeikoThrownDagger"), Is.Null);
            arena.Tick(.04f);
            Assert.That(hunter.IdleSettled, Is.False);
            arena.Tick(.05f);
            Assert.That(hunter.IdleSettled, Is.True, "The configured 0.08s transition must finish through the presentation clock.");
            StepUntil(() => arena.PresentationReady && arena.CameraAtHome, 2f, "cancel camera and rig settlement");
            Assert.That(ContactConfirmed(hunter), Is.False);
            Assert.That(hunter.transform.localPosition, Is.EqualTo(arena.HunterHome));
            Assert.That(arena.ThrowReleaseCount, Is.Zero);
            Assert.That(arena.ThrowContactCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturnMovementToLicensedIdlePreservesSwordPoseBeforeSocketOwnershipChanges()
        {
            root = new GameObject("LicensedIdleSocketHandoffFixture");
            var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
            Transform sword = actor.WeaponAttachment.CurrentWeapon.transform;
            for (int cycle = 0; cycle < 10; cycle++)
            {
                actor.PlayHeavy();
                actor.BindLicensedContact("idle-handoff-" + cycle);
                Assert.That(actor.ConfirmLicensedContact("idle-handoff-" + cycle), Is.True);
                actor.TickPresentation(.6f);
                actor.PlayReturnHome(1f);
                actor.TickPresentation(.09f);
                Assert.That(actor.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.DefaultSocket));
                Vector3 position = sword.position, scale = sword.lossyScale;
                Quaternion rotation = sword.rotation;
                actor.PlayIdle();
                Assert.That(Vector3.Distance(sword.position, position), Is.LessThan(.00001f));
                Assert.That(PreciseAngle(sword.rotation, rotation), Is.LessThan(.01f));
                Assert.That(Vector3.Distance(sword.lossyScale, scale), Is.LessThan(.00001f));
                Assert.That(actor.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.ExitBlend));
                actor.TickPresentation(.04f);
                Assert.That(actor.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.ExitBlend));
                actor.TickPresentation(.05f);
                Assert.That(actor.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.DefaultSocket));
                Assert.That(sword.parent.name, Is.EqualTo("LicensedWeaponSocket"));
                Assert.That(sword.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(sword.localRotation, Is.EqualTo(Quaternion.identity));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NativeSwordExitPreservesPoseAndTransfersOneAuthorityAfterTheBlend()
        {
            root = new GameObject("LicensedSwordAuthorityFixture");
            var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
            Transform sword = actor.WeaponAttachment.CurrentWeapon.transform;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                actor.PlayHeavy();
                actor.BindLicensedContact("owned-sword-" + cycle);
                Assert.That(actor.ConfirmLicensedContact("owned-sword-" + cycle), Is.True);
                actor.TickPresentation(.6f);
                Assert.That(actor.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.NativeMotion));
                Assert.That(sword.parent.name, Is.EqualTo("LicensedWeaponSocket"));
                Vector3 position = sword.position, scale = sword.lossyScale;
                Quaternion rotation = sword.rotation;
                actor.PlayReturnHome(.5f);
                Assert.That(actor.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.ExitBlend));
                Assert.That(Vector3.Distance(sword.position, position), Is.LessThan(.00001f), "Handoff frame translated the blade.");
                Assert.That(PreciseAngle(sword.rotation, rotation), Is.LessThan(.001f), "Handoff frame rotated the blade.");
                Assert.That(Vector3.Distance(sword.lossyScale, scale), Is.LessThan(.00001f));
                actor.TickPresentation(.04f);
                Assert.That(actor.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.ExitBlend));
                actor.TickPresentation(.05f);
                Assert.That(actor.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.DefaultSocket));
                Assert.That(sword.parent, Is.SameAs(actor.WeaponAttachment.Socket));
                Assert.That(sword.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(PreciseAngle(sword.localRotation, Quaternion.identity), Is.LessThan(.001f));
                actor.ResetForPool();
                Assert.That(actor.WeaponAttachment.CurrentWeapon.transform, Is.SameAs(sword));
                Assert.That(ActiveProfile(actor), Is.Null);
                foreach (Animator animator in actor.ModelRoot.GetComponentsInChildren<Animator>(true))
                    Assert.That(animator.enabled, Is.False);
                Assert.That(actor.ModelRoot.GetComponent<Animation>().enabled, Is.False);
            }
            yield return null;
        }
        private static float PreciseAngle(Quaternion a, Quaternion b)
        {
            Quaternion delta = b.normalized * Quaternion.Inverse(a.normalized);
            double vector = Math.Sqrt((double)delta.x * delta.x + (double)delta.y * delta.y + (double)delta.z * delta.z);
            return (float)(2 * Math.Atan2(vector, Math.Abs((double)delta.w)) * 180 / Math.PI);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator LivePreviewCancelNormalAndThrowPreserveCoreContactAuthority()
        {
            yield return EnterLiveEncounter();
            ReactiveCombatSession combat = boot.Session.ReactiveCombat;
            ReactiveCombatActorVisual hunter = Hunter();
            ReactiveCombatArena liveArena = FindLiveArena();
            Camera camera = ActorCamera();
            var tactical = new CameraState(camera);
            Vector3 home = hunter.transform.localPosition;
            int ap = combat.HunterAp, hp = combat.GetActorState("E1").Hp;
            long revision = combat.Revision;
            foreach (string button in new[] { "ReactiveBasic", "ReactiveHeavy", "ReactiveThrow" })
            {
                Click(button);
                yield return WaitForLive(() => ActiveProfile(hunter) != null, 2f, button + " source preparation");
                yield return new WaitForSecondsRealtime(.4f);
                yield return WaitForLive(() => combat.Phase != ReactivePhase.Suspended, 4f, "preview resume");
                Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
                Assert.That(combat.HunterAp, Is.EqualTo(ap));
                Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp));
                Assert.That(combat.Revision, Is.EqualTo(revision));
                Assert.That(hunter.transform.localPosition, Is.EqualTo(home));
                Assert.That(ContactConfirmed(hunter), Is.False);
                CaptureLiveUi(button + "-licensed-preview");
            }
            Assert.That(boot.CancelReactivePreview(), Is.True);
            yield return WaitForLive(() => Ready("ReactiveBasic") && hunter.IdleSettled &&
                liveArena.CameraAtHome && combat.Phase == ReactivePhase.PlayerCommand, 5f, "cancel to tactical");
            tactical.AssertRestored(camera);
            Assert.That(ActiveProfile(hunter), Is.Null);
            Assert.That(combat.Revision, Is.EqualTo(revision));
            Assert.That(combat.HunterAp, Is.EqualTo(ap));
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp));
            CaptureLiveUi("cancel-restored-tactical");

            Click("ReactiveBasic"); Click("ReactiveBasic");
            yield return WaitForLive(() => combat.Phase == ReactivePhase.PlayerExecution, 5f, "normal commit");
            string normalAction = combat.CurrentActionId;
            yield return WaitForLive(() => combat.GetActorState("E1").Hp < hp, 10f, "real normal Core contact");
            Assert.That(ActiveProfile(hunter), Is.Not.Null, "Contact must start the configured source action timeline.");
            Assert.That(ContactConfirmed(hunter), Is.True);
            Assert.That(hunter.AwaitingAttackContact, Is.False);
            CaptureLiveUi("normal-authoritative-contact");
            yield return WaitForLive(() => ActiveProfile(hunter) != null && MotionClock(hunter) >= .2f,
                5f, "normal source recovery clock");
            float clock = MotionClock(hunter);
            int contactHp = combat.GetActorState("E1").Hp;
            long contactRevision = combat.Revision;
            liveArena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: "E1",
                actionId: normalAction, amount: hp - contactHp));
            Assert.That(MotionClock(hunter), Is.EqualTo(clock).Within(.0001f),
                "Duplicate presentation contact cannot restart source action/recovery.");
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(contactHp));
            Assert.That(combat.Revision, Is.EqualTo(contactRevision), "Presentation cannot issue domain damage.");
            yield return WaitForLive(() => boot.View.HunterAtHome && hunter.IdleSettled &&
                liveArena.CameraAtHome, 12f, "normal exact home");
            tactical.AssertRestored(camera);
            Assert.That(hunter.transform.localPosition, Is.EqualTo(home));
            CaptureLiveUi("normal-tactical-return");

            yield return WaitForLive(() => combat.Phase == ReactivePhase.PlayerCommand && Ready("ReactiveThrow") &&
                !boot.ReactivePresentationHeld, 65f, "next player command");
            boot.SelectReactiveTarget("E1");
            hp = combat.GetActorState("E1").Hp;
            ap = combat.HunterAp;
            int releases = liveArena.ThrowReleaseCount, contacts = liveArena.ThrowContactCount;
            Click("ReactiveThrow"); Click("ReactiveThrow");
            yield return WaitForLive(() => combat.Phase == ReactivePhase.PlayerExecution, 5f, "throw commit");
            string throwAction = combat.CurrentActionId;
            bool sawFlight = false;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (combat.GetActorState("E1").Hp == hp && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(hunter.transform.localPosition, Is.EqualTo(home), "Throw cannot become a melee approach.");
                if (GameObject.Find("KeikoThrownDagger") != null)
                {
                    sawFlight = true;
                    Assert.That(hunter.HeldDagger.gameObject.activeInHierarchy, Is.False);
                    Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp), "Release alone cannot deal damage.");
                }
                yield return null;
            }
            Assert.That(sawFlight, Is.True, "A real owned dagger must leave the hand before contact.");
            Assert.That(combat.GetActorState("E1").Hp, Is.LessThan(hp));
            Assert.That(combat.HunterAp, Is.EqualTo(ap - 2));
            Assert.That(liveArena.ThrowReleaseCount, Is.EqualTo(releases + 1));
            Assert.That(liveArena.ThrowContactCount, Is.EqualTo(contacts + 1));
            Assert.That(ContactConfirmed(hunter), Is.True);
            CaptureLiveUi("throw-authoritative-contact");
            clock = MotionClock(hunter);
            contactHp = combat.GetActorState("E1").Hp;
            contactRevision = combat.Revision;
            liveArena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: "E1",
                actionId: throwAction, amount: hp - contactHp));
            Assert.That(MotionClock(hunter), Is.EqualTo(clock).Within(.0001f));
            Assert.That(liveArena.ThrowReleaseCount, Is.EqualTo(releases + 1));
            Assert.That(liveArena.ThrowContactCount, Is.EqualTo(contacts + 1));
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(contactHp));
            Assert.That(combat.Revision, Is.EqualTo(contactRevision));
            yield return WaitForLive(() => boot.View.HunterAtHome && hunter.IdleSettled &&
                liveArena.CameraAtHome && GameObject.Find("KeikoThrownDagger") == null, 12f, "throw exact home");
            tactical.AssertRestored(camera);
            Assert.That(hunter.HeldDagger.gameObject.activeInHierarchy, Is.False);
            Assert.That(hunter.WeaponAttachment.CurrentWeapon.transform.IsChildOf(hunter.ModelRoot), Is.True,
                "Recovered own sword must remain equipped on the actual actor rig.");
            Assert.That(hunter.transform.localPosition, Is.EqualTo(home));
            CaptureLiveUi("throw-tactical-return");
        }

        [UnityTest]
        [Timeout(480000)]
        public IEnumerator SourcePresentationsRenderAtDeterministic60HzAndRetireTheirCameraResources()
        {
            // These render the production arena/rig at deterministic presentation
            // steps. They are not a recording of live Core/gameplay wall time.
            foreach (string scenario in new[] { "normal", "heavy", "throw", "block", "dodge", "enemy", "final_elite_E8" })
            {
                root = new GameObject("LicensedCapture_" + scenario, typeof(RectTransform));
                arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
                Camera camera = ActorCamera();
                var tactical = new CameraState(camera);
                string enemyId = scenario == "final_elite_E8" ? "E8" : "E1";
                string folder = Path.Combine(CaptureRoot, "Deterministic60Hz", scenario);
                Directory.CreateDirectory(folder);
                arena.BeginEncounterIntro();
                arena.SetEnemies(new[] { enemyId }, null);
                bool capturedEntry = false;
                for (int tick = 0; !arena.PresentationReady && tick < 2400; tick++)
                {
                    arena.Tick(1f / 60f);
                    if (scenario == "final_elite_E8" && !capturedEntry &&
                        arena.PortalPhase == ReactiveCombatArena.WavePortalPhase.EmittingEnemies)
                    {
                        SaveActorImage(camera, Path.Combine(folder, "final-elite-portal-entry.png"));
                        capturedEntry = true;
                    }
                }
                Assert.That(arena.PresentationReady, Is.True, "The actual entrance must settle: " + scenario);
                if (scenario == "final_elite_E8") Assert.That(capturedEntry, Is.True);
                ReactiveCombatActorVisual hunter = Hunter();
                ReactiveCombatActorVisual enemy = Enemy();
                Vector3 hunterHome = arena.HunterHome, enemyHome = arena.EnemyHome(enemyId);
                SaveActorImage(camera, Path.Combine(folder, "tactical-before.png"));
                string actionId = "licensed-evidence-" + scenario;
                bool hunterAction = scenario == "normal" || scenario == "heavy" || scenario == "throw";
                LicensedCombatMotionProfile sourceProfile;
                CombatEvent pendingContact, pendingSettlement;
                ReactiveCombatActorVisual sourceActor = hunterAction ? hunter : enemy;
                if (hunterAction)
                {
                    bool heavy = scenario == "heavy", throwing = scenario == "throw";
                    Assert.That(arena.SelectHunterPreview(throwing ? "throw_blade" : heavy ? "heavy" : "normal"), Is.True);
                    for (int tick = 0; tick < 30; tick++) arena.Tick(1f / 60f);
                    Assert.That(ActiveProfile(hunter), Is.Not.Null);
                    Assert.That(ContactConfirmed(hunter), Is.False);
                    SaveActorImage(camera, Path.Combine(folder, "source-anticipation.png"));
                    Assert.That(arena.ConfirmHunterPreview(), Is.True);
                    StepUntil(() => arena.PreviewConfirmed, 3f, "camera restore before confirmation");
                    tactical.AssertRestored(camera);
                    Assert.That(throwing ? arena.StartHunterThrow(enemyId) : arena.StartHunterApproach(enemyId, heavy), Is.True);
                    StepUntil(() => arena.HunterApproachComplete && hunter.AwaitingAttackContact,
                        12f, "licensed action anticipation");
                    arena.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P",
                        actionId: actionId, detail: throwing ? "throw_blade" : heavy ? "heavy" : "normal"));
                    if (throwing)
                    {
                        float preContact = hunter.ThrowContactSeconds - 2f / 60f;
                        while (hunter.CurrentPoseSeconds + 1f / 60f < preContact) arena.Tick(1f / 60f);
                        arena.Tick(Mathf.Max(0f, preContact - hunter.CurrentPoseSeconds));
                        Assert.That(arena.ThrowReleaseCount, Is.EqualTo(1));
                        Assert.That(arena.ThrowContactCount, Is.Zero);
                        Assert.That(GameObject.Find("KeikoThrownDagger"), Is.Not.Null);
                    }
                    sourceProfile = ActiveProfile(hunter);
                    AssertSource(sourceProfile);
                    pendingContact = new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: enemyId,
                        actionId: actionId, amount: heavy ? 40 : 20);
                    pendingSettlement = new CombatEvent(CombatEventKind.ActionSettled, actorId: "P", actionId: actionId);
                }
                else
                {
                    bool elite = scenario == "final_elite_E8";
                    var hits = elite ? new[] {
                        new HitDefinition("h1", 1000000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry),
                        new HitDefinition("h2", 1650000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry),
                        new HitDefinition("h3", 2800000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry)
                    } : new[] { new HitDefinition("h1", 1000000, 8,
                        DefenseResponseMask.Dodge | DefenseResponseMask.Parry) };
                    var sequence = new AttackSequenceDefinition(elite ? "final_triple" : "single",
                        elite ? 4100000 : 1600000, hits, interruptibleOnBreak: elite);
                    arena.Present(new CombatEvent(CombatEventKind.AttackStarted, actorId: enemyId,
                        actionId: actionId, detail: sequence.Id), sequence);
                    StepUntil(() => arena.EnemyApproachComplete(enemyId) && enemy.AwaitingAttackContact,
                        12f, "licensed enemy anticipation");
                    sourceProfile = ActiveProfile(enemy);
                    AssertSource(sourceProfile);
                    Assert.That(ContactConfirmed(enemy), Is.False);
                    bool defense = scenario == "block" || scenario == "dodge";
                    if (defense)
                    {
                        arena.BeginDefense(scenario == "dodge", .2f);
                        for (int tick = 0; tick < 12; tick++) arena.Tick(1f / 60f);
                        Assert.That(hunter.DefenseActive, Is.True);
                        Assert.That(hunter.CurrentPose, Is.EqualTo(scenario == "dodge" ? "Dodge" : "Guard"));
                    }
                    SaveActorImage(camera, Path.Combine(folder, "source-anticipation.png"));
                    pendingContact = new CombatEvent(CombatEventKind.HitResolved, actorId: enemyId, targetId: "P",
                        actionId: actionId, hitId: "h1", amount: defense ? 0 : 8,
                        detail: scenario == "dodge" ? "Dodge" : scenario == "block" ? "Parry" : "Hit");
                    // End the bounded first-hit presentation without inventing
                    // later suffix damage; the current full recovery still runs.
                    pendingSettlement = new CombatEvent(CombatEventKind.ActionSettled, actorId: enemyId, actionId: actionId);
                }
                arena.Tick(0f);
                const int contactFrame = 2;
                int frameCount = contactFrame + Mathf.CeilToInt((sourceProfile.Duration + .2f) * 60f);
                var records = new List<PresentationFrame>(frameCount);
                var readback = new Texture2D(camera.targetTexture.width, camera.targetTexture.height,
                    TextureFormat.RGB24, false);
                bool sawSourceShot = false, previousNative = true;
                int exitFrame = -1, socketRestoredFrame = -1;
                try
                {
                    for (int frame = 0; frame < frameCount; frame++)
                    {
                        if (frame == contactFrame)
                        {
                            arena.Present(pendingContact);
                            Assert.That(ContactConfirmed(sourceActor), Is.True);
                            Assert.That(MotionClock(sourceActor), Is.EqualTo(0f).Within(.000001f));
                            Assert.That(arena.HitStopRemaining, Is.Zero, "Source timeline holds do not need an extra R11 freeze.");
                            arena.Present(pendingSettlement);
                            arena.Tick(0f);
                        }
                        if (frame < contactFrame)
                        {
                            Assert.That(ContactConfirmed(sourceActor), Is.False);
                            tactical.AssertRestored(camera); // A source shot cannot precede authoritative contact.
                        }
                        // Let the production LateUpdate and skin renderer finish this manual sample.
                        // These components do not advance the standalone arena clock.
                        yield return null;
                        bool native = ActiveProfile(sourceActor) != null;
                        if (frame > contactFrame && previousNative && !native)
                        {
                            exitFrame = frame;
                            records[frame - 1].boundary = "E-1 native";
                        }
                        if (exitFrame >= 0 && socketRestoredFrame < 0 && hunterAction &&
                            hunter.SwordTransformOwner == ReactiveCombatActorVisual.SwordTransformAuthority.DefaultSocket)
                            socketRestoredFrame = frame;
                        string boundary = frame < contactFrame + 3
                            ? "N" + (frame == contactFrame ? " contact" : (frame - contactFrame).ToString("+0;-0")) :
                            frame == exitFrame ? "E handoff" : frame == exitFrame + 1 ? "E+1 blend" :
                            frame == socketRestoredFrame ? "E+k socket restored" : null;
                        sawSourceShot |= !camera.orthographic && camera.fieldOfView < 30f;
                        records.Add(new PresentationFrame {
                            frame = frame, presentationSeconds = (frame - contactFrame) / 60f, boundary = boundary,
                            hunterPose = hunter.CurrentPose, enemyPose = enemy.CurrentPose,
                            hunterMotionClock = MotionClock(hunter), enemyMotionClock = MotionClock(enemy),
                            hunterContactConfirmed = ContactConfirmed(hunter), enemyContactConfirmed = ContactConfirmed(enemy),
                            hunterPosition = hunter.transform.localPosition, enemyPosition = enemy.transform.localPosition,
                            cameraPosition = camera.transform.localPosition, cameraRotation = camera.transform.localEulerAngles,
                            cameraOrthographic = camera.orthographic, cameraFov = camera.fieldOfView,
                            cameraOrthographicSize = camera.orthographicSize, hitStop = arena.HitStopRemaining,
                            cameraQuaternion = camera.transform.localRotation, cameraProjection = camera.projectionMatrix,
                            attackerViewport = camera.WorldToViewportPoint(sourceActor.TorsoPoint),
                            defenderViewport = camera.WorldToViewportPoint((hunterAction ? enemy : hunter).TorsoPoint),
                            sword = CaptureSword(hunter),
                            sourceBones = boundary != null || MotionClock(sourceActor) >= sourceProfile.Duration - 2f / 60f
                                ? CaptureBones(sourceActor) : null
                        });
                        previousNative = native;
                        SaveActorImage(camera, Path.Combine(folder, "frame-" + frame.ToString("D4") + ".png"), readback);
                        arena.Tick(1f / 60f);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(readback); }
                Assert.That(sawSourceShot, Is.True, "Accepted licensed contact must activate the source perspective shot.");
                Assert.That(exitFrame, Is.GreaterThan(contactFrame), "Full source recovery must reach its native exit in the capture.");
                Assert.That(records[exitFrame - 1].sourceBones, Is.Not.Null);
                Assert.That(records[exitFrame + 1].sourceBones, Is.Not.Null);
                if (hunterAction && scenario != "throw")
                    Assert.That(records[exitFrame].sword.authority, Is.EqualTo("ExitBlend"));
                if (scenario == "throw")
                { Assert.That(arena.ThrowReleaseCount, Is.EqualTo(1)); Assert.That(arena.ThrowContactCount, Is.EqualTo(1)); }
                StepUntil(() => arena.PresentationReady && arena.CameraAtHome, 15f, "full source recovery and tactical return");
                Assert.That(hunter.transform.localPosition, Is.EqualTo(hunterHome));
                Assert.That(enemy.transform.localPosition, Is.EqualTo(enemyHome));
                Assert.That(hunter.IdleSettled && enemy.IdleSettled, Is.True);
                tactical.AssertRestored(camera);
                Assert.That(ActiveProfile(hunter), Is.Null);
                Assert.That(ActiveProfile(enemy), Is.Null);
                SaveActorImage(camera, Path.Combine(folder, "tactical-after.png"));
                File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(new PresentationManifest {
                    scenario = scenario,
                    mode = "production-presentation-simulation-at-60Hz; no live Core session; no audio",
                    sourceSkill = sourceProfile.sourceSkill, sourceTimeline = sourceProfile.sourceTimeline,
                    sourceAssetIdentifiers = sourceProfile.sourceAssetIdentifiers,
                    fps = 60, width = camera.targetTexture.width, height = camera.targetTexture.height,
                    frameCount = frameCount, contactFrame = contactFrame, exitFrame = exitFrame,
                    socketRestoredFrame = socketRestoredFrame, sourceDuration = sourceProfile.Duration,
                    presentationDuration = frameCount / 60f,
                    frames = records.ToArray()
                }, true));
                RenderTexture ownedTexture = camera.targetTexture;
                Material ownedComposite = root.GetComponentInChildren<RawImage>().material;
                GameObject ownedWorld = GameObject.Find("ReactiveCombatWorld");
                arena.Dispose(); arena = null;
                UnityEngine.Object.Destroy(root); root = null;
                yield return null;
                Assert.That(ownedWorld == null, Is.True, "The arena must destroy its world.");
                Assert.That(ownedTexture == null, Is.True, "The arena must destroy its render texture.");
                if (ownedComposite != null && ownedComposite.name == "Reactive actor UI composite")
                    Assert.Fail("The arena leaked its owned UI composite material.");
            }
        }


        [UnityTest]
        public IEnumerator AuthoredCameraShotEndsOnTimeWhileNativeBodyRecoveryContinues()
        {
            root = new GameObject("LicensedCameraTimingFixture", typeof(RectTransform));
            arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            arena.SetEnemies(new[] { "E1" }, null);
            StepUntil(() => arena.PresentationReady, 30f, "camera timing fixture readiness");
            Camera camera = ActorCamera();
            var tactical = new CameraState(camera);
            var hunter = Hunter();
            Assert.That(arena.StartHunterApproach("E1", true), Is.True);
            StepUntil(() => arena.HunterApproachComplete && hunter.AwaitingAttackContact, 12f, "native heavy preparation");
            arena.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P",
                actionId: "source-camera-timing", detail: "heavy"));
            var profile = ActiveProfile(hunter);
            AssertSource(profile);
            Assert.That(profile.camera, Is.Not.Null);
            Assert.That(profile.camera.shotDuration, Is.EqualTo(83f / 60f).Within(.000001f),
                "This selected shared source shot lasts exactly 83 frames at 60Hz.");
            arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: "E1",
                actionId: "source-camera-timing", amount: 40));
            arena.Tick(0f);
            Assert.That(ContactConfirmed(hunter), Is.True);
            Assert.That(arena.HitStopRemaining, Is.Zero);
            Assert.That(hunter.HitStopRemaining, Is.Zero);
            Assert.That(camera.orthographic, Is.False, "The source action frame must start at contact, not a later tick.");
            Assert.That(camera.fieldOfView, Is.EqualTo(profile.camera.shotFov).Within(.0001f));
            arena.Tick(82f / 60f);
            Assert.That(arena.CameraAtHome, Is.False, "The final source blend sample still belongs to this shot.");
            Assert.That(ActiveProfile(hunter), Is.SameAs(profile));
            arena.Tick(1f / 60f);
            Assert.That(arena.CameraAtHome, Is.True, "The shot must end at 1.3833333 seconds without an extra hit-stop delay.");
            tactical.AssertRestored(camera);
            Assert.That(ActiveProfile(hunter), Is.SameAs(profile), "Camera return must not truncate the longer native body recovery.");
            Assert.That(hunter.LicensedMotionClock, Is.EqualTo(83f / 60f).Within(.000001f));
            Assert.That(hunter.ActionRecoveryComplete, Is.False);
            arena.Present(new CombatEvent(CombatEventKind.ActionSettled, actorId: "P", actionId: "source-camera-timing"));
            StepUntil(() => arena.PresentationReady && arena.CameraAtHome, 12f, "source recovery and exact tactical return");
            tactical.AssertRestored(camera);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConfirmedNativeInterruptionPreservesDisplayedPoseAndReturnsThroughOwnedTransitions()
        {
            root = new GameObject("LicensedMidContactCancelFixture", typeof(RectTransform));
            arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            arena.SetEnemies(new[] { "E1" }, null);
            StepUntil(() => arena.PresentationReady, 30f, "interruption fixture readiness");
            Camera camera = ActorCamera();
            var tactical = new CameraState(camera);
            var hunter = Hunter();
            Assert.That(arena.StartHunterApproach("E1", true), Is.True);
            StepUntil(() => arena.HunterApproachComplete && hunter.AwaitingAttackContact, 12f, "native heavy approach");
            arena.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P",
                actionId: "native-mid-contact-cancel", detail: "heavy"));
            AssertSource(ActiveProfile(hunter));
            arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: "E1",
                actionId: "native-mid-contact-cancel", amount: 40));
            arena.Tick(.6f);
            yield return null; // Include the displayed production LateUpdate pose.
            Assert.That(ContactConfirmed(hunter), Is.True);
            Assert.That(hunter.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.NativeMotion));
            Assert.That(camera.orthographic, Is.False);
            Assert.That(Vector3.Distance(hunter.transform.localPosition, arena.HunterHome), Is.GreaterThan(.1f),
                "This interruption must exercise a real return journey, not an actor already at home.");
            Vector3 actorPosition = hunter.transform.localPosition, modelPosition = hunter.ModelRoot.localPosition;
            Quaternion actorRotation = hunter.transform.localRotation, modelRotation = hunter.ModelRoot.localRotation;
            Vector3 modelScale = hunter.ModelRoot.localScale;
            BonePose[] bones = CaptureBones(hunter);
            SwordPose sword = CaptureSword(hunter);
            GameObject originalSword = hunter.WeaponAttachment.CurrentWeapon;
            Vector3 cameraPosition = camera.transform.localPosition;
            Quaternion cameraRotation = camera.transform.localRotation;
            float cameraFov = camera.fieldOfView, cameraSize = camera.orthographicSize;
            Matrix4x4 cameraProjection = camera.projectionMatrix;
            arena.CancelHunterMotion();
            Assert.That(hunter.transform.localPosition, Is.EqualTo(actorPosition), "Cancel cannot teleport the actor root.");
            Assert.That(PreciseAngle(hunter.transform.localRotation, actorRotation), Is.LessThan(.001f));
            Assert.That(hunter.ModelRoot.localPosition, Is.EqualTo(modelPosition));
            Assert.That(PreciseAngle(hunter.ModelRoot.localRotation, modelRotation), Is.LessThan(.001f));
            Assert.That(hunter.ModelRoot.localScale, Is.EqualTo(modelScale));
            AssertBoneSnapshotsEqual(bones, CaptureBones(hunter), "cancel instant");
            SwordPose cancelledSword = CaptureSword(hunter);
            Assert.That(Vector3.Distance(cancelledSword.worldPosition, sword.worldPosition), Is.LessThan(.00001f));
            Assert.That(PreciseAngle(cancelledSword.worldRotation, sword.worldRotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(cancelledSword.worldScale, sword.worldScale), Is.LessThan(.00001f));
            Assert.That(camera.orthographic, Is.False, "Cancel must preserve the displayed projection before its exit blend.");
            Assert.That(camera.fieldOfView, Is.EqualTo(cameraFov));
            Assert.That(camera.orthographicSize, Is.EqualTo(cameraSize));
            Assert.That(camera.transform.localPosition, Is.EqualTo(cameraPosition));
            Assert.That(PreciseAngle(camera.transform.localRotation, cameraRotation), Is.LessThan(.001f));
            for (int component = 0; component < 16; component++)
                Assert.That(camera.projectionMatrix[component], Is.EqualTo(cameraProjection[component]).Within(.000001f));
            Assert.That(ActiveProfile(hunter), Is.Null);
            Assert.That(ContactConfirmed(hunter), Is.False);
            Assert.That(hunter.AwaitingAttackContact, Is.False);
            Assert.That(arena.HitStopRemaining, Is.Zero);
            arena.Tick(.04f);
            Assert.That(arena.CameraAtHome, Is.False);
            Assert.That(hunter.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.ExitBlend));
            Assert.That(hunter.CurrentPose, Is.EqualTo("ReturnHome"));
            Assert.That(hunter.ActionRecoveryComplete, Is.False);
            Assert.That(arena.HunterAtHome, Is.False);
            arena.Tick(.05f);
            Assert.That(arena.CameraAtHome, Is.True, "The camera's 0.08s interruption blend must finish independently of travel.");
            tactical.AssertRestored(camera);
            Assert.That(hunter.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.DefaultSocket));
            Assert.That(hunter.WeaponAttachment.CurrentWeapon, Is.SameAs(originalSword));
            Assert.That(originalSword.transform.parent, Is.SameAs(hunter.WeaponAttachment.Socket));
            Assert.That(arena.HunterAtHome, Is.False, "The 0.72s return must continue after camera/weapon handoff.");
            StepUntil(() => arena.PresentationReady && arena.CameraAtHome, 3f, "interrupted actor return and settle");
            Assert.That(hunter.transform.localPosition, Is.EqualTo(arena.HunterHome));
            Assert.That(hunter.IdleSettled, Is.True);
            Assert.That(hunter.WeaponAttachment.CurrentWeapon, Is.SameAs(originalSword));
            Assert.That(arena.ThrowReleaseCount, Is.Zero);
            Assert.That(arena.ThrowContactCount, Is.Zero);
            Assert.That(GameObject.Find("KeikoThrownDagger"), Is.Null);
            tactical.AssertRestored(camera);
        }

        [UnityTest]
        public IEnumerator TacticalWideUsesTheFixedNormalHeroCalibrationAcrossDifferentNativeActions()
        {
            root = new GameObject("LicensedTacticalWideFixture", typeof(RectTransform));
            arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            arena.SetEnemies(new[] { "E1", "E2" }, null, new[] { "E1", "E2" });
            StepUntil(() => arena.PresentationReady, 30f, "wide camera fixture readiness");
            var hunter = Hunter();
            Camera camera = ActorCamera();
            LicensedCombatMotionProfile normal = hunter.DefaultLicensedProfile;
            AssertSource(normal);
            Assert.That(normal.camera, Is.Not.Null);
            Assert.That(normal.camera.baseFov, Is.EqualTo(38f));
            float heroNormalScale = hunter.ModelRoot.lossyScale.y * normal.sourceToTargetScale;
            Vector3 widePosition = new Vector3(0f, arena.HunterHome.y, 0f) + normal.camera.basePosition * heroNormalScale;
            float wideHalfHeight = -normal.camera.basePosition.z * heroNormalScale * Mathf.Tan(19f * Mathf.Deg2Rad);
            Assert.That(heroNormalScale, Is.GreaterThan(0f));
            Assert.That(wideHalfHeight, Is.GreaterThan(0f));
            var tactical = new CameraState(camera);
            AssertTacticalWide(camera, widePosition, wideHalfHeight);
            // A BasicDamage camera-root translation is action framing, not part of the source wide camera.
            Vector3 wronglyPrefixedWide = widePosition + new Vector3(0f, 0f, -6f) * heroNormalScale;
            Assert.That(Vector3.Distance(camera.transform.localPosition, wronglyPrefixedWide), Is.GreaterThan(.1f));
            GameObject originalSword = hunter.WeaponAttachment.CurrentWeapon;
            foreach (string action in new[] { "normal", "heavy", "throw" })
            {
                bool heavy = action == "heavy", throwing = action == "throw";
                Assert.That(arena.SelectHunterPreview(throwing ? "throw_blade" : action), Is.True);
                arena.Tick(.4f);
                AssertSource(ActiveProfile(hunter));
                AssertTacticalWide(camera, widePosition, wideHalfHeight);
                Assert.That(arena.ConfirmHunterPreview(), Is.True);
                StepUntil(() => arena.PreviewConfirmed, 3f, "wide camera confirmation");
                Assert.That(throwing ? arena.StartHunterThrow("E1") : arena.StartHunterApproach("E1", heavy), Is.True);
                StepUntil(() => arena.HunterApproachComplete && hunter.AwaitingAttackContact, 12f, "wide camera native action");
                arena.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P",
                    actionId: "wide-" + action, detail: throwing ? "throw_blade" : action));
                if (throwing)
                    StepUntil(() => arena.ThrowReleaseCount == 1, 3f, "wide camera physical Throw release");
                arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: "E1",
                    actionId: "wide-" + action, amount: 20));
                Assert.That(ContactConfirmed(hunter), Is.True);
                Assert.That(camera.orthographic, Is.False);
                arena.Present(new CombatEvent(CombatEventKind.ActionSettled, actorId: "P", actionId: "wide-" + action));
                StepUntil(() => arena.PresentationReady && arena.CameraAtHome, 15f, "wide camera source recovery");
                AssertTacticalWide(camera, widePosition, wideHalfHeight);
                tactical.AssertRestored(camera);
                Assert.That(hunter.ModelRoot.lossyScale.y * normal.sourceToTargetScale, Is.EqualTo(heroNormalScale));
                Assert.That(hunter.WeaponAttachment.CurrentWeapon, Is.SameAs(originalSword));
            }
            // A differently scaled performer must not recalibrate the encounter's tactical camera.
            var enemy = Enemy();
            enemy.SetStandingHeight(3.1f);
            arena.Present(new CombatEvent(CombatEventKind.AttackStarted, actorId: "E1", actionId: "wide-enemy"));
            StepUntil(() => arena.EnemyApproachComplete("E1") && enemy.AwaitingAttackContact, 12f, "wide camera enemy action");
            AssertSource(ActiveProfile(enemy));
            arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "E1", targetId: "P",
                actionId: "wide-enemy", amount: 8));
            Assert.That(ContactConfirmed(enemy), Is.True);
            arena.Present(new CombatEvent(CombatEventKind.ActionSettled, actorId: "E1", actionId: "wide-enemy"));
            StepUntil(() => arena.PresentationReady && arena.CameraAtHome, 15f, "wide camera enemy recovery");
            AssertTacticalWide(camera, widePosition, wideHalfHeight);
            tactical.AssertRestored(camera);
            Assert.That(hunter.ModelRoot.lossyScale.y * normal.sourceToTargetScale, Is.EqualTo(heroNormalScale));
            yield return null;
        }

        private static void AssertTacticalWide(Camera camera, Vector3 position, float halfHeight)
        {
            Assert.That(camera.orthographic, Is.False, "Native wide must retain the source perspective projection.");
            Assert.That(camera.fieldOfView, Is.EqualTo(38f));
            Assert.That(camera.orthographicSize, Is.EqualTo(halfHeight).Within(.000001f));
            Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(.000001f));
            Assert.That(PreciseAngle(camera.transform.localRotation, Quaternion.identity), Is.LessThan(.001f));
        }
        private static void AssertBoneSnapshotsEqual(BonePose[] expected, BonePose[] actual, string context)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            var byPath = new Dictionary<string, BonePose>();
            foreach (BonePose bone in actual) byPath.Add(bone.path, bone);
            foreach (BonePose bone in expected)
            {
                Assert.That(byPath.ContainsKey(bone.path), Is.True, context + ": " + bone.path);
                BonePose sample = byPath[bone.path];
                Assert.That(Vector3.Distance(sample.position, bone.position), Is.LessThan(.00001f), context + ": " + bone.path);
                Assert.That(PreciseAngle(sample.rotation, bone.rotation), Is.LessThan(.001f), context + ": " + bone.path);
                Assert.That(Vector3.Distance(sample.scale, bone.scale), Is.LessThan(.00001f), context + ": " + bone.path);
            }
        }

        private static SwordPose CaptureSword(ReactiveCombatActorVisual actor)
        {
            Transform sword = actor.WeaponAttachment?.CurrentWeapon?.transform;
            if (sword == null) return null;
            return new SwordPose {
                authority = actor.SwordTransformOwner.ToString(), parent = sword.parent == null ? null : sword.parent.name,
                modelPosition = actor.ModelRoot.InverseTransformPoint(sword.position),
                modelRotation = (Quaternion.Inverse(actor.ModelRoot.rotation) * sword.rotation).normalized,
                worldPosition = sword.position, worldRotation = sword.rotation, worldScale = sword.lossyScale
            };
        }
        private static BonePose[] CaptureBones(ReactiveCombatActorVisual actor)
        {
            var seen = new HashSet<Transform>();
            var samples = new List<BonePose>();
            foreach (SkinnedMeshRenderer skin in actor.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (Transform bone in skin.bones)
                {
                    if (bone == null || !seen.Add(bone)) continue;
                    samples.Add(new BonePose { path = BonePath(actor.ModelRoot, bone), position = bone.localPosition,
                        rotation = bone.localRotation, scale = bone.localScale });
                }
            return samples.ToArray();
        }
        private static string BonePath(Transform model, Transform bone)
        {
            string path = bone.name;
            while (bone.parent != null && bone.parent != model) { bone = bone.parent; path = bone.name + "/" + path; }
            return path;
        }
        [Serializable] private sealed class SwordPose
        {
            public string authority, parent;
            public Vector3 modelPosition, worldPosition, worldScale;
            public Quaternion modelRotation, worldRotation;
        }
        [Serializable] private sealed class BonePose
        {
            public string path;
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        private static void AssertSource(LicensedCombatMotionProfile profile)
        {
            Assert.That(profile, Is.Not.Null, "A fallback clip cannot stand in for the selected source presentation.");
            Assert.That(profile.sourceSkill, Is.Not.Null.And.Not.Empty);
            Assert.That(profile.sourceTimeline, Is.Not.Null.And.Not.Empty);
            Assert.That(profile.sourceAssetIdentifiers, Is.Not.Null.And.Not.Empty);
            Assert.That(profile.anticipation, Is.Not.Null);
            Assert.That(profile.baseIdle, Is.Not.Null);
            Assert.That(profile.segments, Is.Not.Null.And.Not.Empty);
            foreach (var segment in profile.segments)
            { Assert.That(segment.clip, Is.Not.Null); Assert.That(segment.clip.legacy, Is.True); }
        }

        private void StepUntil(Func<bool> ready, float seconds, string context)
        {
            for (int tick = 0; !ready() && tick < Mathf.CeilToInt(seconds * 60f); tick++) arena.Tick(1f / 60f);
            Assert.That(ready(), Is.True, context + ": hunter=" + Hunter().CurrentPose +
                " sourceClock=" + MotionClock(Hunter()) + " home=" + arena.HunterAtHome);
        }

        private IEnumerator EnterLiveEncounter()
        {
            profileDirectory = Path.Combine(CaptureRoot, "TemporaryProfiles", Guid.NewGuid().ToString("N"));
            root = new GameObject("LicensedLiveAuthorityFixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(profileDirectory);
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.That(boot.Session.AcceptContract() && boot.Session.LeaveHome(), Is.True);
            yield return null;
            Click("EnterReactivePortal");
            yield return WaitForLive(() => boot.Session.ReactiveCombat != null && Ready("ReactiveBasic") &&
                !boot.ReactivePresentationHeld, 45f, "initial real encounter readiness");
        }
        private IEnumerator WaitForLive(Func<bool> ready, float seconds, string context)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while ((!ready() || boot.View.Paused) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready() && !boot.View.Paused, Is.True, context + ": phase=" +
                boot.Session.ReactiveCombat?.Phase + " pose=" + Hunter()?.CurrentPose);
        }
        private ReactiveCombatArena FindLiveArena()
        {
            foreach (FieldInfo field in boot.View.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (field.FieldType == typeof(ReactiveCombatArena)) return (ReactiveCombatArena)field.GetValue(boot.View);
            Assert.Fail("The live view must own its arena."); return null;
        }
        private bool Ready(string name)
        {
            Button button = Find(name)?.GetComponent<Button>();
            return button != null && button.gameObject.activeInHierarchy && button.IsInteractable();
        }
        private GameObject Find(string name)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }
        private void Click(string name)
        {
            Assert.That(Ready(name), Is.True, "Live UI command must be interactable: " + name);
            Assert.That(ExecuteEvents.Execute(Find(name), new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler), Is.True);
        }
        private static ReactiveCombatActorVisual Hunter()
        {
            GameObject world = GameObject.Find("ReactiveCombatWorld");
            if (world != null)
                foreach (var actor in world.GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                    if (actor.name == "CombatActor_Keiko") return actor;
            return null;
        }
        private static ReactiveCombatActorVisual Enemy()
        {
            foreach (var actor in GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                if (actor.name == "CombatActor_Yokai" && !actor.IsDead) return actor;
            Assert.Fail("Live enemy visual missing."); return null;
        }
        private static Camera ActorCamera() => GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
        private static void SaveActorImage(Camera camera, string path, Texture2D reusable = null)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = camera.targetTexture;
            bool owned = reusable == null;
            Texture2D texture = reusable ?? new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                var bytes = texture.GetRawTextureData<byte>();
                double luma = 0; int samples = 0;
                for (int offset = 0; offset + 2 < bytes.Length; offset += 96)
                { luma += (bytes[offset] + bytes[offset + 1] + bytes[offset + 2]) / (3.0 * 255.0); samples++; }
                Assert.That(luma / Math.Max(1, samples), Is.GreaterThan(.001), "Black actor render: " + path);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (owned) UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        private void CaptureLiveUi(string name)
        {
            string folder = Path.Combine(CaptureRoot, "LiveAuthority");
            Directory.CreateDirectory(folder);
            Canvas canvas = root.GetComponentInChildren<Canvas>();
            RectTransform stage = Find("AuthoredStage").GetComponent<RectTransform>();
            ActorCamera().Render();
            var captureObject = new GameObject("LicensedLiveUiCapture", typeof(Camera));
            Camera camera = captureObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var render = new RenderTexture(1920, 1080, 24); render.Create(); camera.targetTexture = render;
            RenderMode priorMode = canvas.renderMode; Camera priorCamera = canvas.worldCamera;
            float priorDistance = canvas.planeDistance; Vector3 priorScale = stage.localScale;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
                canvas.planeDistance = 1f; stage.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                string path = Path.Combine(folder, name + ".png");
                SaveActorImage(camera, path);
                var combat = boot.Session.ReactiveCombat;
                File.WriteAllText(Path.Combine(folder, name + ".json"), JsonUtility.ToJson(new LiveEvidence {
                    image = path, mode = "real-bootstrap-Core-Input-System; key-frame-render", phase = combat.Phase.ToString(),
                    realtime = Time.realtimeSinceStartup, combatUs = combat.CurrentCombatUs, revision = combat.Revision,
                    actionId = combat.CurrentActionId, hunterHp = combat.HunterHp, targetHp = combat.GetActorState("E1").Hp,
                    hunterAp = combat.HunterAp, hunterPose = Hunter().CurrentPose,
                    sourceSkill = ActiveProfile(Hunter())?.sourceSkill, sourceClock = MotionClock(Hunter()),
                    contactConfirmed = ContactConfirmed(Hunter()), cameraPosition = ActorCamera().transform.localPosition,
                    cameraRotation = ActorCamera().transform.localEulerAngles, cameraFov = ActorCamera().fieldOfView,
                    cameraOrthographic = ActorCamera().orthographic
                }, true));
            }
            finally
            {
                canvas.renderMode = priorMode; canvas.worldCamera = priorCamera; canvas.planeDistance = priorDistance;
                stage.localScale = priorScale; camera.targetTexture = null;
                render.Release(); UnityEngine.Object.DestroyImmediate(render); UnityEngine.Object.DestroyImmediate(captureObject);
            }
        }
        private sealed class CameraState
        {
            private readonly bool orthographic;
            private readonly float fov, size;
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            public CameraState(Camera camera)
            { orthographic = camera.orthographic; fov = camera.fieldOfView; size = camera.orthographicSize;
                position = camera.transform.localPosition; rotation = camera.transform.localRotation; }
            public void AssertRestored(Camera camera)
            {
                Assert.That(camera.orthographic, Is.EqualTo(orthographic));
                Assert.That(camera.fieldOfView, Is.EqualTo(fov).Within(.001f));
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.001f));
                Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(.001f));
                Assert.That(PreciseAngle(camera.transform.localRotation, rotation), Is.LessThan(.01f));
            }
        }
        [Serializable] private sealed class LiveEvidence
        {
            public string image, mode, phase, actionId, hunterPose, sourceSkill;
            public long combatUs, revision;
            public int hunterHp, targetHp, hunterAp;
            public float realtime, sourceClock, cameraFov;
            public bool contactConfirmed, cameraOrthographic;
            public Vector3 cameraPosition, cameraRotation;
        }
        [Serializable] private sealed class PresentationManifest
        {
            public string scenario, mode, sourceSkill, sourceTimeline;
            public string[] sourceAssetIdentifiers;
            public int fps, width, height, frameCount, contactFrame, exitFrame, socketRestoredFrame;
            public float sourceDuration, presentationDuration;
            public PresentationFrame[] frames;
        }
        [Serializable] private sealed class PresentationFrame
        {
            public string hunterPose, enemyPose, boundary;
            public int frame;
            public float presentationSeconds, hunterMotionClock, enemyMotionClock, cameraFov, cameraOrthographicSize, hitStop;
            public bool hunterContactConfirmed, enemyContactConfirmed, cameraOrthographic;
            public Vector3 hunterPosition, enemyPosition, cameraPosition, cameraRotation;
            public Quaternion cameraQuaternion;
            public Matrix4x4 cameraProjection;
            public Vector3 attackerViewport, defenderViewport;
            public SwordPose sword;
            public BonePose[] sourceBones;
        }

        private static LicensedCombatMotionProfile ActiveProfile(ReactiveCombatActorVisual actor) =>
            ReadObservable<LicensedCombatMotionProfile>(actor, "ActiveLicensedProfile");
        private static bool ContactConfirmed(ReactiveCombatActorVisual actor) =>
            ReadObservable<bool>(actor, "LicensedContactConfirmed");
        private static float MotionClock(ReactiveCombatActorVisual actor) =>
            ReadObservable<float>(actor, "LicensedMotionClock");
        private static T ReadObservable<T>(ReactiveCombatActorVisual actor, string name)
        {
            PropertyInfo property = typeof(ReactiveCombatActorVisual).GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Missing licensed runtime observable: " + name);
            return (T)property.GetValue(actor);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (arena != null) { arena.Dispose(); arena = null; }
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
            if (!string.IsNullOrEmpty(profileDirectory) && Directory.Exists(profileDirectory))
                Directory.Delete(profileDirectory, true);
            boot = null;
        }
    }
}
