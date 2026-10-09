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
        private ReactiveMissionView reactiveView;
        private Text damageFeedback;
        private int observedFeedbackContacts;
        private double lastFeedbackObservationClock = -1d;
        private bool observedNormalFocus, observedThrowFocus, observedVisibleDamage, observedGuardFeedback, observedHeavyFocusExcluded, observedLethalFocusCleared;
        private readonly FieldInfo feedbackStartedField = typeof(ReactiveMissionView).GetField("feedbackStartedAt", PrivateInstance);
        private readonly FieldInfo feedbackTargetField = typeof(ReactiveMissionView).GetField("feedbackTargetId", PrivateInstance);
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
        private bool spatialValidation, spatialContactObserved, visibleFlow;
        private Vector3 spatialTargetHome;
        private bool inputConfigured, keyHeld, capture, measuring, completed, observedEnemyNormal, observedEnemyHeavy, observedBlock;
        private bool observedCoreSuffixCancellation, observedNativeHomeRecovery, observedPassiveStageOffset, confirmCounters = true;
        private readonly List<StageOffsetProbe> stageOffsetProbes = new List<StageOffsetProbe>();
        private readonly List<DeathEvidence> deaths = new List<DeathEvidence>();
        private readonly Dictionary<string, DeathEvidence> deathsById = new Dictionary<string, DeathEvidence>();
        private DeathEvidence displayedCorpse;
        private readonly FieldInfo actorAnimationField = typeof(ReactiveCombatActorVisual).GetField("animationPlayer", PrivateInstance);
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

        [UnityTest, Timeout(180000)]
        public IEnumerator ActualCoreUiUsesCompactSourceFormationAndRelativeZoomCamera()
        {
            capture = false; confirmCounters = true; runLabel = "SourceSpatial";
            yield return EnterEncounter();
            float scale = hunter.ModelRoot.lossyScale.y * hunter.DefaultLicensedProfile.sourceToTargetScale;
            string first = boot.Session.ReactiveCombat.ActiveEnemyIds[0];
            Assert.That(arena.HunterHome.x / scale, Is.EqualTo(-.7f).Within(.001f), "Size-one source front hero is -.7, rather than the old wide -5.25 anchor.");
            Assert.That(arena.EnemyHome(first).x / scale, Is.EqualTo(.7f).Within(.001f), "First source enemy slot must use the other compact front anchor.");
            for (int i = 0; i < boot.Session.ReactiveCombat.ActiveEnemyIds.Count; i++)
                Assert.That(arena.EnemyHome(boot.Session.ReactiveCombat.ActiveEnemyIds[i]).x / scale,
                    Is.EqualTo(.7f + .9f * i).Within(.001f), "Active size-one ranks use the source .9 spacing.");
            spatialValidation = true;
            spatialTargetHome = arena.EnemyHome(first);
            yield return VerifyPreviewSwitchAndCancellation();
            yield return ExecuteCommand("ReactiveBasic", "source-spatial-normal", first);
            yield return WaitForCommand();
            Assert.That(spatialContactObserved, Is.True, "Relative shot and mirrored target placement must be seen during the actual Core contact.");
            Assert.That(observedPassiveStageOffset, Is.True, "Real UI refresh must retain the source target placement until its cue.");
            tactical.AssertRestored(actorCamera);
            Assert.That(arena.HunterAtHome && hunter.IdleSettled, Is.True);
            completed = true;
            WriteManifest("source-spatial", "Actual Bootstrap/UI/Core route with source size-one front anchors, mirrored team offsets and camera translated from source ZoomIn floor Z=-7. No capture readback.");
        }

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
                "15_idle_stance", "16_tactical_camera_restored", "17_enemy_death"
            };
            foreach (string name in required) Assert.That(screenshots.Contains(name), Is.True, "Missing actual phase capture: " + name);
            Assert.That(plannedCommands, Is.EqualTo(4));
            Assert.That(boot.Session.SaveBlocked, Is.False, boot.Session.SaveError);
            AssertLiveFoleyDispatches();
            AssertLiveFeedbackCoverage();
            completed = true;
            WriteManifest("capture", "Capture readback/encoding/file I/O is instrumented overhead; it is not a CPU benchmark.");
        }

        [UnityTest, Timeout(420000)]
        public IEnumerator ActualCoreUiVisibleGameViewCompletesNormalHeavyThrowAndEnemyFlow()
        {
            capture = false; visibleFlow = true; confirmCounters = true; runLabel = "ContinuousGameView";
            yield return EnterEncounter();
            yield return VerifyPreviewSwitchAndCancellation();
            string[] plan = { "ReactiveBasic", "ReactiveHeavy", "ReactiveThrow", "ReactiveBasic" };
            foreach (string command in plan)
            {
                yield return WaitForCommand();
                int cost = command == "ReactiveHeavy" ? 5 : command == "ReactiveThrow" ? 2 : 0;
                while (boot.Session.ReactiveCombat.HunterAp < cost)
                {
                    Assert.That(fillerCommands, Is.LessThan(8));
                    yield return ExecuteCommand("ReactiveBasic", "ap-building");
                    fillerCommands++;
                    yield return WaitForCommand();
                }
                yield return ExecuteCommand(command, command);
                plannedCommands++;
            }
            while (!observedEnemyHeavy && fillerCommands < 8)
            {
                yield return WaitForCommand();
                yield return ExecuteCommand("ReactiveBasic", "enemy-heavy-coverage");
                fillerCommands++;
            }
            yield return WaitForCommand();
            Assert.That(observedPassiveStageOffset && observedNativeHomeRecovery, Is.True);
            Assert.That(observedEnemyNormal && observedEnemyHeavy && observedBlock, Is.True);
            Assert.That(arena.CameraAtHome && hunter.IdleSettled && boot.View.HunterAtHome, Is.True);
            Assert.That(arena.ThrowReleaseCount, Is.EqualTo(arena.ThrowContactCount));
            Assert.That(boot.Session.SaveBlocked, Is.False, boot.Session.SaveError);
            Assert.That(uiTarget, Is.Null, "Visible run must leave the production GameView/Canvas intact.");
            Assert.That(sequence.Count, Is.Zero, "External continuous capture must not be replaced by image sequence encoding.");
            tactical.AssertRestored(actorCamera);
            AssertLiveFoleyDispatches();
            AssertLiveFeedbackCoverage();
            completed = true;
            WriteManifest("continuous-game-view", "Actual Bootstrap/UI/Core and production visible Canvas. Phase trace only; external WGC continuous video is recorded separately. No framebuffer readback or per-frame file writes in this fixture.");
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
            deaths.Clear(); deathsById.Clear(); displayedCorpse = null;
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
            damageFeedback = Find("ReactiveHitFeedback").GetComponent<Text>();
            Assert.That(damageFeedback, Is.Not.Null);
            Assert.That(feedbackStartedField, Is.Not.Null);
            Assert.That(feedbackTargetField, Is.Not.Null);
            observedFeedbackContacts = arena.AcceptedContactCount;
            lastFeedbackObservationClock = -1d;
            observedNormalFocus = observedThrowFocus = observedVisibleDamage = observedGuardFeedback = observedHeavyFocusExcluded = observedLethalFocusCleared = false;
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
                Assert.That(arena.CinematicVeilAlpha, Is.Zero, "Real UI preview cannot request confirmed-contact focus.");
            }
            Assert.That(boot.CancelReactivePreview(), Is.True);
            yield return WaitUntil(() => hunter.IdleSettled && arena.CameraAtHome && Ready("ReactiveBasic"), 5f, "cancel exact home");
            Assert.That(combat.HunterAp, Is.EqualTo(ap));
            Assert.That(combat.Revision, Is.EqualTo(revision));
            tactical.AssertRestored(actorCamera);
            Assert.That(arena.CinematicVeilAlpha, Is.Zero, "Real preview cancellation clears the background adaptation.");
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
            CombatIdleStance priorCommittedStance = hunter.ConfirmedCombatStance;
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
                if (capture || spatialValidation || visibleFlow)
                    Assert.That(arena.CinematicVeilAlpha, Is.Zero, "The actual preview precedes Core contact.");
                if (capture && button == "ReactiveHeavy") Capture("05_keiko_heavy_preparation");
                if (capture && button == "ReactiveThrow") Capture("08_dagger_preparation");
                // Readback can produce a FrameGap suspension on the next update.
                // Keep the real gate and wait for its observable recovery.
                yield return null;
                yield return WaitUntil(() => PreviewReady(button), 4f, "native preview ready for confirm");
                Click(button);
            }
            yield return WaitUntil(() => combat.Phase == ReactivePhase.PlayerExecution, 5f, "real commit");
            // Domain commitment is presented after the actual approach hold; inspect both
            // the real motion profile and confirmed sword stance after resolved Core contact.
            LicensedCombatMotionProfile expectedCommittedMotion = button == "ReactiveHeavy"
                ? actorLibrary.keiko.licensedHeavy : button == "ReactiveThrow"
                    ? actorLibrary.keiko.licensedThrow : actorLibrary.keiko.licensedNormal;
            string action = combat.CurrentActionId;
            yield return WaitUntil(() => combat.GetActorState(targetId).Hp < hp, 15f, "real Core hit " + action);
            Assert.That(hunter.ActiveLicensedProfile, Is.SameAs(expectedCommittedMotion),
                "Committed hit must use its real licensed motion profile, not a stale preview or idle.");
            Assert.That(hunter.ConfirmedCombatStance, Is.EqualTo(button == "ReactiveHeavy" ? CombatIdleStance.Heavy :
                button == "ReactiveBasic" ? CombatIdleStance.Normal : priorCommittedStance),
                "Actual presented Core commit owns the sword stance; Throw preserves its last confirmed idle.");
            Assert.That(hunter.LicensedContactConfirmed, Is.True);
            if (capture || spatialValidation || visibleFlow)
                AssertResolvedPlayerFeedback(button, action, combat.GetActorState(targetId).Hp > 0);
            if ((capture || spatialValidation || visibleFlow) && button == "ReactiveBasic")
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
            if (capture || spatialValidation || visibleFlow)
                Assert.That(arena.CinematicVeilAlpha, Is.Zero, "Actual tactical return clears contact focus independently of full FK recovery.");
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
            if (spatialValidation && hunter.LicensedContactConfirmed && hunter.ActiveLicensedProfile == actorLibrary.keiko.licensedNormal &&
                arena.LicensedCameraActive && arena.LicensedCameraClock < .4f && targetId != null &&
                enemies.TryGetValue(targetId, out ReactiveCombatActorVisual spatialTarget) && !spatialTarget.IsDead)
            {
                float sourceScale = hunter.ModelRoot.lossyScale.y * hunter.ActiveLicensedProfile.sourceToTargetScale;
                // ActorSpacingLayout uses local +Z for offset.x: the right team is mirrored.
                Assert.That((spatialTarget.transform.localPosition.x - spatialTargetHome.x) / sourceScale,
                    Is.EqualTo(.25f).Within(.002f), "Touche raw target -.25 maps to world +.25 on Team1.");
                float floorZ = (hunter.transform.localPosition.z + spatialTarget.transform.localPosition.z) * .5f;
                float relativeDepth = (actorCamera.transform.localPosition.z - floorZ) / sourceScale;
                Assert.That(relativeDepth, Is.InRange(-8f, -6f), "BasicDamage camera is around -7.1 relative to ZoomIn floor, not raw -14.1 added again.");
                Vector3 attackerView = actorCamera.WorldToViewportPoint(hunter.TorsoPoint);
                Vector3 targetView = actorCamera.WorldToViewportPoint(spatialTarget.TorsoPoint);
                Assert.That(attackerView.x, Is.InRange(0f, 1f));
                Assert.That(targetView.x, Is.InRange(0f, 1f));
                spatialContactObserved = true;
            }
            if (capture || spatialValidation || visibleFlow)
            { ObserveFeedback(); ObserveRealDeaths(); }
            if (!capture && !visibleFlow) return;
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
                presentationClock = arena.PresentationClock, feedbackStartedAt = (double)feedbackStartedField.GetValue(reactiveView),
                veilAlpha = arena.CinematicVeilAlpha, veilActionId = arena.CinematicVeilActionId,
                feedbackActive = damageFeedback.gameObject.activeInHierarchy, feedbackAlpha = damageFeedback.color.a,
                feedbackText = damageFeedback.text, feedbackTargetId = (string)feedbackTargetField.GetValue(reactiveView),
                feedbackPosition = damageFeedback.rectTransform.anchoredPosition, feedbackPivot = damageFeedback.rectTransform.pivot,
                corpseCount = arena.CorpseCount, corpseId = displayedCorpse == null ? null : displayedCorpse.actorId,
                corpseAge = displayedCorpse == null ? -1f : displayedCorpse.lastCorpseAge,
                corpsePose = displayedCorpse == null ? null : displayedCorpse.lastPose,
                corpseActive = displayedCorpse != null && displayedCorpse.lastActorActive,
                corpseIdleSettled = displayedCorpse != null && displayedCorpse.lastIdleSettled,
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

        private void AssertResolvedPlayerFeedback(string button, string action, bool survivingTarget)
        {
            Assert.That(damageFeedback.gameObject.activeInHierarchy, Is.True,
                "Actual Core HitResolved must display damage even after HideEntryHud.");
            Assert.That(damageFeedback.color.a, Is.GreaterThan(0f));
            AssertProjectedDamage((string)feedbackTargetField.GetValue(reactiveView));
            if (button == "ReactiveHeavy")
            {
                Assert.That(arena.CinematicVeilAlpha, Is.Zero, "Real HeavyAOE cannot use the BasicDamage adaptation.");
                observedHeavyFocusExcluded = true;
            }
            else if (!survivingTarget)
            {
                Assert.That(arena.CinematicVeilAlpha, Is.Zero, "A real lethal contact must clear surviving-target focus.");
                observedLethalFocusCleared = true;
            }
            else if (button == "ReactiveBasic" || button == "ReactiveThrow")
            {
                Assert.That(arena.LicensedCameraActive, Is.True);
                Assert.That(arena.LicensedCameraClock, Is.LessThan(78f / 60f), "Observe the actual contact before its focus deadline.");
                Assert.That(arena.CinematicVeilActionId, Is.EqualTo(action));
                Assert.That(arena.CinematicVeilAlpha, Is.GreaterThan(0f),
                    "Confirmed surviving Normal/Throw must be visibly focused on the actual Core contact frame.");
                if (button == "ReactiveThrow") observedThrowFocus = true; else observedNormalFocus = true;
            }
            observedVisibleDamage = true;
        }

        private void ObserveFeedback()
        {
            double clock = arena.PresentationClock;
            Assert.That(clock, Is.GreaterThanOrEqualTo(lastFeedbackObservationClock), "UI reads cannot rewind the arena-owned clock.");
            lastFeedbackObservationClock = clock;
            double startedAt = (double)feedbackStartedField.GetValue(reactiveView);
            double age = Math.Max(0d, clock - startedAt);
            float expectedAlpha = Mathf.Clamp01((.45f - (float)age) / .15f);
            Assert.That(damageFeedback.color.a, Is.EqualTo(expectedAlpha).Within(.0001f),
                "Visible damage fades only on the arena presentation clock, including actual presentation holds.");
            Assert.That(damageFeedback.gameObject.activeInHierarchy, Is.EqualTo(Mathf.Max(0f, .45f - (float)age) > 0f),
                "A positive feedback lifetime must be active in the actual mission hierarchy; expiry must hide it.");
            if (arena.CameraAtHome)
                Assert.That(arena.CinematicVeilAlpha, Is.Zero, "Tactical restoration may not retain a dark rectangle.");
            if (!string.IsNullOrEmpty(arena.CinematicVeilActionId))
            {
                float fade = Mathf.Clamp01((arena.LicensedCameraClock - 62f / 60f) / (16f / 60f));
                float weight = 1f - fade * fade * (3f - 2f * fade);
                Assert.That(arena.CinematicVeilAlpha, Is.EqualTo(.24f * weight).Within(.0001f),
                    "The own .24 intensity adaptation reads the existing source-camera hold/fade envelope.");
            }
            if (arena.AcceptedContactCount == observedFeedbackContacts) return;
            observedFeedbackContacts = arena.AcceptedContactCount;
            Assert.That(damageFeedback.gameObject.activeInHierarchy, Is.True, "The newest accepted real contact must be visible.");
            Assert.That(damageFeedback.color.a, Is.GreaterThan(0f));
            string shownTarget = (string)feedbackTargetField.GetValue(reactiveView);
            if (!string.IsNullOrEmpty(shownTarget))
            {
                AssertProjectedDamage(shownTarget);
                var state = boot.Session.ReactiveCombat.GetActorState(shownTarget);
                Assert.That(state, Is.Not.Null);
                if (state.Hp <= 0)
                {
                    Assert.That(arena.CinematicVeilAlpha, Is.Zero, "The displayed actual death is excluded from surviving-target focus.");
                    observedLethalFocusCleared = true;
                }
                observedVisibleDamage = true;
            }
            else if (damageFeedback.text == "БЛОК" || damageFeedback.text == "ИДЕАЛЬНЫЙ БЛОК" || damageFeedback.text == "УКЛОНЕНИЕ")
            {
                Assert.That(arena.CinematicVeilAlpha, Is.Zero, "Actual resolved defense is excluded from BasicDamage focus.");
                observedGuardFeedback = true;
            }
        }

        private void AssertProjectedDamage(string actorId)
        {
            Assert.That(actorId, Is.Not.Null.And.Not.Empty);
            ReactiveCombatActorVisual actor = actorId == "P" ? hunter : enemies[actorId];
            Vector3 cameraPosition = actorCamera.transform.position, actorPosition = actor.transform.position;
            float cameraClock = arena.LicensedCameraClock;
            double clock = arena.PresentationClock;
            Assert.That(arena.TryActorFeedbackAnchor(actorId, out Vector2 anchor), Is.True);
            RawImage actors = Find("ReactiveAnimatedWorld").GetComponent<RawImage>();
            Vector3 top = actor.BodyBounds.center; top.y = actor.BodyBounds.max.y;
            Vector3 viewport = actorCamera.WorldToViewportPoint(top);
            Vector2 projected = actors.rectTransform.anchoredPosition + new Vector2(
                viewport.x * actors.rectTransform.rect.width, -(1f - viewport.y) * actors.rectTransform.rect.height);
            Assert.That(Vector2.Distance(anchor, projected), Is.LessThan(.02f), "Actual damage uses the displayed actor-camera viewport.");
            RectTransform feedback = damageFeedback.rectTransform;
            Assert.That(feedback.pivot, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(damageFeedback.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            float age = (float)Math.Max(0d, clock - (double)feedbackStartedField.GetValue(reactiveView));
            Vector2 expected = anchor + new Vector2(0f, 12f + Mathf.Min(.45f, age) * 35f);
            Rect displayedViewport = arena.ActorFeedbackViewport;
            expected.x = Mathf.Clamp(expected.x, displayedViewport.xMin + feedback.rect.width * .5f, displayedViewport.xMax - feedback.rect.width * .5f);
            expected.y = Mathf.Clamp(expected.y, displayedViewport.yMin + feedback.rect.height * .5f, displayedViewport.yMax - feedback.rect.height * .5f);
            Assert.That(Vector2.Distance(feedback.anchoredPosition, expected), Is.LessThan(.02f),
                "Positive damage is centered over its actual actor, with the own rise and viewport clamp.");
            Assert.That(actorCamera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(actor.transform.position, Is.EqualTo(actorPosition));
            Assert.That(arena.LicensedCameraClock, Is.EqualTo(cameraClock));
            Assert.That(arena.PresentationClock, Is.EqualTo(clock));
        }

        private void ObserveRealDeaths()
        {
            displayedCorpse = null;
            foreach (var pair in enemies)
            {
                if (!arena.IsCorpse(pair.Key)) continue;
                ReactiveCombatActorVisual actor = pair.Value;
                var state = boot.Session.ReactiveCombat.GetActorState(pair.Key);
                Assert.That(state, Is.Not.Null);
                Assert.That(state.Hp, Is.LessThanOrEqualTo(0), "The observed corpse must come from actual Core death.");
                Assert.That(actor != null && actor.IsDead, Is.True);
                Assert.That(actor.gameObject.activeInHierarchy, Is.True, "An existing falling/held corpse cannot be removed instantly.");
                Assert.That(actor.ActiveLicensedProfile, Is.Null, "Death must cancel the previous live attack sampler.");
                float age = arena.CorpseElapsed(pair.Key);
                Animation legacy = (Animation)actorAnimationField.GetValue(actor);
                bool hasDeathClip = legacy != null && legacy.GetClip("Death") != null;
                if (hasDeathClip)
                    Assert.That(actor.CurrentPose, Is.EqualTo("Death"), "An authored Death actor cannot resume its live Idle alias.");
                int renderers = ActiveRendererCount(actor.GetComponentsInChildren<Renderer>(true));
                Assert.That(renderers, Is.GreaterThan(0), "Retained corpse must keep actual enabled model renderers.");
                if (!deathsById.TryGetValue(pair.Key, out DeathEvidence proof))
                {
                    proof = new DeathEvidence { actorId = pair.Key, firstUnityFrame = Time.frameCount,
                        firstCombatUs = boot.Session.ReactiveCombat.CurrentCombatUs, hp = state.Hp,
                        firstCorpseAge = age, fallDuration = arena.DeathFallDuration, hasDeathClip = hasDeathClip,
                        firstPose = actor.CurrentPose, isDead = actor.IsDead, firstIdleSettled = actor.IdleSettled,
                        firstActorActive = actor.gameObject.activeInHierarchy, firstRendererCount = renderers };
                    deathsById.Add(pair.Key, proof); deaths.Add(proof);
                }
                proof.lastUnityFrame = Time.frameCount; proof.lastCorpseAge = age; proof.lastPose = actor.CurrentPose;
                proof.lastActorActive = actor.gameObject.activeInHierarchy; proof.lastIdleSettled = actor.IdleSettled;
                proof.retainedAfterFirstFrame |= Time.frameCount > proof.firstUnityFrame;
                proof.noLiveIdleAlias |= hasDeathClip && actor.CurrentPose == "Death";
                displayedCorpse = proof;
                if (capture && age >= Mathf.Min(.3f, arena.DeathFallDuration * .3f)) Capture("17_enemy_death");
            }
        }

        private void AssertLiveFeedbackCoverage()
        {
            Assert.That(observedNormalFocus && observedThrowFocus && observedVisibleDamage, Is.True,
                "The real catalog route must show surviving Normal/Throw focus and active projected damage.");
            Assert.That(observedGuardFeedback && observedHeavyFocusExcluded && observedLethalFocusCleared, Is.True,
                "The same real route must show resolved defense, HeavyAOE and death exclusions.");
            Assert.That(deaths.Count, Is.GreaterThan(0), "The existing route kills an actual enemy; retain its death evidence.");
            Assert.That(deaths.Exists(item => item.retainedAfterFirstFrame), Is.True,
                "A dead actor must remain visibly retained on a later actual frame instead of instant removal.");
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
            if (capture || spatialValidation || visibleFlow)
                Assert.That(arena.CinematicVeilAlpha, Is.Zero, actorId + ": layout return may not retain BasicDamage focus.");
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
                normalFocusObserved = observedNormalFocus, throwFocusObserved = observedThrowFocus, visibleDamageObserved = observedVisibleDamage,
                guardFeedbackObserved = observedGuardFeedback, heavyFocusExcluded = observedHeavyFocusExcluded, lethalFocusCleared = observedLethalFocusCleared,
                stageOffsetProbes = stageOffsetProbes.ToArray(), deaths = deaths.ToArray(),
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

        private void AssertLiveFoleyDispatches()
        {
            object mission = typeof(RokasView).GetField("mission", PrivateInstance).GetValue(boot.View);
            var cues = (ReactiveCombatAudio)mission.GetType().GetField("reactiveAudio", PrivateInstance).GetValue(mission);
            Assert.That(cues.FidelityClipsReady, Is.True);
            var identities = new HashSet<string>();
            var counts = new Dictionary<ReactiveCombatAudioEvent, int>();
            foreach (ReactiveCombatAudioDispatch dispatch in cues.Dispatches)
            {
                string identity = dispatch.AttackSequenceId + ":" + dispatch.ActorId + ":" + dispatch.HitId + ":" + dispatch.EventId;
                Assert.That(identities.Add(identity), Is.True, "Duplicate real UI/Core audio dispatch: " + identity);
                counts.TryGetValue(dispatch.EventId, out int count);
                counts[dispatch.EventId] = count + 1;
            }
            foreach (ReactiveCombatAudioEvent required in new[] { ReactiveCombatAudioEvent.StancePreview, ReactiveCombatAudioEvent.StanceConfirm, ReactiveCombatAudioEvent.StanceCancel, ReactiveCombatAudioEvent.SwordReadiness, ReactiveCombatAudioEvent.HeavyWindup, ReactiveCombatAudioEvent.EnemyWarning, ReactiveCombatAudioEvent.GuardMetalContact, ReactiveCombatAudioEvent.ThrowRelease, ReactiveCombatAudioEvent.ThrowFleshContact })
                Assert.That(counts.ContainsKey(required), Is.True, "Missing actual UI/Core Foley event: " + required);
            Assert.That(counts[ReactiveCombatAudioEvent.StanceConfirm], Is.EqualTo(plannedCommands + fillerCommands), "Only accepted UI commands confirm the stance once.");
        }

        private ReactiveCombatArena FindArena()
        {
            object mission = typeof(RokasView).GetField("mission", PrivateInstance).GetValue(boot.View);
            reactiveView = (ReactiveMissionView)mission.GetType().GetField("reactiveView", PrivateInstance).GetValue(mission);
            return (ReactiveCombatArena)typeof(ReactiveMissionView).GetField("arena", PrivateInstance).GetValue(reactiveView);
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
            uiCamera = null; uiTarget = null; image = null; canvas = null; stage = null;
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
                coreSuffixCancellationObserved, nativeHomeRecoveryObserved, passiveStageOffsetObserved,
                normalFocusObserved, throwFocusObserved, visibleDamageObserved, guardFeedbackObserved, heavyFocusExcluded, lethalFocusCleared;
            public int[] heldRendererInstanceIds, flyingRendererInstanceIds;
            public int plannedCommands, fillerCommands; public string[] screenshots; public FrameEvidence[] frames;
            public SequenceFrame[] sequence; public PerformanceFrame[] performance; public InterruptEvidence[] interruptions;
            public StageOffsetProbe[] stageOffsetProbes; public DeathEvidence[] deaths;
        }
        [Serializable] private sealed class FrameEvidence {
            public int index, unityFrame, heldDaggerInstanceId, flyingDaggerInstanceId, releases, contacts,
                heldActiveRendererCount, flyingActiveRendererCount, corpseCount;
            public long combatUs, revision; public float realtime, frameDelta, sourceClock, enemySourceClock,
                stageRecoveryCue, sourceDuration, cameraClock, cameraFov, veilAlpha, feedbackAlpha, corpseAge;
            public double presentationClock, feedbackStartedAt;
            public Vector2 feedbackPosition, feedbackPivot;
            public string actionId, phase, section, enemyId, hunterPhase, enemyPhase, hunterPose, enemyPose,
                sourceSkill, swordOwner, swordParent, veilActionId, feedbackText, feedbackTargetId, corpseId, corpsePose;
            public bool contactConfirmed, cameraActive, cameraHome, heldDaggerVisible, flyingDaggerVisible, coreSuffixCanceled, feedbackActive, corpseActive, corpseIdleSettled;
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
        [Serializable] private sealed class DeathEvidence {
            public string actorId, firstPose, lastPose; public int firstUnityFrame, lastUnityFrame, hp, firstRendererCount;
            public long firstCombatUs; public float firstCorpseAge, lastCorpseAge, fallDuration;
            public bool isDead, hasDeathClip, firstIdleSettled, lastIdleSettled, firstActorActive, lastActorActive,
                retainedAfterFirstFrame, noLiveIdleAlias;
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
