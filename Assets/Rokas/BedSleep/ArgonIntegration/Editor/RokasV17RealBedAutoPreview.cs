#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Auto-attaches V17 ONLY when the actual ROKAS Hub bed spawns its full
    /// Argon hand/watch in Editor Play Mode. No test scene, no prefab save.
    /// The old watch, OLED, sleeve and game controllers continue unchanged.
    /// </summary>
    [InitializeOnLoad]
    public static class RokasV17RealBedAutoPreview
    {
        private const string Key = "ROKAS.V17.RealBedPreview.Enabled";
        private const string Imported =
            "Assets/Rokas/BedSleep/ArgonIntegration/V17Candidate/Imported/";
        private static readonly HashSet<int> Installed = new HashSet<int>();
        private static readonly HashSet<int> Rejected = new HashSet<int>();

        static RokasV17RealBedAutoPreview()
        {
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    Installed.Clear();
                    Rejected.Clear();
                }
            };
        }

        [MenuItem("ROKAS/Bed Sleep/V17/Enable V17 in REAL Bed Play Mode")]
        private static void Enable()
        {
            EditorPrefs.SetBool(Key, true);
            Debug.Log("[ROKAS V17] ON. Enter the existing ROKAS hub Play Mode and click bed.");
        }

        [MenuItem("ROKAS/Bed Sleep/V17/Disable V17 and return to V16")]
        private static void Disable()
        {
            EditorPrefs.SetBool(Key, false);
            Debug.Log("[ROKAS V17] OFF. Stop and restart Play Mode to return to V16.");
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying || !EditorPrefs.GetBool(Key, true))
                return;
            var game = GameObject.Find("ROKAS_Argon_Complete_Hand_Watch_InGame");
            if (game == null) return; // waiting for real in-game bed click
            int id = game.GetInstanceID();
            if (Installed.Contains(id) || Rejected.Contains(id)) return;
            try { Install(game); Installed.Add(id); }
            catch (Exception error)
            {
                Rejected.Add(id);
                Debug.LogError("[ROKAS V17 REAL BED] V17 NOT installed. " +
                    "Original playable V16 remains: " + error);
            }
        }

        private static void Install(GameObject game)
        {
            string[] filenames =
            {
                "ROKAS_V17_V17_Grip_Animated.fbx",
                "ROKAS_V17_V17_Index_Animated.fbx",
                "ROKAS_V17_V17_Living_Animated.fbx"
            };
            var models = filenames.Select(name =>
                AssetDatabase.LoadAssetAtPath<GameObject>(Imported + name)).ToArray();
            if (models.Any(m => m == null))
                throw new InvalidOperationException("Missing imported V17 FBXs. " +
                    "Do not enable the game candidate without all three native clips.");
            var clips = new AnimationClip[3];
            for (int i = 0; i < models.Length; i++)
            {
                clips[i] = AssetDatabase.LoadAllAssetsAtPath(Imported + filenames[i])
                    .OfType<AnimationClip>().FirstOrDefault(c =>
                        c.length > .05f && AnimationUtility.GetCurveBindings(c).Any(b =>
                            b.propertyName == "blendShape.Index_MCP_Corrective"));
                if (clips[i] == null)
                    throw new InvalidOperationException("No imported morph clip: " + filenames[i]);
            }

            var originalSkin = game.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(s => s.name == "Keiko_Hand_Skin_Mesh" &&
                    s.sharedMesh != null && s.enabled);
            var sourceWrist = game.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Arm_Wrist");
            var sourceTip = game.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Middle_Tip");
            if (originalSkin == null || sourceWrist == null || sourceTip == null ||
                originalSkin.bones == null || originalSkin.bones.Length != 25)
                throw new InvalidOperationException("Full original Argon hand is unavailable. " +
                    "No modifications applied; keep V16.");

            GameObject rig = null;
            try
            {
                // Use the actual animated LIVING FBX: clip targets its own native
                // skinned skeleton. Do NOT swap the V16 renderer's sharedMesh.
                rig = UnityEngine.Object.Instantiate(models[2], game.transform);
                rig.name = "ROKAS_V17_SkinnedHand_RealBed_PlayOnly";
                var skin = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(s => s.name == "Keiko_Hand_Skin_Mesh");
                var wrist = rig.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "Arm_Wrist");
                var tip = rig.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "Middle_Tip");
                if (skin == null || skin.sharedMesh == null || wrist == null || tip == null ||
                    skin.bones == null || skin.bones.Length != 25 ||
                    skin.sharedMesh.blendShapeCount < 8)
                    throw new InvalidOperationException("V17 native 25-bone mesh/morphs absent");

                // Critical: verify every clip addresses this EXACT renderer.
                // A different FBX bone/morph path must stop, not show a static hand.
                string skinPath = AnimationUtility.CalculateTransformPath(
                    skin.transform, rig.transform);
                foreach (var clip in clips)
                {
                    var matches = AnimationUtility.GetCurveBindings(clip).Where(b =>
                        b.path == skinPath &&
                        b.type == typeof(SkinnedMeshRenderer) &&
                        b.propertyName == "blendShape.Index_MCP_Corrective").ToArray();
                    if (matches.Length != 1)
                        throw new InvalidOperationException("V17 clip " + clip.name +
                            " has mismatched skin binding path " + skinPath);
                    var curve = AnimationUtility.GetEditorCurve(clip, matches[0]);
                    if (curve == null || curve.keys.Length == 0 ||
                        curve.keys.Max(k => k.value) < 1f)
                        throw new InvalidOperationException("V17 clip morph animation is absent");
                }

                float oldReach = Vector3.Distance(sourceWrist.position, sourceTip.position);
                float newReach = Vector3.Distance(wrist.position, tip.position);
                if (oldReach < .00001f || newReach < .00001f)
                    throw new InvalidOperationException("Invalid wrist-to-fingertip bind reach");
                float ratio = oldReach / newReach;
                if (ratio < .2f || ratio > 5f)
                    throw new InvalidOperationException("Unsafe imported wrist scale ratio " + ratio);
                if (originalSkin.sharedMaterial == null)
                    throw new InvalidOperationException("Original skin PBR material missing");
                foreach (var animator in rig.GetComponentsInChildren<Animator>(true))
                    animator.enabled = false;
                foreach (var render in rig.GetComponentsInChildren<Renderer>(true))
                    render.enabled = render == skin;
                skin.sharedMaterial = originalSkin.sharedMaterial;
                skin.updateWhenOffscreen = true;
                foreach (var tr in rig.GetComponentsInChildren<Transform>(true))
                    tr.gameObject.layer = originalSkin.gameObject.layer;

                var follow = rig.AddComponent<RokasV17RealBedPoseFollower>();
                follow.Configure(sourceWrist, rig.transform, wrist,
                    originalSkin, skin, clips[0], clips[1], clips[2], ratio);
                Debug.Log("[ROKAS V17 REAL BED INSTALL PASS] Original game bed hand " +
                    "overlay ready, V16 skin kept for rollback, original watch/OLED/sleeve " +
                    "and save unchanged. No new scene and no merge. Ratio=" + ratio);
            }
            catch
            {
                if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
                throw;
            }
        }
    }
}
#endif
