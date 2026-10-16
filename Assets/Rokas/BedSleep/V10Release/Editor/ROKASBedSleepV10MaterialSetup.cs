#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rokas.BedSleep.V10Release.Editor
{
    public static class ROKASBedSleepV10MaterialSetup
    {
        const string Root = "Assets/Rokas/BedSleep/V10Release/";
        const string Materials = Root + "Materials/";

        [MenuItem("ROKAS/Bed Sleep/V10 Release/1 - Build actual PBR materials")]
        public static void CreateMaterials()
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("V10: No PBR shader available");

            Texture2D skin = Need("Skin/T_Keiko_Skin_Albedo_4K.png");
            Texture2D skinNormal = Need("Skin/T_Keiko_Skin_Normal_4K.png");
            Texture2D sleeve = Need("Sleeve/T_Keiko_Sleeve_Albedo_2K.png");
            Texture2D sleeveNormal = Need("Sleeve/T_Keiko_Sleeve_Normal_2K.png");
            Texture2D titanium = Need("Watch/T_Watch_Titanium_Albedo_2K.png");
            Texture2D strap = Need("Watch/T_Watch_Strap_Albedo_2K.png");
            Texture2D strapNormal = Need("Watch/T_Watch_Strap_Normal_2K.png");
            foreach (var name in new[] {"M_Keiko_Skin","M_Keiko_Sleeve","M_Watch_Titanium","M_Watch_Strap","M_Watch_Glass","M_Watch_OLED"})
                if (AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat") != null)
                    throw new InvalidOperationException("V10: refuses to overwrite existing material " + name);

            if (!AssetDatabase.IsValidFolder(Materials.TrimEnd('/')))
                AssetDatabase.CreateFolder(Root.TrimEnd('/'), "Materials");
            Make("M_Keiko_Skin", shader, skin, skinNormal, 0f, .58f);
            Make("M_Keiko_Sleeve", shader, sleeve, sleeveNormal, 0f, .77f);
            Make("M_Watch_Titanium", shader, titanium, null, .84f, .38f);
            Make("M_Watch_Strap", shader, strap, strapNormal, 0f, .73f);
            Material glass = Make("M_Watch_Glass", shader, null, null, .04f, .93f);
            Colorize(glass, new Color(.77f,.87f,1f,.14f));
            if (glass.HasProperty("_Mode"))
            {
                glass.SetFloat("_Mode", 3f);
                glass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                glass.SetInt("_ZWrite", 0);
                glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            if (glass.HasProperty("_Surface")) glass.SetFloat("_Surface", 1f);
            glass.renderQueue = (int)RenderQueue.Transparent;
            Material oled = Make("M_Watch_OLED", shader, null, null, 0f, .28f);
            Colorize(oled, new Color(.015f,.025f,.035f,1f));
            if (oled.HasProperty("_EmissionColor"))
            {
                oled.SetColor("_EmissionColor", new Color(.07f,.15f,.23f));
                oled.EnableKeyword("_EMISSION");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ROKAS V10] Six real UV/PBR material assets created");
        }

        [MenuItem("ROKAS/Bed Sleep/V10 Release/2 - Build candidate prefabs")]
        public static void CreateCandidatePrefabs()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_Keiko_Skin.mat") == null)
                CreateMaterials();
            ROKASBedSleepV10PrefabBuilder.Build();
        }

        public static void BuildAllForCloudCI()
        {
            CreateCandidatePrefabs();
        }

        static Texture2D Need(string rel)
        {
            string path = Root + "Textures/" + rel;
            // GUID-only .meta may import square PNG as a Cubemap.
            // The actual V10 albedo/normal maps are 2D textures, never placeholders.
            TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (textureImporter == null)
            {
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (textureImporter == null)
                throw new InvalidOperationException("V10 PNG missing TextureImporter: " + path);
            bool normal = rel.IndexOf("_Normal_", StringComparison.OrdinalIgnoreCase) >= 0;
            var correctType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (textureImporter.textureShape != TextureImporterShape.Texture2D ||
                textureImporter.textureType != correctType)
            {
                textureImporter.textureShape = TextureImporterShape.Texture2D;
                textureImporter.textureType = correctType;
                textureImporter.SaveAndReimport();
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                // A freshly staged Blender/source import can leave the .meta with
                // a GUID before Unity serializes the complete TextureImporter.
                // Reimport only this genuine PNG; never synthesize a flat texture.
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            if (tex == null)
            {
                var importer = AssetImporter.GetAtPath(path);
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                throw new InvalidOperationException("V10 real texture failed Unity AssetDatabase import: " +
                    path + " guid=" + AssetDatabase.AssetPathToGUID(path) +
                    " importer=" + (importer == null ? "null" : importer.GetType().FullName) +
                    " mainAsset=" + (main == null ? "null" : main.GetType().FullName));
            }
            return tex;
        }

        static Material Make(string name, Shader shader, Texture2D albedo, Texture2D normal, float metallic, float smoothness)
        {
            var material = new Material(shader) {name = name};
            if (albedo != null)
            {
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            }
            if (normal != null)
            {
                if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
                if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", .6f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, Materials + name + ".mat");
            return material;
        }

        static void Colorize(Material m, Color c)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }
    }
}
#endif
