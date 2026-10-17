using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Conservative copy-on-write seam polish for the AUTHENTIC Argon hand.
    /// Measured on the real FBX by cloud Unity: 6 disconnected triangle
    /// islands: 5,265-vertex forearm/palm and five 1,394-vertex fingers.
    /// Softly tucks the hard geometric collar of each floating finger into
    /// the palm, without altering the user-owned source FBX or real 25 bones.
    /// No fake replacement mesh and no generated finger cylinders.
    /// Not a topology weld: visually verify before enabling in game.
    /// </summary>
    public static class ArgonFingerBaseSeamRepair
    {
        public struct Report
        {
            public int Components;
            public int FingersPolished;
            public int VerticesModified;
        }

        public static Report ApplyToCandidate(SkinnedMeshRenderer hand,
            out Mesh disposableCandidateMesh)
        {
            if(hand == null || hand.sharedMesh == null)
                throw new ArgumentNullException(nameof(hand));
            var original=hand.sharedMesh;
            var v=original.vertices;
            var triangles=original.triangles;
            int n=v.Length;
            if(n<5000)throw new InvalidOperationException(
                "Argon repair requires the authentic detailed 3D hand, not a placeholder");
            var parents=new int[n];
            for(int i=0;i<n;i++)parents[i]=i;
            int Find(int x)
            {
                while(parents[x]!=x)
                {
                    parents[x]=parents[parents[x]];
                    x=parents[x];
                }
                return x;
            }
            void Join(int a,int b)
            {
                int x=Find(a),y=Find(b);
                if(x!=y)parents[x]=y;
            }
            for(int i=0;i+2<triangles.Length;i+=3)
            {
                Join(triangles[i],triangles[i+1]);
                Join(triangles[i+1],triangles[i+2]);
            }
            var groups=new Dictionary<int,List<int>>();
            for(int i=0;i<n;i++)
            {
                int id=Find(i);
                if(!groups.TryGetValue(id,out var group))
                    groups[id]=group=new List<int>();
                group.Add(i);
            }
            if(groups.Count!=6)
                throw new InvalidOperationException(
                    "Source Argon topology changed: expected 6 authentic disconnected islands, got "+
                    groups.Count+". Refusing a blind geometry edit.");
            var fingers=groups.Values.Where(g=>g.Count>700&&g.Count<2500)
                .OrderBy(g=>g.Average(id=>v[id].y)).ToArray();
            if(fingers.Length!=5)
                throw new InvalidOperationException(
                    "Expected original five finger-island meshes; detected "+fingers.Length);
            disposableCandidateMesh=UnityEngine.Object.Instantiate(original);
            disposableCandidateMesh.name=original.name+"_Candidate_SeamSmoothed_NoFBXMutation";
            var modified=new bool[n];
            var amounts=new float[n];
            int nModified=0;
            for(int finger=0;finger<fingers.Length;finger++)
            {
                var group=fingers[finger];
                // FBX source bones point toward negative local X. Larger X
                // is the BASE near the palm, smaller X is the fingertip.
                float minX=float.MaxValue,maxX=float.MinValue;
                foreach(int id in group)
                {
                    minX=Mathf.Min(minX,v[id].x);
                    maxX=Mathf.Max(maxX,v[id].x);
                }
                float length=maxX-minX;
                if(length<0.00015f||length>0.003f)
                    throw new InvalidOperationException(
                        "Unexpected source finger scale; no repair applied");
                float baseStart=maxX-length*.26f;
                float rootCenterY=0f,rootCenterZ=0f;
                int centers=0;
                foreach(int id in group)
                {
                    if(v[id].x < maxX-length*.12f)continue;
                    rootCenterY+=v[id].y;
                    rootCenterZ+=v[id].z;
                    centers++;
                }
                if(centers<10)throw new InvalidOperationException(
                    "Finger base rings are missing in real Argon mesh");
                rootCenterY/=centers; rootCenterZ/=centers;
                // Sort by cross-hand Y: thumb, index, middle, ring, little.
                // More seamless overlap for ring and little, conservatively
                // lighter blending for the already acceptable index/thumb.
                float blend=finger==4 ? 0.21f : (finger==3 ? 0.17f : 0.105f);
                foreach(int id in group)
                {
                    float t=Mathf.SmoothStep(0f,1f,
                        Mathf.Clamp01((v[id].x-baseStart)/(maxX-baseStart)));
                    if(t<.001f)continue;
                    var pt=v[id];
                    pt.y=Mathf.Lerp(pt.y,rootCenterY,blend*t);
                    pt.z=Mathf.Lerp(pt.z,rootCenterZ,blend*t);
                    v[id]=pt;
                    modified[id]=true;amounts[id]=t;
                    nModified++;
                }
            }
            disposableCandidateMesh.vertices=v;
            var originalNormals=original.normals;
            if(originalNormals.Length==n)
            {
                disposableCandidateMesh.RecalculateNormals();
                var smooth=disposableCandidateMesh.normals;
                for(int i=0;i<n;i++)
                    if(modified[i])originalNormals[i]=Vector3.Slerp(
                        originalNormals[i],smooth[i],amounts[i]*.45f).normalized;
                disposableCandidateMesh.normals=originalNormals;
            }
            disposableCandidateMesh.RecalculateBounds();
            hand.sharedMesh=disposableCandidateMesh;
            return new Report{
                Components=groups.Count,FingersPolished=fingers.Length,
                VerticesModified=nModified
            };
        }
    }
}
