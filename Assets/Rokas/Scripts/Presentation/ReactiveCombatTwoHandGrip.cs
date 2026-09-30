using System;
using UnityEngine;

namespace Rokas.Presentation
{
    // The FBX takes contain both arms. This small correction keeps the support
    // palm on this sword during Legacy crossfades between those authored poses.
    internal sealed class ReactiveCombatTwoHandGrip
    {
        private readonly Transform upperArm;
        private readonly Transform foreArm;
        private readonly Transform hand;
        private readonly Transform target;
        private Quaternion upperSample;
        private Quaternion foreSample;
        private Quaternion handSample;
        private bool applied;

        public ReactiveCombatTwoHandGrip(Transform model, Transform weapon)
        {
            if (model == null || weapon == null) return;
            target = weapon.Find("LeftHandGrip");
            foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name.EndsWith("LeftArm", StringComparison.Ordinal)) upperArm = bone;
                else if (bone.name.EndsWith("LeftForeArm", StringComparison.Ordinal)) foreArm = bone;
                else if (bone.name.EndsWith("LeftHand", StringComparison.Ordinal)) hand = bone;
            }
        }

        public void RestoreSample()
        {
            if (!applied) return;
            upperArm.localRotation = upperSample;
            foreArm.localRotation = foreSample;
            hand.localRotation = handSample;
            applied = false;
        }

        public void Apply()
        {
            if (upperArm == null || foreArm == null || hand == null || target == null) return;
            RestoreSample();
            upperSample = upperArm.localRotation;
            foreSample = foreArm.localRotation;
            handSample = hand.localRotation;
            applied = true;
            // Imported Legacy bone axes are authored into the take. Preserve
            // that wrist orientation while correcting only its palm position.
            Quaternion wristRotation = hand.rotation;
            float actorScale = Mathf.Abs(hand.lossyScale.y);
            Vector3 wrist = target.position - wristRotation * Vector3.up * (.033f * actorScale);
            Vector3 shoulder = upperArm.position;
            float upperLength = Vector3.Distance(shoulder, foreArm.position);
            float foreLength = Vector3.Distance(foreArm.position, hand.position);
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
            foreArm.rotation = Quaternion.FromToRotation(hand.position - foreArm.position,
                shoulder + direction * distance - foreArm.position) * foreArm.rotation;
            hand.rotation = wristRotation;
        }
    }
}
