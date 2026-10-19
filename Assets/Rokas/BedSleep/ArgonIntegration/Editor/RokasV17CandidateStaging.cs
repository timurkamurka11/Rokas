#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Safely imports the V17 FBX + three animated corrective FBXs from the
    /// checked-in PR artifact folder. NOT a production scene override:
    /// V16 remains the only active game hand until in-game visual approval.
    ///
    /// No test scenes, no EXE, no merge, no runtime root rewiring.
    /// </summary>
    public static class RokasV17CandidateStaging
    {
        private const string Source = "Artifacts/ArgonV17Candidate/";
        private const string Destination =
            "Assets/Rokas/BedSleep/ArgonIntegration/V17Candidate/Imported/";
        private const string Base = "ROKAS_Keiko_FPS_Arm_Rigged_V17_Candidate.fbx";

        private static readonly string[] Labels =
        {
            "V17_Grip", "V17_Index", "V17_Living"
        };

        [Serializable]
        private sealed class BuildMetrics
        {
            public int candidate_components;
            public int exposed_edges;
            public int nonmanifold_edges;
            public string output_fbx;
            public string[] paired_corrective_clip_fbxs;
        }

        [Serializable]
        private sealed class ClipMetrics
        {
            public float corrective_weight_at_start;
            public float corrective_weight_at_peak;
            public float index_skin_max_displacement_m;
            public string result;
        }

        [MenuItem("ROKAS/Bed Sleep/Argon/Stage V17 verified FBXs (inactive)")]
        public static void StageInactiveCandidate()
        {
            // Deliberately never modifies existing live prefab, references
            // or an open scene. This is an editor-only gated import.
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string original = Path.Combine(projectRoot, Source);
            string destination = Path.Combine(projectRoot, Destination);
            if (!Directory.Exists(original))
                throw new InvalidOperationException(
                    "V17 FBXs missing from checked-in PR artifact folder.");

            BuildMetrics build = ReadJson<BuildMetrics>(
                Path.Combine(original, "ROKAS_V17_BuildMetrics.json"));
            if (build.candidate_components != 1 ||
                build.exposed_edges != 0 || build.nonmanifold_edges != 0 ||
                build.output_fbx != Base ||
                build.paired_corrective_clip_fbxs == null ||
                build.paired_corrective_clip_fbxs.Length != 3)
                throw new InvalidOperationException(
                    "V17 geometry or animation manifest has not passed QA.");

            var required = new List<string> { Base };
            foreach (string label in Labels)
            {
                string clip = "ROKAS_V17_" + label + "_Animated.fbx";
                if (!build.paired_corrective_clip_fbxs.Contains(clip))
                    throw new InvalidOperationException(
                        "V17 manifest is missing verified clip " + clip);
                ClipMetrics test = ReadJson<ClipMetrics>(Path.Combine(
                    original, "ROKAS_" + label + "_PairedClipQA.json"));
                if (test.corrective_weight_at_peak <=
                        test.corrective_weight_at_start ||
                    test.index_skin_max_displacement_m <= .001f ||
                    !test.result.StartsWith("PASS:", StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Paired corrective clip failed roundtrip proof: " + label);
                required.Add(clip);
            }

            foreach (string name in required)
            {
                var file = new FileInfo(Path.Combine(original, name));
                if (!file.Exists || file.Length < 100000)
                    throw new InvalidOperationException(
                        "Missing or empty V17 source FBX: " + name);
            }

            string backup = Path.Combine(projectRoot, "Library",
                "ROKAS_V17_StageBackups",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));
            var saved = new Dictionary<string, string>();
            var copied = new List<string>();
            try
            {
                Directory.CreateDirectory(destination);
                Directory.CreateDirectory(backup);
                foreach (string name in required)
                {
                    string target = Path.Combine(destination, name);
                    if (File.Exists(target))
                    {
                        string prior = Path.Combine(backup, name);
                        File.Copy(target, prior, true);
                        saved[name] = prior;
                    }
                    File.Copy(Path.Combine(original, name), target, true);
                    copied.Add(name);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (string name in required)
                {
                    string asset = Destination + name;
                    var importer = AssetImporter.GetAtPath(asset) as ModelImporter;
                    if (importer == null)
                        throw new InvalidOperationException(
                            "Unity did not recognize V17 FBX: " + asset);
                    bool changed = false;
                    if (importer.animationType != ModelImporterAnimationType.Generic)
                    {
                        importer.animationType = ModelImporterAnimationType.Generic;
                        changed = true;
                    }
                    if (!importer.importAnimation)
                    {
                        importer.importAnimation = true;
                        changed = true;
                    }
                    if (!importer.importBlendShapes)
                    {
                        importer.importBlendShapes = true;
                        changed = true;
                    }
                    if (changed) importer.SaveAndReimport();

                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(asset);
                    if (model == null)
                        throw new InvalidOperationException(
                            "Unity V17 model prefab could not be loaded: " + asset);
                    var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .FirstOrDefault(r => r.name == "Keiko_Hand_Skin_Mesh");
                    if (skin == null || skin.sharedMesh == null ||
                        skin.sharedMesh.blendShapeCount < 8)
                        throw new InvalidOperationException(
                            "V17 Unity skin or corrective shapes were lost: " + asset);
                    if (skin.bones == null || skin.bones.Length != 25)
                        throw new InvalidOperationException(
                            "V17 Unity import changed 25-bone hierarchy: " + asset);
                    if (!skin.sharedMesh.HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.TexCoord0))
                        throw new InvalidOperationException(
                            "V17 skin lost material UVs: " + asset);
                    if (!AssetDatabase.LoadAllAssetsAtPath(asset)
                        .OfType<AnimationClip>().Any(c => c != null &&
                            !c.name.StartsWith("__preview__", StringComparison.Ordinal)))
                        throw new InvalidOperationException(
                            "V17 FBX has no playable imported animation: " + asset);
                }
                Debug.Log("[ROKAS V17 INACTIVE STAGING] 4 FBXs imported with " +
                    "25-bone skin, 8+ corrective shapes, preserved UVs and clips. " +
                    "Existing V16 BedSleep prefab, OLED and camera UNCHANGED. " +
                    "Staging path: " + Destination);
            }
            catch
            {
                // Changes are only under the candidate import folder.
                // Restore existing staged FBXs; never touch production files.
                foreach (string name in copied)
                {
                    string target = Path.Combine(destination, name);
                    if (saved.TryGetValue(name, out string old))
                        File.Copy(old, target, true);
                    else if (File.Exists(target))
                        File.Delete(target);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                throw;
            }
        }

        private static T ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Required V17 QA report", path);
            T result = JsonUtility.FromJson<T>(File.ReadAllText(path));
            if (result == null)
                throw new InvalidOperationException(
                    "V17 QA report cannot be parsed: " + path);
            return result;
        }
    }
}
#endif
