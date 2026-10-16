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
                // A soft neutral QA key avoids washing out real skin
                // albedo/normal maps. No texture or geometry substitution.
                light.intensity = 0.75f;
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

                // Authored OLED normal is local +Y; look from the real
                // viewer-facing side, with the forearm naturally extending
                // from the bottom edge rather than from the top of the image.
                Vector3 eyeDirection = (Vector3.up * 0.94f
                    + Vector3.back * 0.25f + Vector3.left * 0.12f).normalized;
                cam.fieldOfView = 38f;
                cam.transform.position = oled.bounds.center + eyeDirection * .24f;
                cam.transform.rotation = Quaternion.LookRotation(-eyeDirection, Vector3.right);
                cam.Render();
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                readback.Apply();
                File.WriteAllBytes(Path.Combine(outDir,
                    "argon-real-oled-viewer-side-closeup.png"), readback.EncodeToPNG());
                int cyanScreenPixels = readback.GetPixels32().Count(p =>
                    p.b > 90 && p.g > 80 && p.b > p.r * 1.35f &&
                    p.g > p.r * 1.25f);
                Debug.Log("[ROKAS ARGON] Physical OLED-facing camera bright cyan pixels: " +
                    cyanScreenPixels + "/230400");
                Assert.That(cyanScreenPixels, Is.GreaterThan(16),
                    "Original watch face is obscured on the actual Unity FBX. " +
                    "Physical seating/UV/material must be corrected, never replaced by a fake screen.");

                // Sample the genuine FBX skeletal clips for mid-raise,
                // watch-inspect, and mid-lower screenshots. No procedural
                // stand-in is used for animation, camera or hand geometry.
                var animator = root.GetComponentInChildren<Animator>(true);
                Assert.That(animator?.runtimeAnimatorController, Is.Not.Null);
                foreach (string state in new[]{
                    "SleepArm_Raise", "SleepArm_InspectIdle", "SleepArm_Lower"})
                {
                    var clip = animator.runtimeAnimatorController.animationClips
                        .Single(x => x.name == state ||
                            x.name.EndsWith("|" + state +
                                "_ROKAS_Keiko_25Bone_GenericRig",
                                System.StringComparison.Ordinal));
                    float t = state == "SleepArm_InspectIdle" ?
                        clip.length * .5f : clip.length * .55f;
                    clip.SampleAnimation(animator.gameObject, t);
                    cam.Render();
                    RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                    readback.Apply();
                    File.WriteAllBytes(Path.Combine(outDir,
                        "argon-genuine-" + state + "-55pct.png"), readback.EncodeToPNG());
                    Debug.Log("[ROKAS ARGON] Genuine FBX clip " + state +
                        " sampled at " + t.ToString("F2") +
                        " seconds. Wrist=" + oled.bounds.center);
                }

                // Separate full-screen 1st-person presentation QA.
                // Use the REAL supplied skinned mesh and imported FBX clips;
                // only the independent render camera and rigid root transform
                // are staged to match the intended lower-left bed entrance.
                var physicalArm = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x => x.name == "Keiko_Hand_Skin_Mesh");
                var shoulder = root.GetComponentsInChildren<Transform>(true)
                    .Single(x => x.name == "Arm_ForearmRoot");
                var realClips = animator.runtimeAnimatorController.animationClips;
                Vector3 startingPosition = root.transform.position;
                var inspectClip = realClips.Single(x =>
                    x.name.EndsWith("|SleepArm_InspectIdle_ROKAS_Keiko_25Bone_GenericRig",
                        System.StringComparison.Ordinal));
                inspectClip.SampleAnimation(animator.gameObject, inspectClip.length * .5f);

                Vector3 faceNormal = oled.transform.TransformDirection(Vector3.up).normalized;
                Vector3 shoulderVector = shoulder.position - oled.bounds.center;
                Vector3 projectedShoulder = Vector3.ProjectOnPlane(shoulderVector, faceNormal);
                if (projectedShoulder.sqrMagnitude < 1e-5f)
                    projectedShoulder = Vector3.ProjectOnPlane(Vector3.forward, faceNormal);
                // The cuff belongs at the BOTTOM, never emerging from ceiling.
                Quaternion bedPovRotation = Quaternion.LookRotation(
                    -faceNormal, -projectedShoulder.normalized);
                Vector3 inspectCenter = oled.bounds.center;
                Vector3 bedPovCamera = inspectCenter + faceNormal * .55f;
                cam.fieldOfView = 42f;
                cam.transform.SetPositionAndRotation(bedPovCamera, bedPovRotation);
                cam.backgroundColor = new Color(.06f, .055f, .065f, 0f);
                var povStages = new[] {
                    new { state="SleepArm_Raise", frac=.15f, slide=-.24f },
                    new { state="SleepArm_Raise", frac=.55f, slide=-.10f },
                    new { state="SleepArm_InspectIdle", frac=.50f, slide=0f },
                    new { state="SleepArm_Lower", frac=.55f, slide=-.10f },
                    new { state="SleepArm_Lower", frac=.90f, slide=-.24f }
                };
                foreach (var stage in povStages)
                {
                    var stageClip = realClips.Single(x =>
                        x.name.EndsWith("|" + stage.state +
                            "_ROKAS_Keiko_25Bone_GenericRig",
                            System.StringComparison.Ordinal));
                    root.transform.position = startingPosition;
                    stageClip.SampleAnimation(animator.gameObject,
                        stageClip.length * stage.frac);
                    root.transform.position = startingPosition +
                        (cam.transform.right + cam.transform.up) * stage.slide;
                    cam.Render();
                    RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                    readback.Apply();
                    string stageName = "argon-bed-pov-" + stage.state + "-" +
                        Mathf.RoundToInt(stage.frac * 100f) + "pct.png";
                    File.WriteAllBytes(Path.Combine(outDir, stageName),
                        readback.EncodeToPNG());
                    Vector3 watchViewport = cam.WorldToViewportPoint(oled.bounds.center);
                    Vector3 cuffViewport = cam.WorldToViewportPoint(physicalArm.bounds.center);
                    Debug.Log("[ROKAS ARGON POV] " + stageName +
                        " watchViewport=" + watchViewport +
                        " armViewport=" + cuffViewport);
                    if (stage.state == "SleepArm_InspectIdle")
                    {
                        Assert.That(watchViewport.z, Is.GreaterThan(0f));
                        Assert.That(watchViewport.x, Is.InRange(.30f, .70f));
                        Assert.That(watchViewport.y, Is.InRange(.27f, .73f));
                        Assert.That(cuffViewport.y, Is.LessThan(watchViewport.y),
                            "Cuff must rise FROM bottom-left, never descend from the ceiling.");
                    }
                }
                root.transform.position = startingPosition;

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
