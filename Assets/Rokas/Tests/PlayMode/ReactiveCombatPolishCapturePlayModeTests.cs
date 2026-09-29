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
    // Captures the live encounter, including actual Core damage and device defense.
    // No pose, health, action clock or enemy position is authored by this fixture.
    public sealed class ReactiveCombatPolishCapturePlayModeTests
    {
        private static readonly string CaptureDirectory =
            Path.Combine(Path.GetTempPath(), "rokas-combat-polish-visuals");
        private GameObject root;
        private string profileDirectory;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode priorEditorBehavior;
        private InputSettings.BackgroundBehavior priorBackgroundBehavior;
        private readonly List<CaptureRecord> records = new List<CaptureRecord>();

        [UnityTest]
        public IEnumerator LiveContactDefenseReturnAndCorpseLifecycleAreRendered()
        {
            priorEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            priorBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            ReleaseKeys();
            profileDirectory = Path.Combine(Path.GetTempPath(), "rokas-polish-capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(CaptureDirectory);
            records.Clear();
            root = new GameObject("ReactivePolishLiveCaptureFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(profileDirectory);
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            yield return null;
            Press("EnterReactivePortal");
            yield return WaitFor(boot, () => boot.Session.ReactiveCombat != null &&
                boot.Session.ReactiveCombat.Phase == ReactivePhase.PlayerCommand, 8f);
            ReactiveCombatSession combat = boot.Session.ReactiveCombat;
            ReactiveCombatActorVisual hunter = FindHunter();
            Assert.That(hunter.WeaponAttachment.CurrentWeapon, Is.Not.Null);
            Assert.That(combat.SelectedTargetId, Is.EqualTo("E1"));
            Vector3 hunterHome = hunter.transform.localPosition;
            ReactiveCombatActorVisual dyingTarget = FindEnemyAt(new Vector3(4.55f, -.38f, 1.5f));
            Assert.That(dyingTarget, Is.Not.Null);
            Vector3 corpseOrigin = dyingTarget.transform.localPosition;
            Vector3 corpseScale = dyingTarget.transform.localScale;

            Press("ReactiveBasic");
            yield return WaitFor(boot, () => combat.GetActorState("E1").Hp == 40, 5f);
            Assert.That(hunter.HitStopRemaining, Is.GreaterThan(0f),
                "Normal contact capture must occur at the authoritative resolved impact.");
            Capture(boot, "normal-contact", "E1");
            yield return WaitFor(boot, () => hunter.CurrentPose == "ReturnHome", 4f);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(hunter.CurrentPose, Is.EqualTo("ReturnHome"));
            Capture(boot, "normal-return", "E1");

            yield return WaitFor(boot, () => combat.Phase == ReactivePhase.EnemyExecution &&
                combat.ActiveActorId == "E1", 5f);
            string firstEnemyAction = combat.CurrentActionId;
            long firstStart = combat.CurrentActionStartUs;
            yield return WaitFor(boot, () => combat.CurrentCombatUs - firstStart >= 550000, 3f);
            Capture(boot, "enemy-reaction-cursor", "E1");
            yield return WaitFor(boot, () => combat.CurrentCombatUs - firstStart >= 820000, 3f);
            Assert.That(combat.CurrentCombatUs - firstStart, Is.LessThan(980000));
            PressKey(Key.Q);
            yield return null;
            ReleaseKeys();
            yield return WaitFor(boot, () => hunter.HitStopRemaining > 0f &&
                Feedback().Contains("УКЛОНЕНИЕ"), 2f);
            Assert.That(combat.HunterHp, Is.EqualTo(100));
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(Feedback(), Does.Contain("УКЛОНЕНИЕ"));
            Capture(boot, "dodge-result", "E1");

            yield return WaitFor(boot, () => combat.Phase == ReactivePhase.EnemyExecution &&
                combat.CurrentActionId != firstEnemyAction, 7f);
            long blockStart = combat.CurrentActionStartUs;
            string blockingEnemy = combat.ActiveActorId;
            long firstImpact = combat.CurrentAttack.Hits[0].ImpactUs;
            yield return WaitFor(boot, () => combat.CurrentCombatUs - blockStart >= firstImpact - 85000, 3f);
            Assert.That(combat.CurrentCombatUs - blockStart, Is.LessThan(firstImpact - 40000),
                "This capture intentionally uses ordinary Block, outside the inner Perfect window.");
            PressKey(Key.E);
            yield return null;
            ReleaseKeys();
            yield return WaitFor(boot, () => hunter.HitStopRemaining > 0f && Feedback() == "БЛОК", 2f);
            Assert.That(combat.HunterHp, Is.EqualTo(100));
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(Feedback(), Is.EqualTo("БЛОК"));
            Capture(boot, "block-result", blockingEnemy);

            yield return WaitFor(boot, () => combat.Phase == ReactivePhase.PlayerCommand &&
                Find("ReactiveHeavy").GetComponent<Button>().IsInteractable(), 12f);
            Assert.That(combat.HunterAp, Is.GreaterThanOrEqualTo(5));
            Press("ReactiveTarget2");
            Assert.That(combat.SelectedTargetId, Is.EqualTo("E2"));
            Assert.That((dyingTarget.transform.localPosition - corpseOrigin).sqrMagnitude,
                Is.GreaterThan(.001f), "Heavy is deliberately submitted while E2 is still returning.");
            Press("ReactiveHeavy");
            yield return WaitFor(boot, () => combat.GetActorState("E2").Hp == 0, 6f);
            float deathStarted = Time.realtimeSinceStartup;
            Assert.That(hunter.HitStopRemaining, Is.GreaterThan(0f),
                "Heavy contact capture must occur at the authoritative resolved impact.");
            Assert.That(dyingTarget.IsDead, Is.True);
            Vector3 expectedContactPosition = new Vector3(corpseOrigin.x - 1.35f,
                hunterHome.y, corpseOrigin.z - .45f);
            Assert.That((hunter.transform.localPosition - expectedContactPosition).sqrMagnitude,
                Is.LessThan(.0001f),
                "Keiko must strike at E2's durable home, not chase its moving return position.");
            Capture(boot, "heavy-contact", "E2");
            yield return WaitFor(boot, () => hunter.CurrentPose == "ReturnHome", 4f);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(hunter.CurrentPose, Is.EqualTo("ReturnHome"));
            Capture(boot, "heavy-return", "E2");
            yield return WaitFor(boot, () => Time.realtimeSinceStartup - deathStarted >= 1.55f, 4f);
            Assert.That(dyingTarget.transform.localPosition, Is.EqualTo(corpseOrigin));
            Capture(boot, "corpse-fallen", "E2");
            yield return WaitFor(boot, () => Time.realtimeSinceStartup - deathStarted >= 2.55f, 3f);
            Assert.That(dyingTarget.transform.localPosition, Is.EqualTo(corpseOrigin));
            Assert.That(dyingTarget.transform.localScale, Is.EqualTo(corpseScale));
            Capture(boot, "corpse-hold", "E2");
            yield return WaitFor(boot, () => dyingTarget.transform.localPosition.y < corpseOrigin.y - .25f, 3f);
            Assert.That(dyingTarget.transform.localScale, Is.EqualTo(corpseScale));
            Capture(boot, "corpse-sink", "E2");
            yield return WaitFor(boot, () => dyingTarget == null, 3f);
            File.WriteAllText(Path.Combine(CaptureDirectory, "capture-manifest.json"),
                JsonUtility.ToJson(new CaptureManifest { captures = records.ToArray() }, true));
            Assert.That(records.Count, Is.EqualTo(10));
        }

        private void Capture(RokasBootstrap boot, string name, string committedTarget)
        {
            const int width = 1920, height = 1080;
            Canvas canvas = root.GetComponentInChildren<Canvas>();
            RectTransform stage = Find("AuthoredStage").GetComponent<RectTransform>();
            var actorCamera = GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
            actorCamera.Render();
            var cameraObject = new GameObject("ReactivePolishCaptureCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var render = new RenderTexture(width, height, 24);
            render.Create();
            camera.targetTexture = render;
            RenderTexture priorRender = RenderTexture.active;
            RenderMode priorMode = canvas.renderMode;
            Camera priorCamera = canvas.worldCamera;
            float priorDistance = canvas.planeDistance;
            Vector3 priorScale = stage.localScale;
            string path = Path.Combine(CaptureDirectory, name + ".png");
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                stage.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = render;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                ReactiveCombatSession combat = boot.Session.ReactiveCombat;
                records.Add(new CaptureRecord { image = path, phase = combat.Phase.ToString(),
                    combatUs = combat.CurrentCombatUs, actionId = combat.CurrentActionId,
                    committedTarget = committedTarget, selectedTarget = combat.SelectedTargetId,
                    targetHp = combat.GetActorState(committedTarget).Hp, hunterHp = combat.HunterHp,
                    hunterPose = FindHunter().CurrentPose, hunterPosition = FindHunter().transform.localPosition,
                    feedback = Feedback() });
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(10000));
                TestContext.WriteLine(path);
            }
            finally
            {
                canvas.renderMode = priorMode;
                canvas.worldCamera = priorCamera;
                canvas.planeDistance = priorDistance;
                stage.localScale = priorScale;
                RenderTexture.active = priorRender;
                camera.targetTexture = null;
                render.Release();
                UnityEngine.Object.DestroyImmediate(render);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private string Feedback() => Find("ReactiveHitFeedback").GetComponent<Text>().text;

        private void Press(string name)
        {
            Button button = Find(name).GetComponent<Button>();
            Assert.That(button.gameObject.activeInHierarchy && button.IsInteractable(), Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private void PressKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key), InputState.currentTime);
            InputSystem.Update();
        }

        private void ReleaseKeys()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
            InputSystem.Update();
        }

        private static IEnumerator WaitFor(RokasBootstrap boot, Func<bool> ready, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while ((!ready() || boot.View.Paused) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(boot.View.Paused, Is.False);
            Assert.That(ready(), Is.True, "Timed out waiting for a live visual capture state.");
        }

        private GameObject Find(string name)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            throw new InvalidOperationException("Missing live capture UI: " + name);
        }

        private static ReactiveCombatActorVisual FindHunter()
        {
            foreach (var actor in GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<ReactiveCombatActorVisual>())
                if (actor.name == "CombatActor_Keiko") return actor;
            return null;
        }

        private static ReactiveCombatActorVisual FindEnemyAt(Vector3 home)
        {
            foreach (var actor in GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<ReactiveCombatActorVisual>())
                if (actor.name == "CombatActor_Yokai" && (actor.transform.localPosition - home).sqrMagnitude < .001f)
                    return actor;
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = priorEditorBehavior;
            InputSystem.settings.backgroundBehavior = priorBackgroundBehavior;
            if (!string.IsNullOrEmpty(profileDirectory) && Directory.Exists(profileDirectory))
                Directory.Delete(profileDirectory, true);
        }

        [Serializable]
        private sealed class CaptureManifest { public CaptureRecord[] captures; }
        [Serializable]
        private sealed class CaptureRecord
        {
            public string image, phase, actionId, committedTarget, selectedTarget, hunterPose, feedback;
            public long combatUs;
            public int hunterHp, targetHp;
            public Vector3 hunterPosition;
        }
    }
}
