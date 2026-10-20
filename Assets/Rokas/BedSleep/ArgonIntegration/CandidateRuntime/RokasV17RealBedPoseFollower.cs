using System;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Temporary visual replacement in the EXISTING bed gameplay (Editor Play Mode).
    /// Watch, OLED, sleeve, gameplay, save and V16 source prefabs are not modified.
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

        public void Configure(Transform oldWrist, Transform newRoot, Transform newWrist,
            SkinnedMeshRenderer oldSkin, SkinnedMeshRenderer newSkin,
            AnimationClip gripClip, AnimationClip indexClip, AnimationClip livingClip,
            float relativeScale)
        {
            if (oldWrist == null || newRoot == null || newWrist == null ||
                oldSkin == null || newSkin == null || gripClip == null ||
                indexClip == null || livingClip == null)
                throw new InvalidOperationException("Missing native V17 or old wrist/skin");
            targetWrist = oldWrist;
            candidateRoot = newRoot;
            candidateWrist = newWrist;
            originalHand = oldSkin;
            candidateHand = newSkin;
            grip = gripClip; index = indexClip; living = livingClip;
            scale = relativeScale;
            gestureStart = Time.unscaledTime;
            gesture = 0;
            candidateHand.enabled = true;
            ready = true;
            Debug.Log("[ROKAS V17 REAL BED] PLAY-MODE visual candidate installed. " +
                "Original smartwatch/OLED/sleeve remain. F7=Living F8=Grip F9=Index");
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
                        if (Input.GetKeyDown(KeyCode.F7)) { gesture = 0; gestureStart = Time.unscaledTime; }
                        if (Input.GetKeyDown(KeyCode.F8)) { gesture = 1; gestureStart = Time.unscaledTime; }
                        if (Input.GetKeyDown(KeyCode.F9)) { gesture = 2; gestureStart = Time.unscaledTime; }
                    }
                    catch (InvalidOperationException)
                    {
                        legacyInput = false;
                        Debug.LogWarning("[ROKAS V17] Old Input API disabled; living animation still active");
                    }
                }

                AnimationClip clip = gesture == 1 ? grip : (gesture == 2 ? index : living);
                float authoredDuration = gesture == 1 ? 95f/30f :
                    (gesture == 2 ? 63f/30f : 94f/30f);
                authoredDuration = Mathf.Min(clip.length, authoredDuration);
                float elapsed = Mathf.Max(0f, Time.unscaledTime - gestureStart);
                float t = gesture == 0 ?
                    Mathf.Repeat(elapsed, Mathf.Max(.05f, authoredDuration)) :
                    Mathf.Min(elapsed, authoredDuration);
                if (gesture != 0 && elapsed > authoredDuration + .18f)
                {
                    gesture = 0; gestureStart = Time.unscaledTime;
                }

                // Each V17 FBX has its OWN 25-bone bindpose: never graft onto V16 bones.
                candidateRoot.localPosition = Vector3.zero;
                candidateRoot.localRotation = Quaternion.identity;
                candidateRoot.localScale = Vector3.one * scale;
                clip.SampleAnimation(candidateRoot.gameObject, t);
                Quaternion adjust = targetWrist.rotation *
                    Quaternion.Inverse(candidateWrist.rotation);
                candidateRoot.rotation = adjust * candidateRoot.rotation;
                candidateRoot.position += targetWrist.position - candidateWrist.position;

                if (!oldHidden) { originalHand.enabled = false; oldHidden = true; }
                candidateHand.enabled = true;

                if (Time.unscaledTime > nextMessage)
                {
                    nextMessage = Time.unscaledTime + 30f;
                    Debug.Log("[ROKAS V17 REAL BED ACTIVE] Original Hub->Bed scene; " +
                        "F7 living / F8 grip / F9 index; stop Play Mode to restore old hand");
                }
            }
            catch (Exception error)
            {
                Debug.LogError("[ROKAS V17] Candidate disabled; reverting original skin: " + error);
                Restore();
                ready = false;
                candidateRoot.gameObject.SetActive(false);
            }
        }

        private void Restore()
        {
            if (oldHidden && originalHand != null) originalHand.enabled = true;
            oldHidden = false;
        }
        private void OnDisable() { Restore(); }
        private void OnDestroy() { Restore(); }
    }
}
