using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class ReactivePolishIILiveLongRunPlayModeTests
    {
        private GameObject root;
        private Keyboard keyboard;
        private string profileRoot;
        private InputSettings.EditorInputBehaviorInPlayMode priorInput;
        private InputSettings.BackgroundBehavior priorBackground;

        [UnityTest]
        [Timeout(660000)]
        public IEnumerator FiveNormalFiveHeavyThenTenAlternatingLiveCommandsCompleteWithDefenseAndExactReturns()
        {
            priorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            priorBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            profileRoot = Path.Combine(Path.GetTempPath(), "rokas-polish-ii-longrun-" + Guid.NewGuid().ToString("N"));
            int completed = 0, fillerNormals = 0, encounters = 0, defenses = 0;
            bool keyHeld = false;
            var defended = new HashSet<string>();
            var offense = new HashSet<string>();
            float deadline = Time.realtimeSinceStartup + 600f;
            float nextDiagnostic = Time.realtimeSinceStartup + 15f;
            RokasBootstrap boot = null;
            ReactiveCombatActorVisual hunter = null;
            Vector3 home = default(Vector3);
            Quaternion facing = default(Quaternion);
            bool outstanding = false, countsTowardPlan = false;
            string target = null;
            int priorHp = 0;

            while (completed < 20 && Time.realtimeSinceStartup < deadline)
            {
                if (keyHeld) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime); InputSystem.Update(); keyHeld = false; }
                if (boot == null)
                {
                    root = new GameObject("PolishIILiveLongRun");
                    boot = root.AddComponent<RokasBootstrap>();
                    boot.Initialize(Path.Combine(profileRoot, "encounter-" + encounters++));
                    boot.SendMessage("OnApplicationFocus", true);
                    yield return null;
                    Assert.That(boot.Session.AcceptContract() && boot.Session.LeaveHome(), Is.True);
                    yield return null;
                    Press("EnterReactivePortal");
                    float entryDeadline = Time.realtimeSinceStartup + 8f;
                    while (boot.Session.ReactiveCombat == null && Time.realtimeSinceStartup < entryDeadline)
                        yield return null;
                    Assert.That(boot.Session.ReactiveCombat, Is.Not.Null, "The live portal transition must create the encounter.");
                    hunter = null;
                    defended.Clear(); offense.Clear();
                    yield return null;
                    continue;
                }
                ReactiveCombatSession combat = boot.Session.ReactiveCombat;
                Assert.That(combat, Is.Not.Null);
                Assert.That(boot.Session.SaveBlocked, Is.False, boot.Session.SaveError);
                Assert.That(combat.TerminalResult, Is.Not.EqualTo(CombatOutcome.Defeat));
                if (hunter == null)
                    foreach (var actor in GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                        if (actor.name == "CombatActor_Keiko") { hunter = actor; break; }

                bool commandReady = combat.Phase == ReactivePhase.PlayerCommand &&
                    boot.View.ReactivePresentationReady && !boot.ReactivePresentationHeld && Ready("ReactiveBasic");
                if (Time.realtimeSinceStartup >= nextDiagnostic)
                {
                    Debug.Log("[POLISH II LONGRUN] completed=" + completed + " filler=" + fillerNormals +
                        " phase=" + combat.Phase + " clock=" + combat.CurrentCombatUs +
                        " held=" + boot.ReactivePresentationHeld + " ready=" + boot.View.ReactivePresentationReady +
                        " controls=" + Ready("ReactiveBasic") + " home=" + boot.View.HunterAtHome +
                        " pose=" + hunter.CurrentPose + " settled=" + hunter.IdleSettled + " hp=" + combat.HunterHp);
                    nextDiagnostic = Time.realtimeSinceStartup + 15f;
                }
                if (outstanding && (commandReady || combat.TerminalResult == CombatOutcome.Victory))
                {
                    Assert.That(combat.GetActorState(target).Hp, Is.LessThan(priorHp), "Every counted command must resolve a real Core contact.");
                    // A victory may retain the final corpse; allow the final attacker return to finish.
                    if (!boot.View.HunterAtHome || !hunter.IdleSettled) { yield return null; continue; }
                    Assert.That(hunter.transform.localPosition, Is.EqualTo(home));
                    Assert.That(hunter.ModelRoot.localRotation, Is.EqualTo(facing));
                    Assert.That(hunter.WeaponAttachment.Socket.childCount, Is.EqualTo(1));
                    var camera = GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
                    Assert.That(camera.transform.localPosition, Is.EqualTo(new Vector3(0f, 2.25f, -20f)));
                    Assert.That(camera.orthographicSize, Is.EqualTo(4.6f));
                    if (countsTowardPlan) completed++;
                    else fillerNormals++;
                    TestContext.WriteLine("completed=" + completed + " fillerNormals=" + fillerNormals + " wave=" + combat.CurrentWaveIndex + " hunterHp=" + combat.HunterHp);
                    outstanding = false;
                }
                if (combat.TerminalResult == CombatOutcome.Victory && !outstanding)
                {
                    UnityEngine.Object.Destroy(root); yield return null;
                    boot = null; hunter = null; continue;
                }
                if (commandReady && !outstanding)
                {
                    home = hunter.transform.localPosition;
                    facing = hunter.ModelRoot.localRotation;
                    bool requestedHeavy = completed >= 5 && (completed < 10 || completed % 2 != 0);
                    bool heavy = requestedHeavy && combat.HunterAp >= 5;
                    countsTowardPlan = !requestedHeavy || heavy;
                    target = combat.ActiveEnemyIds[0];
                    boot.SelectReactiveTarget(target);
                    priorHp = combat.GetActorState(target).Hp;
                    Press(heavy ? "ReactiveHeavy" : "ReactiveBasic");
                    outstanding = true;
                }
                else if (combat.Phase == ReactivePhase.PlayerExecution && combat.CurrentPlayerSkillId == "heavy" &&
                    combat.CurrentCombatUs - combat.CurrentActionStartUs >= 160000 &&
                    offense.Add(combat.CurrentActionId))
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), InputState.currentTime);
                    InputSystem.Update(); keyHeld = true;
                }
                else if (combat.Phase == ReactivePhase.EnemyExecution && combat.CurrentAttack != null)
                {
                    foreach (HitDefinition hit in combat.CurrentAttack.Hits)
                    {
                        string id = combat.CurrentActionId + "/" + hit.Id;
                        long offset = combat.CurrentCombatUs - combat.CurrentActionStartUs;
                        bool dodge = defenses % 2 == 0 && (hit.AllowedResponses & DefenseResponseMask.Dodge) != 0;
                        long early = dodge ? 170000 : 85000;
                        if (offset < hit.ImpactUs - early || offset >= hit.ImpactUs || defended.Contains(id)) continue;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(dodge ? Key.Q : Key.E), InputState.currentTime);
                        InputSystem.Update(); keyHeld = true; defended.Add(id); defenses++;
                        break;
                    }
                }
                else if (combat.Phase == ReactivePhase.CounterWindow && Ready("ReactiveCounterConfirm")) Press("ReactiveCounterConfirm");
                yield return null;
            }
            Assert.That(completed, Is.EqualTo(20), "The complete live command plan must finish before timeout.");
            Assert.That(defenses, Is.GreaterThanOrEqualTo(4));
            Assert.That(encounters, Is.GreaterThanOrEqualTo(2));
        }

        private bool Ready(string name)
        {
            var button = Find(name)?.GetComponent<Button>();
            return button != null && button.gameObject.activeInHierarchy && button.IsInteractable();
        }
        private GameObject Find(string name)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) if (item.name == name) return item.gameObject;
            return null;
        }
        private void Press(string name)
        {
            Button button = Find(name)?.GetComponent<Button>();
            Assert.That(Ready(name), Is.True, name);
            Assert.That(ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler), Is.True);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = priorInput;
            InputSystem.settings.backgroundBehavior = priorBackground;
            if (profileRoot != null && Directory.Exists(profileRoot)) Directory.Delete(profileRoot, true);
        }
    }
}
