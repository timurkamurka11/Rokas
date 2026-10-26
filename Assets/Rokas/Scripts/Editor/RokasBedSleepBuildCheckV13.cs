#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    internal sealed class RokasBedSleepBuildCheckV13 : IPreprocessBuildWithReport
    {
        private const string CompositePath =
            "Assets/Resources/BedSleep/V10ReleaseCandidate/PF_BedSleep_3DComposite_V10_Candidate.prefab";
        private const string ArgonPath =
            "Assets/Resources/BedSleep/ArgonFullHandWatch/PF_Argon_Keiko_Hand_Watch_Preview.prefab";
        public int callbackOrder => -1000;

        [MenuItem("ROKAS/Bed Sleep/Verify Standalone Hand")]
        private static void ValidateMenu() { Validate(); }

        public void OnPreprocessBuild(BuildReport report) { Validate(); }

        private static void Validate()
        {
            var argon = AssetDatabase.LoadAssetAtPath<GameObject>(ArgonPath);
            if (argon == null)
                throw new BuildFailedException(
                    "[ROKAS V13] Argon Resources hand prefab missing: " + ArgonPath);

            // A prefab can exist but contain broken/missing imported FBX
            // children. Prevent the player from silently choosing old visuals.
            bool hasSkin = false, hasSleeve = false, hasFace = false;
            foreach (Transform part in argon.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "Keiko_Hand_Skin_Mesh") hasSkin = true;
                if (part.name == "Keiko_Homewear_Sleeve_Mesh") hasSleeve = true;
                if (part.name == "Watch_OLED_Display") hasFace = true;
            }
            if (!hasSkin || !hasSleeve || !hasFace)
                throw new BuildFailedException(
                    "[ROKAS V13] Argon Resources prefab is incomplete. " +
                    "Skin=" + hasSkin + " sleeve=" + hasSleeve +
                    " physical OLED=" + hasFace + ". Reimport genuine FBX.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompositePath);
            if (prefab == null)
            {
                // Editor-only assets do not automatically enter standalone builds.
                // Only copy an existing authentic V10 composite prefab.
                foreach (var guid in AssetDatabase.FindAssets(
                    "PF_BedSleep_3DComposite_V10_Candidate t:Prefab"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (path == CompositePath || !HasComposite(candidate)) continue;
                    if (!AssetDatabase.IsValidFolder("Assets/Resources/BedSleep/V10ReleaseCandidate"))
                        AssetDatabase.CreateFolder("Assets/Resources/BedSleep", "V10ReleaseCandidate");
                    if (AssetDatabase.CopyAsset(path, CompositePath))
                    {
                        AssetDatabase.SaveAssets();
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompositePath);
                        break;
                    }
                }
            }
            if (!HasComposite(prefab))
                throw new BuildFailedException(
                    "[ROKAS V13] Build cannot include genuine 3D hand. Missing V10 composite Resources prefab: " +
                    CompositePath + ". The old fallback is NOT a valid production build.");

            // V13 proved only the OLD V16 Argon prefab exists. The new
            // user-customized Hand Studio skin was attached by an Editor-only
            // callback and never included in the player. Stage exact local
            // FBX/clip/texture/profile references before accepting the build.
            RokasUserGLBReleaseStager.EnsureReady();

            Debug.Log("[ROKAS V15] Build hand dependency PASS: Argon driver + " +
                "actual approved user-GLB model and calibration staged. " +
                AssetDatabase.GetAssetPath(prefab) + " + " + ArgonPath);
        }

        private static bool HasComposite(GameObject root)
        {
            if (root == null) return false;
            foreach (var component in root.GetComponents<MonoBehaviour>())
                if (component != null &&
                    component.GetType().FullName ==
                    "Rokas.BedSleep.V10Release.BedSleepVisualComposite")
                    return true;
            return false;
        }
    }
}
#endif
