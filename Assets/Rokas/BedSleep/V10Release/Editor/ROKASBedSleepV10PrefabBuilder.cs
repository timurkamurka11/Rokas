#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Rokas.BedSleep.V10Release.Editor
{
    /// <summary>
    /// REAL FBX -> independently verifiable Unity prefabs.
    /// Strictly creates assets under /Resources/BedSleep/V10Candidate and NEVER
    /// overwrites the existing V6/V7 production prefab/asset GUIDs.
    /// No capsule, generated limb, replacement watch mesh or made-up fileID.
    /// Intended for a disposable Unity copy until visual QA is signed off.
    /// </summary>
    public static class ROKASBedSleepV10PrefabBuilder
    {
        private const string ModelDir = "Assets/Rokas/BedSleep/V10Release/Models/";
        private const string MaterialDir = "Assets/Rokas/BedSleep/V10Release/Materials/";
        private const string AnimDir = "Assets/Rokas/BedSleep/Animations/";
        private const string Destination = "Assets/Rokas/Resources/BedSleep/V10ReleaseCandidate/";
        private const int ArmExpectedBones = 25;
        private const int ArmExpectedMaterials = 3;
        private const int WatchExpectedParts = 7;
        private static readonly string[] ClipNames = {
            "SleepArm_HiddenIdle", "SleepArm_Raise", "SleepArm_WristTurn",
            "SleepArm_InspectIdle", "SleepArm_CrownFeedback", "SleepArm_Confirm",
            "SleepArm_Lower", "SleepArm_ReturnIdle"
        };

        private static readonly string[] WatchNames = {
            "Watch_TitaniumCase", "Watch_Strap_Top", "Watch_Strap_Bottom",
            "Watch_DigitalCrown_Rotary", "Watch_SideConfirmButton",
            "Watch_OLED_Screen", "Watch_SapphireGlass"
        };

        [MenuItem("ROKAS/Bed Sleep/V10/Build REAL Imported Candidate Prefabs")]
        public static void Build()
        {
            var errors = new List<string>();
            var srcArm = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "Keiko_FPS_Arm_Rigged.fbx");
            var srcWatch = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "ROKAS_Smartwatch_HighDetail.fbx");
            // Authoritative source is the RE-EXPORTED FBX with real GLB Actions.
            // Never switch to the old .anim files lacking finger articulation.
            var realClips = new Dictionary<string,AnimationClip>();
            string armFbxPath = ModelDir + "Keiko_FPS_Arm_Rigged.fbx";
            // Real exported glTF Actions must be imported as Unity Generic FBX
            // clips. A GUID-only .meta can omit the ModelImporter animation
            // defaults, so set the actual importer explicitly before preflight.
            var modelImporter = AssetImporter.GetAtPath(armFbxPath) as ModelImporter;
            if (modelImporter == null)
                errors.Add("Actual arm FBX is not a ModelImporter");
            else
            {
                bool needsImport = !modelImporter.importAnimation ||
                    modelImporter.animationType != ModelImporterAnimationType.Generic;
                if (needsImport)
                {
                    modelImporter.importAnimation = true;
                    modelImporter.animationType = ModelImporterAnimationType.Generic;
                    modelImporter.SaveAndReimport();
                }
            }
            var allImported = AssetDatabase.LoadAllAssetsAtPath(armFbxPath)
                .OfType<AnimationClip>().ToArray();
            var realClipNames = string.Join(", ", allImported.Select(c => c.name));
            Debug.Log("[ROKAS V10] Actual imported Blender FBX animation clips (" +
                allImported.Length + "): " + realClipNames);
            if (modelImporter != null)
                Debug.Log("[ROKAS V10] FBX importer defaultClipAnimations: " +
                    string.Join(", ", modelImporter.defaultClipAnimations.Select(c => c.name)));
            foreach (var name in ClipNames)
            {
                var matches = allImported.Where(c => c.name == name || c.name.EndsWith("|"+name,
                    StringComparison.Ordinal) || c.name.EndsWith("/"+name, StringComparison.Ordinal)).ToArray();
                if (matches.Length == 1) realClips.Add(name, matches[0]);
                else errors.Add("Real FBX must contain exactly one animation Action " + name +
                    " (found " + matches.Length + ") of real imported clips: " + realClipNames);
            }
            if (srcArm == null) errors.Add("Import the authentic Blender FBX arm first: " + ModelDir);
            if (srcWatch == null) errors.Add("Import the seven-part Blender FBX watch first: " + ModelDir);
            foreach (var clipName in new[]{"SleepArm_Raise", "SleepArm_Lower"})
            {
                AnimationClip clip;
                if (!realClips.TryGetValue(clipName, out clip)) continue;
                var bindings = AnimationUtility.GetCurveBindings(clip);
                // The input GLB contains 15 finger-joint rotation channels;
                // if Blender FBX export omitted them the release must fail.
                foreach (var finger in new[]{"Thumb", "Index", "Middle", "Ring", "Pinky"})
                foreach (int bone in new[]{1,2,3})
                {
                    string joint = finger + "_0" + bone + "_L";
                    if (!bindings.Any(b => b.path.Contains(joint)))
                        errors.Add("Imported FBX clip " + clipName +
                            " missing animated joint " + joint);
                }
            }
            string[] outNames = {"PF_Keiko_FPS_Arm_V10_Candidate", "PF_ROKAS_Smartwatch_V10_Candidate",
                "PF_BedSleep_3DComposite_V10_Candidate"};
            if (AssetDatabase.LoadMainAssetAtPath(Destination + "AC_KeikoBedSleepArm_V10_Candidate.controller") != null)
                errors.Add("V10 Animator Controller candidate already exists; refusing overwrite");
            foreach (var n in outNames)
            {
                if (AssetDatabase.LoadMainAssetAtPath(Destination + n + ".prefab") != null)
                    errors.Add("Candidate already exists; refusing to overwrite: " + Destination + n);
            }
            if (srcArm != null)
            {
                var smr = srcArm.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .OrderByDescending(x => x.sharedMesh == null ? 0 : x.sharedMesh.vertexCount).FirstOrDefault();
                if (smr == null || smr.sharedMesh == null) errors.Add("Arm FBX has no real skinned renderer");
                else
                {
                    if (smr.bones.Length != ArmExpectedBones) errors.Add("Arm FBX does not bind all 25 bones");
                    if (smr.sharedMesh.bindposes.Length != ArmExpectedBones) errors.Add("Arm FBX has wrong bind-pose count");
                    if (smr.sharedMesh.subMeshCount != ArmExpectedMaterials)
                        errors.Add("V10 must contain three real material primitives: skin, sleeve, five skinned fingernail shells");
                    if (smr.sharedMesh.vertexCount < 9000) errors.Add("Arm FBX is missing required high-poly sculpt mesh");
                }
                foreach (var bn in KeikoFPSArmRigController.ExpectedBoneNames)
                    if (FindDescendant(srcArm.transform, bn) == null)
                        errors.Add("Arm FBX missing imported bone " + bn);
            }
            if (srcWatch != null)
            {
                var meshes = new HashSet<Mesh>();
                foreach (var n in WatchNames)
                {
                    var t = FindDescendant(srcWatch.transform, n);
                    var filter = t == null ? null : t.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                        errors.Add("Watch FBX missing separately imported mesh: " + n);
                    else if (!meshes.Add(filter.sharedMesh))
                        errors.Add("Duplicate geometry mesh reused by watch part: " + n);
                }
                if (meshes.Count != WatchExpectedParts) errors.Add("Watch does not have seven distinct mesh assets");
            }
            var skin = RequireMaterial("M_Keiko_Skin", errors);
            var sleeve = RequireMaterial("M_Keiko_Sleeve", errors);
            // V10 enamel/nail plate material is an intentional third mesh group.
            // Saved once as a real independent asset alongside candidate prefabs.
            if (AssetDatabase.LoadMainAssetAtPath(Destination + "M_Keiko_Nails_V10_Candidate.mat") != null)
                errors.Add("V10 nail material candidate already exists; refusing overwrite");
            var titanium = RequireMaterial("M_Watch_Titanium", errors);
            var strap = RequireMaterial("M_Watch_Strap", errors);
            var glass = RequireMaterial("M_Watch_Glass", errors);
            var oled = RequireMaterial("M_Watch_OLED", errors);
            if (skin != null && skin.mainTexture == null)
                errors.Add("Skin material missing real UV albedo texture");
            if (errors.Count != 0)
                throw new InvalidOperationException("V10 native asset preflight failed:\n" +
                    string.Join("\n", errors.Select(x => " - " + x)));

            EnsureDestination();
            GameObject armRoot = null, watchRoot = null, compRoot = null;
            try
            {
                var controller = CreateAnimator(realClips);
                Material nail = CreateNailMaterial(skin);
                armRoot = CreateArm(srcArm, controller, skin, sleeve, nail);
                var armSaved = Save(armRoot, outNames[0]);
                watchRoot = CreateWatch(srcWatch, titanium, strap, glass, oled);
                var watchSaved = Save(watchRoot, outNames[1]);
                compRoot = CreateComposite(armSaved, watchSaved);
                Save(compRoot, outNames[2]);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[ROKAS V10] Three genuine imported candidate prefabs created, NO original prefabs overwritten. " +
                          "Now run the native AssetDatabase QA and record real Unity Game View.");
            }
            finally
            {
                if (armRoot != null) UnityEngine.Object.DestroyImmediate(armRoot);
                if (watchRoot != null) UnityEngine.Object.DestroyImmediate(watchRoot);
                if (compRoot != null) UnityEngine.Object.DestroyImmediate(compRoot);
            }
        }

        private static AnimatorController CreateAnimator(Dictionary<string,AnimationClip> clips)
        {
            string path = Destination + "AC_KeikoBedSleepArm_V10_Candidate.controller";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Animator candidate exists; refusing overwrite: " + path);
            AnimatorController anim = AnimatorController.CreateAnimatorControllerAtPath(path);
            var stateMachine = anim.layers[0].stateMachine;
            // Unity may generate an unused default state on creation.
            foreach (var child in stateMachine.states)
                stateMachine.RemoveState(child.state);
            foreach (var name in new[]{"RaiseTrigger", "LowerTrigger", "ConfirmTrigger", "CrownTickTrigger"})
                anim.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var states = new Dictionary<string,AnimatorState>();
            foreach (var name in ClipNames)
            {
                var state = stateMachine.AddState(name);
                state.motion = clips[name];
                states[name] = state;
            }
            stateMachine.defaultState = states["SleepArm_HiddenIdle"];
            WithTrigger(states["SleepArm_HiddenIdle"], states["SleepArm_Raise"], "RaiseTrigger");
            WithExit(states["SleepArm_Raise"], states["SleepArm_WristTurn"]);
            WithExit(states["SleepArm_WristTurn"], states["SleepArm_InspectIdle"]);
            WithTrigger(states["SleepArm_InspectIdle"], states["SleepArm_CrownFeedback"], "CrownTickTrigger");
            WithExit(states["SleepArm_CrownFeedback"], states["SleepArm_InspectIdle"]);
            WithTrigger(states["SleepArm_InspectIdle"], states["SleepArm_Confirm"], "ConfirmTrigger");
            WithTrigger(states["SleepArm_Confirm"], states["SleepArm_Lower"], "LowerTrigger");
            WithTrigger(states["SleepArm_InspectIdle"], states["SleepArm_Lower"], "LowerTrigger");
            WithExit(states["SleepArm_Lower"], states["SleepArm_ReturnIdle"]);
            WithExit(states["SleepArm_ReturnIdle"], states["SleepArm_HiddenIdle"]);
            EditorUtility.SetDirty(anim);
            return anim;
        }

        private static void WithTrigger(AnimatorState from, AnimatorState to, string trigger)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = .08f;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void WithExit(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = .96f;
            t.duration = .08f;
        }

        private static Material CreateNailMaterial(Material skin)
        {
            if (skin == null || skin.shader == null)
                throw new InvalidOperationException("Real imported skin shader required for the keratin material");
            string path = Destination + "M_Keiko_Nails_V10_Candidate.mat";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Never overwrite existing candidate nail material");
            var nail = new Material(skin) { name = "M_Keiko_Nails_V10_Candidate" };
            nail.mainTexture = null;
            if (nail.HasProperty("_BumpMap")) nail.SetTexture("_BumpMap", null);
            if (nail.HasProperty("_Metallic")) nail.SetFloat("_Metallic", 0f);
            if (nail.HasProperty("_Color")) nail.SetColor("_Color", new Color(.77f,.52f,.47f,1f));
            if (nail.HasProperty("_BaseColor")) nail.SetColor("_BaseColor", new Color(.77f,.52f,.47f,1f));
            if (nail.HasProperty("_Glossiness")) nail.SetFloat("_Glossiness", .62f);
            if (nail.HasProperty("_Smoothness")) nail.SetFloat("_Smoothness", .62f);
            AssetDatabase.CreateAsset(nail, path);
            return nail;
        }

        private static GameObject CreateArm(GameObject source, RuntimeAnimatorController controller,
            Material skin, Material sleeve, Material nail)
        {
            var root = new GameObject("PF_Keiko_FPS_Arm_V10_Candidate");
            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException("Unity failed to instantiate imported arm FBX");
            instance.transform.SetParent(root.transform, false);
            var smr = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .OrderByDescending(x => x.sharedMesh == null ? 0 : x.sharedMesh.vertexCount).First();
            if (smr.sharedMesh.subMeshCount != 3)
                throw new InvalidOperationException("V10 real FBX missing curved fifth-finger nail submesh");
            smr.sharedMaterials = new[]{skin, sleeve, nail};
            smr.updateWhenOffscreen = true;
            // The imported FBX clips are authored relative to the model root.
            // Animator must sit ON that model root or its paths won't resolve.
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var rig = root.AddComponent<KeikoFPSArmRigController>();
            var bones = KeikoFPSArmRigController.ExpectedBoneNames
                .Select(n => FindDescendant(instance.transform, n)).ToArray();
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("_skinnedRenderer").objectReferenceValue = smr;
            serialized.FindProperty("_animator").objectReferenceValue = animator;
            serialized.FindProperty("_watchSocket").objectReferenceValue = bones[24];
            var arr = serialized.FindProperty("_bones");
            arr.arraySize = bones.Length;
            for (int i=0; i<bones.Length; ++i)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = bones[i];
            // Native imported 8-clip Animator drives the base pose;
            // LateUpdate adds only small hand/crown secondary effects.
            serialized.FindProperty("_poseDriverMode").enumValueIndex =
                (int)KeikoFPSArmRigController.PoseDriverMode.AnimatorBaseWithAdditiveLateUpdate;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            rig.skinMaterial = skin;
            rig.sleeveMaterial = sleeve;
            return root;
        }

        private static GameObject CreateWatch(GameObject source, Material titanium,
            Material strap, Material glass, Material oled)
        {
            var root = new GameObject("PF_ROKAS_Smartwatch_V10_Candidate");
            var model = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (model == null) throw new InvalidOperationException("Cannot instantiate 7-mesh watch FBX");
            model.transform.SetParent(root.transform, false);
            var watch = root.AddComponent<Smartwatch3DController>();
            var refs = new Dictionary<string,Transform>();
            foreach(var name in WatchNames)
            {
                var tr = FindDescendant(model.transform, name);
                if (tr == null) throw new InvalidOperationException("Missing imported watch mesh: " + name);
                refs.Add(name, tr);
                var renderer = tr.GetComponent<MeshRenderer>();
                if (renderer == null) throw new InvalidOperationException("Missing MeshRenderer on " + name);
                renderer.sharedMaterial = name.Contains("OLED") ? oled :
                    name.Contains("Glass") ? glass : name.Contains("Strap") ? strap : titanium;
            }
            var oledTr = refs["Watch_OLED_Screen"];
            var mf = oledTr.GetComponent<MeshFilter>();
            var collider = oledTr.GetComponent<MeshCollider>();
            if (collider == null) collider = oledTr.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mf.sharedMesh;
            collider.convex = false;
            var so = new SerializedObject(watch);
            so.FindProperty("_crownTransform").objectReferenceValue = refs["Watch_DigitalCrown_Rotary"];
            so.FindProperty("_sideButtonTransform").objectReferenceValue = refs["Watch_SideConfirmButton"];
            so.FindProperty("_oledRenderer").objectReferenceValue = oledTr.GetComponent<MeshRenderer>();
            so.FindProperty("_oledMeshCollider").objectReferenceValue = collider;
            so.ApplyModifiedPropertiesWithoutUndo();
            watch.titaniumMaterial = titanium;
            watch.strapMaterial = strap;
            watch.sapphireGlassMaterial = glass;
            watch.oledMaterial = oled;
            return root;
        }

        private static GameObject CreateComposite(GameObject arm, GameObject watch)
        {
            var root = new GameObject("PF_BedSleep_3DComposite_V10_Candidate");
            var comp = root.AddComponent<BedSleepVisualComposite>();
            var cObj = new GameObject("PresentationCamera_V9");
            cObj.transform.SetParent(root.transform, false);
            cObj.transform.localPosition = new Vector3(0,0,-0.55f);
            var cam = cObj.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 28f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.cullingMask = 1 << 30;
            var key = MakeLight(root.transform, "WarmPaperLanternKey", new Vector3(-0.4f,0.9f,-0.1f),
                new Color(1f,0.77f,0.49f), 1.6f);
            var rim = MakeLight(root.transform, "ColdWindowRim", new Vector3(0.7f,0.1f,0.5f),
                new Color(0.44f,0.75f,1.0f), .65f);
            var so = new SerializedObject(comp);
            so.FindProperty("_presentationCamera").objectReferenceValue = cam;
            so.FindProperty("_warmKeyLight").objectReferenceValue = key;
            so.FindProperty("_coolRimLight").objectReferenceValue = rim;
            so.FindProperty("_armPrefab").objectReferenceValue = arm;
            so.FindProperty("_watchPrefab").objectReferenceValue = watch;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static Light MakeLight(Transform root, string name, Vector3 p, Color color, float power)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = p;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = power;
            l.range = 3f;
            l.cullingMask = 1 << 30;
            return l;
        }

        private static GameObject Save(GameObject go, string n)
        {
            var path = Destination + n + ".prefab";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Refusing to overwrite existing candidate prefab: " + path);
            bool ok;
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path, out ok);
            if (!ok || saved == null) throw new InvalidOperationException("Unity failed to save real prefab: " + path);
            return saved;
        }

        private static Material RequireMaterial(string n, List<string> errors)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + n + ".mat");
            if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")
                errors.Add("Missing or invalid real material: " + n);
            return m;
        }

        private static Transform FindDescendant(Transform root, string exact)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == exact || t.name == exact + "_Mesh" ||
                    t.name.StartsWith(exact + ".", StringComparison.Ordinal)) return t;
            return null;
        }

        private static void EnsureDestination()
        {
            var parts = Destination.TrimEnd('/').Split('/');
            string parent = "Assets";
            for (int i=1; i<parts.Length; ++i)
            {
                var child = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = child;
            }
        }
    }
}
#endif
