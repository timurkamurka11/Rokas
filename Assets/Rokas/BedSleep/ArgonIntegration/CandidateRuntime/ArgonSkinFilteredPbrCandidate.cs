using System;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Authentic Argon skin PBR, filtered in GPU-memory from existing source
    /// normal and grayscale-roughness maps. No texture/file overrides and no
    /// fallback flesh-color placeholder. Disposable candidate only.
    ///
    /// At first-person watch viewing distance a high-frequency mipless source
    /// normal/roughness texture aliases into a rectilinear skin checkerboard.
    /// Filter spatial frequencies, NOT merely disable real skin normals.
    /// </summary>
    public sealed class ArgonSkinFilteredPbrCandidate : IDisposable
    {
        public Material Material { get; private set; }
        public Texture2D SoftNormal { get; private set; }
        public Texture2D SoftRoughness { get; private set; }
        public int NormalSourceWidth { get; private set; }
        public int RoughnessSourceWidth { get; private set; }

        public static ArgonSkinFilteredPbrCandidate Build(Material source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            if(!source.HasProperty("_MainTex") ||
               !source.HasProperty("_BumpMap") ||
               !source.HasProperty("_MetallicGlossMap"))
                throw new InvalidOperationException(
                    "Authentic Argon skin Standard PBR with its original textures required.");
            if(source.mainTexture==null)
                throw new InvalidOperationException(
                    "Cannot replace genuine skin albedo with flat placeholder");
            var result=new ArgonSkinFilteredPbrCandidate();
            try
            {
                result.Material=new Material(source) {
                    name=source.name+"_ROKAS_FrequencyBalanced_OriginalPBR_QA"
                };
                var normal=source.GetTexture("_BumpMap");
                var rough=source.GetTexture("_MetallicGlossMap");
                if(normal==null||rough==null)
                    throw new InvalidOperationException(
                        "Original imported normal or packed roughness missing");
                result.NormalSourceWidth=normal.width;
                result.RoughnessSourceWidth=rough.width;
                // 512 is enough for a moving hand occupying ~500 game px:
                // suppress UV-frequency checker/moiré but retain form detail.
                result.SoftNormal=Downfilter(normal,512,true);
                result.SoftRoughness=Downfilter(rough,384,false);
                result.Material.SetTexture("_BumpMap",result.SoftNormal);
                result.Material.EnableKeyword("_NORMALMAP");
                result.Material.SetFloat("_BumpScale",.18f);
                result.Material.SetTexture("_MetallicGlossMap",result.SoftRoughness);
                result.Material.EnableKeyword("_METALLICGLOSSMAP");
                result.Material.SetFloat("_GlossMapScale",.64f);
                result.Material.SetFloat("_Glossiness",.40f);
                result.Material.SetFloat("_Metallic",.018f);
                // Real AO, albedo, shader and UVs remain copied unchanged.
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private static Texture2D Downfilter(Texture source,int maxSide,bool isNormal)
        {
            var previous=RenderTexture.active;
            RenderTexture current=null;
            try
            {
                int w=source.width,h=source.height;
                // True progressive low-pass downsampling, not nearest
                // rescale; prevents frequencies folding into enlarged grid.
                while(w>maxSide || h>maxSide)
                {
                    int newW=Mathf.Max(1,w/2),newH=Mathf.Max(1,h/2);
                    var next=RenderTexture.GetTemporary(
                        newW,newH,0,RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.Linear);
                    next.filterMode=FilterMode.Bilinear;
                    next.wrapMode=TextureWrapMode.Repeat;
                    Graphics.Blit(current!=null?(Texture)current:source,next);
                    if(current!=null)RenderTexture.ReleaseTemporary(current);
                    current=next;
                    w=newW;h=newH;
                }
                // Force an RGBA32 copy to retain the Standard shader's
                // packed normal XY channels and roughness/smoothness alpha.
                if(current==null)
                {
                    current=RenderTexture.GetTemporary(
                        w,h,0,RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.Linear);
                    Graphics.Blit(source,current);
                }
                RenderTexture.active=current;
                var result=new Texture2D(w,h,TextureFormat.RGBA32,true,true)
                {
                    name=source.name+
                        (isNormal?"_LowFrequencyOriginalNormal":"_LowFrequencyOriginalRoughness"),
                    wrapMode=TextureWrapMode.Repeat,
                    filterMode=FilterMode.Trilinear,
                    anisoLevel=2,
                    mipMapBias=-.1f
                };
                result.ReadPixels(new Rect(0,0,w,h),0,0,false);
                result.Apply(true,false);
                return result;
            }
            finally
            {
                RenderTexture.active=previous;
                if(current!=null)RenderTexture.ReleaseTemporary(current);
            }
        }

        public void Dispose()
        {
            if(Material!=null)UnityEngine.Object.DestroyImmediate(Material);
            if(SoftNormal!=null)UnityEngine.Object.DestroyImmediate(SoftNormal);
            if(SoftRoughness!=null)UnityEngine.Object.DestroyImmediate(SoftRoughness);
            Material=null;SoftNormal=null;SoftRoughness=null;
        }
    }
}
