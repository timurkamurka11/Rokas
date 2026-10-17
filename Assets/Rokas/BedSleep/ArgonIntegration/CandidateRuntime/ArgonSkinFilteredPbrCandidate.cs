using System;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Source-faithful PBR candidate for the authentic Keiko hand.
    /// The original Blender/Unity imported normal is NOT copied/repacked:
    /// previous RGBA and GPU-copy experiments failed visual/runtime QA.
    /// Preserve original albedo/UV/AO/roughness and original normal texture.
    /// Only tune lighting response on a disposable Material, never source.
    /// </summary>
    public sealed class ArgonSkinFilteredPbrCandidate : IDisposable
    {
        public Material Material { get; private set; }
        public Texture OriginalNormal { get; private set; }
        public Texture OriginalRoughness { get; private set; }

        public static ArgonSkinFilteredPbrCandidate Build(Material source)
        {
            if(source==null) throw new ArgumentNullException(nameof(source));
            foreach(string key in new[] {"_MainTex","_BumpMap",
                "_OcclusionMap","_MetallicGlossMap","_Metallic","_Glossiness"})
                if(!source.HasProperty(key))
                    throw new InvalidOperationException(
                        "Authentic Standard skin missing PBR property "+key);
            if(source.mainTexture==null ||
               source.GetTexture("_BumpMap")==null ||
               source.GetTexture("_MetallicGlossMap")==null)
                throw new InvalidOperationException(
                    "Source skin albedo/normal/roughness required, no placeholders");

            var result=new ArgonSkinFilteredPbrCandidate();
            result.Material=new Material(source);
            result.Material.name=source.name+"_ROKAS_OriginalPBR_MatteSkin_QA";
            result.OriginalNormal=source.GetTexture("_BumpMap");
            result.OriginalRoughness=source.GetTexture("_MetallicGlossMap");

            // Keep original packed BC5/DXT5nm texture references UNCHANGED.
            // Mildly unmask normal detail only, never reinterpret channels.
            result.Material.SetTexture("_BumpMap",result.OriginalNormal);
            result.Material.EnableKeyword("_NORMALMAP");
            result.Material.SetFloat("_BumpScale",.012f);
            result.Material.SetTexture("_MetallicGlossMap",result.OriginalRoughness);
            result.Material.EnableKeyword("_METALLICGLOSSMAP");
            result.Material.SetFloat("_GlossMapScale",.12f);
            result.Material.SetFloat("_Glossiness",.32f);
            result.Material.SetFloat("_Metallic",0f);
            // Real cloud Unity A/B isolated the harsh X pattern to the
            // original baked AO map, NOT missing finger topology or normals.
            // Keep the authentic 2048x2048 AO texture reference for detail,
            // but substantially attenuate its excessive baked self-shadow.
            // Source Unity material remains untouched; candidate QA only.
            result.Material.SetFloat("_OcclusionStrength",.12f);

            // Warm epidermis only in material tint, not replaced texture.
            if(result.Material.HasProperty("_Color"))
                result.Material.SetColor("_Color",
                    new Color(1f,.987f,.975f,1f));
            return result;
        }

        public void Dispose()
        {
            if(Material!=null)
                UnityEngine.Object.DestroyImmediate(Material);
            Material=null;
            // Never destroy imported original normal, roughness or albedo.
            OriginalNormal=null;
            OriginalRoughness=null;
        }
    }
}
