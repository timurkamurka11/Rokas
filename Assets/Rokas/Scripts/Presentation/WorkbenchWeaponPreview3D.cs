using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public static class WorkbenchWeaponMeshLibrary
    {
        public const int SuppliedFbxOriginalVertexCount = 48858;
        public const int SuppliedFbxOriginalPolygonCount = 97712;
        public const string SuppliedFbxName = "metal+sword+3d+model.fbx";

        private const string TwoHandedMeshGzip = "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";
        private const string DaggerMeshGzip = "H4sIAFHewmoC/yWXW1TVZRrGn0k8gGSYYwcxw0ORKWFJUbnZtAuDAjdTFBZRwiiW0kkYO6hExaHSrMyEqSiLDGGKyiJLmIkiOpiz1syFN64uZi64nDUXY+svub2Y3/PlWvv3vM+7n/d7X1ku1vLvkl7k82/5zz93jqW1RGMHxtLm/2fPv8bShv47MnssbeyXoQVjadLE4rG0+EzXFbPdn5jjTFu6856NpzwbT3k2nvJsPOXZeMqz8ZRn4ynPxlOejac8S+Z4xcKa6YM/18+rmT795Pz5NdOlpYtqpv+a6bpqifvxZc5ULZm71P2aXGcmFjvvevBn9/1O36Tf6Zv0O32Tfqdv0u/0Tfqdvkm/43ruUvdrcp2ZWOy8a7/TN7lhRk3utinbptTkbpjRecbsOeWO2XnGnZ5T/rbnlJOdZzbMyK51PrvWebPnlDum89m1zptOdp6RanL9086ulUbTDy/PSMtIO7x8ND1KmYnIHTNKuZOI/G0icjJKjaYfz3f+eL7zZiJyx3T+eL7zppMRP/nDy73reL53tRZ4trXAs2Yicsf0bGuBZ00nvaun0PmeQufNROSO6XxPofOmk97VWuBdPYXeVRbzbFnMs2Yicsf0bFnMs6aT3tVQ7HxDsfNmInLHdL6h2HnTSe8qi3lXQ7F3ZZV4NqvEs2Yicsf0bFaJZ00nvSuv1Pm8UufNROSO6XxeqfOmk96VVeJdeaXedaLcsyfKPWsmIndMz54o96zppHdFSeejpPNmInLHdD5KOm866V0nyr0rSnpXb5Vne6s8ayYid0zP9lZ51nTSu0arnR+tdt7km2rnR6udH6123nTSu3qrvGu0WjqamV1bMbVianbt0cyh02bhSXfModPuFJ70t4UnnRw6fTRzsNH5wUbnzcKT7pjODzY6bzo5dPq3f+3SYKM09ss/Nrel/+/X4S1t6dKKrW3ps6a5js90f9a0HRvdn1jvjGvnWyLnWyLnWyLnWyLnXe/Y6P7Eemd+q3+ns9h1lqbAKeKXE5+pcKqmwWnilwefGXCG0pWhTM2EZ6MZmoVm6Bw0Q1lohmajGToXzdAc1O/PhJ7xpkzoF7zvbOh3vHUW9GvefQ70m74gC/pl3zEb+n1fcy70Ft80B3oXPwi2pIfNv9dcnQfP0/nwfF0AL9CF8ELNg/OUDbM1H87XRfAiLYALdLFytEgL4WI0R0vQHF2C5uhSNEe5aI4uQ3O0FM3R5WiOlqHeuxB63hcsgn7NdyyGftPXLIF+2TddAv2+L7sUeovvy4Xe5Ssvg97oW5dC7/XFl0Nv993LoG+4mP054e+xTMP6QH/RiD7VZ+gHgZ/hhvUhboR6CP0wcAg3rEHcCPXn6GDg57hhfYQboT6MfhR4GDesj3Ej1F+gHwd+gRvWJ7gR6i/RTwK/xA3rEG6E+gh6KPAIbpi7DtE/wm2HqM1PcX/V3/QV/EqjcFRfw6/1DfxGY3BM32pc3+s7+AM6rh/RcR1Fx/UTOq5jqN/5DjrnF7+HnvK7P0DP+vUfoV/wjqPQ73jTT9Cved8x6De/5b3xsP+YYsrTchWpQFejeYFX42K6AldEfQ16ReA1uJjycUXUhWh+YCEuphW4Iupr0RWB1+JiuhJXRH0demXgdbiYrsIVUV+PXhV4PS6mlbgi6lXoysBVuBh3raS/ittWUpsFuKSKFVelblYpWhxYikvqBlwldRl6Q2AZLqkErpL6FjQReAsuqRtxldS3ojcG3opL6iZcJXU5elNgOS6pElwldQVaEliBS2o1rpJ6Dbo6cA0uyV2r6a/httXU5s24Ot2mP6hed+lu9LbAu3F1uh1XT12D3h5Yg6tTFa6e+h60KvAeXJ3uwNVT16J3BNbi6nQnrp76XvTOwHtxdarG1VPfh1YH3oer01pcPfU6dG3gOlwdd62lv47b1lKbd+GatF5/VLM2qxFdH9iIa9IGXDP1g+iGwAdxTWrANVM/hDYEPoRr0kZcM/XD6MbAh3FNuh/XTP0Ien/gI7gmPYBrpn4UfSDwUVyTNuGaqbegmwK34Jq4axP9Ldy2idrcjGvXVv1JHdqhFnRrYAuuXY/hOqifQh8LfArXrsdxHdSt6OOBrbh2PYHroH4afSLwaVy7nsR1UD+DPhn4DK5d23Ad1M+i2wKfxbVrO66Dug3dHtiGa+eu7fTbuG07tbkD16Xn1KluvaSX0ecCX8Z16XlcN/Ur6POBr+C69AKum3oP+kLgHlyXduK6qV9Fdwa+iuvSLlw39V50V+BeXBf/o9hFf69eQ18MfA3Xpd24bup96O7Afbgu7tpNfx+37aY2X8L163X9WQN6R++irwe+i+vXG7gB6l70jcBeXL/exA1Qv4e+Gfgerl89uAHqA2hP4AFcv97CDVC/j74V+D6uX2/jBqj70LcD+3D92o8boD6I7g88iOvnrv30D3LbfmrzHdz/ASXIQfdcDQAA";

        public static Mesh Create(WorkbenchWeaponKind kind)
        {
            return kind == WorkbenchWeaponKind.Dagger ? DecodeDagger() : DecodeTwoHanded();
        }

        private static Mesh DecodeTwoHanded()
        {
            using var stream = Open(TwoHandedMeshGzip);
            using var reader = new BinaryReader(stream);
            int vertexCount = reader.ReadInt32();
            int faceCount = reader.ReadInt32();
            Vector3[] vertices = ReadVertices(reader, vertexCount);
            int[] triangles = ReadTriangles(reader, faceCount);
            var mesh = new Mesh { name = "WorkbenchTwoHanded_FbxDerived" };
            mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh DecodeDagger()
        {
            using var stream = Open(DaggerMeshGzip);
            using var reader = new BinaryReader(stream);
            int vertexCount = reader.ReadInt32();
            int metalFaceCount = reader.ReadInt32();
            int gripFaceCount = reader.ReadInt32();
            Vector3[] vertices = ReadVertices(reader, vertexCount);
            int[] metalTriangles = ReadTriangles(reader, metalFaceCount);
            int[] gripTriangles = ReadTriangles(reader, gripFaceCount);
            var mesh = new Mesh { name = "WorkbenchRitualDagger" };
            mesh.vertices = vertices;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(metalTriangles, 0);
            mesh.SetTriangles(gripTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static MemoryStream Open(string base64)
        {
            byte[] compressed = Convert.FromBase64String(base64);
            using var source = new MemoryStream(compressed, false);
            using var gzip = new GZipStream(source, CompressionMode.Decompress);
            var output = new MemoryStream();
            gzip.CopyTo(output);
            output.Position = 0;
            return output;
        }

        private static Vector3[] ReadVertices(BinaryReader reader, int count)
        {
            var vertices = new Vector3[count];
            for (int index = 0; index < count; index++)
            {
                float x = reader.ReadInt16() / 30000f;
                float y = reader.ReadInt16() / 30000f;
                float z = reader.ReadInt16() / 30000f;
                vertices[index] = new Vector3(x, y, z);
            }
            return vertices;
        }

        private static int[] ReadTriangles(BinaryReader reader, int faceCount)
        {
            int[] triangles = new int[faceCount * 3];
            for (int index = 0; index < triangles.Length; index++)
                triangles[index] = reader.ReadUInt16();
            return triangles;
        }
    }

    [AddComponentMenu("ROKAS/UI/Workbench Weapon Preview 3D")]
    public sealed class WorkbenchWeaponPreview3D : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        private static readonly Vector3 PreviewOrigin = new Vector3(9100f, 9100f, 9100f);

        private RawImage target;
        private RenderTexture renderTexture;
        private GameObject rig;
        private Transform pivot;
        private Camera previewCamera;
        private GameObject weaponObject;
        private Mesh currentMesh;
        private Material metalMaterial;
        private Material gripMaterial;
        private Quaternion baseRotation;
        private float idleTime;

        public WorkbenchWeaponKind CurrentKind { get; private set; }
        public bool HasModel => currentMesh != null && weaponObject;
        public int CurrentVertexCount => currentMesh ? currentMesh.vertexCount : 0;
        public string CurrentSource => CurrentKind == WorkbenchWeaponKind.TwoHanded
            ? WorkbenchWeaponMeshLibrary.SuppliedFbxName + " / geometry-derived preview"
            : "ROKAS authored ritual dagger mesh";

        public void Initialize(RawImage image, WorkbenchWeaponKind initial)
        {
            target = image ?? throw new ArgumentNullException(nameof(image));
            EnsureRig();
            SetWeapon(initial);
            RenderNow();
        }

        public void SetWeapon(WorkbenchWeaponKind kind)
        {
            EnsureRig();
            if (weaponObject) DestroyObject(weaponObject);
            if (currentMesh) DestroyObject(currentMesh);

            currentMesh = WorkbenchWeaponMeshLibrary.Create(kind);
            currentMesh.hideFlags = HideFlags.HideAndDontSave;
            weaponObject = new GameObject(kind == WorkbenchWeaponKind.TwoHanded
                ? "WorkbenchTwoHanded3D" : "WorkbenchDagger3D");
            weaponObject.hideFlags = HideFlags.HideAndDontSave;
            weaponObject.layer = PreviewLayer;
            weaponObject.transform.SetParent(pivot, false);

            var filter = weaponObject.AddComponent<MeshFilter>();
            filter.sharedMesh = currentMesh;
            var renderer = weaponObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sharedMaterials = kind == WorkbenchWeaponKind.Dagger
                ? new[] { metalMaterial, gripMaterial }
                : new[] { metalMaterial };

            CurrentKind = kind;
            float scale = kind == WorkbenchWeaponKind.TwoHanded ? 2.18f : 2.35f;
            weaponObject.transform.localScale = Vector3.one * scale;
            weaponObject.transform.localPosition = Vector3.zero;
            weaponObject.transform.localRotation = Quaternion.identity;
            baseRotation = Quaternion.Euler(
                kind == WorkbenchWeaponKind.TwoHanded ? 4f : 7f,
                kind == WorkbenchWeaponKind.TwoHanded ? -14f : -18f,
                kind == WorkbenchWeaponKind.TwoHanded ? -39f : -31f);
            pivot.localRotation = baseRotation;
            pivot.localPosition = Vector3.zero;
            idleTime = 0f;
            RenderNow();
        }

        public void RenderNow()
        {
            if (!previewCamera || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            previewCamera.Render();
        }

        private void LateUpdate()
        {
            if (!pivot || !weaponObject) return;
            idleTime += Time.unscaledDeltaTime;
            float yaw = Mathf.Sin(idleTime * .72f) * 5.5f;
            float pitch = Mathf.Cos(idleTime * .53f) * 1.6f;
            pivot.localRotation = baseRotation * Quaternion.Euler(pitch, yaw, 0);
            pivot.localPosition = new Vector3(0, Mathf.Sin(idleTime * .9f) * .035f, 0);
            RenderNow();
        }

        private void EnsureRig()
        {
            if (rig) return;

            renderTexture = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32)
            {
                name = "WorkbenchWeaponPreviewRT",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            renderTexture.Create();
            target.texture = renderTexture;
            target.color = Color.white;
            target.raycastTarget = false;

            rig = new GameObject("WorkbenchWeaponPreviewRig");
            rig.hideFlags = HideFlags.HideAndDontSave;
            rig.transform.position = PreviewOrigin;

            var pivotObject = new GameObject("WeaponPivot");
            pivotObject.hideFlags = HideFlags.HideAndDontSave;
            pivotObject.layer = PreviewLayer;
            pivotObject.transform.SetParent(rig.transform, false);
            pivot = pivotObject.transform;

            var cameraObject = new GameObject("PreviewCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.layer = PreviewLayer;
            cameraObject.transform.SetParent(rig.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -5.9f);
            cameraObject.transform.localRotation = Quaternion.identity;
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0, 0, 0, 0);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.fieldOfView = 27f;
            previewCamera.nearClipPlane = .1f;
            previewCamera.farClipPlane = 20f;
            previewCamera.allowHDR = true;
            previewCamera.targetTexture = renderTexture;

            metalMaterial = CreateStandardMaterial("Workbench Preview Metal",
                new Color(.29f, .33f, .36f), .84f, .58f);
            gripMaterial = CreateStandardMaterial("Workbench Preview Grip",
                new Color(.075f, .055f, .045f), .18f, .34f);

            CreateLight("WorkbenchKey", new Color(.76f, .90f, 1f), 1.55f, new Vector3(32f, -28f, 0f));
            CreateLight("WorkbenchFill", new Color(.84f, .88f, .90f), .72f, new Vector3(-18f, 34f, 0f));
            CreateLight("WorkbenchRim", new Color(.18f, .72f, 1f), 1.05f, new Vector3(18f, 145f, 0f));
        }

        private Material CreateStandardMaterial(string materialName, Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Legacy Shaders/Diffuse");
            var material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
                color = color
            };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(.008f, .025f, .035f));
            }
            return material;
        }

        private void CreateLight(string lightName, Color color, float intensity, Vector3 euler)
        {
            var lightObject = new GameObject(lightName);
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            lightObject.layer = PreviewLayer;
            lightObject.transform.SetParent(rig.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(euler);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            if (target) target.texture = null;
            if (weaponObject) DestroyObject(weaponObject);
            if (currentMesh) DestroyObject(currentMesh);
            if (rig) DestroyObject(rig);
            if (metalMaterial) DestroyObject(metalMaterial);
            if (gripMaterial) DestroyObject(gripMaterial);
            if (renderTexture)
            {
                renderTexture.Release();
                DestroyObject(renderTexture);
            }
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
