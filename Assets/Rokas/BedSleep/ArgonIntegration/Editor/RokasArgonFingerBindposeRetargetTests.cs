#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using Rokas.BedSleep.Argon.Candidate;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Fast 25-bone real FBX vertex-level tests. No static screenshots, long
    /// 24fps videos or unsafe modifications to production V10.
    /// </summary>
    public sealed class RokasArgonFingerBindposeRetargetTests
    {
        private static GameObject MakeOriginal()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(prefab,Is.Not.Null);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        [Test]
        [Timeout(45000)]
        public void TwentyCompensatedFingerSlotsKeepAll12235NeutralVertices()
        {
            GameObject instance=null;
            Mesh disposable=null,before=null,after=null;
            try
            {
                instance=MakeOriginal();
                var skin=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh");
                Mesh source=skin.sharedMesh;
                Transform[] bones=skin.bones;
                before=new Mesh();skin.BakeMesh(before);
                var report=ArgonFingerBindposeRetarget.ApplyCandidate(
                    skin,out disposable,out var rememberedBones);
                after=new Mesh();skin.BakeMesh(after);
                Assert.That(report.Components,Is.EqualTo(6));
                Assert.That(report.RetargetedVertices,Is.EqualTo(6970));
                Assert.That(report.ExtraBoneSlots,Is.EqualTo(20));
                Assert.That(report.MaxNeutralDisplacement,
                    Is.LessThanOrEqualTo(Mathf.Max(1e-7f,
                        source.bounds.size.magnitude*.002f)));
                Assert.That(disposable,Is.Not.SameAs(source));
                Assert.That(source.bindposes.Length,Is.EqualTo(25));
                Assert.That(skin.bones.Length,Is.EqualTo(45));
                Assert.That(rememberedBones.Length,Is.EqualTo(25));
                Assert.That(before.vertexCount,Is.EqualTo(12235));
                Assert.That(after.vertexCount,Is.EqualTo(12235));
                Debug.Log("[ARGON REAL FINGER RETARGET] max neutral drift="+
                    report.MaxNeutralDisplacement.ToString("G9")+
                    " affected="+report.RetargetedVertices+
                    " remap="+report.Mapping);
                skin.sharedMesh=source;
                skin.bones=bones;
                Assert.That(skin.sharedMesh,Is.SameAs(source));
                Assert.That(skin.bones.Length,Is.EqualTo(25));
            }
            finally
            {
                if(disposable!=null)UnityEngine.Object.DestroyImmediate(disposable);
                if(before!=null)UnityEngine.Object.DestroyImmediate(before);
                if(after!=null)UnityEngine.Object.DestroyImmediate(after);
                if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        [Timeout(45000)]
        public void IndexPipOnlyBendsItsOwnRealFingerIsland()
        {
            GameObject instance=null;
            Mesh disposable=null,neutral=null,bent=null;
            try
            {
                instance=MakeOriginal();
                var skin=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh");
                var report=ArgonFingerBindposeRetarget.ApplyCandidate(
                    skin,out disposable,out var originalBones);
                neutral=new Mesh();skin.BakeMesh(neutral);
                Transform index=originalBones.Single(x=>x.name=="Index_PIP");
                Quaternion rest=index.localRotation;
                try
                {
                    index.localRotation=rest*
                        Quaternion.AngleAxis(18f,Vector3.forward);
                    bent=new Mesh();
                    skin.BakeMesh(bent);
                    Vector3[] a=neutral.vertices,b=bent.vertices;
                    float significant=Mathf.Max(3e-7f,
                        skin.sharedMesh.bounds.size.magnitude*.0004f);
                    int[] moved=new int[5];
                    for(int finger=0;finger<5;finger++)
                        foreach(int vi in report.FingerVertices[finger])
                            if((a[vi]-b[vi]).magnitude>significant)
                                ++moved[finger];
                    Debug.Log("[ARGON FINGER ISOLATION] index PIP bend 18deg "+
                        "per island moved="+string.Join(",",moved)+
                        " threshold="+significant.ToString("G9"));
                    Assert.That(moved[1],Is.GreaterThan(30),
                        "Original index finger still does not follow its PIP");
                    for(int k=0;k<5;k++)
                        if(k!=1)Assert.That(moved[k],Is.LessThan(6),
                            "Index PIP incorrectly deforms finger "+k);
                }
                finally{index.localRotation=rest;}
            }
            finally
            {
                if(disposable!=null)UnityEngine.Object.DestroyImmediate(disposable);
                if(neutral!=null)UnityEngine.Object.DestroyImmediate(neutral);
                if(bent!=null)UnityEngine.Object.DestroyImmediate(bent);
                if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        [Test]
        [Timeout(45000)]
        public void LivingHandMovesRealSkinVerticesOnAllFiveFingers()
        {
            // Regression for the V14 defect: rotating bone transforms is not
            // enough when the FBX weights target neighbouring finger joints.
            // Compare actual Unity CPU-skinned positions, not only angles.
            GameObject instance=null;
            Mesh correction=null,frameA=null,frameB=null;
            try
            {
                instance=MakeOriginal();
                var skin=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh");
                var original=skin.sharedMesh;
                var originalBones=skin.bones;
                var report=ArgonFingerBindposeRetarget.ApplyCandidate(
                    skin,out correction,out var rememberedBones);
                Assert.That(report.RetargetedVertices,Is.EqualTo(6970));
                var rig=new ArgonLivingRig(instance);
                Transform wrist=rememberedBones.Single(
                    x=>x.name=="Arm_Wrist");
                Quaternion wristRest=wrist.localRotation;
                Vector3 rootRest=instance.transform.position;
                try
                {
                    rig.NotifyAnimationPoseReset();
                    rig.Apply(1.2f,1f,false,Vector3.right,Vector3.up,
                        1f,false);
                    // Isolate actual finger vertices from wrist oscillation.
                    wrist.localRotation=wristRest;
                    frameA=new Mesh();
                    skin.BakeMesh(frameA);

                    instance.transform.position=rootRest;
                    wrist.localRotation=wristRest;
                    rig.NotifyAnimationPoseReset();
                    rig.Apply(6.1f,1f,false,Vector3.right,Vector3.up,
                        1f,false);
                    wrist.localRotation=wristRest;
                    frameB=new Mesh();
                    skin.BakeMesh(frameB);
                    float threshold=Mathf.Max(2e-7f,
                        original.bounds.size.magnitude*.00035f);
                    var a=frameA.vertices;
                    var b=frameB.vertices;
                    var moved=new int[5];
                    for(int digit=0;digit<5;digit++)
                        foreach(int index in report.FingerVertices[digit])
                            if((a[index]-b[index]).magnitude>threshold)
                                ++moved[digit];
                    Debug.Log("[ROKAS LIVING HAND] actual moving skin "+
                        string.Join(",",moved)+" threshold="+
                        threshold.ToString("G6"));
                    Assert.That(moved[1],Is.GreaterThan(45),
                        "Index pixels cannot animate if retarget is absent");
                    Assert.That(moved[2],Is.GreaterThan(45),
                        "Middle finger must respond to independent motion");
                    Assert.That(moved[3],Is.GreaterThan(45),
                        "Ring finger must respond to independent motion");
                    Assert.That(moved[4],Is.GreaterThan(20),
                        "Little finger must flex gently rather than remain frozen");
                    Assert.That(moved[0],Is.GreaterThan(15),
                        "Thumb must have a separate live curl");
                }
                finally
                {
                    skin.sharedMesh=original;
                    skin.bones=originalBones;
                    wrist.localRotation=wristRest;
                    instance.transform.position=rootRest;
                }
            }
            finally
            {
                if(correction!=null)UnityEngine.Object.DestroyImmediate(correction);
                if(frameA!=null)UnityEngine.Object.DestroyImmediate(frameA);
                if(frameB!=null)UnityEngine.Object.DestroyImmediate(frameB);
                if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        [Timeout(45000)]
        public void FingerSocketBlendLeavesOriginalPalmAndDistalGeometryUntouched()
        {
            GameObject instance=null;
            Mesh candidate=null;
            try
            {
                instance=MakeOriginal();
                var skin=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh");
                var source=skin.sharedMesh;
                var originalBones=skin.bones;
                Vector3[] a=source.vertices;
                var b=ArgonFingerRootSocketBlend.ApplyToClone(skin,out candidate);
                Vector3[] p=candidate.vertices;
                Assert.That(b.SourceVertices,Is.EqualTo(12235));
                Assert.That(b.FingerIslands,Is.EqualTo(5));
                Assert.That(b.ModifiedVertices,Is.GreaterThan(200));
                Assert.That(b.ModifiedVertices,Is.LessThan(3000));
                Assert.That(b.MaxVertexMove,Is.LessThan(
                    source.bounds.size.magnitude*.10f));
                Assert.That(candidate.triangles,Is.EqualTo(source.triangles),
                    "Original true finger triangles must remain intact");
                Assert.That(candidate.uv,Is.EqualTo(source.uv));
                Assert.That(candidate.boneWeights.Length,
                    Is.EqualTo(source.boneWeights.Length));
                // Verified original FBX component [0..5264] is the palm and
                // forearm (reported by real source topology diagnostic).
                for(int i=0;i<5265;i++)
                    Assert.That(p[i],Is.EqualTo(a[i]),
                        "Original wrist/palm vertex moved unexpectedly at "+i);
                int distalUntouched=0;
                for(int i=5265;i<a.Length;i++)
                    if(p[i]==a[i])distalUntouched++;
                Assert.That(distalUntouched,Is.GreaterThan(4000),
                    "Too much distal finger mesh edited");
                skin.sharedMesh=source;
                Assert.That(skin.bones.Length,Is.EqualTo(originalBones.Length));
                Debug.Log("[ARGON FINGER SOCKET] candidate-only proximal edit "+
                    b.ModifiedVertices+" vertices; palm=5265 untouched; "+
                    "distal unchanged="+distalUntouched+
                    "; max displacement="+b.MaxVertexMove.ToString("G9"));
            }
            finally
            {
                if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate);
                if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);
            }
        }

    }
}
#endif
