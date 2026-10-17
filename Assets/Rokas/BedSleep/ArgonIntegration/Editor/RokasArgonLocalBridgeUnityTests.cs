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
                for (int variant = 0; variant < 2; ++variant)
                {
                    instances[0].SetActive(variant == 0);
                    instances[1].SetActive(variant == 1);
                    var skin = variant == 0 ? sourceSkin : joinedSkin;
                    var transforms = instances[variant]
                        .GetComponentsInChildren<Transform>(true);
                    var cuff = transforms.FirstOrDefault(t => t.name == "Arm_ForearmRoot");
                    var finger = transforms.FirstOrDefault(t => t.name == "Middle_Tip");
                    Assert.That(cuff, Is.Not.Null);
                    Assert.That(finger, Is.Not.Null);
                    var axis = (finger.position - cuff.position).normalized;
                    Assert.That(axis.sqrMagnitude, Is.GreaterThan(.95f));
                    var normal = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
                    if (normal.sqrMagnitude < .5f)
                        normal = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
                    camera.transform.rotation = Quaternion.LookRotation(
                        -normal, Vector3.Cross(axis,normal).normalized);
                    var extents = skin.bounds.extents;
                    float span = Mathf.Max(extents.x,extents.y,extents.z) * 2f;
                    camera.orthographicSize = Mathf.Max(.03f, span*.83f);
                    camera.transform.position = skin.bounds.center + normal * Mathf.Max(.5f,span*3f);
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
    }
}
#endif
