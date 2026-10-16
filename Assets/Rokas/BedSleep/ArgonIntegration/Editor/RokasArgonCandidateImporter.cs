#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Imports the verified Argon FBX sources into a NEW, isolated preview prefab.
    /// This deliberately does NOT replace ROKAS V10's active resource prefab:
    /// the Argon files have 3 independent skinned renderers and a different
    /// 25-bone hierarchy, so dropping them into the original controller is unsafe.
    /// </summary>
    public static class RokasArgonCandidateImporter
    {
        public const string Source = "Assets/Rokas/BedSleep/ArgonSource/";
        public const string Output = "Assets/Rokas/BedSleep/ArgonCandidate/";
        private const string ArmFile = Source + "Models/ROKAS_Keiko_FPS_Arm_Rigged.fbx";
        private const string WatchFile = Source + "Models/ROKAS_Smartwatch_HighDetail.fbx";
        public const string CandidatePrefab = Output + "PF_Argon_Keiko_Hand_Watch_Preview.prefab";

        private static readonly string[] Skins =
        {
            "Keiko_Hand_Skin_Mesh", "Keiko_Fingernails_Mesh",
            "Keiko_Homewear_Sleeve_Mesh"
        };
        private static readonly string[] WatchParts =
        {
            "Watch_TitaniumCase", "Watch_Strap_Upper", "Watch_Strap_Lower",
            "Watch_OLED_Display", "Watch_SapphireGlass",
            "Watch_DigitalCrown_Rotary", "Watch_SideConfirmButton"
        };
        private static readonly string[] Clips =
        {
            "SleepArm_HiddenIdle", "SleepArm_Raise", "SleepArm_WristTurn",
            "SleepArm_InspectIdle", "SleepArm_CrownFeedback", "SleepArm_Confirm",
            "SleepArm_Lower", "SleepArm_ReturnIdle"
        };

        [MenuItem("ROKAS/Bed Sleep/Argon/Validate and build isolated candidate")]
        public static void BuildCandidate()
        {
            if (!AssetDatabase.IsValidFolder(Source + "Models"))
                throw new InvalidOperationException("Argon source assets are not staged under " +
                    Source + "Models. Do not substitute the old V10 FBX.");

            var armSource = AssetDatabase.LoadAssetAtPath<GameObject>(ArmFile);
            var watchSource = AssetDatabase.LoadAssetAtPath<GameObject>(WatchFile);
            if (armSource == null || watchSource == null)
                throw new InvalidOperationException("Actual Argon FBX import is missing: " +
                    ArmFile + " / " + WatchFile);

            ConfigureImportedFbx(ArmFile, true);
            ConfigureImportedFbx(WatchFile, false);
            // Read importer assets after the reimport, never retain stale
            // FBX subasset references across SaveAndReimport().
            armSource = AssetDatabase.LoadAssetAtPath<GameObject>(ArmFile);
            watchSource = AssetDatabase.LoadAssetAtPath<GameObject>(WatchFile);
            VerifyModelImporter(ArmFile);
            VerifyModelImporter(WatchFile);
            var sourceRig = armSource.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (sourceRig.Length != 3)
                throw new InvalidOperationException("Argon requires 3 real SkinnedMeshRenderers, got " +
                    sourceRig.Length + ". The old V10 1-renderer/3-submesh asset is not compatible.");
            foreach (string name in Skins)
            {
                var renderer = sourceRig.SingleOrDefault(r => r.gameObject.name == name);
                if (renderer == null || renderer.sharedMesh == null)
                    throw new InvalidOperationException("Missing imported skinned mesh: " + name);
                if (renderer.sharedMesh.bindposes.Length != 25 ||
                    renderer.bones.Length != 25)
                    throw new InvalidOperationException(name +
                        " needs 25 actual imported bone bindings / bindposes.");
            }
            var boneNames = armSource.GetComponentsInChildren<Transform>(true)
                .Select(t => t.name).ToList();
            if (boneNames.Count(x => x == "Wrist_WatchAttachment") != 1 ||
                boneNames.Count(x => x == "Arm_ForearmRoot") != 1)
                throw new InvalidOperationException("Argon rig/socket names not imported correctly.");
            var sourceParts = watchSource.GetComponentsInChildren<MeshRenderer>(true);
            if (sourceParts.Length != 7)
                throw new InvalidOperationException("Expected exactly seven authored watch meshes.");
            foreach (string name in WatchParts)
            {
                var part = sourceParts.SingleOrDefault(x => x.name == name);
                if (part == null || part.GetComponent<MeshFilter>()?.sharedMesh == null)
                    throw new InvalidOperationException("Missing physical watch part: " + name);
            }
            var oled = sourceParts.Single(x => x.name == "Watch_OLED_Display");
            var displayMesh = oled.GetComponent<MeshFilter>().sharedMesh;
            if (!displayMesh.isReadable || displayMesh.uv.Length != displayMesh.vertexCount)
                throw new InvalidOperationException("Argon OLED needs readable full UV mesh for QA.");
            Vector2[] uv = displayMesh.uv;
            float minU = uv.Min(v => v.x), maxU = uv.Max(v => v.x);
            float minV = uv.Min(v => v.y), maxV = uv.Max(v => v.y);
            if (maxU - minU < .95f || maxV - minV < .95f)
                throw new InvalidOperationException("Argon OLED UVs do not cover full display.");

            var loadedClips = AssetDatabase.LoadAllAssetsAtPath(ArmFile)
                .OfType<AnimationClip>()
                .Where(a => !a.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            var clipMap = new Dictionary<string, AnimationClip>();
            foreach (string clipName in Clips)
            {
                var found = loadedClips.Where(c =>
                    c.name == clipName || c.name.EndsWith("|" + clipName,
                        StringComparison.Ordinal) || c.name.Contains(clipName + "_Armature"))
                    .ToArray();
                if (found.Length != 1 || found[0].length <= 0.01f)
                    throw new InvalidOperationException("Missing unique, non-empty FBX clip: " +
                        clipName + " imported=" + string.Join(", ", loadedClips.Select(c => c.name)));
                clipMap[clipName] = found[0];
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(CandidatePrefab) != null)
                throw new InvalidOperationException(
                    "Existing Argon candidate would be overwritten. Preserve it and choose a new version.");
            EnsureFolder("Assets/Rokas/BedSleep", "ArgonCandidate");

            var root = new GameObject("PF_Argon_Keiko_Hand_Watch_Preview");
            try
            {
                var arm = (GameObject)PrefabUtility.InstantiatePrefab(armSource);
                var watch = (GameObject)PrefabUtility.InstantiatePrefab(watchSource);
                if (arm == null || watch == null)
                    throw new InvalidOperationException("Cannot instantiate authentic Argon FBX.");

                arm.transform.SetParent(root.transform, false);
                watch.transform.SetParent(root.transform, false);

                var visualSkin = arm.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                SetMaterial(visualSkin, "Keiko_Hand_Skin_Mesh", "Mat_Keiko_Skin",
                    "Textures/Skin/Keiko_Skin_Albedo.png", "Textures/Skin/Keiko_Skin_Normal.png",
                    "Textures/Skin/Keiko_Skin_AO.png", .02f, .58f);
                SetMaterial(visualSkin, "Keiko_Fingernails_Mesh", "Mat_Keiko_Nails",
                    "Textures/Skin/Keiko_Nails_Albedo.png", null, null, .04f, .84f);
                SetMaterial(visualSkin, "Keiko_Homewear_Sleeve_Mesh", "Mat_Keiko_Sleeve",
                    "Textures/Sleeve/Keiko_Sleeve_Albedo.png",
                    "Textures/Sleeve/Keiko_Sleeve_Normal.png", null, 0f, .18f);

                var meshes = watch.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var part in meshes)
                {
                    switch (part.name)
                    {
                        case "Watch_TitaniumCase":
                        case "Watch_SideConfirmButton":
                            part.sharedMaterial = EnsureMaterial("Mat_Watch_TitaniumCase",
                                "Textures/Watch/Watch_Titanium_Albedo.png",
                                "Textures/Watch/Watch_Titanium_Normal.png",
                                null, .9f, .73f); break;
                        case "Watch_Strap_Upper":
                        case "Watch_Strap_Lower":
                            part.sharedMaterial = EnsureMaterial("Mat_Watch_SiliconeStrap",
                                "Textures/Watch/Watch_Strap_Albedo.png",
                                "Textures/Watch/Watch_Strap_Normal.png",
                                null, .03f, .54f); break;
                        case "Watch_DigitalCrown_Rotary":
                            part.sharedMaterial = EnsureMaterial("Mat_Watch_DigitalCrown",
                                "Textures/Watch/Watch_Titanium_Albedo.png",
                                "Textures/Watch/Watch_Titanium_Normal.png",
                                null, .92f, .8f); break;
                        case "Watch_OLED_Display":
                            part.sharedMaterial = EnsureOledMaterial(); break;
                        case "Watch_SapphireGlass":
                            part.sharedMaterial = EnsureGlassMaterial(); break;
                    }
                }
                // Both FBX assets use the same authored rest/metric coordinates.
                // Keep the world-space alignment before parenting to socket.
                var socket = arm.GetComponentsInChildren<Transform>(true)
                    .Single(t => t.name == "Wrist_WatchAttachment");
                watch.transform.SetParent(socket, true);
                var displayObject = watch.GetComponentsInChildren<Transform>(true)
                    .Single(t => t.name == "Watch_OLED_Display");
                var collider = displayObject.GetComponent<MeshCollider>();
                if (collider == null) collider = displayObject.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = displayObject.GetComponent<MeshFilter>().sharedMesh;
                collider.convex = false;

                var importedAnimator = arm.GetComponent<Animator>();
                if (importedAnimator == null) importedAnimator = arm.AddComponent<Animator>();
                importedAnimator.runtimeAnimatorController = BuildController(clipMap);
                importedAnimator.applyRootMotion = false;

                // Validate *real* imported world scale after prefab instance,
                // never apply an arbitrary multiplier as a silent correction.
                Bounds armBounds = visualSkin[0].bounds;
                foreach (var skin in visualSkin) armBounds.Encapsulate(skin.bounds);
                float armLongest = Mathf.Max(armBounds.size.x,
                    Mathf.Max(armBounds.size.y, armBounds.size.z));
                if (armLongest < .25f || armLongest > .65f)
                    throw new InvalidOperationException("Imported arm scale " + armLongest +
                        "m is not physically plausible. Check FBX ModelImporter units.");
                Bounds oledBounds = displayObject.GetComponent<MeshRenderer>().bounds;
                float oledLongest = Mathf.Max(oledBounds.size.x,
                    Mathf.Max(oledBounds.size.y, oledBounds.size.z));
                if (oledLongest < .025f || oledLongest > .07f)
                    throw new InvalidOperationException("Imported OLED size " + oledLongest +
                        "m is wrong. Check FBX units/scene roots.");

                PrefabUtility.SaveAsPrefabAsset(root, CandidatePrefab);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ROKAS ARGON] TRUE imported preview candidate built: " +
                    CandidatePrefab + " 3 skinned meshes, 25 bones, 7 watch parts," +
                    " 8 clips, live-compatible OLED surface. NOT ACTIVE in Bed Sleep V10.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureImportedFbx(string path, bool animated)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Authentic FBX ModelImporter missing: " + path);
            bool change = false;
            if (!importer.isReadable) { importer.isReadable = true; change = true; }
            if (animated && !importer.importAnimation)
                { importer.importAnimation = true; change = true; }
            if (animated && importer.animationType != ModelImporterAnimationType.Generic)
                { importer.animationType = ModelImporterAnimationType.Generic; change = true; }
            // Keep file's meter scale until bounds validation proves otherwise.
            // FBX GlobalSettings declares centimeters-per-unit; this must
            // be verified by the importer and never guessed as an extra x100.
            if (change) importer.SaveAndReimport();
        }

        private static void VerifyModelImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Not a genuine imported FBX: " + path);
            // Do not reimport automatically here: it can silently remap bones or scales.
            if (path == ArmFile && (!importer.importAnimation ||
                importer.animationType != ModelImporterAnimationType.Generic))
                throw new InvalidOperationException("Set Argon Arm FBX to Generic + Import Animation before building.");
        }

        private static void SetMaterial(SkinnedMeshRenderer[] renderers, string mesh,
            string name, string albedo, string normal, string ao, float metal, float smooth)
        {
            var renderer = renderers.Single(x => x.name == mesh);
            renderer.sharedMaterial = EnsureMaterial(name, albedo, normal, ao, metal, smooth);
            renderer.updateWhenOffscreen = true;
        }

        private static Material EnsureMaterial(string name, string albedo, string normal,
            string ao, float metallic, float smoothness)
        {
            string path = Output + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Unity Standard PBR shader not available.");
            var material = new Material(shader) { name = name };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            SetTexture(material, "_MainTex", albedo);
            if (!string.IsNullOrEmpty(normal))
            {
                SetTexture(material, "_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (!string.IsNullOrEmpty(ao)) SetTexture(material, "_OcclusionMap", ao);
            // The supplied titanium map already packs metallic and smoothness.
            // Assign its real PBR texture rather than a flat grey metallic value.
            if (name == "Mat_Watch_TitaniumCase" || name == "Mat_Watch_DigitalCrown")
            {
                SetTexture(material, "_MetallicGlossMap",
                    "Textures/Watch/Watch_Titanium_MetallicSmoothness.png");
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetTexture(Material material, string property, string path)
        {
            string assetPath = Source + path;
            if (property == "_BumpMap")
            {
                var normalImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (normalImporter != null &&
                    normalImporter.textureType != TextureImporterType.NormalMap)
                {
                    normalImporter.textureType = TextureImporterType.NormalMap;
                    normalImporter.SaveAndReimport();
                }
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
                throw new InvalidOperationException("Missing real Argon texture: " + assetPath);
            material.SetTexture(property, texture);
        }

        private static Material EnsureOledMaterial()
        {
            string path = Output + "Mat_Watch_OLED_LiveCandidate.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Unlit/Texture");
            if (shader == null) throw new InvalidOperationException("Unlit/Texture missing");
            var material = new Material(shader) { name = "Mat_Watch_OLED_LiveCandidate" };
            SetTexture(material, "_MainTex",
                "Textures/Watch/Watch_OLED_NeutralGrid_Template.png");
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material EnsureGlassMaterial()
        {
            string path = Output + "Mat_Watch_SapphireGlass.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader missing");
            var material = new Material(shader) { name = "Mat_Watch_SapphireGlass" };
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = 3000;
            material.SetColor("_Color", new Color(.82f, .92f, 1f, .06f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static AnimatorController BuildController(Dictionary<string,AnimationClip> clips)
        {
            string path = Output + "AC_ArgonSleepArm_Preview.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                throw new InvalidOperationException("Existing Argon Animator would be overwritten.");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var child in stateMachine.states)
                stateMachine.RemoveState(child.state);
            var states = new Dictionary<string,AnimatorState>();
            foreach (var pair in clips)
            {
                var state = stateMachine.AddState(pair.Key);
                state.motion = pair.Value;
                states[pair.Key] = state;
            }
            stateMachine.defaultState = states["SleepArm_HiddenIdle"];
            AddExit(states["SleepArm_Raise"], states["SleepArm_WristTurn"]);
            AddExit(states["SleepArm_WristTurn"], states["SleepArm_InspectIdle"]);
            AddExit(states["SleepArm_Lower"], states["SleepArm_ReturnIdle"]);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddExit(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = .98f;
            transition.duration = .07f;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
