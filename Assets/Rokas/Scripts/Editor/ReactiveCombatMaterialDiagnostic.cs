using System;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.Editor
{
    public static class ReactiveCombatMaterialDiagnostic
    {
        private const string Base = "Assets/Rokas/Art/CombatActors/";

        [MenuItem("ROKAS/Combat/Report Imported Actor Materials")]
        public static void Report()
        {
            foreach (string relative in new[]
                {
                    "Keiko/Keiko@Idle.fbx",
                    "Mina/anime girl character 3d model@Standing Idle.fbx",
                    "Yokai/Still stance.fbx"
                })
            {
                string path = Base + relative;
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Debug.Log("Actor material source " + path + ", importer=" + importer);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Texture2D texture)
                        Debug.Log("Imported texture: " + texture.name + " " +
                            texture.width + "x" + texture.height);
                    else if (asset is Material material)
                        Debug.Log("Imported material: " + material.name + " shader=" +
                            (material.shader == null ? "null" : material.shader.name) +
                            " mainTexture=" + (material.mainTexture == null
                                ? "null" : material.mainTexture.name));
                }
            }
        }

        [MenuItem("ROKAS/Combat/Extract Imported Actor Textures")]
        public static void Extract()
        {
            string parent = Base + "Textures";
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(Base.TrimEnd('/'), "Textures");
            ExtractOne("Keiko/Keiko@Idle.fbx", "Keiko");
            ExtractOne("Mina/anime girl character 3d model@Standing Idle.fbx", "Mina");
            ExtractOne("Yokai/Still stance.fbx", "Yokai");
            AssetDatabase.Refresh();
            Report();
        }

        private static void ExtractOne(string relative, string group)
        {
            string folder = Base + "Textures/" + group;
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Base + "Textures", group);
            ModelImporter importer = AssetImporter.GetAtPath(Base + relative) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing importer " + relative);
            importer.ExtractTextures(folder);
            Debug.Log("Extracted embedded textures for " + group + " into " + folder);
        }
    }
}
