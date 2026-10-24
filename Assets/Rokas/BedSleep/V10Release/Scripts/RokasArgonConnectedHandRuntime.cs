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
            Mesh runtimeFingerCandidate = null;
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
                // Preserve original albedo and normals, but restore soft
                // skin micro-detail from the authored high-resolution maps.
                // Do not flatten the model with strong AO or glossy plastic.
                if (skinMaterial.HasProperty("_OcclusionStrength"))
                    skinMaterial.SetFloat("_OcclusionStrength", .135f);
                if (skinMaterial.HasProperty("_BumpScale"))
                    skinMaterial.SetFloat("_BumpScale", .062f);
                if (skinMaterial.HasProperty("_GlossMapScale"))
                    skinMaterial.SetFloat("_GlossMapScale", .16f);
                if (skinMaterial.HasProperty("_Glossiness"))
                    skinMaterial.SetFloat("_Glossiness", .305f);
                if (skinMaterial.HasProperty("_Metallic"))
                    skinMaterial.SetFloat("_Metallic", 0f);
                Mesh originalHandMesh=skin.sharedMesh;
                skin.sharedMaterial = skinMaterial;

                // V14 only changed transform angles, but the imported FBX
                // has finger-island weights mapped to other digit bones.
                // A moving bone therefore did NOT move its own finger skin.
                // Apply the already-validated, neutral-pose-preserving bindpose
                // retarget to a disposable runtime mesh before cosmetic edits.
                // No source FBX, animator, game camera or shader is changed.
                Transform[] originalSkinBones = skin.bones;
                bool fingerSkinRetargeted = false;
                int[][] retargetFingerIslands = null;
                try
                {
                    var report = ArgonFingerBindposeRetarget.ApplyCandidate(
                        skin, out runtimeFingerCandidate, out originalSkinBones);
                    fingerSkinRetargeted = true;
                    retargetFingerIslands = report.FingerVertices;
                    Debug.Log("[ROKAS HAND] Live finger skinning active: " +
                        report.RetargetedVertices + " vertices, " +
                        report.ExtraBoneSlots + " compensated slots; neutral drift=" +
                        report.MaxNeutralDisplacement.ToString("G6"));
                }
                catch (Exception bindError)
                {
                    // ApplyCandidate restores bones+mesh on failure itself.
                    // Do not let a failed optional finger remap hide the watch.
                    skin.sharedMesh = originalHandMesh;
                    skin.bones = originalSkinBones;
                    runtimeFingerCandidate = null;
                    Debug.LogWarning("[ROKAS HAND] Original skin kept: finger " +
                        "remap rejected: " + bindError.Message);
                }

                // Retain the V13 index/little seam improvements on top of the
                // live corrected skin if its topology matches exactly.
                // On a seam failure, keep the retargeted, animatable fingers.
                Mesh beforeSocket = skin.sharedMesh;
                Mesh socketMesh = null;
                try
                {
                    var socket = ArgonFingerRootSocketBlend.ApplyIndexLittleToClone(
                        skin, out socketMesh);
                    runtimeFingerCandidate = socketMesh;
                    if (fingerSkinRetargeted && beforeSocket != socketMesh)
                        UnityEngine.Object.Destroy(beforeSocket);
                    Debug.Log("[ROKAS HAND] Index/little seam blend retained: " +
                        socket.ModifiedVertices + " adjusted vertices");
                }
                catch (Exception geometryError)
                {
                    skin.sharedMesh = beforeSocket;
                    if (socketMesh != null) UnityEngine.Object.Destroy(socketMesh);
                    Debug.LogWarning("[ROKAS HAND] Safe finger socket fallback: " +
                        geometryError.Message);
                }
                Material oledMaterial = new Material(face.sharedMaterial);
                oledMaterial.name = "ROKAS_Argon_Gameplay_Russian_OLED";
                oledMaterial.SetTexture("_MainTex", russian.OledRenderTexture);
                face.material = oledMaterial;

                var follow = instance.AddComponent<RokasArgonFullHandFollower>();
                follow.Initialize(instance.transform, sourceWrist, targetWrist,
                    fingertip, sleeve, face, gameArm, gameOled, russian, camera,
                    gameFoley, skinMaterial, oledMaterial);
                follow.SetOwnedSkinCandidate(runtimeFingerCandidate);
                if (fingerSkinRetargeted)
                    follow.ConfigureFingerMotionProbe(skin, retargetFingerIslands);
                foreach (var t in joints) t.gameObject.layer = 30;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;
                follow.SnapAndFrame();
                // Reuse actual skinned palm surface and authored joints.
                // A failed ray-projection simply skips vascular detail;
                // no new fake skin mesh or change to the OLED / camera.
                ArgonDorsalVeinDetail.TryAttach(skin,joints,camera);

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
                if (runtimeFingerCandidate != null)
                    UnityEngine.Object.Destroy(runtimeFingerCandidate);
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
        private float visibilityVelocity;
        private BedSleepSFXController foley;
        private Material ownedSkinMaterial, ownedOledMaterial;
        private Mesh ownedSkinCandidate;
        // One-off Unity BakeMesh proof in the ACTUAL BED SCENE.
        // The previous diagnostic only showed the remap was accepted;
        // this proves whether live animated skin VERTICES really move.
        private SkinnedMeshRenderer fingerProbeSkin;
        private int[][] fingerProbeIslands;
        private Vector3[] fingerProbeBase;
        private float fingerProbeBegin = -1f;
        private bool fingerProbeFinished;
        internal void ConfigureFingerMotionProbe(
            SkinnedMeshRenderer skinned, int[][] islands)
        {
            fingerProbeSkin=skinned;
            fingerProbeIslands=islands;
            fingerProbeBase=null;
            fingerProbeBegin=-1f;
            fingerProbeFinished=false;
        }
        internal void SetOwnedSkinCandidate(Mesh candidate)
        {
            ownedSkinCandidate=candidate;
        }
        private Quaternion authoredRootRotation;
        private Vector3 authoredRootPosition;
        private Transform forearmRoot, forearmTwist, activeWrist;
        private Quaternion forearmRootRest, forearmTwistRest, wristRest;
        private ArgonLivingRig livingRig;
        private Vector3 heroCameraRight, heroCameraUp, heroCameraForward;
        private bool cameraFramed;
        private float interactingUntil;
        private Transform physicalCrown, physicalButton;
        private Quaternion crownNeutral;
        private Vector3 buttonNeutral;
        private float crownAngle, crownTargetAngle;
        private float crownAngularVelocity, buttonPress, buttonTarget;
        internal void RotatePhysicalCrown(int steps)
        {
            if (steps == 0) return;
            crownTargetAngle += Mathf.Clamp(steps, -12, 12) * 18f;
            interactingUntil = Time.unscaledTime + .32f;
        }
        internal void PressPhysicalButton()
        {
            buttonPress = 1f;
            buttonTarget = 0f;
            interactingUntil = Time.unscaledTime + .22f;
        }
        internal ArgonRussianSleepOledPresenter WatchScreen => russian;

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
            authoredRootPosition = root.position;
            forearmRoot = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Arm_ForearmRoot");
            forearmTwist = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Arm_ForearmTwist");
            activeWrist = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Arm_Wrist");
            if (forearmRoot == null || forearmTwist == null || activeWrist == null)
                throw new MissingReferenceException("Original Argon forearm/wrist rig is missing");
            forearmRootRest = forearmRoot.localRotation;
            forearmTwistRest = forearmTwist.localRotation;
            wristRest = activeWrist.localRotation;
            livingRig = new ArgonLivingRig(root.gameObject);
            // Watch hardware is separate from the original skin rig.
            // Its physical crown and upper button react without driving
            // old V10 wrist/hand pose or changing the camera.
            physicalCrown = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x.name.IndexOf("Watch_", StringComparison.Ordinal) >= 0 &&
                    (x.name.IndexOf("Crown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     x.name.IndexOf("DialKnob", StringComparison.OrdinalIgnoreCase) >= 0));
            physicalButton = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x != physicalCrown &&
                    x.name.StartsWith("Watch_", StringComparison.Ordinal) &&
                    (x.name.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     x.name.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0));
            if (physicalCrown != null) crownNeutral = physicalCrown.localRotation;
            if (physicalButton != null) buttonNeutral = physicalButton.localPosition;
            cameraFramed = false;
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

        internal void NotifyWatchInteraction()
        {
            interactingUntil = Time.unscaledTime + .55f;
        }

        private void RotateBoneInWorldAxis(Transform bone, Vector3 worldAxis, float degrees)
        {
            if (bone == null || Mathf.Abs(degrees) < .001f) return;
            Vector3 localAxis = bone.parent != null
                ? bone.parent.InverseTransformDirection(worldAxis)
                : worldAxis;
            bone.localRotation = Quaternion.AngleAxis(degrees, localAxis.normalized)
                * bone.localRotation;
        }

        internal void SnapAndFrame()
        {
            if (sourceWrist == null || presentationCamera == null || watchFace == null)
                return;

            // Framing is CAPTURED ONCE from the real source hero pose.
            // Previously it tracked the already-displaced wrist every frame
            // and cancelled the entry arc by following the moving hand.
            if (!cameraFramed)
            {
                rigRoot.SetPositionAndRotation(authoredRootPosition, authoredRootRotation);
                Vector3 armAxis = middleTip.position - sleeve.bounds.center;
                if (armAxis.sqrMagnitude < .000001f)
                    armAxis = middleTip.position - sourceWrist.position;
                if (armAxis.sqrMagnitude < .000001f) return;
                armAxis.Normalize();
                Vector3 front = Vector3.ProjectOnPlane(
                    watchFace.bounds.center - sourceWrist.position, armAxis);
                MeshRenderer glass = rigRoot.GetComponentsInChildren<MeshRenderer>(true)
                    .FirstOrDefault(x => x.name == "Watch_SapphireGlass");
                Vector3 lensOut = glass != null
                    ? Vector3.ProjectOnPlane(glass.bounds.center -
                        sourceWrist.position, armAxis) : Vector3.zero;
                if (lensOut.sqrMagnitude > front.sqrMagnitude * .30f)
                    front = lensOut;
                if (front.sqrMagnitude < .00000001f)
                    front = Vector3.ProjectOnPlane(
                        -watchFace.transform.forward, armAxis);
                if (front.sqrMagnitude < .00000001f) return;
                front.Normalize();
                Vector3 up = Vector3.Cross(armAxis, front).normalized;
                Quaternion view = Quaternion.LookRotation(-front, up);
                if (Vector3.Dot(view * Vector3.right, armAxis) < 0f)
                    view = Quaternion.LookRotation(-front, -up);
                Vector3 right = view * Vector3.right;
                presentationCamera.transform.SetPositionAndRotation(
                    watchFace.bounds.center + right * .035f + front * .355f,
                    view);
                // A 35-degree close-up makes the OLED materially larger
                // without changing the real watch mesh or source textures.
                presentationCamera.fieldOfView = 35f;
                presentationCamera.nearClipPlane = .012f;
                presentationCamera.farClipPlane = 10f;
                presentationCamera.cullingMask = 1 << 30;
                heroCameraRight = right;
                heroCameraUp = view * Vector3.up;
                heroCameraForward = view * Vector3.forward;
                cameraFramed = true;
            }

            // Start from an absolute unmodified pose every frame. NEVER
            // integrate additive wrist rotations or translation over time.
            rigRoot.SetPositionAndRotation(authoredRootPosition, authoredRootRotation);
            forearmRoot.localRotation = forearmRootRest;
            forearmTwist.localRotation = forearmTwistRest;
            activeWrist.localRotation = wristRest;
            bool lowering = arm.CurrentState ==
                KeikoFPSArmRigController.ArmClipState.Lower;
            var gesture = ArgonBedOrganicGesture.Evaluate(visibility, lowering);
            // The model remains HORIZONTAL as it rises in a soft shallow arc
            // from lower-left, then comes ~8 cm toward the viewer.
            rigRoot.position = authoredRootPosition +
                heroCameraRight * gesture.Horizontal +
                heroCameraUp * gesture.Vertical +
                heroCameraForward * gesture.Away;
            // Keep entry articulation subtle and separate between sections.
            // All bends return exactly to neutral on the final hero frame.
            RotateBoneInWorldAxis(forearmRoot, heroCameraRight,
                gesture.ProximalElbowDegrees * .26f);
            RotateBoneInWorldAxis(forearmRoot, heroCameraForward,
                gesture.ProximalSwivelDegrees * .28f);
            RotateBoneInWorldAxis(forearmTwist, heroCameraRight,
                gesture.ForearmPitchDegrees * .32f);
            RotateBoneInWorldAxis(forearmTwist, heroCameraForward,
                gesture.ForearmRollDegrees * .30f);
            RotateBoneInWorldAxis(activeWrist, heroCameraRight,
                gesture.WristPitchDegrees * .28f);
            RotateBoneInWorldAxis(activeWrist, heroCameraForward,
                gesture.WristRollDegrees * .29f);

            float response = Mathf.SmoothStep(0f, 1f, visibility);
            livingRig.NotifyAnimationPoseReset();
            // Wheel gestures should move the watch hardware, not rock
            // the wrist. Keep natural non-interaction breathing/fingers.
            livingRig.Apply(Time.unscaledTime, response, false,
                heroCameraRight, heroCameraUp,
                gesture.FingerResponse, lowering);

            if (physicalCrown != null)
            {
                crownAngle = Mathf.SmoothDampAngle(crownAngle,
                    crownTargetAngle, ref crownAngularVelocity,
                    .105f, 700f, Mathf.Min(Time.unscaledDeltaTime, .05f));
                physicalCrown.localRotation = crownNeutral *
                    Quaternion.AngleAxis(crownAngle, Vector3.right);
            }
            if (physicalButton != null)
            {
                buttonPress = Mathf.MoveTowards(buttonPress, buttonTarget,
                    Time.unscaledDeltaTime * 5f);
                physicalButton.localPosition = buttonNeutral +
                    Vector3.back * (.0018f * buttonPress);
            }
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

            // Continuous critically-damped visibility across V10 state
            // changes: unlike stateSeconds-based poses this NEVER jumps from
            // a partially raised hand to a hard-coded 100% on entering
            // TimeSelection, and never snaps when Lower begins.
            bool hidden=state==KeikoFPSArmRigController.ArmClipState.HiddenIdle ||
                state==KeikoFPSArmRigController.ArmClipState.ReturnIdle ||
                state==KeikoFPSArmRigController.ArmClipState.Lower;
            float target=hidden ? 0f : 1f;
            float smoothTime=hidden ? .32f : .37f;
            float delta=Mathf.Clamp(Time.unscaledDeltaTime,.001f,.05f);
            visibility=Mathf.SmoothDamp(visibility,target,
                ref visibilityVelocity,smoothTime,4f,delta);
            visibility=Mathf.Clamp01(visibility);
            if (Mathf.Abs(visibility-target)<.0003f)
            {
                visibility=target;
                visibilityVelocity=0f;
            }
            SnapAndFrame();
            ProbeFingerMeshMotion();
        }

        private void ProbeFingerMeshMotion()
        {
            if (fingerProbeFinished || fingerProbeSkin == null ||
                fingerProbeIslands == null || visibility < .985f)
                return;
            float now=Time.unscaledTime;
            if(fingerProbeBegin>=0f && now-fingerProbeBegin<2.8f)
                return;
            Mesh sample=new Mesh();
            try
            {
                fingerProbeSkin.BakeMesh(sample);
                var current=sample.vertices;
                if(fingerProbeBegin<0f)
                {
                    fingerProbeBase=current;
                    fingerProbeBegin=now;
                    return;
                }
                fingerProbeFinished=true;
                if(fingerProbeBase==null || current.Length!=fingerProbeBase.Length)
                    throw new InvalidOperationException(
                        "Skin vertex count changed during motion probe.");
                var isFinger=new bool[current.Length];
                foreach(var group in fingerProbeIslands)
                    foreach(int vertex in group)
                        if(vertex>=0 && vertex<isFinger.Length)
                            isFinger[vertex]=true;

                // Subtract average palm movement to distinguish actual
                // finger flexion from a global wrist/hand translation.
                Vector3 palmMotion=Vector3.zero;
                int palmCount=0;
                for(int i=0;i<current.Length;i+=13)
                    if(!isFinger[i])
                    {
                        palmMotion+=current[i]-fingerProbeBase[i];
                        palmCount++;
                    }
                if(palmCount>0)palmMotion/=palmCount;
                float threshold=Mathf.Max(1e-8f,
                    fingerProbeSkin.sharedMesh.bounds.size.magnitude*.00065f);
                string[] labels={"Thumb","Index","Middle","Ring","Little"};
                for(int d=0;d<fingerProbeIslands.Length;d++)
                {
                    int moving=0;
                    float peak=0f;
                    foreach(int v in fingerProbeIslands[d])
                    {
                        float movement=(current[v]-fingerProbeBase[v]-
                            palmMotion).magnitude;
                        if(movement>threshold)moving++;
                        peak=Mathf.Max(peak,movement);
                    }
                    string label=d<labels.Length ? labels[d] : d.ToString();
                    Debug.Log("[ROKAS FINGER MOTION PROOF] "+label+
                        " skinnedVerticesMoving="+moving+"/"+
                        fingerProbeIslands[d].Length+
                        " peak="+peak.ToString("G5")+
                        " threshold="+threshold.ToString("G5"));
                    if(moving<10)
                        Debug.LogWarning("[ROKAS FINGER MOTION PROOF] "+
                            label+" is visually STATIONARY: bone-to-skin "+
                            "animation path still needs investigation.");
                }
            }
            catch(Exception error)
            {
                fingerProbeFinished=true;
                Debug.LogWarning("[ROKAS FINGER MOTION PROOF] BakeMesh "+
                    "diagnostic unavailable: "+error.Message);
            }
            finally { Destroy(sample); }
        }

        private void OnDestroy()
        {
            // Clone-only materials; preserve every imported FBX/PBR source.
            if (ownedSkinCandidate != null) Destroy(ownedSkinCandidate);
            if (ownedSkinMaterial != null) Destroy(ownedSkinMaterial);
            if (ownedOledMaterial != null) Destroy(ownedOledMaterial);
        }
    }
}
