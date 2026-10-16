#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Stages only the REAL Argon skinned hand and seven physical watch meshes.
    /// Keeps the authored geometry, skin weights, UVs and PNG materials intact.
    /// Corrects the 2D placement through a rigid camera-relative presentation
    /// track; never generates a replacement hand/watch or alters source FBX.
    /// </summary>
    internal sealed class RokasArgonHorizontalHeroPose
    {
        private static readonly string[] Knuckles = {
            "Thumb_CMC", "Index_MCP", "Middle_MCP", "Ring_MCP", "Little_MCP"
        };
        private static readonly string[] ArmBones = {
            "Arm_ForearmRoot", "Arm_ForearmTwist", "Arm_Wrist"
        };
        private readonly Dictionary<string, Transform> joints;
        private readonly Dictionary<string, Quaternion> neutralRotations;
        private readonly Dictionary<string, Quaternion> heroRotations;
        private readonly Dictionary<string, Vector3> heroPositions;
        private readonly Transform root;
        private readonly Vector3 originalRootPosition;
        private readonly SkinnedMeshRenderer sleeve;
        private readonly MeshRenderer oled;
        private readonly Transform fingerTip;
        private Vector3 right, up;
        public Camera Camera { get; private set; }
        public Vector3 Right => right;
        public Vector3 Up => up;
        public Vector3 WatchCenter => oled.bounds.center;

        internal RokasArgonHorizontalHeroPose(GameObject prefabInstance)
        {
            root = prefabInstance.transform;
            originalRootPosition = root.position;
            var hierarchy = root.GetComponentsInChildren<Transform>(true);
            joints = hierarchy.GroupBy(x => x.name).ToDictionary(
                x => x.Key, x => x.First());
            foreach (string n in Knuckles.Concat(ArmBones).Concat(new[] {"Middle_Tip"}))
                if (!joints.ContainsKey(n))
                    throw new InvalidOperationException("Authentic Argon rig missing " + n);
            neutralRotations = Knuckles.ToDictionary(
                n => n, n => joints[n].localRotation);
            heroRotations = new Dictionary<string, Quaternion>();
            heroPositions = new Dictionary<string, Vector3>();
            sleeve = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(x => x.name == "Keiko_Homewear_Sleeve_Mesh");
            oled = root.GetComponentsInChildren<MeshRenderer>(true)
                .Single(x => x.name == "Watch_OLED_Display");
            fingerTip = joints["Middle_Tip"];
        }

        internal void CaptureHeroPose()
        {
            foreach (string n in ArmBones)
            {
                heroRotations[n] = joints[n].localRotation;
                heroPositions[n] = joints[n].localPosition;
            }
        }

        internal void ConfigureCamera(Camera camera, Vector3 oledFrontNormal,
            float distanceMeters = .57f)
        {
            Camera = camera;
            var armAxis = (fingerTip.position - sleeve.bounds.center).normalized;
            var normal = Vector3.ProjectOnPlane(oledFrontNormal, armAxis).normalized;
            if (armAxis.sqrMagnitude < .9f || normal.sqrMagnitude < .9f)
                throw new InvalidOperationException("Argon sleeve->finger axis or OLED normal invalid");
            // Screen +X = authored sleeve -> fingertip; +Y = upright.
            // Unity camera basis: cameraRight = Cross(cameraUp, cameraForward).
            // Cross(armAxis, normal) makes right point FROM cuff TO fingertips.
            var cameraUp = Vector3.Cross(armAxis, normal).normalized;
            camera.fieldOfView = 38f;
            camera.transform.rotation = Quaternion.LookRotation(-normal, cameraUp);
            right = camera.transform.right;
            up = camera.transform.up;
            if (Vector3.Dot(armAxis, right) < .96f)
                throw new InvalidOperationException("Horizontal frame reversed: sleeve must be LEFT");
            // The watch sits slightly left of screen center; fingers go right.
            Vector3 target = oled.bounds.center + right * .040f;
            camera.transform.position = target + normal * distanceMeters;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 10f;
            Debug.Log("[ARGON HORIZONTAL] axis=" + armAxis +
                " cameraRight=" + right + " OLED=" + oled.bounds.center +
                " tip=" + fingerTip.position + " sleeve=" + sleeve.bounds.center);
        }

        /// <summary>
        /// Imported Argon clips animate ONLY the MCP/CMC finger joints, not
        /// the PIP/DIP phalanges; their 13-32° base-only curls look broken.
        /// Preserve genuine bone weights and gentle motion while capping
        /// knuckle excursion relative to original untouched GLB rest pose.
        /// </summary>
        internal void RestoreNaturalAnatomy()
        {
            foreach (var n in Knuckles)
            {
                var joint = joints[n];
                Quaternion rest = neutralRotations[n];
                joint.localRotation = Quaternion.Slerp(
                    rest, joint.localRotation, .28f);
                if (Quaternion.Angle(rest, joint.localRotation) > 11f)
                    throw new InvalidOperationException(
                        "Original Argon " + n + " still bends >11 degrees");
            }
            foreach (string n in ArmBones)
            {
                // Original clips still add subtle arm movement, but cannot
                // roll the watch edge-on or turn the forearm vertical.
                joints[n].localRotation = Quaternion.Slerp(
                    heroRotations[n], joints[n].localRotation, .12f);
                joints[n].localPosition = Vector3.Lerp(
                    heroPositions[n], joints[n].localPosition, .12f);
            }
        }

        internal void SetPresentation(float horizontalProgress,
            float upOffsetMeters = 0f)
        {
            // progress 0: completely outside on the LEFT.
            // progress 1: reference hero composition, watch near center.
            horizontalProgress = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(horizontalProgress));
            root.position = originalRootPosition +
                right * Mathf.Lerp(-.65f, 0f, horizontalProgress) +
                up * upOffsetMeters;
        }

        internal Vector3 SleeveScreen() =>
            Camera.WorldToViewportPoint(sleeve.bounds.center);
        internal Vector3 WatchScreen() =>
            Camera.WorldToViewportPoint(oled.bounds.center);
        internal Vector3 FingertipScreen() =>
            Camera.WorldToViewportPoint(fingerTip.position);
    }
}
#endif
