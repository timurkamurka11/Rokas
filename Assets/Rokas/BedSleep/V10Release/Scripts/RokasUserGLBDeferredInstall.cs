using System.Collections;
using UnityEngine;

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// Guarantees that the approved user GLB is attached after the V10
    /// presentation camera and the Argon wrist have completed their first
    /// render frame. Used in BOTH Editor and WindowsPlayer to guarantee
    /// identical initialization order, unlike the V15 preview/runtime split.
    /// </summary>
    public sealed class RokasUserGLBDeferredInstall : MonoBehaviour
    {
        private GameObject actor;
        private Camera cameraForHand;
        private bool scheduled;

        internal void Configure(GameObject target, Camera camera)
        {
            if (scheduled) return;
            scheduled = true;
            actor = target;
            cameraForHand = camera;
            StartCoroutine(RunOnceAfterSceneSettles());
        }

        private IEnumerator RunOnceAfterSceneSettles()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (actor == null || cameraForHand == null)
            {
                Debug.LogError("[ROKAS NEW GLB PARITY] Camera/actor destroyed " +
                    "before unified new-hand install.");
                yield break;
            }
            float start = Time.realtimeSinceStartup;
            bool installed = RokasUserGLBPlayerInstaller.TryInstall(
                actor, cameraForHand);
            Debug.Log("[ROKAS NEW GLB PARITY] Same runtime installer in " +
                Application.platform + " => " + (installed ? "ACTIVE" : "FAILED") +
                ", setup=" + ((Time.realtimeSinceStartup - start) * 1000f)
                    .ToString("F1") + "ms, camera=" + cameraForHand.name +
                ", aspect=" + cameraForHand.aspect.ToString("F4") +
                ", screen=" + Screen.width + "x" + Screen.height);
            if (!installed)
                Debug.LogError("[ROKAS NEW GLB PARITY] Argon fallback remains " +
                    "visible. Check the reason above in Player.log.");
        }
    }
}
