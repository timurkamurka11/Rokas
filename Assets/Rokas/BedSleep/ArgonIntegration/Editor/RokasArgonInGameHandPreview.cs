#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    // QA only: actual game bed-sleep camera/room/watch/OLED and original
    // animation stay active; only the V10 hand-skin triangles are replaced
    // by the isolated 25-bone connected Argon candidate during Play Mode.
    // No Unity scene/prefab, FBX or V10 source asset is written.
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
            Debug.Log("[ROKAS Argon] Bed Sleep local connected-hand preview " +
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
                Debug.LogError("[ROKAS Argon] QA hand incompatible; original V10 " +
                               "is preserved. Details: " + ex);
            }
        }

        public static void InstallIntoLiveGameOnly(GameObject game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (asset == null)
                throw new InvalidOperationException(
                    "Download the new 25-bone QA FBX using the Step 7 BAT first");
            var incoming = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(s => s.sharedMesh != null)
                .OrderByDescending(s => s.sharedMesh.vertexCount).FirstOrDefault();
            if (incoming == null || incoming.sharedMesh.bindposes.Length != 25 ||
                incoming.bones.Length != 25)
                throw new InvalidOperationException("Actual QA model must contain 25 bone bindings");
            var current = game.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(s => s.sharedMesh != null && s.sharedMesh.subMeshCount == 3 &&
                            s.bones != null && s.bones.Length == 25)
                .OrderByDescending(s => s.sharedMesh.vertexCount).FirstOrDefault();
            if (current == null || current.sharedMaterials.Length != 3 ||
                current.sharedMaterials[0] == null)
                throw new InvalidOperationException("Original V10 skin/sleeve/nails rig missing");
            var dictionary = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var bone in current.bones)
            {
                if (bone == null || dictionary.ContainsKey(bone.name))
                    throw new InvalidOperationException("Ambiguous V10 source bone name");
                dictionary.Add(bone.name, bone);
            }
            var mapped = new Transform[25];
            for (int i = 0; i < 25; i++)
            {
                var original = incoming.bones[i];
                if (original == null || !dictionary.TryGetValue(original.name, out mapped[i]))
                    throw new InvalidOperationException("The connected model's 25 bone names differ");
            }
            Bounds old = current.sharedMesh.bounds, now = incoming.sharedMesh.bounds;
            float oldExtent = old.size.magnitude, newExtent = now.size.magnitude;
            if (oldExtent < 1e-6f || newExtent / oldExtent < .72f ||
                newExtent / oldExtent > 1.35f ||
                Vector3.Distance(old.center, now.center) > oldExtent * .20f)
                throw new InvalidOperationException("Different mesh units or skin local frames");
            // Keep all 3 real V10 material slots but render only original
            // sleeve and nails. Duplicate the source mesh IN MEMORY only.
            Mesh retained = UnityEngine.Object.Instantiate(current.sharedMesh);
            retained.name = "ROKAS_SleeveAndNails_PlayOnly";
            retained.SetTriangles(Array.Empty<int>(), 0, false);
            var overlayObject = new GameObject("ROKAS_ConnectedHand_QA_PlayOnly");
            overlayObject.transform.SetParent(current.transform, false);
            overlayObject.layer = current.gameObject.layer;
            var replacement = overlayObject.AddComponent<SkinnedMeshRenderer>();
            replacement.sharedMesh = incoming.sharedMesh;
            replacement.bones = mapped;
            replacement.rootBone = incoming.rootBone != null &&
                dictionary.TryGetValue(incoming.rootBone.name, out var newRoot)
                ? newRoot : current.rootBone;
            replacement.sharedMaterial = current.sharedMaterials[0];
            replacement.localBounds = incoming.localBounds;
            replacement.updateWhenOffscreen = true;
            replacement.shadowCastingMode = current.shadowCastingMode;
            replacement.receiveShadows = current.receiveShadows;
            current.sharedMesh = retained;
            Debug.Log("[ROKAS Argon] QA hand is live in the real V10 bed " +
                      "cinematic. Original seven-part watch/OLED, sleeve, nails, " +
                      "arm animation and hub remain unchanged. Play-only.");
        }
    }
}
#endif
