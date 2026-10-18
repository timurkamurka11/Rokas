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
            BedSleepSFXController gameFoley,
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
                // Match the previous approved Unity video: its hand pose
                // was sampled from the genuine SleepArm_InspectIdle FBX clip,
                // not from a raw bind/T-pose. Gently restore finger knuckles
                // toward their authored rest so root-only curls cannot break
                // the visible skin at finger bases.
                var baseJoints = instance.GetComponentsInChildren<Transform>(true);
                string[] knuckles = {
                    "Thumb_CMC", "Index_MCP", "Middle_MCP", "Ring_MCP", "Little_MCP"
                };
                var neutral = baseJoints.Where(x => knuckles.Contains(x.name))
                    .GroupBy(x => x.name)
                    .ToDictionary(g => g.Key, g => g.First().localRotation);
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                {
                    var clips = animator.runtimeAnimatorController != null
                        ? animator.runtimeAnimatorController.animationClips : null;
                    var inspect = clips == null ? null : clips.FirstOrDefault(
                        x => x != null && x.name.Contains("SleepArm_InspectIdle"));
                    if (inspect != null)
                    {
                        inspect.SampleAnimation(animator.gameObject,
                            inspect.length * .5f);
                        foreach (var joint in baseJoints)
                            if (neutral.TryGetValue(joint.name, out var rest))
                                joint.localRotation = Quaternion.Slerp(
                                    rest, joint.localRotation, .28f);
                    }
                    animator.enabled = false; // V10 is sole gameplay driver.
                }

                var joints = instance.GetComponentsInChildren<Transform>(true);
                var sourceWrist = joints.FirstOrDefault(x => x.name == "Arm_Wrist");
                var targetWrist = gameArm.Bones == null ? null
                    : gameArm.Bones.FirstOrDefault(x => x != null && x.name == "Wrist_L");
                var fingertip = joints.FirstOrDefault(x => x.name == "Middle_Tip");
                var sleeve = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.name == "Keiko_Homewear_Sleeve_Mesh");
                var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.name == "Keiko_Hand_Skin_Mesh");
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
                // Preserve the real albedo and UVs. The previous game view
                // showed a dark X at the wrist: source skin AO was 100%.
                // This disposable PBR instance attenuates AO only and adds
                // restrained warm epidermis; never modify authored assets.
                Material skinMaterial = new Material(skin.sharedMaterial);
                skinMaterial.name = "ROKAS_Argon_Gameplay_NaturalWarmSkin";
                if (skinMaterial.HasProperty("_Color"))
                    skinMaterial.SetColor("_Color", new Color(1f, .965f, .935f, 1f));
                if (skinMaterial.HasProperty("_OcclusionStrength"))
                    skinMaterial.SetFloat("_OcclusionStrength", .10f);
                if (skinMaterial.HasProperty("_BumpScale"))
                    skinMaterial.SetFloat("_BumpScale", .045f);
                if (skinMaterial.HasProperty("_GlossMapScale"))
                    skinMaterial.SetFloat("_GlossMapScale", .16f);
                if (skinMaterial.HasProperty("_Glossiness"))
                    skinMaterial.SetFloat("_Glossiness", .34f);
                if (skinMaterial.HasProperty("_Metallic"))
                    skinMaterial.SetFloat("_Metallic", 0f);
                skin.sharedMaterial = skinMaterial;
                Material oledMaterial = new Material(face.sharedMaterial);
                oledMaterial.name = "ROKAS_Argon_Gameplay_Russian_OLED";
                oledMaterial.SetTexture("_MainTex", russian.OledRenderTexture);
                face.material = oledMaterial;

                var follow = instance.AddComponent<RokasArgonFullHandFollower>();
                follow.Initialize(instance.transform, sourceWrist, targetWrist,
                    fingertip, sleeve, face, gameArm, gameOled, russian, camera,
                    gameFoley, skinMaterial, oledMaterial);
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
        private BedSleepSFXController foley;
        private Material ownedSkinMaterial, ownedOledMaterial;
        private Quaternion authoredRootRotation;
        private Vector3 lastCameraWatchNormal;
        private Vector3 lastCameraArmAxis;

        internal void Initialize(Transform root, Transform source, Transform destination,
            Transform tip, SkinnedMeshRenderer cloth, MeshRenderer oled,
            KeikoFPSArmRigController originalArm, SmartwatchOLEDPresenter oldPresenter,
            ArgonRussianSleepOledPresenter newPresenter, Camera cam,
            BedSleepSFXController gameplayFoley,
            Material skinMaterial, Material oledMaterial)
        {
            rigRoot = root; sourceWrist = source; targetWrist = destination;
            middleTip = tip; sleeve = cloth; watchFace = oled;
            arm = originalArm; oldOled = oldPresenter;
            russian = newPresenter; presentationCamera = cam;
            foley = gameplayFoley;
            ownedSkinMaterial = skinMaterial; ownedOledMaterial = oledMaterial;
            // V10 wrist bones have a different bind pose. Preserve the exact
            // authored Argon wrist/forearm roll sampled from InspectIdle.
            authoredRootRotation = root.rotation;
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
            // Reset every frame to the AUTHORED Argon pose. Never copy the
            // old V10 wrist rotation: that is what inverted the watch.
            // Translation follows the gameplay wrist without changing the
            // orientation of Argon's original 25-bone skeleton.
            rigRoot.rotation = authoredRootRotation;
            rigRoot.position += targetWrist.position - sourceWrist.position;

            Vector3 armAxis = middleTip.position - sleeve.bounds.center;
            if (armAxis.sqrMagnitude < .000001f)
                armAxis = middleTip.position - sourceWrist.position;
            if (armAxis.sqrMagnitude < .000001f) return;
            armAxis.Normalize();

            // The physical OLED lies OUTSIDE the wrist on the WATCH side.
            // Its mesh normal is not reliable for front/back on this FBX;
            // the previous camera faced the back of the hand. Use the real
            // watch position relative to Arm_Wrist to select the visible side.
            Vector3 front = Vector3.ProjectOnPlane(
                watchFace.bounds.center - sourceWrist.position, armAxis);
            MeshRenderer glass = rigRoot.GetComponentsInChildren<MeshRenderer>(true)
                .FirstOrDefault(x => x.name == "Watch_SapphireGlass");
            Vector3 lensOut = glass != null
                ? Vector3.ProjectOnPlane(
                    glass.bounds.center - sourceWrist.position, armAxis)
                : Vector3.zero;
            if (lensOut.sqrMagnitude > front.sqrMagnitude * .30f)
                front = lensOut;
            if (front.sqrMagnitude < .00000001f)
            {
                // Deterministic fallback for co-planar watch/wrist imports:
                // reverse the previous back-facing normal, not the wrist.
                front = -watchFace.transform.forward;
                front = Vector3.ProjectOnPlane(front, armAxis);
            }
            if (front.sqrMagnitude < .00000001f) return;
            front.Normalize();
            Vector3 up = Vector3.Cross(armAxis, front).normalized;
            Quaternion horizontalView = Quaternion.LookRotation(-front, up);
            Vector3 screenRight = horizontalView * Vector3.right;
            if (Vector3.Dot(screenRight, armAxis) < 0f)
            {
                up = -up;
                horizontalView = Quaternion.LookRotation(-front, up);
                screenRight = horizontalView * Vector3.right;
            }
            lastCameraWatchNormal = front;
            lastCameraArmAxis = armAxis;

            // Hero pose: sleeve extends off LEFT edge, watch centre-left,
            // fingers RIGHT. This matches the approved Argon preview.
            Vector3 heroWatchCenter = watchFace.bounds.center;
            presentationCamera.transform.SetPositionAndRotation(
                heroWatchCenter + screenRight * .040f + front * .48f,
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
                if (foley != null)
                {
                    if (state == KeikoFPSArmRigController.ArmClipState.Raise)
                    {
                        foley.PlaySleeveRaise();
                        foley.PlayOledPowerOn();
                    }
                    else if (state == KeikoFPSArmRigController.ArmClipState.Lower)
                        foley.PlaySleeveLower();
                }
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

        private void OnDestroy()
        {
            // Clone-only materials; preserve every imported FBX/PBR source.
            if (ownedSkinMaterial != null) Destroy(ownedSkinMaterial);
            if (ownedOledMaterial != null) Destroy(ownedOledMaterial);
        }
    }
}
