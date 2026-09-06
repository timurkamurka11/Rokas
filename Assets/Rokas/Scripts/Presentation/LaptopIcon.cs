using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum LaptopGlyph
    {
        Contracts, News, Food, Shop, Bestiary, Guides, Messages, Profile, Discounts,
        Wifi, Battery, Back, Close
    }

    // Texture-free launcher artwork. Coordinates are authored in a 100-unit square,
    // with a top-left origin, and remain square inside any RectTransform aspect ratio.
    [AddComponentMenu("ROKAS/UI/Laptop Icon")]
    public sealed class LaptopIcon : MaskableGraphic
    {
        [SerializeField] private LaptopGlyph glyph;
        private Rect drawingRect;
        private float drawingScale;

        public LaptopGlyph Glyph
        {
            get => glyph;
            set
            {
                if (glyph == value) return;
                glyph = value;
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            drawingRect = GetPixelAdjustedRect();
            drawingScale = Mathf.Min(drawingRect.width, drawingRect.height) / 100f;
            if (drawingScale <= 0) return;

            switch (glyph)
            {
                case LaptopGlyph.Contracts: DrawTorii(mesh); break;
                case LaptopGlyph.News: DrawNews(mesh); break;
                case LaptopGlyph.Food: DrawFood(mesh); break;
                case LaptopGlyph.Shop: DrawCart(mesh); break;
                case LaptopGlyph.Bestiary: DrawFox(mesh); break;
                case LaptopGlyph.Guides: DrawBook(mesh); break;
                case LaptopGlyph.Messages: DrawEnvelope(mesh); break;
                case LaptopGlyph.Profile: DrawProfile(mesh); break;
                case LaptopGlyph.Discounts: DrawPercent(mesh); break;
                case LaptopGlyph.Wifi: DrawWifi(mesh); break;
                case LaptopGlyph.Battery: DrawBattery(mesh); break;
                case LaptopGlyph.Back: Stroke(mesh, BackPath, 6); break;
                case LaptopGlyph.Close:
                    Line(mesh, 27, 27, 73, 73, 6);
                    Line(mesh, 73, 27, 27, 73, 6);
                    break;
            }
        }

        private void DrawTorii(VertexHelper mesh)
        {
            Stroke(mesh, ToriiRoof, 7);
            Line(mesh, 24, 42, 76, 42, 6);
            Line(mesh, 33, 31, 29, 80, 7);
            Line(mesh, 67, 31, 71, 80, 7);
            Line(mesh, 50, 32, 50, 41, 5);
            Line(mesh, 23, 81, 36, 81, 5);
            Line(mesh, 64, 81, 77, 81, 5);
        }

        private void DrawNews(VertexHelper mesh)
        {
            Stroke(mesh, NewspaperOutline, 4.5f, true);
            Stroke(mesh, NewspaperFold, 4.5f);
            Rectangle(mesh, 34, 29, 17, 17);
            Line(mesh, 59, 31, 68, 31, 3.5f);
            Line(mesh, 59, 42, 68, 42, 3.5f);
            Line(mesh, 34, 56, 68, 56, 3.5f);
            Line(mesh, 34, 66, 68, 66, 3.5f);
            Line(mesh, 34, 76, 59, 76, 3.5f);
        }

        private void DrawFood(VertexHelper mesh)
        {
            Stroke(mesh, SteamLeft, 4);
            Stroke(mesh, SteamMiddle, 4);
            Stroke(mesh, SteamRight, 4);
            Ellipse(mesh, 50, 50, 33, 6.5f, 4, 32);
            Stroke(mesh, BowlOutline, 4.5f);
            Line(mesh, 38, 85, 62, 85, 4.5f);
        }

        private void DrawCart(VertexHelper mesh)
        {
            Stroke(mesh, CartFrame, 5);
            Stroke(mesh, CartBasket, 5);
            Disc(mesh, new Vector2(41, 78), 5.5f);
            Disc(mesh, new Vector2(70, 78), 5.5f);
        }

        private void DrawFox(VertexHelper mesh)
        {
            Stroke(mesh, FoxOutline, 4.5f, true);
            Line(mesh, 29, 27, 31, 38, 3.5f);
            Line(mesh, 71, 27, 69, 38, 3.5f);
            Line(mesh, 28, 44, 42, 49, 3.5f);
            Line(mesh, 72, 44, 58, 49, 3.5f);
            Polygon(mesh, FoxLeftEye);
            Polygon(mesh, FoxRightEye);
            Polygon(mesh, FoxNose);
            Line(mesh, 50, 68, 50, 75, 3.5f);
        }

        private void DrawBook(VertexHelper mesh)
        {
            Stroke(mesh, BookOutline, 4.5f, true);
            Line(mesh, 50, 29, 50, 79, 4);
            Line(mesh, 28, 36, 40, 39, 3);
            Line(mesh, 28, 47, 40, 50, 3);
            Line(mesh, 28, 58, 40, 61, 3);
            Line(mesh, 60, 39, 72, 36, 3);
            Line(mesh, 60, 50, 72, 47, 3);
            Line(mesh, 60, 61, 72, 58, 3);
        }

        private void DrawEnvelope(VertexHelper mesh)
        {
            Stroke(mesh, EnvelopeOutline, 5, true);
            Stroke(mesh, EnvelopeFlap, 4.5f);
            Line(mesh, 20, 71, 38, 53, 4);
            Line(mesh, 80, 71, 62, 53, 4);
        }

        private void DrawProfile(VertexHelper mesh)
        {
            Ellipse(mesh, 50, 33, 14, 14, 5, 28);
            Stroke(mesh, ProfileShoulders, 5, true);
        }

        private void DrawPercent(VertexHelper mesh)
        {
            Line(mesh, 29, 78, 71, 22, 6);
            Ellipse(mesh, 30, 31, 10, 10, 5, 24);
            Ellipse(mesh, 70, 69, 10, 10, 5, 24);
        }

        private void DrawWifi(VertexHelper mesh)
        {
            Arc(mesh, 50, 76, 48, 220, 320, 5.5f, 20);
            Arc(mesh, 50, 76, 32, 220, 320, 5.5f, 16);
            Arc(mesh, 50, 76, 16, 220, 320, 5.5f, 12);
            Disc(mesh, new Vector2(50, 76), 4.5f);
        }

        private void DrawBattery(VertexHelper mesh)
        {
            Stroke(mesh, BatteryOutline, 5, true);
            Rectangle(mesh, 25, 41, 44, 18);
            Line(mesh, 85, 44, 85, 56, 5);
        }

        private void AddVertex(VertexHelper mesh, Vector2 point)
        {
            var center = drawingRect.center;
            mesh.AddVert(new Vector3(center.x + (point.x - 50) * drawingScale,
                center.y + (50 - point.y) * drawingScale, 0), color, Vector2.zero);
        }

        private void Rectangle(VertexHelper mesh, float x, float y, float width, float height)
        {
            int first = mesh.currentVertCount;
            AddVertex(mesh, new Vector2(x, y));
            AddVertex(mesh, new Vector2(x + width, y));
            AddVertex(mesh, new Vector2(x + width, y + height));
            AddVertex(mesh, new Vector2(x, y + height));
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        // All filled polygons below are convex, so a small triangle fan is sufficient.
        private void Polygon(VertexHelper mesh, Vector2[] points)
        {
            int first = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++) AddVertex(mesh, points[i]);
            for (int i = 1; i < points.Length - 1; i++)
                mesh.AddTriangle(first, first + i, first + i + 1);
        }

        private void Line(VertexHelper mesh, float x1, float y1, float x2, float y2, float thickness)
        {
            var a = new Vector2(x1, y1);
            var b = new Vector2(x2, y2);
            var direction = (b - a).normalized;
            var normal = new Vector2(-direction.y, direction.x) * (thickness * .5f);
            int first = mesh.currentVertCount;
            AddVertex(mesh, a + normal);
            AddVertex(mesh, a - normal);
            AddVertex(mesh, b + normal);
            AddVertex(mesh, b - normal);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
            Cap(mesh, a, -direction, thickness * .5f);
            Cap(mesh, b, direction, thickness * .5f);
        }

        // Mitered joins share their vertices; semicircle caps soften open path ends.
        private void Stroke(VertexHelper mesh, Vector2[] points, float thickness, bool closed = false)
        {
            int count = points.Length;
            int first = mesh.currentVertCount;
            float half = thickness * .5f;
            for (int i = 0; i < count; i++)
            {
                var previous = (points[i] - points[(i + count - 1) % count]).normalized;
                var next = (points[(i + 1) % count] - points[i]).normalized;
                if (!closed && i == 0) previous = next;
                if (!closed && i == count - 1) next = previous;
                var n1 = new Vector2(-previous.y, previous.x);
                var n2 = new Vector2(-next.y, next.x);
                var miter = (n1 + n2).normalized;
                float denominator = Mathf.Max(.5f, Vector2.Dot(miter, n2));
                var offset = miter * (half / denominator);
                AddVertex(mesh, points[i] + offset);
                AddVertex(mesh, points[i] - offset);
            }
            int segments = closed ? count : count - 1;
            for (int i = 0; i < segments; i++)
            {
                int a = first + i * 2;
                int b = first + ((i + 1) % count) * 2;
                mesh.AddTriangle(a, a + 1, b);
                mesh.AddTriangle(b, a + 1, b + 1);
            }
            if (!closed)
            {
                Cap(mesh, points[0], (points[0] - points[1]).normalized, half);
                Cap(mesh, points[count - 1], (points[count - 1] - points[count - 2]).normalized, half);
            }
        }

        private void Cap(VertexHelper mesh, Vector2 center, Vector2 direction, float radius)
        {
            const int segments = 6;
            int first = mesh.currentVertCount;
            AddVertex(mesh, center);
            float angle = Mathf.Atan2(direction.y, direction.x) - Mathf.PI * .5f;
            for (int i = 0; i <= segments; i++)
            {
                float step = angle + Mathf.PI * i / segments;
                AddVertex(mesh, center + new Vector2(Mathf.Cos(step), Mathf.Sin(step)) * radius);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Disc(VertexHelper mesh, Vector2 center, float radius)
        {
            const int segments = 20;
            int first = mesh.currentVertCount;
            AddVertex(mesh, center);
            for (int i = 0; i < segments; i++)
            {
                float angle = 2 * Mathf.PI * i / segments;
                AddVertex(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            for (int i = 0; i < segments; i++)
                mesh.AddTriangle(first, first + i + 1, first + (i + 1) % segments + 1);
        }

        private void Ellipse(VertexHelper mesh, float x, float y, float rx, float ry,
            float thickness, int segments)
        {
            int first = mesh.currentVertCount;
            float half = thickness * .5f;
            for (int i = 0; i < segments; i++)
            {
                float angle = 2 * Mathf.PI * i / segments;
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var position = new Vector2(x + radial.x * rx, y + radial.y * ry);
                var normal = new Vector2(radial.x / rx, radial.y / ry).normalized * half;
                AddVertex(mesh, position + normal);
                AddVertex(mesh, position - normal);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = first + i * 2;
                int b = first + ((i + 1) % segments) * 2;
                mesh.AddTriangle(a, a + 1, b);
                mesh.AddTriangle(b, a + 1, b + 1);
            }
        }

        private void Arc(VertexHelper mesh, float x, float y, float radius, float startDegrees,
            float endDegrees, float thickness, int segments)
        {
            int first = mesh.currentVertCount;
            float half = thickness * .5f;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(startDegrees, endDegrees, (float)i / segments) * Mathf.Deg2Rad;
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(mesh, new Vector2(x, y) + radial * (radius + half));
                AddVertex(mesh, new Vector2(x, y) + radial * (radius - half));
                if (i == segments) continue;
                int a = first + i * 2;
                mesh.AddTriangle(a, a + 1, a + 2);
                mesh.AddTriangle(a + 2, a + 1, a + 3);
            }
            float start = startDegrees * Mathf.Deg2Rad;
            float end = endDegrees * Mathf.Deg2Rad;
            Cap(mesh, new Vector2(x + Mathf.Cos(start) * radius, y + Mathf.Sin(start) * radius),
                new Vector2(Mathf.Sin(start), -Mathf.Cos(start)), half);
            Cap(mesh, new Vector2(x + Mathf.Cos(end) * radius, y + Mathf.Sin(end) * radius),
                new Vector2(-Mathf.Sin(end), Mathf.Cos(end)), half);
        }

        private static readonly Vector2[] ToriiRoof =
        { new Vector2(16, 23), new Vector2(30, 28), new Vector2(50, 30), new Vector2(70, 28), new Vector2(84, 23) };
        private static readonly Vector2[] NewspaperOutline =
        { new Vector2(25, 19), new Vector2(77, 19), new Vector2(77, 80), new Vector2(74, 84), new Vector2(25, 84) };
        private static readonly Vector2[] NewspaperFold =
        { new Vector2(25, 34), new Vector2(17, 34), new Vector2(17, 78), new Vector2(20, 83), new Vector2(25, 84) };
        private static readonly Vector2[] SteamLeft =
        { new Vector2(32, 38), new Vector2(29, 34), new Vector2(29, 30), new Vector2(33, 25), new Vector2(33, 21) };
        private static readonly Vector2[] SteamMiddle =
        { new Vector2(50, 37), new Vector2(47, 32), new Vector2(47, 27), new Vector2(51, 22), new Vector2(51, 17) };
        private static readonly Vector2[] SteamRight =
        { new Vector2(68, 38), new Vector2(65, 34), new Vector2(65, 30), new Vector2(69, 25), new Vector2(69, 21) };
        private static readonly Vector2[] BowlOutline =
        { new Vector2(17, 51), new Vector2(22, 65), new Vector2(29, 74), new Vector2(40, 80),
          new Vector2(60, 80), new Vector2(71, 74), new Vector2(78, 65), new Vector2(83, 51) };
        private static readonly Vector2[] CartFrame =
        { new Vector2(16, 23), new Vector2(26, 23), new Vector2(35, 65), new Vector2(75, 65) };
        private static readonly Vector2[] CartBasket =
        { new Vector2(30, 34), new Vector2(84, 34), new Vector2(77, 55), new Vector2(34, 55) };
        private static readonly Vector2[] FoxOutline =
        { new Vector2(50, 29), new Vector2(34, 22), new Vector2(21, 12), new Vector2(23, 46),
          new Vector2(20, 53), new Vector2(29, 69), new Vector2(50, 86), new Vector2(71, 69),
          new Vector2(80, 53), new Vector2(77, 46), new Vector2(79, 12), new Vector2(66, 22) };
        private static readonly Vector2[] FoxLeftEye =
        { new Vector2(28, 52), new Vector2(43, 55), new Vector2(38, 60), new Vector2(31, 57) };
        private static readonly Vector2[] FoxRightEye =
        { new Vector2(72, 52), new Vector2(57, 55), new Vector2(62, 60), new Vector2(69, 57) };
        private static readonly Vector2[] FoxNose =
        { new Vector2(45, 64), new Vector2(55, 64), new Vector2(50, 70) };
        private static readonly Vector2[] BookOutline =
        { new Vector2(50, 29), new Vector2(41, 23), new Vector2(22, 20), new Vector2(18, 20),
          new Vector2(18, 72), new Vector2(34, 73), new Vector2(50, 80), new Vector2(66, 73),
          new Vector2(82, 72), new Vector2(82, 20), new Vector2(78, 20), new Vector2(59, 23) };
        private static readonly Vector2[] EnvelopeOutline =
        { new Vector2(22, 27), new Vector2(78, 27), new Vector2(82, 31), new Vector2(82, 70),
          new Vector2(78, 74), new Vector2(22, 74), new Vector2(18, 70), new Vector2(18, 31) };
        private static readonly Vector2[] EnvelopeFlap =
        { new Vector2(20, 30), new Vector2(50, 55), new Vector2(80, 30) };
        private static readonly Vector2[] ProfileShoulders =
        { new Vector2(22, 80), new Vector2(22, 73), new Vector2(26, 64), new Vector2(34, 58),
          new Vector2(42, 55), new Vector2(58, 55), new Vector2(66, 58), new Vector2(74, 64),
          new Vector2(78, 73), new Vector2(78, 80) };
        private static readonly Vector2[] BatteryOutline =
        { new Vector2(19, 34), new Vector2(75, 34), new Vector2(79, 38), new Vector2(79, 62),
          new Vector2(75, 66), new Vector2(19, 66), new Vector2(15, 62), new Vector2(15, 38) };
        private static readonly Vector2[] BackPath =
        { new Vector2(63, 22), new Vector2(35, 50), new Vector2(63, 78) };
    }
}
