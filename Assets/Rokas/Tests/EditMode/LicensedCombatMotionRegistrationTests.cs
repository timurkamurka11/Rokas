using System.Collections.Generic;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Tests
{
    public sealed class LicensedCombatMotionRegistrationTests
    {
        private GameObject root;
        private Transform animated;
        private Animation animation;
        private readonly List<Object> transient = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("NativeRegistrationFixture");
            var child = new GameObject("Animated");
            child.transform.SetParent(root.transform, false);
            animated = child.transform;
            animation = root.AddComponent<Animation>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (Object value in transient) Object.DestroyImmediate(value);
            transient.Clear();
        }

        [Test]
        public void RepeatedBeginKeepsTheRegisteredRuntimeCopiesAndRestoresClampForever()
        {
            var idle = Clip(5f, 5f);
            var profile = Profile(Clip(0f, 1f), Clip(10f, 12f), idle);
            var player = new LicensedCombatMotionPlayer(animation, null);
            player.Begin(profile, "first", "Licensed_Anticipation");
            var anticipation = animation.GetClip("Licensed_Anticipation");
            var action = animation.GetClip("Licensed_Segment_0");
            var registeredIdle = animation.GetClip("Licensed_BaseIdle");
            int expectedCount = animation.GetClipCount();
            // GetClip identifies the actual Legacy runtime copy, not the serialized source.
            int anticipationId = anticipation.GetInstanceID();
            int actionId = action.GetInstanceID();
            int idleId = registeredIdle.GetInstanceID();
            for (int cycle = 0; cycle < 12; cycle++)
            {
                animation["Licensed_Anticipation"].wrapMode = WrapMode.Loop;
                animation["Licensed_Segment_0"].wrapMode = WrapMode.Loop;
                animation["Licensed_BaseIdle"].wrapMode = WrapMode.Once;
                player.Begin(profile, "repeat-" + cycle, "Licensed_Anticipation");
                Assert.That(animation.GetClip("Licensed_Anticipation").GetInstanceID(), Is.EqualTo(anticipationId));
                Assert.That(animation.GetClip("Licensed_Segment_0").GetInstanceID(), Is.EqualTo(actionId));
                Assert.That(animation.GetClip("Licensed_BaseIdle").GetInstanceID(), Is.EqualTo(idleId));
                Assert.That(animation.GetClipCount(), Is.EqualTo(expectedCount));
                Assert.That(animation["Licensed_Anticipation"].wrapMode, Is.EqualTo(WrapMode.ClampForever));
                Assert.That(animation["Licensed_Segment_0"].wrapMode, Is.EqualTo(WrapMode.ClampForever));
                Assert.That(animation["Licensed_BaseIdle"].wrapMode, Is.EqualTo(WrapMode.ClampForever));
                player.Tick(.25f);
                Assert.That(animated.localPosition.x, Is.EqualTo(.25f).Within(.002f));
                Assert.That(player.ConfirmContact("repeat-" + cycle), Is.True);
                player.Tick(.25f);
                Assert.That(animated.localPosition.x, Is.EqualTo(10.5f).Within(.002f));
                player.Cancel();
                AssertNativeStatesDisabled();
            }
        }

        [Test]
        public void ChangedSourceOrExternallyRemovedAndReplacedAliasesAreActuallyRestored()
        {
            var anticipationA = Clip(1f, 1f);
            var anticipationB = Clip(7f, 7f);
            var actionA = Clip(10f, 10f);
            var actionB = Clip(30f, 30f);
            var profile = Profile(anticipationA, actionA, Clip(5f, 5f));
            var player = new LicensedCombatMotionPlayer(animation, null);
            player.Begin(profile, "a", "Licensed_Anticipation");
            Assert.That(animated.localPosition.x, Is.EqualTo(1f).Within(.002f));
            profile.anticipation = anticipationB;
            profile.segments[0].clip = actionB;
            player.Begin(profile, "b", "Licensed_Anticipation");
            Assert.That(animated.localPosition.x, Is.EqualTo(7f).Within(.002f), "A genuine same-alias source change must sample its replacement");
            Assert.That(player.ConfirmContact("b"), Is.True);
            Assert.That(animated.localPosition.x, Is.EqualTo(30f).Within(.002f));
            animation.RemoveClip("Licensed_Anticipation");
            animation.AddClip(anticipationA, "Licensed_Segment_0");
            animation.AddClip(actionA, "Licensed_BaseIdle");
            player.Begin(profile, "restored", "Licensed_Anticipation");
            Assert.That(animated.localPosition.x, Is.EqualTo(7f).Within(.002f), "Externally removed anticipation must be re-registered");
            Assert.That(player.ConfirmContact("restored"), Is.True);
            Assert.That(animated.localPosition.x, Is.EqualTo(30f).Within(.002f), "Externally replaced segment must be repaired even when the cached source is unchanged");
            player.Tick(.25f);
            profile.segments[0].start = .5f;
            player.Begin(profile, "idle-restored", "Licensed_Anticipation");
            Assert.That(player.ConfirmContact("idle-restored"), Is.True);
            Assert.That(animated.localPosition.x, Is.EqualTo(5f).Within(.002f), "Externally replaced base idle must be repaired too");
        }

        [Test]
        public void ProductionNormalHeavyThrowCopiesRemainStableWithinEachProfileAndAliasesStayBounded()
        {
            var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
            Assert.That(library, Is.Not.Null);
            var profiles = new[] { library.keiko.licensedNormal, library.keiko.licensedHeavy, library.keiko.licensedThrow };
            var player = new LicensedCombatMotionPlayer(animation, library.keiko.idle);
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var profile in profiles)
                {
                    Assert.That(profile, Is.Not.Null);
                    player.Begin(profile, "warm", "Licensed_Anticipation");
                    var identities = RegisteredIdentities();
                    for (int repeat = 0; repeat < 3; repeat++)
                    {
                        string id = profile.name + "-" + pass + "-" + repeat;
                        player.Begin(profile, id, "Licensed_Anticipation");
                        CollectionAssert.AreEquivalent(identities, RegisteredIdentities(), "Repeated production-profile Begin must not rebuild the actual runtime copies");
                        Assert.That(animation.GetClipCount(), Is.LessThanOrEqualTo(profile.segments.Length + 2), "Shorter profiles must not accumulate retired native segment aliases");
                        Assert.That(player.ConfirmContact(id), Is.True);
                        player.Tick(1f / 60f);
                        float clock = player.Clock;
                        Assert.That(player.ConfirmContact(id), Is.False);
                        Assert.That(player.Clock, Is.EqualTo(clock));
                        player.Cancel();
                        AssertNativeStatesDisabled();
                        player.ResetForPool();
                        AssertNativeStatesDisabled();
                    }
                }
            }
            for (int index = 0; index < 16; index++)
            {
                player.Begin(profiles[0], "alias-" + index, "Licensed_Anticipation_" + index);
                Assert.That(animation.GetClipCount(), Is.LessThanOrEqualTo(profiles[0].segments.Length + 2), "Changing an owned anticipation alias must retire its old copy instead of growing the sampler forever");
            }
            player.Cancel();
            AssertNativeStatesDisabled();
        }

        [Test]
        public void FallbackIdleReuseDoesNotChangeContactHistoryOrPoolResetSemantics()
        {
            var fallback = Clip(5f, 5f);
            var profile = Profile(Clip(1f, 1f), Clip(10f, 12f), null);
            profile.segments[0].start = .5f;
            var player = new LicensedCombatMotionPlayer(animation, fallback);
            player.Begin(profile, "contact-once", "Licensed_Anticipation");
            int idleCopy = animation.GetClip("Licensed_BaseIdle").GetInstanceID();
            player.Tick(.25f);
            Assert.That(player.ConfirmContact("wrong"), Is.False);
            Assert.That(player.Clock, Is.EqualTo(.25f));
            Assert.That(player.ConfirmContact("contact-once"), Is.True);
            Assert.That(player.Clock, Is.Zero);
            Assert.That(animated.localPosition.x, Is.EqualTo(5f).Within(.002f));
            player.Tick(.1f);
            Assert.That(player.ConfirmContact("contact-once"), Is.False);
            Assert.That(player.Clock, Is.EqualTo(.1f));
            player.Cancel();
            AssertNativeStatesDisabled();
            player.Begin(profile, "contact-once", "Licensed_Anticipation");
            Assert.That(animation.GetClip("Licensed_BaseIdle").GetInstanceID(), Is.EqualTo(idleCopy));
            Assert.That(player.ConfirmContact("contact-once"), Is.False, "Cancel preserves consumed contact IDs");
            player.ResetForPool();
            player.Begin(profile, "contact-once", "Licensed_Anticipation");
            Assert.That(animation.GetClip("Licensed_BaseIdle").GetInstanceID(), Is.EqualTo(idleCopy));
            Assert.That(player.ConfirmContact("contact-once"), Is.True, "Pool reset alone clears consumed contact history");
            player.Cancel();
            AssertNativeStatesDisabled();
        }

        private Dictionary<string, int> RegisteredIdentities()
        {
            var identities = new Dictionary<string, int>();
            foreach (AnimationState state in animation)
                identities.Add(state.name, animation.GetClip(state.name).GetInstanceID());
            return identities;
        }

        private void AssertNativeStatesDisabled()
        {
            foreach (AnimationState state in animation)
            {
                Assert.That(state.enabled, Is.False, state.name + " remains enabled after cancel/reset");
                Assert.That(state.weight, Is.Zero, state.name + " retains native blend weight after cancel/reset");
            }
        }

        private AnimationClip Clip(float from, float to)
        {
            var clip = new AnimationClip { legacy = true };
            clip.SetCurve("Animated", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, from, 1f, to));
            transient.Add(clip);
            return clip;
        }

        private LicensedCombatMotionProfile Profile(AnimationClip anticipation, AnimationClip action, AnimationClip idle)
        {
            var profile = ScriptableObject.CreateInstance<LicensedCombatMotionProfile>();
            profile.anticipation = anticipation;
            profile.baseIdle = idle;
            profile.segments = new[] { new LicensedMotionSegment { clip = action, start = 0f, duration = 1f } };
            transient.Add(profile);
            return profile;
        }
    }
}
