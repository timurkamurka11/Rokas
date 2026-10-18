using System;
using System.Linq;
using UnityEngine;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// Actual bed-click compositor, not an editor preview. Loads the COMPLETE
    /// authentic Argon hand/25-bone sleeve/nails/physical seven-piece watch.
    /// Keeps the old V10 controllers alive for existing save, sleep duration,
    /// input and audio, but does not mix V10 rendered geometry with Argon.
    /// </summary>
    internal static class RokasArgonConnectedHandRuntime
    {
        private const string AssetResource =
            "BedSleep/ArgonFullHandWatch/PF_Argon_Keiko_Hand_Watch_Preview";

        internal static bool TryInstall(
            KeikoFPSArmRigController gameArm, Smartwatch3DController oldWatch,
            SmartwatchOLEDPresenter gameOled, Camera camera,
            out MeshRenderer physicalOled, out MeshCollider physicalCollider)
        {
            physicalOled = null;
            physicalCollider = null;
            if (gameArm == null || oldWatch == null || gameOled == null || camera == null)
                return false;

            GameObject prefab = Resources.Load<GameObject>(AssetResource);
#if UNITY_EDITOR
            // Also work immediately inside the original Unity project if the
            // Resources copy has not yet been installed. No QA toggle/scene.
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Rokas/BedSleep/ArgonCandidate/" +
                    "PF_Argon_Keiko_Hand_Watch_Preview.prefab");
#endif
            if (prefab == null)
            {
                Debug.LogWarning("[ROKAS Bed Sleep] Authentic full Argon watch/hand " +
                    "prefab missing. Preserving the playable old V10 visuals.");
                return false;
            }

            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, gameArm.transform.parent);
                instance.name = "ROKAS_Argon_Complete_Hand_Watch_InGame";
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                    animator.enabled = false; // V10 gameplay remains sole animation authority.

                var joints = instance.GetComponentsInChildren<Transform>(true);
                var sourceWrist = joints.FirstOrDefault(x => x.name == "Arm_Wrist");
                var targetWrist = gameArm.Bones == null ? null
                    : gameArm.Bones.FirstOrDefault(x => x != null && x.name == "Wrist_L");
                var fingertip = joints.FirstOrDefault(x => x.name == "Middle_Tip");
                var sleeve = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.name == "Keiko_Homewear_Sleeve_Mesh");
                var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.name == "Keiko_Skin_Mesh");
                var face = instance.GetComponentsInChildren<MeshRenderer>(true)
                    .FirstOrDefault(x => x.name == "Watch_OLED_Display");
                var watchParts = instance.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(x => x.name.StartsWith("Watch_", StringComparison.Ordinal)).ToArray();
                if (sourceWrist == null || targetWrist == null || fingertip == null ||
                    sleeve == null || skin == null || face == null ||
                    watchParts.Length < 6)
                    throw new InvalidOperationException(
                        "Complete authentic Argon mesh, sleeve, joints or physical watch not found");

                MeshFilter faceFilter = face.GetComponent<MeshFilter>();
                if (faceFilter == null || faceFilter.sharedMesh == null)
                    throw new InvalidOperationException("Physical Argon OLED source mesh missing");
                var hit = face.GetComponent<MeshCollider>();
                if (hit == null) hit = face.gameObject.AddComponent<MeshCollider>();
                hit.sharedMesh = faceFilter.sharedMesh;

                // Instantiate the original Russian, animated 512x512 moon/OLED
                // renderer on the REAL imported face UV, not the V10 English HUD.
                var russian = instance.AddComponent<ArgonRussianSleepOledPresenter>();
                russian.InitializeOled();
                russian.ConfigureTime(gameOled.CurrentHour, gameOled.CurrentMinute,
                    gameOled.SelectedHalfHours);
                russian.SetConfirmedState(gameOled.IsConfirmed);
                Material oledMaterial = new Material(face.sharedMaterial);
                oledMaterial.name = "ROKAS_Argon_Gameplay_Russian_OLED";
                oledMaterial.SetTexture("_MainTex", russian.OledRenderTexture);
                face.material = oledMaterial;

                var follow = instance.AddComponent<RokasArgonFullHandFollower>();
                follow.Initialize(instance.transform, sourceWrist, targetWrist,
                    fingertip, sleeve, face, gameArm, gameOled, russian, camera);
                foreach (var t in joints) t.gameObject.layer = 30;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;
                follow.SnapAndFrame();

                // Only NOW that the complete replacement is initialized hide
                // every old skinned V10 hand/sleeve/nail/watch mesh. Their
                // controllers, input, sounds, timers and colliders remain alive.
                foreach (var renderer in gameArm.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;
                foreach (var renderer in oldWatch.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;
                physicalOled = face;
                physicalCollider = hit;
                Debug.Log("[ROKAS Bed Sleep] FULL Argon hand, sleeve, titanium watch, " +
                    "strap and Russian OLED ACTIVE in ordinary game. Left-entry POV.");
                return true;
            }
            catch (Exception ex)
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
                Debug.LogError("[ROKAS Bed Sleep] Complete Argon model unavailable: " +
                    ex.Message + ". Old visuals remain active.");
                return false;
            }
        }
    }

    /// <summary>
    /// Fixed left-to-right bedroom framing. The original V10 wrist is used
    /// ONLY as an invisible gameplay pose driver; there is no old skin graft.
    /// Camera follows the unoffset wrist so the source hand really enters
    /// from the LEFT rather than drifting upward from below.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class RokasArgonFullHandFollower : MonoBehaviour
    {
        private Transform rigRoot, sourceWrist, targetWrist, middleTip;
        private SkinnedMeshRenderer sleeve;
        private MeshRenderer watchFace;
        private KeikoFPSArmRigController arm;
        private SmartwatchOLEDPresenter oldOled;
        private ArgonRussianSleepOledPresenter russian;
        private Camera presentationCamera;
        private KeikoFPSArmRigController.ArmClipState previousState;
        private float stateSeconds;
        private float visibility;
        private Vector3 sleeveAxis;
        private Vector3 faceNormal;

        internal void Initialize(Transform root, Transform source, Transform destination,
            Transform tip, SkinnedMeshRenderer cloth, MeshRenderer oled,
            KeikoFPSArmRigController originalArm, SmartwatchOLEDPresenter oldPresenter,
            ArgonRussianSleepOledPresenter newPresenter, Camera cam)
        {
            rigRoot = root; sourceWrist = source; targetWrist = destination;
            middleTip = tip; sleeve = cloth; watchFace = oled;
            arm = originalArm; oldOled = oldPresenter;
            russian = newPresenter; presentationCamera = cam;
            previousState = arm.CurrentState;
            stateSeconds = 0f;
            visibility = 0f;
        }

        private void Update()
        {
            if (russian == null || oldOled == null) return;
            if (russian.CurrentHour != oldOled.CurrentHour ||
                russian.CurrentMinute != oldOled.CurrentMinute ||
                russian.SelectedHalfHours != oldOled.SelectedHalfHours)
                russian.ConfigureTime(oldOled.CurrentHour, oldOled.CurrentMinute,
                    oldOled.SelectedHalfHours);
            if (russian.IsConfirmed != oldOled.IsConfirmed)
                russian.SetConfirmedState(oldOled.IsConfirmed);
        }

        internal void SnapAndFrame()
        {
            if (sourceWrist == null || targetWrist == null || presentationCamera == null)
                return;
            // Align the authentic skeleton to the invisible game driver.
            // No original V10 bindpose is assigned to the Argon mesh.
            Quaternion correction = targetWrist.rotation *
                Quaternion.Inverse(sourceWrist.rotation);
            rigRoot.rotation = correction * rigRoot.rotation;
            rigRoot.position += targetWrist.position - sourceWrist.position;

            Vector3 armAxis = middleTip.position - sleeve.bounds.center;
            if (armAxis.sqrMagnitude < 0.000001f)
                armAxis = middleTip.position - sourceWrist.position;
            armAxis.Normalize();

            MeshFilter mf = watchFace.GetComponent<MeshFilter>();
            Vector3 normal = watchFace.transform.forward;
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
            {
                var normals = mf.sharedMesh.normals;
                Vector3 average = Vector3.zero;
                for (int i = 0; i < normals.Length; i += Mathf.Max(1, normals.Length / 64))
                    average += normals[i];
                if (average.sqrMagnitude > 0.00001f)
                    normal = watchFace.transform.TransformDirection(average.normalized);
            }
            normal = Vector3.ProjectOnPlane(normal, armAxis).normalized;
            if (normal.sqrMagnitude < 0.1f) normal = Vector3.ProjectOnPlane(
                watchFace.transform.forward, armAxis).normalized;
            if (normal.sqrMagnitude < 0.1f) return;
            Vector3 up = Vector3.Cross(armAxis, normal).normalized;
            Quaternion horizontalView = Quaternion.LookRotation(-normal, up);
            Vector3 screenRight = horizontalView * Vector3.right;
            if (Vector3.Dot(screenRight, armAxis) < 0f)
            {
                // Never flip the sleeve/fingers or present an upside-down arm.
                up = -up;
                horizontalView = Quaternion.LookRotation(-normal, up);
                screenRight = horizontalView * Vector3.right;
            }

            // Hero pose: sleeve extends off LEFT edge, watch centre-left,
            // fingers RIGHT. This matches the approved Argon preview.
            Vector3 heroWatchCenter = watchFace.bounds.center;
            presentationCamera.transform.SetPositionAndRotation(
                heroWatchCenter + screenRight * .040f + normal * .57f,
                horizontalView);
            presentationCamera.fieldOfView = 38f;
            presentationCamera.nearClipPlane = .01f;
            presentationCamera.farClipPlane = 10f;
            presentationCamera.cullingMask = 1 << 30;

            float smooth = visibility * visibility *
                (3f - 2f * visibility);
            // The camera was already framed at the final, unshifted wrist.
            // Move ONLY the model left to reveal it organically from sleeve.
            float slide = Mathf.Lerp(-.79f, 0f, smooth);
            float organic = Mathf.Sin(Time.unscaledTime * 1.35f) *
                .0017f * smooth;
            rigRoot.position += screenRight * slide +
                (horizontalView * Vector3.up) * organic;
        }

        private void LateUpdate()
        {
            if (arm == null) return;
            var state = arm.CurrentState;
            if (state != previousState)
            {
                previousState = state;
                stateSeconds = 0f;
            }
            else
                stateSeconds += Time.unscaledDeltaTime;

            switch (state)
            {
                case KeikoFPSArmRigController.ArmClipState.HiddenIdle:
                case KeikoFPSArmRigController.ArmClipState.ReturnIdle:
                    visibility = 0f;
                    break;
                case KeikoFPSArmRigController.ArmClipState.Raise:
                    visibility = Mathf.Clamp01(stateSeconds / 1.05f);
                    break;
                case KeikoFPSArmRigController.ArmClipState.Lower:
                    visibility = 1f - Mathf.Clamp01(stateSeconds / .90f);
                    break;
                default:
                    visibility = 1f;
                    break;
            }
            SnapAndFrame();
        }
    }
}
