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

        private void Update()
        {
            if (Application.isPlaying) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (points == null || points.Count < 2) return;
            Stroke(mesh, glowThickness, glowColor, 1f);
            Stroke(mesh, haloThickness, haloColor, .58f);
            Stroke(mesh, coreThickness, coreColor, .34f);
        }

        private void Stroke(VertexHelper mesh, float thickness, Color strokeColor, float strength)
        {
            int segmentCount = points.Count - 1 + (closed && points.Count > 2 ? 1 : 0);
            int segment = 0;

            for (int i = 0; i < points.Count - 1; i++, segment++)
            {
                float position = (segment + .5f) / Mathf.Max(1, segmentCount);
                Segment(mesh, Map(points[i]), Map(points[i + 1]), thickness,
                    Animate(strokeColor, position, strength));
            }

            if (closed && points.Count > 2)
            {
                float position = (segment + .5f) / Mathf.Max(1, segmentCount);
                Segment(mesh, Map(points[points.Count - 1]), Map(points[0]), thickness,
                    Animate(strokeColor, position, strength));
            }
        }

        private Color Animate(Color source, float position, float strength)
        {
            if (!Application.isPlaying) return source;

            float phase = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * .000173f, 1f);
            float t = Time.unscaledTime;
            float pulse = 1f + Mathf.Sin(t * 1.15f + phase * Mathf.PI * 2f) * .055f;

            float cursor = Mathf.Repeat(t * .115f + phase, 1f);
            float distance = Mathf.Abs(position - cursor);
            distance = Mathf.Min(distance, 1f - distance);
            float sweep = 1f - Mathf.SmoothStep(0f, .16f, distance);
            sweep *= .20f * strength;

            float gain = pulse + sweep;
            Color result = source;
            result.r = Mathf.Clamp01(source.r * gain);
            result.g = Mathf.Clamp01(source.g * gain);
            result.b = Mathf.Clamp01(source.b * gain);
            result.a = Mathf.Clamp01(source.a * (1f + sweep * .70f));
            return result;
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

        private void Update()
        {
            if (Application.isPlaying) SetVerticesDirty();
        }

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

                float position = count <= 1 ? 0f : (float)i / (count - 1);
                Vector2 center = new Vector2(rect.center.x, y);
                if (glowExtra > 0f)
                    Disc(mesh, center, radius + glowExtra, Animate(glowColor, position, 1f), circleSegments);
                Disc(mesh, center, radius, Animate(coreColor, position, .58f), circleSegments);
            }
        }

        private Color Animate(Color source, float position, float strength)
        {
            if (!Application.isPlaying) return source;

            float phase = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * .000191f, 1f);
            float t = Time.unscaledTime;
            float pulse = 1f + Mathf.Sin(t * 1.20f + phase * Mathf.PI * 2f) * .06f;

            float cursor = Mathf.Repeat(t * .18f + phase, 1f);
            float distance = Mathf.Abs(position - cursor);
            distance = Mathf.Min(distance, 1f - distance);
            float sweep = 1f - Mathf.SmoothStep(0f, .22f, distance);
            sweep *= .26f * strength;

            float gain = pulse + sweep;
            Color result = source;
            result.r = Mathf.Clamp01(source.r * gain);
            result.g = Mathf.Clamp01(source.g * gain);
            result.b = Mathf.Clamp01(source.b * gain);
            result.a = Mathf.Clamp01(source.a * (1f + sweep * .75f));
            return result;
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
