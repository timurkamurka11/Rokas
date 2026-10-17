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
            Rokas.BedSleep.Argon.Candidate.ArgonSkinFilteredPbrCandidate
                combinedPresentationSkin = null;
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
                // Two distinct FBX exports legitimately have different
                // top-level importer axes. Directly copying LOCAL joint
                // rotations warped the entire arm in our prior screenshot.
                // Compare their unmodified rest pose, applying only a rigid
                // presentation-space alignment to the disposable candidate.
                var sourceRoot=sourceJoints["Arm_ForearmRoot"];
                var targetRoot=candidateJoints["Arm_ForearmRoot"];
                var srcLong=(sourceJoints["Middle_Tip"].position-
                    sourceRoot.position).normalized;
                var dstLong=(candidateJoints["Middle_Tip"].position-
                    targetRoot.position).normalized;
                Assert.That(srcLong.sqrMagnitude,Is.GreaterThan(.95f));
                Assert.That(dstLong.sqrMagnitude,Is.GreaterThan(.95f));
                instances[1].transform.rotation=
                    Quaternion.FromToRotation(dstLong,srcLong)*
                    instances[1].transform.rotation;
                var srcAcross=Vector3.ProjectOnPlane(
                    sourceJoints["Little_MCP"].position-
                    sourceJoints["Index_MCP"].position,srcLong).normalized;
                var dstAcross=Vector3.ProjectOnPlane(
                    candidateJoints["Little_MCP"].position-
                    candidateJoints["Index_MCP"].position,srcLong).normalized;
                Assert.That(srcAcross.sqrMagnitude,Is.GreaterThan(.8f));
                Assert.That(dstAcross.sqrMagnitude,Is.GreaterThan(.8f));
                instances[1].transform.rotation=
                    Quaternion.FromToRotation(dstAcross,srcAcross)*
                    instances[1].transform.rotation;
                instances[1].transform.position +=
                    sourceRoot.position-targetRoot.position;
                float finalAxis=Vector3.Dot(srcLong,
                    (candidateJoints["Middle_Tip"].position-
                    targetRoot.position).normalized);
                Assert.That(finalAxis,Is.GreaterThan(.995f),
                    "Rig axes must be aligned before any genuine silhouette comparison");
                int poseMatched=2; // longitudinal axis + palm transverse axis
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
                // High-resolution source-matched anatomy closeups in three
                // actual Unity camera directions. Whole-arm framing alone
                // obscured precisely the MCP root collars we need to fix.
                var baseNames = new []{"Thumb_CMC","Index_MCP","Middle_MCP",
                    "Ring_MCP","Little_MCP"};
                Vector3 rootCenter=Vector3.zero;
                foreach (var name in baseNames)
                    rootCenter += sourceJoints[name].position;
                rootCenter /= baseNames.Length;
                var viewNormals = new []{
                    normal, -normal, Vector3.Cross(axis,normal).normalized
                };
                var viewNames = new []{"dorsal","palmar","side"};
                camera.orthographicSize=Mathf.Max(.025f,span*.21f);
                for(int view=0;view<viewNames.Length;view++)
                {
                    var vn=viewNormals[view];
                    camera.transform.rotation=Quaternion.LookRotation(
                        -vn,Vector3.Cross(axis,vn).normalized);
                    camera.transform.position=rootCenter+
                        vn*Mathf.Max(.5f,span*3f);
                    for(int variant=0;variant<2;variant++)
                    {
                        instances[0].SetActive(variant==0);
                        instances[1].SetActive(variant==1);
                        camera.Render();
                        RenderTexture.active=target;
                        readback.ReadPixels(new Rect(0,0,W,H),0,0);
                        readback.Apply(false);
                        var filename="QA_"+viewNames[view]+"_"+
                            (variant==0?"original":"joined_candidate")+".png";
                        var file=Path.Combine(dir,filename);
                        File.WriteAllBytes(file,readback.EncodeToPNG());
                        Assert.That(new FileInfo(file).Length,Is.GreaterThan(15000),
                            "Real root closeup render is missing "+filename);
                    }
                }
                // The original palm shows a striking dark diagonal X
                // even WITHOUT the new bridges. Isolate whether it comes
                // from true source AO, normal import, or actual geometry.
                // Diagnose with source-only materials, no fake hand texture.
                camera.transform.rotation=Quaternion.LookRotation(
                    -viewNormals[1],Vector3.Cross(axis,viewNormals[1]).normalized);
                camera.transform.position=rootCenter+
                    viewNormals[1]*Mathf.Max(.5f,span*3f);
                camera.orthographicSize=Mathf.Max(.025f,span*.21f);
                instances[0].SetActive(false);
                instances[1].SetActive(true);
                var originalAO=realMaterial.GetTexture("_OcclusionMap");
                var originalNormal=realMaterial.GetTexture("_BumpMap");
                float aoStrength=realMaterial.HasProperty("_OcclusionStrength")?
                    realMaterial.GetFloat("_OcclusionStrength"):1f;
                bool wasNormal=realMaterial.IsKeywordEnabled("_NORMALMAP");
                var diagnostic=new Material(Shader.Find("Unlit/Color"));
                diagnostic.color=Color.white;
                var palmarVariants=new []{
                    "01_actual_pbr_original",
                    "02_original_pbr_AO_disabled",
                    "03_AO_and_normal_disabled",
                    "04_unlit_geometry_only"
                };
                try
                {
                    for(int k=0;k<palmarVariants.Length;k++)
                    {
                        if(k==1)
                        {
                            realMaterial.SetTexture("_OcclusionMap",null);
                            realMaterial.SetFloat("_OcclusionStrength",0);
                        }
                        if(k==2)
                        {
                            realMaterial.SetTexture("_BumpMap",null);
                            realMaterial.DisableKeyword("_NORMALMAP");
                        }
                        joinedSkin.sharedMaterial=k==3?diagnostic:realMaterial;
                        camera.Render();
                        RenderTexture.active=target;
                        readback.ReadPixels(new Rect(0,0,W,H),0,0);
                        readback.Apply(false);
                        var filename="QA_palmar_artifact_"+palmarVariants[k]+".png";
                        var file=Path.Combine(dir,filename);
                        File.WriteAllBytes(file,readback.EncodeToPNG());
                        Assert.That(new FileInfo(file).Length,Is.GreaterThan(8000),
                            "No genuine Unity skin artifact diagnostic "+filename);
                    }
                }
                finally
                {
                    joinedSkin.sharedMaterial=realMaterial;
                    realMaterial.SetTexture("_OcclusionMap",originalAO);
                    realMaterial.SetFloat("_OcclusionStrength",aoStrength);
                    realMaterial.SetTexture("_BumpMap",originalNormal);
                    if(wasNormal)realMaterial.EnableKeyword("_NORMALMAP");
                    UnityEngine.Object.DestroyImmediate(diagnostic);
                }
                // Isolated INTEGRATED HERO preview on authentic generated
                // connected FBX. Keep the earlier camera-matched geometry
                // comparison on the SAME source material, then and only then
                // attach genuine 2048 candidate skin with reduced baked AO.
                combinedPresentationSkin=Rokas.BedSleep.Argon.Candidate
                    .ArgonSkinFilteredPbrCandidate.Build(sourceSkin.sharedMaterial);
                Assert.That(combinedPresentationSkin.Material.GetFloat(
                    "_OcclusionStrength"),Is.InRange(.10f,.14f));
                joinedSkin.sharedMaterial=combinedPresentationSkin.Material;
                instances[0].SetActive(false);
                instances[1].SetActive(true);
                string[] presentationViews={"Dorsal","Palmar"};
                for(int view=0;view<2;view++)
                {
                    var direction=viewNormals[view];
                    camera.transform.rotation=Quaternion.LookRotation(
                        -direction,Vector3.Cross(axis,direction).normalized);
                    camera.orthographicSize=Mathf.Max(.025f,span*.21f);
                    camera.transform.position=rootCenter+
                        direction*Mathf.Max(.5f,span*3f);
                    camera.Render();
                    RenderTexture.active=target;
                    readback.ReadPixels(new Rect(0,0,W,H),0,0);
                    readback.Apply(false);
                    var file=Path.Combine(dir,
                        "QA_COMBINED_CONNECTED_25BONE_SKIN_AO12_"+
                        presentationViews[view]+".png");
                    File.WriteAllBytes(file,readback.EncodeToPNG());
                    Assert.That(new FileInfo(file).Length,Is.GreaterThan(16000),
                        "Connected candidate plus source-faithful PBR evidence missing");
                }
                // Real animated evidence, never a slideshow or AI mock-up.
                // Render 36 successive frames from original and reconstructed
                // authentic 25-bone FBX using the SAME fixed 1280x720 camera,
                // then GitHub Actions produces a side-by-side H.264 video.
                // This is a joint-deformation QA clip, NOT final in-game POV.
                var animatedOriginal=Path.Combine(dir,"AnimatedOriginal");
                var animatedCandidate=Path.Combine(dir,"AnimatedCandidate");
                Directory.CreateDirectory(animatedOriginal);
                Directory.CreateDirectory(animatedCandidate);
                camera.transform.rotation=Quaternion.LookRotation(
                    -viewNormals[0],Vector3.Cross(axis,viewNormals[0]).normalized);
                camera.orthographicSize=Mathf.Max(.025f,span*.24f);
                camera.transform.position=
                    Vector3.Lerp(rootCenter,sourceJoints["Middle_Tip"].position,.3f)
                    +viewNormals[0]*Mathf.Max(.5f,span*3f);
                string[] animatedNames={
                    "Thumb_CMC","Thumb_MCP","Thumb_IP",
                    "Index_MCP","Index_PIP","Index_DIP",
                    "Middle_MCP","Middle_PIP","Middle_DIP",
                    "Ring_MCP","Ring_PIP","Ring_DIP",
                    "Little_MCP","Little_PIP","Little_DIP"
                };
                var rigs=new[]{sourceJoints,candidateJoints};
                var poseTransforms=new Transform[2][];
                var restRotations=new Quaternion[2][];
                for(int variant=0;variant<2;variant++)
                {
                    poseTransforms[variant]=new Transform[animatedNames.Length];
                    restRotations[variant]=new Quaternion[animatedNames.Length];
                    for(int joint=0;joint<animatedNames.Length;joint++)
                    {
                        Assert.That(rigs[variant].ContainsKey(animatedNames[joint]),
                            Is.True,"Missing authentic animation bone "+
                            animatedNames[joint]);
                        var transform=rigs[variant][animatedNames[joint]];
                        poseTransforms[variant][joint]=transform;
                        restRotations[variant][joint]=transform.localRotation;
                    }
                }
                const int motionFrames=36;
                try
                {
                    for(int frame=0;frame<motionFrames;frame++)
                    {
                        float cycle=.5f-.5f*Mathf.Cos(
                            2f*Mathf.PI*frame/(motionFrames-1));
                        for(int variant=0;variant<2;variant++)
                        {
                            for(int joint=0;joint<animatedNames.Length;joint++)
                            {
                                string name=animatedNames[joint];
                                float angle=name.EndsWith("_CMC")?10f:
                                    name.EndsWith("_MCP")?26f:
                                    name.EndsWith("_PIP")?32f:
                                    name.EndsWith("_DIP")?16f:22f;
                                poseTransforms[variant][joint].localRotation=
                                    restRotations[variant][joint]*
                                    Quaternion.AngleAxis(angle*cycle,
                                        Vector3.forward);
                            }
                            instances[0].SetActive(variant==0);
                            instances[1].SetActive(variant==1);
                            camera.Render();
                            RenderTexture.active=target;
                            readback.ReadPixels(new Rect(0,0,W,H),0,0);
                            readback.Apply(false);
                            var outputDir=variant==0?animatedOriginal:
                                animatedCandidate;
                            File.WriteAllBytes(Path.Combine(outputDir,
                                "frame_"+frame.ToString("D3")+".png"),
                                readback.EncodeToPNG());
                        }
                    }
                }
                finally
                {
                    for(int variant=0;variant<2;variant++)
                        for(int joint=0;joint<animatedNames.Length;joint++)
                            poseTransforms[variant][joint].localRotation=
                                restRotations[variant][joint];
                }
                File.WriteAllText(Path.Combine(dir,
                    "real_unity_animation_evidence.txt"),
                    "Genuine Unity 6000.3.19f1 per-frame skin rendering\\n" +
                    "Original LEFT, experimental connected finger candidate RIGHT\\n" +
                    "36 real frames per variant, 12 fps, 3 sec seamless cycle\\n" +
                    "Same authentic 25-bone joints, same authentic 2048 PBR maps (candidate AO12), " +
                    "same stationary camera, 0-26 MCP / 0-32 PIP degrees\\n" +
                    "QA ONLY: not the game animation, not visually approved.\\n");
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
                if (combinedPresentationSkin!=null) combinedPresentationSkin.Dispose();
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
