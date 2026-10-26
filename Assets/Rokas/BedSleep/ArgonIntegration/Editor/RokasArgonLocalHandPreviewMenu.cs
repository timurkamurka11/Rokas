#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// One-click VISUAL inspection of the actual CI-generated, source-skinned
    /// connected hand. Never edits production Bed Sleep V10 or active scenes.
    /// The FBX comes from an authenticated GitHub Actions artifact, staged by
    /// ROKAS_ARGON_UNITY_PREVIEW.bat; it is not a fake replacement cylinder.
    /// </summary>
    public static class RokasArgonLocalHandPreviewMenu
    {
        public const string CandidatePath =
            "Assets/Rokas/BedSleep/ArgonIntegration/LocalRootBridgeQA/" +
            "ROKAS_Argon_LocalRootBridge_RIG_QA_ONLY.fbx";
        public const string PreviewScene =
            "Assets/Rokas/BedSleep/ArgonIntegration/LocalRootBridgeQA/" +
            "ROKAS_Argon_Hand_Inspection.unity";

        [MenuItem("ROKAS/Bed Sleep/Argon/Open latest connected hand in Unity")]
        public static void OpenCandidatePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "Stop Play Mode before opening the isolated Argon inspection scene.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("ROKAS — hand candidate not staged",
                    "Run Tools/ROKAS/ArgonFingerRetopo/ROKAS_ARGON_UNITY_PREVIEW.bat " +
                    "from the Unity project, then wait for Unity to import the authenticated " +
                    "25-bone FBX. The production V10 is not changed.", "OK");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                    NewSceneMode.Single);
            var root = PrefabUtility.InstantiatePrefab(asset, scene) as GameObject;
            if (root == null)
                throw new InvalidOperationException("Could not instantiate authentic FBX.");
            root.name = "ROKAS_ActualConnectedHand_VisualInspection_NOT_PRODUCTION";
            root.transform.position = Vector3.zero;

            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(x => x.sharedMesh != null).ToArray();
            if (skins.Length == 0)
                throw new InvalidOperationException("Candidate FBX has no SkinnedMeshRenderer");
            var hand = skins.OrderByDescending(x => x.sharedMesh.vertexCount).First();
            if (hand.bones.Length != 25 || hand.sharedMesh.bindposes.Length != 25)
                throw new InvalidOperationException(
                    "Rejecting preview: authentic 25-bone skin/bindposes missing.");

            // Re-use the already committed authentic Argon PBR material. If
            // the material is missing, leave the FBX's imported material:
            // never fabricate a skin texture or replace geometry.
            var skinMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Rokas/BedSleep/ArgonCandidate/Mat_Keiko_Skin.mat");
            if (skinMaterial != null)
                hand.sharedMaterial = skinMaterial;

            Bounds bounds = hand.bounds;
            if (bounds.size.sqrMagnitude < 1e-10f)
                throw new InvalidOperationException("Candidate skin bounds are invalid");
            float extent = Mathf.Max(bounds.size.x,
                Mathf.Max(bounds.size.y, bounds.size.z));
            var cameraObj = new GameObject("ROKAS_Hand_Inspection_Camera");
            var camera = cameraObj.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .14f, .17f);
            camera.orthographic = true;
            camera.orthographicSize = extent * .72f;
            camera.nearClipPlane = Mathf.Max(.0001f, extent * .005f);
            camera.farClipPlane = Mathf.Max(10f, extent * 50f);
            camera.transform.position =
                bounds.center + new Vector3(.75f, 1.0f, -1.5f).normalized * extent * 3.5f;
            camera.transform.LookAt(bounds.center);

            var lightObj = new GameObject("ROKAS_Inspection_Soft_Key");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, .89f, .80f);
            light.intensity = 1.05f;
            lightObj.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            if (!AssetDatabase.IsValidFolder(
                    "Assets/Rokas/BedSleep/ArgonIntegration/LocalRootBridgeQA"))
                AssetDatabase.CreateFolder("Assets/Rokas/BedSleep/ArgonIntegration",
                    "LocalRootBridgeQA");
            if (!EditorSceneManager.SaveScene(scene, PreviewScene))
                throw new InvalidOperationException("Could not save isolated preview scene.");
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("[ROKAS Argon] Opened exact generated candidate hand at " +
                      CandidatePath + " with " + hand.sharedMesh.vertexCount +
                      " vertices and " + hand.bones.Length +
                      " real bones. Scene is QA-only, Bed Sleep V10 unchanged.");
        }

        [MenuItem("ROKAS/Bed Sleep/Argon/Open latest connected hand in Unity", true)]
        private static bool CanPreview()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
#endif
