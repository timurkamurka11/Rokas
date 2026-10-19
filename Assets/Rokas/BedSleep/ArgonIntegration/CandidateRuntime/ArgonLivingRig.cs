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
        // Small but *visible* movement on real skinned geometry.
        // Earlier V14 oscillated by <0.6 degrees, below pixel visibility.
        // The pinky and index remain conservative at their socket collars.
        private static readonly float[] PipBase = {
            3.5f, 5.8f, 5.0f, 2.0f
        };
        private static readonly float[] McpBase = {
            .52f, 1.28f, 1.05f, .22f
        };
        private static readonly float[] PipAmplitude = {
            1.65f, 2.45f, 2.05f, .94f
        };
        private static readonly float[] FingerFrequency = {
            .57f, .39f, .46f, .31f
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
            // Wrist changes angle locally. The whole forearm and watch
            // anchor remain stable, preventing a visible screen shake.
            float wristSettle=Mathf.Sin(seconds*.26f+.8f);
            previousWristOverlay = Quaternion.Euler(.32f * life,
                .16f * wristSettle * strength, .43f * life);
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
                float lagged=Mathf.Clamp01(
                    (fingerResponse-i*.065f)/(1f-i*.065f));
                float response=lagged*lagged*(3f-2f*lagged);
                float live=strength*(.24f+.76f*response);
                // Forearm raises first; each digit follows after a small
                // offset. Arriving fingers relax independently, rather
                // than rocking the entire wrist in a periodic sine wave.
                float arrival=Mathf.Sin(response*Mathf.PI);
                float primary=Mathf.Sin(
                    seconds*FingerFrequency[i]+FingerPhase[i]);
                float secondary=Mathf.Sin(
                    seconds*(FingerFrequency[i]*.47f)+
                    FingerPhase[i]*1.57f+.56f);
                float episode=Mathf.Max(0f,Mathf.Sin(
                    seconds*.32f+FingerPhase[i]*.73f));
                episode=episode*episode*episode;
                float asymmetry=.65f*primary+.35f*secondary;
                float settle=arrival*(isLowering ? -.68f : .82f);
                float pipDegrees=live*(
                    PipBase[i]+PipAmplitude[i]*asymmetry+
                    (i==3 ? .30f : .94f)*episode+settle);
                // Human DIP flexion follows PIP at a smaller angle. The
                // slight delayed term stops the fingertips from moving
                // as identical rigid rods.
                float dipDegrees=pipDegrees*.54f+
                    .25f*live*Mathf.Sin(
                        seconds*(FingerFrequency[i]*.82f)+
                        FingerPhase[i]+.48f);
                float mcpDegrees=live*(
                    McpBase[i]+.19f*episode+
                    .13f*secondary);
                // Bound separate source finger islands at the MCP. The
                // pinky/index socket remains less mobile than middle/ring.
                pipDegrees=Mathf.Clamp(pipDegrees,0f,
                    i==3 ? 3.75f : (i==0 ? 6.15f : 9.0f));
                dipDegrees=Mathf.Clamp(dipDegrees,0f,
                    i==3 ? 2.2f : (i==0 ? 3.7f : 5.4f));
                mcpDegrees=Mathf.Clamp(mcpDegrees,0f,
                    i==3 ? .48f : (i==0 ? .90f : 1.7f));
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
                (.42f+.20f*Mathf.Sin(seconds*.38f+1.4f)));
            Curl("Thumb_IP", thumbLife *
                (2.65f+1.02f*Mathf.Sin(seconds*.45f+2.03f)));
        }

        public float FingerBendDegrees(string jointName)
        {
            if (!phalanxRest.ContainsKey(jointName)) return 0f;
            return Quaternion.Angle(phalanxRest[jointName],
                joints[jointName].localRotation);
        }
    }
}
