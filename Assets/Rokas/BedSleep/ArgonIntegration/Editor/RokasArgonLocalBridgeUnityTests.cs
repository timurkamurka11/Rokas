#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Runs on the real temporary Blender-exported connected-hand FBX in a
    /// GitHub Actions checkout, NOT on production V10 or the source FBX.
    /// Genuine skinned Unity geometry/UV/bone deformation and PNG evidence.
    /// Geometric Blender PASS alone is not final visual approval.
    /// </summary>
    public sealed class RokasArgonLocalBridgeUnityTests
    {
        private const string CandidatePath =
            "Assets/Rokas/BedSleep/ArgonIntegration/LocalRootBridgeQA/ROKAS_Argon_LocalRootBridge_RIG_QA_ONLY.fbx";

        private static SkinnedMeshRenderer Skin(GameObject go)
        {
            return go.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(x => x.sharedMesh != null)
                .OrderByDescending(x => x.sharedMesh.vertexCount)
                .FirstOrDefault();
        }

        [Test]
        [Timeout(180000)]
        public void RealConnectedRootFbxImportsDeformsAndRendersForVisualReview()
        {
            var generated = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePath);
            Assert.That(generated, Is.Not.Null,
                "Only the original 25-bone Blender-derived ROOT BRIDGE FBX is permitted");
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(original, Is.Not.Null);

            GameObject[] instances = new GameObject[2];
            GameObject cameraGo = null, lightGo = null;
            Material realMaterial = null;
            RenderTexture target = null;
            Texture2D readback = null;
            Mesh rest = null, bent = null;
            var savedTarget = RenderTexture.active;
            try
            {
                instances[0] = (GameObject)PrefabUtility.InstantiatePrefab(original);
                instances[1] = (GameObject)PrefabUtility.InstantiatePrefab(generated);
                var sourceSkin = Skin(instances[0]);
                var joinedSkin = Skin(instances[1]);
                Assert.That(sourceSkin, Is.Not.Null);
                Assert.That(joinedSkin, Is.Not.Null,
                    "Export lost the authentic skinned hand renderer");
                Assert.That(sourceSkin.sharedMesh.vertexCount, Is.InRange(12000, 15000));
                Assert.That(joinedSkin.sharedMesh.vertexCount, Is.InRange(12000, 30000),
                    "Unexpected hand corruption/primitive substitute");
                Assert.That(joinedSkin.bones.Length, Is.GreaterThanOrEqualTo(25),
                    "Export destroyed the authentic 25-bone deformation rig");
                Assert.That(joinedSkin.sharedMesh.uv.Length,
                    Is.EqualTo(joinedSkin.sharedMesh.vertexCount),
                    "Original finger/palm UVs not imported");
                Assert.That(joinedSkin.sharedMesh.boneWeights.Length,
                    Is.EqualTo(joinedSkin.sharedMesh.vertexCount),
                    "Blender candidate no longer has real skin weights");

                foreach (var go in instances)
                {
                    foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                        t.gameObject.layer = 28;
                    var chosen = Skin(go);
                    foreach (var render in go.GetComponentsInChildren<Renderer>(true))
                        render.enabled = render == chosen;
                    chosen.updateWhenOffscreen = true;
                }
                // Use real project skin PBR on BOTH original and experimental FBX:
                // this tests topology, not placeholder colors/material differences.
                realMaterial = new Material(sourceSkin.sharedMaterial);
                joinedSkin.sharedMaterial = realMaterial;

                var rootTransform = instances[1].GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "Arm_ForearmRoot");
                var pip = instances[1].GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "Index_PIP");
                var tip = instances[1].GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "Middle_Tip");
                Assert.That(rootTransform, Is.Not.Null);
                Assert.That(pip, Is.Not.Null);
                Assert.That(tip, Is.Not.Null);

                // This MUST actually deform under a finger joint; topology
                // connectedness on paper is not enough for a rigged hand.
                rest = new Mesh();
                bent = new Mesh();
                joinedSkin.BakeMesh(rest);
                var oldRotation = pip.localRotation;
                try
                {
                    pip.localRotation = oldRotation * Quaternion.AngleAxis(18f, Vector3.forward);
                    joinedSkin.BakeMesh(bent);
                }
                finally { pip.localRotation = oldRotation; }
                int moved = 0;
                float maxMove = 0f;
                var before = rest.vertices;
                var after = bent.vertices;
                float threshold = Mathf.Max(1e-6f,
                    joinedSkin.sharedMesh.bounds.size.magnitude * .0004f);
                for (int i = 0; i < before.Length; ++i)
                {
                    var d = Vector3.Distance(before[i], after[i]);
                    if (d > threshold) ++moved;
                    maxMove = Mathf.Max(maxMove, d);
                }
                Assert.That(moved, Is.GreaterThan(35),
                    "Connected exported finger does not bend at its real PIP joint");

                cameraGo = new GameObject("Argon_RealLocalBridge_Camera");
                var camera = cameraGo.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.aspect = 16f / 9f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .14f, .17f, 1f);
                camera.cullingMask = 1 << 28;
                camera.nearClipPlane = .001f;
                camera.farClipPlane = 20f;

                lightGo = new GameObject("Argon_LocalBridge_NeutralSoftLight");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.color = new Color(1f, .93f, .87f);
                light.cullingMask = 1 << 28;
                lightGo.transform.rotation = Quaternion.Euler(45f, 10f, 30f);

                const int W=1280, H=720;
                target = new RenderTexture(W,H,24,RenderTextureFormat.ARGB32);
                target.Create();
                camera.targetTexture = target;
                readback = new Texture2D(W,H,TextureFormat.RGBA32,false);
                var dir = Path.Combine(Directory.GetCurrentDirectory(),
                    "Artifacts", "ArgonLocalBridgeUnity");
                Directory.CreateDirectory(dir);
                var files = new []{
                    "01_original_authentic_hand.png",
                    "02_connected_root_bridge_candidate.png"
                };
                // Strict apples-to-apples: same source camera, light, pose,
                // scale and focal settings for BOTH actual Unity renders.
                // Previous moving camera was NOT a valid silhouette A/B.
                var sourceJoints = instances[0]
                    .GetComponentsInChildren<Transform>(true)
                    .GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                var candidateJoints = instances[1]
                    .GetComponentsInChildren<Transform>(true)
                    .GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                int poseMatched=0;
                foreach (var kv in sourceJoints)
                {
                    if (!candidateJoints.TryGetValue(kv.Key,out var dst)) continue;
                    if (kv.Key.EndsWith("_MCP") || kv.Key.EndsWith("_PIP") ||
                        kv.Key.EndsWith("_DIP") || kv.Key.EndsWith("_CMC") ||
                        kv.Key.EndsWith("_Tip") || kv.Key=="Arm_Wrist")
                    {
                        dst.localRotation=kv.Value.localRotation;
                        ++poseMatched;
                    }
                }
                Assert.That(poseMatched,Is.GreaterThanOrEqualTo(18),
                    "Original and joined hand poses cannot be matched");
                var sourceCuff=sourceJoints["Arm_ForearmRoot"];
                var sourceTip=sourceJoints["Middle_Tip"];
                var axis=(sourceTip.position-sourceCuff.position).normalized;
                Assert.That(axis.sqrMagnitude,Is.GreaterThan(.95f));
                var normal=Vector3.ProjectOnPlane(Vector3.up,axis).normalized;
                if(normal.sqrMagnitude<.5f)
                    normal=Vector3.ProjectOnPlane(Vector3.forward,axis).normalized;
                camera.transform.rotation=Quaternion.LookRotation(
                    -normal,Vector3.Cross(axis,normal).normalized);
                var extents=sourceSkin.bounds.extents;
                float span=Mathf.Max(extents.x,extents.y,extents.z)*2f;
                camera.orthographicSize=Mathf.Max(.03f,span*.83f);
                camera.transform.position=sourceSkin.bounds.center+
                    normal*Mathf.Max(.5f,span*3f);
                for (int variant = 0; variant < 2; ++variant)
                {
                    instances[0].SetActive(variant == 0);
                    instances[1].SetActive(variant == 1);
                    camera.Render();
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0,0,W,H),0,0);
                    readback.Apply(false);
                    var file = Path.Combine(dir, files[variant]);
                    File.WriteAllBytes(file, readback.EncodeToPNG());
                    Assert.That(new FileInfo(file).Length, Is.GreaterThan(30000),
                        "Actual real Unity hand screenshot is missing");
                }
                var report =
                    "Authentic original FBX vertices=" + sourceSkin.sharedMesh.vertexCount +
                    "\nCandidate source-preserving imported vertices=" + joinedSkin.sharedMesh.vertexCount +
                    "\nCandidate bones=" + joinedSkin.bones.Length +
                    "\nIndex_PIP 18deg moved vertices=" + moved +
                    "\nMax bent vertex displacement=" + maxMove +
                    "\nCANDIDATE ONLY: visual review of root junctions required." +
                    "\nDO NOT MERGE INTO V10";
                File.WriteAllText(Path.Combine(dir,"unity_anatomy_report.txt"),report);
                Debug.Log("[ROKAS CONNECTED FINGER QA] " + report);
            }
            finally
            {
                RenderTexture.active = savedTarget;
                if (cameraGo!=null) cameraGo.GetComponent<Camera>().targetTexture = null;
                if (readback!=null) UnityEngine.Object.DestroyImmediate(readback);
                if (target!=null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (rest!=null) UnityEngine.Object.DestroyImmediate(rest);
                if (bent!=null) UnityEngine.Object.DestroyImmediate(bent);
                if (realMaterial!=null) UnityEngine.Object.DestroyImmediate(realMaterial);
                foreach (var instance in instances)
                    if (instance!=null) UnityEngine.Object.DestroyImmediate(instance);
                if (cameraGo!=null) UnityEngine.Object.DestroyImmediate(cameraGo);
                if (lightGo!=null) UnityEngine.Object.DestroyImmediate(lightGo);
            }
        }
        [Test]
        [Timeout(120000)]
        public void AllFiveAuthenticFingerJointsMustActuallyMoveTheirOwnSkin()
        {
            var generated = AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePath);
            Assert.That(generated, Is.Not.Null);
            GameObject instance = null;
            Mesh neutral = null, bent = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(generated);
                var skin = Skin(instance);
                Assert.That(skin, Is.Not.Null);
                var mesh = skin.sharedMesh;
                neutral = new Mesh();
                bent = new Mesh();
                skin.BakeMesh(neutral);
                var original = neutral.vertices;
                var names = new []{"Thumb_IP","Index_PIP","Middle_PIP",
                    "Ring_PIP","Little_PIP"};
                var report = new System.Collections.Generic.List<string>();
                bool allHaveActualMovement = true;
                float threshold = Mathf.Max(1e-6f,
                    mesh.bounds.size.magnitude*.0004f);
                foreach (string name in names)
                {
                    var joint = instance.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(x => x.name == name);
                    Assert.That(joint, Is.Not.Null,
                        "Original 25-bone candidate joint lost: " + name);
                    var originalRotation = joint.localRotation;
                    try
                    {
                        joint.localRotation = originalRotation *
                            Quaternion.AngleAxis(18f,Vector3.forward);
                        skin.BakeMesh(bent);
                        int moved = 0;
                        var vertices = bent.vertices;
                        for (int i=0;i<vertices.Length;i++)
                            if (Vector3.Distance(original[i],vertices[i])>threshold)
                                moved++;
                        report.Add(name + " 18 degree skin movement: " + moved +
                            " vertices");
                        if (moved<35) allHaveActualMovement = false;
                    }
                    finally { joint.localRotation = originalRotation; }
                }
                var output = Path.Combine(Directory.GetCurrentDirectory(),
                    "Artifacts","ArgonLocalBridgeUnity");
                Directory.CreateDirectory(output);
                File.WriteAllLines(Path.Combine(output,
                    "five_digit_weights_diagnostic.txt"),report);
                Debug.Log("[ARGON 5 AUTHENTIC FINGER BONES]\n" +
                    string.Join("\n",report));
                Assert.That(allHaveActualMovement, Is.True,
                    "Visual reject: some real finger bones are present but their " +
                    "source skin vertex groups are absent/unweighted. Do not merge.");
            }
            finally
            {
                if (neutral!=null) UnityEngine.Object.DestroyImmediate(neutral);
                if (bent!=null) UnityEngine.Object.DestroyImmediate(bent);
                if (instance!=null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
#endif
