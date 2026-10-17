#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.Argon.Editor
{
    public sealed class RokasArgonSkinMaterialVisualTests
    {
        [Test]
        [Timeout(180000)]
        public void GenuineSkinPBRFrequencyBalancePreservesMapsAndLooksNaturalInUnity()
        {
            const int W=1280,H=720;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(prefab,Is.Not.Null);
            GameObject[] instances=new GameObject[3];
            GameObject cameraObj=null,lightObj=null;
            RenderTexture rt=null;
            Texture2D readback=null;
            ArgonSkinFilteredPbrCandidate filtered=null;
            Material noNormal=null;
            var active=RenderTexture.active;
            try
            {
                for(int i=0;i<3;i++)
                {
                    instances[i]=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instances[i].name="ROKAS_OriginalHand_PBR_Compare_"+i;
                    foreach(Transform t in instances[i].GetComponentsInChildren<Transform>(true))
                        t.gameObject.layer=29;
                    var animator=instances[i].GetComponentInChildren<Animator>(true);
                    var inspect=animator.runtimeAnimatorController.animationClips
                        .Single(x=>x.name.Contains("SleepArm_InspectIdle"));
                    inspect.SampleAnimation(animator.gameObject,inspect.length*.5f);
                    var safety=new RokasArgonHorizontalHeroPose(instances[i]);
                    safety.CaptureHeroPose();
                    safety.RestoreNaturalAnatomy();
                    instances[i].SetActive(false);
                }

                var renderer=instances[0].GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh");
                var skin=renderer.sharedMaterial;
                var sourceAlbedo=skin.mainTexture;
                var sourceAO=skin.GetTexture("_OcclusionMap");
                var sourceNormal=skin.GetTexture("_BumpMap");
                var sourceRoughness=skin.GetTexture("_MetallicGlossMap");
                Assert.That(sourceAlbedo,Is.Not.Null);
                Assert.That(sourceNormal,Is.Not.Null);
                Assert.That(sourceRoughness,Is.Not.Null);

                noNormal=new Material(skin) {name="ROKAS_PBR_NoNormal_Diagnostic"};
                noNormal.SetTexture("_BumpMap",null);
                noNormal.DisableKeyword("_NORMALMAP");
                noNormal.SetTexture("_MetallicGlossMap",null);
                noNormal.DisableKeyword("_METALLICGLOSSMAP");
                instances[1].GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh").sharedMaterial=noNormal;

                filtered=ArgonSkinFilteredPbrCandidate.Build(skin);
                var mat=filtered.Material;
                Assert.That(mat.mainTexture,Is.SameAs(sourceAlbedo));
                Assert.That(mat.GetTexture("_OcclusionMap"),Is.SameAs(sourceAO));
                Assert.That(mat.GetTexture("_BumpMap"),Is.Not.Null);
                Assert.That(mat.GetTexture("_MetallicGlossMap"),Is.Not.Null);
                Assert.That(mat.GetTexture("_BumpMap"),Is.Not.SameAs(sourceNormal));
                Assert.That(mat.GetTexture("_MetallicGlossMap"),Is.Not.SameAs(sourceRoughness));
                Assert.That(filtered.SoftNormal.width,Is.EqualTo(sourceNormal.width),
                    "Preserve imported native normal-packing and original mip chain");
                Assert.That(filtered.SoftRoughness.width,Is.EqualTo(sourceRoughness.width));
                Assert.That(filtered.SoftNormal.mipmapCount,Is.GreaterThan(1));
                Assert.That(filtered.SoftNormal.mipMapBias,Is.GreaterThanOrEqualTo(2f));
                Assert.That(mat.IsKeywordEnabled("_NORMALMAP"),Is.True,
                    "Filtered skin must retain real normal microdetail, not replace with flat skin");
                Assert.That(mat.IsKeywordEnabled("_METALLICGLOSSMAP"),Is.True,
                    "Use original packed roughness, safely low-pass filtered");
                instances[2].GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh").sharedMaterial=mat;

                cameraObj=new GameObject("ROKAS_OriginalHand_PBR_ComparisonCamera");
                var camera=cameraObj.AddComponent<Camera>();
                camera.enabled=false;
                camera.aspect=(float)W/H;
                camera.fieldOfView=38f;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.15f,.15f,.17f);
                camera.cullingMask=1<<29;
                var hero=new RokasArgonHorizontalHeroPose(instances[0]);
                hero.CaptureHeroPose();
                hero.ConfigureCamera(camera,Vector3.up,.41f);

                lightObj=new GameObject("ROKAS_SoftBedWarmLightPBR_QA");
                var light=lightObj.AddComponent<Light>();
                light.type=LightType.Directional;
                light.intensity=.28f;
                light.color=new Color(1f,.92f,.86f);
                light.cullingMask=1<<29;
                light.transform.rotation=Quaternion.Euler(35,-20,30);
                rt=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32);
                rt.Create();camera.targetTexture=rt;
                readback=new Texture2D(W,H,TextureFormat.RGBA32,false);
                var dest=Path.Combine(Directory.GetCurrentDirectory(),
                    "Artifacts","ArgonSkinPBRComparison");
                Directory.CreateDirectory(dest);
                var filenames=new []{
                    "01_authentic_source_normal_roughness.png",
                    "02_flat_no_maps_diagnostic.png",
                    "03_original_normal_roughness_frequency_filtered.png"
                };
                Color32[][] comparisons=new Color32[3][];
                for(int variant=0;variant<3;variant++)
                {
                    instances[variant].SetActive(true);
                    camera.Render();
                    RenderTexture.active=rt;
                    readback.ReadPixels(new Rect(0,0,W,H),0,0);
                    readback.Apply(false);
                    comparisons[variant]=readback.GetPixels32();
                    File.WriteAllBytes(Path.Combine(dest,filenames[variant]),
                        readback.EncodeToPNG());
                    instances[variant].SetActive(false);
                }

                long changes=0;
                for(int i=0;i<comparisons[0].Length;i+=7)
                {
                    var a=comparisons[0][i];
                    var b=comparisons[2][i];
                    if(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+
                       Mathf.Abs(a.b-b.b)>12)changes++;
                }
                Assert.That(changes,Is.GreaterThan(40),
                    "Filtered genuine skin texture must produce visible PBR differences");
                var summary=
                    "Original source albedo kept: "+sourceAlbedo.name+"\n"+
                    "Original normal: "+sourceNormal.name+" width "+sourceNormal.width+"\n"+
                    "Original packed roughness: "+sourceRoughness.name+" width "+sourceRoughness.width+"\n"+
                    "Native-packed filtered normal width "+filtered.SoftNormal.width+" lodBias "+filtered.SoftNormal.mipMapBias+"\n"+
                    "Native-packed filtered roughness width "+filtered.SoftRoughness.width+" lodBias "+filtered.SoftRoughness.mipMapBias+"\n"+
                    "Visual changed sample pixels: "+changes+"\n"+
                    "Original materials/FBX untouched; see 3 true Unity closeup PNGs";
                File.WriteAllText(Path.Combine(dest,"material_frequency_comparison.txt"),summary);
                Debug.Log("[ARGON GENUINE PBR FILTERED SKIN]\n"+summary);
            }
            finally
            {
                RenderTexture.active=active;
                if(cameraObj!=null)cameraObj.GetComponent<Camera>().targetTexture=null;
                if(readback!=null)UnityEngine.Object.DestroyImmediate(readback);
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                if(noNormal!=null)UnityEngine.Object.DestroyImmediate(noNormal);
                for(int i=0;i<instances.Length;i++)
                    if(instances[i]!=null)UnityEngine.Object.DestroyImmediate(instances[i]);
                if(filtered!=null)filtered.Dispose();
                if(cameraObj!=null)UnityEngine.Object.DestroyImmediate(cameraObj);
                if(lightObj!=null)UnityEngine.Object.DestroyImmediate(lightObj);
            }
        }
    }
}
#endif
