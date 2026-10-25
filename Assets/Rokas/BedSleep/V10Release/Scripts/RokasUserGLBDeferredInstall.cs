using UnityEngine;

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// Same new user GLB setup in Unity Editor AND standalone WindowsPlayer.
    /// Wait until Argon's source wrist and camera were updated for two
    /// LateUpdate frames; avoid the V15 EXE's mid-initialization alignment
    /// and avoid WaitForEndOfFrame stalling in unfocused Editor Game views.
    /// </summary>
    [DefaultExecutionOrder(32005)]
    public sealed class RokasUserGLBDeferredInstall : MonoBehaviour
    {
        private GameObject actor;
        private Camera cameraForHand;
        private int startedFrame;
        private bool scheduled;

        internal void Configure(GameObject target, Camera camera)
        {
            if (scheduled) return;
            actor = target;
            cameraForHand = camera;
            startedFrame = Time.frameCount;
            scheduled = true;
        }

        private void LateUpdate()
        {
            if (!scheduled || Time.frameCount < startedFrame + 2)
                return;
            scheduled = false;
            enabled = false;
            if (actor == null || cameraForHand == null)
            {
                Debug.LogError("[ROKAS NEW GLB PARITY] Actor/camera destroyed " +
                    "before user-hand calibration.");
                return;
            }
            float start = Time.realtimeSinceStartup;
            bool installed = RokasUserGLBPlayerInstaller.TryInstall(
                actor, cameraForHand);
            Debug.Log("[ROKAS NEW GLB PARITY] Shared Editor/Player install on " +
                Application.platform + " => " + (installed ? "ACTIVE" : "FAILED") +
                "; setup=" + ((Time.realtimeSinceStartup - start) * 1000f)
                    .ToString("F1") + "ms; camera=" + cameraForHand.name +
                "; aspect=" + cameraForHand.aspect.ToString("F4") +
                "; frame=" + Time.frameCount +
                "; resolution=" + Screen.width + "x" + Screen.height);
            if (!installed)
                Debug.LogError("[ROKAS NEW GLB PARITY] Old Argon remains visible. " +
                    "Use the preceding Player.log exception; not a visual PASS.");
        }
    }
}
