using System;
using System.Collections.Generic;
using System.IO;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.Editor
{
    public static class ReactiveCombatActorLibraryBuilder
    {
        private const string Source = "Assets/Rokas/Art/CombatActors/";
        private const string ClipOutput = Source + "Clips/";
        private const string MaterialOutput = Source + "Materials/";
        private const string Output = "Assets/Rokas/Resources/Combat/ReactiveCombatActorLibrary.asset";

        [MenuItem("ROKAS/Combat/Build Imported Actor Library")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Rokas/Resources/Combat"))
                AssetDatabase.CreateFolder("Assets/Rokas/Resources", "Combat");
            if (!AssetDatabase.IsValidFolder(ClipOutput.TrimEnd('/')))
                AssetDatabase.CreateFolder(Source.TrimEnd('/'), "Clips");
            if (!AssetDatabase.IsValidFolder(MaterialOutput.TrimEnd('/')))
                AssetDatabase.CreateFolder(Source.TrimEnd('/'), "Materials");

            ReactiveCombatActorLibrary library =
                AssetDatabase.LoadAssetAtPath<ReactiveCombatActorLibrary>(Output);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ReactiveCombatActorLibrary>();
                AssetDatabase.CreateAsset(library, Output);
            }

            library.keiko.model = Model("Keiko/Keiko@Idle.fbx");
            library.keiko.material = ActorMaterial("Keiko",
                "Keiko/anime_character_3d_model_basecolor.JPEG",
                "Keiko/anime_character_3d_model_normal.JPEG");
            library.keiko.idle = Clip("Keiko/Keiko@Idle.fbx");
            library.keiko.attack = Clip("Keiko/attack.fbx");
            library.keiko.heavy = Clip("Keiko/attack2.fbx");
            library.keiko.hit = null;
            library.keiko.stagger = null;
            library.keiko.death = null;
            library.keiko.walk = null;
            library.keiko.standingHeight = 2f;
            library.keiko.forwardYaw = 0f;

            library.mina.model = Model("Mina/anime girl character 3d model@Standing Idle.fbx");
            library.mina.material = ActorMaterial("Mina",
                "Mina/anime+girl+character+3d+model_basecolor.jpg", null);
            library.mina.idle = Clip("Mina/anime girl character 3d model@Standing Idle.fbx");
            library.mina.attack = Clip("Mina/Attack.fbx");
            library.mina.heavy = Clip("Mina/Magic attack.fbx");
            library.mina.hit = Clip("Mina/hit.fbx");
            library.mina.stagger = Clip("Mina/hit 2.fbx");
            library.mina.death = null;
            library.mina.walk = null;
            library.mina.standingHeight = 2f;
            library.mina.forwardYaw = 0f;

            library.yokai.model = Model("Yokai/Still stance.fbx");
            library.yokai.material = ActorMaterial("Yokai",
                "Yokai/fantasy+creature+3d+model_basecolor.jpg", null);
            library.yokai.idle = Clip("Yokai/Still stance.fbx");
            library.yokai.attack = Clip("Yokai/Attack.fbx");
            library.yokai.heavy = Clip("Yokai/Jump attack.fbx");
            library.yokai.hit = Clip("Yokai/Hit.fbx");
            library.yokai.stagger = Clip("Yokai/hit 2.fbx");
            library.yokai.death = Clip("Yokai/Death.fbx");
            library.yokai.walk = Clip("Yokai/Walking to attack.fbx");
            library.yokai.standingHeight = 2.7f;
            library.yokai.forwardYaw = 0f;

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Verify("Keiko", library.keiko);
            Verify("Mina", library.mina);
            Verify("Yokai", library.yokai);
            VerifyClipSeparation(library);
            Debug.Log("ReactiveCombatActorLibrary built at " + Output);
        }

        private static GameObject Model(string relative)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + relative);
            if (model == null) throw new InvalidOperationException("Missing combat model " + relative);
            return model;
        }

        private static AnimationClip Clip(string relative)
        {
            string path = Source + relative;
            foreach (UnityEngine.Object subasset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip source = subasset as AnimationClip;
                if (source == null || source.length <= .01f ||
                    source.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                string filename = Path.GetFileNameWithoutExtension(relative)
                    .Replace(' ', '_').Replace('@', '_');
                string group = relative.Substring(0, relative.IndexOf('/'));
                string output = ClipOutput + group + "_" + filename + ".anim";
                AnimationClip copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
                if (copy == null)
                {
                    copy = UnityEngine.Object.Instantiate(source);
                    copy.name = group + "_" + filename;
                    copy.legacy = true;
                    AssetDatabase.CreateAsset(copy, output);
                }
                else
                {
                    EditorUtility.CopySerialized(source, copy);
                    copy.name = group + "_" + filename;
                    copy.legacy = true;
                    EditorUtility.SetDirty(copy);
                }
                return copy;
            }
            throw new InvalidOperationException("Missing combat animation " + relative);
        }

        private static Material ActorMaterial(string name, string albedo, string normal)
        {
            string textureFolder = Source + "Textures/";
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + albedo);
            if (baseColor == null)
                throw new InvalidOperationException("Missing extracted base color for " + name);
            Texture2D normalMap = null;
            if (!string.IsNullOrEmpty(normal))
            {
                string normalPath = textureFolder + normal;
                TextureImporter textureImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (textureImporter != null && textureImporter.textureType != TextureImporterType.NormalMap)
                {
                    textureImporter.textureType = TextureImporterType.NormalMap;
                    textureImporter.SaveAndReimport();
                }
                normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            }
            string path = MaterialOutput + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Standard shader unavailable");
                material = new Material(shader) { name = "Combat_" + name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = baseColor;
            material.color = Color.white;
            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Glossiness", .24f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Verify(string name, ReactiveCombatActorClips clips)
        {
            GameObject instance = UnityEngine.Object.Instantiate(clips.model);
            try
            {
                var paths = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (child == instance.transform) continue;
                    var segments = new List<string>();
                    for (Transform cursor = child; cursor != null && cursor != instance.transform;
                         cursor = cursor.parent)
                        segments.Insert(0, cursor.name);
                    paths.Add(string.Join("/", segments));
                }

                var rendererBounds = new Bounds();
                bool found = false;
                int materialCount = 0;
                int texturedMaterials = 0;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (!found) { rendererBounds = renderer.bounds; found = true; }
                    else rendererBounds.Encapsulate(renderer.bounds);
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        materialCount++;
                        if (material != null && material.mainTexture != null) texturedMaterials++;
                    }
                }
                Debug.Log("Combat actor " + name + ": transforms=" + paths.Count +
                    ", renderer bounds=" + (found ? rendererBounds.size.ToString("F3") : "NONE") +
                    ", source materials=" + materialCount + ", source textured=" + texturedMaterials +
                    ", runtime material=" + (clips.material == null ? "null" : clips.material.name) +
                    ", runtime textured=" + (clips.material != null && clips.material.mainTexture != null));
                foreach (AnimationClip clip in new[] { clips.idle, clips.attack, clips.heavy, clips.hit,
                             clips.stagger,
                             clips.death, clips.walk })
                {
                    if (clip == null) continue;
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    int matching = 0;
                    foreach (EditorCurveBinding binding in bindings)
                        if (paths.Contains(binding.path)) matching++;
                    Debug.Log("Combat clip " + name + "/" + clip.name + ": legacy=" + clip.legacy +
                        ", length=" + clip.length.ToString("F3") + "s, bindings=" + bindings.Length +
                        ", matching model paths=" + matching);
                    if (!clip.legacy || matching == 0)
                        Debug.LogError("Combat animation does not bind to model: " + name + "/" + clip.name);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void VerifyClipSeparation(ReactiveCombatActorLibrary library)
        {
            foreach (ReactiveCombatActorClips actor in new[] { library.keiko, library.mina, library.yokai })
            {
                foreach (AnimationClip clip in new[] { actor.idle, actor.attack, actor.heavy, actor.hit,
                             actor.stagger, actor.death, actor.walk })
                {
                    if (clip == null) continue;
                    string path = AssetDatabase.GetAssetPath(clip);
                    if (!path.StartsWith(ClipOutput, StringComparison.Ordinal))
                        Debug.LogError("Combat clip still references mesh-bearing FBX: " + path);
                }
            }
        }
    }
}
