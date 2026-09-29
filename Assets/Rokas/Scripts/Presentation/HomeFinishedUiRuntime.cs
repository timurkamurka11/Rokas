using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    [Serializable]
    internal sealed class HomeFinalRect
    {
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector3 localScale = Vector3.one;
        public float rotationZ;
        public bool active = true;
    }

    [Serializable]
    internal sealed class HomeFinalOutline
    {
        public string id;
        public HomeFinalRect rect;
        public Vector2[] points;
        public bool closed;
        public float coreThickness;
        public float haloThickness;
        public float glowThickness;
        public Color coreColor;
        public Color haloColor;
        public Color glowColor;
    }

    [Serializable]
    internal sealed class HomeFinalConnector
    {
        public string id;
        public HomeFinalRect rect;
        public float dotSize;
        public float spacing;
        public int circleSegments;
        public Color coreColor;
        public Color glowColor;
        public float glowExtra;
    }

    [Serializable]
    internal sealed class HomeFinalIcon
    {
        public string id;
        public string resourceName;
        public HomeFinalRect rect;
        public Color color;
        public bool preserveAspect;
    }

    public static class HomeFinalUiPresenter
    {
        private static readonly HomeFinalOutline[] Outlines =
        {
            new HomeFinalOutline
            {
                id = "Outline_Door",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(0.0f, 0.0f),
                    sizeDelta = new Vector2(0.0f, 0.0f),
                    anchorMin = new Vector2(0.0f, 0.0f),
                    anchorMax = new Vector2(1.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.5f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                points = new Vector2[] { new Vector2(1621.00098f, 68.4429932f), new Vector2(1874.11768f, 25.3491211f), new Vector2(1877.46851f, 407.045166f), new Vector2(1798.93896f, 407.198669f), new Vector2(1796.27771f, 476.557922f), new Vector2(1748.34473f, 472.096008f), new Vector2(1749.07068f, 644.883301f), new Vector2(1616.95166f, 623.532593f) },
                closed = true,
                coreThickness = 3.0f,
                haloThickness = 5.0f,
                glowThickness = 13.0f,
                coreColor = new Color(1.0f, 0.730000019f, 0.300000012f, 0.939999998f),
                haloColor = new Color(0.980000019f, 0.699999988f, 0.280000001f, 0.239999995f),
                glowColor = new Color(0.959999979f, 0.660000026f, 0.239999995f, 0.0799999982f)
            },
            new HomeFinalOutline
            {
                id = "Outline_Laptop",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(0.0f, 0.0f),
                    sizeDelta = new Vector2(0.0f, 0.0f),
                    anchorMin = new Vector2(0.0f, 0.0f),
                    anchorMax = new Vector2(1.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.5f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                points = new Vector2[] { new Vector2(1007.67822f, 525.648499f), new Vector2(1186.97522f, 506.052185f), new Vector2(1149.47925f, 628.488342f), new Vector2(970.929321f, 656.580444f), new Vector2(933.065308f, 642.564331f), new Vector2(891.865601f, 628.24115f), new Vector2(983.132202f, 610.727295f), new Vector2(971.148804f, 656.687927f) },
                closed = true,
                coreThickness = 2.20000005f,
                haloThickness = 6.0f,
                glowThickness = 13.0f,
                coreColor = new Color(1.0f, 0.730000019f, 0.300000012f, 0.939999998f),
                haloColor = new Color(0.980000019f, 0.699999988f, 0.280000001f, 0.239999995f),
                glowColor = new Color(0.959999979f, 0.660000026f, 0.239999995f, 0.0799999982f)
            },
            new HomeFinalOutline
            {
                id = "Outline_Cat",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(0.0f, 0.0f),
                    sizeDelta = new Vector2(0.0f, 0.0f),
                    anchorMin = new Vector2(0.0f, 0.0f),
                    anchorMax = new Vector2(1.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.5f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                points = new Vector2[] { new Vector2(123.781372f, 842.929993f), new Vector2(202.867798f, 815.819458f), new Vector2(291.865356f, 804.273987f), new Vector2(379.853271f, 813.114197f), new Vector2(435.801453f, 841.975098f), new Vector2(440.968872f, 886.599792f), new Vector2(423.304932f, 941.826782f), new Vector2(184.896606f, 1001.55249f), new Vector2(177.928162f, 915.330688f), new Vector2(109.543396f, 862.430908f) },
                closed = true,
                coreThickness = 3.5f,
                haloThickness = 10.0f,
                glowThickness = 20.0f,
                coreColor = new Color(1.0f, 0.730000019f, 0.300000012f, 0.939999998f),
                haloColor = new Color(0.980000019f, 0.699999988f, 0.280000001f, 0.239999995f),
                glowColor = new Color(0.959999979f, 0.660000026f, 0.239999995f, 0.0799999982f)
            }
        };

        private static readonly HomeFinalConnector[] Connectors =
        {
            new HomeFinalConnector
            {
                id = "Connector_Window",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(697.0f, -196.0f),
                    sizeDelta = new Vector2(22.0f, 70.0f),
                    anchorMin = new Vector2(0.0f, 1.0f),
                    anchorMax = new Vector2(0.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.0f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                dotSize = 7.0f,
                spacing = 14.0f,
                circleSegments = 12,
                coreColor = new Color(1.0f, 0.75999999f, 0.349999994f, 0.980000019f),
                glowColor = new Color(1.0f, 0.620000005f, 0.180000007f, 0.219999999f),
                glowExtra = 5.0f
            },
            new HomeFinalConnector
            {
                id = "Connector_Cat",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(297.0f, -845.0f),
                    sizeDelta = new Vector2(22.0f, 70.0f),
                    anchorMin = new Vector2(0.0f, 1.0f),
                    anchorMax = new Vector2(0.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.0f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                dotSize = 7.0f,
                spacing = 14.0f,
                circleSegments = 12,
                coreColor = new Color(1.0f, 0.75999999f, 0.349999994f, 0.980000019f),
                glowColor = new Color(1.0f, 0.620000005f, 0.180000007f, 0.219999999f),
                glowExtra = 5.0f
            }
        };

        private static readonly HomeFinalIcon[] Icons =
        {
            new HomeFinalIcon
            {
                id = "Hotspot_Window",
                resourceName = "Window",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(697.0f, -129.0f),
                    sizeDelta = new Vector2(96.0f, 96.0f),
                    anchorMin = new Vector2(0.0f, 1.0f),
                    anchorMax = new Vector2(0.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.5f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                preserveAspect = true
            },
            new HomeFinalIcon
            {
                id = "Hotspot_Swords",
                resourceName = "Swords",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(1404.0f, -297.0f),
                    sizeDelta = new Vector2(96.0f, 96.0f),
                    anchorMin = new Vector2(0.0f, 1.0f),
                    anchorMax = new Vector2(0.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.5f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                preserveAspect = true
            },
            new HomeFinalIcon
            {
                id = "Hotspot_Door",
                resourceName = "Exit",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(1748.0f, -133.0f),
                    sizeDelta = new Vector2(96.0f, 96.0f),
                    anchorMin = new Vector2(0.0f, 1.0f),
                    anchorMax = new Vector2(0.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.5f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                preserveAspect = true
            },
            new HomeFinalIcon
            {
                id = "Hotspot_Laptop",
                resourceName = "Laptop",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(1093.0f, -490.0f),
                    sizeDelta = new Vector2(96.0f, 96.0f),
                    anchorMin = new Vector2(0.0f, 1.0f),
                    anchorMax = new Vector2(0.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.5f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                preserveAspect = true
            },
            new HomeFinalIcon
            {
                id = "Hotspot_FloorLamp",
                resourceName = "LightBulb",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(139.0f, -400.0f),
                    sizeDelta = new Vector2(96.0f, 96.0f),
                    anchorMin = new Vector2(0.0f, 1.0f),
                    anchorMax = new Vector2(0.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.5f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                preserveAspect = true
            },
            new HomeFinalIcon
            {
                id = "Hotspot_Cat",
                resourceName = "Cat",
                rect = new HomeFinalRect
                {
                    anchoredPosition = new Vector2(297.0f, -782.0f),
                    sizeDelta = new Vector2(96.0f, 96.0f),
                    anchorMin = new Vector2(0.0f, 1.0f),
                    anchorMax = new Vector2(0.0f, 1.0f),
                    pivot = new Vector2(0.5f, 0.5f),
                    localScale = new Vector3(1.0f, 1.0f, 1.0f),
                    rotationZ = 0.0f,
                    active = true
                },
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                preserveAspect = true
            }
        };

        private static bool? cachedEnabled;

        public static bool Enabled
        {
            get
            {
                if (cachedEnabled.HasValue)
                    return cachedEnabled.Value;

                for (int i = 0; i < Icons.Length; i++)
                {
                    if (!Resources.Load<Texture2D>(
                            "HomeFinalIcons/" + Icons[i].resourceName))
                    {
                        cachedEnabled = false;
                        Debug.LogWarning(
                            "[ROKAS HOME] Final icon texture is missing: " +
                            Icons[i].resourceName +
                            ". Legacy Home UI stays available.");
                        return false;
                    }
                }

                cachedEnabled = true;
                return true;
            }
        }

        public static RectTransform Build(RectTransform parent)
        {
            if (!parent || !Enabled)
                return null;

            GameObject rootObject =
                new GameObject(
                    "HomeFinalUiRuntime",
                    typeof(RectTransform));

            RectTransform root =
                rootObject.GetComponent<RectTransform>();

            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(.5f, .5f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;

            RectTransform outlineRoot =
                CreateGroup(root, "Outlines");

            RectTransform connectorRoot =
                CreateGroup(root, "Connectors");

            RectTransform hotspotRoot =
                CreateGroup(root, "Hotspots");

            for (int i = 0; i < Outlines.Length; i++)
            {
                HomeFinalOutline data = Outlines[i];

                GameObject go =
                    new GameObject(
                        data.id,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(HomeFinalOutlineGraphic));

                RectTransform rect =
                    go.GetComponent<RectTransform>();

                rect.SetParent(outlineRoot, false);
                ApplyRect(rect, data.rect);

                HomeFinalOutlineGraphic graphic =
                    go.GetComponent<HomeFinalOutlineGraphic>();

                graphic.raycastTarget = false;
                graphic.Configure(data);
            }

            for (int i = 0; i < Connectors.Length; i++)
            {
                HomeFinalConnector data = Connectors[i];

                GameObject go =
                    new GameObject(
                        data.id,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(HomeFinalConnectorGraphic));

                RectTransform rect =
                    go.GetComponent<RectTransform>();

                rect.SetParent(connectorRoot, false);
                ApplyRect(rect, data.rect);

                HomeFinalConnectorGraphic graphic =
                    go.GetComponent<HomeFinalConnectorGraphic>();

                graphic.raycastTarget = false;
                graphic.Configure(data);
            }

            for (int i = 0; i < Icons.Length; i++)
            {
                HomeFinalIcon data = Icons[i];

                Texture2D texture =
                    Resources.Load<Texture2D>(
                        "HomeFinalIcons/" + data.resourceName);

                if (!texture)
                    continue;

                Sprite sprite =
                    Sprite.Create(
                        texture,
                        new Rect(
                            0f,
                            0f,
                            texture.width,
                            texture.height),
                        new Vector2(.5f, .5f),
                        100f);

                sprite.name =
                    "Runtime_" + data.resourceName;

                GameObject go =
                    new GameObject(
                        data.id,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));

                RectTransform rect =
                    go.GetComponent<RectTransform>();

                rect.SetParent(hotspotRoot, false);
                ApplyRect(rect, data.rect);

                Image image =
                    go.GetComponent<Image>();

                image.sprite = sprite;
                image.color = data.color;
                image.preserveAspect = data.preserveAspect;
                image.raycastTarget = false;
            }

            // Keep the authored visuals above the room background,
            // while the existing transparent gameplay hit targets still work.
            root.SetAsLastSibling();

            Debug.Log(
                "[ROKAS HOME] FINAL DIRECT UI READY: " +
                "outlines=" + Outlines.Length +
                " connectors=" + Connectors.Length +
                " icons=" + Icons.Length);

            return root;
        }

        private static RectTransform CreateGroup(
            RectTransform parent,
            string name)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform));

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void ApplyRect(
            RectTransform rect,
            HomeFinalRect data)
        {
            rect.anchorMin = data.anchorMin;
            rect.anchorMax = data.anchorMax;
            rect.pivot = data.pivot;
            rect.anchoredPosition = data.anchoredPosition;
            rect.sizeDelta = data.sizeDelta;
            rect.localScale = data.localScale;
            rect.localEulerAngles =
                new Vector3(
                    0f,
                    0f,
                    data.rotationZ);

            rect.gameObject.SetActive(
                data.active);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HomeFinalOutlineGraphic :
        MaskableGraphic
    {
        private HomeFinalOutline data;
        private float phase;

        public void Configure(HomeFinalOutline source)
        {
            data = source;
            phase =
                Mathf.Repeat(
                    Mathf.Abs(GetInstanceID()) *
                    .000173f,
                    1f);

            SetVerticesDirty();
        }

        private void Update()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(
            VertexHelper mesh)
        {
            mesh.Clear();

            if (data == null ||
                data.points == null ||
                data.points.Length < 2)
            {
                return;
            }

            Stroke(
                mesh,
                data.glowThickness,
                data.glowColor,
                1f);

            Stroke(
                mesh,
                data.haloThickness,
                data.haloColor,
                .58f);

            Stroke(
                mesh,
                data.coreThickness,
                data.coreColor,
                .34f);
        }

        private void Stroke(
            VertexHelper mesh,
            float thickness,
            Color source,
            float shimmerStrength)
        {
            int segmentCount =
                data.points.Length - 1 +
                (data.closed &&
                 data.points.Length > 2
                    ? 1
                    : 0);

            int segment = 0;

            for (int i = 0;
                 i < data.points.Length - 1;
                 i++, segment++)
            {
                float position =
                    (segment + .5f) /
                    Mathf.Max(1, segmentCount);

                AddSegment(
                    mesh,
                    Map(data.points[i]),
                    Map(data.points[i + 1]),
                    thickness,
                    Animate(
                        source,
                        position,
                        shimmerStrength));
            }

            if (data.closed &&
                data.points.Length > 2)
            {
                float position =
                    (segment + .5f) /
                    Mathf.Max(1, segmentCount);

                AddSegment(
                    mesh,
                    Map(data.points[
                        data.points.Length - 1]),
                    Map(data.points[0]),
                    thickness,
                    Animate(
                        source,
                        position,
                        shimmerStrength));
            }
        }

        private Color Animate(
            Color source,
            float position,
            float shimmerStrength)
        {
            float time =
                Time.unscaledTime;

            // Gentle breathing. Geometry never moves.
            float pulse =
                1f +
                Mathf.Sin(
                    time * 1.12f +
                    phase * Mathf.PI * 2f) *
                .065f;

            // Slow travelling warm highlight.
            float cursor =
                Mathf.Repeat(
                    time * .12f +
                    phase,
                    1f);

            float distance =
                Mathf.Abs(
                    position - cursor);

            distance =
                Mathf.Min(
                    distance,
                    1f - distance);

            float shimmer =
                1f -
                Mathf.SmoothStep(
                    0f,
                    .17f,
                    distance);

            shimmer *=
                .25f *
                shimmerStrength;

            float gain =
                pulse +
                shimmer;

            Color result =
                source;

            result.r =
                Mathf.Clamp01(
                    source.r * gain);

            result.g =
                Mathf.Clamp01(
                    source.g * gain);

            result.b =
                Mathf.Clamp01(
                    source.b * gain);

            result.a =
                Mathf.Clamp01(
                    source.a *
                    (1f + shimmer * .85f));

            return result;
        }

        private Vector2 Map(
            Vector2 reference)
        {
            Rect rect =
                GetPixelAdjustedRect();

            return new Vector2(
                rect.xMin + reference.x,
                rect.yMax - reference.y);
        }

        private static void AddSegment(
            VertexHelper mesh,
            Vector2 a,
            Vector2 b,
            float thickness,
            Color color)
        {
            Vector2 delta =
                b - a;

            if (delta.sqrMagnitude <=
                .0001f)
            {
                return;
            }

            Vector2 normal =
                new Vector2(
                    -delta.y,
                    delta.x).normalized *
                thickness * .5f;

            int first =
                mesh.currentVertCount;

            mesh.AddVert(
                a + normal,
                color,
                Vector2.zero);

            mesh.AddVert(
                a - normal,
                color,
                Vector2.zero);

            mesh.AddVert(
                b + normal,
                color,
                Vector2.zero);

            mesh.AddVert(
                b - normal,
                color,
                Vector2.zero);

            mesh.AddTriangle(
                first,
                first + 1,
                first + 2);

            mesh.AddTriangle(
                first + 2,
                first + 1,
                first + 3);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HomeFinalConnectorGraphic :
        MaskableGraphic
    {
        private HomeFinalConnector data;
        private float phase;

        public void Configure(
            HomeFinalConnector source)
        {
            data = source;

            phase =
                Mathf.Repeat(
                    Mathf.Abs(
                        GetInstanceID()) *
                    .000191f,
                    1f);

            SetVerticesDirty();
        }

        private void Update()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(
            VertexHelper mesh)
        {
            mesh.Clear();

            if (data == null)
                return;

            Rect rect =
                GetPixelAdjustedRect();

            float radius =
                data.dotSize * .5f;

            float usable =
                Mathf.Max(
                    0f,
                    rect.height -
                    data.dotSize);

            int count =
                Mathf.Max(
                    1,
                    Mathf.FloorToInt(
                        usable /
                        Mathf.Max(
                            2f,
                            data.spacing)) +
                    1);

            float startY =
                rect.yMax -
                radius;

            for (int i = 0;
                 i < count;
                 i++)
            {
                float y =
                    startY -
                    i * data.spacing;

                if (y - radius <
                    rect.yMin)
                {
                    break;
                }

                float position =
                    count <= 1
                    ? 0f
                    : (float)i /
                      (count - 1);

                Vector2 center =
                    new Vector2(
                        rect.center.x,
                        y);

                if (data.glowExtra > 0f)
                {
                    AddDisc(
                        mesh,
                        center,
                        radius +
                        data.glowExtra,
                        Animate(
                            data.glowColor,
                            position,
                            1f),
                        data.circleSegments);
                }

                AddDisc(
                    mesh,
                    center,
                    radius,
                    Animate(
                        data.coreColor,
                        position,
                        .65f),
                    data.circleSegments);
            }
        }

        private Color Animate(
            Color source,
            float position,
            float shimmerStrength)
        {
            float time =
                Time.unscaledTime;

            float pulse =
                1f +
                Mathf.Sin(
                    time * 1.18f +
                    phase * Mathf.PI * 2f) *
                .07f;

            float cursor =
                Mathf.Repeat(
                    time * .19f +
                    phase,
                    1f);

            float distance =
                Mathf.Abs(
                    position -
                    cursor);

            distance =
                Mathf.Min(
                    distance,
                    1f - distance);

            float shimmer =
                1f -
                Mathf.SmoothStep(
                    0f,
                    .24f,
                    distance);

            shimmer *=
                .28f *
                shimmerStrength;

            float gain =
                pulse +
                shimmer;

            Color result =
                source;

            result.r =
                Mathf.Clamp01(
                    source.r * gain);

            result.g =
                Mathf.Clamp01(
                    source.g * gain);

            result.b =
                Mathf.Clamp01(
                    source.b * gain);

            result.a =
                Mathf.Clamp01(
                    source.a *
                    (1f +
                     shimmer *
                     .8f));

            return result;
        }

        private static void AddDisc(
            VertexHelper mesh,
            Vector2 center,
            float radius,
            Color color,
            int segmentCount)
        {
            int segments =
                Mathf.Clamp(
                    segmentCount,
                    6,
                    24);

            int first =
                mesh.currentVertCount;

            mesh.AddVert(
                center,
                color,
                Vector2.zero);

            for (int i = 0;
                 i < segments;
                 i++)
            {
                float angle =
                    Mathf.PI *
                    2f *
                    i /
                    segments;

                mesh.AddVert(
                    center +
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)) *
                    radius,
                    color,
                    Vector2.zero);
            }

            for (int i = 0;
                 i < segments;
                 i++)
            {
                mesh.AddTriangle(
                    first,
                    first + i + 1,
                    first +
                    ((i + 1) %
                     segments) +
                    1);
            }
        }
    }
}
