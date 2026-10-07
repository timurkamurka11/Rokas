using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Covers the fixed four-point artifact baked into the launch/menu video pixels.
    // It clones a nearby area from the same live RenderTexture and feathers the patch
    // so no hard rectangle is introduced.
    public sealed class VideoCornerArtifactMask : MonoBehaviour
    {
        public static readonly Vector2 NormalizedAnchor = new Vector2(.895f, .20f);
        public static readonly Vector2 ReferenceSize = new Vector2(190f, 190f);
        public static readonly Vector4 SourceRect = new Vector4(.7675f, .1120f, .0990f, .1760f);

        private Material ownedMaterial;
        private RawImage image;

        public RawImage Image => image;
        public Vector4 SampleRect => SourceRect;

        public static VideoCornerArtifactMask Create(RectTransform parent, Texture source)
        {
            if (!parent || !source) return null;

            var go = new GameObject(
                "VideoCornerArtifactMask",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(VideoCornerArtifactMask));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = NormalizedAnchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = ReferenceSize;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            var component = go.GetComponent<VideoCornerArtifactMask>();
            component.Configure(source);
            rect.SetAsLastSibling();
            return component;
        }

        private void Configure(Texture source)
        {
            image = GetComponent<RawImage>();
            image.texture = source;
            image.color = Color.white;
            image.raycastTarget = false;

            Shader shader = Resources.Load<Shader>("Shaders/VideoCornerCloneMask");
            if (!shader)
            {
                Debug.LogWarning("ROKAS video corner artifact shader is missing; mask disabled.");
                gameObject.SetActive(false);
                return;
            }

            ownedMaterial = new Material(shader)
            {
                name = "ROKAS Video Corner Artifact Mask",
                hideFlags = HideFlags.HideAndDontSave
            };
            ownedMaterial.SetVector("_SourceRect", SourceRect);
            ownedMaterial.SetFloat("_InnerRadius", .39f);
            ownedMaterial.SetFloat("_OuterRadius", .96f);
            image.material = ownedMaterial;
        }

        private void OnDestroy()
        {
            if (!ownedMaterial) return;
            if (Application.isPlaying) Destroy(ownedMaterial);
            else DestroyImmediate(ownedMaterial);
            ownedMaterial = null;
        }
    }
}
