#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rokas.BedSleep.Argon.Candidate;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Minimal visual acceptance gate: three SEPARATE real FBX prefab instances
    /// and three real Unity renders at the same approved horizontal position.
    /// Avoids old false-positive cached screenshot comparisons from swapping
    /// a sharedMesh on one existing SkinnedMeshRenderer between frames.
    /// </summary>
    public sealed class RokasArgonFingerVisualCompareTests
    {
        [Test]
        [Timeout(180000)]
        public void OriginalVersusSafeRetargetVersusProximalSocketOnRealHand()
        {
            const int width=1280,height=720;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(prefab,Is.Not.Null);
            GameObject[] roots=new GameObject[3];
            Mesh[] temps=new Mesh[3];
            GameObject cameraObj=null,lightObj=null;
            RenderTexture rt=null;
            Texture2D capture=null;
            var prior=RenderTexture.active;
            try
            {
                for(int i=0;i<3;i++)
                {
                    roots[i]=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    roots[i].name="ARGON_QA_AuthenticSkin_variant_"+i;
                    foreach(Transform t in roots[i].GetComponentsInChildren<Transform>(true))
                        t.gameObject.layer=29;
                    var animator=roots[i].GetComponentInChildren<Animator>(true);
                    var clip=animator.runtimeAnimatorController.animationClips
                        .Single(x=>x.name.Contains("SleepArm_InspectIdle"));
                    clip.SampleAnimation(animator.gameObject,clip.length*.5f);
                    var pose=new RokasArgonHorizontalHeroPose(roots[i]);
                    pose.CaptureHeroPose();
                    pose.RestoreNaturalAnatomy();
                }
                cameraObj=new GameObject("ROKAS_Argon_TrueSkinComparison_Camera");
                var camera=cameraObj.AddComponent<Camera>();
                camera.enabled=false;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.13f,.17f,.22f,1f);
                camera.cullingMask=1<<29;
                camera.aspect=(float)width/height;
                var framing=new RokasArgonHorizontalHeroPose(roots[0]);
                framing.CaptureHeroPose();
                framing.ConfigureCamera(camera,Vector3.up,.41f);

                var baseline=new SkinnedMeshRenderer[3];
                for(int i=0;i<3;i++)
                    baseline[i]=roots[i].GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Single(x=>x.name=="Keiko_Hand_Skin_Mesh");

                var w1=ArgonFingerBindposeRetarget.ApplyCandidate(
                    baseline[1],out temps[1],out var originalBones1);
                var w2=ArgonFingerBindposeRetarget.ApplyCandidate(
                    baseline[2],out var compensated2,out var originalBones2);
                // Save both disposable clones for safe final cleanup.
                // The socket clone is a second allocation over compensated2.
                Mesh socket=null;
                var soft=ArgonFingerRootSocketBlend.ApplyToClone(
                    baseline[2],out socket);
                temps[2]=socket;
                Assert.That(w1.RetargetedVertices,Is.EqualTo(6970));
                Assert.That(w2.ExtraBoneSlots,Is.EqualTo(20));
                Assert.That(soft.FingerIslands,Is.EqualTo(5));
                Assert.That(originalBones1.Length,Is.EqualTo(25));
                Assert.That(originalBones2.Length,Is.EqualTo(25));

                for(int i=0;i<3;i++)
                {
                    var living=new ArgonLivingRig(roots[i]);
                    living.NotifyAnimationPoseReset();
                    living.Apply(1.35f,1f,true,framing.Right,framing.Up);
                    roots[i].SetActive(false);
                }
                lightObj=new GameObject("ROKAS_Argon_RealHand_SoftCeilingLight");
                var light=lightObj.AddComponent<Light>();
                light.type=LightType.Directional;
                light.intensity=.30f;
                light.color=new Color(1f,.86f,.78f);
                light.cullingMask=1<<29;
                lightObj.transform.rotation=Quaternion.Euler(35,-20,30);
                rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
                rt.Create();
                camera.targetTexture=rt;
                capture=new Texture2D(width,height,TextureFormat.RGBA32,false);
                string dir=Path.Combine(Directory.GetCurrentDirectory(),
                    "Artifacts","ArgonFingerVisualCompare");
                Directory.CreateDirectory(dir);
                string[] filenames={
                    "01_original_authentic_FBXSkin.png",
                    "02_compensated_correct_joint_bindposes.png",
                    "03_correct_joint_bindposes_plus_socket.png"
                };
                Color32[][] pixels=new Color32[3][];
                for(int i=0;i<3;i++)
                {
                    roots[i].SetActive(true);
                    camera.Render();
                    RenderTexture.active=rt;
                    capture.ReadPixels(new Rect(0,0,width,height),0,0);
                    capture.Apply(false);
                    pixels[i]=capture.GetPixels32();
                    int skin=0;
                    for(int y=145;y<600;y+=3)
                    for(int x=480;x<1230;x+=3)
                    {
                        var p=pixels[i][y*width+x];
                        if(p.r>90 && p.r>p.g*1.09f &&
                           p.g>p.b*.97f && p.g>62)
                            ++skin;
                    }
                    Debug.Log("[ARGON 3D FINGER VISUAL] "+filenames[i]+
                        " skin ROI samples="+skin);
                    Assert.That(skin,Is.GreaterThan(700),
                        "Real skinned hand missing: no valid finger comparison");
                    string dest=Path.Combine(dir,filenames[i]);
                    File.WriteAllBytes(dest,capture.EncodeToPNG());
                    Assert.That(new FileInfo(dest).Length,Is.GreaterThan(80000));
                    roots[i].SetActive(false);
                }
                // A completely identical outcome means the supposedly fresh
                // renderer is serving stale skinning. Report and stop rather
                // than claiming the deforming hand has been fixed.
                int differ01=0,differ12=0;
                for(int i=0;i<pixels[0].Length;i+=3)
                {
                    Color32 a=pixels[0][i],b=pixels[1][i],c=pixels[2][i];
                    if(Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+
                       Mathf.Abs(a.b-b.b)>18)differ01++;
                    if(Mathf.Abs(b.r-c.r)+Mathf.Abs(b.g-c.g)+
                       Mathf.Abs(b.b-c.b)>18)differ12++;
                }
                string report="original vs compensated differences="+differ01+
                    "\ncompensated vs socket differences="+differ12+
                    "\nneutral pose drift="+w1.MaxNeutralDisplacement+
                    "\nsource five finger components=5";
                File.WriteAllText(Path.Combine(dir,"comparison.txt"),report);
                Debug.Log("[ARGON 3D ANATOMY COMPARISON] "+report);
                Assert.That(differ01,Is.GreaterThan(70),
                    "Retargeted finger rendering identical to original; QA inconclusive");
                Assert.That(differ12,Is.GreaterThan(70),
                    "Socket geometry not visible; QA inconclusive");
                UnityEngine.Object.DestroyImmediate(compensated2);
                compensated2=null;
            }
            finally
            {
                RenderTexture.active=prior;
                if(cameraObj!=null)
                {
                    var cam=cameraObj.GetComponent<Camera>();
                    if(cam!=null)cam.targetTexture=null;
                }
                if(capture!=null)UnityEngine.Object.DestroyImmediate(capture);
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                for(int i=0;i<roots.Length;i++)
                {
                    if(roots[i]!=null)UnityEngine.Object.DestroyImmediate(roots[i]);
                    if(temps[i]!=null)UnityEngine.Object.DestroyImmediate(temps[i]);
                }
                if(cameraObj!=null)UnityEngine.Object.DestroyImmediate(cameraObj);
                if(lightObj!=null)UnityEngine.Object.DestroyImmediate(lightObj);
            }
        }
    }
}
#endif
