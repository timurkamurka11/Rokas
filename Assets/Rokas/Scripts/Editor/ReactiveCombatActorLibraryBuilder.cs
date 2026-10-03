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
        private const string SwordFolder = Source + "Weapons/KeikoSword/";

        [MenuItem("ROKAS/Combat/Build Imported Actor Library")]
        public static void Build()
        {
            ConfigureSwordSources();
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
            library.keiko.idle = Clip("Keiko/Two handed battle idle.fbx", true);
            library.keiko.attack = Clip("Keiko/Normal attack corrected.fbx", true);
            library.keiko.heavy = Clip("Keiko/Hard jump attack corrected.fbx", true);
            library.keiko.preparation = Clip("Keiko/Normal preparation.fbx", true);
            library.keiko.heavyPreparation = Clip("Keiko/Heavy preparation.fbx", true);
            library.keiko.throwPreparation = Clip("Keiko/Throw preparation.fbx", true);
            library.keiko.throwAttack = Clip("Keiko/Throw attack corrected.fbx", true);
            library.keiko.throwReleaseSeconds = 51f / 120f;
            library.keiko.throwContactSeconds = .785f;
            library.keiko.throwingDaggerPrefab = ThrowingDaggerPrefab();
            library.keiko.daggerSocketPosition = new Vector3(0f, .033f, 0f);
            library.keiko.daggerSocketEuler = Vector3.zero;
            library.keiko.entranceWalk = Clip("Keiko/Enter battle corrected.fbx", true);
            library.keiko.enterBattle = Clip("Keiko/Enter battle settle.fbx", true);
            library.keiko.guard = Clip("Keiko/Two handed sword block.fbx", true);
            library.keiko.dodge = Clip("Keiko/Two handed dodge backstep.fbx", true);
            library.keiko.guardContactSeconds = .34f;
            library.keiko.dodgeContactSeconds = .24f;
            library.keiko.attackContactNormalized = 18f / 33f;
            library.keiko.heavyContactNormalized = 26f / 43f;
            library.keiko.weaponPrefab = SwordPrefab();
            library.keiko.weaponBonePath = "mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2/mixamorig:RightShoulder/mixamorig:RightArm/mixamorig:RightForeArm/mixamorig:RightHand";
            library.keiko.weaponSocketPosition = new Vector3(0f, .033f, 0f);
            library.keiko.weaponSocketEuler = Vector3.zero;
            library.keiko.weaponSocketScale = Vector3.one;
            library.keiko.hit = null;
            library.keiko.stagger = null;
            library.keiko.death = null;
            library.keiko.walk = null;
            library.keiko.approach = Clip("Keiko/Two handed approach.fbx", true);
            library.keiko.returnHome = Clip("Keiko/Two handed return.fbx", true);
            library.keiko.approachStrideDistance = 1.62086f;
            library.keiko.returnStrideDistance = .90815f;
            library.keiko.entranceStrideDistance = 1.46354f;
            library.keiko.standingHeight = 2f;
            library.keiko.forwardYaw = 65f;

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
            library.mina.approach = null;
            library.mina.returnHome = null;
            library.mina.standingHeight = 2f;
            library.mina.forwardYaw = 0f;

            library.yokai.model = Model("Yokai/Still stance.fbx");
            library.yokai.material = ActorMaterial("Yokai",
                "Yokai/fantasy+creature+3d+model_basecolor.jpg", null);
            library.yokai.idle = Clip("Yokai/Still stance.fbx");
            library.yokai.attack = Clip("Yokai/Claw attack corrected.fbx", true);
            library.yokai.heavy = Clip("Yokai/Claw heavy corrected.fbx", true);
            library.yokai.attackContactNormalized = .55f / 1.2f;
            library.yokai.heavyContactNormalized = .8f / 1.5f;
            library.yokai.hit = Clip("Yokai/Hit.fbx");
            library.yokai.stagger = Clip("Yokai/hit 2.fbx");
            library.yokai.death = Clip("Yokai/Death.fbx");
            library.yokai.walk = Clip("Yokai/Claw locomotion corrected.fbx", true);
            library.yokai.approach = library.yokai.walk;
            library.yokai.returnHome = library.yokai.walk;
            library.yokai.entranceWalk = library.yokai.walk;
            library.yokai.approachStrideDistance = .68552f;
            library.yokai.returnStrideDistance = .68552f;
            library.yokai.entranceStrideDistance = .68552f;
            library.yokai.standingHeight = 2.7f;
            library.yokai.forwardYaw = 65f;

            EditorUtility.SetDirty(library);
            Verify("Keiko", library.keiko);
            Verify("Mina", library.mina);
            Verify("Yokai", library.yokai);
            VerifyClipSeparation(library);
            AssetDatabase.SaveAssets();
            Debug.Log("ReactiveCombatActorLibrary built at " + Output);
        }

        private static GameObject Model(string relative)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + relative);
            if (model == null) throw new InvalidOperationException("Missing combat model " + relative);
            return model;
        }

        private static AnimationClip Clip(string relative, bool inPlaceHorizontal = false)
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
                // This repair authors Keiko sources. Preserve already verified
                // Mina/Yokai extracted clips instead of serializing unrelated
                // compressed curves again when rebuilding the sword library.
                if (copy != null && group != "Keiko" && !relative.Contains("corrected.fbx")) return copy;
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
                RemoveBlenderArmatureWrapper(copy);
                if (inPlaceHorizontal) RemoveHorizontalHipTranslation(copy);
                AnimationUtility.SetAnimationEvents(copy, new AnimationEvent[0]);
                return copy;
            }
            throw new InvalidOperationException("Missing combat animation " + relative);
        }

        private static void RemoveBlenderArmatureWrapper(AnimationClip clip)
        {
            // Blender keeps an FBX Armature object above the same existing Hips.
            // The runtime Keiko model starts at Hips. Wrapper transform curves
            // must not overwrite the stage's actor sizing or orientation.
            const string wrapper = "Armature";
            const string prefix = wrapper + "/";
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.path == wrapper)
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                    continue;
                }
                if (!binding.path.StartsWith(prefix + "mixamorig:Hips", StringComparison.Ordinal)) continue;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                EditorCurveBinding mapped = binding;
                mapped.path = binding.path.Substring(prefix.Length);
                AnimationUtility.SetEditorCurve(clip, binding, null);
                AnimationUtility.SetEditorCurve(clip, mapped, curve);
            }
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureSwordSources()
        {
            AssetDatabase.Refresh();
            var frameCounts = new Dictionary<string, float>
            {
                { "Two handed battle idle.fbx", 240f },
                { "Normal preparation.fbx", 36f },
                { "Heavy preparation.fbx", 56f },
                { "Throw preparation.fbx", 48f },
                { "Throw attack corrected.fbx", 132f },
                { "Normal attack corrected.fbx", 132f },
                { "Hard jump attack corrected.fbx", 172f },
                { "Two handed approach.fbx", 108f },
                { "Two handed return.fbx", 92f },
                { "Enter battle corrected.fbx", 72f },
                { "Enter battle settle.fbx", 56f },
                { "Two handed sword block.fbx", 96f },
                { "Two handed dodge backstep.fbx", 96f },
                { "Enter the batle.fbx", 18f }
            };
            foreach (KeyValuePair<string, float> take in frameCounts)
            {
                string file = take.Key;
                var importer = AssetImporter.GetAtPath(Source + "Keiko/" + file) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing sword attack source: " + file);
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.optimizeGameObjects = false;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.resampleCurves = false;
                var takes = importer.defaultClipAnimations;
                if (takes.Length != 1) throw new InvalidOperationException("Expected one authored attack take: " + file);
                takes[0].firstFrame = 0f;
                takes[0].lastFrame = take.Value;
                takes[0].loopTime = file == "Two handed battle idle.fbx" ||
                    file == "Two handed approach.fbx" || file == "Two handed return.fbx" ||
                    file == "Enter battle corrected.fbx";
                takes[0].loopPose = false;
                importer.clipAnimations = takes;
                importer.SaveAndReimport();
            }
            foreach (KeyValuePair<string, float> take in new Dictionary<string, float>
            {
                { "Claw attack corrected.fbx", 144f },
                { "Claw heavy corrected.fbx", 180f },
                { "Claw locomotion corrected.fbx", 56f }
            })
            {
                var importer = AssetImporter.GetAtPath(Source + "Yokai/" + take.Key) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing authored monster source: " + take.Key);
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.optimizeGameObjects = false;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.resampleCurves = false;
                var takes = importer.defaultClipAnimations;
                if (takes.Length != 1) throw new InvalidOperationException("Expected one monster take: " + take.Key);
                takes[0].firstFrame = 0f;
                takes[0].lastFrame = take.Value;
                takes[0].loopTime = take.Key == "Claw locomotion corrected.fbx";
                takes[0].loopPose = false;
                importer.clipAnimations = takes;
                importer.SaveAndReimport();
            }
            var sword = AssetImporter.GetAtPath(SwordFolder + "metal+sword+3d+model.fbx") as ModelImporter;
            if (sword == null) throw new InvalidOperationException("Missing sword model");
            sword.importAnimation = false;
            sword.animationType = ModelImporterAnimationType.None;
            sword.materialImportMode = ModelImporterMaterialImportMode.None;
            sword.importNormals = ModelImporterNormals.Import;
            sword.importTangents = ModelImporterTangents.CalculateMikk;
            sword.isReadable = false;
            sword.SaveAndReimport();
        }

        private static GameObject SwordPrefab()
        {
            string path = SwordFolder + "KeikoSword.prefab";
            string materialPath = SwordFolder + "KeikoSword.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = "Combat_KeikoSword" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SwordFolder + "metal+sword+3d+model_basecolor.jpg");
            material.color = Color.white;
            material.SetFloat("_Metallic", .28f);
            material.SetFloat("_Glossiness", .32f);
            EditorUtility.SetDirty(material);
            var root = new GameObject("KeikoSword");
            try
            {
                GameObject mesh = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SwordFolder + "metal+sword+3d+model.fbx"), root.transform, false);
                mesh.name = "SwordMesh";
                // Source handle is at upper right; wrapper puts the grip at zero and blade on +Z.
                Vector3 grip = new Vector3(-.34207f, .90139f, .00035f);
                Vector3 blade = new Vector3(.75112f, -.8907f, -.00035f).normalized;
                Quaternion orientation = Quaternion.FromToRotation(blade, Vector3.forward);
                mesh.transform.localRotation = orientation;
                mesh.transform.localScale = Vector3.one * .55f;
                mesh.transform.localPosition = -(orientation * grip) * .55f;
                foreach (Renderer renderer in mesh.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
                Transform leftHandGrip = new GameObject("LeftHandGrip").transform;
                leftHandGrip.SetParent(root.transform, false);
                leftHandGrip.localPosition = new Vector3(0f, 0f, -.055f);
                leftHandGrip.localRotation = Quaternion.Euler(0f, 0f, 180f);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject ThrowingDaggerPrefab()
        {
            const string folder = Source + "Weapons/KeikoThrowingDagger/";
            const string path = folder + "KeikoThrowingDagger.prefab";
            var importer = AssetImporter.GetAtPath(folder + "KeikoThrowingDagger.fbx") as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing authored throwing dagger.");
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var root = new GameObject("KeikoThrowingDagger");
            try
            {
                GameObject mesh = UnityEngine.Object.Instantiate(Model("Weapons/KeikoThrowingDagger/KeikoThrowingDagger.fbx"), root.transform, false);
                mesh.name = "DaggerMesh";
                var renderers = mesh.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Vector3 axis = bounds.size.y > bounds.size.z && bounds.size.y > bounds.size.x ? Vector3.up :
                    bounds.size.x > bounds.size.z ? Vector3.right : Vector3.forward;
                if (Vector3.Dot(bounds.center - root.transform.position, axis) < 0f) axis = -axis;
                mesh.transform.localRotation = Quaternion.FromToRotation(axis, Vector3.forward) * mesh.transform.localRotation;
                var colors = new[] { new Color(.12f, .16f, .2f), new Color(.55f, .65f, .71f),
                    new Color(.36f, .25f, .12f), new Color(.055f, .058f, .068f), new Color(.24f, .65f, .74f) };
                var names = new[] { "ObsidianSteel", "HonedSteel", "AgedBronze", "CharcoalWrap", "FrostRune" };
                var materials = new Material[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    string materialPath = folder + names[i] + ".mat";
                    materials[i] = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (materials[i] == null)
                    {
                        materials[i] = new Material(Shader.Find("Standard")) { name = names[i] };
                        AssetDatabase.CreateAsset(materials[i], materialPath);
                    }
                    materials[i].color = colors[i];
                    materials[i].SetFloat("_Metallic", i == 3 ? 0f : .7f);
                    materials[i].SetFloat("_Glossiness", i == 3 ? .18f : .4f);
                    if (i == 4)
                    {
                        materials[i].EnableKeyword("_EMISSION");
                        materials[i].SetColor("_EmissionColor", colors[i] * .4f);
                    }
                    EditorUtility.SetDirty(materials[i]);
                }
                foreach (Renderer renderer in renderers) renderer.sharedMaterials = materials;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // The arena moves the actor root; keep the imported run cycle in place.
        private static void RemoveHorizontalHipTranslation(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.path != "mixamorig:Hips" ||
                    !binding.propertyName.Contains("LocalPosition") ||
                    !(binding.propertyName.EndsWith(".x", StringComparison.Ordinal) ||
                      binding.propertyName.EndsWith(".z", StringComparison.Ordinal))) continue;
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
            EditorUtility.SetDirty(clip);
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
                             clips.stagger, clips.death, clips.walk, clips.approach, clips.returnHome,
                             clips.preparation, clips.heavyPreparation, clips.enterBattle,
                             clips.entranceWalk, clips.guard, clips.dodge, clips.throwPreparation, clips.throwAttack })
                {
                    if (clip == null) continue;
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    int matching = 0;
                    foreach (EditorCurveBinding binding in bindings)
                        if (paths.Contains(binding.path)) matching++;
                    Debug.Log("Combat clip " + name + "/" + clip.name + ": legacy=" + clip.legacy +
                        ", length=" + clip.length.ToString("F3") + "s, bindings=" + bindings.Length +
                        ", matching model paths=" + matching);
                    if (!clip.legacy || bindings.Length == 0 || matching != bindings.Length)
                        throw new InvalidOperationException("Combat animation must bind every curve to the existing model: " +
                            name + "/" + clip.name + " matched " + matching + "/" + bindings.Length);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void VerifyClipSeparation(ReactiveCombatActorLibrary library)
        {
            foreach (ReactiveCombatActorClips actor in new[] { library.keiko, library.mina, library.yokai })
            {
                foreach (AnimationClip clip in new[] { actor.idle, actor.attack, actor.heavy, actor.hit,
                             actor.stagger, actor.death, actor.walk, actor.approach, actor.returnHome,
                             actor.preparation, actor.heavyPreparation, actor.enterBattle,
                             actor.entranceWalk, actor.guard, actor.dodge, actor.throwPreparation, actor.throwAttack })
                {
                    if (clip == null) continue;
                    string path = AssetDatabase.GetAssetPath(clip);
                    if (!path.StartsWith(ClipOutput, StringComparison.Ordinal))
                        throw new InvalidOperationException("Combat clip still references mesh-bearing FBX: " + path);
                }
            }
        }
    }
}
