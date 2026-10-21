using System;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Editor-Play-Mode-only native clip driver. Follow the physical OLED wrist
    /// rather than creating a second, disconnected clock. Do not save a prefab.
    /// </summary>
    [DefaultExecutionOrder(32010)]
    public sealed class RokasUserGLBRealBedFollower : MonoBehaviour
    {
        private Transform candidateRoot, candidateWrist, existingWrist;
        private AnimationClip clip;
        private float calibratedScale, startTime;
        private Renderer[] oldVisuals;
        private bool[] oldVisibility;
        private Renderer newSkin;
        private Material ownedMaterial;
        private bool ready, oldHidden;
        public void Initialize(Transform root, Transform wrist, Transform referenceWrist,
            AnimationClip importedClip, float nativeScale,
            Renderer[] oldRenderers, bool[] previousVisibility,
            Renderer visibleSkin, Material temporaryMaterial)
        {
            if (root == null || wrist == null || referenceWrist == null ||
                importedClip == null || visibleSkin == null ||
                oldRenderers == null || previousVisibility == null ||
                oldRenderers.Length != previousVisibility.Length)
                throw new InvalidOperationException("Invalid actual-bed new hand initialization");
            candidateRoot = root;
            candidateWrist = wrist;
            existingWrist = referenceWrist;
            clip = importedClip;
            calibratedScale = nativeScale;
            oldVisuals = oldRenderers;
            oldVisibility = previousVisibility;
            newSkin = visibleSkin;
            ownedMaterial = temporaryMaterial;
            startTime = Time.unscaledTime;
            ready = true;
            // Disable old skin only once the imported clip and correct renderer
            // have been resolved. Never remove any Watch_* geometry/colliders.
            AlignAndSample(0f);
            for (int i = 0; i < oldVisuals.Length; i++)
                if (oldVisuals[i] != null) oldVisuals[i].enabled = false;
            oldHidden = true;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            if (existingWrist == null || candidateWrist == null ||
                candidateRoot == null || newSkin == null)
            {
                Restore();
                ready = false;
                return;
            }
            try
            {
                float t = Mathf.Repeat(Time.unscaledTime - startTime,
                    Mathf.Max(.1f, clip.length));
                AlignAndSample(t);
            }
            catch (Exception e)
            {
                Debug.LogError("[ROKAS NEW GLB] Candidate failed; restoring V16: " + e);
                Restore();
                ready = false;
                gameObject.SetActive(false);
            }
        }

        private void AlignAndSample(float t)
        {
            candidateRoot.localPosition = Vector3.zero;
            candidateRoot.localRotation = Quaternion.identity;
            candidateRoot.localScale = Vector3.one * calibratedScale;
            clip.SampleAnimation(candidateRoot.gameObject, t);
            Quaternion align = existingWrist.rotation *
                Quaternion.Inverse(candidateWrist.rotation);
            candidateRoot.rotation = align * candidateRoot.rotation;
            candidateRoot.position += existingWrist.position - candidateWrist.position;
            newSkin.enabled = true;
        }

        private void Restore()
        {
            if (!oldHidden) return;
            for (int i = 0; i < oldVisuals.Length; ++i)
                if (oldVisuals[i] != null) oldVisuals[i].enabled = oldVisibility[i];
            oldHidden = false;
        }

        private void OnDisable() { Restore(); }
        private void OnDestroy()
        {
            Restore();
            if (ownedMaterial != null) UnityEngine.Object.Destroy(ownedMaterial);
        }
    }

}
