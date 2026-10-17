#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.Argon.Editor
{
    public sealed class RokasArgonTopologyMaterialDiagnosticTests
    {
        [Test]
        [Timeout(90000)]
        public void MeasureAuthenticPalmFingerBoundariesAndSkinTextureFiltering()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(prefab,Is.Not.Null);
            GameObject root=null;
            try
            {
                root=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var skin=root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x=>x.name=="Keiko_Hand_Skin_Mesh");
                var m=skin.sharedMesh;
                var v=m.vertices;
                var tri=m.triangles;
                int n=v.Length;
                var parent=Enumerable.Range(0,n).ToArray();
                int Find(int i) {
                    while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}
                    return i;
                }
                void Join(int a,int b) {
                    int i=Find(a),j=Find(b); if(i!=j)parent[i]=j;
                }
                var edgeCounts=new Dictionary<ulong,int>();
                ulong Key(int a,int b) {
                    uint lo=(uint)Math.Min(a,b),hi=(uint)Math.Max(a,b);
                    return ((ulong)lo<<32)|hi;
                }
                void Count(int a,int b) {
                    ulong k=Key(a,b);
                    if(edgeCounts.ContainsKey(k))edgeCounts[k]++;
                    else edgeCounts[k]=1;
                }
                for(int j=0;j+2<tri.Length;j+=3) {
                    Join(tri[j],tri[j+1]);Join(tri[j+1],tri[j+2]);
                    Count(tri[j],tri[j+1]);
                    Count(tri[j+1],tri[j+2]);
                    Count(tri[j+2],tri[j]);
                }
                var comps=new Dictionary<int,List<int>>();
                for(int i=0;i<n;i++) {
                    int k=Find(i);
                    if(!comps.TryGetValue(k,out var ids))comps.Add(k,ids=new List<int>());
                    ids.Add(i);
                }
                var ordered=comps.Values.OrderByDescending(ids=>ids.Count).ToArray();
                var sb=new StringBuilder();
                sb.AppendLine("AUTHENTIC SOURCE FINGER / PALM TOPOLOGY");
                sb.AppendLine("vertexCount="+n+" triangleCount="+tri.Length/3+
                    " connectedComponents="+ordered.Length);
                sb.AppendLine("boundaryEdges="+edgeCounts.Count(kv=>kv.Value==1)+
                    " nonManifoldEdges="+edgeCounts.Count(kv=>kv.Value>2));
                foreach(var group in ordered)
                {
                    int rootIndex=Find(group[0]);
                    var edges=edgeCounts.Where(kv=>kv.Value==1 &&
                        Find((int)(kv.Key>>32))==rootIndex).ToArray();
                    var ext=new Bounds(v[group[0]],Vector3.zero);
                    foreach(int i in group)ext.Encapsulate(v[i]);
                    float maxX=group.Max(i=>v[i].x),minX=group.Min(i=>v[i].x);
                    int rootTriangles=0,rootCaps=0;
                    for(int j=0;j+2<tri.Length;j+=3)
                    {
                        int a=tri[j],b=tri[j+1],c=tri[j+2];
                        if(Find(a)!=rootIndex)continue;
                        if(v[a].x>maxX-(maxX-minX)*.08f &&
                           v[b].x>maxX-(maxX-minX)*.08f &&
                           v[c].x>maxX-(maxX-minX)*.08f)
                        {
                            rootTriangles++;
                            if(Mathf.Abs(v[a].x-v[b].x)<.000003f &&
                               Mathf.Abs(v[b].x-v[c].x)<.000003f)
                                rootCaps++;
                        }
                    }
                    sb.AppendLine("island size="+group.Count+
                        " edgeBoundary="+edges.Length+
                        " boundsMin="+ext.min+" boundsMax="+ext.max+
                        " capZoneTriangles="+rootTriangles+
                        " planarRootCapTriangles="+rootCaps);
                    if(edges.Length>0)
                    {
                        var boundaryIds=edges.SelectMany(kv=>new []{
                            (int)(kv.Key>>32),(int)(kv.Key&0xffffffffUL)})
                            .Distinct().ToArray();
                        sb.AppendLine("  boundary X-range="+
                            boundaryIds.Min(i=>v[i].x).ToString("G9")+
                            ".."+boundaryIds.Max(i=>v[i].x).ToString("G9"));
                    }
                }

                var mat=skin.sharedMaterial;
                sb.AppendLine("MATERIAL name="+mat.name+" shader="+mat.shader.name);
                foreach(string key in new[]{
                    "_MainTex","_BumpMap","_OcclusionMap","_MetallicGlossMap",
                    "_Glossiness","_GlossMapScale","_BumpScale","_Metallic"})
                {
                    if(!mat.HasProperty(key))continue;
                    if(key.EndsWith("Tex")||key.EndsWith("Map"))
                    {
                        Texture t=mat.GetTexture(key);
                        sb.AppendLine(key+" "+(t==null?"none":t.name+
                            " "+t.width+"x"+t.height+" filter="+t.filterMode+
                            " aniso="+t.anisoLevel+" mipBias="+t.mipMapBias));
                        var tex=t as Texture2D;
                        if(tex!=null) {
                            var importer=AssetImporter.GetAtPath(
                                AssetDatabase.GetAssetPath(tex)) as TextureImporter;
                            if(importer!=null)sb.AppendLine("  import="+
                                importer.textureType+" mips="+importer.mipmapEnabled+
                                " srgb="+importer.sRGBTexture+
                                " alpha="+importer.alphaSource);
                        }
                    }
                    else sb.AppendLine(key+"="+mat.GetFloat(key));
                }
                string dir=Path.Combine(Directory.GetCurrentDirectory(),
                    "Artifacts","ArgonFingerTopology");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir,"source_mesh_material_report.txt"),sb.ToString());
                Debug.Log("[ARGON AUTHENTIC FINGER TOPOLOGY AND MATERIAL]\n"+sb);
                Assert.That(ordered.Length,Is.EqualTo(6));
                Assert.That(ordered.Count(g=>g.Count==1394),Is.EqualTo(5));
            }
            finally {if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
#endif
