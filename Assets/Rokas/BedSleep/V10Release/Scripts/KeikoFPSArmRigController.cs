using System;
using UnityEngine;

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// V6 Authoritative Skinned Arm Rig Controller.
    /// V6-11 Resolution: Eliminates Animator vs. ApplyPose() bone overwrite conflicts via explicit
    /// PoseDriverMode selection. When Animator is active in PlayMode, Animator evaluates the base
    /// .anim clips and LateUpdate applies only additive secondary breathing/crown recoil deltas.
    /// When in EditMode or DeterministicCurveDriven mode, TickAnimation evaluates the exact curve pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeikoFPSArmRigController : MonoBehaviour
    {
        public enum ArmClipState
        {
            HiddenIdle = 0,
            Raise = 1,
            WristTurn = 2,
            InspectIdle = 3,
            CrownFeedback = 4,
            Confirm = 5,
            Lower = 6,
            ReturnIdle = 7
        }

        public enum PoseDriverMode
        {
            AutoSelectByPlayMode = 0,
            AnimatorBaseWithAdditiveLateUpdate = 1,
            DeterministicCurveDriven = 2
        }

        public static readonly string[] ExpectedBoneNames = new string[25]
        {
            "Keiko_FPS_LeftArm_Root",
            "Clavicle_L", "UpperArm_L", "Elbow_L", "Forearm_L",
            "ForearmTwist_01_L", "ForearmTwist_02_L", "Wrist_L", "Palm_L",
            "Thumb_01_L", "Thumb_02_L", "Thumb_03_L",
            "Index_01_L", "Index_02_L", "Index_03_L",
            "Middle_01_L", "Middle_02_L", "Middle_03_L",
            "Ring_01_L", "Ring_02_L", "Ring_03_L",
            "Pinky_01_L", "Pinky_02_L", "Pinky_03_L",
            "WatchSocket_L"
        };

        public static readonly int[] ParentIndices = new int[25]
        {
            -1, 0, 1, 2, 3, 4, 5, 6, 7,
            8, 9, 10,
            8, 12, 13,
            8, 15, 16,
            8, 18, 19,
            8, 21, 22,
            7
        };

        private static readonly Vector3[] DefaultBindWorldPositions = new Vector3[25]
        {
            new Vector3(-0.242f, -0.125f,  0.030f),
            new Vector3(-0.230f, -0.118f,  0.025f),
            new Vector3(-0.198f, -0.104f,  0.018f),
            new Vector3(-0.156f, -0.088f,  0.010f),
            new Vector3(-0.112f, -0.072f,  0.005f),
            new Vector3(-0.074f, -0.059f,  0.002f),
            new Vector3(-0.040f, -0.048f,  0.000f),
            new Vector3(-0.006f, -0.038f,  0.000f),
            new Vector3( 0.045f, -0.025f, -0.002f),
            new Vector3( 0.051f, -0.060f,  0.004f),
            new Vector3( 0.086f, -0.070f,  0.006f),
            new Vector3( 0.121f, -0.066f,  0.008f),
            new Vector3( 0.085f,  0.012f, -0.004f),
            new Vector3( 0.121f,  0.002f,  0.000f),
            new Vector3( 0.143f, -0.014f,  0.005f),
            new Vector3( 0.097f, -0.005f, -0.003f),
            new Vector3( 0.133f, -0.016f,  0.001f),
            new Vector3( 0.154f, -0.033f,  0.006f),
            new Vector3( 0.103f, -0.024f, -0.001f),
            new Vector3( 0.137f, -0.036f,  0.003f),
            new Vector3( 0.158f, -0.049f,  0.008f),
            new Vector3( 0.100f, -0.043f,  0.002f),
            new Vector3( 0.128f, -0.052f,  0.006f),
            new Vector3( 0.145f, -0.061f,  0.010f),
            new Vector3(-0.011f, -0.032f, -0.024f)
        };

        [SerializeField] private SkinnedMeshRenderer _skinnedRenderer;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _watchSocket;
        [SerializeField] private Transform[] _bones;
        [SerializeField] private PoseDriverMode _poseDriverMode = PoseDriverMode.DeterministicCurveDriven;

        public Material skinMaterial;
        public Material sleeveMaterial;

        public SkinnedMeshRenderer SkinnedRenderer => _skinnedRenderer;
        public Animator ArmAnimator => _animator;
        public Transform[] Bones => _bones;
        public Transform WatchSocket => _watchSocket;
        public ArmClipState CurrentState { get; private set; } = ArmClipState.HiddenIdle;
        public PoseDriverMode ActivePoseDriverMode
        {
            get => _poseDriverMode;
            set
            {
                _poseDriverMode = value;
                SyncAnimatorEnabledState();
            }
        }

        private Vector3[] _bindLocalPositions;
        private Quaternion[] _bindLocalRotations;
        private float _stateTime;
        private float _idleTime;
        private float _crownImpulseTimer = -1f;
        private int _crownImpulseDirection = 1;

        private bool IsAnimatorBaseActive =>
            _poseDriverMode == PoseDriverMode.AnimatorBaseWithAdditiveLateUpdate &&
            _animator != null &&
            _animator.runtimeAnimatorController != null;

        public void InitializeRig()
        {
            if (_bones != null && _bones.Length == 25 && _bones[0] != null && _skinnedRenderer != null && _skinnedRenderer.sharedMesh != null)
            {
                ValidateImportedProductionMaterials();
                CacheBindPose();
                SyncAnimatorEnabledState();
                ResetToHidden();
                return;
            }

            if (_skinnedRenderer == null) _skinnedRenderer = GetComponent<SkinnedMeshRenderer>() ?? GetComponentInChildren<SkinnedMeshRenderer>();
            if (_animator == null) _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();

            // Never instantiate or clone a non-rigged or incorrectly imported mesh.
            // The V10 PrefabBuilder must supply the real FBX SkinnedMeshRenderer.
            if (_skinnedRenderer == null || _skinnedRenderer.sharedMesh == null)
                throw new MissingReferenceException("ROKAS BedSleep V10: real imported FBX SkinnedMeshRenderer missing; aborting.");

            // Production invariant: the source prefab must supply every actual
            // skinned vertex and the real 25-bone armature. Never fabricate a
            // skeleton that merely *looks* correct but cannot deform the mesh.
            if (_skinnedRenderer == null || _skinnedRenderer.sharedMesh == null)
                throw new MissingReferenceException("ROKAS BedSleep V7: authored SkinnedMeshRenderer missing.");
            BindBoneHierarchyFromPrefabTransforms();
            ValidateImportedProductionMaterials();
            _skinnedRenderer.bones = _bones;
            _skinnedRenderer.rootBone = _bones[0];
            _skinnedRenderer.updateWhenOffscreen = true;

            SyncAnimatorEnabledState();
            ResetToHidden();
        }

        private void SyncAnimatorEnabledState()
        {
            if (_animator != null)
            {
                // Prevent Animator and DeterministicCurveDriven from ever overwriting each other on the same frame!
                _animator.enabled = IsAnimatorBaseActive;
            }
        }

        private void BindBoneHierarchyFromPrefabTransforms()
        {
            if (_bones == null || _bones.Length != 25) _bones = new Transform[25];
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < 25; i++)
            {
                if (_bones[i] != null) continue;
                string expected = ExpectedBoneNames[i];
                foreach (Transform t in allChildren)
                {
                    if (t.name == expected)
                    {
                        _bones[i] = t;
                        break;
                    }
                }
            }

            for (int i = 0; i < 25; i++)
            {
                if (_bones[i] == null)
                    throw new MissingReferenceException(
                        "ROKAS BedSleep V7: missing imported authoring bone: " + ExpectedBoneNames[i]);
            }

            _watchSocket = _bones[24];
            CacheBindPose();
        }

        private void CacheBindPose()
        {
            _bindLocalPositions = new Vector3[25];
            _bindLocalRotations = new Quaternion[25];
            for (int i = 0; i < 25; i++)
            {
                _bindLocalPositions[i] = _bones[i].localPosition;
                _bindLocalRotations[i] = _bones[i].localRotation;
            }
            if (_watchSocket == null && _bones.Length >= 25) _watchSocket = _bones[24];
        }

        private void ValidateImportedProductionMaterials()
        {
            // V10 requires the actual three-submesh exported arm (skin,
            // sleeve, conforming fingernails). A silent replacement with
            // 2 runtime materials would REMOVE the skinned nails again.
            if (_skinnedRenderer == null || _skinnedRenderer.sharedMesh == null)
                throw new MissingReferenceException("V10: missing actual imported FBX hand mesh");
            if (_skinnedRenderer.sharedMesh.subMeshCount != 3)
                throw new InvalidOperationException("V10: real skinned FBX must have 3 submeshes: skin, sleeve, five nails");
            var slots = _skinnedRenderer.sharedMaterials;
            if (slots == null || slots.Length != 3)
                throw new MissingReferenceException("V10: 3 imported non-placeholder PBR materials required");
            for (int i = 0; i < slots.Length; ++i)
                if (slots[i] == null || slots[i].shader == null ||
                    slots[i].shader.name == "Hidden/InternalErrorShader")
                    throw new MissingReferenceException("V10: broken PBR material slot " + i);
            if (_skinnedRenderer.sharedMesh.bindposes.Length != 25)
                throw new MissingReferenceException("V10: imported arm requires 25 actual skin bind poses");
            skinMaterial = slots[0];
            sleeveMaterial = slots[1];
        }

        public void ResetToHidden()
        {
            CurrentState = ArmClipState.HiddenIdle;
            _stateTime = 0f;
            _crownImpulseTimer = -1f;
            if (IsAnimatorBaseActive) _animator.Play("SleepArm_HiddenIdle", 0, 0f);
            else ApplyPose(0f, 0f, 0f, 0f);
        }

        public void PlayRaise()
        {
            CurrentState = ArmClipState.Raise;
            _stateTime = 0f;
            if (IsAnimatorBaseActive) _animator.SetTrigger("RaiseTrigger");
        }

        public void PlayInspectIdle()
        {
            CurrentState = ArmClipState.InspectIdle;
            _stateTime = 0f;
        }

        public void PlayCrownFeedback(int direction = 1)
        {
            _crownImpulseDirection = direction >= 0 ? 1 : -1;
            _crownImpulseTimer = 0f;
            if (IsAnimatorBaseActive) _animator.SetTrigger("CrownTickTrigger");
        }

        public void PlayConfirm()
        {
            CurrentState = ArmClipState.Confirm;
            _stateTime = 0f;
            if (IsAnimatorBaseActive) _animator.SetTrigger("ConfirmTrigger");
        }

        public void PlayLower()
        {
            CurrentState = ArmClipState.Lower;
            _stateTime = 0f;
            if (IsAnimatorBaseActive) _animator.SetTrigger("LowerTrigger");
        }

        public void TickAnimation(float dt)
        {
            if (_bones == null || _bones.Length < 25) return;
            _stateTime += dt;
            _idleTime += dt;
            if (_crownImpulseTimer >= 0f)
            {
                _crownImpulseTimer += dt;
                if (_crownImpulseTimer > 0.18f) _crownImpulseTimer = -1f;
            }

            if (IsAnimatorBaseActive)
            {
                // Animator evaluates the base clip; LateUpdate applies additive secondary breathing & crown recoil
                return;
            }

            switch (CurrentState)
            {
                case ArmClipState.HiddenIdle:
                    ApplyPose(0f, 0f, 0f, 0f);
                    break;
                case ArmClipState.Raise:
                {
                    float u = Mathf.Clamp01(_stateTime / 1.05f);
                    float raiseEase = 1f - Mathf.Pow(1f - u, 3f);
                    float turnEase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.15f) / 0.85f));
                    ApplyPose(raiseEase, turnEase, 0f, 0f);
                    if (u >= 1f) PlayInspectIdle();
                    break;
                }
                case ArmClipState.InspectIdle:
                {
                    float crownPulse = EvaluateCrownImpulse();
                    ApplyPose(1f, 1f, _idleTime, crownPulse);
                    break;
                }
                case ArmClipState.Confirm:
                {
                    float u = Mathf.Clamp01(_stateTime / 0.38f);
                    float nod = Mathf.Sin(u * Mathf.PI) * 1.6f;
                    ApplyPose(1f, 1f, _idleTime, nod);
                    break;
                }
                case ArmClipState.Lower:
                {
                    float u = Mathf.Clamp01(_stateTime / 0.90f);
                    float lowerEase = 1f - (u * u * (3f - 2f * u));
                    ApplyPose(lowerEase, lowerEase, 0f, 0f);
                    if (u >= 1f) CurrentState = ArmClipState.HiddenIdle;
                    break;
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsAnimatorBaseActive || _bones == null || _bones.Length < 25) return;
            float crownPulse = EvaluateCrownImpulse();
            if (Mathf.Abs(crownPulse) > 1e-4f)
            {
                _bones[7].localRotation = _bones[7].localRotation * Quaternion.Euler(crownPulse * 0.85f, 0f, 0f);
            }
        }

        private float EvaluateCrownImpulse()
        {
            if (_crownImpulseTimer < 0f) return 0f;
            float t = _crownImpulseTimer;
            return _crownImpulseDirection * Mathf.Exp(-t * 24f) * Mathf.Sin(t * 18f * Mathf.PI * 2f);
        }

        private void ApplyPose(float raise01, float wristTurn01, float breathTime, float crownImpulse)
        {
            for (int i = 0; i < 25; i++)
            {
                _bones[i].localPosition = _bindLocalPositions[i];
                _bones[i].localRotation = _bindLocalRotations[i];
            }

            Vector3 hiddenOffset = new Vector3(-0.12f, -0.44f, 0.05f);
            float breathY = raise01 > 0.9f ? Mathf.Sin(breathTime * (2f * Mathf.PI * 0.22f)) * 0.0018f : 0f;
            _bones[0].localPosition = _bindLocalPositions[0] + Vector3.Lerp(hiddenOffset, new Vector3(0f, breathY, 0f), raise01);

            float pronationDeg = Mathf.Lerp(-38f, 0f, wristTurn01) + crownImpulse * 0.85f;
            // A genuine imported armature's bind-local rotations are NOT identity.
            // Apply the cinematic wrist twist as an offset in each bone's
            // rest-local space, otherwise all three joints snap on Raise.
            _bones[5].localRotation = _bindLocalRotations[5] *
                Quaternion.Euler(pronationDeg * 0.35f, 0f, 0f);
            _bones[6].localRotation = _bindLocalRotations[6] *
                Quaternion.Euler(pronationDeg * 0.65f, 0f, 0f);
            _bones[7].localRotation = _bindLocalRotations[7] *
                Quaternion.Euler(pronationDeg, Mathf.Lerp(-24f, 0f, wristTurn01),
                    Mathf.Lerp(-42f, 0f, raise01));
        }
    }
}
