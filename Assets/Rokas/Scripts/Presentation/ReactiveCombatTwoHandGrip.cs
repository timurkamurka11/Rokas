using System;
using UnityEngine;

namespace Rokas.Presentation
{
    // Authored takes own the arms and fingers. The existing palm correction is
    // used only while owned Legacy poses crossfade, including the native exit.
    internal sealed class ReactiveCombatTwoHandGrip
    {
        private readonly Arm primary;
        private readonly Arm support;
        private readonly Transform primaryTarget;
        private readonly Transform supportTarget;

        public Transform PrimaryHand => primary?.Hand;
        public bool PrimaryHeldByLeft => primaryTarget != null && primary != null && support != null &&
            (support.PalmPosition - primaryTarget.position).sqrMagnitude <
            (primary.PalmPosition - primaryTarget.position).sqrMagnitude;
        public bool SupportHandHolding => supportTarget != null && support != null &&
            Vector3.Distance(support.PalmPosition, supportTarget.position) <
            .002f * Mathf.Abs(support.Hand.lossyScale.y);

        public ReactiveCombatTwoHandGrip(Transform model, Transform weapon)
        {
            if (model == null || weapon == null) return;
            primaryTarget = weapon.Find("RightHandGrip") ?? weapon;
            supportTarget = weapon.Find("LeftHandGrip");
            Transform rightArm = null, rightForeArm = null, rightHand = null;
            Transform leftArm = null, leftForeArm = null, leftHand = null;
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name.EndsWith("RightArm", StringComparison.Ordinal)) rightArm = bone;
                else if (bone.name.EndsWith("RightForeArm", StringComparison.Ordinal)) rightForeArm = bone;
                else if (bone.name.EndsWith("RightHand", StringComparison.Ordinal)) rightHand = bone;
                else if (bone.name.EndsWith("LeftArm", StringComparison.Ordinal)) leftArm = bone;
                else if (bone.name.EndsWith("LeftForeArm", StringComparison.Ordinal)) leftForeArm = bone;
                else if (bone.name.EndsWith("LeftHand", StringComparison.Ordinal)) leftHand = bone;
            }
            // These are the existing owned mesh palm centers, in bone coordinates.
            if (rightArm != null && rightForeArm != null && rightHand != null)
                primary = new Arm(rightArm, rightForeArm, rightHand, .0324945897f);
            if (leftArm != null && leftForeArm != null && leftHand != null)
                support = new Arm(leftArm, leftForeArm, leftHand, .0329871997f);
        }

        public void RestoreSample()
        {
            primary?.RestoreSample();
            support?.RestoreSample();
        }

        public void Apply()
        {
            RestoreSample();
            if (supportTarget != null) support?.Apply(supportTarget.position, 1f);
        }

        public Vector3 ResolveExitPrimaryPosition(Vector3 position, float weight, bool primaryHeldByLeft,
            bool sourceSupportHeld, bool destinationSupportHeld)
        {
            if (weight <= 0f || primary == null || support == null || primaryTarget == null) return position;
            // A local FK crossfade can put the hilt just beyond one arm's reach.
            // Return a feasible hilt position to the actor's single weapon writer;
            // this method never moves a bone, weapon, or finger.
            bool constrainLeft = primaryHeldByLeft
                ? destinationSupportHeld || weight <= .5f
                : sourceSupportHeld && destinationSupportHeld;
            Vector3 leftOffset = supportTarget == null ? Vector3.zero
                : supportTarget.position - primaryTarget.position;
            if (primaryHeldByLeft)
                leftOffset *= destinationSupportHeld ? Mathf.Clamp01((weight - .5f) * 2f) : 0f;
            for (int i = 0; i < 24; i++)
            {
                Vector3 previous = position;
                if (constrainLeft) position = support.ClampPalmTarget(position + leftOffset) - leftOffset;
                position = primary.ClampPalmTarget(position);
                if ((position - previous).sqrMagnitude < 1e-12f) break;
            }
            return position;
        }

        public void ApplyExit(float weight, bool primaryHeldByLeft, bool sourceSupportHeld,
            bool destinationSupportHeld, bool correctPrimary)
        {
            if (weight <= 0f || primaryTarget == null) return;
            RestoreSample();
            weight = Mathf.Clamp01(weight);
            if (primaryHeldByLeft)
            {
                // Touche can leave the source action in its left hand. Keep that
                // palm on the hilt while the right hand closes; then let the left
                // palm slide to the owned support grip or release into source idle.
                primary?.Apply(primaryTarget.position, Mathf.Clamp01(weight * 2f));
                if (destinationSupportHeld && supportTarget != null)
                    support?.Apply(Vector3.Lerp(primaryTarget.position, supportTarget.position,
                        Mathf.Clamp01((weight - .5f) * 2f)), 1f);
                else
                    support?.Apply(primaryTarget.position, Mathf.Clamp01((1f - weight) * 2f));
                return;
            }
            if (correctPrimary) primary?.Apply(primaryTarget.position, 1f);
            if (supportTarget == null) return;
            float supportWeight = destinationSupportHeld
                ? sourceSupportHeld ? 1f : weight
                : sourceSupportHeld ? 1f - weight : 0f;
            support?.Apply(supportTarget.position, supportWeight);
        }

        private sealed class Arm
        {
            private readonly Transform upperArm;
            private readonly Transform foreArm;
            private readonly float palmOffset;
            public Transform Hand { get; }
            public Vector3 PalmPosition => Hand.TransformPoint(Vector3.up * palmOffset);
            private Quaternion upperSample;
            private Quaternion foreSample;
            private Quaternion handSample;
            private bool applied;

            public Arm(Transform upper, Transform fore, Transform hand, float offset)
            {
                upperArm = upper;
                foreArm = fore;
                Hand = hand;
                palmOffset = offset;
            }

            public void RestoreSample()
            {
                if (!applied) return;
                upperArm.localRotation = upperSample;
                foreArm.localRotation = foreSample;
                Hand.localRotation = handSample;
                applied = false;
            }

            public Vector3 ClampPalmTarget(Vector3 palmTarget)
            {
                Vector3 palmOffsetWorld = Hand.TransformVector(Vector3.up * palmOffset);
                Vector3 center = upperArm.position + palmOffsetWorld;
                float upperLength = Vector3.Distance(upperArm.position, foreArm.position);
                float foreLength = Vector3.Distance(foreArm.position, Hand.position);
                Vector3 delta = palmTarget - center;
                float distance = delta.magnitude;
                float radius = upperLength + foreLength - .0002f;
                return distance > radius ? center + delta * (radius / distance) : palmTarget;
            }

            public void Apply(Vector3 palmTarget, float weight)
            {
                if (weight <= 0f) return;
                upperSample = upperArm.localRotation;
                foreSample = foreArm.localRotation;
                handSample = Hand.localRotation;
                applied = true;
                // Preserve the sampled wrist orientation; the correction has no
                // finger channels and never writes the equipped weapon.
                Quaternion wristRotation = Hand.rotation;
                Vector3 palmOffsetWorld = Hand.TransformVector(Vector3.up * palmOffset);
                Vector3 wrist = palmTarget - palmOffsetWorld;
                Vector3 shoulder = upperArm.position;
                float upperLength = Vector3.Distance(shoulder, foreArm.position);
                float foreLength = Vector3.Distance(foreArm.position, Hand.position);
                Vector3 toWrist = wrist - shoulder;
                if (toWrist.sqrMagnitude < .000001f) return;
                Vector3 direction = toWrist.normalized;
                float distance = Mathf.Clamp(toWrist.magnitude,
                    Mathf.Abs(upperLength - foreLength) + .0001f, upperLength + foreLength - .0001f);
                Vector3 pole = Vector3.ProjectOnPlane(foreArm.position - shoulder, direction);
                if (pole.sqrMagnitude < .000001f)
                    pole = Vector3.ProjectOnPlane(upperArm.TransformDirection(Vector3.right), direction);
                pole.Normalize();
                float along = (upperLength * upperLength - foreLength * foreLength + distance * distance) /
                    (2f * distance);
                float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
                Vector3 elbow = shoulder + direction * along + pole * height;
                upperArm.rotation = Quaternion.FromToRotation(foreArm.position - shoulder, elbow - shoulder) *
                    upperArm.rotation;
                foreArm.rotation = Quaternion.FromToRotation(Hand.position - foreArm.position,
                    shoulder + direction * distance - foreArm.position) * foreArm.rotation;
                if (weight < 1f)
                {
                    upperArm.localRotation = Quaternion.Slerp(upperSample, upperArm.localRotation, weight);
                    foreArm.localRotation = Quaternion.Slerp(foreSample, foreArm.localRotation, weight);
                }
                Hand.rotation = wristRotation;
            }
        }
    }
}
