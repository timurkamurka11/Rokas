using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Experimental non-destructive skinning repair for the original Argon
    /// 25-bone FBX. Each detached finger contains influences accidentally
    /// assigned to the preceding finger. Simply renaming bone indices
    /// changes THOUSANDS of rest-pose vertices, as measured in cloud Unity.
    ///
    /// This method APPENDS per-island target bone slots and compensates each
    /// slot's bindpose to keep the same original neutral geometry:
    /// NewTargetWorld * NewBindpose == OldSourceWorld * OldBindpose.
    /// The original 25 slots remain unmodified; the 5 * 4 new slots use
    /// existing authentic target transforms, not a generated skeleton.
    /// It never edits the source FBX, mesh or original bone transforms.
    /// </summary>
    public static class ArgonFingerBindposeRetarget
    {
        public struct Report
        {
            public int Components;
            public int RetargetedVertices;
            public int ExtraBoneSlots;
            public float MaxNeutralDisplacement;
            public string Mapping;
            public int[][] FingerVertices;
        }

        private static readonly string[][] ActualFingerTargets =
        {
            new[] { "Hand_Palm", "Thumb_CMC", "Thumb_MCP", "Thumb_IP" },
            new[] { "Hand_Palm", "Index_MCP", "Index_PIP", "Index_DIP" },
            new[] { "Hand_Palm", "Middle_MCP", "Middle_PIP", "Middle_DIP" },
            new[] { "Hand_Palm", "Ring_MCP", "Ring_PIP", "Ring_DIP" },
            new[] { "Hand_Palm", "Little_MCP", "Little_PIP", "Little_DIP" }
        };

        public static Report ApplyCandidate(SkinnedMeshRenderer hand,
            out Mesh disposableMesh, out Transform[] originalBones)
        {
            disposableMesh=null;
            originalBones=hand!=null?hand.bones:null;
            if(hand==null || hand.sharedMesh==null)
                throw new ArgumentNullException(nameof(hand));
            Mesh original=hand.sharedMesh;
            if(original.vertexCount!=12235 || originalBones.Length!=25 ||
                original.bindposes.Length!=25 ||
                original.boneWeights.Length!=original.vertexCount)
                throw new InvalidOperationException(
                    "Only verified original Argon 12,235-vertex 25-bone hand is supported.");

            Mesh before=new Mesh();
            Mesh after=null;
            try
            {
                // This is a genuine Unity CPU-skinned reference, not guessed
                // positions from mesh.vertices in FBX importer coordinates.
                hand.BakeMesh(before);
                Vector3[] rest=before.vertices;
                int n=original.vertexCount;
                var parent=new int[n];
                for(int i=0;i<n;i++)parent[i]=i;
                int Find(int x)
                {
                    while(parent[x]!=x)
                    {
                        parent[x]=parent[parent[x]];
                        x=parent[x];
                    }
                    return x;
                }
                void Join(int a,int b)
                {
                    a=Find(a);b=Find(b);
                    if(a!=b)parent[a]=b;
                }
                int[] faces=original.triangles;
                for(int i=0;i+2<faces.Length;i+=3)
                {
                    Join(faces[i],faces[i+1]);
                    Join(faces[i+1],faces[i+2]);
                }
                var groups=new Dictionary<int,List<int>>();
                for(int i=0;i<n;i++)
                {
                    int root=Find(i);
                    if(!groups.TryGetValue(root,out var group))
                        groups.Add(root,group=new List<int>());
                    group.Add(i);
                }
                if(groups.Count!=6)
                    throw new InvalidOperationException(
                        "Expected palm and five original finger mesh islands.");
                Vector3[] v=original.vertices;
                var fingers=groups.Values.Where(g=>g.Count==1394)
                    .OrderBy(g=>g.Average(i=>v[i].y)).ToArray();
                if(fingers.Length!=5)
                    throw new InvalidOperationException(
                        "Authentic FBX finger component sizes changed.");

                BoneWeight[] changedWeights=original.boneWeights;
                Matrix4x4[] originalBind=original.bindposes;
                var extendedBones=new List<Transform>(originalBones);
                var extendedBind=new List<Matrix4x4>(originalBind);
                string[] names=originalBones.Select(b=>b.name).ToArray();
                var mapReport=new List<string>();
                int retargeted=0;
                for(int finger=0;finger<5;finger++)
                {
                    var hist=new Dictionary<int,double>();
                    void Count(int bone,float weight)
                    {
                        if(weight<=.0001f)return;
                        if(bone<0||bone>=25)
                            throw new InvalidOperationException("Invalid source FBX bone index");
                        if(hist.ContainsKey(bone)) hist[bone]+=weight;
                        else hist.Add(bone,weight);
                    }
                    foreach(int i in fingers[finger])
                    {
                        var w=changedWeights[i];
                        Count(w.boneIndex0,w.weight0);
                        Count(w.boneIndex1,w.weight1);
                        Count(w.boneIndex2,w.weight2);
                        Count(w.boneIndex3,w.weight3);
                    }
                    int[] from=hist.Where(kv=>kv.Value>1.0)
                        .Select(kv=>kv.Key).OrderBy(k=>k).ToArray();
                    if(from.Length!=4)
                        throw new InvalidOperationException(
                            "Finger "+finger+" must have 4 major source influences");
                    var slots=new Dictionary<int,int>();
                    for(int k=0;k<4;k++)
                    {
                        int src=from[k];
                        string targetName=ActualFingerTargets[finger][k];
                        int dst=Array.IndexOf(names,targetName);
                        if(dst<0)
                            throw new InvalidOperationException(
                                "Original target joint missing: "+targetName);
                        Transform oldBone=originalBones[src];
                        Transform target=originalBones[dst];
                        // Preserve source mesh's *actual imported bindpose*;
                        // the new joint at rest has the exact same skinning
                        // matrix as the old joint before retargeting.
                        Matrix4x4 compensatedBind=
                            target.worldToLocalMatrix*
                            oldBone.localToWorldMatrix*
                            originalBind[src];
                        int slot=extendedBones.Count;
                        extendedBones.Add(target);
                        extendedBind.Add(compensatedBind);
                        slots.Add(src,slot);
                        mapReport.Add("island "+finger+": "+names[src]+
                            " -> "+targetName+" (new slot "+slot+")");
                    }
                    int Resolve(int slot,float weight)
                    {
                        if(weight<=.0001f)return slot;
                        if(!slots.TryGetValue(slot,out int next))
                            throw new InvalidOperationException(
                                "Unexpected influence in original finger "+finger);
                        return next;
                    }
                    foreach(int i in fingers[finger])
                    {
                        BoneWeight w=changedWeights[i];
                        w.boneIndex0=Resolve(w.boneIndex0,w.weight0);
                        w.boneIndex1=Resolve(w.boneIndex1,w.weight1);
                        w.boneIndex2=Resolve(w.boneIndex2,w.weight2);
                        w.boneIndex3=Resolve(w.boneIndex3,w.weight3);
                        changedWeights[i]=w;
                        retargeted++;
                    }
                }
                disposableMesh=UnityEngine.Object.Instantiate(original);
                disposableMesh.name=original.name+
                    "_QA_RestSafe_CorrectFingerJoints_Clone";
                disposableMesh.bindposes=extendedBind.ToArray();
                disposableMesh.boneWeights=changedWeights;
                hand.bones=extendedBones.ToArray();
                hand.sharedMesh=disposableMesh;
                after=new Mesh();
                hand.BakeMesh(after);
                Vector3[] now=after.vertices;
                if(now.Length!=rest.Length)
                    throw new InvalidOperationException("Bone retarget changed vertex count");
                float max=0f;
                for(int i=0;i<now.Length;i++)
                {
                    if(!IsFinite(now[i]))
                        throw new InvalidOperationException(
                            "Retargeted finger vertex nonfinite: "+i);
                    max=Mathf.Max(max,Vector3.Distance(now[i],rest[i]));
                }
                float tolerance=Mathf.Max(1e-7f,
                    original.bounds.size.magnitude*.002f);
                if(max>tolerance)
                    throw new InvalidOperationException(
                        "Rest-pose safety gate rejected compensated joints: "+
                        "max="+max.ToString("G9")+", tol="+
                        tolerance.ToString("G9"));
                return new Report {
                    Components=groups.Count,
                    RetargetedVertices=retargeted,
                    ExtraBoneSlots=extendedBones.Count-originalBones.Length,
                    MaxNeutralDisplacement=max,
                    Mapping=string.Join("\n",mapReport),
                    FingerVertices=fingers.Select(x=>x.ToArray()).ToArray()
                };
            }
            catch
            {
                // Restore BOTH skinning arrays before destroying candidate.
                hand.sharedMesh=original;
                hand.bones=originalBones;
                if(disposableMesh!=null)
                {
                    UnityEngine.Object.DestroyImmediate(disposableMesh);
                    disposableMesh=null;
                }
                throw;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(before);
                if(after!=null)UnityEngine.Object.DestroyImmediate(after);
            }
        }
        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) &&
                !float.IsNaN(v.z) && !float.IsInfinity(v.x) &&
                !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
