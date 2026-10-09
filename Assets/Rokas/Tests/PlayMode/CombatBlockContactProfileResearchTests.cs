#if UNITY_EDITOR
// RESEARCH DRAFT ONLY. Root must opt in by copying this file into the existing PlayMode
// fixture assembly after reviewing BlockProfileMarkers.diff. No asset/package changes.
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
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class CombatBlockContactProfileResearchTests
    {
        private const int WantedContacts = 8, MaxFrames = 18000, MaxEvents = 128;
        private const int MaxRawFrames = 192, MaxRawSamples = 8192, MaxSpikeRawFrames = 104, MaxSpikes = 32;
        private const string HistoryProbeName = "Rokas.Research.CpuHistorySetupProbe";
        private const int MaxRefreshEvents = MaxFrames * 8;
        private const string EvidenceRoot = "D:/DD2-Research/Reports/CombatFidelity3-2026-10-08/BlockProfileRuns";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly string[] ScopeNames = {
            "Rokas.Block.InputSubmit", "Rokas.Block.AttemptPresent", "Rokas.Combat.Advance",
            "Rokas.Combat.PresentStep", "Rokas.Combat.CheckpointClone", "Rokas.Combat.SaveCheckpoint",
            "Rokas.Combat.Refresh", "Rokas.Combat.GlobalRefresh", "Rokas.Combat.TimingRefresh",
            "Rokas.Combat.EventPresent", "Rokas.Block.PoseVfx", "Rokas.Block.Audio", "Rokas.Save.TempWriteAndFlush"
        };
        private GameObject root;
        private RokasBootstrap boot;
        private ReactiveCombatArena arena;
        private ReactiveCombatImpactEffect impacts;
        private Keyboard keyboard;
        private readonly Dictionary<string, GameObject> ui = new Dictionary<string, GameObject>();
        private readonly HashSet<string> submitted = new HashSet<string>();
        private readonly HashSet<string> resolved = new HashSet<string>();
        private readonly Dictionary<int, string> markerNames = new Dictionary<int, string>(1024);
        private readonly HashSet<int> savedRawIndices = new HashSet<int>();
        private readonly ProfilerMarker harvestMarker = new ProfilerMarker("Rokas.Research.HierarchyHarvest");
        private ProfilerMarker historyProbe;
        private string historyProbeLabel;
        private readonly ProfilerSnapshot[] profilerSnapshots = new ProfilerSnapshot[6];
        private readonly ProfilerMarker[] inputTags = new ProfilerMarker[MaxEvents];
        private readonly ProfilerMarker[] contactTags = new ProfilerMarker[WantedContacts];
        private readonly string[] contactLabelNames = new string[WantedContacts];
        private readonly Dictionary<string, int> contactLabelLookup = new Dictionary<string, int>(WantedContacts);
        private readonly ProfilerRecorder[] scopes = new ProfilerRecorder[ScopeNames.Length];
        private readonly Frame[] frames = new Frame[MaxFrames];
        private readonly RefreshEvent[] refreshEvents = new RefreshEvent[MaxRefreshEvents];
        private readonly ProfilerMarker writeFlushMarker = new ProfilerMarker("Rokas.Save.TempWriteAndFlush");
        private readonly Event[] events = new Event[MaxEvents];
        private readonly RawFrame[] rawFrames = new RawFrame[MaxRawFrames];
        private readonly bool[] contactRawFound = new bool[WantedContacts];
        private readonly int[] contactUnityFrames = new int[WantedContacts];
        private readonly int[] contactRawFrames = new int[WantedContacts];
        private readonly Spike[] pacingSpikes = new Spike[MaxSpikes];
        private int spikeCount, calibratedOffset = int.MinValue, calibrationCount;
        private bool calibrationConsistent = true, spikeOverflow;
        private readonly long[] scopeNs = new long[MaxFrames * ScopeNames.Length];
        private readonly long[] scopeCounts = new long[MaxFrames * ScopeNames.Length];
        private readonly int[] parentEnds = new int[128], parentIndices = new int[128];
        private ProfilerRecorder mainThread, gc;
        private FieldInfo inputObserver, contactObserver, saveObserver, refreshObserver, storageObserver;
        private SaveStore isolatedStore;
        private Delegate priorInputObserver, priorContactObserver, priorSaveObserver, priorRefreshObserver, priorStorageObserver;
        private InputSettings.EditorInputBehaviorInPlayMode priorInput;
        private InputSettings.BackgroundBehavior priorBackground;
        private bool inputConfigured, profilerConfigured, observersInstalled, storageObserverInstalled, storageMarkerOpen, measuring, held, completed;
        private bool priorProfilerEnabled, priorBinaryLog, priorDriverEnabled, priorCpuAreaEnabled, historyReady;
        private int priorConnection, profilerSnapshotCount, historySetupWaitedFrames, mainThreadIndex = -1, spikeRawCount;
        private int lastHarvestUnityFrame = -100, missingHistoryObservations;
        private ThreadSnapshot[] setupThreads;
        private Func<bool> isEditorConnection;
        private Func<UnityEditor.Profiling.FrameDataView, bool> belongsToEditorSession;
        private int priorVsync, priorTargetFps, frameCount, eventCount, contactCount, inputCount, saveCount;
        private int rawCount, lastObserved = -1, lastHarvest = -1, lastContactFrame = -100;
        private int commands, harvestSerial, refreshEventCount, globalRefreshCount, missionRefreshCount, timingRefreshCount;
        private string folder, stopReason = "not started";
        private float started;

        [UnityTest]
        [Category("ResearchBlockProfile")]
        public IEnumerator EightNaturalGuardContactsRetainCpuHierarchyAndSaveRefreshScopes()
        {
            // Never coexist with a user's running Bootstrap or load its persistent profile.
            foreach (var existing in Resources.FindObjectsOfTypeAll<RokasBootstrap>())
                Assert.That(existing.gameObject.scene.IsValid() && existing.gameObject.activeInHierarchy, Is.False,
                    "Research requires an isolated test scene; an active user Bootstrap exists.");
            folder = Path.Combine(EvidenceRoot, "NaturalGuard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); // All research writes precede/follow measurement.
            priorVsync = QualitySettings.vSyncCount; priorTargetFps = Application.targetFrameRate;
            priorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            priorBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputConfigured = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            root = new GameObject("CombatBlockActualCoreResearchFixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(Path.Combine(folder, "IsolatedProfile")); // explicit path before Start
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.That(boot.Session.AcceptContract() && boot.Session.LeaveHome(), Is.True);
            yield return null;
            Click("EnterReactivePortal");
            float entryDeadline = Time.realtimeSinceStartup + 45f;
            while ((boot.Session.ReactiveCombat == null || !CommandReady()) && Time.realtimeSinceStartup < entryDeadline)
                yield return null;
            Assert.That(boot.Session.ReactiveCombat, Is.Not.Null);
            Assert.That(CommandReady(), Is.True, "Production entrance/gates did not settle.");
            object mission = typeof(RokasView).GetField("mission", Private).GetValue(boot.View);
            object reactive = mission.GetType().GetField("reactiveView", Private).GetValue(mission);
            arena = (ReactiveCombatArena)reactive.GetType().GetField("arena", Private).GetValue(reactive);
            impacts = (ReactiveCombatImpactEffect)typeof(ReactiveCombatArena).GetField("impactEffect", Private).GetValue(arena);
            Assert.That(impacts, Is.Not.Null);
            InstallObservers(mission.GetType());
            for (int i = 0; i < MaxEvents; i++) inputTags[i] = new ProfilerMarker("Rokas.Research.BlockInput_" + i.ToString("D3"));
            string contactRun = Guid.NewGuid().ToString("N");
            for (int i = 0; i < WantedContacts; i++)
            {
                contactRawFrames[i] = -1;
                contactLabelNames[i] = "Rokas.Research.BlockContact_" + contactRun + "_" + i.ToString("D2");
                contactTags[i] = new ProfilerMarker(contactLabelNames[i]); contactLabelLookup.Add(contactLabelNames[i], i);
            }
            for (int i = 0; i < MaxRawFrames; i++) rawFrames[i] = new RawFrame { samples = new RawSample[MaxRawSamples] };
            // Enable the Editor receiver and CPU area, not only runtime counters. Do not
            // switch profiler connection, clear history, open a window or touch binary logging.
            BindProfilerSessionChecks();
            CaptureProfilerSnapshot("before-setup");
            priorProfilerEnabled = Profiler.enabled; priorBinaryLog = Profiler.enableBinaryLog;
            priorDriverEnabled = ProfilerDriver.enabled;
            priorCpuAreaEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU);
            priorConnection = ProfilerDriver.connectedProfiler;
            Assert.That(priorBinaryLog, Is.False, "An existing user profiler binary recording is active; do not interrupt it.");
            Assert.That(isEditorConnection(), Is.True, "Profiler is connected to a Player/remote target; root must select the local Editor before retrying.");
            historyProbeLabel = HistoryProbeName + "." + Guid.NewGuid().ToString("N");
            historyProbe = new ProfilerMarker(historyProbeLabel);
            profilerConfigured = true;
            ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, true);
            ProfilerDriver.enabled = true; Profiler.enabled = true;
            CaptureProfilerSnapshot("configured");
            float historyDeadline = Time.realtimeSinceStartup + 4f;
            for (historySetupWaitedFrames = 0; historySetupWaitedFrames < 240 && Time.realtimeSinceStartup < historyDeadline; historySetupWaitedFrames++)
            {
                using (historyProbe.Auto()) { }
                yield return null;
                if (FreshHistoryContainsProbe()) { historyReady = true; break; }
            }
            CaptureProfilerSnapshot(historyReady ? "fresh-history-ready" : "fresh-history-unavailable");
            if (!historyReady) stopReason = "Editor CPU receiver did not retain a fresh current-session Main Thread setup marker";
            Assert.That(historyReady, Is.True, stopReason + "; stopped before combat measurement. See partial setup evidence.");
            setupThreads = CaptureThreads(ProfilerDriver.lastFrameIndex);
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            for (int i = 0; i < ScopeNames.Length; i++) scopes[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, ScopeNames[i], 1,
                ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
            started = Time.realtimeSinceStartup; measuring = true; stopReason = "bounded catalog exhausted or timed out";
            float deadline = started + 240f;
            while (contactCount < WantedContacts && Time.realtimeSinceStartup < deadline && frameCount < MaxFrames)
            {
                Pump(); Observe();
                var combat = boot.Session.ReactiveCombat;
                if (combat.TerminalResult.HasValue) { stopReason = "natural terminal state"; break; }
                if (CommandReady())
                {
                    if (++commands > 24) { stopReason = "24 real commands budget exhausted"; break; }
                    yield return BasicPreviewConfirm();
                }
                else yield return null;
            }
            // Complete the last contact window without committing another player action.
            for (int i = 0; i < 12; i++) { Pump(); Observe(); yield return null; }
            measuring = false;
            HarvestCpuWindows();
            CaptureProfilerSnapshot("measurement-end");
            stopReason = contactCount >= WantedContacts ? "eight actual guarded Core contacts" : stopReason;
            completed = contactCount >= WantedContacts && historyReady && mainThread.Valid && gc.Valid;
            for (int i = 0; i < WantedContacts; i++) completed &= contactRawFound[i];
            for (int i = 0; i < scopes.Length; i++) completed &= scopes[i].Valid;
            RefreshSpikeCoverage();
            completed &= calibrationConsistent && calibrationCount == WantedContacts && !spikeOverflow;
            for (int i = 0; i < spikeCount; i++) completed &= pacingSpikes[i].expectedRawFound;
            WriteEvidence(); // after measurement, including partial evidence before assertions
            Assert.That(contactCount, Is.GreaterThanOrEqualTo(WantedContacts), stopReason);
            Assert.That(mainThread.Valid && gc.Valid, Is.True, "Required recorders unavailable; see partial evidence.");
            Assert.That(calibrationConsistent && calibrationCount == WantedContacts, Is.True, "All eight labels must agree on Unity/raw frame calibration.");
            Assert.That(spikeOverflow, Is.False, "More than the bounded spike-event capacity; coverage is incomplete.");
            for (int i = 0; i < spikeCount; i++)
                Assert.That(pacingSpikes[i].expectedRawFound, Is.True, "Genuine completed-frame spike " + i + " expected CPU frame was not retained; see coverage manifest.");
            for (int i = 0; i < WantedContacts; i++)
                Assert.That(contactRawFound[i], Is.True, "Contact " + i + " CPU hierarchy unavailable/evicted; evidence is incomplete.");
            for (int i = 0; i < scopes.Length; i++)
                Assert.That(scopes[i].Valid, Is.True, "Missing marker " + ScopeNames[i] + "; root must apply the reviewed marker draft.");
        }

        private IEnumerator BasicPreviewConfirm()
        {
            var combat = boot.Session.ReactiveCombat;
            string target = null; int largestHp = -1;
            // Only live current-wave actors. Do not extend HP/Seals, waves or enemy catalog.
            for (int i = 0; i < combat.ActiveEnemyIds.Count; i++)
            {
                string id = combat.ActiveEnemyIds[i]; var actor = combat.GetActorState(id);
                if (actor != null && actor.Alive && actor.Hp > largestHp) { target = id; largestHp = actor.Hp; }
            }
            if (target == null) yield break;
            boot.SelectReactiveTarget(target);
            ui.Clear(); yield return null;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!CommandReady() && Time.realtimeSinceStartup < deadline) { Pump(); Observe(); yield return null; }
            Assert.That(CommandReady(), Is.True, "Current Basic not ready after real target selection.");
            Click("ReactiveBasic"); yield return null;
            while (!PreviewReady() && Time.realtimeSinceStartup < deadline) { Pump(); Observe(); yield return null; }
            Assert.That(PreviewReady(), Is.True, "Real native preview/gates did not settle.");
            Click("ReactiveBasic");
            while (combat.Phase != ReactivePhase.PlayerExecution && Time.realtimeSinceStartup < deadline)
            { Pump(); Observe(); yield return null; }
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerExecution), "Preview confirm did not commit through production.");
        }

        private bool CommandReady() => boot != null && boot.Session.ReactiveCombat != null &&
            boot.Session.ReactiveCombat.Phase == ReactivePhase.PlayerCommand &&
            boot.View.ReactivePresentationReady && !boot.ReactivePresentationHeld && !boot.View.Paused && Ready("ReactiveBasic");
        private bool PreviewReady() => CommandReady() && arena.HunterMotionPhase == "Preview";

        private void Pump()
        {
            if (held)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
                InputSystem.Update(); held = false;
            }
            var combat = boot.Session.ReactiveCombat;
            if (combat.Phase != ReactivePhase.EnemyExecution || combat.CurrentAttack == null || boot.ReactivePresentationHeld || boot.View.Paused) return;
            long offset = combat.CurrentCombatUs - combat.CurrentActionStartUs;
            for (int i = 0; i < combat.CurrentAttack.Hits.Count; i++)
            {
                var hit = combat.CurrentAttack.Hits[i];
                // Standard Parry early=110ms, Perfect early=40ms. Aim for an actual ordinary Block.
                if (offset < hit.ImpactUs - 85000 || offset >= hit.ImpactUs - 45000 ||
                    (hit.AllowedResponses & DefenseResponseMask.Parry) == 0) continue;
                string id = combat.CurrentActionId + "/" + hit.Id;
                if (!submitted.Add(id)) continue;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E), InputState.currentTime);
                InputSystem.Update(); held = true; break;
            }
            // No counter confirmation, direct Core submission, synthetic contact or scheduler edit.
        }

        private void InstallObservers(Type missionType)
        {
            inputObserver = typeof(RokasBootstrap).GetField("ResearchDefenseObserved", Static);
            contactObserver = missionType.GetField("ResearchContactObserved", Static);
            saveObserver = typeof(RokasBootstrap).GetField("ResearchCheckpointSaveObserved", Static);
            refreshObserver = typeof(RokasView).GetField("ResearchRefreshObserved", Static);
            Assert.That(inputObserver != null && contactObserver != null && saveObserver != null && refreshObserver != null, Is.True,
                "The root-reviewed Presentation marker/observer draft must be applied first.");
            priorInputObserver = (Delegate)inputObserver.GetValue(null);
            priorContactObserver = (Delegate)contactObserver.GetValue(null);
            priorSaveObserver = (Delegate)saveObserver.GetValue(null);
            priorRefreshObserver = (Delegate)refreshObserver.GetValue(null);
            observersInstalled = true;
            inputObserver.SetValue(null, new Action<ReactiveDevicePress, DefenseAttempt, long>(OnInput));
            contactObserver.SetValue(null, new Action<CombatEvent>(OnContact));
            saveObserver.SetValue(null, new Action<bool>(OnSave));
            refreshObserver.SetValue(null, new Action<int>(OnRefresh));
            isolatedStore = (SaveStore)typeof(RokasBootstrap).GetField("store", Private).GetValue(boot);
            Assert.That(Path.GetFullPath(isolatedStore.PrimaryPath).StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            storageObserver = typeof(SaveStore).GetField("commitObserver", Private);
            priorStorageObserver = (Delegate)storageObserver.GetValue(isolatedStore);
            Assert.That(priorStorageObserver, Is.Null, "Isolated production store must have no preexisting fault observer.");
            // Use the existing precise-storage seam on this fixture-only instance.
            // Mono FieldInfo.SetValue must succeed; failure produces a RED diagnostic, not a fallback save bypass.
            storageObserver.SetValue(isolatedStore, new Action<SaveCommitStage>(OnStorage));
            storageObserverInstalled = true;
        }
        private void OnInput(ReactiveDevicePress press, DefenseAttempt attempt, long combatUs)
        {
            if (!measuring || eventCount >= MaxEvents || inputCount >= MaxEvents) return;
            using (inputTags[inputCount].Auto())
            {
                events[eventCount++] = new Event { type = "input", ordinal = inputCount++, unityFrame = Time.frameCount,
                    profilerLastCompleted = ProfilerDriver.lastFrameIndex, realtime = Time.realtimeSinceStartup - started,
                    timestamp = System.Diagnostics.Stopwatch.GetTimestamp(), combatUs = combatUs, inputId = press.InputId, epoch = press.Epoch, accepted = attempt.Accepted,
                    outcome = attempt.Outcome.ToString(), actionId = boot.Session.ReactiveCombat.CurrentActionId, hitId = attempt.HitId };
            }
        }
        private void OnContact(CombatEvent evt)
        {
            if (!measuring || eventCount >= MaxEvents || evt.Kind != CombatEventKind.HitResolved || evt.TargetId != "P") return;
            bool guarded = evt.Detail == "Parry" || evt.Detail == "Perfect";
            bool duplicate = !resolved.Add(evt.ActionId + "/" + evt.HitId);
            int index = guarded && !duplicate ? contactCount : -1;
            if (index >= 0 && index < WantedContacts) { contactUnityFrames[index] = Time.frameCount; contactTags[index].Begin(); }
            events[eventCount++] = new Event { type = "contact", ordinal = index, unityFrame = Time.frameCount,
                profilerLastCompleted = ProfilerDriver.lastFrameIndex, realtime = Time.realtimeSinceStartup - started,
                actionId = evt.ActionId, actorId = evt.ActorId, hitId = evt.HitId, outcome = evt.Detail,
                timestamp = System.Diagnostics.Stopwatch.GetTimestamp(), combatUs = evt.CombatUs, damage = evt.Amount, duplicate = duplicate, guardVfxDispatches = impacts.GuardDispatches };
            if (index >= 0 && index < WantedContacts) contactTags[index].End();
            if (guarded && !duplicate) { contactCount++; lastContactFrame = Time.frameCount; }
        }
        private void OnSave(bool begin)
        {
            if (!measuring || eventCount >= MaxEvents) return;
            events[eventCount++] = new Event { type = begin ? "save-begin" : "save-end", unityFrame = Time.frameCount,
                profilerLastCompleted = ProfilerDriver.lastFrameIndex, realtime = Time.realtimeSinceStartup - started,
                timestamp = System.Diagnostics.Stopwatch.GetTimestamp(), combatUs = boot.Session.ReactiveCombat.CurrentCombatUs, revision = boot.Session.ReactiveCombat.Revision,
                ordinal = saveCount };
            if (!begin) saveCount++;
        }

        private void OnRefresh(int kind)
        {
            if (!measuring || refreshEventCount >= MaxRefreshEvents) return;
            refreshEvents[refreshEventCount++] = new RefreshEvent { kind = kind, unityFrame = Time.frameCount,
                realtime = Time.realtimeSinceStartup - started, timestamp = System.Diagnostics.Stopwatch.GetTimestamp() };
            if (kind == 0) globalRefreshCount++; else if (kind == 1) missionRefreshCount++; else if (kind == 2) timingRefreshCount++;
        }
        private void OnStorage(SaveCommitStage stage)
        {
            if (!measuring) return;
            if (stage == SaveCommitStage.BeforeTemporaryWrite) { writeFlushMarker.Begin(); storageMarkerOpen = true; }
            if (eventCount < MaxEvents) events[eventCount++] = new Event { type = stage.ToString(), unityFrame = Time.frameCount,
                profilerLastCompleted = ProfilerDriver.lastFrameIndex, realtime = Time.realtimeSinceStartup - started,
                timestamp = System.Diagnostics.Stopwatch.GetTimestamp(), revision = boot.Session.ReactiveCombat.Revision };
            if (stage == SaveCommitStage.AfterTemporaryFlush && storageMarkerOpen) { writeFlushMarker.End(); storageMarkerOpen = false; }
        }

        private void Observe()
        {
            if (!measuring || Time.frameCount == lastObserved || frameCount >= MaxFrames) return;
            lastObserved = Time.frameCount;
            var combat = boot.Session.ReactiveCombat;
            int baseIndex = frameCount * ScopeNames.Length;
            for (int i = 0; i < scopes.Length; i++)
            {
                if (!scopes[i].Valid || scopes[i].Count == 0) { scopeNs[baseIndex + i] = -1; scopeCounts[baseIndex + i] = -1; continue; }
                var sample = scopes[i].GetSample(scopes[i].Count - 1);
                scopeNs[baseIndex + i] = sample.Value; scopeCounts[baseIndex + i] = sample.Count;
            }
            frames[frameCount++] = new Frame { unityFrame = Time.frameCount, profilerLastCompleted = ProfilerDriver.lastFrameIndex,
                phase = (int)combat.Phase, realtime = Time.realtimeSinceStartup - started, delta = Time.unscaledDeltaTime,
                mainThreadNs = mainThread.Valid ? mainThread.LastValue : -1, gcAllocatedBytes = gc.Valid ? gc.LastValue : -1,
                actionId = combat.CurrentActionId, combatUs = combat.CurrentCombatUs, revision = combat.Revision,
                contacts = contactCount, guardVfxDispatches = impacts.GuardDispatches, harvestSerial = harvestSerial };
            // A one-off bounded CPU-history harvest after the contact window keeps the cold
            // contact from eviction. Its own named scope and neighboring frames are excluded
            // from performance interpretation. No screenshot, GPU readback or disk access.
            // Identify the completed sample BEFORE this frame can start any harvest.
            // A harvest in Unity H affects recorder rows H+1/H+2, not the preceding
            // genuine sample that triggered harvest in H. No per-frame reflection/I/O.
            if (mainThread.Valid && mainThread.LastValue > 50000000 &&
                Time.frameCount > lastHarvestUnityFrame + 2) QueueCompletedSpike();
            if (contactCount > 0 && contactCount != lastHarvest && Time.frameCount - lastContactFrame >= 8)
            { HarvestCpuWindows(); lastHarvest = contactCount; }
            if (ProfilerDriver.lastFrameIndex < 0) missingHistoryObservations++;
            HarvestDueSpikeWindows();
        }

        private void HarvestCpuWindows()
        {
            using (harvestMarker.Auto())
            {
                harvestSerial++; lastHarvestUnityFrame = Time.frameCount;
                if (measuring && eventCount < MaxEvents) events[eventCount++] = new Event { type = "hierarchy-harvest-begin", unityFrame = Time.frameCount, profilerLastCompleted = ProfilerDriver.lastFrameIndex,
                    realtime = Time.realtimeSinceStartup - started, timestamp = System.Diagnostics.Stopwatch.GetTimestamp(), ordinal = harvestSerial };
                int last = ProfilerDriver.lastFrameIndex, first = Math.Max(ProfilerDriver.firstFrameIndex, last - 24);
                if (last < 0 || mainThreadIndex < 0) return;
                // Locate unique static research labels. The runtime frame number is not
                // assumed equal to Editor profiler frame indices.
                for (int f = first; f <= last; f++)
                {
                    using (var raw = ProfilerDriver.GetRawFrameDataView(f, mainThreadIndex))
                    {
                        if (!raw.valid || raw.threadName != "Main Thread" || !belongsToEditorSession(raw)) continue;
                        for (int s = 0; s < raw.sampleCount; s++)
                        {
                            string name = SampleName(raw, s);
                            int c;
                            if (name != null && contactLabelLookup.TryGetValue(name, out c) && c < Math.Min(contactCount, WantedContacts) && !contactRawFound[c])
                            {
                                contactRawFound[c] = true;
                                CalibrateContact(c, f);
                                for (int around = Math.Max(ProfilerDriver.firstFrameIndex, f - 6); around <= Math.Min(last, f + 4); around++) CopyRaw(around, c, "guard-contact", contactUnityFrames[c]);
                            }
                        }
                    }
                }
                RefreshSpikeCoverage();
                if (measuring && eventCount < MaxEvents) events[eventCount++] = new Event { type = "hierarchy-harvest-end", unityFrame = Time.frameCount, profilerLastCompleted = ProfilerDriver.lastFrameIndex,
                    realtime = Time.realtimeSinceStartup - started, timestamp = System.Diagnostics.Stopwatch.GetTimestamp(), ordinal = harvestSerial };
            }
        }
        private void BindProfilerSessionChecks()
        {
            // These existing installed Editor checks are internal in 6000.3.19f1.
            // Bind once before measurement; no per-frame reflection or assembly install.
            var connection = typeof(ProfilerDriver).GetMethod("IsConnectionEditor", Static);
            var session = typeof(ProfilerDriver).GetMethod("FrameDataBelongsToCurrentEditorSession", Static);
            Assert.That(connection != null && session != null, Is.True, "Installed Editor session checks unavailable; no profiling target switch is allowed.");
            isEditorConnection = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), connection);
            belongsToEditorSession = (Func<UnityEditor.Profiling.FrameDataView, bool>)Delegate.CreateDelegate(
                typeof(Func<UnityEditor.Profiling.FrameDataView, bool>), session);
        }
        private void CaptureProfilerSnapshot(string stage)
        {
            if (profilerSnapshotCount >= profilerSnapshots.Length) return;
            profilerSnapshots[profilerSnapshotCount++] = new ProfilerSnapshot { stage = stage,
                unityFrame = Time.frameCount, first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex,
                engineEnabled = Profiler.enabled, driverEnabled = ProfilerDriver.enabled,
                cpuAreaEnabled = ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU), profileEditor = ProfilerDriver.profileEditor,
                deepProfiling = ProfilerDriver.deepProfiling, connectedProfiler = ProfilerDriver.connectedProfiler,
                connectionIsEditor = isEditorConnection(), binaryLogEnabled = Profiler.enableBinaryLog };
        }
        private bool FreshHistoryContainsProbe()
        {
            int last = ProfilerDriver.lastFrameIndex;
            if (last < 0 || ProfilerDriver.connectedProfiler != priorConnection) return false;
            int first = Math.Max(Math.Max(0, ProfilerDriver.firstFrameIndex), last - 12);
            for (int f = last; f >= first; f--)
                for (int t = 0; t < 32; t++)
                    using (var raw = ProfilerDriver.GetRawFrameDataView(f, t))
                    {
                        if (!raw.valid) break;
                        if (raw.threadName != "Main Thread" || !belongsToEditorSession(raw)) continue;
                        for (int s = 0; s < raw.sampleCount; s++)
                            if (raw.GetSampleName(s) == historyProbeLabel) { mainThreadIndex = t; return true; }
                    }
            return false;
        }
        private ThreadSnapshot[] CaptureThreads(int frame)
        {
            var inventory = new ThreadSnapshot[32]; int count = 0;
            for (int t = 0; t < inventory.Length; t++)
                using (var raw = ProfilerDriver.GetRawFrameDataView(frame, t))
                {
                    if (!raw.valid) break;
                    inventory[count++] = new ThreadSnapshot { index = t, name = raw.threadName, group = raw.threadGroupName,
                        samples = raw.sampleCount, currentEditorSession = belongsToEditorSession(raw) };
                }
            return Trim(inventory, count);
        }
        private void QueueCompletedSpike()
        {
            if (spikeCount >= MaxSpikes) { spikeOverflow = true; return; }
            pacingSpikes[spikeCount] = new Spike { ordinal = spikeCount,
                observedUnityFrame = Time.frameCount, completedUnityFrame = Time.frameCount - 1,
                profilerAvailableAtTrigger = ProfilerDriver.lastFrameIndex, harvestAfterUnityFrame = Time.frameCount + 2,
                harvestUnityFrame = -1, firstCopiedProfilerFrame = -1, lastCopiedProfilerFrame = -1, expectedProfilerFrame = -1,
                mainThreadNs = mainThread.LastValue, gcAllocatedBytes = gc.Valid ? gc.LastValue : -1,
                delta = Time.unscaledDeltaTime, realtime = Time.realtimeSinceStartup - started,
                actionId = boot.Session.ReactiveCombat.CurrentActionId };
            spikeCount++;
            RefreshSpikeCoverage();
        }
        private void CalibrateContact(int contact, int profilerFrame)
        {
            if (contactUnityFrames[contact] <= 0) { calibrationConsistent = false; return; }
            contactRawFrames[contact] = profilerFrame; calibrationCount++;
            int offset = contactUnityFrames[contact] - profilerFrame;
            if (calibratedOffset == int.MinValue) calibratedOffset = offset;
            else if (calibratedOffset != offset) calibrationConsistent = false;
            RefreshSpikeCoverage();
        }
        private void RefreshSpikeCoverage()
        {
            if (calibratedOffset == int.MinValue || !calibrationConsistent) return;
            for (int i = 0; i < spikeCount; i++)
            {
                Spike spike = pacingSpikes[i];
                spike.expectedProfilerFrame = spike.completedUnityFrame - calibratedOffset;
                spike.expectedRawFound = savedRawIndices.Contains(spike.expectedProfilerFrame);
                pacingSpikes[i] = spike;
            }
        }
        private void HarvestDueSpikeWindows()
        {
            for (int i = 0; i < spikeCount; i++)
            {
                Spike spike = pacingSpikes[i];
                if (spike.harvested || Time.frameCount < spike.harvestAfterUnityFrame) continue;
                int last = ProfilerDriver.lastFrameIndex;
                if (last < 0 || mainThreadIndex < 0) continue;
                // Before the first real contact calibrates indices, retain the deferred
                // available window and validate later. Never invent Unity/raw equality.
                if (calibratedOffset != int.MinValue && calibrationConsistent && last < spike.expectedProfilerFrame) continue;
                using (harvestMarker.Auto())
                {
                    harvestSerial++; lastHarvestUnityFrame = Time.frameCount;
                    if (eventCount < MaxEvents) events[eventCount++] = new Event { type = "spike-harvest-begin",
                        unityFrame = Time.frameCount, profilerLastCompleted = last, realtime = Time.realtimeSinceStartup - started,
                        timestamp = System.Diagnostics.Stopwatch.GetTimestamp(), ordinal = spike.ordinal };
                    spike.harvestUnityFrame = Time.frameCount;
                    spike.firstCopiedProfilerFrame = Math.Max(ProfilerDriver.firstFrameIndex, last - 6);
                    spike.lastCopiedProfilerFrame = last;
                    for (int f = spike.firstCopiedProfilerFrame; f <= last && spikeRawCount < MaxSpikeRawFrames; f++)
                        CopyRaw(f, -1, "pacing-spike", spike.observedUnityFrame);
                    spike.harvested = true; pacingSpikes[i] = spike;
                    RefreshSpikeCoverage();
                    if (eventCount < MaxEvents) events[eventCount++] = new Event { type = "spike-harvest-end",
                        unityFrame = Time.frameCount, profilerLastCompleted = last, realtime = Time.realtimeSinceStartup - started,
                        timestamp = System.Diagnostics.Stopwatch.GetTimestamp(), ordinal = spike.ordinal };
                }
            }
        }
        private string SampleName(UnityEditor.Profiling.RawFrameDataView raw, int index)
        {
            int id = raw.GetSampleMarkerId(index); string name;
            if (!markerNames.TryGetValue(id, out name)) { name = raw.GetSampleName(index); markerNames.Add(id, name); }
            return name;
        }
        private void CopyRaw(int index, int contact, string reason, int triggerUnityFrame)
        {
            if (rawCount >= MaxRawFrames || savedRawIndices.Contains(index)) return;
            using (var raw = ProfilerDriver.GetRawFrameDataView(index, mainThreadIndex))
            {
                if (!raw.valid || raw.threadName != "Main Thread" || !belongsToEditorSession(raw)) return;
                savedRawIndices.Add(index);
                var row = rawFrames[rawCount++]; row.profilerFrame = index; row.contact = contact;
                row.reason = reason; row.triggerUnityFrame = triggerUnityFrame; row.harvestUnityFrame = Time.frameCount; row.threadIndex = mainThreadIndex;
                if (reason == "pacing-spike") spikeRawCount++;
                row.thread = raw.threadName; row.frameStartNs = raw.frameStartTimeNs; row.frameNs = raw.frameTimeNs;
                row.originalSampleCount = raw.sampleCount; row.sampleCount = Math.Min(raw.sampleCount, MaxRawSamples);
                row.truncated = row.sampleCount != raw.sampleCount;
                int depth = 0;
                for (int i = 0; i < row.sampleCount; i++)
                {
                    while (depth > 0 && i > parentEnds[depth - 1]) depth--;
                    row.samples[i] = new RawSample { index = i, parent = depth == 0 ? -1 : parentIndices[depth - 1], depth = depth,
                        markerId = raw.GetSampleMarkerId(i), name = SampleName(raw, i), startNs = raw.GetSampleStartTimeNs(i),
                        durationNs = raw.GetSampleTimeNs(i), children = raw.GetSampleChildrenCount(i) };
                    int descendants = raw.GetSampleChildrenCountRecursive(i);
                    if (descendants > 0 && depth < parentEnds.Length) { parentEnds[depth] = i + descendants; parentIndices[depth] = i; depth++; }
                }
            }
        }

        private GameObject Find(string name)
        {
            GameObject found;
            if (ui.TryGetValue(name, out found) && found != null && found.activeInHierarchy && found.transform.IsChildOf(root.transform)) return found;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name && item.gameObject.activeInHierarchy) { ui[name] = item.gameObject; return item.gameObject; }
            ui.Remove(name); return null;
        }
        private bool Ready(string name) { var button = Find(name)?.GetComponent<Button>(); return button != null && button.IsInteractable(); }
        private void Click(string name)
        {
            Assert.That(Ready(name), Is.True, "Actual active UI button not ready: " + name);
            Assert.That(ExecuteEvents.Execute(Find(name), new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler), Is.True);
        }
        private void WriteEvidence()
        {
            if (folder == null) return;
            var valid = new bool[scopes.Length]; for (int i = 0; i < scopes.Length; i++) valid[i] = scopes[i].Valid;
            var manifest = new Manifest { completed = completed, stopReason = stopReason, wantedContacts = WantedContacts,
                actualGuardContacts = contactCount, commands = commands, realBootstrapCoreUi = true, noUserPersistentSave = true,
                noScreenshotsOrReadback = true, noPerFrameResearchDiskIo = true, isolatedSaveStoreIoRetained = true,
                globalRefreshCount = globalRefreshCount, missionRefreshCount = missionRefreshCount, timingRefreshCount = timingRefreshCount,
                refreshEvents = Trim(refreshEvents, refreshEventCount), stopwatchFrequency = System.Diagnostics.Stopwatch.Frequency,
                mainThreadValid = mainThread.Valid, gcValid = gc.Valid, scopeNames = ScopeNames, scopeValid = valid,
                rawContactFound = contactRawFound, frames = Trim(frames, frameCount), events = Trim(events, eventCount),
                historyReady = historyReady, historySetupWaitedFrames = historySetupWaitedFrames, mainThreadIndex = mainThreadIndex,
                missingHistoryObservations = missingHistoryObservations, rawFrameCount = rawCount, spikeRawFrameCount = spikeRawCount,
                profilerSnapshots = Trim(profilerSnapshots, profilerSnapshotCount), setupThreads = setupThreads, historyProbeLabel = historyProbeLabel,
                contactLabelNames = contactLabelNames, contactUnityFrames = contactUnityFrames, contactRawFrames = contactRawFrames,
                calibrationConsistent = calibrationConsistent, calibrationCount = calibrationCount,
                calibratedUnityMinusProfiler = calibratedOffset, spikeOverflow = spikeOverflow, spikes = Trim(pacingSpikes, spikeCount),
                completedRecorderObservationOffset = 1, actualHarvestFramesAndNextAreExcluded = true,
                scopeDurationNs = Trim(scopeNs, frameCount * scopes.Length), scopeOccurrenceCounts = Trim(scopeCounts, frameCount * scopes.Length),
                limitation = "Profiler/raw harvest changes overhead. Exclude HierarchyHarvest and its frame/next frame; Main Thread includes waits and Editor/test cost. First Guard is first-contact, not a cold application launch. Scope recorders report the last completed sample; profiler labels, not guessed frame-index equality, link exact contacts. V3 sparse spike harvest is deferred >=2 actual Unity frames with last-6..last windows. Exact expected raw indices are validated only after unique current-run contact-label calibration. Exclude actual harvest H/H+1, which affect completed recorder rows H+1/H+2; the genuine preceding trigger remains. No claim of cause or FPS PASS." };
            File.WriteAllText(Path.Combine(folder, "block-profile.json"), JsonUtility.ToJson(manifest, true));
            for (int i = 0; i < rawCount; i++)
            {
                var row = rawFrames[i]; row.samples = Trim(row.samples, row.sampleCount);
                row.actualUnityFrame = calibratedOffset != int.MinValue && calibrationConsistent ? row.profilerFrame + calibratedOffset : -1;
                row.instrumentationExcluded = false;
                if (row.actualUnityFrame >= 0)
                    for (int e = 0; e < eventCount; e++)
                        if ((events[e].type == "hierarchy-harvest-begin" || events[e].type == "spike-harvest-begin") &&
                            (row.actualUnityFrame == events[e].unityFrame || row.actualUnityFrame == events[e].unityFrame + 1))
                            { row.instrumentationExcluded = true; break; }
                File.WriteAllText(Path.Combine(folder, "cpu-main-thread-" + row.profilerFrame + ".json"), JsonUtility.ToJson(row, false));
            }
        }
        private static T[] Trim<T>(T[] source, int count) { var result = new T[count]; Array.Copy(source, result, count); return result; }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            measuring = false;
            // Partial evidence is diagnostic only. These writes occur after measurement.
            if (!completed && folder != null) WriteEvidence();
            if (storageMarkerOpen) { writeFlushMarker.End(); storageMarkerOpen = false; }
            if (observersInstalled) { inputObserver.SetValue(null, priorInputObserver); contactObserver.SetValue(null, priorContactObserver); saveObserver.SetValue(null, priorSaveObserver); refreshObserver.SetValue(null, priorRefreshObserver); }
            if (storageObserverInstalled) storageObserver.SetValue(isolatedStore, priorStorageObserver);
            if (profilerConfigured)
            {
                CaptureProfilerSnapshot("before-restore");
                ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, priorCpuAreaEnabled);
                ProfilerDriver.enabled = priorDriverEnabled; Profiler.enabled = priorProfilerEnabled;
                // Binary logging, target connection and profileEditor were never changed.
                CaptureProfilerSnapshot("after-restore");
                WriteEvidence();
            }
            for (int i = 0; i < scopes.Length; i++) scopes[i].Dispose(); mainThread.Dispose(); gc.Dispose();
            if (held && keyboard != null) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime); InputSystem.Update(); }
            if (root != null) UnityEngine.Object.Destroy(root);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (inputConfigured) { InputSystem.settings.editorInputBehaviorInPlayMode = priorInput; InputSystem.settings.backgroundBehavior = priorBackground; }
            if (inputConfigured) { QualitySettings.vSyncCount = priorVsync; Application.targetFrameRate = priorTargetFps; }
            yield return null;
        }
        [Serializable] private sealed class Manifest {
            public bool completed, realBootstrapCoreUi, noUserPersistentSave, noScreenshotsOrReadback, noPerFrameResearchDiskIo,
                isolatedSaveStoreIoRetained, mainThreadValid, gcValid, historyReady;
            public int wantedContacts, actualGuardContacts, commands, globalRefreshCount, missionRefreshCount, timingRefreshCount;
            public int historySetupWaitedFrames, mainThreadIndex, missingHistoryObservations, rawFrameCount, spikeRawFrameCount;
            public ProfilerSnapshot[] profilerSnapshots; public ThreadSnapshot[] setupThreads; public string historyProbeLabel;
            public string[] contactLabelNames; public int[] contactUnityFrames, contactRawFrames;
            public int calibrationCount, calibratedUnityMinusProfiler, completedRecorderObservationOffset;
            public bool calibrationConsistent, spikeOverflow, actualHarvestFramesAndNextAreExcluded; public Spike[] spikes;
            public long stopwatchFrequency; public RefreshEvent[] refreshEvents; public string stopReason, limitation;
            public string[] scopeNames; public bool[] scopeValid, rawContactFound; public Frame[] frames; public Event[] events;
            public long[] scopeDurationNs, scopeOccurrenceCounts;
        }
        [Serializable] private struct Spike {
            public int ordinal, observedUnityFrame, completedUnityFrame, profilerAvailableAtTrigger, harvestAfterUnityFrame,
                harvestUnityFrame, firstCopiedProfilerFrame, lastCopiedProfilerFrame, expectedProfilerFrame;
            public bool harvested, expectedRawFound; public long mainThreadNs, gcAllocatedBytes;
            public float realtime, delta; public string actionId;
        }
        [Serializable] private struct ProfilerSnapshot {
            public string stage; public int unityFrame, first, last, connectedProfiler;
            public bool engineEnabled, driverEnabled, cpuAreaEnabled, profileEditor, deepProfiling, connectionIsEditor, binaryLogEnabled;
        }
        [Serializable] private struct ThreadSnapshot {
            public int index, samples; public string name, group; public bool currentEditorSession;
        }
        [Serializable] private struct Frame {
            public int unityFrame, profilerLastCompleted, phase, contacts, guardVfxDispatches, harvestSerial;
            public long combatUs, revision, mainThreadNs, gcAllocatedBytes; public float realtime, delta; public string actionId;
        }
        [Serializable] private struct RefreshEvent { public int kind, unityFrame; public float realtime; public long timestamp; }
        [Serializable] private struct Event {
            public string type, actionId, actorId, hitId, outcome; public int ordinal, unityFrame, profilerLastCompleted, damage, guardVfxDispatches;
            public long inputId, epoch, combatUs, revision, timestamp; public float realtime; public bool accepted, duplicate;
        }
        [Serializable] private sealed class RawFrame {
            public int profilerFrame, contact, originalSampleCount, sampleCount, triggerUnityFrame, threadIndex, harvestUnityFrame, actualUnityFrame;
            public bool truncated, instrumentationExcluded; public string thread, reason;
            public double frameStartNs, frameNs; public RawSample[] samples;
        }
        [Serializable] private struct RawSample {
            public int index, parent, depth, markerId, children; public string name; public double startNs, durationNs;
        }
    }
}
#endif
