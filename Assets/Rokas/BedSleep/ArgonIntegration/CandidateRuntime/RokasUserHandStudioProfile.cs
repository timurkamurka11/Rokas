using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Authored user-controlled bed-shot calibration. Never writes into the
    /// source FBX, original V16 prefab or source watch/OLED materials.
    /// </summary>
    public sealed class RokasUserHandStudioProfile : ScriptableObject
    {
        [Header("Hand: offset relative to the existing bed camera, meters")]
        public Vector3 handCameraOffset = new Vector3(-0.14f, -0.015f, 0f);
        [Header("Hand rotation from calibrated horizontal ROKAS pose, degrees")]
        public Vector3 handEuler = Vector3.zero;
        [Range(.65f, 1.35f)] public float handScale = 1f;
        [Range(.35f, 1.5f)] public float handBrightness = .83f;

        [Header("Whole physical smartwatch: relative to new wrist")]
        [Range(-.06f, .18f)] public float watchTowardsFingers = .075f;
        public Vector3 watchWristOffset = Vector3.zero;
        public Vector3 watchEuler = Vector3.zero;
        [Range(.75f, 1.25f)] public float watchScale = 1f;
        [Range(.25f, 2f)] public float oledBrightness = 1f;

        [Header("Subtle individual finger idle")]
        [Range(0f, 5f)] public float thumbDegrees = .6f;
        [Range(0f, 5f)] public float indexDegrees = 1f;
        [Range(0f, 5f)] public float middleDegrees = .8f;
        [Range(0f, 5f)] public float ringDegrees = 1.1f;
        [Range(0f, 5f)] public float littleDegrees = 1.3f;
        [Range(0f, 2f)] public float wristDegrees = .1f;
        [Range(.15f, 2.5f)] public float motionSpeed = .65f;
        public bool freezeAnimation = false;

        [Header("Visual helpers, do not affect render")]
        public bool showSceneHandles = true;

        public void ResetToDefaults()
        {
            handCameraOffset = new Vector3(-.14f, -.015f, 0f);
            handEuler = Vector3.zero;
            handScale = 1f;
            handBrightness = .83f;
            watchTowardsFingers = .075f;
            watchWristOffset = Vector3.zero;
            watchEuler = Vector3.zero;
            watchScale = 1f;
            oledBrightness = 1f;
            thumbDegrees = .6f;
            indexDegrees = 1f;
            middleDegrees = .8f;
            ringDegrees = 1.1f;
            littleDegrees = 1.3f;
            wristDegrees = .1f;
            motionSpeed = .65f;
            freezeAnimation = false;
            showSceneHandles = true;
        }

        private void OnValidate()
        {
            handScale = Mathf.Clamp(handScale, .65f, 1.35f);
            watchScale = Mathf.Clamp(watchScale, .75f, 1.25f);
            watchTowardsFingers = Mathf.Clamp(watchTowardsFingers, -.06f, .18f);
            thumbDegrees = Mathf.Clamp(thumbDegrees, 0f, 5f);
            indexDegrees = Mathf.Clamp(indexDegrees, 0f, 5f);
            middleDegrees = Mathf.Clamp(middleDegrees, 0f, 5f);
            ringDegrees = Mathf.Clamp(ringDegrees, 0f, 5f);
            littleDegrees = Mathf.Clamp(littleDegrees, 0f, 5f);
            wristDegrees = Mathf.Clamp(wristDegrees, 0f, 2f);
            motionSpeed = Mathf.Clamp(motionSpeed, .15f, 2.5f);
        }
    }
}
