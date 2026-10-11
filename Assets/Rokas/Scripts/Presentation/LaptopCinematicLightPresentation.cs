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

        private readonly Func<bool> roomLight;
        private readonly Func<bool> hallwayLight;
        private Material material;
        private RawImage background;
        private Texture2D offPhoto;
        private float roomValue;
        private float hallValue;

        public Material Material => material;
        public bool HasOffPhoto => offPhoto != null;
        public float RoomValue => roomValue;
        public float HallwayValue => hallValue;

        public LaptopCinematicLightPresentation(
            Func<bool> roomLight, Func<bool> hallwayLight)
        {
            this.roomLight = roomLight;
            this.hallwayLight = hallwayLight;
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
            background = source;
            Snap();
            background.material = material;
            background.SetMaterialDirty();
        }

        public void Snap()
        {
            roomValue = roomLight != null && roomLight() ? 1f : 0f;
            hallValue = hallwayLight != null && hallwayLight() ? 1f : 0f;
            WriteMaterial();
        }

        public void Tick(float deltaTime)
        {
            if (!material) return;
            float roomTarget = roomLight != null && roomLight() ? 1f : 0f;
            float hallTarget = hallwayLight != null && hallwayLight() ? 1f : 0f;

            // Smooth only real changes while the POV is present; Bind/Snap
            // applies an initial or resumed session state without delay.
            float t = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / .18f);
            roomValue = Mathf.Lerp(roomValue, roomTarget, t);
            hallValue = Mathf.Lerp(hallValue, hallTarget, t);
            if (Mathf.Abs(roomValue - roomTarget) <= .001f) roomValue = roomTarget;
            if (Mathf.Abs(hallValue - hallTarget) <= .001f) hallValue = hallTarget;
            WriteMaterial();
        }

        private void WriteMaterial()
        {
            if (!material) return;
            material.SetFloat(RoomProperty, roomValue);
            material.SetFloat(HallProperty, hallValue);
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
        }
    }
}
