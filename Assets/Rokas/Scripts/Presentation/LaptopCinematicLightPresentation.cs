using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// Shares the actual GameSession lamp/hallway states without owning new
    /// switches or changing the approved laptop POV bitmap. The room and the
    /// left visible hallway have separate masks in LaptopCinematicLightStates.
    /// </summary>
    public sealed class LaptopCinematicLightPresentation : IDisposable
    {
        public const string ShaderResource = "Shaders/LaptopCinematicLightStates";
        public const string OffPhotoResource = "LaptopCinematic/LaptopPOV_lights_off_APPROVED";
        public const int ReferenceWidth = 1672;
        public const int ReferenceHeight = 941;
        private static readonly int OffTexProperty = Shader.PropertyToID("_OffTex");
        private static readonly int OffReadyProperty = Shader.PropertyToID("_OffPhotoReady");
        private static readonly int RoomProperty = Shader.PropertyToID("_RoomLightOn");
        private static readonly int HallProperty = Shader.PropertyToID("_HallwayLightOn");
        private static readonly int ScreenProperty = Shader.PropertyToID("_ScreenGlow");
        private static readonly int BounceTintProperty = Shader.PropertyToID("_ScreenBounceTint");
        private static readonly int BounceAmountProperty = Shader.PropertyToID("_ScreenBounceAmount");
        private static readonly int PackedMaskProperty = Shader.PropertyToID("_BounceMasksTex");
        private static readonly int BloomMaskProperty = Shader.PropertyToID("_BloomMasksTex");
        private static readonly int BloomAmountProperty = Shader.PropertyToID("_ScreenBloomAmount");
        private static readonly int SpecAmountProperty = Shader.PropertyToID("_ScreenSpecAmount");
        private static readonly int SideBiasProperty = Shader.PropertyToID("_ScreenBounceSide");
        private static readonly int BloomThresholdProperty = Shader.PropertyToID("_BloomThreshold");
        private static readonly int BloomKneeProperty = Shader.PropertyToID("_BloomSoftKnee");
        public const string MaskResource = "LaptopCinematic/LightMasks/HybridScreenBounceMasks";
        public const string BloomMaskResource = "LaptopCinematic/LightMasks/HybridCinematicBloomMasks";

        private readonly Func<bool> roomLight;
        private readonly Func<bool> hallwayLight;
        private readonly Func<bool> laptopScreenPowered;
        private Material material;
        private RawImage background;
        private Texture2D offPhoto;
        private Texture2D packedMasks;
        private Texture2D cinematicBloomMasks;
        private float bloomAmount;
        private float specAmount;
        private float bounceSide;
        private Color bounceTint = new Color(.20f, .22f, .25f, 1f);
        private float bounceAmount;
        private float roomValue;
        private float hallValue;
        private float screenValue;

        public Material Material => material;
        public bool HasOffPhoto => offPhoto != null;
        public bool HasCinematicMasks => packedMasks && cinematicBloomMasks;
        public float RoomValue => roomValue;
        public float HallwayValue => hallValue;
        public float ScreenValue => screenValue;

        public LaptopCinematicLightPresentation(
            Func<bool> roomLight, Func<bool> hallwayLight,
            Func<bool> laptopScreenPowered = null)
        {
            this.roomLight = roomLight;
            this.hallwayLight = hallwayLight;
            this.laptopScreenPowered = laptopScreenPowered;
        }

        /// <summary>Initializes the photo to the ALREADY SAVED light state
        /// before the very first POV frame; prevents warm/cold one-frame flash.</summary>
        public void Bind(RawImage source)
        {
            Dispose();
            if (!source) return;
            Shader shader = Resources.Load<Shader>(ShaderResource);
            if (!shader)
            {
                Debug.LogError("ROKAS laptop lighting: missing Resources/" +
                    ShaderResource + " shader; original POV remains visible.");
                return;
            }

            offPhoto = Resources.Load<Texture2D>(OffPhotoResource);
            bool photoValid = offPhoto &&
                offPhoto.width == ReferenceWidth && offPhoto.height == ReferenceHeight;
            if (!photoValid)
            {
                Debug.LogError("ROKAS laptop lighting: REQUIRED original 1672x941 " +
                    "photographic OFF PNG is missing or incorrectly sized at Resources/" +
                    OffPhotoResource + ". No fake darkening is applied. " +
                    "Install the provided photo first; preserving original image.");
                offPhoto = null;
                return;
            }

            if (!source.texture ||
                source.texture.width != ReferenceWidth ||
                source.texture.height != ReferenceHeight)
            {
                Debug.LogError("ROKAS laptop lighting: source ON photograph must be " +
                    "exactly 1672x941, matching the supplied OFF image.");
                return;
            }

            material = new Material(shader)
            {
                name = "ROKAS-Laptop-Two-Zone-Lighting",
                hideFlags = HideFlags.DontSave
            };
            material.SetTexture(OffTexProperty, offPhoto);
            material.SetFloat(OffReadyProperty, 1f);
            packedMasks = Resources.Load<Texture2D>(MaskResource);
            cinematicBloomMasks = Resources.Load<Texture2D>(BloomMaskResource);
            if (packedMasks) material.SetTexture(PackedMaskProperty, packedMasks);
            if (cinematicBloomMasks)
                material.SetTexture(BloomMaskProperty, cinematicBloomMasks);
            if (!packedMasks || !cinematicBloomMasks)
                Debug.LogError("ROKAS V6: photo-aligned screen masks missing: " +
                    MaskResource + " / " + BloomMaskResource +
                    ". Room/corridor remain active; all screen bounce disabled.");
            // These inspector properties mirror the *actual* CPU highlight
            // extraction settings. They do not trigger a second bloom on UI.
            material.SetFloat(BloomThresholdProperty,
                LaptopScreenBounceEstimator.BloomThreshold);
            material.SetFloat(BloomKneeProperty,
                LaptopScreenBounceEstimator.BloomSoftKnee);
            background = source;
            Snap();
            background.material = material;
            background.SetMaterialDirty();
        }

        /// <summary>Color and strength estimated from the real powered LCD.
        /// Kept separate from both physical lamp switches.</summary>
        public void SetScreenBounce(Color color, float intensity,
            float bloom = 0f, float specular = 0f, float sideBias = 0f)
        {
            bounceTint = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b), 1f);
            bounceAmount = Mathf.Clamp01(intensity);
            bloomAmount = Mathf.Clamp01(bloom);
            specAmount = Mathf.Clamp01(specular);
            bounceSide = Mathf.Clamp(sideBias, -.3f, .3f);
            WriteMaterial();
        }

        public void Snap()
        {
            roomValue = roomLight != null && roomLight() ? 1f : 0f;
            hallValue = hallwayLight != null && hallwayLight() ? 1f : 0f;
            screenValue = laptopScreenPowered != null && laptopScreenPowered() ? 1f : 0f;
            WriteMaterial();
        }

        public void Tick(float deltaTime)
        {
            if (!material) return;
            float roomTarget = roomLight != null && roomLight() ? 1f : 0f;
            float hallTarget = hallwayLight != null && hallwayLight() ? 1f : 0f;
            float screenTarget = laptopScreenPowered != null && laptopScreenPowered() ? 1f : 0f;

            // Smooth only real changes while the POV is present; Bind/Snap
            // applies an initial or resumed session state without delay.
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / .18f);
            roomValue = Mathf.Lerp(roomValue, roomTarget, t);
            hallValue = Mathf.Lerp(hallValue, hallTarget, t);
            screenValue = Mathf.Lerp(screenValue, screenTarget, t);
            if (Mathf.Abs(roomValue - roomTarget) <= .001f) roomValue = roomTarget;
            if (Mathf.Abs(hallValue - hallTarget) <= .001f) hallValue = hallTarget;
            if (Mathf.Abs(screenValue - screenTarget) <= .001f) screenValue = screenTarget;
            WriteMaterial();
        }

        private void WriteMaterial()
        {
            if (!material) return;
            material.SetFloat(RoomProperty, roomValue);
            material.SetFloat(HallProperty, hallValue);
            material.SetFloat(ScreenProperty, screenValue);
            material.SetColor(BounceTintProperty, bounceTint);
            bool masksReady = packedMasks && cinematicBloomMasks;
            material.SetFloat(BounceAmountProperty,
                masksReady ? bounceAmount : 0f);
            material.SetFloat(BloomAmountProperty,
                masksReady ? bloomAmount : 0f);
            material.SetFloat(SpecAmountProperty,
                masksReady ? specAmount : 0f);
            material.SetFloat(SideBiasProperty,
                masksReady ? bounceSide : 0f);
        }

        public void Dispose()
        {
            if (background && material && background.material == material)
            {
                background.material = null;
                background.SetMaterialDirty();
            }
            if (material)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(material);
                else UnityEngine.Object.DestroyImmediate(material);
            }
            material = null;
            background = null;
            offPhoto = null;
            packedMasks = null;
            cinematicBloomMasks = null;
            bounceAmount = 0f;
            bloomAmount = 0f;
            specAmount = 0f;
            bounceSide = 0f;
        }
    }
}
