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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class ReactiveWaveTransitionPlayModeTests
    {
        private GameObject root;
        private string directory;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode priorEditorBehavior;
        private InputSettings.BackgroundBehavior priorBackgroundBehavior;
        private bool inputConfigured;

        [UnityTest]
        public IEnumerator ThreeWavesRefreshTheLiveArenaHudAndQueueThenReachVictory()
        {
            priorEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            priorBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            inputConfigured = true;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
            InputSystem.QueueStateEvent(mouse, new MouseState(), InputState.currentTime);
            InputSystem.Update();

            directory = Path.Combine(Path.GetTempPath(), "rokas-reactive-waves-" + Guid.NewGuid().ToString("N"));
            RokasBootstrap boot = NewBootstrap();
            yield return null;
            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            yield return null;
            Press("EnterReactivePortal");
            yield return WaitForFirstCommand(boot);
            Assert.That(boot.Session.SaveReactiveCheckpoint(), Is.True);

            // Keep the authored eight-actor definitions, queue and wave membership. Lower only
            // enemy HP in this durable fixture so the runtime transition test stays short.
            UnityEngine.Object.Destroy(root);
            yield return null;
            var store = new SaveStore(directory, new UnitySaveCodec());
            SaveLoadResult loaded = store.Load();
            Assert.That(loaded.Succeeded, Is.True, loaded.Message);
            Assert.That(loaded.Data.battleCheckpoint.waveEntry, Is.Not.Null);
            SetEnemyHpToOne(loaded.Data.battleCheckpoint.actors);
            SetEnemyHpToOne(loaded.Data.battleCheckpoint.waveEntry.actors);
            loaded.Data.battleCheckpoint.enemyHp = 1;
            loaded.Data.battleCheckpoint.waveEntry.enemyHp = 1;
            loaded.Data.enemyHp = 1;
            SaveWriteResult shortened = store.Save(loaded.Data);
            Assert.That(shortened.Succeeded, Is.True, shortened.Message);

            boot = NewBootstrap();
            yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
            Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
            AssertWave(boot, 0, new[] { "E1", "E2", "E3" }, 3);
            string firstInstance = boot.Session.ReactiveCombat.WaveInstanceId;

            Press("ReactiveSweep");
            yield return WaitForPhase(boot, ReactivePhase.WaveTransition, 6f);
            AssertTransition(boot, 0, 3, 4);
            Assert.That(boot.Session.State.battleCheckpoint.phase,
                Is.EqualTo(ReactivePhase.WaveTransition.ToString()), "The clear is durable.");

            // Restore during the notice: it must not spawn the next wave twice.
            UnityEngine.Object.Destroy(root);
            yield return null;
            Assert.That(GameObject.Find("ReactiveCombatWorld"), Is.Null,
                "Disposing the old bootstrap must release its detached actor world before reload.");
            boot = NewBootstrap();
            yield return null;
            Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.WaveTransition));
            Assert.That(boot.Session.ReactiveCombat.ActiveEnemyIds, Is.Empty);
            yield return WaitForWave(boot, 1, 9f);
            AssertWave(boot, 1, new[] { "E4", "E5", "E6" }, 3);
            Assert.That(boot.Session.ReactiveCombat.WaveInstanceId, Is.Not.EqualTo(firstInstance));
            Assert.That(boot.Session.ReactiveCombat.SelectedTargetId, Is.EqualTo("E4"));
            Assert.That(boot.Session.ReactiveCombat.Queue.GetEntry("E1").Alive, Is.False);
            Assert.That(boot.Session.ReactiveCombat.Queue.GetEntry("E7").Active, Is.False);
            Assert.That(boot.Session.SaveBlocked, Is.False, boot.Session.SaveError);
            Assert.That(boot.View.Paused, Is.False);
            // The batch renderer can exceed the combat clock's 100 ms frame-gap guard
            // while drawing three skinned actors. Their live instances were checked above;
            // keep testing the real Core/UI wave flow without paying for every 3D frame.
            Camera actorCamera = GameObject.Find("ReactiveCombatWorld").GetComponentInChildren<Camera>(true);
            Assert.That(actorCamera, Is.Not.Null);
            actorCamera.enabled = false;
            Find("ReactiveAnimatedWorld").GetComponent<RawImage>().enabled = false;

            yield return DriveUntil(boot, ReactivePhase.WaveTransition, 45f);
            AssertTransition(boot, 1, 6, 7);
            string secondInstance = boot.Session.ReactiveCombat.WaveInstanceId;
            yield return WaitForWave(boot, 2, 9f);
            yield return WaitForRetiredModels(3, 2f);
            AssertWave(boot, 2, new[] { "E7", "E8" }, 2);
            Assert.That(boot.Session.ReactiveCombat.WaveInstanceId, Is.Not.EqualTo(secondInstance));
            Assert.That(boot.Session.ReactiveCombat.SelectedTargetId, Is.EqualTo("E7"));
            Assert.That(boot.Session.ReactiveCombat.Queue.GetEntry("E6").Alive, Is.False);
            Assert.That(boot.Session.ReactiveCombat.Queue.GetEntry("E8").Active, Is.True);

            yield return DriveUntil(boot, ReactivePhase.Victory, 45f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Sealed));
            Assert.That(boot.Session.ReactiveCombat.TerminalResult, Is.EqualTo(CombatOutcome.Victory));
            Assert.That(boot.Session.ReactiveCombat.CurrentWaveIndex, Is.EqualTo(2));
            Assert.That(boot.Session.ReactiveCombat.ActiveEnemyIds, Is.Empty);
            for (int i = 1; i <= 8; i++)
            {
                string id = "E" + i;
                Assert.That(boot.Session.ReactiveCombat.GetActorState(id).Hp, Is.Zero, id);
                Assert.That(boot.Session.ReactiveCombat.Queue.GetEntry(id).Alive, Is.False, id);
            }
            Assert.That(boot.Session.State.battleCheckpoint.terminalResult,
                Is.EqualTo(CombatOutcome.Victory.ToString()));
        }

        private RokasBootstrap NewBootstrap()
        {
            root = new GameObject("ReactiveWaveTransitionFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            boot.SendMessage("OnApplicationFocus", true);
            return boot;
        }

        private static void SetEnemyHpToOne(BattleActorSnapshot[] actors)
        {
            foreach (BattleActorSnapshot actor in actors)
                if (actor.id != ReactiveEightEnemyDefinitions.HunterId) actor.hp = 1;
        }

        private void AssertWave(RokasBootstrap boot, int waveIndex, string[] expectedIds, int expectedModels)
        {
            ReactiveCombatSession combat = boot.Session.ReactiveCombat;
            Assert.That(combat.CurrentWaveIndex, Is.EqualTo(waveIndex));
            Assert.That(combat.ActiveEnemyIds, Is.EqualTo(expectedIds));
            Assert.That(new HashSet<string>(combat.ActiveEnemyIds).Count, Is.EqualTo(expectedIds.Length));
            Assert.That(combat.SelectedTargetId, Is.EqualTo(expectedIds[0]));
            Assert.That(Find("ReactiveWave").GetComponent<Text>().text,
                Does.Contain((waveIndex + 1) + " / 3"));
            for (int i = 0; i < 4; i++)
            {
                GameObject target = Find("ReactiveTarget" + (i + 1));
                Assert.That(target.activeInHierarchy, Is.EqualTo(i < expectedIds.Length));
                if (i < expectedIds.Length)
                    Assert.That(target.GetComponentInChildren<Text>().text, Does.Contain(expectedIds[i]));
            }
            Assert.That(boot.View.SelectedReactiveTargetId, Is.EqualTo(expectedIds[0]));
            boot.View.RefreshReactiveCombat();
            boot.View.RefreshReactiveCombat();
            Assert.That(boot.View.AnimatedReactiveEnemyCount, Is.EqualTo(expectedModels));
            GameObject world = GameObject.Find("ReactiveCombatWorld");
            Assert.That(world, Is.Not.Null);
            Assert.That(world.GetComponentsInChildren<ReactiveCombatActorVisual>(true).Length,
                Is.EqualTo(expectedModels + 1), "No duplicate model was spawned by repeated refresh.");
        }

        private void AssertTransition(RokasBootstrap boot, int completedWave, int deadCount, int nextReserveId)
        {
            ReactiveCombatSession combat = boot.Session.ReactiveCombat;
            Assert.That(combat.CurrentWaveIndex, Is.EqualTo(completedWave));
            Assert.That(combat.ActiveEnemyIds, Is.Empty);
            Assert.That(Find("ReactiveWaveBanner").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveWaveBanner").GetComponent<Text>().text,
                Does.Contain("ВОЛНА " + (completedWave + 1)));
            Assert.That(Find("ReactiveWave").GetComponent<Text>().text,
                Does.Contain(deadCount + " / 8"));
            Assert.That(combat.Queue.GetEntry("E" + nextReserveId).Active, Is.False);
            Assert.That(combat.GetActorState("E" + nextReserveId).Hp, Is.EqualTo(1));
        }

        private IEnumerator DriveUntil(RokasBootstrap boot, ReactivePhase expected, float timeout)
        {
            ReactiveCombatSession combat = boot.Session.ReactiveCombat;
            float deadline = Time.realtimeSinceStartup + timeout;
            string guardedActionId = null;
            long lastCombatUs = combat.CurrentCombatUs;
            float lastProgressAt = Time.realtimeSinceStartup;
            while (combat.Phase != expected && !combat.TerminalResult.HasValue &&
                   Time.realtimeSinceStartup < deadline)
            {
                if (combat.Phase == ReactivePhase.PlayerCommand)
                {
                    Assert.That(combat.ActiveEnemyIds.Count, Is.InRange(1, 3));
                    string selected = combat.ActiveEnemyIds[0];
                    if (combat.SelectedTargetId != selected) boot.SelectReactiveTarget(selected);
                    Press(combat.ActiveEnemyIds.Count >= 2 && combat.HunterAp >= 3
                        ? "ReactiveSweep" : "ReactiveBasic");
                }
                else if (combat.Phase == ReactivePhase.EnemyExecution &&
                         combat.CurrentActionId != guardedActionId)
                {
                    GuardCurrentAttack(combat);
                    guardedActionId = combat.CurrentActionId;
                }
                Assert.That(combat.ActiveEnemyIds.Count, Is.LessThanOrEqualTo(3));
                Assert.That(new HashSet<string>(combat.ActiveEnemyIds).Count,
                    Is.EqualTo(combat.ActiveEnemyIds.Count), "Each actor has one active queue slot.");
                if (combat.CurrentCombatUs != lastCombatUs)
                {
                    lastCombatUs = combat.CurrentCombatUs;
                    lastProgressAt = Time.realtimeSinceStartup;
                }
                else if (Time.realtimeSinceStartup - lastProgressAt > 8f)
                    Assert.Fail("Combat clock stalled. " + RuntimeState(boot, combat));
                yield return null;
            }
            Assert.That(combat.Phase, Is.EqualTo(expected),
                "Combat stopped after " + timeout + " seconds. " + RuntimeState(boot, combat));
        }

        private static string RuntimeState(RokasBootstrap boot, ReactiveCombatSession watchedCombat)
        {
            ReactiveCombatSession currentCombat = boot.Session.ReactiveCombat;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            bool focused = (bool)typeof(RokasBootstrap).GetField("focused", flags).GetValue(boot);
            long lastDeviceUs = (long)typeof(RokasBootstrap).GetField("lastReactiveDeviceUs", flags).GetValue(boot);
            CombatClock clock = (CombatClock)typeof(RokasBootstrap).GetField("reactiveClock", flags).GetValue(boot);
            ReactiveCombatSession boundCombat = (ReactiveCombatSession)typeof(RokasBootstrap)
                .GetField("reactiveBoundCombat", flags).GetValue(boot);
            string mappedTime;
            try { mappedTime = clock == null ? "none" : clock.CombatTimeAt(lastDeviceUs).ToString(); }
            catch (Exception exception) { mappedTime = exception.GetType().Name; }
            return "WatchedPhase=" + watchedCombat.Phase + " WatchedCombatUs=" + watchedCombat.CurrentCombatUs +
                " CurrentPhase=" + currentCombat.Phase + " CurrentCombatUs=" + currentCombat.CurrentCombatUs +
                " SameCombat=" + ReferenceEquals(watchedCombat, currentCombat) +
                " BoundIsCurrent=" + ReferenceEquals(boundCombat, currentCombat) +
                " ActionStartUs=" + currentCombat.CurrentActionStartUs + " InputTime=" + InputState.currentTime +
                " RunPhase=" + boot.Session.State.phase + " Focused=" + focused +
                " Active=" + boot.isActiveAndEnabled + " LastDeviceUs=" + lastDeviceUs +
                " ClockNull=" + (clock == null) + " ClockPaused=" + (clock != null && clock.IsPaused) +
                " ClockMappedUs=" + mappedTime +
                " Paused=" + boot.View.Paused + " SaveBlocked=" + boot.Session.SaveBlocked +
                " SaveError=" + boot.Session.SaveError;
        }

        private static void GuardCurrentAttack(ReactiveCombatSession combat)
        {
            if (combat.Phase != ReactivePhase.EnemyExecution || combat.CurrentAttack == null) return;
            string actionId = combat.CurrentActionId;
            long start = combat.CurrentActionStartUs;
            foreach (HitDefinition hit in combat.CurrentAttack.Hits)
            {
                DefenseAttempt attempt = combat.SubmitDefense(new DefenseIntent(
                    "wave-guard-" + actionId + "-" + hit.Id, combat.InputEpoch,
                    DefenseKind.Dodge, start + hit.ImpactUs - 150000));
                Assert.That(attempt.Outcome, Is.EqualTo(DefenseOutcome.Dodge));
                combat.ReleaseDefense(DefenseKind.Dodge, combat.InputEpoch);
            }
        }

        private static IEnumerator WaitForFirstCommand(RokasBootstrap boot)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while ((boot.Session.State.phase != RunPhase.Combat || boot.View.Paused ||
                    boot.Session.ReactiveCombat == null ||
                    boot.Session.ReactiveCombat.Phase != ReactivePhase.PlayerCommand) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
            Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
        }

        private static IEnumerator WaitForPhase(RokasBootstrap boot, ReactivePhase phase, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (boot.Session.ReactiveCombat.Phase != phase && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(phase));
        }

        private static IEnumerator WaitForWave(RokasBootstrap boot, int waveIndex, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (boot.Session.ReactiveCombat.CurrentWaveIndex != waveIndex &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(boot.Session.ReactiveCombat.CurrentWaveIndex, Is.EqualTo(waveIndex));
            Assert.That(boot.Session.ReactiveCombat.ActiveEnemyIds.Count,
                Is.GreaterThan(0));
        }

        private static IEnumerator WaitForRetiredModels(int expectedTotal, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            GameObject world = GameObject.Find("ReactiveCombatWorld");
            Assert.That(world, Is.Not.Null);
            while (world.GetComponentsInChildren<ReactiveCombatActorVisual>(true).Length > expectedTotal &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(world.GetComponentsInChildren<ReactiveCombatActorVisual>(true).Length,
                Is.EqualTo(expectedTotal), "Retired wave models must leave the hierarchy after their death animation.");
        }

        private void Press(string name)
        {
            Button button = Find(name)?.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, "Missing interaction: " + name);
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private GameObject Find(string name)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (inputConfigured)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = priorEditorBehavior;
                InputSystem.settings.backgroundBehavior = priorBackgroundBehavior;
            }
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
