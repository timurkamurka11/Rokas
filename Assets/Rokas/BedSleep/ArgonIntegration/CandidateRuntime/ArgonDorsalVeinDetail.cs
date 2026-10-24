using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    // Original skinned surface is probed ONCE; the resulting subtle ribbons
    // follow the real Palm bone. No replacement mesh, skin CPU readback loop,
    // source texture overwrite or always-running LateUpdate.
    public sealed class ArgonDorsalVeinDetail : MonoBehaviour
    {
        private Mesh ownedMesh;
        private Material ownedMaterial;

        public static void TryAttach(SkinnedMeshRenderer skin,
            Transform[] joints, Camera heroCamera)
        {
            if (!skin || joints == null || !heroCamera) return;
            Transform wrist=Find(joints,"Arm_Wrist");
            Transform palm=Find(joints,"Hand_Palm");
            Transform index=Find(joints,"Index_MCP");
            Transform middle=Find(joints,"Middle_MCP");
            Transform little=Find(joints,"Little_MCP");
            if (!wrist || !palm || !index || !middle || !little) return;
            Vector3 axis=middle.position-wrist.position;
            Vector3 side=little.position-index.position;
            float length=axis.magnitude, spread=side.magnitude;
            if(length<.018f||length>.30f||spread<.01f||spread>.18f)return;
            Shader shader=Shader.Find("Sprites/Default");
            if(!shader)return;
            Mesh baked=new Mesh();
            GameObject probe=null;
            try
            {
                skin.BakeMesh(baked);
                probe=new GameObject("ROKAS_Temporary_DorsalSurfaceProbe");
                probe.hideFlags=HideFlags.HideAndDontSave;
                probe.transform.position=skin.transform.position;
                probe.transform.rotation=skin.transform.rotation;
                probe.transform.localScale=skin.transform.lossyScale;
                MeshCollider collider=probe.AddComponent<MeshCollider>();
                collider.sharedMesh=baked;
                Physics.SyncTransforms();
                axis/=length;
                side/=spread;
                Vector3 across=side*(spread*.27f);
                var vertices=new List<Vector3>();
                var colors=new List<Color>();
                var triangles=new List<int>();
                float[] start={-.48f,.02f,.39f};
                float[] low={.33f,.42f,.48f};
                float[] high={.78f,.85f,.76f};
                int complete=0;
                for(int path=0;path<3;path++)
                {
                    var points=new Vector3[13];
                    var normals=new Vector3[13];
                    bool valid=true;
                    for(int i=0;i<13;i++)
                    {
                        float t=i/12f;
                        float along=Mathf.Lerp(low[path],high[path],t);
                        float bend=.13f*Mathf.Sin(t*3.8f+path*1.2f);
                        Vector3 target=wrist.position+axis*(length*along)
                            +across*(start[path]+bend);
                        Ray ray=new Ray(heroCamera.transform.position,
                            (target-heroCamera.transform.position).normalized);
                        RaycastHit hit;
                        if(!collider.Raycast(ray,out hit,15f) ||
                            Vector3.Distance(hit.point,target)>length*.40f)
                        {
                            valid=false;
                            break;
                        }
                        Vector3 towardsCamera=(heroCamera.transform.position-
                            hit.point).normalized;
                        points[i]=hit.point+towardsCamera*.00017f;
                        normals[i]=hit.normal;
                    }
                    if(!valid)continue;
                    int begin=vertices.Count;
                    float width=spread*.006f;
                    for(int i=0;i<13;i++)
                    {
                        float t=i/12f;
                        Vector3 tangent=(points[Mathf.Min(12,i+1)]-
                            points[Mathf.Max(0,i-1)]).normalized;
                        Vector3 lateral=Vector3.Cross(normals[i],tangent).normalized;
                        if(lateral.sqrMagnitude<.1f)lateral=side;
                        float fade=Mathf.Pow(Mathf.Sin(t*Mathf.PI),.9f);
                        float pulse=.89f+.11f*Mathf.Sin(t*9f+path*2.1f);
                        float[] lane={-1f,-.55f,0f,.55f,1f};
                        float[] alpha={0f,.045f,.095f,.045f,0f};
                        for(int c=0;c<5;c++)
                        {
                            vertices.Add(palm.InverseTransformPoint(
                                points[i]+lateral*lane[c]*width));
                            colors.Add(new Color(.31f,.38f,.49f,
                                alpha[c]*fade*pulse));
                        }
                    }
                    for(int i=0;i<12;i++)
                    for(int c=0;c<4;c++)
                    {
                        int a=begin+i*5+c,b=a+5;
                        triangles.Add(a); triangles.Add(b); triangles.Add(a+1);
                        triangles.Add(a+1); triangles.Add(b); triangles.Add(b+1);
                        triangles.Add(a+1); triangles.Add(b); triangles.Add(a);
                        triangles.Add(b+1); triangles.Add(b); triangles.Add(a+1);
                    }
                    complete++;
                }
                if(complete==0)return;
                GameObject go=new GameObject("ROKAS_Subtle_DorsalHandVeins");
                go.transform.SetParent(palm,false);
                var detail=go.AddComponent<ArgonDorsalVeinDetail>();
                detail.ownedMesh=new Mesh {name="ROKAS_Dorsal_VascularSurface"};
                detail.ownedMesh.SetVertices(vertices);
                detail.ownedMesh.SetColors(colors);
                detail.ownedMesh.SetTriangles(triangles,0);
                detail.ownedMesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh=detail.ownedMesh;
                detail.ownedMaterial=new Material(shader)
                {name="ROKAS_LowContrast_HandVeins",renderQueue=3001};
                go.AddComponent<MeshRenderer>().sharedMaterial=detail.ownedMaterial;
                go.layer=skin.gameObject.layer;
                Debug.Log("[ROKAS HAND] Low contrast vein surface applied: "+
                    complete+" branches (source model untouched)");
            }
            catch(Exception ex)
            {
                Debug.LogWarning("[ROKAS HAND] Vein detail skipped: "+ex.Message);
            }
            finally
            {
                if(probe)Destroy(probe);
                Destroy(baked);
            }
        }

        private static Transform Find(Transform[] transforms,string name)
        {
            foreach(var node in transforms)
                if(node && node.name==name)return node;
            return null;
        }

        private void OnDestroy()
        {
            if(ownedMesh)Destroy(ownedMesh);
            if(ownedMaterial)Destroy(ownedMaterial);
        }
    }
}
