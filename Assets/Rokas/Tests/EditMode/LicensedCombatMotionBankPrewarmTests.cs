using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rokas.Tests
{
    public sealed class LicensedCombatMotionBankPrewarmTests
    {
        private GameObject root;
        private Transform animated;
        private Animation animation;
        private readonly List<Object> transient = new List<Object>();

        [SetUp] public void SetUp()
        {
            root = new GameObject("NativePreparedBankFixture");
            animated = new GameObject("Animated").transform;
            animated.SetParent(root.transform, false);
            animation = root.AddComponent<Animation>();
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (Object item in transient) Object.DestroyImmediate(item);
            transient.Clear();
        }

        // A shared-alias replacement on each stance/Throw switch loses the prepared
        // Legacy copies and puts AddClip on the first live command again.
        [Test]
        public void InactiveBankKeepsNormalCopiesAndSwitchesAllThreeProfilesWithoutNewCopies()
        {
            var profiles = new[] { Profile(1f, 10f), Profile(7f, 30f), Profile(13f, 50f) };
            var player = new LicensedCombatMotionPlayer(animation, null);
            Assert.That(player.Prepare(profiles[0], "Licensed_Anticipation"), Is.True);
            var normalCopies = Copies();
            animation.AddClip(Clip(-4f, -4f), "OwnIdle");
            animation["OwnIdle"].enabled = true;
            animation["OwnIdle"].weight = 1f;
            animation["OwnIdle"].speed = 0f;
            animation["OwnIdle"].time = .2f;
            animation.enabled = false;
            animated.localPosition = new Vector3(34f, 12f, 3f);
            Matrix4x4 before = animated.localToWorldMatrix;
            Assert.That(PrepareBank(player, profiles), Is.True);
            Assert.That(player.Active, Is.False);
            Assert.That(animated.localToWorldMatrix, Is.EqualTo(before), "Bank preparation cannot sample the existing rig.");
            Assert.That(animation.enabled, Is.False);
            Assert.That(animation["OwnIdle"].enabled, Is.True);
            Assert.That(animation["OwnIdle"].weight, Is.EqualTo(1f));
            Assert.That(animation["OwnIdle"].time, Is.EqualTo(.2f));
            var prepared = Copies();
            Assert.That(prepared.Count, Is.EqualTo(9), "Only the three requested anticipation/action/idle sets belong in this fixture.");
            foreach (var copy in normalCopies)
                Assert.That(prepared[copy.Key], Is.EqualTo(copy.Value), "Already prepared Normal copies must be adopted.");
            AssertAllNativeDisabled();
            float[] anticipation = { 1f, 7f, 13f };
            float[] contact = { 10f, 30f, 50f };
            for (int repeat = 0; repeat < 4; repeat++)
                for (int index = 0; index < profiles.Length; index++)
                {
                    string id = "bank-" + repeat + "-" + index;
                    player.Begin(profiles[index], id, "Licensed_Anticipation");
                    CollectionAssert.AreEquivalent(prepared, Copies(), "Live profile switching must reuse every prepared runtime copy.");
                    Assert.That(animated.localPosition.x, Is.EqualTo(anticipation[index]).Within(.002f));
                    Assert.That(player.ConfirmContact(id), Is.True);
                    Assert.That(animated.localPosition.x, Is.EqualTo(contact[index]).Within(.002f));
                    player.Tick(.2f);
                    Assert.That(animated.localPosition.x, Is.EqualTo(contact[index] + .4f).Within(.002f));
                    float clock = player.Clock;
                    Assert.That(player.ConfirmContact(id), Is.False);
                    Assert.That(player.Clock, Is.EqualTo(clock));
                    player.Cancel();
                    AssertAllNativeDisabled();
                }
            Assert.That(PrepareBank(player, profiles), Is.True);
            CollectionAssert.AreEquivalent(prepared, Copies(), "Repeated startup preparation cannot duplicate a bank.");
            player.Begin(profiles[2], "bank-3-2", "Licensed_Anticipation");
            Assert.That(player.ConfirmContact("bank-3-2"), Is.False, "Preparing a bank does not clear consumed contacts.");
            player.ResetForPool();
            player.Begin(profiles[2], "bank-3-2", "Licensed_Anticipation");
            Assert.That(player.ConfirmContact("bank-3-2"), Is.True);
            CollectionAssert.AreEquivalent(prepared, Copies());
        }

        [Test]
        public void ActiveBankPreparationCannotChangeSourceClockPoseOrContact()
        {
            var normal = Profile(1f, 10f);
            var other = Profile(7f, 30f);
            var player = new LicensedCombatMotionPlayer(animation, null);
            player.Begin(normal, "active", "Licensed_Anticipation");
            Assert.That(player.ConfirmContact("active"), Is.True);
            player.Tick(.2f);
            Matrix4x4 pose = animated.localToWorldMatrix;
            var copies = Copies();
            Assert.That(PrepareBank(player, new[] { normal, other }), Is.False);
            Assert.That(player.Active, Is.True);
            Assert.That(player.Profile, Is.SameAs(normal));
            Assert.That(player.Contacted, Is.True);
            Assert.That(player.Clock, Is.EqualTo(.2f));
            Assert.That(animated.localToWorldMatrix, Is.EqualTo(pose));
            CollectionAssert.AreEquivalent(copies, Copies());
            player.Tick(.1f);
            Assert.That(animated.localPosition.x, Is.EqualTo(10.6f).Within(.002f));
        }

        [Test]
        public void PreparedBankRepairsOneChangedSourceWithoutReplacingOtherProfileCopies()
        {
            var normal = Profile(1f, 10f);
            var throwing = Profile(13f, 50f);
            var player = new LicensedCombatMotionPlayer(animation, null);
            Assert.That(player.Prepare(normal, "Licensed_Anticipation"), Is.True);
            Assert.That(PrepareBank(player, new[] { normal, throwing }), Is.True);
            var before = Copies();
            throwing.anticipation = Clip(17f, 17f);
            player.Begin(throwing, "new-source", "Licensed_Anticipation");
            Assert.That(animated.localPosition.x, Is.EqualTo(17f).Within(.002f));
            var repaired = Copies();
            Assert.That(repaired.Count, Is.EqualTo(before.Count));
            int changed = 0;
            foreach (var copy in before) if (copy.Value != repaired[copy.Key]) changed++;
            Assert.That(changed, Is.EqualTo(1), "Only the genuinely changed source earns a new Legacy copy.");
            player.Begin(normal, "normal-again", "Licensed_Anticipation");
            CollectionAssert.AreEquivalent(repaired, Copies());
            Assert.That(animated.localPosition.x, Is.EqualTo(1f).Within(.002f));
        }

        private static bool PrepareBank(LicensedCombatMotionPlayer player, LicensedCombatMotionProfile[] profiles)
        {
            var method = typeof(LicensedCombatMotionPlayer).GetMethod("PrepareBank", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Inactive motion bank is missing; live stance switches still rebuild Legacy copies.");
            return (bool)method.Invoke(player, new object[] { profiles });
        }

        private Dictionary<string, int> Copies()
        {
            var result = new Dictionary<string, int>();
            foreach (AnimationState state in animation)
                if (state.name.StartsWith("Licensed_", System.StringComparison.Ordinal))
                    result.Add(state.name, animation.GetClip(state.name).GetInstanceID());
            return result;
        }
        private void AssertAllNativeDisabled()
        {
            foreach (AnimationState state in animation)
                if (state.name.StartsWith("Licensed_", System.StringComparison.Ordinal))
                { Assert.That(state.enabled, Is.False); Assert.That(state.weight, Is.Zero); }
        }
        private AnimationClip Clip(float from, float to)
        {
            var clip = new AnimationClip { legacy = true };
            clip.SetCurve("Animated", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, from, 1f, to));
            transient.Add(clip);
            return clip;
        }
        private LicensedCombatMotionProfile Profile(float anticipation, float contact)
        {
            var profile = ScriptableObject.CreateInstance<LicensedCombatMotionProfile>();
            profile.anticipation = Clip(anticipation, anticipation);
            profile.baseIdle = Clip(-2f, -2f);
            profile.segments = new[] { new LicensedMotionSegment { clip = Clip(contact, contact + 2f), start = 0f, duration = 1f } };
            transient.Add(profile);
            return profile;
        }
    }
}
