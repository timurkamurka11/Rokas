using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Presentation
{
    internal static class WorkbenchPremiumSwordMesh
    {
        private const int BladeMaterial = 0;
        private const int AccentMaterial = 1;
        private const int GripMaterial = 2;

        public static Mesh Create()
        {
            var vertices = new List<Vector3>(1400);
            var blade = new List<int>(1600);
            var accent = new List<int>(1800);
            var grip = new List<int>(900);

            // Narrow hero-sword silhouette inspired by the supplied FBX/reference,
            // rebuilt specifically for a crisp Workbench showcase.
            Vector2[] bladeOutline =
            {
                new Vector2(0f, -1.10f),
                new Vector2(-.105f, -.84f),
                new Vector2(-.142f, .44f),
                new Vector2(-.118f, .61f),
                new Vector2(-.072f, .72f),
                new Vector2(.072f, .72f),
                new Vector2(.118f, .61f),
                new Vector2(.142f, .44f),
                new Vector2(.105f, -.84f)
            };
            AddBeveledPrism(vertices, blade, bladeOutline, .095f, .82f);

            // Raised central ridge/fullers catch the key light without turning the blade into neon.
            AddRaisedDiamond(vertices, accent, -.88f, .61f, .021f, -.052f);
            AddRaisedDiamond(vertices, accent, -.88f, .61f, .021f, .052f);

            // Crossguard: two tapered arms, angled slightly toward the blade.
            AddBox(vertices, accent, new Vector3(-.185f, .765f, 0f),
                new Vector3(.39f, .072f, .135f), Quaternion.Euler(0f, 0f, -10f));
            AddBox(vertices, accent, new Vector3(.185f, .765f, 0f),
                new Vector3(.39f, .072f, .135f), Quaternion.Euler(0f, 0f, 10f));
            AddBox(vertices, accent, new Vector3(0f, .765f, 0f),
                new Vector3(.16f, .115f, .15f), Quaternion.identity);

            // Long two-handed leather grip.
            AddCylinderY(vertices, grip, new Vector3(0f, 1.045f, 0f), .060f, .46f, 10, 0f);
            for (int i = 0; i < 7; i++)
            {
                float y = .845f + i * .061f;
                AddCylinderY(vertices, accent, new Vector3(0f, y, 0f), .0645f, .016f, 10, i % 2 == 0 ? 8f : -8f);
            }

            // Collar and pommel give the hilt a clear premium silhouette at card-icon scale.
            AddCylinderY(vertices, accent, new Vector3(0f, .825f, 0f), .078f, .045f, 10, 0f);
            AddCylinderY(vertices, accent, new Vector3(0f, 1.285f, 0f), .076f, .055f, 10, 0f);
            AddBox(vertices, accent, new Vector3(0f, 1.355f, 0f),
                new Vector3(.145f, .13f, .14f), Quaternion.Euler(0f, 0f, 45f));

            var mesh = new Mesh
            {
                name = "WorkbenchTwoHanded_PremiumAuthored"
            };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(blade, BladeMaterial, true);
            mesh.SetTriangles(accent, AccentMaterial, true);
            mesh.SetTriangles(grip, GripMaterial, true);
            mesh.RecalculateBounds();

            Bounds bounds = mesh.bounds;
            float width = Mathf.Max(bounds.size.x, .0001f);
            float height = Mathf.Max(bounds.size.y, .0001f);
            var uv = new Vector2[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                uv[i] = new Vector2(
                    (vertices[i].x - bounds.min.x) / width,
                    (vertices[i].y - bounds.min.y) / height);
            }
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBeveledPrism(List<Vector3> vertices, List<int> triangles,
            Vector2[] outline, float depth, float insetScale)
        {
            Vector2 center = Vector2.zero;
            for (int i = 0; i < outline.Length; i++) center += outline[i];
            center /= outline.Length;

            var inner = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++)
                inner[i] = Vector2.Lerp(center, outline[i], insetScale);

            float faceZ = depth * .5f;
            float edgeZ = depth * .27f;

            // Front and back inset faces.
            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                AddTriangle(vertices, triangles,
                    new Vector3(center.x, center.y, -faceZ),
                    new Vector3(inner[next].x, inner[next].y, -faceZ),
                    new Vector3(inner[i].x, inner[i].y, -faceZ));
                AddTriangle(vertices, triangles,
                    new Vector3(center.x, center.y, faceZ),
                    new Vector3(inner[i].x, inner[i].y, faceZ),
                    new Vector3(inner[next].x, inner[next].y, faceZ));
            }

            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                Vector3 frontInnerA = new Vector3(inner[i].x, inner[i].y, -faceZ);
                Vector3 frontInnerB = new Vector3(inner[next].x, inner[next].y, -faceZ);
                Vector3 frontOuterA = new Vector3(outline[i].x, outline[i].y, -edgeZ);
                Vector3 frontOuterB = new Vector3(outline[next].x, outline[next].y, -edgeZ);
                Vector3 backOuterA = new Vector3(outline[i].x, outline[i].y, edgeZ);
                Vector3 backOuterB = new Vector3(outline[next].x, outline[next].y, edgeZ);
                Vector3 backInnerA = new Vector3(inner[i].x, inner[i].y, faceZ);
                Vector3 backInnerB = new Vector3(inner[next].x, inner[next].y, faceZ);

                AddQuad(vertices, triangles, frontInnerA, frontInnerB, frontOuterB, frontOuterA);
                AddQuad(vertices, triangles, frontOuterA, frontOuterB, backOuterB, backOuterA);
                AddQuad(vertices, triangles, backOuterA, backOuterB, backInnerB, backInnerA);
            }
        }

        private static void AddRaisedDiamond(List<Vector3> vertices, List<int> triangles,
            float minY, float maxY, float halfWidth, float z)
        {
            Vector3 a = new Vector3(0f, minY, z);
            Vector3 b = new Vector3(-halfWidth, maxY - .08f, z);
            Vector3 c = new Vector3(0f, maxY, z);
            Vector3 d = new Vector3(halfWidth, maxY - .08f, z);
            if (z < 0f)
            {
                AddTriangle(vertices, triangles, a, c, b);
                AddTriangle(vertices, triangles, a, d, c);
            }
            else
            {
                AddTriangle(vertices, triangles, a, b, c);
                AddTriangle(vertices, triangles, a, c, d);
            }
        }

        private static void AddBox(List<Vector3> vertices, List<int> triangles,
            Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * .5f;
            Vector3[] local =
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z),
                new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
            };
            for (int i = 0; i < local.Length; i++) local[i] = center + rotation * local[i];

            AddQuad(vertices, triangles, local[0], local[1], local[2], local[3]);
            AddQuad(vertices, triangles, local[5], local[4], local[7], local[6]);
            AddQuad(vertices, triangles, local[4], local[0], local[3], local[7]);
            AddQuad(vertices, triangles, local[1], local[5], local[6], local[2]);
            AddQuad(vertices, triangles, local[3], local[2], local[6], local[7]);
            AddQuad(vertices, triangles, local[4], local[5], local[1], local[0]);
        }

        private static void AddCylinderY(List<Vector3> vertices, List<int> triangles,
            Vector3 center, float radius, float height, int sides, float twistDegrees)
        {
            float half = height * .5f;
            float twist = twistDegrees * Mathf.Deg2Rad;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides + twist;
                float a1 = (i + 1) * Mathf.PI * 2f / sides + twist;
                Vector3 b0 = center + new Vector3(Mathf.Cos(a0) * radius, -half, Mathf.Sin(a0) * radius);
                Vector3 b1 = center + new Vector3(Mathf.Cos(a1) * radius, -half, Mathf.Sin(a1) * radius);
                Vector3 t0 = center + new Vector3(Mathf.Cos(a0) * radius, half, Mathf.Sin(a0) * radius);
                Vector3 t1 = center + new Vector3(Mathf.Cos(a1) * radius, half, Mathf.Sin(a1) * radius);
                AddQuad(vertices, triangles, b0, b1, t1, t0);
                AddTriangle(vertices, triangles, center + Vector3.down * half, b1, b0);
                AddTriangle(vertices, triangles, center + Vector3.up * half, t0, t1);
            }
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }
    }
}
