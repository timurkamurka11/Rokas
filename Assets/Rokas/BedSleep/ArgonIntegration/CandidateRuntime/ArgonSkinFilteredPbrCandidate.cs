using System;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Authentic Argon skin PBR, filtered in GPU-memory from existing source
    /// original native packed normal and roughness mipmaps. No texture/file overrides and no
    /// fallback flesh-color placeholder. Disposable candidate only.
    ///
    /// At first-person watch viewing distance a high-frequency mipless source
    /// normal/roughness texture aliases into a rectilinear skin checkerboard.
    /// Choose higher original mip levels, NEVER decode/repack DXT5nm channels.    /// The GPU-to-RGBA prototype was visually rejected for amplifying grids.
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
                // The original imported NormalMap uses platform-specific
                // BC5/DXT5nm channel packing. GPU blit into RGBA32 changes
                // its swizzle, causing an ACTUAL 720p checkerboard (visual
                // run #39038761444). Clone imported texture WITH original
                // mip levels/channel packing, use trilinear coarse LOD.
                result.SoftNormal=CopyImportedMipChain(normal,2.5f);
                result.SoftRoughness=CopyImportedMipChain(rough,2.0f);
                result.Material.SetTexture("_BumpMap",result.SoftNormal);
                result.Material.EnableKeyword("_NORMALMAP");
                result.Material.SetFloat("_BumpScale",.055f);
                result.Material.SetTexture("_MetallicGlossMap",result.SoftRoughness);
                result.Material.EnableKeyword("_METALLICGLOSSMAP");
                result.Material.SetFloat("_GlossMapScale",.29f);
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

        private static Texture2D CopyImportedMipChain(Texture source,
            float lodBias)
        {
            var original=source as Texture2D;
            if(original==null || original.mipmapCount<2)
                throw new InvalidOperationException(
                    "Original imported PBR texture mipmaps required. "+
                    "Never reinterpret packed native normal channels as RGBA.");
            // Object.Instantiate(Texture2D) fails for an imported
            // non-readable normal texture in the Linux Unity player.
            // Allocate the SAME compressed native texture format, then use
            // GPU CopyTexture for all actual imported mipmap pixels.
            // Repacking through an RGBA render target is forbidden: it
            // visibly turned the user's skin into a checkerboard.
            var copy=new Texture2D(original.width,original.height,
                original.format,original.mipmapCount,true);
            copy.name=original.name+"_NativePacked_GPU_MipCopy_QA";
            for(int mip=0;mip<original.mipmapCount;mip++)
                Graphics.CopyTexture(original,0,mip,copy,0,mip);
            copy.filterMode=FilterMode.Trilinear;
            copy.wrapMode=original.wrapMode;
            copy.anisoLevel=1;
            copy.mipMapBias=lodBias;
            return copy;
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
