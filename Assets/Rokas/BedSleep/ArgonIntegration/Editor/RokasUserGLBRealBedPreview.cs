#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
            var watch = all.FirstOrDefault(r =>
                r.name == "Watch_OLED_Display" && r.enabled);
            var watchParts = all.Where(r =>
                r.enabled && r.name.StartsWith("Watch_", StringComparison.Ordinal)).ToArray();
            var oldWrist = game.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Arm_Wrist");
            var oldForearm = game.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Arm_ForearmRoot");
            if (oldSkin == null || oldSleeve == null || oldNails == null ||
                watch == null || watchParts.Length < 6 ||
                oldWrist == null || oldForearm == null)
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
                var digits = instance.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.EndsWith("_proximal", StringComparison.Ordinal))
                    .ToArray();
                if (skin == null || wrist == null || forearm == null ||
                    digits.Length < 5 || skin.bones == null || skin.bones.Length < 20)
                    throw new InvalidOperationException(
                        "Imported user-GLB does not have the authentic hand skin + 5 finger chains.");

                float gameLength = Vector3.Distance(oldWrist.position, oldForearm.position);
                float candidateLength = Vector3.Distance(wrist.position, forearm.position);
                if (gameLength <= .001f || candidateLength <= .001f)
                    throw new InvalidOperationException("Cannot calibrate new native hand forearm.");
                float scale = gameLength / candidateLength;
                if (scale < .08f || scale > 6f)
                    throw new InvalidOperationException("Unsafe wrist scale ratio: " + scale);

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
                var follower = instance.AddComponent<RokasUserGLBRealBedFollower>();
                follower.Initialize(instance.transform, wrist, oldWrist,
                    animation, scale, oldVisuals, visibility, skin, temporarySkin);
                Debug.Log("[ROKAS NEW GLB REAL BED] Imported user's 23-bone rig and " +
                    animation.name + "; old V16 visual hand/sleeve/nails hidden " +
                    "ONLY IN PLAY MODE. Real ROKAS watch+strap and Russian OLED kept. " +
                    "Scale=" + scale.ToString("F4") + " (visual acceptance pending)");
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
    /// Editor-Play-Mode-only native clip driver. Follow the physical OLED wrist
    /// rather than creating a second, disconnected clock. Do not save a prefab.
    /// </summary>
    [DefaultExecutionOrder(32010)]
    public sealed class RokasUserGLBRealBedFollower : MonoBehaviour
    {
        private Transform candidateRoot, candidateWrist, existingWrist;
        private AnimationClip clip;
        private float calibratedScale, startTime;
        private Renderer[] oldVisuals;
        private bool[] oldVisibility;
        private Renderer newSkin;
        private Material ownedMaterial;
        private bool ready, oldHidden;
        public void Initialize(Transform root, Transform wrist, Transform referenceWrist,
            AnimationClip importedClip, float nativeScale,
            Renderer[] oldRenderers, bool[] previousVisibility,
            Renderer visibleSkin, Material temporaryMaterial)
        {
            if (root == null || wrist == null || referenceWrist == null ||
                importedClip == null || visibleSkin == null ||
                oldRenderers == null || previousVisibility == null ||
                oldRenderers.Length != previousVisibility.Length)
                throw new InvalidOperationException("Invalid actual-bed new hand initialization");
            candidateRoot = root;
            candidateWrist = wrist;
            existingWrist = referenceWrist;
            clip = importedClip;
            calibratedScale = nativeScale;
            oldVisuals = oldRenderers;
            oldVisibility = previousVisibility;
            newSkin = visibleSkin;
            ownedMaterial = temporaryMaterial;
            startTime = Time.unscaledTime;
            ready = true;
            // Disable old skin only once the imported clip and correct renderer
            // have been resolved. Never remove any Watch_* geometry/colliders.
            AlignAndSample(0f);
            for (int i = 0; i < oldVisuals.Length; i++)
                if (oldVisuals[i] != null) oldVisuals[i].enabled = false;
            oldHidden = true;
        }

        private void LateUpdate()
        {
            if (!ready) return;
            if (existingWrist == null || candidateWrist == null ||
                candidateRoot == null || newSkin == null)
            {
                Restore();
                ready = false;
                return;
            }
            try
            {
                float t = Mathf.Repeat(Time.unscaledTime - startTime,
                    Mathf.Max(.1f, clip.length));
                AlignAndSample(t);
            }
            catch (Exception e)
            {
                Debug.LogError("[ROKAS NEW GLB] Candidate failed; restoring V16: " + e);
                Restore();
                ready = false;
                gameObject.SetActive(false);
            }
        }

        private void AlignAndSample(float t)
        {
            candidateRoot.localPosition = Vector3.zero;
            candidateRoot.localRotation = Quaternion.identity;
            candidateRoot.localScale = Vector3.one * calibratedScale;
            clip.SampleAnimation(candidateRoot.gameObject, t);
            Quaternion align = existingWrist.rotation *
                Quaternion.Inverse(candidateWrist.rotation);
            candidateRoot.rotation = align * candidateRoot.rotation;
            candidateRoot.position += existingWrist.position - candidateWrist.position;
            newSkin.enabled = true;
        }

        private void Restore()
        {
            if (!oldHidden) return;
            for (int i = 0; i < oldVisuals.Length; ++i)
                if (oldVisuals[i] != null) oldVisuals[i].enabled = oldVisibility[i];
            oldHidden = false;
        }

        private void OnDisable() { Restore(); }
        private void OnDestroy()
        {
            Restore();
            if (ownedMaterial != null) UnityEngine.Object.Destroy(ownedMaterial);
        }
    }
}
#endif
