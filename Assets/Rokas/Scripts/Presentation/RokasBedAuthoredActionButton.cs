using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// Keeps the existing UiKit Button, its OnClick, interactability and
    /// keyboard navigation. Replaces ONLY its visuals with the original
    /// transparent user PNG. All hover motion is UI-only, not gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RokasBedAuthoredActionButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private RectTransform rect;
        private Button action;
        private Image authored;
        private Sprite ownedSprite;
        private bool hovering, selected, pressing;
        private float blend;
        private Vector2 restPosition;
        private Vector3 restScale;

        public static bool Apply(Button button, string resourceName)
        {
            if (button == null) return false;
            Texture2D texture = Resources.Load<Texture2D>(
                "BedSleep/UI/" + resourceName);
            if (texture == null)
            {
                Debug.LogWarning("[ROKAS Bed UI] Missing authored PNG " +
                    resourceName + ", preserving original interactive button.");
                return false;
            }

            Image rootImage = button.GetComponent<Image>();
            if (rootImage == null) return false;
            rootImage.color = new Color(1f, 1f, 1f, .001f);
            rootImage.raycastTarget = true;
            button.transition = Selectable.Transition.None;
            button.targetGraphic = rootImage;
            var genericMotion = button.GetComponent<InteractionFeedback>();
            if (genericMotion != null) genericMotion.enabled = false;
            // Keep original Text and other content as inert hidden children,
            // so button behavior, navigation and dynamic caption can recover.
            foreach (string part in new[] { "TopRule", "LeftRule", "Title" })
            {
                Transform t = button.transform.Find(part);
                if (t != null) t.gameObject.SetActive(false);
            }

            var imageGo = new GameObject("ROKAS_Original_" + resourceName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageGo.transform.SetParent(button.transform, false);
            RectTransform imageRect = imageGo.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            Image image = imageGo.GetComponent<Image>();
            Sprite sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            // Keep the PNG itself at its authored ratio even when another
            // UI scale setting changes the parent button's rectangle.
            var fitter = imageGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture.width / (float)texture.height;
            var oldBlue = button.GetComponent<RokasBedWatchFocusButton>();
            if (oldBlue != null) oldBlue.enabled = false;
            var feedback = button.gameObject.AddComponent<RokasBedAuthoredActionButton>();
            feedback.Initialize(button, image, sprite);
            return true;
        }

        private void Initialize(Button button, Image view, Sprite sprite)
        {
            action = button;
            authored = view;
            ownedSprite = sprite;
            rect = button.transform as RectTransform;
            restPosition = rect.anchoredPosition;
            restScale = rect.localScale;
        }

        public void OnPointerEnter(PointerEventData e) { hovering = true; }
        public void OnPointerExit(PointerEventData e)
        { hovering = false; pressing = false; }
        public void OnPointerDown(PointerEventData e)
        { if (e.button == PointerEventData.InputButton.Left) pressing = true; }
        public void OnPointerUp(PointerEventData e) { pressing = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e)
        { selected = false; pressing = false; }

        private void Update()
        {
            if (rect == null || authored == null || action == null) return;
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, .08f);
            float k = 1f - Mathf.Exp(-13f * dt);
            bool active = action.IsInteractable() && (hovering || selected);
            blend = Mathf.Lerp(blend, active ? 1f : 0f, k);
            float pressScale = pressing ? .975f : 1f;
            // Visibly readable gold PNG hover like the laptop buttons:
            // 8% lift/scale, restrained authored-glow emphasis.
            float scale = Mathf.Lerp(1f, 1.082f, blend) * pressScale;
            rect.localScale = Vector3.Lerp(rect.localScale,
                restScale * scale, k);
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition,
                restPosition + new Vector2(0f, Mathf.Lerp(0f, 4f, blend)), k);
            // The authored PNG already has an illuminated gold border;
            // preserve it rather than layering another artificial glow.
            float idle = .93f + .055f * Mathf.Sin(Time.unscaledTime * 2f);
            float selectedPulse = .98f +
                .02f * Mathf.Sin(Time.unscaledTime * 4.5f);
            float alpha = Mathf.Lerp(idle, selectedPulse, blend);
            authored.color = new Color(1f, 1f, 1f,
                Mathf.Clamp01(alpha * (pressing ? .89f : 1f)));
        }

        private void OnDisable()
        {
            hovering = selected = pressing = false;
            blend = 0f;
            if (rect != null)
            {
                rect.localScale = restScale;
                rect.anchoredPosition = restPosition;
            }
        }

        private void OnDestroy()
        {
            if (ownedSprite != null) Destroy(ownedSprite);
        }
    }
}
