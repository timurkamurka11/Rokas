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
        private const float MaxRootDriftMeters = .00065f; // submillimetre, not a floating arm
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
            Apply(seconds,strength,isAdjusting,cameraRight,cameraUp,
                strength,false);
        }

        /// <summary>
        /// Organic cinematic extension: finger flex follows the forearm with
        /// intentional delay on entry and relaxed release on exit.
        /// Original five-argument API stays compatible with existing QA.
        /// </summary>
        public void Apply(float seconds, float strength, bool isAdjusting,
            Vector3 cameraRight, Vector3 cameraUp,
            float fingerResponse, bool isLowering)
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
            fingerResponse=Mathf.Clamp01(fingerResponse);
            // Single low-frequency breathing rhythm with a tiny harmonic,
            // rather than three competing oscillators (0.22/0.37/0.51Hz).
            // The old motion translated 4.5 mm vertically plus 1.45 mm
            // sideways and rolled the wrist 1.3 degrees: visibly rocking.
            float breath = Mathf.Sin(seconds * 2f * Mathf.PI * .145f);
            float harmonic = Mathf.Sin(seconds * 2f * Mathf.PI * .29f + .45f);
            float life = strength * (.86f * breath + .14f * harmonic);
            previousRootOverlay = cameraUp * (life * MaxRootDriftMeters)
                + cameraRight * (.00018f * harmonic * strength);
            root.position += previousRootOverlay;
            previousWristOverlay = Quaternion.Euler(.19f * life, 0f,
                (.23f * life + (isAdjusting ? .08f * harmonic : 0f)) * strength);
            float secondary = Mathf.Sin(seconds*.61f + 1.2f);
            wrist.localRotation *= previousWristOverlay;
            overlayApplied = true;

            string[] fingers = { "Index", "Middle", "Ring", "Little" };
            // Source FBX contains five detached finger base islands.
            // Ring/little must NOT be curled harder than the intact
            // index/middle while collars are being visually repaired.
            // The source ring/little roots have separate, visibly squared mesh
            // islands. Limit articulation in those two until topology QA
            // confirms a safe continuous skin; give index/middle liveliness.
            // Conservative anatomy: index/little are disconnected skin
            // islands, so their PIP/DIP must not fold visibly at the socket.
            // Tiny independent delayed motion, no repeated "wave" gestures.
            float[] pip = { 1.35f, 1.65f, 1.20f, .65f };
            float[] dip = { .60f, .78f, .53f, .25f };
            for (int i = 0; i < fingers.Length; ++i)
            {
                float cycle = Mathf.Sin(seconds*(.52f+i*.035f)+i*.51f);
                float independent = Mathf.Sin(seconds*(.28f+i*.02f)+i*.91f);
                // No perfectly synchronous finger motion; natural tiny
                // flex compensates for slow wrist/breathing drift.
                float independentFlex=(.65f*cycle+.35f*independent) *
                    (i==0 ? .48f : i==3 ? .19f : .34f)*strength;
                // Distributed phalanx timing prevents a rigid parallel
                // "board" of fingers from riding the entering forearm.
                // Keep ring/little amplitude conservative until true source
                // FBX finger-root geometry is repaired.
                float phaseLag=Mathf.Clamp01(fingerResponse-i*.045f);
                float flex=strength*(.70f+.30f*phaseLag);
                float relaxPulse=Mathf.Sin(phaseLag*Mathf.PI)*
                    (isLowering ? -.16f : .19f)*strength;
                float interaction=(isAdjusting && i==0
                    ? .16f*(.5f+.5f*secondary) : 0f)*strength;
                float limitedRelax=relaxPulse*(i>=2?.34f:1f);
                float pipDegrees=Mathf.Clamp(
                    flex*pip[i]+independentFlex+interaction+limitedRelax,
                    -.15f,i==3 ? 1.05f : (i==0 ? 2.4f : 2.0f));
                float dipDegrees=Mathf.Clamp(
                    flex*dip[i]+independentFlex*.28f+limitedRelax*.22f,
                    -.10f,i==3 ? .45f : 1.15f);
                Curl(fingers[i]+"_PIP",pipDegrees);
                Curl(fingers[i]+"_DIP",dipDegrees);
            }
            Curl("Thumb_IP",strength*1.25f +
                strength*.36f*Mathf.Sin(seconds*.49f+.52f));
        }

        public float FingerBendDegrees(string jointName)
        {
            if (!phalanxRest.ContainsKey(jointName)) return 0f;
            return Quaternion.Angle(phalanxRest[jointName],
                joints[jointName].localRotation);
        }
    }
}
