#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Build-only bridge for the user's authentic converted GLB. Links local
    /// private FBX, authored animation, original albedo and Hand Studio
    /// calibration into one Resources ScriptableObject. Unity follows these
    /// serialized references when building EXE; no private binaries are
    /// copied into the PUBLIC GitHub repository.
    /// </summary>
    internal static class RokasUserGLBReleaseStager
    {
        private const string Root =
            "Assets/Rokas/BedSleep/ArgonIntegration/NewUserGLB/";
        private const string Model = Root + "Imported/ROKAS_NewHand_Rigged.fbx";
        private const string Albedo = Root + "Imported/ROKAS_NewHand_Albedo.jpg";
        private const string Profile = Root + "ROKAS_HandStudioProfile.asset";
        private const string ResourceFolder =
            "Assets/Resources/BedSleep/NewUserGLB";
        private const string ConfigPath =
            ResourceFolder + "/ROKAS_NewHandReleaseConfig.asset";
        private const string MaterialPath =
            ResourceFolder + "/ROKAS_NewHand_ApprovedSkin.mat";

        [MenuItem("ROKAS/Bed Sleep/Prepare New GLB Hand For Build")]
        private static void PrepareFromMenu()
        {
            try
            {
                EnsureReady();
                EditorUtility.DisplayDialog("ROKAS New GLB Hand",
                    "Approved user GLB, animation and Hand Studio calibration are staged for Build & Run.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ROKAS NEW GLB RELEASE] Cannot stage new hand: " + ex);
                EditorUtility.DisplayDialog("ROKAS New GLB Hand", ex.Message, "OK");
            }
        }

        internal static void EnsureReady()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Albedo);
            var profile = AssetDatabase.LoadAssetAtPath<RokasUserHandStudioProfile>(Profile);
            if (model == null || albedo == null || profile == null)
                throw new BuildFailedException(
                    "[ROKAS NEW GLB RELEASE] The current NEW hand is Editor-only " +
                    "because its local FBX/texture/Hand Studio calibration is missing. " +
                    "Required: " + Model + " ; " + Albedo + " ; " + Profile +
                    ". Do not replace these with older Argon or V16 assets.");

            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (!skins.Any(s => s.sharedMesh != null &&
                s.sharedMesh.vertexCount > 30000))
                throw new BuildFailedException(
                    "[ROKAS NEW GLB RELEASE] Imported user FBX has no authentic " +
                    "high-detail skinned hand. Preserve the working Editor source.");

            var clips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>()
                .Where(c => c != null && c.length > 1f &&
                    !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderByDescending(c => c.length).ToArray();
            var clip = clips.FirstOrDefault(c =>
                AnimationUtility.GetCurveBindings(c).Any(b =>
                    b.type == typeof(Transform) &&
                    (b.path.Contains("index_proximal") ||
                     b.path.Contains("middle_proximal")) &&
                    b.propertyName.IndexOf("Rotation",
                        StringComparison.OrdinalIgnoreCase) >= 0));
            if (clip == null)
                throw new BuildFailedException(
                    "[ROKAS NEW GLB RELEASE] Genuine user finger AnimationClip " +
                    "could not be resolved; import Generic rig clips first.");

            if (!AssetDatabase.IsValidFolder(ResourceFolder))
            {
                string parent = "Assets";
                foreach (string segment in ResourceFolder.Substring(7).Split('/'))
                {
                    string next = parent + "/" + segment;
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(parent, segment);
                    parent = next;
                }
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
                throw new BuildFailedException(
                    "[ROKAS NEW GLB RELEASE] Supported skin shader not found.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
                material.shader = shader;
            material.name = "ROKAS_New_UserGLB_OriginalTexture_Release";
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", .35f);
            EditorUtility.SetDirty(material);

            var config =
                AssetDatabase.LoadAssetAtPath<RokasUserGLBReleaseConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RokasUserGLBReleaseConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            config.handModel = model;
            config.initialPoseClip = clip;
            config.originalSkinMaterial = material;
            config.studioProfile = profile;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            // Validate the exact player-available load, not just the file path.
            var staged = AssetDatabase.LoadAssetAtPath<RokasUserGLBReleaseConfig>(
                ConfigPath);
            if (staged == null || !staged.IsComplete)
                throw new BuildFailedException(
                    "[ROKAS NEW GLB RELEASE] Serialization failed; do not build.");

            Debug.Log("[ROKAS NEW GLB RELEASE] Player Dependencies STAGED: " +
                "realFBX=" + Model + " clip=" + clip.name +
                " vertexCount=" + skins.Max(x => x.sharedMesh != null ?
                    x.sharedMesh.vertexCount : 0) +
                " material=" + MaterialPath + " studio=" + Profile +
                ". Build & Run now includes the actual user hand.");
        }
    }
}
#endif
