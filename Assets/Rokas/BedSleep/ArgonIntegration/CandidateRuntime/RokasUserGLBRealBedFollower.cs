using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Reversible Editor Play Mode preview of the exact user-supplied
    /// 23-joint FBX. The original 4-second clip is a rig/skin QA animation,
    /// NOT an idle loop for a sleeping first-person character.
    /// Keep the original in-game watch/OLED and approved wrist trajectory.
    /// </summary>
    [DefaultExecutionOrder(32010)]
    public sealed class RokasUserGLBRealBedFollower : MonoBehaviour
    {
        private struct Finger
        {
            public Transform Bone;
            public Quaternion Rest;
            public float Degrees;
            public float Period;
            public float Phase;
        }

        private Transform candidateRoot, candidateWrist, originalWrist;
        private Transform originalMiddleTip, originalIndexRoot, originalLittleRoot;
        private Transform nativeMiddleTip, nativeIndexRoot, nativeLittleRoot;
        private readonly List<Finger> fingers = new List<Finger>(14);
        private Renderer[] oldVisuals;
        private bool[] oldVisibility;
        private Renderer newSkin;
        private Material ownedMaterial;
        private float scale, startedAt;
        private bool active, oldHidden;

        public void Initialize(
            Transform root, Transform wrist, Transform original,
            Transform originalTip, Transform originalIndex, Transform originalLittle,
            AnimationClip sourceClip, float nativeScale,
            Renderer[] legacy, bool[] visibility, Renderer skin, Material material)
        {
            if (root == null || wrist == null || original == null ||
                originalTip == null || originalIndex == null || originalLittle == null ||
                sourceClip == null || skin == null ||
                legacy == null || visibility == null || legacy.Length != visibility.Length ||
                nativeScale <= 0f || float.IsNaN(nativeScale))
                throw new InvalidOperationException("Incomplete safe hand/pose calibration");

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
            ownedMaterial = material;

            // Import the ORIGINAL animation, but only take its initial open
            // resting frame. Never play the aggressive 4-second fist showcase
            // on a loop. Palm, wrist and forearm stay untouched thereafter.
            sourceClip.SampleAnimation(candidateRoot.gameObject, 0f);
            nativeMiddleTip = FindBone("middle_distal");
            nativeIndexRoot = FindBone("index_proximal");
            nativeLittleRoot = FindBone("little_proximal");
            if (nativeMiddleTip == null || nativeIndexRoot == null ||
                nativeLittleRoot == null)
                throw new InvalidOperationException("Five-finger GLB joints not found");

            AddFinger("thumb_proximal", 1.25f, 9.1f, 0.70f);
            AddFinger("thumb_distal", .5f, 9.1f, 1.00f);
            AddFinger("index_proximal", 2.1f, 7.1f, 1.50f);
            AddFinger("index_middle", .9f, 7.1f, 1.80f);
            AddFinger("index_distal", .35f, 7.1f, 2.10f);
            AddFinger("middle_proximal", 1.8f, 8.3f, 2.60f);
            AddFinger("middle_middle", .8f, 8.3f, 2.90f);
            AddFinger("middle_distal", .3f, 8.3f, 3.20f);
            AddFinger("ring_proximal", 2.4f, 6.8f, 4.10f);
            AddFinger("ring_middle", 1.0f, 6.8f, 4.40f);
            AddFinger("ring_distal", .35f, 6.8f, 4.70f);
            AddFinger("little_proximal", 2.7f, 7.7f, 5.20f);
            AddFinger("little_middle", 1.1f, 7.7f, 5.50f);
            AddFinger("little_distal", .4f, 7.7f, 5.80f);
            if (fingers.Count != 14)
                throw new InvalidOperationException("Native finger idle rig incomplete");

            startedAt = Time.unscaledTime;
            // Don't hide V16 or the original watch until the new candidate
            // has been fully initialized and can align to the approved pose.
            AlignAndAnimate(0f);
            for (int i = 0; i < oldVisuals.Length; i++)
                if (oldVisuals[i] != null) oldVisuals[i].enabled = false;
            oldHidden = true;
            active = true;
            Debug.Log("[ROKAS NEW HAND SUBTLE IDLE] Original four-second fist clip " +
                "disabled as loop. Individual finger curls <= 2.7 degrees; " +
                "hand heading + palm spread matched to approved V16 bed pose.");
        }

        private Transform FindBone(string name)
        {
            foreach (Transform t in candidateRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private void AddFinger(string name, float amplitude, float period, float phase)
        {
            Transform t = FindBone(name);
            if (t == null)
                throw new InvalidOperationException("Missing authentic finger bone " + name);
            fingers.Add(new Finger
            {
                Bone = t,
                Rest = t.localRotation,
                Degrees = amplitude,
                Period = period,
                Phase = phase
            });
        }

        private static bool BuildBasis(Vector3 forward, Vector3 across, out Quaternion basis)
        {
            basis = Quaternion.identity;
            if (forward.sqrMagnitude < .000001f) return false;
            forward.Normalize();
            across -= Vector3.Dot(across, forward) * forward;
            if (across.sqrMagnitude < .000001f) return false;
            basis = Quaternion.LookRotation(forward, across.normalized);
            return true;
        }

        private void AlignAndAnimate(float elapsed)
        {
            candidateRoot.localPosition = Vector3.zero;
            candidateRoot.localRotation = Quaternion.identity;
            candidateRoot.localScale = Vector3.one * scale;

            // Asynchronous, sub-3-degree relaxation only. No fist, no wrist
            // swing and no translation of any joints. Smooth ramp at entry.
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.4f));
            for (int i = 0; i < fingers.Count; i++)
            {
                Finger f = fingers[i];
                float cycle = Mathf.Sin(elapsed * (Mathf.PI * 2f / f.Period) + f.Phase);
                float amount = f.Degrees * .5f * (1f + cycle) * fade;
                f.Bone.localRotation = f.Rest *
                    Quaternion.AngleAxis(amount, Vector3.right);
            }

            // V16 is the approved anatomical layout: align BOTH the
            // wrist->middle-finger direction and index->little spread.
            // Matching only wrist.rotation was the cause of the vertical,
            // giant-looking hand close-up in the user's in-game recording.
            Quaternion candidateBasis, approvedBasis;
            if (!BuildBasis(nativeMiddleTip.position - candidateWrist.position,
                    nativeIndexRoot.position - nativeLittleRoot.position,
                    out candidateBasis))
                throw new InvalidOperationException("Candidate palm plane is degenerate");
            Vector3 desiredForward = originalMiddleTip.position - originalWrist.position;
            Vector3 desiredAcross = originalIndexRoot.position - originalLittleRoot.position;

            // Camera safety: if source controller rotates the palm so that
            // fingers point nearly vertically or to screen-left, retain the
            // wrist animation but orient the replacement horizontally right.
            // This does not move the camera or change the original watch.
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 a = cam.WorldToViewportPoint(originalWrist.position);
                Vector3 b = cam.WorldToViewportPoint(originalMiddleTip.position);
                float dx = b.x - a.x, dy = b.y - a.y;
                if (a.z > 0f && b.z > 0f &&
                    (dx < .04f || Mathf.Abs(dy) > Mathf.Abs(dx) * .45f))
                {
                    desiredForward = cam.transform.right +
                        cam.transform.up * .035f;
                    // Keep the V16 anatomical finger-spread orientation.
                    desiredAcross -= Vector3.Dot(desiredAcross,
                        desiredForward.normalized) * desiredForward.normalized;
                }
            }
            if (!BuildBasis(desiredForward, desiredAcross, out approvedBasis))
                throw new InvalidOperationException("Original V16 palm plane unavailable");
            candidateRoot.rotation =
                (approvedBasis * Quaternion.Inverse(candidateBasis)) *
                candidateRoot.rotation;
            // Old game wrist keeps the existing entrance/exit choreography,
            // position and original physical watch/OLED.
            candidateRoot.position += originalWrist.position - candidateWrist.position;
            newSkin.enabled = true;
        }

        private void LateUpdate()
        {
            if (!active) return;
            if (candidateRoot == null || candidateWrist == null ||
                originalWrist == null || originalMiddleTip == null ||
                newSkin == null)
            {
                Restore();
                active = false;
                return;
            }
            try
            {
                AlignAndAnimate(Mathf.Max(0f, Time.unscaledTime - startedAt));
            }
            catch (Exception error)
            {
                Debug.LogError("[ROKAS NEW HAND] Safe fallback to V16: " + error);
                Restore();
                active = false;
                gameObject.SetActive(false);
            }
        }

        private void Restore()
        {
            if (!oldHidden) return;
            for (int i = 0; i < oldVisuals.Length; i++)
                if (oldVisuals[i] != null) oldVisuals[i].enabled = oldVisibility[i];
            oldHidden = false;
        }

        private void OnDisable() { Restore(); }

        private void OnDestroy()
        {
            Restore();
            if (ownedMaterial != null) Destroy(ownedMaterial);
        }
    }
}
