using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class CombatThrowPrewarmPlayModeTests
    {
        [UnityTest]
        public IEnumerator FirstLiveThrowPreviewReusesInactiveProjectileAndNativeCommandBank()
        {
            var root = new GameObject("FirstThrowPrewarmFixture", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            try
            {
                var effectField = typeof(ReactiveCombatArena).GetField("throwEffect", BindingFlags.Instance | BindingFlags.NonPublic);
                var worldField = typeof(ReactiveCombatArena).GetField("world", BindingFlags.Instance | BindingFlags.NonPublic);
                var hunterField = typeof(ReactiveCombatArena).GetField("hunter", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(effectField, Is.Not.Null);
                var preparedEffect = effectField.GetValue(arena);
                Assert.That(preparedEffect, Is.Not.Null, "The first live Throw preview must not create its projectile, materials and particle systems.");
                var world = (GameObject)worldField.GetValue(arena);
                var hunter = (ReactiveCombatActorVisual)hunterField.GetValue(arena);
                var projectile = world.GetComponentsInChildren<Transform>(true).Single(t => t.name == "KeikoThrownDagger");
                int projectileId = projectile.GetInstanceID();
                Assert.That(projectile.gameObject.activeInHierarchy, Is.False);
                Assert.That(arena.ThrowReleaseCount, Is.Zero);
                Assert.That(arena.ThrowContactCount, Is.Zero);
                Assert.That(hunter.HeldDagger.gameObject.activeInHierarchy, Is.False);
                Animation animation = hunter.ModelRoot.GetComponent<Animation>();
                var preparedCopies = Copies(animation);
                Assert.That(preparedCopies.Count, Is.EqualTo(12), "Production Normal/Heavy/Throw each have two action segments plus anticipation and idle.");
                arena.SetEnemies(new[] { "E1" }, null);
                for (int i = 0; i < 1000 && !arena.PresentationReady; i++) arena.Tick(.02f);
                Assert.That(arena.PresentationReady, Is.True);
                Assert.That(arena.SelectHunterPreview("throw_blade"), Is.True);
                arena.Tick(.01f);
                Assert.That(effectField.GetValue(arena), Is.SameAs(preparedEffect));
                CollectionAssert.AreEquivalent(preparedCopies, Copies(animation), "First real preview cannot put AddClip back on the input frame.");
                Assert.That(projectile.GetInstanceID(), Is.EqualTo(projectileId));
                Assert.That(projectile.gameObject.activeInHierarchy, Is.False, "A preview does not release a flying blade.");
                Assert.That(hunter.HeldDagger.gameObject.activeInHierarchy, Is.True);
                Assert.That(arena.ThrowReleaseCount, Is.Zero);
                arena.CancelHunterPreview();
                for (int i = 0; i < 1000 && !arena.PresentationReady; i++) arena.Tick(.02f);
                Assert.That(arena.PresentationReady, Is.True);
                Assert.That(arena.SelectHunterPreview("heavy"), Is.True);
                arena.Tick(.01f);
                CollectionAssert.AreEquivalent(preparedCopies, Copies(animation));
                arena.CancelHunterPreview();
                for (int i = 0; i < 1000 && !arena.PresentationReady; i++) arena.Tick(.02f);
                Assert.That(arena.SelectHunterPreview("normal"), Is.True);
                arena.Tick(.01f);
                CollectionAssert.AreEquivalent(preparedCopies, Copies(animation));
                Assert.That(effectField.GetValue(arena), Is.SameAs(preparedEffect));
                Assert.That(world.GetComponentsInChildren<Transform>(true).Count(t => t.name == "KeikoThrownDagger"), Is.EqualTo(1));
            }
            finally { arena.Dispose(); Object.Destroy(root); }
            yield return null;
        }

        private static Dictionary<string, int> Copies(Animation animation)
        {
            var result = new Dictionary<string, int>();
            foreach (AnimationState state in animation)
                if (state.name.StartsWith("Licensed_", System.StringComparison.Ordinal))
                    result.Add(state.name, animation.GetClip(state.name).GetInstanceID());
            return result;
        }
    }
}
