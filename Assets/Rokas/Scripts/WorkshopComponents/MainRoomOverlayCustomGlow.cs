using UnityEngine;
using UnityEngine.UI;

namespace Rokas.EditorTools
{
    [ExecuteAlways]
    public sealed class MainRoomOverlayCustomGlowElement : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private bool hazeElement;

        public string StableId
        {
            get => stableId;
            set => stableId = value;
        }

        public bool HazeElement
        {
            get => hazeElement;
            set => hazeElement = value;
        }
    }

    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public sealed class MainRoomOverlayGlowFx : MonoBehaviour
    {
        [SerializeField, Range(.25f, 2f)] private float brightness = 1f;
        [SerializeField] private bool pulseEnabled;
        [SerializeField, Range(0f, .25f)] private float pulseAmount = .07f;
        [SerializeField, Min(.01f)] private float pulseSpeed = .32f;
        [SerializeField] private bool shimmerEnabled;
        [SerializeField, Range(0f, 1.5f)] private float shimmerAmount = .22f;
        [SerializeField, Min(.01f)] private float shimmerSpeed = .18f;
        [SerializeField, Range(.01f, .5f)] private float shimmerWidth = .10f;
        [SerializeField] private bool hazeEnabled;
        [SerializeField, Range(0f, 1f)] private float hazeOpacity = .16f;
        [SerializeField, Range(0f, 1f)] private float phaseOffset;

        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int ShimmerAmountId = Shader.PropertyToID("_ShimmerAmount");
        private static readonly int ShimmerSpeedId = Shader.PropertyToID("_ShimmerSpeed");
        private static readonly int ShimmerWidthId = Shader.PropertyToID("_ShimmerWidth");
        private static readonly int OpacityMultiplierId = Shader.PropertyToID("_OpacityMultiplier");
        private static readonly int PhaseOffsetId = Shader.PropertyToID("_PhaseOffset");

        private Image image;
        private Material materialInstance;

        public float Brightness { get => brightness; set { brightness = Mathf.Clamp(value, .25f, 2f); ApplyProperties(); } }
        public bool PulseEnabled { get => pulseEnabled; set { pulseEnabled = value; ApplyProperties(); } }
        public float PulseAmount { get => pulseAmount; set { pulseAmount = Mathf.Clamp(value, 0f, .25f); ApplyProperties(); } }
        public float PulseSpeed { get => pulseSpeed; set { pulseSpeed = Mathf.Max(.01f, value); ApplyProperties(); } }
        public bool ShimmerEnabled { get => shimmerEnabled; set { shimmerEnabled = value; ApplyProperties(); } }
        public float ShimmerAmount { get => shimmerAmount; set { shimmerAmount = Mathf.Clamp(value, 0f, 1.5f); ApplyProperties(); } }
        public float ShimmerSpeed { get => shimmerSpeed; set { shimmerSpeed = Mathf.Max(.01f, value); ApplyProperties(); } }
        public float ShimmerWidth { get => shimmerWidth; set { shimmerWidth = Mathf.Clamp(value, .01f, .5f); ApplyProperties(); } }
        public bool HazeEnabled { get => hazeEnabled; set { hazeEnabled = value; ApplyProperties(); } }
        public float HazeOpacity { get => hazeOpacity; set { hazeOpacity = Mathf.Clamp01(value); ApplyProperties(); } }
        public float PhaseOffset { get => phaseOffset; set { phaseOffset = Mathf.Repeat(value, 1f); ApplyProperties(); } }

        private void OnEnable()
        {
            EnsureMaterial();
            ApplyProperties();
        }

        private void OnDisable()
        {
            ReleaseMaterial();
        }

        private void OnDestroy()
        {
            ReleaseMaterial();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            brightness = Mathf.Clamp(brightness, .25f, 2f);
            pulseAmount = Mathf.Clamp(pulseAmount, 0f, .25f);
            pulseSpeed = Mathf.Max(.01f, pulseSpeed);
            shimmerAmount = Mathf.Clamp(shimmerAmount, 0f, 1.5f);
            shimmerSpeed = Mathf.Max(.01f, shimmerSpeed);
            shimmerWidth = Mathf.Clamp(shimmerWidth, .01f, .5f);
            hazeOpacity = Mathf.Clamp01(hazeOpacity);
            phaseOffset = Mathf.Repeat(phaseOffset, 1f);
            EnsureMaterial();
            ApplyProperties();
        }
#endif

        private void EnsureMaterial()
        {
            if (!image) image = GetComponent<Image>();
            if (!image) return;

            Shader shader = Shader.Find("ROKAS/Workshop/GlowShimmer");
            if (!shader) return;

            if (materialInstance && materialInstance.shader == shader)
            {
                if (image.material != materialInstance) image.material = materialInstance;
                return;
            }

            ReleaseMaterial();
            materialInstance = new Material(shader)
            {
                name = "ROKAS Glow Preview (Instance)",
                hideFlags = HideFlags.HideAndDontSave
            };
            image.material = materialInstance;
        }

        private void ApplyProperties()
        {
            if (!materialInstance) return;

            bool isHaze = false;
            MainRoomOverlayCustomGlowElement marker = GetComponent<MainRoomOverlayCustomGlowElement>();
            if (marker) isHaze = marker.HazeElement;

            materialInstance.SetFloat(BrightnessId, brightness);
            materialInstance.SetFloat(PulseAmountId, pulseEnabled ? pulseAmount : 0f);
            materialInstance.SetFloat(PulseSpeedId, pulseSpeed);
            materialInstance.SetFloat(ShimmerAmountId, shimmerEnabled ? shimmerAmount : 0f);
            materialInstance.SetFloat(ShimmerSpeedId, shimmerSpeed);
            materialInstance.SetFloat(ShimmerWidthId, shimmerWidth);
            materialInstance.SetFloat(OpacityMultiplierId, isHaze ? (hazeEnabled ? hazeOpacity : 0f) : 1f);
            materialInstance.SetFloat(PhaseOffsetId, phaseOffset);
        }

        private void ReleaseMaterial()
        {
            if (!materialInstance) return;
            if (image && image.material == materialInstance) image.material = null;
            if (Application.isPlaying) Destroy(materialInstance);
            else DestroyImmediate(materialInstance);
            materialInstance = null;
        }
    }
}
