using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rokas.Tests
{
    public sealed class LicensedCombatMotionPrewarmTests
    {
        private GameObject root;
        private Transform animated;
        private Animation animation;
        private readonly List<Object> transient = new List<Object>();

        [SetUp] public void SetUp()
        {
            root = new GameObject("NativePrewarmFixture");
            var child = new GameObject("Animated");
            child.transform.SetParent(root.transform, false);
            animated = child.transform;
            animation = root.AddComponent<Animation>();
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (Object item in transient) Object.DestroyImmediate(item);
            transient.Clear();
        }

        [Test]
        public void PrepareRegistersActualCopiesWithoutSamplingAnyPoseOrTakingActiveOwnership()
        {
            var fallback = Clip(5f, 5f);
            var profile = Profile(Clip(0f, 1f), Clip(10f, 12f));
            var ownIdle = Clip(-4f, -4f);
            animation.AddClip(ownIdle, "OwnIdle");
            animation["OwnIdle"].enabled = true;
            animation["OwnIdle"].weight = 1f;
            animation["OwnIdle"].speed = 0f;
            animation["OwnIdle"].time = .2f;
            animation.enabled = false;
            root.transform.SetPositionAndRotation(new Vector3(2f, 3f, 4f), Quaternion.Euler(12f, 23f, 34f));
            root.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
            animated.localPosition = new Vector3(34f, 12f, 3f);
            animated.localRotation = Quaternion.Euler(10f, 20f, 30f);
            animated.localScale = new Vector3(.8f, 1.2f, 1.1f);
            Matrix4x4 beforeRoot = root.transform.localToWorldMatrix;
            Matrix4x4 beforeBone = animated.localToWorldMatrix;
            var player = new LicensedCombatMotionPlayer(animation, fallback);
            Assert.That(Prepare(player, profile), Is.True);
            Assert.That(player.Active, Is.False);
            Assert.That(player.Profile, Is.Null);
            Assert.That(player.Contacted, Is.False);
            Assert.That(player.Clock, Is.Zero);
            Assert.That(root.transform.localToWorldMatrix, Is.EqualTo(beforeRoot));
            Assert.That(animated.localToWorldMatrix, Is.EqualTo(beforeBone), "Prepare must never Sample/Play/Tick the existing rig");
            Assert.That(animation.enabled, Is.False);
            Assert.That(animation["OwnIdle"].enabled, Is.True, "Prepare owns only its prepared aliases, not an existing Idle state");
            Assert.That(animation["OwnIdle"].weight, Is.EqualTo(1f));
            Assert.That(animation["OwnIdle"].time, Is.EqualTo(.2f));
            foreach (AnimationState state in animation)
                if (state.name.StartsWith("Licensed_", StringComparison.Ordinal))
                { Assert.That(state.enabled, Is.False); Assert.That(state.weight, Is.Zero); }
            var copies = NativeCopies(animation);
            Assert.That(copies.Count, Is.EqualTo(3));
            player.Begin(profile, "prepared-first", "Licensed_Anticipation");
            CollectionAssert.AreEquivalent(copies, NativeCopies(animation), "The first real Begin must reuse every actual prepared Legacy runtime copy");
            Assert.That(animated.localPosition.x, Is.EqualTo(0f).Within(.002f));
            Assert.That(player.ConfirmContact("prepared-first"), Is.True);
            player.Tick(.2f);
            player.Cancel();
            Assert.That(Prepare(player, profile), Is.True);
            player.Begin(profile, "prepared-first", "Licensed_Anticipation");
            Assert.That(player.ConfirmContact("prepared-first"), Is.False, "Prepare/Cancel cannot clear consumed contact history");
            player.ResetForPool();
            Assert.That(Prepare(player, profile), Is.True);
            player.Begin(profile, "prepared-first", "Licensed_Anticipation");
            Assert.That(player.ConfirmContact("prepared-first"), Is.True, "Only the existing pool reset clears contact history");
        }

        [Test]
        public void ActivePrepareIsRejectedWithoutChangingClockPoseCopiesOrContactFlow()
        {
            var profile = Profile(Clip(0f, 1f), Clip(10f, 12f));
            var other = Profile(Clip(7f, 7f), Clip(30f, 30f));
            var player = new LicensedCombatMotionPlayer(animation, null);
            player.Begin(profile, "active", "Licensed_Anticipation");
            Assert.That(player.ConfirmContact("active"), Is.True);
            player.Tick(.2f);
            var copies = NativeCopies(animation);
            Matrix4x4 pose = animated.localToWorldMatrix;
            Assert.That(Prepare(player, other), Is.False);
            Assert.That(player.Profile, Is.SameAs(profile));
            Assert.That(player.Active, Is.True);
            Assert.That(player.Contacted, Is.True);
            Assert.That(player.Clock, Is.EqualTo(.2f));
            Assert.That(animated.localToWorldMatrix, Is.EqualTo(pose));
            CollectionAssert.AreEquivalent(copies, NativeCopies(animation));
            Assert.That(player.ConfirmContact("active"), Is.False);
            player.Tick(.1f);
            Assert.That(animated.localPosition.x, Is.EqualTo(10.6f).Within(.002f), "Rejected prewarm cannot interrupt or double-advance the active sampler");
        }

        [TestCase(CombatActorKind.Keiko)]
        [TestCase(CombatActorKind.Yokai)]
        public void SpawnPreparesOnlyDefaultNormalNativeAliasesBeforeFirstPreparation(CombatActorKind kind)
        {
            var actor = ReactiveCombatActorVisual.Spawn(kind, root.transform);
            actor.enabled = false;
            var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
            var normal = library.Get(kind).licensedNormal;
            Assert.That(normal, Is.Not.Null);
            Animation actual = actor.ModelRoot.GetComponent<Animation>();
            var copies = NativeCopies(actual);
            Assert.That(copies.Count, Is.EqualTo(normal.segments.Length + 2), "Spawn must prewarm only the normal bank; do not preload Heavy/Throw/all27");
            Assert.That(actor.ActiveLicensedProfile, Is.Null);
            foreach (AnimationState state in actual)
                if (state.name.StartsWith("Licensed_", StringComparison.Ordinal))
                { Assert.That(state.enabled, Is.False); Assert.That(state.weight, Is.Zero); }
            actor.PlayPreparation(false);
            CollectionAssert.AreEquivalent(copies, NativeCopies(actual), "The first interactive actor preparation cannot rebuild its default normal bank");
            Assert.That(actor.ActiveLicensedProfile, Is.SameAs(normal));
        }

        private static bool Prepare(LicensedCombatMotionPlayer player, LicensedCombatMotionProfile profile)
        {
            var method = typeof(LicensedCombatMotionPlayer).GetMethod("Prepare", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Inactive-only native registration prewarm is missing");
            return (bool)method.Invoke(player, new object[] { profile, "Licensed_Anticipation" });
        }
        private static Dictionary<string, int> NativeCopies(Animation actual)
        {
            var result = new Dictionary<string, int>();
            foreach (AnimationState state in actual)
                if (state.name.StartsWith("Licensed_", StringComparison.Ordinal))
                    result.Add(state.name, actual.GetClip(state.name).GetInstanceID());
            return result;
        }
        private AnimationClip Clip(float from, float to)
        {
            var clip = new AnimationClip { legacy = true };
            clip.SetCurve("Animated", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, from, 1f, to));
            transient.Add(clip);
            return clip;
        }
        private LicensedCombatMotionProfile Profile(AnimationClip anticipation, AnimationClip action)
        {
            var profile = ScriptableObject.CreateInstance<LicensedCombatMotionProfile>();
            profile.anticipation = anticipation;
            profile.segments = new[] { new LicensedMotionSegment { clip = action, start = 0f, duration = 1f } };
            transient.Add(profile);
            return profile;
        }
    }
}
