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

        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-combat3-" + Guid.NewGuid().ToString("N"));
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
            Call(boot.View, "HandleCombat3Input", left, right, attack);
            boot.View.Tick(0);
        }
        private void Advance(float seconds)
        {
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
