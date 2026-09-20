using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class LaptopTileFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform tile;
        private CanvasGroup ring;
        private Button button;
        private bool hovered;
        private bool selected;
        private bool pressed;

        public void Initialize(RectTransform target, CanvasGroup highlight)
        {
            tile = target;
            ring = highlight;
            button = GetComponent<Button>();
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }

        private void Update()
        {
            if (!tile || !ring || !button) return;
            bool active = button.IsInteractable() && (hovered || selected);
            float factor = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
            float size = active ? (pressed ? .97f : 1.035f) : 1;
            tile.localScale = Vector3.Lerp(tile.localScale, new Vector3(size, size, 1), factor);
            ring.alpha = Mathf.Lerp(ring.alpha, active ? (selected ? .85f : .45f) : 0, factor);
        }

        private void OnDisable()
        {
            hovered = selected = pressed = false;
            if (tile) tile.localScale = Vector3.one;
            if (ring) ring.alpha = 0;
        }
    }
}
