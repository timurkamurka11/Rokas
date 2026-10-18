#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// QA-only renderer inside the REAL ROKAS bed cinematic. The Blender
    /// Argon skin and the game's V10 skin both have 25 bones, but their
    /// skeletons ARE NOT THE SAME. Never bind Argon geometry to V10 bone
    /// transforms: the names, bindposes and helper chains are different.
    ///
    /// Instead instantiate the authentic Argon 25-bone rig INTACT, align its
    /// own Arm_Wrist to actual V10 Wrist_L every frame, and copy only the
    /// relative articulations of five real finger chains. All watches,
    /// OLED controls, V10 sleeve, original nails, HUD, camera and game logic
    /// remain on the normal runtime rig. Game-instance-only, reversible
    /// by stopping Play Mode, never writes original assets.
    /// </summary>
    [InitializeOnLoad]
    public static class RokasArgonInGameHandPreview
    {
        private const string Key = "ROKAS.Argon.ConnectedHand.GamePreview";
        private const string MenuPath =
            "ROKAS/Bed Sleep/Argon/Use corrected hand in actual Bed Sleep (Editor only)";
        private const string Fbx =
            "Assets/Rokas/BedSleep/ArgonIntegration/LocalRootBridgeQA/" +
            "ROKAS_Argon_LocalRootBridge_RIG_QA_ONLY.fbx";
        private static readonly HashSet<int> Installed = new HashSet<int>();
        private static readonly HashSet<int> Rejected = new HashSet<int>();

        static RokasArgonInGameHandPreview()
        {
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                { Installed.Clear(); Rejected.Clear(); }
            };
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool enabled = !EditorPrefs.GetBool(Key, false);
            EditorPrefs.SetBool(Key, enabled);
            Menu.SetChecked(MenuPath, enabled);
            Debug.Log("[ROKAS Argon] Bed Sleep editor-only 25bone skin preview " +
                (enabled ? "ON: enter Play Mode and click the bed" : "OFF"));
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(Key, false));
            return true;
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying || !EditorPrefs.GetBool(Key, false))
                return;
            var game = GameObject.Find("ROKAS_BedSleep_V10_RealFBX_Rig");
            if (game == null) return;
            int id = game.GetInstanceID();
            if (Installed.Contains(id) || Rejected.Contains(id)) return;
            try
            {
                InstallIntoLiveGameOnly(game);
                Installed.Add(id);
            }
            catch (Exception ex)
            {
                Rejected.Add(id);
                Debug.LogError("[ROKAS Argon] QA hand rejected; untouched V10 " +
                    "game visuals preserved. " + ex);
            }
        }

        public static void InstallIntoLiveGameOnly(GameObject game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (asset == null)
                throw new InvalidOperationException(
                    "Run the updated step-7 model-download BAT before entering the bed");
            var current = game.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(x => x.sharedMesh != null && x.sharedMesh.subMeshCount == 3
                    && x.bones != null && x.bones.Length == 25)
                .OrderByDescending(x => x.sharedMesh.vertexCount).FirstOrDefault();
            if (current == null || current.sharedMaterials.Length != 3 ||
                current.sharedMaterials[0] == null)
                throw new InvalidOperationException("Original live V10 skin/sleeve/nails missing");

            // Build from asset itself so the generated mesh continues to
            // reference ITS OWN original 25-bone rest pose and bindposes.
            GameObject rig = null, overlay = null;
            Mesh sleeveMesh = null;
            try
            {
                rig = UnityEngine.Object.Instantiate(asset, game.transform);
                rig.name = "ROKAS_Argon_25Bone_Rig_GameQA_PlayOnly";
                var incoming = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(x => x.sharedMesh != null)
                    .OrderByDescending(x => x.sharedMesh.vertexCount).FirstOrDefault();
                if (incoming == null || incoming.sharedMesh.bindposes.Length != 25
                    || incoming.bones == null || incoming.bones.Length != 25)
                    throw new InvalidOperationException("Actual candidate must retain 25 native bone bindposes");

                var targetBones = current.bones
                    .Where(x => x != null)
                    .ToDictionary(x => x.name, x => x, StringComparer.Ordinal);
                var sourceBones = rig.GetComponentsInChildren<Transform>(true)
                    .GroupBy(x => x.name).ToDictionary(x => x.Key,
                        x => x.First(), StringComparer.Ordinal);
                if (!sourceBones.TryGetValue("Arm_Wrist", out var srcWrist) ||
                    !targetBones.TryGetValue("Wrist_L", out var v10Wrist))
                    throw new InvalidOperationException("Authentic wrist reference missing");
                string[] groups = {"Thumb","Index","Middle","Ring","Little"};
                string[][] from = {
                    new[] {"Thumb_CMC","Thumb_MCP","Thumb_IP"},
                    new[] {"Index_MCP","Index_PIP","Index_DIP"},
                    new[] {"Middle_MCP","Middle_PIP","Middle_DIP"},
                    new[] {"Ring_MCP","Ring_PIP","Ring_DIP"},
                    new[] {"Little_MCP","Little_PIP","Little_DIP"}
                };
                string[] toPrefix = {"Thumb","Index","Middle","Ring","Pinky"};
                var follower = rig.AddComponent<RokasArgon25BoneGameplayRetargetQA>();
                var sourceJoints = new List<Transform>();
                var destinationJoints = new List<Transform>();
                for (int digit=0; digit<groups.Length; ++digit)
                for (int joint=0; joint<3; ++joint)
                {
                    string sourceName = from[digit][joint];
                    string targetName = toPrefix[digit] + "_0" + (joint+1) + "_L";
                    if (!sourceBones.TryGetValue(sourceName, out var a) ||
                        !targetBones.TryGetValue(targetName, out var b))
                        throw new InvalidOperationException("Rig lacks semantic finger pair: " +
                            sourceName + " -> " + targetName);
                    sourceJoints.Add(a); destinationJoints.Add(b);
                }
                var oldBounds = current.sharedMesh.bounds;
                var newBounds = incoming.sharedMesh.bounds;
                float oldExtent=oldBounds.size.magnitude, newExtent=newBounds.size.magnitude;
                if (oldExtent<1e-5f || newExtent<1e-5f ||
                    newExtent/oldExtent<.7f || newExtent/oldExtent>1.4f)
                    throw new InvalidOperationException("Argon/V10 actual geometry sizes are incompatible");

                // All corrections happen on the disposable imported skin
                // GameObject only, retaining the original skinned bind rig.
                foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = renderer == incoming;
                incoming.gameObject.name = "ROKAS_ConnectedHand_QA_PlayOnly";
                incoming.sharedMaterial = current.sharedMaterials[0];
                incoming.updateWhenOffscreen = true;
                foreach (var child in rig.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = current.gameObject.layer;
                follower.Configure(rig.transform, srcWrist, v10Wrist,
                    sourceJoints.ToArray(), destinationJoints.ToArray());
                follower.AlignToRuntime();

                // Only after a complete semantic and position preflight
                // turn off submesh0 on a throw-away clone of V10 game mesh.
                // Leave source skin, seven watch meshes, sleeve & nails.
                sleeveMesh = UnityEngine.Object.Instantiate(current.sharedMesh);
                sleeveMesh.name = "ROKAS_Original_SleeveNails_GameOnly";
                sleeveMesh.SetTriangles(Array.Empty<int>(),0,false);
                current.sharedMesh = sleeveMesh;
                Debug.Log("[ROKAS Argon] Game QA connected native 25-bone hand is " +
                    "live on the real Bed Sleep V10 rig. Original smartwatch/" +
                    "OLED/crown/controller, sleeve, nails and animation preserved. " +
                    "Stop Play Mode to restore source game skin.");
            }
            catch
            {
                if (rig != null) UnityEngine.Object.DestroyImmediate(rig);
                if (overlay != null) UnityEngine.Object.DestroyImmediate(overlay);
                if (sleeveMesh != null) UnityEngine.Object.DestroyImmediate(sleeveMesh);
                throw;
            }
        }
    }

    [DefaultExecutionOrder(32000)]
    internal sealed class RokasArgon25BoneGameplayRetargetQA : MonoBehaviour
    {
        private Transform rigRoot, sourceWrist, targetWrist;
        private Transform[] sourceJoints, targetJoints;
        private Quaternion[] sourceRest, targetRest;
        private bool configured;

        internal void Configure(Transform rig, Transform source, Transform target,
            Transform[] src, Transform[] dst)
        {
            if (rig == null || source == null || target == null
                || src == null || dst == null || src.Length != 15 || dst.Length != 15)
                throw new InvalidOperationException("Retarget QA needs actual 5x3 finger hierarchy");
            rigRoot=rig; sourceWrist=source; targetWrist=target;
            sourceJoints=src;targetJoints=dst;
            sourceRest=src.Select(x=>x.localRotation).ToArray();
            targetRest=dst.Select(x=>x.localRotation).ToArray();
            configured=true;
        }

        internal void AlignToRuntime()
        {
            if (!configured) return;
            // Rotation and translation align the WORLD wrist anchors,
            // while each finger stays parented to its OWN original
            // exported Argon skeleton (no invalid V10 bindpose swap).
            var q=targetWrist.rotation*Quaternion.Inverse(sourceWrist.rotation);
            rigRoot.rotation=q*rigRoot.rotation;
            rigRoot.position+=targetWrist.position-sourceWrist.position;
            for (int i=0;i<sourceJoints.Length;i++)
            {
                Quaternion delta=Quaternion.Inverse(targetRest[i])*targetJoints[i].localRotation;
                sourceJoints[i].localRotation=sourceRest[i]*delta;
            }
        }

        private void LateUpdate() { AlignToRuntime(); }
    }
}
#endif
