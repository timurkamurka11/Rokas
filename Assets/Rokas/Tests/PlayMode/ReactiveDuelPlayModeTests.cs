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
    public sealed class ReactiveDuelPlayModeTests
    {
        private GameObject root;
        private string directory;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode priorEditorInputBehavior;
        private InputSettings.BackgroundBehavior priorBackgroundBehavior;

        [UnityTest]
        public IEnumerator PortalOffersPlayableReactiveDuelWithProjectArt()
        {
            priorEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            priorBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
            InputSystem.Update();
            directory = Path.Combine(Path.GetTempPath(), "rokas-reactive-duel-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("ReactiveDuelFixture");
            RokasBootstrap boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Portal));
            boot.SubmitReactiveCommand(CommandKind.Basic, null);
            Assert.That(boot.Session.ReactiveCombat, Is.Null, "Combat commands outside the arena are ignored.");

            Press("EnterReactivePortal");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            RawImage hunter = Find("ReactiveHunterKeiko").GetComponent<RawImage>();
            RawImage enemy = Find("ReactiveEnemyFacelessCommuter").GetComponent<RawImage>();
            Assert.That(hunter.texture, Is.SameAs(assets.vnKeikoCharacterSheet));
            Assert.That(enemy.texture, Is.SameAs(assets.enemy));
            Assert.That(hunter.uvRect, Is.EqualTo(VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko").BodyUv));
            Assert.That(Find("ReactiveHunterMina"), Is.Null);
            Assert.That(Find("ReactiveHunterHp"), Is.Not.Null);
            Assert.That(Find("ReactiveAp"), Is.Not.Null);
            Assert.That(Find("ReactiveTargetHp"), Is.Not.Null);
            Assert.That(Find("ReactiveTargetSeal"), Is.Not.Null);
            Assert.That(Find("ReactiveForecast"), Is.Not.Null);
            Assert.That(Find("ReactiveBasic"), Is.Not.Null);
            Assert.That(Find("ReactiveSealStrike"), Is.Not.Null);
            Assert.That(Find("ReactiveDefend"), Is.Not.Null);

            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(boot.View.Paused, Is.False);
            long revision = boot.Session.ReactiveCombat.Revision;
            Press("ReactiveBasic");
            Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.PlayerExecution));
            Assert.That(boot.Session.ReactiveCombat.Revision, Is.GreaterThan(revision));
            revision = boot.Session.ReactiveCombat.Revision;
            boot.SubmitReactiveCommand(CommandKind.Basic, null);
            Assert.That(boot.Session.ReactiveCombat.Revision, Is.EqualTo(revision),
                "A second command cannot enter the already committed turn.");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (boot.Session.ReactiveCombat.Phase != ReactivePhase.EnemyExecution &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.EnemyExecution));
            long attackStart = boot.Session.ReactiveCombat.CurrentActionStartUs;
            while (boot.Session.ReactiveCombat.CurrentCombatUs - attackStart < 820000 &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(boot.Session.ReactiveCombat.CurrentCombatUs - attackStart, Is.LessThan(1060000),
                "The first defense press must be queued inside its authored acquisition window.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D), InputState.currentTime);
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
            InputSystem.Update();
            while (boot.Session.ReactiveCombat.CurrentCombatUs - attackStart < 1130000 &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(boot.Session.ReactiveCombat.HunterHp, Is.EqualTo(100),
                "Timestamped Dodge from Input System must reach Core before the hit resolves.");
        }

        private void Press(string name)
        {
            Button button = Find(name)?.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, "Missing active interaction: " + name);
            Assert.That(button.IsInteractable(), Is.True, name);
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
