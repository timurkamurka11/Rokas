using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class Combat3Phase0PlayModeTests
    {
        private GameObject root;
        private RokasBootstrap boot;
        private string directory;
        private bool pointerHeld;

        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-combat3-" + Guid.NewGuid().ToString("N"));
            pointerHeld = false;
            root = new GameObject("Combat3Fixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            boot.Session.AcceptContract();
            boot.Session.LeaveHome();
            yield return null;
            boot.enabled = false;
        }

        [UnityTest] public IEnumerator ReviewEntryIsSeparateAndBuildsFiveProxyLanes()
        {
            Assert.That(Find("EnterPortal"), Is.Not.Null);
            Assert.That(Find("EnterCombat3Review"), Is.Not.Null, "Review entry must be opt-in beside the existing portal.");
            EnterReview();
            yield return null;
            Assert.That(Find("EnemyAttack"), Is.Null, "Combat 2 charge surface must not own Combat 3 input.");
            Assert.That(Find("Combat3ArenaSurface"), Is.Not.Null);
            for (int i = 0; i < 5; i++) Assert.That(Find("Combat3Lane" + i), Is.Not.Null);
            Assert.That(Find("Combat3Player").GetComponent<MeshRenderer>(), Is.Not.Null);
            Assert.That(Get<int>(Encounter, "Lane"), Is.EqualTo(2));
            Capture("0a-start");
        }

        [UnityTest] public IEnumerator HeavyCanBeAvoidedThenFreshPointerCountersExactlyOnce()
        {
            EnterReview();
            yield return null;
            float enemyHp = boot.Session.State.enemyHp;
            float playerHp = boot.Session.State.playerHp;
            Capture("0a-telegraph");
            InputSample(true, false, false);
            Advance(.13f);
            InputSample(false, false, false);
            Assert.That(Get<int>(Encounter, "Lane"), Is.EqualTo(1));
            Capture("0a-moved");
            Until("Recovery");
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(playerHp));
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(enemyHp), "Waiting and lane movement deal no automatic damage.");
            Capture("0a-resolved");
            Until("CounterWindow");
            Assert.That(Find("Combat3CounterCue").GetComponent<Text>().text, Does.Contain("ЛКМ"));
            Capture("0a-window");
            var surface = Find("Combat3ArenaSurface");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(surface, pointer, ExecuteEvents.pointerDownHandler);
            Advance(1f / 60f);
            float after = boot.Session.State.enemyHp;
            Assert.That(after, Is.EqualTo(enemyHp - boot.Session.Contract.clickDamage * 1.8f).Within(.001f));
            ExecuteEvents.Execute(surface, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(surface, pointer, ExecuteEvents.pointerClickHandler);
            Advance(1f / 60f);
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(after));
            Assert.That(Find("Combat3CounterCue").GetComponent<Text>().text, Does.Not.Contain("ЛКМ"),
                "A consumed counter must stop inviting another press in the same window.");
            Capture("0a-counter");
            ExecuteEvents.Execute(surface, pointer, ExecuteEvents.pointerUpHandler);
        }

        [UnityTest] public IEnumerator StayingTakesDamageButStillGetsCounterAndEarlyHoldDoesNotBuffer()
        {
            EnterReview();
            yield return null;
            float playerHp = boot.Session.State.playerHp;
            float enemyHp = boot.Session.State.enemyHp;
            InputSample(false, false, true);
            Until("CounterWindow");
            Assert.That(boot.Session.State.playerHp, Is.LessThan(playerHp));
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(enemyHp));
            Capture("0a-hit-window");
            InputSample(false, false, true);
            Advance(.1f);
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(enemyHp));
            InputSample(false, false, false);
            InputSample(false, false, true);
            Advance(1f / 60f);
            Assert.That(boot.Session.State.enemyHp, Is.LessThan(enemyHp));
            InputSample(false, false, false);
        }

        [UnityTest] public IEnumerator ApplicationPauseCannotBeUndoneByTheNextUpdate()
        {
            EnterReview();
            boot.SendMessage("OnApplicationFocus", true);
            boot.SendMessage("OnApplicationPause", true);
            float before = boot.Session.State.combatTime;
            boot.SendMessage("Update");
            Advance(.9f);
            Assert.That(boot.Session.State.combatTime, Is.EqualTo(before), "Application pause must remain authoritative across Update.");
            boot.SendMessage("OnApplicationPause", false);
            Advance(.7f);
            Assert.That(boot.Session.State.combatTime, Is.GreaterThan(before));
            yield return null;
        }

        [UnityTest] public IEnumerator SettingsPauseCancelsQueuedCounterAndHeldResumeNeedsRelease()
        {
            EnterReview();
            Until("CounterWindow");
            float enemyHp = boot.Session.State.enemyHp;
            InputSample(false, false, true);
            boot.View.Escape();
            float before = boot.Session.State.combatTime;
            Advance(.7f);
            Assert.That(boot.Session.State.combatTime, Is.EqualTo(before));
            boot.View.Escape();
            InputSample(false, false, true);
            Advance(.65f);
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(enemyHp));
            InputSample(false, false, false);
            InputSample(false, false, true);
            Advance(1f / 60f);
            Assert.That(boot.Session.State.enemyHp, Is.LessThan(enemyHp));
            yield return null;
        }

        [UnityTest] public IEnumerator LowWaveHitsAcrossLanesDespiteAnOrdinaryStep()
        {
            EnterPractice("LowWave");
            yield return null;
            Assert.That(Find("Combat3AttackCue").GetComponent<Text>().text, Does.Contain("ВОЛНА"));
            Capture("0b-wave-telegraph");
            InputSample(true, false, false);
            Advance(.13f);
            InputSample(false, false, false);
            Until("CounterWindow");
            Assert.That(Get<int>(Encounter, "Lane"), Is.EqualTo(1));
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(92));
            Capture("0b-wave-hit");
        }

        [UnityTest] public IEnumerator EmptyDodgeGrantsNothingButActualPerfectPreventionRewardsOnce()
        {
            EnterPractice("LowWave");
            yield return null;
            var surface = Find("Combat3ArenaSurface");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            ExecuteEvents.Execute(surface, pointer, ExecuteEvents.pointerDownHandler);
            Advance(1f / 60f);
            Assert.That(Get<float>(Encounter, "DodgeRemaining"), Is.GreaterThan(0), "Real RMB on arena starts dodge.");
            Advance(.2f);
            Assert.That(boot.Session.Combat.Resonance, Is.EqualTo(0));
            Assert.That(boot.Session.Combat.Seal, Is.EqualTo(100));
            Capture("0b-empty-dodge");
            Until("Active");
            ExecuteEvents.Execute(surface, pointer, ExecuteEvents.pointerDownHandler);
            Advance(1f / 60f);
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(100));
            Assert.That(boot.Session.Combat.Resonance, Is.EqualTo(8));
            Assert.That(boot.Session.Combat.Seal, Is.EqualTo(92));
            Assert.That(Find("Combat3DefenseCue").GetComponent<Text>().text, Does.Contain("ИДЕАЛЬНО"));
            Capture("0b-perfect-dodge");
            Until("CounterWindow");
            Assert.That(boot.Session.Combat.Resonance, Is.EqualTo(8), "Same wave must not reward again after the initial prevented contact.");
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(100));
        }

        [UnityTest] public IEnumerator NormalTimedDodgeAvoidsWaveWithoutPerfectAndDirectionalDodgeMovesOneLane()
        {
            EnterPractice("LowWave");
            yield return null;
            Advance(.8f);
            InputSample(true, false, false);
            boot.View.HandleCombatInput(true, false, false);
            Advance(.13f);
            InputSample(false, false, false);
            Assert.That(Get<int>(Encounter, "Lane"), Is.EqualTo(1));
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(100));
            Assert.That(boot.Session.Combat.Resonance, Is.EqualTo(0));
            Capture("0b-normal-dodge");
            Until("CounterWindow");
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(100));
        }

        [UnityTest] public IEnumerator PortalPracticeButtonEntersLowWaveThroughExistingTravel()
        {
            var button = Find("Combat3PracticeLowWave");
            Assert.That(button, Is.Not.Null, "Wave practice must be reachable from the review entry.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button, pointer, ExecuteEvents.pointerClickHandler);
            float deadline = Time.realtimeSinceStartup + 2;
            while (boot.Session.State.phase != RunPhase.Combat && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
            Assert.That(Get<string>(Encounter, "AttackKindName"), Is.EqualTo("LowWave"));
            Assert.That(Find("Combat3ArenaSurface"), Is.Not.Null);
        }

        [UnityTest] public IEnumerator RawMouseHoldWithoutArenaPressCannotCounter()
        {
            EnterReview();
            Until("CounterWindow");
            float enemyHp = boot.Session.State.enemyHp;
            // A physical button may be held over Settings; only the arena captures counter intent.
            Call(boot.View, "HandleCombat3Input", false, false, true);
            Advance(1f / 60f);
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(enemyHp));
            Call(boot.View, "HandleCombat3Input", false, false, false);
            InputSample(false, false, true);
            Advance(1f / 60f);
            Assert.That(boot.Session.State.enemyHp, Is.LessThan(enemyHp));
            yield return null;
        }

        [UnityTest] public IEnumerator PointerBeforeDirectionSampleUsesCurrentFrameDodgeDirection()
        {
            EnterPractice("LowWave");
            yield return null;
            InputSample(true, false, false);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            ExecuteEvents.Execute(Find("Combat3ArenaSurface"), pointer, ExecuteEvents.pointerDownHandler);
            InputSample(false, false, false);
            boot.View.HandleCombatInput(true, false, false);
            Advance(.2f);
            Assert.That(Get<int>(Encounter, "Lane"), Is.EqualTo(2), "Releasing A with RMB must dodge stationary even if EventSystem ran first.");
            Advance(.4f);
            yield return null;
            InputSample(true, false, false);
            ExecuteEvents.Execute(Find("Combat3ArenaSurface"), pointer, ExecuteEvents.pointerDownHandler);
            InputSample(false, true, false);
            boot.View.HandleCombatInput(true, false, false);
            Advance(.13f);
            InputSample(false, false, false);
            Assert.That(Get<int>(Encounter, "Lane"), Is.EqualTo(3), "Reversing to D with RMB must move exactly one lane right.");
            Advance(.13f);
            Assert.That(Get<int>(Encounter, "Lane"), Is.EqualTo(3), "Duplicate pointer/raw delivery must not queue another ordinary step.");
        }

        [UnityTest] public IEnumerator WaveWarningStaysAtContactLineDuringActivePhase()
        {
            EnterPractice("LowWave");
            Until("Active");
            Assert.That(Find("Combat3HeavyTelegraph").transform.localPosition.z,
                Is.EqualTo(Find("Combat3HeavyStrike").transform.localPosition.z).Within(.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator PointerDuplicateCannotReplayRejectedRawDodgeAfterCooldownExpires()
        {
            EnterPractice("LowWave");
            Assert.That(boot.Session.Dodge(), Is.True);
            Advance(32f / 60f);
            boot.View.HandleCombatInput(true, false, false);
            boot.View.FlushCombat3Input();
            Advance(1f / 60f);
            Assert.That(Get<float>(Encounter, "DodgeRemaining"), Is.EqualTo(0));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            ExecuteEvents.Execute(Find("Combat3ArenaSurface"), pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            Advance(1f / 60f);
            Assert.That(Get<float>(Encounter, "DodgeRemaining"), Is.EqualTo(0), "Same physical down cannot retry after the cooldown boundary.");
        }

        [UnityTest] public IEnumerator ProjectileCanBeAvoidedByAnOrdinaryLaneStep()
        {
            EnterPractice("Projectile");
            yield return null;
            Assert.That(Find("Combat3AttackCue").GetComponent<Text>().text, Does.Contain("СНАРЯД"));
            Capture("0c-projectile-warning");
            InputSample(true, false, false);
            Advance(.13f);
            InputSample(false, false, false);
            Until("CounterWindow");
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(100));
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(180));
            Capture("0c-projectile-avoided");
        }

        [UnityTest] public IEnumerator SpaceDeflectsActualProjectileWithoutDirectEnemyDamage()
        {
            EnterPractice("Projectile");
            yield return null;
            boot.View.HandleCombatInput(false, true, false);
            Advance(.2f);
            Assert.That(boot.Session.Combat.Seal, Is.EqualTo(100), "Empty deflect grants nothing.");
            Assert.That(boot.Session.Combat.Resonance, Is.EqualTo(0));
            Until("Active");
            boot.View.HandleCombatInput(false, true, false);
            Advance(1f / 60f);
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(100));
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(180), "Reflected visual is not an extra damage source.");
            Assert.That(boot.Session.Combat.Seal, Is.EqualTo(88));
            Assert.That(boot.Session.Combat.Resonance, Is.EqualTo(12));
            Assert.That(Find("Combat3DefenseCue").GetComponent<Text>().text, Does.Contain("ОТРАЖЕНО"));
            var attacks = (IList)Get<object>(Encounter, "Attacks");
            Assert.That(attacks.Count, Is.EqualTo(2));
            Assert.That(Get<string>(attacks[0], "StateName"), Is.EqualTo("Deflected"));
            Assert.That(Get<string>(attacks[1], "StateName"), Is.EqualTo("Telegraph"), "Sibling keeps travelling after primary reflection.");
            Assert.That(Find("Combat3Projectile1").activeSelf, Is.True);
            Assert.That(Find("Combat3ProjectileReturn").activeSelf, Is.True);
            Capture("0c-deflected");
            InputSample(true, false, false);
            Advance(.13f);
            InputSample(false, false, false);
            Until("CounterWindow");
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(100));
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(180));
            Assert.That(boot.Session.Combat.Resonance, Is.EqualTo(12));
        }

        [UnityTest] public IEnumerator SpaceCannotProtectAgainstHeavyStrike()
        {
            EnterPractice("Heavy");
            Until("Active");
            boot.View.HandleCombatInput(false, true, false);
            Advance(1f / 60f);
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(92));
            Assert.That(boot.Session.Combat.Resonance, Is.EqualTo(0));
            Assert.That(boot.Session.Combat.Seal, Is.EqualTo(100));
            yield return null;
        }

        [UnityTest] public IEnumerator PortalPracticeButtonEntersProjectileThroughExistingTravel()
        {
            var button = Find("Combat3PracticeProjectile");
            Assert.That(button, Is.Not.Null);
            ExecuteEvents.Execute(button, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            float deadline = Time.realtimeSinceStartup + 2;
            while (boot.Session.State.phase != RunPhase.Combat && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Get<string>(Encounter, "AttackKindName"), Is.EqualTo("Projectile"));
        }

        [UnityTest] public IEnumerator SpaceCommandStartsC3DefenseAndSettingsCancelsIt()
        {
            EnterPractice("Heavy");
            boot.View.HandleCombatInput(false, true, false);
            Assert.That(Get<float>(Encounter, "DeflectRemaining"), Is.GreaterThan(0), "Space route must reach C3 defense.");
            boot.View.Escape();
            Assert.That(Get<float>(Encounter, "DeflectRemaining"), Is.EqualTo(0));
            boot.View.HandleCombatInput(false, true, false);
            boot.View.Escape();
            Advance(.6f);
            Assert.That(Get<float>(Encounter, "DeflectRemaining"), Is.EqualTo(0), "Space from settings cannot replay after resume.");
            yield return null;
        }

        [UnityTest] public IEnumerator ReflectedProjectileLeavesSiblingVisibleAndDangerous()
        {
            EnterPractice("Projectile");
            Until("Active");
            boot.View.HandleCombatInput(false, true, false);
            Advance(1f / 60f);
            InputSample(false, true, false);
            Advance(8f / 60f);
            InputSample(false, false, false);
            Advance(15f / 60f);
            Assert.That(Find("Combat3Projectile1").activeSelf, Is.True);
            Assert.That(Get<string>(Encounter, "StageName"), Is.EqualTo("Active"));
            Capture("0c-sibling-contact");
            Advance(1f / 60f);
            Assert.That(boot.Session.State.playerHp, Is.EqualTo(92));
            Assert.That(boot.Session.Combat.Seal, Is.EqualTo(88));
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(180));
            yield return null;
        }

        [UnityTest] public IEnumerator FocusLossClearsDeflectReturnVisual()
        {
            EnterPractice("Projectile");
            Until("Active");
            boot.View.HandleCombatInput(false, true, false);
            Advance(1f / 60f);
            Assert.That(Find("Combat3ProjectileReturn").activeSelf, Is.True);
            boot.SendMessage("OnApplicationFocus", false);
            boot.View.Tick(0);
            Assert.That(Find("Combat3ProjectileReturn").activeSelf, Is.False);
            Assert.That(Get<float>(Encounter, "DeflectRemaining"), Is.EqualTo(0));
            boot.SendMessage("OnApplicationFocus", true);
            Advance(.6f);
            Assert.That(Find("Combat3ProjectileReturn").activeSelf, Is.False);
            yield return null;
        }

        private void EnterPractice(string family)
        {
            MethodInfo method = boot.Session.GetType().GetMethod("EnterCombat3Practice");
            Assert.That(method, Is.Not.Null, "The separate review path must offer focused attack practice.");
            object choice = Enum.Parse(method.GetParameters()[0].ParameterType, family);
            Assert.That(method.Invoke(boot.Session, new[] { choice }), Is.EqualTo(true));
            Call(boot.Session, "SetCombat3Focused", true);
            Call(boot.Session, "SetCombat3Paused", false);
            boot.View.Tick(0);
        }

        private object Encounter { get { return Get<object>(boot.Session.Combat, "Combat3"); } }
        private void EnterReview()
        {
            Assert.That(Call(boot.Session, "EnterCombat3Review"), Is.EqualTo(true));
            Call(boot.Session, "SetCombat3Focused", true);
            Call(boot.Session, "SetCombat3Paused", false);
            boot.View.Tick(0);
        }
        private void InputSample(bool left, bool right, bool attack)
        {
            if (attack != pointerHeld)
            {
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                if (attack) ExecuteEvents.Execute(Find("Combat3ArenaSurface"), pointer, ExecuteEvents.pointerDownHandler);
                else ExecuteEvents.Execute(Find("Combat3ArenaSurface"), pointer, ExecuteEvents.pointerUpHandler);
                pointerHeld = attack;
            }
            Call(boot.View, "HandleCombat3Input", left, right, attack);
            boot.View.Tick(0);
        }
        private void Advance(float seconds)
        {
            boot.View.FlushCombat3Input();
            for (int i = 0; i < Mathf.CeilToInt(seconds * 60); i++) boot.Session.Tick(1f / 60f);
            boot.View.Tick(0);
        }
        private void Until(string stage)
        {
            for (int i = 0; i < 900 && Get<string>(Encounter, "StageName") != stage; i++) Advance(1f / 60f);
            Assert.That(Get<string>(Encounter, "StageName"), Is.EqualTo(stage), "Expected reachable combat cycle stage.");
        }
        private static object Call(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(name);
            Assert.That(method, Is.Not.Null, "Missing approved Phase 0 API: " + name);
            return method.Invoke(target, args);
        }
        private static T Get<T>(object target, string name)
        {
            Assert.That(target, Is.Not.Null);
            PropertyInfo property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, "Missing combat observation: " + name);
            return (T)property.GetValue(target);
        }
        private GameObject Find(string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child.gameObject;
            return null;
        }
        private void Capture(string name)
        {
            string output = Environment.GetEnvironmentVariable("ROKAS_COMBAT3_CAPTURE_DIR");
            if (string.IsNullOrEmpty(output)) return;
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var canvas = Find("RokasCanvas").GetComponent<Canvas>();
            var stage = Find("AuthoredStage").GetComponent<RectTransform>();
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            Vector3 oldScale = stage.localScale;
            var cameraRoot = new GameObject("Combat3CaptureCamera");
            var camera = cameraRoot.AddComponent<Camera>();
            var target = new RenderTexture(1920, 1080, 24);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 540;
                camera.nearClipPlane = .01f; camera.farClipPlane = 100;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                stage.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; stage.localScale = oldScale;
                RenderTexture.active = previous; camera.targetTexture = null; target.Release();
                UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(cameraRoot);
                Canvas.ForceUpdateCanvases();
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
