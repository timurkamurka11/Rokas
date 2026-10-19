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
        // Whole-arm drift must be barely detectable. The visible life comes
        // from local finger articulation, NOT translating the watch across
        // the bedroom camera frame.
        private const float MaxRootDriftMeters = .00020f;
        private static readonly string[] FingerNames = {
            "Index", "Middle", "Ring", "Little"
        };
        private static readonly float[] PipBase = {
            1.72f, 2.28f, 1.76f, .53f
        };
        private static readonly float[] DipBase = {
            .70f, 1.02f, .77f, .20f
        };
        private static readonly float[] McpBase = {
            .16f, .19f, .14f, .07f
        };
        private static readonly float[] PipAmplitude = {
            .52f, .62f, .43f, .18f
        };
        private static readonly float[] FingerFrequency = {
            .67f, .49f, .56f, .38f
        };
        private static readonly float[] FingerPhase = {
            .38f, 1.89f, 3.38f, 4.76f
        };
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
            foreach (string finger in FingerNames)
            {
                Cache(finger + "_MCP", finger + "_PIP");
                Cache(finger + "_PIP", finger + "_DIP");
                Cache(finger + "_DIP", finger + "_Tip");
            }
            Cache("Thumb_MCP", "Thumb_IP");
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
            // Low-amplitude, non-dominant motion. Avoid obvious regular
            // side-to-side wrist "waving" while setting the watch.
            float breath = Mathf.Sin(seconds * .72f);
            float secondaryBreath = Mathf.Sin(seconds * .29f + 1.7f);
            float life = strength * (.78f * breath + .22f * secondaryBreath);
            previousRootOverlay = cameraUp * (life * MaxRootDriftMeters)
                + cameraRight * (.000055f * secondaryBreath * strength);
            root.position += previousRootOverlay;
            previousWristOverlay = Quaternion.Euler(.09f * life,
                0f, .11f * life);
            wrist.localRotation *= previousWristOverlay;
            overlayApplied = true;

            // Separate "settle on arrival" from quiet idle. During entry
            // fingerResponse progresses with the forearm/wrist animation.
            // On lowering it gently reverses. Every finger has its own
            // response lag, two non-locked oscillators, and tiny MCP
            // participation. Index/little root movement stays below .3°
            // because their detached socket geometry is fragile.
            for (int i = 0; i < FingerNames.Length; ++i)
            {
                float lagged = Mathf.Clamp01(
                    (fingerResponse - i * .035f) / (1f - i * .035f));
                float response = lagged * lagged * (3f - 2f * lagged);
                float live = strength * (.16f + .84f * response);
                float arrival = Mathf.Sin(response * Mathf.PI);
                arrival *= arrival;
                float primary = Mathf.Sin(
                    seconds * FingerFrequency[i] + FingerPhase[i]);
                float secondary = Mathf.Sin(
                    seconds * (FingerFrequency[i] * .43f) +
                    FingerPhase[i] * 1.63f + .72f);
                float asymmetry = .70f * primary + .30f * secondary;
                // Lowering briefly releases tension, without flinging tips
                // into the source finger islands / opening socket seams.
                float relax = arrival * (isLowering ? -.25f : .31f);
                float pipDegrees = live * (
                    PipBase[i] + PipAmplitude[i] * asymmetry +
                    relax * (i == 3 ? .28f : .75f));
                float dipDegrees = live * (
                    DipBase[i] + PipAmplitude[i] * .43f *
                    Mathf.Sin(seconds * (FingerFrequency[i] * .81f) +
                        FingerPhase[i] + .56f) + relax * .17f);
                float mcpDegrees = live * (
                    McpBase[i] + .045f *
                    Mathf.Sin(seconds * .41f + FingerPhase[i]));
                // Actual angular limits for the damaged index and little.
                // No cyclic root rotation, even when adjusting the crown.
                pipDegrees = Mathf.Clamp(pipDegrees, 0f,
                    i == 3 ? .78f : (i == 0 ? 2.35f : 3.10f));
                dipDegrees = Mathf.Clamp(dipDegrees, 0f,
                    i == 3 ? .34f : 1.42f);
                mcpDegrees = Mathf.Clamp(mcpDegrees, 0f,
                    i == 3 ? .14f : (i == 0 ? .24f : .30f));
                Curl(FingerNames[i] + "_MCP", mcpDegrees);
                Curl(FingerNames[i] + "_PIP", pipDegrees);
                Curl(FingerNames[i] + "_DIP", dipDegrees);
            }

            // Thumb gently lags the index. Its independent response keeps
            // the hand natural without an exaggerated pinching gesture.
            float thumbResponse = Mathf.Clamp01((fingerResponse-.055f)/.945f);
            thumbResponse = thumbResponse*thumbResponse*
                (3f-2f*thumbResponse);
            float thumbLife = strength*(.14f+.86f*thumbResponse);
            Curl("Thumb_MCP", thumbLife *
                (.13f+.035f*Mathf.Sin(seconds*.36f+1.4f)));
            Curl("Thumb_IP", thumbLife *
                (.87f+.29f*Mathf.Sin(seconds*.47f+2.03f)));
        }

        public float FingerBendDegrees(string jointName)
        {
            if (!phalanxRest.ContainsKey(jointName)) return 0f;
            return Quaternion.Angle(phalanxRest[jointName],
                joints[jointName].localRotation);
        }
    }
}
