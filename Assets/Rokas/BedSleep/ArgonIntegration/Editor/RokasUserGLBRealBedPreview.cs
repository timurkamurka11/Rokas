#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Reversible Play Mode preview in the ACTUAL Hub -> Bed sequence.
    /// Native user-supplied, already-rigged GLB is converted to an FBX by Blender.
    /// The old ROKAS bed scene, prefab, watch casing, strap, OLED, controls and
    /// V16 runtime code are never serialized or changed.
    /// </summary>
    [InitializeOnLoad]
    public static class RokasUserGLBRealBedPreview
    {
        public const string EnabledKey = "ROKAS.NewUserGLB.RealBedPreview.Enabled";
        private const string AssetPath =
            "Assets/Rokas/BedSleep/ArgonIntegration/NewUserGLB/Imported/ROKAS_NewHand_Rigged.fbx";
        private const string AlbedoPath =
            "Assets/Rokas/BedSleep/ArgonIntegration/NewUserGLB/Imported/ROKAS_NewHand_Albedo.jpg";
        private const string ActorName = "ROKAS_Argon_Complete_Hand_Watch_InGame";
        private static readonly HashSet<int> Installed = new HashSet<int>();
        private static readonly HashSet<int> Rejected = new HashSet<int>();

        static RokasUserGLBRealBedPreview()
        {
            // User's newly uploaded animated hand takes precedence over old
            // procedural V17/V22. This editor preference never changes a
            // runtime prefab, scene or any source file.
            EditorPrefs.SetBool("ROKAS.V17.RealBedPreview.Enabled", false);
            // Create the persistent profile in EDIT mode. Creating/importing an
            // .asset during live Play Mode would trigger an unwanted reload.
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    RokasUserHandStudioWindow.LoadOrCreateProfile();
            };
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    Installed.Clear();
                    Rejected.Clear();
                }
            };
        }

        [MenuItem("ROKAS/Bed Sleep/New GLB Hand/Enable new hand in REAL bed")]
        private static void Enable()
        {
            EditorPrefs.SetBool(EnabledKey, true);
            EditorPrefs.SetBool("ROKAS.V17.RealBedPreview.Enabled", false);
            Debug.Log("[ROKAS NEW GLB] Enabled in ordinary Hub/Bed Play Mode. V16 source unchanged.");
        }

        [MenuItem("ROKAS/Bed Sleep/New GLB Hand/Disable preview (V16 fallback)")]
        private static void Disable()
        {
            EditorPrefs.SetBool(EnabledKey, false);
            Debug.Log("[ROKAS NEW GLB] Disabled. Stop and restart Play Mode for original hand.");
        }

        [MenuItem("ROKAS/Bed Sleep/New GLB Hand/Enable new hand in REAL bed", true)]
        private static bool EnableChecked()
        {
            Menu.SetChecked("ROKAS/Bed Sleep/New GLB Hand/Enable new hand in REAL bed",
                EditorPrefs.GetBool(EnabledKey, true));
            return true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || !EditorPrefs.GetBool(EnabledKey, true))
                return;
            GameObject game = GameObject.Find(ActorName);
            if (game == null) return; // real player has not clicked the bed yet
            int id = game.GetInstanceID();
            if (Installed.Contains(id) || Rejected.Contains(id))
                return;
            try
            {
                Install(game);
                Installed.Add(id);
            }
            catch (InvalidOperationException notYet)
            {
                // The old actor is created before its watch materials / OLED
                // finish initializing. Retry on the next Editor tick instead
                // of permanently rejecting a healthy first Bed click.
                if (notYet.Message.Contains("not ready"))
                    return;
                Rejected.Add(id);
                Debug.LogError("[ROKAS NEW GLB] Candidate NOT installed: " +
                    notYet + ". Existing V16 skin and watch stay visible.");
            }
            catch (Exception error)
            {
                Rejected.Add(id);
                Debug.LogError("[ROKAS NEW GLB] Candidate NOT installed: " +
                    error + ". Existing V16 skin and watch stay visible.");
            }
        }

        private static void Install(GameObject game)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            if (model == null)
                throw new InvalidOperationException(
                    "Converted native GLB FBX is missing at " + AssetPath +
                    ". Run the single-BAT Blender conversion first.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(AssetPath)
                .OfType<AnimationClip>()
                .Where(c => c != null && c.length > 1f &&
                    !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderByDescending(c => c.length)
                .ToArray();
            AnimationClip animation = clips.FirstOrDefault(c =>
                AnimationUtility.GetCurveBindings(c).Any(b =>
                    b.type == typeof(Transform) &&
                    (b.path.Contains("index_proximal") ||
                     b.path.Contains("middle_proximal")) &&
                    b.propertyName.IndexOf("Rotation", StringComparison.OrdinalIgnoreCase) >= 0));
            if (animation == null)
                throw new InvalidOperationException(
                    "Real user-GLB animation was not imported with finger rotation curves.");

            var all = game.GetComponentsInChildren<Renderer>(true);
            var oldSkin = all.OfType<SkinnedMeshRenderer>()
                .FirstOrDefault(r => r.name == "Keiko_Hand_Skin_Mesh" && r.enabled);
            var oldSleeve = all.FirstOrDefault(r =>
                r.name == "Keiko_Homewear_Sleeve_Mesh" && r.enabled);
            var oldNails = all.FirstOrDefault(r =>
                r.name == "Keiko_Fingernails_Mesh" && r.enabled);
            var watch = all.OfType<MeshRenderer>().FirstOrDefault(r =>
                r.name == "Watch_OLED_Display" && r.enabled);
            var watchParts = all.Where(r =>
                r.enabled && r.name.StartsWith("Watch_", StringComparison.Ordinal)).ToArray();
            // Move the entire ORIGINAL smartwatch tree as one unit,
            // never just the display or a few watch parts.
            var physicalWatchRoot = game.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "ROKAS_Smartwatch_HighDetail");
            if (physicalWatchRoot != null && watchParts.Any(r =>
                !r.transform.IsChildOf(physicalWatchRoot)))
                throw new InvalidOperationException(
                    "Original seven-part ROKAS watch has an unexpected hierarchy; " +
                    "cannot safely move only part of physical watch.");
            var oldWrist = game.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Arm_Wrist");
            var originalBones = game.GetComponentsInChildren<Transform>(true);
            var oldForearm = originalBones.FirstOrDefault(t => t.name == "Arm_ForearmRoot");
            var oldMiddleTip = originalBones.FirstOrDefault(t => t.name == "Middle_Tip");
            var oldIndexRoot = originalBones.FirstOrDefault(t => t.name == "Index_MCP");
            var oldLittleRoot = originalBones.FirstOrDefault(t => t.name == "Little_MCP");
            if (oldSkin == null || oldSleeve == null || oldNails == null ||
                watch == null || watchParts.Length < 6 ||
                physicalWatchRoot == null ||
                oldWrist == null || oldForearm == null || oldMiddleTip == null ||
                oldIndexRoot == null || oldLittleRoot == null)
                throw new InvalidOperationException(
                    "Existing original bed actor, wrist or physical watch/OLED not ready. " +
                    "Do not hide the playable old hand.");

            GameObject instance = null;
            Material temporarySkin = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(model, game.transform);
                instance.name = "ROKAS_NEW_USER_GLB_RealBed_PlayOnly";
                var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(r => r.sharedMesh != null &&
                        r.sharedMesh.vertexCount > 30000);
                var wrist = instance.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "wrist");
                var forearm = instance.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "forearm");
                var nativeBones = instance.GetComponentsInChildren<Transform>(true);
                var digits = nativeBones.Where(t =>
                    t.name.EndsWith("_proximal", StringComparison.Ordinal)).ToArray();
                var nativeMiddleDistal = nativeBones.FirstOrDefault(t =>
                    t.name == "middle_distal");
                var nativeMiddleMid = nativeBones.FirstOrDefault(t =>
                    t.name == "middle_middle");
                if (skin == null || wrist == null || forearm == null ||
                    nativeMiddleDistal == null || nativeMiddleMid == null ||
                    digits.Length < 5 || skin.bones == null || skin.bones.Length < 20)
                    throw new InvalidOperationException(
                        "Imported user-GLB does not have the authentic hand skin + 5 finger chains.");

                // Prior calibration used forearm bone length, but user GLB
                // and V16 have different forearm-to-hand proportions: it
                // oversized the new hand and filled the entire camera.
                // Match actual wrist-to-middle-fingertip span instead.
                float originalReach = Vector3.Distance(
                    oldWrist.position, oldMiddleTip.position);
                float nativeReach = Vector3.Distance(
                    wrist.position, nativeMiddleDistal.position) +
                    .75f * Vector3.Distance(nativeMiddleDistal.position,
                                            nativeMiddleMid.position);
                if (originalReach <= .001f || nativeReach <= .001f)
                    throw new InvalidOperationException("Cannot match the two hand reaches");
                float scale = originalReach / nativeReach;
                if (scale < .04f || scale > 3f)
                    throw new InvalidOperationException(
                        "Unsafe authentic user-GLB wrist-to-finger scale " + scale);

                var image = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
                if (image == null)
                    throw new InvalidOperationException(
                        "Exact user-GLB embedded skin/sleeve albedo is missing.");
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Unity PBR shader unavailable");
                temporarySkin = new Material(shader);
                temporarySkin.name = "ROKAS_New_UserGLB_OriginalTexture_PlayOnly";
                if (temporarySkin.HasProperty("_BaseMap"))
                    temporarySkin.SetTexture("_BaseMap", image);
                if (temporarySkin.HasProperty("_MainTex"))
                    temporarySkin.SetTexture("_MainTex", image);
                if (temporarySkin.HasProperty("_BaseColor"))
                    temporarySkin.SetColor("_BaseColor", Color.white);
                if (temporarySkin.HasProperty("_Color"))
                    temporarySkin.SetColor("_Color", Color.white);
                if (temporarySkin.HasProperty("_Metallic"))
                    temporarySkin.SetFloat("_Metallic", 0f);
                if (temporarySkin.HasProperty("_Smoothness"))
                    temporarySkin.SetFloat("_Smoothness", .35f);

                foreach (Animator a in instance.GetComponentsInChildren<Animator>(true))
                    a.enabled = false;
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                    r.enabled = (r == skin);
                skin.sharedMaterial = temporarySkin;
                skin.updateWhenOffscreen = true;
                skin.gameObject.layer = oldSkin.gameObject.layer;

                Renderer[] oldVisuals = all.Where(r =>
                    r == oldSkin || r == oldSleeve || r == oldNails ||
                    r.name == "ROKAS_Subtle_DorsalHandVeins").ToArray();
                // No Watch_* renderer is ever added to hidden list.
                bool[] visibility = oldVisuals.Select(r => r.enabled).ToArray();
                var calibration = RokasUserHandStudioWindow.LoadOrCreateProfile();
                var bedCamera = Camera.allCameras.Where(c =>
                    c != null && c.enabled &&
                    (c.cullingMask & (1 << oldSkin.gameObject.layer)) != 0)
                    .OrderByDescending(c =>
                        c.cullingMask == (1 << oldSkin.gameObject.layer) ? 1 : 0)
                    .FirstOrDefault() ?? Camera.main;
                if (bedCamera == null)
                    throw new InvalidOperationException(
                        "Bed presentation camera not ready. V16 remains active.");
                var follower = instance.AddComponent<RokasUserGLBRealBedFollower>();
                follower.Initialize(instance.transform, wrist, oldWrist,
                    oldMiddleTip, oldIndexRoot, oldLittleRoot,
                    animation, scale, oldVisuals, visibility, skin, temporarySkin,
                    physicalWatchRoot, watch, calibration, bedCamera);
                Debug.Log("[ROKAS NEW GLB REAL BED] Imported user's 23-bone rig and " +
                    animation.name + "; old V16 visual hand/sleeve/nails hidden " +
                    "ONLY IN PLAY MODE. Real ROKAS watch+strap and Russian OLED kept. " +
                    "Original wrist-to-finger scale=" + scale.ToString("F4") +
                    "; open ROKAS > Bed Sleep > Hand Studio to adjust cuff, watch, " +
                    "OLED brightness and micro-movement live. Game View QA required.");
                temporarySkin = null; // follower owns disposable Play Mode material
            }
            catch
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (temporarySkin != null) UnityEngine.Object.DestroyImmediate(temporarySkin);
                throw;
            }
        }
    }


    /// <summary>
    /// Imports just the converted, user-supplied GLB FBX as a Generic rig with
    /// its authored finger animation. Does not modify any V16 or V17 model.
    /// </summary>
    public sealed class RokasUserGLBModelImportSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (assetPath !=
                "Assets/Rokas/BedSleep/ArgonIntegration/NewUserGLB/Imported/ROKAS_NewHand_Rigged.fbx")
                return;
            var importer = assetImporter as ModelImporter;
            if (importer == null) return;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
#endif
