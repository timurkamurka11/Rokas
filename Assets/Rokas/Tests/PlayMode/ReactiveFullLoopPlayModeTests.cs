using System;
using System.Collections;
using System.IO;
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
    public sealed class ReactiveFullLoopPlayModeTests
    {
        private GameObject root;
        private string directory;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode priorEditorBehavior;
        private InputSettings.BackgroundBehavior priorBackgroundBehavior;

        [UnityTest]
        public IEnumerator PortalDuelVictoryHomePaymentAndReload()
        {
            priorEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            priorBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
            InputSystem.QueueStateEvent(mouse, new MouseState(), InputState.currentTime);
            InputSystem.Update();

            directory = Path.Combine(Path.GetTempPath(), "rokas-reactive-full-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("ReactiveFullLoopFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            yield return null;
            Assert.That(boot.Session.EnterReactiveDuelTestEncounter(), Is.True);
            yield return new WaitForSecondsRealtime(1.9f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
            Assert.That(boot.View.Paused, Is.False);
            Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));

            Press("ReactiveSealStrike");
            yield return WaitForPhase(boot, ReactivePhase.EnemyExecution, 5f);
            var combat = boot.Session.ReactiveCombat;
            Assert.That(combat.EnemyHp, Is.EqualTo(132));
            Assert.That(combat.EnemySeal, Is.EqualTo(25));
            long firstAttackStart = combat.CurrentActionStartUs;

            yield return WaitForOffset(combat, firstAttackStart, 950000, 5f);
            yield return PressKey(Key.F);
            yield return WaitForOffset(combat, firstAttackStart, 1300000, 5f);
            yield return PressKey(Key.F); // Intentionally too early for h2.
            yield return WaitForOffset(combat, firstAttackStart, 2535000, 5f);
            yield return PressKey(Key.F); // Perfect final defense.
            yield return WaitForPhase(boot, ReactivePhase.CounterWindow, 3f);

            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left), InputState.currentTime);
            InputSystem.Update();
            Press("ReactiveCounterConfirm");
            InputSystem.QueueStateEvent(mouse, new MouseState(), InputState.currentTime);
            InputSystem.Update();
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
            Assert.That(combat.HunterHp, Is.EqualTo(92));
            Assert.That(combat.EnemyHp, Is.EqualTo(107));
            Assert.That(combat.EnemyBroken, Is.True);

            Press("ReactiveSealStrike");
            yield return WaitForPhase(boot, ReactivePhase.PlayerCommand, 3f);
            Assert.That(combat.EnemyHp, Is.EqualTo(47));
            Assert.That(combat.EnemyBroken, Is.False);
            Press("ReactiveBasic");
            yield return WaitForPhase(boot, ReactivePhase.EnemyExecution, 3f);
            Assert.That(combat.EnemyHp, Is.EqualTo(27));
            long secondAttackStart = combat.CurrentActionStartUs;
            yield return WaitForOffset(combat, secondAttackStart, 930000, 4f);
            yield return PressKey(Key.D);
            yield return WaitForPhase(boot, ReactivePhase.PlayerCommand, 4f);
            Assert.That(combat.HunterHp, Is.EqualTo(92));
            Press("ReactiveSealStrike");
            yield return WaitForRunPhase(boot, RunPhase.Sealed, 4f);
            Assert.That(combat.TerminalResult, Is.EqualTo(CombatOutcome.Victory));
            Assert.That(combat.EnemyHp, Is.EqualTo(0));
            Assert.That(combat.HunterAp, Is.EqualTo(2));
            long terminalRevision = combat.Revision;
            yield return null; // Let arena ownership release before a new physical key event.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D), InputState.currentTime);
            InputSystem.Update();
            yield return null;
            Assert.That(combat.Revision, Is.EqualTo(terminalRevision),
                "Post-battle input must not mutate the completed combat instance.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
            InputSystem.Update();

            Press("ReturnHome");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Payment));
            Press("LaptopHotspot");
            if (Find("LaptopBootSurface")?.activeInHierarchy == true)
            {
                boot.View.Escape();
                yield return new WaitForSecondsRealtime(.3f);
                Press("LaptopHotspot");
            }
            Press("LaptopContracts");
            int yenBeforeClaim = boot.Session.State.yen;
            Press("ClaimPayment");
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            Assert.That(boot.Session.State.yen, Is.GreaterThan(yenBeforeClaim));
            int paidYen = boot.Session.State.yen;
            Assert.That(boot.Session.ClaimPayment(), Is.False);
            Assert.That(boot.Session.State.yen, Is.EqualTo(paidYen));

            UnityEngine.Object.Destroy(root);
            yield return null;
            root = new GameObject("ReactiveReloadFixture");
            var reload = root.AddComponent<RokasBootstrap>();
            reload.Initialize(directory);
            Assert.That(reload.Session.State.phase, Is.EqualTo(RunPhase.Home));
            Assert.That(reload.Session.State.yen, Is.EqualTo(paidYen));
            Assert.That(reload.Session.ClaimPayment(), Is.False);
        }

        private IEnumerator PressKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key), InputState.currentTime);
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator WaitForPhase(RokasBootstrap boot, ReactivePhase expected, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (boot.Session.ReactiveCombat.Phase != expected && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(expected));
        }

        private static IEnumerator WaitForRunPhase(RokasBootstrap boot, RunPhase expected, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (boot.Session.State.phase != expected && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(expected));
        }

        private static IEnumerator WaitForOffset(ReactiveCombatSession combat, long startUs, long offsetUs, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (combat.CurrentCombatUs - startUs < offsetUs && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(combat.CurrentCombatUs - startUs, Is.InRange(offsetUs, offsetUs + 40000),
                "Timestamped input must be scheduled against the authored contact time.");
        }

        private void Press(string name)
        {
            Button button = Find(name)?.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, "Missing active interaction: " + name);
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
            InputSystem.settings.editorInputBehaviorInPlayMode = priorEditorBehavior;
            InputSystem.settings.backgroundBehavior = priorBackgroundBehavior;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
