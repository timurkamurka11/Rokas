using System;
using System.Linq;
using UnityEngine;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// Bridges the previously Editor-only user GLB hand into actual builds.
    /// Runs only after the original Argon hand/watch finishes initializing.
    /// Reuses the approved studio profile, original watch/OLED and native
    /// animator take; it never alters the FBX or serialized game scenes.
    /// </summary>
    internal static class RokasUserGLBPlayerInstaller
    {
        private const string Resource =
            "BedSleep/NewUserGLB/ROKAS_NewHandReleaseConfig";

        internal static bool TryInstall(GameObject actor, Camera presentationCamera)
        {
            var config = Resources.Load<RokasUserGLBReleaseConfig>(Resource);
            if (config == null || !config.IsComplete)
            {
                Debug.LogError("[ROKAS NEW GLB PLAYER] Release configuration missing/incomplete: " +
                    Resource + ". Editor preview is NOT included in EXE. " +
                    "Use ROKAS > Bed Sleep > Prepare New GLB Hand For Build.");
                return false;
            }
            if (actor == null || presentationCamera == null)
            {
                Debug.LogError("[ROKAS NEW GLB PLAYER] Old Argon actor/camera not ready.");
                return false;
            }

            var previous = actor.GetComponentsInChildren<Renderer>(true);
            var originalSkin = previous.OfType<SkinnedMeshRenderer>()
                .FirstOrDefault(x => x.name == "Keiko_Hand_Skin_Mesh" && x.enabled);
            var originalSleeve = previous.FirstOrDefault(x =>
                x.name == "Keiko_Homewear_Sleeve_Mesh" && x.enabled);
            var originalNails = previous.FirstOrDefault(x =>
                x.name == "Keiko_Fingernails_Mesh" && x.enabled);
            var oled = previous.OfType<MeshRenderer>().FirstOrDefault(x =>
                x.name == "Watch_OLED_Display" && x.enabled);
            var watchParts = previous.Where(x => x.enabled &&
                x.name.StartsWith("Watch_", StringComparison.Ordinal)).ToArray();

            var transforms = actor.GetComponentsInChildren<Transform>(true);
            var watchRoot = transforms.FirstOrDefault(x =>
                x.name == "ROKAS_Smartwatch_HighDetail");
            var oldWrist = transforms.FirstOrDefault(x => x.name == "Arm_Wrist");
            var oldTip = transforms.FirstOrDefault(x => x.name == "Middle_Tip");
            var oldIndex = transforms.FirstOrDefault(x => x.name == "Index_MCP");
            var oldLittle = transforms.FirstOrDefault(x => x.name == "Little_MCP");
            if (originalSkin == null || originalSleeve == null ||
                originalNails == null || oled == null || watchParts.Length < 6 ||
                watchRoot == null || oldWrist == null || oldTip == null ||
                oldIndex == null || oldLittle == null ||
                watchParts.Any(x => !x.transform.IsChildOf(watchRoot)))
            {
                Debug.LogError("[ROKAS NEW GLB PLAYER] Original hand/watch hierarchy is " +
                    "not ready. Keep Argon visible and preserve the working bed interaction.");
                return false;
            }

            GameObject instance = null;
            Material material = null;
            try
            {
                float started = Time.realtimeSinceStartup;
                instance = UnityEngine.Object.Instantiate(config.handModel, actor.transform);
                instance.name = "ROKAS_NEW_USER_GLB_RealBed_Player";
                var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.sharedMesh != null &&
                        x.sharedMesh.vertexCount > 30000);
                var imported = instance.GetComponentsInChildren<Transform>(true);
                var wrist = imported.FirstOrDefault(x => x.name == "wrist");
                var forearm = imported.FirstOrDefault(x => x.name == "forearm");
                var mid = imported.FirstOrDefault(x => x.name == "middle_distal");
                var midBone = imported.FirstOrDefault(x => x.name == "middle_middle");
                int proximal = imported.Count(x =>
                    x.name.EndsWith("_proximal", StringComparison.Ordinal));
                if (skin == null || wrist == null || forearm == null ||
                    mid == null || midBone == null || proximal < 5 ||
                    skin.bones == null || skin.bones.Length < 20)
                    throw new InvalidOperationException(
                        "New user FBX does not contain the authored skin/five fingers/23-bone rig.");

                float originalReach = Vector3.Distance(oldWrist.position, oldTip.position);
                float nativeReach = Vector3.Distance(wrist.position, mid.position) +
                    .75f * Vector3.Distance(mid.position, midBone.position);
                if (originalReach <= .001f || nativeReach <= .001f)
                    throw new InvalidOperationException("Cannot calibrate new wrist-to-finger reach.");
                float scale = originalReach / nativeReach;
                if (scale < .04f || scale > 3f)
                    throw new InvalidOperationException("Unsafe new hand calibration: " + scale);

                material = new Material(config.originalSkinMaterial);
                material.name = "ROKAS_NEW_GLB_Approved_Player_Material";
                foreach (var anim in instance.GetComponentsInChildren<Animator>(true))
                    anim.enabled = false;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = renderer == skin;
                skin.sharedMaterial = material;
                skin.updateWhenOffscreen = true;
                skin.gameObject.layer = originalSkin.gameObject.layer;
                foreach (var bone in imported)
                    bone.gameObject.layer = originalSkin.gameObject.layer;

                var oldVisuals = previous.Where(x => x == originalSkin ||
                    x == originalSleeve || x == originalNails ||
                    x.name == "ROKAS_Subtle_DorsalHandVeins").ToArray();
                var originalVisibility = oldVisuals.Select(x => x.enabled).ToArray();

                var follower = instance.AddComponent<RokasUserGLBRealBedFollower>();
                follower.Initialize(instance.transform, wrist, oldWrist,
                    oldTip, oldIndex, oldLittle, config.initialPoseClip,
                    scale, oldVisuals, originalVisibility, skin, material,
                    watchRoot, oled, config.studioProfile, presentationCamera);
                material = null; // Follower owns its instance material.
                Debug.Log("[ROKAS NEW GLB PLAYER] ACTIVE in " +
                    Application.platform + ": native user hand vertices=" +
                    skin.sharedMesh.vertexCount + ", clip=" +
                    config.initialPoseClip.name + ", Hand Studio=" +
                    config.studioProfile.name + ", setup=" +
                    ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") +
                    "ms. Old Argon skin hidden; original watch/OLED retained.");
                return true;
            }
            catch (Exception ex)
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
                if (material != null) UnityEngine.Object.Destroy(material);
                Debug.LogError("[ROKAS NEW GLB PLAYER] INSTALL FAILED; " +
                    "original Argon remains usable: " + ex);
                return false;
            }
        }
    }
}
