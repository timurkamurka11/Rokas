using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Non-destructive, deterministic secondary motion on the ORIGINAL Argon
    /// 25-joint skinned FBX. Call after sampling authored animation and after
    /// the preview's rest-relative safety correction, every frame.
    /// Requires no Blender and never replaces the mesh or bind poses.
    /// </summary>
    public sealed class ArgonLivingRig
    {
        private readonly Transform root;
        private readonly Dictionary<string, Transform> joints =
            new Dictionary<string, Transform>();
        private readonly Dictionary<string, Quaternion> phalanxRest =
            new Dictionary<string, Quaternion>();
        private readonly Dictionary<string, Vector3> bendAxis =
            new Dictionary<string, Vector3>();
        private readonly Transform wrist;
        private const float MaxRootDriftMeters = .0035f;
        private Vector3 previousRootOverlay;
        private Quaternion previousWristOverlay = Quaternion.identity;
        private bool overlayApplied;

        private static readonly string[] Required = {
            "Arm_Wrist", "Index_MCP", "Middle_MCP", "Ring_MCP",
            "Little_MCP", "Thumb_MCP", "Thumb_IP",
            "Index_PIP", "Index_DIP", "Middle_PIP", "Middle_DIP",
            "Ring_PIP", "Ring_DIP", "Little_PIP", "Little_DIP"
        };

        public ArgonLivingRig(GameObject instance)
        {
            root = instance.transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!joints.ContainsKey(t.name)) joints.Add(t.name, t);
            foreach (var name in Required)
                if (!joints.ContainsKey(name))
                    throw new InvalidOperationException("Original Argon rig missing: " + name);
            wrist = joints["Arm_Wrist"];
            string[] fingers = { "Index", "Middle", "Ring", "Little" };
            foreach (string finger in fingers)
            {
                Cache(finger + "_PIP", finger + "_DIP");
                Cache(finger + "_DIP", finger + "_Tip");
            }
            Cache("Thumb_IP", "Thumb_Tip");
        }

        private void Cache(string name, string childName)
        {
            Transform joint = joints[name];
            Transform tip = joints[childName];
            phalanxRest[name] = joint.localRotation;
            // Direction of authored finger and the actual back-of-hand face.
            // Cross toward PALM (-Y) rather than arbitrary local Euler axes.
            Vector3 axis = Vector3.Cross(
                (tip.position - joint.position).normalized, Vector3.down);
            if (axis.sqrMagnitude < .001f) axis = joint.right;
            bendAxis[name] = axis.normalized;
        }

        private void Curl(string name, float degrees)
        {
            var t = joints[name];
            // Unanimated GLB PIP/DIP joints MUST return to their bind pose
            // each frame; otherwise small curls accumulate and fingers break.
            t.localRotation = phalanxRest[name];
            Vector3 localAxis = t.parent.InverseTransformDirection(bendAxis[name]);
            t.localRotation = Quaternion.AngleAxis(degrees, localAxis) * t.localRotation;
        }

        /// <summary>
        /// Call this when an Animator/SampleAnimation explicitly rewrites the
        /// root and wrist from the base authored pose before Apply. Prevents
        /// trying to undo an overlay that the animator has already replaced.
        /// </summary>
        public void NotifyAnimationPoseReset()
        {
            overlayApplied = false;
            previousRootOverlay = Vector3.zero;
            previousWristOverlay = Quaternion.identity;
        }

        public void Apply(float seconds, float strength, bool isAdjusting,
            Vector3 cameraRight, Vector3 cameraUp)
        {
            // If no animator reset occurred, remove *our own* previous
            // offset before applying the new frame. Otherwise the former
            // += implementation drifted meters over an extended PlayMode
            // session and accumulated wrist rotation indefinitely.
            if (overlayApplied)
            {
                root.position -= previousRootOverlay;
                wrist.localRotation *= Quaternion.Inverse(previousWristOverlay);
            }
            strength = Mathf.Clamp01(strength);
            float breath = Mathf.Sin(seconds * 2f * Mathf.PI * .22f);
            float secondary = Mathf.Sin(seconds * 2f * Mathf.PI * .37f + 1.2f);
            float life = strength * (.55f * breath + .45f * secondary);
            previousRootOverlay = cameraUp * (life * MaxRootDriftMeters)
                + cameraRight * (secondary * .00115f * strength);
            root.position += previousRootOverlay;
            // Slow restraint: under one degree of wrist roll / pitch.
            previousWristOverlay = Quaternion.Euler(.65f * life, 0f,
                (.85f * life + (isAdjusting ? .55f * secondary : 0f)) * strength);
            wrist.localRotation *= previousWristOverlay;
            overlayApplied = true;

            string[] fingers = { "Index", "Middle", "Ring", "Little" };
            float[] pip = { 4.8f, 6.8f, 8.1f, 9.3f };
            float[] dip = { 2.2f, 3.2f, 3.9f, 4.9f };
            for (int i = 0; i < fingers.Length; ++i)
            {
                float cycle = Mathf.Sin(seconds * (1.32f + i * .09f) + i * .84f);
                float micro = strength * (.85f * cycle + (isAdjusting ? 1.1f : 0f));
                Curl(fingers[i] + "_PIP", strength * pip[i] + micro);
                Curl(fingers[i] + "_DIP", strength * dip[i] + micro * .38f);
            }
            Curl("Thumb_IP", strength * 4.6f +
                strength * .6f * Mathf.Sin(seconds * 1.42f + .5f));
        }

        public float FingerBendDegrees(string jointName)
        {
            if (!phalanxRest.ContainsKey(jointName)) return 0f;
            return Quaternion.Angle(phalanxRest[jointName],
                joints[jointName].localRotation);
        }
    }
}
