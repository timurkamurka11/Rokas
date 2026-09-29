using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Rounded uGUI geometry avoids generated textures and stays crisp as the canvas scales.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LaptopSurface : MaskableGraphic
    {
        private float radius = 24;
        public float Radius { get { return radius; } set { radius = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float r = Mathf.Clamp(radius, 0, Mathf.Min(rect.width, rect.height) * .5f);
            const int steps = 12;
            const int count = (steps + 1) * 4;
            mesh.AddVert(rect.center, Tint(.5f), Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - r : rect.xMin + r,
                    corner < 2 ? rect.yMax - r : rect.yMin + r);
                for (int i = 0; i <= steps; i++)
                {
                    float angle = (corner * 90f + i * 90f / steps) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var point = center + direction * r;
                    var tint = Tint(Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
                    mesh.AddVert(point, tint, Vector2.zero);
                    tint.a = 0;
                    mesh.AddVert(center + direction * (r + .8f), tint, Vector2.zero);
                }
            }
            for (int i = 0; i < count; i++)
            {
                int a = 1 + i * 2;
                int b = 1 + ((i + 1) % count) * 2;
                mesh.AddTriangle(0, a, b);
                mesh.AddTriangle(a, a + 1, b);
                mesh.AddTriangle(b, a + 1, b + 1);
            }
        }

        private Color Tint(float height)
        {
            float light = Mathf.Lerp(.94f, 1.06f, height);
            return new Color(color.r * light, color.g * light, color.b * light, color.a);
        }
    }
}
