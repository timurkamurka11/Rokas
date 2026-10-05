using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Presentation
{
    internal static class WorkbenchPremiumDaggerMesh
    {
        private const int BladeMaterial = 0;
        private const int AccentMaterial = 1;
        private const int GripMaterial = 2;

        public static Mesh Create()
        {
            var vertices = new List<Vector3>(1100);
            var blade = new List<int>(1500);
            var accent = new List<int>(1700);
            var grip = new List<int>(800);

            Vector2[] bladeOutline =
            {
                new Vector2(0f, -.62f),
                new Vector2(-.11f, -.50f),
                new Vector2(-.18f, -.25f),
                new Vector2(-.20f, .02f),
                new Vector2(-.17f, .22f),
                new Vector2(-.11f, .34f),
                new Vector2(-.06f, .39f),
                new Vector2(.06f, .39f),
                new Vector2(.11f, .34f),
                new Vector2(.17f, .22f),
                new Vector2(.20f, .02f),
                new Vector2(.18f, -.25f),
                new Vector2(.11f, -.50f)
            };
            AddBeveledPrism(vertices, blade, bladeOutline, .13f, .76f);
            AddRaisedDiamond(vertices, accent, -.49f, .30f, .036f, -.070f);
            AddRaisedDiamond(vertices, accent, -.49f, .30f, .036f, .070f);

            // Compact down-swept quillons distinguish the dagger from the greatsword
            // while keeping the same angular ritual-metal language.
            AddBox(vertices, accent, new Vector3(0f, .425f, 0f),
                new Vector3(.16f, .105f, .15f), Quaternion.identity);
            AddBox(vertices, accent, new Vector3(-.17f, .455f, 0f),
                new Vector3(.35f, .060f, .135f), Quaternion.Euler(0f, 0f, -18f));
            AddBox(vertices, accent, new Vector3(.17f, .455f, 0f),
                new Vector3(.35f, .060f, .135f), Quaternion.Euler(0f, 0f, 18f));

            AddCylinderY(vertices, grip, new Vector3(0f, .635f, 0f), .058f, .30f, 12, 0f);
            for (int i = 0; i < 5; i++)
            {
                float y = .515f + i * .052f;
                AddCylinderY(vertices, accent, new Vector3(0f, y, 0f), .063f, .014f, 12,
                    i % 2 == 0 ? 7f : -7f);
            }

            AddCylinderY(vertices, accent, new Vector3(0f, .485f, 0f), .072f, .040f, 12, 0f);
            AddCylinderY(vertices, accent, new Vector3(0f, .790f, 0f), .070f, .045f, 12, 0f);
            AddBox(vertices, accent, new Vector3(0f, .850f, 0f),
                new Vector3(.135f, .115f, .13f), Quaternion.Euler(0f, 0f, 45f));

            var mesh = new Mesh { name = "WorkbenchDagger_PremiumRitualSet" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(blade, BladeMaterial, true);
            mesh.SetTriangles(accent, AccentMaterial, true);
            mesh.SetTriangles(grip, GripMaterial, true);
            FinalizeMesh(mesh, vertices);
            return mesh;
        }

        private static void FinalizeMesh(Mesh mesh, List<Vector3> vertices)
        {
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            float width = Mathf.Max(bounds.size.x, .0001f);
            float height = Mathf.Max(bounds.size.y, .0001f);
            var uv = new Vector2[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
                uv[i] = new Vector2((vertices[i].x - bounds.min.x) / width,
                    (vertices[i].y - bounds.min.y) / height);
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        private static void AddBeveledPrism(List<Vector3> vertices, List<int> triangles,
            Vector2[] outline, float depth, float insetScale)
        {
            Vector2 center = Vector2.zero;
            for (int i = 0; i < outline.Length; i++) center += outline[i];
            center /= outline.Length;
            var inner = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++) inner[i] = Vector2.Lerp(center, outline[i], insetScale);
            float faceZ = depth * .5f;
            float edgeZ = depth * .27f;

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

                Vector3 fia = new Vector3(inner[i].x, inner[i].y, -faceZ);
                Vector3 fib = new Vector3(inner[next].x, inner[next].y, -faceZ);
                Vector3 foa = new Vector3(outline[i].x, outline[i].y, -edgeZ);
                Vector3 fob = new Vector3(outline[next].x, outline[next].y, -edgeZ);
                Vector3 boa = new Vector3(outline[i].x, outline[i].y, edgeZ);
                Vector3 bob = new Vector3(outline[next].x, outline[next].y, edgeZ);
                Vector3 bia = new Vector3(inner[i].x, inner[i].y, faceZ);
                Vector3 bib = new Vector3(inner[next].x, inner[next].y, faceZ);

                AddQuad(vertices, triangles, fia, fib, fob, foa);
                AddQuad(vertices, triangles, foa, fob, bob, boa);
                AddQuad(vertices, triangles, boa, bob, bib, bia);
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
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }
    }
}
