using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    // Real Bootstrap -> mission UI -> Core -> production arena. No renderer-only
    // replacement scene and no writes to the user's persistent save directory.
    public sealed class CombatCinematicFlowRegressionTests
    {
        private const string EvidenceRoot = "D:/DD2-Research/AnimationStudy/CombatFidelity2026-10-08";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private RokasBootstrap boot;
        private ReactiveCombatArena arena;
        private ReactiveCombatActorVisual hunter;
        private Dictionary<string, ReactiveCombatActorVisual> enemies;
        private Camera actorCamera, uiCamera;
        private Canvas canvas;
        private RectTransform stage;
        private RenderTexture uiTarget;
        private Texture2D image;
        private RenderMode previousMode;
        private Camera previousUiCamera;
        private float previousDistance;
        private Vector3 previousStageScale;
        private Keyboard keyboard;
        private bool inputConfigured, keyHeld, capture, measuring, completed, observedEnemyNormal, observedEnemyHeavy, observedBlock;
        private bool observedCoreSuffixCancellation, observedNativeHomeRecovery, observedPassiveStageOffset, confirmCounters = true;
        private readonly List<StageOffsetProbe> stageOffsetProbes = new List<StageOffsetProbe>();
        private string runLabel;
        private readonly List<InterruptEvidence> interruptions = new List<InterruptEvidence>();
        private readonly HashSet<string> interruptedActions = new HashSet<string>();
        private readonly Dictionary<string, string> observedEnemyActions = new Dictionary<string, string>();
        private int lastObservedFrame = -1;
        private Transform worldTransform;
        private LicensedCombatMotionProfile enemyHeavyProfile;
        private ReactiveCombatActorLibrary actorLibrary;
        private readonly FieldInfo defenseLedgerField = typeof(ReactiveCombatSession).GetField("_defenseLedger", PrivateInstance);
        private readonly FieldInfo suffixCanceledField = typeof(DefenseSequenceLedger).GetField("suffixCanceled", PrivateInstance);
        private InputSettings.EditorInputBehaviorInPlayMode priorInput;
        private InputSettings.BackgroundBehavior priorBackground;
        private readonly HashSet<string> defended = new HashSet<string>();
        private readonly HashSet<string> offense = new HashSet<string>();
        private readonly HashSet<string> screenshots = new HashSet<string>();
        private readonly Dictionary<string, GameObject> uiItems = new Dictionary<string, GameObject>();
        private readonly List<FrameEvidence> frames = new List<FrameEvidence>(16000);
        private readonly List<SequenceFrame> sequence = new List<SequenceFrame>(2400);
        private readonly List<PerformanceFrame> performance = new List<PerformanceFrame>(16000);
        private Renderer[] heldRenderers, flyingRenderers;
        private GameObject flying;
        private GameObject initialSword;
        private Transform spine;
        private CameraSnapshot tactical;
        private string runFolder, section = "entry", targetId, lastEnemyId;
        private string previousHunterPhase, previousEnemyPhase;
        private float started, nextSequenceTime;
        private int sequenceFrame, plannedCommands, fillerCommands;
        private ProfilerRecorder mainThreadRecorder, gcRecorder;

        [UnityTest, Timeout(420000)]
        public IEnumerator ActualCoreUiFlowCapturesNativeReturnAndSingleThrowVisualOwnership()
        {
            capture = true; confirmCounters = true; runLabel = "LiveCapture";
            yield return EnterEncounter();
            PrepareCapture();
            Capture("01_tactical_camera");
            Capture("15_idle_stance");
            yield return VerifyPreviewSwitchAndCancellation();

            string[] plan = { "ReactiveBasic", "ReactiveHeavy", "ReactiveThrow", "ReactiveBasic" };
            for (int index = 0; index < plan.Length; index++)
            {
                yield return WaitForCommand();
                int cost = plan[index] == "ReactiveHeavy" ? 5 : plan[index] == "ReactiveThrow" ? 2 : 0;
                while (boot.Session.ReactiveCombat.HunterAp < cost)
                {
                    Assert.That(fillerCommands, Is.LessThan(8), "Bounded AP-building commands exhausted.");
                    yield return ExecuteCommand("ReactiveBasic", "ap-building");
                    fillerCommands++;
                    yield return WaitForCommand();
                }
                yield return ExecuteCommand(plan[index], plan[index]);
                plannedCommands++;
            }
            // The real catalog's Heavy enemy is E6 in wave2. Advance only the
            // existing encounter; do not replace definitions or fabricate events.
            while (!observedEnemyHeavy && fillerCommands < 8)
            {
                yield return WaitForCommand();
                yield return ExecuteCommand("ReactiveBasic", "enemy-heavy-coverage");
                fillerCommands++;
            }
            yield return WaitForCommand();
            Assert.That(observedPassiveStageOffset, Is.True, "A living Normal target must retain its source stage offset across actual refresh before the saved cue.");
            Assert.That(observedNativeHomeRecovery, Is.True, "Full Native FK must continue after the performer reaches its outer home.");
            Assert.That(observedEnemyNormal, Is.True, "A real Core enemy Normal must be observed.");
            Assert.That(observedEnemyHeavy, Is.True, "A real Core enemy Heavy must be observed.");
            Assert.That(observedBlock, Is.True, "Accepted real defensive input must enter the actual Guard pose.");
            tactical.AssertRestored(actorCamera);
            Assert.That(arena.CameraAtHome && hunter.IdleSettled && boot.View.HunterAtHome, Is.True);
            Capture("16_tactical_camera_restored");
            string[] required = {
                "01_tactical_camera", "02_keiko_approach", "03_keiko_normal_impact", "04_keiko_normal_return",
                "05_keiko_heavy_preparation", "06_keiko_heavy_impact", "07_keiko_heavy_recovery",
                "08_dagger_preparation", "09_dagger_release", "10_dagger_impact",
                "11_enemy_approach", "12_enemy_impact", "13_enemy_return", "14_block",
                "15_idle_stance", "16_tactical_camera_restored"
            };
            foreach (string name in required) Assert.That(screenshots.Contains(name), Is.True, "Missing actual phase capture: " + name);
            Assert.That(plannedCommands, Is.EqualTo(4));
            Assert.That(boot.Session.SaveBlocked, Is.False, boot.Session.SaveError);
            completed = true;
            WriteManifest("capture", "Capture readback/encoding/file I/O is instrumented overhead; it is not a CPU benchmark.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator WarmActualRepeatedFlowMeasuresFramePacingWithoutCaptureReadback()
        {
            capture = false; confirmCounters = true; runLabel = "Performance";
            yield return EnterEncounter();
            yield return VerifyPreviewSwitchAndCancellation();
            // Warm one actual command before measuring; lazy Throw carriers are
            // already instantiated by the real Throw preview above.
            yield return ExecuteCommand("ReactiveBasic", "warmup");
            yield return WaitForCommand();
            section = "measurement";
            performance.Clear();
            mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 128);
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 128);
            measuring = true;
            string[] plan = { "ReactiveBasic", "ReactiveHeavy", "ReactiveThrow", "ReactiveBasic" };
            for (int index = 0; index < plan.Length; index++)
            {
                yield return WaitForCommand();
                int cost = plan[index] == "ReactiveHeavy" ? 5 : plan[index] == "ReactiveThrow" ? 2 : 0;
                while (boot.Session.ReactiveCombat.HunterAp < cost)
                {
                    Assert.That(fillerCommands, Is.LessThan(6));
                    yield return ExecuteCommand("ReactiveBasic", "measured-ap-building");
                    fillerCommands++;
                    yield return WaitForCommand();
                }
                yield return ExecuteCommand(plan[index], "measured-" + plan[index]);
                plannedCommands++;
            }
            yield return WaitForCommand();
            measuring = false;
            Assert.That(plannedCommands, Is.EqualTo(4));
            Assert.That(performance.Count, Is.GreaterThan(30));
            Assert.That(uiTarget, Is.Null, "Performance pass must not allocate a capture RenderTexture.");
            Assert.That(sequence.Count, Is.Zero, "Performance pass must not encode capture frames.");
            foreach (PerformanceFrame row in performance)
            {
                Assert.That(row.heldAndFlying, Is.False, "Mesh ownership overlap in measured pass.");
                Assert.That(row.swordStable && row.cameraFinite, Is.True);
                Assert.That(row.inFlight && !row.projectileOnly, Is.False, "Released flight must have one projectile carrier and no held mesh.");
            }
            completed = true;
            WriteManifest("performance", "Editor PlayMode frame deltas, including normal UI/input test instrumentation. ProfilerRecorder values are evidence only when recorderValid=true. No frame readback, image encoding, or frame-file writes occur during measurement; no zero-spike guarantee.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ActualDefenseSealBreakCancelsEnemySuffixAndRestoresCommandReadiness()
        {
            capture = false; confirmCounters = true; runLabel = "InterruptedFlow";
            yield return EnterEncounter();
            yield return VerifyPreviewSwitchAndCancellation();
            // Use the current visible HUD and catalog. Killing E1 with Normal then
            // Heavy leaves E2 alive for Throw and accepted parries. Repeating only
            // Normal on E2 can instead break its seal before its attack starts,
            // which does not exercise a remaining-hit suffix cancellation.
            string[] plan = { "ReactiveBasic", "ReactiveHeavy", "ReactiveThrow" };
            foreach (string command in plan)
            {
                yield return WaitForCommand();
                int cost = command == "ReactiveHeavy" ? 5 : command == "ReactiveThrow" ? 2 : 0;
                while (boot.Session.ReactiveCombat.HunterAp < cost)
                {
                    Assert.That(fillerCommands, Is.LessThan(6));
                    yield return ExecuteCommand("ReactiveBasic", "interrupt-ap-building");
                    fillerCommands++;
                    yield return WaitForCommand();
                }
                yield return ExecuteCommand(command, "interrupt-" + command);
                plannedCommands++;
                yield return WaitForCommand();
                if (observedCoreSuffixCancellation) break;
            }
            Assert.That(observedCoreSuffixCancellation, Is.True, "Real defense seal break must cancel a remaining enemy hit suffix.");
            Assert.That(interruptions.Count, Is.GreaterThan(0));
            Assert.That(hunter.IdleSettled && arena.CameraAtHome && boot.View.ReactivePresentationReady, Is.True);
            tactical.AssertRestored(actorCamera);
            Assert.That(arena.ThrowReleaseCount, Is.EqualTo(arena.ThrowContactCount));
            Assert.That(hunter.HeldDagger.gameObject.activeInHierarchy, Is.False);
            Assert.That(boot.Session.SaveBlocked, Is.False, boot.Session.SaveError);
            completed = true;
            WriteManifest("interrupted-flow", "Read-only observation of the real Core defense ledger suffix cancellation after actual UI command and defensive input; no fabricated interruption event.");
        }

        private IEnumerator EnterEncounter()
        {
            frames.Clear(); sequence.Clear(); performance.Clear(); screenshots.Clear(); defended.Clear(); offense.Clear();
            interruptions.Clear(); interruptedActions.Clear(); observedEnemyActions.Clear(); stageOffsetProbes.Clear(); uiItems.Clear();
            observedEnemyNormal = observedEnemyHeavy = observedBlock = observedCoreSuffixCancellation = observedNativeHomeRecovery = observedPassiveStageOffset = false;
            measuring = completed = keyHeld = false;
            previousHunterPhase = previousEnemyPhase = lastEnemyId = targetId = null;
            sequenceFrame = plannedCommands = fillerCommands = 0; lastObservedFrame = -1; nextSequenceTime = 0f;
            flying = null; flyingRenderers = null; section = "entry";
            started = Time.realtimeSinceStartup;
            runFolder = Path.Combine(EvidenceRoot, runLabel + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runFolder);
            priorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            priorBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputConfigured = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            root = new GameObject("CombatCinematicActualCoreFixture");
            boot = root.AddComponent<RokasBootstrap>();
            // Explicit isolated path before Start; never loads the user's save.
            boot.Initialize(Path.Combine(runFolder, "IsolatedProfile"));
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.That(boot.Session.AcceptContract() && boot.Session.LeaveHome(), Is.True);
            yield return null;
            Click("EnterReactivePortal");
            float deadline = Time.realtimeSinceStartup + 45f;
            while ((boot.Session.ReactiveCombat == null || !Ready("ReactiveBasic") || boot.ReactivePresentationHeld) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(boot.Session.ReactiveCombat, Is.Not.Null);
            Assert.That(Ready("ReactiveBasic") && !boot.ReactivePresentationHeld, Is.True, "Actual entrance did not settle.");
            arena = FindArena();
            GameObject world = GameObject.Find("ReactiveCombatWorld");
            Assert.That(world, Is.Not.Null);
            worldTransform = world.transform;
            actorLibrary = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
            enemyHeavyProfile = actorLibrary.yokai.licensedHeavy;
            foreach (var actor in world.GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                if (actor.name == "CombatActor_Keiko") hunter = actor;
            Assert.That(hunter, Is.Not.Null);
            Assert.That(hunter.DefaultLicensedProfile, Is.Not.Null, "The current COPY must use the controlled Native profiles.");
            enemies = (Dictionary<string, ReactiveCombatActorVisual>)typeof(ReactiveCombatArena).GetField("enemies", PrivateInstance).GetValue(arena);
            actorCamera = GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
            tactical = new CameraSnapshot(actorCamera);
            initialSword = hunter.WeaponAttachment.CurrentWeapon;
            heldRenderers = hunter.HeldDagger.GetComponentsInChildren<Renderer>(true);
            spine = hunter.ModelRoot.Find("mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2");
            WriteSourceExpectations();
        }

        private IEnumerator VerifyPreviewSwitchAndCancellation()
        {
            section = "preview-cancel";
            var combat = boot.Session.ReactiveCombat;
            long revision = combat.Revision;
            int ap = combat.HunterAp;
            Vector3 home = hunter.transform.localPosition;
            foreach (string button in new[] { "ReactiveBasic", "ReactiveHeavy", "ReactiveThrow" })
            {
                Click(button);
                if (button == "ReactiveThrow") CacheFlyingRenderer();
                yield return WaitUntil(() => hunter.ActiveLicensedProfile != null, 3f, "source preview");
                yield return WaitUntil(() => combat.Phase != ReactivePhase.Suspended, 4f, "source preview resume");
                Assert.That(combat.HunterAp, Is.EqualTo(ap));
                Assert.That(combat.Revision, Is.EqualTo(revision), "A preview must not commit domain action/damage.");
                Assert.That(hunter.transform.localPosition, Is.EqualTo(home), "Preview must not approach.");
            }
            Assert.That(boot.CancelReactivePreview(), Is.True);
            yield return WaitUntil(() => hunter.IdleSettled && arena.CameraAtHome && Ready("ReactiveBasic"), 5f, "cancel exact home");
            Assert.That(combat.HunterAp, Is.EqualTo(ap));
            Assert.That(combat.Revision, Is.EqualTo(revision));
            tactical.AssertRestored(actorCamera);
            Assert.That(hunter.transform.localPosition, Is.EqualTo(home));
            if (capture) Capture("stance_cancel_restored");
        }

        private bool PreviewReady(string button)
        {
            LicensedCombatMotionProfile expected = button == "ReactiveHeavy" ? actorLibrary.keiko.licensedHeavy :
                button == "ReactiveThrow" ? actorLibrary.keiko.licensedThrow : actorLibrary.keiko.licensedNormal;
            string pose = button == "ReactiveHeavy" ? "HeavyPreparation" :
                button == "ReactiveThrow" ? "ThrowPreparation" : "Preparation";
            return arena.HunterMotionPhase == "Preview" && hunter.ActiveLicensedProfile == expected &&
                hunter.CurrentPose == pose && !hunter.LicensedContactConfirmed &&
                hunter.SwordTransformOwner != ReactiveCombatActorVisual.SwordTransformAuthority.ExitBlend &&
                boot.Session.ReactiveCombat.Phase == ReactivePhase.PlayerCommand &&
                boot.View.ReactivePresentationReady && !boot.ReactivePresentationHeld && Ready(button);
        }

        private IEnumerator WaitForCommand()
        {
            yield return WaitUntil(() => boot.Session.ReactiveCombat.Phase == ReactivePhase.PlayerCommand &&
                boot.View.ReactivePresentationReady && !boot.ReactivePresentationHeld && Ready("ReactiveBasic"),
                65f, "next actual command");
        }

        private IEnumerator ExecuteCommand(string button, string label, string explicitTarget = null)
        {
            section = label;
            var combat = boot.Session.ReactiveCombat;
            Assert.That(combat.ActiveEnemyIds.Count, Is.GreaterThan(0));
            targetId = explicitTarget ?? combat.ActiveEnemyIds[0];
            boot.SelectReactiveTarget(targetId);
            // Selection saves/refreshes the real UI. Allow its lifecycle to finish
            // before re-resolving the command; never click a deferred stale object.
            uiItems.Clear();
            yield return null;
            yield return WaitUntil(() => combat.Phase == ReactivePhase.PlayerCommand &&
                boot.View.ReactivePresentationReady && !boot.ReactivePresentationHeld && Ready(button),
                5f, "actual UI ready after target selection");
            Assert.That(combat.SelectedTargetId, Is.EqualTo(targetId));
            ReactiveCombatActorVisual targetActor = null;
            enemies.TryGetValue(targetId, out targetActor);
            Vector3 targetHome = targetActor == null ? Vector3.zero : targetActor.transform.localPosition;
            int hp = combat.GetActorState(targetId).Hp;
            int releases = arena.ThrowReleaseCount, contacts = arena.ThrowContactCount;
            Click(button);
            if (button == "ReactiveBasic" || button == "ReactiveHeavy" || button == "ReactiveThrow")
            {
                if (button == "ReactiveThrow") CacheFlyingRenderer();
                // Observe the entry on a real update. A visible old button is not
                // proof that the native equipment handoff has finished.
                yield return null;
                yield return WaitUntil(() => PreviewReady(button), 4f, "exact native preview ready");
                if (capture && button == "ReactiveHeavy") Capture("05_keiko_heavy_preparation");
                if (capture && button == "ReactiveThrow") Capture("08_dagger_preparation");
                // Readback can produce a FrameGap suspension on the next update.
                // Keep the real gate and wait for its observable recovery.
                yield return null;
                yield return WaitUntil(() => PreviewReady(button), 4f, "native preview ready for confirm");
                Click(button);
            }
            yield return WaitUntil(() => combat.Phase == ReactivePhase.PlayerExecution, 5f, "real commit");
            string action = combat.CurrentActionId;
            yield return WaitUntil(() => combat.GetActorState(targetId).Hp < hp, 15f, "real Core hit " + action);
            Assert.That(hunter.ActiveLicensedProfile, Is.Not.Null);
            Assert.That(hunter.LicensedContactConfirmed, Is.True);
            if (capture && button == "ReactiveBasic")
                yield return VerifyPassiveTargetOffsetAcrossRefresh(targetActor, targetHome);
            if (capture)
                Capture(button == "ReactiveHeavy" ? "06_keiko_heavy_impact" :
                    button == "ReactiveThrow" ? "10_dagger_impact" : "03_keiko_normal_impact");
            if (button == "ReactiveThrow")
            {
                Assert.That(arena.ThrowReleaseCount, Is.EqualTo(releases + 1));
                Assert.That(arena.ThrowContactCount, Is.EqualTo(contacts + 1));
                Assert.That(hunter.HeldDagger.gameObject.activeInHierarchy, Is.False);
            }
            float clock = hunter.LicensedMotionClock;
            int hitHp = combat.GetActorState(targetId).Hp;
            long revision = combat.Revision;
            arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: targetId,
                actionId: action, amount: hp - hitHp));
            Assert.That(hunter.LicensedMotionClock, Is.EqualTo(clock).Within(.0001f), "Duplicate contact may not restart Native recovery.");
            Assert.That(combat.Revision, Is.EqualTo(revision));
            Assert.That(combat.GetActorState(targetId).Hp, Is.EqualTo(hitHp));
            yield return WaitUntil(() => boot.View.HunterAtHome && hunter.IdleSettled && arena.CameraAtHome,
                14f, "real exact return " + action);
            tactical.AssertRestored(actorCamera);
            Assert.That(hunter.WeaponAttachment.CurrentWeapon, Is.SameAs(initialSword));
            Assert.That(initialSword.transform.IsChildOf(hunter.ModelRoot), Is.True);
        }

        private IEnumerator VerifyPassiveTargetOffsetAcrossRefresh(ReactiveCombatActorVisual targetActor, Vector3 home)
        {
            LicensedCombatMotionProfile profile = hunter.ActiveLicensedProfile;
            if (targetActor == null || targetActor.IsDead || profile == null || !profile.sourceStageOffsets ||
                profile.targetTeamOffset == Vector2.zero || hunter.LicensedMotionClock >= profile.StageRecoveryTime) yield break;
            Vector3 displayed = targetActor.transform.localPosition;
            Assert.That(Vector3.Distance(displayed, home), Is.GreaterThan(.001f),
                "Actual living Normal target must display the saved source offset before its recovery cue.");
            long revision = boot.Session.ReactiveCombat.Revision;
            long combatUs = boot.Session.ReactiveCombat.CurrentCombatUs;
            boot.View.RefreshReactiveCombat();
            Vector3 refreshed = targetActor.transform.localPosition;
            stageOffsetProbes.Add(new StageOffsetProbe { unityFrame = Time.frameCount, targetId = targetId,
                sourceClock = hunter.LicensedMotionClock, recoveryCue = profile.StageRecoveryTime,
                home = home, displayed = displayed, afterRefresh = refreshed, coreRevision = revision, combatUs = combatUs });
            Assert.That(Vector3.Distance(refreshed, displayed), Is.LessThan(.001f),
                "Actual refresh may update the durable slot but must preserve passive source placement until the cue.");
            Assert.That(boot.Session.ReactiveCombat.Revision, Is.EqualTo(revision));
            Assert.That(boot.Session.ReactiveCombat.CurrentCombatUs, Is.EqualTo(combatUs));
            yield return null;
            if (!targetActor.IsDead && hunter.ActiveLicensedProfile == profile && hunter.LicensedMotionClock < profile.StageRecoveryTime)
                Assert.That(Vector3.Distance(targetActor.transform.localPosition, displayed), Is.LessThan(.001f),
                    "The next normal runtime refresh must also preserve the passive source offset before its cue.");
            observedPassiveStageOffset = true;
            Observe();
        }

        private IEnumerator WaitUntil(Func<bool> predicate, float seconds, string reason)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while ((!predicate() || boot.View.Paused) && Time.realtimeSinceStartup < deadline)
            {
                PumpInput();
                Observe();
                yield return null;
            }
            Observe();
            Assert.That(predicate() && !boot.View.Paused, Is.True,
                reason + " phase=" + boot.Session.ReactiveCombat.Phase + " pose=" + hunter.CurrentPose);
        }

        private void PumpInput()
        {
            if (keyHeld)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
                InputSystem.Update();
                keyHeld = false;
            }
            var combat = boot.Session.ReactiveCombat;
            if (combat.Phase == ReactivePhase.PlayerExecution && combat.CurrentPlayerSkillId == "heavy" &&
                combat.CurrentCombatUs - combat.CurrentActionStartUs >= 160000 && offense.Add(combat.CurrentActionId))
                Pulse(Key.Space);
            else if (combat.Phase == ReactivePhase.EnemyExecution && combat.CurrentAttack != null)
            {
                long offset = combat.CurrentCombatUs - combat.CurrentActionStartUs;
                for (int index = 0; index < combat.CurrentAttack.Hits.Count; index++)
                {
                    HitDefinition hit = combat.CurrentAttack.Hits[index];
                    if (offset < hit.ImpactUs - 85000 || offset >= hit.ImpactUs) continue;
                    string id = combat.CurrentActionId + "/" + hit.Id;
                    if (defended.Contains(id)) continue;
                    if ((hit.AllowedResponses & DefenseResponseMask.Parry) != 0)
                    { defended.Add(id); Pulse(Key.E); }
                    break;
                }
            }
            else if (confirmCounters && combat.Phase == ReactivePhase.CounterWindow && Ready("ReactiveCounterConfirm")) Click("ReactiveCounterConfirm");
        }

        private void Pulse(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key), InputState.currentTime);
            InputSystem.Update();
            keyHeld = true;
        }

        private void Observe()
        {
            if (lastObservedFrame == Time.frameCount) return;
            lastObservedFrame = Time.frameCount;
            var combat = boot.Session.ReactiveCombat;
            int heldCount = ActiveRendererCount(heldRenderers), flyingCount = ActiveRendererCount(flyingRenderers);
            bool held = heldCount > 0, flight = flyingCount > 0;
            bool inFlight = arena.ThrowReleaseCount > arena.ThrowContactCount;
            if (measuring)
            {
                performance.Add(new PerformanceFrame {
                    unityFrame = Time.frameCount, realtime = Time.realtimeSinceStartup - started, frameDelta = Time.unscaledDeltaTime,
                    mainThreadNs = mainThreadRecorder.Valid ? mainThreadRecorder.LastValue : -1,
                    allocatedBytes = gcRecorder.Valid ? gcRecorder.LastValue : -1,
                    phase = (int)combat.Phase, pose = hunter.CurrentPose, section = section,
                    heldAndFlying = held && flight, inFlight = inFlight, projectileOnly = !held && flight,
                    swordStable = hunter.WeaponAttachment.CurrentWeapon == initialSword,
                    cameraFinite = !float.IsNaN(actorCamera.fieldOfView) && !float.IsInfinity(actorCamera.fieldOfView)
                });
                return;
            }
            object defenseLedger = defenseLedgerField.GetValue(combat);
            bool suffixCanceled = defenseLedger != null && (bool)suffixCanceledField.GetValue(defenseLedger);
            if (suffixCanceled)
            {
                observedCoreSuffixCancellation = true;
                if (interruptedActions.Add(combat.CurrentActionId))
                    interruptions.Add(new InterruptEvidence { unityFrame = Time.frameCount, realtime = Time.realtimeSinceStartup - started,
                        combatUs = combat.CurrentCombatUs, actionId = combat.CurrentActionId, actorId = combat.ActiveActorId,
                        attack = combat.CurrentAttack == null ? null : combat.CurrentAttack.Id, suffixCanceled = true });
            }
            if (combat.ActiveActorId != null && combat.ActiveActorId != "P")
            {
                lastEnemyId = combat.ActiveActorId;
                if (!string.IsNullOrEmpty(combat.CurrentActionId)) observedEnemyActions[lastEnemyId] = combat.CurrentActionId;
            }
            ReactiveCombatActorVisual enemy = null;
            if (lastEnemyId != null) enemies.TryGetValue(lastEnemyId, out enemy);
            string hunterPhase = arena.HunterMotionPhase;
            string enemyPhase = lastEnemyId == null ? "None" : arena.EnemyMotionPhase(lastEnemyId);
            if (enemy != null && enemy.ActiveLicensedProfile != null)
            {
                bool heavy = enemy.ActiveLicensedProfile == enemyHeavyProfile;
                if (heavy) observedEnemyHeavy = true; else observedEnemyNormal = true;
            }
            if (hunter.CurrentPose == "Guard" && hunter.DefenseActive) observedBlock = true;
            Assert.That(held && flight, Is.False, "Held dagger mesh and projectile mesh overlap in the same actual frame; effects do not count as mesh ownership.");
            Assert.That(inFlight && (!flight || held), Is.False, "During released flight, ownership must be projectile-only; release VFX are excluded.");
            Assert.That(hunter.WeaponAttachment.CurrentWeapon, Is.SameAs(initialSword), "Sword identity must survive switches.");
            Assert.That(float.IsNaN(actorCamera.fieldOfView) || float.IsInfinity(actorCamera.fieldOfView), Is.False);
            Assert.That(actorCamera.orthographic, Is.False);
            if (hunterPhase == "Return" && previousHunterPhase != "Return")
                AssertNativeReturn(hunter, "hunter");
            if (enemy != null && enemyPhase == "Return" && previousEnemyPhase != "Return" && !enemy.IsDead)
                AssertNativeReturn(enemy, lastEnemyId);
            previousHunterPhase = hunterPhase;
            previousEnemyPhase = enemyPhase;

            if (arena.HunterMotionPhase == "None" && hunter.LicensedContactConfirmed &&
                hunter.ActiveLicensedProfile != null && hunter.LicensedStageRecovered)
                observedNativeHomeRecovery = true;
            if (!capture) return;
            var row = new FrameEvidence {
                index = frames.Count, unityFrame = Time.frameCount, realtime = Time.realtimeSinceStartup - started, frameDelta = Time.unscaledDeltaTime,
                combatUs = combat.CurrentCombatUs, revision = combat.Revision, actionId = combat.CurrentActionId,
                phase = combat.Phase.ToString(), section = section, enemyId = lastEnemyId,
                hunterPhase = hunterPhase, enemyPhase = enemyPhase, hunterPose = hunter.CurrentPose,
                enemyPose = enemy == null ? null : enemy.CurrentPose,
                hunterRoot = hunter.transform.localPosition, enemyRoot = enemy == null ? Vector3.zero : enemy.transform.localPosition,
                sourceClock = hunter.LicensedMotionClock, enemySourceClock = enemy == null ? 0f : enemy.LicensedMotionClock,
                sourceSkill = hunter.ActiveLicensedProfile == null ? null : hunter.ActiveLicensedProfile.sourceSkill,
                stageRecoveryCue = hunter.ActiveLicensedProfile == null ? 0f : hunter.ActiveLicensedProfile.StageRecoveryTime,
                sourceDuration = hunter.ActiveLicensedProfile == null ? 0f : hunter.ActiveLicensedProfile.Duration,
                contactConfirmed = hunter.LicensedContactConfirmed, cameraClock = arena.LicensedCameraClock,
                cameraActive = arena.LicensedCameraActive, cameraHome = arena.CameraAtHome,
                cameraPosition = actorCamera.transform.localPosition, cameraRotation = actorCamera.transform.localRotation,
                cameraFov = actorCamera.fieldOfView, swordOwner = hunter.SwordTransformOwner.ToString(),
                swordParent = initialSword.transform.parent.name, swordWorldPosition = initialSword.transform.position,
                swordWorldRotation = initialSword.transform.rotation, swordLocalRotation = initialSword.transform.localRotation,
                spineWorldRotation = spine == null ? Quaternion.identity : spine.rotation,
                heldDaggerVisible = held, flyingDaggerVisible = flight,
                heldActiveRendererCount = heldCount, flyingActiveRendererCount = flyingCount, coreSuffixCanceled = suffixCanceled,
                heldDaggerInstanceId = hunter.HeldDagger.gameObject.GetInstanceID(),
                flyingDaggerInstanceId = flying == null ? 0 : flying.GetInstanceID(),
                releases = arena.ThrowReleaseCount, contacts = arena.ThrowContactCount
            };
            frames.Add(row);
            if (uiTarget == null) return;
            if (hunterPhase == "Approach") Capture("02_keiko_approach");
            if (hunterPhase == "Return" && section == "ReactiveBasic") Capture("04_keiko_normal_return");
            if (section == "ReactiveHeavy" && hunter.LicensedContactConfirmed && hunter.LicensedMotionClock >= .25f)
                Capture("07_keiko_heavy_recovery");
            if (flight) Capture("09_dagger_release");
            if (enemyPhase == "Approach") Capture("11_enemy_approach");
            if (enemy != null && enemy.LicensedContactConfirmed) Capture("12_enemy_impact");
            if (enemyPhase == "Return") Capture("13_enemy_return");
            if (hunter.CurrentPose == "Guard" && hunter.DefenseActive) Capture("14_block");
            // Original wall PTS are retained; encode with this manifest's timestamps,
            // never claim these readback frames are a stable 60 fps capture.
            if (Time.realtimeSinceStartup >= nextSequenceTime && section != "preview-cancel")
            {
                nextSequenceTime = Time.realtimeSinceStartup + .1f;
                string name = "sequence-" + (++sequenceFrame).ToString("D5") + ".jpg";
                ReadLiveImage();
                File.WriteAllBytes(Path.Combine(runFolder, name), image.EncodeToJPG(90));
                sequence.Add(new SequenceFrame { image = name, realtime = Time.realtimeSinceStartup - started,
                    sourceClock = row.sourceClock, phase = row.phase, hunterPhase = hunterPhase, section = section });
            }
        }

        private void AssertNativeReturn(ReactiveCombatActorVisual actor, string actorId)
        {
            var profile = actor.ActiveLicensedProfile;
            string enemyAction;
            if (profile == null && actorId != "hunter" &&
                observedEnemyActions.TryGetValue(actorId, out enemyAction) && interruptedActions.Contains(enemyAction))
            {
                // A real Core suffix cancellation discards the pending anticipation.
                // This is a cancelled sequence, not completed source FK recovery.
                Assert.That(arena.CameraAtHome, Is.True, actorId + ": interrupted source shot must restore home.");
                return;
            }
            Assert.That(profile, Is.Not.Null, actorId + ": outer Return must not cancel active Native FK.");
            Assert.That(actor.LicensedContactConfirmed, Is.True);
            Assert.That(actor.LicensedMotionClock + .002f, Is.GreaterThanOrEqualTo(profile.StageRecoveryTime),
                actorId + ": Return cannot precede saved stage-recovery cue.");
            Assert.That(actor.LicensedMotionClock, Is.LessThan(profile.Duration),
                actorId + ": actual Return must begin before the full source FK recovery ends.");
            if (actor.WeaponAttachment != null && actor.WeaponAttachment.CurrentWeapon != null)
                Assert.That(actor.SwordTransformOwner, Is.EqualTo(ReactiveCombatActorVisual.SwordTransformAuthority.NativeMotion),
                    actorId + ": Return must preserve Native equipment authority.");
            Assert.That(arena.CameraAtHome, Is.True, actorId + ": source camera home cue must be respected.");
        }

        private void PrepareCapture()
        {
            canvas = root.GetComponentInChildren<Canvas>();
            stage = Find("AuthoredStage").GetComponent<RectTransform>();
            previousMode = canvas.renderMode; previousUiCamera = canvas.worldCamera;
            previousDistance = canvas.planeDistance; previousStageScale = stage.localScale;
            uiCamera = new GameObject("CinematicActualUiReadback", typeof(Camera)).GetComponent<Camera>();
            uiCamera.clearFlags = CameraClearFlags.SolidColor; uiCamera.backgroundColor = Color.black;
            uiTarget = new RenderTexture(1920, 1080, 24); uiTarget.Create(); uiCamera.targetTexture = uiTarget;
            image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f; stage.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases();
        }

        private void ReadLiveImage()
        {
            // Match the 1920x1080 readback target. Production Tick fits this same
            // stage to the batch GameView size, which is smaller than uiTarget.
            stage.localScale = Vector3.one;
            actorCamera.Render();
            Canvas.ForceUpdateCanvases();
            uiCamera.Render();
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = uiTarget;
                image.ReadPixels(new Rect(0, 0, uiTarget.width, uiTarget.height), 0, 0);
                image.Apply();
            }
            finally { RenderTexture.active = previous; }
        }

        private void Capture(string name)
        {
            if (!capture || uiTarget == null || screenshots.Contains(name)) return;
            ReadLiveImage();
            var bytes = image.GetRawTextureData<byte>();
            long sum = 0; int count = 0;
            for (int i = 0; i + 2 < bytes.Length; i += 96) { sum += bytes[i] + bytes[i + 1] + bytes[i + 2]; count++; }
            Assert.That(sum / (double)Math.Max(1, count), Is.GreaterThan(1), "Black actual UI frame: " + name);
            File.WriteAllBytes(Path.Combine(runFolder, name + ".png"), image.EncodeToPNG());
            screenshots.Add(name);
        }

        private void WriteSourceExpectations()
        {
            var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
            var profiles = new[] { library.keiko.licensedNormal, library.keiko.licensedHeavy,
                library.keiko.licensedThrow, library.yokai.licensedNormal, library.yokai.licensedHeavy };
            var rows = new List<ExpectedTiming>();
            foreach (var profile in profiles)
                rows.Add(new ExpectedTiming { sourceSkill = profile.sourceSkill, sourceTimeline = profile.sourceTimeline,
                    stageRecoveryCue = profile.StageRecoveryTime, sourceDuration = profile.Duration });
            File.WriteAllText(Path.Combine(runFolder, "source-timing-expectations.json"),
                JsonUtility.ToJson(new ExpectedTimings { timings = rows.ToArray(),
                    semantics = "StageRecoveryTime is the saved camera-home/outer-root-return cue; full FK recovery continues. Core may delay return until ActionSettled. Own arena root travel duration is an explicit adaptation." }, true));
        }

        private void WriteManifest(string mode, string limitation)
        {
            File.WriteAllText(Path.Combine(runFolder, "manifest.json"), JsonUtility.ToJson(new Manifest {
                mode = mode, limitation = limitation, completed = completed, noUserPersistentSave = true, realBootstrapCoreUi = true,
                screenshots = new List<string>(screenshots).ToArray(), frames = frames.ToArray(), sequence = sequence.ToArray(),
                performance = performance.ToArray(), interruptions = interruptions.ToArray(), mainThreadRecorderValid = mainThreadRecorder.Valid,
                gcRecorderValid = gcRecorder.Valid, enemyNormalObserved = observedEnemyNormal,
                enemyHeavyObserved = observedEnemyHeavy, blockObserved = observedBlock, coreSuffixCancellationObserved = observedCoreSuffixCancellation,
                nativeHomeRecoveryObserved = observedNativeHomeRecovery, passiveStageOffsetObserved = observedPassiveStageOffset,
                stageOffsetProbes = stageOffsetProbes.ToArray(),
                heldRendererInstanceIds = RendererIds(heldRenderers), flyingRendererInstanceIds = RendererIds(flyingRenderers),
                plannedCommands = plannedCommands, fillerCommands = fillerCommands
            }, true));
        }

        private void CacheFlyingRenderer()
        {
            // Include inactive children; a prepared projectile is deliberately hidden.
            if (flying != null) return;
            foreach (Transform item in worldTransform.GetComponentsInChildren<Transform>(true))
                if (item.name == "KeikoThrownDagger") { flying = item.gameObject; break; }
            if (flying != null) flyingRenderers = flying.GetComponentsInChildren<Renderer>(true);
        }

        private static int ActiveRendererCount(Renderer[] renderers)
        {
            int count = 0;
            if (renderers != null)
                foreach (var renderer in renderers)
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy) count++;
            return count;
        }

        private static int[] RendererIds(Renderer[] renderers)
        {
            if (renderers == null) return new int[0];
            var ids = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) ids[i] = renderers[i] == null ? 0 : renderers[i].GetInstanceID();
            return ids;
        }

        private ReactiveCombatArena FindArena()
        {
            object mission = typeof(RokasView).GetField("mission", PrivateInstance).GetValue(boot.View);
            object reactive = mission.GetType().GetField("reactiveView", PrivateInstance).GetValue(mission);
            return (ReactiveCombatArena)reactive.GetType().GetField("arena", PrivateInstance).GetValue(reactive);
        }

        private GameObject Find(string name)
        {
            GameObject cached;
            if (uiItems.TryGetValue(name, out cached) && cached != null && cached.activeInHierarchy &&
                cached.transform.IsChildOf(root.transform)) return cached;
            GameObject inactive = null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name != name) continue;
                if (item.gameObject.activeInHierarchy) { uiItems[name] = item.gameObject; return item.gameObject; }
                if (inactive == null) inactive = item.gameObject;
            }
            // Inactive controls are legitimate during non-command phases, but do
            // not allow one to shadow a replacement that is currently active.
            uiItems.Remove(name);
            return inactive;
        }

        private bool Ready(string name)
        {
            Button button = Find(name)?.GetComponent<Button>();
            return button != null && button.gameObject.activeInHierarchy && button.IsInteractable();
        }

        private void Click(string name)
        {
            Assert.That(Ready(name), Is.True, "Actual mission UI button not ready: " + name);
            Assert.That(ExecuteEvents.Execute(Find(name), new PointerEventData(EventSystem.current) {
                button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler), Is.True);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            measuring = false;
            if (runFolder != null && !completed)
                WriteManifest(runLabel + "-partial", "Partial evidence is preserved even if an assertion fails; no PASS is implied.");
            mainThreadRecorder.Dispose(); gcRecorder.Dispose();
            if (canvas != null && uiTarget != null)
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousUiCamera;
                canvas.planeDistance = previousDistance;
                if (stage != null) stage.localScale = previousStageScale;
            }
            if (uiCamera != null) { uiCamera.targetTexture = null; UnityEngine.Object.Destroy(uiCamera.gameObject); }
            if (uiTarget != null) { uiTarget.Release(); UnityEngine.Object.Destroy(uiTarget); }
            if (image != null) UnityEngine.Object.Destroy(image);
            if (root != null) UnityEngine.Object.Destroy(root);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (inputConfigured)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = priorInput;
                InputSystem.settings.backgroundBehavior = priorBackground;
            }
            yield return null;
        }

        private sealed class CameraSnapshot
        {
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly float fov, size;
            private readonly bool orthographic;
            public CameraSnapshot(Camera camera) { position = camera.transform.localPosition; rotation = camera.transform.localRotation;
                fov = camera.fieldOfView; size = camera.orthographicSize; orthographic = camera.orthographic; }
            public void AssertRestored(Camera camera)
            {
                Assert.That(camera.orthographic, Is.EqualTo(orthographic));
                Assert.That(camera.fieldOfView, Is.EqualTo(fov).Within(.001f));
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.001f));
                Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(camera.transform.localRotation, rotation), Is.LessThan(.01f));
            }
        }

        [Serializable] private sealed class Manifest {
            public string mode, limitation; public bool completed, noUserPersistentSave, realBootstrapCoreUi;
            public bool mainThreadRecorderValid, gcRecorderValid, enemyNormalObserved, enemyHeavyObserved, blockObserved,
                coreSuffixCancellationObserved, nativeHomeRecoveryObserved, passiveStageOffsetObserved;
            public int[] heldRendererInstanceIds, flyingRendererInstanceIds;
            public int plannedCommands, fillerCommands; public string[] screenshots; public FrameEvidence[] frames;
            public SequenceFrame[] sequence; public PerformanceFrame[] performance; public InterruptEvidence[] interruptions;
            public StageOffsetProbe[] stageOffsetProbes;
        }
        [Serializable] private sealed class FrameEvidence {
            public int index, unityFrame, heldDaggerInstanceId, flyingDaggerInstanceId, releases, contacts,
                heldActiveRendererCount, flyingActiveRendererCount;
            public long combatUs, revision; public float realtime, frameDelta, sourceClock, enemySourceClock,
                stageRecoveryCue, sourceDuration, cameraClock, cameraFov;
            public string actionId, phase, section, enemyId, hunterPhase, enemyPhase, hunterPose, enemyPose,
                sourceSkill, swordOwner, swordParent;
            public bool contactConfirmed, cameraActive, cameraHome, heldDaggerVisible, flyingDaggerVisible, coreSuffixCanceled;
            public Vector3 hunterRoot, enemyRoot, cameraPosition, swordWorldPosition;
            public Quaternion cameraRotation, swordWorldRotation, swordLocalRotation, spineWorldRotation;
        }
        [Serializable] private sealed class SequenceFrame {
            public string image, phase, hunterPhase, section; public float realtime, sourceClock;
        }
        [Serializable] private struct PerformanceFrame {
            public int unityFrame, phase; public string pose, section; public float realtime, frameDelta;
            public long mainThreadNs, allocatedBytes;
            public bool heldAndFlying, inFlight, projectileOnly, swordStable, cameraFinite;
        }
        [Serializable] private sealed class StageOffsetProbe {
            public int unityFrame; public string targetId; public float sourceClock, recoveryCue;
            public long coreRevision, combatUs; public Vector3 home, displayed, afterRefresh;
        }
        [Serializable] private sealed class InterruptEvidence {
            public int unityFrame; public float realtime; public long combatUs;
            public string actionId, actorId, attack; public bool suffixCanceled;
        }
        [Serializable] private sealed class ExpectedTiming {
            public string sourceSkill, sourceTimeline; public float stageRecoveryCue, sourceDuration;
        }
        [Serializable] private sealed class ExpectedTimings { public string semantics; public ExpectedTiming[] timings; }
    }
}
