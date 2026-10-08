using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Rokas.Tests
{
    public sealed class CombatStanceAndBackSocketRegressionTests
    {
        private GameObject root;
        private ReactiveCombatActorVisual actor;
        private ReactiveCombatActorLibrary library;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("CombatStanceAndBackSocketFixture");
            library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
            Assert.That(library, Is.Not.Null);
            actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
            actor.enabled = false;
            Tick(.12f);
        }

        [TearDown]
        public void TearDown() => Object.Destroy(root);

        [UnityTest]
        public IEnumerator HeavyCommitSurvivesNormalAndThrowPreviewCancellationAndCompletedThrow()
        {
            actor.CommitCombatStance(CombatIdleStance.Heavy);
            actor.PlayHeavy();
            CompleteNativeAction("heavy-confirmed", false);
            AssertSwordIdle(library.keiko.licensedHeavy, CombatIdleStance.Heavy);

            actor.PlayPreparation(false);
            Tick(.18f);
            Assert.That(actor.ActiveLicensedProfile, Is.SameAs(library.keiko.licensedNormal));
            Assert.That(actor.ConfirmedCombatStance, Is.EqualTo(CombatIdleStance.Heavy),
                "A Normal preview must not commit its sword idle.");
            actor.CancelPendingAttackContact();
            Tick(.12f);
            AssertSwordIdle(library.keiko.licensedHeavy, CombatIdleStance.Heavy);

            actor.PlayThrowPreparation();
            Tick(.18f);
            Assert.That(actor.HeldDagger.gameObject.activeSelf, Is.True, "Throw preview has its transient held dagger.");
            actor.CancelPendingAttackContact();
            Tick(.12f);
            AssertSwordIdle(library.keiko.licensedHeavy, CombatIdleStance.Heavy);

            actor.PlayThrow();
            actor.ReleaseThrowWeapon();
            CompleteNativeAction("throw-preserves-heavy", true);
            AssertSwordIdle(library.keiko.licensedHeavy, CombatIdleStance.Heavy);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OnlyExplicitCommitChangesTheIdleUsedAfterAFullNativeAction()
        {
            actor.PlayPreparation(true);
            Tick(.18f);
            actor.CancelPendingAttackContact();
            Tick(.12f);
            AssertSwordIdle(library.keiko.licensedNormal, CombatIdleStance.Normal);

            actor.CommitCombatStance(CombatIdleStance.Heavy);
            actor.PlayHeavy();
            CompleteNativeAction("select-heavy", false);
            AssertSwordIdle(library.keiko.licensedHeavy, CombatIdleStance.Heavy);

            actor.PlayPreparation(false);
            Tick(.18f);
            Assert.That(actor.ConfirmedLicensedProfile, Is.SameAs(library.keiko.licensedHeavy));
            actor.CommitCombatStance(CombatIdleStance.Normal);
            actor.PlayAttack();
            CompleteNativeAction("select-normal", false);
            AssertSwordIdle(library.keiko.licensedNormal, CombatIdleStance.Normal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GuardAndDodgeReturnToTheConfirmedHeavySwordIdle()
        {
            actor.CommitCombatStance(CombatIdleStance.Heavy);
            actor.PlayIdle();
            Tick(.12f);
            AssertSwordIdle(library.keiko.licensedHeavy, CombatIdleStance.Heavy);
            actor.PlayGuard();
            Assert.That(actor.DefenseActive, Is.True);
            TickUntil(() => actor.IdleSettled && actor.DefenseRecoveryComplete, 3f, "Guard exit");
            AssertSwordIdle(library.keiko.licensedHeavy, CombatIdleStance.Heavy);
            actor.PlayDodge();
            Assert.That(actor.DefenseActive, Is.True);
            TickUntil(() => actor.IdleSettled && actor.DefenseRecoveryComplete, 3f, "Dodge exit");
            AssertSwordIdle(library.keiko.licensedHeavy, CombatIdleStance.Heavy);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThrowBackCarryFollowsItsSpineWithoutAWorldRotationWriter()
        {
            Assert.That(actor.SwordStowCalibrationValid, Is.True, "Use the actual skin bind frame.");
            Transform stow = actor.SwordStowSocket;
            Assert.That(stow, Is.Not.Null);
            Assert.That(stow.parent.name, Is.EqualTo("mixamorig:Spine2"));
            Vector3 configuredPosition = library.keiko.swordStowPosition;
            Quaternion calibratedRotation = actor.SwordStowCalibratedLocalRotation;
            actor.PlayThrowPreparation();
            Tick(.16f);
            Transform sword = actor.WeaponAttachment.CurrentWeapon.transform;
            Assert.That(sword.parent, Is.SameAs(stow));
            AssertLocalCarry(stow, configuredPosition, calibratedRotation);
            Quaternion beforeFacing = sword.rotation;
            actor.SetFacing(false);
            Tick(.12f);
            AssertLocalCarry(stow, configuredPosition, calibratedRotation);
            Assert.That(Quaternion.Angle(beforeFacing, sword.rotation), Is.GreaterThan(45f),
                "Back carry must turn with the rig instead of remaining aligned to world down.");

            Quaternion beforeSpine = sword.rotation;
            stow.parent.localRotation *= Quaternion.AngleAxis(25f, Vector3.right);
            Assert.That(Quaternion.Angle(beforeSpine, sword.rotation), Is.GreaterThan(20f),
                "The carried sword follows a real spine change immediately, including a paused frame.");
            AssertLocalCarry(stow, configuredPosition, calibratedRotation);
            actor.CancelPendingAttackContact();
            Tick(.12f);
            AssertSwordIdle(library.keiko.licensedNormal, CombatIdleStance.Normal);
            Assert.That(sword.parent, Is.Not.SameAs(stow));
            yield return null;
        }

        private void CompleteNativeAction(string actionId, bool throwing)
        {
            LicensedCombatMotionProfile profile = actor.ActiveLicensedProfile;
            Assert.That(profile, Is.Not.Null);
            actor.BindLicensedContact(actionId);
            Assert.That(actor.ConfirmLicensedContact(actionId), Is.True);
            TickUntil(() => actor.ActiveLicensedProfile == null && actor.IdleSettled, profile.Duration + 1f, actionId);
            if (throwing) Assert.That(actor.HeldDagger.gameObject.activeSelf, Is.False,
                "The released dagger must not persist through the sword-idle handoff.");
        }

        private void AssertSwordIdle(LicensedCombatMotionProfile expected, CombatIdleStance stance)
        {
            Assert.That(actor.ConfirmedCombatStance, Is.EqualTo(stance));
            Assert.That(actor.ConfirmedLicensedProfile, Is.SameAs(expected));
            Assert.That(actor.CurrentPose, Is.EqualTo("Idle"));
            Assert.That(actor.ActiveLicensedProfile, Is.Null);
            Assert.That(actor.IdleSettled, Is.True);
            Assert.That(actor.HeldDagger.gameObject.activeSelf, Is.False, "Only the selected sword is held after transient actions.");
            Transform sword = actor.WeaponAttachment.CurrentWeapon.transform;
            Assert.That(sword.parent.name, Is.EqualTo("LicensedWeaponSocket"));
            Assert.That(sword.localPosition.sqrMagnitude, Is.LessThan(1e-10f));
            Assert.That(Quaternion.Angle(sword.localRotation, Quaternion.identity), Is.LessThan(.001f));

            // Compare actual rendered rig pose to the required saved clip, independently
            // of the new confirmed-state property or the legacy runtime Idle alias.
            var reference = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
            reference.enabled = false;
            expected.baseIdle.SampleAnimation(reference.ModelRoot.gameObject, actor.CurrentPoseSeconds);
            int compared = 0;
            foreach (Transform bone in actor.ModelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!bone.name.StartsWith("mixamorig:", StringComparison.Ordinal) ||
                    bone.name.Contains("HandIndex") || bone.name.Contains("HandMiddle") ||
                    bone.name.Contains("HandRing") || bone.name.Contains("HandPinky") || bone.name.Contains("HandThumb")) continue;
                Transform source = reference.ModelRoot.Find(RelativePath(bone, actor.ModelRoot));
                Assert.That(source, Is.Not.Null, bone.name);
                Assert.That(Vector3.Distance(bone.localPosition, source.localPosition), Is.LessThan(.0001f), bone.name + " idle position");
                Assert.That(Quaternion.Angle(bone.localRotation, source.localRotation), Is.LessThan(.08f), bone.name + " idle rotation");
                compared++;
            }
            Assert.That(compared, Is.GreaterThan(15), "Inspect the real body skeleton, beyond state metadata.");
            Object.Destroy(reference.gameObject);
        }

        private static string RelativePath(Transform bone, Transform model)
        {
            var names = new List<string>();
            for (Transform current = bone; current != null && current != model; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static void AssertLocalCarry(Transform stow, Vector3 position, Quaternion rotation)
        {
            Assert.That(Vector3.Distance(stow.localPosition, position), Is.LessThan(.000001f));
            Assert.That(Mathf.Abs(Quaternion.Dot(stow.localRotation, rotation)), Is.GreaterThan(.9999995f));
        }

        private void Tick(float seconds)
        {
            for (float elapsed = 0; elapsed < seconds; elapsed += 1f / 60f)
                actor.TickPresentation(Mathf.Min(1f / 60f, seconds - elapsed));
        }

        private void TickUntil(Func<bool> settled, float timeout, string label)
        {
            for (float elapsed = 0; elapsed < timeout && !settled(); elapsed += 1f / 60f) actor.TickPresentation(1f / 60f);
            Assert.That(settled(), Is.True, label + " did not settle within its source duration and owned exit.");
        }
    }
}
