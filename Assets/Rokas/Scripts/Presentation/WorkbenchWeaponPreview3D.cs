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
        public int CurrentTriangleCount => currentMesh ? currentMesh.triangles.Length / 3 : 0;
        public int ActiveRendererCount => rig ? rig.GetComponentsInChildren<MeshRenderer>(true).Length : 0;
        public int ActivePreviewCameraCount => rig ? rig.GetComponentsInChildren<Camera>(true).Length : 0;
        public bool HasCreatedRenderTexture => renderTexture && renderTexture.IsCreated();
        public int RenderTextureWidth => renderTexture ? renderTexture.width : 0;
        public int RenderTextureHeight => renderTexture ? renderTexture.height : 0;
        public string CurrentMeshName => currentMesh ? currentMesh.name : string.Empty;
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
            float scale = kind == WorkbenchWeaponKind.TwoHanded ? 1.92f : 1.86f;
            weaponObject.transform.localScale = Vector3.one * scale;
            weaponObject.transform.localPosition = Vector3.zero;
            weaponObject.transform.localRotation = Quaternion.identity;
            baseRotation = Quaternion.Euler(
                kind == WorkbenchWeaponKind.TwoHanded ? 2f : 5f,
                kind == WorkbenchWeaponKind.TwoHanded ? -22f : -24f,
                kind == WorkbenchWeaponKind.TwoHanded ? -38f : -34f);
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
            previewCamera.allowHDR = false;
            previewCamera.targetTexture = renderTexture;

            metalMaterial = CreateStandardMaterial("Workbench Preview Metal",
                new Color(.16f, .19f, .22f), .76f, .48f);
            gripMaterial = CreateStandardMaterial("Workbench Preview Grip",
                new Color(.055f, .038f, .030f), .12f, .28f);

            CreateLight("WorkbenchKey", new Color(.74f, .84f, .92f), .78f, new Vector3(30f, -34f, 0f));
            CreateLight("WorkbenchFill", new Color(.48f, .58f, .64f), .24f, new Vector3(-16f, 38f, 0f));
            CreateLight("WorkbenchRim", new Color(.12f, .56f, .86f), .56f, new Vector3(18f, 145f, 0f));
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
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
