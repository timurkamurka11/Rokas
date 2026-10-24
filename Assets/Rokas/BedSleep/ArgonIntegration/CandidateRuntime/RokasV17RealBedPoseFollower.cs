using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Existing ROKAS bed only; independent native V17 rig in Editor Play Mode.
    /// Runtime V16 controls the wrist/watch, OLED, sleeve and game logic.
    /// This is a reversible visual correction, NOT a permanent skinning rebuild.
    /// </summary>
    [DefaultExecutionOrder(32005)]
    public sealed class RokasV17RealBedPoseFollower : MonoBehaviour
    {
        private Transform targetWrist, candidateWrist, candidateRoot;
        private SkinnedMeshRenderer originalHand, candidateHand;
        private AnimationClip grip, index, living;
        private float scale, gestureStart, nextMessage;
        private int gesture;
        private bool ready, oldHidden, legacyInput = true;
        private Renderer[] legacyVisuals, candidateVisuals;
        private bool[] savedLegacyVisibility;

        private readonly Dictionary<Transform, Quaternion> restRotation =
            new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> smoothedRotation =
            new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<int, float> smoothedShapes =
            new Dictionary<int, float>();
        private readonly List<Transform> fingerJoints = new List<Transform>();
        private readonly List<Transform> rigidWristChain = new List<Transform>();
        private readonly List<Quaternion> rigidRestRotation = new List<Quaternion>();
        private readonly List<Vector3> rigidRestPosition = new List<Vector3>();

        public void Configure(Transform oldWrist, Transform newRoot, Transform newWrist,
            SkinnedMeshRenderer oldSkin, SkinnedMeshRenderer newSkin,
            AnimationClip gripClip, AnimationClip indexClip, AnimationClip livingClip,
            float relativeScale, Renderer[] sourceRenderers, Renderer[] candidateRenderers)
        {
            if (oldWrist == null || newRoot == null || newWrist == null ||
                oldSkin == null || newSkin == null ||
                gripClip == null || indexClip == null || livingClip == null ||
                relativeScale <= 0f || float.IsNaN(relativeScale) ||
                sourceRenderers == null || sourceRenderers.Length < 3 ||
                candidateRenderers == null || candidateRenderers.Length < 3 ||
                sourceRenderers.Any(r => r == null) ||
                candidateRenderers.Any(r => r == null))
                throw new InvalidOperationException("Missing native V17, V16 or valid wrist scale");
            targetWrist = oldWrist;
            candidateRoot = newRoot;
            candidateWrist = newWrist;
            originalHand = oldSkin;
            candidateHand = newSkin;
            grip = gripClip; index = indexClip; living = livingClip;
            scale = relativeScale;
            // One coherent native V17 rig for skin + sleeve + nails.
            // Original V16 watch/OLED/timer remain untouched.
            legacyVisuals = sourceRenderers;
            candidateVisuals = candidateRenderers;
            savedLegacyVisibility = sourceRenderers.Select(r => r.enabled).ToArray();

            // Bind-neutral frame: before any gesture, capture the *native*
            // transforms. Never transfer V16 joint rotations to V17 bindposes.
            living.SampleAnimation(candidateRoot.gameObject, 0f);
            foreach (var tr in candidateRoot.GetComponentsInChildren<Transform>(true))
            {
                if (IsFingerJoint(tr.name))
                {
                    fingerJoints.Add(tr);
                    restRotation.Add(tr, tr.localRotation);
                    smoothedRotation.Add(tr, tr.localRotation);
                }
                else if (tr != candidateRoot && IsStableForearmJoint(tr.name))
                {
                    rigidWristChain.Add(tr);
                    rigidRestRotation.Add(tr.localRotation);
                    rigidRestPosition.Add(tr.localPosition);
                }
            }
            if (fingerJoints.Count < 14 || rigidWristChain.Count < 3)
                throw new InvalidOperationException("V17 anatomical bone chains incomplete");
            for (int i = 0; i < candidateHand.sharedMesh.blendShapeCount; i++)
            {
                candidateHand.SetBlendShapeWeight(i, 0f);
                smoothedShapes[i] = 0f;
            }

            // No runtime mesh edits, triangle clipping or fake cuff extension:
            // the FBX V17 sleeve, nails and hand share native transforms.
            gestureStart = Time.unscaledTime;
            gesture = 0;
            candidateHand.enabled = true;
            ready = true;
            Debug.Log("[ROKAS V17 REAL BED V18] Stable native bone limits, smooth " +
                "morphs and native-sleeve unity active; existing Russian OLED/watch untouched. " +
                "F7=Living F8=Safe Grip F9=Safe Index");
        }

        private static bool IsFingerJoint(string name)
        {
            if (!(name.StartsWith("Thumb_", StringComparison.Ordinal) ||
                  name.StartsWith("Index_", StringComparison.Ordinal) ||
                  name.StartsWith("Middle_", StringComparison.Ordinal) ||
                  name.StartsWith("Ring_", StringComparison.Ordinal) ||
                  name.StartsWith("Little_", StringComparison.Ordinal)))
                return false;
            return name.EndsWith("_CMC", StringComparison.Ordinal) ||
                   name.EndsWith("_MCP", StringComparison.Ordinal) ||
                   name.EndsWith("_IP", StringComparison.Ordinal) ||
                   name.EndsWith("_PIP", StringComparison.Ordinal) ||
                   name.EndsWith("_DIP", StringComparison.Ordinal);
        }

        private static bool IsStableForearmJoint(string name)
        {
            return name == "Arm_ForearmRoot" || name == "Arm_ForearmTwist" ||
                   name == "Arm_Wrist" || name == "Hand_Palm";
        }

        // Anatomical safety envelope for the inherited rough V17 FBX weights:
        // full imported 90-degree joint curls gave dislocated-looking digits.
        // These limits also prevent an edited FBX's broken keyframe from
        // abruptly driving the live game into an implausible pose.
        private static float AllowedAngle(string name)
        {
            if (name.StartsWith("Thumb_", StringComparison.Ordinal)) return 24f;
            if (name.EndsWith("_MCP", StringComparison.Ordinal)) return 30f;
            if (name.EndsWith("_PIP", StringComparison.Ordinal)) return 35f;
            if (name.EndsWith("_DIP", StringComparison.Ordinal)) return 26f;
            return 28f;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            if (targetWrist == null || candidateWrist == null ||
                candidateRoot == null || originalHand == null || candidateHand == null)
            {
                Restore(); ready = false; return;
            }
            try
            {
                if (legacyInput)
                {
                    try
                    {
                        if (Input.GetKeyDown(KeyCode.F7)) SetGesture(0);
                        if (Input.GetKeyDown(KeyCode.F8)) SetGesture(1);
                        if (Input.GetKeyDown(KeyCode.F9)) SetGesture(2);
                    }
                    catch (InvalidOperationException)
                    {
                        legacyInput = false; // new Input System project: safe default
                    }
                }

                AnimationClip clip = gesture == 1 ? grip : (gesture == 2 ? index : living);
                float authoredDuration = gesture == 1 ? 95f / 30f :
                    (gesture == 2 ? 63f / 30f : 94f / 30f);
                authoredDuration = Mathf.Min(clip.length, authoredDuration);
                float elapsed = Mathf.Max(0f, Time.unscaledTime - gestureStart);
                // Living: cosine mirrored cycle = position AND velocity agree at
                // the loop boundary, unlike abrupt Replay(3.13s -> 0s).
                float t;
                if (gesture == 0)
                {
                    float cycle = Mathf.Repeat(elapsed / 5.6f, 1f);
                    t = authoredDuration * .5f *
                        (1f - Mathf.Cos(cycle * Mathf.PI * 2f));
                }
                else
                {
                    t = Mathf.Min(elapsed, authoredDuration);
                    if (elapsed > authoredDuration + .5f) SetGesture(0);
                }

                candidateRoot.localPosition = Vector3.zero;
                candidateRoot.localRotation = Quaternion.identity;
                candidateRoot.localScale = Vector3.one * scale;
                clip.SampleAnimation(candidateRoot.gameObject, t);

                // Do not use animation data from the candidate forearm/wrist:
                // original ROKAS already animates the watch and sleeve there.
                for (int i = 0; i < rigidWristChain.Count; i++)
                {
                    rigidWristChain[i].localRotation = rigidRestRotation[i];
                    rigidWristChain[i].localPosition = rigidRestPosition[i];
                }

                float alpha = 1f - Mathf.Exp(
                    -Mathf.Min(Time.unscaledDeltaTime, .06f) * 12f);
                foreach (var joint in fingerJoints)
                {
                    Quaternion initial = restRotation[joint];
                    Quaternion target = Quaternion.RotateTowards(
                        initial, joint.localRotation, AllowedAngle(joint.name));
                    Quaternion safeTarget = Quaternion.Slerp(initial, target, .85f);
                    Quaternion value = Quaternion.Slerp(
                        smoothedRotation[joint], safeTarget, alpha);
                    smoothedRotation[joint] = value;
                    joint.localRotation = value;
                }

                for (int i = 0; i < candidateHand.sharedMesh.blendShapeCount; i++)
                {
                    string shape = candidateHand.sharedMesh.GetBlendShapeName(i);
                    // The rough morph sources are useful but must not drive
                    // sharp knuckle bulges, ripped nailbeds or intersecting skin.
                    float cap = shape.StartsWith("Index_", StringComparison.Ordinal)
                        ? 15f : (shape.StartsWith("Little_", StringComparison.Ordinal)
                            ? 12f : 10f);
                    float target = Mathf.Clamp(
                        candidateHand.GetBlendShapeWeight(i) * .30f, 0f, cap);
                    float value = Mathf.Lerp(smoothedShapes[i], target, alpha);
                    smoothedShapes[i] = value;
                    candidateHand.SetBlendShapeWeight(i, value);
                }

                Quaternion adjust = targetWrist.rotation *
                    Quaternion.Inverse(candidateWrist.rotation);
                candidateRoot.rotation = adjust * candidateRoot.rotation;
                candidateRoot.position += targetWrist.position - candidateWrist.position;
                if (!oldHidden)
                {
                    for (int i = 0; i < legacyVisuals.Length; ++i)
                        if (legacyVisuals[i] != null) legacyVisuals[i].enabled = false;
                    oldHidden = true;
                }
                foreach (var renderer in candidateVisuals)
                    if (renderer != null) renderer.enabled = true;

                if (Time.unscaledTime > nextMessage)
                {
                    nextMessage = Time.unscaledTime + 30f;
                    Debug.Log("[ROKAS V17 REAL BED V18 ACTIVE] smoothed finger rotations, " +
                        "capped morphs and coherent native sleeve; original watch retained");
                }
            }
            catch (Exception error)
            {
                Debug.LogError("[ROKAS V17 V18] Candidate failed; original skin restored: " +
                    error);
                Restore(); ready = false;
                if (candidateRoot != null) candidateRoot.gameObject.SetActive(false);
            }
        }

        private void SetGesture(int value)
        {
            gesture = value;
            gestureStart = Time.unscaledTime;
        }

        private void Restore()
        {
            if (oldHidden && legacyVisuals != null)
            {
                for (int i = 0; i < legacyVisuals.Length; i++)
                    if (legacyVisuals[i] != null)
                        legacyVisuals[i].enabled = savedLegacyVisibility[i];
            }
            oldHidden = false;
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
