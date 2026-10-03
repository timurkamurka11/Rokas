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
    public sealed class ReactiveHeavyTimingPlayModeTests
    {
        private GameObject root;
        private string directory;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode priorEditorInputBehavior;
        private InputSettings.BackgroundBehavior priorBackgroundBehavior;

        [UnityTest]
        public IEnumerator HeavyAcceptsOneTimedDevicePressAndKeepsItsCommittedTarget()
        {
            yield return RunHeavyScenario(true);
        }

        [UnityTest]
        public IEnumerator HeavyRejectsEarlyAndLateDevicePressesAndStillSettles()
        {
            yield return RunHeavyScenario(false);
        }

        private IEnumerator RunHeavyScenario(bool timed)
        {
            priorEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            priorBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
            InputSystem.Update();
            directory = Path.Combine(Path.GetTempPath(), "rokas-reactive-heavy-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("ReactiveHeavyTimingFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            yield return null;
            Press("EnterReactivePortal");
            yield return WaitFor(boot, () => boot.Session.ReactiveCombat != null &&
                boot.Session.ReactiveCombat.Phase == ReactivePhase.PlayerCommand &&
                boot.View.ReactivePresentationReady && !boot.ReactivePresentationHeld, 35f);
            ReactiveCombatSession combat = boot.Session.ReactiveCombat;
            Assert.That(combat.HunterAp, Is.EqualTo(4));

            // Basic earns the two AP needed for Heavy. The live encounter, clock and UI keep running.
            Press("ReactiveBasic");
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand),
                "The first Basic press only selects its preview.");
            Assert.That(combat.HunterAp, Is.EqualTo(4),
                "Selecting Basic cannot spend or grant AP.");
            Press("ReactiveBasic");
            yield return WaitFor(boot, () => combat.Phase == ReactivePhase.PlayerExecution, 3f);
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerExecution));
            yield return WaitFor(boot, () => combat.Phase == ReactivePhase.PlayerCommand &&
                boot.View.ReactivePresentationReady && !boot.ReactivePresentationHeld, 18f);
            Assert.That(combat.HunterAp, Is.GreaterThanOrEqualTo(5));
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(40));
            Assert.That(combat.GetActorState("E2").Hp, Is.EqualTo(60));

            Press("ReactiveTarget2");
            Assert.That(combat.SelectedTargetId, Is.EqualTo("E2"));
            int clicksBeforeHeavy = boot.View.GenericClickCount;
            int apBeforeHeavy = combat.HunterAp;
            Press("ReactiveHeavy");
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand),
                "The first Heavy press only selects its preview.");
            Assert.That(combat.HunterAp, Is.EqualTo(apBeforeHeavy),
                "Selecting Heavy cannot spend AP.");
            Press("ReactiveHeavy");
            Assert.That(boot.View.GenericClickCount, Is.EqualTo(clicksBeforeHeavy),
                "The Heavy command must not layer a generic UI click over its single contact cue.");
            yield return WaitFor(boot, () => combat.Phase == ReactivePhase.PlayerExecution, 3f);
            Assert.That(GameObject.Find("ReactiveActorCamera").GetComponent<Camera>().orthographicSize, Is.EqualTo(4.6f).Within(.001f));
            Assert.That(combat.CurrentPlayerSkillId, Is.EqualTo("heavy"));
            boot.SelectReactiveTarget("E3");
            Assert.That(combat.SelectedTargetId, Is.EqualTo("E2"),
                "Target selection is locked to the committed action.");
            yield return null;
            Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.False,
                "The reaction bar belongs only to incoming enemy attacks.");
            Assert.That(Find("ReactiveOffenseTiming").activeInHierarchy, Is.False,
                "Heavy timing starts after Keiko reaches the target.");
            long start = combat.CurrentActionStartUs;
            // Selection/approach is the real early-input phase. Waiting for the
            // timing HUD's first rendered frame can already enter its window.
            Assert.That(combat.CurrentCombatUs - start, Is.LessThan(100000),
                "The first keyboard press must precede the Heavy timing window.");
            PressSpace();
            yield return null;
            Assert.That(combat.CurrentOffenseTimingAccepted, Is.False,
                "Early device input before the strike-range timing phase is rejected.");
            ReleaseSpace();
            yield return null;
            yield return WaitFor(boot, () => Find("ReactiveOffenseTiming").activeInHierarchy, 4f);
            Assert.That(Find("ReactiveOffenseTiming").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.False);
            Assert.That(Find("ReactiveDefense").activeInHierarchy, Is.False);
            Assert.That(Find("OffenseHint").GetComponent<Text>().text, Does.Contain("SPACE"));
            Assert.That(Find("ReactiveTelegraph").GetComponent<Text>().text, Does.Contain("ТЯЖЁЛЫЙ"));

            long secondPressAt = timed ? 150000 : 260000;
            yield return WaitFor(boot, () => combat.CurrentCombatUs - start >= secondPressAt, 2f);
            Assert.That(combat.CurrentPlayerSkillId, Is.EqualTo("heavy"));
            PressSpace();
            yield return null;
            Assert.That(combat.CurrentOffenseTimingAccepted, Is.EqualTo(timed),
                timed ? "A device press in the contact window is accepted." :
                    "A device press after the contact window is rejected.");
            ReleaseSpace();

            yield return WaitFor(boot, () => combat.GetActorState("E2").Hp == 0, 4f);
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(40));
            Assert.That(combat.GetActorState("E3").Hp, Is.EqualTo(60));
            Assert.That(combat.SelectedTargetId, Is.EqualTo("E3"),
                "After target death, Core chooses the next living enemy.");
            Assert.That(boot.View.SelectedReactiveTargetId, Is.EqualTo("E3"));
            string feedback = Find("ReactiveHitFeedback").GetComponent<Text>().text;
            Assert.That(feedback, Does.Contain(timed ? "ТОЧНЫЙ УДАР  −78" : "УДАР  −68"));

            yield return WaitFor(boot, () => combat.Phase != ReactivePhase.PlayerExecution, 3f);
            Assert.That(combat.CurrentPlayerSkillId, Is.Null);
            Assert.That(Find("ReactiveOffenseTiming").activeInHierarchy, Is.False,
                "The offensive timing UI is removed when Heavy ends.");
            Assert.That(combat.Phase, Is.Not.EqualTo(ReactivePhase.SafeError));
        }

        private void PressSpace()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), InputState.currentTime);
            InputSystem.Update();
        }

        private void ReleaseSpace()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
            InputSystem.Update();
        }

        private static IEnumerator WaitFor(RokasBootstrap boot, Func<bool> ready, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while ((!ready() || boot.View.Paused) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(boot.View.Paused, Is.False);
            Assert.That(ready(), Is.True, "Timed out waiting for the combat phase.");
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
            InputSystem.settings.editorInputBehaviorInPlayMode = priorEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = priorBackgroundBehavior;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
