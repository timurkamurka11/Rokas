using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.EditorTools
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainRoomOverlayEditablePath : MaskableGraphic
    {
        [SerializeField] private List<Vector2> points = new List<Vector2>();
        [SerializeField] private bool closed = true;
        [SerializeField, Min(.25f)] private float coreThickness = 2.2f;
        [SerializeField, Min(.25f)] private float haloThickness = 6f;
        [SerializeField, Min(.25f)] private float glowThickness = 13f;
        [SerializeField] private Color coreColor = new Color(1f, .73f, .30f, .94f);
        [SerializeField] private Color haloColor = new Color(.98f, .70f, .28f, .24f);
        [SerializeField] private Color glowColor = new Color(.96f, .66f, .24f, .08f);

        public int PointCount => points?.Count ?? 0;

        public Vector2 GetPoint(int index) => points[index];

        public void SetPoint(int index, Vector2 point)
        {
            points[index] = point;
            SetVerticesDirty();
        }

        public void SetPoints(IEnumerable<Vector2> source, bool closePath = true)
        {
            points = source != null ? new List<Vector2>(source) : new List<Vector2>();
            closed = closePath;
            SetVerticesDirty();
        }

        public void AddPoint(Vector2 point)
        {
            if (points == null) points = new List<Vector2>();
            points.Add(point);
            SetVerticesDirty();
        }

        public void RemoveLastPoint()
        {
            if (points == null || points.Count == 0) return;
            points.RemoveAt(points.Count - 1);
            SetVerticesDirty();
        }

        public Vector3 ReferenceToWorld(Vector2 reference)
        {
            Rect rect = GetPixelAdjustedRect();
            Vector3 local = new Vector3(rect.xMin + reference.x, rect.yMax - reference.y, 0f);
            return rectTransform.TransformPoint(local);
        }

        public Vector2 WorldToReference(Vector3 world)
        {
            Rect rect = GetPixelAdjustedRect();
            Vector3 local = rectTransform.InverseTransformPoint(world);
            return new Vector2(local.x - rect.xMin, rect.yMax - local.y);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (points == null || points.Count < 2) return;
            Stroke(mesh, glowThickness, glowColor);
            Stroke(mesh, haloThickness, haloColor);
            Stroke(mesh, coreThickness, coreColor);
        }

        private void Stroke(VertexHelper mesh, float thickness, Color strokeColor)
        {
            for (int i = 0; i < points.Count - 1; i++)
                Segment(mesh, Map(points[i]), Map(points[i + 1]), thickness, strokeColor);
            if (closed && points.Count > 2)
                Segment(mesh, Map(points[points.Count - 1]), Map(points[0]), thickness, strokeColor);
        }

        private Vector2 Map(Vector2 reference)
        {
            Rect rect = GetPixelAdjustedRect();
            return new Vector2(rect.xMin + reference.x, rect.yMax - reference.y);
        }

        private static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color strokeColor)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude <= .0001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * thickness * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(a + normal, strokeColor, Vector2.zero);
            mesh.AddVert(a - normal, strokeColor, Vector2.zero);
            mesh.AddVert(b + normal, strokeColor, Vector2.zero);
            mesh.AddVert(b - normal, strokeColor, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }

    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainRoomOverlayDottedConnector : MaskableGraphic
    {
        [SerializeField, Min(1f)] private float dotSize = 7f;
        [SerializeField, Min(2f)] private float spacing = 14f;
        [SerializeField, Range(6, 24)] private int circleSegments = 12;
        [SerializeField] private Color coreColor = new Color(1f, .76f, .35f, .98f);
        [SerializeField] private Color glowColor = new Color(1f, .62f, .18f, .22f);
        [SerializeField, Min(0f)] private float glowExtra = 5f;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = dotSize * .5f;
            float usable = Mathf.Max(0f, rect.height - dotSize);
            int count = Mathf.Max(1, Mathf.FloorToInt(usable / spacing) + 1);
            float startY = rect.yMax - radius;

            for (int i = 0; i < count; i++)
            {
                float y = startY - i * spacing;
                if (y - radius < rect.yMin) break;
                Vector2 center = new Vector2(rect.center.x, y);
                if (glowExtra > 0f)
                    Disc(mesh, center, radius + glowExtra, glowColor, circleSegments);
                Disc(mesh, center, radius, coreColor, circleSegments);
            }
        }

        private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color color, int segments)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
                mesh.AddTriangle(first, first + i + 1, first + ((i + 1) % segments) + 1);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
