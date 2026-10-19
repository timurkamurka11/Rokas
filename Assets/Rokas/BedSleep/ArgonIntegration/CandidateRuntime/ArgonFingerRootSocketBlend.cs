using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Experimental COPY-ONLY socket blend for the original five separate
    /// Argon finger meshes. No replacement tubes, FBX edits or made-up nails.
    ///
    /// The source FBX has hard rectangular proximal finger collars outside
    /// the palm. The previous 10–21% Y/Z shrink left those collars visible.
    /// This candidate locally narrows AND nests ONLY the proximal 28% of
    /// each real finger inside the original palm; distal 62-69% is bitwise
    /// identical. Surface quality must be approved by real Unity frames
    /// before any cinematic/runtime activation.
    /// </summary>
    public static class ArgonFingerRootSocketBlend
    {
        public struct Report
        {
            public int SourceVertices;
            public int FingerIslands;
            public int ModifiedVertices;
            public float MaxVertexMove;
        }

        public static Report ApplyToClone(SkinnedMeshRenderer skin,
            out Mesh temporaryMesh)
        {
            return ApplyInternal(skin,out temporaryMesh,false);
        }

        /// <summary>
        /// Conservative GAMEPLAY candidate: only index (next to thumb)
        /// and little finger, which have obvious squared proximal seams.
        /// Never retopo/modify source FBX; rejects unknown topology.
        /// </summary>
        public static Report ApplyIndexLittleToClone(SkinnedMeshRenderer skin,
            out Mesh temporaryMesh)
        {
            return ApplyInternal(skin,out temporaryMesh,true);
        }

        private static Report ApplyInternal(SkinnedMeshRenderer skin,
            out Mesh temporaryMesh,bool indexLittleOnly)
        {
            temporaryMesh=null;
            if(skin==null||skin.sharedMesh==null)
                throw new ArgumentNullException(nameof(skin));
            var source=skin.sharedMesh;
            if(source.vertexCount!=12235)
                throw new InvalidOperationException("Expected original Argon source hand");
            int n=source.vertexCount;
            Vector3[] v=source.vertices;
            var parent=new int[n];
            for(int i=0;i<n;i++)parent[i]=i;
            int Find(int i)
            {
                while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}
                return i;
            }
            void Join(int x,int y)
            {
                x=Find(x);y=Find(y);if(x!=y)parent[x]=y;
            }
            int[] tri=source.triangles;
            for(int i=0;i+2<tri.Length;i+=3)
            {
                Join(tri[i],tri[i+1]); Join(tri[i+1],tri[i+2]);
            }
            var comps=new Dictionary<int,List<int>>();
            for(int i=0;i<n;i++)
            {
                int r=Find(i);
                if(!comps.TryGetValue(r,out var items))
                    comps.Add(r,items=new List<int>());
                items.Add(i);
            }
            if(comps.Count!=6)
                throw new InvalidOperationException("Expected 6 source mesh islands");
            var fingers=comps.Values.Where(x=>x.Count==1394)
                .OrderBy(g=>g.Average(i=>v[i].y)).ToArray();
            if(fingers.Length!=5)
                throw new InvalidOperationException("Missing original source finger");

            Vector3[] copy=(Vector3[])v.Clone();
            var edited=new float[n];
            int modified=0;
            float maxMove=0f;
            for(int finger=0;finger<fingers.Length;finger++)
            {
                // Retain original thumb/middle/ring shapes, UVs and weights.
                if(indexLittleOnly && finger!=1 && finger!=4)continue;
                var f=fingers[finger];
                float minX=float.PositiveInfinity,maxX=float.NegativeInfinity;
                foreach(int i in f)
                {
                    minX=Mathf.Min(minX,v[i].x);
                    maxX=Mathf.Max(maxX,v[i].x);
                }
                float length=maxX-minX;
                if(length<.0004f||length>.0012f)
                    throw new InvalidOperationException("Source fingertip dimensions changed");
                float centerY=0f,centerZ=0f;
                int count=0;
                foreach(int i in f)
                {
                    if(v[i].x<maxX-length*.065f)continue;
                    centerY+=v[i].y;
                    centerZ+=v[i].z;
                    count++;
                }
                if(count<5)
                    throw new InvalidOperationException("Cannot determine real proximal ring");
                centerY/=count;centerZ/=count;
                // Ring and little have the most conspicuous square caps.
                // Nest those genuine skin rings further inside the palm.
                float fraction=indexLittleOnly
                    ? (finger==4 ? .34f : .30f)
                    : (finger>=3 ? .38f : .31f);
                float begin=maxX-length*fraction;
                for(int j=0;j<f.Count;j++)
                {
                    int i=f[j];
                    if(v[i].x<=begin)continue; // NEVER touch distal phalanges.
                    float t=Mathf.Clamp01((v[i].x-begin)/(maxX-begin));
                    // Smooth 0 slope at the untouched distal boundary,
                    // smooth 0 slope at proximal end, no geometric crease.
                    float s=t*t*(3f-2f*t);
                    float narrow=(indexLittleOnly
                        ? (finger==4 ? .49f : .39f)
                        : (finger>=3 ? .78f : .62f))*s;
                    Vector3 p=v[i];
                    p.x+=length*(indexLittleOnly
                        ? (finger==4 ? .135f : .105f)
                        : (finger>=3 ? .25f : .18f))*s;
                    // Hide the original hard proximal end under palm skin.
                    p.y=Mathf.Lerp(p.y,centerY,narrow);
                    p.z=Mathf.Lerp(p.z,centerZ,narrow);
                    copy[i]=p;
                    edited[i]=s;
                    float delta=(p-v[i]).magnitude;
                    if(delta>1e-10f){modified++;maxMove=Mathf.Max(maxMove,delta);}
                }
            }
            if(modified<(indexLittleOnly ? 60 : 200)||modified>n/2)
                throw new InvalidOperationException(
                    "Unexpected proximal-finger edit extent; do not use repair");
            // Reject unusual scaling, a wrong socket axis, or an imported
            // mesh whose bind-space units differ from the source FBX.
            // This check happens BEFORE instantiating/replacing the mesh.
            if (float.IsNaN(maxMove) || float.IsInfinity(maxMove) ||
                maxMove > source.bounds.size.magnitude * .15f)
                throw new InvalidOperationException(
                    "Proximal correction would move vertices too far; " +
                    "preserving the original Argon finger mesh.");
            temporaryMesh=UnityEngine.Object.Instantiate(source);
            temporaryMesh.name=source.name+(indexLittleOnly
                ? "_ROKAS_IndexLittleOnly_SafeSocketBlend"
                : "_Candidate_ProximalSocketNesting");
            temporaryMesh.vertices=copy;
            if(source.normals.Length==n)
            {
                Vector3[] normals=(Vector3[])source.normals.Clone();
                temporaryMesh.RecalculateNormals();
                Vector3[] revised=temporaryMesh.normals;
                for(int i=0;i<n;i++)
                    if(edited[i]>.001f)
                        normals[i]=Vector3.Slerp(normals[i],
                            revised[i],edited[i]*.55f).normalized;
                temporaryMesh.normals=normals;
            }
            temporaryMesh.RecalculateBounds();
            skin.sharedMesh=temporaryMesh;
            return new Report{
                SourceVertices=n,FingerIslands=fingers.Length,
                ModifiedVertices=modified,MaxVertexMove=maxMove
            };
        }
    }
}
