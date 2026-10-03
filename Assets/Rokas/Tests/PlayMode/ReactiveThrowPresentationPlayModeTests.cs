using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class ReactiveThrowPresentationPlayModeTests
    {
        [Test]
        public void ThrowReadinessRequiresInitializedPropAndRegisteredClips()
        {
            var root = new GameObject("ThrowReadinessTest");
            var uninitialized = root.AddComponent<ReactiveCombatActorVisual>();
            ReactiveCombatActorVisual actor = null;
            try
            {
                Assert.That(uninitialized.ThrowReady, Is.False, "An uninitialized actor cannot expose Throw input.");
                actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
                Animation animation = actor.ModelRoot.GetComponent<Animation>();
                var definition = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary").keiko;
                foreach (string alias in new[] { "ThrowPreparation", "Throw" })
                {
                    AnimationClip expected = alias == "Throw" ? definition.throwAttack : definition.throwPreparation;
                    AnimationClip registered = animation.GetClip(alias);
                    TestContext.WriteLine(alias + " source=" + expected.GetInstanceID() + " runtime=" +
                        (registered == null ? "null" : registered.GetInstanceID() + " " + registered.name +
                        " legacy=" + registered.legacy + " length=" + registered.length) +
                        " sourceLegacy=" + expected.legacy + " sourceLength=" + expected.length);
                }
                Assert.That(actor.ThrowReady, Is.True);
                animation.RemoveClip("Throw");
                Assert.That(actor.ThrowReady, Is.False, "A missing runtime clip is not a working Throw asset.");
                animation.AddClip(definition.throwPreparation, "Throw");
                Assert.That(actor.ThrowReady, Is.False, "The held preview cannot stand in for the release animation.");
                AnimationClip clip = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary").keiko.throwAttack;
                animation.AddClip(clip, "Throw");
                Assert.That(actor.ThrowReady, Is.True);
                Object.DestroyImmediate(actor.HeldDagger.gameObject);
                Assert.That(actor.ThrowReady, Is.False, "The actual held prop is required, beyond library references.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReleaseAndContactAreDistinctAndIdempotent()
        {
            var root = new GameObject("ThrowProjectileTest");
            var prefab = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary").keiko.throwingDaggerPrefab;
            var effect = new ReactiveCombatThrowEffect(root.transform, 30, prefab);
            try
            {
                Assert.That(effect.Release("one", Vector3.zero, Vector3.right * 4f, Vector3.one, .36f), Is.True);
                Assert.That(effect.Release("one", Vector3.zero, Vector3.right * 4f, Vector3.one, .36f), Is.False);
                effect.Tick(.18f);
                Assert.That(GameObject.Find("KeikoThrownDagger").transform.position.x, Is.EqualTo(2f).Within(.01f));
                Assert.That(effect.InFlight, Is.True);
                Assert.That(effect.ResolveContact(), Is.True);
                Assert.That(effect.ResolveContact(), Is.False);
                effect.Tick(.2f);
                Assert.That(effect.ReleaseCount, Is.EqualTo(1));
                Assert.That(effect.ContactCount, Is.EqualTo(1));
                Assert.That(GameObject.Find("KeikoThrownDagger"), Is.Null);
                effect.Tick(.4f);
                Assert.That(effect.ParticleCount, Is.Zero, "The one-use release/flight wake must fully retire after contact.");
            }
            finally { effect.Dispose(); Object.DestroyImmediate(root); }
        }

        [Test]
        public void ThrowAudioUsesOneReleaseAndOneContactAcrossAliases()
        {
            var root = new GameObject("ThrowAudioTest");
            var audio = new RokasAudio(root, Resources.Load<RokasAssets>("RokasAssets"), new SettingsData());
            try
            {
                var cues = new ReactiveCombatAudio(audio);
                cues.BindActionId("core", "visual");
                cues.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P", actionId: "core", detail: "throw_blade"));
                Assert.That(cues.PlaybackCount, Is.Zero, "Commit is not the release event.");
                cues.PresentThrowRelease("visual"); cues.PresentThrowRelease("core");
                var contact = new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: "E1", actionId: "core", amount: 20);
                cues.Present(contact); cues.Present(contact);
                Assert.That(cues.Dispatches.Select(d => d.EventId).ToArray(), Is.EqualTo(new[] {
                    ReactiveCombatAudioEvent.ThrowRelease, ReactiveCombatAudioEvent.ThrowFleshContact }));
                Assert.That(cues.Dispatches.Select(d => d.ClipName).ToArray(), Is.EqualTo(new[] { "ThrowRelease", "ThrowContact" }));
            }
            finally { audio.Dispose(); Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator ThreeHeldPreviewClipsChangeTheActualRigPose()
        {
            var root = new GameObject("PreviewRigTest", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                arena.SetEnemies(new[] { "E1" }, null);
                for (int i = 0; i < 1000 && !arena.PresentationReady; i++) arena.Tick(.02f);
                var actor = GameObject.Find("CombatActor_Keiko").GetComponent<ReactiveCombatActorVisual>();
                Vector3 idleHand = actor.WeaponAttachment.Socket.parent.position;
                foreach (string id in new[] { "normal", "heavy", "throw_blade" })
                {
                    Assert.That(arena.SelectHunterPreview(id), Is.True);
                    for (int i = 0; i < 50; i++) arena.Tick(.02f);
                    Vector3 hand = actor.WeaponAttachment.Socket.parent.position;
                    TestContext.WriteLine(id + " time=" + actor.CurrentPoseSeconds + " hand=" + hand + " idle=" + idleHand);
                    Assert.That(Vector3.Distance(hand, idleHand), Is.GreaterThan(.1f), id + " must change real hand position, beyond idle breathing.");
                    Assert.That(arena.PresentationReady, Is.False, "Held preview is not turn-transition settlement.");
                    Assert.That(arena.CommandReady, Is.True);
                    Assert.That(arena.HunterPosition, Is.EqualTo(arena.HunterHome));
                }
            }
            finally { arena.Dispose(); Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CancelledThrowRetiresReleasedBladeWithoutContact()
        {
            var root = new GameObject("CancelledThrowTest", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                arena.SetEnemies(new[] { "E1" }, null);
                for (int i = 0; i < 1000 && !arena.PresentationReady; i++) arena.Tick(.02f);
                Assert.That(arena.StartHunterThrow("E1"), Is.True);
                arena.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P", actionId: "cancelled", detail: "throw_blade"));
                for (int i = 0; i < 24; i++) arena.Tick(.02f);
                Assert.That(arena.ThrowReleaseCount, Is.EqualTo(1));
                Assert.That(GameObject.Find("KeikoThrownDagger"), Is.Not.Null);
                arena.Present(new CombatEvent(CombatEventKind.CommandCancelled, actorId: "P", actionId: "cancelled",
                    detail: "AllTargetsUnavailableBeforeImpact"));
                for (int i = 0; i < 30; i++) arena.Tick(.02f);
                Assert.That(GameObject.Find("KeikoThrownDagger"), Is.Null);
                Assert.That(arena.ThrowContactCount, Is.Zero, "Cancellation is not a fake projectile contact.");
                Assert.That(arena.HunterPosition, Is.EqualTo(arena.HunterHome));
                Assert.That(arena.PresentationReady, Is.True);
            }
            finally { arena.Dispose(); Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThrowAimsAtLiveTorsoInsteadOfInflatedSkinBoundsOrReleaseHandHeight()
        {
            var root = new GameObject("ThrowTorsoAimTest", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                arena.SetEnemies(new[] { "E1" }, null);
                for (int i = 0; i < 1000 && !arena.PresentationReady; i++) arena.Tick(.02f);
                var enemy = GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<ReactiveCombatActorVisual>(true)
                    .Single(actor => actor.name == "CombatActor_Yokai");
                Transform chest = enemy.ModelRoot.GetComponentsInChildren<Transform>(true)
                    .Single(bone => bone.name.EndsWith("Spine2"));
                Transform head = enemy.ModelRoot.GetComponentsInChildren<Transform>(true)
                    .Single(bone => bone.name.EndsWith("Head"));
                // Imported animated bounds can be broad and offset. Changing only
                // these bounds must not change the anatomical projectile target.
                foreach (var skin in enemy.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Bounds bounds = skin.localBounds;
                    bounds.center += Vector3.up * bounds.size.y;
                    bounds.size += Vector3.up * bounds.size.y * 6f;
                    skin.localBounds = bounds;
                }
                Vector3 enemyHome = arena.EnemyHome("E1");
                Assert.That(arena.StartHunterThrow("E1"), Is.True);
                arena.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P", actionId: "torso", detail: "throw_blade"));
                for (int i = 0; i < 100 && arena.ThrowReleaseCount == 0; i++) arena.Tick(.01f);
                Assert.That(arena.ThrowReleaseCount, Is.EqualTo(1));
                Vector3 sampledChest = chest.position;
                float headHeight = head.position.y;
                for (int i = 0; i < 45; i++) arena.Tick(.01f);
                GameObject projectile = GameObject.Find("KeikoThrownDagger");
                Assert.That(projectile, Is.Not.Null);
                Vector3 endpoint = projectile.transform.position;
                TestContext.WriteLine("THROW torso=" + sampledChest + " endpoint=" + endpoint + " headY=" + headHeight);
                Assert.That(endpoint.x, Is.EqualTo(sampledChest.x).Within(.03f));
                Assert.That(endpoint.y, Is.EqualTo(sampledChest.y).Within(.03f), "Throw must hit the sampled torso, not the release hand or bounds centre.");
                Assert.That(endpoint.y, Is.LessThan(headHeight));
                Assert.That(arena.HunterPosition, Is.EqualTo(arena.HunterHome));
                Assert.That(arena.EnemyPosition("E1"), Is.EqualTo(enemyHome));
                var contact = new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: "E1", actionId: "torso", amount: 10);
                arena.Present(contact); arena.Present(contact);
                Assert.That(arena.ThrowContactCount, Is.EqualTo(1));
                Assert.That(projectile.transform.position, Is.EqualTo(endpoint));
            }
            finally { arena.Dispose(); Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ZeroDamageThrowStillResolvesAndRetiresItsVisualProjectile()
        {
            var root = new GameObject("ZeroThrowTest", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                arena.SetEnemies(new[] { "E1" }, null);
                for (int i = 0; i < 1000 && !arena.PresentationReady; i++) arena.Tick(.02f);
                Assert.That(arena.StartHunterThrow("E1"), Is.True);
                arena.Present(new CombatEvent(CombatEventKind.CommandCommitted, actorId: "P", actionId: "zero", detail: "throw_blade"));
                for (int i = 0; i < 50; i++) arena.Tick(.02f);
                Assert.That(arena.ThrowReleaseCount, Is.EqualTo(1));
                arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: "E1", actionId: "zero", amount: 0));
                arena.Present(new CombatEvent(CombatEventKind.HitResolved, actorId: "P", targetId: "E1", actionId: "zero", amount: 0));
                Assert.That(arena.ThrowContactCount, Is.EqualTo(1));
                for (int i = 0; i < 20; i++) arena.Tick(.02f);
                Assert.That(GameObject.Find("KeikoThrownDagger"), Is.Null);
            }
            finally { arena.Dispose(); Object.Destroy(root); }
            yield return null;
        }
    }
}
