using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    [Serializable]
    public sealed class HomeCustomGlowRuntimeEntry
    {
        public string stableId;
        public Sprite sprite;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector3 localScale = Vector3.one;
        public float rotationZ;
        public bool active = true;
        public Color color = Color.white;
        public bool preserveAspect;
        public int imageType;
        public bool fillCenter = true;
        public bool hazeElement;
        public float brightness = 1.08f;
        public bool pulseEnabled = true;
        public float pulseSpeed = .24f;
        public float pulseAmount = .065f;
        public bool shimmerEnabled = true;
        public float shimmerSpeed = .14f;
        public float shimmerAmount = .24f;
        public float shimmerWidth = .14f;
        public bool hazeEnabled;
        public float hazeOpacity = .16f;
        public float phaseOffset;
    }

    public sealed class HomeCustomGlowRuntimeLayout : ScriptableObject
    {
        [SerializeField] private Material materialTemplate;
        [SerializeField] private List<HomeCustomGlowRuntimeEntry> entries = new List<HomeCustomGlowRuntimeEntry>();

        public Material MaterialTemplate => materialTemplate;
        public IReadOnlyList<HomeCustomGlowRuntimeEntry> Entries => entries;

        public void Configure(Material template, List<HomeCustomGlowRuntimeEntry> value)
        {
            materialTemplate = template;
            entries = value ?? new List<HomeCustomGlowRuntimeEntry>();
        }
    }

    [RequireComponent(typeof(Image))]
    public sealed class HomeCustomGlowFx : MonoBehaviour
    {
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int ShimmerAmountId = Shader.PropertyToID("_ShimmerAmount");
        private static readonly int ShimmerSpeedId = Shader.PropertyToID("_ShimmerSpeed");
        private static readonly int ShimmerWidthId = Shader.PropertyToID("_ShimmerWidth");
        private static readonly int ShimmerAxisId = Shader.PropertyToID("_ShimmerAxis");
        private static readonly int OpacityMultiplierId = Shader.PropertyToID("_OpacityMultiplier");
        private static readonly int PhaseOffsetId = Shader.PropertyToID("_PhaseOffset");

        private Image image;
        private Material materialInstance;

        public void Initialize(Image target, Material template, HomeCustomGlowRuntimeEntry entry)
        {
            image = target;
            if (!image || !template || entry == null) return;

            materialInstance = new Material(template)
            {
                name = "ROKAS Home CustomGlow Runtime (Instance)",
                hideFlags = HideFlags.DontSave
            };
            image.material = materialInstance;

            materialInstance.SetFloat(BrightnessId, Mathf.Clamp(entry.brightness, .25f, 2f));
            materialInstance.SetFloat(PulseAmountId,
                entry.pulseEnabled && !entry.hazeElement ? Mathf.Clamp(entry.pulseAmount, 0f, .25f) : 0f);
            materialInstance.SetFloat(PulseSpeedId, Mathf.Max(.01f, entry.pulseSpeed));
            materialInstance.SetFloat(ShimmerAmountId,
                entry.shimmerEnabled && !entry.hazeElement ? Mathf.Clamp(entry.shimmerAmount, 0f, 1.5f) : 0f);
            materialInstance.SetFloat(ShimmerSpeedId, Mathf.Max(.01f, entry.shimmerSpeed));
            materialInstance.SetFloat(ShimmerWidthId, Mathf.Clamp(entry.shimmerWidth, .01f, .5f));
            materialInstance.SetFloat(ShimmerAxisId,
                Mathf.Abs(entry.sizeDelta.y) > Mathf.Abs(entry.sizeDelta.x) ? 1f : 0f);
            materialInstance.SetFloat(OpacityMultiplierId,
                entry.hazeElement ? (entry.hazeEnabled ? Mathf.Clamp01(entry.hazeOpacity) : 0f) : 1f);
            materialInstance.SetFloat(PhaseOffsetId, Mathf.Repeat(entry.phaseOffset, 1f));
        }

        private void OnDestroy()
        {
            if (!materialInstance) return;
            if (image && image.material == materialInstance) image.material = null;
            Destroy(materialInstance);
            materialInstance = null;
        }
    }

    public static class HomeOverlayRuntimePresenter
    {
        public const string ResourcesPath = "HomeOverlayRuntimeIcons";
        public static bool Enabled => true;

        public static RectTransform Build(RectTransform parent)
        {
            if (!parent) return null;
            GameObject prefab = Resources.Load<GameObject>(ResourcesPath);
            if (!prefab)
            {
                Debug.LogWarning("[ROKAS HOME] Authored PNG hotspot prefab is missing.");
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = "HomeOverlayRuntimeIcons";
            RectTransform rect = instance.transform as RectTransform;
            if (rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(.5f, .5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            return rect;
        }
    }

    public static class HomeCustomGlowRuntimePresenter
    {
        public const string ResourcesPath = "HomeCustomGlowRuntimeLayout";

        public static RectTransform Build(RectTransform parent)
        {
            if (!parent) return null;
            HomeCustomGlowRuntimeLayout layout = Resources.Load<HomeCustomGlowRuntimeLayout>(ResourcesPath);
            if (!layout || layout.MaterialTemplate == null || layout.Entries == null || layout.Entries.Count == 0)
            {
                Debug.LogWarning("[ROKAS HOME] Authored CustomGlow runtime layout is missing; old procedural outlines stay disabled.");
                return null;
            }

            GameObject rootObject = new GameObject("HomeCustomGlowRuntime", typeof(RectTransform));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(.5f, .5f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            for (int i = 0; i < layout.Entries.Count; i++)
            {
                HomeCustomGlowRuntimeEntry entry = layout.Entries[i];
                if (entry == null || !entry.active || !entry.sprite) continue;

                GameObject go = new GameObject(
                    string.IsNullOrWhiteSpace(entry.stableId) ? "GlowRuntime" : "GlowRuntime_" + entry.stableId,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HomeCustomGlowFx));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(root, false);
                rect.anchorMin = entry.anchorMin;
                rect.anchorMax = entry.anchorMax;
                rect.pivot = entry.pivot;
                rect.anchoredPosition = entry.anchoredPosition;
                rect.sizeDelta = entry.sizeDelta;
                rect.localScale = entry.localScale;
                rect.localEulerAngles = new Vector3(0f, 0f, entry.rotationZ);

                Image image = go.GetComponent<Image>();
                image.sprite = entry.sprite;
                image.color = entry.color;
                image.preserveAspect = entry.preserveAspect;
                image.raycastTarget = false;
                image.type = Enum.IsDefined(typeof(Image.Type), entry.imageType)
                    ? (Image.Type)entry.imageType
                    : Image.Type.Simple;
                image.fillCenter = entry.fillCenter;

                go.GetComponent<HomeCustomGlowFx>().Initialize(image, layout.MaterialTemplate, entry);
            }

            return root;
        }
    }
}