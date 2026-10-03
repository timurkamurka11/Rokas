using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class ReactiveEightEnemyPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator PortalBuildsThreeAnimatedEnemiesAndPersistsTheChosenCommandTarget()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-reactive-eight-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("ReactiveEightEnemyFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;

            Assert.That(boot.Session.AcceptContract(), Is.True);
            Assert.That(boot.Session.LeaveHome(), Is.True);
            yield return null;
            Press("EnterReactivePortal");
            yield return WaitForCombat(boot);

            ReactiveCombatSession combat = boot.Session.ReactiveCombat;
            Assert.That(combat, Is.Not.Null);
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
            Assert.That(combat.WaveCount, Is.EqualTo(3));
            Assert.That(combat.CurrentWaveIndex, Is.Zero);
            Assert.That(combat.ActiveEnemyIds, Is.EqualTo(new[] { "E1", "E2", "E3" }));
            Assert.That(combat.GetActorState("E4").Hp, Is.EqualTo(60), "Reserve waves have not spawned.");
            Assert.That(Find("ReactiveTarget1").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveTarget2").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveTarget3").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveTarget4").activeInHierarchy, Is.False);
            Assert.That(Find("ReactiveWave").GetComponent<Text>().text, Does.Contain("1 / 3"));
            Assert.That(Find("ReactiveForecast").GetComponent<Text>().text, Does.Contain("КЕЙКО"));
            Assert.That(Find("ReactiveBasic"), Is.Not.Null);
            Assert.That(Find("ReactiveSealStrike"), Is.Not.Null);
            Assert.That(Find("ReactiveDefend"), Is.Not.Null);
            Assert.That(Find("ReactiveSweep"), Is.Not.Null);
            Assert.That(Find("ReactiveHeavy"), Is.Not.Null);
            Assert.That(Find("ReactiveAnchor"), Is.Not.Null);

            Texture2D background = Resources.Load<Texture2D>("CombatB/AbyssArenaBackground");
            Assert.That(background, Is.Not.Null);
            Assert.That(Find("WorldIllustration").GetComponent<RawImage>().texture, Is.SameAs(background));
            Assert.That(boot.View.ReactiveActorsReady, Is.True);
            Assert.That(boot.View.AnimatedReactiveEnemyCount, Is.EqualTo(3));
            var actorTexture = Find("ReactiveAnimatedWorld").GetComponent<RawImage>().texture as RenderTexture;
            Assert.That(actorTexture, Is.Not.Null);
            Assert.That(actorTexture.IsCreated(), Is.True);
            GameObject world = GameObject.Find("ReactiveCombatWorld");
            Assert.That(world, Is.Not.Null);
            Camera actorCamera = world.GetComponentInChildren<Camera>(true);
            Assert.That(actorCamera, Is.Not.Null);
            Assert.That(actorCamera.targetTexture, Is.SameAs(actorTexture));
            ReactiveCombatActorVisual[] actors = world.GetComponentsInChildren<ReactiveCombatActorVisual>(true);
            Assert.That(actors.Length, Is.EqualTo(4), "One Hunter and three enemies have live models.");
            foreach (ReactiveCombatActorVisual actor in actors)
            {
                Assert.That(actor.ModelRoot, Is.Not.Null);
                Assert.That(actor.ModelRoot.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
                Animation animation = actor.ModelRoot.GetComponent<Animation>();
                Assert.That(animation, Is.Not.Null);
                Assert.That(animation.GetClip("Idle"), Is.Not.Null);
            }

            Press("ReactiveTarget2");
            Assert.That(combat.SelectedTargetId, Is.EqualTo("E2"));
            Assert.That(boot.View.SelectedReactiveTargetId, Is.EqualTo("E2"));
            Assert.That(boot.Session.State.battleCheckpoint.selectedTargetId, Is.EqualTo("E2"));

            UnityEngine.Object.Destroy(root);
            yield return null;
            root = new GameObject("ReactiveEightEnemyReloadFixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;

            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
            combat = boot.Session.ReactiveCombat;
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
            Assert.That(combat.SelectedTargetId, Is.EqualTo("E2"));
            Assert.That(combat.ActiveEnemyIds, Is.EqualTo(new[] { "E1", "E2", "E3" }));
            Assert.That(boot.View.SelectedReactiveTargetId, Is.EqualTo("E2"));
            yield return WaitForCombat(boot);
            long previewRevision = combat.Revision;
            Press("ReactiveBasic");
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
            Assert.That(combat.Revision, Is.EqualTo(previewRevision));
            Press("ReactiveBasic");
            float confirmDeadline = Time.realtimeSinceStartup + 3f;
            while (combat.Phase != ReactivePhase.PlayerExecution && Time.realtimeSinceStartup < confirmDeadline) yield return null;
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerExecution));
            Assert.That(GameObject.Find("ReactiveActorCamera").GetComponent<Camera>().orthographicSize, Is.EqualTo(4.6f).Within(.001f));
            boot.SelectReactiveTarget("E3");
            Assert.That(combat.SelectedTargetId, Is.EqualTo("E2"),
                "The committed target remains frozen during the action.");

            float deadline = Time.realtimeSinceStartup + 4f;
            while (combat.GetActorState("E2").Hp == 60 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(combat.GetActorState("E2").Hp, Is.LessThan(60));
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(60));
            Assert.That(combat.GetActorState("E3").Hp, Is.EqualTo(60));
        }

        private static IEnumerator WaitForCombat(RokasBootstrap boot)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((boot.Session.State.phase != RunPhase.Combat || boot.View.Paused ||
                    boot.Session.ReactiveCombat == null ||
                    boot.Session.ReactiveCombat.Phase != ReactivePhase.PlayerCommand ||
                    boot.ReactivePresentationHeld || !boot.View.ReactivePresentationReady) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
            Assert.That(boot.View.Paused, Is.False);
            Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
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
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
