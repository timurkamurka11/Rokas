using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// References the exact locally-imported user GLB FBX, authored clip,
    /// PBR skin material and Hand Studio tuning for a real Windows player.
    /// The editor stager creates an asset under Resources before building.
    /// No user-owned binary/model is added to GitHub by this source code.
    /// </summary>
    public sealed class RokasUserGLBReleaseConfig : ScriptableObject
    {
        public GameObject handModel;
        public AnimationClip initialPoseClip;
        public Material originalSkinMaterial;
        public RokasUserHandStudioProfile studioProfile;

        public bool IsComplete =>
            handModel != null && initialPoseClip != null &&
            originalSkinMaterial != null && studioProfile != null;
    }
}
