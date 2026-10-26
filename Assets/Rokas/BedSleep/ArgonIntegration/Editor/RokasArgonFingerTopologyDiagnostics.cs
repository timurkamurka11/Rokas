#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Analyze genuine Argon source hand topology without changing mesh.
    /// Reports disconnected triangle islands and candidate pinky-base seams.
    /// The output is data for precise Blender/Unity geometry repair, not a
    /// misleading visual fix by simply covering gaps with quads.
    /// </summary>
    public sealed class RokasArgonFingerTopologyDiagnostics
    {
        [Test]
        [Timeout(90000)]
        public void ExportOriginalHandConnectedComponentsAndPinkyBoneWeights()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(prefab,Is.Not.Null);
            GameObject root=null, cameraGo=null;
            try
            {
                root=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var hand=root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh");
                var mesh=hand.sharedMesh;
                Assert.That(mesh,Is.Not.Null);
                int count=mesh.vertexCount;
                var tris=mesh.triangles;
                var vertices=mesh.vertices;
                var bw=mesh.boneWeights;
                var parents=new int[count];
                for(int i=0;i<count;i++)parents[i]=i;
                int Find(int x)
                {
                    while(parents[x]!=x)
                    {
                        parents[x]=parents[parents[x]];
                        x=parents[x];
                    }
                    return x;
                }
                void Union(int a,int b)
                {
                    int ra=Find(a),rb=Find(b);
                    if(ra!=rb)parents[ra]=rb;
                }
                for(int i=0;i+2<tris.Length;i+=3)
                {
                    Union(tris[i],tris[i+1]);
                    Union(tris[i+1],tris[i+2]);
                }
                var sections=new Dictionary<int,List<int>>();
                for(int i=0;i<count;i++)
                {
                    int r=Find(i);
                    if(!sections.TryGetValue(r,out var group))
                        sections.Add(r,group=new List<int>());
                    group.Add(i);
                }
                var outDir=Path.Combine(Directory.GetCurrentDirectory(),
                    "Artifacts","ArgonSeamLayers");
                Directory.CreateDirectory(outDir);
                var lines=new List<string>{
                    "authentic hand mesh name="+mesh.name,
                    "vertices="+count+" triangles="+tris.Length/3+
                    " components="+sections.Count,
                    "original skinned bones="+string.Join(",",
                        hand.bones.Select(x=>x.name))
                };
                foreach(var c in sections.OrderByDescending(x=>x.Value.Count).Take(20))
                {
                    var group=c.Value;
                    Vector3 min=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
                    Vector3 max=new Vector3(float.MinValue,float.MinValue,float.MinValue);
                    var bones=new Dictionary<int,int>();
                    foreach(int idx in group)
                    {
                        min=Vector3.Min(min,vertices[idx]);
                        max=Vector3.Max(max,vertices[idx]);
                        if(bw.Length==count)
                        {
                            int id=bw[idx].boneIndex0;
                            if(!bones.ContainsKey(id))bones[id]=0;
                            bones[id]++;
                        }
                    }
                    var top=bones.OrderByDescending(v=>v.Value).Take(4)
                        .Select(v=>hand.bones[v.Key].name+":"+v.Value);
                    lines.Add("COMPONENT root="+c.Key+" vertices="+group.Count+
                        " boundsMin="+min.ToString("F5")+" boundsMax="+
                        max.ToString("F5")+" dominantBones="+string.Join("|",top));
                }
                // A skin seam might be INSIDE a single topological component,
                // so retain a precise camera-relative vertex/bone weight map.
                var animator=root.GetComponentInChildren<Animator>(true);
                var clip=animator.runtimeAnimatorController.animationClips.Single(
                    c=>c.name.Contains("SleepArm_InspectIdle"));
                var pose=new RokasArgonHorizontalHeroPose(root);
                clip.SampleAnimation(animator.gameObject,clip.length*.5f);
                pose.CaptureHeroPose();
                pose.RestoreNaturalAnatomy();
                cameraGo=new GameObject("ROKAS_Argon_TopologyDiagnosticCam");
                var cam=cameraGo.AddComponent<Camera>();
                pose.ConfigureCamera(cam,Vector3.up,.41f);
                var baked=new Mesh();
                try
                {
                    hand.BakeMesh(baked);
                    var bverts=baked.vertices;
                    var near=new List<Tuple<float,string>>();
                    for(int i=0;i<bverts.Length;i++)
                    {
                        Vector3 world=hand.transform.TransformPoint(bverts[i]);
                        Vector3 point=cam.WorldToViewportPoint(world);
                        if(point.z<=0f)continue;
                        // Screen x .59-.78 contains ring/little finger bases.
                        // This ROI deliberately includes the actual visible
                        // square seam around the side of the little finger.
                        if(point.x>.57f&&point.x<.76f&&
                           point.y>.33f&&point.y<.62f)
                        {
                            float dist=Mathf.Abs(point.x-.61f)+
                                Mathf.Abs(point.y-.42f);
                            int bone=bw.Length==count?bw[i].boneIndex0:0;
                            string name=bw.Length==count?hand.bones[bone].name:"unknown";
                            near.Add(Tuple.Create(dist,"vertex="+i+
                                " viewport="+point.ToString("F4")+
                                " skinBone="+name+" weight="+
                                (bw.Length==count?bw[i].weight0.ToString("F4"):"n/a")+
                                " component="+Find(i)+
                                " model="+vertices[i].ToString("F5")));
                        }
                    }
                    lines.Add("PINKY_SIDE_ROI_VERTEX_COUNT="+near.Count);
                    foreach(var v in near.OrderBy(x=>x.Item1).Take(200))
                        lines.Add(v.Item2);
                }
                finally{ UnityEngine.Object.DestroyImmediate(baked); }
                File.WriteAllLines(Path.Combine(outDir,
                    "ArgonOriginalPinkyMeshTopology.txt"),lines);
                Debug.Log("[ARGON ORIGINAL PINKY MESH] "+lines[1]+
                    " ROI samples exported="+lines.Count);
                Assert.That(count,Is.GreaterThan(100));
            }
            finally
            {
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                if(cameraGo!=null)UnityEngine.Object.DestroyImmediate(cameraGo);
            }
        }
    }
}
#endif
