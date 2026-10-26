using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// User-authored ROKAS Bed shot. Real user GLB mesh/skin, existing original
    /// seven-part ROKAS watch, Russian OLED and bed gameplay. Scene objects are
    /// changed ONLY during Unity Play Mode; no source mesh/prefab modified.
    /// </summary>
    [DefaultExecutionOrder(32010)]
    public sealed class RokasUserGLBRealBedFollower : MonoBehaviour
    {
        private struct Finger
        {
            public Transform Bone;
            public Quaternion Rest;
            public int Family;
            public float Phase;
            public float Speed;
            public float Part;
        }

        private readonly List<Finger> fingers = new List<Finger>(14);
        private Transform candidateRoot, candidateWrist, originalWrist;
        private Transform originalMiddleTip, originalIndexRoot, originalLittleRoot;
        private Transform nativeMiddleTip, nativeIndexRoot, nativeLittleRoot;
        private Transform watchRoot;
        private MeshRenderer watchFace;
        private MaterialPropertyBlock initialOledBlock, workingOledBlock;
        private RokasUserHandStudioProfile profile;
        private Renderer[] oldVisuals;
        private bool[] oldVisibility;
        private Renderer newSkin;
        private Material skinMaterial;
        private Camera sceneCamera;
        private Vector3 watchRestLocalPosition, watchRestLocalScale;
        private Quaternion watchRestLocalRotation, watchRestRelativeRotation;
        private Vector3 watchRestRelativePosition;
        private float scale, began;
        private bool running, oldHidden;

        public Transform HandGizmo => candidateWrist;
        public Transform WatchGizmo => watchRoot;
        public Transform NativeWrist => candidateWrist;
        public Camera GameplayCamera => sceneCamera;
        public RokasUserHandStudioProfile Settings => profile;

        public void Initialize(
            Transform root, Transform wrist, Transform original,
            Transform originalTip, Transform originalIndex, Transform originalLittle,
            AnimationClip sourceClip, float nativeScale,
            Renderer[] legacy, bool[] visibility, Renderer skin, Material material,
            Transform sourceWatchRoot, MeshRenderer sourceWatchFace,
            RokasUserHandStudioProfile settings, Camera camera)
        {
            if (root == null || wrist == null || original == null ||
                originalTip == null || originalIndex == null || originalLittle == null ||
                sourceClip == null || skin == null || material == null ||
                sourceWatchRoot == null || sourceWatchFace == null || settings == null ||
                legacy == null || visibility == null || legacy.Length != visibility.Length ||
                nativeScale <= 0f || float.IsNaN(nativeScale))
                throw new InvalidOperationException("Hand Studio requires actual original game wrist/watch");

            candidateRoot = root;
            candidateWrist = wrist;
            originalWrist = original;
            originalMiddleTip = originalTip;
            originalIndexRoot = originalIndex;
            originalLittleRoot = originalLittle;
            scale = nativeScale;
            oldVisuals = legacy;
            oldVisibility = visibility;
            newSkin = skin;
            skinMaterial = material;
            watchRoot = sourceWatchRoot;
            watchFace = sourceWatchFace;
            profile = settings;
            sceneCamera = camera;

            // Capture original WATCH ROOT pose. All watch meshes / sapphire /
            // OLED / crown and button are descendants of the same root.
            watchRestLocalPosition = watchRoot.localPosition;
            watchRestLocalRotation = watchRoot.localRotation;
            watchRestLocalScale = watchRoot.localScale;
            watchRestRelativePosition = Quaternion.Inverse(originalWrist.rotation) *
                (watchRoot.position - originalWrist.position);
            watchRestRelativeRotation = Quaternion.Inverse(originalWrist.rotation) *
                watchRoot.rotation;
            initialOledBlock = new MaterialPropertyBlock();
            workingOledBlock = new MaterialPropertyBlock();
            watchFace.GetPropertyBlock(initialOledBlock);

            // Native 4-second full-fist clip is a QA take, NOT our sleep idle.
            sourceClip.SampleAnimation(candidateRoot.gameObject, 0f);
            nativeMiddleTip = FindBone("middle_distal");
            nativeIndexRoot = FindBone("index_proximal");
            nativeLittleRoot = FindBone("little_proximal");
            if (nativeMiddleTip == null || nativeIndexRoot == null || nativeLittleRoot == null)
                throw new InvalidOperationException("Native user rig missing pose landmarks");

            AddFinger("thumb_proximal", 0, .70f, .94f, 1f);
            AddFinger("thumb_distal", 0, 1.00f, .94f, .4f);
            AddFinger("index_proximal", 1, 1.50f, 1.04f, 1f);
            AddFinger("index_middle", 1, 1.80f, 1.04f, .44f);
            AddFinger("index_distal", 1, 2.10f, 1.04f, .17f);
            AddFinger("middle_proximal", 2, 2.60f, .88f, 1f);
            AddFinger("middle_middle", 2, 2.90f, .88f, .43f);
            AddFinger("middle_distal", 2, 3.20f, .88f, .16f);
            AddFinger("ring_proximal", 3, 4.10f, 1.10f, 1f);
            AddFinger("ring_middle", 3, 4.40f, 1.10f, .45f);
            AddFinger("ring_distal", 3, 4.70f, 1.10f, .17f);
            AddFinger("little_proximal", 4, 5.20f, .99f, 1f);
            AddFinger("little_middle", 4, 5.50f, .99f, .43f);
            AddFinger("little_distal", 4, 5.80f, .99f, .16f);
            if (fingers.Count != 14)
                throw new InvalidOperationException("Native animated finger rig incomplete");

            began = Time.unscaledTime;
            // Fail before hiding V16. Never turn off OLED or Watch_* pieces.
            ApplyPose(0f);
            for (int i = 0; i < oldVisuals.Length; i++)
                if (oldVisuals[i] != null) oldVisuals[i].enabled = false;
            oldHidden = true;
            running = true;
            Debug.Log("[ROKAS HAND STUDIO] User hand and original watch active. " +
                "Edit live in ROKAS > Bed Sleep > Hand Studio. V16 kept for fallback.");
        }

        private Transform FindBone(string name)
        {
            foreach (Transform t in candidateRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private void AddFinger(string name, int family, float phase, float speed, float part)
        {
            Transform tr = FindBone(name);
            if (tr == null)
                throw new InvalidOperationException("Native GLB missing finger " + name);
            fingers.Add(new Finger
            {
                Bone = tr,
                Rest = tr.localRotation,
                Family = family,
                Phase = phase,
                Speed = speed,
                Part = part
            });
        }

        private float DegreesForFamily(int family)
        {
            switch (family)
            {
                case 0: return profile.thumbDegrees;
                case 1: return profile.indexDegrees;
                case 2: return profile.middleDegrees;
                case 3: return profile.ringDegrees;
                default: return profile.littleDegrees;
            }
        }

        private static bool Basis(Vector3 forward, Vector3 across, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (forward.sqrMagnitude < 1e-7f) return false;
            forward.Normalize();
            across -= Vector3.Dot(across, forward) * forward;
            if (across.sqrMagnitude < 1e-7f) return false;
            rotation = Quaternion.LookRotation(forward, across.normalized);
            return true;
        }

        private void ApplyPose(float elapsed)
        {
            candidateRoot.localPosition = Vector3.zero;
            candidateRoot.localRotation = Quaternion.identity;
            candidateRoot.localScale = Vector3.one * (scale * profile.handScale);

            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.4f));
            float runningTime = elapsed * Mathf.Max(.15f, profile.motionSpeed);
            for (int i = 0; i < fingers.Count; i++)
            {
                Finger f = fingers[i];
                float amount = profile.freezeAnimation ? 0f :
                    DegreesForFamily(f.Family) * f.Part * fade * .5f *
                    (1f + Mathf.Sin(runningTime * f.Speed + f.Phase));
                f.Bone.localRotation = f.Rest *
                    Quaternion.AngleAxis(amount, Vector3.right);
            }

            Quaternion nativeBasis, targetBasis;
            if (!Basis(nativeMiddleTip.position - candidateWrist.position,
                nativeIndexRoot.position - nativeLittleRoot.position,
                out nativeBasis))
                throw new InvalidOperationException("Native palm orientation invalid");

            Vector3 heading = originalMiddleTip.position - originalWrist.position;
            Vector3 spread = originalIndexRoot.position - originalLittleRoot.position;
            if (sceneCamera != null)
            {
                Vector3 a = sceneCamera.WorldToViewportPoint(originalWrist.position);
                Vector3 b = sceneCamera.WorldToViewportPoint(originalMiddleTip.position);
                if (a.z > 0f && b.z > 0f &&
                    (b.x - a.x < .04f ||
                     Mathf.Abs(b.y - a.y) > Mathf.Abs(b.x - a.x) * .45f))
                    heading = sceneCamera.transform.right +
                              sceneCamera.transform.up * .035f;
            }
            if (!Basis(heading, spread, out targetBasis))
                throw new InvalidOperationException("Original ROKAS palm orientation missing");

            Quaternion aligned = (targetBasis * Quaternion.Inverse(nativeBasis)) *
                                 candidateRoot.rotation;
            Quaternion poseRotation = Quaternion.Euler(profile.handEuler);
            float wristAngle = profile.freezeAnimation ? 0f :
                profile.wristDegrees * fade *
                Mathf.Sin(runningTime * .7f + .65f);
            Quaternion wristMotion = Quaternion.AngleAxis(
                wristAngle, sceneCamera != null
                    ? sceneCamera.transform.right : Vector3.right);
            candidateRoot.rotation = wristMotion * poseRotation * aligned;

            // Translate in REAL game-camera directions so the user can extend
            // the forearm off the left edge of screen, never fake a sleeve cap.
            Vector3 offset = sceneCamera != null
                ? sceneCamera.transform.TransformVector(profile.handCameraOffset)
                : profile.handCameraOffset;
            candidateRoot.position += originalWrist.position + offset -
                                      candidateWrist.position;
            if (newSkin != null) newSkin.enabled = true;

            // Place the actual full watch rig (case/strap/OLED/crown/buttons)
            // around the NEW skinned wrist. The old game watch remains live;
            // we never fabricate a new UI, texture or mesh.
            Vector3 fingerAxis = nativeMiddleTip.position - candidateWrist.position;
            if (fingerAxis.sqrMagnitude < 1e-7f)
                throw new InvalidOperationException("Cannot locate candidate finger axis");
            Vector3 towardsFingers =
                Quaternion.Inverse(candidateWrist.rotation) * fingerAxis.normalized;
            Vector3 localWristOffset = watchRestRelativePosition +
                towardsFingers * profile.watchTowardsFingers +
                profile.watchWristOffset;
            watchRoot.position = candidateWrist.position +
                candidateWrist.rotation * localWristOffset;
            watchRoot.rotation = candidateWrist.rotation * watchRestRelativeRotation *
                Quaternion.Euler(profile.watchEuler);
            watchRoot.localScale = watchRestLocalScale * profile.watchScale;

            // Instance of the newly imported hand's material ONLY.
            float skinBrightness = Mathf.Clamp(profile.handBrightness, .35f, 1.5f);
            if (skinMaterial != null)
            {
                if (skinMaterial.HasProperty("_BaseColor"))
                    skinMaterial.SetColor("_BaseColor",
                        new Color(skinBrightness,skinBrightness,skinBrightness,1f));
                if (skinMaterial.HasProperty("_Color"))
                    skinMaterial.SetColor("_Color",
                        new Color(skinBrightness,skinBrightness,skinBrightness,1f));
            }

            // PropertyBlock maintains the existing Russian OLED texture,
            // presenters and interactive timer; never alter shared material.
            if (watchFace != null)
            {
                watchFace.GetPropertyBlock(workingOledBlock);
                float b = Mathf.Clamp(profile.oledBrightness, .25f, 2f);
                Color tint = new Color(b, b, b, 1f);
                workingOledBlock.SetColor("_BaseColor", tint);
                workingOledBlock.SetColor("_Color", tint);
                workingOledBlock.SetColor("_EmissionColor", tint);
                watchFace.SetPropertyBlock(workingOledBlock);
            }
        }

        private void LateUpdate()
        {
            if (!running) return;
            if (candidateRoot == null || candidateWrist == null ||
                originalWrist == null || watchRoot == null || profile == null ||
                originalMiddleTip == null || newSkin == null)
            {
                Restore();
                running = false;
                return;
            }
            try { ApplyPose(Mathf.Max(0f, Time.unscaledTime - began)); }
            catch (Exception error)
            {
                Debug.LogError("[ROKAS HAND STUDIO] Restoring original V16: " + error);
                Restore();
                running = false;
                gameObject.SetActive(false);
            }
        }

        private void Restore()
        {
            if (watchRoot != null)
            {
                watchRoot.localPosition = watchRestLocalPosition;
                watchRoot.localRotation = watchRestLocalRotation;
                watchRoot.localScale = watchRestLocalScale;
            }
            if (watchFace != null)
                watchFace.SetPropertyBlock(initialOledBlock);
            if (!oldHidden) return;
            for (int i = 0; i < oldVisuals.Length; i++)
                if (oldVisuals[i] != null)
                    oldVisuals[i].enabled = oldVisibility[i];
            oldHidden = false;
        }

        private void OnDisable() { Restore(); }
        private void OnDestroy()
        {
            Restore();
            if (skinMaterial != null) Destroy(skinMaterial);
        }
    }
}
