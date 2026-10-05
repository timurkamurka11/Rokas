using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    internal sealed class WorkbenchWeaponMaterialSet
    {
        public Material Metal;
        public Material Grip;
        public Material Accent;
        public Texture2D MetallicGloss;
        public Texture2D NormalDetail;

        public bool HasSurfaceMaps => Metal &&
            Metal.GetTexture("_MetallicGlossMap") &&
            Metal.GetTexture("_BumpMap");

        public void Dispose()
        {
            WorkbenchWeaponVisualFactory.DestroyObject(Metal);
            WorkbenchWeaponVisualFactory.DestroyObject(Grip);
            WorkbenchWeaponVisualFactory.DestroyObject(Accent);
            WorkbenchWeaponVisualFactory.DestroyObject(MetallicGloss);
            WorkbenchWeaponVisualFactory.DestroyObject(NormalDetail);
            Metal = null;
            Grip = null;
            Accent = null;
            MetallicGloss = null;
            NormalDetail = null;
        }
    }

    internal static class WorkbenchWeaponVisualFactory
    {
        public const int PreviewLayer = 31;

        public static WorkbenchWeaponMaterialSet CreateMaterials()
        {
            var set = new WorkbenchWeaponMaterialSet
            {
                MetallicGloss = CreateMetallicGlossTexture(),
                NormalDetail = CreateBrushedNormalTexture()
            };

            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Legacy Shaders/Diffuse");

            set.Metal = new Material(shader)
            {
                name = "Workbench Premium Dark Steel",
                hideFlags = HideFlags.HideAndDontSave,
                color = new Color(.18f, .195f, .22f, 1f)
            };
            if (set.Metal.HasProperty("_Metallic")) set.Metal.SetFloat("_Metallic", .86f);
            if (set.Metal.HasProperty("_Glossiness")) set.Metal.SetFloat("_Glossiness", .54f);
            if (set.Metal.HasProperty("_GlossMapScale")) set.Metal.SetFloat("_GlossMapScale", .68f);
            if (set.Metal.HasProperty("_MetallicGlossMap"))
            {
                set.Metal.SetTexture("_MetallicGlossMap", set.MetallicGloss);
                set.Metal.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (set.Metal.HasProperty("_BumpMap"))
            {
                set.Metal.SetTexture("_BumpMap", set.NormalDetail);
                set.Metal.SetFloat("_BumpScale", .055f);
                set.Metal.EnableKeyword("_NORMALMAP");
            }
            if (set.Metal.HasProperty("_EmissionColor"))
            {
                set.Metal.EnableKeyword("_EMISSION");
                set.Metal.SetColor("_EmissionColor", new Color(.004f, .012f, .018f));
            }
            if (set.Metal.HasProperty("_SpecularHighlights")) set.Metal.SetFloat("_SpecularHighlights", 1f);
            if (set.Metal.HasProperty("_GlossyReflections")) set.Metal.SetFloat("_GlossyReflections", 1f);

            set.Grip = new Material(shader)
            {
                name = "Workbench Premium Grip",
                hideFlags = HideFlags.HideAndDontSave,
                color = new Color(.045f, .030f, .026f, 1f)
            };
            if (set.Grip.HasProperty("_Metallic")) set.Grip.SetFloat("_Metallic", .10f);
            if (set.Grip.HasProperty("_Glossiness")) set.Grip.SetFloat("_Glossiness", .34f);

            set.Accent = new Material(shader)
            {
                name = "Workbench Polished Accent Steel",
                hideFlags = HideFlags.HideAndDontSave,
                color = new Color(.30f, .335f, .38f, 1f)
            };
            if (set.Accent.HasProperty("_Metallic")) set.Accent.SetFloat("_Metallic", .92f);
            if (set.Accent.HasProperty("_Glossiness")) set.Accent.SetFloat("_Glossiness", .66f);
            if (set.Accent.HasProperty("_MetallicGlossMap"))
            {
                set.Accent.SetTexture("_MetallicGlossMap", set.MetallicGloss);
                set.Accent.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (set.Accent.HasProperty("_BumpMap"))
            {
                set.Accent.SetTexture("_BumpMap", set.NormalDetail);
                set.Accent.SetFloat("_BumpScale", .045f);
                set.Accent.EnableKeyword("_NORMALMAP");
            }
            return set;
        }

        public static void ConfigureRenderer(MeshRenderer renderer, WorkbenchWeaponKind kind,
            WorkbenchWeaponMaterialSet materials)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.sharedMaterials = new[] { materials.Metal, materials.Accent, materials.Grip };
        }

        public static void ApplyPose(Transform weapon, Mesh mesh, WorkbenchWeaponKind kind, bool icon)
        {
            Bounds bounds = mesh.bounds;
            float planarSpan = Mathf.Max(bounds.size.x, bounds.size.y);
            float targetSpan = icon
                ? (kind == WorkbenchWeaponKind.TwoHanded ? 1.56f : 1.54f)
                : (kind == WorkbenchWeaponKind.TwoHanded ? 1.82f : 1.86f);

            float scale = targetSpan / Mathf.Max(planarSpan, .0001f);
            weapon.localScale = Vector3.one * scale;
            weapon.localPosition = -bounds.center * scale;
            weapon.localRotation = Quaternion.identity;
        }

        public static Quaternion PresentationRotation(WorkbenchWeaponKind kind, bool icon)
        {
            if (kind == WorkbenchWeaponKind.TwoHanded)
                return Quaternion.Euler(icon ? 5f : 4f, icon ? -13f : -16f, icon ? -34f : -31f);
            return Quaternion.Euler(icon ? 7f : 6f, icon ? -18f : -20f, icon ? -32f : -33f);
        }

        public static void AddDirectionalLight(Transform root, string name, Color color, float intensity, Vector3 euler)
        {
            var lightObject = new GameObject(name);
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            lightObject.layer = PreviewLayer;
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localRotation = Quaternion.Euler(euler);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
        }

        public static void AddPointLight(Transform root, string name, Color color, float intensity, Vector3 localPosition)
        {
            var lightObject = new GameObject(name);
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            lightObject.layer = PreviewLayer;
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = localPosition;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 7f;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
        }

        public static void DestroyObject(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private static Texture2D CreateMetallicGlossTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "WorkbenchBrushedMetallicGloss",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float streak = .5f + .5f * Mathf.Sin(y * .92f + Mathf.Sin(x * .31f) * 1.7f);
                    float grain = .5f + .5f * Mathf.Sin(x * 2.13f + y * .17f);
                    float smoothness = Mathf.Clamp01(.50f + streak * .09f + grain * .015f);
                    pixels[y * size + x] = new Color(.86f, .04f, .02f, smoothness);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateBrushedNormalTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "WorkbenchBrushedNormal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wave = Mathf.Sin(y * .83f + Mathf.Sin(x * .13f) * 1.4f) * .018f;
                    Vector3 normal = new Vector3(wave, 0f, 1f).normalized;
                    pixels[y * size + x] = new Color(
                        normal.x * .5f + .5f,
                        normal.y * .5f + .5f,
                        normal.z * .5f + .5f,
                        1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }

    [AddComponentMenu("ROKAS/UI/Workbench Weapon Icon 3D")]
    public sealed class WorkbenchWeaponIcon3D : MonoBehaviour
    {
        private RawImage target;
        private RenderTexture renderTexture;
        private static readonly Color SelectedTint = Color.white;
        private static readonly Color UnselectedTint = new Color(.68f, .78f, .84f, .76f);

        public WorkbenchWeaponKind Kind { get; private set; }
        public int SourceVertexCount { get; private set; }
        public int SourceTriangleCount { get; private set; }
        public bool HasCreatedRenderTexture => renderTexture && renderTexture.IsCreated();
        public int RenderTextureWidth => renderTexture ? renderTexture.width : 0;
        public int RenderTextureHeight => renderTexture ? renderTexture.height : 0;
        public bool RenderedWithPremiumSurfaceMaps { get; private set; }
        public string CurrentSource => Kind == WorkbenchWeaponKind.TwoHanded
            ? WorkbenchWeaponMeshLibrary.SuppliedFbxName + " / premium heavy greatsword card render"
            : "ROKAS premium authored matching dagger / card render";

        public void Initialize(RawImage image, WorkbenchWeaponKind kind)
        {
            target = image ?? throw new ArgumentNullException(nameof(image));
            Kind = kind;

            renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "WorkbenchWeaponIconRT_" + kind,
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            renderTexture.Create();
            target.texture = renderTexture;
            target.color = SelectedTint;
            target.raycastTarget = false;
            RenderIcon();
        }

        public void SetSelected(bool selected)
        {
            if (target) target.color = selected ? SelectedTint : UnselectedTint;
        }

        private void RenderIcon()
        {
            Vector3 origin = Kind == WorkbenchWeaponKind.TwoHanded
                ? new Vector3(9400f, 9400f, 9400f)
                : new Vector3(9440f, 9400f, 9400f);

            var rig = new GameObject("WorkbenchWeaponIconRig_" + Kind);
            rig.hideFlags = HideFlags.HideAndDontSave;
            rig.transform.position = origin;

            Mesh mesh = WorkbenchWeaponMeshLibrary.Create(Kind);
            mesh.hideFlags = HideFlags.HideAndDontSave;
            SourceVertexCount = mesh.vertexCount;
            SourceTriangleCount = mesh.triangles.Length / 3;

            var materials = WorkbenchWeaponVisualFactory.CreateMaterials();
            RenderedWithPremiumSurfaceMaps = materials.HasSurfaceMaps;

            var pivotObject = new GameObject("IconPivot");
            pivotObject.hideFlags = HideFlags.HideAndDontSave;
            pivotObject.layer = WorkbenchWeaponVisualFactory.PreviewLayer;
            pivotObject.transform.SetParent(rig.transform, false);

            var weapon = new GameObject("IconWeapon_" + Kind);
            weapon.hideFlags = HideFlags.HideAndDontSave;
            weapon.layer = WorkbenchWeaponVisualFactory.PreviewLayer;
            weapon.transform.SetParent(pivotObject.transform, false);
            weapon.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = weapon.AddComponent<MeshRenderer>();
            WorkbenchWeaponVisualFactory.ConfigureRenderer(renderer, Kind, materials);
            WorkbenchWeaponVisualFactory.ApplyPose(weapon.transform, mesh, Kind, true);
            pivotObject.transform.localRotation = WorkbenchWeaponVisualFactory.PresentationRotation(Kind, true);

            var cameraObject = new GameObject("IconCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.layer = WorkbenchWeaponVisualFactory.PreviewLayer;
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -4f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = Kind == WorkbenchWeaponKind.TwoHanded ? 1.10f : 1.04f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.cullingMask = 1 << WorkbenchWeaponVisualFactory.PreviewLayer;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 10f;
            camera.allowHDR = false;
            camera.targetTexture = renderTexture;

            WorkbenchWeaponVisualFactory.AddPointLight(rig.transform, "IconKey",
                new Color(.84f, .90f, .96f), 3.8f, new Vector3(-1.7f, 1.3f, -2.1f));
            WorkbenchWeaponVisualFactory.AddPointLight(rig.transform, "IconFill",
                new Color(.35f, .47f, .58f), 1.2f, new Vector3(1.4f, -1.4f, -1.7f));
            WorkbenchWeaponVisualFactory.AddPointLight(rig.transform, "IconRim",
                new Color(.08f, .58f, 1f), 2.9f, new Vector3(1.8f, 1.6f, .4f));

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) camera.Render();

            camera.targetTexture = null;
            materials.Dispose();
            WorkbenchWeaponVisualFactory.DestroyObject(mesh);
            WorkbenchWeaponVisualFactory.DestroyObject(rig);
        }

        private void OnDestroy()
        {
            if (target) target.texture = null;
            if (!renderTexture) return;
            renderTexture.Release();
            WorkbenchWeaponVisualFactory.DestroyObject(renderTexture);
            renderTexture = null;
        }
    }
}
