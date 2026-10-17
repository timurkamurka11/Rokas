using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Isolated ROKAS Argon hand rig diagnosis / source-weight reassociation.
    /// Genuine FBX is never modified. The source contains five disconnected
    /// finger islands with some weights mapped to NEIGHBORING finger bones,
    /// confirmed from full topology export on the real source hand.
    ///
    /// This preserves authored vertices, triangles, UVs, original weights,
    /// bindposes and 25 source bone transforms. Only reindexes the four
    /// source bone influences on each disconnected finger island. Must pass
    /// visual QA before replacing the real cinematic renderer's mesh.
    /// </summary>
    public static class ArgonFingerWeightRepair
    {
        public struct Report
        {
            public int Islands;
            public int ReassociatedVertices;
            public string Mapping;
        }

        private static readonly string[][] ExpectedTargets =
        {
            new[] { "Hand_Palm", "Thumb_CMC", "Thumb_MCP", "Thumb_IP" },
            new[] { "Hand_Palm", "Index_MCP", "Index_PIP", "Index_DIP" },
            new[] { "Hand_Palm", "Middle_MCP", "Middle_PIP", "Middle_DIP" },
            new[] { "Hand_Palm", "Ring_MCP", "Ring_PIP", "Ring_DIP" },
            new[] { "Hand_Palm", "Little_MCP", "Little_PIP", "Little_DIP" }
        };


        /// <summary>
        /// Reassigns experimental finger joints while solving a per-vertex
        /// inverse blended skin matrix to KEEP THE CURRENT REST SHAPE intact.
        /// Must be invoked on the freshly instantiated NEUTRAL preview rig
        /// before any hand animation, and visually verified before shipping.
        /// This is temporary candidate-only geometry, not an FBX overwrite.
        /// </summary>
        public static Report ApplyRestPreservingToCandidate(
            SkinnedMeshRenderer skin,out Mesh temporaryMesh)
        {
            temporaryMesh=null;
            if(skin==null||skin.sharedMesh==null)
                throw new ArgumentNullException(nameof(skin));
            var source=skin.sharedMesh;
            var sourceVertices=source.vertices;
            var sourceWeights=source.boneWeights;
            var bindposes=source.bindposes;
            var bones=skin.bones;
            var cpuRest=new Mesh();
            try
            {
                skin.BakeMesh(cpuRest);
                var actualRest=cpuRest.vertices;
                var scope=skin.transform.worldToLocalMatrix;
                Matrix4x4 BlendedMatrix(BoneWeight w)
                {
                    var output=Matrix4x4.zero;
                    void Include(int b,float influence)
                    {
                        if(influence<=0f)return;
                        if(b<0||b>=bones.Length || b>=bindposes.Length)
                            throw new InvalidOperationException(
                                "Invalid original FBX skin bindpose index");
                        var matrix=scope*bones[b].localToWorldMatrix*bindposes[b];
                        for(int row=0;row<4;row++)
                            for(int col=0;col<4;col++)
                                output[row,col]+=matrix[row,col]*influence;
                    }
                    Include(w.boneIndex0,w.weight0);
                    Include(w.boneIndex1,w.weight1);
                    Include(w.boneIndex2,w.weight2);
                    Include(w.boneIndex3,w.weight3);
                    return output;
                }

                // Guard against coordinate-system mismatch (FBX scene scale,
                // Blender axis conversion or differing BakeMesh local space).
                // Without this validation an inverse skin solve could create
                // exploding fingers, even if the maths itself is sound.
                float scale=Mathf.Max(1e-8f,source.bounds.size.magnitude);
                float maxForwardError=0f;
                for(int i=0;i<sourceVertices.Length;i++)
                {
                    if(i%37!=0)continue;
                    var predicted=BlendedMatrix(sourceWeights[i]).
                        MultiplyPoint3x4(sourceVertices[i]);
                    maxForwardError=Mathf.Max(maxForwardError,
                        Vector3.Distance(predicted,actualRest[i]));
                }
                if(maxForwardError>scale*.02f)
                    throw new InvalidOperationException(
                        "FBX skin matrix convention mismatch (error "+
                        maxForwardError.ToString("G9")+
                        ", source span "+scale.ToString("G9")+
                        "). Refusing any vertex inversion.");

                // The original experimental reweight is NOT usable as-is:
                // cloud BakeMesh QA showed 4,895 neutral vertices moved.
                // Make a disposable mesh clone first, then restore its
                // neutral raster by solving weighted NEW bone transforms.
                var report=ApplyToCandidate(skin,out temporaryMesh);
                var nextWeights=temporaryMesh.boneWeights;
                var corrected=temporaryMesh.vertices;
                int changed=0;
                for(int i=0;i<corrected.Length;i++)
                {
                    var a=sourceWeights[i];
                    var b=nextWeights[i];
                    bool changedBones=
                        a.boneIndex0!=b.boneIndex0||
                        a.boneIndex1!=b.boneIndex1||
                        a.boneIndex2!=b.boneIndex2||
                        a.boneIndex3!=b.boneIndex3;
                    if(!changedBones)continue;
                    var forward=BlendedMatrix(b);
                    float det=forward.determinant;
                    if(float.IsNaN(det)||float.IsInfinity(det)||
                        Mathf.Abs(det)<1e-8f)
                        throw new InvalidOperationException(
                            "Singular target skin transform at vertex "+i);
                    var vertex=forward.inverse.MultiplyPoint3x4(actualRest[i]);
                    if(float.IsNaN(vertex.x)||float.IsNaN(vertex.y)||
                        float.IsNaN(vertex.z)||float.IsInfinity(vertex.x)||
                        float.IsInfinity(vertex.y)||float.IsInfinity(vertex.z))
                        throw new InvalidOperationException(
                            "Invalid compensated vertex position at "+i);
                    // A correct neutral-rest inverse often requires
                    // significant coordinate adjustment when source
                    // bindposes were associated with the wrong skeleton.
                    corrected[i]=vertex;
                    changed++;
                }
                temporaryMesh.vertices=corrected;
                temporaryMesh.RecalculateBounds();
                skin.sharedMesh=temporaryMesh;

                // If the corrected neutral skin does not match the original,
                // REJECT the candidate rather than produce another broken
                // hand in the game.
                var verify=new Mesh();
                try
                {
                    skin.BakeMesh(verify);
                    var after=verify.vertices;
                    float maxDelta=0f;
                    for(int i=0;i<after.Length;i++)
                        maxDelta=Mathf.Max(maxDelta,
                            Vector3.Distance(after[i],actualRest[i]));
                    if(maxDelta>scale*.002f)
                        throw new InvalidOperationException(
                            "Bone remap failed original neutral pose safety: "+
                            "max delta="+maxDelta.ToString("G9")+
                            ", model span="+scale.ToString("G9"));
                    report.Mapping+="\nrest compensation vertices="+changed+
                        "; CPU BakeMesh max rest delta="+
                        maxDelta.ToString("G9");
                }
                finally{UnityEngine.Object.DestroyImmediate(verify);}
                return report;
            }
            catch
            {
                // The current user game's imported arm must never retain an
                // unsafe, partially corrected runtime mesh on error.
                skin.sharedMesh=source;
                if(temporaryMesh!=null)
                    UnityEngine.Object.DestroyImmediate(temporaryMesh);
                temporaryMesh=null;
                throw;
            }
            finally{UnityEngine.Object.DestroyImmediate(cpuRest);}
        }

        public static Report ApplyToCandidate(
            SkinnedMeshRenderer skin, out Mesh temporaryMesh)
        {
            temporaryMesh = null;
            if (skin == null || skin.sharedMesh == null)
                throw new ArgumentNullException(nameof(skin));
            var source = skin.sharedMesh;
            int n=source.vertexCount;
            if (source.boneWeights.Length != n || skin.bones.Length != 25)
                throw new InvalidOperationException("Authentic 25-bone Argon mesh required");
            int[] triangles=source.triangles;
            int[] parent=new int[n];
            for(int i=0;i<n;i++)parent[i]=i;
            int Find(int index)
            {
                while(parent[index]!=index)
                {
                    parent[index]=parent[parent[index]];
                    index=parent[index];
                }
                return index;
            }
            void Join(int a,int b)
            {
                a=Find(a);b=Find(b);
                if(a!=b)parent[a]=b;
            }
            for(int k=0;k+2<triangles.Length;k+=3)
            {
                Join(triangles[k],triangles[k+1]);
                Join(triangles[k+1],triangles[k+2]);
            }
            var groups=new Dictionary<int,List<int>>();
            for(int i=0;i<n;i++)
            {
                int root=Find(i);
                if(!groups.TryGetValue(root,out var list))
                    groups.Add(root,list=new List<int>());
                list.Add(i);
            }
            if(groups.Count!=6)
                throw new InvalidOperationException(
                    "Source FBX topology unexpectedly changed: expected six islands");

            var verts=source.vertices;
            var fingers=groups.Values.Where(g=>g.Count>=900 && g.Count<=1800)
                .OrderBy(g=>g.Average(v=>verts[v].y)).ToArray();
            if(fingers.Length!=5)
                throw new InvalidOperationException(
                    "Expected five original disconnected finger meshes");
            var weight=source.boneWeights;
            var names=skin.bones.Select(b=>b.name).ToArray();
            int Index(string name)
            {
                int i=Array.IndexOf(names,name);
                if(i<0)throw new InvalidOperationException("Source bone missing: "+name);
                return i;
            }

            var report=new List<string>();
            int affected=0;
            for(int k=0;k<5;k++)
            {
                var from=new Dictionary<int,float>();
                foreach(int i in fingers[k])
                {
                    BoneWeight w=weight[i];
                    if(w.weight0>.0001f) Add(w.boneIndex0,w.weight0);
                    if(w.weight1>.0001f) Add(w.boneIndex1,w.weight1);
                    if(w.weight2>.0001f) Add(w.boneIndex2,w.weight2);
                    if(w.weight3>.0001f) Add(w.boneIndex3,w.weight3);
                }
                void Add(int index,float influence)
                {
                    if(index<0||index>=skin.bones.Length)
                        throw new InvalidOperationException("Out-of-range FBX bone index");
                    if(from.ContainsKey(index))from[index]+=influence;
                    else from.Add(index,influence);
                }
                // Expect a baked 4-influence procedural finger island. Any
                // unknown layout is a hard stop; don't silently deform hands.
                int[] sourceIndices=from.Where(x=>x.Value>1f)
                    .OrderBy(x=>x.Key).Select(x=>x.Key).ToArray();
                if(sourceIndices.Length!=4)
                    throw new InvalidOperationException("Finger "+k+
                        " source influence count changed: "+sourceIndices.Length+
                        ", observed "+string.Join(",",
                            from.OrderBy(v=>v.Key).Select(v=>
                                names[v.Key]+"="+v.Value.ToString("F2"))));
                int[] target=ExpectedTargets[k].Select(Index).ToArray();
                var indexMap=new Dictionary<int,int>();
                for(int t=0;t<4;t++)
                    indexMap.Add(sourceIndices[t],target[t]);
                report.Add("finger="+k+" "+
                    string.Join(", ",sourceIndices.Select((b,t)=>
                        names[b]+"=>"+names[target[t]])));
                int Map(int sourceIndex,float sourceWeight)
                {
                    if(sourceWeight<.0001f)return sourceIndex;
                    if(!indexMap.TryGetValue(sourceIndex,out int mapped))
                        throw new InvalidOperationException(
                            "Unexpected source weight at finger "+k+
                            " on "+names[sourceIndex]);
                    return mapped;
                }
                foreach(int i in fingers[k])
                {
                    var w=weight[i];
                    w.boneIndex0=Map(w.boneIndex0,w.weight0);
                    w.boneIndex1=Map(w.boneIndex1,w.weight1);
                    w.boneIndex2=Map(w.boneIndex2,w.weight2);
                    w.boneIndex3=Map(w.boneIndex3,w.weight3);
                    weight[i]=w;
                    ++affected;
                }
            }
            temporaryMesh=UnityEngine.Object.Instantiate(source);
            temporaryMesh.name=source.name+"_ArgonCandidate_CorrectFingerBoneAssignments";
            temporaryMesh.boneWeights=weight;
            skin.sharedMesh=temporaryMesh;
            return new Report{Islands=groups.Count,
                ReassociatedVertices=affected, Mapping=string.Join("\n",report)};
        }
    }
}
