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
    public sealed class ReactiveArenaVisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator ArenaCanBeCapturedAtAuthoredAndSmallScreenSizes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-reactive-visual-" + Guid.NewGuid().ToString("N"));
            var root = new GameObject("ReactiveArenaVisualFixture");
            try
            {
                var boot = root.AddComponent<RokasBootstrap>();
                boot.Initialize(directory);
                boot.SendMessage("OnApplicationFocus", true);
                yield return null;
                Assert.That(boot.Session.AcceptContract(), Is.True);
                Assert.That(boot.Session.LeaveHome(), Is.True);
                yield return null;
                Button button = Find(root, "EnterReactivePortal").GetComponent<Button>();
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
                float introDeadline = Time.realtimeSinceStartup + 18f;
                while ((boot.Session.State.phase != RunPhase.Combat ||
                    !boot.View.ReactivePresentationReady || boot.ReactivePresentationHeld) &&
                    Time.realtimeSinceStartup < introDeadline) yield return null;
                Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
                Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(Rokas.Core.ReactiveTurns.ReactivePhase.PlayerCommand));
                Assert.That(Find(root, "MessagesNotification")?.activeInHierarchy, Is.Not.True,
                    "Battle HUD must remain readable when an unread message arrives.");
                Assert.That(Find(root, "ReactiveBasic").activeInHierarchy, Is.True);
                Assert.That(Find(root, "ReactiveHeavy").activeInHierarchy, Is.True);
                foreach (string hidden in new[] { "ReactiveSealStrike", "ReactiveDefend", "ReactiveSweep", "ReactiveAnchor" })
                    Assert.That(Find(root, hidden).activeInHierarchy, Is.False, hidden);
                Assert.That(Find(root, "ReactiveContactTrack").activeInHierarchy, Is.False,
                    "No incoming attack means no reaction bar.");

                Capture(root, 1920, 1080);
                Capture(root, 1280, 720);
                GameObject world = GameObject.Find("ReactiveCombatWorld");
                Assert.That(world, Is.Not.Null);
                Transform hunterActor = Find(world, "CombatActor_Keiko")?.transform;
                Assert.That(hunterActor, Is.Not.Null);
                Vector3 hunterHome = hunterActor.localPosition;
                button = Find(root, "ReactiveBasic").GetComponent<Button>();
                int clicksBeforeAttack = boot.View.GenericClickCount;
                Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
                Assert.That(boot.View.GenericClickCount, Is.EqualTo(clicksBeforeAttack),
                    "Attack selection must not add a generic UI click to its presentation layers.");
                float travelDeadline = Time.realtimeSinceStartup + 3f;
                while (hunterActor.localPosition.x <= hunterHome.x && Time.realtimeSinceStartup < travelDeadline) yield return null;
                Assert.That(hunterActor.localPosition.x, Is.GreaterThan(hunterHome.x));
                Capture(root, 1920, 1080, "-approach");
                float deadline = Time.realtimeSinceStartup + 6f;
                while (boot.Session.ReactiveCombat.Phase != ReactivePhase.EnemyExecution &&
                       Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.EnemyExecution));
                Assert.That(boot.ReactivePresentationHeld, Is.True);
                Capture(root, 1920, 1080, "-return");
                deadline = Time.realtimeSinceStartup + 5f;
                while (boot.ReactivePresentationHeld && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(boot.ReactivePresentationHeld, Is.False);
                AudioClip keikoImpact = Resources.Load<AudioClip>(
                    "Combat/ReactiveTurns/Audio/PolishII/NormalFleshContact");
                AudioClip monsterAttack = Resources.Load<AudioClip>(
                    "Combat/ReactiveTurns/Audio/Monsters/Monster attack sound");
                Assert.That(keikoImpact, Is.Not.Null);
                Assert.That(monsterAttack, Is.Not.Null);
                int impactCues = 0;
                int monsterCues = 0;
                foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                {
                    if (source.clip == keikoImpact)
                    {
                        impactCues++;
                        float expected = boot.Session.State.settings.masterVolume *
                            boot.Session.State.settings.sfxVolume * .56f;
                        Assert.That(source.volume, Is.EqualTo(expected).Within(.001f));
                    }
                    if (source.clip == monsterAttack) monsterCues++;
                    Assert.That(source.clip, Is.Not.EqualTo(Resources.Load<RokasAssets>("RokasAssets").hit),
                        "The imported Keiko impact must not be doubled by the generic hit cue.");
                }
                Assert.That(impactCues, Is.EqualTo(1));
                Assert.That(monsterCues, Is.EqualTo(1));
                deadline = Time.realtimeSinceStartup + 4f;
                while (!boot.View.HunterAtHome && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(boot.View.HunterAtHome, Is.True);
                Assert.That(hunterActor.localPosition, Is.EqualTo(hunterHome),
                    "The return animation must restore Keiko's exact home before enemy contact.");
                Capture(root, 1920, 1080, "-windup");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [UnityTest]
        public IEnumerator ArenaUsesActionSpecificImportedClips()
        {
            var root = new GameObject("ReactiveActorClipFixture", typeof(RectTransform));
            ReactiveCombatArena arena = null;
            try
            {
                arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
                arena.SetEnemies(new[] { "E1" }, null);
                GameObject world = GameObject.Find("ReactiveCombatWorld");
                Assert.That(world, Is.Not.Null);
                ReactiveCombatActorVisual hunterActor = null;
                ReactiveCombatActorVisual enemyActor = null;
                foreach (ReactiveCombatActorVisual visual in world.GetComponentsInChildren<ReactiveCombatActorVisual>())
                {
                    if (visual.name == "CombatActor_Keiko") hunterActor = visual;
                    if (visual.name == "CombatActor_Yokai") enemyActor = visual;
                }
                Assert.That(hunterActor, Is.Not.Null);
                Assert.That(enemyActor, Is.Not.Null);
                Animation hunterAnimation = hunterActor.ModelRoot.GetComponent<Animation>();
                Animation enemyAnimation = enemyActor.ModelRoot.GetComponent<Animation>();
                Assert.That(hunterAnimation.GetClip("Heavy"), Is.Not.Null);
                Assert.That(enemyAnimation.GetClip("Heavy"), Is.Not.Null);
                Assert.That(enemyAnimation.GetClip("Stagger"), Is.Not.Null);

                arena.Present(new CombatEvent(CombatEventKind.CommandCommitted,
                    actionId: "basic-action", detail: "Basic"));
                Assert.That(hunterAnimation.IsPlaying("Attack"), Is.True);
                arena.Present(new CombatEvent(CombatEventKind.CommandCommitted,
                    actionId: "defend-action", detail: "Defend"));
                arena.Tick(.2f);
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(hunterAnimation.IsPlaying("Attack"), Is.False,
                    "Defend must stop the ordinary attack pose.");

                arena.Present(new CombatEvent(CombatEventKind.CommandCommitted,
                    actionId: "heavy-hunter", detail: "heavy"));
                Assert.That(hunterAnimation.IsPlaying("Heavy"), Is.True);
                arena.Present(new CombatEvent(CombatEventKind.AttackStarted,
                    actorId: "E1", actionId: "heavy-enemy", detail: "heavy"));
                Assert.That(enemyAnimation.IsPlaying("Heavy"), Is.False,
                    "The enemy approaches before its strike pose starts.");
                for (int tick = 0; !arena.EnemyApproachComplete("E1") && tick < 100; tick++) arena.Tick(.02f);
                yield return null;
                Assert.That(enemyAnimation.IsPlaying("Heavy"), Is.True);
                arena.Present(new CombatEvent(CombatEventKind.HitResolved,
                    targetId: "E1", actionId: "heavy-hunter", amount: 8));
                Assert.That(enemyAnimation.IsPlaying("Stagger"), Is.True);
            }
            finally
            {
                arena?.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator ImportedRunClipsApproachAndReturnToTheSameHomeAcrossActions()
        {
            var root = new GameObject("ReactiveHunterMotionFixture", typeof(RectTransform));
            ReactiveCombatArena arena = null;
            try
            {
                arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
                arena.SetEnemies(new[] { "E1", "E2" }, null, new[] { "E1", "E2" });
                GameObject world = GameObject.Find("ReactiveCombatWorld");
                Assert.That(world, Is.Not.Null);
                ReactiveCombatActorVisual hunterActor = null;
                foreach (ReactiveCombatActorVisual visual in world.GetComponentsInChildren<ReactiveCombatActorVisual>())
                    if (visual.name == "CombatActor_Keiko") hunterActor = visual;
                Assert.That(hunterActor, Is.Not.Null);
                Animation animation = hunterActor.ModelRoot.GetComponent<Animation>();
                Assert.That(animation.GetClip("Approach"), Is.Not.Null);
                Assert.That(animation.GetClip("ReturnHome"), Is.Not.Null);
                for (int warmup = 0; !arena.PresentationReady && warmup < 100; warmup++) arena.Tick(.02f);
                Vector3 home = hunterActor.transform.localPosition;

                foreach (string enemyId in new[] { "E1", "E2", "E1" })
                {
                    Assert.That(arena.StartHunterApproach(enemyId), Is.True);
                    int approachTicks = 0;
                    while ((hunterActor.CurrentPose != "Approach" ||
                            hunterActor.transform.localPosition.x <= home.x + .01f) && approachTicks++ < 150)
                        arena.Tick(.02f);
                    yield return null;
                    Assert.That(hunterActor.transform.localPosition.x, Is.GreaterThan(home.x));
                    Assert.That(animation.IsPlaying("Approach"), Is.True);
                    float deadline = Time.realtimeSinceStartup + 5f;
                    while (!arena.HunterApproachComplete && Time.realtimeSinceStartup < deadline)
                    {
                        arena.Tick(.04f);
                        yield return null;
                    }
                    Assert.That(arena.HunterApproachComplete, Is.True);
                    arena.Present(new CombatEvent(CombatEventKind.CommandCommitted,
                        actorId: ReactiveDuelDefinitions.HunterId, actionId: enemyId,
                        detail: "Basic"));
                    yield return null;
                    Assert.That(animation.IsPlaying("Attack"), Is.True);
                    arena.Tick(.2f);
                    arena.Present(new CombatEvent(CombatEventKind.HitResolved,
                        actorId: ReactiveDuelDefinitions.HunterId, targetId: enemyId,
                        actionId: enemyId, amount: 8));
                    arena.Present(new CombatEvent(CombatEventKind.ActionSettled,
                        actorId: ReactiveDuelDefinitions.HunterId, actionId: enemyId));
                    deadline = Time.realtimeSinceStartup + 3f;
                    while (!animation.IsPlaying("ReturnHome") && !arena.HunterAtHome &&
                           Time.realtimeSinceStartup < deadline)
                    {
                        arena.Tick(.04f);
                        yield return null;
                    }
                    Assert.That(animation.IsPlaying("ReturnHome"), Is.True);
                    while (!arena.PresentationReady && Time.realtimeSinceStartup < deadline)
                    {
                        arena.Tick(.04f);
                        yield return null;
                    }
                    Assert.That(arena.HunterAtHome, Is.True);
                    Assert.That(hunterActor.transform.localPosition, Is.EqualTo(home));
                }
            }
            finally
            {
                arena?.Dispose();
                UnityEngine.Object.Destroy(root);
            }
        }

        private static void Capture(GameObject root, int width, int height, string suffix = "")
        {
            Canvas canvas = root.GetComponentInChildren<Canvas>();
            RectTransform stage = Find(root, "AuthoredStage").GetComponent<RectTransform>();
            Assert.That(canvas, Is.Not.Null);
            var cameraObject = new GameObject("ReactiveCaptureCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = ~0;
            var render = new RenderTexture(width, height, 24);
            render.Create();
            camera.targetTexture = render;
            RenderTexture prior = RenderTexture.active;
            RenderMode priorMode = canvas.renderMode;
            Camera priorCamera = canvas.worldCamera;
            Vector3 priorScale = stage.localScale;
            string path = Path.Combine(Path.GetTempPath(), "rokas-reactive-arena-" + width + "x" + height + suffix + ".png");
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                stage.localScale = Vector3.one * Mathf.Min(width / 1920f, height / 1080f);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = render;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(path, image.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(10000));
                TestContext.WriteLine(path);
            }
            finally
            {
                canvas.renderMode = priorMode;
                canvas.worldCamera = priorCamera;
                stage.localScale = priorScale;
                RenderTexture.active = prior;
                camera.targetTexture = null;
                render.Release();
                UnityEngine.Object.DestroyImmediate(render);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static GameObject Find(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }
    }
}
