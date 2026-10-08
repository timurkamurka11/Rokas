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

                // Attach subtle synchronized micro-animation to all main interaction icons.
                if (data.id == "Hotspot_Window" ||
                    data.id == "Hotspot_Door" ||
                    data.id == "Hotspot_Laptop" ||
                    data.id == "Hotspot_FloorLamp" ||
                    data.id == "Hotspot_Cat")
                {
                    var iconFx = go.AddComponent<HomeInteractionIconFx>();

                    // Bind to the underlying scene hotspot so we can react to its hover events
                    string targetName = data.id.Replace("Hotspot_", "");
                    if (targetName == "Cat") targetName = "Mame";
                    if (targetName == "FloorLamp") targetName = "Lamp";
                    targetName += "Hotspot";

                    GameObject bgHitTarget = null;
                    Transform hitTrans = parent.Find(targetName);
                    if (hitTrans != null) bgHitTarget = hitTrans.gameObject;

                    if (bgHitTarget != null)
                    {
                        var reporter = bgHitTarget.AddComponent<HoverReporter>();
                        reporter.iconFx = iconFx;
                        // Share the existing gameplay hotspot hover events with
                        // its matching outline; do not create a second collider.
                        if (targetName == "DoorHotspot" || targetName == "LaptopHotspot")
                        {
                            Transform outline = outlineRoot.Find("Outline_" +
                                (targetName == "DoorHotspot" ? "Door" : "Laptop"));
                            if (outline) reporter.outlineFx =
                                outline.GetComponent<HomeFinalOutlineGraphic>();
                        }
                    }
                }
            }

            // --- Window atmospheric FX layer ---
            // Placed above outlines/connectors/icons; below gameplay hit targets.
            BuildWindowAtmosphere(root);

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

        private static void BuildWindowAtmosphere(
            RectTransform parent)
        {
            // Sparse wet-glass rain streaks catching city light
            GameObject streaksGo =
                new GameObject(
                    "WindowRainStreaks",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(HomeWindowRainStreaksGraphic));

            RectTransform streaksRect =
                streaksGo.GetComponent<RectTransform>();

            streaksRect.SetParent(parent, false);
            streaksRect.anchorMin = Vector2.zero;
            streaksRect.anchorMax = Vector2.one;
            streaksRect.pivot = new Vector2(.5f, .5f);
            streaksRect.offsetMin = Vector2.zero;
            streaksRect.offsetMax = Vector2.zero;

            HomeWindowRainStreaksGraphic streaks =
                streaksGo.GetComponent<HomeWindowRainStreaksGraphic>();

            streaks.raycastTarget = false;

            // Occasional tasteful specular glints on the glass panes
            GameObject glintsGo =
                new GameObject(
                    "WindowSpecularGlints",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(HomeWindowSpecularGlintsGraphic));

            RectTransform glintsRect =
                glintsGo.GetComponent<RectTransform>();

            glintsRect.SetParent(parent, false);
            glintsRect.anchorMin = Vector2.zero;
            glintsRect.anchorMax = Vector2.one;
            glintsRect.pivot = new Vector2(.5f, .5f);
            glintsRect.offsetMin = Vector2.zero;
            glintsRect.offsetMax = Vector2.zero;

            HomeWindowSpecularGlintsGraphic glints =
                glintsGo.GetComponent<HomeWindowSpecularGlintsGraphic>();

            glints.raycastTarget = false;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HomeFinalOutlineGraphic :
        MaskableGraphic
    {
        private HomeFinalOutline data;
        private float phase;
        private bool livingGold;
        private bool laptopGold;
        private float hoverStrength;
        public bool Hovered { get; set; }

        public void Configure(HomeFinalOutline source)
        {
            data = source;
            livingGold = source.id == "Outline_Door" || source.id == "Outline_Laptop";
            laptopGold = source.id == "Outline_Laptop";
            // Identical phase after scene recreation; Cat retains its old style.
            phase = livingGold ? (laptopGold ? .57f : .13f) :
                Mathf.Repeat(Mathf.Abs(GetInstanceID()) * .000173f, 1f);

            SetVerticesDirty();
        }

        private void Update()
        {
            if (livingGold && Application.isPlaying)
            {
                float smoothing = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
                hoverStrength = Mathf.Lerp(hoverStrength, Hovered ? 1f : 0f, smoothing);
            }
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            Hovered = false;
            hoverStrength = 0f;
            base.OnDisable();
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
            if (livingGold)
            {
                StrokeLivingGold(mesh, thickness, source, shimmerStrength);
                return;
            }
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

        // Walk the actual authored polygon perimeter. Short vertex-colored
        // segments give a continuous glint instead of blinking whole edges.
        // The original points, pivot, hit targets and light mask never move.
        private void StrokeLivingGold(VertexHelper mesh, float thickness,
            Color source, float strength)
        {
            int edges = data.points.Length - 1 +
                (data.closed && data.points.Length > 2 ? 1 : 0);
            float perimeter = 0f;
            for (int i = 0; i < edges; i++)
                perimeter += Vector2.Distance(data.points[i],
                    data.points[(i + 1) % data.points.Length]);
            if (perimeter < .001f) return;

            float time = Application.isPlaying ? Time.unscaledTime : 0f;
            // Breathing is confined to width (max 2.2%); contour coordinates
            // remain pixel-identical to the current Door and Laptop assets.
            float breath = 1f + Mathf.Sin(time * (laptopGold ? 1.73f : 1.21f) +
                phase * Mathf.PI * 2f) * .022f;
            float width = thickness * breath * (1f + hoverStrength * .025f);
            float distanceAlong = 0f;
            for (int i = 0; i < edges; i++)
            {
                Vector2 a = Map(data.points[i]);
                Vector2 b = Map(data.points[(i + 1) % data.points.Length]);
                float length = Vector2.Distance(a, b);
                if (length < .001f) continue;
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / 12f));
                for (int k = 0; k < steps; k++)
                {
                    float begin = (float)k / steps, end = (float)(k + 1) / steps;
                    Color startColor = LivingColor(source,
                        (distanceAlong + length * begin) / perimeter, strength, time);
                    Color endColor = LivingColor(source,
                        (distanceAlong + length * end) / perimeter, strength, time);
                    AddGradientSegment(mesh, Vector2.Lerp(a, b, begin),
                        Vector2.Lerp(a, b, end), width, startColor, endColor);
                }
                distanceAlong += length;
            }
        }

        private Color LivingColor(Color source, float location, float strength, float time)
        {
            float cursor = Mathf.Repeat(time * (laptopGold ? .175f : .115f) + phase, 1f);
            float delta = Mathf.Abs(Mathf.Repeat(location - cursor + .5f, 1f) - .5f);
            float band = 1f - Mathf.SmoothStep(0f, laptopGold ? .083f : .115f, delta);
            float pulse = Mathf.Sin(time * (laptopGold ? 1.73f : 1.21f) +
                phase * Mathf.PI * 2f) * .075f;
            // No frame-random flicker. Micro movement is in luminosity only.
            float micro = Mathf.Sin(time * 3.7f + location * 29f +
                phase * Mathf.PI * 2f) * .012f;
            float sweep = band * (laptopGold ? .47f : .38f) * strength;
            float gain = (1f + pulse + micro + sweep) * (1f + hoverStrength * .14f);
            Color result = source;
            result.r = Mathf.Clamp01(source.r * gain);
            result.g = Mathf.Clamp01(source.g * gain);
            result.b = Mathf.Clamp01(source.b * gain);
            result.a = Mathf.Clamp01(source.a *
                (1f + pulse * .7f + sweep * .8f + hoverStrength * .12f));
            return result;
        }

        private static void AddGradientSegment(VertexHelper mesh, Vector2 a, Vector2 b,
            float thickness, Color start, Color end)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < .0001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized *
                thickness * .5f;
            int first = mesh.currentVertCount;
            mesh.AddVert(a + normal, start, Vector2.zero);
            mesh.AddVert(a - normal, start, Vector2.zero);
            mesh.AddVert(b + normal, end, Vector2.zero);
            mesh.AddVert(b - normal, end, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 1, first + 3);
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

    /// <summary>
    /// Subtle, pane-localized wet-glass rain streaks.
    /// Draws thin, slightly slanted, elongated soft reflections inside the glass panes.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HomeWindowRainStreaksGraphic : MaskableGraphic
    {
        // Actual glass panes, avoiding black structural mullions and curtains.
        private static readonly Rect Pane1 = new Rect(402f, 38f, 210f, 510f);
        private static readonly Rect Pane2 = new Rect(628f, 38f, 216f, 510f);
        private static readonly Rect Pane3 = new Rect(860f, 38f, 140f, 510f);

        private float phase;

        protected override void Awake()
        {
            base.Awake();
            phase = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * .000317f, 1f);
        }

        private void Update() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float time = Time.unscaledTime;

            // Draw a few very thin, elongated rain streaks inside the panes.
            DrawRainStreak(mesh, rect, Pane1, time, phase + 0.15f, new Color(.75f, .90f, 1f));
            DrawRainStreak(mesh, rect, Pane2, time, phase + 0.55f, new Color(.80f, .95f, 1f));
            DrawRainStreak(mesh, rect, Pane3, time, phase + 0.85f, new Color(.70f, .88f, 1f));
        }

        private void DrawRainStreak(VertexHelper mesh, Rect baseRect, Rect pane, float time, float offset, Color color)
        {
            // Streaks fade in and out, and drift slowly downwards
            float cycle = Mathf.Repeat(time * 0.15f + offset, 1f);
            float alpha = Mathf.Sin(cycle * Mathf.PI) * .08f; // Very soft, max 8% alpha

            if (alpha < 0.001f) return;

            float nx = Mathf.PerlinNoise(offset * 10f, 0f);
            float cx = Mathf.Lerp(pane.xMin + 20f, pane.xMax - 20f, nx);

            // Downward drift
            float cy = Mathf.Lerp(pane.yMax - 50f, pane.yMin + 50f, cycle);

            Color cPeak = new Color(color.r, color.g, color.b, alpha);
            Color cFade = new Color(color.r, color.g, color.b, 0f);

            // Thin, elongated diamond (streak)
            float width = 3f;
            float height = 120f;
            float slant = 10f; // Slight slant to match rain/perspective

            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(baseRect.xMin + cx, baseRect.yMax - cy), cPeak, Vector2.zero); // Center
            mesh.AddVert(new Vector3(baseRect.xMin + cx + slant, baseRect.yMax - (cy - height)), cFade, Vector2.zero); // Top
            mesh.AddVert(new Vector3(baseRect.xMin + cx + width, baseRect.yMax - cy), cFade, Vector2.zero); // Right
            mesh.AddVert(new Vector3(baseRect.xMin + cx - slant, baseRect.yMax - (cy + height)), cFade, Vector2.zero); // Bottom
            mesh.AddVert(new Vector3(baseRect.xMin + cx - width, baseRect.yMax - cy), cFade, Vector2.zero); // Left

            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
            mesh.AddTriangle(first, first + 3, first + 4);
            mesh.AddTriangle(first, first + 4, first + 1);
        }
    }

    /// <summary>
    /// Occasional specular glints catching city lights on the wet glass.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HomeWindowSpecularGlintsGraphic : MaskableGraphic
    {
        private static readonly Rect Pane1 = new Rect(402f, 38f, 210f, 510f);
        private static readonly Rect Pane2 = new Rect(628f, 38f, 216f, 510f);
        private static readonly Rect Pane3 = new Rect(860f, 38f, 140f, 510f);

        private float phase;

        protected override void Awake()
        {
            base.Awake();
            phase = Mathf.Repeat(Mathf.Abs(GetInstanceID()) * .000211f, 1f);
        }

        private void Update() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float time = Time.unscaledTime;

            // Occasional glint in Pane 1 (every ~7s)
            DrawGlintEvent(mesh, rect, Pane1, time, 0.14f, phase + 0.1f, new Color(.7f, .95f, 1f));

            // Occasional glint in Pane 2 (every ~9s)
            DrawGlintEvent(mesh, rect, Pane2, time, 0.11f, phase + 0.4f, new Color(.85f, .98f, 1f));

            // Occasional glint in Pane 3 (every ~11s)
            DrawGlintEvent(mesh, rect, Pane3, time, 0.09f, phase + 0.7f, new Color(.8f, .95f, 1f));
        }

        private void DrawGlintEvent(VertexHelper mesh, Rect baseRect, Rect pane, float time, float frequency, float offset, Color color)
        {
            float cycle = Mathf.Repeat(time * frequency + offset, 1f);

            // Glint only visible during the middle 20% of its cycle
            if (cycle > 0.4f && cycle < 0.6f)
            {
                float localTime = (cycle - 0.4f) * 5f; // 0 to 1
                float alpha = Mathf.Sin(localTime * Mathf.PI) * .15f; // Max 15% opacity

                if (alpha < 0.001f) return;

                // Position is pseudo-randomized based on the offset
                float nx = Mathf.PerlinNoise(offset * 13f, 0f);
                float ny = Mathf.PerlinNoise(0f, offset * 13f);

                float cx = Mathf.Lerp(pane.xMin + 30f, pane.xMax - 30f, nx);
                float cy = Mathf.Lerp(pane.yMin + 100f, pane.yMax - 100f, ny);

                // Subtle drift
                cx += (localTime - 0.5f) * 15f;
                cy += (localTime - 0.5f) * 10f;

                Color cPeak = new Color(color.r, color.g, color.b, alpha);
                Color cFade = new Color(color.r, color.g, color.b, 0f);

                float size = 40f;

                int first = mesh.currentVertCount;
                mesh.AddVert(new Vector3(baseRect.xMin + cx, baseRect.yMax - cy), cPeak, Vector2.zero); // Center
                mesh.AddVert(new Vector3(baseRect.xMin + cx, baseRect.yMax - (cy - size)), cFade, Vector2.zero); // Top
                mesh.AddVert(new Vector3(baseRect.xMin + cx + size, baseRect.yMax - cy), cFade, Vector2.zero); // Right
                mesh.AddVert(new Vector3(baseRect.xMin + cx, baseRect.yMax - (cy + size)), cFade, Vector2.zero); // Bottom
                mesh.AddVert(new Vector3(baseRect.xMin + cx - size, baseRect.yMax - cy), cFade, Vector2.zero); // Left

                mesh.AddTriangle(first, first + 1, first + 2);
                mesh.AddTriangle(first, first + 2, first + 3);
                mesh.AddTriangle(first, first + 3, first + 4);
                mesh.AddTriangle(first, first + 4, first + 1);
            }
        }
    }

    /// <summary>
    /// Micro-animation for all Hub interaction icons.
    /// Synchronized 2.5px float and alpha breathing.
    /// Supports laptop-style hover emphasis.
    /// </summary>
    internal sealed class HomeInteractionIconFx : MonoBehaviour
    {
        private RectTransform rect;
        private Image image;
        private Vector2 origin;
        private bool initialized;

        public bool Hovered;
        private float hoverState; // 0 to 1 smooth transition

        private void Start()
        {
            rect = transform as RectTransform;
            image = GetComponent<Image>();
            if (rect != null)
                origin = rect.anchoredPosition;
            initialized = rect != null;
        }

        private void Update()
        {
            if (!initialized) return;
            float time = Time.unscaledTime;

            // Smoothly blend into hover state
            float dt = Time.unscaledDeltaTime;
            float speed = 1f - Mathf.Exp(-14f * dt);
            hoverState = Mathf.Lerp(hoverState, Hovered ? 1f : 0f, speed);

            // Vertical float: Idle sine wave overrides to a static lift on hover
            float idleFloat = Mathf.Sin(time * 1.8f) * 2.5f;
            float hoverLift = 6f; // Lift amount
            float finalY = Mathf.Lerp(idleFloat, hoverLift, hoverState);

            rect.anchoredPosition = origin + new Vector2(0f, finalY);

            // Scale emphasis like laptop
            float scale = Mathf.Lerp(1f, 1.055f, hoverState);
            rect.localScale = new Vector3(scale, scale, 1f);

            // Alpha and color breathing:
            // Idle breathes ±15%. Hover pulses rapidly like laptop attention glow.
            if (image != null)
            {
                float idleBreath = 0.85f + Mathf.Sin(time * 2.2f + .5f) * .15f;
                float activePulse = 0.90f + Mathf.Sin(time * 4.4f) * 0.10f;

                float finalAlpha = Mathf.Lerp(idleBreath, activePulse, hoverState);

                // Color emphasis: slightly brighter/golden on hover
                Color idleColor = Color.white;
                Color hoverColor = new Color(1f, 0.95f, 0.8f);
                Color finalColor = Color.Lerp(idleColor, hoverColor, hoverState);

                finalColor.a = Mathf.Clamp01(finalAlpha);
                image.color = finalColor;
            }
        }

        private void OnDisable()
        {
            Hovered = false;
            hoverState = 0f;
            if (initialized && rect != null)
            {
                rect.anchoredPosition = origin;
                rect.localScale = Vector3.one;
            }
        }
    }

    /// <summary>
    /// Attaches to background gameplay hit targets to relay hover state to the UI icon.
    /// </summary>
    internal sealed class HoverReporter : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public HomeInteractionIconFx iconFx;
        public HomeFinalOutlineGraphic outlineFx;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
        {
            if (iconFx) iconFx.Hovered = true;
            if (outlineFx) outlineFx.Hovered = true;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
        {
            if (iconFx) iconFx.Hovered = false;
            if (outlineFx) outlineFx.Hovered = false;
        }

        private void OnDisable()
        {
            if (iconFx) iconFx.Hovered = false;
            if (outlineFx) outlineFx.Hovered = false;
        }
    }
}
