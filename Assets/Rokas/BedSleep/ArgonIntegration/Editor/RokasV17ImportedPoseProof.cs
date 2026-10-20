#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Real Unity importer + evaluated-skinned-mesh preflight for the V17 candidate.
    /// Does NOT modify any scene, existing V16 rig, watch, OLED, animation graph,
    /// Resources prefab, or user settings. Transient preview instances are destroyed.
    /// It checks the actual ModelImporter output, not Blender's CI report alone.
    /// </summary>
    public static class RokasV17ImportedPoseProof
    {
        private const string Root =
            "Assets/Rokas/BedSleep/ArgonIntegration/V17Candidate/Imported/";
        private const string Base = "ROKAS_Keiko_FPS_Arm_Rigged_V17_Candidate.fbx";

        private struct PoseCheck
        {
            public string Name;
            public float Peak;
            public float MinWeight;
            public float MinDisplacement;
            public PoseCheck(string name, float peak, float minWeight, float minMovement)
            {
                Name = name; Peak = peak; MinWeight = minWeight;
                MinDisplacement = minMovement;
            }
        }
        private static readonly PoseCheck[] Checks =
        {
            new PoseCheck("V17_Grip", .53f, 12f, .004f),
            new PoseCheck("V17_Index", .67f, 12f, .004f),
            new PoseCheck("V17_Living", .155f, 1f, .0003f),
        };

        [MenuItem("ROKAS/Bed Sleep/Argon/Validate V17 imported bone+morph motion (inactive)")]
        public static void ValidateInactiveCandidate()
        {
            var baseModel = AssetDatabase.LoadAssetAtPath<GameObject>(Root + Base);
            if (baseModel == null)
                throw new InvalidOperationException(
                    "V17 base FBX is not imported. Run Stage V17 first.");

            var baseSkin = GetSkin(baseModel);
            if (baseSkin.sharedMesh.vertexCount < 20000 ||
                baseSkin.sharedMesh.vertexCount > 40000)
                throw new InvalidOperationException(
                    "V17 unexpected imported vertex count: " +
                    baseSkin.sharedMesh.vertexCount);
            if (baseSkin.sharedMesh.blendShapeCount < 8)
                throw new InvalidOperationException("V17 lost authored shape keys");

            foreach (var test in Checks)
            {
                string path = Root + "ROKAS_V17_" + test.Name + "_Animated.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                    throw new InvalidOperationException(
                        "V17 paired clip model is missing: " + path);
                var importedSkinned = GetSkin(model);
                var mesh = importedSkinned.sharedMesh;
                if (mesh.vertexCount != baseSkin.sharedMesh.vertexCount ||
                    mesh.blendShapeCount < 8)
                    throw new InvalidOperationException(
                        test.Name + ": imported skinned mesh topology/shape mismatch");
                if (importedSkinned.bones.Length != 25)
                    throw new InvalidOperationException(
                        test.Name + ": lost original 25-bone skeleton");

                var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .Where(c => c != null && c.length > .05f &&
                        !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                    .ToArray();
                if (clips.Length == 0)
                    throw new InvalidOperationException(
                        test.Name + ": no imported playable animation clip");

                // Different FBX exporters may name the clip "Scene", "Take 001",
                // or "RokasV17Rig|Scene". Select by actual shape channels.
                AnimationClip correct = null;
                foreach (var clip in clips)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    bool hasSkin = bindings.Any(b =>
                        b.propertyName.StartsWith("blendShape.",
                            StringComparison.Ordinal));
                    bool hasBone = bindings.Any(b =>
                        b.propertyName.IndexOf("Rotation",
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                        b.propertyName.IndexOf("Euler",
                            StringComparison.OrdinalIgnoreCase) >= 0);
                    if (hasSkin && hasBone)
                    {
                        correct = clip;
                        break;
                    }
                }
                if (correct == null)
                    throw new InvalidOperationException(
                        test.Name + ": no SINGLE Unity clip binds both " +
                        "the deforming armature and BlendShape channels. " +
                        "Exported Blender PASS does not prove Unity animation.");

                var temp = UnityEngine.Object.Instantiate(model);
                temp.name = "__ROKAS_V17_UNSAVED_IMPORT_PROBE__";
                Mesh rest = new Mesh(), bent = new Mesh();
                try
                {
                    // No file/prefab save. Temporarily instantiated in the
                    // currently open Editor scene, then destroyed in finally.
                    foreach (var a in temp.GetComponentsInChildren<Animator>(true))
                        a.enabled = false;
                    var skin = GetSkin(temp);
                    int corrective = skin.sharedMesh.GetBlendShapeIndex(
                        "Index_MCP_Corrective");
                    if (corrective < 0)
                        throw new InvalidOperationException(
                            test.Name + ": index corrective missing after import");

                    correct.SampleAnimation(temp, 0f);
                    skin.BakeMesh(rest);
                    float start = skin.GetBlendShapeWeight(corrective);
                    correct.SampleAnimation(temp,
                        Mathf.Max(.005f, correct.length * test.Peak));
                    skin.BakeMesh(bent);
                    float end = skin.GetBlendShapeWeight(corrective);
                    if (rest.vertexCount != bent.vertexCount ||
                        rest.vertexCount == 0)
                        throw new InvalidOperationException(
                            test.Name + ": skinned mesh vanished during sampling");

                    Vector3[] aVerts = rest.vertices, bVerts = bent.vertices;
                    float max = 0f;
                    int moving = 0;
                    for (int i = 0; i < aVerts.Length; ++i)
                    {
                        float d = (aVerts[i] - bVerts[i]).magnitude;
                        if (d > max) max = d;
                        if (d > .001f) ++moving;
                    }

                    if (end - start < test.MinWeight)
                    {
                        // A true Unity-local failure has two possible causes:
                        // absent/static imported FBX curves, OR imported curve
                        // bindings which do not target the instantiated skin.
                        // Keep QA FAIL (do not fake an animation PASS), but print
                        // exact curve paths and values so the next fix is targeted.
                        Debug.LogError(BuildMorphBindingDiagnostic(
                            test.Name, correct, temp, skin, test.Peak));
                        throw new InvalidOperationException(
                            test.Name + ": Unity-native imported BlendShape is static " +
                            start.ToString("F3") + " -> " + end.ToString("F3") +
                            "; expects >= " + test.MinWeight.ToString("F3") +
                            ". See [ROKAS V17 MORPH BINDING DIAGNOSTIC].");
                    }

                    if (max < test.MinDisplacement)
                        throw new InvalidOperationException(
                            test.Name + ": Unity BakeMesh shows no real " +
                            "finger skin motion, max=" + max.ToString("G6"));
                    if (test.Name != "V17_Living" && moving < 200)
                        throw new InvalidOperationException(
                            test.Name + ": too few skin vertices react: " + moving);

                    // Additional index-only anti-cross-talk: other fingertip
                    // bones should stay put in this isolated authored action.
                    if (test.Name == "V17_Index")
                    {
                        var joints = temp.GetComponentsInChildren<Transform>(true);
                        var tips = new[] { "Thumb_Tip", "Middle_Tip",
                                           "Ring_Tip", "Little_Tip" };
                        correct.SampleAnimation(temp, 0f);
                        var reference = tips.Select(name =>
                        {
                            var t = joints.FirstOrDefault(x => x.name == name);
                            if (t == null)
                                throw new InvalidOperationException(
                                    "V17 index-only QA cannot find " + name);
                            return t.localToWorldMatrix.MultiplyPoint3x4(
                                Vector3.zero);
                        }).ToArray();
                        correct.SampleAnimation(temp, correct.length * test.Peak);
                        for (int i = 0; i < tips.Length; ++i)
                        {
                            var t = joints.First(x => x.name == tips[i]);
                            float drift = Vector3.Distance(
                                t.localToWorldMatrix.MultiplyPoint3x4(
                                    Vector3.zero), reference[i]);
                            if (drift > .0025f)
                                throw new InvalidOperationException(
                                    "V17 Index-only changes " + tips[i] +
                                    " by " + drift.ToString("G5") + "m");
                        }
                    }
                    Debug.Log("[ROKAS V17 UNITY IMPORT PASS] " + test.Name +
                        " clip='" + correct.name +
                        "' indexMCPWeight=" + start.ToString("F2") +
                        " -> " + end.ToString("F2") +
                        " maxSkinDelta=" + max.ToString("G5") + "m" +
                        " movingVertices=" + moving +
                        " | V16 untouched.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(rest);
                    UnityEngine.Object.DestroyImmediate(bent);
                    UnityEngine.Object.DestroyImmediate(temp);
                }
            }
            Debug.Log("[ROKAS V17 UNITY IMPORT QA COMPLETE] " +
                "4 imported FBXs, 3 real animated morph poses checked. " +
                "No scene changes, no V16 replacement, no production enable.");
        }

        /// <summary>
        /// Inspect the actual imported Unity clip, NOT Blender QA metadata.
        /// Diagnostic only: never creates/saves animations or changes production.
        /// </summary>
        private static string BuildMorphBindingDiagnostic(
            string label, AnimationClip clip, GameObject root,
            SkinnedMeshRenderer skin, float peakFraction)
        {
            var lines = new List<string>();
            lines.Add("[ROKAS V17 MORPH BINDING DIAGNOSTIC] " + label);
            lines.Add("clip=" + clip.name + " length=" + clip.length.ToString("F4") +
                " frameRate=" + clip.frameRate.ToString("F2"));
            string actualSkinPath = AnimationUtility.CalculateTransformPath(
                skin.transform, root.transform);
            lines.Add("actualSkinnedRendererPath='" + actualSkinPath +
                "' actualType=" + skin.GetType().FullName);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            lines.Add("floatCurveBindings=" + bindings.Length);
            var morphs = bindings.Where(b =>
                b.propertyName.StartsWith("blendShape.", StringComparison.Ordinal)).ToArray();
            lines.Add("blendShapeBindings=" + morphs.Length);
            var bones = bindings.Where(b => b.type == typeof(Transform) &&
                (b.propertyName.IndexOf("Rotation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 b.propertyName.IndexOf("Euler", StringComparison.OrdinalIgnoreCase) >= 0));
            var unresolvedBones = bones.Where(b =>
                (b.path.Length == 0 ? root.transform : root.transform.Find(b.path)) == null)
                .ToArray();
            lines.Add("boneRotationBindings=" + bones.Count() +
                " unresolvedBoneBindings=" + unresolvedBones.Length);
            foreach (var b in unresolvedBones.Take(8))
                lines.Add("unresolvedBonePath='" + b.path + "' property='" + b.propertyName + "'");
            float peak = Mathf.Clamp(clip.length * peakFraction, 0, clip.length);
            foreach (var b in morphs.Take(30))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                Transform target = b.path.Length == 0 ? root.transform : root.transform.Find(b.path);
                float first = curve != null ? curve.Evaluate(0f) : float.NaN;
                float atPeak = curve != null ? curve.Evaluate(peak) : float.NaN;
                float max = curve != null && curve.keys.Length > 0 ?
                    curve.keys.Max(k => k.value) : float.NaN;
                bool matchesSkin = target == skin.transform &&
                    b.type == typeof(SkinnedMeshRenderer);
                lines.Add("binding path='" + b.path + "' type='" + b.type.FullName +
                    "' property='" + b.propertyName + "' foundTransform=" +
                    (target != null) + " matchesSkinnedRenderer=" + matchesSkin +
                    " curveStart=" + first.ToString("G6") +
                    " curvePeak=" + atPeak.ToString("G6") +
                    " maxKey=" + max.ToString("G6"));
            }
            if (morphs.Length == 0)
                lines.Add("NO imported BlendShape animation bindings");
            lines.Add("Diagnosis: curve values > 0 with matchesSkinnedRenderer=false " +
                "indicate path/type mismatch. All-zero curves indicate FBX export/import " +
                "loss or an incorrect clip. Do not mark Unity PASS without real playback.");
            return string.Join("\\n", lines);
        }

        private static SkinnedMeshRenderer GetSkin(GameObject model)
        {
            var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(s => s.name == "Keiko_Hand_Skin_Mesh");
            if (skin == null || skin.sharedMesh == null)
                throw new InvalidOperationException(
                    "V17 FBX missing named skinned hand renderer: " + model.name);
            return skin;
        }
    }
}
#endif
