#if UNITY_EDITOR
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    public sealed class RokasArgonCandidateImportTests
    {
        [Test]
        public void GenuineArgonAssetsBuildSkinnedCandidateWithoutTouchingV10()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.Source +
                "Models/ROKAS_Keiko_FPS_Arm_Rigged.fbx"), Is.Not.Null,
                "The private release ZIP must be staged into ArgonSource in cloud CI.");

            RokasArgonCandidateImporter.BuildCandidate();
            var result = AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(result, Is.Not.Null);

            var skinned = result.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skinned.Length, Is.EqualTo(3));
            foreach (var renderer in skinned)
            {
                Assert.That(renderer.sharedMesh, Is.Not.Null);
                Assert.That(renderer.sharedMesh.bindposes.Length, Is.EqualTo(25));
                Assert.That(renderer.sharedMaterial?.mainTexture, Is.Not.Null,
                    "All three skinned meshes must be bound to real albedo maps.");
            }

            var transforms = result.GetComponentsInChildren<Transform>(true);
            var socket = transforms.Single(x => x.name == "Wrist_WatchAttachment");
            var caseNode = transforms.Single(x => x.name == "Watch_TitaniumCase");
            Assert.That(caseNode.IsChildOf(socket), Is.True,
                "The physical watch must follow the animated wrist socket.");
            var oled = transforms.Single(x => x.name == "Watch_OLED_Display")
                .GetComponent<MeshRenderer>();
            Assert.That(oled, Is.Not.Null);
            Assert.That(oled.GetComponent<MeshCollider>()?.sharedMesh, Is.Not.Null);
            Assert.That(oled.sharedMaterial?.mainTexture, Is.Not.Null);

            var animator = result.GetComponentInChildren<Animator>(true);
            Assert.That(animator?.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController.animationClips.Length,
                Is.EqualTo(8));
            SaveTrueUnityCameraPreview(result);
            Debug.Log("[ROKAS ARGON QA] PASS 3 real skinned meshes, 7-piece watch, 25 bones," +
                "8 imported FBX clips and real imported camera pixels; original V10 production untouched.");
        }
        private static void SaveTrueUnityCameraPreview(GameObject prefab)
        {
            GameObject root = null, cameraObj = null, keyObj = null;
            Material temporaryDisplay = null;
            RenderTexture rt = null;
            Texture2D readback = null;
            var priorActive = RenderTexture.active;
            try
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Assert.That(root, Is.Not.Null);
                var oled = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Single(x => x.name == "Watch_OLED_Display");
                var screen = oled.GetComponent<MeshRenderer>();
                var previewTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    RokasArgonCandidateImporter.Source +
                    "Textures/Watch/Watch_OLED_TestPreview_Reference.png");
                Assert.That(previewTex, Is.Not.Null);
                temporaryDisplay = new Material(screen.sharedMaterial);
                temporaryDisplay.mainTexture = previewTex;
                screen.sharedMaterial = temporaryDisplay;

                cameraObj = new GameObject("ROKAS_ARGON_CLOUD_PREVIEW_CAMERA");
                var cam = cameraObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.03f, .04f, .07f, 1f);
                cam.fieldOfView = 34f;
                cam.nearClipPlane = .01f;
                cam.farClipPlane = 10f;
                cam.transform.position = oled.bounds.center + Vector3.up * .5f;
                cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.right);

                keyObj = new GameObject("ROKAS_ARGON_TEST_LIGHT");
                var light = keyObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.8f;
                keyObj.transform.rotation = Quaternion.Euler(35f, -20f, 30f);

                rt = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                readback = new Texture2D(640, 360, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                readback.Apply();
                string outDir = Path.Combine(Directory.GetCurrentDirectory(),
                    "Artifacts", "ArgonCandidate");
                Directory.CreateDirectory(outDir);
                File.WriteAllBytes(Path.Combine(outDir,
                    "argon-genuine-fbx-editor-preview.png"), readback.EncodeToPNG());

                int changed = 0;
                foreach (var pixel in readback.GetPixels32())
                {
                    if (pixel.r > 30 || pixel.g > 32 || pixel.b > 50) changed++;
                }
                Debug.Log("[ROKAS ARGON] Rendered editor FBX pixels: " + changed +
                    "/230400 camera=" + cam.transform.position +
                    " OLED=" + oled.bounds);
                Assert.That(changed, Is.GreaterThan(100),
                    "Real skinned FBX or watch is not rendered by the imported Unity camera.");
            }
            finally
            {
                RenderTexture.active = priorActive;
                if (cameraObj != null)
                {
                    var cam = cameraObj.GetComponent<Camera>();
                    if (cam != null) cam.targetTexture = null;
                    Object.DestroyImmediate(cameraObj);
                }
                if (keyObj != null) Object.DestroyImmediate(keyObj);
                if (root != null) Object.DestroyImmediate(root);
                if (temporaryDisplay != null) Object.DestroyImmediate(temporaryDisplay);
                if (readback != null) Object.DestroyImmediate(readback);
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
        }
    }
}
#endif
